using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport
{
    public sealed class TransportRepository
    {
        private readonly string _conn;
        private readonly string _connSymf;

        public TransportRepository(string connLibra, string connSymfonia) 
        {
            _conn = connLibra ?? throw new ArgumentNullException(nameof(connLibra));
            _connSymf = connSymfonia ?? throw new ArgumentNullException(nameof(connSymfonia));
        }
        public async Task<Dictionary<int, string>> GetClientNamesAsync(IEnumerable<int> ids)
        {
            var result = new Dictionary<int, string>();
            var idList = ids?.Distinct().Where(i => i > 0).ToList() ?? new List<int>();
            if (idList.Count == 0) return result;

            await using var cn = new SqlConnection(_connSymf);
            await cn.OpenAsync();

            // Diagnoza œrodowiska
            string currentDb = "", serverName = "";
            await using (var diag = new SqlCommand("SELECT DB_NAME(), @@SERVERNAME;", cn))
            await using (var r = await diag.ExecuteReaderAsync())
                if (await r.ReadAsync()) { currentDb = r.GetString(0); serverName = r.GetString(1); }

            var inParams = string.Join(",", idList.Select((_, i) => $"@p{i}"));

            // Najpierw UDPiorkowscy, potem HANDEL (kolejnoœæ wg Twojej listy)
            var candidates = new[]
            {
                "[HANDEL].[SSCommon].[STContractors]"
            };

            foreach (var table in candidates)
            {
                var sql = $@"SELECT Id, Shortcut, Name FROM {table} WHERE Id IN ({inParams})";
                try
                {
                    await using var cmd = new SqlCommand(sql, cn);
                    for (int i = 0; i < idList.Count; i++)
                        cmd.Parameters.Add($"@p{i}", SqlDbType.Int).Value = idList[i];

                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        if (rd.IsDBNull(0)) continue;
                        int id = rd.GetInt32(0);
                        // Prefer Shortcut over Name
                        string displayName = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        if (string.IsNullOrWhiteSpace(displayName) && !rd.IsDBNull(2))
                            displayName = rd.GetString(2);
                        if (!string.IsNullOrWhiteSpace(displayName)) result[id] = displayName;
                    }
                    return result; // uda³o siê na tym wariancie
                }
                catch (SqlException ex) when (ex.Number == 208) { /* spróbuj nastêpnego */ }
            }

            throw new InvalidOperationException(
                $"Nie znajdujê STContractors w tym œrodowisku. Serwer: {serverName}, baza po³¹czenia: {currentDb}. " +
                "SprawdŸ, czy ³¹czysz siê do serwera, na którym istnieje baza UDPiorkowscy lub HANDEL, " +
                "albo utwórz synonim dbo.STContractors -> UDPiorkowscy.SSCommon.STContractors.");
        }


        // ================== Drivers ==================
        public async Task<DataTable> GetDrivers2Async(bool includeInactive = false)
        {
            var dt = new DataTable();
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var da = new SqlDataAdapter($@"
                SELECT DriverID, FirstName + ' ' + LastName AS FullName, Phone, Active 
                FROM dbo.TDriver 
                WHERE {(includeInactive ? "1=1" : "Active=1")} 
                ORDER BY LastName, FirstName", cn);
            da.Fill(dt);
            return dt;
        }

        public async Task<int> AddDriver2Async(string fullName, string? phone)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.FirstOrDefault() ?? string.Empty;
            var lastName = string.Join(" ", parts.Skip(1));

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.TDriver (FirstName, LastName, Phone, Active, CreatedAtUTC) 
                VALUES (@fn, @ln, @ph, 1, SYSUTCDATETIME()); 
                SELECT SCOPE_IDENTITY()", cn);
            cmd.Parameters.AddWithValue("@fn", firstName);
            cmd.Parameters.AddWithValue("@ln", lastName);
            cmd.Parameters.AddWithValue("@ph", (object?)phone ?? DBNull.Value);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateDriver2Async(int driverId, string fullName, string? phone, bool? active = null)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.FirstOrDefault() ?? string.Empty;
            var lastName = string.Join(" ", parts.Skip(1));

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.TDriver 
                SET FirstName = @fn, LastName = @ln, Phone = @ph, ModifiedAtUTC = SYSUTCDATETIME()
                WHERE DriverID = @id", cn);
            cmd.Parameters.AddWithValue("@fn", firstName);
            cmd.Parameters.AddWithValue("@ln", lastName);
            cmd.Parameters.AddWithValue("@ph", (object?)phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", driverId);
            await cmd.ExecuteNonQueryAsync();
            
            if (active.HasValue)
            {
                using var cmd2 = new SqlCommand(@"
                    UPDATE dbo.TDriver 
                    SET Active = @a, ModifiedAtUTC = SYSUTCDATETIME() 
                    WHERE DriverID = @id", cn);
                cmd2.Parameters.AddWithValue("@a", active.Value);
                cmd2.Parameters.AddWithValue("@id", driverId);
                await cmd2.ExecuteNonQueryAsync();
            }
        }

        // Add missing method
        public async Task SetDriver2ActiveAsync(int driverId, bool active)
        {
            var name = await GetDriverNameAsync(driverId);
            await UpdateDriver2Async(driverId, name, null, active);
        }

        // ================== Vehicles ==================
        public async Task<DataTable> GetVehicles2Async(int? kind = null, bool includeInactive = false)
        {
            var dt = new DataTable();
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            var where = new List<string>();
            if (!includeInactive) where.Add("Active=1");
            if (kind.HasValue) where.Add($"Kind={kind.Value}");
            using var da = new SqlDataAdapter($@"
                SELECT VehicleID, Registration, Brand, Model, CapacityKg, Kind, 
                       PalletSlotsH1, E2Factor, Active 
                FROM dbo.TVehicle 
                WHERE {(where.Any() ? string.Join(" AND ", where) : "1=1")}
                ORDER BY Registration", cn);
            da.Fill(dt);
            return dt;
        }

        public async Task<int> AddVehicle2Async(string registration, decimal? capacityKg, int? palletSlotsH1, 
            int kind, string? brand, string? model, decimal? e2Factor = null)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.TVehicle (
                    Registration, Brand, Model, CapacityKg, Kind, 
                    PalletSlotsH1, E2Factor, Active, CreatedAtUTC
                )
                VALUES (
                    @reg, @brand, @model, @cap, @kind, 
                    @slots, @e2, 1, SYSUTCDATETIME()
                );
                SELECT SCOPE_IDENTITY()", cn);
            cmd.Parameters.AddWithValue("@reg", registration);
            cmd.Parameters.AddWithValue("@brand", (object?)brand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model", (object?)model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cap", (object?)capacityKg ?? 0m);
            cmd.Parameters.AddWithValue("@kind", kind);
            cmd.Parameters.AddWithValue("@slots", (object?)palletSlotsH1 ?? 0);
            cmd.Parameters.AddWithValue("@e2", (object?)e2Factor ?? 0.10m);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateVehicle2Async(int vehicleId, string registration, decimal? capacityKg, 
            int? palletSlots, int kind, string? brand, string? model, decimal? e2Factor, bool? active = null)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.TVehicle 
                SET Registration = @reg, Brand = @brand, Model = @model, 
                    CapacityKg = @cap, Kind = @kind, PalletSlotsH1 = @slots, 
                    E2Factor = @e2, ModifiedAtUTC = SYSUTCDATETIME()
                WHERE VehicleID = @id", cn);
            cmd.Parameters.AddWithValue("@reg", registration);
            cmd.Parameters.AddWithValue("@brand", (object?)brand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model", (object?)model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cap", (object?)capacityKg ?? 0m);
            cmd.Parameters.AddWithValue("@kind", kind);
            cmd.Parameters.AddWithValue("@slots", (object?)palletSlots ?? 0);
            cmd.Parameters.AddWithValue("@e2", (object?)e2Factor ?? 0.10m);
            cmd.Parameters.AddWithValue("@id", vehicleId);
            await cmd.ExecuteNonQueryAsync();

            if (active.HasValue)
            {
                using var cmd2 = new SqlCommand(@"
                    UPDATE dbo.TVehicle 
                    SET Active = @a, ModifiedAtUTC = SYSUTCDATETIME() 
                    WHERE VehicleID = @id", cn);
                cmd2.Parameters.AddWithValue("@a", active.Value);
                cmd2.Parameters.AddWithValue("@id", vehicleId);
                await cmd2.ExecuteNonQueryAsync();
            }
        }

        // Add missing method
        public async Task SetVehicle2ActiveAsync(int vehicleId, bool active)
        {
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // Get existing vehicle data
            await using var getCmd = new SqlCommand(
                "SELECT Registration, Kind FROM dbo.TVehicle WHERE VehicleID = @id", cn);
            getCmd.Parameters.AddWithValue("@id", vehicleId);
            await using var rd = await getCmd.ExecuteReaderAsync();
            
            if (!await rd.ReadAsync())
                throw new InvalidOperationException($"Nie znaleziono pojazdu o ID={vehicleId}");

            var registration = rd.GetString(0);
            var kind = rd.GetInt32(1);

            // Update with active status
            await UpdateVehicle2Async(vehicleId, registration, null, null, kind, null, null, null, active);
        }

        // ================== Trips ==================
        public async Task<DataTable> GetTripsByDateAsync(DateTime date)
        {
            var dt = new DataTable();
            
            // Najpierw pobierz wszystkie ³adunki dla danej daty aby zebraæ ID klientów
            var customerIds = new HashSet<int>();
            using (var cnLoads = new SqlConnection(_conn))
            {
                await cnLoads.OpenAsync();
                using var cmdLoads = new SqlCommand(@"
                    SELECT DISTINCT TRY_CAST(tl.CustomerCode AS INT) as CustomerId
                    FROM dbo.TTrip t
                    JOIN dbo.TTripLoad tl ON t.TripID = tl.TripID
                    WHERE t.TripDate = @date 
                    AND tl.CustomerCode IS NOT NULL", cnLoads);
                cmdLoads.Parameters.AddWithValue("@date", date);
                using var reader = await cmdLoads.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        customerIds.Add(reader.GetInt32(0));
                }
            }

            // Pobierz nazwy klientów
            var customerNames = await GetClientNamesAsync(customerIds);

            // G³ówne zapytanie
            using (var cn = new SqlConnection(_conn))
            {
                await cn.OpenAsync();
                using var da = new SqlDataAdapter(@"
                    ;WITH LoadCustomers AS (
                        SELECT 
                            tl.TripID,
                            STUFF((
                                SELECT N' > ' + tl2.CustomerCode
                                FROM dbo.TTripLoad tl2
                                WHERE tl2.TripID = tl.TripID 
                                AND tl2.CustomerCode IS NOT NULL
                                ORDER BY tl2.SequenceNo
                                FOR XML PATH(''), TYPE
                            ).value('.', 'nvarchar(max)'), 1, 3, N'') AS RouteFromLoads
                        FROM dbo.TTripLoad tl
                        GROUP BY tl.TripID
                    )
                    SELECT 
                        t.TripID, 
                        t.TripDate, 
                        t.DriverID, 
                        t.VehicleID, 
                        t.TrailerVehicleID,
                        COALESCE(lc.RouteFromLoads, t.RouteName) AS RouteName,
                        t.PlannedDeparture, 
                        t.PlannedReturn, 
                        t.Status, 
                        t.Notes,
                        d.FirstName + ' ' + d.LastName AS DriverName,
                        v.Registration AS VehicleReg,
                        v.CapacityKg AS VehicleCapacity,
                        v.PalletSlotsH1 AS VehiclePallets,
                        vt.Registration AS TrailerReg,
                        vt.CapacityKg AS TrailerCapacity,
                        vt.PalletSlotsH1 AS TrailerPallets,
                        f.MassFillPct,
                        f.SpaceFillPct,
                        CASE WHEN f.MassFillPct > f.SpaceFillPct THEN f.MassFillPct ELSE f.SpaceFillPct END AS FinalFillPct
                    FROM dbo.TTrip t
                    JOIN dbo.TDriver d ON t.DriverID = d.DriverID
                    JOIN dbo.TVehicle v ON t.VehicleID = v.VehicleID
                    LEFT JOIN dbo.TVehicle vt ON t.TrailerVehicleID = vt.VehicleID
                    LEFT JOIN dbo.vTTripFill f ON t.TripID = f.TripID
                    LEFT JOIN LoadCustomers lc ON t.TripID = lc.TripID
                    WHERE t.TripDate = @date
                    ORDER BY t.PlannedDeparture, t.TripID;", cn);

                da.SelectCommand.Parameters.AddWithValue("@date", date);
                da.Fill(dt);

                // Zamieñ kody klientów na nazwy
                if (dt.Columns.Contains("RouteName"))
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["RouteName"] != DBNull.Value)
                        {
                            var route = row["RouteName"].ToString();
                            if (!string.IsNullOrEmpty(route))
                            {
                                var parts = route.Split(new[] { " > " }, StringSplitOptions.None);
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (int.TryParse(parts[i], out int customerId) && customerNames.TryGetValue(customerId, out string name))
                                    {
                                        parts[i] = name;
                                    }
                                }
                                row["RouteName"] = string.Join(" > ", parts);
                            }
                        }
                    }
                }
            }

            return dt;
        }

        public async Task<long> AddTripAsync(DateTime date, int driverId, int vehicleId, string? route, 
            TimeSpan? dep, string user, DateTime? plannedDepartureDt = null, int? trailerVehicleId = null)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.TTrip (
                    TripDate, DriverID, VehicleID, TrailerVehicleID, 
                    RouteName, PlannedDeparture, Status, CreatedAtUTC, CreatedBy
                )
                VALUES (
                    @date, @driver, @vehicle, @trailer, 
                    @route, @dep, 'Planned', SYSUTCDATETIME(), @user
                );
                SELECT SCOPE_IDENTITY()", cn);
            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@driver", driverId);
            cmd.Parameters.AddWithValue("@vehicle", vehicleId);
            cmd.Parameters.AddWithValue("@trailer", (object?)trailerVehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@route", (object?)route ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dep", (object?)dep ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@user", user);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateTripHeaderAsync(long tripId, int driverId, int vehicleId, int? trailerVehicleId, 
            string? route, TimeSpan? plannedDeparture, string status, string user, string? notes, 
            DateTime? plannedDepartureDt)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.TTrip 
                SET DriverID = @driver, VehicleID = @vehicle, TrailerVehicleID = @trailer,
                    RouteName = @route, PlannedDeparture = @dep, Status = @status, 
                    Notes = @notes, ModifiedAtUTC = SYSUTCDATETIME(), ModifiedBy = @user
                WHERE TripID = @id", cn);
            cmd.Parameters.AddWithValue("@driver", driverId);
            cmd.Parameters.AddWithValue("@vehicle", vehicleId);
            cmd.Parameters.AddWithValue("@trailer", (object?)trailerVehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@route", (object?)route ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dep", (object?)plannedDeparture ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@user", user);
            cmd.Parameters.AddWithValue("@id", tripId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ================== Loads ==================
        public async Task<DataTable> GetTripLoadsAsync(long tripId)
        {
            var dt = new DataTable();
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var da = new SqlDataAdapter(@"
        SELECT TripLoadID, TripID, SequenceNo, CustomerCode, 
               MeatKg, CarcassCount, PalletsH1, ContainersE2, Comment
        FROM dbo.TTripLoad 
        WHERE TripID = @id
        ORDER BY SequenceNo", cn);
            da.SelectCommand.Parameters.AddWithValue("@id", tripId);
            da.Fill(dt);

            // >>> NOWE: kolumna „Klient” + mapowanie nazw
            if (!dt.Columns.Contains("Klient"))
                dt.Columns.Add("Klient", typeof(string));

            // zbierz ID z CustomerCode (jeœli to liczby)
            var idSet = new HashSet<int>();
            foreach (DataRow r in dt.Rows)
            {
                var code = r["CustomerCode"]?.ToString();
                if (int.TryParse(code, out var id) && id > 0)
                    idSet.Add(id);
            }

            var names = await GetClientNamesAsync(idSet);

            foreach (DataRow r in dt.Rows)
            {
                var code = r["CustomerCode"]?.ToString();
                if (int.TryParse(code, out var id) && id > 0 && names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
                    r["Klient"] = name;
                else
                    r["Klient"] = code; // fallback: poka¿ to co by³o (np. kod)
            }

            return dt;
        }


        public async Task UpdateTripLoadAsync(long tripLoadId, int seq, string? customer, 
            decimal meatKg, int carcass, int pallets, int e2, string? comment)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                UPDATE dbo.TTripLoad 
                SET SequenceNo = @seq, CustomerCode = @cust, 
                    MeatKg = @meat, CarcassCount = @carcass,
                    PalletsH1 = @pal, ContainersE2 = @e2, Comment = @com
                WHERE TripLoadID = @id", cn);
            cmd.Parameters.AddWithValue("@seq", seq);
            cmd.Parameters.AddWithValue("@cust", (object?)customer ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@meat", meatKg);
            cmd.Parameters.AddWithValue("@carcass", carcass);
            cmd.Parameters.AddWithValue("@pal", pallets);
            cmd.Parameters.AddWithValue("@e2", e2);
            cmd.Parameters.AddWithValue("@com", (object?)comment ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", tripLoadId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteTripLoadAsync(long tripLoadId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(
                "DELETE FROM dbo.TTripLoad WHERE TripLoadID = @id", cn);
            cmd.Parameters.AddWithValue("@id", tripLoadId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RenumberTripLoadsAsync(long tripId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
                ;WITH x AS (
                    SELECT TripLoadID, ROW_NUMBER() OVER (ORDER BY SequenceNo, TripLoadID) AS rn
                    FROM dbo.TTripLoad 
                    WHERE TripID = @id
                )
                UPDATE l 
                SET SequenceNo = x.rn
                FROM dbo.TTripLoad l
                JOIN x ON l.TripLoadID = x.TripLoadID", cn);
            cmd.Parameters.AddWithValue("@id", tripId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<DataTable> GetAvailableOrdersForDateAsync(DateTime date)
        {
            var dt = new DataTable();
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var da = new SqlDataAdapter(@"
        SELECT z.Id AS OrderID, z.KlientId,
               CAST(ISNULL(SUM(zmt.Ilosc),0) AS DECIMAL(12,2)) AS TotalKg,
               ISNULL(z.Status,'Nowe') AS Status,
               LEFT(ISNULL(z.Uwagi,''),200) AS Notes
        FROM dbo.ZamowieniaMieso z
        JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id = zmt.ZamowienieId
        WHERE z.DataZamowienia = @date 
          AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')
          AND NOT EXISTS (
            SELECT 1 
            FROM dbo.TTripLoad tl 
            JOIN dbo.TTrip t ON tl.TripID = t.TripID 
            WHERE t.TripDate = @date AND tl.CustomerCode = CAST(z.KlientId AS NVARCHAR(50))
          )
        GROUP BY z.Id, z.KlientId, ISNULL(z.Status,'Nowe'), z.Uwagi
        ORDER BY z.Id", cn);
            da.SelectCommand.Parameters.AddWithValue("@date", date);
            da.Fill(dt);

            // Estymacje
            dt.Columns.Add("ContainersEst", typeof(int));
            dt.Columns.Add("PalletsEst", typeof(decimal));
            foreach (DataRow r in dt.Rows)
            {
                var kg = r["TotalKg"] is decimal d ? d : 0m;
                var containers = kg <= 0 ? 0 : (int)Math.Ceiling(kg / 15m);
                var pallets = containers / 36m;
                r["ContainersEst"] = containers;
                r["PalletsEst"] = pallets;
            }

            // >>> NOWE: kolumna „Klient” + mapowanie nazw
            dt.Columns.Add("Klient", typeof(string));

            // zbierz unikalne ID i pobierz nazwy 1 strza³em
            var ids = dt.AsEnumerable()
                        .Select(r => r.Field<int>("KlientId"))
                        .Where(id => id > 0)
                        .Distinct()
                        .ToList();

            var names = await GetClientNamesAsync(ids); // Dictionary<int,string>

            foreach (DataRow r in dt.Rows)
            {
                var id = r.Field<int>("KlientId");
                if (id > 0 && names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
                    r["Klient"] = name;
                else
                    r["Klient"] = $"ID:{id}";
            }

            return dt;
        }

        // Add missing method
        public async Task<long> AddTripLoadFromOrderAsync(long tripId, int orderId, string user)
        {
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // First check trip status
            using var chkCmd = new SqlCommand("SELECT Status FROM dbo.TTrip WHERE TripID = @id", cn);
            chkCmd.Parameters.AddWithValue("@id", tripId);
            var status = await chkCmd.ExecuteScalarAsync() as string;
            if (status == null) throw new InvalidOperationException($"Nie znaleziono kursu o ID={tripId}");
            if (status == "Completed") throw new InvalidOperationException("Nie mo¿na dodawaæ ³adunków do zakoñczonego kursu");
            if (status == "Canceled") throw new InvalidOperationException("Nie mo¿na dodawaæ ³adunków do anulowanego kursu");

            // Get order details
            decimal totalKg = 0;
            int clientId = 0;
            using (var ordCmd = new SqlCommand(@"
                SELECT z.KlientId, SUM(zmt.Ilosc) AS TotalKg
                FROM dbo.ZamowieniaMieso z
                JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id = zmt.ZamowienieId
                WHERE z.Id = @id
                GROUP BY z.KlientId", cn))
            {
                ordCmd.Parameters.AddWithValue("@id", orderId);
                using var rd = await ordCmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    clientId = !rd.IsDBNull(0) ? rd.GetInt32(0) : 0;
                    totalKg = !rd.IsDBNull(1) ? Convert.ToDecimal(rd.GetValue(1)) : 0m;
                }
            }

            // Calculate containers and pallets
            int containers = totalKg <= 0 ? 0 : (int)Math.Ceiling(totalKg / 15m);
            int palletSlots = (int)Math.Ceiling(containers / 36m);

            // Get next sequence number
            int nextSeq = 1;
            using (var seqCmd = new SqlCommand("SELECT ISNULL(MAX(SequenceNo), 0) + 1 FROM dbo.TTripLoad WHERE TripID = @id", cn))
            {
                seqCmd.Parameters.AddWithValue("@id", tripId);
                nextSeq = Convert.ToInt32(await seqCmd.ExecuteScalarAsync());
            }

            // Insert load
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.TTripLoad (
                    TripID, SequenceNo, CustomerCode, OrderID,
                    MeatKg, CarcassCount, PalletsH1, ContainersE2,
                    Comment, CreatedAtUTC
                )
                VALUES (
                    @trip, @seq, @cust, @order,
                    @kg, 0, @pal, @e2,
                    @comm, SYSUTCDATETIME()
                );
                SELECT SCOPE_IDENTITY();", cn);
            
            cmd.Parameters.AddWithValue("@trip", tripId);
            cmd.Parameters.AddWithValue("@seq", nextSeq);
            cmd.Parameters.AddWithValue("@cust", clientId.ToString());
            cmd.Parameters.AddWithValue("@order", orderId);
            cmd.Parameters.AddWithValue("@kg", totalKg);
            cmd.Parameters.AddWithValue("@pal", palletSlots);
            cmd.Parameters.AddWithValue("@e2", containers);
            cmd.Parameters.AddWithValue("@comm", $"Zamówienie #{orderId}");

            var idObj = await cmd.ExecuteScalarAsync();
            long id = 0;
            long.TryParse(idObj?.ToString(), out id);
            return id;
        }

        // Add missing helper method
        private async Task<string> GetDriverNameAsync(int driverId)
        {
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT FirstName + ' ' + LastName FROM dbo.TDriver WHERE DriverID = @id", cn);
            cmd.Parameters.AddWithValue("@id", driverId);
            var result = await cmd.ExecuteScalarAsync() as string;
            return result ?? throw new InvalidOperationException($"Nie znaleziono kierowcy o ID={driverId}");
        }

        // Add legacy compatibility methods
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
                dt.Rows.Add(
                    r["DriverID"],
                    r["FullName"],
                    r["Phone"],
                    r["Active"]
                );
            }
            return dt;
        }

        public async Task AddDriverAsync(string fullName, string user)
        {
            await AddDriver2Async(fullName, phone: null);
        }

        public async Task SoftDeleteDriverAsync(int gid)
        {
            await UpdateDriver2Async(gid, await GetDriverNameAsync(gid), null, active: false);
        }

        public async Task<DataTable> GetVehiclesAsync(string kind)
        {
            if (!int.TryParse(kind, out var k)) k = 3;
            var src = await GetVehicles2Async(k, includeInactive: false);
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("Brand", typeof(string));
            dt.Columns.Add("Model", typeof(string));
            dt.Columns.Add("Capacity", typeof(decimal));
            dt.Columns.Add("Kind", typeof(int));
            foreach (DataRow r in src.Rows)
            {
                dt.Rows.Add(
                    r["Registration"],
                    r["Brand"],
                    r["Model"],
                    r["CapacityKg"],
                    r["Kind"]
                );
            }
            return dt;
        }

        public async Task AddVehicleAsync(string registration, string kind, string? brand, string? model, decimal? capacityKg)
        {
            if (!int.TryParse(kind, out var k)) k = 3;
            await AddVehicle2Async(registration, capacityKg, palletSlotsH1: null, kind: k, brand: brand, model: model);
        }

        public async Task UpdateVehicleAsync(string registration, string? brand, string? model, decimal? capacityKg)
        {
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // Get existing vehicle data first
            int vehicleId = 0;
            int kind = 3;
            int? palletSlots = null;
            decimal? e2Factor = null;

            await using (var cmd = new SqlCommand(
                "SELECT VehicleID, Kind, PalletSlotsH1, E2Factor FROM dbo.TVehicle WHERE Registration = @r", cn))
            {
                cmd.Parameters.AddWithValue("@r", registration);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    vehicleId = rd.GetInt32(0);
                    kind = rd.GetInt32(1);
                    palletSlots = rd.IsDBNull(2) ? null : rd.GetInt32(2);
                    e2Factor = rd.IsDBNull(3) ? null : rd.GetDecimal(3);
                }
            }

            if (vehicleId == 0)
                throw new InvalidOperationException($"Nie znaleziono pojazdu o rejestracji {registration}");

            await UpdateVehicle2Async(vehicleId, registration, capacityKg, palletSlots, kind, brand, model, e2Factor);
        }

        // Add missing method
        public async Task<long> AddTripLoadAsync(long tripId, string? customer, decimal meatKg, int carcass, int pallets, int e2, string? comment)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // Get next sequence number
            int nextSeq = 1;
            using (var cmdSeq = new SqlCommand(
                "SELECT ISNULL(MAX(SequenceNo),0) + 1 FROM dbo.TTripLoad WHERE TripID = @id", cn))
            {
                cmdSeq.Parameters.AddWithValue("@id", tripId);
                var seqObj = await cmdSeq.ExecuteScalarAsync();
                if (seqObj != null) nextSeq = Convert.ToInt32(seqObj);
            }

            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.TTripLoad (
                    TripID, SequenceNo, CustomerCode,
                    MeatKg, CarcassCount, PalletsH1, ContainersE2,
                    Comment, CreatedAtUTC
                )
                VALUES (
                    @trip, @seq, @cust,
                    @meat, @carcass, @pal, @e2,
                    @com, SYSUTCDATETIME()
                );
                SELECT SCOPE_IDENTITY()", cn);

            cmd.Parameters.AddWithValue("@trip", tripId);
            cmd.Parameters.AddWithValue("@seq", nextSeq);
            cmd.Parameters.AddWithValue("@cust", (object?)customer ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@meat", meatKg);
            cmd.Parameters.AddWithValue("@carcass", carcass);
            cmd.Parameters.AddWithValue("@pal", pallets);
            cmd.Parameters.AddWithValue("@e2", e2);
            cmd.Parameters.AddWithValue("@com", (object?)comment ?? DBNull.Value);

            var idObj = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(idObj);
        }
    }
}
