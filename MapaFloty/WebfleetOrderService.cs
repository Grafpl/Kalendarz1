using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Serwis synchronizacji kursów transportowych z Webfleet.connect
    /// Wysyła zlecenia dostawy (destination orders) na nawigacje TomTom kierowców
    /// </summary>
    public class WebfleetOrderService
    {
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly string _conn =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ══════════════════════════════════════════════════════════════════
        // Inicjalizacja tabel
        // ══════════════════════════════════════════════════════════════════

        public async Task EnsureTablesAsync()
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='KlientAdres')
                CREATE TABLE KlientAdres (
                    KodKlienta varchar(100) NOT NULL PRIMARY KEY,
                    NazwaKlienta nvarchar(200) NULL,
                    Ulica nvarchar(200) NULL, Miasto nvarchar(100) NULL,
                    KodPocztowy varchar(10) NULL, Kraj varchar(2) NULL DEFAULT 'PL',
                    Latitude float NULL, Longitude float NULL,
                    GeokodowanyUTC datetime2 NULL,
                    ModifiedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    ModifiedBy nvarchar(64) NULL);

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='WebfleetOrderSync')
                CREATE TABLE WebfleetOrderSync (
                    SyncID int IDENTITY(1,1) PRIMARY KEY,
                    KursID bigint NOT NULL, WebfleetOrderId varchar(30) NOT NULL,
                    WebfleetObjectNo varchar(20) NULL,
                    Status varchar(30) NOT NULL DEFAULT 'Oczekujacy',
                    WyslanoUTC datetime2 NULL, OdpowiedzKod varchar(10) NULL,
                    OdpowiedzMsg nvarchar(500) NULL, IloscPrzystankow int NULL,
                    CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy nvarchar(64) NULL,
                    CONSTRAINT UQ_WOS_Kurs UNIQUE (KursID));";
            await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // Pobierz kurs z ładunkami
        // ══════════════════════════════════════════════════════════════════

        public async Task<KursDoSync> PobierzKursAsync(long kursId)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();

            // Kurs
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT k.KursID, k.DataKursu, k.Trasa, k.Status,
                k.GodzWyjazdu, k.GodzPowrotu, k.PojazdID,
                CONCAT(ki.Imie,' ',ki.Nazwisko) AS KierowcaNazwa, p.Rejestracja
                FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                LEFT JOIN Pojazd p ON k.PojazdID=p.PojazdID WHERE k.KursID=@id";
            cmd.Parameters.AddWithValue("@id", kursId);

            KursDoSync? kurs = null;
            using (var r = await cmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    kurs = new KursDoSync
                    {
                        KursID = r.GetInt64(0),
                        DataKursu = r.GetDateTime(1),
                        Trasa = r["Trasa"]?.ToString() ?? "",
                        Status = r["Status"]?.ToString() ?? "",
                        GodzWyjazdu = r["GodzWyjazdu"] == DBNull.Value ? null : ((TimeSpan)r["GodzWyjazdu"]).ToString(@"hh\:mm"),
                        PojazdID = r["PojazdID"] == DBNull.Value ? 0 : Convert.ToInt32(r["PojazdID"]),
                        Kierowca = r["KierowcaNazwa"]?.ToString() ?? "",
                        Rejestracja = r["Rejestracja"]?.ToString() ?? ""
                    };
                }
            }
            if (kurs == null) throw new Exception($"Kurs {kursId} nie istnieje");

            // Ładunki
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = @"SELECT LadunekID, Kolejnosc, KodKlienta, PojemnikiE2, Uwagi
                FROM Ladunek WHERE KursID=@id ORDER BY Kolejnosc";
            cmd2.Parameters.AddWithValue("@id", kursId);
            using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync())
            {
                kurs.Ladunki.Add(new LadunekDoSync
                {
                    LadunekID = r2.GetInt64(0),
                    Kolejnosc = r2.GetInt32(1),
                    KodKlienta = r2["KodKlienta"]?.ToString() ?? "",
                    PojemnikiE2 = r2.GetInt32(3),
                    Uwagi = r2["Uwagi"]?.ToString() ?? ""
                });
            }
            return kurs;
        }

        private static readonly string _connHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private static readonly string _connLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ══════════════════════════════════════════════════════════════════
        // Szybkie pobieranie adresów — BEZ geokodowania Nominatim (dla TransportWindow)
        // ══════════════════════════════════════════════════════════════════

        public async Task<Dictionary<string, KlientAdresInfo>> PobierzAdresySzybkoAsync(IEnumerable<string> kodyKlientow)
        {
            var result = new Dictionary<string, KlientAdresInfo>();
            var brakujace = new List<string>();

            // 1) Cache
            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();
                foreach (var kod in kodyKlientow.Distinct())
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT NazwaKlienta, Ulica, Miasto, KodPocztowy, Kraj, Latitude, Longitude FROM KlientAdres WHERE KodKlienta=@k";
                    cmd.Parameters.AddWithValue("@k", kod);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        result[kod] = new KlientAdresInfo
                        {
                            KodKlienta = kod, Nazwa = r["NazwaKlienta"]?.ToString() ?? kod,
                            Ulica = r["Ulica"]?.ToString() ?? "", Miasto = r["Miasto"]?.ToString() ?? "",
                            KodPocztowy = r["KodPocztowy"]?.ToString() ?? "", Kraj = r["Kraj"]?.ToString() ?? "PL",
                            Lat = r["Latitude"] == DBNull.Value ? 0 : Convert.ToDouble(r["Latitude"]),
                            Lon = r["Longitude"] == DBNull.Value ? 0 : Convert.ToDouble(r["Longitude"])
                        };
                    }
                    else brakujace.Add(kod);
                }
            }
            if (brakujace.Count == 0) return result;

            // 2) Batch z Handel — BEZ Nominatim
            foreach (var kod in brakujace)
            {
                try
                {
                    int? klientId = null;
                    if (kod.StartsWith("ZAM_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(kod.Substring(4), out var zamId)) continue;
                        using var cL = new SqlConnection(_connLibra);
                        await cL.OpenAsync();
                        using var cmdZ = cL.CreateCommand();
                        cmdZ.CommandText = "SELECT KlientId FROM dbo.ZamowieniaMieso WHERE Id=@z";
                        cmdZ.Parameters.AddWithValue("@z", zamId);
                        var rv = await cmdZ.ExecuteScalarAsync();
                        if (rv != null && rv != DBNull.Value) klientId = Convert.ToInt32(rv);
                    }
                    if (klientId == null) continue;

                    using var cH = new SqlConnection(_connHandel);
                    await cH.OpenAsync();
                    using var cmd = cH.CreateCommand();
                    cmd.CommandText = @"SELECT TOP 1 c.Name,
                        ISNULL(a.Street,'') + CASE WHEN a.HouseNo IS NOT NULL THEN ' '+a.HouseNo ELSE '' END AS Ulica,
                        ISNULL(a.Place,'') AS Miasto, ISNULL(a.PostCode,'') AS Kod
                        FROM [SSCommon].[STContractors] c
                        LEFT JOIN [SSCommon].[STPostOfficeAddresses] a ON a.ContactGuid=c.ContactGuid
                        WHERE c.Id=@kid";
                    cmd.Parameters.AddWithValue("@kid", klientId.Value);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        var info = new KlientAdresInfo
                        {
                            KodKlienta = kod, Nazwa = r["Name"]?.ToString() ?? kod,
                            Ulica = r["Ulica"]?.ToString() ?? "", Miasto = r["Miasto"]?.ToString() ?? "",
                            KodPocztowy = r["Kod"]?.ToString() ?? "", Kraj = "PL"
                        };
                        // GPS z KartotekaOdbiorcyDane (bez Nominatim)
                        try
                        {
                            using var cL2 = new SqlConnection(_connLibra);
                            await cL2.OpenAsync();
                            using var cmdG = cL2.CreateCommand();
                            cmdG.CommandText = "SELECT Latitude, Longitude FROM KartotekaOdbiorcyDane WHERE IdSymfonia=@id AND Latitude IS NOT NULL AND Latitude!=0";
                            cmdG.Parameters.AddWithValue("@id", klientId.Value);
                            using var rG = await cmdG.ExecuteReaderAsync();
                            if (await rG.ReadAsync()) { info.Lat = Convert.ToDouble(rG["Latitude"]); info.Lon = Convert.ToDouble(rG["Longitude"]); }
                        }
                        catch { }
                        await ZapiszAdresAsync(info, "auto-fast");
                        result[kod] = info;
                    }
                }
                catch { }
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        // Pełne pobieranie adresów — z geokodowaniem Nominatim
        // ══════════════════════════════════════════════════════════════════

        public async Task<Dictionary<string, KlientAdresInfo>> PobierzAdresyAsync(IEnumerable<string> kodyKlientow)
        {
            var result = new Dictionary<string, KlientAdresInfo>();
            var brakujace = new List<string>();

            // 1) Sprawdź cache (KlientAdres w TransportPL)
            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();
                foreach (var kod in kodyKlientow.Distinct())
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT NazwaKlienta, Ulica, Miasto, KodPocztowy, Kraj, Latitude, Longitude FROM KlientAdres WHERE KodKlienta=@k";
                    cmd.Parameters.AddWithValue("@k", kod);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync() && r["Latitude"] != DBNull.Value && Convert.ToDouble(r["Latitude"]) != 0)
                    {
                        result[kod] = new KlientAdresInfo
                        {
                            KodKlienta = kod,
                            Nazwa = r["NazwaKlienta"]?.ToString() ?? kod,
                            Ulica = r["Ulica"]?.ToString() ?? "",
                            Miasto = r["Miasto"]?.ToString() ?? "",
                            KodPocztowy = r["KodPocztowy"]?.ToString() ?? "",
                            Kraj = r["Kraj"]?.ToString() ?? "PL",
                            Lat = Convert.ToDouble(r["Latitude"]),
                            Lon = Convert.ToDouble(r["Longitude"])
                        };
                    }
                    else
                        brakujace.Add(kod);
                }
            }

            if (brakujace.Count == 0) return result;

            // 2) Dla każdego brakującego KodKlienta:
            //    ZAM_xxx → LibraNet.ZamowieniaMieso(KlientId) → Handel.STContractors → STPostOfficeAddresses
            //    Inne → bezpośrednio Handel.STContractors
            //    OSOBNE połączenia (brak linked server na LibraNet)

            foreach (var kod in brakujace.ToList())
            {
                try
                {
                    int? klientId = null;

                    // ── Krok A: wyciągnij KlientId ──
                    if (kod.StartsWith("ZAM_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(kod.Substring(4), out var zamId)) continue;
                        using var cL = new SqlConnection(_connLibra);
                        await cL.OpenAsync();
                        using var cmdZ = cL.CreateCommand();
                        cmdZ.CommandText = "SELECT KlientId FROM dbo.ZamowieniaMieso WHERE Id=@z";
                        cmdZ.Parameters.AddWithValue("@z", zamId);
                        var rv = await cmdZ.ExecuteScalarAsync();
                        if (rv != null && rv != DBNull.Value) klientId = Convert.ToInt32(rv);
                    }
                    else if (int.TryParse(kod, out var numId))
                    {
                        klientId = numId;
                    }

                    // Szukaj po Shortcut jeśli nie liczbowy
                    if (klientId == null && !kod.StartsWith("ZAM_", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var cH = new SqlConnection(_connHandel);
                            await cH.OpenAsync();
                            using var cmdS = cH.CreateCommand();
                            cmdS.CommandText = "SELECT TOP 1 Id FROM [SSCommon].[STContractors] WHERE Shortcut=@k";
                            cmdS.Parameters.AddWithValue("@k", kod);
                            var rv = await cmdS.ExecuteScalarAsync();
                            if (rv != null && rv != DBNull.Value) klientId = Convert.ToInt32(rv);
                        }
                        catch { }
                    }

                    if (klientId == null) continue;

                    // ── Krok B: adres z Handel (osobne połączenie) ──
                    // STContractors: Id, Name, Shortcut, ContactGuid
                    // STPostOfficeAddresses: Street, Place, PostCode, HouseNo, ApartmentNo, ContactGuid
                    // Join przez ContactGuid (nie ContractorId, brak IsDefault)
                    using var cH2 = new SqlConnection(_connHandel);
                    await cH2.OpenAsync();
                    using var cmd = cH2.CreateCommand();
                    cmd.CommandText = @"SELECT TOP 1 c.Id, c.Name,
                            ISNULL(a.Street,'') + CASE WHEN a.HouseNo IS NOT NULL THEN ' ' + a.HouseNo ELSE '' END
                                + CASE WHEN a.ApartmentNo IS NOT NULL THEN '/' + a.ApartmentNo ELSE '' END AS Ulica,
                            ISNULL(a.Place, '') AS Miasto,
                            ISNULL(a.PostCode, '') AS KodPocztowy
                        FROM [SSCommon].[STContractors] c
                        LEFT JOIN [SSCommon].[STPostOfficeAddresses] a ON a.ContactGuid = c.ContactGuid
                        WHERE c.Id = @kid";
                    cmd.Parameters.AddWithValue("@kid", klientId.Value);

                    var info = new KlientAdresInfo { KodKlienta = kod, Kraj = "PL" };
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            info.Nazwa = r["Name"]?.ToString() ?? kod;
                            info.Ulica = r["Ulica"]?.ToString() ?? "";
                            info.Miasto = r["Miasto"]?.ToString() ?? "";
                            info.KodPocztowy = r["KodPocztowy"]?.ToString() ?? "";
                        }
                        else continue;
                    }

                    // ── Krok C: GPS z KartotekaOdbiorcyDane (LibraNet) ──
                    try
                    {
                        using var cL2 = new SqlConnection(_connLibra);
                        await cL2.OpenAsync();
                        using var cmdG = cL2.CreateCommand();
                        cmdG.CommandText = "SELECT Latitude, Longitude FROM KartotekaOdbiorcyDane WHERE IdSymfonia=@id AND Latitude IS NOT NULL AND Latitude!=0";
                        cmdG.Parameters.AddWithValue("@id", klientId.Value);
                        using var rG = await cmdG.ExecuteReaderAsync();
                        if (await rG.ReadAsync())
                        { info.Lat = Convert.ToDouble(rG["Latitude"]); info.Lon = Convert.ToDouble(rG["Longitude"]); }
                    }
                    catch { }

                    // ── Krok D: auto-geokodowanie Nominatim ──
                    if (info.Lat == 0 && !string.IsNullOrWhiteSpace(info.Miasto))
                    {
                        try
                        {
                            var (lat, lon) = await GeokodujAdresAsync(info.Ulica ?? "", info.Miasto, info.KodPocztowy);
                            info.Lat = lat; info.Lon = lon;
                            await Task.Delay(1100);
                        }
                        catch { }
                    }

                    // ── Krok E: zapisz do cache ──
                    await ZapiszAdresAsync(info, "auto");
                    result[kod] = info;
                }
                catch { continue; }
            }

            return result;
        }

        public async Task ZapiszAdresAsync(KlientAdresInfo adres, string user)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE KlientAdres AS t USING (SELECT @k AS KodKlienta) AS s ON t.KodKlienta=s.KodKlienta
                WHEN MATCHED THEN UPDATE SET NazwaKlienta=@n, Ulica=@u, Miasto=@m, KodPocztowy=@kp, Kraj=@kr,
                    Latitude=@lat, Longitude=@lon, GeokodowanyUTC=@geo, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@user
                WHEN NOT MATCHED THEN INSERT (KodKlienta,NazwaKlienta,Ulica,Miasto,KodPocztowy,Kraj,Latitude,Longitude,GeokodowanyUTC,ModifiedBy)
                    VALUES (@k,@n,@u,@m,@kp,@kr,@lat,@lon,@geo,@user);";
            cmd.Parameters.AddWithValue("@k", adres.KodKlienta);
            cmd.Parameters.AddWithValue("@n", (object?)adres.Nazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", (object?)adres.Ulica ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@m", (object?)adres.Miasto ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@kp", (object?)adres.KodPocztowy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@kr", (object?)adres.Kraj ?? "PL");
            cmd.Parameters.AddWithValue("@lat", adres.Lat != 0 ? adres.Lat : DBNull.Value);
            cmd.Parameters.AddWithValue("@lon", adres.Lon != 0 ? adres.Lon : DBNull.Value);
            cmd.Parameters.AddWithValue("@geo", adres.Lat != 0 ? DateTime.UtcNow : DBNull.Value);
            cmd.Parameters.AddWithValue("@user", user ?? "system");
            await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // Geocoding via Nominatim (OpenStreetMap)
        // ══════════════════════════════════════════════════════════════════

        public async Task<(double lat, double lon)> GeokodujAdresAsync(string ulica, string miasto, string kodPocztowy)
        {
            var query = $"{ulica}, {kodPocztowy} {miasto}, Polska";
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1&countrycodes=pl";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "MapaFloty/1.0 (kontakt@piorkowscy.com.pl)");
            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            var arr = JArray.Parse(json);
            if (arr.Count > 0)
            {
                var lat = arr[0]["lat"]?.Value<double>() ?? 0;
                var lon = arr[0]["lon"]?.Value<double>() ?? 0;
                return (lat, lon);
            }
            return (0, 0);
        }

        // ══════════════════════════════════════════════════════════════════
        // Wysyłanie kursu do Webfleet (sendDestinationOrderExtern)
        // ══════════════════════════════════════════════════════════════════

        public async Task<SyncResult> WyslijKursAsync(long kursId, string user)
        {
            await EnsureTablesAsync();
            var kurs = await PobierzKursAsync(kursId);
            if (kurs.PojazdID <= 0) throw new Exception("Kurs nie ma przypisanego pojazdu");
            if (kurs.Ladunki.Count == 0) throw new Exception("Kurs nie ma ładunków (przystanków)");

            // Znajdź objectNo Webfleet dla pojazdu
            var objectNo = await FindWebfleetObjectNo(kurs.PojazdID);
            if (string.IsNullOrEmpty(objectNo))
                throw new Exception($"Pojazd (ID={kurs.PojazdID}, rej. {kurs.Rejestracja}) nie jest zmapowany z Webfleet. Użyj mapowania pojazdów.");

            // Pobierz adresy klientów
            var kody = kurs.Ladunki.Select(l => l.KodKlienta).ToList();
            var adresy = await PobierzAdresyAsync(kody);

            // Sprawdź czy wszystkie adresy mają współrzędne
            var brakGeo = new List<string>();
            foreach (var l in kurs.Ladunki)
            {
                if (!adresy.TryGetValue(l.KodKlienta, out var a) || a.Lat == 0 || a.Lon == 0)
                    brakGeo.Add(l.KodKlienta);
            }
            if (brakGeo.Count > 0)
                throw new NeedAddressException($"Brak adresów/współrzędnych dla: {string.Join(", ", brakGeo)}", brakGeo);

            // Buduj orderId
            var orderId = $"K{kursId}-{DateTime.Now:yyMMddHHmm}";

            // Pierwszy przystanek jako główny cel, reszta jako waypoints
            var firstStop = kurs.Ladunki.First();
            var firstAddr = adresy[firstStop.KodKlienta];

            // Tekst zlecenia z listą przystanków
            var sb = new StringBuilder();
            sb.AppendLine($"Kurs: {kurs.Trasa}");
            sb.AppendLine($"Data: {kurs.DataKursu:dd.MM.yyyy} | Wyjazd: {kurs.GodzWyjazdu ?? "—"}");
            sb.AppendLine($"Kierowca: {kurs.Kierowca} | Pojazd: {kurs.Rejestracja}");
            sb.AppendLine($"Przystanki: {kurs.Ladunki.Count}");
            foreach (var l in kurs.Ladunki)
            {
                var a = adresy.GetValueOrDefault(l.KodKlienta);
                sb.AppendLine($"  {l.Kolejnosc}. {a?.Nazwa ?? l.KodKlienta} — {l.PojemnikiE2} E2 {(string.IsNullOrEmpty(l.Uwagi) ? "" : $"({l.Uwagi})")}");
            }
            var orderText = sb.ToString();
            if (orderText.Length > 1000) orderText = orderText[..997] + "...";

            // insertDestinationOrderExtern — tworzy zlecenie w Webfleet BEZ wysyłania na urządzenie
            // objectno opcjonalny — linkuje z pojazdem dla ETA
            var pars = new Dictionary<string, string>
            {
                ["account"] = WfAccount,
                ["apikey"] = WfKey,
                ["lang"] = "pl",
                ["outputformat"] = "json",
                ["action"] = "insertDestinationOrderExtern",
                ["orderid"] = orderId,
                ["ordertext"] = orderText,
                ["ordertype"] = "3", // delivery
                ["longitude"] = ((int)(firstAddr.Lon * 1e6)).ToString(),
                ["latitude"] = ((int)(firstAddr.Lat * 1e6)).ToString(),
                ["country"] = "PL",
                ["city"] = firstAddr.Miasto,
                ["street"] = firstAddr.Ulica,
                ["zip"] = firstAddr.KodPocztowy,
                ["contact"] = firstAddr.Nazwa
            };

            // NIE przypisujemy objectno — insert tworzy zlecenie w systemie Webfleet
            // bez wysyłania na urządzenie (unika błędu 2605 na jednostkach bez terminala PRO)
            // Pojazd można przypisać później przez assignOrderExtern lub w panelu Webfleet

            // Czas zlecenia
            if (!string.IsNullOrEmpty(kurs.GodzWyjazdu))
            {
                pars["orderdate"] = kurs.DataKursu.ToString("dd.MM.yyyy");
                pars["ordertime"] = kurs.GodzWyjazdu;
            }

            // Buduj URL z parametrami (POST body)
            var postBody = string.Join("&", pars.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

            // Dodaj waypoints dla kolejnych przystanków
            for (int i = 1; i < kurs.Ladunki.Count; i++)
            {
                var l = kurs.Ladunki[i];
                var a = adresy[l.KodKlienta];
                var wpLat = (int)(a.Lat * 1e6);
                var wpLon = (int)(a.Lon * 1e6);
                var wpDesc = $"{l.Kolejnosc}. {a.Nazwa} ({l.PojemnikiE2} E2)";
                if (!string.IsNullOrEmpty(l.Uwagi)) wpDesc += $" - {l.Uwagi}";
                // Format: latitude,longitude,description
                postBody += $"&wp={wpLat},{wpLon},{Uri.EscapeDataString(wpDesc)}";
            }

            // Wyślij POST
            using var req = new HttpRequestMessage(HttpMethod.Post, WfUrl);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            req.Content = new StringContent(postBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            using var res = await _http.SendAsync(req);
            var respBody = await res.Content.ReadAsStringAsync();

            // Sprawdź błędy
            string? errCode = null, errMsg = null;
            if (!string.IsNullOrWhiteSpace(respBody) && respBody.TrimStart().StartsWith("{"))
            {
                try
                {
                    var obj = JObject.Parse(respBody);
                    errCode = obj["errorCode"]?.ToString();
                    errMsg = obj["errorMsg"]?.ToString();
                }
                catch { }
            }

            var success = string.IsNullOrEmpty(errCode) || errCode == "0";
            var status = success ? "Wyslany" : "Blad";

            // Zapisz status synca
            await SaveSyncStatus(kursId, orderId, objectNo, status, errCode, errMsg, kurs.Ladunki.Count, user);

            return new SyncResult
            {
                Success = success,
                OrderId = orderId,
                ErrorCode = errCode,
                ErrorMessage = errMsg,
                StopsCount = kurs.Ladunki.Count
            };
        }

        // ══════════════════════════════════════════════════════════════════
        // Aktualizacja zlecenia w Webfleet (updateDestinationOrderExtern)
        // ══════════════════════════════════════════════════════════════════

        public async Task<SyncResult> AktualizujZlecenieAsync(long kursId, string user)
        {
            await EnsureTablesAsync();
            string? existingOrderId = null;
            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT WebfleetOrderId FROM WebfleetOrderSync WHERE KursID=@id AND Status IN ('Wyslany','Oczekujacy')";
                cmd.Parameters.AddWithValue("@id", kursId);
                existingOrderId = (await cmd.ExecuteScalarAsync())?.ToString();
            }
            if (string.IsNullOrEmpty(existingOrderId))
                return await WyslijKursAsync(kursId, user);

            var kurs = await PobierzKursAsync(kursId);
            var kody = kurs.Ladunki.Select(l => l.KodKlienta).ToList();
            var adresy = await PobierzAdresyAsync(kody);
            var brakGeo = kurs.Ladunki.Where(l => !adresy.TryGetValue(l.KodKlienta, out var a2) || a2.Lat == 0).Select(l => l.KodKlienta).ToList();
            if (brakGeo.Count > 0) throw new NeedAddressException($"Brak adresów: {string.Join(", ", brakGeo)}", brakGeo);

            var firstAddr = adresy[kurs.Ladunki.First().KodKlienta];
            var sb = new StringBuilder();
            sb.AppendLine($"Kurs: {kurs.Trasa} [AKT. {DateTime.Now:HH:mm}]");
            sb.AppendLine($"Data: {kurs.DataKursu:dd.MM.yyyy} | Wyjazd: {kurs.GodzWyjazdu ?? "—"}");
            sb.AppendLine($"Kierowca: {kurs.Kierowca} | Pojazd: {kurs.Rejestracja}");
            sb.AppendLine($"Przystanki: {kurs.Ladunki.Count}");
            foreach (var l in kurs.Ladunki)
            {
                var a = adresy.GetValueOrDefault(l.KodKlienta);
                sb.AppendLine($"  {l.Kolejnosc}. {a?.Nazwa ?? l.KodKlienta} — {l.PojemnikiE2} E2");
            }
            var orderText = sb.ToString(); if (orderText.Length > 1000) orderText = orderText[..997] + "...";

            var pars = new Dictionary<string, string>
            {
                ["account"] = WfAccount, ["apikey"] = WfKey, ["lang"] = "pl", ["outputformat"] = "json",
                ["action"] = "updateDestinationOrderExtern",
                ["orderid"] = existingOrderId, ["ordertext"] = orderText,
                ["longitude"] = ((int)(firstAddr.Lon * 1e6)).ToString(),
                ["latitude"] = ((int)(firstAddr.Lat * 1e6)).ToString(),
                ["country"] = "PL", ["city"] = firstAddr.Miasto, ["street"] = firstAddr.Ulica, ["zip"] = firstAddr.KodPocztowy
            };
            if (!string.IsNullOrEmpty(kurs.GodzWyjazdu))
            { pars["orderdate"] = kurs.DataKursu.ToString("dd.MM.yyyy"); pars["ordertime"] = kurs.GodzWyjazdu; }

            var postBody = string.Join("&", pars.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            using var req = new HttpRequestMessage(HttpMethod.Post, WfUrl);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            req.Content = new StringContent(postBody, Encoding.UTF8, "application/x-www-form-urlencoded");
            using var res = await _http.SendAsync(req);
            var respBody = await res.Content.ReadAsStringAsync();
            string? errCode = null, errMsg = null;
            if (!string.IsNullOrWhiteSpace(respBody) && respBody.TrimStart().StartsWith("{"))
            { try { var o = JObject.Parse(respBody); errCode = o["errorCode"]?.ToString(); errMsg = o["errorMsg"]?.ToString(); } catch { } }
            var success = string.IsNullOrEmpty(errCode) || errCode == "0";
            if (success) await SaveSyncStatus(kursId, existingOrderId, "", "Wyslany", null, "Zaktualizowano", kurs.Ladunki.Count, user);
            return new SyncResult { Success = success, OrderId = existingOrderId, ErrorCode = errCode, ErrorMessage = errMsg, StopsCount = kurs.Ladunki.Count };
        }

        // ══════════════════════════════════════════════════════════════════
        // Pobierz status zlecenia z Webfleet (showOrderReportExtern)
        // ══════════════════════════════════════════════════════════════════

        public async Task<WebfleetOrderInfo?> PobierzStatusZleceniaAsync(string orderId)
        {
            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json&action=showOrderReportExtern&orderid={U(orderId)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (body.TrimStart().StartsWith("["))
            {
                var arr = Newtonsoft.Json.Linq.JArray.Parse(body);
                if (arr.Count > 0)
                {
                    var o = arr[0];
                    return new WebfleetOrderInfo
                    {
                        OrderId = o["orderid"]?.ToString() ?? "", OrderText = o["ordertext"]?.ToString() ?? "",
                        OrderState = o["orderstate"]?.Value<int>() ?? 0, OrderStateTime = o["orderstate_time"]?.ToString() ?? "",
                        ObjectName = o["objectname"]?.ToString() ?? "", DriverName = o["drivername"]?.ToString() ?? "",
                        EstimatedArrival = o["estimated_arrival_time"]?.ToString() ?? "",
                        DelayWarning = o["delay_warnings"]?.Value<int>() ?? 0,
                        City = o["city"]?.ToString() ?? "", Street = o["street"]?.ToString() ?? ""
                    };
                }
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // Anulowanie zlecenia w Webfleet
        // ══════════════════════════════════════════════════════════════════

        public async Task<bool> AnulujZlecenieAsync(long kursId)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WebfleetOrderId, WebfleetObjectNo FROM WebfleetOrderSync WHERE KursID=@id";
            cmd.Parameters.AddWithValue("@id", kursId);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return false;
            var orderId = r["WebfleetOrderId"]?.ToString() ?? "";
            var objectNo = r["WebfleetObjectNo"]?.ToString() ?? "";
            r.Close();

            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json" +
                $"&action=cancelOrderExtern&orderid={U(orderId)}&objectno={U(objectNo)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "UPDATE WebfleetOrderSync SET Status='Anulowany' WHERE KursID=@id";
            cmd2.Parameters.AddWithValue("@id", kursId);
            await cmd2.ExecuteNonQueryAsync();

            return true;
        }

        // ══════════════════════════════════════════════════════════════════
        // Status synca
        // ══════════════════════════════════════════════════════════════════

        public async Task<List<SyncStatus>> PobierzStatusySyncAsync(DateTime data)
        {
            await EnsureTablesAsync();
            var result = new List<SyncStatus>();
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT k.KursID, k.Trasa, k.Status AS KursStatus, k.GodzWyjazdu,
                CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca, p.Rejestracja,
                s.WebfleetOrderId, s.Status AS SyncStatus, s.WyslanoUTC, s.OdpowiedzMsg, s.IloscPrzystankow,
                (SELECT COUNT(*) FROM Ladunek l WHERE l.KursID=k.KursID) AS LadunkiCount
                FROM Kurs k LEFT JOIN Kierowca ki ON k.KierowcaID=ki.KierowcaID
                LEFT JOIN Pojazd p ON k.PojazdID=p.PojazdID
                LEFT JOIN WebfleetOrderSync s ON s.KursID=k.KursID
                WHERE k.DataKursu=@d ORDER BY k.GodzWyjazdu";
            cmd.Parameters.AddWithValue("@d", data.Date);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new SyncStatus
                {
                    KursID = r.GetInt64(0),
                    Trasa = r["Trasa"]?.ToString() ?? "",
                    KursStatus = r["KursStatus"]?.ToString() ?? "",
                    GodzWyjazdu = r["GodzWyjazdu"] == DBNull.Value ? "" : ((TimeSpan)r["GodzWyjazdu"]).ToString(@"hh\:mm"),
                    Kierowca = r["Kierowca"]?.ToString() ?? "",
                    Rejestracja = r["Rejestracja"]?.ToString() ?? "",
                    WebfleetOrderId = r["WebfleetOrderId"]?.ToString(),
                    SyncStatusText = r["SyncStatus"]?.ToString() ?? "Nie wysłany",
                    WyslanoUTC = r["WyslanoUTC"] == DBNull.Value ? null : (DateTime?)r["WyslanoUTC"],
                    Blad = r["OdpowiedzMsg"]?.ToString(),
                    Przystanki = r["IloscPrzystankow"] == DBNull.Value ? 0 : Convert.ToInt32(r["IloscPrzystankow"]),
                    LadunkiCount = Convert.ToInt32(r["LadunkiCount"])
                });
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════

        private async Task<string?> FindWebfleetObjectNo(int pojazdId)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WebfleetObjectNo FROM WebfleetVehicleMapping WHERE PojazdID=@id";
            cmd.Parameters.AddWithValue("@id", pojazdId);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        private async Task SaveSyncStatus(long kursId, string orderId, string objectNo, string status,
            string? errCode, string? errMsg, int stops, string user)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE WebfleetOrderSync AS t USING (SELECT @kid AS KursID) AS s ON t.KursID=s.KursID
                WHEN MATCHED THEN UPDATE SET WebfleetOrderId=@oid, WebfleetObjectNo=@obj, Status=@st,
                    WyslanoUTC=SYSUTCDATETIME(), OdpowiedzKod=@ec, OdpowiedzMsg=@em, IloscPrzystankow=@stops
                WHEN NOT MATCHED THEN INSERT (KursID,WebfleetOrderId,WebfleetObjectNo,Status,WyslanoUTC,OdpowiedzKod,OdpowiedzMsg,IloscPrzystankow,CreatedBy)
                    VALUES (@kid,@oid,@obj,@st,SYSUTCDATETIME(),@ec,@em,@stops,@user);";
            cmd.Parameters.AddWithValue("@kid", kursId);
            cmd.Parameters.AddWithValue("@oid", orderId);
            cmd.Parameters.AddWithValue("@obj", objectNo);
            cmd.Parameters.AddWithValue("@st", status);
            cmd.Parameters.AddWithValue("@ec", (object?)errCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@em", (object?)errMsg ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@stops", stops);
            cmd.Parameters.AddWithValue("@user", user);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string U(string s) => Uri.EscapeDataString(s);

        // ══════════════════════════════════════════════════════════════════
        // Modele
        // ══════════════════════════════════════════════════════════════════

        public class KursDoSync
        {
            public long KursID { get; set; }
            public DateTime DataKursu { get; set; }
            public string Trasa { get; set; } = "";
            public string Status { get; set; } = "";
            public string? GodzWyjazdu { get; set; }
            public int PojazdID { get; set; }
            public string Kierowca { get; set; } = "";
            public string Rejestracja { get; set; } = "";
            public List<LadunekDoSync> Ladunki { get; set; } = new();
        }

        public class LadunekDoSync
        {
            public long LadunekID { get; set; }
            public int Kolejnosc { get; set; }
            public string KodKlienta { get; set; } = "";
            public int PojemnikiE2 { get; set; }
            public string Uwagi { get; set; } = "";
        }

        public class KlientAdresInfo
        {
            public string KodKlienta { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string Ulica { get; set; } = "";
            public string Miasto { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Kraj { get; set; } = "PL";
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string PelnyAdres => $"{Ulica}, {KodPocztowy} {Miasto}".Trim().Trim(',');
        }

        public class SyncResult
        {
            public bool Success { get; set; }
            public string OrderId { get; set; } = "";
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
            public int StopsCount { get; set; }
        }

        public class SyncStatus
        {
            public long KursID { get; set; }
            public string Trasa { get; set; } = "";
            public string KursStatus { get; set; } = "";
            public string GodzWyjazdu { get; set; } = "";
            public string Kierowca { get; set; } = "";
            public string Rejestracja { get; set; } = "";
            public string? WebfleetOrderId { get; set; }
            public string SyncStatusText { get; set; } = "Nie wysłany";
            public DateTime? WyslanoUTC { get; set; }
            public string? Blad { get; set; }
            public int Przystanki { get; set; }
            public int LadunkiCount { get; set; }
        }

        public class WebfleetOrderInfo
        {
            public string OrderId { get; set; } = "";
            public string OrderText { get; set; } = "";
            public int OrderState { get; set; }
            public string OrderStateTime { get; set; } = "";
            public string ObjectName { get; set; } = "";
            public string DriverName { get; set; } = "";
            public string EstimatedArrival { get; set; } = "";
            public int DelayWarning { get; set; }
            public string City { get; set; } = "";
            public string Street { get; set; } = "";

            public string StatusNazwa => OrderState switch
            {
                0 => "Nie wysłany",
                100 => "Wysłany", 101 => "Odebrane", 102 => "Przeczytane", 103 => "Zaakceptowane",
                241 => "Dostawa rozpoczęta", 242 => "Dotarł na miejsce", 243 => "Rozładunek",
                244 => "Rozładunek zakończony", 245 => "Odjazd z punktu", 247 => "Potwierdzenie dostawy",
                301 => "Anulowane", 302 => "Odrzucone", 401 => "Zakończone",
                _ => $"Stan {OrderState}"
            };

            public string OpoznienieNazwa => DelayWarning switch
            {
                0 => "—",
                1 => "W normie",
                2 => "OPÓŹNIONY",
                _ => $"Ostrzeżenie {DelayWarning}"
            };
        }

        public class NeedAddressException : Exception
        {
            public List<string> BrakujaceKody { get; }
            public NeedAddressException(string msg, List<string> kody) : base(msg) { BrakujaceKody = kody; }
        }
    }
}
