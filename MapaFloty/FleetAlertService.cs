using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Centralny serwis alertów floty — spóźnienia, nieplanowane postoje, prędkość, powroty
    /// Odpytuje Webfleet API i porównuje z planem kursów
    /// </summary>
    public class FleetAlertService
    {
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly string _conn = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const double BazaLat = 51.86857, BazaLon = 19.79476, BazaR = 1500;

        public List<FleetAlert> Alerts { get; } = new();

        // ══════════════════════════════════════════════════════════════════
        // Główna metoda — sprawdź wszystkie alerty
        // ══════════════════════════════════════════════════════════════════

        public async Task CheckAllAsync()
        {
            Alerts.Clear();
            var svc = new KursMonitorService();

            // Pobierz zmapowane pojazdy + pozycje GPS
            var vehicles = new List<(string objectNo, int pojazdId, string rej)>();
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT m.WebfleetObjectNo, m.PojazdID, p.Rejestracja
                    FROM WebfleetVehicleMapping m INNER JOIN Pojazd p ON p.PojazdID=m.PojazdID
                    WHERE m.WebfleetObjectNo IS NOT NULL AND m.PojazdID IS NOT NULL AND p.Aktywny=1";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    vehicles.Add((r["WebfleetObjectNo"]?.ToString() ?? "", Convert.ToInt32(r["PojazdID"]), r["Rejestracja"]?.ToString() ?? ""));
            }
            catch { return; }

            // GPS równolegle
            var gpsTasks = vehicles.Select(v => (v, task: svc.PobierzPozycjeAsync(v.objectNo))).ToList();
            try { await Task.WhenAll(gpsTasks.Select(t => t.task)); } catch { }

            var gpsMap = new Dictionary<int, (double lat, double lon, int speed, string address)>();
            foreach (var (v, task) in gpsTasks)
                if (task.IsCompletedSuccessfully && task.Result.HasValue)
                    gpsMap[v.pojazdId] = task.Result.Value;

            // Dzisiejsze kursy
            var kursy = new List<(long kursId, int pojazdId, string trasa, TimeSpan? godzWyj, TimeSpan? godzPow, string kierowca)>();
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT k.KursID, k.PojazdID, k.Trasa, k.GodzWyjazdu, k.GodzPowrotu,
                    CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca
                    FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                    WHERE k.DataKursu=CAST(GETDATE() AS DATE) AND k.PojazdID IS NOT NULL";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    kursy.Add((r.GetInt64(0), Convert.ToInt32(r["PojazdID"]),
                        r["Trasa"]?.ToString() ?? "", r["GodzWyjazdu"] == DBNull.Value ? null : (TimeSpan?)r["GodzWyjazdu"],
                        r["GodzPowrotu"] == DBNull.Value ? null : (TimeSpan?)r["GodzPowrotu"],
                        r["Kierowca"]?.ToString() ?? ""));
            }
            catch { }

            var now = DateTime.Now;

            foreach (var kurs in kursy)
            {
                var rej = vehicles.FirstOrDefault(v => v.pojazdId == kurs.pojazdId).rej ?? $"Pojazd {kurs.pojazdId}";

                // 1. Alert: nie wyjechał o planowanej godzinie
                if (kurs.godzWyj.HasValue && now.TimeOfDay > kurs.godzWyj.Value.Add(TimeSpan.FromMinutes(15)))
                {
                    if (gpsMap.TryGetValue(kurs.pojazdId, out var gps) && HavM(gps.lat, gps.lon, BazaLat, BazaLon) < BazaR && gps.speed < 5)
                    {
                        var spoznienie = (int)(now.TimeOfDay - kurs.godzWyj.Value).TotalMinutes;
                        Alerts.Add(new FleetAlert
                        {
                            Type = AlertType.NieWyjechał,
                            Severity = spoznienie > 30 ? AlertSeverity.Krytyczny : AlertSeverity.Ostrzeżenie,
                            Vehicle = rej, Message = $"Nie wyjechał — plan {kurs.godzWyj:hh\\:mm}, spóźnienie {spoznienie} min",
                            Detail = $"Kurs: {kurs.trasa} | Kier: {kurs.kierowca}",
                            Time = now
                        });
                    }
                }

                // 5. Alert: nie wrócił do bazy
                if (kurs.godzPow.HasValue && now.TimeOfDay > kurs.godzPow.Value.Add(TimeSpan.FromMinutes(30)))
                {
                    if (gpsMap.TryGetValue(kurs.pojazdId, out var gps) && HavM(gps.lat, gps.lon, BazaLat, BazaLon) > BazaR)
                    {
                        var spoznienie = (int)(now.TimeOfDay - kurs.godzPow.Value).TotalMinutes;
                        var dist = HavM(gps.lat, gps.lon, BazaLat, BazaLon) / 1000.0;
                        Alerts.Add(new FleetAlert
                        {
                            Type = AlertType.NieWrócił,
                            Severity = spoznienie > 60 ? AlertSeverity.Krytyczny : AlertSeverity.Ostrzeżenie,
                            Vehicle = rej, Message = $"Nie wrócił — plan {kurs.godzPow:hh\\:mm}, {dist:F0} km od bazy",
                            Detail = $"Kurs: {kurs.trasa} | Kier: {kurs.kierowca} | {gps.address}",
                            Time = now
                        });
                    }
                }
            }

            // 4. Kontrola prędkości — kto jedzie > 90 km/h TERAZ
            foreach (var (v, task) in gpsTasks)
            {
                if (!task.IsCompletedSuccessfully || !task.Result.HasValue) continue;
                var pos = task.Result.Value;
                if (pos.speed > 90)
                {
                    var rej = vehicles.First(x => x.objectNo == v.objectNo).rej;
                    Alerts.Add(new FleetAlert
                    {
                        Type = AlertType.Prędkość,
                        Severity = pos.speed > 120 ? AlertSeverity.Krytyczny : AlertSeverity.Ostrzeżenie,
                        Vehicle = rej, Message = $"Przekroczenie prędkości: {pos.speed} km/h",
                        Detail = pos.address, Time = now
                    });
                }
            }

            // 3. Nieplanowane postoje — showStandStills, postój > 15 min poza klientem i bazą
            try
            {
                foreach (var v in vehicles)
                {
                    var url = $"{WfUrl}?account={Uri.EscapeDataString(WfAccount)}&apikey={Uri.EscapeDataString(WfKey)}" +
                        $"&lang=pl&outputformat=json&action=showStandStills&objectno={Uri.EscapeDataString(v.objectNo)}" +
                        $"&useISO8601=true&rangefrom_string={now:yyyy-MM-dd}T00:00:00&rangeto_string={now:yyyy-MM-dd}T23:59:59";
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
                    using var res = await _http.SendAsync(req);
                    var body = await res.Content.ReadAsStringAsync();
                    if (!body.TrimStart().StartsWith("[")) continue;
                    var arr = JArray.Parse(body);
                    foreach (var o in arr)
                    {
                        DateTime.TryParse(o["start_time"]?.ToString(), out var st);
                        DateTime.TryParse(o["end_time"]?.ToString(), out var et);
                        if (et < now.AddMinutes(-5)) continue; // stary postój — skip
                        var lat = (o["latitude"]?.Value<double>() ?? 0) / 1e6;
                        var lon = (o["longitude"]?.Value<double>() ?? 0) / 1e6;
                        if (lat == 0) continue;
                        var dist = HavM(lat, lon, BazaLat, BazaLon);
                        if (dist < BazaR) continue; // na bazie — OK
                        var durMin = (int)(et - st).TotalMinutes;
                        if (durMin < 15) continue; // krótki — OK

                        // Sprawdź czy to jest u klienta (z kursu) — jeśli tak to OK
                        var isAtClient = false;
                        foreach (var k in kursy.Where(x => x.pojazdId == v.pojazdId))
                        {
                            // Sprawdź adresy klientów kursu — uproszczone
                            isAtClient = true; // zakładamy że postój przy kursie jest planowany
                            break;
                        }
                        if (isAtClient && durMin < 60) continue; // u klienta, normalny rozładunek

                        var addr = o["postext"]?.ToString() ?? "";
                        Alerts.Add(new FleetAlert
                        {
                            Type = AlertType.NieplanowanyPostój,
                            Severity = durMin > 30 ? AlertSeverity.Ostrzeżenie : AlertSeverity.Info,
                            Vehicle = v.rej,
                            Message = $"Nieplanowany postój {durMin} min",
                            Detail = $"{addr} | {st:HH:mm}—{et:HH:mm} | {dist / 1000:F0} km od bazy",
                            Time = st
                        });
                    }
                }
            }
            catch { }

            // Kursy bez pojazdu
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Kurs WHERE DataKursu=CAST(GETDATE() AS DATE) AND PojazdID IS NULL";
                var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (cnt > 0)
                    Alerts.Add(new FleetAlert
                    {
                        Type = AlertType.BrakPojazdu,
                        Severity = AlertSeverity.Ostrzeżenie,
                        Vehicle = "", Message = $"{cnt} kursów bez przypisanego pojazdu",
                        Detail = "Otwórz Planowanie Transportu i przypisz pojazdy", Time = now
                    });
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════
        // Quick stats (dla sidepanelu Menu)
        // ══════════════════════════════════════════════════════════════════

        public async Task<FleetQuickStats> GetQuickStatsAsync()
        {
            var stats = new FleetQuickStats();
            var svc = new KursMonitorService();

            try
            {
                // Zmapowane pojazdy
                var vehicles = new List<(string objectNo, int pojazdId)>();
                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using var chk = conn.CreateCommand();
                    chk.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name='WebfleetVehicleMapping'";
                    if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0) return stats;

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT WebfleetObjectNo, PojazdID FROM WebfleetVehicleMapping WHERE PojazdID IS NOT NULL AND WebfleetObjectNo IS NOT NULL";
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        vehicles.Add((r["WebfleetObjectNo"]?.ToString() ?? "", Convert.ToInt32(r["PojazdID"])));
                }
                stats.TotalVehicles = vehicles.Count;

                // GPS równolegle
                var tasks = vehicles.Select(v => svc.PobierzPozycjeAsync(v.objectNo)).ToList();
                try { await Task.WhenAll(tasks); } catch { }
                foreach (var t in tasks)
                {
                    if (!t.IsCompletedSuccessfully || !t.Result.HasValue) continue;
                    var p = t.Result.Value;
                    if (HavM(p.lat, p.lon, BazaLat, BazaLon) < BazaR) stats.AtBase++;
                    else if (p.speed > 3) stats.InTransit++;
                    else stats.AtStop++;
                }

                // Kursy
                using (var conn = new SqlConnection(_conn))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM Kurs WHERE DataKursu=CAST(GETDATE() AS DATE)";
                    stats.TotalCourses = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    using var cmd2 = conn.CreateCommand();
                    cmd2.CommandText = "SELECT COUNT(*) FROM Kurs WHERE DataKursu=CAST(GETDATE() AS DATE) AND PojazdID IS NULL";
                    stats.PendingCourses = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
                }
            }
            catch { }
            return stats;
        }

        private static double HavM(double a1, double o1, double a2, double o2)
        {
            var dA = (a2 - a1) * Math.PI / 180; var dO = (o2 - o1) * Math.PI / 180;
            var x = Math.Sin(dA / 2) * Math.Sin(dA / 2) + Math.Cos(a1 * Math.PI / 180) * Math.Cos(a2 * Math.PI / 180) * Math.Sin(dO / 2) * Math.Sin(dO / 2);
            return 6371000 * 2 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(1 - x));
        }

        // ══════════════════════════════════════════════════════════════════
        // Modele
        // ══════════════════════════════════════════════════════════════════

        public enum AlertType { NieWyjechał, NieWrócił, Prędkość, NieplanowanyPostój, BrakPojazdu, Spóźnienie }
        public enum AlertSeverity { Info, Ostrzeżenie, Krytyczny }

        public class FleetAlert
        {
            public AlertType Type { get; set; }
            public AlertSeverity Severity { get; set; }
            public string Vehicle { get; set; } = "";
            public string Message { get; set; } = "";
            public string Detail { get; set; } = "";
            public DateTime Time { get; set; }

            public string Icon => Type switch
            {
                AlertType.NieWyjechał => "\u26A0",
                AlertType.NieWrócił => "\U0001F6A8",
                AlertType.Prędkość => "\U0001F3CE",
                AlertType.NieplanowanyPostój => "\u23F8",
                AlertType.BrakPojazdu => "\u2753",
                AlertType.Spóźnienie => "\u23F0",
                _ => "\u2139"
            };

            public string SeverityColor => Severity switch
            {
                AlertSeverity.Krytyczny => "#c62828",
                AlertSeverity.Ostrzeżenie => "#e65100",
                _ => "#1565c0"
            };
        }

        public class FleetQuickStats
        {
            public int TotalVehicles { get; set; }
            public int AtBase { get; set; }
            public int InTransit { get; set; }
            public int AtStop { get; set; }
            public int TotalCourses { get; set; }
            public int PendingCourses { get; set; }

            public string Summary
            {
                get
                {
                    var parts = new List<string>();
                    if (AtBase > 0) parts.Add($"{AtBase} baza");
                    if (InTransit > 0) parts.Add($"{InTransit} trasa");
                    if (AtStop > 0) parts.Add($"{AtStop} postój");
                    var line = string.Join(" | ", parts);
                    if (TotalCourses > 0) line += $" | {TotalCourses} kursów";
                    if (PendingCourses > 0) line += $" ({PendingCourses} bez pojazdu!)";
                    return line;
                }
            }
        }
    }
}
