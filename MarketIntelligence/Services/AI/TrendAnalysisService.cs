using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Faza B: kalkulowane metryki trendu (bez AI). Czyste SQL na istniejących danych:
    /// - mentions.{entity_slug}.week — liczba wzmianek encji w ostatnim tygodniu (per top 10 trackowane)
    /// - hpai.poland.outbreaks_week — liczba artykułów Category='HPAI' w ostatnim tygodniu
    /// UPSERT do intel_TrendDataPoints (AIExtracted=0).
    /// </summary>
    public class TrendAnalysisService
    {
        private readonly string _connectionString;
        private readonly TrendsService _trends;

        public TrendAnalysisService(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
            _trends = new TrendsService(_connectionString);
        }

        /// <summary>Liczy snapshot na konkretną datę (zwykle dziś).</summary>
        public async Task<int> ComputeDailyTrendsAsync(DateTime forDate, CancellationToken ct = default)
        {
            int wrote = 0;
            try
            {
                wrote += await ComputeEntityMentionsAsync(forDate, ct);
                wrote += await ComputeHpaiOutbreaksAsync(forDate, ct);
            }
            catch (Exception ex) { Debug.WriteLine($"[TrendAnalysis] error: {ex.Message}"); }

            Debug.WriteLine($"[TrendAnalysis] ✓ zapisano {wrote} punktów trendu na {forDate:yyyy-MM-dd}.");
            return wrote;
        }

        private async Task<int> ComputeEntityMentionsAsync(DateTime forDate, CancellationToken ct)
        {
            // Top 10 encji wg liczby wzmianek ostatnich 14 dni — żeby mieć stałą listę metryk do śledzenia.
            var top = new List<(int Id, string Name)>();
            using (var cn = new SqlConnection(_connectionString))
            {
                await cn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
SELECT TOP 10 e.Id, e.Name
FROM intel_Entities e
JOIN intel_EntityMentions m ON m.EntityId = e.Id
WHERE e.IsTracked = 1
  AND m.MentionedAt >= DATEADD(day, -14, @d)
GROUP BY e.Id, e.Name
ORDER BY COUNT(*) DESC", cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@d", forDate);
                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) top.Add((r.GetInt32(0), r.GetString(1)));
            }

            int wrote = 0;
            foreach (var (id, name) in top)
            {
                ct.ThrowIfCancellationRequested();
                int count = await CountMentionsLastWeekAsync(id, forDate, ct);
                var slug = Slugify(name);
                await _trends.UpsertDailyPointAsync(
                    metricKey: $"mentions.{slug}.week",
                    value: count,
                    unit: "count",
                    date: forDate,
                    aiExtracted: false,
                    confidence: 5,
                    notes: $"Wzmianki o {name} w ostatnich 7 dniach");
                wrote++;
            }
            return wrote;
        }

        private async Task<int> CountMentionsLastWeekAsync(int entityId, DateTime forDate, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT COUNT(*) FROM intel_EntityMentions
WHERE EntityId=@e AND MentionedAt BETWEEN DATEADD(day,-7,@d) AND @d", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@e", entityId);
            cmd.Parameters.AddWithValue("@d", forDate);
            return (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        }

        private async Task<int> ComputeHpaiOutbreaksAsync(DateTime forDate, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT COUNT(*) FROM intel_Articles
WHERE (Category='HPAI' OR Title LIKE N'%HPAI%' OR Title LIKE N'%ptasia gryp%' OR Title LIKE N'%ognisk%')
  AND PublishDate BETWEEN DATEADD(day,-7,@d) AND @d", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@d", forDate);
            var count = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);

            await _trends.UpsertDailyPointAsync(
                metricKey: "hpai.poland.outbreaks_week",
                value: count,
                unit: "count",
                date: forDate,
                aiExtracted: false,
                confidence: 4,
                notes: "Artykuły z kategorią/tytułem HPAI/ptasia grypa w ostatnich 7 dniach (proxy ognisk)");
            return 1;
        }

        /// <summary>Slug: lower + bez diakrytyków + tylko a-z0-9, spacja → kropka.</summary>
        private static string Slugify(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '-' || c == '_') sb.Append('.');
            }
            var slug = sb.ToString().Trim('.');
            return string.IsNullOrEmpty(slug) ? "unknown" : slug;
        }
    }
}
