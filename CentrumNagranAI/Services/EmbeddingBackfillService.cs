using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Worker w tle: dla każdej klatki (Status=0 pending) tworzy caption (Haiku)
    /// i embedding (OpenAI). Wsadowo, max N klatek na cykl, żeby nie zatkać API.
    ///
    /// Cel: po N minutach od zindeksowania klatki, ma już caption i embedding,
    /// a wyszukiwanie używa KNN zamiast brute-force VLM.
    /// </summary>
    public class EmbeddingBackfillService : IDisposable
    {
        private static EmbeddingBackfillService? _instance;
        private static readonly object _instanceLock = new();
        public static EmbeddingBackfillService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock) { _instance ??= new EmbeddingBackfillService(); }
                }
                return _instance;
            }
        }

        private System.Timers.Timer? _timer;
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        // Domyślnie nadążamy za indekserem (4 kamery × 6/min = 24 klatek/min).
        // 15 klatek × 4/min = 60 klatek/min - z marginesem na pierwszą indeksację.
        public int BatchSize { get; set; } = 15;       // klatek na cykl
        public int IntervalSeconds { get; set; } = 15; // co 15s sprawdza pending

        public long Processed { get; private set; }
        public long Failed { get; private set; }
        public double TotalCostUsd { get; private set; }

        public Task StartAsync()
        {
            if (_isRunning) return Task.CompletedTask;
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();
            _cts = new CancellationTokenSource();

            _timer = new System.Timers.Timer(IntervalSeconds * 1000.0) { AutoReset = true };
            _timer.Elapsed += (_, _) => _ = RunCycleAsync();
            _timer.Start();
            _isRunning = true;
            Log($"Backfill wystartowany. Batch={BatchSize}, interval={IntervalSeconds}s");

            // Pierwszy cykl
            _ = RunCycleAsync();
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            _cts?.Cancel();
            _isRunning = false;
            Log($"Backfill zatrzymany. Processed={Processed} failed={Failed} cost=${TotalCostUsd:F4}");
        }

        public async Task RunCycleAsync()
        {
            if (!await _runLock.WaitAsync(0)) return;
            try
            {
                if (!EmbeddingService.IsConfigured)
                {
                    return; // bez OpenAI key nie mamy jak embedować
                }

                var pending = FrameIndex.GetFramesWithoutEmbedding(BatchSize);
                if (pending.Count == 0) return;

                Log($"Backfill: {pending.Count} klatek do uzupełnienia...");

                foreach (var frame in pending)
                {
                    if (_cts?.IsCancellationRequested == true) break;
                    if (!File.Exists(frame.FilePath))
                    {
                        FrameIndex.MarkEmbeddingFailed(frame.Id);
                        Failed++;
                        continue;
                    }

                    try
                    {
                        var (sc, costCap) = await CaptionService.CaptionStructuredAsync(frame.FilePath, _cts!.Token);
                        string embedText = CaptionService.BuildEmbeddingText(sc);
                        var vec = await EmbeddingService.EmbedAsync(embedText, _cts.Token);

                        var tags = CaptionService.ExtractTags(sc);
                        string tagsJson = Newtonsoft.Json.JsonConvert.SerializeObject(tags);
                        string structJson = Newtonsoft.Json.JsonConvert.SerializeObject(sc);

                        FrameIndex.UpsertCaptionAndEmbedding(frame.Id, sc.Caption, vec, tagsJson, structJson);
                        Processed++;
                        TotalCostUsd += costCap;

                        // Aktywność: delta vs poprzedniej klatki tej samej kamery (#20).
                        try { ActivityService.RecordActivity(frame.Id, frame.CameraId, frame.TsUtc, vec); }
                        catch (Exception ex) { Debug.WriteLine($"[Activity fail] {ex.Message}"); }

                        // Po skutecznym embedingu - równolegle sprawdź guard rules + anomaly.
                        // Fire-and-forget żeby nie blokować backfill pipeline'u.
                        _ = Task.Run(async () =>
                        {
                            try { await GuardService.CheckFrameAgainstRulesAsync(frame.Id, frame.CameraId, frame.FilePath, frame.TsUtc); }
                            catch (Exception ex) { Debug.WriteLine($"[Guard fail] {ex.Message}"); }
                        });
                        try { AnomalyService.CheckFrame(frame.Id, frame.CameraId, frame.TsUtc, vec); }
                        catch (Exception ex) { Debug.WriteLine($"[Anomaly fail] {ex.Message}"); }

                        // OCR tablic - tylko dla wybranych kamer (drogie). D1: multi-frame voting.
                        if (CnaConfig.KameryDoOcr.Contains(frame.CameraId))
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var plates = await PlateDetectionService.DetectWithVotingAsync(frame.Id);
                                    if (plates.Count > 0)
                                    {
                                        PlateDetectionService.SavePlates(frame.Id, frame.CameraId, frame.TsUtc, plates, string.Join(",", plates));
                                        TotalCostUsd += 0.003; // 3 klatki × $0.001 (multi-frame voting)
                                    }
                                }
                                catch (Exception ex) { Debug.WriteLine($"[Plate fail] {ex.Message}"); }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Failed++;
                        Log($"frame {frame.Id} fail: {ex.Message.Split('\n')[0]}");
                        try { FrameIndex.MarkEmbeddingFailed(frame.Id); } catch { }
                    }
                }

                Log($"Cykl koniec. Total processed={Processed} failed={Failed} cost=${TotalCostUsd:F4}");
            }
            finally
            {
                _runLock.Release();
            }
        }

        public void Dispose() => Stop();

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Backfill] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(
                    Path.Combine(CnaConfig.AuditDir, "cna_backfill.log"),
                    line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
