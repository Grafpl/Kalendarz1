using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis do pobierania pelnej tresci artykulow ze stron internetowych
    /// </summary>
    public class ContentEnrichmentService
    {
        private readonly HttpClient _httpClient;

        // Selektory CSS dla polskich portali branżowych
        private static readonly Dictionary<string, string[]> PortalSelectors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Portale rolnicze/branżowe
            { "farmer.pl", new[] { ".article-content", ".field-item", ".node-content", "article" } },
            { "portalspozywczy.pl", new[] { ".article-body", ".text-article", ".content-text", "article" } },
            { "topagrar.pl", new[] { ".article-text", ".article-content", ".news-text", "article" } },
            { "sadyogrody.pl", new[] { ".article-body", ".article-content", "article" } },
            { "meatinfo.pl", new[] { ".news-content", ".article-body", "article" } },
            { "poultryworld.net", new[] { ".article-body", ".article__body", "article" } },
            { "wattagnet.com", new[] { ".article-body", ".body-copy", "article" } },
            { "thepoultrysite.com", new[] { ".article-content", ".post-content", "article" } },

            // Portale biznesowe
            { "money.pl", new[] { ".article-body", ".sc-kPVwWT", "article" } },
            { "bankier.pl", new[] { ".article-content", ".articleBody", "article" } },
            { "pulshr.pl", new[] { ".article-text", ".article-content", "article" } },
            { "rp.pl", new[] { ".article-content", ".article__content", "article" } },
            { "gazetaprawna.pl", new[] { ".article-content", ".article-body", "article" } },
            { "pb.pl", new[] { ".article-content", ".story-content", "article" } },
            { "wnp.pl", new[] { ".article-content", ".news-content", "article" } },
            { "forsal.pl", new[] { ".article-body", ".article-content", "article" } },

            // Portale newsowe
            { "polskieradio.pl", new[] { ".article-content", ".article__content", "article" } },
            { "tvn24.pl", new[] { ".article-body", ".article__body", "article" } },
            { "onet.pl", new[] { ".article-body", ".hyphenate", "article" } },
            { "wp.pl", new[] { ".article--text", ".article-body", "article" } },
            { "interia.pl", new[] { ".article-body", ".news-text", "article" } },
            { "rmf24.pl", new[] { ".article-content", ".articleBody", "article" } },
            { "se.pl", new[] { ".article-body", ".article-content", "article" } },
            { "fakt.pl", new[] { ".article-body", ".article-content", "article" } },

            // Strony rządowe
            { "wetgiw.gov.pl", new[] { ".content", ".article-content", "main", ".main-content" } },
            { "gov.pl", new[] { ".article-content", ".content", "main" } },
            { "minrol.gov.pl", new[] { ".article-content", ".content", "main" } },
            { "arimr.gov.pl", new[] { ".article-content", ".content", "main" } },

            // Portale europejskie
            { "reuters.com", new[] { ".article-body", ".StandardArticleBody_body", "article" } },
            { "euractiv.com", new[] { ".article-content", ".post-content", "article" } },
            { "efsa.europa.eu", new[] { ".content", ".article-content", "main" } },

            // Portale brazylijskie (dla monitoringu konkurencji)
            { "abpa-br.org", new[] { ".content", ".article-content", ".post-content", "article" } },
            { "avisite.com.br", new[] { ".article-body", ".content", "article" } },
            { "aviculturaindustrial.com.br", new[] { ".article-content", ".post-content", "article" } }
        };

        public ContentEnrichmentService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en;q=0.8");
        }

        /// <summary>
        /// Pobiera pelna tresc artykulu z URL
        /// </summary>
        public async Task<EnrichmentResult> FetchFullContentAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                return new EnrichmentResult { Success = false, Error = "Brak URL" };
            }

            try
            {
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

                // Wyciagnij tresc
                var content = ExtractContent(html, url);

                if (string.IsNullOrEmpty(content) || content.Length < 100)
                {
                    return new EnrichmentResult
                    {
                        Success = false,
                        Error = "Nie udało się wyodrębnić treści",
                        Url = url,
                        RawLength = html.Length
                    };
                }

                // Wyciagnij metadane
                var title = ExtractTitle(html);
                var date = ExtractPublishDate(html);
                var author = ExtractAuthor(html);

                return new EnrichmentResult
                {
                    Success = true,
                    Url = url,
                    Content = content,
                    Title = title,
                    PublishDate = date,
                    Author = author,
                    ContentLength = content.Length,
                    RawLength = html.Length
                };
            }
            catch (TaskCanceledException)
            {
                return new EnrichmentResult { Success = false, Error = "Timeout", Url = url };
            }
            catch (HttpRequestException ex)
            {
                return new EnrichmentResult { Success = false, Error = $"HTTP error: {ex.Message}", Url = url };
            }
            catch (Exception ex)
            {
                return new EnrichmentResult { Success = false, Error = ex.Message, Url = url };
            }
        }

        /// <summary>
        /// Pobiera tresc dla wielu artykulow
        /// </summary>
        public async Task<List<EnrichmentResult>> EnrichArticlesAsync(
            List<string> urls,
            IProgress<(int completed, int total, string currentUrl)> progress = null,
            CancellationToken ct = default)
        {
            var results = new List<EnrichmentResult>();
            int completed = 0;

            foreach (var url in urls)
            {
                if (ct.IsCancellationRequested) break;

                progress?.Report((completed, urls.Count, url));

                var result = await FetchFullContentAsync(url, ct);
                results.Add(result);

                // Maly delay zeby nie przeciazyc serwerow
                await Task.Delay(300, ct);

                completed++;
            }

            progress?.Report((completed, urls.Count, "Zakończono"));

            return results;
        }

        #region Content Extraction

        private string ExtractContent(string html, string url)
        {
            // Znajdz odpowiednie selektory dla portalu
            var domain = GetDomain(url);
            var selectors = GetSelectorsForDomain(domain);

            // Probuj kazdy selektor
            foreach (var selector in selectors)
            {
                var content = ExtractBySelector(html, selector);
                if (!string.IsNullOrEmpty(content) && content.Length > 200)
                {
                    return CleanContent(content);
                }
            }

            // Fallback - generyczny ekstraktor
            return ExtractGenericContent(html);
        }

        private string[] GetSelectorsForDomain(string domain)
        {
            // Szukaj pasujacego portalu
            foreach (var portal in PortalSelectors)
            {
                if (domain.Contains(portal.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return portal.Value;
                }
            }

            // Domyslne selektory
            return new[] { "article", ".article-content", ".article-body", ".post-content", ".content", "main" };
        }

        private string GetDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return url;
            }
        }

        private string ExtractBySelector(string html, string selector)
        {
            // Prosta implementacja bez biblioteki HTML - regex based
            // W produkcji uzyłbym HtmlAgilityPack

            if (selector.StartsWith("."))
            {
                // Class selector
                var className = selector.Substring(1);
                var pattern = $@"<[^>]+class\s*=\s*[""'][^""']*{Regex.Escape(className)}[^""']*[""'][^>]*>(.*?)</";
                var match = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                // Proba z div/article
                pattern = $@"<(?:div|article|section)[^>]+class\s*=\s*[""'][^""']*{Regex.Escape(className)}[^""']*[""'][^>]*>([\s\S]*?)</(?:div|article|section)>";
                match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            else
            {
                // Tag selector
                var pattern = $@"<{selector}[^>]*>([\s\S]*?)</{selector}>";
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private string ExtractGenericContent(string html)
        {
            // Usun skrypty i style
            html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<nav[^>]*>[\s\S]*?</nav>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<footer[^>]*>[\s\S]*?</footer>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<header[^>]*>[\s\S]*?</header>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<!--[\s\S]*?-->", "", RegexOptions.IgnoreCase);

            // Znajdz wszystkie akapity
            var paragraphs = new List<string>();
            var matches = Regex.Matches(html, @"<p[^>]*>([\s\S]*?)</p>", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var text = CleanHtmlTags(match.Groups[1].Value).Trim();
                if (text.Length > 50) // Pomijaj krotkie akapity (reklamy itp)
                {
                    paragraphs.Add(text);
                }
            }

            return string.Join("\n\n", paragraphs);
        }

        private string CleanContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            // Usun tagi HTML
            content = CleanHtmlTags(content);

            // Usun wielokrotne biale znaki
            content = Regex.Replace(content, @"\s+", " ");

            // Usun puste linie
            content = Regex.Replace(content, @"\n\s*\n", "\n\n");

            // Usun typowe smieci
            content = Regex.Replace(content, @"(Czytaj (też|także|więcej):?|Zobacz też:?|Przeczytaj również:?|REKLAMA|Advertisement|Loading\.\.\.)", "", RegexOptions.IgnoreCase);

            return content.Trim();
        }

        private string CleanHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";

            // Zamien <br> na nowe linie
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

            // Zamien </p> na nowe linie
            html = Regex.Replace(html, @"</p>", "\n\n", RegexOptions.IgnoreCase);

            // Usun wszystkie tagi
            html = Regex.Replace(html, @"<[^>]+>", "");

            // Dekoduj encje HTML
            html = System.Net.WebUtility.HtmlDecode(html);

            return html;
        }

        #endregion

        #region Metadata Extraction

        private string ExtractTitle(string html)
        {
            // og:title
            var match = Regex.Match(html, @"<meta[^>]+property\s*=\s*[""']og:title[""'][^>]+content\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (match.Success) return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);

            // title tag
            match = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
            if (match.Success) return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);

            // h1
            match = Regex.Match(html, @"<h1[^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase);
            if (match.Success) return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);

            return null;
        }

        private DateTime? ExtractPublishDate(string html)
        {
            // article:published_time
            var match = Regex.Match(html, @"<meta[^>]+property\s*=\s*[""']article:published_time[""'][^>]+content\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date1))
                return date1;

            // datePublished (schema.org)
            match = Regex.Match(html, @"""datePublished""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date2))
                return date2;

            // time tag
            match = Regex.Match(html, @"<time[^>]+datetime\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date3))
                return date3;

            return null;
        }

        private string ExtractAuthor(string html)
        {
            // article:author
            var match = Regex.Match(html, @"<meta[^>]+name\s*=\s*[""']author[""'][^>]+content\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (match.Success) return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);

            // author (schema.org)
            match = Regex.Match(html, @"""author""\s*:\s*(?:\{[^}]*""name""\s*:\s*""([^""]+)""|""([^""]+)"")", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return System.Net.WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
            }

            return null;
        }

        #endregion
    }

    public class EnrichmentResult
    {
        public bool Success { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public DateTime? PublishDate { get; set; }
        public string Author { get; set; }
        public string Error { get; set; }
        public int ContentLength { get; set; }
        public int RawLength { get; set; }
    }
}
