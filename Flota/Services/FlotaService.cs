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
        // KIEROWCY — WYŁĄCZNIE TransportPL.Kierowca
        // ══════════════════════════════════════════════════════════════

        private const string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private sealed class TKierowca
        {
            public int KierowcaID; public int? LibraNetDriverGID;
            public string Imie = ""; public string Nazwisko = "";
            public string? Telefon; public bool Aktywny;
            public DateTime? UtworzonoUTC; public DateTime? ZmienionoUTC;
            public string FullName => $"{Imie} {Nazwisko}".Trim();
        }

        public async Task<DataTable> GetDriversAsync()
        {
            // PRIMARY = TransportPL.Kierowca (bez LibraNet, bez backfill)
            var tAll = await FetchTransportKierowcyAllAsync();

            var dt = new DataTable();
            dt.Columns.Add("GID", typeof(int));            // = KierowcaID (primary key TransportPL)
            dt.Columns.Add("Imie", typeof(string));
            dt.Columns.Add("Nazwisko", typeof(string));
            dt.Columns.Add("Name", typeof(string));        // Imie + Nazwisko (compat)
            dt.Columns.Add("Telefon", typeof(string));
            dt.Columns.Add("Aktywny", typeof(bool));
            dt.Columns.Add("Halt", typeof(bool));          // = !Aktywny (compat z filtrami XAML)
            dt.Columns.Add("UtworzonoUTC", typeof(DateTime));
            dt.Columns.Add("ZmienionoUTC", typeof(DateTime));

            foreach (var k in tAll.OrderBy(t => !t.Aktywny).ThenBy(t => t.Nazwisko).ThenBy(t => t.Imie))
            {
                var row = dt.NewRow();
                row["GID"] = k.KierowcaID;
                row["Imie"] = k.Imie;
                row["Nazwisko"] = k.Nazwisko;
                row["Name"] = k.FullName;
                row["Telefon"] = (object?)k.Telefon ?? DBNull.Value;
                row["Aktywny"] = k.Aktywny;
                row["Halt"] = !k.Aktywny;
                if (k.UtworzonoUTC.HasValue) row["UtworzonoUTC"] = k.UtworzonoUTC.Value;
                if (k.ZmienionoUTC.HasValue) row["ZmienionoUTC"] = k.ZmienionoUTC.Value;
                dt.Rows.Add(row);
            }
            return dt;
        }

        private async Task<List<TKierowca>> FetchTransportKierowcyAllAsync()
        {
            var list = new List<TKierowca>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT KierowcaID, LibraNetDriverGID, Imie, Nazwisko, Telefon, Aktywny,
                         UtworzonoUTC, ZmienionoUTC
                  FROM dbo.Kierowca", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new TKierowca
                {
                    KierowcaID = Convert.ToInt32(r["KierowcaID"]),
                    LibraNetDriverGID = r["LibraNetDriverGID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["LibraNetDriverGID"]),
                    Imie = r["Imie"]?.ToString() ?? "",
                    Nazwisko = r["Nazwisko"]?.ToString() ?? "",
                    Telefon = r["Telefon"] == DBNull.Value ? null : r["Telefon"].ToString(),
                    Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"]),
                    UtworzonoUTC = r["UtworzonoUTC"] == DBNull.Value ? (DateTime?)null : (DateTime)r["UtworzonoUTC"],
                    ZmienionoUTC = r["ZmienionoUTC"] == DBNull.Value ? (DateTime?)null : (DateTime)r["ZmienionoUTC"]
                });
            }
            return list;
        }

        /// <summary>Tłumaczy KierowcaID (TransportPL primary) → LibraNetDriverGID (FK target dla legacy Assign).</summary>
        private async Task<int?> ResolveLibraDriverGidAsync(int kierowcaId)
        {
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT LibraNetDriverGID FROM dbo.Kierowca WHERE KierowcaID = @id", conn);
            cmd.Parameters.AddWithValue("@id", kierowcaId);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return null;
            return Convert.ToInt32(result);
        }

        /// <summary>gid = KierowcaID. Czysty TransportPL.Kierowca, brak LibraNet.</summary>
        public async Task<DataRow?> GetDriverByGIDAsync(int gid)
        {
            TKierowca? k = null;
            using (var conn = new SqlConnection(_connTransport))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    @"SELECT TOP 1 KierowcaID, LibraNetDriverGID, Imie, Nazwisko, Telefon, Aktywny
                      FROM dbo.Kierowca WHERE KierowcaID = @id", conn);
                cmd.Parameters.AddWithValue("@id", gid);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    k = new TKierowca
                    {
                        KierowcaID = Convert.ToInt32(r["KierowcaID"]),
                        LibraNetDriverGID = r["LibraNetDriverGID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["LibraNetDriverGID"]),
                        Imie = r["Imie"]?.ToString() ?? "",
                        Nazwisko = r["Nazwisko"]?.ToString() ?? "",
                        Telefon = r["Telefon"] == DBNull.Value ? null : r["Telefon"].ToString(),
                        Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"])
                    };
                }
            }
            if (k == null) return null;

            // DataTable z polami uzywanymi przez DriverEditWindow (pola spoza TransportPL pozostaja puste)
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
            row["GID"] = k.KierowcaID;
            row["Name"] = k.FullName;
            row["FirstName"] = k.Imie;
            row["LastName"] = k.Nazwisko;
            row["Phone1"] = (object?)k.Telefon ?? DBNull.Value;
            row["Halt"] = !k.Aktywny;
            dt.Rows.Add(row);
            return dt.Rows[0];
        }

        /// <summary>Save Kierowca primary TransportPL. Tylko Imie/Nazwisko/Telefon/Aktywny.
        /// Pozostale parametry (PJ, badania, BHP, PESEL, Email itd.) sa ignorowane — TransportPL.Kierowca ich nie ma.</summary>
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
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();

            if (gid.HasValue && gid.Value > 0)
            {
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.Kierowca SET Imie=@imie, Nazwisko=@nazwisko, Telefon=@tel, Aktywny=@akt,
                                            ZmienionoUTC=SYSUTCDATETIME()
                    WHERE KierowcaID = @id", conn);
                cmd.Parameters.AddWithValue("@id", gid.Value);
                cmd.Parameters.AddWithValue("@imie", (object?)firstName ?? "");
                cmd.Parameters.AddWithValue("@nazwisko", (object?)lastName ?? "");
                cmd.Parameters.AddWithValue("@tel", (object?)phone1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@akt", !halt);
                await cmd.ExecuteNonQueryAsync();
                return gid.Value;
            }
            else
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Kierowca (Imie, Nazwisko, Telefon, Aktywny, UtworzonoUTC)
                    OUTPUT INSERTED.KierowcaID
                    VALUES (@imie, @nazwisko, @tel, @akt, SYSUTCDATETIME())", conn);
                cmd.Parameters.AddWithValue("@imie", (object?)firstName ?? "");
                cmd.Parameters.AddWithValue("@nazwisko", (object?)lastName ?? "");
                cmd.Parameters.AddWithValue("@tel", (object?)phone1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@akt", !halt);
                var result = await cmd.ExecuteScalarAsync();
                return result == null ? 0 : Convert.ToInt32(result);
            }
        }

        public async Task ToggleDriverHaltAsync(int gid, string user)
        {
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.Kierowca SET Aktywny = CASE WHEN Aktywny=1 THEN 0 ELSE 1 END,
                                        ZmienionoUTC=SYSUTCDATETIME()
                WHERE KierowcaID = @id", conn);
            cmd.Parameters.AddWithValue("@id", gid);
            await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════════
        // POJAZDY (PRIMARY = TransportPL.Pojazd)
        // ══════════════════════════════════════════════════════════════

        private sealed class TPojazd
        {
            public int PojazdID; public string? LibraNetCarTrailerID;
            public string Rejestracja = ""; public string? Marka; public string? Model;
            public int PaletyH1; public bool Aktywny;
            public DateTime? UtworzonoUTC; public DateTime? ZmienionoUTC;
        }

        private async Task<List<TPojazd>> FetchTransportPojazdyAllAsync()
        {
            var list = new List<TPojazd>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT PojazdID, LibraNetCarTrailerID, Rejestracja, Marka, Model, PaletyH1, Aktywny,
                         UtworzonoUTC, ZmienionoUTC
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
                    Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"]),
                    UtworzonoUTC = r["UtworzonoUTC"] == DBNull.Value ? (DateTime?)null : (DateTime)r["UtworzonoUTC"],
                    ZmienionoUTC = r["ZmienionoUTC"] == DBNull.Value ? (DateTime?)null : (DateTime)r["ZmienionoUTC"]
                });
            }
            return list;
        }

        public async Task<DataTable> GetVehiclesAsync()
        {
            // ═══ WYŁĄCZNIE TransportPL.Pojazd ═══
            var tAll = await FetchTransportPojazdyAllAsync();

            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));        // = PojazdID
            dt.Columns.Add("Registration", typeof(string));
            dt.Columns.Add("Brand", typeof(string));
            dt.Columns.Add("Model", typeof(string));
            dt.Columns.Add("MaxPaletH1", typeof(int));
            dt.Columns.Add("Aktywny", typeof(bool));
            // Compat-only kolumny dla detail panel (puste — TransportPL.Pojazd ich nie ma):
            dt.Columns.Add("Kind", typeof(int));
            dt.Columns.Add("Capacity", typeof(decimal));
            dt.Columns.Add("VIN", typeof(string));
            dt.Columns.Add("DataPrzegladu", typeof(DateTime));
            dt.Columns.Add("DataUbezpieczenia", typeof(DateTime));
            dt.Columns.Add("AktualnyKierowca", typeof(string));
            dt.Columns.Add("OstatniSerwis", typeof(string));
            dt.Columns.Add("KosztyYTD", typeof(decimal));
            dt.Columns.Add("UtworzonoUTC", typeof(DateTime));
            dt.Columns.Add("ZmienionoUTC", typeof(DateTime));

            foreach (var t in tAll.OrderBy(t => !t.Aktywny).ThenBy(t => t.Rejestracja))
            {
                var row = dt.NewRow();
                row["ID"] = t.PojazdID.ToString();
                row["Registration"] = t.Rejestracja;
                row["Brand"] = (object?)t.Marka ?? DBNull.Value;
                row["Model"] = (object?)t.Model ?? DBNull.Value;
                row["MaxPaletH1"] = t.PaletyH1;
                row["Aktywny"] = t.Aktywny;
                row["KosztyYTD"] = 0m;
                if (t.UtworzonoUTC.HasValue) row["UtworzonoUTC"] = t.UtworzonoUTC.Value;
                if (t.ZmienionoUTC.HasValue) row["ZmienionoUTC"] = t.ZmienionoUTC.Value;
                dt.Rows.Add(row);
            }
            return dt;
        }

        /// <summary>Primary: TransportPL.Pojazd po PojazdID (string z listy, np "123").</summary>
        public async Task<DataRow?> GetVehicleByIDAsync(string carTrailerID)
        {
            if (!int.TryParse(carTrailerID?.TrimStart('T') ?? "", out int pojazdId))
                return null;

            TPojazd? t = null;
            using (var conn = new SqlConnection(_connTransport))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    @"SELECT PojazdID, LibraNetCarTrailerID, Rejestracja, Marka, Model, PaletyH1, Aktywny
                      FROM dbo.Pojazd WHERE PojazdID = @id", conn);
                cmd.Parameters.AddWithValue("@id", pojazdId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    t = new TPojazd
                    {
                        PojazdID = Convert.ToInt32(r["PojazdID"]),
                        LibraNetCarTrailerID = r["LibraNetCarTrailerID"] == DBNull.Value ? null : r["LibraNetCarTrailerID"].ToString(),
                        Rejestracja = r["Rejestracja"]?.ToString() ?? "",
                        Marka = r["Marka"] == DBNull.Value ? null : r["Marka"].ToString(),
                        Model = r["Model"] == DBNull.Value ? null : r["Model"].ToString(),
                        PaletyH1 = r["PaletyH1"] == DBNull.Value ? 0 : Convert.ToInt32(r["PaletyH1"]),
                        Aktywny = r["Aktywny"] != DBNull.Value && Convert.ToBoolean(r["Aktywny"])
                    };
                }
            }
            if (t == null) return null;

            // Zbuduj DataTable z polami które VehicleEditWindow expectuje (większość pusta — TransportPL.Pojazd ich nie ma)
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("Kind", typeof(int));
            dt.Columns.Add("Brand", typeof(string));
            dt.Columns.Add("Model", typeof(string));
            dt.Columns.Add("Capacity", typeof(decimal));
            dt.Columns.Add("Registration", typeof(string));
            dt.Columns.Add("VIN", typeof(string));
            dt.Columns.Add("RokProdukcji", typeof(int));
            dt.Columns.Add("TypNadwozia", typeof(string));
            dt.Columns.Add("DataPrzegladu", typeof(DateTime));
            dt.Columns.Add("DataUbezpieczenia", typeof(DateTime));
            dt.Columns.Add("NrPolisyOC", typeof(string));
            dt.Columns.Add("NrPolisyAC", typeof(string));
            dt.Columns.Add("Ubezpieczyciel", typeof(string));
            dt.Columns.Add("PrzebiegKm", typeof(int));
            dt.Columns.Add("SrednieSpalanie", typeof(decimal));
            dt.Columns.Add("PojemnoscBaku", typeof(int));
            dt.Columns.Add("MaxLadownoscKg", typeof(int));
            dt.Columns.Add("MaxPaletH1", typeof(int));
            dt.Columns.Add("MaxPojemnikE2", typeof(int));
            dt.Columns.Add("TemperaturaMin", typeof(decimal));
            dt.Columns.Add("TemperaturaMax", typeof(decimal));
            dt.Columns.Add("GPSModul", typeof(string));
            dt.Columns.Add("Uwagi", typeof(string));
            dt.Columns.Add("ZdjeciePojazdu", typeof(byte[]));

            var row = dt.NewRow();
            row["ID"] = t.PojazdID.ToString();
            row["Brand"] = (object?)t.Marka ?? DBNull.Value;
            row["Model"] = (object?)t.Model ?? DBNull.Value;
            row["Registration"] = t.Rejestracja;
            row["MaxPaletH1"] = t.PaletyH1;
            dt.Rows.Add(row);
            return dt.Rows[0];
        }

        /// <summary>Primary: zapis do TransportPL.Pojazd. Pozostałe parametry (VIN/OC/przegląd)
        /// nie mają miejsca w schema TransportPL — są ignorowane.</summary>
        public async Task SaveVehicleAsync(string id, bool isNew, int kind, string? brand, string? model,
            decimal? capacity, string? registration, string? vin, int? rokProdukcji,
            DateTime? dataPrzegladu, DateTime? dataUbezpieczenia,
            string? nrOC, string? nrAC, string? ubezpieczyciel,
            int? przebiegKm, decimal? spalanie, int? pojemnoscBaku,
            int? maxLadownosc, int? maxPalet, int? maxE2,
            string? typNadwozia, decimal? tempMin, decimal? tempMax,
            string? gpsModul, string? uwagi, byte[]? zdjecie, string user)
        {
            int paletyH1 = maxPalet ?? 0;
            string rej = !string.IsNullOrWhiteSpace(registration) ? registration! : id;

            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();

            if (isNew)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Pojazd (Rejestracja, Marka, Model, PaletyH1, Aktywny, UtworzonoUTC)
                    VALUES (@rej, @marka, @model, @palety, 1, SYSUTCDATETIME())", conn);
                cmd.Parameters.AddWithValue("@rej", rej);
                cmd.Parameters.AddWithValue("@marka", (object?)brand ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@model", (object?)model ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@palety", paletyH1);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                if (!int.TryParse(id?.TrimStart('T') ?? "", out int pojazdId)) return;
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.Pojazd SET Rejestracja=@rej, Marka=@marka, Model=@model, PaletyH1=@palety,
                                          ZmienionoUTC=SYSUTCDATETIME()
                    WHERE PojazdID = @id", conn);
                cmd.Parameters.AddWithValue("@id", pojazdId);
                cmd.Parameters.AddWithValue("@rej", rej);
                cmd.Parameters.AddWithValue("@marka", (object?)brand ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@model", (object?)model ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@palety", paletyH1);
                await cmd.ExecuteNonQueryAsync();
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

        /// <summary>Tłumaczy PojazdID (string z combo TransportPL) na LibraNet.CarTrailer.ID
        /// — wymagane dla FK DriverVehicleAssignment.CarTrailerID. Null gdy pojazd niezmapowany.</summary>
        private async Task<string?> ResolveLibraCarTrailerIdAsync(string idFromCombo)
        {
            if (string.IsNullOrEmpty(idFromCombo)) return null;
            if (!int.TryParse(idFromCombo.TrimStart('T'), out int pojazdId)) return idFromCombo; // już LibraNet ID
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                @"SELECT LibraNetCarTrailerID FROM dbo.Pojazd WHERE PojazdID = @id", conn);
            cmd.Parameters.AddWithValue("@id", pojazdId);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        public async Task AssignDriverAsync(int driverGID, string carTrailerID, string rola,
            DateTime dataOd, string? powod, bool closeExistingForDriver, string user)
        {
            // Tłumaczenie PojazdID (TransportPL) → LibraNetCarTrailerID (FK target)
            string? libraCarId = await ResolveLibraCarTrailerIdAsync(carTrailerID);
            if (string.IsNullOrEmpty(libraCarId))
                throw new InvalidOperationException(
                    "Pojazd nie ma odpowiednika w LibraNet (LibraNetCarTrailerID).\n" +
                    "Przypisanie wymaga zmapowania — uzyj 'Flota > Mapowanie systemow'.");

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
                cmd1.Parameters.AddWithValue("@CarID", libraCarId);
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
                cmdIns.Parameters.AddWithValue("@CarID", libraCarId);
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
            // WYŁĄCZNIE z TransportPL.Kierowca. GID = KierowcaID.
            var tAll = await FetchTransportKierowcyAllAsync();
            var dt = new DataTable();
            dt.Columns.Add("GID", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            foreach (var k in tAll.Where(k => k.Aktywny).OrderBy(k => k.Nazwisko).ThenBy(k => k.Imie))
                dt.Rows.Add(k.KierowcaID, k.FullName);
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
            // driverGID = KierowcaID (TransportPL). Translacja do LibraNetDriverGID dla FK lookup.
            int? libraGid = await ResolveLibraDriverGidAsync(driverGID);
            if (!libraGid.HasValue) return false;
            const string sql = @"SELECT COUNT(*) FROM DriverVehicleAssignment
                WHERE DriverGID = @GID AND DataDo IS NULL";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", libraGid.Value);
            await conn.OpenAsync();
            return (int)(await cmd.ExecuteScalarAsync())! > 0;
        }

        public async Task<string?> GetActiveAssignmentInfoAsync(int driverGID)
        {
            int? libraGid = await ResolveLibraDriverGidAsync(driverGID);
            if (!libraGid.HasValue) return null;
            const string sql = @"
                SELECT TOP 1 ct.Brand + ' ' + ISNULL(vd.Registration, ct.ID)
                FROM DriverVehicleAssignment dva
                JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                WHERE dva.DriverGID = @GID AND dva.DataDo IS NULL";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", libraGid.Value);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        public async Task<(int totalDrivers, int active, int halted)> GetDriverCountsAsync()
        {
            const string sql = @"
                SELECT COUNT(*) AS Total,
                       SUM(CASE WHEN Aktywny=1 THEN 1 ELSE 0 END) AS Active,
                       SUM(CASE WHEN Aktywny=0 THEN 1 ELSE 0 END) AS Halted
                FROM dbo.Kierowca";
            using var conn = new SqlConnection(_connTransport);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            return (0, 0, 0);
        }

        public async Task<(int totalVehicles, int cars, int trailers)> GetVehicleCountsAsync()
        {
            // TransportPL.Pojazd nie ma Kind — wszystkie traktujemy jako 'cars'.
            const string sql = @"
                SELECT COUNT(*) AS Total,
                       SUM(CASE WHEN Aktywny=1 THEN 1 ELSE 0 END) AS Active,
                       0 AS Trailers
                FROM dbo.Pojazd";
            using var conn = new SqlConnection(_connTransport);
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
