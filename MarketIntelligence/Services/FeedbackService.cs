using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Zapis feedbacku 👍/👎 do artykułów (intel_ArticleFeedback).
    /// W przyszłości użyte przez ArticleRanker do podbicia/zaniżenia podobnych artykułów
    /// (po Category + Source + Tags). Per-user (App.UserID).
    /// </summary>
    public class FeedbackService
    {
        private readonly string _connectionString;
        private bool _tableEnsured;

        public FeedbackService(string connectionString = null)
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
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_ArticleFeedback')
BEGIN
    CREATE TABLE intel_ArticleFeedback (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ArticleId INT NOT NULL,
        UserId NVARCHAR(50) NOT NULL,
        Vote INT NOT NULL,                  -- 1 = thumbs up, -1 = thumbs down
        Reason NVARCHAR(200),               -- opcjonalne: dlaczego (label)
        Category NVARCHAR(50),              -- snapshot dla agregacji
        SourceName NVARCHAR(200),           -- snapshot dla agregacji
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_ArticleFeedback_ArticleUser ON intel_ArticleFeedback(ArticleId, UserId);
    CREATE INDEX IX_ArticleFeedback_CategorySource ON intel_ArticleFeedback(Category, SourceName, Vote);
END", conn);
                await cmd.ExecuteNonQueryAsync();
                _tableEnsured = true;
                Debug.WriteLine("[Feedback] Tabela intel_ArticleFeedback zweryfikowana");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Feedback] Ensure error: {ex.Message}");
            }
        }

        /// <summary>Zapisuje feedback. Jeśli już istnieje wpis dla (ArticleId, UserId), aktualizuje go (upsert).</summary>
        public async Task RecordAsync(int articleId, string userId, int vote, string reason = null, string category = null, string sourceName = null)
        {
            if (vote != 1 && vote != -1) return;
            await EnsureTableAsync();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM intel_ArticleFeedback WHERE ArticleId=@aid AND UserId=@uid)
    UPDATE intel_ArticleFeedback
       SET Vote=@v, Reason=@reason, Category=@cat, SourceName=@src, CreatedAt=GETDATE()
     WHERE ArticleId=@aid AND UserId=@uid
ELSE
    INSERT INTO intel_ArticleFeedback (ArticleId, UserId, Vote, Reason, Category, SourceName)
    VALUES (@aid, @uid, @v, @reason, @cat, @src)", conn);
                cmd.Parameters.AddWithValue("@aid", articleId);
                cmd.Parameters.AddWithValue("@uid", userId ?? "unknown");
                cmd.Parameters.AddWithValue("@v", vote);
                cmd.Parameters.AddWithValue("@reason", (object)reason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", (object)category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@src", (object)sourceName ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Feedback] {userId} → artykuł #{articleId}: {(vote > 0 ? "👍" : "👎")} ({reason ?? "-"})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Feedback] Record error: {ex.Message}");
            }
        }

        /// <summary>Pobiera feedback usera dla konkretnego artykułu (0 jeśli brak).</summary>
        public async Task<int> GetVoteAsync(int articleId, string userId)
        {
            await EnsureTableAsync();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 Vote FROM intel_ArticleFeedback WHERE ArticleId=@aid AND UserId=@uid", conn);
                cmd.Parameters.AddWithValue("@aid", articleId);
                cmd.Parameters.AddWithValue("@uid", userId ?? "unknown");
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Agreguje preferencje usera: dla (Category, SourceName) zwraca sumę głosów.
        /// Dodatnia suma → user lubi tę kombinację, ujemna → nie lubi.
        /// Wynik używany przez przyszły ArticleScorer do boost/penalty.
        /// </summary>
        public async Task<List<FeedbackPreference>> GetUserPreferencesAsync(string userId, int sinceDaysAgo = 90)
        {
            await EnsureTableAsync();
            var result = new List<FeedbackPreference>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT Category, SourceName,
       SUM(Vote) AS Net,
       COUNT(*) AS Total
FROM intel_ArticleFeedback
WHERE UserId=@uid
  AND CreatedAt >= DATEADD(day, -@days, GETDATE())
GROUP BY Category, SourceName
ORDER BY ABS(SUM(Vote)) DESC", conn);
                cmd.Parameters.AddWithValue("@uid", userId ?? "unknown");
                cmd.Parameters.AddWithValue("@days", sinceDaysAgo);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    result.Add(new FeedbackPreference
                    {
                        Category = r.IsDBNull(0) ? "" : r.GetString(0),
                        SourceName = r.IsDBNull(1) ? "" : r.GetString(1),
                        NetVote = r.GetInt32(2),
                        TotalVotes = r.GetInt32(3)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Feedback] GetPrefs error: {ex.Message}");
            }
            return result;
        }
    }

    public class FeedbackPreference
    {
        public string Category { get; set; } = "";
        public string SourceName { get; set; } = "";
        public int NetVote { get; set; }     // dodatnia = lubi, ujemna = nie lubi
        public int TotalVotes { get; set; }  // łączna liczba głosów (kalibracja pewności)
    }
}
