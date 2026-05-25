using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Services.DataSources;
using Kalendarz1.MarketIntelligence.Services.AI;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Główny orkiestrator pobierania i analizy newsów
    /// Koordynuje RSS, scraping, filtrowanie i analizę AI
    /// </summary>
    public class NewsFetchOrchestrator : IDisposable
    {
        private readonly RssFeedService _rssFeedService;
        private readonly WebScraperService _webScraperService;
        private readonly PerplexitySearchService _perplexitySearchService;
        private readonly ContentFilterService _contentFilterService;
        private readonly ClaudeAnalysisService _claudeAnalysisService;
        private readonly ContextBuilderService _contextBuilderService;
        private readonly ContentEnrichmentService _contentEnrichmentService;

        private readonly string _connectionString;

        // Statistics
        public FetchStatistics LastFetchStats { get; private set; }

        public NewsFetchOrchestrator(string connectionString = null, string claudeApiKey = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;

            _rssFeedService = new RssFeedService();
            _webScraperService = new WebScraperService();
            _perplexitySearchService = new PerplexitySearchService();
            _contentFilterService = new ContentFilterService(_connectionString);
            _claudeAnalysisService = new ClaudeAnalysisService(claudeApiKey);
            _contextBuilderService = new ContextBuilderService(_connectionString);
            _contentEnrichmentService = new ContentEnrichmentService();
        }

        /// <summary>
        /// Uniwersalny watchdog: uruchamia work z hard cap timeoutu przez Task.WhenAny.
        /// Przy timeout RZECZYWIŚCIE anuluje wewnętrzny ct (per-stage CTS) — bez tego
        /// task wycieka: porzucony Perplexity nadal robi requesty w tle marnując budżet API.
        ///
        /// Twarda gwarancja: niezależnie od tego co dzieje się w środku work
        /// (DNS hang, ReadAsStringAsync nieprzerywalne, deadlock semafora, parsing CPU),
        /// metoda ZAWSZE zwraca w &lt;timeoutSec sekund.
        /// </summary>
        private async Task<T> RunWithTimeoutAsync<T>(
            string stageName, Func<CancellationToken, Task<T>> work, int timeoutSec, T fallback, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            Debug.WriteLine($"[Orchestrator] ▶ {stageName} (limit {timeoutSec}s)...");

            // Per-stage CTS linked z głównym — żeby przy timeout faktycznie anulować wewnętrzną pracę
            using var stageCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                var workTask = Task.Run(() => work(stageCts.Token));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSec), ct);
                var finished = await Task.WhenAny(workTask, timeoutTask);

                if (finished == timeoutTask && !ct.IsCancellationRequested)
                {
                    sw.Stop();
                    stageCts.Cancel(); // ŁOPATĄ ANULUJ! żeby task się zatrzymał, a nie wisiał w tle
                    Debug.WriteLine($"[Orchestrator] ⏱⏱ {stageName}: HARD TIMEOUT {timeoutSec}s — anuluję task ({sw.ElapsedMilliseconds}ms)");
                    return fallback;
                }

                ct.ThrowIfCancellationRequested();
                var result = await workTask;
                sw.Stop();
                Debug.WriteLine($"[Orchestrator] ✓ {stageName}: OK ({sw.ElapsedMilliseconds}ms)");
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Debug.WriteLine($"[Orchestrator] ⏹ {stageName}: anulowane przez użytkownika");
                throw;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Debug.WriteLine($"[Orchestrator] ⏱⏱ {stageName}: timeout (cooperative cancel), elapsed {sw.ElapsedMilliseconds}ms");
                return fallback;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Debug.WriteLine($"[Orchestrator] ❌ {stageName}: {ex.Message} ({sw.ElapsedMilliseconds}ms) — używam fallback");
                return fallback;
            }
        }

        /// <summary>Backward-compat overload bez CancellationToken w work (przyjmuje ct z zewnątrz przez closure).</summary>
        private Task<T> RunWithTimeoutAsync<T>(
            string stageName, Func<Task<T>> work, int timeoutSec, T fallback, CancellationToken ct)
            => RunWithTimeoutAsync(stageName, _ => work(), timeoutSec, fallback, ct);

        /// <summary>Wariant void (Task bez wyniku) dla operacji save-to-DB itd.</summary>
        private async Task RunWithTimeoutAsync(
            string stageName, Func<Task> work, int timeoutSec, CancellationToken ct)
        {
            await RunWithTimeoutAsync(stageName, async () => { await work(); return true; }, timeoutSec, false, ct);
        }

        /// <summary>
        /// Ranking tematyczny dla wyboru artykułów do głębokiej analizy AI (Sonnet).
        /// Preferencje Sergiusza: drób/HPAI/ceny/konkurencja/klienci PRZED zbożami/suszą/dopłatami.
        /// Wyższy = ważniejszy. Sortowane przed RelevanceScore.
        /// </summary>
        private static int PoultryTopicRank(RawArticle a)
        {
            var t = ((a.Title ?? "") + " " + (a.Summary ?? "") + " " + (a.SourceCategory ?? "")).ToLowerInvariant();

            // HPAI / choroby drobiu — najwyżej
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"hpai|grypa ptak|ptasia gryp|h5n|newcastle|rzekomy pomór|pomór drobiu"))
                return 100;
            // Drób bezpośrednio
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"drób|drobi|kurczak|brojler|tuszka|filet|ubojni"))
                return 90;
            // Ceny / skup mięsa-żywca
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"(cen|skup|notowani).*(drob|mięs|mies|żywiec|zywiec|kurcz)") ||
                System.Text.RegularExpressions.Regex.IsMatch(t, @"(żywiec|zywiec).*cen"))
                return 80;
            // Konkurencja
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"cedrob|drosed|superdrob|super drob|animex|indykpol|drobimex|adq"))
                return 70;
            // Klienci / sieci
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"biedronk|lidl|dino|carrefour|makro|auchan|kaufland|selgros"))
                return 60;
            // Import / zagrożenia handlowe
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"mercosur|brazyl|brf|jbs|seara|ukrain|mhp|import"))
                return 50;
            // Kategoria źródła drobiarska/mięsna
            if (a.SourceCategory == "Drób" || a.SourceCategory == "HPAI" || a.SourceCategory == "Mięso")
                return 40;
            // Pasze (koszt hodowcy) — niżej, ale wyżej niż czysta polityka/zboża ogólne
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"pasz|kukurydz|śrut|soj"))
                return 20;
            return 0;
        }

        #region Main Fetch Pipeline

        /// <summary>
        /// Wykonaj pełny cykl pobierania i analizy newsów
        /// </summary>
        public async Task<FetchResult> FetchAndAnalyzeAsync(
            FetchOptions options = null,
            CancellationToken ct = default,
            IProgress<FetchProgress> progress = null)
        {
            options ??= FetchOptions.Default;
            var result = new FetchResult { StartTime = DateTime.Now };
            var stats = new FetchStatistics();

            try
            {
                // 1. Report starting
                progress?.Report(new FetchProgress
                {
                    Stage = "Inicjalizacja",
                    Percent = 0,
                    Message = "Rozpoczynam pobieranie newsów..."
                });

                // 2. Fetch RSS feeds
                progress?.Report(new FetchProgress
                {
                    Stage = "RSS",
                    Percent = 10,
                    Message = "Pobieram artykuły z kanałów RSS..."
                });

                // Sub-progress per RSS source (slot 10-25%)
                var rssProgress = new Progress<string>(msg =>
                {
                    progress?.Report(new FetchProgress { Stage = "RSS", Percent = 15, Message = msg });
                });
                var rssArticles = await RunWithTimeoutAsync(
                    "RSS (40 źródeł)",
                    () => _rssFeedService.FetchAllSourcesAsync(ct, rssProgress),
                    timeoutSec: 90,
                    fallback: new List<RawArticle>(),
                    ct);
                stats.RssArticlesFetched = rssArticles.Count;

                Debug.WriteLine($"[Orchestrator] RSS: {rssArticles.Count} articles");

                // 3. Fetch scraped sources (if enabled)
                // Hard timeout 120s na CAŁY scraping — fail-safe gdyby wszystkie 25s per-source
                // timeouty zsumowały się powyżej rozsądnej granicy.
                var scrapedArticles = new List<RawArticle>();
                if (options.IncludeScrapingSources)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Scraping",
                        Percent = 25,
                        Message = "Pobieram dane ze stron internetowych..."
                    });

                    // Sub-progress: scraping ma slot 25-30%, raportuj per źródło
                    var scrapingProgress = new Progress<string>(msg =>
                    {
                        progress?.Report(new FetchProgress
                        {
                            Stage = "Scraping",
                            Percent = 27, // mid-range
                            Message = msg
                        });
                    });

                    using var scrapingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    scrapingCts.CancelAfter(TimeSpan.FromSeconds(120));

                    scrapedArticles = await RunWithTimeoutAsync(
                        "Scraping (7 źródeł)",
                        () => _webScraperService.FetchScrapingSourcesAsync(scrapingCts.Token, scrapingProgress),
                        timeoutSec: 120,
                        fallback: new List<RawArticle>(),
                        ct);
                    stats.ScrapedArticlesFetched = scrapedArticles.Count;

                    Debug.WriteLine($"[Orchestrator] Scraped: {scrapedArticles.Count} articles");
                }

                // 3.5 Perplexity AI internet search (if enabled)
                var perplexityArticles = new List<RawArticle>();
                if (options.UsePerplexitySearch && _perplexitySearchService.IsConfigured)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Perplexity AI",
                        Percent = 30,
                        Message = "Przeszukuję cały internet przez Perplexity AI..."
                    });

                    // Sub-progress per query (slot 30-40%)
                    var perplexityProgress = new Progress<string>(msg =>
                    {
                        progress?.Report(new FetchProgress { Stage = "Perplexity", Percent = 32, Message = msg });
                    });
                    // TRYB OSZCZĘDNY: Critical priority + bez INT = ~16 zapytań (zamiast 113 All).
                    // 16 × ~7s avg + 600ms delay = ~120s. 240s daje 2× margines.
                    // Koszt: 16 × $0.005 = $0.08/fetch (~30 gr).
                    var perplexityResult = await RunWithTimeoutAsync(
                        "Perplexity (16 zapytań Critical PL)",
                        stageCt => _perplexitySearchService.SearchPoultryNewsAsync(
                            includeInternational: false,
                            maxPriority: SearchPriority.Critical,
                            ct: stageCt,
                            progress: perplexityProgress),
                        timeoutSec: 240,
                        fallback: new PerplexityNewsSearchResult(),
                        ct);

                    // Convert to RawArticle format
                    perplexityArticles = _perplexitySearchService.ConvertToRawArticles(perplexityResult);

                    stats.PerplexityArticlesFetched = perplexityArticles.Count;
                    Debug.WriteLine($"[Orchestrator] Perplexity: {perplexityArticles.Count} articles from internet search " +
                        $"({perplexityResult.PolishNews.Count} PL, {perplexityResult.InternationalNews.Count} INT)");
                }

                // 4. Combine and deduplicate
                var allArticles = rssArticles
                    .Concat(scrapedArticles)
                    .Concat(perplexityArticles)  // Zastąpiło tavilyArticles
                    .GroupBy(a => a.UrlHash)
                    .Select(g => g.First())
                    .ToList();

                stats.TotalArticlesFetched = allArticles.Count;

                // 4.5 Translate English articles (Perplexity often returns EN from international sources)
                if (_claudeAnalysisService.IsConfigured)
                {
                    var englishArticles = allArticles.Where(a => a.Language?.ToLower() == "en").ToList();
                    if (englishArticles.Any())
                    {
                        progress?.Report(new FetchProgress
                        {
                            Stage = "Tłumaczenie",
                            Percent = 35,
                            Message = $"Tłumaczę {englishArticles.Count} artykułów angielskich..."
                        });

                        allArticles = await RunWithTimeoutAsync(
                            $"Tłumaczenie ({englishArticles.Count} EN)",
                            stageCt => _claudeAnalysisService.TranslateEnglishArticlesAsync(allArticles, stageCt),
                            timeoutSec: 120,
                            fallback: allArticles,
                            ct);
                        Debug.WriteLine($"[Orchestrator] Translated {englishArticles.Count} English articles");
                    }
                }

                // 5. Content filtering (local + optionally AI)
                progress?.Report(new FetchProgress
                {
                    Stage = "Filtrowanie",
                    Percent = 40,
                    Message = $"Filtruję {allArticles.Count} artykułów..."
                });

                var filteredArticles = await RunWithTimeoutAsync(
                    "Filter lokalny",
                    () => _contentFilterService.FilterArticlesAsync(allArticles),
                    timeoutSec: 30,
                    fallback: allArticles,
                    ct);
                stats.RelevantArticles = filteredArticles.Count;

                Debug.WriteLine($"[Orchestrator] Filtered: {filteredArticles.Count} relevant articles");

                // 6. Additional AI filtering (if API available)
                List<FilteredArticle> aiFiltered = null;
                if (options.UseAiFiltering && _claudeAnalysisService.IsConfigured)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "AI Filtrowanie",
                        Percent = 50,
                        Message = "Analizuję relevantność z AI..."
                    });

                    aiFiltered = await RunWithTimeoutAsync(
                        $"AI Filter Haiku ({filteredArticles.Count})",
                        stageCt => _claudeAnalysisService.QuickFilterArticlesAsync(filteredArticles, stageCt),
                        timeoutSec: 90,
                        fallback: new List<FilteredArticle>(),
                        ct);
                    if (aiFiltered != null && aiFiltered.Any())
                    {
                        filteredArticles = aiFiltered.Select(f => f.Article).ToList();
                        stats.AiFilteredArticles = filteredArticles.Count;
                    }
                }

                // 6.5 Content Enrichment - pobierz pełną treść artykułów
                if (options.UseContentEnrichment)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Wzbogacanie treści",
                        Percent = 52,
                        Message = $"Pobieram pełną treść {Math.Min(options.MaxArticlesToAnalyze, filteredArticles.Count)} artykułów..."
                    });

                    var enrichCount = Math.Min(options.MaxArticlesToAnalyze, filteredArticles.Count);
                    filteredArticles = await RunWithTimeoutAsync(
                        $"Content Enrichment ({enrichCount} art.)",
                        () => _contentEnrichmentService.EnrichArticlesAsync(filteredArticles, enrichCount, ct),
                        timeoutSec: 180,
                        fallback: filteredArticles,
                        ct);

                    var enrichedCount = filteredArticles.Count(a => !string.IsNullOrEmpty(a.FullContent) && a.FullContent.Length > 500);
                    stats.ArticlesEnriched = enrichedCount;
                    Debug.WriteLine($"[Orchestrator] Enriched {enrichedCount} articles with full content");
                }

                // 7. Get business context
                progress?.Report(new FetchProgress
                {
                    Stage = "Kontekst",
                    Percent = 55,
                    Message = "Pobieram kontekst biznesowy..."
                });

                var businessContext = await RunWithTimeoutAsync(
                    "BusinessContext (HANDEL+LibraNet)",
                    () => _contextBuilderService.GetContextAsync(false),
                    timeoutSec: 30,
                    fallback: new BusinessContext { Alerts = new ThreatsAndOpportunities() },
                    ct);

                // 8. Full AI analysis (top articles)
                var analyses = new List<ArticleAnalysis>();
                if (options.UseAiAnalysis && _claudeAnalysisService.IsConfigured)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "AI Analiza",
                        Percent = 60,
                        Message = $"Analizuję top {options.MaxArticlesToAnalyze} artykułów z AI..."
                    });

                    // 2026-05-25: najpierw ranking TEMATYCZNY (drób/HPAI/ceny/konkurencja/klienci),
                    // potem RelevanceScore. Bez tego sloty analizy (Sonnet) zżerały zboża/susza/dopłaty/nawozy.
                    var topArticles = filteredArticles
                        .OrderByDescending(a => PoultryTopicRank(a))
                        .ThenByDescending(a => a.RelevanceScore)
                        .Take(options.MaxArticlesToAnalyze)
                        .ToList();

                    // Sonnet 4.6 parallel x3 × ~30s/art = ~50s dla 5 art. 360s daje 7x margines.
                    analyses = await RunWithTimeoutAsync(
                        $"AI Analysis Sonnet ({topArticles.Count} art.)",
                        stageCt => _claudeAnalysisService.AnalyzeArticlesAsync(
                            topArticles, businessContext, options.MaxArticlesToAnalyze, stageCt),
                        timeoutSec: 360,
                        fallback: new List<ArticleAnalysis>(),
                        ct);
                    stats.ArticlesAnalyzed = analyses.Count;
                }
                else
                {
                    // Create stub analyses for display
                    analyses = filteredArticles.Take(options.MaxArticlesToAnalyze).Select(a => new ArticleAnalysis
                    {
                        Article = a,
                        Category = _contentFilterService.DetermineCategory(a),
                        Severity = _contentFilterService.DetermineSeverity(a),
                        Importance = Math.Min(10, a.RelevanceScore / 3),
                        CeoAnalysis = "Analiza AI niedostępna",
                        SalesAnalysis = "Analiza AI niedostępna",
                        BuyerAnalysis = "Analiza AI niedostępna",
                        EducationalContent = a.Summary,
                        AnalyzedAt = DateTime.Now
                    }).ToList();
                }

                // 9. Generate daily summary
                DailySummary summary = null;
                if (options.GenerateSummary && _claudeAnalysisService.IsConfigured && analyses.Any())
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Streszczenie",
                        Percent = 85,
                        Message = "Generuję poranne streszczenie..."
                    });

                    summary = await RunWithTimeoutAsync(
                        "Daily Summary Sonnet",
                        () => _claudeAnalysisService.GenerateDailySummaryAsync(analyses, businessContext, ct),
                        timeoutSec: 90,
                        fallback: (DailySummary)null,
                        ct);

                    // Zapisz Daily Summary do DB żeby historia była dostępna w One-pager.
                    // Bez tego intel_DailySummary ma 0 rekordów mimo generowania w każdym fetchu.
                    if (summary != null)
                    {
                        await RunWithTimeoutAsync(
                            "Save Daily Summary",
                            () => SaveDailySummaryAsync(summary, analyses, ct),
                            timeoutSec: 15,
                            ct);
                    }
                }

                // 10. Fetch HPAI alerts
                var hpaiAlerts = new List<HpaiAlert>();
                if (options.FetchHpaiAlerts)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "HPAI",
                        Percent = 90,
                        Message = "Sprawdzam alerty HPAI..."
                    });

                    hpaiAlerts = await RunWithTimeoutAsync(
                        "HPAI Alerts (GLW)",
                        () => _webScraperService.FetchHpaiAlertsAsync(ct),
                        timeoutSec: 75,
                        fallback: new List<HpaiAlert>(),
                        ct);
                    stats.HpaiAlertsFound = hpaiAlerts.Count;
                }

                // 11. Fetch prices
                var poultryPrices = new List<PoultryPrice>();
                var commodityPrices = new List<CommodityPrice>();
                if (options.FetchPrices)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Ceny",
                        Percent = 92,
                        Message = "Pobieram aktualne ceny..."
                    });

                    poultryPrices = await RunWithTimeoutAsync(
                        "Poultry Prices",
                        () => _webScraperService.FetchPoultryPricesAsync(ct),
                        timeoutSec: 60,
                        fallback: new List<PoultryPrice>(),
                        ct);
                    commodityPrices = await RunWithTimeoutAsync(
                        "Commodity Prices",
                        () => _webScraperService.FetchCommodityPricesAsync(ct),
                        timeoutSec: 60,
                        fallback: new List<CommodityPrice>(),
                        ct);
                }

                // 12. Save to database
                progress?.Report(new FetchProgress
                {
                    Stage = "Zapis",
                    Percent = 95,
                    Message = "Zapisuję do bazy danych..."
                });

                if (options.SaveToDatabase)
                {
                    await RunWithTimeoutAsync(
                        "Save to DB",
                        () => SaveResultsToDatabaseAsync(filteredArticles, analyses, hpaiAlerts, ct),
                        timeoutSec: 60,
                        ct);
                    await RunWithTimeoutAsync(
                        "Log fetch",
                        () => LogFetchAsync(stats, ct),
                        timeoutSec: 15,
                        ct);
                }

                // Track w UsageTracker — koszty dnia w Diagnostyce
                UsageTracker.TrackFetch();

                // 13. Complete
                progress?.Report(new FetchProgress
                {
                    Stage = "Zakończone",
                    Percent = 100,
                    Message = $"Pobrano {stats.RelevantArticles} artykułów, przeanalizowano {stats.ArticlesAnalyzed}"
                });

                result.Success = true;
                result.Articles = filteredArticles;
                result.Analyses = analyses;
                result.Summary = summary;
                result.HpaiAlerts = hpaiAlerts;
                result.PoultryPrices = poultryPrices;
                result.CommodityPrices = commodityPrices;
                result.BusinessContext = businessContext;
                result.EndTime = DateTime.Now;

                stats.TotalDuration = result.EndTime - result.StartTime;
                LastFetchStats = stats;
                result.Statistics = stats;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Error = "Operacja anulowana";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                Debug.WriteLine($"[Orchestrator] Error: {ex}");
            }

            return result;
        }

        /// <summary>
        /// Szybkie odświeżenie - tylko RSS bez pełnej analizy
        /// </summary>
        public async Task<FetchResult> QuickRefreshAsync(CancellationToken ct = default)
        {
            return await FetchAndAnalyzeAsync(new FetchOptions
            {
                IncludeScrapingSources = false,
                UseAiFiltering = false,
                UseAiAnalysis = false,
                GenerateSummary = false,
                FetchHpaiAlerts = true,
                FetchPrices = false,
                MaxArticlesToAnalyze = 3, // TRYB OSZCZĘDNY: szybki test = tylko 3 artykuły
                SaveToDatabase = false
            }, ct);
        }

        /// <summary>
        /// Pełne pobieranie z wszystkimi opcjami
        /// </summary>
        public async Task<FetchResult> FullFetchAsync(
            CancellationToken ct = default,
            IProgress<FetchProgress> progress = null)
        {
            return await FetchAndAnalyzeAsync(FetchOptions.Full, ct, progress);
        }

        #endregion

        #region Database Operations

        /// <summary>Zwraca lista kolumn tabeli (HashSet case-insensitive). Pusty = tabela nie istnieje.</summary>
        private async Task<HashSet<string>> GetTableColumnsAsync(SqlConnection conn, string tableName, CancellationToken ct)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t", conn);
                cmd.Parameters.AddWithValue("@t", tableName);
                cmd.CommandTimeout = 10;
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    result.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] GetTableColumns({tableName}) error: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Zwraca słownik COLUMN_NAME → max length (znaków). NVARCHAR(MAX) = int.MaxValue.
        /// Używane do trim'owania wartości stringów przed INSERT — chroni przed
        /// "String or binary data would be truncated" gdy Summary/CeoAnalysis itp.
        /// są długie a stara kolumna ma NVARCHAR(500).
        /// </summary>
        private async Task<Dictionary<string, int>> GetColumnMaxLengthsAsync(SqlConnection conn, string tableName, CancellationToken ct)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = new SqlCommand(@"
SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @t AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL", conn);
                cmd.Parameters.AddWithValue("@t", tableName);
                cmd.CommandTimeout = 10;
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var col = reader.GetString(0);
                    var len = reader.IsDBNull(1) ? -1 : reader.GetInt32(1);
                    // -1 oznacza NVARCHAR(MAX) — traktujemy jako int.MaxValue (no trim)
                    result[col] = len == -1 ? int.MaxValue : len;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] GetColumnMaxLengths({tableName}) error: {ex.Message}");
            }
            return result;
        }

        private static object TrimToColumn(string column, object value, Dictionary<string, int> maxLengths)
        {
            if (value is string s && maxLengths.TryGetValue(column, out var maxLen) && maxLen > 0 && maxLen != int.MaxValue)
            {
                if (s.Length > maxLen)
                {
                    return s.Substring(0, maxLen);
                }
            }
            return value;
        }

        private async Task SaveResultsToDatabaseAsync(
            List<RawArticle> articles,
            List<ArticleAnalysis> analyses,
            List<HpaiAlert> hpaiAlerts,
            CancellationToken ct)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                // SCHEMA-AWARE: jeśli stara wersja tabeli intel_Articles nie ma niektórych kolumn
                // (FetchedAt, UrlHash, SourceName itd.), buduj INSERT TYLKO z istniejących kolumn.
                // Bez tego stary schemat dawał 50× "Invalid column name" per fetch.
                var existingColumns = await GetTableColumnsAsync(conn, "intel_Articles", ct);
                if (existingColumns.Count == 0)
                {
                    Debug.WriteLine("[Orchestrator] ⚠ Tabela intel_Articles nie istnieje albo brak kolumn — pomijam zapis");
                }
                else
                {
                    Debug.WriteLine($"[Orchestrator] Schema intel_Articles: {existingColumns.Count} kolumn dostępnych");
                    var columnMaxLengths = await GetColumnMaxLengthsAsync(conn, "intel_Articles", ct);

                    int savedOk = 0, savedFail = 0;
                    foreach (var article in articles.Take(100))
                    {
                        var analysis = analyses.FirstOrDefault(a => a.Article.UrlHash == article.UrlHash);

                        // Wszystkie potencjalne pola — schema-aware filter pominie te których nie ma w DB
                        var allFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["SourceId"] = article.SourceId ?? "",
                            ["SourceName"] = article.SourceName ?? "",
                            ["Title"] = article.Title ?? "",
                            ["Url"] = article.Url ?? "",
                            ["UrlHash"] = article.UrlHash ?? "",
                            ["Summary"] = (object)article.Summary ?? DBNull.Value,
                            ["FullContent"] = (object)article.FullContent ?? DBNull.Value,
                            ["PublishDate"] = article.PublishDate,
                            ["FetchedAt"] = article.FetchedAt,
                            ["Language"] = article.Language ?? "pl",
                            ["Category"] = analysis?.Category ?? article.SourceCategory ?? "Info",
                            ["Severity"] = analysis?.Severity ?? "info",
                            ["RelevanceScore"] = article.RelevanceScore,
                            ["IsRelevant"] = article.IsRelevant,
                            ["MatchedKeywords"] = article.MatchedKeywords != null ? string.Join(",", article.MatchedKeywords) : "",
                            ["CeoAnalysis"] = (object)analysis?.CeoAnalysis ?? DBNull.Value,
                            ["SalesAnalysis"] = (object)analysis?.SalesAnalysis ?? DBNull.Value,
                            ["BuyerAnalysis"] = (object)analysis?.BuyerAnalysis ?? DBNull.Value,
                            ["EducationalContent"] = (object)analysis?.EducationalContent ?? DBNull.Value,
                            ["AnalyzedAt"] = (object)(analysis?.AnalyzedAt ?? (DateTime?)null) ?? DBNull.Value,
                            ["AiModel"] = (object)analysis?.Model ?? DBNull.Value,
                            // Stary schemat alternatywne nazwy
                            ["Body"] = (object)article.Summary ?? DBNull.Value,
                            ["Source"] = article.SourceName ?? "",
                            ["SourceUrl"] = article.Url ?? "",
                            ["AiAnalysis"] = (object)analysis?.CeoAnalysis ?? DBNull.Value,
                        };

                        // Filtruj tylko kolumny które ISTNIEJĄ w bazie
                        var usableFields = allFields
                            .Where(kvp => existingColumns.Contains(kvp.Key))
                            .ToList();

                        if (usableFields.Count == 0)
                        {
                            Debug.WriteLine("[Orchestrator] ⚠ Brak wspólnych kolumn — pomijam");
                            break;
                        }

                        // Buduj dynamiczny INSERT — z opcjonalnym WHERE NOT EXISTS jeśli mamy UrlHash
                        var columns = string.Join(", ", usableFields.Select(kvp => kvp.Key));
                        var paramsList = string.Join(", ", usableFields.Select(kvp => "@" + kvp.Key));
                        string sql;
                        if (existingColumns.Contains("UrlHash"))
                        {
                            sql = $@"IF NOT EXISTS (SELECT 1 FROM intel_Articles WHERE UrlHash = @UrlHash)
                                     BEGIN INSERT INTO intel_Articles ({columns}) VALUES ({paramsList}) END";
                        }
                        else
                        {
                            sql = $"INSERT INTO intel_Articles ({columns}) VALUES ({paramsList})";
                        }

                        using var cmd = new SqlCommand(sql, conn);
                        foreach (var kvp in usableFields)
                        {
                            // Trim string values do max-length kolumny żeby nie wpaść w
                            // "String or binary data would be truncated" przy starych kolumnach NVARCHAR(500)
                            var trimmed = TrimToColumn(kvp.Key, kvp.Value, columnMaxLengths);
                            cmd.Parameters.AddWithValue("@" + kvp.Key, trimmed ?? DBNull.Value);
                        }

                        try
                        {
                            await cmd.ExecuteNonQueryAsync(ct);
                            _contentFilterService.MarkAsProcessed(article.UrlHash);
                            savedOk++;
                        }
                        catch (SqlException ex)
                        {
                            savedFail++;
                            if (savedFail <= 3)
                                Debug.WriteLine($"[Orchestrator] Save article error: {ex.Message}");
                            else if (savedFail == 4)
                                Debug.WriteLine($"[Orchestrator] Tłumię kolejne błędy zapisu (pierwsze 3 widoczne wyżej)");
                        }
                    }
                    Debug.WriteLine($"[Orchestrator] Articles save: {savedOk} OK, {savedFail} fail");
                }

                // Save HPAI alerts
                foreach (var alert in hpaiAlerts)
                {
                    var sql = @"
                        IF NOT EXISTS (
                            SELECT 1 FROM intel_HpaiAlerts
                            WHERE Location = @Location AND ReportDate = @ReportDate
                        )
                        BEGIN
                            INSERT INTO intel_HpaiAlerts (
                                Title, AlertType, Location, Voivodeship, County, Municipality,
                                BirdCount, ZoneRadiusKm, ReportDate, SourceUrl, FetchedAt, Severity
                            ) VALUES (
                                @Title, @AlertType, @Location, @Voivodeship, @County, @Municipality,
                                @BirdCount, @ZoneRadiusKm, @ReportDate, @SourceUrl, @FetchedAt, @Severity
                            )
                        END";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Title", alert.Title ?? "");
                    cmd.Parameters.AddWithValue("@AlertType", alert.AlertType ?? "");
                    cmd.Parameters.AddWithValue("@Location", alert.Location ?? "");
                    cmd.Parameters.AddWithValue("@Voivodeship", (object)alert.Voivodeship ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@County", (object)alert.County ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Municipality", (object)alert.Municipality ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BirdCount", (object)alert.BirdCount ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ZoneRadiusKm", (object)alert.ZoneRadiusKm ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReportDate", alert.ReportDate);
                    cmd.Parameters.AddWithValue("@SourceUrl", alert.SourceUrl ?? "");
                    cmd.Parameters.AddWithValue("@FetchedAt", alert.FetchedAt);
                    cmd.Parameters.AddWithValue("@Severity", alert.Severity);

                    try
                    {
                        await cmd.ExecuteNonQueryAsync(ct);
                    }
                    catch (SqlException ex)
                    {
                        Debug.WriteLine($"[Orchestrator] Error saving HPAI alert: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[Orchestrator] Saved {articles.Count} articles, {hpaiAlerts.Count} HPAI alerts");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] Database save error: {ex.Message}");
            }
        }

        /// <summary>
        /// Zapisz Daily Summary do intel_DailySummary. UPSERT po SummaryDate
        /// — jeden rekord per dzień, kolejne fetche tego samego dnia nadpisują.
        /// </summary>
        private async Task SaveDailySummaryAsync(DailySummary summary, List<ArticleAnalysis> analyses, CancellationToken ct)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                var critical = analyses.Count(a => a.Severity == "critical");
                var warning = analyses.Count(a => a.Severity == "warning");
                var positive = analyses.Count(a => a.Severity == "positive");

                var topAlertsJson = System.Text.Json.JsonSerializer.Serialize(summary.TopAlerts ?? new List<Alert>());
                var actionItemsJson = System.Text.Json.JsonSerializer.Serialize(summary.ActionItems ?? new List<ActionItem>());

                var sql = @"
MERGE intel_DailySummary AS target
USING (SELECT @SummaryDate AS SummaryDate) AS source
ON target.SummaryDate = source.SummaryDate
WHEN MATCHED THEN UPDATE SET
    Headline = @Headline,
    CeoSummary = @CeoSummary,
    SalesSummary = @SalesSummary,
    BuyerSummary = @BuyerSummary,
    MarketMood = @MarketMood,
    MarketMoodReason = @MarketMoodReason,
    WeeklyOutlook = @WeeklyOutlook,
    TopAlerts = @TopAlerts,
    ActionItems = @ActionItems,
    ArticlesAnalyzed = @ArticlesAnalyzed,
    CriticalCount = @CriticalCount,
    WarningCount = @WarningCount,
    PositiveCount = @PositiveCount,
    GeneratedAt = @GeneratedAt,
    AiModel = @AiModel
WHEN NOT MATCHED THEN INSERT (
    SummaryDate, Headline, CeoSummary, SalesSummary, BuyerSummary,
    MarketMood, MarketMoodReason, WeeklyOutlook,
    TopAlerts, ActionItems,
    ArticlesAnalyzed, CriticalCount, WarningCount, PositiveCount,
    GeneratedAt, AiModel
) VALUES (
    @SummaryDate, @Headline, @CeoSummary, @SalesSummary, @BuyerSummary,
    @MarketMood, @MarketMoodReason, @WeeklyOutlook,
    @TopAlerts, @ActionItems,
    @ArticlesAnalyzed, @CriticalCount, @WarningCount, @PositiveCount,
    @GeneratedAt, @AiModel
);";

                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@SummaryDate", summary.Date.Date);
                cmd.Parameters.AddWithValue("@Headline", (object)summary.Headline ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CeoSummary", (object)summary.CeoSummary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SalesSummary", (object)summary.SalesSummary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BuyerSummary", (object)summary.BuyerSummary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MarketMood", (object)summary.MarketMood ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MarketMoodReason", (object)summary.MarketMoodReason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WeeklyOutlook", (object)summary.WeeklyOutlook ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TopAlerts", topAlertsJson);
                cmd.Parameters.AddWithValue("@ActionItems", actionItemsJson);
                cmd.Parameters.AddWithValue("@ArticlesAnalyzed", summary.ArticlesAnalyzed);
                cmd.Parameters.AddWithValue("@CriticalCount", critical);
                cmd.Parameters.AddWithValue("@WarningCount", warning);
                cmd.Parameters.AddWithValue("@PositiveCount", positive);
                cmd.Parameters.AddWithValue("@GeneratedAt", summary.GeneratedAt);
                cmd.Parameters.AddWithValue("@AiModel", "claude-sonnet-4-6");

                await cmd.ExecuteNonQueryAsync(ct);
                Debug.WriteLine($"[Orchestrator] ✓ DailySummary saved for {summary.Date:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] SaveDailySummary error: {ex.Message}");
            }
        }

        private async Task LogFetchAsync(FetchStatistics stats, CancellationToken ct)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                var sql = @"
                    INSERT INTO intel_FetchLog (
                        FetchTime, RssArticles, ScrapedArticles, TotalArticles,
                        RelevantArticles, AnalyzedArticles, HpaiAlerts,
                        DurationMs, Success, ErrorMessage
                    ) VALUES (
                        @FetchTime, @RssArticles, @ScrapedArticles, @TotalArticles,
                        @RelevantArticles, @AnalyzedArticles, @HpaiAlerts,
                        @DurationMs, @Success, @ErrorMessage
                    )";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FetchTime", DateTime.Now);
                cmd.Parameters.AddWithValue("@RssArticles", stats.RssArticlesFetched);
                cmd.Parameters.AddWithValue("@ScrapedArticles", stats.ScrapedArticlesFetched);
                cmd.Parameters.AddWithValue("@TotalArticles", stats.TotalArticlesFetched);
                cmd.Parameters.AddWithValue("@RelevantArticles", stats.RelevantArticles);
                cmd.Parameters.AddWithValue("@AnalyzedArticles", stats.ArticlesAnalyzed);
                cmd.Parameters.AddWithValue("@HpaiAlerts", stats.HpaiAlertsFound);
                cmd.Parameters.AddWithValue("@DurationMs", (int)stats.TotalDuration.TotalMilliseconds);
                cmd.Parameters.AddWithValue("@Success", true);
                cmd.Parameters.AddWithValue("@ErrorMessage", DBNull.Value);

                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] Error logging fetch: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobierz artykuły z bazy danych
        /// </summary>
        public async Task<List<StoredArticle>> GetStoredArticlesAsync(
            int days = 7,
            string category = null,
            string severity = null,
            int limit = 100,
            CancellationToken ct = default)
        {
            var articles = new List<StoredArticle>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                var sql = @"
                    SELECT TOP (@Limit)
                        Id, SourceId, SourceName, Title, Url, UrlHash, Summary,
                        PublishDate, FetchedAt, Language, Category, Severity,
                        RelevanceScore, IsRelevant, MatchedKeywords,
                        CeoAnalysis, SalesAnalysis, BuyerAnalysis, EducationalContent,
                        AnalyzedAt
                    FROM intel_Articles
                    WHERE FetchedAt >= DATEADD(day, -@Days, GETDATE())
                      AND (@Category IS NULL OR Category = @Category)
                      AND (@Severity IS NULL OR Severity = @Severity)
                    ORDER BY RelevanceScore DESC, PublishDate DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                cmd.Parameters.AddWithValue("@Days", days);
                cmd.Parameters.AddWithValue("@Category", (object)category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Severity", (object)severity ?? DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    articles.Add(new StoredArticle
                    {
                        Id = reader.GetInt32(0),
                        SourceId = reader.GetString(1),
                        SourceName = reader.GetString(2),
                        Title = reader.GetString(3),
                        Url = reader.GetString(4),
                        UrlHash = reader.GetString(5),
                        Summary = reader.IsDBNull(6) ? null : reader.GetString(6),
                        PublishDate = reader.GetDateTime(7),
                        FetchedAt = reader.GetDateTime(8),
                        Language = reader.GetString(9),
                        Category = reader.GetString(10),
                        Severity = reader.GetString(11),
                        RelevanceScore = reader.GetInt32(12),
                        IsRelevant = reader.GetBoolean(13),
                        MatchedKeywords = reader.IsDBNull(14) ? null : reader.GetString(14),
                        CeoAnalysis = reader.IsDBNull(15) ? null : reader.GetString(15),
                        SalesAnalysis = reader.IsDBNull(16) ? null : reader.GetString(16),
                        BuyerAnalysis = reader.IsDBNull(17) ? null : reader.GetString(17),
                        EducationalContent = reader.IsDBNull(18) ? null : reader.GetString(18),
                        AnalyzedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] Error loading stored articles: {ex.Message}");
            }

            return articles;
        }

        #endregion

        public void Dispose()
        {
            _rssFeedService?.Dispose();
            _webScraperService?.Dispose();
            _perplexitySearchService?.Dispose();  // Zastąpiło _tavilySearchService
            _claudeAnalysisService?.Dispose();
            _contentEnrichmentService?.Dispose();
        }
    }

    #region Models

    public class FetchOptions
    {
        public bool IncludeScrapingSources { get; set; } = true;
        public bool UsePerplexitySearch { get; set; } = true;  // Przeszukiwanie całego internetu przez Perplexity AI
        public bool UseContentEnrichment { get; set; } = true;  // Wzbogacanie artykułów o pełną treść (web scraping)
        public bool UseAiFiltering { get; set; } = true;
        public bool UseAiAnalysis { get; set; } = true;
        public bool GenerateSummary { get; set; } = true;
        public bool FetchHpaiAlerts { get; set; } = true;
        public bool FetchPrices { get; set; } = true;
        public bool SaveToDatabase { get; set; } = true;
        // 2026-05-25: 5 → 15 — Sergiusz chce analizę AI dla "wszystkich newsów".
        // 15 to praktyczny sweet-spot: 15/3 concurrent × ~25s = ~125s (pod watchdog 180s).
        // Literalnie "wszystkie" (40-60) = 5-8 min fetch + ryzyko timeout, więc cap na 15.
        public int MaxArticlesToAnalyze { get; set; } = 15;

        public static FetchOptions Default => new();

        public static FetchOptions Full => new()
        {
            IncludeScrapingSources = true,
            UsePerplexitySearch = true,
            UseContentEnrichment = true,
            UseAiFiltering = true,
            UseAiAnalysis = true,
            GenerateSummary = true,
            FetchHpaiAlerts = true,
            FetchPrices = true,
            SaveToDatabase = true,
            MaxArticlesToAnalyze = 15 // 2026-05-25: analiza AI dla większości newsów (sweet-spot pod watchdog)
        };

        public static FetchOptions Economy => new()
        {
            IncludeScrapingSources = true,
            UsePerplexitySearch = true,
            UseContentEnrichment = true,
            UseAiFiltering = true,
            UseAiAnalysis = true,
            GenerateSummary = true,
            FetchHpaiAlerts = true,
            FetchPrices = true,
            SaveToDatabase = true,
            MaxArticlesToAnalyze = 5
        };

        public static FetchOptions Quick => new()
        {
            IncludeScrapingSources = false,
            UsePerplexitySearch = false,
            UseAiFiltering = false,
            UseAiAnalysis = false,
            GenerateSummary = false,
            FetchHpaiAlerts = true,
            FetchPrices = false,
            SaveToDatabase = false,
            MaxArticlesToAnalyze = 3 // TRYB OSZCZĘDNY: Quick = tylko 3 artykuły
        };
    }

    public class FetchResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public List<RawArticle> Articles { get; set; } = new();
        public List<ArticleAnalysis> Analyses { get; set; } = new();
        public DailySummary Summary { get; set; }
        public List<HpaiAlert> HpaiAlerts { get; set; } = new();
        public List<PoultryPrice> PoultryPrices { get; set; } = new();
        public List<CommodityPrice> CommodityPrices { get; set; } = new();
        public BusinessContext BusinessContext { get; set; }
        public FetchStatistics Statistics { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
    }

    public class FetchStatistics
    {
        public int RssArticlesFetched { get; set; }
        public int ScrapedArticlesFetched { get; set; }
        public int PerplexityArticlesFetched { get; set; }  // Z przeszukiwania internetu przez Perplexity
        public int TotalArticlesFetched { get; set; }
        public int RelevantArticles { get; set; }
        public int AiFilteredArticles { get; set; }
        public int ArticlesEnriched { get; set; }  // Artykuły wzbogacone o pełną treść
        public int ArticlesAnalyzed { get; set; }
        public int HpaiAlertsFound { get; set; }
        public TimeSpan TotalDuration { get; set; }
    }

    public class FetchProgress
    {
        public string Stage { get; set; }
        public int Percent { get; set; }
        public string Message { get; set; }
    }

    public class StoredArticle
    {
        public int Id { get; set; }
        public string SourceId { get; set; }
        public string SourceName { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string UrlHash { get; set; }
        public string Summary { get; set; }
        public DateTime PublishDate { get; set; }
        public DateTime FetchedAt { get; set; }
        public string Language { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public int RelevanceScore { get; set; }
        public bool IsRelevant { get; set; }
        public string MatchedKeywords { get; set; }
        public string CeoAnalysis { get; set; }
        public string SalesAnalysis { get; set; }
        public string BuyerAnalysis { get; set; }
        public string EducationalContent { get; set; }
        public DateTime? AnalyzedAt { get; set; }
    }

    #endregion
}
