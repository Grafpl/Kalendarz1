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
    /// Serwis wyszukiwania wiadomości przez Brave Search API.
    /// Dokumentacja: https://api.search.brave.com/app/documentation/news-search
    /// </summary>
    public class BraveSearchService : INewsSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private const string ApiUrl = "https://api.search.brave.com/res/v1/news/search";

        // Koszt szacunkowy za 1000 zapytań (Brave Search - darmowe do 2000/miesiąc, potem $3/1000)
        private const decimal CostPer1000Queries = 3.00m;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
        public string ApiKeyPreview => IsConfigured
            ? _apiKey.Substring(0, Math.Min(8, _apiKey.Length)) + "..."
            : "BRAK";

        public BraveSearchService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Priorytet: 1) zmienna środowiskowa, 2) App.config
            _apiKey = Environment.GetEnvironmentVariable("BRAVE_API_KEY")
                      ?? ConfigurationManager.AppSettings["BraveApiKey"]
                      ?? "";

            if (IsConfigured)
            {
                // Brave używa nagłówka X-Subscription-Token
                _httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _apiKey);
            }

            // Standardowe nagłówki
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Testuje połączenie z Brave Search API
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (false, "Brak klucza API Brave. Ustaw BRAVE_API_KEY lub BraveApiKey w App.config");
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
        /// Wyszukuje artykuły w Brave News
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
                    "Klucz API Brave Search nie jest skonfigurowany. " +
                    "Proszę uzupełnić BraveApiKey w App.config lub ustawić zmienną środowiskową BRAVE_API_KEY. " +
                    "Klucz można uzyskać na: https://brave.com/search/api/");
            }

            var articles = new List<NewsArticle>();
            var debugInfo = new System.Text.StringBuilder();

            try
            {
                // Budowanie URL z parametrami
                // Brave API parametry: q (query), count, freshness, country, search_lang
                var encodedQuery = HttpUtility.UrlEncode(query);
                var requestUrl = $"{ApiUrl}?q={encodedQuery}&count=20&freshness=pw&country=pl&search_lang=pl";

                debugInfo.AppendLine($"[Brave Search API Request]");
                debugInfo.AppendLine($"Query: {query}");
                debugInfo.AppendLine($"URL: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl, ct);
                var responseContent = await response.Content.ReadAsStringAsync();

                debugInfo.AppendLine($"Status: {response.StatusCode}");
                debugInfo.AppendLine($"Response length: {responseContent.Length} chars");

                if (!response.IsSuccessStatusCode)
                {
                    debugInfo.AppendLine($"Error response: {responseContent}");
                    throw new HttpRequestException($"Brave API zwróciło błąd {response.StatusCode}: {responseContent}");
                }

                // Parsowanie JSON response
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                // Brave zwraca wyniki w "results" array
                if (root.TryGetProperty("results", out var newsArray))
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
                    debugInfo.AppendLine("No 'results' array in response");
                }

                debugInfo.AppendLine($"Parsed {articles.Count} valid articles");
            }
            catch (JsonException ex)
            {
                debugInfo.AppendLine($"JSON parse error: {ex.Message}");
                Debug.WriteLine($"[BraveSearch] JSON error: {ex}");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                debugInfo.AppendLine($"Error: {ex.Message}");
                Debug.WriteLine($"[BraveSearch] Error: {ex}");
                throw;
            }

            return (articles, debugInfo.ToString());
        }

        /// <summary>
        /// Parsuje pojedynczy artykuł z JSON Brave API
        /// </summary>
        private NewsArticle ParseArticle(JsonElement item)
        {
            try
            {
                var article = new NewsArticle
                {
                    Title = item.TryGetProperty("title", out var title) ? title.GetString() : null,
                    Snippet = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    Url = item.TryGetProperty("url", out var url) ? url.GetString() : null,
                    Category = "News"
                };

                // Źródło (source)
                if (item.TryGetProperty("meta_url", out var metaUrl) &&
                    metaUrl.TryGetProperty("hostname", out var hostname))
                {
                    article.Source = hostname.GetString();
                }
                else if (item.TryGetProperty("source", out var source))
                {
                    article.Source = source.GetString();
                }
                else
                {
                    article.Source = "Brave Search";
                }

                // Data publikacji - Brave używa "age" (np. "2 hours ago") lub "page_age"
                if (item.TryGetProperty("page_age", out var pageAge))
                {
                    if (DateTime.TryParse(pageAge.GetString(), out var date))
                    {
                        article.PublishedDate = date;
                    }
                }
                else if (item.TryGetProperty("age", out var age))
                {
                    // Parsowanie względnego czasu jak "2 hours ago", "1 day ago"
                    article.PublishedDate = ParseRelativeTime(age.GetString());
                }

                // Miniaturka
                if (item.TryGetProperty("thumbnail", out var thumb) &&
                    thumb.TryGetProperty("src", out var thumbSrc))
                {
                    article.ThumbnailUrl = thumbSrc.GetString();
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
                Debug.WriteLine($"[BraveSearch] Parse article error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parsuje względny czas ("2 hours ago", "1 day ago") na DateTime
        /// </summary>
        private DateTime? ParseRelativeTime(string relativeTime)
        {
            if (string.IsNullOrEmpty(relativeTime))
                return null;

            var now = DateTime.Now;
            var lower = relativeTime.ToLowerInvariant();

            try
            {
                if (lower.Contains("second"))
                {
                    var num = ExtractNumber(lower);
                    return now.AddSeconds(-num);
                }
                if (lower.Contains("minute"))
                {
                    var num = ExtractNumber(lower);
                    return now.AddMinutes(-num);
                }
                if (lower.Contains("hour"))
                {
                    var num = ExtractNumber(lower);
                    return now.AddHours(-num);
                }
                if (lower.Contains("day"))
                {
                    var num = ExtractNumber(lower);
                    return now.AddDays(-num);
                }
                if (lower.Contains("week"))
                {
                    var num = ExtractNumber(lower);
                    return now.AddDays(-num * 7);
                }
                if (lower.Contains("month"))
                {
                    var num = ExtractNumber(lower);
                    return now.AddMonths(-num);
                }
            }
            catch
            {
                // Ignoruj błędy parsowania
            }

            return null;
        }

        private int ExtractNumber(string text)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? 1 : int.Parse(digits);
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

                    // Mały delay aby nie przekroczyć rate limit (Brave: 1 req/sec dla darmowego planu)
                    await Task.Delay(1100, ct);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BraveSearch] Query '{query}' failed: {ex.Message}");
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
            // Brave Search: 2000 darmowych zapytań/miesiąc, potem ~$3 per 1000
            // Zwracamy pesymistyczny koszt
            return (queryCount / 1000m) * CostPer1000Queries;
        }
    }
}
