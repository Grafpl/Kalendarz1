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
        private readonly TavilySearchService _tavilySearchService;
        private readonly ContentFilterService _contentFilterService;
        private readonly ClaudeAnalysisService _claudeAnalysisService;
        private readonly ContextBuilderService _contextBuilderService;
        private readonly ContentEnrichmentService _contentEnrichmentService;

        private readonly string _connectionString;

        // Statistics
        public FetchStatistics LastFetchStats { get; private set; }

        public NewsFetchOrchestrator(string connectionString = null, string claudeApiKey = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

            _rssFeedService = new RssFeedService();
            _webScraperService = new WebScraperService();
            _tavilySearchService = new TavilySearchService();
            _contentFilterService = new ContentFilterService(_connectionString);
            _claudeAnalysisService = new ClaudeAnalysisService(claudeApiKey);
            _contextBuilderService = new ContextBuilderService(_connectionString);
            _contentEnrichmentService = new ContentEnrichmentService();
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

                var rssArticles = await _rssFeedService.FetchAllSourcesAsync(ct);
                stats.RssArticlesFetched = rssArticles.Count;

                Debug.WriteLine($"[Orchestrator] RSS: {rssArticles.Count} articles");

                // 3. Fetch scraped sources (if enabled)
                var scrapedArticles = new List<RawArticle>();
                if (options.IncludeScrapingSources)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Scraping",
                        Percent = 25,
                        Message = "Pobieram dane ze stron internetowych..."
                    });

                    scrapedArticles = await _webScraperService.FetchScrapingSourcesAsync(ct);
                    stats.ScrapedArticlesFetched = scrapedArticles.Count;

                    Debug.WriteLine($"[Orchestrator] Scraped: {scrapedArticles.Count} articles");
                }

                // 3.5 Tavily internet search (if enabled)
                var tavilyArticles = new List<RawArticle>();
                if (options.UseTavilySearch && _tavilySearchService.IsConfigured)
                {
                    progress?.Report(new FetchProgress
                    {
                        Stage = "Tavily",
                        Percent = 30,
                        Message = "Przeszukuję cały internet przez Tavily AI..."
                    });

                    var tavilyResult = await _tavilySearchService.SearchPoultryNewsAsync(
                        includeInternational: true, ct: ct);

                    // Convert to RawArticle format
                    tavilyArticles = _tavilySearchService.ConvertToRawArticles(
                        new TavilySearchResult
                        {
                            Success = true,
                            Results = tavilyResult.PolishNews.Concat(tavilyResult.InternationalNews).ToList()
                        });

                    stats.TavilyArticlesFetched = tavilyArticles.Count;
                    Debug.WriteLine($"[Orchestrator] Tavily: {tavilyArticles.Count} articles from internet search");
                }

                // 4. Combine and deduplicate
                var allArticles = rssArticles
                    .Concat(scrapedArticles)
                    .Concat(tavilyArticles)
                    .GroupBy(a => a.UrlHash)
                    .Select(g => g.First())
                    .ToList();

                stats.TotalArticlesFetched = allArticles.Count;

                // 4.5 Translate English articles (Tavily often returns EN)
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

                        allArticles = await _claudeAnalysisService.TranslateEnglishArticlesAsync(allArticles, ct);
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

                var filteredArticles = await _contentFilterService.FilterArticlesAsync(allArticles);
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

                    aiFiltered = await _claudeAnalysisService.QuickFilterArticlesAsync(filteredArticles, ct);
                    filteredArticles = aiFiltered.Select(f => f.Article).ToList();
                    stats.AiFilteredArticles = filteredArticles.Count;
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

                    filteredArticles = await _contentEnrichmentService.EnrichArticlesAsync(
                        filteredArticles,
                        Math.Min(options.MaxArticlesToAnalyze, filteredArticles.Count),
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

                var businessContext = await _contextBuilderService.GetContextAsync();

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

                    var topArticles = filteredArticles
                        .OrderByDescending(a => a.RelevanceScore)
                        .Take(options.MaxArticlesToAnalyze)
                        .ToList();

                    analyses = await _claudeAnalysisService.AnalyzeArticlesAsync(
                        topArticles, businessContext, options.MaxArticlesToAnalyze, ct);

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

                    summary = await _claudeAnalysisService.GenerateDailySummaryAsync(analyses, businessContext, ct);
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

                    hpaiAlerts = await _webScraperService.FetchHpaiAlertsAsync(ct);
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

                    poultryPrices = await _webScraperService.FetchPoultryPricesAsync(ct);
                    commodityPrices = await _webScraperService.FetchCommodityPricesAsync(ct);
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
                    await SaveResultsToDatabaseAsync(filteredArticles, analyses, hpaiAlerts, ct);
                    await LogFetchAsync(stats, ct);
                }

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
                MaxArticlesToAnalyze = 10,
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

                // Save articles
                foreach (var article in articles.Take(100)) // Limit to prevent overflow
                {
                    var analysis = analyses.FirstOrDefault(a => a.Article.UrlHash == article.UrlHash);

                    var sql = @"
                        IF NOT EXISTS (SELECT 1 FROM intel_Articles WHERE UrlHash = @UrlHash)
                        BEGIN
                            INSERT INTO intel_Articles (
                                SourceId, SourceName, Title, Url, UrlHash, Summary,
                                PublishDate, FetchedAt, Language, Category, Severity,
                                RelevanceScore, IsRelevant, MatchedKeywords,
                                CeoAnalysis, SalesAnalysis, BuyerAnalysis, EducationalContent,
                                AnalyzedAt, AiModel
                            ) VALUES (
                                @SourceId, @SourceName, @Title, @Url, @UrlHash, @Summary,
                                @PublishDate, @FetchedAt, @Language, @Category, @Severity,
                                @RelevanceScore, @IsRelevant, @MatchedKeywords,
                                @CeoAnalysis, @SalesAnalysis, @BuyerAnalysis, @EducationalContent,
                                @AnalyzedAt, @AiModel
                            )
                        END";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@SourceId", article.SourceId ?? "");
                    cmd.Parameters.AddWithValue("@SourceName", article.SourceName ?? "");
                    cmd.Parameters.AddWithValue("@Title", article.Title ?? "");
                    cmd.Parameters.AddWithValue("@Url", article.Url ?? "");
                    cmd.Parameters.AddWithValue("@UrlHash", article.UrlHash ?? "");
                    cmd.Parameters.AddWithValue("@Summary", (object)article.Summary ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PublishDate", article.PublishDate);
                    cmd.Parameters.AddWithValue("@FetchedAt", article.FetchedAt);
                    cmd.Parameters.AddWithValue("@Language", article.Language ?? "pl");
                    cmd.Parameters.AddWithValue("@Category", analysis?.Category ?? article.SourceCategory ?? "Info");
                    cmd.Parameters.AddWithValue("@Severity", analysis?.Severity ?? "info");
                    cmd.Parameters.AddWithValue("@RelevanceScore", article.RelevanceScore);
                    cmd.Parameters.AddWithValue("@IsRelevant", article.IsRelevant);
                    cmd.Parameters.AddWithValue("@MatchedKeywords",
                        article.MatchedKeywords != null ? string.Join(",", article.MatchedKeywords) : "");
                    cmd.Parameters.AddWithValue("@CeoAnalysis", (object)analysis?.CeoAnalysis ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SalesAnalysis", (object)analysis?.SalesAnalysis ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BuyerAnalysis", (object)analysis?.BuyerAnalysis ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EducationalContent", (object)analysis?.EducationalContent ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@AnalyzedAt", analysis?.AnalyzedAt ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@AiModel", (object)analysis?.Model ?? DBNull.Value);

                    try
                    {
                        await cmd.ExecuteNonQueryAsync(ct);
                        _contentFilterService.MarkAsProcessed(article.UrlHash);
                    }
                    catch (SqlException ex)
                    {
                        Debug.WriteLine($"[Orchestrator] Error saving article: {ex.Message}");
                    }
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
            _tavilySearchService?.Dispose();
            _claudeAnalysisService?.Dispose();
            _contentEnrichmentService?.Dispose();
        }
    }

    #region Models

    public class FetchOptions
    {
        public bool IncludeScrapingSources { get; set; } = true;
        public bool UseTavilySearch { get; set; } = true;  // Przeszukiwanie całego internetu
        public bool UseContentEnrichment { get; set; } = true;  // Wzbogacanie artykułów o pełną treść (web scraping)
        public bool UseAiFiltering { get; set; } = true;
        public bool UseAiAnalysis { get; set; } = true;
        public bool GenerateSummary { get; set; } = true;
        public bool FetchHpaiAlerts { get; set; } = true;
        public bool FetchPrices { get; set; } = true;
        public bool SaveToDatabase { get; set; } = true;
        public int MaxArticlesToAnalyze { get; set; } = 20;

        public static FetchOptions Default => new();

        public static FetchOptions Full => new()
        {
            IncludeScrapingSources = true,
            UseTavilySearch = true,
            UseContentEnrichment = true,
            UseAiFiltering = true,
            UseAiAnalysis = true,
            GenerateSummary = true,
            FetchHpaiAlerts = true,
            FetchPrices = true,
            SaveToDatabase = true,
            MaxArticlesToAnalyze = 25
        };

        public static FetchOptions Quick => new()
        {
            IncludeScrapingSources = false,
            UseTavilySearch = false,
            UseAiFiltering = false,
            UseAiAnalysis = false,
            GenerateSummary = false,
            FetchHpaiAlerts = true,
            FetchPrices = false,
            SaveToDatabase = false,
            MaxArticlesToAnalyze = 10
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
        public int TavilyArticlesFetched { get; set; }  // Z przeszukiwania internetu
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
