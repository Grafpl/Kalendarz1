using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do wyszukiwania wiadomosci przez Perplexity AI
    /// </summary>
    public class PerplexitySearchService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private const string ApiUrl = "https://api.perplexity.ai/chat/completions";
        // Model "llama-3.1-sonar-small-128k-online" wycofany 22.02.2025 - uzywamy nowego "sonar"
        private const string Model = "sonar";

        // Koszt szacunkowy za zapytanie (USD)
        private const decimal CostPerQuery = 0.005m;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
        public string ApiKeyPreview => IsConfigured ? _apiKey.Substring(0, Math.Min(10, _apiKey.Length)) + "..." : "BRAK";

        public PerplexitySearchService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(2);

            _apiKey = Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY")
                      ?? ConfigurationManager.AppSettings["PerplexityApiKey"]
                      ?? "";

            if (IsConfigured)
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        /// <summary>
        /// Testuje polaczenie z Perplexity API
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (false, "Brak klucza API Perplexity. Ustaw PERPLEXITY_API_KEY lub PerplexityApiKey w App.config");
            }

            try
            {
                var results = await SearchAsync("drob Polska ceny 2026", ct);
                if (results.Any())
                {
                    return (true, $"Polaczenie OK. Znaleziono {results.Count} wynikow.");
                }
                return (true, "Polaczenie dziala, brak wynikow testowych.");
            }
            catch (Exception ex)
            {
                return (false, $"Blad polaczenia: {ex.Message}");
            }
        }

        /// <summary>
        /// Wykonuje pojedyncze zapytanie do Perplexity
        /// </summary>
        public async Task<List<PerplexityArticle>> SearchAsync(string query, CancellationToken ct = default)
        {
            var (articles, _) = await SearchWithDebugAsync(query, ct);
            return articles;
        }

        /// <summary>
        /// Wykonuje zapytanie do Perplexity i zwraca rowniez raw response dla debugowania
        /// </summary>
        public async Task<(List<PerplexityArticle> Articles, string DebugInfo)> SearchWithDebugAsync(string query, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (new List<PerplexityArticle>(), "API nie skonfigurowane");
            }

            try
            {
                // Prompt naturalny - NIE prosimy o JSON, zeby Perplexity zwrocil citations
                var requestBody = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Jestes ekspertem od branzy drobiarskiej w Polsce. Podawaj konkretne informacje z wiarygodnych zrodel. Cytuj zrodla uzywajac numerow [1], [2] itd."
                        },
                        new
                        {
                            role = "user",
                            content = $"Znajdz najnowsze wiadomosci i artykuly na temat: {query}. Podaj krotkie podsumowanie kazdej znalezionej informacji z numerem zrodla. Szukaj w polskich portalach: farmer.pl, topagrar.pl, poultry.pl, portalspozywczy.pl, wiadomoscihandlowe.pl, money.pl, pulshr.pl, rp.pl."
                        }
                    },
                    max_tokens = 2000,
                    temperature = 0.3,
                    search_recency_filter = "week",
                    return_citations = true  // Wymuszenie zwracania citations
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiUrl, content, ct);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Perplexity] Error {response.StatusCode}: {responseText}");
                    return (new List<PerplexityArticle>(), $"HTTP {response.StatusCode}: {responseText.Substring(0, Math.Min(500, responseText.Length))}");
                }

                // Wyciagnij content i citations do debugowania
                string debugContent = "";
                int citationsCount = 0;
                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    debugContent = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";

                    // Sprawdz citations
                    if (doc.RootElement.TryGetProperty("citations", out var citationsElement))
                    {
                        citationsCount = citationsElement.GetArrayLength();
                    }
                }
                catch
                {
                    debugContent = responseText.Substring(0, Math.Min(1000, responseText.Length));
                }

                var articles = ParseResponse(responseText, query);
                var citationsInfo = citationsCount > 0 ? $" [CITATIONS: {citationsCount}]" : " [NO CITATIONS]";
                return (articles, $"Content ({debugContent.Length} chars){citationsInfo}: {debugContent.Substring(0, Math.Min(400, debugContent.Length))}...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Perplexity] Search error: {ex.Message}");
                return (new List<PerplexityArticle>(), $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Wykonuje wszystkie zapytania branżowe i zwraca zebrane artykuły
        /// </summary>
        public async Task<List<PerplexityArticle>> FetchAllNewsAsync(
            IProgress<(int completed, int total, string currentQuery)> progress = null,
            bool quickMode = false,
            CancellationToken ct = default)
        {
            var queries = quickMode ? GetQuickQueries() : GetAllQueries();
            var allArticles = new List<PerplexityArticle>();
            var seenUrls = new HashSet<string>();

            int completed = 0;
            foreach (var query in queries)
            {
                if (ct.IsCancellationRequested) break;

                progress?.Report((completed, queries.Count, query));

                try
                {
                    var articles = await SearchAsync(query, ct);
                    foreach (var article in articles)
                    {
                        // Deduplikacja po URL
                        var urlKey = article.Url?.ToLowerInvariant() ?? article.Title.ToLowerInvariant();
                        if (!seenUrls.Contains(urlKey))
                        {
                            seenUrls.Add(urlKey);
                            article.SearchQuery = query;
                            allArticles.Add(article);
                        }
                    }

                    // Maly delay zeby nie przekroczyc rate limit
                    await Task.Delay(200, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Perplexity] Error for query '{query}': {ex.Message}");
                }

                completed++;
            }

            progress?.Report((completed, queries.Count, "Zakończono"));

            return allArticles;
        }

        /// <summary>
        /// Szybkie zapytania (20) dla trybu "szybki briefing"
        /// </summary>
        public List<string> GetQuickQueries()
        {
            return new List<string>
            {
                // HPAI - krytyczne
                "HPAI ptasia grypa Polska 2026 ogniska",
                "ptasia grypa województwo łódzkie luty 2026",
                "GLW komunikat ptasia grypa",

                // Ceny - krytyczne
                "ceny drobiu Polska luty 2026",
                "cena żywca kurczak skup",
                "cena filet z kurczaka hurt",

                // Konkurencja - najważniejsi
                "Cedrob ADQ przejęcie 2026",
                "SuperDrob LipCo CPF",
                "Drosed LDC Indykpol",

                // Sieci - najważniejsze
                "Biedronka promocja kurczak",
                "Dino Polska ekspansja 2026",
                "Makro drób mięso",

                // Import - krytyczne
                "import drobiu Brazylia Polska",
                "Mercosur drób bezcłowy UE",
                "import kurczak Ukraina",

                // Regulacje - pilne
                "KSeF obowiązkowy 2026",
                "regulacje weterynaryjne drób Polska",

                // Pasze
                "ceny kukurydzy pszenicy MATIF",
                "ceny pasz drobiowych Polska",

                // Pogoda
                "pogoda Polska luty 2026 mróz"
            };
        }

        /// <summary>
        /// Wszystkie zapytania (80+) dla pelnego skanu
        /// </summary>
        public List<string> GetAllQueries()
        {
            return new List<string>
            {
                // ═══════════════════════════════════════════════════════════════
                // HPAI / CHOROBY (krytyczne)
                // ═══════════════════════════════════════════════════════════════
                "HPAI Polska 2026 ogniska",
                "ptasia grypa łódzkie 2026",
                "ptasia grypa województwo łódzkie Brzeziny Tomaszów",
                "GLW PIW komunikat ptasia grypa luty 2026",
                "HPAI Europa UE ogniska 2026",
                "szczepionka ptasia grypa UE decyzja 2026",
                "HPAI Niemcy Francja Holandia",
                "Newcastle disease drób Polska",
                "bioasekuracja fermy drób",
                "strefy ochronne HPAI Polska",

                // ═══════════════════════════════════════════════════════════════
                // CENY (krytyczne)
                // ═══════════════════════════════════════════════════════════════
                "ceny drobiu Polska luty 2026",
                "cena żywca drobiowego wolny rynek",
                "cena żywca kurczak kontraktacja",
                "cena filet z kurczaka hurt Polska",
                "cena tuszka kurczak hurt",
                "ceny elementów drobiowych udko skrzydło",
                "ceny kukurydzy pszenicy soi Polska MATIF",
                "cena drobiu UE benchmark",
                "ceny detaliczne kurczak sieci handlowe",
                "relacja żywiec pasza wskaźnik",

                // ═══════════════════════════════════════════════════════════════
                // KONKURENCJA - GIGANCI
                // ═══════════════════════════════════════════════════════════════
                "Cedrob ADQ przejęcie 2026",
                "Cedrob wyniki finansowe inwestycje",
                "SuperDrob LipCo CPF inwestycje Jagiełło",
                "Drosed LDC wyniki finansowe",
                "Drosed Indykpol Konspol konsolidacja",
                "Animex Smithfield Polska drób",
                "Drobimex eksport Szczecin PHW Wiesenhof",
                "Wipasz drób pasze inwestycje",
                "Plukon Polska przejęcia Sieradz",
                "Indykpol LDC drób indyk",

                // ═══════════════════════════════════════════════════════════════
                // KONKURENCJA - REGIONALNI
                // ═══════════════════════════════════════════════════════════════
                "RADDROB Chlebowski drób",
                "System-Drob łódzkie",
                "Drobex drób Polska",
                "Roldrob Ciechanów",
                "Exdrob Kutno drób",
                "Gobarto drób Polska",
                "Konspol drób przetwory",

                // ═══════════════════════════════════════════════════════════════
                // SIECI HANDLOWE
                // ═══════════════════════════════════════════════════════════════
                "Biedronka promocja kurczak luty 2026",
                "Lidl promocja drób mięso",
                "Dino Polska ekspansja 2026 dostawcy",
                "Makro Cash Carry drób HoReCa",
                "Kaufland Polska drób mięso",
                "Carrefour Polska drób dostawcy",
                "Auchan Polska mięso drobiowe",
                "Selgros oferta drobiowa hurt",
                "Stokrotka Polska mięso",
                "Polomarket dostawcy mięso drób",
                "Netto Polska mięso drobiowe",
                "Aldi Polska wejście rynek mięso",
                "E.Leclerc Polska drób",
                "Intermarche Polska mięso",
                "Chata Polska franczyza sklepy",
                "Chorten Polska sklepy",
                "Lewiatan Polska sklepy mięso",
                "Topaz sklepy regionalne",

                // ═══════════════════════════════════════════════════════════════
                // IMPORT / EKSPORT
                // ═══════════════════════════════════════════════════════════════
                "import drobiu Brazylia Polska UE 2026",
                "cena filet brazylijski Europa hurt",
                "Mercosur drób bezcłowy 180 tysięcy ton",
                "import kurczak Ukraina Polska 2026",
                "eksport drobiu Polska UK Bliski Wschód",
                "eksport drobiu Polska Chiny regionalizacja",
                "MHP Ukraina eksport drób UE",
                "BRF JBS Brazylia eksport Europa",

                // ═══════════════════════════════════════════════════════════════
                // REGULACJE
                // ═══════════════════════════════════════════════════════════════
                "KSeF obowiązkowy kwiecień 2026",
                "KSeF Sage Symfonia integracja",
                "normy dobrostan drobiu UE 2026",
                "ESG emisje przemysł drobiowy",
                "wymogi weterynaryjne ubojnia drobiu 2026",
                "certyfikaty jakości drób Polska",
                "KOWR kontrole drób mięso",

                // ═══════════════════════════════════════════════════════════════
                // PASZE / SUROWCE
                // ═══════════════════════════════════════════════════════════════
                "ceny pasz drobiowych Polska 2026",
                "kukurydza MATIF prognoza ceny",
                "soja śruta cena Polska",
                "pszenica paszowa cena",
                "USDA produkcja kukurydzy globalna",

                // ═══════════════════════════════════════════════════════════════
                // POGODA / LOGISTYKA
                // ═══════════════════════════════════════════════════════════════
                "pogoda Polska zima luty 2026 mróz",
                "transport żywiec drób zimą",
                "logistyka chłodnicza drób",

                // ═══════════════════════════════════════════════════════════════
                // ANALIZY / PROGNOZY
                // ═══════════════════════════════════════════════════════════════
                "prognoza cen drobiu Polska 2026",
                "Rabobank drób analiza 2026",
                "rynek drobiu Polska raport 2026",
                "produkcja drobiu GUS statystyki",

                // ═══════════════════════════════════════════════════════════════
                // DOTACJE / INWESTYCJE
                // ═══════════════════════════════════════════════════════════════
                "dotacje ARiMR przetwórstwo mięsa 2026",
                "modernizacja ubojnia drób dotacje",
                "NFOŚiGW chłodnictwo przemysłowe",
                "KPO przetwórstwo spożywcze",

                // ═══════════════════════════════════════════════════════════════
                // TECHNOLOGIE
                // ═══════════════════════════════════════════════════════════════
                "Meyn linie ubojowe drób",
                "automatyzacja ubojnia drób",
                "chłodzenie glikolowe drób",

                // ═══════════════════════════════════════════════════════════════
                // LOKALNE (łódzkie)
                // ═══════════════════════════════════════════════════════════════
                "PIW Brzeziny weterynaria",
                "fermy drobiu łódzkie",
                "hodowcy drobiu region łódzki"
            };
        }

        private List<PerplexityArticle> ParseResponse(string responseText, string query)
        {
            var articles = new List<PerplexityArticle>();

            try
            {
                using var doc = JsonDocument.Parse(responseText);

                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                // ═══════════════════════════════════════════════════════════════
                // NOWY FORMAT: Perplexity Sonar zwraca citations w osobnym polu
                // ═══════════════════════════════════════════════════════════════
                var citations = new List<string>();
                if (doc.RootElement.TryGetProperty("citations", out var citationsElement))
                {
                    foreach (var citation in citationsElement.EnumerateArray())
                    {
                        var url = citation.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            citations.Add(url);
                        }
                    }
                    Debug.WriteLine($"[Perplexity] Found {citations.Count} citations in response");
                }

                // Jesli mamy citations, tworz artykuly z nich
                if (citations.Count > 0)
                {
                    var contentLines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < citations.Count; i++)
                    {
                        var url = citations[i];
                        var article = new PerplexityArticle
                        {
                            Url = url,
                            Source = ExtractDomain(url),
                            PublishDate = DateTime.Today,
                            SearchQuery = query,
                            Title = $"Artykul z {ExtractDomain(url)}",
                            Snippet = ""
                        };

                        // Sprobuj znalezc tytul w tresci (przy [1], [2] itp)
                        var citationMarker = $"[{i + 1}]";
                        foreach (var line in contentLines)
                        {
                            if (line.Contains(citationMarker) || line.Contains(url))
                            {
                                var cleanLine = line.Replace(citationMarker, "").Trim();
                                if (cleanLine.Length > 10 && cleanLine.Length < 200)
                                {
                                    article.Title = cleanLine;
                                    break;
                                }
                            }
                        }

                        // Snippet z contentu
                        if (content.Length > 50)
                        {
                            article.Snippet = content.Substring(0, Math.Min(300, content.Length));
                        }

                        articles.Add(article);
                    }

                    return articles;
                }

                // ═══════════════════════════════════════════════════════════════
                // FALLBACK: JSON array w content lub tekst naturalny
                // ═══════════════════════════════════════════════════════════════
                if (string.IsNullOrEmpty(content)) return articles;

                // Probuj parsowac jako JSON TYLKO jesli wyglada jak prawdziwa tablica JSON
                // (nie mylmy znacznikow [1], [2] z JSON)
                var jsonStart = content.IndexOf("[{");  // Szukaj [{ a nie samego [
                var jsonEnd = content.LastIndexOf("}]");

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonArray = content.Substring(jsonStart, jsonEnd - jsonStart + 2);

                    try
                    {
                        using var arrayDoc = JsonDocument.Parse(jsonArray);
                        foreach (var item in arrayDoc.RootElement.EnumerateArray())
                        {
                            var article = new PerplexityArticle
                            {
                                Title = GetStringProp(item, "title", "Bez tytułu"),
                                Snippet = GetStringProp(item, "snippet", ""),
                                Source = GetStringProp(item, "source", "Perplexity"),
                                Url = GetStringProp(item, "url", ""),
                                SearchQuery = query
                            };

                            var dateStr = GetStringProp(item, "date", "");
                            if (DateTime.TryParse(dateStr, out var date))
                            {
                                article.PublishDate = date;
                            }
                            else
                            {
                                article.PublishDate = DateTime.Today;
                            }

                            if (!string.IsNullOrEmpty(article.Title))
                            {
                                articles.Add(article);
                            }
                        }

                        Debug.WriteLine($"[Perplexity] Parsed {articles.Count} articles from JSON format");
                    }
                    catch
                    {
                        // JSON parsowanie nie powiodlo sie - uzyj parsowania tekstowego
                        Debug.WriteLine("[Perplexity] JSON parsing failed, falling back to text parsing");
                        articles.AddRange(ParseTextResponse(content, query));
                    }
                }
                else
                {
                    // Brak JSON - parsuj tekst naturalny
                    Debug.WriteLine("[Perplexity] No JSON found, using text parsing");
                    articles.AddRange(ParseTextResponse(content, query));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Perplexity] Parse error: {ex.Message}");
            }

            return articles;
        }

        private string ExtractDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "Perplexity";
            }
        }

        private List<PerplexityArticle> ParseTextResponse(string content, string query)
        {
            var articles = new List<PerplexityArticle>();

            // Najpierw usun formatowanie markdown z tresci
            var cleanContent = content;
            // Usun **bold** - zamien na zwykly tekst
            cleanContent = System.Text.RegularExpressions.Regex.Replace(cleanContent, @"\*\*([^*]+)\*\*", "$1");
            // Usun *italic*
            cleanContent = System.Text.RegularExpressions.Regex.Replace(cleanContent, @"\*([^*]+)\*", "$1");
            // Usun naglowki markdown ###
            cleanContent = System.Text.RegularExpressions.Regex.Replace(cleanContent, @"^#+\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Regex do parsowania ponumerowanych sekcji:
            // 1. Tytul lub Zrodlo: Tresc artykulu [1]
            // lub: 1. Zrodlo (data): Tresc artykulu
            var numberedPattern = new System.Text.RegularExpressions.Regex(
                @"^\s*(\d+)\.\s*(.+?)(?=\n\s*\d+\.\s|\n\s*$|$)",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline);

            var matches = numberedPattern.Matches(cleanContent);

            if (matches.Count > 0)
            {
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var fullText = match.Groups[2].Value.Trim();
                    if (string.IsNullOrWhiteSpace(fullText) || fullText.Length < 20) continue;

                    // Usun znaczniki [1], [2] itp. z tresci
                    fullText = System.Text.RegularExpressions.Regex.Replace(fullText, @"\[\d+\]", "").Trim();

                    // Sprobuj wyodrebnic zrodlo i tresc jesli jest dwukropek
                    string sourceName = "Perplexity";
                    string articleContent = fullText;
                    var publishDate = DateTime.Today;

                    // Sprawdz czy format to "Zrodlo (data): tresc" lub "Zrodlo: tresc"
                    var sourceMatch = System.Text.RegularExpressions.Regex.Match(fullText, @"^([A-Za-z][A-Za-z0-9\.\-\s]+(?:\s*\([^)]+\))?)\s*:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (sourceMatch.Success && sourceMatch.Groups[1].Value.Length < 50)
                    {
                        var potentialSource = sourceMatch.Groups[1].Value.Trim();
                        // Sprawdz czy to wyglada jak nazwa zrodla (ma kropke .pl lub jest znane zrodlo)
                        if (potentialSource.Contains(".pl") || potentialSource.Contains(".com") ||
                            potentialSource.ToLower().Contains("farmer") || potentialSource.ToLower().Contains("topagrar") ||
                            potentialSource.ToLower().Contains("poultry") || potentialSource.ToLower().Contains("portal"))
                        {
                            sourceName = potentialSource;
                            articleContent = sourceMatch.Groups[2].Value.Trim();

                            // Wyciagnij date z nazwy zrodla jesli jest
                            var dateMatch = System.Text.RegularExpressions.Regex.Match(sourceName, @"\(([^)]+)\)");
                            if (dateMatch.Success)
                            {
                                sourceName = System.Text.RegularExpressions.Regex.Replace(sourceName, @"\s*\([^)]+\)", "").Trim();
                                var dateStr = dateMatch.Groups[1].Value;
                                publishDate = ParsePolishDate(dateStr);
                            }
                        }
                    }

                    // Tytul to pierwszych max 120 znakow tresci
                    var title = articleContent.Length > 120
                        ? articleContent.Substring(0, 120).TrimEnd() + "..."
                        : articleContent;

                    // Jesli mamy zrodlo, dodaj je do tytulu
                    if (sourceName != "Perplexity")
                    {
                        title = $"[{sourceName}] {title}";
                    }

                    var article = new PerplexityArticle
                    {
                        Title = title,
                        Snippet = articleContent,  // Pelna tresc!
                        Source = sourceName,
                        PublishDate = publishDate,
                        SearchQuery = query,
                        Url = ""  // Brak URL - uzywamy tresci z Perplexity
                    };

                    articles.Add(article);
                }

                Debug.WriteLine($"[Perplexity] Parsed {articles.Count} articles from numbered format");
                return articles;
            }

            // Fallback - stara logika dla innych formatow
            var lines = cleanContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            PerplexityArticle current = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 10) continue;

                // Nowy artykul (zaczyna sie od liczby lub myslnika)
                if (trimmed.Length > 2 && (char.IsDigit(trimmed[0]) || trimmed[0] == '-'))
                {
                    if (current != null && !string.IsNullOrEmpty(current.Title))
                    {
                        articles.Add(current);
                    }

                    var titleText = trimmed.TrimStart('-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ');
                    current = new PerplexityArticle
                    {
                        Title = titleText.Length > 120 ? titleText.Substring(0, 120) + "..." : titleText,
                        Snippet = titleText,
                        Source = "Perplexity",
                        PublishDate = DateTime.Today,
                        SearchQuery = query
                    };
                }
                else if (current != null)
                {
                    current.Snippet += " " + trimmed;
                }
            }

            if (current != null && !string.IsNullOrEmpty(current.Title))
            {
                articles.Add(current);
            }

            Debug.WriteLine($"[Perplexity] Parsed {articles.Count} articles from line-based format");
            return articles;
        }

        private DateTime ParsePolishDate(string dateStr)
        {
            var months = new Dictionary<string, int>
            {
                {"stycznia", 1}, {"lutego", 2}, {"marca", 3}, {"kwietnia", 4},
                {"maja", 5}, {"czerwca", 6}, {"lipca", 7}, {"sierpnia", 8},
                {"września", 9}, {"października", 10}, {"listopada", 11}, {"grudnia", 12}
            };

            var polishDateMatch = System.Text.RegularExpressions.Regex.Match(dateStr, @"(\d+)\s+(\w+)\s+(\d{4})");
            if (polishDateMatch.Success)
            {
                var day = int.Parse(polishDateMatch.Groups[1].Value);
                var monthName = polishDateMatch.Groups[2].Value.ToLower();
                var year = int.Parse(polishDateMatch.Groups[3].Value);
                if (months.TryGetValue(monthName, out var month))
                {
                    try { return new DateTime(year, month, day); } catch { }
                }
            }

            return DateTime.Today;
        }

        private string GetStringProp(JsonElement element, string name, string defaultValue)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? defaultValue;
            }
            return defaultValue;
        }

        public decimal EstimateTotalCost(int queryCount)
        {
            return queryCount * CostPerQuery;
        }
    }

    public class PerplexityArticle
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public DateTime PublishDate { get; set; }
        public string SearchQuery { get; set; }
        public string FullContent { get; set; }
        public bool IsEnriched { get; set; }

        // Hash do deduplikacji
        public string UrlHash => string.IsNullOrEmpty(Url)
            ? Title.GetHashCode().ToString()
            : Url.ToLowerInvariant().GetHashCode().ToString();
    }
}
