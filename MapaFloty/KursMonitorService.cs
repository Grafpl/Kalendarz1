using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
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
    /// Serwis monitorowania realizacji kursów na podstawie GPS
    /// Porównuje planowane przystanki z faktyczną trasą pojazdu
    /// </summary>
    public class KursMonitorService
    {
        private static readonly string _conn = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Webfleet
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly JsonSerializerSettings _js = new()
        { MissingMemberHandling = MissingMemberHandling.Ignore, Error = (_, a) => { a.ErrorContext.Handled = true; } };

        private const double DOTARL_PROMIEN_M = 500;    // < 500m = dotarł
        private const double OBSLUZONY_PROMIEN_M = 1000; // > 1km = odjechał
        private const int POSTOJ_ALERT_MIN = 15;         // nieplanowany postój > 15 min

        // ══════════════════════════════════════════════════════════════════

        public async Task EnsureTableAsync()
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='KursRealizacja')
                CREATE TABLE KursRealizacja (
                    ID int IDENTITY(1,1) PRIMARY KEY,
                    KursID bigint NOT NULL, LadunekID bigint NOT NULL,
                    KodKlienta varchar(100) NOT NULL, NazwaKlienta nvarchar(200) NULL,
                    KolejnoscPlan int NOT NULL, KolejnoscFakt int NULL,
                    Status varchar(30) NOT NULL DEFAULT 'Oczekujacy',
                    CzasDotarcia datetime2 NULL, CzasOdjazdu datetime2 NULL,
                    CzasPostojuMin int NULL, OdlegloscMinM int NULL,
                    LatKlient float NULL, LonKlient float NULL,
                    LatDotarcie float NULL, LonDotarcie float NULL,
                    ModifiedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_KR_Kurs')
                    CREATE INDEX IX_KR_Kurs ON KursRealizacja(KursID);";
            await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // Pobierz pełne dane kursu do monitorowania
        // ══════════════════════════════════════════════════════════════════

        public async Task<MonitorKurs?> PobierzKursDoMonitoruAsync(long kursId)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();

            MonitorKurs? kurs = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT k.KursID, k.Trasa, k.Status, k.DataKursu, k.GodzWyjazdu, k.GodzPowrotu, k.PojazdID,
                    CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca, p.Rejestracja
                    FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                    LEFT JOIN Pojazd p ON k.PojazdID=p.PojazdID WHERE k.KursID=@id";
                cmd.Parameters.AddWithValue("@id", kursId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    kurs = new MonitorKurs
                    {
                        KursID = r.GetInt64(0), Trasa = r["Trasa"]?.ToString() ?? "",
                        Status = r["Status"]?.ToString() ?? "", DataKursu = r.GetDateTime(3),
                        GodzWyjazdu = r["GodzWyjazdu"] == DBNull.Value ? "" : ((TimeSpan)r["GodzWyjazdu"]).ToString(@"hh\:mm"),
                        GodzPowrotu = r["GodzPowrotu"] == DBNull.Value ? "" : ((TimeSpan)r["GodzPowrotu"]).ToString(@"hh\:mm"),
                        PojazdID = r["PojazdID"] == DBNull.Value ? 0 : Convert.ToInt32(r["PojazdID"]),
                        Kierowca = r["Kierowca"]?.ToString() ?? "", Rejestracja = r["Rejestracja"]?.ToString() ?? ""
                    };
            }
            if (kurs == null) return null;

            // Ładunki + adresy (przez WebfleetOrderService)
            var svcAddr = new WebfleetOrderService();
            await svcAddr.EnsureTablesAsync();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT LadunekID, Kolejnosc, KodKlienta, PojemnikiE2, Uwagi FROM Ladunek WHERE KursID=@id ORDER BY Kolejnosc";
                cmd.Parameters.AddWithValue("@id", kursId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    kurs.Przystanki.Add(new MonitorPrzystanek
                    {
                        LadunekID = r.GetInt64(0), Kolejnosc = r.GetInt32(1),
                        KodKlienta = r["KodKlienta"]?.ToString() ?? "", PojemnikiE2 = r.GetInt32(3),
                        Uwagi = r["Uwagi"]?.ToString() ?? ""
                    });
            }

            // Pobierz adresy i GPS klientów
            var kody = kurs.Przystanki.Select(p => p.KodKlienta).ToList();
            var adresy = await svcAddr.PobierzAdresyAsync(kody);
            foreach (var p in kurs.Przystanki)
            {
                if (adresy.TryGetValue(p.KodKlienta, out var a))
                {
                    p.NazwaKlienta = a.Nazwa; p.Adres = a.PelnyAdres;
                    p.Lat = a.Lat; p.Lon = a.Lon;
                }
            }

            // Pobierz istniejące statusy realizacji
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT LadunekID, Status, CzasDotarcia, CzasOdjazdu, CzasPostojuMin, OdlegloscMinM, KolejnoscFakt FROM KursRealizacja WHERE KursID=@id";
                cmd.Parameters.AddWithValue("@id", kursId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var lid = r.GetInt64(0);
                    var p = kurs.Przystanki.FirstOrDefault(x => x.LadunekID == lid);
                    if (p != null)
                    {
                        p.Status = r["Status"]?.ToString() ?? "Oczekujacy";
                        p.CzasDotarcia = r["CzasDotarcia"] == DBNull.Value ? null : (DateTime?)r["CzasDotarcia"];
                        p.CzasOdjazdu = r["CzasOdjazdu"] == DBNull.Value ? null : (DateTime?)r["CzasOdjazdu"];
                        p.CzasPostojuMin = r["CzasPostojuMin"] == DBNull.Value ? 0 : Convert.ToInt32(r["CzasPostojuMin"]);
                        p.OdlegloscMinM = r["OdlegloscMinM"] == DBNull.Value ? 0 : Convert.ToInt32(r["OdlegloscMinM"]);
                        p.KolejnoscFakt = r["KolejnoscFakt"] == DBNull.Value ? 0 : Convert.ToInt32(r["KolejnoscFakt"]);
                    }
                }
            }

            // Webfleet objectNo
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT WebfleetObjectNo FROM WebfleetVehicleMapping WHERE PojazdID=@id";
                cmd.Parameters.AddWithValue("@id", kurs.PojazdID);
                kurs.WebfleetObjectNo = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
            }

            return kurs;
        }

        // ══════════════════════════════════════════════════════════════════
        // Aktualizuj statusy przystanków na podstawie aktualnej pozycji GPS
        // ══════════════════════════════════════════════════════════════════

        public async Task AktualizujStatusyAsync(MonitorKurs kurs, double pojazdLat, double pojazdLon, int pojazdSpeed)
        {
            await EnsureTableAsync();
            int nextFaktKolejnosc = kurs.Przystanki.Where(p => p.KolejnoscFakt > 0).Select(p => p.KolejnoscFakt).DefaultIfEmpty(0).Max() + 1;

            foreach (var p in kurs.Przystanki)
            {
                if (p.Lat == 0 || p.Lon == 0) continue;

                var distM = HaversineM(pojazdLat, pojazdLon, p.Lat, p.Lon);

                // Śledź minimalną odległość
                if (p.OdlegloscMinM == 0 || distM < p.OdlegloscMinM)
                    p.OdlegloscMinM = (int)distM;

                var oldStatus = p.Status;

                switch (p.Status)
                {
                    case "Oczekujacy":
                    case "WDrodze":
                        if (distM < DOTARL_PROMIEN_M && pojazdSpeed < 5)
                        {
                            p.Status = "Dotarl";
                            p.CzasDotarcia = DateTime.Now;
                            p.LatDotarcie = pojazdLat;
                            p.LonDotarcie = pojazdLon;
                            p.KolejnoscFakt = nextFaktKolejnosc++;
                        }
                        else if (distM < 5000)
                        {
                            p.Status = "WDrodze";
                        }
                        break;

                    case "Dotarl":
                        if (distM > OBSLUZONY_PROMIEN_M)
                        {
                            p.Status = "Obsluzony";
                            p.CzasOdjazdu = DateTime.Now;
                            if (p.CzasDotarcia.HasValue)
                                p.CzasPostojuMin = (int)(DateTime.Now - p.CzasDotarcia.Value).TotalMinutes;
                        }
                        break;
                }

                // Wykryj pominiecie — jeśli następny przystanek jest obsłużony, a ten nie
                if (p.Status == "Oczekujacy" || p.Status == "WDrodze")
                {
                    var nastepne = kurs.Przystanki.Where(x => x.Kolejnosc > p.Kolejnosc && (x.Status == "Dotarl" || x.Status == "Obsluzony")).Any();
                    if (nastepne) p.Status = "Pominiety";
                }

                // Zapisz do bazy jeśli zmieniony
                if (p.Status != oldStatus || p.OdlegloscMinM != 0)
                    await SaveRealizacja(kurs.KursID, p);
            }

            // Oblicz ETA i dystans do następnego przystanku
            var next = kurs.Przystanki.FirstOrDefault(p => p.Status == "Oczekujacy" || p.Status == "WDrodze");
            if (next != null && next.Lat != 0)
            {
                next.DystansDoKm = HaversineM(pojazdLat, pojazdLon, next.Lat, next.Lon) / 1000.0;
                next.EtaMin = pojazdSpeed > 5 ? (int)Math.Ceiling(next.DystansDoKm / pojazdSpeed * 60) : -1;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Pobierz aktualną pozycję pojazdu z Webfleet
        // ══════════════════════════════════════════════════════════════════

        public async Task<(double lat, double lon, int speed, string address)?> PobierzPozycjeAsync(string objectNo)
        {
            if (string.IsNullOrEmpty(objectNo)) return null;
            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showObjectReportExtern&objectno={U(objectNo)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (body.TrimStart().StartsWith("["))
            {
                var arr = JArray.Parse(body);
                if (arr.Count > 0)
                {
                    var o = arr[0];
                    var lat = (o["latitude_mdeg"]?.Value<double>() ?? 0) / 1e6;
                    var lon = (o["longitude_mdeg"]?.Value<double>() ?? 0) / 1e6;
                    var spd = o["speed"]?.Value<int>() ?? 0;
                    var addr = o["postext"]?.ToString() ?? "";
                    return (lat, lon, spd, addr);
                }
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // Pobierz listę kursów na dany dzień (do monitora)
        // ══════════════════════════════════════════════════════════════════

        public async Task<List<MonitorKursRow>> PobierzKursyNaDzienAsync(DateTime data)
        {
            await EnsureTableAsync();
            var result = new List<MonitorKursRow>();
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT k.KursID, k.Trasa, k.Status, k.GodzWyjazdu, k.PojazdID,
                    CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca, p.Rejestracja,
                    (SELECT COUNT(*) FROM Ladunek l WHERE l.KursID=k.KursID) AS Ladunkow,
                    (SELECT COUNT(*) FROM KursRealizacja r WHERE r.KursID=k.KursID AND r.Status='Obsluzony') AS Obsluzonych,
                    (SELECT COUNT(*) FROM KursRealizacja r WHERE r.KursID=k.KursID AND r.Status='Pominiety') AS Pominietych
                FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                LEFT JOIN Pojazd p ON k.PojazdID=p.PojazdID
                WHERE k.DataKursu=@d ORDER BY k.GodzWyjazdu";
            cmd.Parameters.AddWithValue("@d", data.Date);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var ladunkow = Convert.ToInt32(r["Ladunkow"]);
                var obsluzonych = Convert.ToInt32(r["Obsluzonych"]);
                var pominietych = Convert.ToInt32(r["Pominietych"]);
                result.Add(new MonitorKursRow
                {
                    KursID = r.GetInt64(0), Trasa = r["Trasa"]?.ToString() ?? "",
                    Status = r["Status"]?.ToString() ?? "", GodzWyjazdu = r["GodzWyjazdu"] == DBNull.Value ? "" : ((TimeSpan)r["GodzWyjazdu"]).ToString(@"hh\:mm"),
                    PojazdID = r["PojazdID"] == DBNull.Value ? 0 : Convert.ToInt32(r["PojazdID"]),
                    Kierowca = r["Kierowca"]?.ToString() ?? "", Rejestracja = r["Rejestracja"]?.ToString() ?? "",
                    Ladunkow = ladunkow, Obsluzonych = obsluzonych, Pominietych = pominietych,
                    Postep = ladunkow > 0 ? (int)(obsluzonych * 100.0 / ladunkow) : 0
                });
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════════

        public async Task SaveRealizacjaPublic(long kursId, MonitorPrzystanek p) => await SaveRealizacja(kursId, p);

        private async Task SaveRealizacja(long kursId, MonitorPrzystanek p)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE KursRealizacja AS t
                USING (SELECT @kid AS KursID, @lid AS LadunekID) AS s ON t.KursID=s.KursID AND t.LadunekID=s.LadunekID
                WHEN MATCHED THEN UPDATE SET Status=@st, CzasDotarcia=@cd, CzasOdjazdu=@co, CzasPostojuMin=@cp,
                    OdlegloscMinM=@om, KolejnoscFakt=@kf, LatDotarcie=@latd, LonDotarcie=@lond, ModifiedAtUTC=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (KursID,LadunekID,KodKlienta,NazwaKlienta,KolejnoscPlan,KolejnoscFakt,Status,
                    CzasDotarcia,CzasOdjazdu,CzasPostojuMin,OdlegloscMinM,LatKlient,LonKlient,LatDotarcie,LonDotarcie)
                    VALUES (@kid,@lid,@kk,@nk,@kp,@kf,@st,@cd,@co,@cp,@om,@latk,@lonk,@latd,@lond);";
            cmd.Parameters.AddWithValue("@kid", kursId);
            cmd.Parameters.AddWithValue("@lid", p.LadunekID);
            cmd.Parameters.AddWithValue("@kk", p.KodKlienta);
            cmd.Parameters.AddWithValue("@nk", (object?)p.NazwaKlienta ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@kp", p.Kolejnosc);
            cmd.Parameters.AddWithValue("@kf", p.KolejnoscFakt > 0 ? p.KolejnoscFakt : DBNull.Value);
            cmd.Parameters.AddWithValue("@st", p.Status);
            cmd.Parameters.AddWithValue("@cd", (object?)p.CzasDotarcia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@co", (object?)p.CzasOdjazdu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cp", p.CzasPostojuMin > 0 ? p.CzasPostojuMin : DBNull.Value);
            cmd.Parameters.AddWithValue("@om", p.OdlegloscMinM > 0 ? p.OdlegloscMinM : DBNull.Value);
            cmd.Parameters.AddWithValue("@latk", p.Lat != 0 ? p.Lat : DBNull.Value);
            cmd.Parameters.AddWithValue("@lonk", p.Lon != 0 ? p.Lon : DBNull.Value);
            cmd.Parameters.AddWithValue("@latd", p.LatDotarcie != 0 ? p.LatDotarcie : DBNull.Value);
            cmd.Parameters.AddWithValue("@lond", p.LonDotarcie != 0 ? p.LonDotarcie : DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private static double HaversineM(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180; var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static string U(string s) => Uri.EscapeDataString(s);

        // ══════════════════════════════════════════════════════════════════
        // Modele
        // ══════════════════════════════════════════════════════════════════

        public class MonitorKurs
        {
            public long KursID { get; set; }
            public string Trasa { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime DataKursu { get; set; }
            public string GodzWyjazdu { get; set; } = "";
            public string GodzPowrotu { get; set; } = "";
            public int PojazdID { get; set; }
            public string Kierowca { get; set; } = "";
            public string Rejestracja { get; set; } = "";
            public string WebfleetObjectNo { get; set; } = "";
            public List<MonitorPrzystanek> Przystanki { get; set; } = new();
        }

        public class MonitorPrzystanek
        {
            public long LadunekID { get; set; }
            public int Kolejnosc { get; set; }
            public string KodKlienta { get; set; } = "";
            public int PojemnikiE2 { get; set; }
            public string Uwagi { get; set; } = "";
            public string NazwaKlienta { get; set; } = "";
            public string Adres { get; set; } = "";
            public double Lat { get; set; }
            public double Lon { get; set; }

            // Realizacja
            public string Status { get; set; } = "Oczekujacy";
            public DateTime? CzasDotarcia { get; set; }
            public DateTime? CzasOdjazdu { get; set; }
            public int CzasPostojuMin { get; set; }
            public int OdlegloscMinM { get; set; }
            public int KolejnoscFakt { get; set; }
            public double LatDotarcie { get; set; }
            public double LonDotarcie { get; set; }

            // Live
            public double DystansDoKm { get; set; }
            public int EtaMin { get; set; }

            public string StatusIkona => Status switch
            {
                "Obsluzony" => "\u2705",  // ✅
                "Dotarl" => "\uD83D\uDE9B",  // 🚛
                "WDrodze" => "\u27A1",     // ➡
                "Pominiety" => "\u274C",   // ❌
                _ => "\u23F3"              // ⏳
            };

            public string StatusNazwa => Status switch
            {
                "Obsluzony" => "Obsłużony",
                "Dotarl" => "Na miejscu",
                "WDrodze" => "W drodze",
                "Pominiety" => "Pominięty",
                _ => "Oczekujący"
            };
        }

        public class MonitorKursRow
        {
            public long KursID { get; set; }
            public string Trasa { get; set; } = "";
            public string Status { get; set; } = "";
            public string GodzWyjazdu { get; set; } = "";
            public int PojazdID { get; set; }
            public string Kierowca { get; set; } = "";
            public string Rejestracja { get; set; } = "";
            public int Ladunkow { get; set; }
            public int Obsluzonych { get; set; }
            public int Pominietych { get; set; }
            public int Postep { get; set; } // 0-100%
        }
    }
}
