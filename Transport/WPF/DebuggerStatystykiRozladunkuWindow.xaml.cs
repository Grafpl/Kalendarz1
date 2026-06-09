// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/DebuggerStatystykiRozladunkuWindow.xaml.cs
//
// Debugger uczenia czasu rozładunku. Trzy zakładki:
//   1. Inwentaryzacja — co jest w bazach (LibraNet, TransportPL, Sage)
//   2. Test 1 dnia z Webfleet — surowe tracks + analiza postojów
//   3. Drill konkretnego klienta — czy pojazdy były blisko, czemu nie wizyta
//
// Cel: każdy raport tekstowy ma być KONKRETNY (liczby, przykłady, top-N),
// kopiowalny i wklejany w czacie z Claude.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Kalendarz1.Transport.Services;

namespace Kalendarz1.Transport.WPF
{
    public partial class DebuggerStatystykiRozladunkuWindow : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private readonly HistoriaRozladunkuService _svc = new();

        // Cache nazw klientów (Id → skrót Sage)
        private Dictionary<int, string> _nazwyKlientow = new();
        // Lista pojazdów (zmapowane do Webfleet) + nazwa "REJ • Marka Model"
        private List<(int PojazdID, string ObjectNo, string Opis)> _pojazdy = new();
        // Klienci z GPS
        private Dictionary<int, (double Lat, double Lon)> _klienciGps = new();

        // Krótka nazwa pojazdu po ObjectNo do raportów
        private string OpisPojazdu(string objectNo)
        {
            var p = _pojazdy.FirstOrDefault(x => x.ObjectNo == objectNo);
            return p.ObjectNo == null ? objectNo : $"{p.ObjectNo}/{p.Opis}";
        }

        // Krótka nazwa klienta + ID
        private string OpisKlienta(int klientId)
        {
            return _nazwyKlientow.TryGetValue(klientId, out var n) && !string.IsNullOrWhiteSpace(n)
                ? $"[{klientId}] {n}"
                : $"[{klientId}] (brak w Sage)";
        }

        public DebuggerStatystykiRozladunkuWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujListyAsync();
            DtData1.SelectedDate = DateTime.Today.AddDays(-1);
        }

        // ════════════════════════════════════════════════════════════════════
        // ŁADOWANIE LIST W TLE (combo poj + klient)
        // ════════════════════════════════════════════════════════════════════
        private async Task ZaladujListyAsync()
        {
            try
            {
                TxtStatus.Text = "Ładuję listy pojazdów i klientów…";
                var raw = await _svc.PobierzZmapowanePojazdyAsync();
                var opisy = await PobierzOpisyPojazdowAsync(raw.Select(p => p.PojazdID).ToList());
                _pojazdy = raw
                    .Select(p => (p.PojazdID, p.ObjectNo,
                                  opisy.TryGetValue(p.PojazdID, out var op) ? op : "(brak danych)"))
                    .OrderBy(p => p.ObjectNo)
                    .ToList();

                _klienciGps = await _svc.PobierzKlientowZGpsAsync();
                _nazwyKlientow = await PobierzNazwyZSageAsync(_klienciGps.Keys.ToList());

                CmbPojazd1.ItemsSource = _pojazdy.Select(p => $"{p.ObjectNo} • {p.Opis}").ToList();
                if (_pojazdy.Count > 0) CmbPojazd1.SelectedIndex = 0;

                var listaKlientow = _klienciGps.Keys
                    .Select(id => new
                    {
                        Id = id,
                        Nazwa = _nazwyKlientow.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n)
                                ? $"[{id}] {n}" : $"[{id}] (brak w Sage)"
                    })
                    .OrderBy(x => x.Nazwa)
                    .ToList();
                CmbKlient.ItemsSource = listaKlientow;
                CmbKlient.DisplayMemberPath = "Nazwa";
                CmbKlient.SelectedValuePath = "Id";

                // Combo zakresu (Zakładka 4)
                if (DtOd4 != null) DtOd4.SelectedDate = DateTime.Today.AddDays(-2);
                if (DtDo4 != null) DtDo4.SelectedDate = DateTime.Today.AddDays(-1);

                int znalezionoNazwy = _nazwyKlientow.Count(kv => !string.IsNullOrWhiteSpace(kv.Value));
                TxtStatus.Text = $"Gotowy. Pojazdy: {_pojazdy.Count} • Klienci GPS: {_klienciGps.Count} • Nazw z Sage: {znalezionoNazwy}/{_klienciGps.Count}.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Błąd ładowania: {ex.Message}";
            }
        }

        // Pobiera Marka/Model/Rejestracja z TransportPL.Pojazd dla listy ID
        private async Task<Dictionary<int, string>> PobierzOpisyPojazdowAsync(List<int> pojazdIds)
        {
            var wynik = new Dictionary<int, string>();
            if (pojazdIds.Count == 0) return wynik;
            try
            {
                await using var cn = new SqlConnection(ConnTransport);
                await cn.OpenAsync();
                using var cmd = cn.CreateCommand();
                var p = new List<string>();
                for (int i = 0; i < pojazdIds.Count; i++)
                {
                    var name = $"@id{i}";
                    p.Add(name);
                    cmd.Parameters.AddWithValue(name, pojazdIds[i]);
                }
                cmd.CommandText = $@"
                    SELECT PojazdID,
                           ISNULL(Rejestracja, '') AS Rejestracja,
                           ISNULL(Marka, '')       AS Marka,
                           ISNULL(Model, '')       AS Model
                    FROM dbo.Pojazd
                    WHERE PojazdID IN ({string.Join(",", p)})";
                cmd.CommandTimeout = 20;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string rej = rd.GetString(1).Trim();
                    string marka = rd.GetString(2).Trim();
                    string model = rd.GetString(3).Trim();
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(rej)) parts.Add(rej);
                    string mm = (marka + " " + model).Trim();
                    if (!string.IsNullOrEmpty(mm)) parts.Add(mm);
                    wynik[id] = parts.Count > 0 ? string.Join(" • ", parts) : "(bez nazwy)";
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[OpisyPojazdow] {ex.Message}"); }
            return wynik;
        }

        private async Task<Dictionary<int, string>> PobierzNazwyZSageAsync(List<int> idy)
        {
            var wynik = new Dictionary<int, string>();
            if (idy.Count == 0) return wynik;
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                using var cmd = cn.CreateCommand();
                var p = new List<string>();
                for (int i = 0; i < idy.Count; i++)
                {
                    var name = $"@id{i}";
                    p.Add(name);
                    cmd.Parameters.AddWithValue(name, idy[i]);
                }
                // Wzorzec z Customer360Service: ISNULL(Shortcut,'') + Name + NIP
                cmd.CommandText = $@"
                    SELECT Id,
                           ISNULL(Shortcut, '') AS Shortcut,
                           ISNULL(Name,     '') AS Name
                    FROM [SSCommon].[STContractors]
                    WHERE Id IN ({string.Join(",", p)})";
                cmd.CommandTimeout = 30;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string shortcut = rd.GetString(1).Trim();
                    string name = rd.GetString(2).Trim();
                    // Priorytet: shortcut > name (skrót zwykle jest, name to pełna nazwa firmy)
                    string disp = !string.IsNullOrEmpty(shortcut) ? shortcut
                                : !string.IsNullOrEmpty(name)     ? name
                                : "";
                    wynik[id] = disp;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NazwyKlientow] {ex.Message}"); }
            return wynik;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA 1: INWENTARYZACJA
        // ════════════════════════════════════════════════════════════════════
        private async void BtnInwent_Click(object sender, RoutedEventArgs e)
        {
            BtnInwent.IsEnabled = false;
            TxtStatus.Text = "Liczę…";
            try { TxtInwent.Text = await ZbierzInwentaryzacjeAsync(); }
            catch (Exception ex) { TxtInwent.Text = $"BŁĄD: {ex.Message}\n\n{ex.StackTrace}"; }
            finally { BtnInwent.IsEnabled = true; TxtStatus.Text = "Inwentaryzacja gotowa."; }
        }

        private async Task<string> ZbierzInwentaryzacjeAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  INWENTARYZACJA — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // ── KartotekaOdbiorcyDane (LibraNet) ──
            sb.AppendLine("┌─ LibraNet.dbo.KartotekaOdbiorcyDane ───────────────────────────────┐");
            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();
                long total = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane");
                long zGps = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL");
                long zCzasem = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane WHERE CzasRozladunkuMin IS NOT NULL");
                long gpsBezCzasu = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane WHERE Latitude IS NOT NULL AND CzasRozladunkuMin IS NULL");
                sb.AppendLine($"│ Wszystkich klientów w karcie:                       {total,8}        │");
                sb.AppendLine($"│ Z geolokalizacją (Lat+Lon):                         {zGps,8}        │");
                sb.AppendLine($"│ Z CzasRozladunkuMin (oficjalny czas):               {zCzasem,8}        │");
                sb.AppendLine($"│ Z GPS ale BEZ ustawionego czasu (kandydaci):        {gpsBezCzasu,8}        │");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── EstymacjeRozladunku ──
            sb.AppendLine("┌─ LibraNet.dbo.EstymacjeRozladunku (uczone z Webfleet) ─────────────┐");
            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();
                long erTotal = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.EstymacjeRozladunku");
                long erWiarygodne = await ScalarAsync(cn, $"SELECT COUNT(*) FROM dbo.EstymacjeRozladunku WHERE LiczbaProb >= {HistoriaRozladunkuService.MinProbDoZaufania}");
                long erMalo = await ScalarAsync(cn, $"SELECT COUNT(*) FROM dbo.EstymacjeRozladunku WHERE LiczbaProb < {HistoriaRozladunkuService.MinProbDoZaufania}");
                var ostatniRefresh = await ScalarStrAsync(cn, "SELECT CONVERT(VARCHAR, MAX(OstatniRefresh), 120) FROM dbo.EstymacjeRozladunku");
                sb.AppendLine($"│ Wpisów łącznie:                                     {erTotal,8}        │");
                sb.AppendLine($"│   • Wiarygodnych (≥{HistoriaRozladunkuService.MinProbDoZaufania} wizyt):                          {erWiarygodne,8}        │");
                sb.AppendLine($"│   • Za mało prób (<{HistoriaRozladunkuService.MinProbDoZaufania} wizyt):                          {erMalo,8}        │");
                sb.AppendLine($"│ Ostatni refresh którejkolwiek estymacji: {ostatniRefresh,-25}   │");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── EstymacjeTras ──
            sb.AppendLine("┌─ LibraNet.dbo.EstymacjeTras (B2 — czas jazdy A↔B) ─────────────────┐");
            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();
                long etTotal = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.EstymacjeTras");
                long etZBazy = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.EstymacjeTras WHERE LokalizacjaA = 0");
                long etDoBazy = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.EstymacjeTras WHERE LokalizacjaB = 0");
                sb.AppendLine($"│ Odcinków A→B łącznie:                               {etTotal,8}        │");
                sb.AppendLine($"│   • Z bazy Koziołki (LokA=0):                       {etZBazy,8}        │");
                sb.AppendLine($"│   • Do bazy Koziołki (LokB=0):                      {etDoBazy,8}        │");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── WebfleetVehicleMapping (TransportPL) ──
            sb.AppendLine("┌─ TransportPL.dbo.WebfleetVehicleMapping (mapowanie pojazd↔Webfleet) ┐");
            await using (var cn = new SqlConnection(ConnTransport))
            {
                await cn.OpenAsync();
                long wvmTotal = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.WebfleetVehicleMapping");
                long wvmObjNo = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.WebfleetVehicleMapping WHERE WebfleetObjectNo IS NOT NULL AND LTRIM(RTRIM(WebfleetObjectNo))<>''");
                long pojTotal = await ScalarAsync(cn, "SELECT COUNT(*) FROM dbo.Pojazd");
                sb.AppendLine($"│ Pojazdów w TransportPL.Pojazd:                      {pojTotal,8}        │");
                sb.AppendLine($"│ Wpisów w WebfleetVehicleMapping:                    {wvmTotal,8}        │");
                sb.AppendLine($"│ Z poprawnym WebfleetObjectNo:                       {wvmObjNo,8}        │");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── Lista zmapowanych pojazdów ──
            sb.AppendLine("┌─ ZMAPOWANE POJAZDY (te będą pobierane z Webfleet) ─────────────────┐");
            foreach (var p in _pojazdy.OrderBy(x => x.ObjectNo))
                sb.AppendLine($"│   ObjectNo='{p.ObjectNo,-6}'  ID={p.PojazdID,5}  →  {p.Opis}");
            if (_pojazdy.Count == 0)
                sb.AppendLine("│   ❌ ŻADNYCH POJAZDÓW. Mapowanie WebfleetVehicleMapping puste!");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── Lista klientów z GPS ──
            sb.AppendLine("┌─ KLIENCI Z GEOLOKALIZACJĄ (z nazwami z Sage) ──────────────────────┐");
            foreach (var id in _klienciGps.Keys.OrderBy(x => x))
                sb.AppendLine($"│   {OpisKlienta(id)}");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── Klienci BEZ GPS — kandydaci do geokodowania (z liczbą ostatnich zamówień) ──
            sb.AppendLine("┌─ KLIENCI BEZ GEOLOKALIZACJI — DO GEOKODOWANIA (top 30 wg zamówień) ┐");
            try
            {
                var lista = new List<(int Id, int Zamowien, DateTime? Ostatnie)>();
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT kod.IdSymfonia,
                           COUNT(zm.Id) AS LiczbaZamowien,
                           MAX(zm.DataPrzyjazdu) AS OstatnieZam
                    FROM dbo.KartotekaOdbiorcyDane kod
                    LEFT JOIN dbo.ZamowieniaMieso zm ON zm.KlientId = kod.IdSymfonia
                         AND zm.DataPrzyjazdu >= DATEADD(MONTH, -6, CAST(GETDATE() AS DATE))
                         AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                    WHERE kod.Latitude IS NULL OR kod.Longitude IS NULL
                    GROUP BY kod.IdSymfonia
                    ORDER BY COUNT(zm.Id) DESC", cn);
                cmd.CommandTimeout = 60;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    int zam = rd.GetInt32(1);
                    DateTime? last = rd.IsDBNull(2) ? null : rd.GetDateTime(2);
                    lista.Add((id, zam, last));
                }

                // Doczytaj nazwy dla tych klientów
                var idyBezGps = lista.Select(x => x.Id).Take(30).ToList();
                var nazwyBezGps = await PobierzNazwyZSageAsync(idyBezGps);

                sb.AppendLine($"│ Łącznie klientów bez GPS: {lista.Count,3}                                      │");
                sb.AppendLine($"│                                                                    │");
                sb.AppendLine($"│   {"ID",-7} {"Zam. 6mc",-9} {"Ost. zam.",-12} Nazwa");
                sb.AppendLine("│   " + new string('─', 65));
                foreach (var (id, zam, last) in lista.Take(30))
                {
                    string nazwa = nazwyBezGps.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : "(brak w Sage)";
                    if (nazwa.Length > 40) nazwa = nazwa.Substring(0, 40);
                    string lastStr = last?.ToString("yyyy-MM-dd") ?? "—";
                    sb.AppendLine($"│   {id,-7} {zam,-9} {lastStr,-12} {nazwa}");
                }
                if (lista.Count > 30)
                    sb.AppendLine($"│   … (+{lista.Count - 30} więcej — przewiń w Mapie Klientów)");
                sb.AppendLine($"│                                                                    │");
                sb.AppendLine($"│ DZIAŁANIE: otwórz Mapę Klientów → '📍 Geokoduj adresy'              │");
                sb.AppendLine($"│ Tylko po geokodowaniu Webfleet GPS wykryje wizyty u tych klientów. │");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"│ (Błąd odczytu: {ex.Message,-50} )│");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── Klienci z estymacją vs bez ──
            sb.AppendLine("┌─ KLIENCI Z GPS: wykryte wizyty vs brak ────────────────────────────┐");
            await using (var cn = new SqlConnection(ConnLibra))
            {
                await cn.OpenAsync();
                long bezEstymacji = await ScalarAsync(cn, @"
                    SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane kod
                    LEFT JOIN dbo.EstymacjeRozladunku er ON er.KlientId = kod.IdSymfonia
                    WHERE kod.Latitude IS NOT NULL AND er.KlientId IS NULL");
                long maloProb = await ScalarAsync(cn, $@"
                    SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane kod
                    JOIN dbo.EstymacjeRozladunku er ON er.KlientId = kod.IdSymfonia
                    WHERE er.LiczbaProb < {HistoriaRozladunkuService.MinProbDoZaufania}");
                long ok = await ScalarAsync(cn, $@"
                    SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane kod
                    JOIN dbo.EstymacjeRozladunku er ON er.KlientId = kod.IdSymfonia
                    WHERE er.LiczbaProb >= {HistoriaRozladunkuService.MinProbDoZaufania}");
                sb.AppendLine($"│ ✓ Z GPS + wiarygodna estymacja (≥{HistoriaRozladunkuService.MinProbDoZaufania} wizyt):       {ok,8}        │");
                sb.AppendLine($"│ ⏳ Z GPS + estymacja, ale za mało wizyt:           {maloProb,8}        │");
                sb.AppendLine($"│ ❌ Z GPS + brak ANY wizyt (cel debugowania):       {bezEstymacji,8}        │");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── PING WEBFLEET ──
            sb.AppendLine("┌─ TEST WEBFLEET API (1 zapytanie pingowe) ─────────────────────────┐");
            if (_pojazdy.Count > 0)
            {
                var testPojazd = _pojazdy[0];
                var testData = DateTime.Today.AddDays(-1);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var tracks = await _svc.PobierzTracksAsync(testPojazd.ObjectNo, testData);
                    sw.Stop();
                    sb.AppendLine($"│ ✓ Połączenie OK — odpowiedź {sw.ElapsedMilliseconds} ms                     │");
                    sb.AppendLine($"│   Test: {testPojazd.ObjectNo} ({testPojazd.Opis,-30}) z {testData:yyyy-MM-dd}   │");
                    sb.AppendLine($"│   Punktów GPS: {tracks.Count,5}                                       │");
                    if (tracks.Count == 0)
                        sb.AppendLine($"│   ⚠️ Brak tracków — może być weekend albo pojazd nie jeździł.      │");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    sb.AppendLine($"│ ❌ BŁĄD ({sw.ElapsedMilliseconds} ms): {ex.Message,-50} │");
                    sb.AppendLine($"│   Sprawdź: WebfleetHttp credentials, internet, dostęp showTracks. │");
                }
                var (interval, cacheSize, locked) = HistoriaRozladunkuService.GetThrottleStats();
                sb.AppendLine($"│   Stan: interwał {interval} ms • cache {cacheSize} wpisów • quota lock: {(locked ? "TAK" : "nie")}     │");
            }
            else
            {
                sb.AppendLine("│ ⚠️ Pominięto — brak zmapowanych pojazdów.                          │");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── Parametry algorytmu ──
            sb.AppendLine("┌─ AKTYWNE PARAMETRY ALGORYTMU ──────────────────────────────────────┐");
            sb.AppendLine($"│ PromienKlientaKm  = {HistoriaRozladunkuService.PromienKlientaKm}    (max odległość pojazd↔klient)         │");
            sb.AppendLine($"│ PromienBazyKm     = {HistoriaRozladunkuService.PromienBazyKm}    (max odległość pojazd↔baza Koziołki)   │");
            sb.AppendLine($"│ ProgStoiKmh       = {HistoriaRozladunkuService.ProgStoiKmh}      (<= tej predkosci pojazd stoi)         │");
            sb.AppendLine($"│ MinWizytaMin      = {HistoriaRozladunkuService.MinWizytaMin}      (min czas postoju zeby uznac za wizyte) │");
            sb.AppendLine($"│ MaxWizytaMin      = {HistoriaRozladunkuService.MaxWizytaMin}    (max czas wizyty - dluzej = pauza)      │");
            sb.AppendLine($"│ RoboczeStart/End  = {HistoriaRozladunkuService.RoboczeStart:00}:00 / {HistoriaRozladunkuService.RoboczeKoniec:00}:00 (poza = nocleg/pauza)       │");
            sb.AppendLine($"│ MinProbDoZaufania = {HistoriaRozladunkuService.MinProbDoZaufania}      (min wizyt aby estymacja byla pewna)    │");
            sb.AppendLine($"│ MaxMergePrzerwaMin= {HistoriaRozladunkuService.MaxMergePrzerwaMin}     (scal sasiad. wizyty u tego samego kl.) │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");

            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA 2: TEST 1 DNIA Z WEBFLEET
        // ════════════════════════════════════════════════════════════════════
        private async void BtnTestDzien_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPojazd1.SelectedIndex < 0 || !DtData1.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz pojazd i datę.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var (pojazdId, objectNo, _) = _pojazdy[CmbPojazd1.SelectedIndex];
            var data = DtData1.SelectedDate.Value;

            BtnTestDzien.IsEnabled = false;
            TxtStatus.Text = $"Pobieram tracks dla {objectNo} z {data:yyyy-MM-dd}…";
            try { TxtTestDzien.Text = await TestDzienAsync(pojazdId, objectNo, data); }
            catch (Exception ex) { TxtTestDzien.Text = $"BŁĄD: {ex.Message}\n\n{ex.StackTrace}"; }
            finally { BtnTestDzien.IsEnabled = true; TxtStatus.Text = "Gotowe."; }
        }

        private async Task<string> TestDzienAsync(int pojazdId, string objectNo, DateTime data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  TEST 1 DNIA — {OpisPojazdu(objectNo)}, {data:yyyy-MM-dd}");
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // 1. Tracks
            List<HistoriaRozladunkuService.TrackPoint> tracks;
            try { tracks = await _svc.PobierzTracksAsync(objectNo, data); }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Webfleet API błąd: {ex.Message}");
                sb.AppendLine("   Sprawdź: poświadczenia (account/user/pass), dostęp do showTracks.");
                return sb.ToString();
            }

            sb.AppendLine($"📡 WEBFLEET — pobrano {tracks.Count} trackpointów");
            if (tracks.Count == 0)
            {
                sb.AppendLine("   ⚠️ ZERO punktów. Możliwe powody:");
                sb.AppendLine("      - pojazd nie jeździł w tym dniu (weekend, postój)");
                sb.AppendLine("      - ObjectNo nieprawidłowy / pojazd niezarejestrowany w Webfleet");
                sb.AppendLine("      - brak uprawnień konta API do showTracks");
                return sb.ToString();
            }
            var pierwszy = tracks.First();
            var ostatni = tracks.Last();
            sb.AppendLine($"   Pierwszy: {pierwszy.Time:HH:mm:ss}  speed={pierwszy.Speed} km/h  lat={pierwszy.Lat:F5}, lon={pierwszy.Lon:F5}");
            sb.AppendLine($"   Ostatni : {ostatni.Time:HH:mm:ss}  speed={ostatni.Speed} km/h  lat={ostatni.Lat:F5}, lon={ostatni.Lon:F5}");
            sb.AppendLine();

            // 2. Histogram speed
            int spd0 = tracks.Count(t => t.Speed == 0);
            int spdSt = tracks.Count(t => t.Speed > 0 && t.Speed <= HistoriaRozladunkuService.ProgStoiKmh);
            int spdJ = tracks.Count(t => t.Speed > HistoriaRozladunkuService.ProgStoiKmh);
            sb.AppendLine($"📊 HISTOGRAM SPEED:");
            sb.AppendLine($"   = 0 km/h        : {spd0,5}  ({100.0 * spd0 / tracks.Count:F1}%) — stoi");
            sb.AppendLine($"   1..{HistoriaRozladunkuService.ProgStoiKmh,2} km/h     : {spdSt,5}  ({100.0 * spdSt / tracks.Count:F1}%) — prawie stoi");
            sb.AppendLine($"   > {HistoriaRozladunkuService.ProgStoiKmh,2} km/h     : {spdJ,5}  ({100.0 * spdJ / tracks.Count:F1}%) — jedzie");
            sb.AppendLine();

            // 3. Wykryj wizyty — WykryjWizyty już zwraca wynik PO mergu.
            // Robimy też wersję PRZED mergem (bez ScalSasiadujace) żeby user widział co algorytm scalił.
            var wizytyPoMerg = HistoriaRozladunkuService.WykryjWizyty(tracks, _klienciGps);
            // Surowy run = wynik z fragmentami (scalSasiadujace: false)
            var wizytySurowe = HistoriaRozladunkuService.WykryjWizyty(tracks, _klienciGps, scalSasiadujace: false);
            int przedMerg = wizytySurowe.Count;
            int poMerg = wizytyPoMerg.Count;

            sb.AppendLine($"✓ WYKRYTE WIZYTY: {poMerg} (przed scaleniem fragmentów: {przedMerg})");
            if (przedMerg != poMerg)
                sb.AppendLine($"   ℹ️ Scalono {przedMerg - poMerg} fragmentów GPS u tego samego klienta (manewry na placu).");
            foreach (var w in wizytyPoMerg)
            {
                sb.AppendLine($"   • {w.Start:HH:mm}–{w.Koniec:HH:mm} ({w.Minuty,3} min)  →  {OpisKlienta(w.KlientId)}");
            }
            // Pokaż surowe wizyty jeśli scalono — żeby było widać CO zostało scalone
            if (przedMerg != poMerg)
            {
                sb.AppendLine();
                sb.AppendLine($"   Surowe fragmenty PRZED scaleniem ({przedMerg}):");
                foreach (var w in wizytySurowe)
                    sb.AppendLine($"     · {w.Start:HH:mm}–{w.Koniec:HH:mm} ({w.Minuty,3} min)  →  [{w.KlientId}]");
            }
            var wizyty = wizytyPoMerg;
            if (wizyty.Count == 0)
            {
                sb.AppendLine("   (żadnych wizyt — pojazd nie zatrzymał się <3,5 km od klienta z GPS)");
            }
            sb.AppendLine();

            // 4. Top 5 najdłuższe BLOKI POSTOJU (≤5 km/h) i ich najbliższy klient
            sb.AppendLine("🅿️ TOP 10 NAJDŁUŻSZE BLOKI POSTOJU (speed≤5 km/h) — z najbliższym klientem:");
            var bloki = WykryjBlokiPostoju(tracks);
            int i = 0;
            foreach (var b in bloki.OrderByDescending(x => x.Minuty).Take(10))
            {
                i++;
                // Środek bloku
                double midLat = (b.LatA + b.LatB) / 2;
                double midLon = (b.LonA + b.LonB) / 2;
                var (klId, klDist) = NajblizszyKlient(midLat, midLon);
                string flaga = klId > 0 && klDist <= HistoriaRozladunkuService.PromienKlientaKm ? "✓ W ZASIĘGU" : "  poza";
                string klInfo = klId > 0
                    ? $"{flaga}  najbliższy: {OpisKlienta(klId)}  ({klDist:F2} km)"
                    : "  brak żadnego klienta w pobliżu";
                sb.AppendLine($"   {i,2}.  {b.Start:HH:mm}–{b.Koniec:HH:mm} ({b.Minuty,3} min)  lat={midLat:F5} lon={midLon:F5}");
                sb.AppendLine($"        {klInfo}");
            }
            if (bloki.Count == 0) sb.AppendLine("   (żadnych bloków postoju ≥1 min)");
            sb.AppendLine();

            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  WNIOSKI: tracks {tracks.Count} → bloki postoju {bloki.Count} → wizyty {wizyty.Count}");
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA 3: DRILL KLIENTA
        // ════════════════════════════════════════════════════════════════════
        private async void BtnDrill_Click(object sender, RoutedEventArgs e)
        {
            if (CmbKlient.SelectedValue is not int klientId)
            {
                MessageBox.Show("Wybierz klienta z listy.", "Brak klienta", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int dni = 30;
            if (CmbDniWstecz.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Content?.ToString(), out var d)) dni = d;

            if (_pojazdy.Count == 0)
            {
                MessageBox.Show("Brak zmapowanych pojazdów (WebfleetVehicleMapping pusty).", "Brak pojazdów", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnDrill.IsEnabled = false;
            try
            {
                TxtDrill.Text = $"⏳ Pobieram tracks dla {_pojazdy.Count} pojazdów × {dni} dni = {_pojazdy.Count * dni} zapytań…\n" +
                                "   Może potrwać 1-5 min. Zaczekaj.";
                TxtDrill.Text = await DrillKlientaAsync(klientId, dni);
            }
            catch (Exception ex) { TxtDrill.Text = $"BŁĄD: {ex.Message}\n\n{ex.StackTrace}"; }
            finally { BtnDrill.IsEnabled = true; TxtStatus.Text = "Drill gotowy."; }
        }

        private async Task<string> DrillKlientaAsync(int klientId, int dni)
        {
            var sb = new StringBuilder();
            if (!_klienciGps.TryGetValue(klientId, out var gps))
            {
                sb.AppendLine($"❌ Klient {klientId} nie ma GPS w KartotekaOdbiorcyDane.");
                return sb.ToString();
            }
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  DRILL — Klient {OpisKlienta(klientId)}");
            sb.AppendLine($"  GPS karty: lat={gps.Lat:F6}, lon={gps.Lon:F6}");
            sb.AppendLine($"  Zakres: ostatnie {dni} dni × {_pojazdy.Count} pojazdów = {dni * _pojazdy.Count} zapytań do Webfleet");
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // Lista dni z zamówieniami do tego klienta — potem dopasujemy do wykrytych wizyt
            var dniZamowien = new HashSet<DateTime>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT DISTINCT CAST(DataPrzyjazdu AS DATE)
                    FROM dbo.ZamowieniaMieso
                    WHERE KlientId = @id AND DataPrzyjazdu >= DATEADD(DAY, -@dni, CAST(GETDATE() AS DATE))
                      AND ISNULL(Status, 'Nowe') NOT IN ('Anulowane')", cn);
                cmd.Parameters.AddWithValue("@id", klientId);
                cmd.Parameters.AddWithValue("@dni", dni);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    dniZamowien.Add(rd.GetDateTime(0).Date);
                sb.AppendLine($"📋 Zamówienia (LibraNet.ZamowieniaMieso, {dni} dni): {dniZamowien.Count} unikalnych dni");
                if (dniZamowien.Count > 0)
                {
                    sb.AppendLine($"   pierwsze: {dniZamowien.Min():yyyy-MM-dd}, ostatnie: {dniZamowien.Max():yyyy-MM-dd}");
                }
                else
                {
                    sb.AppendLine("   ⚠️ Brak zamówień = pojazdy w ogóle tu nie jeździły. To NORMALNE że brak wizyt.");
                }
                sb.AppendLine();
            }
            catch (Exception ex) { sb.AppendLine($"  (nie udało się odczytać ZamowieniaMieso: {ex.Message})"); sb.AppendLine(); }

            // Iteruj dni × pojazdy
            var podejscia = new List<(string ObjectNo, DateTime Czas, double Dist, int Speed)>();
            int przeszukanych = 0;
            var dataDo = DateTime.Today;
            for (int d = 0; d < dni; d++)
            {
                var data = dataDo.AddDays(-d);
                foreach (var (_, objectNo, _) in _pojazdy)
                {
                    przeszukanych++;
                    TxtStatus.Text = $"Drill: pojazd {objectNo}, dzień {data:yyyy-MM-dd} ({przeszukanych}/{dni * _pojazdy.Count})";
                    try
                    {
                        var tracks = await _svc.PobierzTracksAsync(objectNo, data);
                        foreach (var t in tracks)
                        {
                            double dist = HistoriaRozladunkuService.HaversineKm(t.Lat, t.Lon, gps.Lat, gps.Lon);
                            if (dist <= 10.0)   // bierzemy szeroko, żeby pokazać też te "blisko ale nie wystarczająco"
                                podejscia.Add((objectNo, t.Time, dist, t.Speed));
                        }
                    }
                    catch { /* pomijaj błędne dni */ }
                }
            }

            sb.AppendLine($"📡 Sprawdzonych zapytań do Webfleet: {przeszukanych}");
            sb.AppendLine($"📍 Punktów GPS w promieniu ≤10 km od klienta: {podejscia.Count}");
            sb.AppendLine();

            if (podejscia.Count == 0)
            {
                sb.AppendLine("❌ ŻADEN pojazd nigdy nie był bliżej niż 10 km od tego klienta.");
                sb.AppendLine("   Możliwe przyczyny:");
                sb.AppendLine("   1. Nie jeździliśmy tam w ostatnich " + dni + " dni — rozszerz zakres.");
                sb.AppendLine("   2. Zły GPS na karcie klienta (Latitude/Longitude wskazują złe miejsce).");
                sb.AppendLine("   3. Pojazdy obsługujące tego klienta NIE są zmapowane w WebfleetVehicleMapping.");
                return sb.ToString();
            }

            // Grupuj per pojazd × dzień
            var grupy = podejscia
                .GroupBy(p => new { p.ObjectNo, Dzien = p.Czas.Date })
                .Select(g => new
                {
                    g.Key.ObjectNo,
                    g.Key.Dzien,
                    Punktow = g.Count(),
                    MinDist = g.Min(x => x.Dist),
                    PostojeStojac = g.Count(x => x.Speed <= HistoriaRozladunkuService.ProgStoiKmh && x.Dist <= HistoriaRozladunkuService.PromienKlientaKm),
                    PierwszyBlisko = g.Where(x => x.Dist <= HistoriaRozladunkuService.PromienKlientaKm).OrderBy(x => x.Czas).FirstOrDefault(),
                    OstatniBlisko = g.Where(x => x.Dist <= HistoriaRozladunkuService.PromienKlientaKm).OrderByDescending(x => x.Czas).FirstOrDefault()
                })
                .OrderByDescending(x => x.PostojeStojac)
                .ThenBy(x => x.MinDist)
                .ToList();

            sb.AppendLine($"📅 PODEJŚCIA per pojazd × dzień (top 30):");
            string nagStojac = $"Stojąc<{HistoriaRozladunkuService.PromienKlientaKm}km";
            sb.AppendLine($"   {"#",-3} {"Pojazd",-30} {"Data",-12} {"Punktów",-9} {"Min dist",-10} {nagStojac,-15} Status");
            sb.AppendLine("   " + new string('─', 125));
            int idx = 0;
            foreach (var g in grupy.Take(30))
            {
                idx++;
                string opisP = OpisPojazdu(g.ObjectNo);
                if (opisP.Length > 28) opisP = opisP.Substring(0, 28);
                string status;
                if (g.PostojeStojac == 0)
                    status = $"przejazd (najbliżej {g.MinDist:F2} km)";
                else
                {
                    int minutNaPostoju = (int)((g.OstatniBlisko.Czas - g.PierwszyBlisko.Czas).TotalMinutes);
                    string flaga = "";
                    if (minutNaPostoju < HistoriaRozladunkuService.MinWizytaMin) flaga = $" ❌ za krótki ({minutNaPostoju} min < {HistoriaRozladunkuService.MinWizytaMin})";
                    else if (minutNaPostoju > HistoriaRozladunkuService.MaxWizytaMin) flaga = $" ❌ za długi ({minutNaPostoju} min > {HistoriaRozladunkuService.MaxWizytaMin})";
                    else if (g.PierwszyBlisko.Czas.Hour < HistoriaRozladunkuService.RoboczeStart ||
                             g.PierwszyBlisko.Czas.Hour >= HistoriaRozladunkuService.RoboczeKoniec)
                        flaga = $" ❌ poza godz. roboczymi (start {g.PierwszyBlisko.Czas:HH:mm})";
                    else flaga = $" ✓ wizyta {minutNaPostoju} min";
                    status = $"postój {g.PierwszyBlisko.Czas:HH:mm}-{g.OstatniBlisko.Czas:HH:mm}{flaga}";
                }
                sb.AppendLine($"   {idx,-3} {opisP,-30} {g.Dzien:yyyy-MM-dd}  {g.Punktow,-9} {g.MinDist:F2} km    {g.PostojeStojac,-12} {status}");
            }
            sb.AppendLine();

            // Podsumowanie powodów
            int blizejNizPromien = grupy.Count(g => g.MinDist <= HistoriaRozladunkuService.PromienKlientaKm);
            int zPostojem = grupy.Count(g => g.PostojeStojac > 0);
            sb.AppendLine("📊 PODSUMOWANIE:");
            sb.AppendLine($"   • Dni z jakimkolwiek podejściem ≤10 km:                   {grupy.Count}");
            sb.AppendLine($"   • Dni gdy pojazd był ≤{HistoriaRozladunkuService.PromienKlientaKm} km od klienta:                {blizejNizPromien}");
            sb.AppendLine($"   • Dni z postojem (speed≤{HistoriaRozladunkuService.ProgStoiKmh}) w zasięgu:                  {zPostojem}");

            if (grupy.Count > 0 && blizejNizPromien == 0)
            {
                double minZawsze = grupy.Min(g => g.MinDist);
                sb.AppendLine();
                sb.AppendLine($"⚠️ Pojazdy podjeżdżały najbliżej {minZawsze:F2} km — POZA promień {HistoriaRozladunkuService.PromienKlientaKm} km.");
                sb.AppendLine("   PRAWDOPODOBNIE: GPS karty wskazuje błędne miejsce (siedziba zamiast magazynu).");
                sb.AppendLine("   ROZWIĄZANIE: popraw lat/lon klienta w Mapie Klientów albo zwiększ PromienKlientaKm w kodzie.");
            }
            if (blizejNizPromien > 0 && zPostojem == 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠️ Pojazdy były ≤{HistoriaRozladunkuService.PromienKlientaKm} km ale tylko przejeżdżały. Nigdy się nie zatrzymały.");
                sb.AppendLine("   PRAWDOPODOBNIE: rozładunek odbywa się dalej (np. brama z innej strony).");
            }

            // ── ZAMÓWIENIE ↔ WIZYTA — porównanie ──
            if (dniZamowien.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"🔗 ZAMÓWIENIA vs WIZYTY (czy w dniu zamówienia był pojazd):");
                sb.AppendLine($"   {"Data zam.",-12} Status");
                sb.AppendLine("   " + new string('─', 100));
                int zWizyta = 0, zPostojeBezWizyty = 0, zPrzejazdami = 0, bezGps = 0;
                foreach (var dz in dniZamowien.OrderByDescending(x => x))
                {
                    var grupyTegoDnia = grupy.Where(g => g.Dzien == dz).ToList();
                    if (grupyTegoDnia.Count == 0)
                    {
                        sb.AppendLine($"   {dz:yyyy-MM-dd}  ❌ brak punktów GPS żadnego pojazdu ≤10 km (mimo zamówienia!)");
                        bezGps++;
                    }
                    else if (grupyTegoDnia.Any(g => g.PostojeStojac > 0))
                    {
                        var g = grupyTegoDnia.First(x => x.PostojeStojac > 0);
                        int minPostoj = (int)((g.OstatniBlisko.Czas - g.PierwszyBlisko.Czas).TotalMinutes);
                        sb.AppendLine($"   {dz:yyyy-MM-dd}  ✓ wizyta wykryta: {OpisPojazdu(g.ObjectNo)} ({minPostoj} min postoju)");
                        zWizyta++;
                    }
                    else if (grupyTegoDnia.Any(g => g.MinDist <= HistoriaRozladunkuService.PromienKlientaKm))
                    {
                        var g = grupyTegoDnia.First(x => x.MinDist <= HistoriaRozladunkuService.PromienKlientaKm);
                        sb.AppendLine($"   {dz:yyyy-MM-dd}  ⚠️ pojazd ≤{HistoriaRozladunkuService.PromienKlientaKm}km ale bez postoju ≤5 km/h: {OpisPojazdu(g.ObjectNo)} (min {g.MinDist:F2} km)");
                        zPostojeBezWizyty++;
                    }
                    else
                    {
                        var g = grupyTegoDnia.OrderBy(x => x.MinDist).First();
                        sb.AppendLine($"   {dz:yyyy-MM-dd}  ↻ tylko przejazdy: {OpisPojazdu(g.ObjectNo)} (najbliżej {g.MinDist:F2} km, poza promień)");
                        zPrzejazdami++;
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"   📊 Dopasowanie: ✓ {zWizyta} wizyta • ⚠️ {zPostojeBezWizyty} blisko bez postoju • ↻ {zPrzejazdami} przejazdy • ❌ {bezGps} brak GPS");
                if (zWizyta == 0 && (zPrzejazdami > 0 || bezGps > 0))
                {
                    sb.AppendLine();
                    sb.AppendLine($"   ⚠️ Mimo {dniZamowien.Count} zamówień — ŻADNA wizyta wykryta.");
                    sb.AppendLine($"      Najczęściej: dostawcą był pojazd NIEZMAPOWANY w WebfleetVehicleMapping,");
                    sb.AppendLine($"      albo to klient typu 'wjazd przez bramę z innej strony' (GPS poza promień).");
                }
            }

            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA 4: SKAN ZAKRESU — wszystkie auta × wszystkie dni
        // ════════════════════════════════════════════════════════════════════
        private async void BtnSkanZakresu_Click(object sender, RoutedEventArgs e)
        {
            if (!DtOd4.SelectedDate.HasValue || !DtDo4.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat.", "Brak dat", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var od = DtOd4.SelectedDate.Value.Date;
            var doD = DtDo4.SelectedDate.Value.Date;
            if (doD < od) { (od, doD) = (doD, od); }
            int dni = (int)(doD - od).TotalDays + 1;
            if (dni > 90)
            {
                if (MessageBox.Show($"Zakres ma {dni} dni × {_pojazdy.Count} pojazdów = {dni * _pojazdy.Count} zapytań do Webfleet. To może zająć 10+ min. Kontynuować?",
                    "Duży zakres", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }

            BtnSkanZakresu.IsEnabled = false;
            try
            {
                TxtSkan.Text = $"⏳ Skanuję {dni} dni × {_pojazdy.Count} pojazdów = {dni * _pojazdy.Count} zapytań…";
                TxtSkan.Text = await SkanZakresuAsync(od, doD);
            }
            catch (Exception ex) { TxtSkan.Text = $"BŁĄD: {ex.Message}\n\n{ex.StackTrace}"; }
            finally { BtnSkanZakresu.IsEnabled = true; TxtStatus.Text = "Skan zakresu gotowy."; }
        }

        private async Task<string> SkanZakresuAsync(DateTime od, DateTime doD)
        {
            var sb = new StringBuilder();
            int dni = (int)(doD - od).TotalDays + 1;
            var swTotal = System.Diagnostics.Stopwatch.StartNew();

            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  SKAN ZAKRESU — {od:yyyy-MM-dd} → {doD:yyyy-MM-dd} ({dni} dni)");
            sb.AppendLine($"  Pojazdów zmapowanych: {_pojazdy.Count} • Klientów z GPS: {_klienciGps.Count}");
            sb.AppendLine($"  Zapytań do Webfleet: {dni * _pojazdy.Count}  (throttling: max 3 jednoczesnych, 200ms między)");
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // pojazdId → dzien → (tracks count, wizyty, statusKategoria)
            var rezultat = new Dictionary<int, Dictionary<DateTime, DayResult>>();
            var perKlient = new Dictionary<int, List<(DateTime Dzien, string ObjectNo, int Minuty)>>();
            var bledy = new List<string>();
            int przetworzonych = 0;
            int total = dni * _pojazdy.Count;
            int sumaApiErrors = 0;

            for (int d = 0; d < dni; d++)
            {
                var data = od.AddDays(d);
                foreach (var (pojazdId, objectNo, _) in _pojazdy)
                {
                    przetworzonych++;
                    if (przetworzonych % 5 == 0)
                        TxtStatus.Text = $"Skan {przetworzonych}/{total} ({100 * przetworzonych / total}%) — {objectNo} {data:yyyy-MM-dd}";

                    if (!rezultat.TryGetValue(pojazdId, out var perDzien))
                        rezultat[pojazdId] = perDzien = new();

                    try
                    {
                        var tracks = await _svc.PobierzTracksAsync(objectNo, data);
                        var wizyty = HistoriaRozladunkuService.WykryjWizyty(tracks, _klienciGps);
                        perDzien[data] = new DayResult(tracks.Count, wizyty, DayStatus.Ok, null);

                        foreach (var w in wizyty)
                        {
                            if (!perKlient.TryGetValue(w.KlientId, out var lista))
                                perKlient[w.KlientId] = lista = new();
                            lista.Add((data, objectNo, w.Minuty));
                        }
                    }
                    catch (HistoriaRozladunkuService.WebfleetApiException ex)
                    {
                        sumaApiErrors++;
                        bledy.Add($"{objectNo} {data:yyyy-MM-dd}: {ex.Message}");
                        perDzien[data] = new DayResult(0, new(), DayStatus.ApiError, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        bledy.Add($"{objectNo} {data:yyyy-MM-dd}: {ex.GetType().Name}: {ex.Message}");
                        perDzien[data] = new DayResult(0, new(), DayStatus.ApiError, ex.Message);
                    }
                }
            }
            swTotal.Stop();

            // ── KRYTYCZNY KOMUNIKAT JEŚLI DUŻO BŁĘDÓW ──
            if (sumaApiErrors > total / 4)
            {
                sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
                sb.AppendLine($"║  ⚠️  UWAGA: {sumaApiErrors}/{total} zapytań do Webfleet zwróciło BŁĄD!         ║");
                sb.AppendLine("║  Najczęstsza przyczyna: rate limit przekroczony.               ║");
                sb.AppendLine("║  Wynik skanu jest NIEWIARYGODNY dla błędnych dni.              ║");
                sb.AppendLine("║  Spróbuj mniejszy zakres (7-14 dni) lub poczekaj 5-10 min.    ║");
                sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
            }

            // ── KPI PER POJAZD (krótka tabela na początku) ──
            sb.AppendLine("┌─ KPI PER POJAZD (sortowane po liczbie wizyt) ──────────────────────┐");
            sb.AppendLine($"   {"Pojazd",-30} {"Akt.dni",-8} {"Wizyt",-7} {"Śred.min",-9} {"Top klient (krotnie)",-30}");
            sb.AppendLine("   " + new string('─', 95));
            var kpiPojazdow = rezultat
                .Select(kv =>
                {
                    var p = _pojazdy.FirstOrDefault(x => x.PojazdID == kv.Key);
                    var dniZRuchem = kv.Value.Where(d => d.Value.Status == DayStatus.Ok && d.Value.Tracks > 0).Count();
                    var wszystkieWizyty = kv.Value.Values.SelectMany(d => d.Wizyty).ToList();
                    var topKlient = wszystkieWizyty.GroupBy(w => w.KlientId)
                                        .OrderByDescending(g => g.Count())
                                        .Select(g => new { Id = g.Key, N = g.Count() })
                                        .FirstOrDefault();
                    return new
                    {
                        PojazdId = kv.Key,
                        Opis = p.ObjectNo == null ? $"ID{kv.Key}" : $"{p.ObjectNo} • {p.Opis}",
                        DniZRuchem = dniZRuchem,
                        Wizyt = wszystkieWizyty.Count,
                        SredniaMin = wszystkieWizyty.Count > 0 ? (int)wszystkieWizyty.Average(w => w.Minuty) : 0,
                        TopKlient = topKlient == null ? "—" : $"{OpisKlienta(topKlient.Id)} ({topKlient.N}x)"
                    };
                })
                .OrderByDescending(x => x.Wizyt)
                .ToList();
            foreach (var k in kpiPojazdow)
            {
                string opis = k.Opis.Length > 28 ? k.Opis.Substring(0, 28) : k.Opis;
                string topKl = k.TopKlient.Length > 30 ? k.TopKlient.Substring(0, 30) : k.TopKlient;
                sb.AppendLine($"   {opis,-30} {k.DniZRuchem,-8} {k.Wizyt,-7} {k.SredniaMin,-9} {topKl,-30}");
            }
            // Pojazdy które miały 0 wizyt (kandydaci do sprawdzenia: serwis, niezmapowane GPS)
            var bezWizyt = kpiPojazdow.Where(x => x.Wizyt == 0).ToList();
            if (bezWizyt.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"   ⚠️ Pojazdy z 0 wizyt ({bezWizyt.Count}): {string.Join(", ", bezWizyt.Select(x => x.Opis.Split(" • ")[0]))}");
                sb.AppendLine($"      Sprawdź: serwis/awaria, kierowca na urlopie, albo trasy do klientów bez GPS w karcie.");
            }
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── PER POJAZD — kompaktowy widok ──
            sb.AppendLine("┌─ PER POJAZD (tylko dni z aktywnością; puste/błędne zwinięte) ──────┐");
            foreach (var (pojazdId, perDzien) in rezultat.OrderBy(kv => kv.Key))
            {
                var p = _pojazdy.FirstOrDefault(x => x.PojazdID == pojazdId);
                string opis = p.ObjectNo == null ? $"ID{pojazdId}" : $"{p.ObjectNo} • {p.Opis}";
                int okPuste = perDzien.Count(kv => kv.Value.Status == DayStatus.Ok && kv.Value.Tracks == 0);
                int okJecha = perDzien.Count(kv => kv.Value.Status == DayStatus.Ok && kv.Value.Tracks > 0 && kv.Value.Wizyty.Count == 0);
                int okWizyty = perDzien.Count(kv => kv.Value.Status == DayStatus.Ok && kv.Value.Wizyty.Count > 0);
                int apiErr = perDzien.Count(kv => kv.Value.Status == DayStatus.ApiError);

                sb.AppendLine();
                sb.AppendLine($"  🚛 {opis}");
                sb.AppendLine($"     Dni: {perDzien.Count} • z wizytami: {okWizyty} • jechał bez wizyt: {okJecha} • postoje: {okPuste} • błędy API: {apiErr}");

                // Tylko dni z wizytami (szczegóły) — reszta zwinięta w stats wyżej
                var dniZWizytami = perDzien.Where(kv => kv.Value.Status == DayStatus.Ok && kv.Value.Wizyty.Count > 0)
                                            .OrderBy(kv => kv.Key);
                foreach (var (data, val) in dniZWizytami)
                {
                    var klStr = string.Join(", ", val.Wizyty
                        .GroupBy(w => w.KlientId)
                        .Select(g => $"{OpisKlienta(g.Key)} ({g.Sum(w => w.Minuty)}min)"));
                    sb.AppendLine($"     {data:yyyy-MM-dd}  {val.Tracks,5} tracków • {val.Wizyty.Count} wizyt → {klStr}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── ZBIORCZO PER KLIENT ──
            sb.AppendLine("┌─ ZBIORCZO PER KLIENT (sortowane po liczbie wizyt malejąco) ────────┐");
            sb.AppendLine($"   {"Klient",-42} {"Pewn.",-7} {"Wizyt",-6} {"Mediana",-9} {"σ",-5} {"Pierwsza",-12} {"Ostatnia",-12}");
            sb.AppendLine("   " + new string('─', 110));
            foreach (var (klId, wizyty) in perKlient.OrderByDescending(kv => kv.Value.Count))
            {
                var minuty = wizyty.Select(w => w.Minuty).OrderBy(x => x).ToList();
                int mediana = minuty[minuty.Count / 2];
                double srednia = minuty.Average();
                double stdDev = minuty.Count > 1
                    ? Math.Sqrt(minuty.Average(x => Math.Pow(x - srednia, 2)))
                    : 0;
                var pierwsza = wizyty.Min(w => w.Dzien);
                var ostatnia = wizyty.Max(w => w.Dzien);
                string pewnosc = wizyty.Count >= 10 ? "✓✓✓"
                              : wizyty.Count >= 5  ? "✓✓ "
                              : wizyty.Count >= HistoriaRozladunkuService.MinProbDoZaufania ? "✓  "
                              : "⚠️ ";
                string opis = OpisKlienta(klId);
                if (opis.Length > 40) opis = opis.Substring(0, 40);
                sb.AppendLine($"   {opis,-42} {pewnosc,-7} {wizyty.Count,-6} {mediana + " min",-9} {(int)stdDev,-5} {pierwsza:yyyy-MM-dd}  {ostatnia:yyyy-MM-dd}");
            }
            if (perKlient.Count == 0)
                sb.AppendLine("   (żadnych wizyt w tym zakresie)");
            sb.AppendLine();
            sb.AppendLine("   Pewność:  ✓✓✓ ≥10 wizyt   ✓✓ ≥5   ✓ ≥3   ⚠️ <3 (za mało aby zaufać medianie)");
            sb.AppendLine("   σ = odchylenie standardowe w minutach (niskie = stabilny czas, wysokie = zmienny)");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── KLIENCI Z GPS BEZ WIZYT W ZAKRESIE ──
            var nieosiagnieci = _klienciGps.Keys.Except(perKlient.Keys).ToList();
            sb.AppendLine($"┌─ KLIENCI Z GPS NIEODWIEDZENI W TYM ZAKRESIE ({nieosiagnieci.Count}) ────┐");
            foreach (var id in nieosiagnieci.OrderBy(x => x))
                sb.AppendLine($"   {OpisKlienta(id)}");
            if (nieosiagnieci.Count == 0)
                sb.AppendLine("   ✓ wszyscy klienci z GPS zostali odwiedzeni!");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── PREVIEW ZAPISU DO BAZY ──
            sb.AppendLine("┌─ PREVIEW: CO ZOSTANIE ZAPISANE PO 'Odśwież z Webfleet' ────────────┐");
            var doZapisu = new List<(int KlientId, int Mediana, int LiczbaProb)>();
            var pominiete = new List<(int KlientId, int LiczbaProb, string Powod)>();
            foreach (var (klId, wizyty) in perKlient.OrderByDescending(kv => kv.Value.Count))
            {
                int mediana = wizyty.Select(w => w.Minuty).OrderBy(x => x).ToList()[wizyty.Count / 2];
                if (wizyty.Count >= HistoriaRozladunkuService.MinProbDoZaufania
                    && mediana >= HistoriaRozladunkuService.MinWizytaMin
                    && mediana <= HistoriaRozladunkuService.MaxWizytaMin)
                {
                    doZapisu.Add((klId, mediana, wizyty.Count));
                }
                else
                {
                    string powod = wizyty.Count < HistoriaRozladunkuService.MinProbDoZaufania
                                   ? $"tylko {wizyty.Count} wizyt (min {HistoriaRozladunkuService.MinProbDoZaufania})"
                                   : mediana < HistoriaRozladunkuService.MinWizytaMin
                                     ? $"mediana {mediana} min za krótka (<{HistoriaRozladunkuService.MinWizytaMin})"
                                     : $"mediana {mediana} min za długa (>{HistoriaRozladunkuService.MaxWizytaMin})";
                    pominiete.Add((klId, wizyty.Count, powod));
                }
            }
            sb.AppendLine($"│ ✓ DO EstymacjeRozladunku + KartotekaOdbiorcyDane ({doZapisu.Count} klientów): │");
            foreach (var (id, med, n) in doZapisu)
                sb.AppendLine($"│    {OpisKlienta(id),-50}  → {med} min ({n} wizyt)");
            if (doZapisu.Count == 0)
                sb.AppendLine($"│    (brak — żaden klient nie spełnił warunków)");
            sb.AppendLine($"│                                                                    │");
            sb.AppendLine($"│ ✗ POMINIĘCI ({pominiete.Count}):                                                       │");
            foreach (var (id, n, powod) in pominiete)
                sb.AppendLine($"│    {OpisKlienta(id),-50}  — {powod}");
            if (pominiete.Count == 0)
                sb.AppendLine($"│    (wszyscy spełniają warunki)");
            sb.AppendLine($"│                                                                    │");
            sb.AppendLine($"│ Aby zapisać: zamknij ten debugger → Statystyki rozładunku →        │");
            sb.AppendLine($"│ '🔄 Odśwież z Webfleet' — użyje cache z tej sesji (bez kolejnych API). │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // ── BŁĘDY API ──
            if (bledy.Count > 0)
            {
                sb.AppendLine($"┌─ BŁĘDY API ({bledy.Count}) — pełna lista ─────────────────────────────┐");
                foreach (var b in bledy.Take(50)) sb.AppendLine($"   • {b}");
                if (bledy.Count > 50) sb.AppendLine($"   … (+{bledy.Count - 50} więcej)");
                sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
                sb.AppendLine();
            }

            // ── PODSUMOWANIE ──
            int sumaTracks = rezultat.Values.SelectMany(d => d.Values).Sum(v => v.Tracks);
            int sumaWizyt = rezultat.Values.SelectMany(d => d.Values).Sum(v => v.Wizyty.Count);
            var (interval, cache, locked) = HistoriaRozladunkuService.GetThrottleStats();
            double msPerReq = total > 0 ? (double)swTotal.ElapsedMilliseconds / total : 0;
            sb.AppendLine("════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  PODSUMOWANIE: {sumaTracks} tracków • {sumaWizyt} wizyt • {perKlient.Count}/{_klienciGps.Count} klientów odwiedzonych");
            sb.AppendLine($"  Błędów API: {sumaApiErrors}/{total} ({100.0 * sumaApiErrors / total:F1}%) — {(sumaApiErrors == 0 ? "✓ pełne dane" : "⚠️ niepełne (retry po 30s lockdown)")}");
            sb.AppendLine($"  Czas: {swTotal.Elapsed.TotalMinutes:F1} min  •  {msPerReq:F0} ms/zapytanie  •  interwał: {interval} ms");
            sb.AppendLine($"  Cache w pamięci: {cache} wpisów — powtórny skan tych dni będzie z cache (bez API)");
            if (locked) sb.AppendLine("  ⚠️ Webfleet API quota LOCK aktywny — następny skan poczeka 30s");
            sb.AppendLine("════════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        private enum DayStatus { Ok, ApiError }
        private record DayResult(int Tracks, List<HistoriaRozladunkuService.Wizyta> Wizyty, DayStatus Status, string? ErrorMsg);

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════
        private record BlokPostoju(DateTime Start, DateTime Koniec, int Minuty,
                                   double LatA, double LonA, double LatB, double LonB);

        private static List<BlokPostoju> WykryjBlokiPostoju(List<HistoriaRozladunkuService.TrackPoint> tracks)
        {
            var wynik = new List<BlokPostoju>();
            HistoriaRozladunkuService.TrackPoint? start = null;
            HistoriaRozladunkuService.TrackPoint? prev = null;
            foreach (var t in tracks)
            {
                if (t.Speed <= HistoriaRozladunkuService.ProgStoiKmh)
                {
                    if (start == null) start = t;
                    prev = t;
                }
                else
                {
                    if (start != null && prev != null)
                    {
                        int min = (int)((prev.Time - start.Time).TotalMinutes);
                        if (min >= 1)
                            wynik.Add(new BlokPostoju(start.Time, prev.Time, min, start.Lat, start.Lon, prev.Lat, prev.Lon));
                    }
                    start = null; prev = null;
                }
            }
            if (start != null && prev != null)
            {
                int min = (int)((prev.Time - start.Time).TotalMinutes);
                if (min >= 1)
                    wynik.Add(new BlokPostoju(start.Time, prev.Time, min, start.Lat, start.Lon, prev.Lat, prev.Lon));
            }
            return wynik;
        }

        private (int Id, double DistKm) NajblizszyKlient(double lat, double lon)
        {
            int bestId = 0;
            double bestDist = double.MaxValue;
            foreach (var (id, g) in _klienciGps)
            {
                double d = HistoriaRozladunkuService.HaversineKm(lat, lon, g.Lat, g.Lon);
                if (d < bestDist) { bestDist = d; bestId = id; }
            }
            return (bestId, bestDist);
        }

        private static async Task<long> ScalarAsync(SqlConnection cn, string sql)
        {
            await using var cmd = new SqlCommand(sql, cn);
            cmd.CommandTimeout = 60;
            var o = await cmd.ExecuteScalarAsync();
            return o == null || o == DBNull.Value ? 0L : Convert.ToInt64(o);
        }

        private static async Task<string> ScalarStrAsync(SqlConnection cn, string sql)
        {
            await using var cmd = new SqlCommand(sql, cn);
            cmd.CommandTimeout = 60;
            var o = await cmd.ExecuteScalarAsync();
            return o == null || o == DBNull.Value ? "(NULL)" : o.ToString() ?? "";
        }

        // ════════════════════════════════════════════════════════════════════
        // FOOTER HANDLERY
        // ════════════════════════════════════════════════════════════════════
        private void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            (_, string tresc) = AktualnyTabContent();
            if (string.IsNullOrWhiteSpace(tresc))
            {
                MessageBox.Show("Brak treści do skopiowania — uruchom najpierw analizę.",
                                "Pusto", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { Clipboard.SetText(tresc); TxtStatus.Text = $"📋 Skopiowano ({tresc.Length} znaków)."; }
            catch (Exception ex) { TxtStatus.Text = $"Błąd kopiowania: {ex.Message}"; }
        }

        private void BtnCzyscCache_Click(object sender, RoutedEventArgs e)
        {
            int przedtem = HistoriaRozladunkuService.CacheSize;
            HistoriaRozladunkuService.ClearTracksCache();
            TxtStatus.Text = $"🗑 Cache wyczyszczony ({przedtem} wpisów usuniętych).";
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            (string typ, string tresc) = AktualnyTabContent();
            if (string.IsNullOrWhiteSpace(tresc))
            {
                MessageBox.Show("Brak treści do zapisania — uruchom najpierw analizę.",
                                "Pusto", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Debugger_{typ}_{DateTime.Now:yyyy-MM-dd_HHmm}.txt",
                    Filter = "Plik tekstowy (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*",
                    DefaultExt = "txt"
                };
                if (dlg.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(dlg.FileName, tresc, System.Text.Encoding.UTF8);
                    TxtStatus.Text = $"💾 Zapisano: {dlg.FileName}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (string Typ, string Tresc) AktualnyTabContent()
        {
            if (TxtInwent.IsVisible) return ("Inwentaryzacja", TxtInwent.Text);
            if (TxtTestDzien.IsVisible) return ("TestDnia", TxtTestDzien.Text);
            if (TxtDrill.IsVisible) return ("Drill", TxtDrill.Text);
            if (TxtSkan.IsVisible) return ("SkanZakresu", TxtSkan.Text);
            return ("Pusto", "");
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
