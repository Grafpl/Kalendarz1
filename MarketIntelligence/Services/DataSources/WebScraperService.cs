using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using HtmlAgilityPack;
using Polly;
using Polly.Retry;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do scrapowania stron bez RSS
    /// Obsługuje GLW (HPAI), giełdy, ceny, sieci handlowe
    /// </summary>
    public class WebScraperService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _rateLimiter;

        public WebScraperService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseCookies = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(45)
            };

            // Realistic browser headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            // Retry policy
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .OrResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            _rateLimiter = new SemaphoreSlim(3); // Max 3 concurrent scraping requests
        }

        #region GLW - HPAI Monitoring

        /// <summary>
        /// Pobierz alerty HPAI z Głównego Inspektoratu Weterynaryjnego
        /// </summary>
        public async Task<List<HpaiAlert>> FetchHpaiAlertsAsync(CancellationToken ct = default)
        {
            var alerts = new List<HpaiAlert>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching GLW HPAI alerts...");

                    // Main HPAI page
                    var urls = new[]
                    {
                        "https://www.wetgiw.gov.pl/nadzor-weterynaryjny/wysoce-zjadliwa-grypa-ptakow-hpai",
                        "https://www.wetgiw.gov.pl/nadzor-weterynaryjny/grypa-ptakow",
                        "https://www.wetgiw.gov.pl/nadzor-weterynaryjny/choroby-zakazne-zwierzat"
                    };

                    foreach (var url in urls)
                    {
                        try
                        {
                            var response = await _retryPolicy.ExecuteAsync(
                                async (token) => await _httpClient.GetAsync(url, token), ct);

                            if (!response.IsSuccessStatusCode)
                            {
                                Debug.WriteLine($"[Scraper] GLW returned {(int)response.StatusCode}");
                                continue;
                            }

                            var html = await response.Content.ReadAsStringAsync(ct);
                            var pageAlerts = ParseGlwHpaiPage(html, url);
                            alerts.AddRange(pageAlerts);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Scraper] Error fetching {url}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] HPAI fetch error: {ex.Message}");
            }

            // Deduplicate by location + date
            return alerts
                .GroupBy(a => $"{a.Location}_{a.ReportDate:yyyyMMdd}")
                .Select(g => g.First())
                .OrderByDescending(a => a.ReportDate)
                .ToList();
        }

        private List<HpaiAlert> ParseGlwHpaiPage(string html, string sourceUrl)
        {
            var alerts = new List<HpaiAlert>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Look for news items, tables, or lists with HPAI information
            var newsItems = doc.DocumentNode.SelectNodes("//article | //div[contains(@class,'news')] | //div[contains(@class,'aktualnosc')] | //tr");

            if (newsItems == null)
            {
                // Try alternative structure - look for links with HPAI content
                var links = doc.DocumentNode.SelectNodes("//a[contains(translate(text(),'HPAI','hpai'),'hpai') or contains(text(),'grypa') or contains(text(),'ognisko')]");
                if (links != null)
                {
                    foreach (var link in links.Take(20))
                    {
                        var text = link.InnerText?.Trim();
                        var href = link.GetAttributeValue("href", "");

                        if (!string.IsNullOrEmpty(text) && text.Length > 20)
                        {
                            var alert = new HpaiAlert
                            {
                                Title = CleanText(text),
                                SourceUrl = href.StartsWith("http") ? href : $"https://www.wetgiw.gov.pl{href}",
                                ReportDate = ExtractDateFromText(text) ?? DateTime.Today,
                                AlertType = DetermineAlertType(text),
                                FetchedAt = DateTime.Now
                            };

                            // Try to extract location
                            var locationMatch = Regex.Match(text, @"woj\.\s*(\w+)|powiat\s+(\w+)|gmin[ay]\s+(\w+)|(\w+skie)", RegexOptions.IgnoreCase);
                            if (locationMatch.Success)
                            {
                                alert.Location = locationMatch.Value;
                                alert.Voivodeship = ExtractVoivodeship(text);
                            }

                            alerts.Add(alert);
                        }
                    }
                }
                return alerts;
            }

            foreach (var item in newsItems.Take(30))
            {
                try
                {
                    var text = item.InnerText?.Trim();
                    if (string.IsNullOrEmpty(text) || text.Length < 30) continue;

                    // Check if it's HPAI related
                    if (!Regex.IsMatch(text, @"hpai|grypa\s+ptak|ognisko|strefa\s+ochronna|strefa\s+nadzoru|likwidacj", RegexOptions.IgnoreCase))
                        continue;

                    var alert = new HpaiAlert
                    {
                        Title = CleanText(text.Length > 200 ? text.Substring(0, 200) + "..." : text),
                        SourceUrl = sourceUrl,
                        ReportDate = ExtractDateFromText(text) ?? DateTime.Today,
                        AlertType = DetermineAlertType(text),
                        FetchedAt = DateTime.Now
                    };

                    // Extract location details
                    var voivMatch = Regex.Match(text, @"woj(?:ewództwo|\.)\s*(\w+)|(\w+skie)\s+(?:woj|powiat)", RegexOptions.IgnoreCase);
                    if (voivMatch.Success)
                    {
                        alert.Voivodeship = voivMatch.Groups[1].Success ? voivMatch.Groups[1].Value : voivMatch.Groups[2].Value;
                    }

                    var countyMatch = Regex.Match(text, @"powiat\s+(\w+)", RegexOptions.IgnoreCase);
                    if (countyMatch.Success)
                    {
                        alert.County = countyMatch.Groups[1].Value;
                    }

                    var municipalityMatch = Regex.Match(text, @"gmin[ay]\s+(\w+)", RegexOptions.IgnoreCase);
                    if (municipalityMatch.Success)
                    {
                        alert.Municipality = municipalityMatch.Groups[1].Value;
                    }

                    alert.Location = string.Join(", ", new[] { alert.Municipality, alert.County, alert.Voivodeship }
                        .Where(s => !string.IsNullOrEmpty(s)));

                    // Extract bird count if mentioned
                    var countMatch = Regex.Match(text, @"(\d[\d\s]*)\s*(?:szt|sztuk|ptaków|kurczak|indyk|kacz|gęsi)", RegexOptions.IgnoreCase);
                    if (countMatch.Success)
                    {
                        if (int.TryParse(countMatch.Groups[1].Value.Replace(" ", ""), out var count))
                        {
                            alert.BirdCount = count;
                        }
                    }

                    // Extract zone radius
                    var radiusMatch = Regex.Match(text, @"(\d+)\s*km", RegexOptions.IgnoreCase);
                    if (radiusMatch.Success)
                    {
                        if (int.TryParse(radiusMatch.Groups[1].Value, out var radius))
                        {
                            alert.ZoneRadiusKm = radius;
                        }
                    }

                    alerts.Add(alert);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Scraper] Error parsing HPAI item: {ex.Message}");
                }
            }

            return alerts;
        }

        private string DetermineAlertType(string text)
        {
            text = text.ToLowerInvariant();

            if (Regex.IsMatch(text, @"nowe\s+ognisko|stwierdzono|wykryto|potwierdzon"))
                return "Nowe ognisko";

            if (Regex.IsMatch(text, @"strefa\s+ochronna|strefa.*3\s*km"))
                return "Strefa ochronna";

            if (Regex.IsMatch(text, @"strefa\s+nadzoru|strefa.*10\s*km"))
                return "Strefa nadzoru";

            if (Regex.IsMatch(text, @"likwidacj|ubój|ubito|utylizacj"))
                return "Likwidacja";

            if (Regex.IsMatch(text, @"zniesienie|cofnięcie|zakończen"))
                return "Zniesienie strefy";

            if (Regex.IsMatch(text, @"ostrzeżenie|alert|uwaga"))
                return "Ostrzeżenie";

            return "Informacja";
        }

        private string ExtractVoivodeship(string text)
        {
            var voivodeships = new[]
            {
                "dolnośląskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
                "łódzkie", "małopolskie", "mazowieckie", "opolskie",
                "podkarpackie", "podlaskie", "pomorskie", "śląskie",
                "świętokrzyskie", "warmińsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
            };

            foreach (var voiv in voivodeships)
            {
                if (text.ToLowerInvariant().Contains(voiv))
                    return voiv;
            }

            return null;
        }

        #endregion

        #region Poultry Prices - Polish Markets

        /// <summary>
        /// Pobierz ceny drobiu z polskich giełd i portali
        /// </summary>
        public async Task<List<PoultryPrice>> FetchPoultryPricesAsync(CancellationToken ct = default)
        {
            var prices = new List<PoultryPrice>();

            // E-drob.pl
            var edrobPrices = await FetchEdrobPricesAsync(ct);
            prices.AddRange(edrobPrices);

            // Giełda drobiowa
            var gieldaPrices = await FetchGieldaDrobiowaPricesAsync(ct);
            prices.AddRange(gieldaPrices);

            // MRiRW (Ministerstwo) - ceny referencyjne
            var mrirwPrices = await FetchMrirwPricesAsync(ct);
            prices.AddRange(mrirwPrices);

            return prices
                .OrderByDescending(p => p.Date)
                .ThenBy(p => p.ProductName)
                .ToList();
        }

        private async Task<List<PoultryPrice>> FetchEdrobPricesAsync(CancellationToken ct)
        {
            var prices = new List<PoultryPrice>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching e-drob.pl prices...");

                    var response = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync("https://e-drob.pl/ceny/", token), ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[Scraper] e-drob.pl returned {(int)response.StatusCode}");
                        return prices;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    // Look for price tables
                    var tables = doc.DocumentNode.SelectNodes("//table");
                    if (tables != null)
                    {
                        foreach (var table in tables)
                        {
                            var rows = table.SelectNodes(".//tr");
                            if (rows == null) continue;

                            foreach (var row in rows)
                            {
                                var cells = row.SelectNodes(".//td | .//th");
                                if (cells == null || cells.Count < 2) continue;

                                var productText = cells[0].InnerText?.Trim();
                                var priceText = cells.Count > 1 ? cells[1].InnerText?.Trim() : null;

                                if (string.IsNullOrEmpty(productText) || string.IsNullOrEmpty(priceText))
                                    continue;

                                // Check if it's a poultry product
                                if (!Regex.IsMatch(productText, @"kurczak|brojler|tuszka|filet|udko|skrzydło|indyk|kaczka", RegexOptions.IgnoreCase))
                                    continue;

                                var price = ParsePrice(priceText);
                                if (price > 0)
                                {
                                    prices.Add(new PoultryPrice
                                    {
                                        Source = "e-drob.pl",
                                        ProductName = CleanText(productText),
                                        Price = price,
                                        Unit = DetermineUnit(productText, priceText),
                                        Currency = "PLN",
                                        Date = DateTime.Today,
                                        FetchedAt = DateTime.Now
                                    });
                                }
                            }
                        }
                    }

                    // Also look for price divs/spans
                    var priceElements = doc.DocumentNode.SelectNodes("//*[contains(@class,'price') or contains(@class,'cena')]");
                    if (priceElements != null)
                    {
                        foreach (var elem in priceElements)
                        {
                            var text = elem.InnerText?.Trim();
                            var price = ParsePrice(text);
                            if (price > 0)
                            {
                                // Try to find associated product name
                                var parent = elem.ParentNode;
                                var productName = parent?.SelectSingleNode(".//*[contains(@class,'product') or contains(@class,'nazwa')]")?.InnerText?.Trim()
                                    ?? parent?.InnerText?.Trim();

                                if (!string.IsNullOrEmpty(productName) && productName.Length < 100)
                                {
                                    prices.Add(new PoultryPrice
                                    {
                                        Source = "e-drob.pl",
                                        ProductName = CleanText(productName),
                                        Price = price,
                                        Unit = "kg",
                                        Currency = "PLN",
                                        Date = DateTime.Today,
                                        FetchedAt = DateTime.Now
                                    });
                                }
                            }
                        }
                    }

                    Debug.WriteLine($"[Scraper] e-drob.pl: {prices.Count} prices");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] e-drob.pl error: {ex.Message}");
            }

            return prices;
        }

        private async Task<List<PoultryPrice>> FetchGieldaDrobiowaPricesAsync(CancellationToken ct)
        {
            var prices = new List<PoultryPrice>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching giełda drobiowa prices...");

                    var urls = new[]
                    {
                        "https://gieldadrobiowasc.pl/",
                        "https://gieldadrobiowa.pl/",
                        "http://www.drobiarstwo.com.pl/ceny/"
                    };

                    foreach (var url in urls)
                    {
                        try
                        {
                            var response = await _retryPolicy.ExecuteAsync(
                                async (token) => await _httpClient.GetAsync(url, token), ct);

                            if (!response.IsSuccessStatusCode) continue;

                            var html = await response.Content.ReadAsStringAsync(ct);
                            var doc = new HtmlDocument();
                            doc.LoadHtml(html);

                            // Generic price extraction
                            var pricePatterns = doc.DocumentNode.SelectNodes("//*[contains(text(),'zł') or contains(text(),'PLN')]");
                            if (pricePatterns != null)
                            {
                                foreach (var elem in pricePatterns.Take(30))
                                {
                                    var text = elem.InnerText?.Trim();
                                    if (string.IsNullOrEmpty(text) || text.Length > 200) continue;

                                    // Look for price patterns
                                    var match = Regex.Match(text, @"([\w\s]+?)[\s:]+(\d+[,.]?\d*)\s*(?:zł|PLN)", RegexOptions.IgnoreCase);
                                    if (match.Success)
                                    {
                                        var productName = match.Groups[1].Value.Trim();
                                        var price = ParsePrice(match.Groups[2].Value);

                                        if (price > 0 && !string.IsNullOrEmpty(productName))
                                        {
                                            prices.Add(new PoultryPrice
                                            {
                                                Source = new Uri(url).Host,
                                                ProductName = CleanText(productName),
                                                Price = price,
                                                Unit = "kg",
                                                Currency = "PLN",
                                                Date = DateTime.Today,
                                                FetchedAt = DateTime.Now
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Scraper] {url} error: {ex.Message}");
                        }
                    }

                    Debug.WriteLine($"[Scraper] Giełda drobiowa: {prices.Count} prices");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] Giełda drobiowa error: {ex.Message}");
            }

            return prices;
        }

        private async Task<List<PoultryPrice>> FetchMrirwPricesAsync(CancellationToken ct)
        {
            var prices = new List<PoultryPrice>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching MRiRW prices...");

                    // MRiRW publishes reference prices
                    var url = "https://www.gov.pl/web/rolnictwo/ceny-produktow-rolnych";
                    var response = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync(url, token), ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[Scraper] MRiRW returned {(int)response.StatusCode}");
                        return prices;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    // Look for Excel/PDF links with price data
                    var links = doc.DocumentNode.SelectNodes("//a[contains(@href,'.xlsx') or contains(@href,'.xls') or contains(@href,'.pdf')]");
                    if (links != null)
                    {
                        foreach (var link in links.Take(5))
                        {
                            var linkText = link.InnerText?.Trim();
                            if (Regex.IsMatch(linkText ?? "", @"drób|mięso|cen", RegexOptions.IgnoreCase))
                            {
                                // Store the link info for potential download
                                var href = link.GetAttributeValue("href", "");
                                Debug.WriteLine($"[Scraper] Found MRiRW price document: {linkText} -> {href}");
                            }
                        }
                    }

                    // Also check for inline price tables
                    var tables = doc.DocumentNode.SelectNodes("//table");
                    if (tables != null)
                    {
                        foreach (var table in tables)
                        {
                            var tableText = table.InnerText?.ToLowerInvariant() ?? "";
                            if (!tableText.Contains("drób") && !tableText.Contains("kurczak") && !tableText.Contains("mięso"))
                                continue;

                            var rows = table.SelectNodes(".//tr");
                            if (rows == null) continue;

                            foreach (var row in rows)
                            {
                                var cells = row.SelectNodes(".//td");
                                if (cells == null || cells.Count < 2) continue;

                                var productName = cells[0].InnerText?.Trim();
                                var priceText = cells[cells.Count - 1].InnerText?.Trim();

                                if (string.IsNullOrEmpty(productName)) continue;

                                var price = ParsePrice(priceText);
                                if (price > 0)
                                {
                                    prices.Add(new PoultryPrice
                                    {
                                        Source = "MRiRW",
                                        ProductName = CleanText(productName),
                                        Price = price,
                                        Unit = "kg",
                                        Currency = "PLN",
                                        Date = DateTime.Today,
                                        PriceType = "Referencyjna",
                                        FetchedAt = DateTime.Now
                                    });
                                }
                            }
                        }
                    }

                    Debug.WriteLine($"[Scraper] MRiRW: {prices.Count} prices");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] MRiRW error: {ex.Message}");
            }

            return prices;
        }

        #endregion

        #region Feed/Grain Prices

        /// <summary>
        /// Pobierz ceny pasz i zbóż
        /// </summary>
        public async Task<List<CommodityPrice>> FetchCommodityPricesAsync(CancellationToken ct = default)
        {
            var prices = new List<CommodityPrice>();

            // MATIF (scraping)
            var matifPrices = await FetchMatifPricesAsync(ct);
            prices.AddRange(matifPrices);

            // Polish grain exchanges
            var polishPrices = await FetchPolishGrainPricesAsync(ct);
            prices.AddRange(polishPrices);

            return prices.OrderByDescending(p => p.Date).ToList();
        }

        private async Task<List<CommodityPrice>> FetchMatifPricesAsync(CancellationToken ct)
        {
            var prices = new List<CommodityPrice>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching MATIF prices...");

                    // Try investing.com for MATIF futures
                    var url = "https://www.investing.com/commodities/milling-wheat";
                    var response = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync(url, token), ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var html = await response.Content.ReadAsStringAsync(ct);

                        // Look for price data
                        var priceMatch = Regex.Match(html, @"data-test=""instrument-price-last""\s*>([0-9,\.]+)<", RegexOptions.IgnoreCase);
                        if (priceMatch.Success)
                        {
                            var price = ParsePrice(priceMatch.Groups[1].Value);
                            if (price > 0)
                            {
                                prices.Add(new CommodityPrice
                                {
                                    Commodity = "Pszenica",
                                    Exchange = "MATIF",
                                    Price = price,
                                    Currency = "EUR",
                                    Unit = "tona",
                                    Date = DateTime.Today,
                                    FetchedAt = DateTime.Now
                                });
                            }
                        }
                    }

                    // Try barchart.com as alternative
                    var barchartUrl = "https://www.barchart.com/futures/quotes/EBM*0/overview";
                    var barchartResponse = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync(barchartUrl, token), ct);

                    if (barchartResponse.IsSuccessStatusCode)
                    {
                        var html = await barchartResponse.Content.ReadAsStringAsync(ct);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var priceNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'priceValue')]");
                        if (priceNode != null)
                        {
                            var price = ParsePrice(priceNode.InnerText);
                            if (price > 0)
                            {
                                prices.Add(new CommodityPrice
                                {
                                    Commodity = "Pszenica konsumpcyjna",
                                    Exchange = "MATIF/Barchart",
                                    Price = price,
                                    Currency = "EUR",
                                    Unit = "tona",
                                    Date = DateTime.Today,
                                    FetchedAt = DateTime.Now
                                });
                            }
                        }
                    }

                    Debug.WriteLine($"[Scraper] MATIF: {prices.Count} prices");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] MATIF error: {ex.Message}");
            }

            return prices;
        }

        private async Task<List<CommodityPrice>> FetchPolishGrainPricesAsync(CancellationToken ct)
        {
            var prices = new List<CommodityPrice>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching Polish grain prices...");

                    // KOWR (Krajowy Ośrodek Wsparcia Rolnictwa)
                    var url = "https://www.kowr.gov.pl/interwencja/ceny-na-rynkach-rolnych";
                    var response = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync(url, token), ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var html = await response.Content.ReadAsStringAsync(ct);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var tables = doc.DocumentNode.SelectNodes("//table");
                        if (tables != null)
                        {
                            foreach (var table in tables)
                            {
                                var tableText = table.InnerText?.ToLowerInvariant() ?? "";
                                if (!tableText.Contains("kukurydz") && !tableText.Contains("pszenica") && !tableText.Contains("soja"))
                                    continue;

                                var rows = table.SelectNodes(".//tr");
                                if (rows == null) continue;

                                foreach (var row in rows.Skip(1)) // Skip header
                                {
                                    var cells = row.SelectNodes(".//td");
                                    if (cells == null || cells.Count < 2) continue;

                                    var commodity = cells[0].InnerText?.Trim();
                                    var priceText = cells[cells.Count - 1].InnerText?.Trim();

                                    if (string.IsNullOrEmpty(commodity)) continue;

                                    var price = ParsePrice(priceText);
                                    if (price > 0)
                                    {
                                        prices.Add(new CommodityPrice
                                        {
                                            Commodity = CleanText(commodity),
                                            Exchange = "KOWR",
                                            Price = price,
                                            Currency = "PLN",
                                            Unit = "tona",
                                            Date = DateTime.Today,
                                            FetchedAt = DateTime.Now
                                        });
                                    }
                                }
                            }
                        }
                    }

                    // Agrolok - giełda zbożowa
                    var agrolokUrl = "https://www.agrolok.pl/ceny/";
                    var agrolokResponse = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync(agrolokUrl, token), ct);

                    if (agrolokResponse.IsSuccessStatusCode)
                    {
                        var html = await agrolokResponse.Content.ReadAsStringAsync(ct);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var priceNodes = doc.DocumentNode.SelectNodes("//*[contains(@class,'price') or contains(@class,'cena')]");
                        if (priceNodes != null)
                        {
                            foreach (var node in priceNodes.Take(20))
                            {
                                var text = node.ParentNode?.InnerText?.Trim() ?? node.InnerText?.Trim();
                                var match = Regex.Match(text, @"(kukurydz|pszenica|soja|śruta|rzepak)\w*[:\s]+(\d+[,.]?\d*)", RegexOptions.IgnoreCase);

                                if (match.Success)
                                {
                                    var price = ParsePrice(match.Groups[2].Value);
                                    if (price > 0)
                                    {
                                        prices.Add(new CommodityPrice
                                        {
                                            Commodity = CleanText(match.Groups[1].Value),
                                            Exchange = "Agrolok",
                                            Price = price,
                                            Currency = "PLN",
                                            Unit = "tona",
                                            Date = DateTime.Today,
                                            FetchedAt = DateTime.Now
                                        });
                                    }
                                }
                            }
                        }
                    }

                    Debug.WriteLine($"[Scraper] Polish grain: {prices.Count} prices");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] Polish grain error: {ex.Message}");
            }

            return prices;
        }

        #endregion

        #region Retail Prices (Biedronka, Lidl)

        /// <summary>
        /// Pobierz ceny detaliczne drobiu z sieci handlowych
        /// </summary>
        public async Task<List<RetailPrice>> FetchRetailPricesAsync(CancellationToken ct = default)
        {
            var prices = new List<RetailPrice>();

            // This is more complex as it requires parsing promotional flyers
            // For now, we'll scrape available price comparison sites

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine("[Scraper] Fetching retail prices...");

                    // ceneo.pl for price comparison
                    var urls = new Dictionary<string, string>
                    {
                        { "Filet z kurczaka", "https://www.ceneo.pl/szukaj-filet+z+kurczaka" },
                        { "Tuszka kurczaka", "https://www.ceneo.pl/szukaj-tuszka+kurczaka" },
                        { "Pierś z kurczaka", "https://www.ceneo.pl/szukaj-pierś+z+kurczaka" }
                    };

                    foreach (var (product, url) in urls)
                    {
                        try
                        {
                            var response = await _retryPolicy.ExecuteAsync(
                                async (token) => await _httpClient.GetAsync(url, token), ct);

                            if (!response.IsSuccessStatusCode) continue;

                            var html = await response.Content.ReadAsStringAsync(ct);
                            var doc = new HtmlDocument();
                            doc.LoadHtml(html);

                            // Look for price elements
                            var priceNodes = doc.DocumentNode.SelectNodes("//*[contains(@class,'price') or contains(@class,'Price')]");
                            if (priceNodes != null)
                            {
                                var priceValues = new List<decimal>();
                                foreach (var node in priceNodes.Take(10))
                                {
                                    var price = ParsePrice(node.InnerText);
                                    if (price > 5 && price < 100) // Reasonable price range per kg
                                    {
                                        priceValues.Add(price);
                                    }
                                }

                                if (priceValues.Any())
                                {
                                    prices.Add(new RetailPrice
                                    {
                                        ProductName = product,
                                        MinPrice = priceValues.Min(),
                                        MaxPrice = priceValues.Max(),
                                        AvgPrice = priceValues.Average(),
                                        Source = "ceneo.pl",
                                        Currency = "PLN",
                                        Unit = "kg",
                                        Date = DateTime.Today,
                                        FetchedAt = DateTime.Now
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Scraper] Error fetching {product}: {ex.Message}");
                        }

                        // Rate limiting between requests
                        await Task.Delay(1000, ct);
                    }

                    Debug.WriteLine($"[Scraper] Retail: {prices.Count} price ranges");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] Retail prices error: {ex.Message}");
            }

            return prices;
        }

        #endregion

        #region News Articles from Scraping Sources

        /// <summary>
        /// Pobierz artykuły ze źródeł wymagających scrapingu (bez RSS)
        /// </summary>
        public async Task<List<RawArticle>> FetchScrapingSourcesAsync(CancellationToken ct = default)
        {
            var articles = new List<RawArticle>();
            var scrapingSources = NewsSourceConfig.GetAllScrapingSources();

            foreach (var source in scrapingSources.Where(s => s.IsActive))
            {
                try
                {
                    var sourceArticles = await FetchFromScrapingSourceAsync(source, ct);
                    articles.AddRange(sourceArticles);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Scraper] Error fetching {source.Name}: {ex.Message}");
                }
            }

            return articles;
        }

        private async Task<List<RawArticle>> FetchFromScrapingSourceAsync(NewsSource source, CancellationToken ct)
        {
            var articles = new List<RawArticle>();

            try
            {
                await _rateLimiter.WaitAsync(ct);
                try
                {
                    Debug.WriteLine($"[Scraper] Fetching {source.Name} from {source.Url}...");

                    var response = await _retryPolicy.ExecuteAsync(
                        async (token) => await _httpClient.GetAsync(source.Url, token), ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[Scraper] {source.Name}: HTTP {(int)response.StatusCode}");
                        return articles;
                    }

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    // Generic article extraction - look for common patterns
                    var articleNodes = doc.DocumentNode.SelectNodes(
                        "//article | " +
                        "//div[contains(@class,'news')] | " +
                        "//div[contains(@class,'aktualnosc')] | " +
                        "//div[contains(@class,'post')] | " +
                        "//li[contains(@class,'news')] | " +
                        "//div[contains(@class,'article')]");

                    if (articleNodes == null || !articleNodes.Any())
                    {
                        // Try link-based extraction
                        articleNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/news/') or contains(@href,'/aktualnosc') or contains(@href,'/artykul')]");
                    }

                    if (articleNodes != null)
                    {
                        foreach (var node in articleNodes.Take(20))
                        {
                            try
                            {
                                var titleNode = node.SelectSingleNode(".//h1 | .//h2 | .//h3 | .//h4 | .//a[@title] | .//a");
                                var title = titleNode?.GetAttributeValue("title", null)
                                    ?? titleNode?.InnerText?.Trim()
                                    ?? node.GetAttributeValue("title", null);

                                if (string.IsNullOrWhiteSpace(title) || title.Length < 15) continue;

                                var linkNode = node.SelectSingleNode(".//a[@href]") ?? (node.Name == "a" ? node : null);
                                var href = linkNode?.GetAttributeValue("href", "");

                                if (string.IsNullOrEmpty(href)) continue;

                                // Make absolute URL
                                if (!href.StartsWith("http"))
                                {
                                    var baseUri = new Uri(source.Url);
                                    href = new Uri(baseUri, href).ToString();
                                }

                                var summary = node.SelectSingleNode(".//p | .//div[contains(@class,'excerpt')] | .//div[contains(@class,'summary')]")?.InnerText?.Trim();

                                // Try to extract date
                                var dateNode = node.SelectSingleNode(".//*[contains(@class,'date') or contains(@class,'data') or contains(@class,'time')]");
                                var dateText = dateNode?.InnerText?.Trim() ?? node.InnerText;
                                var publishDate = ExtractDateFromText(dateText) ?? DateTime.Today;

                                var article = new RawArticle
                                {
                                    SourceId = source.Id,
                                    SourceName = source.Name,
                                    SourceCategory = source.Category,
                                    Language = source.Language,
                                    Title = CleanText(title),
                                    Url = href,
                                    UrlHash = RssFeedService.ComputeHash(href),
                                    Summary = string.IsNullOrEmpty(summary) ? null : CleanText(summary),
                                    PublishDate = publishDate,
                                    FetchedAt = DateTime.Now
                                };

                                // Skip duplicates and old articles
                                if (!string.IsNullOrEmpty(article.Title) &&
                                    !string.IsNullOrEmpty(article.Url) &&
                                    article.PublishDate > DateTime.Now.AddDays(-7))
                                {
                                    articles.Add(article);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Scraper] Error parsing article: {ex.Message}");
                            }
                        }
                    }

                    Debug.WriteLine($"[Scraper] {source.Name}: {articles.Count} articles");
                    source.LastFetchTime = DateTime.Now;
                    source.ConsecutiveFailures = 0;
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scraper] {source.Name} error: {ex.Message}");
                source.ConsecutiveFailures++;
            }

            return articles;
        }

        #endregion

        #region Utility Methods

        private decimal ParsePrice(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // Remove currency symbols and whitespace
            text = Regex.Replace(text, @"[zł\$€PLN\s]", "", RegexOptions.IgnoreCase);

            // Replace comma with dot for parsing
            text = text.Replace(",", ".");

            // Extract first number
            var match = Regex.Match(text, @"(\d+\.?\d*)");
            if (match.Success)
            {
                if (decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                {
                    return price;
                }
            }

            return 0;
        }

        private DateTime? ExtractDateFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Polish date patterns
            var patterns = new[]
            {
                @"(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{4})",  // dd.mm.yyyy
                @"(\d{4})[.\-/](\d{1,2})[.\-/](\d{1,2})",  // yyyy-mm-dd
                @"(\d{1,2})\s+(stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia)\s+(\d{4})",
                @"(\d{1,2})\s+(sty|lut|mar|kwi|maj|cze|lip|sie|wrz|paź|lis|gru)\w*\s+(\d{4})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    try
                    {
                        if (pattern.Contains("yyyy-mm-dd"))
                        {
                            return new DateTime(
                                int.Parse(match.Groups[1].Value),
                                int.Parse(match.Groups[2].Value),
                                int.Parse(match.Groups[3].Value));
                        }
                        else if (pattern.Contains("stycznia"))
                        {
                            var monthNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                {"stycznia", 1}, {"lutego", 2}, {"marca", 3}, {"kwietnia", 4},
                                {"maja", 5}, {"czerwca", 6}, {"lipca", 7}, {"sierpnia", 8},
                                {"września", 9}, {"października", 10}, {"listopada", 11}, {"grudnia", 12}
                            };

                            if (monthNames.TryGetValue(match.Groups[2].Value, out var month))
                            {
                                return new DateTime(
                                    int.Parse(match.Groups[3].Value),
                                    month,
                                    int.Parse(match.Groups[1].Value));
                            }
                        }
                        else if (pattern.Contains("sty|lut"))
                        {
                            var monthAbbrev = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                {"sty", 1}, {"lut", 2}, {"mar", 3}, {"kwi", 4},
                                {"maj", 5}, {"cze", 6}, {"lip", 7}, {"sie", 8},
                                {"wrz", 9}, {"paź", 10}, {"lis", 11}, {"gru", 12}
                            };

                            var abbrev = match.Groups[2].Value.Substring(0, 3).ToLowerInvariant();
                            if (monthAbbrev.TryGetValue(abbrev, out var month))
                            {
                                return new DateTime(
                                    int.Parse(match.Groups[3].Value),
                                    month,
                                    int.Parse(match.Groups[1].Value));
                            }
                        }
                        else
                        {
                            return new DateTime(
                                int.Parse(match.Groups[3].Value),
                                int.Parse(match.Groups[2].Value),
                                int.Parse(match.Groups[1].Value));
                        }
                    }
                    catch
                    {
                        // Continue to next pattern
                    }
                }
            }

            return null;
        }

        private string DetermineUnit(string productText, string priceText)
        {
            var combined = $"{productText} {priceText}".ToLowerInvariant();

            if (combined.Contains("/kg") || combined.Contains("za kg") || combined.Contains("zł/kg"))
                return "kg";

            if (combined.Contains("/szt") || combined.Contains("za szt") || combined.Contains("sztuk"))
                return "szt";

            if (combined.Contains("/t") || combined.Contains("za tonę") || combined.Contains("tona"))
                return "tona";

            return "kg"; // Default
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Remove HTML tags
            var noHtml = Regex.Replace(text, @"<[^>]+>", " ");

            // Decode HTML entities
            noHtml = System.Net.WebUtility.HtmlDecode(noHtml);

            // Normalize whitespace
            noHtml = Regex.Replace(noHtml, @"\s+", " ").Trim();

            return noHtml;
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }

    #region Data Models

    /// <summary>
    /// Alert HPAI (wysoce zjadliwa grypa ptaków)
    /// </summary>
    public class HpaiAlert
    {
        public string Title { get; set; }
        public string AlertType { get; set; } // Nowe ognisko, Strefa ochronna, etc.
        public string Location { get; set; }
        public string Voivodeship { get; set; }
        public string County { get; set; }
        public string Municipality { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? BirdCount { get; set; }
        public int? ZoneRadiusKm { get; set; }
        public DateTime ReportDate { get; set; }
        public string SourceUrl { get; set; }
        public DateTime FetchedAt { get; set; }

        public string Severity => AlertType switch
        {
            "Nowe ognisko" => "Critical",
            "Strefa ochronna" => "Critical",
            "Likwidacja" => "Critical",
            "Strefa nadzoru" => "Warning",
            "Ostrzeżenie" => "Warning",
            _ => "Info"
        };
    }

    /// <summary>
    /// Cena drobiu (skup/hurt)
    /// </summary>
    public class PoultryPrice
    {
        public string Source { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "PLN";
        public string Unit { get; set; } = "kg";
        public string PriceType { get; set; } // Skup, Hurt, Detal, Referencyjna
        public DateTime Date { get; set; }
        public DateTime FetchedAt { get; set; }
    }

    /// <summary>
    /// Cena surowca (zboża, pasze)
    /// </summary>
    public class CommodityPrice
    {
        public string Commodity { get; set; }
        public string Exchange { get; set; }
        public decimal Price { get; set; }
        public decimal? PriceChange { get; set; }
        public decimal? PriceChangePercent { get; set; }
        public string Currency { get; set; }
        public string Unit { get; set; }
        public DateTime Date { get; set; }
        public DateTime FetchedAt { get; set; }
    }

    /// <summary>
    /// Cena detaliczna (sieci handlowe)
    /// </summary>
    public class RetailPrice
    {
        public string ProductName { get; set; }
        public string Retailer { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal AvgPrice { get; set; }
        public string Source { get; set; }
        public string Currency { get; set; } = "PLN";
        public string Unit { get; set; } = "kg";
        public DateTime Date { get; set; }
        public DateTime FetchedAt { get; set; }
    }

    #endregion
}
