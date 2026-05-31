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
    /// Faza A.5: dla top N wątków generuje LastDigest (3-4 akapity chronologicznie) + BusinessImpact
    /// (2-3 zdania konkretnie dla ubojni Piórkowscy) + rewizja Severity/PoultryRelevance (Sonnet 4.6).
    /// Działa PO StoryClusteringService — tylko wątki bez aktualnego digestu
    /// (LastDigestAt NULL lub starsze niż LastUpdatedAt).
    /// </summary>
    public class StoryDigestService
    {
        private readonly string _connectionString;
        private readonly ClaudeAnalysisService _claude;

        public StoryDigestService(string connectionString, ClaudeAnalysisService claude)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
            _claude = claude;
        }

        /// <summary>Generuje digesty dla top N wątków wymagających odświeżenia. Zwraca liczbę.</summary>
        public async Task<int> GenerateDigestsAsync(int topN = 5, CancellationToken ct = default)
        {
            if (_claude == null || !_claude.IsConfigured) return 0;

            var stories = await LoadStoriesNeedingDigestAsync(topN, ct);
            if (stories.Count == 0) return 0;

            int generated = 0;
            // Sonnet ~30-60s per story, equalibrium: leverage SemaphoreSlim(5) wewnątrz Claude'a.
            // Tu sekwencyjnie po wątku — żeby nie zalewać i dla czystych logów.
            foreach (var s in stories)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var articles = await LoadStoryArticlesAsync(s.Id, ct);
                    if (articles.Count == 0) continue;

                    var systemPrompt = BuildSystemPrompt();
                    var userPrompt = BuildUserPrompt(s.Title, s.StoryType, articles);

                    var raw = await _claude.CompleteAsync(systemPrompt, userPrompt, useHaiku: false, maxTokens: 2000, ct: ct, cacheSystem: true);
                    var parsed = ParseDigest(raw);
                    if (parsed == null) continue;

                    await UpdateStoryAsync(s.Id, parsed, ct);
                    generated++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Digest] story={s.Id} error: {ex.Message}");
                }
            }

            Debug.WriteLine($"[Digest] ✓ wygenerowano {generated}/{stories.Count} digestów.");
            return generated;
        }

        // ── Prompts ──

        private static string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Jesteś analitykiem branży drobiarskiej dla Ubojni Drobiu Piórkowscy (Brzeziny, łódzkie,");
            sb.AppendLine("~258 mln zł obrotu, 200 t/dzień, klienci: Biedronka/Auchan/Carrefour/Makro/Selgros,");
            sb.AppendLine("kryzys -40% sprzedaży YoY, zagrożenia: Cedrob/ADQ, import Brazylia/Ukraina, HPAI łódzkie).");
            sb.AppendLine();
            sb.AppendLine("Dostajesz WĄTEK (skupisko artykułów o jednej sprawie). Wygeneruj DOKŁADNIE JSON:");
            sb.AppendLine(@"{");
            sb.AppendLine(@"  ""digest"": ""3-4 akapity, chronologicznie co się działo, z konkretnymi liczbami i datami"",");
            sb.AppendLine(@"  ""businessImpact"": ""2-3 zdania KONKRETNIE dla Ubojni Piórkowscy (co robić, czego unikać, ile to nas kosztuje/da)"",");
            sb.AppendLine(@"  ""severity"": 1-5,");
            sb.AppendLine(@"  ""poultryRelevance"": 1-5");
            sb.AppendLine(@"}");
            sb.AppendLine();
            sb.AppendLine("severity: 1=tło / 2=warto wiedzieć / 3=ważne / 4=pilne / 5=krytyczne (akcja teraz).");
            sb.AppendLine("poultryRelevance: 1=marginalnie / 5=bezpośrednio dotyka drobiu.");
            sb.AppendLine("Pisz PO POLSKU, konkretnie, bez lania wody. Bez nagłówków markdown. Bez wstępów.");
            sb.AppendLine("ODPOWIEDZ TYLKO JSON-em (bez ```json otoczki).");
            return sb.ToString();
        }

        private static string BuildUserPrompt(string title, string type, List<(string Title, string Summary, DateTime Date, string Source)> articles)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"WĄTEK: {title}");
            sb.AppendLine($"TYP: {type}");
            sb.AppendLine();
            sb.AppendLine($"ARTYKUŁY ŹRÓDŁOWE ({articles.Count}), chronologicznie:");
            foreach (var a in articles.OrderBy(x => x.Date))
            {
                var snip = string.IsNullOrEmpty(a.Summary) ? "" : (a.Summary.Length > 400 ? a.Summary.Substring(0, 400) + "..." : a.Summary);
                sb.AppendLine($"--- {a.Date:yyyy-MM-dd} · {a.Source} ---");
                sb.AppendLine(a.Title);
                if (snip.Length > 0) sb.AppendLine(snip);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private readonly record struct DigestResult(string Digest, string BusinessImpact, int Severity, int PoultryRelevance);

        private static DigestResult? ParseDigest(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                var start = raw.IndexOf('{');
                var end = raw.LastIndexOf('}');
                if (start < 0 || end <= start) return null;
                using var doc = JsonDocument.Parse(raw.Substring(start, end - start + 1));
                var root = doc.RootElement;
                var digest = root.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                var impact = root.TryGetProperty("businessImpact", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;
                int sev = root.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt32(out var sv) ? Math.Clamp(sv, 1, 5) : 3;
                int rel = root.TryGetProperty("poultryRelevance", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pv) ? Math.Clamp(pv, 1, 5) : 3;
                if (string.IsNullOrWhiteSpace(digest)) return null;
                return new DigestResult(digest, impact ?? "", sev, rel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Digest] JSON parse fallback: {ex.Message}");
                return null;
            }
        }

        // ── DB ──

        private async Task<List<(int Id, string Title, string StoryType)>> LoadStoriesNeedingDigestAsync(int topN, CancellationToken ct)
        {
            var list = new List<(int, string, string)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            // Wątki które wymagają digestu: nigdy nie miały LUB digest starszy niż ostatnia aktualizacja.
            // Tylko z artykułami (ArticleCount > 0) i statusem developing/stable.
            using var cmd = new SqlCommand(@"
SELECT TOP (@topN) Id, Title, StoryType
FROM intel_Stories
WHERE Status IN ('developing','stable')
  AND ArticleCount > 0
  AND (LastDigestAt IS NULL OR LastDigestAt < LastUpdatedAt)
ORDER BY (Severity * PoultryRelevance) DESC, LastUpdatedAt DESC", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@topN", topN);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? "other" : r.GetString(2)));
            return list;
        }

        private async Task<List<(string Title, string Summary, DateTime Date, string Source)>> LoadStoryArticlesAsync(int storyId, CancellationToken ct)
        {
            var list = new List<(string, string, DateTime, string)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT a.Title, ISNULL(a.Summary,''), a.PublishDate, ISNULL(a.SourceName,'?')
FROM intel_StoryArticles sa
JOIN intel_Articles a ON a.Id = sa.ArticleId
WHERE sa.StoryId = @s
ORDER BY a.PublishDate ASC", cn) { CommandTimeout = 15 };
            cmd.Parameters.AddWithValue("@s", storyId);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetString(0), r.GetString(1), r.IsDBNull(2) ? DateTime.MinValue : r.GetDateTime(2), r.GetString(3)));
            return list;
        }

        private async Task UpdateStoryAsync(int storyId, DigestResult? d, CancellationToken ct)
        {
            if (d == null) return;
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
UPDATE intel_Stories
SET LastDigest = @digest,
    BusinessImpact = @impact,
    Severity = @sev,
    PoultryRelevance = @rel,
    LastDigestAt = SYSUTCDATETIME()
WHERE Id = @s", cn) { CommandTimeout = 15 };
            var dv = d.Value;
            cmd.Parameters.AddWithValue("@digest", dv.Digest.Length > 4000 ? dv.Digest.Substring(0, 4000) : dv.Digest);
            cmd.Parameters.AddWithValue("@impact", string.IsNullOrEmpty(dv.BusinessImpact) ? (object)DBNull.Value : (dv.BusinessImpact.Length > 2000 ? dv.BusinessImpact.Substring(0, 2000) : dv.BusinessImpact));
            cmd.Parameters.AddWithValue("@sev", dv.Severity);
            cmd.Parameters.AddWithValue("@rel", dv.PoultryRelevance);
            cmd.Parameters.AddWithValue("@s", storyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
