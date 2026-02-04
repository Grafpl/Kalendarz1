using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis wzbogacania artykułów o pełną treść
    /// Używany gdy Perplexity/Tavily nie zwraca pełnej treści lub treść jest za krótka
    ///
    /// Funkcje:
    /// - Pobieranie HTML ze strony źródłowej
    /// - Ekstrakcja głównej treści artykułu (usuwanie nawigacji, reklam, komentarzy)
    /// - Czyszczenie tekstu (HTML entities, nadmiarowe whitespace)
    /// - Rate limiting (max 3 równoczesne żądania)
    /// - Cache pobranych treści (24h)
    /// </summary>
    public class ContentEnrichmentService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly Dictionary<string, CachedContent> _contentCache;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromHours(24);

        // Minimalna długość treści do uznania za "pełną"
        private const int MinContentLength = 500;
        // Maksymalna długość treści (ograniczenie dla Claude)
        private const int MaxContentLength = 8000;

        public ContentEnrichmentService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Headers jak przeglądarka - niektóre strony blokują boty
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");

            _rateLimiter = new SemaphoreSlim(3); // Max 3 równoczesne żądania
            _contentCache = new Dictionary<string, CachedContent>();
        }

        /// <summary>
        /// Wzbogać listę artykułów o pełną treść
        /// Przetwarza tylko artykuły z krótką/brakującą treścią
        /// </summary>
        /// <param name="articles">Lista artykułów do wzbogacenia</param>
        /// <param name="maxToEnrich">Maksymalna liczba artykułów do przetworzenia</param>
        /// <param name="ct">Token anulowania</param>
        /// <returns>Lista artykułów z uzupełnioną treścią</returns>
        public async Task<List<RawArticle>> EnrichArticlesAsync(
            List<RawArticle> articles,
            int maxToEnrich = 25,
            CancellationToken ct = default)
        {
            if (articles == null || !articles.Any())
                return articles ?? new List<RawArticle>();

            // Znajdź artykuły wymagające wzbogacenia
            var toEnrich = articles
                .Where(a => NeedsEnrichment(a))
                .OrderByDescending(a => a.RelevanceScore) // Najpierw najważniejsze
                .Take(maxToEnrich)
                .ToList();

            if (!toEnrich.Any())
            {
                Debug.WriteLine("[ContentEnricher] All articles already have sufficient content");
                return articles;
            }

            Debug.WriteLine($"[ContentEnricher] Starting enrichment of {toEnrich.Count} articles...");

            var enrichedCount = 0;
            var failedCount = 0;

            foreach (var article in toEnrich)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var fullContent = await FetchFullContentAsync(article.Url, ct);

                    if (!string.IsNullOrEmpty(fullContent) && fullContent.Length > MinContentLength)
                    {
                        article.FullContent = fullContent;
                        enrichedCount++;

                        var titlePreview = article.Title.Length > 40
                            ? article.Title.Substring(0, 40) + "..."
                            : article.Title;
                        Debug.WriteLine($"[ContentEnricher] ✓ {titlePreview} ({fullContent.Length} chars)");
                    }
                    else
                    {
                        failedCount++;
                        Debug.WriteLine($"[ContentEnricher] ○ {GetDomain(article.Url)} - content too short or unavailable");
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    Debug.WriteLine($"[ContentEnricher] ✗ {GetDomain(article.Url)}: {ex.Message}");
                }

                // Małe opóźnienie między żądaniami
                await Task.Delay(200, ct);
            }

            Debug.WriteLine($"[ContentEnricher] Completed: {enrichedCount} enriched, {failedCount} failed");

            return articles;
        }

        /// <summary>
        /// Sprawdź czy artykuł wymaga wzbogacenia
        /// </summary>
        private bool NeedsEnrichment(RawArticle article)
        {
            if (string.IsNullOrEmpty(article.Url))
                return false;

            // Brak treści lub zbyt krótka
            if (string.IsNullOrEmpty(article.FullContent))
                return true;

            if (article.FullContent.Length < MinContentLength)
                return true;

            // FullContent = Summary (nie pobrano pełnej treści)
            if (article.FullContent == article.Summary)
                return true;

            return false;
        }

        /// <summary>
        /// Pobierz pełną treść artykułu z URL
        /// </summary>
        public async Task<string> FetchFullContentAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Sprawdź cache
            if (_contentCache.TryGetValue(url, out var cached))
            {
                if (DateTime.Now - cached.FetchedAt < _cacheLifetime)
                {
                    Debug.WriteLine($"[ContentEnricher] Cache hit: {GetDomain(url)}");
                    return cached.Content;
                }
                else
                {
                    _contentCache.Remove(url);
                }
            }

            await _rateLimiter.WaitAsync(ct);
            try
            {
                var html = await FetchHtmlAsync(url, ct);
                if (string.IsNullOrEmpty(html))
                    return null;

                var content = ExtractArticleContent(html, url);

                // Zapisz do cache
                if (!string.IsNullOrEmpty(content) && content.Length > MinContentLength)
                {
                    _contentCache[url] = new CachedContent
                    {
                        Content = content,
                        FetchedAt = DateTime.Now
                    };
                }

                return content;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        /// <summary>
        /// Pobierz HTML ze strony
        /// </summary>
        private async Task<string> FetchHtmlAsync(string url, CancellationToken ct)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[ContentEnricher] HTTP {(int)response.StatusCode} for {GetDomain(url)}");
                    return null;
                }

                // Sprawdź Content-Type
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.Contains("html") && !contentType.Contains("text"))
                {
                    Debug.WriteLine($"[ContentEnricher] Non-HTML content: {contentType}");
                    return null;
                }

                return await response.Content.ReadAsStringAsync(ct);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[ContentEnricher] HTTP error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContentEnricher] Fetch error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Wyekstrahuj główną treść artykułu z HTML
        /// Usuwa nawigację, reklamy, komentarze, skrypty
        /// </summary>
        private string ExtractArticleContent(string html, string url)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. Usuń niepotrzebne elementy
            RemoveUnwantedElements(doc);

            // 2. Znajdź główną treść używając różnych selektorów
            var content = FindMainContent(doc);

            if (string.IsNullOrEmpty(content) || content.Length < MinContentLength)
            {
                // Fallback: wszystkie paragrafy
                content = ExtractAllParagraphs(doc);
            }

            if (string.IsNullOrEmpty(content))
                return null;

            // 3. Oczyść tekst
            content = CleanText(content);

            // 4. Ogranicz długość
            if (content.Length > MaxContentLength)
            {
                content = TruncateAtSentence(content, MaxContentLength);
            }

            return content;
        }

        /// <summary>
        /// Usuń niepotrzebne elementy HTML
        /// </summary>
        private void RemoveUnwantedElements(HtmlDocument doc)
        {
            // Selektory elementów do usunięcia
            var selectorsToRemove = new[]
            {
                "//script",
                "//style",
                "//noscript",
                "//iframe",
                "//nav",
                "//header",
                "//footer",
                "//aside",
                "//form",

                // Reklamy
                "//div[contains(@class,'ad')]",
                "//div[contains(@class,'ads')]",
                "//div[contains(@class,'advert')]",
                "//div[contains(@class,'banner')]",
                "//div[contains(@id,'ad')]",

                // Komentarze
                "//div[contains(@class,'comment')]",
                "//div[contains(@class,'comments')]",
                "//section[contains(@class,'comment')]",

                // Sidebar
                "//div[contains(@class,'sidebar')]",
                "//div[contains(@class,'side-bar')]",
                "//div[contains(@id,'sidebar')]",

                // Social media
                "//div[contains(@class,'social')]",
                "//div[contains(@class,'share')]",
                "//div[contains(@class,'sharing')]",

                // Related articles
                "//div[contains(@class,'related')]",
                "//div[contains(@class,'recommended')]",
                "//div[contains(@class,'more-news')]",

                // Newsletter
                "//div[contains(@class,'newsletter')]",
                "//div[contains(@class,'subscribe')]",

                // Navigation
                "//div[contains(@class,'breadcrumb')]",
                "//div[contains(@class,'pagination')]",
                "//div[contains(@class,'menu')]",

                // Author box, tags
                "//div[contains(@class,'author')]",
                "//div[contains(@class,'tags')]",
                "//div[contains(@class,'meta')]"
            };

            foreach (var selector in selectorsToRemove)
            {
                try
                {
                    var nodes = doc.DocumentNode.SelectNodes(selector);
                    if (nodes != null)
                    {
                        foreach (var node in nodes.ToList())
                        {
                            node.Remove();
                        }
                    }
                }
                catch
                {
                    // Ignoruj błędy XPath
                }
            }
        }

        /// <summary>
        /// Znajdź główną treść artykułu
        /// </summary>
        private string FindMainContent(HtmlDocument doc)
        {
            // Selektory w kolejności priorytetów
            var contentSelectors = new[]
            {
                // Specyficzne dla artykułów
                "//article//div[contains(@class,'content')]",
                "//article//div[contains(@class,'body')]",
                "//article//div[contains(@class,'text')]",
                "//article",

                // Popularne klasy CSS dla treści
                "//div[contains(@class,'article-content')]",
                "//div[contains(@class,'article-body')]",
                "//div[contains(@class,'post-content')]",
                "//div[contains(@class,'post-body')]",
                "//div[contains(@class,'entry-content')]",
                "//div[contains(@class,'story-body')]",
                "//div[contains(@class,'news-content')]",
                "//div[contains(@class,'text-content')]",

                // ID
                "//div[@id='content']",
                "//div[@id='article']",
                "//div[@id='main-content']",
                "//div[@id='post-content']",

                // Semantyczne HTML5
                "//main//article",
                "//main",

                // Ogólne - ostatnia deska ratunku
                "//div[contains(@class,'content')]",
                "//div[contains(@class,'body')]"
            };

            foreach (var selector in contentSelectors)
            {
                try
                {
                    var node = doc.DocumentNode.SelectSingleNode(selector);
                    if (node != null)
                    {
                        var text = CleanText(node.InnerText);
                        if (text.Length > MinContentLength)
                        {
                            return text;
                        }
                    }
                }
                catch
                {
                    // Ignoruj błędy XPath
                }
            }

            return null;
        }

        /// <summary>
        /// Wyekstrahuj wszystkie paragrafy jako fallback
        /// </summary>
        private string ExtractAllParagraphs(HtmlDocument doc)
        {
            try
            {
                var paragraphs = doc.DocumentNode.SelectNodes("//p");
                if (paragraphs == null || !paragraphs.Any())
                    return null;

                var texts = paragraphs
                    .Select(p => CleanText(p.InnerText))
                    .Where(t => t.Length > 50) // Filtruj krótkie paragrafy
                    .ToList();

                if (!texts.Any())
                    return null;

                return string.Join("\n\n", texts);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Oczyść tekst z HTML entities i nadmiarowych whitespace
        /// </summary>
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Dekoduj HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            // Usuń znaki sterujące
            text = Regex.Replace(text, @"[\x00-\x1F\x7F]", " ");

            // Normalizuj whitespace (wiele spacji/tabulatorów -> jedna spacja)
            text = Regex.Replace(text, @"[ \t]+", " ");

            // Normalizuj nowe linie (wiele -> max 2)
            text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n");

            // Usuń spacje na początku/końcu linii
            text = Regex.Replace(text, @"^ +| +$", "", RegexOptions.Multiline);

            return text.Trim();
        }

        /// <summary>
        /// Obetnij tekst na granicy zdania
        /// </summary>
        private string TruncateAtSentence(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;

            // Znajdź ostatni koniec zdania przed limitem
            var truncated = text.Substring(0, maxLength);
            var lastSentenceEnd = truncated.LastIndexOfAny(new[] { '.', '!', '?' });

            if (lastSentenceEnd > maxLength * 0.7) // Co najmniej 70% tekstu
            {
                return truncated.Substring(0, lastSentenceEnd + 1);
            }

            // Fallback: obetnij na ostatniej spacji
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > maxLength * 0.8)
            {
                return truncated.Substring(0, lastSpace) + "...";
            }

            return truncated + "...";
        }

        /// <summary>
        /// Pobierz domenę z URL
        /// </summary>
        private string GetDomain(string url)
        {
            try
            {
                return new Uri(url).Host.Replace("www.", "");
            }
            catch
            {
                return url?.Substring(0, Math.Min(30, url?.Length ?? 0)) ?? "unknown";
            }
        }

        /// <summary>
        /// Wyczyść cache
        /// </summary>
        public void ClearCache()
        {
            _contentCache.Clear();
            Debug.WriteLine("[ContentEnricher] Cache cleared");
        }

        /// <summary>
        /// Pobierz statystyki cache
        /// </summary>
        public (int count, long totalBytes) GetCacheStats()
        {
            var count = _contentCache.Count;
            var totalBytes = _contentCache.Values.Sum(c => c.Content?.Length ?? 0) * 2; // UTF-16
            return (count, totalBytes);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }

        /// <summary>
        /// Struktura cache'owanej treści
        /// </summary>
        private class CachedContent
        {
            public string Content { get; set; }
            public DateTime FetchedAt { get; set; }
        }
    }
}
