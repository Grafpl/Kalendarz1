using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Pojedynczy hit w wyszukiwaniu — klatka + ocena VLM.
    /// </summary>
    public class SearchHit
    {
        public long FrameId { get; set; }
        public string CameraId { get; set; } = string.Empty;
        public DateTime TsUtc { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int Score { get; set; }            // 0..100
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pełny pipeline wyszukiwania zdarzeń w nagraniach. PoC v1: bez CLIP,
    /// brute-force VLM rerank wszystkich klatek-kandydatów.
    /// Wariant docelowy (5b/przyszłość): CLIP prefilter → top-50 → VLM rerank.
    /// </summary>
    public static class SearchService
    {
        public class SearchSummary
        {
            public List<SearchHit> Hits { get; set; } = new();
            public int Candidates { get; set; }
            public int VlmCalls { get; set; }
            public double TotalCostUsd { get; set; }
            public long DurationMs { get; set; }
            public long? AuditId { get; set; }
        }

        public static async Task<SearchSummary> SearchAsync(
            string polishQuery,
            int topN = 10,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            IEnumerable<string>? cameraIds = null,
            int candidateLimit = 200,
            int maxConcurrency = 5,
            string userId = "ser",
            CancellationToken ct = default)
        {
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            var sw = Stopwatch.StartNew();
            var summary = new SearchSummary();

            var kandydaci = FrameIndex.GetFrames(fromUtc, toUtc, cameraIds, candidateLimit);
            summary.Candidates = kandydaci.Count;
            if (kandydaci.Count == 0)
            {
                summary.DurationMs = sw.ElapsedMilliseconds;
                return summary;
            }

            // Paralelne wywołania VLM z ograniczeniem - Anthropic toleruje, my nie chcemy się zatkać.
            var sem = new SemaphoreSlim(maxConcurrency);
            var bag = new System.Collections.Concurrent.ConcurrentBag<SearchHit>();
            double costAccum = 0;
            int callsAccum = 0;
            object accumLock = new();

            string prompt = BudujPrompt(polishQuery);

            var tasks = kandydaci.Select(async k =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    string fullPrompt = prompt
                        + $"\n\nKontekst klatki: kamera={k.CameraId}, czas (UTC)={k.TsUtc:yyyy-MM-dd HH:mm:ss}.";

                    var vlm = await VlmClient.AnalyzeImageAsync(
                        k.FilePath, fullPrompt,
                        model: VlmClient.ModelHaiku, maxTokens: 200, ct: ct);

                    lock (accumLock) { costAccum += vlm.CostUsd; callsAccum++; }

                    var (score, reason) = WyciagnijScoreReason(vlm.Text);
                    bag.Add(new SearchHit
                    {
                        FrameId = k.Id,
                        CameraId = k.CameraId,
                        TsUtc = k.TsUtc,
                        FilePath = k.FilePath,
                        Score = score,
                        Reason = reason
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CNA-Search] frame {k.Id} fail: {ex.Message}");
                    // Klatki z błędem dostają score=0 — nie blokujemy całego search z powodu jednej.
                    bag.Add(new SearchHit
                    {
                        FrameId = k.Id, CameraId = k.CameraId, TsUtc = k.TsUtc, FilePath = k.FilePath,
                        Score = 0, Reason = $"błąd VLM: {ex.Message.Split('\n')[0]}"
                    });
                }
                finally { sem.Release(); }
            }).ToArray();

            await Task.WhenAll(tasks);

            summary.VlmCalls = callsAccum;
            summary.TotalCostUsd = costAccum;
            summary.Hits = bag.OrderByDescending(h => h.Score).ThenByDescending(h => h.TsUtc).Take(topN).ToList();
            sw.Stop();
            summary.DurationMs = sw.ElapsedMilliseconds;

            try
            {
                summary.AuditId = FrameIndex.InsertAudit(
                    polishQuery, userId,
                    summary.Hits.Select(h => h.FrameId),
                    summary.VlmCalls, summary.TotalCostUsd, summary.DurationMs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CNA-Search] audit DB fail: {ex.Message}");
            }

            // Append-only audit log (plain text). Niezależny od SQLite żeby przetrwać
            // korupcję/wykasowanie bazy. Format: pipe-separated dla łatwego grepowania.
            try
            {
                string topIds = string.Join(",", summary.Hits.Select(h => h.FrameId));
                string topScores = string.Join(",", summary.Hits.Select(h => h.Score));
                string line =
                    $"{DateTime.UtcNow:o}|user={userId}|q=\"{polishQuery.Replace('"', '\'')}\"" +
                    $"|cands={summary.Candidates}|vlm_calls={summary.VlmCalls}" +
                    $"|cost_usd={summary.TotalCostUsd:F4}|dur_ms={summary.DurationMs}" +
                    $"|top_ids={topIds}|top_scores={topScores}";
                System.IO.Directory.CreateDirectory(CnaConfig.AuditDir);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(CnaConfig.AuditDir, "cna_search_audit.log"),
                    line + System.Environment.NewLine,
                    System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CNA-Search] audit log fail: {ex.Message}");
            }

            return summary;
        }

        /// <summary>
        /// Polski prompt dla rerankera. Wymuszamy JSON output z dwoma polami.
        /// </summary>
        private static string BudujPrompt(string polishQuery) =>
            "Jesteś asystentem analizującym klatki z kamer CCTV w zakładzie drobiarskim (ubojnia).\n" +
            $"PYTANIE UŻYTKOWNIKA: \"{polishQuery}\"\n\n" +
            "Twoje zadanie: oceń, jak bardzo to konkretne zdjęcie pasuje do pytania.\n" +
            "Zwróć WYŁĄCZNIE JSON o strukturze:\n" +
            "{\"score\": <0-100>, \"reason\": \"<jedno zdanie po polsku, dlaczego pasuje albo nie>\"}\n\n" +
            "Reguły:\n" +
            " - score = 0 gdy zupełnie nie pasuje\n" +
            " - score = 100 gdy idealnie pasuje (np. dokładnie to zdarzenie i kontekst)\n" +
            " - score 30-70 gdy częściowo pasuje\n" +
            " - reason: max 1 zdanie po polsku, konkretne, bez ozdobników\n" +
            " - NIE dodawaj nic poza JSON.";

        // Haiku zwykle zwraca czysty JSON, ale czasami zawija w ```json ... ``` lub dodaje tekst przed.
        // Tu wyciągamy najbardziej prawdopodobne {...} z odpowiedzi.
        private static (int score, string reason) WyciagnijScoreReason(string text)
        {
            try
            {
                var match = Regex.Match(text, @"\{[^{}]*""score""[^{}]*\}", RegexOptions.Singleline);
                if (!match.Success) return (0, "brak JSON w odpowiedzi VLM");

                var jo = JObject.Parse(match.Value);
                int score = jo["score"]?.Value<int?>() ?? 0;
                if (score < 0) score = 0;
                if (score > 100) score = 100;
                string reason = jo["reason"]?.Value<string>() ?? string.Empty;
                return (score, reason);
            }
            catch (Exception ex)
            {
                return (0, $"parse fail: {ex.Message}");
            }
        }
    }
}
