using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis do analizy artykulow przez Claude API
    /// </summary>
    public class ClaudeAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        // Modele Claude 4.5 - aktualne nazwy (luty 2026)
        // Dokumentacja: https://platform.claude.com/docs/en/about-claude/models/overview
        public const string SonnetModel = "claude-sonnet-4-5-20250929";
        public const string HaikuModel = "claude-haiku-4-5-20251001";

        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";

        // Koszty za 1M tokenow (szacunkowe, USD)
        private const decimal SonnetInputCost = 3.0m;
        private const decimal SonnetOutputCost = 15.0m;
        private const decimal HaikuInputCost = 0.25m;
        private const decimal HaikuOutputCost = 1.25m;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
        public string ApiKeyPreview => IsConfigured ? _apiKey.Substring(0, Math.Min(15, _apiKey.Length)) + "..." : "BRAK";
        public string CurrentSonnetModel => SonnetModel;

        public ClaudeAnalysisService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(2);

            // Probuj pobrac klucz API z roznych zrodel
            _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                      ?? ConfigurationManager.AppSettings["ClaudeApiKey"]
                      ?? ConfigurationManager.AppSettings["AnthropicApiKey"]
                      ?? "";

            if (IsConfigured)
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
            }
        }

        /// <summary>
        /// Testuje polaczenie z Claude API
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (false, "Brak klucza API Claude. Ustaw ANTHROPIC_API_KEY lub ClaudeApiKey w App.config");
            }

            try
            {
                var response = await CallClaudeAsync("Odpowiedz tylko: OK", HaikuModel, 50, ct);
                if (response.Contains("OK"))
                {
                    return (true, $"Polaczenie OK. Model: {HaikuModel}");
                }
                return (true, $"Polaczenie dziala, odpowiedz: {response}");
            }
            catch (Exception ex)
            {
                return (false, $"Blad polaczenia: {ex.Message}");
            }
        }

        /// <summary>
        /// Analizuje artykul i generuje pelna analize dla 3 rol
        /// </summary>
        public async Task<ArticleAnalysisResult> AnalyzeArticleAsync(
            string title,
            string content,
            string source,
            string businessContext,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return CreateStubAnalysis(title, "Analiza AI niedostepna - brak klucza API Claude.");
            }

            var prompt = CreateAnalysisPrompt(title, content, source, businessContext);

            try
            {
                var response = await CallClaudeAsync(prompt, SonnetModel, 4000, ct);
                return ParseAnalysisResponse(response, title);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeAnalysis] Blad analizy: {ex.Message}");
                return CreateStubAnalysis(title, $"Blad analizy AI: {ex.Message}");
            }
        }

        /// <summary>
        /// Szybkie filtrowanie artykulow przez Haiku
        /// </summary>
        public async Task<List<QuickFilterResult>> QuickFilterArticlesAsync(
            List<(string Title, string Snippet)> articles,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                // Przepusc wszystko jesli brak API
                var results = new List<QuickFilterResult>();
                foreach (var a in articles)
                {
                    results.Add(new QuickFilterResult { Title = a.Title, IsRelevant = true, Reason = "Brak API - przepuszczam" });
                }
                return results;
            }

            var prompt = CreateFilterPrompt(articles);

            try
            {
                var response = await CallClaudeAsync(prompt, HaikuModel, 2000, ct);
                return ParseFilterResponse(response, articles);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeFilter] Blad filtrowania: {ex.Message}");
                // W razie bledu przepusc wszystko
                var results = new List<QuickFilterResult>();
                foreach (var a in articles)
                {
                    results.Add(new QuickFilterResult { Title = a.Title, IsRelevant = true, Reason = $"Blad: {ex.Message}" });
                }
                return results;
            }
        }

        /// <summary>
        /// Tlumaczenie artykulu z EN/PT/ES/FR/DE na polski
        /// </summary>
        public async Task<(string TranslatedTitle, string TranslatedContent)> TranslateArticleAsync(
            string title,
            string content,
            string sourceLanguage,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (title, content);
            }

            var prompt = $@"Przetlumacz ponizszy tytul i tresc artykulu z jezyka {sourceLanguage} na polski.
Zachowaj wszystkie fakty, liczby, daty, nazwy firm i osob.
Odpowiedz TYLKO w formacie JSON:
{{
  ""title"": ""przetlumaczony tytul"",
  ""content"": ""przetlumaczona tresc""
}}

TYTUL:
{title}

TRESC:
{content}";

            try
            {
                var response = await CallClaudeAsync(prompt, HaikuModel, 2000, ct);
                var json = ExtractJson(response);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                return (
                    root.GetProperty("title").GetString() ?? title,
                    root.GetProperty("content").GetString() ?? content
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeTranslate] Blad tlumaczenia: {ex.Message}");
                return (title, content);
            }
        }

        /// <summary>
        /// Generuje streszczenie dnia
        /// </summary>
        public async Task<DailySummaryResult> GenerateDailySummaryAsync(
            List<BriefingArticle> articles,
            string businessContext,
            CancellationToken ct = default)
        {
            if (!IsConfigured || articles.Count == 0)
            {
                return new DailySummaryResult
                {
                    Summary = "Streszczenie niedostepne - brak klucza API lub artykulow.",
                    TopThreats = new List<string> { "Brak danych" },
                    TopOpportunities = new List<string> { "Brak danych" },
                    MarketMood = "neutral"
                };
            }

            var articleSummaries = new StringBuilder();
            foreach (var a in articles.Take(20)) // Max 20 artykulow
            {
                articleSummaries.AppendLine($"- [{a.Category}] {a.Title}");
            }

            var prompt = $@"Jestes analitykiem rynku drobiarskiego dla Ubojni Drobiu Piorkowscy.

KONTEKST BIZNESOWY:
{businessContext}

DZISIEJSZE ARTYKULY:
{articleSummaries}

Wygeneruj streszczenie poranne w formacie JSON:
{{
  ""summary"": ""2-3 zdania podsumowania najwazniejszych wydarzen. Uzyj konkretnych faktow, liczb, nazw."",
  ""top_threats"": [""zagrozenie 1"", ""zagrozenie 2"", ""zagrozenie 3""],
  ""top_opportunities"": [""szansa 1"", ""szansa 2""],
  ""market_mood"": ""positive|negative|neutral"",
  ""key_numbers"": [
    {{""label"": ""etykieta"", ""value"": ""wartosc"", ""trend"": ""up|down|stable""}}
  ],
  ""urgent_actions"": [""pilne dzialanie 1"", ""pilne dzialanie 2""]
}}";

            try
            {
                var response = await CallClaudeAsync(prompt, SonnetModel, 1500, ct);
                return ParseDailySummaryResponse(response);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeSummary] Blad generowania streszczenia: {ex.Message}");
                return new DailySummaryResult
                {
                    Summary = $"Blad generowania streszczenia: {ex.Message}",
                    TopThreats = new List<string>(),
                    TopOpportunities = new List<string>(),
                    MarketMood = "neutral"
                };
            }
        }

        #region Private Methods

        private async Task<string> CallClaudeAsync(string prompt, string model, int maxTokens, CancellationToken ct)
        {
            var requestBody = new
            {
                model = model,
                max_tokens = maxTokens,
                system = "Jestes asystentem AI ktory ZAWSZE odpowiada TYLKO w formacie JSON. NIGDY nie uzywaj markdown, NIGDY nie dodawaj ``` przed ani po JSON. Odpowiadaj CZYSTYM JSON bez zadnych dodatkowych znakow.",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ApiUrl, content, ct);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Claude API error {response.StatusCode}: {responseText}");
            }

            using var doc = JsonDocument.Parse(responseText);
            var textContent = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return textContent ?? "";
        }

        private string CreateAnalysisPrompt(string title, string content, string source, string businessContext)
        {
            return $@"Jestes starszym analitykiem rynku drobiarskiego dla Ubojni Drobiu Piorkowscy w Brzezinach.
Twoja analiza musi byc KONKRETNA, z FAKTAMI, LICZBAMI, NAZWAMI. Zadnych ogolnikow!

KONTEKST BIZNESOWY FIRMY:
{businessContext}

ARTYKUL DO ANALIZY:
Zrodlo: {source}
Tytul: {title}
Tresc: {content}

Odpowiedz TYLKO w formacie JSON (bez markdown, bez ```):
{{
  ""streszczenie"": ""MINIMUM 5 zdan szczegolowego streszczenia. Podaj WSZYSTKIE fakty, liczby, daty, nazwy z artykulu. Jesli artykul jest krotki, UZUPELNIJ wlasnymi informacjami o temacie bazujac na wiedzy o branzy drobiarskiej. NIGDY nie pisz mniej niz 5 zdan."",

  ""kim_jest"": ""Wyjasnienie KAZDEGO podmiotu wspomnianego w artykule (firmy, osoby, organizacje, instytucje). Dla kazdego podmiotu: 2-4 zdania opisu, powiazania kapitalowe, wlasciciele, skala dzialalnosci, znaczenie dla branzy. Format: '• NAZWA — opis...'"",

  ""analiza_ceo"": ""Analiza strategiczna dla CEO/wlasciciela. Co to oznacza dla firmy? Jakie ryzyka? Jakie decyzje podjac? Odnosic sie do konkretnej sytuacji Piorkowscy (straty 2M/mies, import brazylijski, HPAI w lodzkim)."",

  ""analiza_handlowiec"": ""Analiza dla handlowca. Jak to wplywa na ceny? Jakie argumenty dla klientow (Makro, Biedronka, Dino)? Jakie szanse sprzedazowe? Konkretne dzialania."",

  ""analiza_zakupowiec"": ""Analiza dla zakupowca. Jak to wplywa na hodowcow? Na ceny zywca? Na pasze? Konkretne dzialania wobec dostawcow (Sukiennikowa, Kaczmarek, Wojciechowski)."",

  ""dzialania_ceo"": [
    ""[PILNE/WAZNE/DO_ROZWAZENIA] Konkretne dzialanie. Odpowiedzialny: osoba. Termin: kiedy."",
    ""Kolejne dzialanie...""
  ],

  ""dzialania_handlowiec"": [
    ""[PILNE/WAZNE] Konkretne dzialanie dla handlowca. Odpowiedzialny: Jola/Maja/Radek/Teresa/Ania. Termin: kiedy.""
  ],

  ""dzialania_zakupowiec"": [
    ""[PILNE/WAZNE] Konkretne dzialanie dla zakupowca. Termin: kiedy.""
  ],

  ""kategoria"": ""HPAI|Ceny|Konkurencja|Regulacje|Eksport|Import|Klienci|Koszty|Pogoda|Logistyka|Inwestycje"",
  ""severity"": ""critical|warning|positive|info"",
  ""tagi"": [""tag1"", ""tag2"", ""tag3""]
}}

PAMIETAJ:
- Streszczenie MINIMUM 5 zdan z faktami!
- Kim_jest musi wyjasnic KAZDY podmiot!
- Dzialania musza byc KONKRETNE z terminem i odpowiedzialnym!
- ZADNYCH ogolnikow typu 'monitoruj sytuacje' - tylko konkretne kroki!";
        }

        private string CreateFilterPrompt(List<(string Title, string Snippet)> articles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ocen czy ponizsze artykuly sa ISTOTNE dla polskiej ubojni drobiu (kurczaki).");
            sb.AppendLine();
            sb.AppendLine("ODRZUC artykuly o:");
            sb.AppendLine("- przepiorkach, strusiach, golebiach, gesiach (chyba ze HPAI)");
            sb.AppendLine("- lokalnych fermach w USA, Azji, Afryce, Ameryce Poludniowej (chyba ze eksport do UE)");
            sb.AppendLine("- indykach (chyba ze Indykpol, Cedrob, lub duzy gracz polski)");
            sb.AppendLine("- kaczkach (chyba ze HPAI lub duzy producent)");
            sb.AppendLine();
            sb.AppendLine("AKCEPTUJ artykuly o:");
            sb.AppendLine("- kurczakach, drobiu, ubojniach, filetach, tuszkach, zywcu");
            sb.AppendLine("- HPAI, ptasiej grypie w Polsce/UE");
            sb.AppendLine("- cenach drobiu, pasz, kukurydzy, soi");
            sb.AppendLine("- polskich firmach: Cedrob, SuperDrob, Drosed, Animex, Drobimex, Plukon, Wipasz, Indykpol");
            sb.AppendLine("- sieciach handlowych: Biedronka, Lidl, Dino, Makro, Carrefour, Kaufland, Auchan");
            sb.AppendLine("- imporcie/eksporcie drobiu Polska/UE");
            sb.AppendLine("- regulacjach: KSeF, weterynaryjne, dobrostan");
            sb.AppendLine();
            sb.AppendLine("ARTYKULY:");

            for (int i = 0; i < articles.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {articles[i].Title}");
                if (!string.IsNullOrEmpty(articles[i].Snippet))
                {
                    sb.AppendLine($"   {articles[i].Snippet.Substring(0, Math.Min(200, articles[i].Snippet.Length))}...");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Odpowiedz TYLKO w JSON (bez markdown):");
            sb.AppendLine("[");
            sb.AppendLine("  {\"index\": 1, \"relevant\": true, \"reason\": \"krotki powod\"},");
            sb.AppendLine("  ...");
            sb.AppendLine("]");

            return sb.ToString();
        }

        private ArticleAnalysisResult ParseAnalysisResponse(string response, string originalTitle)
        {
            try
            {
                Debug.WriteLine($"[ClaudeAnalysis] Raw response length: {response?.Length ?? 0}");
                Debug.WriteLine($"[ClaudeAnalysis] Response start: {response?.Substring(0, Math.Min(100, response?.Length ?? 0))}");

                var json = ExtractJson(response);
                Debug.WriteLine($"[ClaudeAnalysis] Extracted JSON start: {json?.Substring(0, Math.Min(100, json?.Length ?? 0))}");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var result = new ArticleAnalysisResult
                {
                    Summary = GetStringProperty(root, "streszczenie", "Brak streszczenia"),
                    WhoIs = GetStringProperty(root, "kim_jest", ""),
                    AnalysisCeo = GetStringProperty(root, "analiza_ceo", ""),
                    AnalysisSales = GetStringProperty(root, "analiza_handlowiec", ""),
                    AnalysisBuyer = GetStringProperty(root, "analiza_zakupowiec", ""),
                    ActionsCeo = GetStringArrayProperty(root, "dzialania_ceo"),
                    ActionsSales = GetStringArrayProperty(root, "dzialania_handlowiec"),
                    ActionsBuyer = GetStringArrayProperty(root, "dzialania_zakupowiec"),
                    Category = GetStringProperty(root, "kategoria", "Info"),
                    Severity = GetStringProperty(root, "severity", "info"),
                    Tags = GetStringArrayProperty(root, "tagi")
                };

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeAnalysis] Blad parsowania JSON: {ex.Message}");
                Debug.WriteLine($"Response: {response}");
                return CreateStubAnalysis(originalTitle, $"Blad parsowania odpowiedzi AI: {ex.Message}");
            }
        }

        private List<QuickFilterResult> ParseFilterResponse(string response, List<(string Title, string Snippet)> articles)
        {
            var results = new List<QuickFilterResult>();

            try
            {
                var json = ExtractJson(response);
                using var doc = JsonDocument.Parse(json);

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var index = item.GetProperty("index").GetInt32() - 1;
                    if (index >= 0 && index < articles.Count)
                    {
                        results.Add(new QuickFilterResult
                        {
                            Title = articles[index].Title,
                            IsRelevant = item.GetProperty("relevant").GetBoolean(),
                            Reason = GetStringProperty(item, "reason", "")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeFilter] Blad parsowania: {ex.Message}");
                // W razie bledu przepusc wszystko
                foreach (var a in articles)
                {
                    results.Add(new QuickFilterResult { Title = a.Title, IsRelevant = true, Reason = "Parse error" });
                }
            }

            return results;
        }

        private DailySummaryResult ParseDailySummaryResponse(string response)
        {
            try
            {
                var json = ExtractJson(response);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new DailySummaryResult
                {
                    Summary = GetStringProperty(root, "summary", "Brak streszczenia"),
                    TopThreats = GetStringArrayProperty(root, "top_threats"),
                    TopOpportunities = GetStringArrayProperty(root, "top_opportunities"),
                    MarketMood = GetStringProperty(root, "market_mood", "neutral"),
                    UrgentActions = GetStringArrayProperty(root, "urgent_actions")
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClaudeSummary] Blad parsowania: {ex.Message}");
                return new DailySummaryResult
                {
                    Summary = response,
                    TopThreats = new List<string>(),
                    TopOpportunities = new List<string>(),
                    MarketMood = "neutral"
                };
            }
        }

        private string ExtractJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "{}";

            // Usun wszystkie bloki markdown ``` ... ```
            while (text.Contains("```"))
            {
                var start = text.IndexOf("```");
                var endMarker = text.IndexOf("```", start + 3);

                if (endMarker > start)
                {
                    // Wyciagnij zawartosc bloku (bez ```)
                    var blockContent = text.Substring(start + 3, endMarker - start - 3);

                    // Usun ewentualne "json" na poczatku
                    blockContent = blockContent.TrimStart();
                    if (blockContent.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                        blockContent = blockContent.Substring(4);

                    // Zamien caly blok na sama zawartosc
                    text = text.Substring(0, start) + blockContent + text.Substring(endMarker + 3);
                }
                else
                {
                    // Niepelny blok - usun samo ```
                    text = text.Remove(start, 3);
                }
            }

            // Usun pozostale pojedyncze backticki
            text = text.Replace("`", "");

            // Znajdz pierwszy { lub [
            var jsonStart = text.IndexOfAny(new[] { '{', '[' });
            if (jsonStart >= 0)
            {
                // Znajdz pasujacy koniec
                var openChar = text[jsonStart];
                var closeChar = openChar == '{' ? '}' : ']';
                var depth = 0;
                var inString = false;
                var escaped = false;

                for (int i = jsonStart; i < text.Length; i++)
                {
                    var c = text[i];

                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\' && inString)
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (!inString)
                    {
                        if (c == openChar) depth++;
                        if (c == closeChar) depth--;
                        if (depth == 0)
                        {
                            return text.Substring(jsonStart, i - jsonStart + 1);
                        }
                    }
                }
            }

            Debug.WriteLine($"[ExtractJson] Nie znaleziono JSON w: {text.Substring(0, Math.Min(200, text.Length))}...");
            return text.Trim();
        }

        private string GetStringProperty(JsonElement element, string propertyName, string defaultValue)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? defaultValue;
            }
            return defaultValue;
        }

        private List<string> GetStringArrayProperty(JsonElement element, string propertyName)
        {
            var result = new List<string>();
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        result.Add(item.GetString() ?? "");
                    }
                }
            }
            return result;
        }

        private ArticleAnalysisResult CreateStubAnalysis(string title, string message)
        {
            return new ArticleAnalysisResult
            {
                Summary = message,
                WhoIs = "Informacja niedostepna - sprawdz konfiguracje klucza API Claude.",
                AnalysisCeo = message,
                AnalysisSales = message,
                AnalysisBuyer = message,
                ActionsCeo = new List<string> { "Skonfiguruj klucz API Claude w App.config lub zmiennej srodowiskowej ANTHROPIC_API_KEY" },
                ActionsSales = new List<string>(),
                ActionsBuyer = new List<string>(),
                Category = "Info",
                Severity = "info",
                Tags = new List<string>()
            };
        }

        #endregion

        #region Cost Estimation

        public decimal EstimateCost(int inputTokens, int outputTokens, bool isSonnet)
        {
            if (isSonnet)
            {
                return (inputTokens / 1000000m) * SonnetInputCost + (outputTokens / 1000000m) * SonnetOutputCost;
            }
            else
            {
                return (inputTokens / 1000000m) * HaikuInputCost + (outputTokens / 1000000m) * HaikuOutputCost;
            }
        }

        #endregion
    }

    #region Result Classes

    public class ArticleAnalysisResult
    {
        public string Summary { get; set; }
        public string WhoIs { get; set; }
        public string AnalysisCeo { get; set; }
        public string AnalysisSales { get; set; }
        public string AnalysisBuyer { get; set; }
        public List<string> ActionsCeo { get; set; } = new List<string>();
        public List<string> ActionsSales { get; set; } = new List<string>();
        public List<string> ActionsBuyer { get; set; } = new List<string>();
        public string Category { get; set; }
        public string Severity { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        public string ActionsToString(List<string> actions) => string.Join("\n", actions);
    }

    public class QuickFilterResult
    {
        public string Title { get; set; }
        public bool IsRelevant { get; set; }
        public string Reason { get; set; }
    }

    public class DailySummaryResult
    {
        public string Summary { get; set; }
        public List<string> TopThreats { get; set; } = new List<string>();
        public List<string> TopOpportunities { get; set; } = new List<string>();
        public string MarketMood { get; set; }
        public List<string> UrgentActions { get; set; } = new List<string>();
    }

    #endregion
}
