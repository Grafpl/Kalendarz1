using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Profiler wydajności dla modułu opakowań
    /// Mierzy czasy wszystkich operacji i generuje raport
    /// </summary>
    public static class PerformanceProfiler
    {
        private static readonly ConcurrentDictionary<string, List<OperationTiming>> _timings = new();
        private static readonly ConcurrentDictionary<string, int> _cacheHits = new();
        private static readonly ConcurrentDictionary<string, int> _cacheMisses = new();
        private static readonly Stopwatch _sessionStopwatch = Stopwatch.StartNew();
        private static int _totalOperations;
        private static bool _isEnabled = true;

        /// <summary>
        /// Włącz/wyłącz profiler
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// Rozpoczyna pomiar operacji
        /// </summary>
        public static IDisposable MeasureOperation(string operationName)
        {
            if (!_isEnabled) return new NullDisposable();
            return new OperationMeasurement(operationName);
        }

        /// <summary>
        /// Rejestruje cache hit
        /// </summary>
        public static void RecordCacheHit(string cacheName)
        {
            if (!_isEnabled) return;
            _cacheHits.AddOrUpdate(cacheName, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Rejestruje cache miss
        /// </summary>
        public static void RecordCacheMiss(string cacheName)
        {
            if (!_isEnabled) return;
            _cacheMisses.AddOrUpdate(cacheName, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Rejestruje zakończenie operacji
        /// </summary>
        internal static void RecordTiming(string operationName, TimeSpan elapsed, int? recordCount = null)
        {
            Interlocked.Increment(ref _totalOperations);

            var timing = new OperationTiming
            {
                Timestamp = DateTime.Now,
                Elapsed = elapsed,
                RecordCount = recordCount
            };

            _timings.AddOrUpdate(
                operationName,
                new List<OperationTiming> { timing },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(timing);
                        // Keep only last 100 timings per operation
                        if (list.Count > 100)
                            list.RemoveAt(0);
                    }
                    return list;
                });
        }

        /// <summary>
        /// Generuje pełny raport wydajności
        /// </summary>
        public static string GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           RAPORT WYDAJNOŚCI - MODUŁ OPAKOWAŃ                    ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Czas sesji: {_sessionStopwatch.Elapsed:hh\\:mm\\:ss}                                          ║");
            sb.AppendLine($"║ Łączna liczba operacji: {_totalOperations,-10}                            ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║                    CZASY OPERACJI                                ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            foreach (var kvp in _timings.OrderByDescending(x => x.Value.Count))
            {
                var operationName = kvp.Key;
                List<OperationTiming> timingsCopy;
                lock (kvp.Value)
                {
                    timingsCopy = kvp.Value.ToList();
                }

                if (timingsCopy.Count == 0) continue;

                var avgMs = timingsCopy.Average(t => t.Elapsed.TotalMilliseconds);
                var minMs = timingsCopy.Min(t => t.Elapsed.TotalMilliseconds);
                var maxMs = timingsCopy.Max(t => t.Elapsed.TotalMilliseconds);
                var lastMs = timingsCopy.Last().Elapsed.TotalMilliseconds;
                var count = timingsCopy.Count;

                // Truncate operation name if too long
                var displayName = operationName.Length > 30
                    ? operationName.Substring(0, 27) + "..."
                    : operationName;

                sb.AppendLine($"║ {displayName,-30}                                    ║");
                sb.AppendLine($"║   Wywołań: {count,-6} | Ostatni: {lastMs,8:F1}ms | Średni: {avgMs,8:F1}ms   ║");
                sb.AppendLine($"║   Min: {minMs,8:F1}ms | Max: {maxMs,8:F1}ms                          ║");

                // Show record count if available
                var lastWithRecords = timingsCopy.LastOrDefault(t => t.RecordCount.HasValue);
                if (lastWithRecords != null)
                {
                    var recordsPerSec = lastWithRecords.RecordCount.Value / lastWithRecords.Elapsed.TotalSeconds;
                    sb.AppendLine($"║   Rekordów: {lastWithRecords.RecordCount,-6} | {recordsPerSec:F0} rek/s                        ║");
                }
                sb.AppendLine("║                                                                  ║");
            }

            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║                    STATYSTYKI CACHE                              ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            var allCacheNames = _cacheHits.Keys.Union(_cacheMisses.Keys).Distinct();
            foreach (var cacheName in allCacheNames)
            {
                _cacheHits.TryGetValue(cacheName, out var hits);
                _cacheMisses.TryGetValue(cacheName, out var misses);
                var total = hits + misses;
                var hitRate = total > 0 ? (double)hits / total * 100 : 0;

                var displayName = cacheName.Length > 25
                    ? cacheName.Substring(0, 22) + "..."
                    : cacheName;

                sb.AppendLine($"║ {displayName,-25} Hit: {hits,4} | Miss: {misses,4} | Rate: {hitRate,5:F1}% ║");
            }

            if (!allCacheNames.Any())
            {
                sb.AppendLine("║ (brak danych cache)                                              ║");
            }

            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║                    REKOMENDACJE                                  ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            // Generate recommendations based on data
            var recommendations = GenerateRecommendations();
            foreach (var rec in recommendations)
            {
                var lines = WrapText(rec, 62);
                foreach (var line in lines)
                {
                    sb.AppendLine($"║ • {line,-63}║");
                }
            }

            sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");

            return sb.ToString();
        }

        /// <summary>
        /// Generuje krótki raport do wyświetlenia w UI
        /// </summary>
        public static string GenerateShortReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PROFILER WYDAJNOŚCI ===\n");

            foreach (var kvp in _timings.OrderByDescending(x => x.Value.LastOrDefault()?.Elapsed.TotalMilliseconds ?? 0).Take(5))
            {
                List<OperationTiming> timingsCopy;
                lock (kvp.Value)
                {
                    timingsCopy = kvp.Value.ToList();
                }

                if (timingsCopy.Count == 0) continue;

                var last = timingsCopy.Last();
                var avg = timingsCopy.Average(t => t.Elapsed.TotalMilliseconds);

                sb.AppendLine($"{kvp.Key}:");
                sb.AppendLine($"  Ostatni: {last.Elapsed.TotalMilliseconds:F1}ms, Średni: {avg:F1}ms");
                if (last.RecordCount.HasValue)
                    sb.AppendLine($"  Rekordów: {last.RecordCount}");
                sb.AppendLine();
            }

            // Cache stats
            sb.AppendLine("=== CACHE ===");
            foreach (var cacheName in _cacheHits.Keys.Union(_cacheMisses.Keys))
            {
                _cacheHits.TryGetValue(cacheName, out var hits);
                _cacheMisses.TryGetValue(cacheName, out var misses);
                var total = hits + misses;
                var hitRate = total > 0 ? (double)hits / total * 100 : 0;
                sb.AppendLine($"{cacheName}: {hitRate:F0}% hit rate ({hits} hits, {misses} misses)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Czyści wszystkie zebrane dane
        /// </summary>
        public static void Reset()
        {
            _timings.Clear();
            _cacheHits.Clear();
            _cacheMisses.Clear();
            _totalOperations = 0;
        }

        private static List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();

            // Check cache hit rates
            foreach (var cacheName in _cacheHits.Keys.Union(_cacheMisses.Keys))
            {
                _cacheHits.TryGetValue(cacheName, out var hits);
                _cacheMisses.TryGetValue(cacheName, out var misses);
                var total = hits + misses;
                if (total > 5)
                {
                    var hitRate = (double)hits / total * 100;
                    if (hitRate < 50)
                    {
                        recommendations.Add($"Cache '{cacheName}' ma niski hit rate ({hitRate:F0}%). Rozważ wydłużenie TTL.");
                    }
                    else if (hitRate > 95)
                    {
                        recommendations.Add($"Cache '{cacheName}' działa świetnie ({hitRate:F0}% hit rate).");
                    }
                }
            }

            // Check slow operations
            foreach (var kvp in _timings)
            {
                List<OperationTiming> timingsCopy;
                lock (kvp.Value)
                {
                    timingsCopy = kvp.Value.ToList();
                }

                if (timingsCopy.Count >= 3)
                {
                    var avg = timingsCopy.Average(t => t.Elapsed.TotalMilliseconds);
                    if (avg > 2000)
                    {
                        recommendations.Add($"Operacja '{kvp.Key}' jest wolna (śr. {avg:F0}ms). Wymaga optymalizacji.");
                    }
                    else if (avg < 100)
                    {
                        recommendations.Add($"Operacja '{kvp.Key}' jest szybka (śr. {avg:F0}ms).");
                    }
                }
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Zbierz więcej danych aby uzyskać rekomendacje.");
            }

            return recommendations;
        }

        private static IEnumerable<string> WrapText(string text, int maxLength)
        {
            var words = text.Split(' ');
            var line = "";
            foreach (var word in words)
            {
                if ((line + " " + word).Trim().Length > maxLength)
                {
                    yield return line.Trim().PadRight(maxLength);
                    line = word;
                }
                else
                {
                    line = (line + " " + word).Trim();
                }
            }
            if (!string.IsNullOrEmpty(line))
                yield return line.Trim().PadRight(maxLength);
        }

        private class OperationTiming
        {
            public DateTime Timestamp { get; set; }
            public TimeSpan Elapsed { get; set; }
            public int? RecordCount { get; set; }
        }

        private class OperationMeasurement : IDisposable
        {
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            private int? _recordCount;

            public OperationMeasurement(string operationName)
            {
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void SetRecordCount(int count)
            {
                _recordCount = count;
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                RecordTiming(_operationName, _stopwatch.Elapsed, _recordCount);
            }
        }

        private class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
