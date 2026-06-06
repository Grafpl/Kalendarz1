// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Services/EtaNastepnyService.cs
// ETA świadoma czasu — z pozycji GPS pojazdu do CELU:
//   • cel = pierwszy klient którego (awizacja + 30 min) jeszcze nie minęła
//   • gdy wszyscy klienci po terminie → cel = baza (Koziołki, ubojnia)
// + kontekst: zapas czasu / spóźnienie względem awizacji, status stoi/jedzie.
//
// Reuse:
//  - KursMonitorService.PobierzPozycjeAsync(objectNo) — pozycja GPS pojazdu z Webfleet
//  - WebfleetOrderService.PobierzAdresySzybkoAsync(kody) — współrzędne klientów (cache KlientAdres)
//  - tabela TransportPL.WebfleetVehicleMapping — PojazdID → WebfleetObjectNo
//  - Haversine + RoadFactor 1.3 + 60 km/h jak w EtaService
//
// Cache GPS 60 s — żeby przy 10 kursach widocznych nie pukać do Webfleet 10x co odświeżenie.
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
        public class WynikEta
        {
            public string KlientNazwa { get; set; } = "—";   // nazwa celu (klient lub „Baza")
            public double DystansKm { get; set; }
            public TimeSpan Czas { get; set; }               // czas dojazdu z GPS do celu
            public bool MaDane { get; set; }
            public string Powod { get; set; } = "";
            public string SkadAdres { get; set; } = "";
            public string SkadMiasto { get; set; } = "";
            public int Predkosc { get; set; }
            public double? PojazdLat { get; set; }
            public double? PojazdLon { get; set; }

            // Kontekst czasowy — logika „cel zależy od awizacji vs teraz"
            public bool CelToBaza { get; set; }              // true → wszyscy klienci po terminie, cel = ubojnia
            public int ObsluzonychPoTerminie { get; set; }   // ile klientów już za czasem (do diagnostyki)
            public DateTime? AwizacjaCelu { get; set; }      // godzina awizacji klienta-celu (null gdy CelToBaza)
            public TimeSpan? Spoznienie { get; set; }        // czasDojazdu > czasDoAwizacji → spóźniony o ...
            public TimeSpan? Zapas { get; set; }             // czasDoAwizacji > czasDojazdu → zapas ...
            public bool Stoi { get; set; }                   // Predkosc <= 5 km/h
            public bool WBazie { get; set; }                 // pojazd ≤ 2 km od ubojni Koziołki
        }

        // Promień bazy — pojazd w tym dystansie liniowym uznajemy za „w bazie"
        // (manewry, plac, parking — nie ma sensu liczyć ETA „do bazy")
        private const double PromienBazyKm = 2.0;

        // Baza — ubojnia Piórkowscy w Koziołkach 40 (powrót po wszystkich klientach)
        private const double BazaLat = 51.86857;
        private const double BazaLon = 19.79476;
        // Bufor po awizacji — uznajemy klienta za obsłużonego/spóźnionego dopiero gdy minęło N min od awizacji
        // 30 min to typowy czas rozładunku/odbioru u nas
        private static readonly TimeSpan BuforPoAwizacji = TimeSpan.FromMinutes(30);

        private const double AvgKmh = 60.0;
        private const double RoadFactor = 1.3;

        // Cache pozycji GPS per objectNo, 60 s TTL — przy refresh listy nie ddosuje Webfleet
        private static readonly Dictionary<string, (PozycjaGPS gps, DateTime utc)> _gpsCache = new();
        private static readonly TimeSpan GpsTtl = TimeSpan.FromSeconds(60);

        // Cache mapowania PojazdID → WebfleetObjectNo (rzadko się zmienia)
        private static Dictionary<int, string>? _objectNoCache;
        private static DateTime _objectNoCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan ObjectNoTtl = TimeSpan.FromMinutes(10);

        private static readonly string _conn = TransportWpfService.ConnTransport;

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

        /// <summary>Pozycja GPS pojazdu z Webfleet (cache 60 s). null gdy brak danych / pojazd niezmapowany.</summary>
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

        /// <summary>
        /// Liczy ETA z pozycji GPS pojazdu (jeśli zmapowany i ma pozycję) do następnego klienta
        /// (pierwszy ZAM_ w Kolejnosc, którego współrzędne znamy). Bez powrotu do bazy.
        /// </summary>
        public async Task<WynikEta> WyliczDlaKursuAsync(
            int? pojazdId,
            IEnumerable<Ladunek> ladunki,
            Dictionary<int, ZamowienieNazwaInfo> info)
        {
            var wynik = new WynikEta();
            if (!pojazdId.HasValue) { wynik.Powod = "brak pojazdu"; return wynik; }

            var mapowanie = await PobierzMapowaniePojazdowAsync();
            if (!mapowanie.TryGetValue(pojazdId.Value, out var objectNo))
            { wynik.Powod = "pojazd niezmapowany z Webfleet"; return wynik; }

            var gps = await PobierzPozycjeAsync(objectNo);
            if (gps == null) { wynik.Powod = "brak pozycji GPS"; return wynik; }

            // Już teraz wypełniamy „skąd jest pojazd" — wartościowe dla planisty nawet jeśli ETA dalej się nie uda
            wynik.SkadAdres = gps.Address ?? "";
            wynik.SkadMiasto = SkrocAdresDoMiasta(gps.Address);
            wynik.Predkosc = gps.Speed;
            wynik.Stoi = gps.Speed <= 5;
            wynik.PojazdLat = gps.Lat;
            wynik.PojazdLon = gps.Lon;

            // Pojazd w bazie? (≤2 km od ubojni Koziołki w linii prostej, bez RoadFactor)
            double dystOdBazy = HaversineKm(gps.Lat, gps.Lon, BazaLat, BazaLon);
            wynik.WBazie = dystOdBazy <= PromienBazyKm;

            // Budujemy listę przystanków: nazwa + kod + awizacja (z ZamowienieNazwaInfo) + kolejność.
            // Sortujemy po Awizacji (jeśli jest), inaczej po Kolejnosc — to odpowiada faktycznemu planowi dnia.
            var stops = new List<(string Kod, string Klient, DateTime? Awizacja, int Kolejnosc)>();
            foreach (var l in ladunki)
            {
                if (l.KodKlienta == null || !l.KodKlienta.StartsWith("ZAM_")) continue;
                if (!int.TryParse(l.KodKlienta.Substring(4), out var zid)) continue;
                if (!info.TryGetValue(zid, out var zi)) continue;
                if (string.IsNullOrEmpty(zi.Nazwa)) continue;
                stops.Add((l.KodKlienta, zi.Nazwa, zi.Awizacja, l.Kolejnosc));
            }
            if (stops.Count == 0) { wynik.Powod = "brak ZAM_ ładunków"; return wynik; }

            stops = stops
                .OrderBy(s => s.Awizacja ?? DateTime.MaxValue)   // klienci z awizacją najpierw, w kolejności godzin
                .ThenBy(s => s.Kolejnosc)                         // klienci bez awizacji — w kolejności w planie
                .ToList();

            // Pobierz współrzędne klientów (cache KlientAdres + Handel fallback)
            var ws = new WebfleetOrderService();
            var adresy = await ws.PobierzAdresySzybkoAsync(stops.Select(s => s.Kod));

            // Wybór celu — pierwszy klient którego awizacja+30min jeszcze nie minęła I ma GPS.
            // Brak awizacji = traktujemy jak „dziś, nadal do obsługi" (najczęściej tak właśnie jest).
            // Klient po terminie + bez GPS → pominięty (planista nie ma jak go obsłużyć).
            var teraz = DateTime.Now;
            int obsluzonychPoTerminie = 0;
            int zGps = 0;
            (string Kod, string Klient, DateTime? Awizacja, int Kolejnosc)? celKlient = null;

            foreach (var s in stops)
            {
                bool maGps = adresy.TryGetValue(s.Kod, out var a) && a.Lat != 0 && a.Lon != 0;
                if (maGps) zGps++;

                // Po terminie? Awizacja + 30 min minęła → już obsłużony / przegapiony.
                bool poTerminie = s.Awizacja.HasValue && s.Awizacja.Value + BuforPoAwizacji < teraz;
                if (poTerminie) { obsluzonychPoTerminie++; continue; }

                // Nie po terminie + ma GPS → pierwszy taki staje się celem
                if (maGps && celKlient == null) celKlient = s;
            }

            wynik.ObsluzonychPoTerminie = obsluzonychPoTerminie;

            // Wszyscy klienci po terminie → cel = baza (powrót do ubojni)
            if (celKlient == null && obsluzonychPoTerminie == stops.Count)
            {
                wynik.CelToBaza = true;
                wynik.KlientNazwa = "Baza (Koziołki)";
                wynik.MaDane = true;

                if (wynik.WBazie)
                {
                    // Pojazd już dojechał — koniec trasy, nie liczymy ETA
                    wynik.DystansKm = 0;
                    wynik.Czas = TimeSpan.Zero;
                }
                else
                {
                    // W drodze do bazy — liczymy ETA
                    double dystBaza = dystOdBazy * RoadFactor;
                    wynik.DystansKm = dystBaza;
                    wynik.Czas = TimeSpan.FromMinutes(dystBaza / AvgKmh * 60.0);
                }
                return wynik;
            }

            // Ktoś jest celem ale nie ma GPS — diagnostyka
            if (celKlient == null)
            {
                wynik.Powod = zGps == 0
                    ? $"0/{stops.Count} klientów ma GPS — uruchom Kartoteka → Mapa klientów → 📍 Geokoduj"
                    : $"następny klient bez GPS — dwuklik na nim w Mapie Klientów";
                return wynik;
            }

            // Mamy cel — wylicz dystans + czas + kontekst awizacji
            var adr = adresy[celKlient.Value.Kod];
            double dystans = HaversineKm(gps.Lat, gps.Lon, adr.Lat, adr.Lon) * RoadFactor;
            double minuty = dystans / AvgKmh * 60.0;
            var czasDojazdu = TimeSpan.FromMinutes(minuty);

            wynik.KlientNazwa = celKlient.Value.Klient;
            wynik.DystansKm = dystans;
            wynik.Czas = czasDojazdu;
            wynik.AwizacjaCelu = celKlient.Value.Awizacja;
            wynik.MaDane = true;

            // Kontekst czasowy — zapas vs spóźnienie
            if (celKlient.Value.Awizacja.HasValue)
            {
                var doAwizacji = celKlient.Value.Awizacja.Value - teraz;
                if (doAwizacji < czasDojazdu)
                {
                    // Pojazd nie zdąży na czas
                    wynik.Spoznienie = czasDojazdu - doAwizacji;
                }
                else
                {
                    // Zapas — wystarczy czasu, plus rezerwa
                    wynik.Zapas = doAwizacji - czasDojazdu;
                }
            }

            return wynik;
        }

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
