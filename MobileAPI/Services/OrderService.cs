using Microsoft.Data.SqlClient;
using MobileAPI.DTOs;

namespace MobileAPI.Services;

public class OrderService
{
    private readonly string _libraNetConnection;
    private readonly string _handelConnection;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IConfiguration configuration, ILogger<OrderService> logger)
    {
        _libraNetConnection = configuration.GetConnectionString("LibraNet")
            ?? throw new InvalidOperationException("LibraNet connection string is missing.");
        _handelConnection = configuration.GetConnectionString("Handel")
            ?? throw new InvalidOperationException("Handel connection string is missing.");
        _logger = logger;
    }

    /// <summary>
    /// Gets orders filtered by handlowiec and optional date range.
    /// </summary>
    public async Task<List<OrderDto>> GetOrdersAsync(
        string handlowiec, DateTime? dateFrom = null, DateTime? dateTo = null, string? status = null)
    {
        var orders = new List<OrderDto>();

        await using var connection = new SqlConnection(_libraNetConnection);
        await connection.OpenAsync();

        var sql = @"
            SELECT
                z.Id,
                z.KlientId,
                z.Odbiorca,
                z.Handlowiec,
                z.IloscZamowiona,
                z.IloscFaktyczna,
                z.Roznica,
                z.Pojemniki,
                z.Palety,
                z.TrybE2,
                z.DataPrzyjecia,
                z.GodzinaPrzyjecia,
                z.TerminOdbioru,
                z.DataUboju,
                z.UtworzonePrzez,
                z.Status,
                z.MaNotatke,
                z.MaFolie,
                z.MaHallal,
                z.CzyMaCeny,
                z.SredniaCena,
                z.Uwagi,
                z.TransportKursId,
                z.CzyZrealizowane,
                z.DataWydania,
                z.Waluta
            FROM dbo.ZamowieniaMieso z
            WHERE z.Handlowiec = @Handlowiec";

        if (dateFrom.HasValue)
            sql += " AND z.DataPrzyjecia >= @DateFrom";
        if (dateTo.HasValue)
            sql += " AND z.DataPrzyjecia <= @DateTo";
        if (!string.IsNullOrEmpty(status))
            sql += " AND z.Status = @Status";

        sql += " ORDER BY z.DataPrzyjecia DESC, z.Id DESC";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);

        if (dateFrom.HasValue)
            command.Parameters.AddWithValue("@DateFrom", dateFrom.Value.Date);
        if (dateTo.HasValue)
            command.Parameters.AddWithValue("@DateTo", dateTo.Value.Date.AddDays(1).AddTicks(-1));
        if (!string.IsNullOrEmpty(status))
            command.Parameters.AddWithValue("@Status", status);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            orders.Add(MapOrderFromReader(reader));
        }

        return orders;
    }

    /// <summary>
    /// Gets a single order by ID, ensuring it belongs to the given handlowiec.
    /// </summary>
    public async Task<OrderDto?> GetOrderByIdAsync(int id, string handlowiec)
    {
        await using var connection = new SqlConnection(_libraNetConnection);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                z.Id, z.KlientId, z.Odbiorca, z.Handlowiec,
                z.IloscZamowiona, z.IloscFaktyczna, z.Roznica,
                z.Pojemniki, z.Palety, z.TrybE2,
                z.DataPrzyjecia, z.GodzinaPrzyjecia, z.TerminOdbioru, z.DataUboju,
                z.UtworzonePrzez, z.Status,
                z.MaNotatke, z.MaFolie, z.MaHallal, z.CzyMaCeny, z.SredniaCena,
                z.Uwagi, z.TransportKursId, z.CzyZrealizowane, z.DataWydania, z.Waluta
            FROM dbo.ZamowieniaMieso z
            WHERE z.Id = @Id AND z.Handlowiec = @Handlowiec";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        var order = MapOrderFromReader(reader);
        await reader.CloseAsync();

        // Load order items
        order.Pozycje = await GetOrderItemsInternalAsync(connection, id);

        return order;
    }

    /// <summary>
    /// Gets order items for a given order.
    /// </summary>
    public async Task<List<OrderItemDto>> GetOrderItemsAsync(int orderId, string handlowiec)
    {
        await using var connection = new SqlConnection(_libraNetConnection);
        await connection.OpenAsync();

        // Verify order belongs to handlowiec
        const string verifySql = @"
            SELECT COUNT(1) FROM dbo.ZamowieniaMieso
            WHERE Id = @Id AND Handlowiec = @Handlowiec";

        await using var verifyCmd = new SqlCommand(verifySql, connection);
        verifyCmd.Parameters.AddWithValue("@Id", orderId);
        verifyCmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

        var count = (int)(await verifyCmd.ExecuteScalarAsync() ?? 0);
        if (count == 0)
            return new List<OrderItemDto>();

        return await GetOrderItemsInternalAsync(connection, orderId);
    }

    /// <summary>
    /// Creates a new order with items.
    /// </summary>
    public async Task<OrderDto?> CreateOrderAsync(OrderCreateDto dto, string createdBy)
    {
        await using var connection = new SqlConnection(_libraNetConnection);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            // Calculate totals from items
            var totalIlosc = dto.Pozycje.Sum(p => p.Ilosc);
            var totalPojemniki = dto.Pozycje.Sum(p => p.Pojemniki);
            var totalPalety = dto.Pozycje.Sum(p => p.Palety);
            var hasCeny = dto.Pozycje.All(p => p.Cena > 0);
            var sredniaCena = dto.Pozycje.Count > 0 && totalIlosc > 0
                ? dto.Pozycje.Sum(p => p.Cena * p.Ilosc) / totalIlosc
                : 0m;
            var maFolie = dto.MaFolie || dto.Pozycje.Any(p => p.Folia);
            var maHallal = dto.MaHallal || dto.Pozycje.Any(p => p.Hallal);

            const string insertOrderSql = @"
                INSERT INTO dbo.ZamowieniaMieso (
                    KlientId, Odbiorca, Handlowiec, IloscZamowiona, IloscFaktyczna, Roznica,
                    Pojemniki, Palety, TrybE2, DataPrzyjecia, GodzinaPrzyjecia,
                    TerminOdbioru, DataUboju, UtworzonePrzez, Status,
                    MaNotatke, MaFolie, MaHallal, CzyMaCeny, SredniaCena,
                    Uwagi, TransportKursId, CzyZrealizowane, Waluta
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @KlientId, @Odbiorca, @Handlowiec, @IloscZamowiona, 0, @Roznica,
                    @Pojemniki, @Palety, @TrybE2, @DataPrzyjecia, @GodzinaPrzyjecia,
                    @TerminOdbioru, @DataUboju, @UtworzonePrzez, @Status,
                    @MaNotatke, @MaFolie, @MaHallal, @CzyMaCeny, @SredniaCena,
                    @Uwagi, @TransportKursId, 0, @Waluta
                )";

            await using var insertCmd = new SqlCommand(insertOrderSql, connection, transaction);
            insertCmd.Parameters.AddWithValue("@KlientId", dto.KlientId);
            insertCmd.Parameters.AddWithValue("@Odbiorca", dto.Odbiorca);
            insertCmd.Parameters.AddWithValue("@Handlowiec", dto.Handlowiec);
            insertCmd.Parameters.AddWithValue("@IloscZamowiona", totalIlosc);
            insertCmd.Parameters.AddWithValue("@Roznica", -totalIlosc); // Roznica = Faktyczna - Zamowiona
            insertCmd.Parameters.AddWithValue("@Pojemniki", totalPojemniki);
            insertCmd.Parameters.AddWithValue("@Palety", totalPalety);
            insertCmd.Parameters.AddWithValue("@TrybE2", dto.TrybE2);
            insertCmd.Parameters.AddWithValue("@DataPrzyjecia",
                (object?)dto.DataPrzyjecia ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@GodzinaPrzyjecia",
                (object?)dto.GodzinaPrzyjecia ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@TerminOdbioru",
                (object?)dto.TerminOdbioru ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@DataUboju",
                (object?)dto.DataUboju ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@UtworzonePrzez", createdBy);
            insertCmd.Parameters.AddWithValue("@Status", "Nowe");
            insertCmd.Parameters.AddWithValue("@MaNotatke",
                !string.IsNullOrWhiteSpace(dto.Uwagi));
            insertCmd.Parameters.AddWithValue("@MaFolie", maFolie);
            insertCmd.Parameters.AddWithValue("@MaHallal", maHallal);
            insertCmd.Parameters.AddWithValue("@CzyMaCeny", hasCeny);
            insertCmd.Parameters.AddWithValue("@SredniaCena", sredniaCena);
            insertCmd.Parameters.AddWithValue("@Uwagi",
                (object?)dto.Uwagi ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@TransportKursId",
                (object?)dto.TransportKursId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Waluta",
                (object?)dto.Waluta ?? DBNull.Value);

            var newId = (int)(await insertCmd.ExecuteScalarAsync())!;

            // Insert order items
            foreach (var item in dto.Pozycje)
            {
                const string insertItemSql = @"
                    INSERT INTO dbo.ZamowieniaMiesoTowar (
                        ZamowienieId, KodTowaru, NazwaTowaru, Ilosc, Cena,
                        Pojemniki, Palety, E2, Folia, Hallal, Wydano
                    )
                    VALUES (
                        @ZamowienieId, @KodTowaru, @NazwaTowaru, @Ilosc, @Cena,
                        @Pojemniki, @Palety, @E2, @Folia, @Hallal, 0
                    )";

                await using var itemCmd = new SqlCommand(insertItemSql, connection, transaction);
                itemCmd.Parameters.AddWithValue("@ZamowienieId", newId);
                itemCmd.Parameters.AddWithValue("@KodTowaru", item.KodTowaru);
                itemCmd.Parameters.AddWithValue("@NazwaTowaru", item.NazwaTowaru);
                itemCmd.Parameters.AddWithValue("@Ilosc", item.Ilosc);
                itemCmd.Parameters.AddWithValue("@Cena", item.Cena);
                itemCmd.Parameters.AddWithValue("@Pojemniki", item.Pojemniki);
                itemCmd.Parameters.AddWithValue("@Palety", item.Palety);
                itemCmd.Parameters.AddWithValue("@E2", item.E2);
                itemCmd.Parameters.AddWithValue("@Folia", item.Folia);
                itemCmd.Parameters.AddWithValue("@Hallal", item.Hallal);

                await itemCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Order {OrderId} created by {User} for handlowiec {Handlowiec}.",
                newId, createdBy, dto.Handlowiec);

            // Return the created order
            return await GetOrderByIdAsync(newId, dto.Handlowiec);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Updates an existing order (only if status is 'Nowe').
    /// </summary>
    public async Task<bool> UpdateOrderAsync(int id, OrderCreateDto dto, string handlowiec)
    {
        await using var connection = new SqlConnection(_libraNetConnection);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            // Verify ownership and status
            const string verifySql = @"
                SELECT Status FROM dbo.ZamowieniaMieso
                WHERE Id = @Id AND Handlowiec = @Handlowiec";

            await using var verifyCmd = new SqlCommand(verifySql, connection, transaction);
            verifyCmd.Parameters.AddWithValue("@Id", id);
            verifyCmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

            var currentStatus = (string?)await verifyCmd.ExecuteScalarAsync();
            if (currentStatus == null)
                return false;

            if (currentStatus != "Nowe")
            {
                _logger.LogWarning(
                    "Cannot update order {OrderId}: status is '{Status}', expected 'Nowe'.",
                    id, currentStatus);
                return false;
            }

            // Recalculate totals
            var totalIlosc = dto.Pozycje.Sum(p => p.Ilosc);
            var totalPojemniki = dto.Pozycje.Sum(p => p.Pojemniki);
            var totalPalety = dto.Pozycje.Sum(p => p.Palety);
            var hasCeny = dto.Pozycje.All(p => p.Cena > 0);
            var sredniaCena = dto.Pozycje.Count > 0 && totalIlosc > 0
                ? dto.Pozycje.Sum(p => p.Cena * p.Ilosc) / totalIlosc
                : 0m;
            var maFolie = dto.MaFolie || dto.Pozycje.Any(p => p.Folia);
            var maHallal = dto.MaHallal || dto.Pozycje.Any(p => p.Hallal);

            const string updateSql = @"
                UPDATE dbo.ZamowieniaMieso SET
                    KlientId = @KlientId,
                    Odbiorca = @Odbiorca,
                    IloscZamowiona = @IloscZamowiona,
                    Roznica = IloscFaktyczna - @IloscZamowiona,
                    Pojemniki = @Pojemniki,
                    Palety = @Palety,
                    TrybE2 = @TrybE2,
                    DataPrzyjecia = @DataPrzyjecia,
                    GodzinaPrzyjecia = @GodzinaPrzyjecia,
                    TerminOdbioru = @TerminOdbioru,
                    DataUboju = @DataUboju,
                    MaNotatke = @MaNotatke,
                    MaFolie = @MaFolie,
                    MaHallal = @MaHallal,
                    CzyMaCeny = @CzyMaCeny,
                    SredniaCena = @SredniaCena,
                    Uwagi = @Uwagi,
                    TransportKursId = @TransportKursId,
                    Waluta = @Waluta
                WHERE Id = @Id AND Handlowiec = @Handlowiec";

            await using var updateCmd = new SqlCommand(updateSql, connection, transaction);
            updateCmd.Parameters.AddWithValue("@Id", id);
            updateCmd.Parameters.AddWithValue("@Handlowiec", handlowiec);
            updateCmd.Parameters.AddWithValue("@KlientId", dto.KlientId);
            updateCmd.Parameters.AddWithValue("@Odbiorca", dto.Odbiorca);
            updateCmd.Parameters.AddWithValue("@IloscZamowiona", totalIlosc);
            updateCmd.Parameters.AddWithValue("@Pojemniki", totalPojemniki);
            updateCmd.Parameters.AddWithValue("@Palety", totalPalety);
            updateCmd.Parameters.AddWithValue("@TrybE2", dto.TrybE2);
            updateCmd.Parameters.AddWithValue("@DataPrzyjecia",
                (object?)dto.DataPrzyjecia ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@GodzinaPrzyjecia",
                (object?)dto.GodzinaPrzyjecia ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@TerminOdbioru",
                (object?)dto.TerminOdbioru ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@DataUboju",
                (object?)dto.DataUboju ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@MaNotatke",
                !string.IsNullOrWhiteSpace(dto.Uwagi));
            updateCmd.Parameters.AddWithValue("@MaFolie", maFolie);
            updateCmd.Parameters.AddWithValue("@MaHallal", maHallal);
            updateCmd.Parameters.AddWithValue("@CzyMaCeny", hasCeny);
            updateCmd.Parameters.AddWithValue("@SredniaCena", sredniaCena);
            updateCmd.Parameters.AddWithValue("@Uwagi",
                (object?)dto.Uwagi ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@TransportKursId",
                (object?)dto.TransportKursId ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@Waluta",
                (object?)dto.Waluta ?? DBNull.Value);

            await updateCmd.ExecuteNonQueryAsync();

            // Delete existing items and re-insert
            const string deleteItemsSql = @"
                DELETE FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @ZamowienieId";

            await using var deleteCmd = new SqlCommand(deleteItemsSql, connection, transaction);
            deleteCmd.Parameters.AddWithValue("@ZamowienieId", id);
            await deleteCmd.ExecuteNonQueryAsync();

            foreach (var item in dto.Pozycje)
            {
                const string insertItemSql = @"
                    INSERT INTO dbo.ZamowieniaMiesoTowar (
                        ZamowienieId, KodTowaru, NazwaTowaru, Ilosc, Cena,
                        Pojemniki, Palety, E2, Folia, Hallal, Wydano
                    )
                    VALUES (
                        @ZamowienieId, @KodTowaru, @NazwaTowaru, @Ilosc, @Cena,
                        @Pojemniki, @Palety, @E2, @Folia, @Hallal, 0
                    )";

                await using var itemCmd = new SqlCommand(insertItemSql, connection, transaction);
                itemCmd.Parameters.AddWithValue("@ZamowienieId", id);
                itemCmd.Parameters.AddWithValue("@KodTowaru", item.KodTowaru);
                itemCmd.Parameters.AddWithValue("@NazwaTowaru", item.NazwaTowaru);
                itemCmd.Parameters.AddWithValue("@Ilosc", item.Ilosc);
                itemCmd.Parameters.AddWithValue("@Cena", item.Cena);
                itemCmd.Parameters.AddWithValue("@Pojemniki", item.Pojemniki);
                itemCmd.Parameters.AddWithValue("@Palety", item.Palety);
                itemCmd.Parameters.AddWithValue("@E2", item.E2);
                itemCmd.Parameters.AddWithValue("@Folia", item.Folia);
                itemCmd.Parameters.AddWithValue("@Hallal", item.Hallal);

                await itemCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Order {OrderId} updated by handlowiec {Handlowiec}.", id, handlowiec);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Cancels an order by setting status to 'Anulowane'.
    /// </summary>
    public async Task<bool> CancelOrderAsync(int id, string handlowiec)
    {
        await using var connection = new SqlConnection(_libraNetConnection);
        await connection.OpenAsync();

        const string sql = @"
            UPDATE dbo.ZamowieniaMieso
            SET Status = 'Anulowane'
            WHERE Id = @Id AND Handlowiec = @Handlowiec AND Status = 'Nowe'";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);

        var rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Order {OrderId} cancelled by handlowiec {Handlowiec}.", id, handlowiec);
        }

        return rowsAffected > 0;
    }

    // ---------- Private helpers ----------

    private async Task<List<OrderItemDto>> GetOrderItemsInternalAsync(SqlConnection connection, int orderId)
    {
        var items = new List<OrderItemDto>();

        const string sql = @"
            SELECT
                t.ZamowienieId,
                t.KodTowaru,
                t.NazwaTowaru,
                t.Ilosc,
                t.Cena,
                t.Pojemniki,
                t.Palety,
                t.E2,
                t.Folia,
                t.Hallal,
                t.Wydano
            FROM dbo.ZamowieniaMiesoTowar t
            WHERE t.ZamowienieId = @ZamowienieId
            ORDER BY t.KodTowaru";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ZamowienieId", orderId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new OrderItemDto
            {
                ZamowienieId = reader.GetInt32(reader.GetOrdinal("ZamowienieId")),
                KodTowaru = reader.GetString(reader.GetOrdinal("KodTowaru")),
                NazwaTowaru = reader.GetString(reader.GetOrdinal("NazwaTowaru")),
                Ilosc = reader.GetDecimal(reader.GetOrdinal("Ilosc")),
                Cena = reader.GetDecimal(reader.GetOrdinal("Cena")),
                Pojemniki = reader.GetInt32(reader.GetOrdinal("Pojemniki")),
                Palety = reader.GetInt32(reader.GetOrdinal("Palety")),
                E2 = reader.GetBoolean(reader.GetOrdinal("E2")),
                Folia = reader.GetBoolean(reader.GetOrdinal("Folia")),
                Hallal = reader.GetBoolean(reader.GetOrdinal("Hallal")),
                Wydano = reader.GetDecimal(reader.GetOrdinal("Wydano"))
            });
        }

        return items;
    }

    private static OrderDto MapOrderFromReader(SqlDataReader reader)
    {
        return new OrderDto
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            KlientId = reader.GetInt32(reader.GetOrdinal("KlientId")),
            Odbiorca = reader.GetString(reader.GetOrdinal("Odbiorca")),
            Handlowiec = reader.GetString(reader.GetOrdinal("Handlowiec")),
            IloscZamowiona = reader.GetDecimal(reader.GetOrdinal("IloscZamowiona")),
            IloscFaktyczna = reader.GetDecimal(reader.GetOrdinal("IloscFaktyczna")),
            Roznica = reader.GetDecimal(reader.GetOrdinal("Roznica")),
            Pojemniki = reader.GetInt32(reader.GetOrdinal("Pojemniki")),
            Palety = reader.GetInt32(reader.GetOrdinal("Palety")),
            TrybE2 = reader.GetBoolean(reader.GetOrdinal("TrybE2")),
            DataPrzyjecia = reader.IsDBNull(reader.GetOrdinal("DataPrzyjecia"))
                ? null : reader.GetDateTime(reader.GetOrdinal("DataPrzyjecia")),
            GodzinaPrzyjecia = reader.IsDBNull(reader.GetOrdinal("GodzinaPrzyjecia"))
                ? null : reader.GetString(reader.GetOrdinal("GodzinaPrzyjecia")),
            TerminOdbioru = reader.IsDBNull(reader.GetOrdinal("TerminOdbioru"))
                ? null : reader.GetDateTime(reader.GetOrdinal("TerminOdbioru")),
            DataUboju = reader.IsDBNull(reader.GetOrdinal("DataUboju"))
                ? null : reader.GetDateTime(reader.GetOrdinal("DataUboju")),
            UtworzonePrzez = reader.IsDBNull(reader.GetOrdinal("UtworzonePrzez"))
                ? null : reader.GetString(reader.GetOrdinal("UtworzonePrzez")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            MaNotatke = reader.GetBoolean(reader.GetOrdinal("MaNotatke")),
            MaFolie = reader.GetBoolean(reader.GetOrdinal("MaFolie")),
            MaHallal = reader.GetBoolean(reader.GetOrdinal("MaHallal")),
            CzyMaCeny = reader.GetBoolean(reader.GetOrdinal("CzyMaCeny")),
            SredniaCena = reader.GetDecimal(reader.GetOrdinal("SredniaCena")),
            Uwagi = reader.IsDBNull(reader.GetOrdinal("Uwagi"))
                ? null : reader.GetString(reader.GetOrdinal("Uwagi")),
            TransportKursId = reader.IsDBNull(reader.GetOrdinal("TransportKursId"))
                ? null : reader.GetInt32(reader.GetOrdinal("TransportKursId")),
            CzyZrealizowane = reader.GetBoolean(reader.GetOrdinal("CzyZrealizowane")),
            DataWydania = reader.IsDBNull(reader.GetOrdinal("DataWydania"))
                ? null : reader.GetDateTime(reader.GetOrdinal("DataWydania")),
            Waluta = reader.IsDBNull(reader.GetOrdinal("Waluta"))
                ? null : reader.GetString(reader.GetOrdinal("Waluta"))
        };
    }
}
