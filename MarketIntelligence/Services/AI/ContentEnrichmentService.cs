using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartReader;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis do pobierania pełnej treści artykułów ze stron internetowych.
    /// Używa biblioteki SmartReader (Readability algorithm) do ekstrakcji czystej treści.
    ///
    /// NuGet: Install-Package SmartReader
    /// </summary>
    public class ContentEnrichmentService
    {
        private readonly HttpClient _httpClient;

        public ContentEnrichmentService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en;q=0.8");
        }

        /// <summary>
        /// Pobiera pełną treść artykułu z URL używając SmartReader
        /// </summary>
        public async Task<EnrichmentResult> FetchFullContentAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                return new EnrichmentResult { Success = false, Error = "Brak URL" };
            }

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

                // Użyj SmartReader do ekstrakcji treści
                var article = Reader.ParseArticle(url, html);

                if (article == null || !article.IsReadable)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = "SmartReader nie mógł wyodrębnić treści (strona może być za dynamiczna lub zablokowana)",
                        Url = url,
                        RawLength = html.Length
                    };
                }

                // Pobierz czysty tekst (bez HTML)
                var textContent = article.TextContent?.Trim();

                // Minimum 500 znaków dla wartościowej analizy AI
                if (string.IsNullOrEmpty(textContent) || textContent.Length < 500)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = $"Treść zbyt krótka ({textContent?.Length ?? 0} znaków, wymagane min. 500). Pomijam artykuł.",
                        Url = url,
                        RawLength = html.Length,
                        IsLowQuality = true
                    };
                }

                return new EnrichmentResult
                {
                    Success = true,
                    Url = url,
                    Content = textContent,
                    HtmlContent = article.Content, // HTML content jeśli potrzebny
                    Title = article.Title,
                    PublishDate = article.PublicationDate,
                    Author = article.Author,
                    SiteName = article.SiteName,
                    FeaturedImage = article.FeaturedImage,
                    Language = article.Language,
                    ContentLength = textContent.Length,
                    RawLength = html.Length,
                    Excerpt = article.Excerpt
                };
            }
            catch (TaskCanceledException)
            {
                return new EnrichmentResult
                {
                    Success = false,
                    Error = "Timeout - strona nie odpowiedziała w wyznaczonym czasie",
                    Url = url
                };
            }
            catch (HttpRequestException ex)
            {
                return new EnrichmentResult
                {
                    Success = false,
                    Error = $"Błąd połączenia HTTP: {ex.Message}",
                    Url = url
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContentEnrichment] Error for {url}: {ex}");
                return new EnrichmentResult
                {
                    Success = false,
                    Error = $"Błąd: {ex.Message}",
                    Url = url
                };
            }
        }

        /// <summary>
        /// Alias dla FetchFullContentAsync (zachowanie kompatybilności wstecznej)
        /// </summary>
        public async Task<EnrichmentResult> EnrichSingleAsync(string url, CancellationToken ct = default)
        {
            return await FetchFullContentAsync(url, ct);
        }

        /// <summary>
        /// Pobiera treść dla wielu artykułów równolegle (z ograniczeniem)
        /// </summary>
        public async Task<List<EnrichmentResult>> EnrichArticlesAsync(
            List<string> urls,
            IProgress<(int completed, int total, string currentUrl)> progress = null,
            CancellationToken ct = default,
            int maxConcurrency = 3)
        {
            var results = new List<EnrichmentResult>();
            var completed = 0;

            // Przetwarzaj z ograniczeniem równoległości
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task<EnrichmentResult>>();

            foreach (var url in urls)
            {
                if (ct.IsCancellationRequested) break;

                await semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        progress?.Report((completed, urls.Count, url));
                        var result = await FetchFullContentAsync(url, ct);

                        Interlocked.Increment(ref completed);
                        progress?.Report((completed, urls.Count, url));

                        // Mały delay aby nie przeciążyć serwerów
                        await Task.Delay(200, ct);

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

            progress?.Report((urls.Count, urls.Count, "Zakończono"));

            return results;
        }

        /// <summary>
        /// Próbuje pobrać treść z kilku alternatywnych źródeł (np. archive.org)
        /// </summary>
        public async Task<EnrichmentResult> FetchWithFallbackAsync(string url, CancellationToken ct = default)
        {
            // Najpierw próba bezpośrednia
            var result = await FetchFullContentAsync(url, ct);
            if (result.Success)
            {
                return result;
            }

            // Fallback: Google Cache (jeśli dostępny)
            try
            {
                var cacheUrl = $"https://webcache.googleusercontent.com/search?q=cache:{Uri.EscapeDataString(url)}";
                var cacheResult = await FetchFullContentAsync(cacheUrl, ct);
                if (cacheResult.Success)
                {
                    cacheResult.Url = url; // Przywróć oryginalny URL
                    cacheResult.Source = "Google Cache";
                    return cacheResult;
                }
            }
            catch
            {
                // Ignoruj błędy cache
            }

            // Zwróć oryginalny błąd
            return result;
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

        /// <summary>Czy artykuł oznaczony jako niska jakość (za krótki)</summary>
        public bool IsLowQuality { get; set; }
    }
}
