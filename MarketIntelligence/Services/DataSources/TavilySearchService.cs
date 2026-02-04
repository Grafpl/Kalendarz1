using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do przeszukiwania całego internetu przez Tavily API
    /// Tavily jest zoptymalizowany dla agentów AI - zwraca czyste, relevantne wyniki
    /// </summary>
    public class TavilySearchService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string TavilyApiUrl = "https://api.tavily.com/search";

        // Rate limiting
        private readonly SemaphoreSlim _rateLimiter;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _minRequestInterval = TimeSpan.FromMilliseconds(500);

        public TavilySearchService(string apiKey = null)
        {
            _apiKey = apiKey
                ?? ConfigurationManager.AppSettings["TavilyApiKey"]
                ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY");

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            _rateLimiter = new SemaphoreSlim(1);
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        #region Predefined Search Queries for Poultry Industry

        /// <summary>
        /// Predefiniowane zapytania dla branży drobiarskiej
        /// </summary>
        public static class PoultryQueries
        {
            // HPAI / Grypa ptaków
            public const string HpaiPoland = "HPAI ptasia grypa Polska ogniska 2024 2025 2026";
            public const string HpaiEurope = "avian influenza HPAI Europe outbreaks poultry";
            public const string HpaiGlobal = "bird flu HPAI global outbreak news";

            // Ceny i rynek
            public const string PoultryPricesPoland = "ceny drobiu kurczak Polska skup hurt 2026";
            public const string PoultryPricesEU = "chicken prices Europe wholesale market";
            public const string BroilerPrices = "broiler chicken prices market report";

            // Eksport/Import
            public const string PoultryExportPoland = "eksport drobiu Polska mięso kurczaka";
            public const string PoultryTradeEU = "poultry trade EU import export statistics";
            public const string BrazilPoultryExport = "Brazil chicken export world market";

            // Konkurencja
            public const string CedrobNews = "Cedrob drób Polska aktualności";
            public const string PolishPoultryCompanies = "polskie firmy drobiarskie ubojnie";

            // Pasze
            public const string FeedPricesPoland = "ceny pasz kukurydza pszenica Polska";
            public const string CornWheatPrices = "corn wheat prices MATIF futures";
            public const string SoybeanPrices = "soybean meal prices feed";

            // Sieci handlowe
            public const string RetailPoultryPrices = "Biedronka Lidl ceny kurczaka promocje";

            // Regulacje
            public const string PoultryRegulationsEU = "EU poultry regulations veterinary law";
            public const string PoultrySubsidiesPoland = "dotacje ARiMR drób hodowla 2026";

            // Ogólne branżowe
            public const string PoultryIndustryNews = "poultry industry news market trends";
            public const string PoultryWorldNews = "drób mięso kurczaka aktualności branża";
        }

        #endregion

        #region Search Methods

        /// <summary>
        /// Wyszukaj w internecie z dowolnym zapytaniem
        /// </summary>
        public async Task<TavilySearchResult> SearchAsync(
            string query,
            SearchOptions options = null,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                Debug.WriteLine("[Tavily] API key not configured");
                return new TavilySearchResult { Success = false, Error = "API key not configured" };
            }

            options ??= SearchOptions.Default;

            await _rateLimiter.WaitAsync(ct);
            try
            {
                // Rate limiting
                var elapsed = DateTime.Now - _lastRequestTime;
                if (elapsed < _minRequestInterval)
                {
                    await Task.Delay(_minRequestInterval - elapsed, ct);
                }

                var request = new TavilyRequest
                {
                    ApiKey = _apiKey,
                    Query = query,
                    SearchDepth = options.Deep ? "advanced" : "basic",
                    IncludeAnswer = options.IncludeAiAnswer,
                    IncludeRawContent = options.IncludeRawContent,
                    MaxResults = options.MaxResults,
                    IncludeDomains = options.IncludeDomains,
                    ExcludeDomains = options.ExcludeDomains
                };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                Debug.WriteLine($"[Tavily] Searching: {query}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(TavilyApiUrl, content, ct);

                _lastRequestTime = DateTime.Now;

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    Debug.WriteLine($"[Tavily] Error {(int)response.StatusCode}: {error}");
                    return new TavilySearchResult
                    {
                        Success = false,
                        Error = $"HTTP {(int)response.StatusCode}: {error}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<TavilyResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Debug.WriteLine($"[Tavily] Found {result?.Results?.Count ?? 0} results");

                return new TavilySearchResult
                {
                    Success = true,
                    Query = query,
                    AiAnswer = result?.Answer,
                    Results = result?.Results?.Select(r => new TavilyArticle
                    {
                        Title = r.Title,
                        Url = r.Url,
                        Content = r.Content,
                        RawContent = r.RawContent,
                        Score = r.Score,
                        PublishedDate = r.PublishedDate
                    }).ToList() ?? new List<TavilyArticle>(),
                    SearchedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tavily] Exception: {ex.Message}");
                return new TavilySearchResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        /// <summary>
        /// Wyszukaj wiele zapytań równolegle
        /// </summary>
        public async Task<List<TavilySearchResult>> SearchMultipleAsync(
            IEnumerable<string> queries,
            SearchOptions options = null,
            CancellationToken ct = default)
        {
            var results = new List<TavilySearchResult>();

            foreach (var query in queries)
            {
                ct.ThrowIfCancellationRequested();
                var result = await SearchAsync(query, options, ct);
                results.Add(result);

                // Small delay between requests
                await Task.Delay(300, ct);
            }

            return results;
        }

        /// <summary>
        /// Wykonaj pełne wyszukiwanie newsów dla branży drobiarskiej
        /// </summary>
        public async Task<PoultryNewsSearchResult> SearchPoultryNewsAsync(
            bool includeInternational = true,
            CancellationToken ct = default)
        {
            var result = new PoultryNewsSearchResult
            {
                SearchedAt = DateTime.Now
            };

            var options = new SearchOptions
            {
                MaxResults = 5,
                IncludeAiAnswer = true,
                Deep = false
            };

            // Polish queries
            var polishQueries = new[]
            {
                PoultryQueries.HpaiPoland,
                PoultryQueries.PoultryPricesPoland,
                PoultryQueries.PoultryWorldNews,
                PoultryQueries.FeedPricesPoland
            };

            foreach (var query in polishQueries)
            {
                ct.ThrowIfCancellationRequested();
                var searchResult = await SearchAsync(query, options, ct);

                if (searchResult.Success)
                {
                    result.PolishNews.AddRange(searchResult.Results);

                    if (!string.IsNullOrEmpty(searchResult.AiAnswer))
                    {
                        result.AiSummaries.Add(new AiSummary
                        {
                            Topic = query,
                            Summary = searchResult.AiAnswer
                        });
                    }
                }
            }

            // International queries
            if (includeInternational)
            {
                var intlQueries = new[]
                {
                    PoultryQueries.HpaiEurope,
                    PoultryQueries.PoultryPricesEU,
                    PoultryQueries.BrazilPoultryExport,
                    PoultryQueries.CornWheatPrices
                };

                foreach (var query in intlQueries)
                {
                    ct.ThrowIfCancellationRequested();
                    var searchResult = await SearchAsync(query, options, ct);

                    if (searchResult.Success)
                    {
                        result.InternationalNews.AddRange(searchResult.Results);

                        if (!string.IsNullOrEmpty(searchResult.AiAnswer))
                        {
                            result.AiSummaries.Add(new AiSummary
                            {
                                Topic = query,
                                Summary = searchResult.AiAnswer
                            });
                        }
                    }
                }
            }

            // Deduplicate by URL
            result.PolishNews = result.PolishNews
                .GroupBy(a => a.Url)
                .Select(g => g.First())
                .OrderByDescending(a => a.Score)
                .ToList();

            result.InternationalNews = result.InternationalNews
                .GroupBy(a => a.Url)
                .Select(g => g.First())
                .OrderByDescending(a => a.Score)
                .ToList();

            result.TotalArticles = result.PolishNews.Count + result.InternationalNews.Count;

            Debug.WriteLine($"[Tavily] Poultry search complete: {result.TotalArticles} articles, {result.AiSummaries.Count} AI summaries");

            return result;
        }

        /// <summary>
        /// Szybkie sprawdzenie alertów HPAI
        /// </summary>
        public async Task<TavilySearchResult> SearchHpaiAlertsAsync(CancellationToken ct = default)
        {
            return await SearchAsync(
                "HPAI ptasia grypa Polska Europa ogniska alert 2026",
                new SearchOptions
                {
                    MaxResults = 10,
                    IncludeAiAnswer = true,
                    Deep = true
                },
                ct);
        }

        /// <summary>
        /// Wyszukaj informacje o konkurencji
        /// </summary>
        public async Task<TavilySearchResult> SearchCompetitorNewsAsync(
            string competitorName,
            CancellationToken ct = default)
        {
            return await SearchAsync(
                $"{competitorName} drób aktualności inwestycje",
                new SearchOptions
                {
                    MaxResults = 5,
                    IncludeAiAnswer = true
                },
                ct);
        }

        /// <summary>
        /// Wyszukaj aktualne ceny
        /// </summary>
        public async Task<TavilySearchResult> SearchCurrentPricesAsync(CancellationToken ct = default)
        {
            return await SearchAsync(
                "aktualne ceny kurczaka drobiu skup hurt Polska styczeń luty 2026",
                new SearchOptions
                {
                    MaxResults = 8,
                    IncludeAiAnswer = true,
                    Deep = true
                },
                ct);
        }

        #endregion

        #region Convert to RawArticle

        /// <summary>
        /// Konwertuj wyniki Tavily do formatu RawArticle (kompatybilny z resztą systemu)
        /// </summary>
        public List<RawArticle> ConvertToRawArticles(TavilySearchResult searchResult)
        {
            if (!searchResult.Success || searchResult.Results == null)
                return new List<RawArticle>();

            return searchResult.Results.Select(r => new RawArticle
            {
                SourceId = "tavily",
                SourceName = "Tavily Search",
                SourceCategory = "Internet Search",
                Language = DetectLanguage(r.Title + " " + r.Content),
                Title = r.Title ?? "Bez tytułu",
                Url = r.Url,
                UrlHash = RssFeedService.ComputeHash(r.Url),
                Summary = TruncateContent(r.Content, 500),
                FullContent = r.RawContent ?? r.Content,
                PublishDate = ParsePublishDate(r.PublishedDate) ?? DateTime.Today,
                FetchedAt = DateTime.Now,
                RelevanceScore = (int)(r.Score * 20), // Convert 0-1 score to 0-20
                IsRelevant = r.Score > 0.3
            }).ToList();
        }

        private string DetectLanguage(string text)
        {
            if (string.IsNullOrEmpty(text)) return "pl";

            // Simple heuristic - check for Polish characters/words
            var polishIndicators = new[] { "ą", "ę", "ć", "ł", "ń", "ó", "ś", "ź", "ż", " i ", " w ", " na ", " z " };
            var polishCount = polishIndicators.Count(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));

            return polishCount >= 2 ? "pl" : "en";
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return "";
            if (content.Length <= maxLength) return content;
            return content.Substring(0, maxLength - 3) + "...";
        }

        private DateTime? ParsePublishDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return null;

            if (DateTime.TryParse(dateStr, out var date))
                return date;

            return null;
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }

    #region Request/Response Models

    internal class TavilyRequest
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("search_depth")]
        public string SearchDepth { get; set; } = "basic";

        [JsonPropertyName("include_answer")]
        public bool IncludeAnswer { get; set; } = true;

        [JsonPropertyName("include_raw_content")]
        public bool IncludeRawContent { get; set; } = false;

        [JsonPropertyName("max_results")]
        public int MaxResults { get; set; } = 5;

        [JsonPropertyName("include_domains")]
        public List<string> IncludeDomains { get; set; }

        [JsonPropertyName("exclude_domains")]
        public List<string> ExcludeDomains { get; set; }
    }

    internal class TavilyResponse
    {
        public string Answer { get; set; }
        public string Query { get; set; }
        public List<TavilyResultItem> Results { get; set; }
    }

    internal class TavilyResultItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }

        [JsonPropertyName("raw_content")]
        public string RawContent { get; set; }

        public double Score { get; set; }

        [JsonPropertyName("published_date")]
        public string PublishedDate { get; set; }
    }

    #endregion

    #region Public Models

    public class SearchOptions
    {
        public int MaxResults { get; set; } = 5;
        public bool IncludeAiAnswer { get; set; } = true;
        public bool IncludeRawContent { get; set; } = false;
        public bool Deep { get; set; } = false;
        public List<string> IncludeDomains { get; set; }
        public List<string> ExcludeDomains { get; set; }

        public static SearchOptions Default => new();

        public static SearchOptions DeepSearch => new()
        {
            MaxResults = 10,
            IncludeAiAnswer = true,
            IncludeRawContent = true,
            Deep = true
        };
    }

    public class TavilySearchResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Query { get; set; }
        public string AiAnswer { get; set; }
        public List<TavilyArticle> Results { get; set; } = new();
        public DateTime SearchedAt { get; set; }
    }

    public class TavilyArticle
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public string RawContent { get; set; }
        public double Score { get; set; }
        public string PublishedDate { get; set; }
    }

    public class PoultryNewsSearchResult
    {
        public List<TavilyArticle> PolishNews { get; set; } = new();
        public List<TavilyArticle> InternationalNews { get; set; } = new();
        public List<AiSummary> AiSummaries { get; set; } = new();
        public int TotalArticles { get; set; }
        public DateTime SearchedAt { get; set; }
    }

    public class AiSummary
    {
        public string Topic { get; set; }
        public string Summary { get; set; }
    }

    #endregion
}
