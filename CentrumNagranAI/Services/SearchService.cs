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

        /// <summary>
        /// Wyszukiwanie podobnych klatek na bazie embedingu danej klatki.
        /// Brak VLM, brak kosztu - tylko lokalne KNN cosine.
        /// </summary>
        public static SearchSummary SearchSimilar(long frameId, int topN = 10)
        {
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            var sw = Stopwatch.StartNew();
            var summary = new SearchSummary();

            var refVec = FrameIndex.GetEmbedding(frameId);
            if (refVec == null)
            {
                summary.DurationMs = sw.ElapsedMilliseconds;
                return summary;
            }

            var knn = FrameIndex.KnnSearch(refVec, topN + 1);
            // Pomiń sam ref frame, weź top-N podobnych
            knn.RemoveAll(h => h.FrameId == frameId);
            summary.Hits = knn.Take(topN).Select((h, i) => new SearchHit
            {
                FrameId = h.FrameId,
                CameraId = h.CameraId,
                TsUtc = h.TsUtc,
                FilePath = h.FilePath,
                Score = (int)Math.Round(h.Similarity * 100),
                Reason = h.Caption ?? "(brak opisu)"
            }).ToList();
            summary.Candidates = knn.Count;
            summary.VlmCalls = 0;
            summary.TotalCostUsd = 0;
            summary.DurationMs = sw.ElapsedMilliseconds;
            return summary;
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

            // Strategia: jeśli >= 70% klatek ma embedding, używamy KNN prefilter.
            // Inaczej fallback do brute-force VLM (jak było).
            var (totalFrames, withEmb) = FrameIndex.GetEmbeddingStats();
            bool useKnn = totalFrames > 0 && (withEmb * 100 / Math.Max(1, totalFrames)) >= 70 && EmbeddingService.IsConfigured;

            List<FrameIndex.FrameRecord> kandydaci;

            if (useKnn)
            {
                // KNN: query embed → top-K kandydatów (zwykle 50 zamiast wszystkich 200+)
                Debug.WriteLine($"[CNA-Search] KNN tryb (embedding pokrycie {withEmb}/{totalFrames}={withEmb * 100 / totalFrames}%)");
                float[] qVec;
                try
                {
                    qVec = await EmbeddingService.EmbedAsync(polishQuery, ct);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CNA-Search] embed fail, fallback brute-force: {ex.Message}");
                    useKnn = false;
                    qVec = Array.Empty<float>();
                }

                if (useKnn)
                {
                    int knnK = Math.Min(CnaConfig.TopKCandydatow, candidateLimit);
                    var knn = FrameIndex.KnnSearch(qVec, knnK, fromUtc, toUtc, cameraIds);
                    kandydaci = knn.Select(h => new FrameIndex.FrameRecord
                    {
                        Id = h.FrameId,
                        CameraId = h.CameraId,
                        TsUtc = h.TsUtc,
                        FilePath = h.FilePath,
                        FileSize = 0
                    }).ToList();
                }
                else
                {
                    kandydaci = FrameIndex.GetFrames(fromUtc, toUtc, cameraIds, candidateLimit);
                }
            }
            else
            {
                Debug.WriteLine($"[CNA-Search] Brute-force tryb (embedding pokrycie {withEmb}/{totalFrames})");
                kandydaci = FrameIndex.GetFrames(fromUtc, toUtc, cameraIds, candidateLimit);
            }

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
        /// Polski prompt dla rerankera. Few-shot examples + ścisła skala score
        /// żeby Haiku nie zwracał wszystkim 85/100. Wymuszamy JSON output.
        /// </summary>
        private static string BudujPrompt(string polishQuery) =>
            "Jesteś dokładnym analitykiem klatek CCTV w zakładzie drobiarskim (ubojnia kurczaków).\n" +
            "Twoje zadanie: ocenić jak BARDZO BEZPOŚREDNIO to zdjęcie odpowiada na pytanie użytkownika.\n" +
            "Bądź SUROWY w ocenie. NIE dawaj wysokich punktów za luźne skojarzenia.\n\n" +
            "===== SKALA OCENY (ścisła) =====\n" +
            "  0-15  : zdjęcie NIE MA NIC WSPÓLNEGO z pytaniem\n" +
            " 16-35  : zdjęcie odległe luźnym skojarzeniem (ten sam typ pomieszczenia, nic więcej)\n" +
            " 36-55  : zdjęcie z otoczeniem właściwym, ale brak konkretnego elementu z pytania\n" +
            " 56-75  : zdjęcie zawiera większość elementów z pytania, ale nie wszystkie\n" +
            " 76-90  : zdjęcie zawiera wszystkie elementy z pytania, dobrze widoczne\n" +
            " 91-100 : DOSKONAŁY match - zdjęcie jest dokładnie tym o co pytano, idealnie kadr i kontekst\n\n" +
            "===== PRZYKŁADY (jak punktować) =====\n" +
            "Pytanie: \"osoba bez czepka w pakowalni\"\n" +
            "  - Zdjęcie pustej pakowalni bez ludzi → score=10 (jest pakowalnia, nie ma osoby)\n" +
            "  - Osoba w czepku w pakowalni → score=20 (jest osoba i pakowalnia, ale CZEPEK na głowie)\n" +
            "  - Osoba bez czepka w hali uboju → score=40 (osoba bez czepka, ale ZŁE pomieszczenie)\n" +
            "  - Osoba bez czepka w pakowalni, blisko kamery → score=95 (DOKŁADNIE to pytanie)\n\n" +
            "Pytanie: \"ciężarówka cofająca na rampie\"\n" +
            "  - Pusty parking → score=5\n" +
            "  - Ciężarówka stojąca na rampie → score=45 (jest ciężarówka i rampa, ale STOI nie cofa)\n" +
            "  - Ciężarówka w ruchu na drodze, nie na rampie → score=25\n" +
            "  - Tył ciężarówki przy bramie rampy z pracownikiem dającym znaki → score=85\n\n" +
            $"===== PYTANIE UŻYTKOWNIKA =====\n\"{polishQuery}\"\n\n" +
            "===== ODPOWIEDŹ =====\n" +
            "Zwróć WYŁĄCZNIE czysty JSON, bez ```, bez komentarzy:\n" +
            "{\"score\": <liczba 0-100>, \"reason\": \"<jedno zdanie po polsku konkretnie co widzisz w kontekście pytania>\"}\n";

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
