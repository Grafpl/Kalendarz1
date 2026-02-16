using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace Kalendarz1.Transport
{
    public partial class TransportMapaWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly DateTime _data;
        private readonly StringBuilder _debugLog = new();

        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;
        private const string ApiKey = "AIzaSyCFXL2NYDnLBpiih1pG27SbsY62ZYsKdgo";

        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        { Timeout = TimeSpan.FromSeconds(15) };

        public TransportMapaWindow(string connLibra, string connHandel, DateTime data)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;
            _data = data;
            txtSubtitle.Text = $"Wolne zamówienia na {data:dd.MM.yyyy (dddd)}";

            Log($"═══════════════════════════════════════");
            Log($"  TRANSPORT MAPA DEBUGGER");
            Log($"═══════════════════════════════════════");
            Log($"Data wybrana: {data:yyyy-MM-dd (dddd)}");
            Log($"Teraz: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"ConnLibra: {MaskConn(connLibra)}");
            Log($"ConnHandel: {MaskConn(connHandel)}");
            Log("");

            Loaded += async (s, e) => await LoadAsync();
        }

        private void Log(string msg)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            _debugLog.AppendLine($"[{ts}] {msg}");
            Dispatcher.Invoke(() =>
            {
                txtDebugLog.Text = _debugLog.ToString();
                debugScroll.ScrollToEnd();
            });
        }

        private void BtnCopyDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_debugLog.ToString());
                Log(">>> Skopiowano do schowka <<<");
            }
            catch (Exception ex)
            {
                Log($"!!! Clipboard BŁĄD: {ex.Message}");
            }
        }

        private string MaskConn(string conn)
        {
            // Pokaż Server i Database, ukryj resztę
            var parts = conn.Split(';');
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (trimmed.StartsWith("Server", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
                    sb.Append(trimmed).Append("; ");
                else if (trimmed.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                    sb.Append("Password=***; ");
            }
            return sb.ToString().TrimEnd(' ', ';');
        }

        private void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            if (debugPanel.Visibility == Visibility.Collapsed)
            {
                debugPanel.Visibility = Visibility.Visible;
                colDebug.Width = new GridLength(420);
            }
            else
            {
                debugPanel.Visibility = Visibility.Collapsed;
                colDebug.Width = new GridLength(0);
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                // ========== KROK 1: SQL ==========
                Log("══════════════════════════════════════════");
                Log("  KROK 1: SQL - ZamowieniaMieso (LibraNet)");
                Log("══════════════════════════════════════════");
                txtLoading.Text = "Pobieranie zamówień...";
                var zamowienia = await PobierzZamowieniaAsync();

                txtLiczbaZam.Text = zamowienia.Count.ToString();
                txtSumaPoj.Text = zamowienia.Sum(z => z.Pojemniki).ToString();
                Log($"Wynik SQL: {zamowienia.Count} zamówień, {zamowienia.Sum(z => z.Pojemniki)} pojemników");

                if (zamowienia.Count == 0)
                {
                    Log("STOP: Brak zamówień. Sprawdź datę i filtry SQL.");
                    txtLoading.Text = "Brak wolnych zamówień na ten dzień.";
                    // Auto-otwórz debug
                    debugPanel.Visibility = Visibility.Visible;
                    colDebug.Width = new GridLength(420);
                    return;
                }

                // Tabela WSZYSTKICH zamówień
                Log("");
                Log("╔══════╦══════════╦════════════════════════╦══════════════╦════════════════╦═══════╗");
                Log("║  ID  ║ KlientId ║ Klient                 ║ Kod          ║ Miasto         ║ Poj.  ║");
                Log("╠══════╬══════════╬════════════════════════╬══════════════╬════════════════╬═══════╣");
                foreach (var z in zamowienia)
                {
                    var klient = (z.Klient.Length > 22 ? z.Klient.Substring(0, 22) : z.Klient).PadRight(22);
                    var kod = (z.Kod.Length > 12 ? z.Kod.Substring(0, 12) : z.Kod).PadRight(12);
                    var miasto = (z.Miasto.Length > 14 ? z.Miasto.Substring(0, 14) : z.Miasto).PadRight(14);
                    Log($"║ {z.Id,4} ║ {z.KlientId,8} ║ {klient} ║ {kod} ║ {miasto} ║ {z.Pojemniki,5} ║");
                }
                Log("╚══════╩══════════╩════════════════════════╩══════════════╩════════════════╩═══════╝");
                Log("");

                // Pełne adresy
                Log("--- Pełne adresy zamówień: ---");
                foreach (var z in zamowienia)
                    Log($"  #{z.Id}: Adres=[{z.Adres}] Kod=[{z.Kod}] Miasto=[{z.Miasto}] Handl=[{z.Handlowiec}]");
                Log("");

                // Statystyki adresowe
                var zKodem = zamowienia.Count(z => !string.IsNullOrWhiteSpace(z.Kod) && z.Kod.Trim().Length >= 5);
                var zKodemKrotkim = zamowienia.Count(z => !string.IsNullOrWhiteSpace(z.Kod) && z.Kod.Trim().Length < 5 && z.Kod.Trim().Length > 0);
                var zMiastem = zamowienia.Count(z => !string.IsNullOrWhiteSpace(z.Miasto));
                var zAdresem = zamowienia.Count(z => !string.IsNullOrWhiteSpace(z.Adres));
                var bezKodu = zamowienia.Count(z => string.IsNullOrWhiteSpace(z.Kod));
                var bezDanych = zamowienia.Count(z => string.IsNullOrWhiteSpace(z.Kod) && string.IsNullOrWhiteSpace(z.Miasto));
                Log("┌─── STATYSTYKI ADRESOWE ───┐");
                Log($"│ Z kodem (>=5 zn): {zKodem,5}   │");
                Log($"│ Z kodem (<5 zn):  {zKodemKrotkim,5}   │");
                Log($"│ Bez kodu:         {bezKodu,5}   │");
                Log($"│ Z miastem:        {zMiastem,5}   │");
                Log($"│ Z adresem:        {zAdresem,5}   │");
                Log($"│ BEZ danych:       {bezDanych,5}   │");
                Log("└────────────────────────────┘");

                var unikKody = zamowienia
                    .Where(z => !string.IsNullOrWhiteSpace(z.Kod))
                    .Select(z => z.Kod.Trim())
                    .Distinct().ToList();
                Log($"Unikalne kody ({unikKody.Count}): {string.Join(", ", unikKody)}");

                var unikMiasta = zamowienia
                    .Where(z => !string.IsNullOrWhiteSpace(z.Miasto))
                    .Select(z => z.Miasto.Trim())
                    .Distinct().ToList();
                Log($"Unikalne miasta ({unikMiasta.Count}): {string.Join(", ", unikMiasta)}");
                Log("");

                // ========== KROK 2: KodyPocztowe ==========
                Log("══════════════════════════════════════════");
                Log("  KROK 2: Tabela KodyPocztowe (LibraNet)");
                Log("══════════════════════════════════════════");
                txtLoading.Text = "Pobieranie współrzędnych...";
                await PrzypiszWspolrzedneAsync(zamowienia);

                var naMapie = zamowienia.Count(z => z.Lat.HasValue);
                txtNaMapie.Text = naMapie.ToString();
                Log($"Po KodyPocztowe: {naMapie}/{zamowienia.Count} z współrzędnymi");

                // Które nie mają współrzędnych?
                var bezWsp = zamowienia.Where(z => !z.Lat.HasValue).ToList();
                if (bezWsp.Any())
                {
                    Log($"Zamówienia BEZ współrzędnych ({bezWsp.Count}):");
                    foreach (var z in bezWsp)
                        Log($"  #{z.Id} [{z.Klient}] Kod=[{z.Kod}] Miasto=[{z.Miasto}]");
                }
                Log("");

                // ========== KROK 3: Geokodowanie brakujących ==========
                if (naMapie < zamowienia.Count)
                {
                    Log("══════════════════════════════════════════");
                    Log("  KROK 3: Google Geocoding REST API");
                    Log("══════════════════════════════════════════");
                    txtLoading.Text = $"Geokodowanie brakujących ({zamowienia.Count - naMapie})...";
                    await GeokodujBrakujaceAsync(zamowienia);
                    naMapie = zamowienia.Count(z => z.Lat.HasValue);
                    txtNaMapie.Text = naMapie.ToString();
                    Log($"Po geokodowaniu: {naMapie}/{zamowienia.Count} z współrzędnymi");
                }

                if (naMapie == 0)
                {
                    Log("STOP: 0 zamówień z współrzędnymi. Mapa będzie pusta.");
                    Log("Sprawdź: kody pocztowe klientów w Handel, tabelę KodyPocztowe, Google API key");
                    txtLoading.Text = $"Znaleziono {zamowienia.Count} zamówień, ale brak współrzędnych.";
                    debugPanel.Visibility = Visibility.Visible;
                    colDebug.Width = new GridLength(420);
                    return;
                }

                // Pełna tabela wynikowa
                Log("");
                Log("╔══════╦════════════════════════╦════════════╦════════════╦════════╗");
                Log("║  ID  ║ Klient                 ║ Lat        ║ Lng        ║ Status ║");
                Log("╠══════╬════════════════════════╬════════════╬════════════╬════════╣");
                foreach (var z in zamowienia)
                {
                    var klient = (z.Klient.Length > 22 ? z.Klient.Substring(0, 22) : z.Klient).PadRight(22);
                    var lat = z.Lat.HasValue ? z.Lat.Value.ToString("F4").PadRight(10) : "---       ";
                    var lng = z.Lng.HasValue ? z.Lng.Value.ToString("F4").PadRight(10) : "---       ";
                    var status = z.Lat.HasValue ? "  OK  " : " BRAK ";
                    Log($"║ {z.Id,4} ║ {klient} ║ {lat} ║ {lng} ║ {status} ║");
                }
                Log("╚══════╩════════════════════════╩════════════╩════════════╩════════╝");
                Log("");

                // ========== KROK 4: WebView2 + HTML ==========
                Log("══════════════════════════════════════════");
                Log("  KROK 4: Generowanie mapy (WebView2)");
                Log("══════════════════════════════════════════");
                txtLoading.Text = "Inicjalizacja mapy...";
                await webView.EnsureCoreWebView2Async();
                Log("WebView2 gotowy");

                var mapData = zamowienia.Where(z => z.Lat.HasValue).ToList();
                Log($"Zamówienia do renderowania: {mapData.Count}");
                var html = GenerujHtml(mapData);
                Log($"HTML wygenerowany: {html.Length} znaków");

                // Pokaż fragment JSON (pierwsze 500 znaków)
                var jsonStart = html.IndexOf("var orders = ");
                if (jsonStart >= 0)
                {
                    var jsonEnd = html.IndexOf("];", jsonStart);
                    if (jsonEnd > jsonStart)
                    {
                        var jsonSnippet = html.Substring(jsonStart, Math.Min(500, jsonEnd - jsonStart + 2));
                        Log($"JSON (fragment): {jsonSnippet}");
                    }
                }

                webView.NavigateToString(html);
                Log("NavigateToString() wywołane");
                Log("");
                Log("═══════════════════════════════════════");
                Log("  GOTOWE - mapa powinna się wyświetlić");
                Log("═══════════════════════════════════════");

                loadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Log($"!!! WYJĄTEK: {ex.GetType().Name}: {ex.Message}");
                Log($"StackTrace: {ex.StackTrace}");
                txtLoading.Text = $"Błąd: {ex.Message}";
                debugPanel.Visibility = Visibility.Visible;
                colDebug.Width = new GridLength(420);
            }
        }

        private async Task<List<ZamMapItem>> PobierzZamowieniaAsync()
        {
            var lista = new List<ZamMapItem>();

            Log($"Łączę z LibraNet...");
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            Log("Połączono z LibraNet");

            var dataOd = _data.Date;
            var dataDo = _data.Date.AddDays(2);
            Log($"Filtr dat: DataUboju >= {dataOd:yyyy-MM-dd} AND <= {dataDo:yyyy-MM-dd}");
            Log("Filtr: Status NOT IN ('Anulowane'), TransportStatus NOT IN ('Przypisany','Wlasny'), KursID IS NULL");

            var sql = @"
                SELECT
                    zm.Id,
                    zm.KlientId,
                    zm.DataPrzyjazdu,
                    zm.DataUboju,
                    ISNULL(zm.LiczbaPalet, 0) AS Palety,
                    ISNULL(zm.LiczbaPojemnikow, 0) AS Pojemniki
                FROM dbo.ZamowieniaMieso zm
                WHERE CAST(zm.DataUboju AS DATE) >= @DataOd
                  AND CAST(zm.DataUboju AS DATE) <= @DataDo
                  AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                  AND ISNULL(zm.TransportStatus, 'Oczekuje') NOT IN ('Przypisany', 'Wlasny')
                  AND zm.TransportKursID IS NULL
                ORDER BY zm.DataPrzyjazdu";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@DataOd", dataOd);
            cmd.Parameters.AddWithValue("@DataDo", dataDo);

            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new ZamMapItem
                {
                    Id = rdr.GetInt32(0),
                    KlientId = rdr.GetInt32(1),
                    DataPrzyjazdu = rdr.IsDBNull(2) ? _data : rdr.GetDateTime(2),
                    DataUboju = rdr.IsDBNull(3) ? _data : rdr.GetDateTime(3),
                    Palety = rdr.IsDBNull(4) ? 0 : rdr.GetDecimal(4),
                    Pojemniki = rdr.IsDBNull(5) ? 0 : rdr.GetInt32(5)
                });
            }
            Log($"SQL zwrócił {lista.Count} wierszy");

            if (!lista.Any())
            {
                Log("UWAGA: 0 wierszy! Sprawdź:");
                Log($"  - Czy są zamówienia z DataUboju między {dataOd:yyyy-MM-dd} a {dataDo:yyyy-MM-dd}?");
                Log("  - Czy Status != 'Anulowane'?");
                Log("  - Czy TransportStatus != 'Przypisany' i != 'Wlasny'?");
                Log("  - Czy TransportKursID IS NULL?");

                // Sprawdź ile jest w ogóle w bazie na ten okres
                try
                {
                    var sqlCount = @"SELECT
                        COUNT(*) AS Total,
                        SUM(CASE WHEN ISNULL(zm.Status,'Nowe') = 'Anulowane' THEN 1 ELSE 0 END) AS Anulowane,
                        SUM(CASE WHEN ISNULL(zm.TransportStatus,'Oczekuje') IN ('Przypisany','Wlasny') THEN 1 ELSE 0 END) AS Przypisane,
                        SUM(CASE WHEN zm.TransportKursID IS NOT NULL THEN 1 ELSE 0 END) AS ZKursem
                    FROM dbo.ZamowieniaMieso zm
                    WHERE CAST(zm.DataUboju AS DATE) >= @DataOd AND CAST(zm.DataUboju AS DATE) <= @DataDo";
                    await using var cmdDiag = new SqlCommand(sqlCount, cn);
                    cmdDiag.Parameters.AddWithValue("@DataOd", dataOd);
                    cmdDiag.Parameters.AddWithValue("@DataDo", dataDo);
                    await using var rdrDiag = await cmdDiag.ExecuteReaderAsync();
                    if (await rdrDiag.ReadAsync())
                    {
                        Log($"  Diagnostyka na {dataOd:dd.MM}-{dataDo:dd.MM}:");
                        Log($"    Total w okresie: {rdrDiag.GetInt32(0)}");
                        Log($"    Anulowane: {rdrDiag.GetInt32(1)}");
                        Log($"    Przypisane/Własny: {rdrDiag.GetInt32(2)}");
                        Log($"    Z TransportKursID: {rdrDiag.GetInt32(3)}");
                    }
                }
                catch (Exception exDiag) { Log($"  Diagnostyka błąd: {exDiag.Message}"); }

                return lista;
            }

            // Loguj surowe dane z SQL
            Log("Surowe dane z SQL (Id, KlientId, DataUboju, Palety, Poj):");
            foreach (var z in lista.Take(10))
                Log($"  #{z.Id} KlientId={z.KlientId} Ubój={z.DataUboju:yyyy-MM-dd} Palety={z.Palety} Poj={z.Pojemniki}");
            if (lista.Count > 10) Log($"  ... i jeszcze {lista.Count - 10} więcej");
            Log("");

            // Pobierz dane klientów z Handel
            Log("Łączę z Handel (adres domyślny)...");
            try
            {
                var klientIds = string.Join(",", lista.Select(z => z.KlientId).Distinct());
                Log($"KlientIds do pobrania: {klientIds}");

                await using var cnH = new SqlConnection(_connHandel);
                await cnH.OpenAsync();
                Log("Połączono z Handel");

                var sqlK = $@"
                    SELECT
                        c.Id,
                        ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10))) AS Nazwa,
                        ISNULL(poa.Street, '') AS Ulica,
                        ISNULL(poa.Postcode, '') AS Kod,
                        ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec
                    FROM SSCommon.STContractors c
                    LEFT JOIN SSCommon.STPostOfficeAddresses poa
                        ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                    LEFT JOIN SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                    WHERE c.Id IN ({klientIds})";

                await using var cmdK = new SqlCommand(sqlK, cnH);
                await using var rdrK = await cmdK.ExecuteReaderAsync();

                var dict = new Dictionary<int, (string Nazwa, string Ulica, string Kod, string Handlowiec)>();
                while (await rdrK.ReadAsync())
                {
                    dict[rdrK.GetInt32(0)] = (
                        rdrK.GetString(1),
                        rdrK.GetString(2).Trim(),
                        rdrK.GetString(3).Trim(),
                        rdrK.GetString(4).Trim()
                    );
                }
                Log($"Handel zwrócił dane dla {dict.Count} klientów");
                Log("");

                // Loguj WSZYSTKO co zwróciło Handel
                Log("Dane z Handel (KAŻDY klient):");
                foreach (var kv in dict)
                    Log($"  ID={kv.Key}: [{kv.Value.Nazwa}] Kod=[{kv.Value.Kod}] Ulica=[{kv.Value.Ulica}] Handl=[{kv.Value.Handlowiec}]");

                int matched = 0, unmatched = 0;
                foreach (var z in lista)
                {
                    if (dict.TryGetValue(z.KlientId, out var k))
                    {
                        z.Klient = k.Nazwa;
                        z.Adres = string.Join(", ", new[] { k.Ulica, k.Kod }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        z.Kod = k.Kod;
                        z.Handlowiec = k.Handlowiec;
                        matched++;
                    }
                    else
                    {
                        z.Klient = $"KH #{z.KlientId}";
                        unmatched++;
                    }
                }
                Log($"Dopasowanie: {matched} znalezionych, {unmatched} bez danych w Handel");
            }
            catch (Exception ex)
            {
                Log($"!!! Handel BŁĄD: {ex.GetType().Name}: {ex.Message}");
            }

            return lista;
        }

        private async Task PrzypiszWspolrzedneAsync(List<ZamMapItem> zamowienia)
        {
            var kody = zamowienia
                .Where(z => !string.IsNullOrWhiteSpace(z.Kod))
                .Select(z => z.Kod.Trim().Replace("-", ""))
                .Distinct()
                .ToList();

            Log($"Unikalne kody (bez myślnika): {kody.Count} → [{string.Join(", ", kody.Take(10))}]");

            if (!kody.Any())
            {
                Log("Brak kodów pocztowych - pomijam KodyPocztowe");
                return;
            }

            var cache = new Dictionary<string, (double lat, double lng)>();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var checkSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'KodyPocztowe'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                Log($"Tabela KodyPocztowe istnieje: {tableExists}");

                if (!tableExists)
                {
                    Log("Brak tabeli KodyPocztowe - pomijam");
                    return;
                }

                // Ile wierszy w tabeli?
                var cntCmd = new SqlCommand("SELECT COUNT(*) FROM KodyPocztowe", cn);
                var totalRows = (int)await cntCmd.ExecuteScalarAsync();
                var cntWithCoords = (int)await new SqlCommand("SELECT COUNT(*) FROM KodyPocztowe WHERE Latitude IS NOT NULL", cn).ExecuteScalarAsync();
                Log($"KodyPocztowe: {totalRows} wierszy, {cntWithCoords} z współrzędnymi");

                var kodParams = string.Join(",", kody.Select((k, i) => $"@k{i}"));
                var sql = $@"SELECT Kod, Latitude, Longitude
                             FROM KodyPocztowe
                             WHERE REPLACE(Kod, '-', '') IN ({kodParams})
                               AND Latitude IS NOT NULL AND Longitude IS NOT NULL";

                await using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < kody.Count; i++)
                    cmd.Parameters.AddWithValue($"@k{i}", kody[i]);

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var kod = rdr.GetString(0).Replace("-", "");
                    var lat = rdr.GetDouble(1);
                    var lng = rdr.GetDouble(2);
                    if (Math.Abs(lat) > 0.001 && Math.Abs(lng) > 0.001)
                        cache[kod] = (lat, lng);
                }
                Log($"Znaleziono współrzędne dla {cache.Count}/{kody.Count} kodów");
                foreach (var kv in cache.Take(5))
                    Log($"  {kv.Key} → lat={kv.Value.lat:F4}, lng={kv.Value.lng:F4}");
            }
            catch (Exception ex)
            {
                Log($"!!! KodyPocztowe BŁĄD: {ex.GetType().Name}: {ex.Message}");
            }

            int assigned = 0;
            foreach (var z in zamowienia)
            {
                if (string.IsNullOrWhiteSpace(z.Kod)) continue;
                var kodNorm = z.Kod.Trim().Replace("-", "");
                if (cache.TryGetValue(kodNorm, out var c))
                {
                    z.Lat = c.lat;
                    z.Lng = c.lng;
                    assigned++;
                }
            }
            Log($"Przypisano współrzędne do {assigned} zamówień");
        }

        private async Task GeokodujBrakujaceAsync(List<ZamMapItem> zamowienia)
        {
            var brakujace = zamowienia
                .Where(z => !z.Lat.HasValue && !string.IsNullOrWhiteSpace(z.Kod) && z.Kod.Trim().Length >= 5)
                .Select(z => z.Kod.Trim())
                .Distinct()
                .ToList();

            Log($"Brakujące kody do geokodowania: {brakujace.Count}");
            foreach (var b in brakujace.Take(5))
                Log($"  Kod=[{b}]");

            if (!brakujace.Any())
            {
                Log("Brak kodów pocztowych do geokodowania");
                return;
            }

            var cache = new Dictionary<string, (double lat, double lng)>();
            int ok = 0, fail = 0;

            foreach (var kod in brakujace)
            {
                var query = kod + ", Poland";
                var coords = await GeokodujGoogle(query, "Poland");
                if (coords.HasValue)
                {
                    cache[kod] = coords.Value;
                    ok++;
                    Log($"  OK: [{query}] → lat={coords.Value.lat:F4}, lng={coords.Value.lng:F4}");
                }
                else
                {
                    fail++;
                    Log($"  FAIL: [{query}] → brak wyniku");
                }
                await Task.Delay(30);
            }
            Log($"Geokodowanie: {ok} OK, {fail} FAIL");

            // Zapisz do KodyPocztowe
            if (cache.Any())
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    await new SqlCommand(
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KodyPocztowe') " +
                        "CREATE TABLE KodyPocztowe (Kod NVARCHAR(10) PRIMARY KEY, miej NVARCHAR(100), Latitude FLOAT, Longitude FLOAT)",
                        cn).ExecuteNonQueryAsync();

                    int saved = 0;
                    foreach (var (kod, (lat, lng)) in cache)
                    {
                        var upsert = @"IF NOT EXISTS (SELECT 1 FROM KodyPocztowe WHERE REPLACE(Kod,'-','') = @kodNorm)
                                        INSERT INTO KodyPocztowe (Kod, Latitude, Longitude) VALUES (@kod, @lat, @lng)
                                       ELSE
                                        UPDATE KodyPocztowe SET Latitude=@lat, Longitude=@lng WHERE REPLACE(Kod,'-','')=@kodNorm AND Latitude IS NULL";
                        await using var cmd = new SqlCommand(upsert, cn);
                        cmd.Parameters.AddWithValue("@kod", kod);
                        cmd.Parameters.AddWithValue("@kodNorm", kod.Replace("-", ""));
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        await cmd.ExecuteNonQueryAsync();
                        saved++;
                    }
                    Log($"Zapisano {saved} kodów do KodyPocztowe");
                }
                catch (Exception ex)
                {
                    Log($"!!! Zapis KodyPocztowe BŁĄD: {ex.Message}");
                }
            }

            int assignedFromGeo = 0;
            foreach (var z in zamowienia)
            {
                if (!z.Lat.HasValue && !string.IsNullOrWhiteSpace(z.Kod) &&
                    cache.TryGetValue(z.Kod.Trim(), out var c))
                {
                    z.Lat = c.lat;
                    z.Lng = c.lng;
                    assignedFromGeo++;
                }
            }
            Log($"Przypisano z geokodowania: {assignedFromGeo}");
        }

        private async Task<(double lat, double lng)?> GeokodujGoogle(string addr, string kraj)
        {
            try
            {
                var q = HttpUtility.UrlEncode(addr + " " + kraj);
                var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={q}&key={ApiKey}";
                using var resp = await _http.GetAsync(url);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    Log($"    HTTP {(int)resp.StatusCode}: {json.Substring(0, Math.Min(200, json.Length))}");
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.GetProperty("status").GetString();
                if (status == "OK")
                {
                    var loc = doc.RootElement.GetProperty("results")[0].GetProperty("geometry").GetProperty("location");
                    return (loc.GetProperty("lat").GetDouble(), loc.GetProperty("lng").GetDouble());
                }
                else
                {
                    // Loguj status + error_message jeśli jest
                    var errMsg = "";
                    if (doc.RootElement.TryGetProperty("error_message", out var em))
                        errMsg = em.GetString() ?? "";
                    Log($"    Google API status=[{status}] error=[{errMsg}]");
                }
            }
            catch (Exception ex)
            {
                Log($"    Geocode EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }
            return null;
        }

        private string GenerujHtml(List<ZamMapItem> zamowienia)
        {
            var ci = CultureInfo.GetCultureInfo("pl-PL");
            var inv = CultureInfo.InvariantCulture;
            var dataJson = JsonSerializer.Serialize(zamowienia.Select(z => new
            {
                id = z.Id,
                klientId = z.KlientId,
                klient = z.Klient,
                adres = z.Adres,
                miasto = z.Miasto,
                kod = z.Kod,
                pojemniki = z.Pojemniki,
                palety = z.Palety.ToString("N1", ci),
                godz = z.DataPrzyjazdu.ToString("HH:mm"),
                uboj = z.DataUboju.ToString("dd.MM"),
                handlowiec = z.Handlowiec,
                lat = z.Lat!.Value.ToString(inv),
                lng = z.Lng!.Value.ToString(inv)
            }), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<style>");
            sb.AppendLine("html,body,#map{height:100%;width:100%;margin:0;padding:0;font-family:'Segoe UI',sans-serif;}");
            sb.AppendLine(".iw{padding:0;min-width:260px;max-width:340px;font-family:'Segoe UI',sans-serif;}");
            sb.AppendLine(".iw-h{background:linear-gradient(135deg,#4F46E5,#6366F1);color:#fff;padding:12px 14px;border-radius:8px 8px 0 0;margin:-12px -12px 10px -12px;}");
            sb.AppendLine(".iw-h b{font-size:14px;display:block;margin-bottom:2px;}");
            sb.AppendLine(".iw-h small{opacity:.85;font-size:11px;display:block;line-height:1.3;}");
            sb.AppendLine(".iw-body{padding:0 14px 12px;}");
            sb.AppendLine(".iw-row{display:flex;justify-content:space-between;padding:4px 0;font-size:12px;color:#334155;}");
            sb.AppendLine(".iw-row span:first-child{color:#64748B;}");
            sb.AppendLine(".iw-row span:last-child{font-weight:600;}");
            sb.AppendLine(".iw-sep{border-top:1px solid #E2E8F0;margin:6px 0;}");
            sb.AppendLine(".iw-badge{display:inline-block;background:#EEF2FF;color:#4F46E5;font-size:10px;font-weight:600;padding:3px 10px;border-radius:4px;margin-top:4px;}");
            sb.AppendLine(".iw-badge-green{display:inline-block;background:#F0FDF4;color:#16A34A;font-size:10px;font-weight:600;padding:3px 10px;border-radius:4px;margin-top:4px;margin-left:4px;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div id='map'></div>");
            sb.AppendLine("<script>");

            sb.AppendLine($"var orders = {dataJson};");
            sb.AppendLine($"var bazaLat={BazaLat.ToString(inv)}, bazaLng={BazaLng.ToString(inv)};");

            sb.AppendLine(@"
var map, infoWindow;

function initMap(){
    map = new google.maps.Map(document.getElementById('map'),{
        center:{lat:bazaLat,lng:bazaLng},
        zoom:8,
        styles:[
            {featureType:'poi',stylers:[{visibility:'off'}]},
            {featureType:'transit',stylers:[{visibility:'off'}]}
        ],
        mapTypeControl:true,
        streetViewControl:false,
        fullscreenControl:true
    });

    infoWindow = new google.maps.InfoWindow();

    // Baza
    new google.maps.Marker({
        position:{lat:bazaLat,lng:bazaLng},
        map:map,
        title:'BAZA - Koziółki 40',
        icon:{
            path:google.maps.SymbolPath.CIRCLE,
            fillColor:'#16A34A',fillOpacity:1,
            strokeColor:'#fff',strokeWeight:3,
            scale:14
        },
        zIndex:9999
    }).addListener('click',function(){
        infoWindow.setContent('<div style=""padding:10px;font-weight:700;font-size:15px;"">&#127981; BAZA<br><span style=""font-weight:400;font-size:12px;color:#64748B;"">Koziółki 40, 95-061 Dmosin</span></div>');
        infoWindow.open(map,this);
    });

    var bounds = new google.maps.LatLngBounds();
    bounds.extend({lat:bazaLat,lng:bazaLng});

    // Grupuj zamówienia po lokalizacji (ten sam klient/kod)
    var labels = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

    for(var i=0; i<orders.length; i++){
        var o = orders[i];
        var lat = parseFloat(o.lat);
        var lng = parseFloat(o.lng);
        if(isNaN(lat) || isNaN(lng)) continue;

        var pos = {lat:lat, lng:lng};
        bounds.extend(pos);

        (function(o, pos, idx){
            var m = new google.maps.Marker({
                position: pos,
                map: map,
                title: o.klient + ' (' + o.pojemniki + ' poj.)',
                label:{
                    text: o.pojemniki.toString(),
                    color:'#fff',
                    fontSize:'9px',
                    fontWeight:'bold'
                },
                icon:{
                    path: google.maps.SymbolPath.CIRCLE,
                    fillColor:'#4F46E5', fillOpacity:0.9,
                    strokeColor:'#fff', strokeWeight:2,
                    scale:16
                }
            });
            m.addListener('click',function(){
                var html = '<div class=""iw"">'
                    +'<div class=""iw-h"">'
                    +'<b>'+esc(o.klient)+'</b>'
                    +'<small>'+esc(o.adres)+'</small>'
                    +(o.miasto ? '<small>'+esc(o.miasto)+'</small>' : '')
                    +'</div>'
                    +'<div class=""iw-body"">'
                    +'<div class=""iw-row""><span>Zamówienie</span><span>#'+o.id+'</span></div>'
                    +'<div class=""iw-row""><span>ID klienta</span><span>'+o.klientId+'</span></div>'
                    +'<div class=""iw-row""><span>Kod pocztowy</span><span>'+esc(o.kod)+'</span></div>'
                    +'<div class=""iw-sep""></div>'
                    +'<div class=""iw-row""><span>Pojemniki</span><span style=""color:#4F46E5;font-size:14px;"">'+o.pojemniki+'</span></div>'
                    +'<div class=""iw-row""><span>Palety</span><span>'+o.palety+'</span></div>'
                    +'<div class=""iw-sep""></div>'
                    +'<div class=""iw-row""><span>Godz. przyjazdu</span><span>'+o.godz+'</span></div>'
                    +'<div class=""iw-row""><span>Data uboju</span><span>'+o.uboj+'</span></div>'
                    +(o.handlowiec ? '<div class=""iw-sep""></div><div class=""iw-badge"">&#128100; '+esc(o.handlowiec)+'</div>' : '')
                    +'</div></div>';
                infoWindow.setContent(html);
                infoWindow.open(map, m);
            });
        })(o, pos, i);
    }

    if(orders.length > 0) map.fitBounds(bounds, 60);
}

function esc(s){return s?s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/'/g,'&#39;').replace(/""/g,'&quot;'):'';}
");
            sb.AppendLine("</script>");
            sb.Append($"<script async defer src=\"https://maps.googleapis.com/maps/api/js?key={ApiKey}&callback=initMap\"></script>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        private class ZamMapItem
        {
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Adres { get; set; } = "";
            public string Miasto { get; set; } = "";
            public string Kod { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public decimal Palety { get; set; }
            public int Pojemniki { get; set; }
            public DateTime DataPrzyjazdu { get; set; }
            public DateTime DataUboju { get; set; }
            public double? Lat { get; set; }
            public double? Lng { get; set; }
        }
    }
}
