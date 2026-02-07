using Microsoft.Data.SqlClient;
using MobileAPI.DTOs;

namespace MobileAPI.Services;

public class ProductService
{
    private readonly string _connectionString;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IConfiguration configuration, ILogger<ProductService> logger)
    {
        _connectionString = configuration.GetConnectionString("Handel")
            ?? throw new InvalidOperationException("Handel connection string is missing.");
        _logger = logger;
    }

    /// <summary>
    /// Gets all active products from the poultry catalog (Kurczak A + Kurczak B).
    /// Katalog 67095 = Kurczak A, Katalog 67153 = Kurczak B
    /// </summary>
    public async Task<List<ProductDto>> GetProductsAsync()
    {
        var products = new List<ProductDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                tw.Id,
                tw.Code AS Kod,
                tw.Name AS Nazwa,
                CASE tw.Katalog
                    WHEN 67095 THEN 'Kurczak A'
                    WHEN 67153 THEN 'Kurczak B'
                    ELSE 'Inny'
                END AS Katalog,
                tw.UnitOfMeasure AS JM,
                ISNULL(tw.Price, 0) AS Cena,
                tw.Active AS Aktywny
            FROM [HANDEL].[SSC].[TW] tw
            WHERE tw.Katalog IN (67095, 67153)
                AND tw.Active = 1
            ORDER BY tw.Katalog, tw.Code";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            products.Add(new ProductDto
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Kod = reader.GetString(reader.GetOrdinal("Kod")),
                Nazwa = reader.GetString(reader.GetOrdinal("Nazwa")),
                Katalog = reader.GetString(reader.GetOrdinal("Katalog")),
                JM = reader.IsDBNull(reader.GetOrdinal("JM"))
                    ? "kg" : reader.GetString(reader.GetOrdinal("JM")),
                Cena = reader.GetDecimal(reader.GetOrdinal("Cena")),
                Aktywny = reader.GetBoolean(reader.GetOrdinal("Aktywny"))
            });
        }

        return products;
    }

    /// <summary>
    /// Gets a single product by its code.
    /// </summary>
    public async Task<ProductDto?> GetProductByCodeAsync(string kod)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                tw.Id,
                tw.Code AS Kod,
                tw.Name AS Nazwa,
                CASE tw.Katalog
                    WHEN 67095 THEN 'Kurczak A'
                    WHEN 67153 THEN 'Kurczak B'
                    ELSE 'Inny'
                END AS Katalog,
                tw.UnitOfMeasure AS JM,
                ISNULL(tw.Price, 0) AS Cena,
                tw.Active AS Aktywny
            FROM [HANDEL].[SSC].[TW] tw
            WHERE tw.Code = @Kod
                AND tw.Katalog IN (67095, 67153)";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Kod", kod);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new ProductDto
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Kod = reader.GetString(reader.GetOrdinal("Kod")),
            Nazwa = reader.GetString(reader.GetOrdinal("Nazwa")),
            Katalog = reader.GetString(reader.GetOrdinal("Katalog")),
            JM = reader.IsDBNull(reader.GetOrdinal("JM"))
                ? "kg" : reader.GetString(reader.GetOrdinal("JM")),
            Cena = reader.GetDecimal(reader.GetOrdinal("Cena")),
            Aktywny = reader.GetBoolean(reader.GetOrdinal("Aktywny"))
        };
    }

    /// <summary>
    /// Searches products by code or name.
    /// </summary>
    public async Task<List<ProductDto>> SearchProductsAsync(string searchTerm)
    {
        var products = new List<ProductDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                tw.Id,
                tw.Code AS Kod,
                tw.Name AS Nazwa,
                CASE tw.Katalog
                    WHEN 67095 THEN 'Kurczak A'
                    WHEN 67153 THEN 'Kurczak B'
                    ELSE 'Inny'
                END AS Katalog,
                tw.UnitOfMeasure AS JM,
                ISNULL(tw.Price, 0) AS Cena,
                tw.Active AS Aktywny
            FROM [HANDEL].[SSC].[TW] tw
            WHERE tw.Katalog IN (67095, 67153)
                AND tw.Active = 1
                AND (tw.Code LIKE @Search OR tw.Name LIKE @Search)
            ORDER BY tw.Katalog, tw.Code";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            products.Add(new ProductDto
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Kod = reader.GetString(reader.GetOrdinal("Kod")),
                Nazwa = reader.GetString(reader.GetOrdinal("Nazwa")),
                Katalog = reader.GetString(reader.GetOrdinal("Katalog")),
                JM = reader.IsDBNull(reader.GetOrdinal("JM"))
                    ? "kg" : reader.GetString(reader.GetOrdinal("JM")),
                Cena = reader.GetDecimal(reader.GetOrdinal("Cena")),
                Aktywny = reader.GetBoolean(reader.GetOrdinal("Aktywny"))
            });
        }

        return products;
    }
}
