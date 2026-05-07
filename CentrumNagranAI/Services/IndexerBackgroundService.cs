using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Background service indeksacji klatek RTSP. Pętla co N sekund:
    ///   dla każdej kamery → 1 klatka JPEG → INSERT do SQLite (status=pending).
    /// Embeddingi i VLM są obrabiane w osobnych pipeline'ach (kolejne checkpointy).
    ///
    /// Wzorzec singleton + System.Timers.Timer skopiowany z
    /// MarketIntelligenceBackgroundService.cs by zachować spójność z istniejącym kodem ZPSP.
    /// </summary>
    public class IndexerBackgroundService : IDisposable
    {
        private static IndexerBackgroundService? _instance;
        private static readonly object _instanceLock = new();
        public static IndexerBackgroundService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new IndexerBackgroundService();
                    }
                }
                return _instance;
            }
        }

        private System.Timers.Timer? _timer;
        private CancellationTokenSource? _cts;
        // Semaphore dla cykli — gdy poprzedni cykl trwa, kolejny tick jest pomijany.
        private readonly SemaphoreSlim _cycleLock = new(1, 1);
        private bool _isRunning;
        private long _cycleCounter;
        private long _frameCounter;

        public bool IsRunning => _isRunning;
        public long Cycles => _cycleCounter;
        public long FramesGrabbed => _frameCounter;

        // Cleanup uruchamiany raz na dobę o tej samej godzinie od startu indexera.
        private System.Timers.Timer? _cleanupTimer;

        public Task StartAsync()
        {
            if (_isRunning) return Task.CompletedTask;

            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            _cts = new CancellationTokenSource();

            int interval = Math.Max(2, CnaConfig.InterwalKlatkiSekund);
            _timer = new System.Timers.Timer(interval * 1000.0) { AutoReset = true };
            _timer.Elapsed += (_, _) => _ = ExecuteCycleAsync();
            _timer.Start();

            // Cleanup co 24h
            _cleanupTimer = new System.Timers.Timer(TimeSpan.FromHours(24).TotalMilliseconds) { AutoReset = true };
            _cleanupTimer.Elapsed += (_, _) => { try { CleanupOldFrames(CnaConfig.RetencjaDni); } catch (Exception ex) { Log($"Cleanup fail: {ex.Message}"); } };
            _cleanupTimer.Start();

            _isRunning = true;
            Log($"Indexer wystartowany. Kamer: {CnaConfig.Kamery.Count}, interwał: {interval}s, retencja: {CnaConfig.RetencjaDni} dni");

            _ = ExecuteCycleAsync();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Kasuje klatki starsze niż retencja - pliki JPEG na dysku + wpisy w SQLite.
        /// Zwraca (ile_plików_skasowanych, MB_zwolnione, ile_rekordów_DB_skasowanych).
        /// </summary>
        public static (int FilesDeleted, double MbFreed, int DbRowsDeleted) CleanupOldFrames(int retencjaDni)
        {
            CnaConfig.ZaladujJesliTrzeba();
            DateTime cutoff = DateTime.UtcNow.AddDays(-retencjaDni);

            int filesDel = 0;
            long bytesFreed = 0;

            // 1) Skanuj folder frames — kasuj per data folder (yyyy-MM-dd) jeśli starsze
            if (Directory.Exists(CnaConfig.FramesDir))
            {
                foreach (var camDir in Directory.GetDirectories(CnaConfig.FramesDir))
                {
                    foreach (var dayDir in Directory.GetDirectories(camDir))
                    {
                        var name = Path.GetFileName(dayDir);
                        if (!DateTime.TryParseExact(name, "yyyy-MM-dd", null,
                            System.Globalization.DateTimeStyles.None, out var dayDate)) continue;
                        if (dayDate.Date >= cutoff.Date) continue;

                        try
                        {
                            foreach (var f in Directory.GetFiles(dayDir, "*.jpg"))
                            {
                                bytesFreed += new FileInfo(f).Length;
                                File.Delete(f);
                                filesDel++;
                            }
                            Directory.Delete(dayDir, recursive: false);
                        }
                        catch (Exception ex)
                        {
                            Log($"Cleanup folder {dayDir} fail: {ex.Message}");
                        }
                    }
                }
            }

            // 2) Skasuj wpisy w bazie
            int dbRows = FrameIndex.DeleteFramesOlderThan(cutoff);

            double mbFreed = bytesFreed / 1024.0 / 1024.0;
            Log($"Cleanup: {filesDel} plików ({mbFreed:F1} MB) + {dbRows} rekordów DB skasowane (starsze niż {retencjaDni} dni)");
            return (filesDel, mbFreed, dbRows);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
            _cts?.Cancel();
            _isRunning = false;
            Log($"Indexer zatrzymany. Cykli: {_cycleCounter}, klatek: {_frameCounter}");
        }

        private async Task ExecuteCycleAsync()
        {
            // Jeśli poprzedni cykl jeszcze chodzi — pomijamy ten tick (typowe gdy ffmpeg długo).
            if (!await _cycleLock.WaitAsync(0)) return;

            long cycleId = Interlocked.Increment(ref _cycleCounter);
            try
            {
                var sw = Stopwatch.StartNew();
                int ok = 0, fail = 0;

                foreach (var kamera in CnaConfig.Kamery)
                {
                    if (_cts?.IsCancellationRequested == true) break;

                    var tsUtc = DateTime.UtcNow;
                    string outPath = BudujSciezkeKlatki(kamera, tsUtc);

                    try
                    {
                        await RtspFrameGrabber.ZapiszKlatkeAsync(kamera, outPath, timeoutSekund: 12, ct: _cts!.Token);
                        long size = new FileInfo(outPath).Length;
                        FrameIndex.InsertFrame(kamera.Id, tsUtc, outPath, size);
                        Interlocked.Increment(ref _frameCounter);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        Log($"[cykl {cycleId}] {kamera.Id} FAIL: {ex.Message.Split('\n')[0]}");
                    }
                }

                sw.Stop();
                Log($"[cykl {cycleId}] ok={ok} fail={fail} czas={sw.ElapsedMilliseconds}ms total_frames={_frameCounter}");
            }
            finally
            {
                _cycleLock.Release();
            }
        }

        /// <summary>
        /// Ścieżka pliku klatki: frames\&lt;cameraId&gt;\&lt;yyyy-MM-dd&gt;\&lt;unix_ts&gt;_&lt;HHmmss&gt;.jpg
        /// Datowane folderyzacja ułatwia retencję (kasowanie po N dniach = rm folderów &gt; data).
        /// </summary>
        private static string BudujSciezkeKlatki(CnaCameraEndpoint kamera, DateTime tsUtc)
        {
            string dzien = tsUtc.ToString("yyyy-MM-dd");
            string nazwa = $"{new DateTimeOffset(tsUtc).ToUnixTimeSeconds()}_{tsUtc:HHmmss}.jpg";
            return Path.Combine(CnaConfig.FramesDir, kamera.Id, dzien, nazwa);
        }

        public void Dispose() => Stop();

        private static void Log(string msg)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [CNA-Indexer] {msg}";
            Debug.WriteLine(line);
            try
            {
                Directory.CreateDirectory(CnaConfig.AuditDir);
                File.AppendAllText(Path.Combine(CnaConfig.AuditDir, "cna_indexer.log"), line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { /* logowanie nie może rozwalić indexera */ }
        }
    }
}
