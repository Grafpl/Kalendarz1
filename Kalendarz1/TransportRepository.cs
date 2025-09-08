using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1;

internal sealed class TransportRepository
{
    private readonly string _conn;
    public TransportRepository(string connLibra) => _conn = connLibra ?? throw new ArgumentNullException(nameof(connLibra));

    // ================== Infrastructure helpers ==================
    private async Task EnsureTripLoadOrderColumnAsync(SqlConnection cn)
    {
        const string sql = @"IF COL_LENGTH('dbo.TTripLoad','OrderID') IS NULL ALTER TABLE dbo.TTripLoad ADD OrderID INT NULL; 
IF COL_LENGTH('dbo.TTripLoad','OrderID') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TTripLoad_OrderID') CREATE INDEX IX_TTripLoad_OrderID ON dbo.TTripLoad(OrderID);";
        await using var cmd = new SqlCommand(sql, cn);
        await cmd.ExecuteNonQueryAsync();
    }

    // ================== NEW TABLES ENSURE ==================
    public async Task EnsureNewTransportTablesAsync()
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        var sql = @"
IF OBJECT_ID('dbo.TDriver') IS NULL BEGIN
    CREATE TABLE dbo.TDriver(DriverID INT IDENTITY PRIMARY KEY, FirstName NVARCHAR(50) NOT NULL, LastName NVARCHAR(80) NOT NULL, Phone NVARCHAR(30) NULL, Active BIT NOT NULL DEFAULT 1, CreatedAtUTC DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedAtUTC DATETIME2 NULL);
END;
IF OBJECT_ID('dbo.TVehicle') IS NULL BEGIN
    CREATE TABLE dbo.TVehicle(VehicleID INT IDENTITY PRIMARY KEY, Registration NVARCHAR(20) NOT NULL UNIQUE, Kind INT NOT NULL DEFAULT 3, Brand NVARCHAR(50) NULL, Model NVARCHAR(50) NULL, CapacityKg DECIMAL(10,2) NOT NULL DEFAULT 0, PalletSlotsH1 INT NOT NULL DEFAULT 0, E2Factor DECIMAL(6,4) NOT NULL DEFAULT 0.10, Active BIT NOT NULL DEFAULT 1, CreatedAtUTC DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedAtUTC DATETIME2 NULL);
END;
IF OBJECT_ID('dbo.TTrip') IS NULL BEGIN
    CREATE TABLE dbo.TTrip(TripID BIGINT IDENTITY PRIMARY KEY, TripDate DATE NOT NULL, DriverID INT NOT NULL REFERENCES dbo.TDriver(DriverID), VehicleID INT NOT NULL REFERENCES dbo.TVehicle(VehicleID), TrailerVehicleID INT NULL REFERENCES dbo.TVehicle(VehicleID), RouteName NVARCHAR(120) NULL, PlannedDeparture TIME NULL, PlannedReturn TIME NULL, Status NVARCHAR(20) NOT NULL DEFAULT 'Planned', Notes NVARCHAR(MAX) NULL, CreatedAtUTC DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy NVARCHAR(64) NULL, ModifiedAtUTC DATETIME2 NULL, ModifiedBy NVARCHAR(64) NULL);
END;
IF OBJECT_ID('dbo.TTripLoad') IS NULL BEGIN
    CREATE TABLE dbo.TTripLoad(TripLoadID BIGINT IDENTITY PRIMARY KEY, TripID BIGINT NOT NULL REFERENCES dbo.TTrip(TripID) ON DELETE CASCADE, SequenceNo INT NOT NULL, CustomerCode NVARCHAR(50) NULL, MeatKg DECIMAL(10,2) NOT NULL DEFAULT 0, CarcassCount INT NOT NULL DEFAULT 0, PalletsH1 INT NOT NULL DEFAULT 0, ContainersE2 INT NOT NULL DEFAULT 0, Comment NVARCHAR(255) NULL, CreatedAtUTC DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
END;";
        await using (var cmd = new SqlCommand(sql, cn)) { await cmd.ExecuteNonQueryAsync(); }
        await EnsureTripLoadOrderColumnAsync(cn);
    }

    // ================== Drivers ==================
    public async Task<DataTable> GetDrivers2Async(bool includeInactive = false)
    {
        var dt = new DataTable();
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        var sql = "SELECT DriverID, FirstName, LastName, Phone, Active, (FirstName + ' ' + LastName) AS FullName FROM dbo.TDriver" + (includeInactive ? string.Empty : " WHERE Active=1") + " ORDER BY LastName, FirstName";
        using var da = new SqlDataAdapter(sql, cn); da.Fill(dt); return dt;
    }

    public async Task<int> AddDriver2Async(string fullName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("fullName required", nameof(fullName));
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? parts[0] : fullName.Trim();
        var last = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand("INSERT INTO dbo.TDriver(FirstName,LastName,Phone) VALUES(@f,@l,@p); SELECT SCOPE_IDENTITY();", cn);
        cmd.Parameters.AddWithValue("@f", first);
        cmd.Parameters.AddWithValue("@l", last);
        cmd.Parameters.AddWithValue("@p", (object?)phone ?? DBNull.Value);
        var obj = await cmd.ExecuteScalarAsync(); int id=0; int.TryParse(obj?.ToString(), out id); return id;
    }

    public async Task UpdateDriver2Async(int driverId, string fullName, string? phone, bool? active = null)
    {
        var parts = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? parts[0] : fullName.Trim();
        var last = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        var sql = "UPDATE dbo.TDriver SET FirstName=@f, LastName=@l, Phone=@p, ModifiedAtUTC=SYSUTCDATETIME()" + (active.HasValue ? ", Active=@a" : string.Empty) + " WHERE DriverID=@id";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@f", first);
        cmd.Parameters.AddWithValue("@l", last);
        cmd.Parameters.AddWithValue("@p", (object?)phone ?? DBNull.Value);
        if (active.HasValue) cmd.Parameters.AddWithValue("@a", active.Value);
        cmd.Parameters.AddWithValue("@id", driverId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetDriver2ActiveAsync(int driverId, bool active)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.TDriver SET Active=@a, ModifiedAtUTC=SYSUTCDATETIME() WHERE DriverID=@id", cn);
        cmd.Parameters.AddWithValue("@a", active);
        cmd.Parameters.AddWithValue("@id", driverId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ================== Vehicles ==================
    public async Task<DataTable> GetVehicles2Async(int? kind = null, bool includeInactive = false)
    {
        var dt = new DataTable(); await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        var sql = "SELECT VehicleID, Registration, Brand, Model, CapacityKg, PalletSlotsH1, E2Factor, Kind, Active FROM dbo.TVehicle WHERE 1=1";
        if (kind.HasValue) sql += " AND Kind=@k";
        if (!includeInactive) sql += " AND Active=1";
        sql += " ORDER BY Registration";
        await using var cmd = new SqlCommand(sql, cn);
        if (kind.HasValue) cmd.Parameters.AddWithValue("@k", kind.Value);
        using var da = new SqlDataAdapter(cmd); da.Fill(dt); return dt;
    }

    public async Task<int> AddVehicle2Async(string registration, decimal? capacityKg, int? palletSlotsH1, int kind, string? brand, string? model, decimal? e2Factor = null)
    {
        if (string.IsNullOrWhiteSpace(registration)) throw new ArgumentException("registration required", nameof(registration));
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        using (var chk = new SqlCommand("SELECT COUNT(*) FROM dbo.TVehicle WHERE Registration=@r", cn))
        { chk.Parameters.AddWithValue("@r", registration.Trim()); if ((int)await chk.ExecuteScalarAsync() > 0) throw new InvalidOperationException("Rejestracja ju¿ istnieje"); }
        await using var ins = new SqlCommand(@"INSERT INTO dbo.TVehicle(Registration, Brand, Model, CapacityKg, PalletSlotsH1, Kind, E2Factor) VALUES(@r,@b,@m,@c,@p,@k,@e); SELECT SCOPE_IDENTITY();", cn);
        ins.Parameters.AddWithValue("@r", registration.Trim());
        ins.Parameters.AddWithValue("@b", (object?)brand ?? DBNull.Value);
        ins.Parameters.AddWithValue("@m", (object?)model ?? DBNull.Value);
        ins.Parameters.AddWithValue("@c", (object?)capacityKg ?? 0m);
        ins.Parameters.AddWithValue("@p", (object?)palletSlotsH1 ?? 0);
        ins.Parameters.AddWithValue("@k", kind);
        ins.Parameters.AddWithValue("@e", (object?)e2Factor ?? 0.10m);
        var obj = await ins.ExecuteScalarAsync(); int id=0; int.TryParse(obj?.ToString(), out id); return id;
    }

    public async Task UpdateVehicle2Async(int vehicleId, string registration, decimal? capacityKg, int? palletSlots, int kind, string? brand, string? model, decimal? e2Factor, bool? active=null)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        var sql = "UPDATE dbo.TVehicle SET Registration=@r, Brand=@b, Model=@m, CapacityKg=@c, PalletSlotsH1=@p, Kind=@k, E2Factor=@e, ModifiedAtUTC=SYSUTCDATETIME()" + (active.HasValue?", Active=@a":"") + " WHERE VehicleID=@id";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@r", registration.Trim());
        cmd.Parameters.AddWithValue("@b", (object?)brand ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@m", (object?)model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", (object?)capacityKg ?? 0m);
        cmd.Parameters.AddWithValue("@p", (object?)palletSlots ?? 0);
        cmd.Parameters.AddWithValue("@k", kind);
        cmd.Parameters.AddWithValue("@e", (object?)e2Factor ?? 0.10m);
        if (active.HasValue) cmd.Parameters.AddWithValue("@a", active.Value);
        cmd.Parameters.AddWithValue("@id", vehicleId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetVehicle2ActiveAsync(int vehicleId, bool active)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.TVehicle SET Active=@a, ModifiedAtUTC=SYSUTCDATETIME() WHERE VehicleID=@id", cn);
        cmd.Parameters.AddWithValue("@a", active);
        cmd.Parameters.AddWithValue("@id", vehicleId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ================== Trips ==================
    public async Task<DataTable> GetTripsByDateAsync(DateTime date)
    {
        var dt = new DataTable();
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        string sql = @"SELECT t.TripID, t.TripDate, t.DriverID, t.VehicleID, t.TrailerVehicleID, t.RouteName, t.PlannedDeparture, t.Status,
                              d.FirstName + ' ' + d.LastName AS DriverName, v.Registration, vt.Registration AS TrailerRegistration,
                              CAST(f.MassFillPct AS DECIMAL(9,4)) AS MassFillPct,
                              CAST(f.SpaceFillPct AS DECIMAL(9,4)) AS SpaceFillPct,
                              CAST(CASE WHEN ISNULL(f.MassFillPct,0) > ISNULL(f.SpaceFillPct,0) THEN ISNULL(f.MassFillPct,0) ELSE ISNULL(f.SpaceFillPct,0) END AS DECIMAL(9,4)) AS FinalFillPct
                       FROM dbo.TTrip t
                       JOIN dbo.TDriver d ON t.DriverID = d.DriverID
                       JOIN dbo.TVehicle v ON t.VehicleID = v.VehicleID
                       LEFT JOIN dbo.TVehicle vt ON t.TrailerVehicleID = vt.VehicleID
                       LEFT JOIN dbo.vTTripFill f ON t.TripID = f.TripID
                       WHERE t.TripDate=@d
                       ORDER BY t.PlannedDeparture, t.TripID";
        await using var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@d", date.Date);
        using var da = new SqlDataAdapter(cmd); da.Fill(dt); return dt;
    }

    public async Task<long> AddTripAsync(DateTime date, int driverId, int vehicleId, string? route, TimeSpan? dep, string user, int? trailerVehicleId = null)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand(@"INSERT INTO dbo.TTrip(TripDate,DriverID,VehicleID,TrailerVehicleID,RouteName,PlannedDeparture,CreatedBy) VALUES(@dt,@dr,@veh,@tr,@r,@pd,@u); SELECT SCOPE_IDENTITY();", cn);
        cmd.Parameters.AddWithValue("@dt", date.Date);
        cmd.Parameters.AddWithValue("@dr", driverId);
        cmd.Parameters.AddWithValue("@veh", vehicleId);
        cmd.Parameters.AddWithValue("@tr", (object?)trailerVehicleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@r", (object?)route ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pd", (object?)dep ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@u", (object?)user ?? DBNull.Value);
        var obj = await cmd.ExecuteScalarAsync(); long id=0; long.TryParse(obj?.ToString(), out id); return id;
    }

    public async Task UpdateTripHeaderAsync(long tripId, int driverId, int vehicleId, int? trailerVehicleId, string? route, TimeSpan? plannedDeparture, string status, string user, string? notes)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        string sql = @"UPDATE dbo.TTrip SET DriverID=@d, VehicleID=@v, TrailerVehicleID=@tr, RouteName=@r, PlannedDeparture=@pd, Status=@s, Notes=@n, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@u WHERE TripID=@id";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@d", driverId);
        cmd.Parameters.AddWithValue("@v", vehicleId);
        cmd.Parameters.AddWithValue("@tr", (object?)trailerVehicleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@r", (object?)route ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pd", (object?)plannedDeparture ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@s", status);
        cmd.Parameters.AddWithValue("@n", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@id", tripId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateTripStatusAsync(long tripId, string status, string user)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.TTrip SET Status=@s, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@u WHERE TripID=@id", cn);
        cmd.Parameters.AddWithValue("@s", status);
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@id", tripId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTripAsync(long tripId)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.TTrip WHERE TripID=@id", cn);
        cmd.Parameters.AddWithValue("@id", tripId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ================== Loads ==================
    public async Task<DataTable> GetTripLoadsAsync(long tripId)
    {
        var dt = new DataTable(); await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await EnsureTripLoadOrderColumnAsync(cn);
        string sql = "SELECT TripLoadID, TripID, SequenceNo, CustomerCode, MeatKg, CarcassCount, PalletsH1, ContainersE2, Comment, OrderID FROM dbo.TTripLoad WHERE TripID=@id ORDER BY SequenceNo, TripLoadID";
        await using var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@id", tripId);
        using var da = new SqlDataAdapter(cmd); da.Fill(dt); return dt;
    }

    public async Task<long> AddTripLoadAsync(long tripId, string? customer, decimal meatKg, int carcass, int pallets, int e2, string? comment)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        int nextSeq = 1;
        await using (var seqCmd = new SqlCommand("SELECT ISNULL(MAX(SequenceNo),0)+1 FROM dbo.TTripLoad WHERE TripID=@t", cn))
        { seqCmd.Parameters.AddWithValue("@t", tripId); var objSeq = await seqCmd.ExecuteScalarAsync(); nextSeq = Convert.ToInt32(objSeq); }
        await using var cmd = new SqlCommand(@"INSERT INTO dbo.TTripLoad(TripID,SequenceNo,CustomerCode,MeatKg,CarcassCount,PalletsH1,ContainersE2,Comment) VALUES(@t,@s,@c,@kg,@car,@p,@e,@m); SELECT SCOPE_IDENTITY();", cn);
        cmd.Parameters.AddWithValue("@t", tripId);
        cmd.Parameters.AddWithValue("@s", nextSeq);
        cmd.Parameters.AddWithValue("@c", (object?)customer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@kg", meatKg);
        cmd.Parameters.AddWithValue("@car", carcass);
        cmd.Parameters.AddWithValue("@p", pallets);
        cmd.Parameters.AddWithValue("@e", e2);
        cmd.Parameters.AddWithValue("@m", (object?)comment ?? DBNull.Value);
        var idObj = await cmd.ExecuteScalarAsync(); long id=0; long.TryParse(idObj?.ToString(), out id); return id;
    }

    public async Task<long> AddTripLoadFromOrderAsync(long tripId, int orderId, string user)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await EnsureTripLoadOrderColumnAsync(cn);
        decimal kg = 0m; int klientId = 0;
        await using (var cmd = new SqlCommand(@"SELECT z.KlientId, SUM(ISNULL(zmt.Ilosc,0)) FROM dbo.ZamowieniaMieso z JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id=zmt.ZamowienieId WHERE z.Id=@id GROUP BY z.KlientId", cn))
        {
            cmd.Parameters.AddWithValue("@id", orderId);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync()) { klientId = rd.IsDBNull(0)?0:rd.GetInt32(0); kg = rd.IsDBNull(1)?0:Convert.ToDecimal(rd.GetValue(1)); }
        }
        int nextSeq = 1;
        await using (var cmdSeq = new SqlCommand("SELECT ISNULL(MAX(SequenceNo),0)+1 FROM dbo.TTripLoad WHERE TripID=@t", cn)) { cmdSeq.Parameters.AddWithValue("@t", tripId); var o = await cmdSeq.ExecuteScalarAsync(); nextSeq = Convert.ToInt32(o); }
        int containers = kg <= 0 ? 0 : (int)Math.Ceiling(kg / 15m);
        int palletSlots = (int)Math.Ceiling(containers / 36m); // rough mapping
        await using var cmdIns = new SqlCommand(@"INSERT INTO dbo.TTripLoad(TripID, SequenceNo, CustomerCode, MeatKg, CarcassCount, PalletsH1, ContainersE2, Comment, OrderID)
                                                 VALUES(@t,@s,@c,@kg,0,@pal,@e2,@com,@oid); SELECT SCOPE_IDENTITY();", cn);
        cmdIns.Parameters.AddWithValue("@t", tripId);
        cmdIns.Parameters.AddWithValue("@s", nextSeq);
        cmdIns.Parameters.AddWithValue("@c", (object)(klientId==0?null:klientId.ToString()) ?? DBNull.Value);
        cmdIns.Parameters.AddWithValue("@kg", kg);
        cmdIns.Parameters.AddWithValue("@pal", palletSlots);
        cmdIns.Parameters.AddWithValue("@e2", containers);
        cmdIns.Parameters.AddWithValue("@com", (object)$"Order #{orderId}" ?? DBNull.Value);
        cmdIns.Parameters.AddWithValue("@oid", orderId);
        var idObj = await cmdIns.ExecuteScalarAsync(); long id=0; long.TryParse(idObj?.ToString(), out id); return id;
    }

    public async Task UpdateTripLoadAsync(long tripLoadId, int seq, string? customer, decimal meatKg, int carcass, int pallets, int e2, string? comment)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand(@"UPDATE dbo.TTripLoad SET SequenceNo=@s, CustomerCode=@c, MeatKg=@kg, CarcassCount=@car, PalletsH1=@p, ContainersE2=@e, Comment=@m WHERE TripLoadID=@id", cn);
        cmd.Parameters.AddWithValue("@s", seq);
        cmd.Parameters.AddWithValue("@c", (object?)customer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@kg", meatKg);
        cmd.Parameters.AddWithValue("@car", carcass);
        cmd.Parameters.AddWithValue("@p", pallets);
        cmd.Parameters.AddWithValue("@e", e2);
        cmd.Parameters.AddWithValue("@m", (object?)comment ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", tripLoadId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTripLoadAsync(long tripLoadId)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.TTripLoad WHERE TripLoadID=@id", cn);
        cmd.Parameters.AddWithValue("@id", tripLoadId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RenumberTripLoadsAsync(long tripId)
    {
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        var sql = @";WITH x AS (SELECT TripLoadID, ROW_NUMBER() OVER(ORDER BY SequenceNo, TripLoadID) rn FROM dbo.TTripLoad WHERE TripID=@id) UPDATE l SET SequenceNo = x.rn FROM dbo.TTripLoad l JOIN x ON l.TripLoadID=x.TripLoadID";
        await using var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@id", tripId); await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DataTable> GetAvailableOrdersForDateAsync(DateTime date)
    {
        var dt = new DataTable();
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        await EnsureTripLoadOrderColumnAsync(cn);
        var sql = @"SELECT z.Id AS OrderID, z.KlientId, CAST(ISNULL(SUM(zmt.Ilosc),0) AS DECIMAL(12,2)) AS TotalKg, ISNULL(z.Status,'Nowe') AS Status, LEFT(ISNULL(z.Uwagi,''),200) AS Uwagi
                    FROM dbo.ZamowieniaMieso z
                    JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id = zmt.ZamowienieId
                    WHERE z.DataZamowienia=@d AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')
                      AND NOT EXISTS (SELECT 1 FROM dbo.TTripLoad tl JOIN dbo.TTrip t ON tl.TripID=t.TripID WHERE tl.OrderID = z.Id AND t.TripDate=@d)
                    GROUP BY z.Id, z.KlientId, ISNULL(z.Status,'Nowe'), ISNULL(z.Uwagi,'')
                    ORDER BY z.Id";
        await using var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@d", date.Date);
        using var da = new SqlDataAdapter(cmd); da.Fill(dt);
        if (!dt.Columns.Contains("ContainersEst")) dt.Columns.Add("ContainersEst", typeof(int));
        if (!dt.Columns.Contains("PalletsEst")) dt.Columns.Add("PalletsEst", typeof(decimal));
        foreach (DataRow r in dt.Rows)
        {
            var kg = r["TotalKg"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalKg"]);
            var cont = kg <= 0 ? 0 : (int)Math.Ceiling(kg / 15m);
            var pal = cont / 36m;
            r["ContainersEst"] = cont;
            r["PalletsEst"] = pal;
        }
        return dt;
    }

    public async Task<DataTable> GetDriversAsync()
    {
        var src = await GetDrivers2Async(includeInactive: false);
        var dt = new DataTable();
        dt.Columns.Add("GID", typeof(int));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("Phone", typeof(string));
        dt.Columns.Add("Active", typeof(bool));
        foreach (DataRow r in src.Rows)
        {
            dt.Rows.Add(r["DriverID"], r["FullName"], r["Phone"], r["Active"]);
        }
        return dt;
    }

    public async Task AddDriverAsync(string fullName, string user)
    {
        await AddDriver2Async(fullName, phone: null);
    }

    public async Task SoftDeleteDriverAsync(int gid)
    {
        await SetDriver2ActiveAsync(gid, false);
    }

    public async Task<DataTable> GetVehiclesAsync(string kind)
    {
        if (!int.TryParse(kind, out var k)) k = 3;
        var src = await GetVehicles2Async(k, includeInactive: false);
        var dt = new DataTable();
        dt.Columns.Add("ID", typeof(string)); // registration as ID
        dt.Columns.Add("Brand", typeof(string));
        dt.Columns.Add("Model", typeof(string));
        dt.Columns.Add("Capacity", typeof(decimal));
        dt.Columns.Add("Kind", typeof(int));
        foreach (DataRow r in src.Rows)
        {
            dt.Rows.Add(r["Registration"], r["Brand"], r["Model"], r["CapacityKg"], r["Kind"]);
        }
        return dt;
    }

    public async Task AddVehicleAsync(string registration, string kind, string? brand, string? model, decimal? capacityKg)
    {
        if (!int.TryParse(kind, out var k)) k = 3;
        await AddVehicle2Async(registration, capacityKg, palletSlotsH1: null, kind: k, brand: brand, model: model, e2Factor: null);
    }

    public async Task UpdateVehicleAsync(string registration, string? brand, string? model, decimal? capacityKg)
    {
        // find vehicleId by registration
        await using var cn = new SqlConnection(_conn); await cn.OpenAsync();
        int vehicleId = 0; decimal? e2Factor = null; int? slots = null; int kind = 3;
        await using (var cmd = new SqlCommand("SELECT VehicleID, Kind, PalletSlotsH1, E2Factor FROM dbo.TVehicle WHERE Registration=@r", cn))
        { cmd.Parameters.AddWithValue("@r", registration); await using var rd = await cmd.ExecuteReaderAsync(); if (await rd.ReadAsync()) { vehicleId = rd.GetInt32(0); kind = rd.GetInt32(1); slots = rd.IsDBNull(2)?null:rd.GetInt32(2); e2Factor = rd.IsDBNull(3)?null:rd.GetDecimal(3); } }
        if (vehicleId == 0) throw new InvalidOperationException("Nie znaleziono pojazdu o rejestracji: " + registration);
        await UpdateVehicle2Async(vehicleId, registration, capacityKg, slots, kind, brand, model, e2Factor, active: null);
    }
}
