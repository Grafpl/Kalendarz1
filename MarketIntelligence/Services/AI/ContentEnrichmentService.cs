using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartReader;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// BATTLE-TESTED Content Enrichment Service
    ///
    /// Serwis do pobierania pełnej treści artykułów ze stron internetowych.
    /// FAIL-SAFE: Jeśli scraping się nie uda, zwraca dane z wyszukiwarki jako fallback.
    ///
    /// NuGet: Install-Package SmartReader
    /// </summary>
    public class ContentEnrichmentService
    {
        private readonly HttpClient _httpClient;

        // Minimum 300 znaków dla analizy (obniżone z 500 dla większej tolerancji)
        private const int MinContentLength = 300;

        public ContentEnrichmentService()
        {
            // BATTLE-TESTED: Tworzymy HttpClient bez automatycznej dekompresji
            // aby uniknąć problemów z niektórymi serwerami
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(15); // Krótszy timeout dla szybszego failover

            // BATTLE-TESTED: Nagłówki idealnie udające Chrome na Windows
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language",
                "pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        }

        /// <summary>
        /// FAIL-SAFE: Pobiera pełną treść artykułu.
        /// Jeśli scraping się nie uda - zwraca fallback z danymi z wyszukiwarki.
        /// NIGDY nie zwraca Success=false - zawsze jest jakiś content.
        /// </summary>
        /// <param name="url">URL artykułu</param>
        /// <param name="fallbackTitle">Tytuł z wyszukiwarki (fallback)</param>
        /// <param name="fallbackSnippet">Opis/snippet z wyszukiwarki (fallback)</param>
        /// <param name="ct">Token anulowania</param>
        /// <returns>EnrichmentResult - ZAWSZE z Success=true i jakimś contentem</returns>
        public async Task<EnrichmentResult> EnrichWithFallbackAsync(
            string url,
            string fallbackTitle,
            string fallbackSnippet,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                // Nawet bez URL - zwracamy fallback
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet, "Brak URL");
            }

            try
            {
                // Próba pobrania pełnej treści
                var result = await FetchFullContentInternalAsync(url, ct);

                // Sprawdź czy mamy wystarczająco treści
                if (result.Success && !string.IsNullOrEmpty(result.Content) && result.Content.Length >= MinContentLength)
                {
                    Debug.WriteLine($"[ContentEnrichment] SUCCESS: {url} - {result.Content.Length} znaków");
                    return result;
                }

                // Scraping nie dał wystarczająco treści - fallback
                Debug.WriteLine($"[ContentEnrichment] FALLBACK (za krótka treść): {url}");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                    result.Error ?? $"Treść za krótka ({result.Content?.Length ?? 0} znaków)");
            }
            catch (Exception ex)
            {
                // Każdy błąd = fallback
                Debug.WriteLine($"[ContentEnrichment] FALLBACK (exception): {url} - {ex.Message}");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet, ex.Message);
            }
        }

        /// <summary>
        /// Tworzy fallback result z danymi z wyszukiwarki
        /// </summary>
        private EnrichmentResult CreateFallbackResult(string url, string title, string snippet, string reason)
        {
            // Buduj treść fallback
            var contentBuilder = new System.Text.StringBuilder();
            contentBuilder.AppendLine("[TREŚĆ NA PODSTAWIE STRESZCZENIA Z WYSZUKIWARKI]");
            contentBuilder.AppendLine();

            if (!string.IsNullOrEmpty(title))
            {
                contentBuilder.AppendLine($"TYTUŁ: {title}");
                contentBuilder.AppendLine();
            }

            if (!string.IsNullOrEmpty(snippet))
            {
                contentBuilder.AppendLine("STRESZCZENIE:");
                contentBuilder.AppendLine(snippet);
                contentBuilder.AppendLine();
            }

            contentBuilder.AppendLine($"[Powód użycia streszczenia: {reason}]");

            var content = contentBuilder.ToString();

            return new EnrichmentResult
            {
                Success = true, // ZAWSZE sukces - mamy przynajmniej fallback
                Url = url,
                Content = content,
                Title = title,
                SiteName = GetDomain(url),
                ContentLength = content.Length,
                IsFallback = true, // Nowe pole - oznacza że to fallback
                FallbackReason = reason,
                Excerpt = snippet
            };
        }

        /// <summary>
        /// Wewnętrzna metoda pobierania treści - może zwrócić błąd
        /// </summary>
        private async Task<EnrichmentResult> FetchFullContentInternalAsync(string url, CancellationToken ct)
        {
            try
            {
                // Pobierz HTML
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                        Url = url
                    };
                }

                var html = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(html) || html.Length < 500)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = $"Zbyt krótka odpowiedź HTML ({html?.Length ?? 0} znaków)",
                        Url = url
                    };
                }

                // Użyj SmartReader do ekstrakcji treści
                var article = Reader.ParseArticle(url, html);

                if (article == null || !article.IsReadable)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = "SmartReader nie mógł wyodrębnić treści",
                        Url = url,
                        RawLength = html.Length
                    };
                }

                // Pobierz czysty tekst (bez HTML)
                var textContent = article.TextContent?.Trim();

                if (string.IsNullOrEmpty(textContent))
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = "Wyodrębniona treść jest pusta",
                        Url = url,
                        RawLength = html.Length
                    };
                }

                return new EnrichmentResult
                {
                    Success = true,
                    Url = url,
                    Content = textContent,
                    HtmlContent = article.Content,
                    Title = article.Title,
                    PublishDate = article.PublicationDate,
                    Author = article.Author,
                    SiteName = article.SiteName,
                    FeaturedImage = article.FeaturedImage,
                    Language = article.Language,
                    ContentLength = textContent.Length,
                    RawLength = html.Length,
                    Excerpt = article.Excerpt,
                    IsFallback = false
                };
            }
            catch (TaskCanceledException)
            {
                return new EnrichmentResult
                {
                    Success = false,
                    Error = "Timeout - strona nie odpowiedziała w czasie",
                    Url = url
                };
            }
            catch (HttpRequestException ex)
            {
                return new EnrichmentResult
                {
                    Success = false,
                    Error = $"Błąd HTTP: {ex.Message}",
                    Url = url
                };
            }
            catch (Exception ex)
            {
                return new EnrichmentResult
                {
                    Success = false,
                    Error = $"Błąd: {ex.Message}",
                    Url = url
                };
            }
        }

        /// <summary>
        /// Pobiera pełną treść artykułu z URL (stara metoda - zachowana dla kompatybilności)
        /// </summary>
        public async Task<EnrichmentResult> FetchFullContentAsync(string url, CancellationToken ct = default)
        {
            return await FetchFullContentInternalAsync(url, ct);
        }

        /// <summary>
        /// Alias dla kompatybilności wstecznej
        /// </summary>
        public async Task<EnrichmentResult> EnrichSingleAsync(string url, CancellationToken ct = default)
        {
            return await FetchFullContentAsync(url, ct);
        }

        /// <summary>
        /// BATTLE-TESTED: Wzbogaca listę artykułów równolegle z fallback
        /// </summary>
        public async Task<List<EnrichmentResult>> EnrichArticlesWithFallbackAsync(
            List<(string Url, string Title, string Snippet)> articles,
            IProgress<(int completed, int total, string currentUrl)> progress = null,
            CancellationToken ct = default,
            int maxConcurrency = 5)
        {
            var results = new List<EnrichmentResult>();
            var completed = 0;

            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task<EnrichmentResult>>();

            foreach (var article in articles)
            {
                if (ct.IsCancellationRequested) break;

                await semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        progress?.Report((completed, articles.Count, article.Url));

                        var result = await EnrichWithFallbackAsync(article.Url, article.Title, article.Snippet, ct);

                        Interlocked.Increment(ref completed);
                        progress?.Report((completed, articles.Count, article.Url));

                        // Mały delay dla rate limiting
                        await Task.Delay(100, ct);

                        return result;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            var taskResults = await Task.WhenAll(tasks);
            results.AddRange(taskResults);

            progress?.Report((articles.Count, articles.Count, "Zakończono"));

            return results;
        }

        /// <summary>
        /// Stara metoda - zachowana dla kompatybilności
        /// </summary>
        public async Task<List<EnrichmentResult>> EnrichArticlesAsync(
            List<string> urls,
            IProgress<(int completed, int total, string currentUrl)> progress = null,
            CancellationToken ct = default,
            int maxConcurrency = 3)
        {
            var articles = urls.Select(u => (u, "", "")).ToList();
            return await EnrichArticlesWithFallbackAsync(articles, progress, ct, maxConcurrency);
        }

        private string GetDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Wynik ekstrakcji treści artykułu
    /// </summary>
    public class EnrichmentResult
    {
        /// <summary>Czy ekstrakcja się powiodła</summary>
        public bool Success { get; set; }

        /// <summary>URL źródłowy</summary>
        public string Url { get; set; }

        /// <summary>Wyodrębniona treść tekstowa (bez HTML)</summary>
        public string Content { get; set; }

        /// <summary>Wyodrębniona treść HTML (z formatowaniem)</summary>
        public string HtmlContent { get; set; }

        /// <summary>Tytuł artykułu</summary>
        public string Title { get; set; }

        /// <summary>Data publikacji</summary>
        public DateTime? PublishDate { get; set; }

        /// <summary>Autor artykułu</summary>
        public string Author { get; set; }

        /// <summary>Nazwa strony źródłowej</summary>
        public string SiteName { get; set; }

        /// <summary>URL głównego obrazka</summary>
        public string FeaturedImage { get; set; }

        /// <summary>Język artykułu</summary>
        public string Language { get; set; }

        /// <summary>Krótki fragment/streszczenie</summary>
        public string Excerpt { get; set; }

        /// <summary>Źródło danych (np. "Google Cache")</summary>
        public string Source { get; set; }

        /// <summary>Komunikat błędu (jeśli Success = false)</summary>
        public string Error { get; set; }

        /// <summary>Długość wyodrębnionej treści</summary>
        public int ContentLength { get; set; }

        /// <summary>Długość surowego HTML</summary>
        public int RawLength { get; set; }

        /// <summary>Czy artykuł oznaczony jako niska jakość (za krótki) - DEPRECATED</summary>
        public bool IsLowQuality { get; set; }

        /// <summary>Czy to fallback z danych wyszukiwarki (nie scraping)</summary>
        public bool IsFallback { get; set; }

        /// <summary>Powód użycia fallbacku</summary>
        public string FallbackReason { get; set; }
    }
}
