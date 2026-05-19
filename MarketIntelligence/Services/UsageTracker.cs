using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Persistent tracker zużycia API per dzień — Claude tokens + Perplexity queries.
    /// Plik: %LOCALAPPDATA%\Kalendarz1\MarketIntelligence\usage-YYYY-MM-DD.json
    /// Resetuje się o północy (nowy plik na nowy dzień).
    /// Używane przez DiagnosticsReportGenerator do pokazania kosztów dnia.
    /// </summary>
    public static class UsageTracker
    {
        private static readonly object _lock = new object();
        private static UsageDay _today;

        // Cennik Anthropic per 1M tokens (2026-05, USD)
        private const double HaikuInputPerMln = 1.00;
        private const double HaikuOutputPerMln = 5.00;
        private const double SonnetInputPerMln = 3.00;
        private const double SonnetOutputPerMln = 15.00;
        private const double SonnetCacheReadPerMln = 0.30;   // 10x taniej niż input
        private const double SonnetCacheCreatePerMln = 3.75; // 25% droższy niż input (jednorazowo)

        // Perplexity Sonar — przybliżony koszt per query (USD)
        private const double PerplexityCostPerQuery = 0.005;

        // PLN/USD konwersja (przybliżona)
        private const decimal PLNperUSD = 4.0m;

        private static string GetFilePath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "MarketIntelligence",
            $"usage-{DateTime.Today:yyyy-MM-dd}.json");

        public class UsageDay
        {
            public string Date { get; set; }
            public int HaikuInputTokens { get; set; }
            public int HaikuOutputTokens { get; set; }
            public int SonnetInputTokens { get; set; }
            public int SonnetOutputTokens { get; set; }
            public int SonnetCacheReadTokens { get; set; }
            public int SonnetCacheCreateTokens { get; set; }
            public int PerplexityQueries { get; set; }
            public int FetchCount { get; set; }
        }

        private static void EnsureLoaded()
        {
            lock (_lock)
            {
                if (_today != null && _today.Date == DateTime.Today.ToString("yyyy-MM-dd")) return;

                var path = GetFilePath();
                try
                {
                    if (File.Exists(path))
                    {
                        _today = JsonSerializer.Deserialize<UsageDay>(File.ReadAllText(path));
                    }
                }
                catch { }

                _today ??= new UsageDay { Date = DateTime.Today.ToString("yyyy-MM-dd") };
                if (_today.Date != DateTime.Today.ToString("yyyy-MM-dd"))
                    _today = new UsageDay { Date = DateTime.Today.ToString("yyyy-MM-dd") };
            }
        }

        private static void Save()
        {
            try
            {
                var path = GetFilePath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonSerializer.Serialize(_today));
            }
            catch (Exception ex) { Debug.WriteLine($"[UsageTracker] Save error: {ex.Message}"); }
        }

        /// <summary>Wywołane przez ClaudeAnalysisService po każdym udanym requeście.</summary>
        public static void TrackClaude(string model, int inputTokens, int outputTokens, int cacheRead = 0, int cacheCreate = 0)
        {
            lock (_lock)
            {
                EnsureLoaded();
                if (model.Contains("haiku"))
                {
                    _today.HaikuInputTokens += inputTokens;
                    _today.HaikuOutputTokens += outputTokens;
                }
                else // sonnet, opus, default
                {
                    _today.SonnetInputTokens += inputTokens;
                    _today.SonnetOutputTokens += outputTokens;
                    _today.SonnetCacheReadTokens += cacheRead;
                    _today.SonnetCacheCreateTokens += cacheCreate;
                }
                Save();
            }
        }

        /// <summary>Wywołane przez PerplexitySearchService po każdej udanej query.</summary>
        public static void TrackPerplexity()
        {
            lock (_lock)
            {
                EnsureLoaded();
                _today.PerplexityQueries++;
                Save();
            }
        }

        /// <summary>Wywołane przez NewsFetchOrchestrator na końcu każdego fetcha.</summary>
        public static void TrackFetch()
        {
            lock (_lock)
            {
                EnsureLoaded();
                _today.FetchCount++;
                Save();
            }
        }

        public static UsageDay GetToday()
        {
            lock (_lock) { EnsureLoaded(); return _today; }
        }

        /// <summary>Oblicza koszt dnia w PLN (przybliżony, na podstawie cennika 2026-05).</summary>
        public static (decimal HaikuPLN, decimal SonnetPLN, decimal PerplexityPLN, decimal TotalPLN) ComputeCostPLN()
        {
            var u = GetToday();
            var haikuUSD = (u.HaikuInputTokens / 1_000_000.0) * HaikuInputPerMln
                         + (u.HaikuOutputTokens / 1_000_000.0) * HaikuOutputPerMln;
            var sonnetUSD = (u.SonnetInputTokens / 1_000_000.0) * SonnetInputPerMln
                          + (u.SonnetOutputTokens / 1_000_000.0) * SonnetOutputPerMln
                          + (u.SonnetCacheReadTokens / 1_000_000.0) * SonnetCacheReadPerMln
                          + (u.SonnetCacheCreateTokens / 1_000_000.0) * SonnetCacheCreatePerMln;
            var pxUSD = u.PerplexityQueries * PerplexityCostPerQuery;

            return (
                (decimal)haikuUSD * PLNperUSD,
                (decimal)sonnetUSD * PLNperUSD,
                (decimal)pxUSD * PLNperUSD,
                ((decimal)(haikuUSD + sonnetUSD + pxUSD)) * PLNperUSD
            );
        }
    }
}
