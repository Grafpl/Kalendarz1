using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Faza A: konsoliduje pojedyncze artykuły w „wątki" (intel_Stories).
    /// Działa PO zapisie artykułów do bazy (potrzebuje ich Id). Haiku decyduje czy artykuł
    /// należy do istniejącego wątku, czy zakłada nowy.
    /// </summary>
    public class StoryClusteringService
    {
        private readonly string _connectionString;
        private readonly ClaudeAnalysisService _claude;
        private const int BatchSize = 20;

        public StoryClusteringService(string connectionString, ClaudeAnalysisService claude)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
            _claude = claude;
        }

        /// <summary>Klastruje artykuły dodane od <paramref name="since"/>. Zwraca (dodane, nowe wątki).</summary>
        public async Task<(int clustered, int newStories)> ClusterRecentArticlesAsync(DateTime since, CancellationToken ct = default)
        {
            if (_claude == null || !_claude.IsConfigured) return (0, 0);

            var articles = await LoadRecentArticlesAsync(since, ct);
            if (articles.Count == 0) return (0, 0);

            var activeStories = await LoadActiveStoriesAsync(ct);
            int clustered = 0, created = 0;

            // System prompt = instrukcja + lista aktywnych wątków (identyczna między batchami → cache hit).
            var systemPrompt = BuildSystemPrompt(activeStories);

            foreach (var batch in Chunk(articles, BatchSize))
            {
                ct.ThrowIfCancellationRequested();
                var userPrompt = BuildUserPrompt(batch);

                var raw = await _claude.CompleteAsync(systemPrompt, userPrompt, useHaiku: true, maxTokens: 1500, ct: ct, cacheSystem: true);
                var decisions = ParseDecisions(raw);
                if (decisions.Count == 0) continue;

                foreach (var d in decisions)
                {
                    var art = batch.FirstOrDefault(a => a.Id == d.ArticleId);
                    if (art.Id == 0) continue;

                    try
                    {
                        if (d.IsNewStory && !string.IsNullOrWhiteSpace(d.SuggestedNewTitle))
                        {
                            var storyId = await CreateStoryAsync(d.SuggestedNewTitle, d.SuggestedStoryType ?? "other", art.PublishDate, ct);
                            if (storyId > 0)
                            {
                                await LinkArticleAsync(storyId, art.Id, ct);
                                created++;
                                clustered++;
                            }
                        }
                        else if (d.StoryId is int sid && sid > 0)
                        {
                            await LinkArticleAsync(sid, art.Id, ct);
                            await TouchStoryAsync(sid, art.PublishDate, ct);
                            clustered++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Clustering] write error art={art.Id}: {ex.Message}");
                    }
                }
            }

            Debug.WriteLine($"[Clustering] ✓ zcluster {clustered} artykułów, utworzono {created} nowych wątków.");
            return (clustered, created);
        }

        // ── Prompts ──

        private static string BuildSystemPrompt(List<(int Id, string Title, string Digest)> stories)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Jesteś analitykiem wiadomości branży drobiarskiej. Grupujesz artykuły w WĄTKI (stories).");
            sb.AppendLine("Wątek = ciąg artykułów o tej samej sprawie (np. 'przejęcie Cedrob przez ADQ', 'HPAI w łódzkiem').");
            sb.AppendLine();
            sb.AppendLine("AKTYWNE WĄTKI (dopasuj artykuł do jednego z nich jeśli pasuje):");
            if (stories.Count == 0) sb.AppendLine("(brak — wszystkie artykuły zakładają nowe wątki)");
            foreach (var s in stories)
            {
                var dig = string.IsNullOrEmpty(s.Digest) ? "" : " — " + (s.Digest.Length > 200 ? s.Digest.Substring(0, 200) : s.Digest);
                sb.AppendLine($"[{s.Id}] {s.Title}{dig}");
            }
            sb.AppendLine();
            sb.AppendLine("Dla KAŻDEGO artykułu zwróć obiekt JSON. Odpowiedz TYLKO tablicą JSON:");
            sb.AppendLine(@"[{""articleId"":123,""storyId"":47,""isNewStory"":false,""suggestedNewTitle"":null,""suggestedStoryType"":null}]");
            sb.AppendLine("Jeśli artykuł pasuje do aktywnego wątku → storyId=jego id, isNewStory=false.");
            sb.AppendLine("Jeśli to nowa sprawa → storyId=null, isNewStory=true, suggestedNewTitle=krótki tytuł PL, suggestedStoryType=hpai_outbreak|price_movement|competitor_action|regulation|export_event|customer_event|other.");
            return sb.ToString();
        }

        private static string BuildUserPrompt(List<(int Id, string Title, string Summary, DateTime PublishDate)> batch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ARTYKUŁY:");
            foreach (var a in batch)
            {
                var snip = string.IsNullOrEmpty(a.Summary) ? "" : (a.Summary.Length > 200 ? a.Summary.Substring(0, 200) : a.Summary);
                sb.AppendLine($"articleId={a.Id}: {a.Title}");
                if (snip.Length > 0) sb.AppendLine($"   {snip}");
            }
            return sb.ToString();
        }

        private static List<ClusterDecision> ParseDecisions(string raw)
        {
            var list = new List<ClusterDecision>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            try
            {
                var start = raw.IndexOf('[');
                var end = raw.LastIndexOf(']');
                if (start < 0 || end <= start) return list;
                var json = raw.Substring(start, end - start + 1);
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var d = new ClusterDecision();
                    if (el.TryGetProperty("articleId", out var aid) && aid.TryGetInt32(out var aidV)) d.ArticleId = aidV;
                    if (el.TryGetProperty("storyId", out var sid) && sid.ValueKind == JsonValueKind.Number && sid.TryGetInt32(out var sidV)) d.StoryId = sidV;
                    if (el.TryGetProperty("isNewStory", out var ns) && (ns.ValueKind == JsonValueKind.True || ns.ValueKind == JsonValueKind.False)) d.IsNewStory = ns.GetBoolean();
                    if (el.TryGetProperty("suggestedNewTitle", out var t) && t.ValueKind == JsonValueKind.String) d.SuggestedNewTitle = t.GetString();
                    if (el.TryGetProperty("suggestedStoryType", out var st) && st.ValueKind == JsonValueKind.String) d.SuggestedStoryType = st.GetString();
                    if (d.ArticleId > 0) list.Add(d);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Clustering] JSON parse fallback: {ex.Message}");
            }
            return list;
        }

        // ── DB ──

        private async Task<List<(int Id, string Title, string Summary, DateTime PublishDate)>> LoadRecentArticlesAsync(DateTime since, CancellationToken ct)
        {
            var list = new List<(int, string, string, DateTime)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT TOP 200 Id, Title, ISNULL(Summary,''), PublishDate
FROM intel_Articles
WHERE FetchedAt >= @since
  AND Id NOT IN (SELECT ArticleId FROM intel_StoryArticles)   -- jeszcze nie sklastrowane
ORDER BY FetchedAt DESC", cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@since", since);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? DateTime.Now : r.GetDateTime(3)));
            return list;
        }

        private async Task<List<(int Id, string Title, string Digest)>> LoadActiveStoriesAsync(CancellationToken ct)
        {
            var list = new List<(int, string, string)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT TOP 50 Id, Title, ISNULL(LastDigest,'')
FROM intel_Stories
WHERE Status IN ('developing','stable')
ORDER BY LastUpdatedAt DESC", cn) { CommandTimeout = 15 };
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
            return list;
        }

        private async Task<int> CreateStoryAsync(string title, string type, DateTime firstSeen, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
INSERT INTO intel_Stories (Title, StoryType, FirstSeenAt, LastUpdatedAt, Status, Severity, PoultryRelevance, ArticleCount)
OUTPUT INSERTED.Id
VALUES (@title, @type, @first, SYSUTCDATETIME(), 'developing', 3, 3, 0)", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@title", title.Length > 500 ? title.Substring(0, 500) : title);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@first", firstSeen);
            return (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        }

        private async Task LinkArticleAsync(int storyId, int articleId, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM intel_StoryArticles WHERE StoryId=@s AND ArticleId=@a)
BEGIN
    INSERT INTO intel_StoryArticles (StoryId, ArticleId) VALUES (@s, @a);
    UPDATE intel_Stories SET ArticleCount = ArticleCount + 1 WHERE Id=@s;
END", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@s", storyId);
            cmd.Parameters.AddWithValue("@a", articleId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task TouchStoryAsync(int storyId, DateTime when, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(
                "UPDATE intel_Stories SET LastUpdatedAt = SYSUTCDATETIME(), Status='developing' WHERE Id=@s", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@s", storyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
