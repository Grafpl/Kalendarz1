using Microsoft.Data.SqlClient;
using MobileAPI.DTOs;

namespace MobileAPI.Services;

public class CustomerService
{
    private readonly string _connectionString;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(IConfiguration configuration, ILogger<CustomerService> logger)
    {
        _connectionString = configuration.GetConnectionString("Handel")
            ?? throw new InvalidOperationException("Handel connection string is missing.");
        _logger = logger;
    }

    /// <summary>
    /// Gets all active customers for a given handlowiec.
    /// </summary>
    public async Task<List<CustomerDto>> GetCustomersByHandlowiecAsync(string handlowiec)
    {
        var customers = new List<CustomerDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                c.Id,
                c.Shortcut,
                c.Name1 AS Nazwa,
                cc.Value AS Handlowiec,
                c.NIP,
                a.Street AS Adres,
                a.City AS Miasto,
                a.ZipCode AS KodPocztowy,
                a.Phone AS Telefon,
                a.Email,
                ISNULL(c.PaymentDeadline, 0) AS TerminPlatnosci,
                ISNULL(c.CreditLimit, 0) AS LimitKredytowy,
                ISNULL(c.Balance, 0) AS SaldoNaleznosci,
                c.Active AS Aktywny
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] cc
                ON c.Id = cc.ContractorId AND cc.ClassificationTypeId = 1
            LEFT JOIN [HANDEL].[SSCommon].[STAddresses] a
                ON c.Id = a.ContractorId AND a.IsDefault = 1
            WHERE cc.Value = @Handlowiec
                AND c.Active = 1
            ORDER BY c.Shortcut";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            customers.Add(MapCustomerFromReader(reader));
        }

        return customers;
    }

    /// <summary>
    /// Gets a single customer by ID.
    /// </summary>
    public async Task<CustomerDto?> GetCustomerByIdAsync(int id, string handlowiec)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                c.Id,
                c.Shortcut,
                c.Name1 AS Nazwa,
                cc.Value AS Handlowiec,
                c.NIP,
                a.Street AS Adres,
                a.City AS Miasto,
                a.ZipCode AS KodPocztowy,
                a.Phone AS Telefon,
                a.Email,
                ISNULL(c.PaymentDeadline, 0) AS TerminPlatnosci,
                ISNULL(c.CreditLimit, 0) AS LimitKredytowy,
                ISNULL(c.Balance, 0) AS SaldoNaleznosci,
                c.Active AS Aktywny
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] cc
                ON c.Id = cc.ContractorId AND cc.ClassificationTypeId = 1
            LEFT JOIN [HANDEL].[SSCommon].[STAddresses] a
                ON c.Id = a.ContractorId AND a.IsDefault = 1
            WHERE c.Id = @Id AND cc.Value = @Handlowiec";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapCustomerFromReader(reader);
    }

    /// <summary>
    /// Searches customers by name or shortcut.
    /// </summary>
    public async Task<List<CustomerDto>> SearchCustomersAsync(string handlowiec, string searchTerm)
    {
        var customers = new List<CustomerDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                c.Id,
                c.Shortcut,
                c.Name1 AS Nazwa,
                cc.Value AS Handlowiec,
                c.NIP,
                a.Street AS Adres,
                a.City AS Miasto,
                a.ZipCode AS KodPocztowy,
                a.Phone AS Telefon,
                a.Email,
                ISNULL(c.PaymentDeadline, 0) AS TerminPlatnosci,
                ISNULL(c.CreditLimit, 0) AS LimitKredytowy,
                ISNULL(c.Balance, 0) AS SaldoNaleznosci,
                c.Active AS Aktywny
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] cc
                ON c.Id = cc.ContractorId AND cc.ClassificationTypeId = 1
            LEFT JOIN [HANDEL].[SSCommon].[STAddresses] a
                ON c.Id = a.ContractorId AND a.IsDefault = 1
            WHERE cc.Value = @Handlowiec
                AND c.Active = 1
                AND (c.Shortcut LIKE @Search OR c.Name1 LIKE @Search)
            ORDER BY c.Shortcut";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);
        command.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            customers.Add(MapCustomerFromReader(reader));
        }

        return customers;
    }

    private static CustomerDto MapCustomerFromReader(SqlDataReader reader)
    {
        return new CustomerDto
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Shortcut = reader.GetString(reader.GetOrdinal("Shortcut")),
            Nazwa = reader.IsDBNull(reader.GetOrdinal("Nazwa"))
                ? string.Empty : reader.GetString(reader.GetOrdinal("Nazwa")),
            Handlowiec = reader.IsDBNull(reader.GetOrdinal("Handlowiec"))
                ? null : reader.GetString(reader.GetOrdinal("Handlowiec")),
            NIP = reader.IsDBNull(reader.GetOrdinal("NIP"))
                ? null : reader.GetString(reader.GetOrdinal("NIP")),
            Adres = reader.IsDBNull(reader.GetOrdinal("Adres"))
                ? null : reader.GetString(reader.GetOrdinal("Adres")),
            Miasto = reader.IsDBNull(reader.GetOrdinal("Miasto"))
                ? null : reader.GetString(reader.GetOrdinal("Miasto")),
            KodPocztowy = reader.IsDBNull(reader.GetOrdinal("KodPocztowy"))
                ? null : reader.GetString(reader.GetOrdinal("KodPocztowy")),
            Telefon = reader.IsDBNull(reader.GetOrdinal("Telefon"))
                ? null : reader.GetString(reader.GetOrdinal("Telefon")),
            Email = reader.IsDBNull(reader.GetOrdinal("Email"))
                ? null : reader.GetString(reader.GetOrdinal("Email")),
            TerminPlatnosci = reader.GetInt32(reader.GetOrdinal("TerminPlatnosci")),
            LimitKredytowy = reader.GetDecimal(reader.GetOrdinal("LimitKredytowy")),
            SaldoNaleznosci = reader.GetDecimal(reader.GetOrdinal("SaldoNaleznosci")),
            Aktywny = reader.GetBoolean(reader.GetOrdinal("Aktywny"))
        };
    }
}
