using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport
{
    public class TransportZmiana
    {
        public int Id { get; set; }
        public int ZamowienieId { get; set; }
        public string? KlientKod { get; set; }
        public string? KlientNazwa { get; set; }
        public string TypZmiany { get; set; } = "";
        public string? Opis { get; set; }
        public string? StareWartosc { get; set; }
        public string? NowaWartosc { get; set; }
        public string StatusZmiany { get; set; } = "Oczekuje";
        public string ZgloszonePrzez { get; set; } = "";
        public DateTime DataZgloszenia { get; set; }
        public string? ZaakceptowanePrzez { get; set; }
        public DateTime? DataAkceptacji { get; set; }
        public string? Komentarz { get; set; }

        public string TypLabel => TypZmiany switch
        {
            "NoweZamowienie" => "Nowe zamowienie",
            "ZmianaIlosci" => "Zmiana palet",
            "ZmianaTerminu" => "Zmiana terminu",
            "Anulowanie" => "Anulowanie",
            "ZmianaPojemnikow" => "Zmiana pojemnikow",
            "ZmianaKg" => "Zmiana wagi",
            "ZmianaAwizacji" => "Zmiana awizacji",
            "ZmianaUwag" => "Zmiana uwag",
            "ZmianaOdbiorcy" => "Zmiana odbiorcy",
            "ZmianaDataProdukcji" => "Zmiana daty produkcji",
            _ => TypZmiany
        };

        public string TypIcon => TypZmiany switch
        {
            "NoweZamowienie" => "\u25B6",       // ▶
            "ZmianaIlosci" => "\u25A0",          // ■
            "ZmianaPojemnikow" => "\u25A3",      // ▣
            "ZmianaKg" => "\u2261",              // ≡
            "ZmianaAwizacji" => "\u25CB",         // ○
            "ZmianaTerminu" => "\u25CB",          // ○
            "Anulowanie" => "\u2716",             // ✖
            "ZmianaUwag" => "\u270E",             // ✎
            "ZmianaOdbiorcy" => "\u263A",          // ☺
            "ZmianaDataProdukcji" => "\u2692",     // ⚒
            _ => "\u26A0"                         // ⚠
        };

        public System.Drawing.Color TypColor => TypZmiany switch
        {
            "NoweZamowienie" => System.Drawing.Color.FromArgb(39, 174, 96),
            "ZmianaIlosci" => System.Drawing.Color.FromArgb(142, 68, 173),
            "ZmianaPojemnikow" => System.Drawing.Color.FromArgb(41, 128, 185),
            "ZmianaKg" => System.Drawing.Color.FromArgb(230, 126, 34),
            "ZmianaAwizacji" => System.Drawing.Color.FromArgb(231, 76, 60),
            "ZmianaTerminu" => System.Drawing.Color.FromArgb(231, 76, 60),
            "Anulowanie" => System.Drawing.Color.FromArgb(192, 57, 43),
            "ZmianaUwag" => System.Drawing.Color.FromArgb(127, 140, 141),
            "ZmianaOdbiorcy" => System.Drawing.Color.FromArgb(211, 84, 0),
            "ZmianaDataProdukcji" => System.Drawing.Color.FromArgb(155, 89, 182),  // Fioletowy
            _ => System.Drawing.Color.FromArgb(243, 156, 18)
        };

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - DataZgloszenia;
                if (diff.TotalMinutes < 1) return "przed chwila";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} godz. temu";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
                return DataZgloszenia.ToString("yyyy-MM-dd HH:mm");
            }
        }
    }

    public static class TransportZmianyService
    {
        private static readonly string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ═══════════════════════════════════════════════════
        // BADGE COUNTS (synchronous for WinForms timer)
        // ═══════════════════════════════════════════════════

        public static int GetPendingCount()
        {
            try
            {
                using var conn = new SqlConnection(_connTransport);
                conn.Open();
                // Liczymy zmiany z dzisiaj oczekujące na akceptację
                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM TransportZmiany
                      WHERE StatusZmiany = 'Oczekuje'
                        AND TypZmiany != 'ZmianaStatusu'
                        AND CAST(DataZgloszenia AS date) = CAST(GETDATE() AS date)", conn);
                return (int)(cmd.ExecuteScalar() ?? 0);
            }
            catch { return 0; }
        }

        public static int GetFreeOrdersCount()
        {
            try
            {
                using var conn = new SqlConnection(_connLibra);
                conn.Open();
                // Liczymy tylko zamówienia na DZISIAJ — spójne z pending badge
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT zm.Id)
                    FROM dbo.ZamowieniaMieso zm
                    WHERE ISNULL(zm.TransportStatus, 'Oczekuje') NOT IN ('Przypisany', 'Wlasny')
                      AND zm.TransportKursID IS NULL
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                      AND CAST(zm.DataZamowienia AS date) = CAST(GETDATE() AS date)", conn);
                return (int)(cmd.ExecuteScalar() ?? 0);
            }
            catch { return 0; }
        }

        // ═══════════════════════════════════════════════════
        // CRUD (async)
        // ═══════════════════════════════════════════════════

        public static async Task<List<TransportZmiana>> GetPendingAsync()
        {
            return await GetByStatusAsync("Oczekuje");
        }

        /// <summary>
        /// Zwraca oczekujące zmiany zgłoszone dzisiaj (zgodnie z badge count).
        /// Starsze oczekujące nie są pokazywane jako bieżące notyfikacje.
        /// </summary>
        public static async Task<List<TransportZmiana>> GetPendingTodayAsync()
        {
            var list = new List<TransportZmiana>();
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT Id, ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                           StareWartosc, NowaWartosc, StatusZmiany, ZgloszonePrzez,
                           DataZgloszenia, ZaakceptowanePrzez, DataAkceptacji, Komentarz
                    FROM TransportZmiany
                    WHERE StatusZmiany = 'Oczekuje'
                      AND TypZmiany != 'ZmianaStatusu'
                      AND CAST(DataZgloszenia AS date) = CAST(GETDATE() AS date)
                    ORDER BY DataZgloszenia DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(MapZmiana(reader));
            }
            catch { }
            return list;
        }

        public static async Task<List<TransportZmiana>> GetAllAsync(int top = 100)
        {
            var list = new List<TransportZmiana>();
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand($@"
                    SELECT TOP ({top}) Id, ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                           StareWartosc, NowaWartosc, StatusZmiany, ZgloszonePrzez,
                           DataZgloszenia, ZaakceptowanePrzez, DataAkceptacji, Komentarz
                    FROM TransportZmiany
                    WHERE TypZmiany != 'ZmianaStatusu'
                    ORDER BY DataZgloszenia DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(MapZmiana(reader));
            }
            catch { }
            return list;
        }

        public static async Task<List<TransportZmiana>> GetByStatusAsync(string status)
        {
            var list = new List<TransportZmiana>();
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT Id, ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                           StareWartosc, NowaWartosc, StatusZmiany, ZgloszonePrzez,
                           DataZgloszenia, ZaakceptowanePrzez, DataAkceptacji, Komentarz
                    FROM TransportZmiany
                    WHERE StatusZmiany = @Status AND TypZmiany != 'ZmianaStatusu'
                    ORDER BY DataZgloszenia DESC", conn);
                cmd.Parameters.AddWithValue("@Status", status);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(MapZmiana(reader));
            }
            catch { }
            return list;
        }

        public static async Task SubmitChangeAsync(int zamowienieId, string klientKod, string klientNazwa,
            string typZmiany, string opis, string? stareWartosc, string? nowaWartosc, string user)
        {
            if (typZmiany == "ZmianaStatusu") return;

            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    INSERT INTO TransportZmiany
                        (ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                         StareWartosc, NowaWartosc, ZgloszonePrzez)
                    VALUES (@ZamId, @KlientKod, @KlientNazwa, @Typ, @Opis,
                            @Stare, @Nowe, @User)", conn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmd.Parameters.AddWithValue("@KlientKod", (object?)klientKod ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientNazwa", (object?)klientNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Typ", typZmiany);
                cmd.Parameters.AddWithValue("@Opis", (object?)opis ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Stare", (object?)stareWartosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Nowe", (object?)nowaWartosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@User", user);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad zapisu zmiany: {ex.Message}", ex);
            }
        }

        public static async Task AcceptAsync(int id, string user, string? komentarz = null)
        {
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE TransportZmiany SET
                        StatusZmiany = 'Zaakceptowano',
                        ZaakceptowanePrzez = @User,
                        DataAkceptacji = GETDATE(),
                        Komentarz = @Komentarz
                    WHERE Id = @Id AND StatusZmiany = 'Oczekuje'", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@User", user);
                cmd.Parameters.AddWithValue("@Komentarz", (object?)komentarz ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad akceptacji: {ex.Message}", ex);
            }
        }

        public static async Task RejectAsync(int id, string user, string? komentarz = null)
        {
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE TransportZmiany SET
                        StatusZmiany = 'Odrzucono',
                        ZaakceptowanePrzez = @User,
                        DataAkceptacji = GETDATE(),
                        Komentarz = @Komentarz
                    WHERE Id = @Id AND StatusZmiany = 'Oczekuje'", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@User", user);
                cmd.Parameters.AddWithValue("@Komentarz", (object?)komentarz ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad odrzucenia: {ex.Message}", ex);
            }
        }

        public static async Task AcceptAllAsync(string user)
        {
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE TransportZmiany SET
                        StatusZmiany = 'Zaakceptowano',
                        ZaakceptowanePrzez = @User,
                        DataAkceptacji = GETDATE()
                    WHERE StatusZmiany = 'Oczekuje'", conn);
                cmd.Parameters.AddWithValue("@User", user);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad akceptacji zbiorczej: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Pobiera WSZYSTKIE zmiany (niezależnie od statusu) dla podanych zamówień — do historii kursu.
        /// </summary>
        public static async Task<List<TransportZmiana>> GetByZamowienieIdsAsync(IEnumerable<int> zamIds)
        {
            var list = new List<TransportZmiana>();
            var idList = zamIds?.ToList();
            if (idList == null || idList.Count == 0) return list;
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                var idsStr = string.Join(",", idList);
                using var cmd = new SqlCommand($@"
                    SELECT Id, ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                           StareWartosc, NowaWartosc, StatusZmiany, ZgloszonePrzez,
                           DataZgloszenia, ZaakceptowanePrzez, DataAkceptacji, Komentarz
                    FROM TransportZmiany
                    WHERE ZamowienieId IN ({idsStr}) AND TypZmiany != 'ZmianaStatusu'
                    ORDER BY DataZgloszenia DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(MapZmiana(reader));
            }
            catch { }
            return list;
        }

        // ═══════════════════════════════════════════════════
        // AUTO-DETECT new/changed orders (snapshot comparison)
        // ═══════════════════════════════════════════════════

        private class OrderSnapshot
        {
            public int ZamowienieId;
            public int KlientId;
            public int Pojemniki;
            public int Palety;
            public decimal IloscKg;
            public DateTime DataZamowienia;
            public DateTime? DataPrzyjazdu;
            public DateTime? DataProdukcji;
            public string? Status;
            public string? TransportStatus;
            public long? TransportKursID;
            public string? Uwagi;
            public string? KlientNazwa;
            public string? ModyfikowalPrzez;
        }

        private static async Task EnsureSnapshotTableAsync(SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportOrderSnapshot')
                CREATE TABLE TransportOrderSnapshot (
                    ZamowienieId     INT PRIMARY KEY,
                    KlientId         INT NOT NULL,
                    LiczbaPojemnikow INT NULL,
                    LiczbaPalet      INT NULL,
                    IloscKg          DECIMAL(18,2) NULL,
                    DataZamowienia   DATETIME NULL,
                    DataPrzyjazdu    DATETIME NULL,
                    Status           VARCHAR(50) NULL,
                    TransportStatus  VARCHAR(50) NULL,
                    TransportKursID  BIGINT NULL,
                    Uwagi            NVARCHAR(500) NULL,
                    KlientNazwa      NVARCHAR(200) NULL,
                    ModyfikowalPrzez NVARCHAR(100) NULL,
                    LastChecked      DATETIME NOT NULL DEFAULT GETDATE()
                );

                -- Dodaj brakujace kolumny jesli tabela juz istnieje
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TransportOrderSnapshot') AND name = 'IloscKg')
                    ALTER TABLE TransportOrderSnapshot ADD IloscKg DECIMAL(18,2) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TransportOrderSnapshot') AND name = 'DataPrzyjazdu')
                    ALTER TABLE TransportOrderSnapshot ADD DataPrzyjazdu DATETIME NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TransportOrderSnapshot') AND name = 'ModyfikowalPrzez')
                    ALTER TABLE TransportOrderSnapshot ADD ModyfikowalPrzez NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TransportOrderSnapshot') AND name = 'DataProdukcji')
                    ALTER TABLE TransportOrderSnapshot ADD DataProdukcji DATETIME NULL;
            ", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public static string LastDetectError { get; private set; }

        public static async Task<int> DetectNewOrdersAsync(string user)
        {
            int detected = 0;
            LastDetectError = null;
            try
            {
                using var connLibra = new SqlConnection(_connLibra);
                await connLibra.OpenAsync();

                using var connTransport = new SqlConnection(_connTransport);
                await connTransport.OpenAsync();

                await EnsureSnapshotTableAsync(connTransport);

                // 0. Clean up very old snapshots (>30 days) to keep table manageable
                using (var cmdCleanup = new SqlCommand(@"
                    DELETE FROM TransportOrderSnapshot
                    WHERE DataZamowienia IS NOT NULL
                      AND DataZamowienia < DATEADD(day, -30, CAST(GETDATE() AS date))", connTransport))
                {
                    await cmdCleanup.ExecuteNonQueryAsync();
                }

                // 1. Load ALL existing snapshots (no date filter — needed for change detection)
                var snapshots = new Dictionary<int, OrderSnapshot>();
                using (var cmdSnap = new SqlCommand(@"
                    SELECT ZamowienieId, KlientId, ISNULL(LiczbaPojemnikow,0), ISNULL(LiczbaPalet,0),
                           ISNULL(IloscKg,0), DataZamowienia, DataPrzyjazdu,
                           Status, TransportStatus, TransportKursID, Uwagi, KlientNazwa, ModyfikowalPrzez,
                           DataProdukcji
                    FROM TransportOrderSnapshot", connTransport))
                {
                    using var reader = await cmdSnap.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var s = new OrderSnapshot
                        {
                            ZamowienieId = Convert.ToInt32(reader[0]),
                            KlientId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]),
                            Pojemniki = Convert.ToInt32(reader[2]),
                            Palety = Convert.ToInt32(reader[3]),
                            IloscKg = Convert.ToDecimal(reader[4]),
                            DataZamowienia = reader.IsDBNull(5) ? DateTime.MinValue : Convert.ToDateTime(reader[5]),
                            DataPrzyjazdu = reader.IsDBNull(6) ? null : Convert.ToDateTime(reader[6]),
                            Status = reader.IsDBNull(7) ? null : reader[7].ToString(),
                            TransportStatus = reader.IsDBNull(8) ? null : reader[8].ToString(),
                            TransportKursID = reader.IsDBNull(9) ? null : Convert.ToInt64(reader[9]),
                            Uwagi = reader.IsDBNull(10) ? null : reader[10].ToString(),
                            KlientNazwa = reader.IsDBNull(11) ? null : reader[11].ToString(),
                            ModyfikowalPrzez = reader.IsDBNull(12) ? null : reader[12].ToString(),
                            DataProdukcji = reader.IsDBNull(13) ? null : Convert.ToDateTime(reader[13])
                        };
                        snapshots[s.ZamowienieId] = s;
                    }
                }

                // 2. Get ALL orders from LibraNet (with IloscKg from ZamowieniaMiesoTowar, DataPrzyjazdu, ModyfikowalPrzez)
                var currentOrders = new List<OrderSnapshot>();
                using (var cmdOrders = new SqlCommand(@"
                    SELECT zm.Id, ISNULL(zm.KlientId, 0), zm.DataZamowienia,
                           ISNULL(zm.LiczbaPojemnikow, 0), ISNULL(zm.LiczbaPalet, 0),
                           ISNULL(SUM(zmt.Ilosc), 0), zm.DataPrzyjazdu,
                           ISNULL(zm.Status, 'Nowe'), ISNULL(zm.TransportStatus, 'Oczekuje'),
                           zm.TransportKursID, zm.Uwagi, zm.ModyfikowalPrzez, zm.DataProdukcji
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataZamowienia >= DATEADD(day, -1, CAST(GETDATE() AS date))
                      AND zm.DataZamowienia <= DATEADD(day, 14, CAST(GETDATE() AS date))
                      AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
                    GROUP BY zm.Id, zm.KlientId, zm.DataZamowienia, zm.LiczbaPojemnikow, zm.LiczbaPalet,
                             zm.DataPrzyjazdu, zm.Status, zm.TransportStatus, zm.TransportKursID,
                             zm.Uwagi, zm.ModyfikowalPrzez, zm.DataProdukcji
                    ORDER BY zm.DataZamowienia", connLibra))
                {
                    using var reader = await cmdOrders.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        try
                        {
                            currentOrders.Add(new OrderSnapshot
                            {
                                ZamowienieId = Convert.ToInt32(reader[0]),
                                KlientId = Convert.ToInt32(reader[1]),
                                DataZamowienia = reader.IsDBNull(2) ? DateTime.MinValue : Convert.ToDateTime(reader[2]),
                                Pojemniki = Convert.ToInt32(reader[3]),
                                Palety = Convert.ToInt32(reader[4]),
                                IloscKg = Convert.ToDecimal(reader[5]),
                                DataPrzyjazdu = reader.IsDBNull(6) ? null : Convert.ToDateTime(reader[6]),
                                Status = reader[7].ToString(),
                                TransportStatus = reader[8].ToString(),
                                TransportKursID = reader.IsDBNull(9) ? null : Convert.ToInt64(reader[9]),
                                Uwagi = reader.IsDBNull(10) ? null : reader[10].ToString(),
                                ModyfikowalPrzez = reader.IsDBNull(11) ? null : reader[11].ToString(),
                                DataProdukcji = reader.IsDBNull(12) ? null : Convert.ToDateTime(reader[12])
                            });
                        }
                        catch (Exception exRow)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DetectNewOrders] Row read error: {exRow.Message}");
                        }
                    }
                }

                if (currentOrders.Count == 0)
                    return 0;

                // 3. Resolve client names from Handel DB
                var klientNames = new Dictionary<int, string>();
                try
                {
                    var klientIds = string.Join(",", currentOrders.Select(o => o.KlientId).Distinct());
                    using var connHandel = new SqlConnection(_connHandel);
                    await connHandel.OpenAsync();
                    using var cmdK = new SqlCommand($@"
                        SELECT c.Id, ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS VARCHAR(10)))
                        FROM SSCommon.STContractors c
                        WHERE c.Id IN ({klientIds})", connHandel);
                    using var rK = await cmdK.ExecuteReaderAsync();
                    while (await rK.ReadAsync())
                        klientNames[rK.GetInt32(0)] = rK.GetString(1);
                }
                catch { }

                // 4. Compare each order with snapshot
                foreach (var order in currentOrders)
                {
                    var nazwa = klientNames.TryGetValue(order.KlientId, out var n) ? n : $"Klient {order.KlientId}";
                    order.KlientNazwa = nazwa;
                    var modifier = order.ModyfikowalPrzez ?? user;

                    if (!snapshots.TryGetValue(order.ZamowienieId, out var snap))
                    {
                        await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                            "NoweZamowienie",
                            $"Nowe zamowienie: {order.Pojemniki} poj., {order.Palety} pal., {order.IloscKg:N0} kg",
                            null,
                            $"{order.Pojemniki} poj. / {order.Palety} pal. / {order.IloscKg:N0} kg",
                            modifier);
                        detected++;
                    }
                    else
                    {
                        if (snap.Pojemniki != order.Pojemniki)
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaPojemnikow", $"Zmiana liczby pojemnikow",
                                $"{snap.Pojemniki}", $"{order.Pojemniki}", modifier);
                            detected++;
                        }

                        if (snap.Palety != order.Palety)
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaIlosci", $"Zmiana liczby palet",
                                $"{snap.Palety}", $"{order.Palety}", modifier);
                            detected++;
                        }

                        if (snap.IloscKg != order.IloscKg)
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaKg", $"Zmiana wagi (kg)",
                                $"{snap.IloscKg:N0} kg", $"{order.IloscKg:N0} kg", modifier);
                            detected++;
                        }

                        var snapPrzyjazd = snap.DataPrzyjazdu?.ToString("yyyy-MM-dd HH:mm") ?? "";
                        var orderPrzyjazd = order.DataPrzyjazdu?.ToString("yyyy-MM-dd HH:mm") ?? "";
                        if (snapPrzyjazd != orderPrzyjazd)
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaAwizacji", $"Zmiana daty/godziny awizacji",
                                string.IsNullOrEmpty(snapPrzyjazd) ? "(brak)" : snapPrzyjazd,
                                string.IsNullOrEmpty(orderPrzyjazd) ? "(brak)" : orderPrzyjazd, modifier);
                            detected++;
                        }

                        // Zmiana daty produkcji — tylko gdy obie daty istnieją (ignoruj (brak) → data)
                        if (snap.DataProdukcji.HasValue && order.DataProdukcji.HasValue
                            && snap.DataProdukcji.Value.Date != order.DataProdukcji.Value.Date)
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaDataProdukcji", $"Zmiana daty produkcji",
                                snap.DataProdukcji.Value.ToString("yyyy-MM-dd"),
                                order.DataProdukcji.Value.ToString("yyyy-MM-dd"), modifier);
                            detected++;
                        }

                        if (snap.DataZamowienia.Date != order.DataZamowienia.Date)
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaTerminu", $"Zmiana daty zamowienia",
                                snap.DataZamowienia.ToString("yyyy-MM-dd"), order.DataZamowienia.ToString("yyyy-MM-dd"), modifier);
                            detected++;
                        }

                        if ((snap.Uwagi ?? "") != (order.Uwagi ?? ""))
                        {
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nazwa,
                                "ZmianaUwag", $"Zmiana uwag",
                                string.IsNullOrEmpty(snap.Uwagi) ? "(brak)" : snap.Uwagi,
                                string.IsNullOrEmpty(order.Uwagi) ? "(brak)" : order.Uwagi, modifier);
                            detected++;
                        }

                        // Zmiana odbiorcy (klienta)
                        if (snap.KlientId != order.KlientId)
                        {
                            var staraNazwa = snap.KlientNazwa ?? $"Klient {snap.KlientId}";
                            var nowaNazwa = nazwa;
                            await InsertZmiana(connTransport, order.ZamowienieId, order.KlientId.ToString(), nowaNazwa,
                                "ZmianaOdbiorcy", $"Zmiana odbiorcy",
                                staraNazwa, nowaNazwa, modifier);
                            detected++;
                        }
                    }

                    await UpsertSnapshot(connTransport, order);
                }

                // 5. Detect cancelled/removed orders - only check orders within our date window
                var currentIds = new HashSet<int>(currentOrders.Select(o => o.ZamowienieId));
                foreach (var snap in snapshots.Values)
                {
                    // Only flag as cancelled if the snapshot's date is within our current query window
                    // (-1 day to +14 days). This prevents false cancellations from old snapshots.
                    if (!currentIds.Contains(snap.ZamowienieId) && snap.Status != "Anulowane"
                        && snap.DataZamowienia >= DateTime.Today.AddDays(-1)
                        && snap.DataZamowienia <= DateTime.Today.AddDays(14))
                    {
                        // Double-check by querying LibraNet directly for this specific order
                        bool actuallyRemoved = true;
                        try
                        {
                            using var cmdCheck = new SqlCommand(
                                "SELECT COUNT(*) FROM dbo.ZamowieniaMieso WHERE Id = @Id AND ISNULL(Status, 'Nowe') NOT IN ('Anulowane')",
                                connLibra);
                            cmdCheck.Parameters.AddWithValue("@Id", snap.ZamowienieId);
                            var exists = (int)(await cmdCheck.ExecuteScalarAsync() ?? 0);
                            if (exists > 0) actuallyRemoved = false;
                        }
                        catch { }

                        if (actuallyRemoved)
                        {
                            var nazwa = snap.KlientNazwa ?? $"Klient {snap.KlientId}";
                            await InsertZmiana(connTransport, snap.ZamowienieId, snap.KlientId.ToString(), nazwa,
                                "Anulowanie", "Zamowienie anulowane lub usuniete",
                                $"{snap.Pojemniki} poj. / {snap.Palety} pal.", null, user);
                            detected++;

                            using var cmdDel = new SqlCommand(
                                "DELETE FROM TransportOrderSnapshot WHERE ZamowienieId = @Id", connTransport);
                            cmdDel.Parameters.AddWithValue("@Id", snap.ZamowienieId);
                            await cmdDel.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LastDetectError = $"{ex.Message}\n{ex.StackTrace?.Substring(0, Math.Min(300, ex.StackTrace?.Length ?? 0))}";
                System.Diagnostics.Debug.WriteLine($"[TransportZmianyService] DetectNewOrdersAsync ERROR: {ex.Message}\n{ex.StackTrace}");
            }
            return detected;
        }

        private static async Task InsertZmiana(SqlConnection conn, int zamId, string klientKod, string klientNazwa,
            string typ, string opis, string? stare, string? nowe, string user)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO TransportZmiany
                    (ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                     StareWartosc, NowaWartosc, ZgloszonePrzez)
                VALUES (@ZamId, @Kod, @Nazwa, @Typ, @Opis, @Stare, @Nowe, @User)", conn);
            cmd.Parameters.AddWithValue("@ZamId", zamId);
            cmd.Parameters.AddWithValue("@Kod", (object?)klientKod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nazwa", (object?)klientNazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Typ", typ);
            cmd.Parameters.AddWithValue("@Opis", (object?)opis ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Stare", (object?)stare ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nowe", (object?)nowe ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@User", user);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task UpsertSnapshot(SqlConnection conn, OrderSnapshot order)
        {
            using var cmd = new SqlCommand(@"
                MERGE TransportOrderSnapshot AS target
                USING (SELECT @Id AS ZamowienieId) AS source
                ON target.ZamowienieId = source.ZamowienieId
                WHEN MATCHED THEN UPDATE SET
                    KlientId = @KlientId, LiczbaPojemnikow = @Poj, LiczbaPalet = @Pal,
                    IloscKg = @Kg, DataZamowienia = @Data, DataPrzyjazdu = @Przyjazd,
                    DataProdukcji = @Produkcji,
                    Status = @Status, TransportStatus = @TStatus,
                    TransportKursID = @KursId, Uwagi = @Uwagi, KlientNazwa = @Nazwa,
                    ModyfikowalPrzez = @Modifier, LastChecked = GETDATE()
                WHEN NOT MATCHED THEN INSERT
                    (ZamowienieId, KlientId, LiczbaPojemnikow, LiczbaPalet, IloscKg,
                     DataZamowienia, DataPrzyjazdu, DataProdukcji, Status, TransportStatus, TransportKursID,
                     Uwagi, KlientNazwa, ModyfikowalPrzez)
                VALUES (@Id, @KlientId, @Poj, @Pal, @Kg, @Data, @Przyjazd, @Produkcji,
                        @Status, @TStatus, @KursId, @Uwagi, @Nazwa, @Modifier);",
                conn);
            cmd.Parameters.AddWithValue("@Id", order.ZamowienieId);
            cmd.Parameters.AddWithValue("@KlientId", order.KlientId);
            cmd.Parameters.AddWithValue("@Poj", order.Pojemniki);
            cmd.Parameters.AddWithValue("@Pal", order.Palety);
            cmd.Parameters.AddWithValue("@Kg", order.IloscKg);
            cmd.Parameters.AddWithValue("@Data", order.DataZamowienia);
            cmd.Parameters.AddWithValue("@Przyjazd", (object?)order.DataPrzyjazdu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Produkcji", (object?)order.DataProdukcji ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)order.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TStatus", (object?)order.TransportStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KursId", (object?)order.TransportKursID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uwagi", (object?)order.Uwagi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nazwa", (object?)order.KlientNazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Modifier", (object?)order.ModyfikowalPrzez ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Accept all pending changes for orders assigned to a specific kurs.
        /// Updates Ladunek.PojemnikiE2 in DB, accepts TransportZmiany records, updates snapshots.
        /// </summary>
        public static async Task AcceptChangesForKursAsync(long kursId, string user)
        {
            try
            {
                // 1. Get ladunki for this kurs from TransportPL
                using var connTransport = new SqlConnection(_connTransport);
                await connTransport.OpenAsync();

                var zamIds = new List<int>();
                var ladunkiToUpdate = new List<(long LadunekID, int ZamId)>();

                using (var cmd = new SqlCommand(
                    "SELECT LadunekID, KodKlienta FROM Ladunek WHERE KursID = @KursID", connTransport))
                {
                    cmd.Parameters.AddWithValue("@KursID", kursId);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var kodKlienta = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                        if (kodKlienta.StartsWith("ZAM_") && int.TryParse(kodKlienta.Substring(4), out int zamId))
                        {
                            zamIds.Add(zamId);
                            ladunkiToUpdate.Add((rdr.GetInt64(0), zamId));
                        }
                    }
                }

                if (zamIds.Count == 0) return;

                // 2. Get live data from LibraNet (all tracked fields)
                using var connLibra = new SqlConnection(_connLibra);
                await connLibra.OpenAsync();

                var liveData = new Dictionary<int, OrderSnapshot>();
                var zamIdsStr = string.Join(",", zamIds);
                using (var cmd = new SqlCommand($@"
                    SELECT zm.Id, ISNULL(zm.LiczbaPojemnikow, 0), ISNULL(zm.TrybE2, 0), zm.KlientId,
                           ISNULL(zm.LiczbaPalet, 0), ISNULL(SUM(zmt.Ilosc), 0), zm.DataPrzyjazdu,
                           zm.DataZamowienia, ISNULL(zm.TransportStatus, 'Oczekuje'), zm.ModyfikowalPrzez,
                           zm.DataProdukcji
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.Id IN ({zamIdsStr})
                    GROUP BY zm.Id, zm.LiczbaPojemnikow, zm.TrybE2, zm.KlientId,
                             zm.LiczbaPalet, zm.DataPrzyjazdu, zm.DataZamowienia,
                             zm.TransportStatus, zm.ModyfikowalPrzez, zm.DataProdukcji", connLibra))
                {
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var id = Convert.ToInt32(rdr[0]);
                        liveData[id] = new OrderSnapshot
                        {
                            ZamowienieId = id,
                            Pojemniki = Convert.ToInt32(rdr[1]),
                            KlientId = rdr.IsDBNull(3) ? 0 : Convert.ToInt32(rdr[3]),
                            Palety = Convert.ToInt32(rdr[4]),
                            IloscKg = Convert.ToDecimal(rdr[5]),
                            DataPrzyjazdu = rdr.IsDBNull(6) ? null : Convert.ToDateTime(rdr[6]),
                            DataZamowienia = rdr.IsDBNull(7) ? DateTime.MinValue : Convert.ToDateTime(rdr[7]),
                            TransportStatus = rdr[8]?.ToString() ?? "Oczekuje",
                            ModyfikowalPrzez = rdr.IsDBNull(9) ? null : rdr[9].ToString(),
                            DataProdukcji = rdr.IsDBNull(10) ? null : Convert.ToDateTime(rdr[10]),
                            TransportKursID = kursId
                        };
                    }
                }

                // 3. Update Ladunek.PojemnikiE2 in TransportPL to match live values
                foreach (var (ladunekId, zamId) in ladunkiToUpdate)
                {
                    if (liveData.TryGetValue(zamId, out var live))
                    {
                        using var cmdUpd = new SqlCommand(@"
                            UPDATE Ladunek SET PojemnikiE2 = @Poj
                            WHERE LadunekID = @Id", connTransport);
                        cmdUpd.Parameters.AddWithValue("@Id", ladunekId);
                        cmdUpd.Parameters.AddWithValue("@Poj", live.Pojemniki);
                        await cmdUpd.ExecuteNonQueryAsync();
                    }
                }

                // 4. Accept all pending TransportZmiany for these zamowienie IDs
                using (var cmdAccept = new SqlCommand($@"
                    UPDATE TransportZmiany SET
                        StatusZmiany = 'Zaakceptowano',
                        ZaakceptowanePrzez = @User,
                        DataAkceptacji = GETDATE()
                    WHERE StatusZmiany = 'Oczekuje'
                      AND ZamowienieId IN ({zamIdsStr})", connTransport))
                {
                    cmdAccept.Parameters.AddWithValue("@User", user);
                    await cmdAccept.ExecuteNonQueryAsync();
                }

                // 5. Update snapshots for these orders with all current data
                await EnsureSnapshotTableAsync(connTransport);
                foreach (var zamId in zamIds)
                {
                    if (liveData.TryGetValue(zamId, out var live))
                        await UpsertSnapshot(connTransport, live);
                }
            }
            catch { }
        }

        /// <summary>
        /// Get pending changes for orders assigned to a specific kurs.
        /// </summary>
        public static async Task<List<TransportZmiana>> GetPendingForKursAsync(long kursId)
        {
            var result = new List<TransportZmiana>();
            try
            {
                using var connTransport = new SqlConnection(_connTransport);
                await connTransport.OpenAsync();

                // Get ZAM_ ids for this kurs
                var zamIds = new List<int>();
                using (var cmd = new SqlCommand(
                    "SELECT KodKlienta FROM Ladunek WHERE KursID = @KursID", connTransport))
                {
                    cmd.Parameters.AddWithValue("@KursID", kursId);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var kod = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
                        if (kod.StartsWith("ZAM_") && int.TryParse(kod.Substring(4), out int zamId))
                            zamIds.Add(zamId);
                    }
                }

                if (zamIds.Count == 0) return result;

                var zamIdsStr = string.Join(",", zamIds);
                using (var cmd = new SqlCommand($@"
                    SELECT Id, ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                           StareWartosc, NowaWartosc, StatusZmiany, ZgloszonePrzez,
                           DataZgloszenia, ZaakceptowanePrzez, DataAkceptacji, Komentarz
                    FROM TransportZmiany
                    WHERE StatusZmiany = 'Oczekuje'
                      AND TypZmiany NOT IN ('NoweZamowienie', 'ZmianaStatusu')
                      AND ZamowienieId IN ({zamIdsStr})
                    ORDER BY DataZgloszenia DESC", connTransport))
                {
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                        result.Add(MapZmiana(rdr));
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Get list of zamowienie IDs with pending changes for a given kurs.
        /// </summary>
        public static async Task<List<int>> GetPendingZamIdyForKursAsync(long kursId)
        {
            var result = new List<int>();
            try
            {
                using var connTransport = new SqlConnection(_connTransport);
                await connTransport.OpenAsync();

                // Get ZAM_ ids for this kurs
                var zamIds = new List<int>();
                using (var cmd = new SqlCommand(
                    "SELECT KodKlienta FROM Ladunek WHERE KursID = @KursID", connTransport))
                {
                    cmd.Parameters.AddWithValue("@KursID", kursId);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var kod = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
                        if (kod.StartsWith("ZAM_") && int.TryParse(kod.Substring(4), out int zamId))
                            zamIds.Add(zamId);
                    }
                }

                if (zamIds.Count == 0) return result;

                // Check which have pending changes
                var zamIdsStr = string.Join(",", zamIds);
                using (var cmd = new SqlCommand($@"
                    SELECT DISTINCT ZamowienieId FROM TransportZmiany
                    WHERE StatusZmiany = 'Oczekuje' AND TypZmiany != 'ZmianaStatusu' AND ZamowienieId IN ({zamIdsStr})", connTransport))
                {
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                        result.Add(rdr.GetInt32(0));
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Log a transport change (called from transport editor when orders are assigned/removed).
        /// </summary>
        public static async Task LogChangeAsync(int zamowienieId, string klientKod, string klientNazwa,
            string typZmiany, string opis, string? stareWartosc, string? nowaWartosc, string user)
        {
            // Zmiany statusu (przypisanie/usunięcie z kursu) nie są logowane — zbędne dla logistyka
            if (typZmiany == "ZmianaStatusu") return;

            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    INSERT INTO TransportZmiany
                        (ZamowienieId, KlientKod, KlientNazwa, TypZmiany, Opis,
                         StareWartosc, NowaWartosc, ZgloszonePrzez)
                    VALUES (@ZamId, @KlientKod, @KlientNazwa, @Typ, @Opis,
                            @Stare, @Nowe, @User)", conn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmd.Parameters.AddWithValue("@KlientKod", (object?)klientKod ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientNazwa", (object?)klientNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Typ", typZmiany);
                cmd.Parameters.AddWithValue("@Opis", (object?)opis ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Stare", (object?)stareWartosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Nowe", (object?)nowaWartosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@User", user);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════

        private static TransportZmiana MapZmiana(SqlDataReader r)
        {
            return new TransportZmiana
            {
                Id = r.GetInt32(0),
                ZamowienieId = r.GetInt32(1),
                KlientKod = r["KlientKod"] as string,
                KlientNazwa = r["KlientNazwa"] as string,
                TypZmiany = r.GetString(4),
                Opis = r["Opis"] as string,
                StareWartosc = r["StareWartosc"] as string,
                NowaWartosc = r["NowaWartosc"] as string,
                StatusZmiany = r.GetString(8),
                ZgloszonePrzez = r.GetString(9),
                DataZgloszenia = r.GetDateTime(10),
                ZaakceptowanePrzez = r["ZaakceptowanePrzez"] as string,
                DataAkceptacji = r["DataAkceptacji"] as DateTime?,
                Komentarz = r["Komentarz"] as string
            };
        }
    }
}
