using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Zarządza tabelą intel_UserQueries — własne, edytowalne zapytania użytkownika
    /// dla Perplexity. Doklejane do hardcoded GetPolishQueries().
    /// </summary>
    public class UserQueriesService
    {
        private readonly string _connectionString;
        private bool _tableEnsured;

        public UserQueriesService(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        public async Task EnsureTableAsync()
        {
            if (_tableEnsured) return;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_UserQueries')
BEGIN
    CREATE TABLE intel_UserQueries (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        QueryText NVARCHAR(1000) NOT NULL,
        Category NVARCHAR(50) DEFAULT 'Custom',
        Priority INT DEFAULT 5,           -- 1=krytyczne, 5=normalne, 9=opcjonalne
        Enabled BIT NOT NULL DEFAULT 1,
        RecencyFilter NVARCHAR(20) DEFAULT 'week', -- day/week/month
        LastUsedAt DATETIME,
        TimesUsed INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        Notes NVARCHAR(500)
    );
END", conn);
                await cmd.ExecuteNonQueryAsync();
                _tableEnsured = true;
                Debug.WriteLine("[UserQueries] Tabela intel_UserQueries zweryfikowana");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserQueries] Ensure error: {ex.Message}");
            }
        }

        public async Task<List<UserQuery>> GetAllAsync(bool onlyEnabled = false)
        {
            await EnsureTableAsync();
            var list = new List<UserQuery>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var sql = @"SELECT Id, QueryText, Category, Priority, Enabled, RecencyFilter,
                                   LastUsedAt, TimesUsed, CreatedAt, Notes
                            FROM intel_UserQueries " +
                          (onlyEnabled ? "WHERE Enabled = 1 " : "") +
                          "ORDER BY Priority, CreatedAt";
                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new UserQuery
                    {
                        Id = reader.GetInt32(0),
                        QueryText = reader.GetString(1),
                        Category = reader.IsDBNull(2) ? "Custom" : reader.GetString(2),
                        Priority = reader.GetInt32(3),
                        Enabled = reader.GetBoolean(4),
                        RecencyFilter = reader.IsDBNull(5) ? "week" : reader.GetString(5),
                        LastUsedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                        TimesUsed = reader.GetInt32(7),
                        CreatedAt = reader.GetDateTime(8),
                        Notes = reader.IsDBNull(9) ? "" : reader.GetString(9)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserQueries] GetAll error: {ex.Message}");
            }
            return list;
        }

        public async Task<int> InsertAsync(UserQuery q)
        {
            await EnsureTableAsync();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
INSERT INTO intel_UserQueries (QueryText, Category, Priority, Enabled, RecencyFilter, Notes)
OUTPUT INSERTED.Id
VALUES (@q, @cat, @pri, @en, @rec, @notes)", conn);
                cmd.Parameters.AddWithValue("@q", q.QueryText ?? "");
                cmd.Parameters.AddWithValue("@cat", (object)q.Category ?? "Custom");
                cmd.Parameters.AddWithValue("@pri", q.Priority);
                cmd.Parameters.AddWithValue("@en", q.Enabled);
                cmd.Parameters.AddWithValue("@rec", (object)q.RecencyFilter ?? "week");
                cmd.Parameters.AddWithValue("@notes", (object)q.Notes ?? "");
                var id = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                Debug.WriteLine($"[UserQueries] Dodano zapytanie #{id}: {q.QueryText?.Substring(0, Math.Min(50, q.QueryText.Length))}...");
                return id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserQueries] Insert error: {ex.Message}");
                return 0;
            }
        }

        public async Task UpdateAsync(UserQuery q)
        {
            await EnsureTableAsync();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
UPDATE intel_UserQueries
SET QueryText=@q, Category=@cat, Priority=@pri, Enabled=@en, RecencyFilter=@rec, Notes=@notes
WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@id", q.Id);
                cmd.Parameters.AddWithValue("@q", q.QueryText ?? "");
                cmd.Parameters.AddWithValue("@cat", (object)q.Category ?? "Custom");
                cmd.Parameters.AddWithValue("@pri", q.Priority);
                cmd.Parameters.AddWithValue("@en", q.Enabled);
                cmd.Parameters.AddWithValue("@rec", (object)q.RecencyFilter ?? "week");
                cmd.Parameters.AddWithValue("@notes", (object)q.Notes ?? "");
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserQueries] Update error: {ex.Message}");
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("DELETE FROM intel_UserQueries WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[UserQueries] Usunięto zapytanie #{id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserQueries] Delete error: {ex.Message}");
            }
        }

        /// <summary>Aktualizuje statystyki użycia po wykonaniu zapytania.</summary>
        public async Task RecordUsageAsync(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "UPDATE intel_UserQueries SET LastUsedAt=GETDATE(), TimesUsed=TimesUsed+1 WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* non-fatal */ }
        }
    }

    /// <summary>Własne, edytowalne zapytanie użytkownika do Perplexity.</summary>
    public class UserQuery
    {
        public int Id { get; set; }
        public string QueryText { get; set; } = "";
        public string Category { get; set; } = "Custom";
        public int Priority { get; set; } = 5;
        public bool Enabled { get; set; } = true;
        public string RecencyFilter { get; set; } = "week"; // day/week/month
        public DateTime? LastUsedAt { get; set; }
        public int TimesUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; } = "";

        public string LastUsedDisplay => LastUsedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        public string PriorityLabel => Priority switch
        {
            <= 2 => "🔴 Krytyczne",
            <= 4 => "🟠 Wysokie",
            <= 6 => "🟡 Normalne",
            _ => "🟢 Niskie"
        };
    }
}
