// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Services/EtaNastepnyService.cs
// ETA z pozycji GPS pojazdu (Webfleet) do następnego klienta w kursie.
// NIE wylicza powrotu do bazy — wyłącznie czas dojazdu do kolejnego przystanku.
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
            public string KlientNazwa { get; set; } = "—";
            public double DystansKm { get; set; }
            public TimeSpan Czas { get; set; }
            public bool MaDane { get; set; }      // false → brak GPS pojazdu / brak współrzędnych klienta / brak następnego
            public string Powod { get; set; } = "";   // diagnostyka gdy MaDane=false
        }

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

            // Lista (Klient, KodKlienta) po Kolejnosc, tylko z dostępnymi nazwami
            var stops = new List<(string Kod, string Klient)>();
            foreach (var l in ladunki.OrderBy(x => x.Kolejnosc))
            {
                if (l.KodKlienta == null || !l.KodKlienta.StartsWith("ZAM_")) continue;
                if (!int.TryParse(l.KodKlienta.Substring(4), out var zid)) continue;
                if (!info.TryGetValue(zid, out var zi)) continue;
                if (string.IsNullOrEmpty(zi.Nazwa)) continue;
                stops.Add((l.KodKlienta, zi.Nazwa));
            }
            if (stops.Count == 0) { wynik.Powod = "brak ZAM_ ładunków"; return wynik; }

            // Pobierz współrzędne klientów ładunków (cache KlientAdres + Handel fallback)
            var ws = new WebfleetOrderService();
            var adresy = await ws.PobierzAdresySzybkoAsync(stops.Select(s => s.Kod));

            // Znajdź pierwszy klient z dostępnym GPS, którego pojazd JESZCZE nie odwiedził (>500 m od jego punktu)
            const double PROG_ODWIEDZONY_KM = 0.5;
            foreach (var s in stops)
            {
                if (!adresy.TryGetValue(s.Kod, out var a) || a.Lat == 0 || a.Lon == 0) continue;
                double dystans = HaversineKm(gps.Lat, gps.Lon, a.Lat, a.Lon) * RoadFactor;
                if (dystans < PROG_ODWIEDZONY_KM) continue;   // pojazd jest u tego klienta → bierz następnego
                // To jest nasz cel — pierwszy nieodwiedzony klient po kolejności
                var minuty = dystans / AvgKmh * 60.0;
                wynik.KlientNazwa = s.Klient;
                wynik.DystansKm = dystans;
                wynik.Czas = TimeSpan.FromMinutes(minuty);
                wynik.MaDane = true;
                return wynik;
            }

            wynik.Powod = "brak współrzędnych klientów lub wszyscy w pobliżu";
            return wynik;
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
