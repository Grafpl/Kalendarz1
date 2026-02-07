using Microsoft.Data.SqlClient;
using MobileAPI.DTOs;
using MobileAPI.Models;

namespace MobileAPI.Services;

public class DashboardService
{
    private readonly string _connectionString;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IConfiguration configuration, ILogger<DashboardService> logger)
    {
        _connectionString = configuration.GetConnectionString("LibraNet")
            ?? throw new InvalidOperationException("LibraNet connection string is missing.");
        _logger = logger;
    }

    /// <summary>
    /// Gets dashboard summary for a given handlowiec and date.
    /// </summary>
    public async Task<DashboardDto> GetDashboardAsync(string handlowiec, DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                @Data AS Data,
                ISNULL(SUM(z.IloscZamowiona), 0) AS SumaZamowien,
                COUNT(*) AS LiczbaZamowien,
                COUNT(DISTINCT z.KlientId) AS LiczbaKlientow,
                ISNULL(SUM(z.Palety), 0) AS SumaPalet,
                SUM(CASE WHEN z.Status = 'Anulowane' THEN 1 ELSE 0 END) AS LiczbaAnulowanych
            FROM dbo.ZamowieniaMieso z
            WHERE z.Handlowiec = @Handlowiec
                AND z.DataPrzyjecia = @Data";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);
        command.Parameters.AddWithValue("@Data", targetDate.Date);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new DashboardDto
            {
                Data = targetDate.Date,
                SumaZamowien = reader.GetDecimal(reader.GetOrdinal("SumaZamowien")),
                LiczbaZamowien = reader.GetInt32(reader.GetOrdinal("LiczbaZamowien")),
                LiczbaKlientow = reader.GetInt32(reader.GetOrdinal("LiczbaKlientow")),
                SumaPalet = reader.GetInt32(reader.GetOrdinal("SumaPalet")),
                LiczbaAnulowanych = reader.GetInt32(reader.GetOrdinal("LiczbaAnulowanych"))
            };
        }

        return new DashboardDto
        {
            Data = targetDate.Date,
            SumaZamowien = 0,
            LiczbaZamowien = 0,
            LiczbaKlientow = 0,
            SumaPalet = 0,
            LiczbaAnulowanych = 0
        };
    }

    /// <summary>
    /// Gets dashboard summary for a date range (daily breakdown).
    /// </summary>
    public async Task<List<DashboardDto>> GetDashboardRangeAsync(
        string handlowiec, DateTime dateFrom, DateTime dateTo)
    {
        var results = new List<DashboardDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                z.DataPrzyjecia AS Data,
                ISNULL(SUM(z.IloscZamowiona), 0) AS SumaZamowien,
                COUNT(*) AS LiczbaZamowien,
                COUNT(DISTINCT z.KlientId) AS LiczbaKlientow,
                ISNULL(SUM(z.Palety), 0) AS SumaPalet,
                SUM(CASE WHEN z.Status = 'Anulowane' THEN 1 ELSE 0 END) AS LiczbaAnulowanych
            FROM dbo.ZamowieniaMieso z
            WHERE z.Handlowiec = @Handlowiec
                AND z.DataPrzyjecia >= @DateFrom
                AND z.DataPrzyjecia <= @DateTo
            GROUP BY z.DataPrzyjecia
            ORDER BY z.DataPrzyjecia DESC";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Handlowiec", handlowiec);
        command.Parameters.AddWithValue("@DateFrom", dateFrom.Date);
        command.Parameters.AddWithValue("@DateTo", dateTo.Date);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new DashboardDto
            {
                Data = reader.GetDateTime(reader.GetOrdinal("Data")),
                SumaZamowien = reader.GetDecimal(reader.GetOrdinal("SumaZamowien")),
                LiczbaZamowien = reader.GetInt32(reader.GetOrdinal("LiczbaZamowien")),
                LiczbaKlientow = reader.GetInt32(reader.GetOrdinal("LiczbaKlientow")),
                SumaPalet = reader.GetInt32(reader.GetOrdinal("SumaPalet")),
                LiczbaAnulowanych = reader.GetInt32(reader.GetOrdinal("LiczbaAnulowanych"))
            });
        }

        return results;
    }
}
