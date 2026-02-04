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
                var requestBody = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Jestes pomocnym asystentem wyszukujacym najnowsze wiadomosci z branzy drobiarskiej w Polsce. Odpowiadaj TYLKO w formacie JSON. Szukaj wiadomosci po polsku i z polskich zrodel."
                        },
                        new
                        {
                            role = "user",
                            content = $"Znajdz najnowsze wiadomosci na temat: {query}. Dla kazdej wiadomosci podaj: tytul, krotki opis (2-3 zdania), zrodlo (nazwa portalu), URL, date publikacji. WAZNE: Odpowiedz TYLKO jako tablica JSON bez zadnego tekstu przed ani po: [{{\"title\": \"tytul\", \"snippet\": \"opis\", \"source\": \"portal.pl\", \"url\": \"https://...\", \"date\": \"2026-02-04\"}}]"
                        }
                    },
                    max_tokens = 2000,
                    temperature = 0.2,
                    search_recency_filter = "week"
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

                // Wyciagnij content do debugowania
                string debugContent = "";
                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    debugContent = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                }
                catch
                {
                    debugContent = responseText.Substring(0, Math.Min(1000, responseText.Length));
                }

                var articles = ParseResponse(responseText, query);
                return (articles, $"Content ({debugContent.Length} chars): {debugContent.Substring(0, Math.Min(500, debugContent.Length))}...");
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
                    .GetString();

                if (string.IsNullOrEmpty(content)) return articles;

                // Probuj parsowac jako JSON
                var jsonStart = content.IndexOf('[');
                var jsonEnd = content.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonArray = content.Substring(jsonStart, jsonEnd - jsonStart + 1);

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

                            // Parse date
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
                    }
                    catch
                    {
                        // Jesli JSON nie zadziala, parsuj tekst
                        articles.AddRange(ParseTextResponse(content, query));
                    }
                }
                else
                {
                    // Parsuj jako tekst
                    articles.AddRange(ParseTextResponse(content, query));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Perplexity] Parse error: {ex.Message}");
            }

            return articles;
        }

        private List<PerplexityArticle> ParseTextResponse(string content, string query)
        {
            var articles = new List<PerplexityArticle>();

            // Prosta heurystyka - szukaj linii z tytulami
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            PerplexityArticle current = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Nowy artykul (zaczyna sie od liczby lub myslnika)
                if (trimmed.Length > 2 && (char.IsDigit(trimmed[0]) || trimmed[0] == '-' || trimmed[0] == '*'))
                {
                    if (current != null && !string.IsNullOrEmpty(current.Title))
                    {
                        articles.Add(current);
                    }

                    current = new PerplexityArticle
                    {
                        Title = trimmed.TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' '),
                        Source = "Perplexity",
                        PublishDate = DateTime.Today,
                        SearchQuery = query
                    };
                }
                else if (current != null)
                {
                    // Dodaj do snippetu
                    current.Snippet += " " + trimmed;
                }
            }

            if (current != null && !string.IsNullOrEmpty(current.Title))
            {
                articles.Add(current);
            }

            return articles;
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
