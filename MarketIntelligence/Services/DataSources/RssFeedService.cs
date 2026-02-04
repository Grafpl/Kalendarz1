using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Polly;
using Polly.Retry;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do pobierania newsów z RSS feeds
    /// Obsługuje wiele polskich i międzynarodowych źródeł
    /// </summary>
    public class RssFeedService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly Dictionary<string, DateTime> _lastFetchTimes;
        private readonly TimeSpan _minFetchInterval = TimeSpan.FromSeconds(2);

        public RssFeedService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Set realistic User-Agent to avoid being blocked
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "application/rss+xml, application/xml, application/atom+xml, text/xml, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7");

            // Retry policy with exponential backoff
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .OrResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Debug.WriteLine($"[RSS] Retry {retryCount} for {context["url"]} after {timespan.TotalSeconds}s");
                    });

            _rateLimiter = new SemaphoreSlim(5); // Max 5 concurrent requests
            _lastFetchTimes = new Dictionary<string, DateTime>();
        }

        /// <summary>
        /// Pobierz artykuły ze wszystkich skonfigurowanych źródeł RSS
        /// </summary>
        public async Task<List<RawArticle>> FetchAllSourcesAsync(CancellationToken ct = default)
        {
            var allSources = NewsSourceConfig.GetAllRssSources();
            var articles = new List<RawArticle>();
            var tasks = new List<Task<List<RawArticle>>>();

            Debug.WriteLine($"[RSS] Starting fetch from {allSources.Count} sources...");

            foreach (var source in allSources.Where(s => s.IsActive))
            {
                tasks.Add(FetchFromSourceSafeAsync(source, ct));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                articles.AddRange(result);
            }

            Debug.WriteLine($"[RSS] Total articles fetched: {articles.Count}");

            // Deduplicate by URL
            var uniqueArticles = articles
                .GroupBy(a => a.UrlHash)
                .Select(g => g.First())
                .ToList();

            Debug.WriteLine($"[RSS] Unique articles after deduplication: {uniqueArticles.Count}");

            return uniqueArticles;
        }

        /// <summary>
        /// Pobierz tylko z polskich źródeł
        /// </summary>
        public async Task<List<RawArticle>> FetchPolishSourcesAsync(CancellationToken ct = default)
        {
            var polishSources = NewsSourceConfig.PolishAgricultureRss
                .Where(s => s.IsActive)
                .ToList();

            var articles = new List<RawArticle>();

            foreach (var source in polishSources)
            {
                var result = await FetchFromSourceSafeAsync(source, ct);
                articles.AddRange(result);
            }

            return DeduplicateArticles(articles);
        }

        /// <summary>
        /// Pobierz z pojedynczego źródła z retry i rate limiting
        /// </summary>
        private async Task<List<RawArticle>> FetchFromSourceSafeAsync(NewsSource source, CancellationToken ct)
        {
            try
            {
                // Rate limiting - don't hit same source too often
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    if (_lastFetchTimes.TryGetValue(source.Id, out var lastFetch))
                    {
                        var elapsed = DateTime.Now - lastFetch;
                        if (elapsed < _minFetchInterval)
                        {
                            await Task.Delay(_minFetchInterval - elapsed, ct);
                        }
                    }

                    var result = await FetchFromSourceAsync(source, ct);
                    _lastFetchTimes[source.Id] = DateTime.Now;
                    return result;
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RSS] Error fetching {source.Name}: {ex.Message}");
                source.ConsecutiveFailures++;
                return new List<RawArticle>();
            }
        }

        /// <summary>
        /// Pobierz artykuły z pojedynczego źródła RSS
        /// </summary>
        public async Task<List<RawArticle>> FetchFromSourceAsync(NewsSource source, CancellationToken ct = default)
        {
            var articles = new List<RawArticle>();
            var urlsToTry = new List<string> { source.Url };
            urlsToTry.AddRange(source.AlternateUrls ?? Array.Empty<string>());

            foreach (var url in urlsToTry)
            {
                try
                {
                    Debug.WriteLine($"[RSS] Fetching {source.Name} from {url}...");

                    var context = new Context { ["url"] = url };
                    var response = await _retryPolicy.ExecuteAsync(
                        async (ctx, token) => await _httpClient.GetAsync(url, token),
                        context, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[RSS] {source.Name}: HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync(ct);

                    // Parse RSS/Atom feed
                    var feedArticles = ParseFeed(content, source);
                    articles.AddRange(feedArticles);

                    Debug.WriteLine($"[RSS] {source.Name}: {feedArticles.Count} articles");
                    source.ConsecutiveFailures = 0;
                    source.LastFetchTime = DateTime.Now;
                    break; // Success - don't try alternate URLs
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RSS] {source.Name} ({url}): {ex.Message}");
                }
            }

            return articles;
        }

        /// <summary>
        /// Parsuj zawartość RSS/Atom do artykułów
        /// </summary>
        private List<RawArticle> ParseFeed(string content, NewsSource source)
        {
            var articles = new List<RawArticle>();

            try
            {
                using var reader = XmlReader.Create(new System.IO.StringReader(content));
                var feed = SyndicationFeed.Load(reader);

                if (feed == null) return articles;

                foreach (var item in feed.Items)
                {
                    try
                    {
                        var article = new RawArticle
                        {
                            SourceId = source.Id,
                            SourceName = source.Name,
                            SourceCategory = source.Category,
                            Language = source.Language,

                            Title = CleanText(item.Title?.Text),
                            Url = item.Links?.FirstOrDefault()?.Uri?.ToString()
                                  ?? item.Id,

                            Summary = CleanText(item.Summary?.Text)
                                      ?? ExtractTextFromHtml(item.Content as TextSyndicationContent),

                            PublishDate = item.PublishDate.DateTime != DateTime.MinValue
                                ? item.PublishDate.DateTime
                                : item.LastUpdatedTime.DateTime,

                            Authors = string.Join(", ", item.Authors?.Select(a => a.Name) ?? Array.Empty<string>()),
                            Categories = item.Categories?.Select(c => c.Name).ToList() ?? new List<string>(),

                            FetchedAt = DateTime.Now
                        };

                        // Calculate URL hash for deduplication
                        article.UrlHash = ComputeHash(article.Url);

                        // Skip if no title or URL
                        if (string.IsNullOrWhiteSpace(article.Title) ||
                            string.IsNullOrWhiteSpace(article.Url))
                            continue;

                        // Skip if too old (older than 7 days)
                        if (article.PublishDate < DateTime.Now.AddDays(-7))
                            continue;

                        articles.Add(article);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RSS] Error parsing item: {ex.Message}");
                    }
                }
            }
            catch (XmlException ex)
            {
                Debug.WriteLine($"[RSS] XML parse error for {source.Name}: {ex.Message}");
                // Try alternate parsing for malformed feeds
                articles.AddRange(ParseFeedAlternate(content, source));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RSS] Feed parse error for {source.Name}: {ex.Message}");
            }

            return articles;
        }

        /// <summary>
        /// Alternatywna metoda parsowania dla źle sformatowanych feedów
        /// </summary>
        private List<RawArticle> ParseFeedAlternate(string content, NewsSource source)
        {
            var articles = new List<RawArticle>();

            try
            {
                // Simple regex-based extraction for malformed feeds
                var itemPattern = @"<item>(.*?)</item>";
                var titlePattern = @"<title>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</title>";
                var linkPattern = @"<link>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</link>";
                var descPattern = @"<description>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</description>";
                var datePattern = @"<pubDate>(.*?)</pubDate>";

                var itemMatches = Regex.Matches(content, itemPattern, RegexOptions.Singleline);

                foreach (Match itemMatch in itemMatches)
                {
                    var itemContent = itemMatch.Groups[1].Value;

                    var titleMatch = Regex.Match(itemContent, titlePattern, RegexOptions.Singleline);
                    var linkMatch = Regex.Match(itemContent, linkPattern, RegexOptions.Singleline);
                    var descMatch = Regex.Match(itemContent, descPattern, RegexOptions.Singleline);
                    var dateMatch = Regex.Match(itemContent, datePattern, RegexOptions.Singleline);

                    if (!titleMatch.Success || !linkMatch.Success) continue;

                    var article = new RawArticle
                    {
                        SourceId = source.Id,
                        SourceName = source.Name,
                        SourceCategory = source.Category,
                        Language = source.Language,
                        Title = CleanText(titleMatch.Groups[1].Value),
                        Url = CleanText(linkMatch.Groups[1].Value),
                        Summary = descMatch.Success ? CleanText(descMatch.Groups[1].Value) : null,
                        FetchedAt = DateTime.Now
                    };

                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var pubDate))
                    {
                        article.PublishDate = pubDate;
                    }
                    else
                    {
                        article.PublishDate = DateTime.Now;
                    }

                    article.UrlHash = ComputeHash(article.Url);

                    if (!string.IsNullOrWhiteSpace(article.Title) && !string.IsNullOrWhiteSpace(article.Url))
                    {
                        articles.Add(article);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RSS] Alternate parse error: {ex.Message}");
            }

            return articles;
        }

        /// <summary>
        /// Wyczyść tekst z HTML i zbędnych znaków
        /// </summary>
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Remove HTML tags
            var noHtml = Regex.Replace(text, @"<[^>]+>", " ");

            // Decode HTML entities
            noHtml = System.Net.WebUtility.HtmlDecode(noHtml);

            // Remove CDATA markers
            noHtml = noHtml.Replace("<![CDATA[", "").Replace("]]>", "");

            // Normalize whitespace
            noHtml = Regex.Replace(noHtml, @"\s+", " ").Trim();

            return noHtml;
        }

        /// <summary>
        /// Wyciągnij tekst z SyndicationContent
        /// </summary>
        private string ExtractTextFromHtml(TextSyndicationContent content)
        {
            if (content == null) return null;
            return CleanText(content.Text);
        }

        /// <summary>
        /// Oblicz hash SHA256 dla URL (do deduplikacji)
        /// </summary>
        public static string ComputeHash(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url.ToLowerInvariant().Trim()));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Deduplikuj artykuły po URL hash
        /// </summary>
        private List<RawArticle> DeduplicateArticles(List<RawArticle> articles)
        {
            return articles
                .Where(a => !string.IsNullOrEmpty(a.UrlHash))
                .GroupBy(a => a.UrlHash)
                .Select(g => g.First())
                .ToList();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }

    /// <summary>
    /// Surowy artykuł pobrany ze źródła (przed analizą AI)
    /// </summary>
    public class RawArticle
    {
        public string SourceId { get; set; }
        public string SourceName { get; set; }
        public string SourceCategory { get; set; }
        public string Language { get; set; }

        public string Title { get; set; }
        public string Url { get; set; }
        public string UrlHash { get; set; }
        public string Summary { get; set; }
        public string FullContent { get; set; }

        public DateTime PublishDate { get; set; }
        public DateTime FetchedAt { get; set; }

        public string Authors { get; set; }
        public List<string> Categories { get; set; } = new();

        // Calculated relevance
        public bool IsRelevant { get; set; }
        public int RelevanceScore { get; set; }
        public string[] MatchedKeywords { get; set; }

        public override string ToString() => $"[{SourceName}] {Title}";
    }
}
