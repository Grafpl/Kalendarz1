using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// ENTERPRISE Content Enrichment Service z PuppeteerSharp
    ///
    /// Strategia "Tank" - używamy headless Chrome do omijania Cloudflare.
    /// Fallback: HTTP -> Puppeteer -> Snippet z wyszukiwarki
    ///
    /// NuGet: Install-Package PuppeteerSharp
    /// </summary>
    public class ContentEnrichmentService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private IBrowser _browser;
        private bool _browserInitialized = false;
        private readonly SemaphoreSlim _browserLock = new SemaphoreSlim(1, 1);

        // OBNIŻONE MINIMUM - akceptujemy nawet snippety!
        private const int MinContentLengthFull = 300;    // Dla pełnych artykułów
        private const int MinContentLengthSnippet = 50;  // Dla snippetów (fallback)

        // Lista logów diagnostycznych
        public List<string> DiagnosticLogs { get; } = new List<string>();

        // Słowa kluczowe wskazujące na blokadę
        private static readonly string[] BlockedIndicators = new[]
        {
            "access denied", "403 forbidden", "captcha",
            "verify you are human", "please wait",
            "checking your browser", "ddos protection", "bot detection",
            "rate limit", "too many requests", "javascript required",
            "enable javascript", "browser verification"
        };

        public ContentEnrichmentService()
        {
            // HTTP Client z kamuflażem i PEŁNĄ obsługą kompresji
            var handler = new HttpClientHandler
            {
                // WAŻNE: AutomaticDecompression dla GZip, Deflate i Brotli (jeśli dostępne)
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            // Próba włączenia Brotli (dostępne od .NET Core 2.1+)
            try
            {
                handler.AutomaticDecompression |= (DecompressionMethods)4; // Brotli = 4
            }
            catch { /* Brotli niedostępne w tej wersji .NET */ }

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(20); // Zwiększony timeout

            ConfigureChromeHeaders();
        }

        private void ConfigureChromeHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7");
            // WAŻNE: Usunięto 'br' (Brotli) - powoduje krzaczki gdy nie można zdekompresować
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua",
                "\"Not A(Brand\";v=\"99\", \"Google Chrome\";v=\"121\", \"Chromium\";v=\"121\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");
        }

        private void Log(string message, string level = "INFO")
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] {message}";
            DiagnosticLogs.Add(logLine);
            Debug.WriteLine($"[ContentEnrichment] {logLine}");
        }

        public void ClearLogs() => DiagnosticLogs.Clear();

        #region Puppeteer Browser Management

        /// <summary>
        /// Inicjalizuje przeglądarkę Puppeteer (pobiera Chromium jeśli potrzeba)
        /// </summary>
        private async Task EnsureBrowserAsync()
        {
            if (_browserInitialized && _browser != null) return;

            await _browserLock.WaitAsync();
            try
            {
                if (_browserInitialized && _browser != null) return;

                Log("Inicjalizacja Puppeteer...");

                // Sprawdź i pobierz Chromium
                var browserFetcher = new BrowserFetcher();
                var installedBrowser = browserFetcher.GetInstalledBrowsers().FirstOrDefault();

                if (installedBrowser == null)
                {
                    Log("Pobieranie Chromium (jednorazowo)... To może potrwać kilka minut.");
                    await browserFetcher.DownloadAsync();
                    Log("Chromium pobrane!");
                }

                // Uruchom przeglądarkę w trybie headless
                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu",
                        "--window-size=1920,1080"
                    }
                });

                _browserInitialized = true;
                Log("Puppeteer zainicjalizowany!");
            }
            catch (Exception ex)
            {
                Log($"Błąd inicjalizacji Puppeteer: {ex.Message}", "ERROR");
                throw;
            }
            finally
            {
                _browserLock.Release();
            }
        }

        #endregion

        #region Main Enrichment Methods

        // Retry configuration
        private const int MaxHttpRetries = 2;
        private const int RetryDelayMs = 1000;

        /// <summary>
        /// ENTERPRISE: Pobiera treść artykułu z trzema poziomami fallback:
        /// 1. HTTP (szybki) - z retry
        /// 2. Puppeteer (omija Cloudflare)
        /// 3. Snippet z wyszukiwarki (zawsze działa)
        /// </summary>
        public async Task<EnrichmentResult> EnrichWithFallbackAsync(
            string url,
            string fallbackTitle,
            string fallbackSnippet,
            CancellationToken ct = default)
        {
            Log($"========== START: {GetDomain(url)} ==========");
            Log($"URL: {url}");

            if (string.IsNullOrEmpty(url))
            {
                Log("BRAK URL - używam snippetu", "WARNING");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet, "Brak URL");
            }

            try
            {
                // ═══════════════════════════════════════════════════════════
                // POZIOM 1: Próba HTTP (szybkie) z RETRY
                // ═══════════════════════════════════════════════════════════
                EnrichmentResult httpResult = null;

                for (int attempt = 1; attempt <= MaxHttpRetries; attempt++)
                {
                    Log($"POZIOM 1: Próba HTTP ({attempt}/{MaxHttpRetries})...");
                    httpResult = await TryHttpFetchAsync(url, ct);

                    if (httpResult.Success && httpResult.Content.Length >= MinContentLengthFull)
                    {
                        Log($"HTTP SUKCES: {httpResult.Content.Length} znaków", "SUCCESS");
                        return httpResult;
                    }

                    // Jeśli to garbage lub Cloudflare - nie retry, idź do Puppeteer
                    if (httpResult.Error?.Contains("garbage") == true ||
                        httpResult.Error?.Contains("Brotli") == true ||
                        httpResult.Error?.Contains("403") == true ||
                        httpResult.Error?.Contains("Cloudflare") == true)
                    {
                        break;
                    }

                    // Retry dla timeout lub innych błędów
                    if (attempt < MaxHttpRetries)
                    {
                        Log($"HTTP retry za {RetryDelayMs}ms...", "INFO");
                        await Task.Delay(RetryDelayMs, ct);
                    }
                }

                // HTTP nie dało pełnej treści - sprawdź czy Cloudflare
                bool isCloudflareBlock = httpResult?.Error?.Contains("403") == true ||
                                         httpResult?.Error?.Contains("Cloudflare") == true ||
                                         IsBlockedPage(httpResult?.Content ?? "");

                // Sprawdź czy to problem z encoding
                bool isEncodingIssue = httpResult?.Error?.Contains("garbage") == true ||
                                       httpResult?.Error?.Contains("Brotli") == true;

                // ═══════════════════════════════════════════════════════════
                // POZIOM 2: Puppeteer (dla blokad i problemów z encoding)
                // ═══════════════════════════════════════════════════════════
                if (isCloudflareBlock || isEncodingIssue)
                {
                    Log("POZIOM 2: Wykryto blokadę/encoding - próba Puppeteer...", "WARNING");
                    try
                    {
                        var puppeteerResult = await TryPuppeteerFetchAsync(url, ct);

                        if (puppeteerResult.Success && puppeteerResult.Content.Length >= MinContentLengthFull)
                        {
                            Log($"PUPPETEER SUKCES: {puppeteerResult.Content.Length} znaków", "SUCCESS");
                            return puppeteerResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Puppeteer FAILED: {ex.Message}", "WARNING");
                    }
                }

                // ═══════════════════════════════════════════════════════════
                // POZIOM 3: Fallback na snippet (zawsze działa)
                // ═══════════════════════════════════════════════════════════
                Log("POZIOM 3: Używam snippetu z wyszukiwarki", "WARNING");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                    httpResult?.Error ?? "Brak pełnej treści");
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.Message}", "ERROR");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet, ex.Message);
            }
        }

        /// <summary>
        /// Próba pobrania przez HTTP (szybka metoda)
        /// </summary>
        private async Task<EnrichmentResult> TryHttpFetchAsync(string url, CancellationToken ct)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(url, ct);
                stopwatch.Stop();

                Log($"HTTP Response: {(int)response.StatusCode} w {stopwatch.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                    };
                }

                // Sprawdź Content-Encoding - jeśli Brotli i nie obsługujemy, użyj fallback
                var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault()?.ToLower();
                if (contentEncoding == "br")
                {
                    Log("Serwer zwraca Brotli mimo braku wsparcia - fallback", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = "Brotli encoding not supported - fallback to snippet"
                    };
                }

                var html = await response.Content.ReadAsStringAsync();
                Log($"HTML: {html.Length} znaków");

                // Sprawdź czy to binarne śmieci
                if (IsGarbageContent(html))
                {
                    Log("Wykryto binarne śmieci w odpowiedzi HTTP!", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = "Binary/compressed garbage detected"
                    };
                }

                // Sprawdź blokadę
                if (IsBlockedPage(html))
                {
                    Log("Wykryto stronę blokady!", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Content = html,
                        Error = "Cloudflare/Captcha block"
                    };
                }

                // Wyodrębnij tekst (prosty parser - usuń HTML)
                var textContent = ExtractTextFromHtml(html);
                Log($"Wyodrębniono tekst: {textContent.Length} znaków");

                // Dodatkowa walidacja - sprawdź czy tekst nie jest śmieciami
                if (IsGarbageContent(textContent))
                {
                    Log("Wyodrębniony tekst zawiera śmieci!", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = "Extracted text contains garbage"
                    };
                }

                return new EnrichmentResult
                {
                    Success = textContent.Length >= MinContentLengthSnippet,
                    Url = url,
                    Content = textContent,
                    Title = ExtractTitle(html),
                    ContentLength = textContent.Length,
                    RawLength = html.Length,
                    SiteName = GetDomain(url)
                };
            }
            catch (TaskCanceledException)
            {
                return new EnrichmentResult { Success = false, Url = url, Error = "Timeout" };
            }
            catch (Exception ex)
            {
                return new EnrichmentResult { Success = false, Url = url, Error = ex.Message };
            }
        }

        /// <summary>
        /// Próba pobrania przez Puppeteer (omija Cloudflare)
        /// </summary>
        private async Task<EnrichmentResult> TryPuppeteerFetchAsync(string url, CancellationToken ct)
        {
            await EnsureBrowserAsync();

            IPage page = null;
            try
            {
                page = await _browser.NewPageAsync();

                // Ustaw User-Agent
                await page.SetUserAgentAsync(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");

                // Ustaw viewport
                await page.SetViewportAsync(new ViewPortOptions
                {
                    Width = 1920,
                    Height = 1080
                });

                Log("Puppeteer: Ładuję stronę...");

                // Przejdź do strony
                var response = await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    Timeout = 30000 // 30 sekund
                });

                if (response == null || !response.Ok)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = $"Puppeteer: HTTP {response?.Status}"
                    };
                }

                // Poczekaj na body
                await page.WaitForSelectorAsync("body", new WaitForSelectorOptions { Timeout = 10000 });

                // Pobierz tekst z article, main lub body
                var textContent = await page.EvaluateFunctionAsync<string>(@"() => {
                    // Spróbuj znaleźć główną treść
                    const article = document.querySelector('article');
                    if (article) return article.innerText;

                    const main = document.querySelector('main');
                    if (main) return main.innerText;

                    const content = document.querySelector('.content, .article-content, .post-content, .entry-content');
                    if (content) return content.innerText;

                    // Fallback na body (bez menu, footer)
                    const body = document.body;
                    // Usuń menu i footer
                    const clone = body.cloneNode(true);
                    clone.querySelectorAll('nav, header, footer, aside, .menu, .sidebar').forEach(el => el.remove());
                    return clone.innerText;
                }");

                Log($"Puppeteer: Wyodrębniono {textContent?.Length ?? 0} znaków");

                return new EnrichmentResult
                {
                    Success = !string.IsNullOrEmpty(textContent) && textContent.Length >= MinContentLengthSnippet,
                    Url = url,
                    Content = textContent ?? "",
                    ContentLength = textContent?.Length ?? 0,
                    SiteName = GetDomain(url),
                    Source = "Puppeteer"
                };
            }
            finally
            {
                if (page != null)
                {
                    try { await page.CloseAsync(); } catch { }
                }
            }
        }

        #endregion

        #region Helper Methods

        private bool IsBlockedPage(string html)
        {
            if (string.IsNullOrEmpty(html)) return false;

            var htmlLower = html.ToLower();
            foreach (var indicator in BlockedIndicators)
            {
                if (htmlLower.Contains(indicator)) return true;
            }

            // Cloudflare challenge
            if (htmlLower.Contains("cf-browser-verification") ||
                htmlLower.Contains("__cf_chl") ||
                htmlLower.Contains("cf_chl_prog"))
            {
                return true;
            }

            return false;
        }

        private string ExtractTextFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";

            // Sprawdź czy to binarne śmieci (Brotli, itp.)
            if (IsGarbageContent(html))
            {
                Log("Wykryto binarne/skompresowane śmieci w treści", "WARNING");
                return ""; // Zwróć pusty string - wymusi fallback na snippet
            }

            // Prosty ekstraktor - usuwa tagi HTML
            var text = Regex.Replace(html, "<script[^>]*>.*?</script>", "",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<style[^>]*>.*?</style>", "",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<noscript[^>]*>.*?</noscript>", "",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<!--.*?-->", "",
                RegexOptions.Singleline);
            text = Regex.Replace(text, "<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ");
            text = WebUtility.HtmlDecode(text);

            // Usuń pozostałe znaki kontrolne i binarne śmieci
            text = CleanGarbageCharacters(text);

            return text.Trim();
        }

        /// <summary>
        /// Wykrywa czy treść zawiera binarne śmieci (niezdekompresowany Brotli, itp.)
        /// </summary>
        private bool IsGarbageContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;

            // Policz znaki nie-ASCII i znaki kontrolne
            int garbageCount = 0;
            int totalChecked = Math.Min(content.Length, 1000); // Sprawdź pierwsze 1000 znaków

            for (int i = 0; i < totalChecked; i++)
            {
                char c = content[i];
                // Znaki kontrolne (0-31, 127-159) lub wysokie Unicode (>65000)
                if ((c < 32 && c != '\n' && c != '\r' && c != '\t') ||
                    (c >= 127 && c <= 159) ||
                    c > 65000 ||
                    // Typowe bajty Brotli/GZip
                    c == '\uFFFD' || c == '\u0000')
                {
                    garbageCount++;
                }
            }

            // Jeśli więcej niż 10% to śmieci - odrzuć
            double garbageRatio = (double)garbageCount / totalChecked;
            if (garbageRatio > 0.10)
            {
                Log($"Garbage ratio: {garbageRatio:P1} ({garbageCount}/{totalChecked} znaków)", "DEBUG");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Czyści znaki binarne i kontrolne z tekstu
        /// </summary>
        private string CleanGarbageCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                // Akceptuj tylko czytelne znaki
                if ((c >= 32 && c < 127) ||  // Basic ASCII
                    (c >= 160 && c < 8192) || // Extended Latin, Greek, Cyrillic, itp.
                    (c >= 8192 && c < 8304) || // Punctuation
                    c == '\n' || c == '\r' || c == '\t' ||
                    // Polskie znaki
                    "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    // Zamień na spację
                    sb.Append(' ');
                }
            }

            // Usuń wielokrotne spacje
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private string ExtractTitle(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;

            var match = System.Text.RegularExpressions.Regex.Match(html,
                @"<title[^>]*>([^<]+)</title>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
        }

        private EnrichmentResult CreateFallbackResult(string url, string title, string snippet, string reason)
        {
            Log($"Tworzę FALLBACK: {reason}");

            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("[UŻYTO STRESZCZENIA Z BRAVE SEARCH]");
            contentBuilder.AppendLine();

            if (!string.IsNullOrEmpty(title))
            {
                contentBuilder.AppendLine($"TYTUŁ: {title}");
                contentBuilder.AppendLine();
            }

            if (!string.IsNullOrEmpty(snippet))
            {
                contentBuilder.AppendLine(snippet);
            }

            var content = contentBuilder.ToString();

            return new EnrichmentResult
            {
                Success = true, // ZAWSZE sukces - snippet jest OK
                Url = url,
                Content = content,
                Title = title,
                SiteName = GetDomain(url),
                ContentLength = content.Length,
                IsFallback = true,
                FallbackReason = reason,
                Excerpt = snippet
            };
        }

        private string GetDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch { return null; }
        }

        #endregion

        #region Legacy Compatibility Methods

        public async Task<EnrichmentResult> FetchFullContentAsync(string url, CancellationToken ct = default)
        {
            return await EnrichWithFallbackAsync(url, null, null, ct);
        }

        public async Task<EnrichmentResult> EnrichSingleAsync(string url, CancellationToken ct = default)
        {
            return await FetchFullContentAsync(url, ct);
        }

        public async Task<List<EnrichmentResult>> EnrichArticlesWithFallbackAsync(
            List<(string Url, string Title, string Snippet)> articles,
            IProgress<(int completed, int total, string currentUrl)> progress = null,
            CancellationToken ct = default,
            int maxConcurrency = 3) // Obniżone dla Puppeteer
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
                        await Task.Delay(200, ct); // Rate limiting
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

            return results;
        }

        public async Task<List<EnrichmentResult>> EnrichArticlesAsync(
            List<string> urls,
            IProgress<(int completed, int total, string currentUrl)> progress = null,
            CancellationToken ct = default,
            int maxConcurrency = 3)
        {
            var articles = urls.Select(u => (u, "", "")).ToList();
            return await EnrichArticlesWithFallbackAsync(articles, progress, ct, maxConcurrency);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _browser?.Dispose();
            _httpClient?.Dispose();
            _browserLock?.Dispose();
        }

        #endregion
    }

    public class EnrichmentResult
    {
        public bool Success { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public string HtmlContent { get; set; }
        public string Title { get; set; }
        public DateTime? PublishDate { get; set; }
        public string Author { get; set; }
        public string SiteName { get; set; }
        public string FeaturedImage { get; set; }
        public string Language { get; set; }
        public string Excerpt { get; set; }
        public string Source { get; set; }
        public string Error { get; set; }
        public int ContentLength { get; set; }
        public int RawLength { get; set; }
        public bool IsLowQuality { get; set; }
        public bool IsFallback { get; set; }
        public string FallbackReason { get; set; }
    }
}
