using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Flota.Services
{
    public class FlotaService
    {
        private readonly string _connectionString;

        public FlotaService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private bool _tablesChecked;

        public async Task EnsureTablesExistAsync()
        {
            if (_tablesChecked) return;

            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DriverDetails')
                CREATE TABLE DriverDetails (
                    DriverGID int NOT NULL PRIMARY KEY,
                    FirstName nvarchar(50) NULL, LastName nvarchar(80) NULL,
                    Phone1 nvarchar(20) NULL, Phone2 nvarchar(20) NULL,
                    Email nvarchar(100) NULL, PESEL nvarchar(11) NULL,
                    NrPrawaJazdy nvarchar(30) NULL, KategoriePrawaJazdy nvarchar(20) NULL,
                    DataWaznosciPJ date NULL, NrBadanLekarskich nvarchar(30) NULL,
                    DataWazBadanLek date NULL, NrSzkoleniaBHP nvarchar(30) NULL,
                    DataWazBHP date NULL, DataZatrudnienia date NULL, DataZwolnienia date NULL,
                    TypZatrudnienia nvarchar(30) NULL, Uwagi nvarchar(500) NULL,
                    ZdjecieKierowcy varbinary(MAX) NULL,
                    CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    ModifiedAtUTC datetime2 NULL, ModifiedBy nvarchar(64) NULL,
                    CONSTRAINT FK_DriverDetails_Driver FOREIGN KEY (DriverGID) REFERENCES Driver(GID)
                );

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VehicleDetails')
                CREATE TABLE VehicleDetails (
                    CarTrailerID varchar(10) NOT NULL PRIMARY KEY,
                    Registration nvarchar(20) NULL, VIN nvarchar(17) NULL,
                    RokProdukcji int NULL, DataPrzegladu date NULL,
                    DataUbezpieczenia date NULL, NrPolisyOC nvarchar(30) NULL,
                    NrPolisyAC nvarchar(30) NULL, Ubezpieczyciel nvarchar(100) NULL,
                    PrzebiegKm int NULL, DataOstatniegoTank date NULL,
                    SrednieSpalanie decimal(5,2) NULL, PojemnoscBaku int NULL,
                    MaxLadownoscKg int NULL, MaxPaletH1 int NULL, MaxPojemnikE2 int NULL,
                    TypNadwozia nvarchar(30) NULL, TemperaturaMin decimal(5,1) NULL,
                    TemperaturaMax decimal(5,1) NULL, GPSModul nvarchar(50) NULL,
                    Uwagi nvarchar(500) NULL, ZdjeciePojazdu varbinary(MAX) NULL,
                    CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    ModifiedAtUTC datetime2 NULL, ModifiedBy nvarchar(64) NULL,
                    CONSTRAINT FK_VehicleDetails_CarTrailer FOREIGN KEY (CarTrailerID) REFERENCES CarTrailer(ID)
                );

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DriverVehicleAssignment')
                BEGIN
                    CREATE TABLE DriverVehicleAssignment (
                        ID int NOT NULL IDENTITY PRIMARY KEY,
                        DriverGID int NOT NULL, CarTrailerID varchar(10) NOT NULL,
                        Rola nvarchar(30) NOT NULL DEFAULT N'Glowny',
                        DataOd date NOT NULL, DataDo date NULL,
                        Powod nvarchar(200) NULL, Uwagi nvarchar(500) NULL,
                        CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        CreatedBy nvarchar(64) NULL,
                        CONSTRAINT FK_DVA_Driver FOREIGN KEY (DriverGID) REFERENCES Driver(GID),
                        CONSTRAINT FK_DVA_CarTrailer FOREIGN KEY (CarTrailerID) REFERENCES CarTrailer(ID)
                    );
                    CREATE INDEX IX_DVA_Active ON DriverVehicleAssignment(DriverGID, DataDo) WHERE DataDo IS NULL;
                    CREATE INDEX IX_DVA_Vehicle ON DriverVehicleAssignment(CarTrailerID, DataDo) WHERE DataDo IS NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VehicleServiceLog')
                BEGIN
                    CREATE TABLE VehicleServiceLog (
                        ID int NOT NULL IDENTITY PRIMARY KEY,
                        CarTrailerID varchar(10) NOT NULL,
                        TypZdarzenia nvarchar(30) NOT NULL, Data date NOT NULL,
                        DataNastepne date NULL, Opis nvarchar(500) NULL,
                        KosztBrutto decimal(10,2) NULL, PrzebiegKm int NULL,
                        LitryPaliwa decimal(8,2) NULL, CenaLitra decimal(6,3) NULL,
                        Warsztat nvarchar(100) NULL, NrFaktury nvarchar(50) NULL,
                        Uwagi nvarchar(500) NULL,
                        CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        CreatedBy nvarchar(64) NULL,
                        CONSTRAINT FK_VSL_CarTrailer FOREIGN KEY (CarTrailerID) REFERENCES CarTrailer(ID)
                    );
                    CREATE INDEX IX_VSL_Vehicle_Data ON VehicleServiceLog(CarTrailerID, Data DESC);
                END";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 30;
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            _tablesChecked = true;
        }

        // ══════════════════════════════════════════════════════════════
        // TRANSPORTPL.KIEROWCA — JEDYNE ŹRÓDŁO PRAWDY (Imie/Nazwisko/Tel/Aktywny)
        // ══════════════════════════════════════════════════════════════

        private const string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private sealed class TKierowca
        {
            public int KierowcaID; public int LibraNetDriverGID;
            public string Imie = ""; public string Nazwisko = "";
            public string? Telefon; public bool Aktywny;
            public string FullName => $"{Imie} {Nazwisko}".Trim();
        }

        private Dictionary<int, TKierowca>? _tCache;
        private DateTime _tCacheTime;

        /// <summary>Zwraca mapę LibraNetDriverGID → TransportPL.Kierowca. Cache 60s.
        /// Przy pierwszym wywolaniu auto-backfilluje TransportPL.Kierowca z LibraNet.Driver
        /// dla wszystkich niezmapowanych — gwarantuje ze KAZDY LibraNet.Driver ma swoj
        /// rekord w TransportPL.Kierowca z LibraNetDriverGID ustawionym.</summary>
        private async Task<Dictionary<int, TKierowca>> LoadTransportKierowcyMapAsync()
        {
            if (_tCache != null && (DateTime.UtcNow - _tCacheTime).TotalSeconds < 60)
                return _tCache;

            var map = await FetchTransportMapAsync();

            // Backfill: dla każdego LibraNet.Driver bez TransportPL match → INSERT
            try
            {
                var libraDrivers = await FetchLibraNetDriversForBackfillAsync();
                var toInsert = libraDrivers.Where(d => !map.ContainsKey(d.gid)).ToList();
                if (toInsert.Count > 0)
                {
                    foreach (var (gid, imie, nazwisko, tel, halt) in toInsert)
                        await SyncToTransportPLAsync(gid, imie, nazwisko, tel, !halt);
                    map = await FetchTransportMapAsync();  // re-fetch po insertach
                }
            }
            catch { /* backfill best-effort, nie blokuje czytania */ }

            _tCache = map; _tCacheTime = DateTime.UtcNow;
            return map;
        }

        private async Task<Dictionary<int, TKierowca>> FetchTransportMapAsync()
        {
            var map = new Dictionary<int, TKierowca>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT KierowcaID, LibraNetDriverGID, Imie, Nazwisko, Telefon, Aktywny
                  FROM dbo.Kierowca WHERE LibraNetDriverGID IS NOT NULL", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var gid = Convert.ToInt32(r["LibraNetDriverGID"]);
                map[gid] = new TKierowca
                {
                    KierowcaID = Convert.ToInt32(r["KierowcaID"]),
                    LibraNetDriverGID = gid,
                    Imie = r["Imie"]?.ToString() ?? "",
                    Nazwisko = r["Nazwisko"]?.ToString() ?? "",
                    Telefon = r["Telefon"] == DBNull.Value ? null : r["Telefon"].ToString(),
                    Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"])
                };
            }
            return map;
        }

        /// <summary>Ładuje LibraNet.Driver + DriverDetails (FirstName/LastName/Phone1) dla backfill.</summary>
        private async Task<List<(int gid, string imie, string nazwisko, string? tel, bool halt)>> FetchLibraNetDriversForBackfillAsync()
        {
            var list = new List<(int, string, string, string?, bool)>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT d.GID, d.Name, d.Halt, dd.FirstName, dd.LastName, dd.Phone1
                FROM Driver d
                LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE d.Deleted = 0", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int gid = Convert.ToInt32(r["GID"]);
                string fn = r["FirstName"] == DBNull.Value ? "" : r["FirstName"].ToString() ?? "";
                string ln = r["LastName"]  == DBNull.Value ? "" : r["LastName"].ToString()  ?? "";
                // Fallback: split Driver.Name "Imie Nazwisko" gdy DriverDetails puste
                if (string.IsNullOrWhiteSpace(fn) && string.IsNullOrWhiteSpace(ln))
                {
                    var name = r["Name"]?.ToString() ?? "";
                    var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1) fn = parts[0];
                    if (parts.Length >= 2) ln = parts[1];
                }
                string? tel = r["Phone1"] == DBNull.Value ? null : r["Phone1"].ToString();
                bool halt = r["Halt"] != DBNull.Value && Convert.ToBoolean(r["Halt"]);
                list.Add((gid, fn, ln, tel, halt));
            }
            return list;
        }

        /// <summary>UPSERT do TransportPL.Kierowca po LibraNetDriverGID (sync edycji).</summary>
        private async Task SyncToTransportPLAsync(int libraGid, string imie, string nazwisko, string? telefon, bool aktywny)
        {
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                MERGE dbo.Kierowca AS tgt
                USING (SELECT @gid AS LibraNetDriverGID) AS src ON tgt.LibraNetDriverGID = src.LibraNetDriverGID
                WHEN MATCHED THEN UPDATE SET
                    Imie=@imie, Nazwisko=@nazwisko, Telefon=@tel, Aktywny=@akt, ZmienionoUTC=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (Imie, Nazwisko, Telefon, Aktywny, LibraNetDriverGID, UtworzonoUTC)
                    VALUES (@imie, @nazwisko, @tel, @akt, @gid, SYSUTCDATETIME());", conn);
            cmd.Parameters.AddWithValue("@gid", libraGid);
            cmd.Parameters.AddWithValue("@imie", (object?)imie ?? "");
            cmd.Parameters.AddWithValue("@nazwisko", (object?)nazwisko ?? "");
            cmd.Parameters.AddWithValue("@tel", (object?)telefon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@akt", aktywny);
            await cmd.ExecuteNonQueryAsync();
            _tCache = null;  // unieważnij cache
        }

        // ══════════════════════════════════════════════════════════════
        // KIEROWCY
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetDriversAsync()
        {
            // ═══ PRIMARY = TransportPL.Kierowca (lista wszystkich, ten sam zestaw co edytor kursu) ═══
            var tmap = await LoadTransportKierowcyMapAsync();  // backfill + cache
            var tAll = await FetchTransportKierowcyAllAsync();  // wszyscy z TransportPL (też niezmapowani)
            var detailMap = await FetchLibraNetDriverDetailMapAsync();  // detale po LibraNet GID

            var dt = new DataTable();
            dt.Columns.Add("GID", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("FirstName", typeof(string));
            dt.Columns.Add("LastName", typeof(string));
            dt.Columns.Add("Phone1", typeof(string));
            dt.Columns.Add("Halt", typeof(bool));
            dt.Columns.Add("Email", typeof(string));
            dt.Columns.Add("TypZatrudnienia", typeof(string));
            dt.Columns.Add("DataZatrudnienia", typeof(DateTime));
            dt.Columns.Add("DataZwolnienia", typeof(DateTime));
            dt.Columns.Add("DataWaznosciPJ", typeof(DateTime));
            dt.Columns.Add("KategoriePrawaJazdy", typeof(string));
            dt.Columns.Add("DataWazBadanLek", typeof(DateTime));
            dt.Columns.Add("DataWazBHP", typeof(DateTime));
            dt.Columns.Add("AktualneAuta", typeof(string));
            dt.Columns.Add("KursySkup30d", typeof(int));
            dt.Columns.Add("Km30d", typeof(decimal));
            dt.Columns.Add("Created", typeof(DateTime));

            foreach (var k in tAll.OrderBy(t => !t.Aktywny).ThenBy(t => t.Nazwisko).ThenBy(t => t.Imie))
            {
                var row = dt.NewRow();
                // GID = LibraNet GID gdy zmapowany (potrzebne dla DriverVehicleAssignment FK), inaczej -KierowcaID
                int gid = k.LibraNetDriverGID > 0 ? k.LibraNetDriverGID : -k.KierowcaID;
                row["GID"] = gid;
                row["Name"] = k.FullName;
                row["FirstName"] = k.Imie;
                row["LastName"] = k.Nazwisko;
                row["Phone1"] = (object?)k.Telefon ?? DBNull.Value;
                row["Halt"] = !k.Aktywny;
                row["KursySkup30d"] = 0;
                row["Km30d"] = 0m;

                // Opcjonalny LibraNet detail (PJ/badania/BHP/AktualneAuta) gdy zmapowany
                if (gid > 0 && detailMap.TryGetValue(gid, out var d))
                {
                    row["Email"] = (object?)d.Email ?? DBNull.Value;
                    row["TypZatrudnienia"] = (object?)d.TypZatr ?? DBNull.Value;
                    if (d.DataZatr.HasValue) row["DataZatrudnienia"] = d.DataZatr.Value;
                    if (d.DataZwoln.HasValue) row["DataZwolnienia"] = d.DataZwoln.Value;
                    if (d.DataPJ.HasValue) row["DataWaznosciPJ"] = d.DataPJ.Value;
                    row["KategoriePrawaJazdy"] = (object?)d.KategoriePJ ?? DBNull.Value;
                    if (d.DataBadan.HasValue) row["DataWazBadanLek"] = d.DataBadan.Value;
                    if (d.DataBHP.HasValue) row["DataWazBHP"] = d.DataBHP.Value;
                    row["AktualneAuta"] = (object?)d.AktualneAuta ?? DBNull.Value;
                    row["KursySkup30d"] = d.Kursy30;
                    row["Km30d"] = d.Km30;
                    if (d.Created.HasValue) row["Created"] = d.Created.Value;
                }
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ═══ Helpers — primary fetch TransportPL.Kierowca ═══
        private async Task<List<TKierowca>> FetchTransportKierowcyAllAsync()
        {
            var list = new List<TKierowca>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT KierowcaID, ISNULL(LibraNetDriverGID, 0) AS LibraNetDriverGID,
                         Imie, Nazwisko, Telefon, Aktywny
                  FROM dbo.Kierowca", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new TKierowca
                {
                    KierowcaID = Convert.ToInt32(r["KierowcaID"]),
                    LibraNetDriverGID = Convert.ToInt32(r["LibraNetDriverGID"]),
                    Imie = r["Imie"]?.ToString() ?? "",
                    Nazwisko = r["Nazwisko"]?.ToString() ?? "",
                    Telefon = r["Telefon"] == DBNull.Value ? null : r["Telefon"].ToString(),
                    Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"])
                });
            }
            return list;
        }

        private sealed class LDriverDetail
        {
            public string? Email; public string? TypZatr;
            public DateTime? DataZatr, DataZwoln, DataPJ, DataBadan, DataBHP, Created;
            public string? KategoriePJ; public string? AktualneAuta;
            public int Kursy30; public decimal Km30;
        }

        private async Task<Dictionary<int, LDriverDetail>> FetchLibraNetDriverDetailMapAsync()
        {
            var map = new Dictionary<int, LDriverDetail>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT d.GID, d.Created, dd.Email,
                           dd.TypZatrudnienia, dd.DataZatrudnienia, dd.DataZwolnienia,
                           dd.DataWaznosciPJ, dd.KategoriePrawaJazdy,
                           dd.DataWazBadanLek, dd.DataWazBHP,
                           STUFF((SELECT ', ' + ct.ID + ' (' + dva.Rola + ')'
                                  FROM DriverVehicleAssignment dva
                                  JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                                  WHERE dva.DriverGID = d.GID AND dva.DataDo IS NULL
                                  FOR XML PATH('')), 1, 2, '') AS AktualneAuta,
                           (SELECT COUNT(*) FROM FarmerCalc WHERE DriverGID = d.GID
                                  AND CalcDate >= DATEADD(DAY,-30,GETDATE())) AS Kursy30,
                           (SELECT ISNULL(SUM(DistanceKM),0) FROM FarmerCalc WHERE DriverGID = d.GID
                                  AND CalcDate >= DATEADD(DAY,-30,GETDATE())) AS Km30
                    FROM Driver d
                    LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                    WHERE d.Deleted = 0", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int gid = Convert.ToInt32(r["GID"]);
                    map[gid] = new LDriverDetail
                    {
                        Email = r["Email"] == DBNull.Value ? null : r["Email"].ToString(),
                        TypZatr = r["TypZatrudnienia"] == DBNull.Value ? null : r["TypZatrudnienia"].ToString(),
                        DataZatr = r["DataZatrudnienia"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataZatrudnienia"],
                        DataZwoln = r["DataZwolnienia"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataZwolnienia"],
                        DataPJ = r["DataWaznosciPJ"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataWaznosciPJ"],
                        KategoriePJ = r["KategoriePrawaJazdy"] == DBNull.Value ? null : r["KategoriePrawaJazdy"].ToString(),
                        DataBadan = r["DataWazBadanLek"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataWazBadanLek"],
                        DataBHP = r["DataWazBHP"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataWazBHP"],
                        AktualneAuta = r["AktualneAuta"] == DBNull.Value ? null : r["AktualneAuta"].ToString(),
                        Kursy30 = r["Kursy30"] == DBNull.Value ? 0 : Convert.ToInt32(r["Kursy30"]),
                        Km30 = r["Km30"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Km30"]),
                        Created = r["Created"] == DBNull.Value ? (DateTime?)null : (DateTime)r["Created"]
                    };
                }
            }
            catch { /* LibraNet niedostępne — detail będzie pusty */ }
            return map;
        }

        /// <summary>
        /// gid > 0 → LibraNet.Driver.GID (zmapowany). gid < 0 → -KierowcaID (niezmapowany w TransportPL).
        /// PRIMARY TransportPL.Kierowca; LibraNet.DriverDetails dorzucony gdy zmapowany.
        /// </summary>
        public async Task<DataRow?> GetDriverByGIDAsync(int gid)
        {
            // Pobierz primary z TransportPL
            int? libraGid = gid > 0 ? gid : (int?)null;
            int? kierowcaId = gid < 0 ? -gid : (int?)null;

            TKierowca? k = null;
            using (var conn = new SqlConnection(_connTransport))
            {
                await conn.OpenAsync();
                var sql = libraGid.HasValue
                    ? @"SELECT TOP 1 KierowcaID, ISNULL(LibraNetDriverGID,0) AS LibraNetDriverGID,
                               Imie, Nazwisko, Telefon, Aktywny
                        FROM dbo.Kierowca WHERE LibraNetDriverGID = @id"
                    : @"SELECT TOP 1 KierowcaID, ISNULL(LibraNetDriverGID,0) AS LibraNetDriverGID,
                               Imie, Nazwisko, Telefon, Aktywny
                        FROM dbo.Kierowca WHERE KierowcaID = @id";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", libraGid ?? kierowcaId ?? 0);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    k = new TKierowca
                    {
                        KierowcaID = Convert.ToInt32(r["KierowcaID"]),
                        LibraNetDriverGID = Convert.ToInt32(r["LibraNetDriverGID"]),
                        Imie = r["Imie"]?.ToString() ?? "",
                        Nazwisko = r["Nazwisko"]?.ToString() ?? "",
                        Telefon = r["Telefon"] == DBNull.Value ? null : r["Telefon"].ToString(),
                        Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"])
                    };
                }
            }
            if (k == null) return null;

            // Zbuduj DataTable z 1 wierszem (zachowując strukturę używaną przez DriverEditWindow)
            var dt = new DataTable();
            dt.Columns.Add("GID", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("FirstName", typeof(string));
            dt.Columns.Add("LastName", typeof(string));
            dt.Columns.Add("Phone1", typeof(string));
            dt.Columns.Add("Phone2", typeof(string));
            dt.Columns.Add("Email", typeof(string));
            dt.Columns.Add("PESEL", typeof(string));
            dt.Columns.Add("Halt", typeof(bool));
            dt.Columns.Add("Typ", typeof(int));
            dt.Columns.Add("Created", typeof(DateTime));
            dt.Columns.Add("TypZatrudnienia", typeof(string));
            dt.Columns.Add("DataZatrudnienia", typeof(DateTime));
            dt.Columns.Add("DataZwolnienia", typeof(DateTime));
            dt.Columns.Add("NrPrawaJazdy", typeof(string));
            dt.Columns.Add("KategoriePrawaJazdy", typeof(string));
            dt.Columns.Add("DataWaznosciPJ", typeof(DateTime));
            dt.Columns.Add("NrBadanLekarskich", typeof(string));
            dt.Columns.Add("DataWazBadanLek", typeof(DateTime));
            dt.Columns.Add("NrSzkoleniaBHP", typeof(string));
            dt.Columns.Add("DataWazBHP", typeof(DateTime));
            dt.Columns.Add("Uwagi", typeof(string));
            dt.Columns.Add("ZdjecieKierowcy", typeof(byte[]));

            var row = dt.NewRow();
            row["GID"] = k.LibraNetDriverGID > 0 ? k.LibraNetDriverGID : -k.KierowcaID;
            row["Name"] = k.FullName;
            row["FirstName"] = k.Imie;
            row["LastName"] = k.Nazwisko;
            row["Phone1"] = (object?)k.Telefon ?? DBNull.Value;
            row["Halt"] = !k.Aktywny;

            // Dorzuć detail z LibraNet (jeśli zmapowany)
            if (k.LibraNetDriverGID > 0)
            {
                try
                {
                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(@"
                        SELECT d.Typ, d.Created, dd.*
                        FROM Driver d LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                        WHERE d.GID = @GID AND d.Deleted = 0", conn);
                    cmd.Parameters.AddWithValue("@GID", k.LibraNetDriverGID);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        void CopyIf(string col, string src) {
                            if (dt.Columns.Contains(col) && HasCol(r, src) && r[src] != DBNull.Value) row[col] = r[src];
                        }
                        CopyIf("Typ", "Typ");
                        CopyIf("Created", "Created");
                        CopyIf("Phone2", "Phone2");
                        CopyIf("Email", "Email");
                        CopyIf("PESEL", "PESEL");
                        CopyIf("TypZatrudnienia", "TypZatrudnienia");
                        CopyIf("DataZatrudnienia", "DataZatrudnienia");
                        CopyIf("DataZwolnienia", "DataZwolnienia");
                        CopyIf("NrPrawaJazdy", "NrPrawaJazdy");
                        CopyIf("KategoriePrawaJazdy", "KategoriePrawaJazdy");
                        CopyIf("DataWaznosciPJ", "DataWaznosciPJ");
                        CopyIf("NrBadanLekarskich", "NrBadanLekarskich");
                        CopyIf("DataWazBadanLek", "DataWazBadanLek");
                        CopyIf("NrSzkoleniaBHP", "NrSzkoleniaBHP");
                        CopyIf("DataWazBHP", "DataWazBHP");
                        CopyIf("Uwagi", "Uwagi");
                        CopyIf("ZdjecieKierowcy", "ZdjecieKierowcy");
                    }
                }
                catch { /* LibraNet detail opcjonalne */ }
            }
            dt.Rows.Add(row);
            return dt.Rows[0];
        }

        private static bool HasCol(System.Data.IDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public async Task<int> SaveDriverAsync(int? gid, string firstName, string lastName,
            bool halt, int? typ,
            string? phone1, string? phone2, string? email, string? pesel,
            string? nrPJ, string? kategoriePJ, DateTime? dataPJ,
            string? nrBadan, DateTime? dataBadan,
            string? nrBHP, DateTime? dataBHP,
            DateTime? dataZatrudnienia, DateTime? dataZwolnienia,
            string? typZatrudnienia, string? uwagi, byte[]? zdjecie,
            string user)
        {
            string fullName = $"{firstName} {lastName}".Trim();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            int driverGID = 0;
            try
            {
                if (gid.HasValue)
                {
                    // UPDATE Driver
                    const string sqlUpd = @"UPDATE Driver SET Name=@Name, Halt=@Halt, Typ=@Typ,
                        Modified=GETDATE(), ModifiedBy=@User WHERE GID=@GID";
                    using var cmdUpd = new SqlCommand(sqlUpd, conn, tx);
                    cmdUpd.Parameters.AddWithValue("@Name", fullName);
                    cmdUpd.Parameters.AddWithValue("@Halt", halt);
                    cmdUpd.Parameters.AddWithValue("@Typ", (object?)typ ?? DBNull.Value);
                    cmdUpd.Parameters.AddWithValue("@User", user);
                    cmdUpd.Parameters.AddWithValue("@GID", gid.Value);
                    await cmdUpd.ExecuteNonQueryAsync();
                    driverGID = gid.Value;

                    // UPSERT DriverDetails
                    const string sqlMerge = @"
                        MERGE DriverDetails AS tgt
                        USING (SELECT @GID AS DriverGID) AS src ON tgt.DriverGID = src.DriverGID
                        WHEN MATCHED THEN UPDATE SET
                            FirstName=@FirstName, LastName=@LastName, Phone1=@Phone1, Phone2=@Phone2,
                            Email=@Email, PESEL=@PESEL, NrPrawaJazdy=@NrPJ,
                            KategoriePrawaJazdy=@KategoriePJ, DataWaznosciPJ=@DataPJ,
                            NrBadanLekarskich=@NrBadan, DataWazBadanLek=@DataBadan,
                            NrSzkoleniaBHP=@NrBHP, DataWazBHP=@DataBHP,
                            DataZatrudnienia=@DataZatr, DataZwolnienia=@DataZwoln,
                            TypZatrudnienia=@TypZatr, Uwagi=@Uwagi, ZdjecieKierowcy=@Zdjecie,
                            ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@User
                        WHEN NOT MATCHED THEN INSERT
                            (DriverGID, FirstName, LastName, Phone1, Phone2, Email, PESEL,
                             NrPrawaJazdy, KategoriePrawaJazdy, DataWaznosciPJ,
                             NrBadanLekarskich, DataWazBadanLek, NrSzkoleniaBHP, DataWazBHP,
                             DataZatrudnienia, DataZwolnienia, TypZatrudnienia, Uwagi, ZdjecieKierowcy, ModifiedBy)
                        VALUES (@GID, @FirstName, @LastName, @Phone1, @Phone2, @Email, @PESEL,
                             @NrPJ, @KategoriePJ, @DataPJ,
                             @NrBadan, @DataBadan, @NrBHP, @DataBHP,
                             @DataZatr, @DataZwoln, @TypZatr, @Uwagi, @Zdjecie, @User);";
                    using var cmdM = new SqlCommand(sqlMerge, conn, tx);
                    AddDriverDetailsParams(cmdM, driverGID, firstName, lastName,
                        phone1, phone2, email, pesel, nrPJ, kategoriePJ, dataPJ,
                        nrBadan, dataBadan, nrBHP, dataBHP,
                        dataZatrudnienia, dataZwolnienia, typZatrudnienia, uwagi, zdjecie, user);
                    await cmdM.ExecuteNonQueryAsync();
                }
                else
                {
                    // INSERT Driver
                    const string sqlIns = @"INSERT INTO Driver (Name, Halt, Deleted, Created, ModifiedBy, Typ)
                        VALUES (@Name, 0, 0, GETDATE(), @User, @Typ);
                        SELECT SCOPE_IDENTITY();";
                    using var cmdIns = new SqlCommand(sqlIns, conn, tx);
                    cmdIns.Parameters.AddWithValue("@Name", fullName);
                    cmdIns.Parameters.AddWithValue("@User", user);
                    cmdIns.Parameters.AddWithValue("@Typ", (object?)typ ?? DBNull.Value);
                    var result = await cmdIns.ExecuteScalarAsync();
                    driverGID = Convert.ToInt32(result);

                    // INSERT DriverDetails
                    const string sqlDet = @"INSERT INTO DriverDetails
                        (DriverGID, FirstName, LastName, Phone1, Phone2, Email, PESEL,
                         NrPrawaJazdy, KategoriePrawaJazdy, DataWaznosciPJ,
                         NrBadanLekarskich, DataWazBadanLek, NrSzkoleniaBHP, DataWazBHP,
                         DataZatrudnienia, DataZwolnienia, TypZatrudnienia, Uwagi, ZdjecieKierowcy, ModifiedBy)
                        VALUES (@GID, @FirstName, @LastName, @Phone1, @Phone2, @Email, @PESEL,
                         @NrPJ, @KategoriePJ, @DataPJ,
                         @NrBadan, @DataBadan, @NrBHP, @DataBHP,
                         @DataZatr, @DataZwoln, @TypZatr, @Uwagi, @Zdjecie, @User)";
                    using var cmdDet = new SqlCommand(sqlDet, conn, tx);
                    AddDriverDetailsParams(cmdDet, driverGID, firstName, lastName,
                        phone1, phone2, email, pesel, nrPJ, kategoriePJ, dataPJ,
                        nrBadan, dataBadan, nrBHP, dataBHP,
                        dataZatrudnienia, dataZwolnienia, typZatrudnienia, uwagi, zdjecie, user);
                    await cmdDet.ExecuteNonQueryAsync();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            // SYNC do TransportPL.Kierowca (jedyne źródło Imie/Nazwisko/Telefon/Aktywny)
            try { await SyncToTransportPLAsync(driverGID, firstName, lastName, phone1, !halt); }
            catch { /* sync porażki nie blokują saveLibraNet */ }
            return driverGID;
        }

        private static void AddDriverDetailsParams(SqlCommand cmd, int gid,
            string? firstName, string? lastName,
            string? phone1, string? phone2, string? email, string? pesel,
            string? nrPJ, string? kategoriePJ, DateTime? dataPJ,
            string? nrBadan, DateTime? dataBadan,
            string? nrBHP, DateTime? dataBHP,
            DateTime? dataZatr, DateTime? dataZwoln,
            string? typZatr, string? uwagi, byte[]? zdjecie, string user)
        {
            cmd.Parameters.AddWithValue("@GID", gid);
            cmd.Parameters.AddWithValue("@FirstName", (object?)firstName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastName", (object?)lastName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Phone1", (object?)phone1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Phone2", (object?)phone2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", (object?)email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PESEL", (object?)pesel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NrPJ", (object?)nrPJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KategoriePJ", (object?)kategoriePJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataPJ", (object?)dataPJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NrBadan", (object?)nrBadan ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataBadan", (object?)dataBadan ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NrBHP", (object?)nrBHP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataBHP", (object?)dataBHP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataZatr", (object?)dataZatr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataZwoln", (object?)dataZwoln ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TypZatr", (object?)typZatr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uwagi", (object?)uwagi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Zdjecie", (object?)zdjecie ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@User", user);
        }

        public async Task ToggleDriverHaltAsync(int gid, string user)
        {
            // LibraNet update
            int newHalt;
            using (var conn = new SqlConnection(_connectionString))
            {
                const string sql = @"UPDATE Driver SET Halt = CASE WHEN Halt=0 THEN 1 ELSE 0 END,
                    Modified=GETDATE(), ModifiedBy=@User
                    OUTPUT inserted.Halt
                    WHERE GID=@GID";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@GID", gid);
                cmd.Parameters.AddWithValue("@User", user);
                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                newHalt = result == null ? 0 : Convert.ToInt32(result);
            }

            // Sync Aktywny do TransportPL.Kierowca (TransportPL.Aktywny = !LibraNet.Halt)
            try
            {
                using var conn2 = new SqlConnection(_connTransport);
                await conn2.OpenAsync();
                using var cmd2 = new SqlCommand(
                    @"UPDATE dbo.Kierowca SET Aktywny=@akt, ZmienionoUTC=SYSUTCDATETIME() WHERE LibraNetDriverGID=@gid", conn2);
                cmd2.Parameters.AddWithValue("@gid", gid);
                cmd2.Parameters.AddWithValue("@akt", newHalt == 0);
                await cmd2.ExecuteNonQueryAsync();
                _tCache = null;
            }
            catch { /* sync nie blokuje toggle */ }
        }

        // ══════════════════════════════════════════════════════════════
        // POJAZDY (PRIMARY = TransportPL.Pojazd)
        // ══════════════════════════════════════════════════════════════

        private sealed class TPojazd
        {
            public int PojazdID; public string? LibraNetCarTrailerID;
            public string Rejestracja = ""; public string? Marka; public string? Model;
            public int PaletyH1; public bool Aktywny;
        }

        private sealed class LVehicleDetail
        {
            public string? Kind, Brand, Model, VIN, TypNadwozia, NrPolisyOC, NrPolisyAC, Ubezpieczyciel, GPSModul;
            public int? RokProdukcji, PrzebiegKm, MaxLadownoscKg, MaxPaletH1, PojemnoscBaku, MaxPojemnikE2;
            public decimal? SrednieSpalanie, TemperaturaMin, TemperaturaMax, KosztyYTD, Capacity;
            public DateTime? DataPrzegladu, DataUbezpieczenia;
            public string? AktualnyKierowca, OstatniSerwis, VdUwagi;
        }

        private async Task<List<TPojazd>> FetchTransportPojazdyAllAsync()
        {
            var list = new List<TPojazd>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT PojazdID, LibraNetCarTrailerID, Rejestracja, Marka, Model, PaletyH1, Aktywny
                  FROM dbo.Pojazd", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new TPojazd
                {
                    PojazdID = Convert.ToInt32(r["PojazdID"]),
                    LibraNetCarTrailerID = r["LibraNetCarTrailerID"] == DBNull.Value ? null : r["LibraNetCarTrailerID"].ToString(),
                    Rejestracja = r["Rejestracja"]?.ToString() ?? "",
                    Marka = r["Marka"] == DBNull.Value ? null : r["Marka"].ToString(),
                    Model = r["Model"] == DBNull.Value ? null : r["Model"].ToString(),
                    PaletyH1 = r["PaletyH1"] == DBNull.Value ? 0 : Convert.ToInt32(r["PaletyH1"]),
                    Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"])
                });
            }
            return list;
        }

        private async Task<Dictionary<string, LVehicleDetail>> FetchLibraNetVehicleDetailMapAsync()
        {
            var map = new Dictionary<string, LVehicleDetail>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT ct.ID, ct.Kind, ct.Brand, ct.Model, ct.Capacity,
                           vd.VIN, vd.RokProdukcji, vd.TypNadwozia,
                           vd.DataPrzegladu, vd.DataUbezpieczenia, vd.PrzebiegKm,
                           vd.MaxLadownoscKg, vd.MaxPaletH1, vd.SrednieSpalanie,
                           vd.NrPolisyOC, vd.NrPolisyAC, vd.Ubezpieczyciel,
                           vd.TemperaturaMin, vd.TemperaturaMax, vd.GPSModul,
                           vd.PojemnoscBaku, vd.MaxPojemnikE2, vd.Uwagi AS VdUwagi,
                           STUFF((SELECT ', ' + drv.Name + ' (' + dva.Rola + ')'
                                  FROM DriverVehicleAssignment dva
                                  JOIN Driver drv ON dva.DriverGID = drv.GID
                                  WHERE dva.CarTrailerID = ct.ID AND dva.DataDo IS NULL
                                  FOR XML PATH('')), 1, 2, '') AS AktualnyKierowca,
                           (SELECT TOP 1 TypZdarzenia + ' ' + CONVERT(varchar(10), Data, 120)
                                  FROM VehicleServiceLog WHERE CarTrailerID = ct.ID ORDER BY Data DESC) AS OstatniSerwis,
                           (SELECT ISNULL(SUM(KosztBrutto), 0) FROM VehicleServiceLog
                                  WHERE CarTrailerID = ct.ID AND YEAR(Data) = YEAR(GETDATE())) AS KosztyYTD
                    FROM CarTrailer ct
                    LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string id = r["ID"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(id)) continue;
                    map[id] = new LVehicleDetail
                    {
                        Kind = r["Kind"] == DBNull.Value ? null : r["Kind"].ToString(),
                        Brand = r["Brand"] == DBNull.Value ? null : r["Brand"].ToString(),
                        Model = r["Model"] == DBNull.Value ? null : r["Model"].ToString(),
                        Capacity = r["Capacity"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Capacity"]),
                        VIN = r["VIN"] == DBNull.Value ? null : r["VIN"].ToString(),
                        RokProdukcji = r["RokProdukcji"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["RokProdukcji"]),
                        TypNadwozia = r["TypNadwozia"] == DBNull.Value ? null : r["TypNadwozia"].ToString(),
                        DataPrzegladu = r["DataPrzegladu"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataPrzegladu"],
                        DataUbezpieczenia = r["DataUbezpieczenia"] == DBNull.Value ? (DateTime?)null : (DateTime)r["DataUbezpieczenia"],
                        PrzebiegKm = r["PrzebiegKm"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PrzebiegKm"]),
                        MaxLadownoscKg = r["MaxLadownoscKg"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["MaxLadownoscKg"]),
                        MaxPaletH1 = r["MaxPaletH1"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["MaxPaletH1"]),
                        SrednieSpalanie = r["SrednieSpalanie"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["SrednieSpalanie"]),
                        NrPolisyOC = r["NrPolisyOC"] == DBNull.Value ? null : r["NrPolisyOC"].ToString(),
                        NrPolisyAC = r["NrPolisyAC"] == DBNull.Value ? null : r["NrPolisyAC"].ToString(),
                        Ubezpieczyciel = r["Ubezpieczyciel"] == DBNull.Value ? null : r["Ubezpieczyciel"].ToString(),
                        TemperaturaMin = r["TemperaturaMin"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["TemperaturaMin"]),
                        TemperaturaMax = r["TemperaturaMax"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["TemperaturaMax"]),
                        GPSModul = r["GPSModul"] == DBNull.Value ? null : r["GPSModul"].ToString(),
                        PojemnoscBaku = r["PojemnoscBaku"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PojemnoscBaku"]),
                        MaxPojemnikE2 = r["MaxPojemnikE2"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["MaxPojemnikE2"]),
                        VdUwagi = r["VdUwagi"] == DBNull.Value ? null : r["VdUwagi"].ToString(),
                        AktualnyKierowca = r["AktualnyKierowca"] == DBNull.Value ? null : r["AktualnyKierowca"].ToString(),
                        OstatniSerwis = r["OstatniSerwis"] == DBNull.Value ? null : r["OstatniSerwis"].ToString(),
                        KosztyYTD = r["KosztyYTD"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["KosztyYTD"])
                    };
                }
            }
            catch { /* LibraNet niedostępne */ }
            return map;
        }

        /// <summary>Backfill: dla każdego LibraNet.CarTrailer bez TransportPL.Pojazd match → INSERT do TransportPL.</summary>
        private async Task EnsurePojazdyBackfillAsync()
        {
            try
            {
                var tList = await FetchTransportPojazdyAllAsync();
                var mapped = new HashSet<string>(
                    tList.Where(t => !string.IsNullOrEmpty(t.LibraNetCarTrailerID))
                         .Select(t => t.LibraNetCarTrailerID!),
                    StringComparer.OrdinalIgnoreCase);

                // Pobierz LibraNet CarTrailer
                var libraVehicles = new List<(string id, string? kind, string? brand, string? model, string? reg, int paletyH1)>();
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(@"
                        SELECT ct.ID, ct.Kind, ct.Brand, ct.Model,
                               ISNULL(vd.Registration, ct.ID) AS Reg,
                               ISNULL(vd.MaxPaletH1, 0) AS PaletyH1
                        FROM CarTrailer ct LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID", conn);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        string id = r["ID"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(id) || mapped.Contains(id)) continue;
                        libraVehicles.Add((id,
                            r["Kind"] == DBNull.Value ? null : r["Kind"].ToString(),
                            r["Brand"] == DBNull.Value ? null : r["Brand"].ToString(),
                            r["Model"] == DBNull.Value ? null : r["Model"].ToString(),
                            r["Reg"]?.ToString(),
                            r["PaletyH1"] == DBNull.Value ? 0 : Convert.ToInt32(r["PaletyH1"])));
                    }
                }

                foreach (var v in libraVehicles)
                    await SyncToTransportPLPojazdAsync(v.id, v.reg ?? v.id, v.brand, v.model, v.paletyH1, true);
            }
            catch { /* backfill best-effort */ }
        }

        private async Task SyncToTransportPLPojazdAsync(string libraId, string rejestracja, string? marka, string? model, int paletyH1, bool aktywny)
        {
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                MERGE dbo.Pojazd AS tgt
                USING (SELECT @id AS LibraNetCarTrailerID) AS src ON tgt.LibraNetCarTrailerID = src.LibraNetCarTrailerID
                WHEN MATCHED THEN UPDATE SET
                    Rejestracja=@rej, Marka=@marka, Model=@model, PaletyH1=@palety, Aktywny=@akt,
                    ZmienionoUTC=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (Rejestracja, Marka, Model, PaletyH1, Aktywny, LibraNetCarTrailerID, UtworzonoUTC)
                    VALUES (@rej, @marka, @model, @palety, @akt, @id, SYSUTCDATETIME());", conn);
            cmd.Parameters.AddWithValue("@id", libraId);
            cmd.Parameters.AddWithValue("@rej", (object?)rejestracja ?? "");
            cmd.Parameters.AddWithValue("@marka", (object?)marka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model", (object?)model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@palety", paletyH1);
            cmd.Parameters.AddWithValue("@akt", aktywny);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<DataTable> GetVehiclesAsync()
        {
            // ═══ WYŁĄCZNIE TransportPL.Pojazd — bez LibraNet, bez backfill ═══
            var tAll = await FetchTransportPojazdyAllAsync();

            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("Kind", typeof(string));
            dt.Columns.Add("Brand", typeof(string));
            dt.Columns.Add("Model", typeof(string));
            dt.Columns.Add("Registration", typeof(string));
            dt.Columns.Add("MaxPaletH1", typeof(int));
            dt.Columns.Add("AktualnyKierowca", typeof(string));
            dt.Columns.Add("KosztyYTD", typeof(decimal));
            // Pozostałe kolumny zachowane w schema dla detail panel (puste — TransportPL.Pojazd ich nie ma):
            dt.Columns.Add("Capacity", typeof(decimal));
            dt.Columns.Add("VIN", typeof(string));
            dt.Columns.Add("RokProdukcji", typeof(int));
            dt.Columns.Add("TypNadwozia", typeof(string));
            dt.Columns.Add("DataPrzegladu", typeof(DateTime));
            dt.Columns.Add("DataUbezpieczenia", typeof(DateTime));
            dt.Columns.Add("PrzebiegKm", typeof(int));
            dt.Columns.Add("MaxLadownoscKg", typeof(int));
            dt.Columns.Add("SrednieSpalanie", typeof(decimal));
            dt.Columns.Add("NrPolisyOC", typeof(string));
            dt.Columns.Add("NrPolisyAC", typeof(string));
            dt.Columns.Add("Ubezpieczyciel", typeof(string));
            dt.Columns.Add("TemperaturaMin", typeof(decimal));
            dt.Columns.Add("TemperaturaMax", typeof(decimal));
            dt.Columns.Add("GPSModul", typeof(string));
            dt.Columns.Add("PojemnoscBaku", typeof(int));
            dt.Columns.Add("MaxPojemnikE2", typeof(int));
            dt.Columns.Add("VdUwagi", typeof(string));
            dt.Columns.Add("OstatniSerwis", typeof(string));

            foreach (var t in tAll.OrderBy(t => !t.Aktywny).ThenBy(t => t.Rejestracja))
            {
                var row = dt.NewRow();
                row["ID"] = t.PojazdID.ToString();
                row["Registration"] = t.Rejestracja;
                row["Brand"] = (object?)t.Marka ?? DBNull.Value;
                row["Model"] = (object?)t.Model ?? DBNull.Value;
                row["MaxPaletH1"] = t.PaletyH1;
                row["KosztyYTD"] = 0m;
                dt.Rows.Add(row);
            }
            return dt;
        }

        public async Task<DataRow?> GetVehicleByIDAsync(string carTrailerID)
        {
            const string sql = @"
                SELECT ct.*, vd.*
                FROM CarTrailer ct
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE ct.ID = @ID";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public async Task SaveVehicleAsync(string id, bool isNew, int kind, string? brand, string? model,
            decimal? capacity, string? registration, string? vin, int? rokProdukcji,
            DateTime? dataPrzegladu, DateTime? dataUbezpieczenia,
            string? nrOC, string? nrAC, string? ubezpieczyciel,
            int? przebiegKm, decimal? spalanie, int? pojemnoscBaku,
            int? maxLadownosc, int? maxPalet, int? maxE2,
            string? typNadwozia, decimal? tempMin, decimal? tempMax,
            string? gpsModul, string? uwagi, byte[]? zdjecie, string user)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                if (isNew)
                {
                    const string sqlIns = @"INSERT INTO CarTrailer (ID, Kind, Brand, Model, Capacity, Created, Modified, ModifiedBy)
                        VALUES (@ID, @Kind, @Brand, @Model, @Capacity, GETDATE(), GETDATE(), @User)";
                    using var cmdIns = new SqlCommand(sqlIns, conn, tx);
                    cmdIns.Parameters.AddWithValue("@ID", id);
                    cmdIns.Parameters.AddWithValue("@Kind", kind);
                    cmdIns.Parameters.AddWithValue("@Brand", (object?)brand ?? DBNull.Value);
                    cmdIns.Parameters.AddWithValue("@Model", (object?)model ?? DBNull.Value);
                    cmdIns.Parameters.AddWithValue("@Capacity", (object?)capacity ?? DBNull.Value);
                    cmdIns.Parameters.AddWithValue("@User", user);
                    await cmdIns.ExecuteNonQueryAsync();
                }
                else
                {
                    const string sqlUpd = @"UPDATE CarTrailer SET Kind=@Kind, Brand=@Brand, Model=@Model,
                        Capacity=@Capacity, Modified=GETDATE(), ModifiedBy=@User WHERE ID=@ID";
                    using var cmdUpd = new SqlCommand(sqlUpd, conn, tx);
                    cmdUpd.Parameters.AddWithValue("@ID", id);
                    cmdUpd.Parameters.AddWithValue("@Kind", kind);
                    cmdUpd.Parameters.AddWithValue("@Brand", (object?)brand ?? DBNull.Value);
                    cmdUpd.Parameters.AddWithValue("@Model", (object?)model ?? DBNull.Value);
                    cmdUpd.Parameters.AddWithValue("@Capacity", (object?)capacity ?? DBNull.Value);
                    cmdUpd.Parameters.AddWithValue("@User", user);
                    await cmdUpd.ExecuteNonQueryAsync();
                }

                // UPSERT VehicleDetails
                const string sqlMerge = @"
                    MERGE VehicleDetails AS tgt
                    USING (SELECT @ID AS CarTrailerID) AS src ON tgt.CarTrailerID = src.CarTrailerID
                    WHEN MATCHED THEN UPDATE SET
                        Registration=@Reg, VIN=@VIN, RokProdukcji=@Rok,
                        DataPrzegladu=@DataPrzegl, DataUbezpieczenia=@DataUbezp,
                        NrPolisyOC=@NrOC, NrPolisyAC=@NrAC, Ubezpieczyciel=@Ubezp,
                        PrzebiegKm=@Przebieg, SrednieSpalanie=@Spalanie, PojemnoscBaku=@Bak,
                        MaxLadownoscKg=@MaxLad, MaxPaletH1=@MaxPalet, MaxPojemnikE2=@MaxE2,
                        TypNadwozia=@TypNad, TemperaturaMin=@TempMin, TemperaturaMax=@TempMax,
                        GPSModul=@GPS, Uwagi=@Uwagi, ZdjeciePojazdu=@Zdjecie,
                        ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@User
                    WHEN NOT MATCHED THEN INSERT
                        (CarTrailerID, Registration, VIN, RokProdukcji,
                         DataPrzegladu, DataUbezpieczenia, NrPolisyOC, NrPolisyAC, Ubezpieczyciel,
                         PrzebiegKm, SrednieSpalanie, PojemnoscBaku,
                         MaxLadownoscKg, MaxPaletH1, MaxPojemnikE2,
                         TypNadwozia, TemperaturaMin, TemperaturaMax, GPSModul, Uwagi, ZdjeciePojazdu, ModifiedBy)
                    VALUES (@ID, @Reg, @VIN, @Rok,
                         @DataPrzegl, @DataUbezp, @NrOC, @NrAC, @Ubezp,
                         @Przebieg, @Spalanie, @Bak,
                         @MaxLad, @MaxPalet, @MaxE2,
                         @TypNad, @TempMin, @TempMax, @GPS, @Uwagi, @Zdjecie, @User);";
                using var cmdM = new SqlCommand(sqlMerge, conn, tx);
                cmdM.Parameters.AddWithValue("@ID", id);
                cmdM.Parameters.AddWithValue("@Reg", (object?)registration ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@VIN", (object?)vin ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Rok", (object?)rokProdukcji ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@DataPrzegl", (object?)dataPrzegladu ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@DataUbezp", (object?)dataUbezpieczenia ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@NrOC", (object?)nrOC ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@NrAC", (object?)nrAC ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Ubezp", (object?)ubezpieczyciel ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Przebieg", (object?)przebiegKm ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Spalanie", (object?)spalanie ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Bak", (object?)pojemnoscBaku ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@MaxLad", (object?)maxLadownosc ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@MaxPalet", (object?)maxPalet ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@MaxE2", (object?)maxE2 ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@TypNad", (object?)typNadwozia ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@TempMin", (object?)tempMin ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@TempMax", (object?)tempMax ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@GPS", (object?)gpsModul ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Uwagi", (object?)uwagi ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@Zdjecie", (object?)zdjecie ?? DBNull.Value);
                cmdM.Parameters.AddWithValue("@User", user);
                await cmdM.ExecuteNonQueryAsync();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // PRZYPISANIA
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetAssignmentsAsync(bool onlyActive)
        {
            string sql = @"
                SELECT dva.ID, dva.DriverGID, d.Name AS KierowcaNazwa,
                       dva.CarTrailerID, ct.Brand + ' ' + ISNULL(ct.Model,'') AS PojazdMarka,
                       vd.Registration AS Rejestracja,
                       dva.Rola, dva.DataOd, dva.DataDo, dva.Powod, dva.Uwagi,
                       DATEDIFF(DAY, dva.DataOd, ISNULL(dva.DataDo, GETDATE())) AS Dni
                FROM DriverVehicleAssignment dva
                JOIN Driver d ON dva.DriverGID = d.GID
                JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE 1=1" +
                (onlyActive ? " AND dva.DataDo IS NULL" : "") +
                @" ORDER BY CASE WHEN dva.DataDo IS NULL THEN 0 ELSE 1 END, d.Name, dva.DataOd DESC";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task AssignDriverAsync(int driverGID, string carTrailerID, string rola,
            DateTime dataOd, string? powod, bool closeExistingForDriver, string user)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Close existing assignment of this driver to this vehicle
                const string sqlClose1 = @"UPDATE DriverVehicleAssignment
                    SET DataDo = @DataOd WHERE DriverGID = @DriverGID AND CarTrailerID = @CarID AND DataDo IS NULL";
                using var cmd1 = new SqlCommand(sqlClose1, conn, tx);
                cmd1.Parameters.AddWithValue("@DriverGID", driverGID);
                cmd1.Parameters.AddWithValue("@CarID", carTrailerID);
                cmd1.Parameters.AddWithValue("@DataOd", dataOd);
                await cmd1.ExecuteNonQueryAsync();

                if (closeExistingForDriver)
                {
                    const string sqlClose2 = @"UPDATE DriverVehicleAssignment
                        SET DataDo = @DataOd WHERE DriverGID = @DriverGID AND DataDo IS NULL";
                    using var cmd2 = new SqlCommand(sqlClose2, conn, tx);
                    cmd2.Parameters.AddWithValue("@DriverGID", driverGID);
                    cmd2.Parameters.AddWithValue("@DataOd", dataOd);
                    await cmd2.ExecuteNonQueryAsync();
                }

                const string sqlIns = @"INSERT INTO DriverVehicleAssignment
                    (DriverGID, CarTrailerID, Rola, DataOd, Powod, CreatedBy)
                    VALUES (@DriverGID, @CarID, @Rola, @DataOd, @Powod, @User)";
                using var cmdIns = new SqlCommand(sqlIns, conn, tx);
                cmdIns.Parameters.AddWithValue("@DriverGID", driverGID);
                cmdIns.Parameters.AddWithValue("@CarID", carTrailerID);
                cmdIns.Parameters.AddWithValue("@Rola", rola);
                cmdIns.Parameters.AddWithValue("@DataOd", dataOd);
                cmdIns.Parameters.AddWithValue("@Powod", (object?)powod ?? DBNull.Value);
                cmdIns.Parameters.AddWithValue("@User", user);
                await cmdIns.ExecuteNonQueryAsync();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task EndAssignmentAsync(int assignmentId, DateTime dataDo)
        {
            const string sql = @"UPDATE DriverVehicleAssignment SET DataDo = @DataDo WHERE ID = @ID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", assignmentId);
            cmd.Parameters.AddWithValue("@DataDo", dataDo);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<DataTable> GetAssignmentsForDriverAsync(int driverGID)
        {
            const string sql = @"
                SELECT dva.ID, dva.CarTrailerID, ct.Brand + ' ' + ISNULL(ct.Model,'') AS PojazdMarka,
                       vd.Registration, dva.Rola, dva.DataOd, dva.DataDo,
                       DATEDIFF(DAY, dva.DataOd, ISNULL(dva.DataDo, GETDATE())) AS Dni
                FROM DriverVehicleAssignment dva
                JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE dva.DriverGID = @GID
                ORDER BY CASE WHEN dva.DataDo IS NULL THEN 0 ELSE 1 END, dva.DataOd DESC";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", driverGID);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task<DataTable> GetAssignmentsForVehicleAsync(string carTrailerID)
        {
            const string sql = @"
                SELECT dva.ID, dva.DriverGID, d.Name AS KierowcaNazwa,
                       dva.Rola, dva.DataOd, dva.DataDo,
                       DATEDIFF(DAY, dva.DataOd, ISNULL(dva.DataDo, GETDATE())) AS Dni
                FROM DriverVehicleAssignment dva
                JOIN Driver d ON dva.DriverGID = d.GID
                WHERE dva.CarTrailerID = @ID
                ORDER BY CASE WHEN dva.DataDo IS NULL THEN 0 ELSE 1 END, dva.DataOd DESC";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        // ══════════════════════════════════════════════════════════════
        // ALERTY
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetAlertsAsync()
        {
            const string sql = @"
                SELECT 'Kierowca' AS Typ, d.Name AS Kto, 'Prawo jazdy' AS Dokument,
                       dd.DataWaznosciPJ AS DataWaznosci,
                       DATEDIFF(DAY, GETDATE(), dd.DataWaznosciPJ) AS DniDoWygasniecia
                FROM Driver d
                JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE d.Deleted = 0 AND d.Halt = 0
                  AND dd.DataWaznosciPJ IS NOT NULL
                  AND dd.DataWaznosciPJ <= DATEADD(DAY, 30, GETDATE())
                UNION ALL
                SELECT 'Kierowca', d.Name, 'Badania lekarskie', dd.DataWazBadanLek,
                       DATEDIFF(DAY, GETDATE(), dd.DataWazBadanLek)
                FROM Driver d JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE d.Deleted = 0 AND d.Halt = 0
                  AND dd.DataWazBadanLek IS NOT NULL
                  AND dd.DataWazBadanLek <= DATEADD(DAY, 30, GETDATE())
                UNION ALL
                SELECT 'Kierowca', d.Name, 'BHP', dd.DataWazBHP,
                       DATEDIFF(DAY, GETDATE(), dd.DataWazBHP)
                FROM Driver d JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE d.Deleted = 0 AND d.Halt = 0
                  AND dd.DataWazBHP IS NOT NULL
                  AND dd.DataWazBHP <= DATEADD(DAY, 30, GETDATE())
                UNION ALL
                SELECT 'Pojazd', ct.Brand + ' ' + ISNULL(vd.Registration, ct.ID), 'Przeglad',
                       vd.DataPrzegladu, DATEDIFF(DAY, GETDATE(), vd.DataPrzegladu)
                FROM CarTrailer ct JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE vd.DataPrzegladu IS NOT NULL
                  AND vd.DataPrzegladu <= DATEADD(DAY, 30, GETDATE())
                UNION ALL
                SELECT 'Pojazd', ct.Brand + ' ' + ISNULL(vd.Registration, ct.ID), 'OC/AC',
                       vd.DataUbezpieczenia, DATEDIFF(DAY, GETDATE(), vd.DataUbezpieczenia)
                FROM CarTrailer ct JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE vd.DataUbezpieczenia IS NOT NULL
                  AND vd.DataUbezpieczenia <= DATEADD(DAY, 30, GETDATE())
                ORDER BY DniDoWygasniecia ASC";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        // ══════════════════════════════════════════════════════════════
        // SERWIS
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetServiceLogsAsync(string carTrailerID, string? typFilter = null)
        {
            string sql = @"
                SELECT ID, CarTrailerID, TypZdarzenia, Data, DataNastepne, Opis,
                       KosztBrutto, PrzebiegKm, LitryPaliwa, CenaLitra,
                       Warsztat, NrFaktury, Uwagi
                FROM VehicleServiceLog
                WHERE CarTrailerID = @ID" +
                (typFilter != null ? " AND TypZdarzenia = @Typ" : "") +
                " ORDER BY Data DESC";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            if (typFilter != null)
                cmd.Parameters.AddWithValue("@Typ", typFilter);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task AddServiceLogAsync(string carTrailerID, string typZdarzenia,
            DateTime data, DateTime? dataNastepne, string? opis,
            decimal? kosztBrutto, int? przebiegKm,
            decimal? litryPaliwa, decimal? cenaLitra,
            string? warsztat, string? nrFaktury, string? uwagi, string user)
        {
            const string sql = @"INSERT INTO VehicleServiceLog
                (CarTrailerID, TypZdarzenia, Data, DataNastepne, Opis,
                 KosztBrutto, PrzebiegKm, LitryPaliwa, CenaLitra,
                 Warsztat, NrFaktury, Uwagi, CreatedBy)
                VALUES (@ID, @Typ, @Data, @DataNast, @Opis,
                 @Koszt, @Przebieg, @Litry, @Cena,
                 @Warsztat, @NrFV, @Uwagi, @User)";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            cmd.Parameters.AddWithValue("@Typ", typZdarzenia);
            cmd.Parameters.AddWithValue("@Data", data);
            cmd.Parameters.AddWithValue("@DataNast", (object?)dataNastepne ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Opis", (object?)opis ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Koszt", (object?)kosztBrutto ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Przebieg", (object?)przebiegKm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Litry", (object?)litryPaliwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cena", (object?)cenaLitra ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Warsztat", (object?)warsztat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NrFV", (object?)nrFaktury ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uwagi", (object?)uwagi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@User", user);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            // Update VehicleDetails if service type updates a document date
            if (typZdarzenia == "Przeglad" && dataNastepne.HasValue)
            {
                await UpdateVehicleDetailFieldAsync(carTrailerID, "DataPrzegladu", dataNastepne.Value, przebiegKm, user);
            }
            else if ((typZdarzenia == "OC" || typZdarzenia == "AC") && dataNastepne.HasValue)
            {
                await UpdateVehicleDetailFieldAsync(carTrailerID, "DataUbezpieczenia", dataNastepne.Value, przebiegKm, user);
            }
            else if (typZdarzenia == "Tankowanie" && przebiegKm.HasValue)
            {
                await UpdateVehicleMileageAsync(carTrailerID, przebiegKm.Value, user);
            }
        }

        private async Task UpdateVehicleDetailFieldAsync(string carTrailerID, string fieldName, DateTime value, int? przebieg, string user)
        {
            // fieldName is controlled internally, not from user input
            string sql = $@"UPDATE VehicleDetails SET {fieldName} = @Val,
                {(przebieg.HasValue ? "PrzebiegKm = @Przebieg," : "")}
                ModifiedAtUTC = SYSUTCDATETIME(), ModifiedBy = @User
                WHERE CarTrailerID = @ID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Val", value);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            cmd.Parameters.AddWithValue("@User", user);
            if (przebieg.HasValue)
                cmd.Parameters.AddWithValue("@Przebieg", przebieg.Value);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateVehicleMileageAsync(string carTrailerID, int przebiegKm, string user)
        {
            const string sql = @"UPDATE VehicleDetails SET PrzebiegKm = @Przebieg,
                DataOstatniegoTank = GETDATE(), ModifiedAtUTC = SYSUTCDATETIME(), ModifiedBy = @User
                WHERE CarTrailerID = @ID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Przebieg", przebiegKm);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            cmd.Parameters.AddWithValue("@User", user);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════════
        // STATYSTYKI
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetDriverStatsAsync(int driverGID, DateTime dateFrom, DateTime dateTo)
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM FarmerCalc WHERE DriverGID = @GID
                     AND CalcDate >= @From AND CalcDate <= @To) AS KursySkup,
                    (SELECT ISNULL(SUM(DistanceKM), 0) FROM FarmerCalc WHERE DriverGID = @GID
                     AND CalcDate >= @From AND CalcDate <= @To) AS KmSkup,
                    (SELECT ISNULL(SUM(CAST(WeightNetto AS decimal)), 0) FROM FarmerCalc WHERE DriverGID = @GID
                     AND CalcDate >= @From AND CalcDate <= @To) AS TonSkup";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", driverGID);
            cmd.Parameters.AddWithValue("@From", dateFrom);
            cmd.Parameters.AddWithValue("@To", dateTo);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task<DataTable> GetVehicleStatsAsync(string carTrailerID, DateTime dateFrom, DateTime dateTo)
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM FarmerCalc WHERE (CarID = @ID OR TrailerID = @ID)
                     AND CalcDate >= @From AND CalcDate <= @To) AS KursySkup,
                    (SELECT ISNULL(SUM(DistanceKM), 0) FROM FarmerCalc WHERE (CarID = @ID OR TrailerID = @ID)
                     AND CalcDate >= @From AND CalcDate <= @To) AS KmSkup,
                    (SELECT ISNULL(SUM(LitryPaliwa), 0) FROM VehicleServiceLog
                     WHERE CarTrailerID = @ID AND TypZdarzenia = 'Tankowanie'
                     AND Data >= @From AND Data <= @To) AS LitryPaliwa,
                    (SELECT ISNULL(SUM(KosztBrutto), 0) FROM VehicleServiceLog
                     WHERE CarTrailerID = @ID AND Data >= @From AND Data <= @To) AS KosztyOgolne";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            cmd.Parameters.AddWithValue("@From", dateFrom);
            cmd.Parameters.AddWithValue("@To", dateTo);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        // ══════════════════════════════════════════════════════════════
        // COMBO DATA (for dialogs)
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetActiveDriversComboAsync()
        {
            // Lista NAPĘDZANA przez TransportPL.Kierowca (Aktywny=1 + LibraNetDriverGID NOT NULL).
            // GID zwracamy LibraNetDriverGID — bo DriverVehicleAssignment.DriverGID ma FK na LibraNet.Driver.GID.
            var tmap = await LoadTransportKierowcyMapAsync();
            var dt = new DataTable();
            dt.Columns.Add("GID", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            foreach (var k in tmap.Values
                .Where(k => k.Aktywny)
                .OrderBy(k => k.Nazwisko).ThenBy(k => k.Imie))
            {
                dt.Rows.Add(k.LibraNetDriverGID, k.FullName);
            }
            return dt;
        }

        public async Task<DataTable> GetActiveVehiclesComboAsync()
        {
            // WYŁĄCZNIE z TransportPL.Pojazd. ID = PojazdID (string).
            var tAll = await FetchTransportPojazdyAllAsync();
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("Kind", typeof(string));
            dt.Columns.Add("Display", typeof(string));
            foreach (var t in tAll.Where(t => t.Aktywny).OrderBy(t => t.Rejestracja))
            {
                string display = string.IsNullOrEmpty(t.Marka)
                    ? t.Rejestracja
                    : $"{t.Marka} {t.Model} ({t.Rejestracja})";
                dt.Rows.Add(t.PojazdID.ToString(), "", display);
            }
            return dt;
        }

        public async Task<bool> HasActiveAssignmentAsync(int driverGID)
        {
            const string sql = @"SELECT COUNT(*) FROM DriverVehicleAssignment
                WHERE DriverGID = @GID AND DataDo IS NULL";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", driverGID);
            await conn.OpenAsync();
            return (int)(await cmd.ExecuteScalarAsync())! > 0;
        }

        public async Task<string?> GetActiveAssignmentInfoAsync(int driverGID)
        {
            const string sql = @"
                SELECT TOP 1 ct.Brand + ' ' + ISNULL(vd.Registration, ct.ID)
                FROM DriverVehicleAssignment dva
                JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE dva.DriverGID = @GID AND dva.DataDo IS NULL";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", driverGID);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        public async Task<(int totalDrivers, int active, int halted)> GetDriverCountsAsync()
        {
            const string sql = @"
                SELECT COUNT(*) AS Total,
                       SUM(CASE WHEN Halt=0 THEN 1 ELSE 0 END) AS Active,
                       SUM(CASE WHEN Halt=1 THEN 1 ELSE 0 END) AS Halted
                FROM Driver WHERE Deleted=0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            return (0, 0, 0);
        }

        public async Task<(int totalVehicles, int cars, int trailers)> GetVehicleCountsAsync()
        {
            const string sql = @"
                SELECT COUNT(*) AS Total,
                       SUM(CASE WHEN Kind=1 THEN 1 ELSE 0 END) AS Cars,
                       SUM(CASE WHEN Kind=2 THEN 1 ELSE 0 END) AS Trailers
                FROM CarTrailer";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            return (0, 0, 0);
        }

        public async Task<(int activeAssign, int freeDrivers, int freeVehicles)> GetAssignmentCountsAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM DriverVehicleAssignment WHERE DataDo IS NULL) AS ActiveAssign,
                    (SELECT COUNT(*) FROM Driver d WHERE d.Deleted=0 AND d.Halt=0
                     AND NOT EXISTS (SELECT 1 FROM DriverVehicleAssignment dva
                                     WHERE dva.DriverGID = d.GID AND dva.DataDo IS NULL)) AS FreeDrivers,
                    (SELECT COUNT(*) FROM CarTrailer ct
                     WHERE NOT EXISTS (SELECT 1 FROM DriverVehicleAssignment dva
                                       WHERE dva.CarTrailerID = ct.ID AND dva.DataDo IS NULL)) AS FreeVehicles";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            return (0, 0, 0);
        }

        public async Task<decimal> GetServiceCostYTDAsync(string carTrailerID)
        {
            const string sql = @"SELECT ISNULL(SUM(KosztBrutto), 0)
                FROM VehicleServiceLog WHERE CarTrailerID = @ID AND YEAR(Data) = YEAR(GETDATE())";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", carTrailerID);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToDecimal(result) : 0;
        }

        // ══════════════════════════════════════════════════════════════
        // DIAGNOSTYKA
        // ══════════════════════════════════════════════════════════════

        public async Task<List<string>> RunDiagnosticsAsync()
        {
            var issues = new List<string>();

            using var conn = new SqlConnection(_connectionString);
            try
            {
                await conn.OpenAsync();
                issues.Add("[OK] Polaczenie z baza LibraNet (192.168.0.109)");
            }
            catch (Exception ex)
            {
                issues.Add($"[BLAD] Nie mozna polaczyc z baza: {ex.Message}");
                return issues;
            }

            // 1. Check required tables exist
            var requiredTables = new[] { "Driver", "CarTrailer", "DriverDetails", "VehicleDetails", "DriverVehicleAssignment", "VehicleServiceLog" };
            foreach (var table in requiredTables)
            {
                try
                {
                    using var cmd = new SqlCommand($"SELECT COUNT(*) FROM sys.tables WHERE name = @T", conn);
                    cmd.Parameters.AddWithValue("@T", table);
                    int exists = (int)(await cmd.ExecuteScalarAsync())!;
                    if (exists == 0)
                        issues.Add($"[BLAD] Tabela '{table}' NIE ISTNIEJE");
                    else
                    {
                        using var cmdCnt = new SqlCommand($"SELECT COUNT(*) FROM [{table}]", conn);
                        int count = (int)(await cmdCnt.ExecuteScalarAsync())!;
                        issues.Add($"[OK] Tabela '{table}' istnieje ({count} rekordow)");
                    }
                }
                catch (Exception ex)
                {
                    issues.Add($"[BLAD] Tabela '{table}': {ex.Message}");
                }
            }

            // 2. FarmerCalc (used for stats)
            try
            {
                using var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = 'FarmerCalc'", conn);
                int exists = (int)(await cmd.ExecuteScalarAsync())!;
                if (exists == 0)
                    issues.Add("[UWAGA] Tabela 'FarmerCalc' nie istnieje - statystyki kursow beda puste");
                else
                    issues.Add("[OK] Tabela 'FarmerCalc' istnieje");
            }
            catch (Exception ex)
            {
                issues.Add($"[UWAGA] FarmerCalc: {ex.Message}");
            }

            // 3. Drivers without DriverDetails
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM Driver d
                    LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                    WHERE d.Deleted = 0 AND dd.DriverGID IS NULL", conn);
                int count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count > 0)
                    issues.Add($"[UWAGA] {count} kierowcow bez rekordu DriverDetails (nie uzupelniono danych szczegolowych)");
                else
                    issues.Add("[OK] Wszyscy kierowcy maja rekord DriverDetails");
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie DriverDetails: {ex.Message}"); }

            // 4. Vehicles without VehicleDetails
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM CarTrailer ct
                    LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                    WHERE vd.CarTrailerID IS NULL", conn);
                int count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count > 0)
                    issues.Add($"[UWAGA] {count} pojazdow bez rekordu VehicleDetails (nie uzupelniono danych szczegolowych)");
                else
                    issues.Add("[OK] Wszystkie pojazdy maja rekord VehicleDetails");
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie VehicleDetails: {ex.Message}"); }

            // 5. Orphaned assignments (driver deleted but assignment active)
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM DriverVehicleAssignment dva
                    LEFT JOIN Driver d ON dva.DriverGID = d.GID
                    WHERE dva.DataDo IS NULL AND (d.GID IS NULL OR d.Deleted = 1)", conn);
                int count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count > 0)
                    issues.Add($"[BLAD] {count} aktywnych przypisan do usunietych/nieistniejacych kierowcow");
                else
                    issues.Add("[OK] Brak osieroconych przypisan (kierowcy)");
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie osieroconych przypisan: {ex.Message}"); }

            // 6. Orphaned assignments (vehicle missing)
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM DriverVehicleAssignment dva
                    LEFT JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                    WHERE dva.DataDo IS NULL AND ct.ID IS NULL", conn);
                int count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count > 0)
                    issues.Add($"[BLAD] {count} aktywnych przypisan do nieistniejacych pojazdow");
                else
                    issues.Add("[OK] Brak osieroconych przypisan (pojazdy)");
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie osieroconych przypisan: {ex.Message}"); }

            // 7. Multiple active assignments per driver
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM (
                        SELECT DriverGID FROM DriverVehicleAssignment
                        WHERE DataDo IS NULL
                        GROUP BY DriverGID HAVING COUNT(*) > 1
                    ) x", conn);
                int count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count > 0)
                    issues.Add($"[UWAGA] {count} kierowcow ma wiecej niz 1 aktywne przypisanie");
                else
                    issues.Add("[OK] Kazdy kierowca ma max 1 aktywne przypisanie");
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie duplikatow przypisan: {ex.Message}"); }

            // 8. Expired documents
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT
                        (SELECT COUNT(*) FROM DriverDetails WHERE DataWaznosciPJ IS NOT NULL AND DataWaznosciPJ < GETDATE()) AS PJ_Expired,
                        (SELECT COUNT(*) FROM DriverDetails WHERE DataWazBadanLek IS NOT NULL AND DataWazBadanLek < GETDATE()) AS Badania_Expired,
                        (SELECT COUNT(*) FROM DriverDetails WHERE DataWazBHP IS NOT NULL AND DataWazBHP < GETDATE()) AS BHP_Expired,
                        (SELECT COUNT(*) FROM VehicleDetails WHERE DataPrzegladu IS NOT NULL AND DataPrzegladu < GETDATE()) AS Przeglad_Expired,
                        (SELECT COUNT(*) FROM VehicleDetails WHERE DataUbezpieczenia IS NOT NULL AND DataUbezpieczenia < GETDATE()) AS OC_Expired", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int pj = reader.GetInt32(0), bad = reader.GetInt32(1), bhp = reader.GetInt32(2);
                    int prz = reader.GetInt32(3), oc = reader.GetInt32(4);
                    int total = pj + bad + bhp + prz + oc;
                    if (total > 0)
                        issues.Add($"[UWAGA] Wygasle dokumenty: PrawoJazdy={pj}, Badania={bad}, BHP={bhp}, Przeglady={prz}, OC/AC={oc}");
                    else
                        issues.Add("[OK] Brak wygaslych dokumentow");
                }
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie dokumentow: {ex.Message}"); }

            // 9. Halted drivers with active assignments
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM DriverVehicleAssignment dva
                    JOIN Driver d ON dva.DriverGID = d.GID
                    WHERE dva.DataDo IS NULL AND d.Halt = 1", conn);
                int count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count > 0)
                    issues.Add($"[UWAGA] {count} wstrzymanych kierowcow ma aktywne przypisanie do pojazdu");
                else
                    issues.Add("[OK] Wstrzymani kierowcy nie maja aktywnych przypisan");
            }
            catch (Exception ex) { issues.Add($"[BLAD] Sprawdzanie wstrzymanych: {ex.Message}"); }

            return issues;
        }
    }
}
