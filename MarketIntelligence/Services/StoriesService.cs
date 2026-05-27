using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Faza A: odczyt wątków (intel_Stories) do UI + artykuły źródłowe wątku.
    /// </summary>
    public class StoriesService
    {
        private readonly string _connectionString;

        public StoriesService(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        /// <summary>Wątki posortowane wg ważności (Severity×PoultryRelevance), potem ostatniej aktualizacji.</summary>
        public async Task<List<IntelStory>> GetStoriesAsync(int daysBack = 7, string statusFilter = null, string typeFilter = null)
        {
            var list = new List<IntelStory>();
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();
                var sql = @"
SELECT Id, Title, StoryType, FirstSeenAt, LastUpdatedAt, Status, Severity, PoultryRelevance,
       BusinessImpact, EntitiesJson, LastDigest, LastDigestAt, ArticleCount
FROM intel_Stories
WHERE LastUpdatedAt >= DATEADD(day, -@days, GETDATE())
" + (string.IsNullOrEmpty(statusFilter) ? "" : " AND Status = @status ")
  + (string.IsNullOrEmpty(typeFilter) ? "" : " AND StoryType = @type ") + @"
ORDER BY (Severity * PoultryRelevance) DESC, LastUpdatedAt DESC";
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 20 };
                cmd.Parameters.AddWithValue("@days", daysBack);
                if (!string.IsNullOrEmpty(statusFilter)) cmd.Parameters.AddWithValue("@status", statusFilter);
                if (!string.IsNullOrEmpty(typeFilter)) cmd.Parameters.AddWithValue("@type", typeFilter);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new IntelStory
                    {
                        Id = r.GetInt32(0),
                        Title = r.GetString(1),
                        StoryType = r.IsDBNull(2) ? "other" : r.GetString(2),
                        FirstSeenAt = r.GetDateTime(3),
                        LastUpdatedAt = r.GetDateTime(4),
                        Status = r.IsDBNull(5) ? "developing" : r.GetString(5),
                        Severity = r.GetInt32(6),
                        PoultryRelevance = r.GetInt32(7),
                        BusinessImpact = r.IsDBNull(8) ? null : r.GetString(8),
                        EntitiesJson = r.IsDBNull(9) ? null : r.GetString(9),
                        LastDigest = r.IsDBNull(10) ? null : r.GetString(10),
                        LastDigestAt = r.IsDBNull(11) ? (DateTime?)null : r.GetDateTime(11),
                        ArticleCount = r.GetInt32(12)
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Stories] GetStories error: {ex.Message}"); }
            return list;
        }

        /// <summary>Artykuły źródłowe wątku (do rozwinięcia karty).</summary>
        public async Task<List<StorySourceArticle>> GetStoryArticlesAsync(int storyId)
        {
            var list = new List<StorySourceArticle>();
            try
            {
                using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT a.Title, ISNULL(a.SourceName,'?'), ISNULL(a.Url,''), a.PublishDate
FROM intel_StoryArticles sa
JOIN intel_Articles a ON a.Id = sa.ArticleId
WHERE sa.StoryId = @s
ORDER BY a.PublishDate DESC", cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@s", storyId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new StorySourceArticle
                    {
                        Title = r.GetString(0),
                        SourceName = r.GetString(1),
                        Url = r.GetString(2),
                        PublishDate = r.IsDBNull(3) ? DateTime.MinValue : r.GetDateTime(3)
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Stories] GetStoryArticles error: {ex.Message}"); }
            return list;
        }
    }

    public class StorySourceArticle
    {
        public string Title { get; set; } = "";
        public string SourceName { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PublishDate { get; set; }
        public string DateDisplay => PublishDate == DateTime.MinValue ? "" : PublishDate.ToString("dd.MM");
    }
}
