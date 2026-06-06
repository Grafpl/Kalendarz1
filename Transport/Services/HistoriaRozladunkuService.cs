// ════════════════════════════════════════════════════════════════════════════
// Transport/Services/HistoriaRozladunkuService.cs
//
// Uczy się czasu rozładunku per klient z historii GPS Webfleet (showTracks).
//
// Algorytm:
//   1. Dla każdego zmapowanego pojazdu, pobierz tracks z ostatnich N dni
//   2. Dla każdego trackpoint: jeśli stoi (speed≤5) i jest ≤2 km od klienta z GPS,
//      kontynuuj/rozpocznij „wizytę" u tego klienta
//   3. Gdy pojazd odjedzie albo zmieni klienta — zamknij wizytę
//   4. Filtruj: wizyta < 5 min (krótki postój) lub > 240 min (anomalia, nocowanie)
//   5. Per klient: oblicz medianę z wszystkich wizyt → UPSERT EstymacjeRozladunku
//
// Tabela EstymacjeRozladunku:
//   KlientId INT PRIMARY KEY,
//   MinutyMediana INT NOT NULL,
//   LiczbaProb INT NOT NULL,
//   OstatniRefresh DATETIME NOT NULL
//
// Konsument: CzasRozladunkuService.PobierzCzasyAsync używa tej tabeli jako
// drugiego źródła (po ręcznym ustawieniu w karcie odbiorcy).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Kalendarz1.Webfleet;

namespace Kalendarz1.Transport.Services
{
    public class HistoriaRozladunkuService
    {
        // ── Stałe ────────────────────────────────────────────────────────────
        // Filtr „realnej wizyty"
        private const int MinWizytaMin = 5;     // krótszy postój = przelot / korek
        private const int MaxWizytaMin = 240;   // dłuższy = nocowanie / anomalia
        // Filtr „realnego odcinka jazdy" (B2)
        private const int MinOdcinekMin = 5;    // <5 min = klient blisko, w sąsiednim bloku
        private const int MaxOdcinekMin = 300;  // >5h = przerwa nocna lub long-haul
        // Bliskość klienta (linia prosta, bez RoadFactor — to próg „jesteśmy na miejscu")
        private const double PromienKlientaKm = 2.0;
        // Próg „stoi" (≤ km/h)
        private const int ProgStoiKmh = 5;
        // Minimalna liczba wizyt aby zaufać medianie
        public const int MinProbDoZaufania = 3;
        // Baza Koziołki — id=0 w EstymacjeTras
        public const int BazaLokId = 0;
        private const double BazaLat = 51.86857;
        private const double BazaLon = 19.79476;

        private readonly string _connLibra;
        private readonly string _connTransport;

        private static readonly string _connLibraDefault =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connTransportDefault =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public HistoriaRozladunkuService() : this(_connLibraDefault, _connTransportDefault) { }
        public HistoriaRozladunkuService(string connLibra, string connTransport)
        {
            _connLibra = connLibra;
            _connTransport = connTransport;
        }

        // ════════════════════════════════════════════════════════════════════
        // Schema
        // ════════════════════════════════════════════════════════════════════

        public async Task EnsureTableAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EstymacjeRozladunku')
                BEGIN
                    CREATE TABLE dbo.EstymacjeRozladunku (
                        KlientId INT NOT NULL PRIMARY KEY,
                        MinutyMediana INT NOT NULL,
                        LiczbaProb INT NOT NULL,
                        OstatniRefresh DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END

                -- B2: tabela czasów jazdy odcinek↔odcinek
                -- LokalizacjaA/B = KlientId z Sage, 0 = baza Koziołki
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EstymacjeTras')
                BEGIN
                    CREATE TABLE dbo.EstymacjeTras (
                        LokalizacjaA INT NOT NULL,
                        LokalizacjaB INT NOT NULL,
                        MinutyMediana INT NOT NULL,
                        LiczbaProb INT NOT NULL,
                        OstatniRefresh DATETIME NOT NULL DEFAULT GETDATE(),
                        PRIMARY KEY (LokalizacjaA, LokalizacjaB)
                    );
                END";
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        // Public API
        // ════════════════════════════════════════════════════════════════════

        public class WynikOdswiezenia
        {
            public int PojazdowPrzetworzonych { get; set; }
            public int DniPrzetworzonych { get; set; }
            public int WizytaWykrytych { get; set; }
            public int KlientowZestymowanych { get; set; }
            public int OdcinkowZestymowanych { get; set; }   // B2
            public List<string> Bledy { get; } = new();
        }

        public class EstymacjaInfo
        {
            public int MinutyMediana { get; set; }
            public int LiczbaProb { get; set; }
            public DateTime OstatniRefresh { get; set; }
        }

        /// <summary>
        /// Główna metoda — pobiera tracks z Webfleet dla wszystkich zmapowanych pojazdów,
        /// wykrywa wizyty, agreguje per klient, zapisuje medianę.
        /// </summary>
        public async Task<WynikOdswiezenia> OdswiezAsync(int daysBack, IProgress<string>? progress = null)
        {
            var wynik = new WynikOdswiezenia();
            await EnsureTableAsync();

            // 1. Pobierz zmapowane pojazdy
            var pojazdy = await PobierzZmapowanePojazdyAsync();
            if (pojazdy.Count == 0)
            {
                wynik.Bledy.Add("Brak zmapowanych pojazdów w TransportPL.WebfleetVehicleMapping.");
                return wynik;
            }
            progress?.Report($"Pojazdów do przetworzenia: {pojazdy.Count}");

            // 2. Pobierz GPS klientów (raz, do reuse w pętli)
            var klienciGps = await PobierzKlientowZGpsAsync();
            if (klienciGps.Count == 0)
            {
                wynik.Bledy.Add("Brak klientów z GPS w KartotekaOdbiorcyDane. Geokoduj w Mapie Klientów.");
                return wynik;
            }
            progress?.Report($"Klientów z GPS: {klienciGps.Count}");

            // 3. Dla każdego pojazdu × każdego dnia — pobierz tracks i wykryj wizyty + odcinki
            var wizytyAgregowane = new Dictionary<int, List<int>>();                    // klientId → minuty
            var odcinkiAgregowane = new Dictionary<(int A, int B), List<int>>();        // (A, B) → minuty

            var dataDo = DateTime.Today;
            for (int d = 0; d < daysBack; d++)
            {
                var data = dataDo.AddDays(-d);
                wynik.DniPrzetworzonych++;
                foreach (var (pojazdId, objectNo) in pojazdy)
                {
                    progress?.Report($"Pojazd {objectNo}, dzień {data:yyyy-MM-dd}...");
                    try
                    {
                        var tracks = await PobierzTracksAsync(objectNo, data);
                        if (tracks.Count == 0) continue;

                        // Wizyty u klientów
                        var wizyty = WykryjWizyty(tracks, klienciGps);
                        foreach (var w in wizyty)
                        {
                            if (!wizytyAgregowane.TryGetValue(w.KlientId, out var lista))
                                wizytyAgregowane[w.KlientId] = lista = new List<int>();
                            lista.Add(w.Minuty);
                            wynik.WizytaWykrytych++;
                        }

                        // B2: odcinki jazdy pomiędzy wizytami + baza→pierwsza i ostatnia→baza
                        var odcinki = WykryjOdcinki(tracks, wizyty);
                        foreach (var o in odcinki)
                        {
                            var klucz = (o.A, o.B);
                            if (!odcinkiAgregowane.TryGetValue(klucz, out var lista))
                                odcinkiAgregowane[klucz] = lista = new List<int>();
                            lista.Add(o.Minuty);
                        }
                    }
                    catch (Exception ex)
                    {
                        wynik.Bledy.Add($"Pojazd {objectNo} dzień {data:yyyy-MM-dd}: {ex.Message}");
                    }
                }
                wynik.PojazdowPrzetworzonych = pojazdy.Count;
            }

            // 4. Per klient — mediana rozładunku + UPSERT
            foreach (var (klientId, czasy) in wizytyAgregowane)
            {
                if (czasy.Count < 1) continue;
                int mediana = Mediana(czasy);
                if (mediana < MinWizytaMin) continue;
                await ZapiszEstymacjeAsync(klientId, mediana, czasy.Count);
                wynik.KlientowZestymowanych++;
            }

            // 5. B2: per odcinek — mediana czasu jazdy + UPSERT do EstymacjeTras
            foreach (var (klucz, czasy) in odcinkiAgregowane)
            {
                if (czasy.Count < 1) continue;
                int mediana = Mediana(czasy);
                if (mediana < MinOdcinekMin || mediana > MaxOdcinekMin) continue;
                await ZapiszOdcinekAsync(klucz.A, klucz.B, mediana, czasy.Count);
                wynik.OdcinkowZestymowanych++;
            }

            progress?.Report($"Gotowe. Wykryto {wynik.WizytaWykrytych} wizyt u {wynik.KlientowZestymowanych} klientów, " +
                             $"{wynik.OdcinkowZestymowanych} odcinków jazdy.");
            return wynik;
        }

        /// <summary>Pobiera estymację dla pojedynczego klienta — do UI karty odbiorcy.</summary>
        public async Task<EstymacjaInfo?> PobierzAsync(int klientId)
        {
            await EnsureTableAsync();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT MinutyMediana, LiczbaProb, OstatniRefresh FROM dbo.EstymacjeRozladunku WHERE KlientId = @id", cn);
                cmd.Parameters.AddWithValue("@id", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return new EstymacjaInfo
                    {
                        MinutyMediana = rd.GetInt32(0),
                        LiczbaProb = rd.GetInt32(1),
                        OstatniRefresh = rd.GetDateTime(2)
                    };
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Historia.Pobierz {klientId}] {ex.Message}"); }
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers prywatne
        // ════════════════════════════════════════════════════════════════════

        public record TrackPoint(double Lat, double Lon, int Speed, DateTime Time);
        public record Wizyta(int KlientId, int Minuty, DateTime Start, DateTime Koniec);

        private async Task<List<(int PojazdID, string ObjectNo)>> PobierzZmapowanePojazdyAsync()
        {
            var lista = new List<(int, string)>();
            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT PojazdID, WebfleetObjectNo FROM dbo.WebfleetVehicleMapping WHERE WebfleetObjectNo IS NOT NULL", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    lista.Add((rd.GetInt32(0), rd.GetString(1)));
            }
            catch (Exception ex) { Debug.WriteLine($"[Historia.Pojazdy] {ex.Message}"); }
            return lista;
        }

        private async Task<Dictionary<int, (double Lat, double Lon)>> PobierzKlientowZGpsAsync()
        {
            var wynik = new Dictionary<int, (double, double)>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    @"SELECT IdSymfonia, Latitude, Longitude
                      FROM dbo.KartotekaOdbiorcyDane
                      WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    double lat = Convert.ToDouble(rd.GetValue(1));
                    double lon = Convert.ToDouble(rd.GetValue(2));
                    if (lat != 0 && lon != 0) wynik[id] = (lat, lon);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Historia.Klienci] {ex.Message}"); }
            return wynik;
        }

        /// <summary>Pobiera tracks z Webfleet dla pojazdu w danym dniu.</summary>
        public async Task<List<TrackPoint>> PobierzTracksAsync(string objectNo, DateTime data)
        {
            var dataStr = data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var url = WebfleetHttp.BuildUrlBase("showTracks")
                + $"&objectno={Uri.EscapeDataString(objectNo)}&useISO8601=true"
                + $"&rangefrom_string={dataStr}T00:00:00&rangeto_string={dataStr}T23:59:59";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = WebfleetHttp.BasicAuthHeader();
            using var res = await WebfleetHttp.Instance.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            // Webfleet zwraca błędy w formacie tekstowym (nie JSON)
            if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith("["))
                return new List<TrackPoint>();

            var raw = JsonConvert.DeserializeObject<List<WfTrackRaw>>(body) ?? new();
            var lista = new List<TrackPoint>(raw.Count);
            foreach (var t in raw)
            {
                double lat = (t.latitude_mdeg != 0 ? t.latitude_mdeg : t.latitude) / 1e6;
                double lon = (t.longitude_mdeg != 0 ? t.longitude_mdeg : t.longitude) / 1e6;
                if (lat == 0 || lon == 0) continue;   // bez sygnału, pomiń
                if (!DateTime.TryParse(t.pos_time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    continue;
                lista.Add(new TrackPoint(lat, lon, t.speed, time));
            }
            // Posortuj chronologicznie — Webfleet zazwyczaj zwraca posortowane, ale na pewno
            return lista.OrderBy(p => p.Time).ToList();
        }

        /// <summary>
        /// Chronologiczne skanowanie tracks. Wizyta = ciągły blok punktów gdzie
        /// pojazd stoi (≤5 km/h) i jest blisko klienta (≤2 km).
        /// </summary>
        public static List<Wizyta> WykryjWizyty(List<TrackPoint> tracks, Dictionary<int, (double Lat, double Lon)> klienciGps)
        {
            var wynik = new List<Wizyta>();
            int? aktKlient = null;
            DateTime? aktStart = null;
            DateTime aktKoniec = DateTime.MinValue;

            foreach (var p in tracks)
            {
                int? klient = null;
                if (p.Speed <= ProgStoiKmh)
                {
                    // szukaj najbliższego klienta w promieniu
                    double bestDist = PromienKlientaKm;
                    foreach (var (id, gps) in klienciGps)
                    {
                        double d = HaversineKm(p.Lat, p.Lon, gps.Lat, gps.Lon);
                        if (d <= bestDist) { bestDist = d; klient = id; }
                    }
                }

                if (klient.HasValue)
                {
                    if (aktKlient == klient)
                    {
                        aktKoniec = p.Time;   // kontynuacja
                    }
                    else
                    {
                        // Zamknij poprzednią (jeśli była)
                        if (aktKlient.HasValue && aktStart.HasValue)
                            DodajJesliRealna(wynik, aktKlient.Value, aktStart.Value, aktKoniec);
                        // Otwórz nową
                        aktKlient = klient;
                        aktStart = p.Time;
                        aktKoniec = p.Time;
                    }
                }
                else
                {
                    // Punkt nie kwalifikuje się (jedzie albo poza klientami) — zamknij wizytę
                    if (aktKlient.HasValue && aktStart.HasValue)
                        DodajJesliRealna(wynik, aktKlient.Value, aktStart.Value, aktKoniec);
                    aktKlient = null;
                    aktStart = null;
                }
            }
            // Końcówka
            if (aktKlient.HasValue && aktStart.HasValue)
                DodajJesliRealna(wynik, aktKlient.Value, aktStart.Value, aktKoniec);

            return wynik;
        }

        private static void DodajJesliRealna(List<Wizyta> wynik, int klientId, DateTime start, DateTime koniec)
        {
            int min = (int)Math.Round((koniec - start).TotalMinutes);
            if (min < MinWizytaMin || min > MaxWizytaMin) return;
            wynik.Add(new Wizyta(klientId, min, start, koniec));
        }

        // ════════════════════════════════════════════════════════════════════
        // B2 — Wykrywanie odcinków jazdy między wizytami (i baza↔klient)
        // ════════════════════════════════════════════════════════════════════

        public record Odcinek(int A, int B, int Minuty);

        /// <summary>
        /// Z wizyt + tracks wyciąga odcinki jazdy:
        ///   baza → wizyta1 → wizyta2 → ... → baza
        /// Czas odcinka = (start wizyty B) - (koniec wizyty A).
        /// Pierwszy odcinek: czas od (pierwszy track w bazie) do (start wizyty 1).
        /// Ostatni odcinek: od (koniec ostatniej wizyty) do (pierwszy track w bazie po niej).
        /// </summary>
        public static List<Odcinek> WykryjOdcinki(List<TrackPoint> tracks, List<Wizyta> wizyty)
        {
            var wynik = new List<Odcinek>();
            if (wizyty.Count == 0) return wynik;
            wizyty = wizyty.OrderBy(w => w.Start).ToList();

            // Odcinki między kolejnymi wizytami
            for (int i = 0; i < wizyty.Count - 1; i++)
            {
                var a = wizyty[i];
                var b = wizyty[i + 1];
                int min = (int)Math.Round((b.Start - a.Koniec).TotalMinutes);
                if (min >= MinOdcinekMin && min <= MaxOdcinekMin)
                    wynik.Add(new Odcinek(a.KlientId, b.KlientId, min));
            }

            // Pierwszy odcinek: baza → pierwsza wizyta
            // Szukamy ostatniego momentu „w bazie" przed pierwszą wizytą
            var pierwsza = wizyty[0];
            DateTime? wyjazdZBazy = null;
            foreach (var p in tracks)
            {
                if (p.Time >= pierwsza.Start) break;
                if (HaversineKm(p.Lat, p.Lon, BazaLat, BazaLon) <= PromienKlientaKm)
                    wyjazdZBazy = p.Time;   // zapamiętuj ostatni moment w bazie
            }
            if (wyjazdZBazy.HasValue)
            {
                int min = (int)Math.Round((pierwsza.Start - wyjazdZBazy.Value).TotalMinutes);
                if (min >= MinOdcinekMin && min <= MaxOdcinekMin)
                    wynik.Add(new Odcinek(BazaLokId, pierwsza.KlientId, min));
            }

            // Ostatni odcinek: ostatnia wizyta → baza (gdy wraca tego samego dnia)
            var ostatnia = wizyty[^1];
            DateTime? przyjazdDoBazy = null;
            foreach (var p in tracks)
            {
                if (p.Time <= ostatnia.Koniec) continue;
                if (HaversineKm(p.Lat, p.Lon, BazaLat, BazaLon) <= PromienKlientaKm)
                {
                    przyjazdDoBazy = p.Time;
                    break;
                }
            }
            if (przyjazdDoBazy.HasValue)
            {
                int min = (int)Math.Round((przyjazdDoBazy.Value - ostatnia.Koniec).TotalMinutes);
                if (min >= MinOdcinekMin && min <= MaxOdcinekMin)
                    wynik.Add(new Odcinek(ostatnia.KlientId, BazaLokId, min));
            }

            return wynik;
        }

        private async Task ZapiszOdcinekAsync(int a, int b, int mediana, int liczbaProb)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.EstymacjeTras WHERE LokalizacjaA = @a AND LokalizacjaB = @b)
                    UPDATE dbo.EstymacjeTras
                    SET MinutyMediana = @m, LiczbaProb = @n, OstatniRefresh = GETDATE()
                    WHERE LokalizacjaA = @a AND LokalizacjaB = @b;
                ELSE
                    INSERT INTO dbo.EstymacjeTras (LokalizacjaA, LokalizacjaB, MinutyMediana, LiczbaProb, OstatniRefresh)
                    VALUES (@a, @b, @m, @n, GETDATE());";
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@a", a);
                cmd.Parameters.AddWithValue("@b", b);
                cmd.Parameters.AddWithValue("@m", mediana);
                cmd.Parameters.AddWithValue("@n", liczbaProb);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[Historia.Odcinek {a}→{b}] {ex.Message}"); }
        }

        /// <summary>
        /// Wszystkie odcinki z bazy do użycia w EtaService.Calculate.
        /// Zwraca słownik (A, B) → minuty (gdy LiczbaProb >= MinProbDoZaufania).
        /// </summary>
        public async Task<Dictionary<(int, int), int>> PobierzWszystkieOdcinkiAsync()
        {
            await EnsureTableAsync();
            var wynik = new Dictionary<(int, int), int>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    $"SELECT LokalizacjaA, LokalizacjaB, MinutyMediana FROM dbo.EstymacjeTras WHERE LiczbaProb >= {MinProbDoZaufania}", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    wynik[(rd.GetInt32(0), rd.GetInt32(1))] = rd.GetInt32(2);
            }
            catch (Exception ex) { Debug.WriteLine($"[Historia.PobierzOdcinki] {ex.Message}"); }
            return wynik;
        }

        private async Task ZapiszEstymacjeAsync(int klientId, int mediana, int liczbaProb)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.EstymacjeRozladunku WHERE KlientId = @id)
                    UPDATE dbo.EstymacjeRozladunku
                    SET MinutyMediana = @m, LiczbaProb = @n, OstatniRefresh = GETDATE()
                    WHERE KlientId = @id;
                ELSE
                    INSERT INTO dbo.EstymacjeRozladunku (KlientId, MinutyMediana, LiczbaProb, OstatniRefresh)
                    VALUES (@id, @m, @n, GETDATE());";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@id", klientId);
                cmd.Parameters.AddWithValue("@m", mediana);
                cmd.Parameters.AddWithValue("@n", liczbaProb);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[Historia.Zapisz {klientId}] {ex.Message}"); }
        }

        private static int Mediana(List<int> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n == 0) return 0;
            return n % 2 == 1
                ? sorted[n / 2]
                : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
        private static double ToRad(double d) => d * Math.PI / 180.0;

        private class WfTrackRaw
        {
            public double latitude_mdeg { get; set; }
            public double longitude_mdeg { get; set; }
            public double latitude { get; set; }
            public double longitude { get; set; }
            public int speed { get; set; }
            public string? pos_time { get; set; }
        }
    }
}
