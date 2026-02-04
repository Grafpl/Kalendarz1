using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis wyszukiwania wiadomości przez Bing News Search API v7.
    /// Dokumentacja: https://docs.microsoft.com/en-us/bing/search-apis/bing-news-search/
    /// </summary>
    public class BingNewsSearchService : INewsSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private const string ApiUrl = "https://api.bing.microsoft.com/v7.0/news/search";

        // Koszt szacunkowy za 1000 zapytań (Bing News Search S1)
        private const decimal CostPer1000Queries = 3.00m;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
        public string ApiKeyPreview => IsConfigured
            ? _apiKey.Substring(0, Math.Min(8, _apiKey.Length)) + "..."
            : "BRAK";

        public BingNewsSearchService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Priorytet: 1) zmienna środowiskowa, 2) App.config
            _apiKey = Environment.GetEnvironmentVariable("BING_API_KEY")
                      ?? ConfigurationManager.AppSettings["BingApiKey"]
                      ?? "";

            if (IsConfigured)
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
            }
        }

        /// <summary>
        /// Testuje połączenie z Bing News API
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (false, "Brak klucza API Bing. Ustaw BING_API_KEY lub BingApiKey w App.config");
            }

            try
            {
                var results = await SearchAsync("polska drób news", ct);
                if (results.Any())
                {
                    return (true, $"Połączenie OK. Znaleziono {results.Count} artykułów.");
                }
                return (true, "Połączenie działa, ale brak wyników dla zapytania testowego.");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Błąd HTTP: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Błąd połączenia: {ex.Message}");
            }
        }

        /// <summary>
        /// Wyszukuje artykuły w Bing News
        /// </summary>
        public async Task<List<NewsArticle>> SearchAsync(string query, CancellationToken ct = default)
        {
            var (articles, _) = await SearchWithDebugAsync(query, ct);
            return articles;
        }

        /// <summary>
        /// Wyszukuje artykuły z informacjami debugowymi
        /// </summary>
        public async Task<(List<NewsArticle> Articles, string DebugInfo)> SearchWithDebugAsync(
            string query,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "Klucz API Bing News Search nie jest skonfigurowany. " +
                    "Proszę uzupełnić BingApiKey w App.config lub ustawić zmienną środowiskową BING_API_KEY. " +
                    "Klucz można uzyskać w: https://portal.azure.com -> Bing Search v7");
            }

            var articles = new List<NewsArticle>();
            var debugInfo = new System.Text.StringBuilder();

            try
            {
                // Budowanie URL z parametrami
                var encodedQuery = HttpUtility.UrlEncode(query);
                var requestUrl = $"{ApiUrl}?q={encodedQuery}&count=20&mkt=pl-PL&freshness=Week&sortBy=Date";

                debugInfo.AppendLine($"[Bing News API Request]");
                debugInfo.AppendLine($"Query: {query}");
                debugInfo.AppendLine($"URL: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl, ct);
                var responseContent = await response.Content.ReadAsStringAsync();

                debugInfo.AppendLine($"Status: {response.StatusCode}");
                debugInfo.AppendLine($"Response length: {responseContent.Length} chars");

                if (!response.IsSuccessStatusCode)
                {
                    debugInfo.AppendLine($"Error response: {responseContent}");
                    throw new HttpRequestException($"Bing API zwróciło błąd {response.StatusCode}: {responseContent}");
                }

                // Parsowanie JSON response
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var newsArray))
                {
                    debugInfo.AppendLine($"Found {newsArray.GetArrayLength()} articles");

                    foreach (var item in newsArray.EnumerateArray())
                    {
                        var article = ParseArticle(item);
                        if (article != null)
                        {
                            articles.Add(article);
                        }
                    }
                }
                else
                {
                    debugInfo.AppendLine("No 'value' array in response");
                }

                debugInfo.AppendLine($"Parsed {articles.Count} valid articles");
            }
            catch (JsonException ex)
            {
                debugInfo.AppendLine($"JSON parse error: {ex.Message}");
                Debug.WriteLine($"[BingNewsSearch] JSON error: {ex}");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                debugInfo.AppendLine($"Error: {ex.Message}");
                Debug.WriteLine($"[BingNewsSearch] Error: {ex}");
                throw;
            }

            return (articles, debugInfo.ToString());
        }

        /// <summary>
        /// Parsuje pojedynczy artykuł z JSON
        /// </summary>
        private NewsArticle ParseArticle(JsonElement item)
        {
            try
            {
                var article = new NewsArticle
                {
                    Title = item.TryGetProperty("name", out var name) ? name.GetString() : null,
                    Snippet = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    Url = item.TryGetProperty("url", out var url) ? url.GetString() : null,
                    Category = item.TryGetProperty("category", out var cat) ? cat.GetString() : "News"
                };

                // Źródło (provider)
                if (item.TryGetProperty("provider", out var providers) && providers.GetArrayLength() > 0)
                {
                    var provider = providers[0];
                    article.Source = provider.TryGetProperty("name", out var provName)
                        ? provName.GetString()
                        : "Bing News";
                }

                // Data publikacji
                if (item.TryGetProperty("datePublished", out var dateStr))
                {
                    if (DateTime.TryParse(dateStr.GetString(), out var date))
                    {
                        article.PublishedDate = date;
                    }
                }

                // Miniaturka
                if (item.TryGetProperty("image", out var image) &&
                    image.TryGetProperty("thumbnail", out var thumb) &&
                    thumb.TryGetProperty("contentUrl", out var thumbUrl))
                {
                    article.ThumbnailUrl = thumbUrl.GetString();
                }

                // Walidacja - musi mieć tytuł i URL
                if (string.IsNullOrEmpty(article.Title) || string.IsNullOrEmpty(article.Url))
                {
                    return null;
                }

                return article;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BingNewsSearch] Parse article error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Pobiera wszystkie wiadomości dla predefiniowanych zapytań branżowych
        /// </summary>
        public async Task<List<NewsArticle>> FetchAllNewsAsync(
            IProgress<(int completed, int total, string query)> progress = null,
            bool quickMode = false,
            CancellationToken ct = default)
        {
            var queries = quickMode ? GetQuickQueries() : GetAllQueries();
            var allArticles = new List<NewsArticle>();
            var seenUrls = new HashSet<string>();
            var completed = 0;

            foreach (var query in queries)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    progress?.Report((completed, queries.Count, query));

                    var articles = await SearchAsync(query, ct);

                    // Deduplikacja po URL
                    foreach (var article in articles)
                    {
                        if (!string.IsNullOrEmpty(article.Url) && seenUrls.Add(article.Url))
                        {
                            allArticles.Add(article);
                        }
                    }

                    // Mały delay aby nie przekroczyć rate limit
                    await Task.Delay(200, ct);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BingNewsSearch] Query '{query}' failed: {ex.Message}");
                }

                completed++;
            }

            progress?.Report((completed, queries.Count, "Zakończono"));
            return allArticles;
        }

        /// <summary>
        /// Zapytania dla pełnego trybu - kompleksowe pokrycie branży drobiarskiej
        /// </summary>
        public List<string> GetAllQueries()
        {
            return new List<string>
            {
                // HPAI / Choroby
                "ptasia grypa Polska 2026",
                "HPAI ogniska Europa",
                "ASF świnie Polska",

                // Ceny i rynek
                "ceny drobiu Polska",
                "ceny kurczaka hurt",
                "ceny żywca drobiowego",
                "ceny pasz kukurydza pszenica",

                // Konkurencja
                "Cedrob przejęcie",
                "SuperDrob LipCo",
                "Drosed Indykpol",
                "Animex drób",

                // Eksport/Import
                "eksport drobiu Polska",
                "import drobiu Brazylia Ukraina",
                "Mercosur drób UE",
                "Chiny polski drób",

                // Regulacje
                "KSeF faktury 2026",
                "dobrostan drobiu przepisy",
                "weterynaryjne regulacje drób",

                // Sieci handlowe
                "Biedronka dostawcy mięso",
                "Dino sklepy ekspansja",
                "Lidl Kaufland drób",

                // Ogólne branżowe
                "branża drobiarska Polska",
                "produkcja drobiu GUS",
                "ubojnie drobiu inwestycje"
            };
        }

        /// <summary>
        /// Zapytania dla trybu szybkiego - najważniejsze tematy
        /// </summary>
        public List<string> GetQuickQueries()
        {
            return new List<string>
            {
                "ptasia grypa Polska 2026",
                "ceny drobiu kurczak",
                "Cedrob SuperDrob Drosed",
                "eksport drobiu Polska",
                "branża drobiarska news"
            };
        }

        /// <summary>
        /// Szacowany koszt zapytań
        /// </summary>
        public decimal EstimateCost(int queryCount)
        {
            // Bing News Search S1: ~$3 per 1000 transactions
            return (queryCount / 1000m) * CostPer1000Queries;
        }
    }
}
