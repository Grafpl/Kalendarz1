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
    /// Faza B: Haiku wyciąga konkretne ceny/wartości/kursy z artykułów do intel_TrendDataPoints.
    /// Tylko predefiniowana lista metryk (whitelista) — żeby nie zbierać śmiecia.
    /// Działa PO save (potrzebuje ArticleId). UPSERT przez TrendsService.
    /// </summary>
    public class PriceExtractionService
    {
        private readonly string _connectionString;
        private readonly ClaudeAnalysisService _claude;
        private readonly TrendsService _trends;
        private const int BatchSize = 10;

        // Whitelista oczekiwanych metryk — Haiku zwraca tylko te.
        private static readonly (string Key, string Label, string Unit)[] AllowedMetrics =
        {
            ("price.zywiec_brojler_kg",  "Cena żywca brojlera",     "PLN/kg"),
            ("price.kurczak_tuszka_kg",  "Cena tuszki kurczaka",    "PLN/kg"),
            ("price.kurczak_filet_kg",   "Cena fileta z kurczaka",  "PLN/kg"),
            ("price.pszenica_t",         "Cena pszenicy",           "PLN/t"),
            ("price.soja_t",             "Cena soi/śruty",          "PLN/t"),
            ("price.kukurydza_t",        "Cena kukurydzy",          "PLN/t"),
            ("fx.eur_pln",               "Kurs EUR/PLN",            "ratio"),
            ("fx.usd_pln",               "Kurs USD/PLN",            "ratio")
        };

        public PriceExtractionService(string connectionString, ClaudeAnalysisService claude)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
            _claude = claude;
            _trends = new TrendsService(_connectionString);
        }

        /// <summary>Ekstrahuje metryki z artykułów dodanych od <paramref name="since"/>. Zwraca liczbę zapisanych.</summary>
        public async Task<int> ExtractRecentMetricsAsync(DateTime since, CancellationToken ct = default)
        {
            if (_claude == null || !_claude.IsConfigured) return 0;

            var articles = await LoadRecentArticlesAsync(since, ct);
            if (articles.Count == 0) return 0;

            var systemPrompt = BuildSystemPrompt();
            int saved = 0;

            foreach (var batch in Chunk(articles, BatchSize))
            {
                ct.ThrowIfCancellationRequested();
                var userPrompt = BuildUserPrompt(batch);
                var raw = await _claude.CompleteAsync(systemPrompt, userPrompt, useHaiku: true, maxTokens: 1500, ct: ct, cacheSystem: true);

                foreach (var m in ParseMetrics(raw))
                {
                    if (!AllowedMetrics.Any(a => a.Key == m.Metric)) continue;
                    var art = batch.FirstOrDefault(a => a.Id == m.ArticleId);
                    if (art.Id == 0) continue;

                    try
                    {
                        var unit = AllowedMetrics.First(a => a.Key == m.Metric).Unit;
                        await _trends.UpsertDailyPointAsync(
                            metricKey: m.Metric,
                            value: m.Value,
                            unit: unit,
                            date: (m.Date ?? art.PublishDate).Date,
                            aiExtracted: true,
                            sourceArticleId: art.Id,
                            sourceUrl: art.Url,
                            confidence: m.Confidence,
                            notes: m.SourceText?.Length > 500 ? m.SourceText.Substring(0, 500) : m.SourceText);
                        saved++;
                    }
                    catch (Exception ex) { Debug.WriteLine($"[PriceExt] save error: {ex.Message}"); }
                }
            }

            Debug.WriteLine($"[PriceExt] ✓ zapisano {saved} punktów cen/wskaźników z {articles.Count} artykułów.");
            return saved;
        }

        // ── Prompts ──

        private static string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Wyciągasz KONKRETNE liczby z artykułów dla polskiej ubojni drobiu.");
            sb.AppendLine("Tylko metryki Z TEJ LISTY (jeśli artykuł nie zawiera żadnej — zwróć []):");
            foreach (var (k, _, u) in AllowedMetrics)
                sb.AppendLine($"- {k} ({u})");
            sb.AppendLine();
            sb.AppendLine("REGUŁY:");
            sb.AppendLine("1. Liczba musi być JAWNIE w tekście artykułu (nie zgaduj, nie szacuj).");
            sb.AppendLine("2. Konwertuj jednostki do podanych: PLN/kg, PLN/t, ratio (np. 'EUR 4.30 zł' → fx.eur_pln=4.30).");
            sb.AppendLine("3. date: data do której odnosi się wartość (z artykułu); jeśli brak — null.");
            sb.AppendLine("4. confidence: 1-5 (1=mglista wzmianka, 5=cena na fakturze/notowaniu).");
            sb.AppendLine("5. sourceText: krótki cytat z artykułu uzasadniający wartość (max 300 znaków).");
            sb.AppendLine();
            sb.AppendLine("Odpowiedz TYLKO tablicą JSON:");
            sb.AppendLine(@"[{""articleId"":123,""metric"":""price.zywiec_brojler_kg"",""value"":4.72,""date"":""2026-05-30"",""confidence"":4,""sourceText"":""cena żywca brojlera wynosi 4.72 zł/kg""}]");
            return sb.ToString();
        }

        private static string BuildUserPrompt(List<(int Id, string Title, string Summary, DateTime PublishDate, string Url)> batch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ARTYKUŁY:");
            foreach (var a in batch)
            {
                var snip = string.IsNullOrEmpty(a.Summary) ? "" : (a.Summary.Length > 400 ? a.Summary.Substring(0, 400) : a.Summary);
                sb.AppendLine($"articleId={a.Id} (publikacja={a.PublishDate:yyyy-MM-dd}): {a.Title}");
                if (snip.Length > 0) sb.AppendLine($"   {snip}");
            }
            return sb.ToString();
        }

        private readonly record struct MetricDto(int ArticleId, string Metric, decimal Value, DateTime? Date, int Confidence, string SourceText);

        private static List<MetricDto> ParseMetrics(string raw)
        {
            var list = new List<MetricDto>();
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
                    string metric = el.TryGetProperty("metric", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                    decimal value = 0m;
                    if (el.TryGetProperty("value", out var v))
                    {
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var dv)) value = dv;
                        else if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sv)) value = sv;
                    }
                    DateTime? date = null;
                    if (el.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String && DateTime.TryParse(d.GetString(), out var dt)) date = dt;
                    int conf = el.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var cv) ? Math.Clamp(cv, 1, 5) : 3;
                    string src = el.TryGetProperty("sourceText", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;

                    if (aid > 0 && !string.IsNullOrEmpty(metric) && value != 0m)
                        list.Add(new MetricDto(aid, metric, value, date, conf, src));
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[PriceExt] JSON parse fallback: {ex.Message}"); }
            return list;
        }

        // ── DB ──

        private async Task<List<(int Id, string Title, string Summary, DateTime PublishDate, string Url)>> LoadRecentArticlesAsync(DateTime since, CancellationToken ct)
        {
            var list = new List<(int, string, string, DateTime, string)>();
            using var cn = new SqlConnection(_connectionString);
            await cn.OpenAsync(ct);
            using var cmd = new SqlCommand(@"
SELECT TOP 200 Id, Title, ISNULL(Summary,''), PublishDate, ISNULL(Url,'')
FROM intel_Articles
WHERE FetchedAt >= @since
ORDER BY FetchedAt DESC", cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@since", since);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? DateTime.Now : r.GetDateTime(3), r.GetString(4)));
            return list;
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
