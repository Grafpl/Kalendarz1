using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.MarketIntelligence.Database;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services.AI;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Serwis ładujący dane z internetu/bazy do ViewModelu Poranny Briefing
    /// Łączy dane z NewsFetchOrchestrator z modelami BriefingArticle
    /// </summary>
    public class BriefingDataLoaderService : IDisposable
    {
        private readonly NewsFetchOrchestrator _orchestrator;
        private readonly DatabaseSetup _databaseSetup;
        private readonly string _connectionString;

        private static BriefingDataLoaderService _instance;
        private static readonly object _lock = new object();

        public static BriefingDataLoaderService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new BriefingDataLoaderService();
                    }
                }
                return _instance;
            }
        }

        public BriefingDataLoaderService(string connectionString = null, string claudeApiKey = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

            _orchestrator = new NewsFetchOrchestrator(_connectionString, claudeApiKey);
            _databaseSetup = new DatabaseSetup(_connectionString);
        }

        /// <summary>
        /// Inicjalizuj bazę danych (utwórz tabele jeśli nie istnieją)
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            await _databaseSetup.EnsureTablesExistAsync();
        }

        /// <summary>
        /// Pobierz nowe dane z internetu i bazy danych
        /// </summary>
        public async Task<BriefingData> FetchNewDataAsync(
            bool fullFetch = false,
            CancellationToken ct = default,
            IProgress<FetchProgress> progress = null)
        {
            var result = new BriefingData();

            try
            {
                // Ensure database tables exist
                await InitializeDatabaseAsync();

                // Fetch from internet
                var options = fullFetch ? FetchOptions.Full : FetchOptions.Default;
                options.SaveToDatabase = true;

                var fetchResult = await _orchestrator.FetchAndAnalyzeAsync(options, ct, progress);

                if (fetchResult.Success)
                {
                    // Convert to BriefingArticle format
                    result.Articles = ConvertToArticles(fetchResult.Analyses, fetchResult.Articles);
                    result.Indicators = ConvertToIndicators(fetchResult.PoultryPrices, fetchResult.CommodityPrices);
                    result.HpaiAlerts = ConvertHpaiAlerts(fetchResult.HpaiAlerts);
                    result.Summary = fetchResult.Summary;
                    result.Statistics = fetchResult.Statistics;
                    result.Success = true;
                }
                else
                {
                    result.Error = fetchResult.Error;
                    result.Success = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BriefingDataLoader] Error: {ex.Message}");
                result.Error = ex.Message;
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Szybkie odświeżenie - tylko pobierz z bazy bez internetu
        /// </summary>
        public async Task<BriefingData> LoadFromDatabaseAsync(
            int days = 7,
            CancellationToken ct = default)
        {
            var result = new BriefingData();

            try
            {
                var storedArticles = await _orchestrator.GetStoredArticlesAsync(days, null, null, 100, ct);

                // Convert stored articles to BriefingArticle format
                result.Articles = storedArticles.Select(ConvertStoredArticle).ToList();
                result.Success = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BriefingDataLoader] Database load error: {ex.Message}");
                result.Error = ex.Message;
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Tylko pobierz HPAI alerts bez pełnego fetch
        /// </summary>
        public async Task<List<BriefingArticle>> FetchHpaiAlertsOnlyAsync(CancellationToken ct = default)
        {
            try
            {
                using var scraper = new WebScraperService();
                var alerts = await scraper.FetchHpaiAlertsAsync(ct);
                return ConvertHpaiAlerts(alerts);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BriefingDataLoader] HPAI fetch error: {ex.Message}");
                return new List<BriefingArticle>();
            }
        }

        #region Conversion Methods

        private List<BriefingArticle> ConvertToArticles(
            List<ArticleAnalysis> analyses,
            List<RawArticle> rawArticles)
        {
            var articles = new List<BriefingArticle>();
            var id = 1000; // Start from 1000 to avoid conflicts with seed data

            foreach (var analysis in analyses)
            {
                // Użyj nowych pól (WhoIs, WhatItMeansForPiorkowscy, StructuredActions) z fallback do starych
                var educationalContent = !string.IsNullOrEmpty(analysis.WhoIs)
                    ? analysis.WhoIs
                    : analysis.EducationalContent ?? "Brak informacji";

                var ceoAnalysis = !string.IsNullOrEmpty(analysis.WhatItMeansForPiorkowscy)
                    ? analysis.WhatItMeansForPiorkowscy
                    : analysis.CeoAnalysis ?? "Brak analizy";

                // Formatuj strukturalne akcje jeśli dostępne
                var ceoRecommendations = analysis.StructuredActions?.Any() == true
                    ? string.Join("\n", analysis.StructuredActions.Select(a =>
                        $"• [{a.Priorytet}] {a.Dzialanie}\n   Odpowiedzialny: {a.Odpowiedzialny} | Termin: {a.Termin}"))
                    : string.Join("\n", analysis.CeoRecommendations?.Select(r => $"• {r}") ?? Array.Empty<string>());

                var article = new BriefingArticle
                {
                    Id = id++,
                    Title = analysis.Article.Title,
                    ShortPreview = TruncateText(analysis.Summary ?? analysis.Article.Summary, 300),
                    FullContent = analysis.Summary ?? analysis.Article.Summary ?? analysis.Article.Title,
                    EducationalSection = educationalContent,
                    AiAnalysisCeo = ceoAnalysis,
                    AiAnalysisSales = analysis.SalesAnalysis ?? ceoAnalysis,
                    AiAnalysisBuyer = analysis.BuyerAnalysis ?? ceoAnalysis,
                    RecommendedActionsCeo = ceoRecommendations,
                    RecommendedActionsSales = string.Join("\n", analysis.SalesRecommendations?.Select(r => $"• {r}") ?? Array.Empty<string>()),
                    RecommendedActionsBuyer = string.Join("\n", analysis.BuyerRecommendations?.Select(r => $"• {r}") ?? Array.Empty<string>()),
                    Category = MapCategory(analysis.Category),
                    Source = analysis.Article.SourceName,
                    SourceUrl = analysis.Article.Url,
                    PublishDate = analysis.Article.PublishDate,
                    Severity = MapSeverity(analysis.Severity),
                    IsFeatured = analysis.Importance >= 8,
                    Tags = analysis.RelatedTopics ?? new List<string>()
                };

                articles.Add(article);
            }

            // Add raw articles without analysis
            foreach (var raw in rawArticles.Where(r =>
                !analyses.Any(a => a.Article.UrlHash == r.UrlHash)))
            {
                articles.Add(new BriefingArticle
                {
                    Id = id++,
                    Title = raw.Title,
                    ShortPreview = TruncateText(raw.Summary, 200),
                    FullContent = raw.Summary ?? raw.Title,
                    Category = raw.SourceCategory ?? "Info",
                    Source = raw.SourceName,
                    SourceUrl = raw.Url,
                    PublishDate = raw.PublishDate,
                    Severity = raw.RelevanceScore >= 15 ? SeverityLevel.Warning : SeverityLevel.Info,
                    Tags = raw.MatchedKeywords?.ToList() ?? new List<string>()
                });
            }

            return articles
                .OrderByDescending(a => a.Severity == SeverityLevel.Critical)
                .ThenByDescending(a => a.IsFeatured)
                .ThenByDescending(a => a.PublishDate)
                .ToList();
        }

        private BriefingArticle ConvertStoredArticle(StoredArticle stored)
        {
            return new BriefingArticle
            {
                Id = stored.Id,
                Title = stored.Title,
                ShortPreview = TruncateText(stored.Summary, 200),
                FullContent = stored.Summary ?? stored.Title,
                EducationalSection = stored.EducationalContent,
                AiAnalysisCeo = stored.CeoAnalysis,
                AiAnalysisSales = stored.SalesAnalysis,
                AiAnalysisBuyer = stored.BuyerAnalysis,
                Category = stored.Category,
                Source = stored.SourceName,
                SourceUrl = stored.Url,
                PublishDate = stored.PublishDate,
                Severity = MapSeverity(stored.Severity),
                IsFeatured = stored.RelevanceScore >= 20,
                Tags = stored.MatchedKeywords?.Split(',').ToList() ?? new List<string>()
            };
        }

        private List<PriceIndicator> ConvertToIndicators(
            List<PoultryPrice> poultryPrices,
            List<CommodityPrice> commodityPrices)
        {
            var indicators = new List<PriceIndicator>();

            // Poultry prices
            var skupPrice = poultryPrices.FirstOrDefault(p =>
                p.ProductName.ToLower().Contains("żywiec") ||
                p.ProductName.ToLower().Contains("skup"));
            if (skupPrice != null)
            {
                indicators.Add(new PriceIndicator
                {
                    Name = "SKUP",
                    Value = skupPrice.Price.ToString("F2"),
                    Unit = "zł/kg",
                    SubLabel = skupPrice.Source
                });
            }

            var tuszkaPrice = poultryPrices.FirstOrDefault(p =>
                p.ProductName.ToLower().Contains("tuszka"));
            if (tuszkaPrice != null)
            {
                indicators.Add(new PriceIndicator
                {
                    Name = "TUSZKA",
                    Value = tuszkaPrice.Price.ToString("F2"),
                    Unit = "zł/kg",
                    SubLabel = "hurt"
                });
            }

            var filetPrice = poultryPrices.FirstOrDefault(p =>
                p.ProductName.ToLower().Contains("filet") ||
                p.ProductName.ToLower().Contains("pierś"));
            if (filetPrice != null)
            {
                indicators.Add(new PriceIndicator
                {
                    Name = "FILET",
                    Value = filetPrice.Price.ToString("F2"),
                    Unit = "zł/kg",
                    SubLabel = "pierś"
                });
            }

            // Commodity prices
            var cornPrice = commodityPrices.FirstOrDefault(p =>
                p.Commodity.ToLower().Contains("kukurydz") ||
                p.Commodity.ToLower().Contains("pszenica"));
            if (cornPrice != null)
            {
                indicators.Add(new PriceIndicator
                {
                    Name = cornPrice.Commodity.ToUpper(),
                    Value = cornPrice.Price.ToString("F2"),
                    Unit = $"{cornPrice.Currency}/t",
                    SubLabel = cornPrice.Exchange
                });
            }

            return indicators;
        }

        private List<BriefingArticle> ConvertHpaiAlerts(List<HpaiAlert> alerts)
        {
            return alerts.Select((alert, i) => new BriefingArticle
            {
                Id = 9000 + i,
                Title = $"HPAI Alert: {alert.AlertType} - {alert.Location ?? alert.Voivodeship}",
                ShortPreview = alert.Title,
                FullContent = BuildHpaiContent(alert),
                EducationalSection = "HPAI (Highly Pathogenic Avian Influenza) - wysoce zjadliwa grypa ptaków. Choroba zakaźna o wysokiej śmiertelności w stadach drobiu.",
                AiAnalysisCeo = $"Alert HPAI w lokalizacji: {alert.Location}. {(alert.Voivodeship == "łódzkie" ? "UWAGA: Twój region!" : "Monitorować sytuację.")}",
                AiAnalysisSales = "Poinformować klientów o utrzymaniu wysokich standardów bioasekuracji.",
                AiAnalysisBuyer = $"Sprawdzić status hodowców w pobliżu: {alert.Location}. {(alert.BirdCount.HasValue ? $"Zagrożone: {alert.BirdCount:N0} szt." : "")}",
                Category = "HPAI",
                Source = "GLW",
                SourceUrl = alert.SourceUrl,
                PublishDate = alert.ReportDate,
                Severity = alert.Severity == "Critical" ? SeverityLevel.Critical : SeverityLevel.Warning,
                IsFeatured = alert.Voivodeship?.ToLower() == "łódzkie",
                Tags = new List<string> { "HPAI", alert.Voivodeship ?? "", alert.AlertType }
            }).ToList();
        }

        private string BuildHpaiContent(HpaiAlert alert)
        {
            var parts = new List<string>
            {
                $"Typ alertu: {alert.AlertType}",
                $"Data zgłoszenia: {alert.ReportDate:yyyy-MM-dd}"
            };

            if (!string.IsNullOrEmpty(alert.Voivodeship))
                parts.Add($"Województwo: {alert.Voivodeship}");
            if (!string.IsNullOrEmpty(alert.County))
                parts.Add($"Powiat: {alert.County}");
            if (!string.IsNullOrEmpty(alert.Municipality))
                parts.Add($"Gmina: {alert.Municipality}");
            if (alert.BirdCount.HasValue)
                parts.Add($"Liczba ptaków: {alert.BirdCount:N0}");
            if (alert.ZoneRadiusKm.HasValue)
                parts.Add($"Promień strefy: {alert.ZoneRadiusKm} km");

            return string.Join("\n", parts);
        }

        private string MapCategory(string category)
        {
            return category switch
            {
                "HPAI" => "HPAI",
                "Ceny" => "Ceny",
                "Konkurencja" => "Konkurencja",
                "Eksport" => "Eksport",
                "Import" => "Import",
                "Regulacje" => "Regulacje",
                "Pasze" => "Pasze",
                "Pogoda" => "Pogoda",
                "Klienci" => "Klienci",
                "Dotacje" => "Dotacje",
                _ => "Info"
            };
        }

        private SeverityLevel MapSeverity(string severity)
        {
            return severity?.ToLower() switch
            {
                "critical" => SeverityLevel.Critical,
                "warning" => SeverityLevel.Warning,
                "positive" => SeverityLevel.Positive,
                _ => SeverityLevel.Info
            };
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        #endregion

        public void Dispose()
        {
            _orchestrator?.Dispose();
        }
    }

    /// <summary>
    /// Wynik ładowania danych do briefingu
    /// </summary>
    public class BriefingData
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        public List<BriefingArticle> Articles { get; set; } = new();
        public List<PriceIndicator> Indicators { get; set; } = new();
        public List<BriefingArticle> HpaiAlerts { get; set; } = new();
        public DailySummary Summary { get; set; }
        public FetchStatistics Statistics { get; set; }
    }
}
