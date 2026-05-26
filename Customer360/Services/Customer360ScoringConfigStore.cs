using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Trwałe przechowywanie konfiguracji scoringu w tabeli LibraNet (wspólnej dla wszystkich).
    /// Tabela tworzona automatycznie. Fallback na domyślne gdy brak/błąd.
    /// </summary>
    public static class Customer360ScoringConfigStore
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Cache w pamięci — config zmienia się rzadko
        private static Customer360ScoringConfig? _cache;
        private static DateTime _cacheAt = DateTime.MinValue;

        private const string SqlEnsure = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Customer360_ScoreConfig')
            CREATE TABLE dbo.Customer360_ScoreConfig (
                Id INT NOT NULL PRIMARY KEY,
                ConfigJson NVARCHAR(MAX) NOT NULL,
                UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                UpdatedBy NVARCHAR(100) NULL
            );";

        public static async Task<Customer360ScoringConfig> WczytajAsync(bool force = false)
        {
            if (!force && _cache != null && (DateTime.Now - _cacheAt).TotalMinutes < 10)
                return _cache;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using (var ens = new SqlCommand(SqlEnsure, cn)) await ens.ExecuteNonQueryAsync();
                await using var cmd = new SqlCommand("SELECT TOP 1 ConfigJson FROM dbo.Customer360_ScoreConfig WHERE Id=1", cn) { CommandTimeout = 8 };
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value)
                {
                    var cfg = JsonConvert.DeserializeObject<Customer360ScoringConfig>(r.ToString()!);
                    if (cfg != null) { _cache = cfg; _cacheAt = DateTime.Now; return cfg; }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 cfg load] " + ex.Message); }

            // Brak wiersza lub błąd → domyślne
            _cache = new Customer360ScoringConfig();
            _cacheAt = DateTime.Now;
            return _cache;
        }

        public static async Task<bool> ZapiszAsync(Customer360ScoringConfig cfg, string uzytkownik)
        {
            try
            {
                string json = JsonConvert.SerializeObject(cfg);
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using (var ens = new SqlCommand(SqlEnsure, cn)) await ens.ExecuteNonQueryAsync();
                const string upsert = @"
                    IF EXISTS (SELECT 1 FROM dbo.Customer360_ScoreConfig WHERE Id=1)
                        UPDATE dbo.Customer360_ScoreConfig SET ConfigJson=@j, UpdatedAt=GETDATE(), UpdatedBy=@u WHERE Id=1;
                    ELSE
                        INSERT INTO dbo.Customer360_ScoreConfig (Id, ConfigJson, UpdatedAt, UpdatedBy) VALUES (1, @j, GETDATE(), @u);";
                await using var cmd = new SqlCommand(upsert, cn) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@j", json);
                cmd.Parameters.AddWithValue("@u", (object?)uzytkownik ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                _cache = cfg; _cacheAt = DateTime.Now;
                return true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 cfg save] " + ex.Message); return false; }
        }

        /// <summary>Wyczyść cache — by następny odczyt wziął świeże z bazy.</summary>
        public static void InvalidateCache() { _cache = null; }
    }
}
