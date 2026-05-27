using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Faza A: wykrywa wzmianki o tracked entities (intel_Entities) w artykułach + sentyment.
    /// Działa PO zapisie artykułów (potrzebuje Id). Dedup po (EntityId, ArticleId).
    /// </summary>
    public class EntityExtractionService
    {
        private readonly string _connectionString;
        private readonly ClaudeAnalysisService _claude;
        private const int BatchSize = 10;

        public EntityExtractionService(string connectionString, ClaudeAnalysisService claude)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
            _claude = claude;
        }

        /// <summary>Wyciąga wzmianki z artykułów dodanych od <paramref name="since"/>. Zwraca liczbę zapisanych.</summary>
        public async Task<int> ExtractRecentMentionsAsync(DateTime since, CancellationToken ct = default)
        {
            if (_claude == null || !_claude.IsConfigured) return 0;

            var entities = await LoadTrackedEntitiesAsync(ct);
            if (entities.Count == 0) return 0;

            var articles = await LoadRecentArticlesAsync(since, ct);
            if (articles.Count == 0) return 0;

            var systemPrompt = BuildSystemPrompt(entities);
            int saved = 0;

            foreach (var batch in Chunk(articles, BatchSize))
            {
                ct.ThrowIfCancellationRequested();
                var userPrompt = BuildUserPrompt(batch);
                var raw = await _claude.CompleteAsync(systemPrompt, userPrompt, useHaiku: true, maxTokens: 1500, ct: ct, cacheSystem: true);

                foreach (var m in ParseMentions(raw))
                {
                    var art = batch.FirstOrDefault(a => a.Id == m.ArticleId);
                    if (art.Id == 0) continue;
                    try
                    {
                        await SaveMentionAsync(m.EntityId, art.Id, m.Sentiment, m.Context, art.PublishDate, ct);
                        saved++;
                    }
                    catch (Exception ex) { Debug.WriteLine($"[Entities] save error: {ex.Message}"); }
                }
            }

            Debug.WriteLine($"[Entities] ✓ zapisano {saved} wzmianek.");
            return saved;
        }

        // ── Prompts ──

        private static string BuildSystemPrompt(List<(int Id, string Name, string Aliases)> entities)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Jesteś analitykiem. Wykrywasz wzmianki o ŚLEDZONYCH PODMIOTACH w artykułach drobiarskich + oceniasz sentyment.");
            sb.AppendLine();
            sb.AppendLine("ŚLEDZONE PODMIOTY (entityId: nazwa [aliasy]):");
            foreach (var e in entities)
            {
                var al = string.IsNullOrEmpty(e.Aliases) ? "" : $" [{e.Aliases}]";
                sb.AppendLine($"{e.Id}: {e.Name}{al}");
            }
            sb.AppendLine();
            sb.AppendLine("Dla każdego artykułu znajdź WSZYSTKIE wzmianki śledzonych podmiotów (po nazwie LUB aliasie).");
            sb.AppendLine("sentiment: -5 (bardzo źle dla podmiotu) … 0 (neutralnie) … +5 (bardzo dobrze).");
            sb.AppendLine("context: jedno zdanie PL co o podmiocie mówi artykuł.");
            sb.AppendLine("Odpowiedz TYLKO tablicą JSON (pomiń artykuły bez wzmianek):");
            sb.AppendLine(@"[{""articleId"":123,""entityId"":5,""sentiment"":-2,""context"":""Cedrob obniżył ceny skupu żywca o 5%""}]");
            return sb.ToString();
        }

        private static string BuildUserPrompt(List<(int Id, string Title, string Summary, DateTime PublishDate)> batch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ARTYKUŁY:");
            foreach (var a in batch)
            {
                var snip = string.IsNullOrEmpty(a.Summary) ? "" : (a.Summary.Length > 300 ? a.Summary.Substring(0, 300) : a.Summary);
                sb.AppendLine($"articleId={a.Id}: {a.Title}");
                if (snip.Length > 0) sb.AppendLine($"   {snip}");
            }
            return sb.ToString();
        }

        private readonly record struct MentionDto(int ArticleId, int EntityId, int Sentiment, string Context);

        private static List<MentionDto> ParseMentions(string raw)
        {
            var list = new List<MentionDto>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            try
            {
                var start = raw.IndexOf('[');
                var end = raw.LastIndexOf(']');
                if (start < 0 || end <= start) return list;
                using var doc = JsonDocument.Parse(raw.Substring(start, end - start + 1));
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    int aid = el.TryGetProperty("articleId", out var a) && a.TryGetInt32(out var av) ? av : 0;
                    int eid = el.TryGetProperty("entityId", out var e) && e.TryGetInt32(out var ev) ? ev : 0;
                    int sent = el.TryGetProperty("sentiment", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt32(out var sv) ? sv : 0;
                    string ctx = el.TryGetProperty("context", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                    if (aid > 0 && eid > 0)
                        list.Add(new MentionDto(aid, eid, Math.Clamp(sent, -5, 5), ctx));
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Entities] JSON parse fallback: {ex.Message}"); }
            return list;
        }

        // ── DB ──

        private async Task<List<(int Id, string Name, string Aliases)>> LoadTrackedEntitiesAsync(CancellationToken ct)
        {
            var list = new List<(int, string, string)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(
                "SELECT Id, Name, ISNULL(Aliases,'') FROM intel_Entities WHERE IsTracked=1", cn) { CommandTimeout = 15 };
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
            return list;
        }

        private async Task<List<(int Id, string Title, string Summary, DateTime PublishDate)>> LoadRecentArticlesAsync(DateTime since, CancellationToken ct)
        {
            var list = new List<(int, string, string, DateTime)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT TOP 200 a.Id, a.Title, ISNULL(a.Summary,''), a.PublishDate
FROM intel_Articles a
WHERE a.FetchedAt >= @since
  AND NOT EXISTS (SELECT 1 FROM intel_EntityMentions m WHERE m.ArticleId = a.Id)  -- jeszcze nie przetworzone
ORDER BY a.FetchedAt DESC", cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@since", since);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? DateTime.Now : r.GetDateTime(3)));
            return list;
        }

        private async Task SaveMentionAsync(int entityId, int articleId, int sentiment, string context, DateTime mentionedAt, CancellationToken ct)
        {
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM intel_EntityMentions WHERE EntityId=@e AND ArticleId=@a)
INSERT INTO intel_EntityMentions (EntityId, ArticleId, Sentiment, Context, MentionedAt)
VALUES (@e, @a, @s, @ctx, @when)", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@e", entityId);
            cmd.Parameters.AddWithValue("@a", articleId);
            cmd.Parameters.AddWithValue("@s", sentiment);
            cmd.Parameters.AddWithValue("@ctx", (object)(context?.Length > 1000 ? context.Substring(0, 1000) : context) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@when", mentionedAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
