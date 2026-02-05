using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartReader;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// BATTLE-TESTED Content Enrichment Service z VERBOSE LOGGING
    ///
    /// Serwis do pobierania pełnej treści artykułów ze stron internetowych.
    /// FAIL-SAFE: Jeśli scraping się nie uda, zwraca dane z wyszukiwarki jako fallback.
    /// VERBOSE: Loguje szczegółowo każdy krok dla debugowania.
    ///
    /// NuGet: Install-Package SmartReader
    /// </summary>
    public class ContentEnrichmentService
    {
        private readonly HttpClient _httpClient;

        // Minimum 300 znaków dla analizy
        private const int MinContentLength = 300;

        // Lista logów diagnostycznych (dostępna z zewnątrz)
        public List<string> DiagnosticLogs { get; } = new List<string>();

        // Słowa kluczowe wskazujące na blokadę
        private static readonly string[] BlockedIndicators = new[]
        {
            "access denied", "403 forbidden", "blocked", "captcha",
            "verify you are human", "cloudflare", "please wait",
            "checking your browser", "ddos protection", "bot detection",
            "rate limit", "too many requests", "javascript required"
        };

        public ContentEnrichmentService()
        {
            // MAKSYMALNY KAMUFLAŻ: HttpClientHandler z pełną konfiguracją
            var handler = new HttpClientHandler
            {
                // Kompresja: GZip + Deflate + Brotli (jeśli dostępne)
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                UseCookies = true, // WAŻNE: Obsługa cookies jak prawdziwa przeglądarka
                CookieContainer = new CookieContainer()
            };

            // Próba dodania Brotli (dostępne od .NET Core 2.1+)
            try
            {
                handler.AutomaticDecompression |= (DecompressionMethods)4; // Brotli = 4
            }
            catch
            {
                // Brotli niedostępne - ignoruj
            }

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(20); // Timeout na połączenie

            // MAKSYMALNY KAMUFLAŻ: Nagłówki identyczne z Chrome 121
            ConfigureChromeHeaders();
        }

        /// <summary>
        /// Konfiguruje nagłówki HTTP do idealnego udawania Chrome 121 na Windows
        /// </summary>
        private void ConfigureChromeHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            // User-Agent: Chrome 121 na Windows 10/11
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");

            // Accept: Standardowe dla przeglądarki
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");

            // Accept-Language: Polski jako główny
            _httpClient.DefaultRequestHeaders.Add("Accept-Language",
                "pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7");

            // Accept-Encoding: Z Brotli
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");

            // Sec-Ch-Ua: Client Hints dla Chrome 121
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua",
                "\"Not A(Brand\";v=\"99\", \"Google Chrome\";v=\"121\", \"Chromium\";v=\"121\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");

            // Sec-Fetch: Nawigacja z zewnętrznej strony
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");

            // Upgrade-Insecure-Requests: Standardowe dla przeglądarki
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            // Cache-Control: Bez cache
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");

            // Referer: Google (wygląda na kliknięcie z wyszukiwarki)
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");

            // DNT: Do Not Track (opcjonalne, ale niektóre strony sprawdzają)
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        }

        /// <summary>
        /// Dodaje log diagnostyczny
        /// </summary>
        private void Log(string message, string level = "INFO")
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] {message}";
            DiagnosticLogs.Add(logLine);
            Debug.WriteLine($"[ContentEnrichment] {logLine}");
        }

        /// <summary>
        /// Czyści logi diagnostyczne
        /// </summary>
        public void ClearLogs()
        {
            DiagnosticLogs.Clear();
        }

        /// <summary>
        /// FAIL-SAFE + VERBOSE: Pobiera pełną treść artykułu z rozbudowanym logowaniem.
        /// NIGDY nie zwraca Success=false - zawsze jest jakiś content.
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
                Log("BRAK URL - używam fallbacku", "WARNING");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet, "Brak URL");
            }

            try
            {
                // KROK 1: Wykonaj zapytanie HTTP z verbose logowaniem
                var (html, httpResult) = await FetchHtmlWithVerboseLoggingAsync(url, ct);

                // Jeśli HTTP failed - fallback
                if (!httpResult.Success)
                {
                    Log($"HTTP FAILED - używam fallbacku: {httpResult.Error}", "WARNING");
                    return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                        $"[BLOKADA HTTP {httpResult.StatusCode}] {httpResult.Error}");
                }

                // KROK 2: Sprawdź czy treść HTML nie jest stroną blokady
                if (IsBlockedPage(html))
                {
                    Log("WYKRYTO STRONĘ BLOKADY (Cloudflare/Captcha) - używam fallbacku", "WARNING");
                    return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                        "[BLOKADA STRONY - CAPTCHA/CLOUDFLARE]");
                }

                // KROK 3: Użyj SmartReader do ekstrakcji treści
                Log("Uruchamiam SmartReader...");
                var extractionResult = ExtractContentWithSmartReader(url, html);

                if (!extractionResult.Success)
                {
                    Log($"SmartReader FAILED: {extractionResult.Error} - używam fallbacku", "WARNING");
                    return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                        $"[SMARTREADER FAILED] {extractionResult.Error}");
                }

                // KROK 4: Sprawdź długość treści
                if (extractionResult.Content.Length < MinContentLength)
                {
                    Log($"Treść za krótka ({extractionResult.Content.Length} < {MinContentLength}) - używam fallbacku", "WARNING");
                    return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                        $"[TREŚĆ ZA KRÓTKA: {extractionResult.Content.Length} znaków]");
                }

                // SUKCES!
                Log($"SUKCES! Wyodrębniono {extractionResult.Content.Length} znaków", "SUCCESS");
                Log($"========== END: {GetDomain(url)} - OK ==========");

                return extractionResult;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}", "ERROR");
                Log($"========== END: {GetDomain(url)} - EXCEPTION ==========");
                return CreateFallbackResult(url, fallbackTitle, fallbackSnippet,
                    $"[EXCEPTION] {ex.Message}");
            }
        }

        /// <summary>
        /// VERBOSE: Pobiera HTML z rozbudowanym logowaniem każdego szczegółu odpowiedzi
        /// </summary>
        private async Task<(string Html, HttpFetchResult Result)> FetchHtmlWithVerboseLoggingAsync(
            string url, CancellationToken ct)
        {
            var result = new HttpFetchResult { Url = url };

            try
            {
                Log($"Wysyłam GET request...");
                var stopwatch = Stopwatch.StartNew();

                var response = await _httpClient.GetAsync(url, ct);

                stopwatch.Stop();
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                result.StatusCode = (int)response.StatusCode;

                // ═══════════════════════════════════════════════════════════
                // VERBOSE: Loguj szczegóły odpowiedzi HTTP
                // ═══════════════════════════════════════════════════════════
                Log($"Response Time: {stopwatch.ElapsedMilliseconds}ms");
                Log($"Status Code: {(int)response.StatusCode} {response.StatusCode}");

                // Loguj kluczowe nagłówki serwera
                if (response.Headers.Server != null)
                {
                    var serverHeader = string.Join(", ", response.Headers.Server.Select(s => s.ToString()));
                    Log($"Server: {serverHeader}");
                    result.ServerHeader = serverHeader;

                    // Wykryj Cloudflare
                    if (serverHeader.ToLower().Contains("cloudflare"))
                    {
                        Log("⚠️ WYKRYTO CLOUDFLARE!", "WARNING");
                    }
                }

                // Content-Type
                if (response.Content.Headers.ContentType != null)
                {
                    Log($"Content-Type: {response.Content.Headers.ContentType}");
                    result.ContentType = response.Content.Headers.ContentType.ToString();
                }

                // Content-Length
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    Log($"Content-Length: {response.Content.Headers.ContentLength} bytes");
                }

                // Sprawdź nagłówki anty-botowe
                foreach (var header in response.Headers)
                {
                    var headerName = header.Key.ToLower();
                    if (headerName.Contains("cf-") || headerName.Contains("x-") ||
                        headerName.Contains("rate") || headerName.Contains("captcha"))
                    {
                        Log($"Header [{header.Key}]: {string.Join(", ", header.Value)}", "DEBUG");
                    }
                }

                // ═══════════════════════════════════════════════════════════
                // Sprawdź Status Code
                // ═══════════════════════════════════════════════════════════
                if (!response.IsSuccessStatusCode)
                {
                    result.Success = false;
                    result.Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";

                    // Specjalne komunikaty dla znanych kodów
                    switch ((int)response.StatusCode)
                    {
                        case 403:
                            Log("❌ 403 FORBIDDEN - Strona zablokowała dostęp (prawdopodobnie anty-bot)", "ERROR");
                            break;
                        case 404:
                            Log("❌ 404 NOT FOUND - Strona nie istnieje", "ERROR");
                            break;
                        case 429:
                            Log("❌ 429 TOO MANY REQUESTS - Rate limiting!", "ERROR");
                            break;
                        case 503:
                            Log("❌ 503 SERVICE UNAVAILABLE - Serwer przeciążony lub blokada", "ERROR");
                            break;
                        default:
                            Log($"❌ HTTP {(int)response.StatusCode} - {response.ReasonPhrase}", "ERROR");
                            break;
                    }

                    return (null, result);
                }

                // ═══════════════════════════════════════════════════════════
                // Pobierz treść HTML
                // ═══════════════════════════════════════════════════════════
                var html = await response.Content.ReadAsStringAsync();
                result.HtmlLength = html.Length;

                Log($"Pobrano HTML: {html.Length} znaków");

                // VERBOSE: Pokaż pierwsze 300 znaków treści (dla debugowania)
                var preview = html.Length > 300 ? html.Substring(0, 300) : html;
                preview = preview.Replace("\n", " ").Replace("\r", " ").Replace("  ", " ");
                Log($"HTML Preview: {preview}...", "DEBUG");

                // Sprawdź czy HTML wygląda na prawdziwą stronę
                if (html.Length < 500)
                {
                    Log($"⚠️ HTML bardzo krótki ({html.Length} znaków) - możliwe przekierowanie lub blokada", "WARNING");
                }

                result.Success = true;
                return (html, result);
            }
            catch (TaskCanceledException)
            {
                result.Success = false;
                result.Error = "TIMEOUT - strona nie odpowiedziała w czasie";
                Log($"❌ TIMEOUT po {_httpClient.Timeout.TotalSeconds}s", "ERROR");
                return (null, result);
            }
            catch (HttpRequestException ex)
            {
                result.Success = false;
                result.Error = $"HTTP Error: {ex.Message}";
                Log($"❌ HTTP Exception: {ex.Message}", "ERROR");

                // Sprawdź przyczynę
                if (ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
                {
                    Log("Możliwy problem z certyfikatem SSL", "WARNING");
                }
                if (ex.Message.Contains("Name or service not known") || ex.Message.Contains("DNS"))
                {
                    Log("Problem z DNS - domena może nie istnieć", "WARNING");
                }

                return (null, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Exception: {ex.Message}";
                Log($"❌ Exception: {ex.GetType().Name}: {ex.Message}", "ERROR");
                return (null, result);
            }
        }

        /// <summary>
        /// Sprawdza czy HTML to strona blokady (Cloudflare, CAPTCHA, etc.)
        /// </summary>
        private bool IsBlockedPage(string html)
        {
            if (string.IsNullOrEmpty(html)) return false;

            var htmlLower = html.ToLower();

            foreach (var indicator in BlockedIndicators)
            {
                if (htmlLower.Contains(indicator))
                {
                    Log($"Wykryto wskaźnik blokady: '{indicator}'", "WARNING");
                    return true;
                }
            }

            // Sprawdź czy to strona Cloudflare challenge
            if (htmlLower.Contains("cf-browser-verification") ||
                htmlLower.Contains("__cf_chl") ||
                htmlLower.Contains("cf_chl_prog"))
            {
                Log("Wykryto Cloudflare Browser Challenge", "WARNING");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ekstrahuje treść ze SmartReader z logowaniem
        /// </summary>
        private EnrichmentResult ExtractContentWithSmartReader(string url, string html)
        {
            try
            {
                var article = Reader.ParseArticle(url, html);

                if (article == null)
                {
                    Log("SmartReader zwrócił null", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = "SmartReader zwrócił null",
                        RawLength = html.Length
                    };
                }

                if (!article.IsReadable)
                {
                    Log("SmartReader: Artykuł oznaczony jako nieczytelny", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = "Artykuł nieczytelny (IsReadable=false)",
                        RawLength = html.Length
                    };
                }

                var textContent = article.TextContent?.Trim();

                if (string.IsNullOrEmpty(textContent))
                {
                    Log("SmartReader: Wyodrębniona treść jest pusta", "WARNING");
                    return new EnrichmentResult
                    {
                        Success = false,
                        Url = url,
                        Error = "Wyodrębniona treść jest pusta",
                        RawLength = html.Length
                    };
                }

                Log($"SmartReader OK: Tytuł='{article.Title?.Substring(0, Math.Min(50, article.Title?.Length ?? 0))}...'");
                Log($"SmartReader OK: Treść={textContent.Length} znaków, HTML={html.Length} znaków");

                return new EnrichmentResult
                {
                    Success = true,
                    Url = url,
                    Content = textContent,
                    HtmlContent = article.Content,
                    Title = article.Title,
                    PublishDate = article.PublicationDate,
                    Author = article.Author,
                    SiteName = article.SiteName ?? GetDomain(url),
                    FeaturedImage = article.FeaturedImage,
                    Language = article.Language,
                    ContentLength = textContent.Length,
                    RawLength = html.Length,
                    Excerpt = article.Excerpt,
                    IsFallback = false
                };
            }
            catch (Exception ex)
            {
                Log($"SmartReader EXCEPTION: {ex.Message}", "ERROR");
                return new EnrichmentResult
                {
                    Success = false,
                    Url = url,
                    Error = $"SmartReader exception: {ex.Message}",
                    RawLength = html.Length
                };
            }
        }

        /// <summary>
        /// Tworzy fallback result z danymi z wyszukiwarki
        /// </summary>
        private EnrichmentResult CreateFallbackResult(string url, string title, string snippet, string reason)
        {
            Log($"Tworzę FALLBACK: {reason}");

            // Buduj treść fallback
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("[BLOKADA STRONY - UŻYTO SKRÓTU Z WYSZUKIWARKI]");
            contentBuilder.AppendLine();
            contentBuilder.AppendLine($"Powód: {reason}");
            contentBuilder.AppendLine();

            if (!string.IsNullOrEmpty(title))
            {
                contentBuilder.AppendLine($"TYTUŁ: {title}");
                contentBuilder.AppendLine();
            }

            if (!string.IsNullOrEmpty(snippet))
            {
                contentBuilder.AppendLine("STRESZCZENIE Z BRAVE SEARCH:");
                contentBuilder.AppendLine(snippet);
                contentBuilder.AppendLine();
            }

            contentBuilder.AppendLine("[Przeczytaj pełny artykuł na stronie źródłowej]");

            var content = contentBuilder.ToString();

            return new EnrichmentResult
            {
                Success = true, // ZAWSZE sukces - mamy przynajmniej fallback
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

        /// <summary>
        /// Pobiera pełną treść artykułu z URL (stara metoda - zachowana dla kompatybilności)
        /// </summary>
        public async Task<EnrichmentResult> FetchFullContentAsync(string url, CancellationToken ct = default)
        {
            return await EnrichWithFallbackAsync(url, null, null, ct);
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
                        await Task.Delay(150, ct);

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
    /// Wynik zapytania HTTP (dla verbose logging)
    /// </summary>
    public class HttpFetchResult
    {
        public bool Success { get; set; }
        public string Url { get; set; }
        public int StatusCode { get; set; }
        public string Error { get; set; }
        public long ResponseTimeMs { get; set; }
        public string ServerHeader { get; set; }
        public string ContentType { get; set; }
        public int HtmlLength { get; set; }
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
