// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Services/EtaNastepnyService.cs
//
// ETA świadoma kontekstu — z pozycji GPS pojazdu (Webfleet) wybiera CEL i liczy czas.
//
// REGUŁY DECYZYJNE (kolejność sprawdzania):
//   1. Kurs nie na dziś?            → HistoriaKursu (przeszłość / planowany)
//   2. Brak pojazdu/GPS/Lat=0?      → BrakDanych + diagnostyczny powód
//   3. Brak ZAM_ ładunków?          → BrakDanych
//   4. Wszyscy klienci po terminie?
//        WBazie (≤2 km od ubojni)   → WBazie         (koniec trasy)
//        !WBazie                    → DoBazy         (powrót do ubojni)
//   5. Mamy klienta-cel (pierwszy nie po terminie z GPS):
//        Pojazd ≤2 km od celu       → UKlienta       (rozładunek)
//        GodzWyjazdu > teraz + WBazie → PrzedWyjazdem (kurs jeszcze nie startował)
//        Inaczej                    → DoKlienta (zapas/spóźnienie wg awizacji)
//
// SPECIAL CASE — klient po terminie, ale pojazd właśnie do niego dojechał (≤2 km):
//   ten klient ZOSTAJE celem (UKlienta) — bufor 30 min minął, ale obsługa trwa.
//
// Reuse:
//   - KursMonitorService.PobierzPozycjeAsync(objectNo) — pozycja GPS pojazdu
//   - WebfleetOrderService.PobierzAdresySzybkoAsync(kody) — współrzędne klientów
//   - TransportPL.WebfleetVehicleMapping — PojazdID → WebfleetObjectNo
//   - Haversine + RoadFactor 1.3 + 60 km/h jak w EtaService
//
// Cache GPS 60 s + mapowania 10 min — przy 10 kursach widocznych nie ddosuje Webfleet.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MapaFloty;
using Kalendarz1.Transport;
using Kalendarz1.Transport.Services;
using Kalendarz1.Transport.WPF.Models;

namespace Kalendarz1.Transport.WPF.Services
{
    public class EtaNastepnyService
    {
        public class PozycjaGPS { public double Lat; public double Lon; public int Speed; public string Address = ""; }

        /// <summary>Wynikowy stan ETA — UI wybiera format wyświetlania na podstawie tego enum.</summary>
        public enum EtaStatus
        {
            BrakDanych,       // nie udało się policzyć — zobacz Powod
            HistoriaKursu,    // DataKursu ≠ dziś (z przeszłości lub planowany)
            PrzedWyjazdem,    // GodzWyjazdu > teraz + pojazd w bazie — kurs jeszcze nie startował
            UKlienta,         // pojazd ≤2 km od klienta-celu — rozładunek
            DoKlienta,        // jedzie/stoi w drodze do klienta
            WBazie,           // wszyscy klienci po terminie + pojazd ≤2 km od ubojni — koniec
            DoBazy            // wszyscy klienci po terminie + pojazd jeszcze poza bazą
        }

        public class WynikEta
        {
            public EtaStatus Status { get; set; } = EtaStatus.BrakDanych;
            public string Powod { get; set; } = "";

            // Pozycja pojazdu (wypełniana zawsze gdy mamy GPS — wartościowa nawet gdy ETA się nie udała)
            public string SkadAdres { get; set; } = "";
            public string SkadMiasto { get; set; } = "";
            public int Predkosc { get; set; }
            public bool Stoi { get; set; }
            public bool WBazie { get; set; }
            public double? PojazdLat { get; set; }
            public double? PojazdLon { get; set; }

            // Cel + ETA (wypełniane gdy Status != BrakDanych/HistoriaKursu)
            public string KlientNazwa { get; set; } = "—";
            public double DystansKm { get; set; }
            public TimeSpan Czas { get; set; }

            // Kontekst awizacji (dla DoKlienta i UKlienta)
            public DateTime? AwizacjaCelu { get; set; }
            public TimeSpan? Spoznienie { get; set; }   // czasDojazdu > czasDoAwizacji
            public TimeSpan? Zapas { get; set; }        // czasDoAwizacji > czasDojazdu

            // PrzedWyjazdem — ile do startu kursu
            public TimeSpan? DoStartu { get; set; }

            // Diagnostyka
            public int ObsluzonychPoTerminie { get; set; }
            public int LiczbaPrzystankow { get; set; }
        }

        // ── Stałe ────────────────────────────────────────────────────────────
        // Baza — ubojnia Piórkowscy, Koziołki 40
        private const double BazaLat = 51.86857;
        private const double BazaLon = 19.79476;
        // Promień bazy/klienta — pojazd w tym dystansie liniowym uznajemy za „na miejscu"
        // (2 km obejmuje cały plac ubojni, parking u klienta, drogę dojazdową)
        private const double PromienBliskoKm = 2.0;
        // Bufor po awizacji — uznajemy klienta za obsłużonego/przegapionego po N min
        // (typowy czas rozładunku/odbioru u nas — 30 min)
        private static readonly TimeSpan BuforPoAwizacji = TimeSpan.FromMinutes(30);
        // Próg „stoi" — Webfleet daje speed w km/h. ≤5 to manewry/postój
        private const int ProgStoiKmh = 5;
        // Wyliczanie czasu jazdy
        private const double AvgKmh = 60.0;
        private const double RoadFactor = 1.3;

        // ── Cache ────────────────────────────────────────────────────────────
        private static readonly Dictionary<string, (PozycjaGPS gps, DateTime utc)> _gpsCache = new();
        private static readonly TimeSpan GpsTtl = TimeSpan.FromSeconds(60);
        private static Dictionary<int, string>? _objectNoCache;
        private static DateTime _objectNoCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan ObjectNoTtl = TimeSpan.FromMinutes(10);

        private static readonly string _conn = TransportWpfService.ConnTransport;

        // ════════════════════════════════════════════════════════════════════
        // Publiczne pomocnicze (cache)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Mapowanie pojazdów na Webfleet objectNo (cache 10 min).</summary>
        public async Task<Dictionary<int, string>> PobierzMapowaniePojazdowAsync()
        {
            if (_objectNoCache != null && DateTime.UtcNow - _objectNoCacheUtc < ObjectNoTtl)
                return new Dictionary<int, string>(_objectNoCache);
            var map = new Dictionary<int, string>();
            try
            {
                await using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT PojazdID, WebfleetObjectNo FROM dbo.WebfleetVehicleMapping WHERE WebfleetObjectNo IS NOT NULL", cn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var pid = r.GetInt32(0);
                    var ono = r.GetString(1);
                    map[pid] = ono;
                }
                _objectNoCache = map;
                _objectNoCacheUtc = DateTime.UtcNow;
            }
            catch { }
            return new Dictionary<int, string>(map);
        }

        /// <summary>Pozycja GPS pojazdu z Webfleet (cache 60 s). null gdy brak / błąd / pojazd niezmapowany.</summary>
        public async Task<PozycjaGPS?> PobierzPozycjeAsync(string objectNo)
        {
            if (string.IsNullOrEmpty(objectNo)) return null;
            if (_gpsCache.TryGetValue(objectNo, out var cached) && DateTime.UtcNow - cached.utc < GpsTtl)
                return cached.gps;
            try
            {
                var svc = new KursMonitorService();
                var raw = await svc.PobierzPozycjeAsync(objectNo);
                if (raw == null) return null;
                var gps = new PozycjaGPS { Lat = raw.Value.lat, Lon = raw.Value.lon, Speed = raw.Value.speed, Address = raw.Value.address };
                _gpsCache[objectNo] = (gps, DateTime.UtcNow);
                return gps;
            }
            catch { return null; }
        }

        // ════════════════════════════════════════════════════════════════════
        // Główna logika — WyliczDlaKursuAsync
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wylicza WynikEta dla pojedynczego kursu. Sprawdza wszystkie scenariusze:
        /// historia/przyszłość kursu, brak GPS/danych, w bazie, u klienta, w drodze, spóźnienie, zapas.
        /// </summary>
        public async Task<WynikEta> WyliczDlaKursuAsync(
            DateTime dataKursu,
            int? pojazdId,
            TimeSpan? godzWyjazdu,
            IEnumerable<Ladunek> ladunki,
            Dictionary<int, ZamowienieNazwaInfo> info)
        {
            var wynik = new WynikEta();
            var teraz = DateTime.Now;

            // ── 1. Kurs nie na dziś? ──────────────────────────────────────────
            if (dataKursu.Date != teraz.Date)
            {
                wynik.Status = EtaStatus.HistoriaKursu;
                wynik.Powod = dataKursu.Date < teraz.Date ? "kurs zakończony" : "kurs zaplanowany";
                return wynik;
            }

            // ── 2. Pojazd + GPS ──────────────────────────────────────────────
            if (!pojazdId.HasValue) { wynik.Powod = "brak pojazdu w kursie"; return wynik; }

            var mapowanie = await PobierzMapowaniePojazdowAsync();
            if (!mapowanie.TryGetValue(pojazdId.Value, out var objectNo))
            { wynik.Powod = "pojazd niezmapowany z Webfleet"; return wynik; }

            var gps = await PobierzPozycjeAsync(objectNo);
            if (gps == null) { wynik.Powod = "brak danych z Webfleet"; return wynik; }
            // Webfleet zwraca (0,0) gdy brak sygnału (np. w garażu) lub niepoprawne dane.
            // Polskie GPS-y mają lat~49-55, lon~14-24, więc 0 zawsze oznacza błąd.
            if (gps.Lat == 0 || gps.Lon == 0) { wynik.Powod = "pojazd poza zasięgiem GPS"; return wynik; }

            WypelnijPozycjePojazdu(wynik, gps);

            // ── 3. Ładunki ZAM_ z nazwami klientów ────────────────────────────
            var stops = ZbudujListePrzystankow(ladunki, info);
            wynik.LiczbaPrzystankow = stops.Count;
            if (stops.Count == 0) { wynik.Powod = "brak ZAM_ ładunków"; return wynik; }

            // Pobierz GPS klientów (cache KlientAdres + Handel fallback)
            var ws = new WebfleetOrderService();
            var adresy = await ws.PobierzAdresySzybkoAsync(stops.Select(s => s.Kod));

            // ── 4. Wybierz cel + policz statystyki klientów ───────────────────
            var (celIdx, obsluzonych, zGps) = WybierzCel(stops, adresy, gps, teraz);
            wynik.ObsluzonychPoTerminie = obsluzonych;

            // ── 5a. Cel = klient ─────────────────────────────────────────────
            if (celIdx >= 0)
            {
                var cel = stops[celIdx];
                var adr = adresy[cel.Kod];
                WyliczDoKlienta(wynik, gps, adr, cel, teraz, godzWyjazdu);
                return wynik;
            }

            // ── 5b. Brak celu klienta ──────────────────────────────────────────
            // Czy wszyscy klienci są obsłużeni (po terminie)?
            if (obsluzonych >= stops.Count)
            {
                // Wszyscy po terminie → cel = baza
                WyliczDoBazy(wynik);
                return wynik;
            }

            // Są klienci nie obsłużeni, ale żaden bez GPS → nie wiemy dokąd
            wynik.Powod = zGps == 0
                ? $"0/{stops.Count} klientów ma GPS — uruchom Kartoteka → Mapa klientów → 📍 Geokoduj"
                : $"następni klienci ({stops.Count - obsluzonych - zGps}) bez GPS — dwuklik w Mapie Klientów";
            return wynik;
        }

        // ════════════════════════════════════════════════════════════════════
        // Sub-helpers — czystsze i testowalne
        // ════════════════════════════════════════════════════════════════════

        private static void WypelnijPozycjePojazdu(WynikEta w, PozycjaGPS gps)
        {
            w.SkadAdres = gps.Address ?? "";
            w.SkadMiasto = SkrocAdresDoMiasta(gps.Address);
            w.Predkosc = gps.Speed;
            w.Stoi = gps.Speed <= ProgStoiKmh;
            w.PojazdLat = gps.Lat;
            w.PojazdLon = gps.Lon;
            double dystOdBazy = HaversineKm(gps.Lat, gps.Lon, BazaLat, BazaLon);
            w.WBazie = dystOdBazy <= PromienBliskoKm;
        }

        private static List<Przystanek> ZbudujListePrzystankow(IEnumerable<Ladunek> ladunki, Dictionary<int, ZamowienieNazwaInfo> info)
        {
            var lista = new List<Przystanek>();
            foreach (var l in ladunki)
            {
                if (l.KodKlienta == null || !l.KodKlienta.StartsWith("ZAM_")) continue;
                if (!int.TryParse(l.KodKlienta.Substring(4), out var zid)) continue;
                if (!info.TryGetValue(zid, out var zi)) continue;
                if (string.IsNullOrEmpty(zi.Nazwa)) continue;
                lista.Add(new Przystanek(l.KodKlienta, zi.Nazwa, zi.Awizacja, l.Kolejnosc));
            }
            // Sortowanie: najpierw po awizacji (z awizacją wcześniej), potem po kolejności w planie
            return lista
                .OrderBy(s => s.Awizacja ?? DateTime.MaxValue)
                .ThenBy(s => s.Kolejnosc)
                .ToList();
        }

        /// <summary>
        /// Wybiera cel z listy przystanków. Reguły:
        ///  - klient po terminie ale pojazd ≤2 km od niego → ZOSTAJE celem (obsługa trwa)
        ///  - klient po terminie + pojazd daleko → uznany za obsłużony, pomijany
        ///  - inaczej: pierwszy klient nie po terminie z GPS staje się celem
        /// Zwraca index w `stops` (-1 gdy brak celu), liczbę obsłużonych po terminie, liczbę z GPS.
        /// </summary>
        private static (int celIdx, int obsluzonych, int zGps) WybierzCel(
            List<Przystanek> stops,
            Dictionary<string, MapaFloty.WebfleetOrderService.KlientAdresInfo> adresy,
            PozycjaGPS gps,
            DateTime teraz)
        {
            int cel = -1;
            int obsluzonych = 0;
            int zGps = 0;

            for (int i = 0; i < stops.Count; i++)
            {
                var s = stops[i];
                bool maGps = adresy.TryGetValue(s.Kod, out var a) && a.Lat != 0 && a.Lon != 0;
                if (maGps) zGps++;
                bool poTerminie = s.Awizacja.HasValue && s.Awizacja.Value + BuforPoAwizacji < teraz;

                // SPECIAL: klient po terminie + pojazd ≤2 km od niego → obsługa trwa, to nadal cel
                if (poTerminie && maGps)
                {
                    double dystKlient = HaversineKm(gps.Lat, gps.Lon, a.Lat, a.Lon);
                    if (dystKlient <= PromienBliskoKm)
                    {
                        if (cel == -1) cel = i;
                        continue;   // nie zliczamy do obsluzonych, bo właśnie obsługujemy
                    }
                }

                if (poTerminie) { obsluzonych++; continue; }

                // Nie po terminie + ma GPS → kandydat na cel
                if (maGps && cel == -1) cel = i;
            }

            return (cel, obsluzonych, zGps);
        }

        private static void WyliczDoKlienta(
            WynikEta w,
            PozycjaGPS gps,
            MapaFloty.WebfleetOrderService.KlientAdresInfo adr,
            Przystanek cel,
            DateTime teraz,
            TimeSpan? godzWyjazdu)
        {
            w.KlientNazwa = cel.Klient;
            w.AwizacjaCelu = cel.Awizacja;

            // U klienta? (pojazd ≤2 km od celu w linii prostej)
            double dystLin = HaversineKm(gps.Lat, gps.Lon, adr.Lat, adr.Lon);
            if (dystLin <= PromienBliskoKm)
            {
                w.Status = EtaStatus.UKlienta;
                w.DystansKm = 0;
                w.Czas = TimeSpan.Zero;
                return;
            }

            // Wylicz ETA (road distance + czas jazdy)
            double dystans = dystLin * RoadFactor;
            double minuty = dystans / AvgKmh * 60.0;
            w.DystansKm = dystans;
            w.Czas = TimeSpan.FromMinutes(minuty);

            // Przed wyjazdem? (kurs ma start w przyszłości i pojazd nie wyjechał z bazy)
            if (godzWyjazdu.HasValue && godzWyjazdu.Value > teraz.TimeOfDay && w.WBazie)
            {
                w.Status = EtaStatus.PrzedWyjazdem;
                w.DoStartu = godzWyjazdu.Value - teraz.TimeOfDay;
                // Zostawiamy ETA wyliczone — UI pokaże ile do startu + dystans do klienta
                return;
            }

            // Zwykła jazda do klienta — sprawdź zapas/spóźnienie wzg. awizacji
            w.Status = EtaStatus.DoKlienta;
            if (cel.Awizacja.HasValue)
            {
                var doAwizacji = cel.Awizacja.Value - teraz;
                if (doAwizacji < w.Czas)
                {
                    // Nie zdąży — spóźnienie. Może być duże (gdy awizacja w przeszłości i pojazd daleko)
                    w.Spoznienie = w.Czas - doAwizacji;
                }
                else
                {
                    w.Zapas = doAwizacji - w.Czas;
                }
            }
        }

        private static void WyliczDoBazy(WynikEta w)
        {
            w.KlientNazwa = "Baza (Koziołki)";

            if (w.WBazie)
            {
                // Już dojechał — koniec trasy
                w.Status = EtaStatus.WBazie;
                w.DystansKm = 0;
                w.Czas = TimeSpan.Zero;
                return;
            }

            // W drodze do bazy
            w.Status = EtaStatus.DoBazy;
            double dystLin = HaversineKm(w.PojazdLat ?? BazaLat, w.PojazdLon ?? BazaLon, BazaLat, BazaLon);
            double dystans = dystLin * RoadFactor;
            w.DystansKm = dystans;
            w.Czas = TimeSpan.FromMinutes(dystans / AvgKmh * 60.0);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private record Przystanek(string Kod, string Klient, DateTime? Awizacja, int Kolejnosc);

        /// <summary>
        /// Wyciąga miasto z pełnego adresu Webfleet ("ul. Piotrkowska 15, 90-001 Łódź, Polska" → "Łódź").
        /// Heuristic: ostatnia część po przecinku, bez „Polska/Poland", bez kodu pocztowego.
        /// </summary>
        public static string SkrocAdresDoMiasta(string? adres)
        {
            if (string.IsNullOrWhiteSpace(adres)) return "";
            var parts = adres.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => !p.Equals("Polska", StringComparison.OrdinalIgnoreCase)
                         && !p.Equals("Poland", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (parts.Length == 0) return adres.Trim();
            var last = parts[^1];
            // „90-001 Łódź" → „Łódź"
            var m = System.Text.RegularExpressions.Regex.Match(last, @"^\d{2}-?\d{3}\s+(.+)$");
            return m.Success ? m.Groups[1].Value : last;
        }

        public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
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
    }
}
