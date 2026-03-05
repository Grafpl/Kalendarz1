using System;
using System.Collections.Generic;
using System.Data;
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
        // KIEROWCY
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetDriversAsync()
        {
            const string sql = @"
                SELECT d.GID, d.Name, d.Halt, d.Typ, d.Created,
                       dd.FirstName, dd.LastName, dd.Phone1, dd.Email,
                       dd.TypZatrudnienia, dd.DataZatrudnienia, dd.DataZwolnienia,
                       dd.DataWaznosciPJ, dd.DataWazBadanLek, dd.DataWazBHP,
                       dd.KategoriePrawaJazdy,
                       STUFF((
                           SELECT ', ' + ct.ID + ' (' + dva.Rola + ')'
                           FROM DriverVehicleAssignment dva
                           JOIN CarTrailer ct ON dva.CarTrailerID = ct.ID
                           WHERE dva.DriverGID = d.GID AND dva.DataDo IS NULL
                           FOR XML PATH('')
                       ), 1, 2, '') AS AktualneAuta,
                       (SELECT COUNT(*) FROM FarmerCalc WHERE DriverGID = d.GID
                        AND CalcDate >= DATEADD(DAY, -30, GETDATE())) AS KursySkup30d,
                       (SELECT ISNULL(SUM(DistanceKM), 0) FROM FarmerCalc WHERE DriverGID = d.GID
                        AND CalcDate >= DATEADD(DAY, -30, GETDATE())) AS Km30d
                FROM Driver d
                LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE d.Deleted = 0
                ORDER BY d.Halt ASC, d.Name ASC";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task<DataRow?> GetDriverByGIDAsync(int gid)
        {
            const string sql = @"
                SELECT d.GID, d.Name, d.Halt, d.Typ, d.Created,
                       dd.*
                FROM Driver d
                LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE d.GID = @GID AND d.Deleted = 0";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", gid);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
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

            try
            {
                int driverGID;

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
                return driverGID;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
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
            const string sql = @"UPDATE Driver SET Halt = CASE WHEN Halt=0 THEN 1 ELSE 0 END,
                Modified=GETDATE(), ModifiedBy=@User WHERE GID=@GID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GID", gid);
            cmd.Parameters.AddWithValue("@User", user);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════════
        // POJAZDY
        // ══════════════════════════════════════════════════════════════

        public async Task<DataTable> GetVehiclesAsync()
        {
            const string sql = @"
                SELECT ct.ID, ct.Kind, ct.Brand, ct.Model, ct.Capacity,
                       vd.Registration, vd.VIN, vd.RokProdukcji, vd.TypNadwozia,
                       vd.DataPrzegladu, vd.DataUbezpieczenia, vd.PrzebiegKm,
                       vd.MaxLadownoscKg, vd.MaxPaletH1, vd.SrednieSpalanie,
                       vd.NrPolisyOC, vd.NrPolisyAC, vd.Ubezpieczyciel,
                       vd.TemperaturaMin, vd.TemperaturaMax, vd.GPSModul,
                       vd.PojemnoscBaku, vd.MaxPojemnikE2, vd.Uwagi AS VdUwagi,
                       STUFF((
                           SELECT ', ' + drv.Name + ' (' + dva.Rola + ')'
                           FROM DriverVehicleAssignment dva
                           JOIN Driver drv ON dva.DriverGID = drv.GID
                           WHERE dva.CarTrailerID = ct.ID AND dva.DataDo IS NULL
                           FOR XML PATH('')
                       ), 1, 2, '') AS AktualnyKierowca,
                       (SELECT TOP 1 TypZdarzenia + ' ' + CONVERT(varchar(10), Data, 120)
                        FROM VehicleServiceLog WHERE CarTrailerID = ct.ID ORDER BY Data DESC) AS OstatniSerwis,
                       (SELECT ISNULL(SUM(KosztBrutto), 0) FROM VehicleServiceLog
                        WHERE CarTrailerID = ct.ID AND YEAR(Data) = YEAR(GETDATE())) AS KosztyYTD
                FROM CarTrailer ct
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                ORDER BY ct.Kind, ct.ID";

            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
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
            const string sql = @"SELECT GID, Name FROM Driver WHERE Deleted=0 AND Halt=0 ORDER BY Name";
            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task<DataTable> GetActiveVehiclesComboAsync()
        {
            const string sql = @"
                SELECT ct.ID, ct.Kind,
                       ct.Brand + ' ' + ISNULL(ct.Model,'') + ' (' + ISNULL(vd.Registration, ct.ID) + ')' AS Display
                FROM CarTrailer ct
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                ORDER BY ct.Kind, ct.ID";
            var dt = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
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
    }
}
