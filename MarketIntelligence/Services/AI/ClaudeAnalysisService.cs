using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis do analizy artykułów przez Claude AI
    /// Używa Haiku do filtrowania i Sonnet do pełnej analizy
    /// </summary>
    public class ClaudeAnalysisService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly SemaphoreSlim _rateLimiter;

        // Claude API endpoints and models
        private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
        private const string HaikuModel = "claude-3-haiku-20240307";
        private const string SonnetModel = "claude-3-5-sonnet-20241022";
        private const string ApiVersion = "2023-06-01";

        // Rate limiting (Anthropic limits)
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _minRequestInterval = TimeSpan.FromMilliseconds(200);

        public ClaudeAnalysisService(string apiKey = null)
        {
            _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? System.Configuration.ConfigurationManager.AppSettings["ClaudeApiKey"];

            // Debug logging - help diagnose API key issues
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.WriteLine("[Claude] WARNING: No API key found! Checked:");
                Debug.WriteLine("[Claude]   - Constructor parameter: " + (apiKey == null ? "null" : "provided"));
                Debug.WriteLine($"[Claude]   - Environment ANTHROPIC_API_KEY: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")) ? "not set" : "set")}");
                Debug.WriteLine($"[Claude]   - App.config ClaudeApiKey: {(string.IsNullOrEmpty(System.Configuration.ConfigurationManager.AppSettings["ClaudeApiKey"]) ? "not set" : "set")}");
            }
            else
            {
                Debug.WriteLine($"[Claude] API key configured: {_apiKey.Substring(0, Math.Min(15, _apiKey.Length))}...");
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey ?? "");
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);

            _rateLimiter = new SemaphoreSlim(1);
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        #region Quick Filtering (Haiku)

        /// <summary>
        /// Szybkie filtrowanie artykułów z Haiku (tanie)
        /// Zwraca tylko relevantne artykuły z podstawową kategoryzacją
        /// </summary>
        public async Task<List<FilteredArticle>> QuickFilterArticlesAsync(
            List<RawArticle> articles,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                Debug.WriteLine("[Claude] API key not configured, returning all articles as unfiltered");
                return articles.Select(a => new FilteredArticle
                {
                    Article = a,
                    IsRelevant = a.IsRelevant,
                    Category = DetermineLocalCategory(a),
                    Priority = a.RelevanceScore / 10
                }).ToList();
            }

            var filtered = new List<FilteredArticle>();
            var batch = new List<RawArticle>();
            const int batchSize = 10;

            foreach (var article in articles)
            {
                batch.Add(article);

                if (batch.Count >= batchSize)
                {
                    var results = await FilterBatchAsync(batch, ct);
                    filtered.AddRange(results);
                    batch.Clear();
                }
            }

            // Process remaining
            if (batch.Any())
            {
                var results = await FilterBatchAsync(batch, ct);
                filtered.AddRange(results);
            }

            return filtered.Where(f => f.IsRelevant).OrderByDescending(f => f.Priority).ToList();
        }

        private async Task<List<FilteredArticle>> FilterBatchAsync(List<RawArticle> batch, CancellationToken ct)
        {
            var results = new List<FilteredArticle>();

            var articlesText = string.Join("\n\n", batch.Select((a, i) =>
                $"[{i + 1}] {a.Title}\n{(a.Summary?.Length > 300 ? a.Summary.Substring(0, 300) + "..." : a.Summary)}"));

            var prompt = $@"Jesteś asystentem analityka rynku drobiarskiego dla polskiej ubojni drobiu.
Oceń poniższe artykuły pod kątem relevantności dla branży drobiarskiej w Polsce.

ARTYKUŁY:
{articlesText}

Dla każdego artykułu odpowiedz w formacie JSON (tablica):
[
  {{
    ""id"": 1,
    ""relevant"": true/false,
    ""category"": ""HPAI|Ceny|Konkurencja|Eksport|Import|Regulacje|Pasze|Pogoda|Klienci|Info"",
    ""priority"": 1-10,
    ""reason"": ""krótkie uzasadnienie""
  }},
  ...
]

Relevant = true jeśli artykuł dotyczy:
- Drobiu, kurczaków, indyków, ubojni
- HPAI / grypy ptaków
- Cen skupu/hurtowych mięsa
- Eksportu/importu drobiu
- Pasz, zbóż (kukurydza, soja, pszenica)
- Konkurencji (Cedrob, SuperDrob, Animex, etc.)
- Regulacji weterynaryjnych/rolnych
- Sieci handlowych (Biedronka, Lidl) - jeśli o cenach mięsa

Relevant = false jeśli:
- Przepisy kulinarne
- Restauracje (chyba że o cenach hurtowych)
- Niezwiązane z branżą spożywczą/rolną

Odpowiedz TYLKO tablicą JSON, bez dodatkowego tekstu.";

            try
            {
                var response = await CallClaudeAsync(prompt, HaikuModel, 1000, ct);

                if (!string.IsNullOrEmpty(response))
                {
                    // Parse JSON response
                    var jsonStart = response.IndexOf('[');
                    var jsonEnd = response.LastIndexOf(']') + 1;

                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                        var filterResults = JsonSerializer.Deserialize<List<FilterResult>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (filterResults != null)
                        {
                            foreach (var result in filterResults)
                            {
                                var idx = result.Id - 1;
                                if (idx >= 0 && idx < batch.Count)
                                {
                                    results.Add(new FilteredArticle
                                    {
                                        Article = batch[idx],
                                        IsRelevant = result.Relevant,
                                        Category = result.Category,
                                        Priority = result.Priority,
                                        FilterReason = result.Reason
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claude] Filter batch error: {ex.Message}");
            }

            // Add any missing articles with local filtering
            foreach (var article in batch)
            {
                if (!results.Any(r => r.Article.UrlHash == article.UrlHash))
                {
                    results.Add(new FilteredArticle
                    {
                        Article = article,
                        IsRelevant = article.IsRelevant,
                        Category = DetermineLocalCategory(article),
                        Priority = article.RelevanceScore / 10
                    });
                }
            }

            return results;
        }

        #endregion

        #region Full Analysis (Sonnet)

        /// <summary>
        /// Pełna analiza artykułu z Claude Sonnet
        /// Generuje 3 perspektywy (CEO/Handlowiec/Zakupowiec) + edukację
        /// </summary>
        public async Task<ArticleAnalysis> AnalyzeArticleAsync(
            RawArticle article,
            BusinessContext context,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                Debug.WriteLine("[Claude] API key not configured, returning stub analysis");
                return CreateStubAnalysis(article);
            }

            var prompt = CreateAnalysisPrompt(article, context);

            try
            {
                var response = await CallClaudeAsync(prompt, SonnetModel, 2000, ct);

                if (!string.IsNullOrEmpty(response))
                {
                    return ParseAnalysisResponse(response, article);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claude] Analysis error: {ex.Message}");
            }

            return CreateStubAnalysis(article);
        }

        /// <summary>
        /// Analiza wielu artykułów (batch)
        /// </summary>
        public async Task<List<ArticleAnalysis>> AnalyzeArticlesAsync(
            List<RawArticle> articles,
            BusinessContext context,
            int maxArticles = 20,
            CancellationToken ct = default)
        {
            var analyses = new List<ArticleAnalysis>();
            var toAnalyze = articles.Take(maxArticles).ToList();

            foreach (var article in toAnalyze)
            {
                ct.ThrowIfCancellationRequested();

                var analysis = await AnalyzeArticleAsync(article, context, ct);
                analyses.Add(analysis);

                // Progress logging
                Debug.WriteLine($"[Claude] Analyzed {analyses.Count}/{toAnalyze.Count}: {article.Title.Substring(0, Math.Min(50, article.Title.Length))}...");
            }

            return analyses;
        }

        private string CreateAnalysisPrompt(RawArticle article, BusinessContext context)
        {
            // 1. Pobierz pełną treść (FullContent ma priorytet nad Summary)
            var contentToAnalyze = !string.IsNullOrEmpty(article.FullContent) && article.FullContent.Length > 500
                ? article.FullContent
                : article.Summary ?? article.Title;

            // Ogranicz długość do 6000 znaków (limit dla Claude)
            if (contentToAnalyze.Length > 6000)
            {
                // Obetnij na granicy zdania
                var truncated = contentToAnalyze.Substring(0, 6000);
                var lastSentence = truncated.LastIndexOfAny(new[] { '.', '!', '?' });
                if (lastSentence > 4000)
                    contentToAnalyze = truncated.Substring(0, lastSentence + 1) + "\n[...]";
                else
                    contentToAnalyze = truncated + "...";
            }

            // 2. Wykryj podmioty i wygeneruj sekcję "KIM JEST"
            var fullText = $"{article.Title} {contentToAnalyze}";
            var detectedEntities = EntityKnowledgeBase.FindEntitiesInText(fullText);
            var whoIsSection = detectedEntities.Any()
                ? EntityKnowledgeBase.GenerateWhoIsSection(detectedEntities)
                : "Brak rozpoznanych podmiotów wymagających wyjaśnienia.";

            // 3. Przygotuj skrócony kontekst firmy
            var companyContext = $@"
FIRMA: Ubojnia Drobiu Piórkowscy, Brzeziny (łódzkie)
ZDOLNOŚĆ: 70 000 kurczaków/dzień (~200 ton)
SYTUACJA KRYZYSOWA: Sprzedaż spadła z 25M do 15M PLN/mies, straty ~2M PLN/mies
CENY WŁASNE: żywiec {context?.Company?.LiveChickenPrice ?? 4.72m} zł/kg, filet {context?.Company?.FiletWholesalePrice ?? 24.50m} zł/kg

KONKURENCI GŁÓWNI:
- Cedrob (ADQ negocjuje przejęcie za 8 mld) - KRYTYCZNE ZAGROŻENIE
- SuperDrob/LipCo (CPF Tajlandia) - WYSOKIE ZAGROŻENIE
- Drosed (LDC/ADQ) - część potencjalnego monopolu

GŁÓWNI KLIENCI: {string.Join(", ", context?.TopCustomers?.Take(5).Select(c => c.Name) ?? new[] { "Biedronka", "Makro", "Dino" })}

BIEŻĄCE ZAGROŻENIA:
- HPAI: 19 ognisk w Polsce, 2 w łódzkim (NASZ REGION!)
- Import: Brazylia filet 13 zł/kg vs nasze 15-17 zł
- Mercosur: 180k ton bezcłowego drobiu do UE
- KSeF: obowiązkowy od 01.04.2026";

            // 4. Zbuduj pełny prompt
            return $@"Jesteś STARSZYM ANALITYKIEM RYNKU DROBIARSKIEGO dla Ubojni Drobiu Piórkowscy.
Przygotuj SZCZEGÓŁOWĄ i KONKRETNĄ analizę artykułu.

═══════════════════════════════════════════════════════════════════
                           ARTYKUŁ
═══════════════════════════════════════════════════════════════════
TYTUŁ: {article.Title}
ŹRÓDŁO: {article.SourceName}
DATA: {article.PublishDate:yyyy-MM-dd}
KATEGORIA: {article.SourceCategory}

PEŁNA TREŚĆ:
{contentToAnalyze}

═══════════════════════════════════════════════════════════════════
                    KONTEKST NASZEJ FIRMY
═══════════════════════════════════════════════════════════════════
{companyContext}

═══════════════════════════════════════════════════════════════════
             KIM SĄ PODMIOTY WYMIENIONE W ARTYKULE
═══════════════════════════════════════════════════════════════════
{whoIsSection}

═══════════════════════════════════════════════════════════════════
                          ZADANIE
═══════════════════════════════════════════════════════════════════
Przygotuj SZCZEGÓŁOWĄ analizę w formacie JSON:

{{
  ""kategoria"": ""HPAI|Ceny|Konkurencja|Eksport|Import|Regulacje|Pasze|Pogoda|Klienci|Info"",
  ""severity"": ""critical|warning|positive|info"",
  ""istotnosc"": 1-10,

  ""streszczenie"": ""SZCZEGÓŁOWE streszczenie artykułu (3-5 zdań): CO się wydarzyło? Podaj KONKRETNE fakty, liczby, daty, nazwy firm/osób z artykułu. Nie pisz ogólników."",

  ""kim_jest"": ""Wyjaśnij WSZYSTKIE podmioty (firmy, osoby, organizacje) wymienione w artykule:
- Kim są? Czym się zajmują? Jaka jest ich skala działalności?
- Dlaczego są ważni dla branży drobiarskiej?
- Jakie mają powiązania kapitałowe (właściciele, inwestorzy)?
- Użyj informacji z sekcji KIM SĄ PODMIOTY powyżej.
Napisz minimum 2-4 zdania na KAŻDY istotny podmiot."",

  ""co_to_znaczy_dla_piorkowscy"": ""KONKRETNA analiza wpływu na NASZĄ firmę (3-5 zdań):
- Czy to zagrożenie czy szansa dla Piórkowscy?
- Jak to wpływa na naszą konkurencyjność w regionie łódzkim?
- Jaki wpływ na nasze ceny, klientów, dostawców (hodowców)?
- ODNIEŚ SIĘ DO NASZEJ SYTUACJI KRYZYSOWEJ (spadek sprzedaży o 40%, straty 2M/mies)
- Co KONKRETNIE powinniśmy zrobić w reakcji?"",

  ""analiza_ceo"": ""Dla WŁAŚCICIELA firmy (2-3 zdania): strategiczne implikacje, decyzje do podjęcia, ryzyka."",
  ""analiza_handlowiec"": ""Dla DZIAŁU SPRZEDAŻY (2-3 zdania): wpływ na ceny, argumenty dla klientów, zagrożenia/szanse."",
  ""analiza_zakupowiec"": ""Dla DZIAŁU ZAKUPÓW (2-3 zdania): wpływ na hodowców, ceny skupu żywca, pasze."",

  ""rekomendacje_ceo"": [""KONKRETNA akcja 1 z terminem"", ""KONKRETNA akcja 2 z terminem""],
  ""rekomendacje_handlowiec"": [""KONKRETNA akcja 1"", ""KONKRETNA akcja 2""],
  ""rekomendacje_zakupowiec"": [""KONKRETNA akcja 1"", ""KONKRETNA akcja 2""],

  ""zalecane_dzialania"": [
    {{
      ""priorytet"": ""PILNE|WAŻNE|DO_ROZWAŻENIA"",
      ""dzialanie"": ""KONKRETNY krok do podjęcia - co DOKŁADNIE zrobić?"",
      ""odpowiedzialny"": ""CEO|Sprzedaż|Zakupy|Produkcja"",
      ""termin"": ""natychmiast|ten tydzień|ten miesiąc""
    }}
  ],

  ""kluczowe_liczby"": [
    {{""nazwa"": ""np. cena fileta"", ""wartosc"": ""wartość z artykułu"", ""kontekst"": ""co to oznacza dla nas""}}
  ],

  ""zrodla_do_monitorowania"": [""źródło do dalszego śledzenia tematu""]
}}

═══════════════════════════════════════════════════════════════════
                    ZASADY BEZWZGLĘDNE
═══════════════════════════════════════════════════════════════════
1. STRESZCZENIE musi zawierać FAKTY z artykułu - liczby, daty, nazwy. NIE PISZ OGÓLNIKÓW.
2. KIM_JEST musi wyjaśnić KAŻDY podmiot z artykułu - nie pisz ""nieznany"" ani ""brak informacji"".
3. CO_TO_ZNACZY musi odnieść się do NASZEJ SYTUACJI KRYZYSOWEJ i być KONKRETNE.
4. ZALECANE_DZIALANIA muszą być WYKONALNE i KONKRETNE (nie ""monitoruj sytuację"" ani ""bądź na bieżąco"").
5. Pisz po polsku, profesjonalnie, rzeczowo.

Odpowiedz WYŁĄCZNIE poprawnym JSON-em, bez żadnego tekstu przed ani po.";
        }

        private ArticleAnalysis ParseAnalysisResponse(string response, RawArticle article)
        {
            try
            {
                // Find JSON in response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}') + 1;

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                    var parsed = JsonSerializer.Deserialize<AnalysisResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed != null)
                    {
                        return new ArticleAnalysis
                        {
                            Article = article,
                            Category = parsed.Kategoria ?? "Info",
                            Severity = parsed.Severity ?? "info",
                            Importance = parsed.Istotnosc,

                            // NOWE POLA - priorytet dla rozszerzonej analizy
                            Summary = parsed.Streszczenie,
                            WhoIs = parsed.KimJest,
                            WhatItMeansForPiorkowscy = parsed.CoToZnaczyDlaPiorkowscy,
                            StructuredActions = parsed.ZalecaneDzialania ?? new List<ZalecaneDzialaniaItem>(),
                            SourcesToMonitor = parsed.ZrodlaDoMonitorowania ?? new List<string>(),

                            // Mapowanie do starych pól (kompatybilność wsteczna z UI)
                            CeoAnalysis = parsed.CoToZnaczyDlaPiorkowscy ?? parsed.AnalizaCeo ?? "Brak analizy",
                            SalesAnalysis = parsed.AnalizaHandlowiec ?? parsed.CoToZnaczyDlaPiorkowscy ?? "Brak analizy",
                            BuyerAnalysis = parsed.AnalizaZakupowiec ?? parsed.CoToZnaczyDlaPiorkowscy ?? "Brak analizy",

                            // Rekomendacje - użyj strukturalnych jeśli dostępne
                            CeoRecommendations = parsed.ZalecaneDzialania?.Any() == true
                                ? parsed.ZalecaneDzialania.Select(z => $"[{z.Priorytet}] {z.Dzialanie} ({z.Odpowiedzialny}, {z.Termin})").ToList()
                                : parsed.RekomendacjeCeo ?? new List<string>(),
                            SalesRecommendations = parsed.RekomendacjeHandlowiec ?? new List<string>(),
                            BuyerRecommendations = parsed.RekomendacjeZakupowiec ?? new List<string>(),

                            // Edukacja - użyj KimJest jeśli dostępne
                            EducationalContent = parsed.KimJest ?? parsed.Edukacja ?? "Brak informacji edukacyjnej",
                            KeyNumbers = parsed.KluczoweLiczby ?? new List<KeyNumber>(),
                            RelatedTopics = parsed.PowiazaneTematy ?? new List<string>(),

                            AnalyzedAt = DateTime.Now,
                            Model = SonnetModel
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claude] Parse analysis error: {ex.Message}");
            }

            return CreateStubAnalysis(article);
        }

        #endregion

        #region Daily Summary

        /// <summary>
        /// Generuj poranne streszczenie dnia
        /// </summary>
        public async Task<DailySummary> GenerateDailySummaryAsync(
            List<ArticleAnalysis> analyses,
            BusinessContext context,
            CancellationToken ct = default)
        {
            if (!IsConfigured || !analyses.Any())
            {
                return CreateStubSummary(analyses);
            }

            var articlesOverview = string.Join("\n", analyses.Take(20).Select((a, i) =>
                $"{i + 1}. [{a.Category}] {a.Article.Title} (ważność: {a.Importance}/10)"));

            var prompt = $@"Jesteś analitykiem przygotowującym poranny briefing dla Ubojni Drobiu Piórkowscy.

=== DZISIEJSZE ARTYKUŁY ===
{articlesOverview}

=== KONTEKST FIRMY ===
Lokalizacja: Brzeziny, łódzkie
Specjalizacja: ubój kurczaków brojlerów
Zdolność: ~70,000 szt/dzień

=== ZADANIE ===
Przygotuj poranne streszczenie w formacie JSON:

{{
  ""headline"": ""Główny nagłówek dnia (max 100 znaków)"",
  ""summary_ceo"": ""3-4 zdania: kluczowe informacje dla CEO. Co najważniejsze? Jakie ryzyka/szanse?"",
  ""summary_sales"": ""3-4 zdania: podsumowanie dla działu sprzedaży"",
  ""summary_buyer"": ""3-4 zdania: podsumowanie dla działu zakupów"",

  ""top_alerts"": [
    {{""category"": ""HPAI"", ""severity"": ""critical"", ""message"": ""krótki opis alertu""}},
    ...
  ],

  ""market_mood"": ""positive|neutral|negative"",
  ""market_mood_reason"": ""Krótkie uzasadnienie nastroju rynku"",

  ""action_items"": [
    {{""priority"": ""high|medium|low"", ""action"": ""co zrobić"", ""owner"": ""CEO|Sprzedaż|Zakupy""}},
    ...
  ],

  ""weekly_outlook"": ""Krótka prognoza na najbliższe dni""
}}

Odpowiedz TYLKO JSON-em.";

            try
            {
                var response = await CallClaudeAsync(prompt, SonnetModel, 1500, ct);

                if (!string.IsNullOrEmpty(response))
                {
                    var jsonStart = response.IndexOf('{');
                    var jsonEnd = response.LastIndexOf('}') + 1;

                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                        var parsed = JsonSerializer.Deserialize<SummaryResponse>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (parsed != null)
                        {
                            return new DailySummary
                            {
                                Date = DateTime.Today,
                                Headline = parsed.Headline,
                                CeoSummary = parsed.SummaryCeo,
                                SalesSummary = parsed.SummarySales,
                                BuyerSummary = parsed.SummaryBuyer,
                                TopAlerts = parsed.TopAlerts ?? new List<Alert>(),
                                MarketMood = parsed.MarketMood,
                                MarketMoodReason = parsed.MarketMoodReason,
                                ActionItems = parsed.ActionItems ?? new List<ActionItem>(),
                                WeeklyOutlook = parsed.WeeklyOutlook,
                                ArticlesAnalyzed = analyses.Count,
                                GeneratedAt = DateTime.Now
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claude] Summary error: {ex.Message}");
            }

            return CreateStubSummary(analyses);
        }

        #endregion

        #region Translation

        /// <summary>
        /// Przetłumacz tekst z angielskiego na polski używając Claude Haiku
        /// </summary>
        public async Task<TranslatedArticle> TranslateArticleAsync(
            RawArticle article,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                Debug.WriteLine("[Claude] API key not configured, skipping translation");
                return new TranslatedArticle
                {
                    Original = article,
                    TranslatedTitle = article.Title,
                    TranslatedSummary = article.Summary,
                    WasTranslated = false
                };
            }

            if (article.Language?.ToLower() != "en")
            {
                return new TranslatedArticle
                {
                    Original = article,
                    TranslatedTitle = article.Title,
                    TranslatedSummary = article.Summary,
                    WasTranslated = false
                };
            }

            Debug.WriteLine($"[Claude] Translating: {article.Title.Substring(0, Math.Min(50, article.Title.Length))}...");

            var prompt = $@"Przetłumacz poniższy tekst z angielskiego na polski.
Zachowaj:
- Nazwy własne firm (np. Tyson Foods, JBS)
- Terminy branżowe (HPAI, H5N1)
- Liczby i jednostki

Tekst do tłumaczenia:

TYTUŁ: {article.Title}

TREŚĆ: {article.Summary ?? ""}

Odpowiedz w formacie JSON:
{{
  ""tytul"": ""przetłumaczony tytuł"",
  ""tresc"": ""przetłumaczona treść""
}}";

            try
            {
                var response = await CallClaudeAsync(prompt, HaikuModel, 1500, ct);

                if (!string.IsNullOrEmpty(response))
                {
                    var jsonStart = response.IndexOf('{');
                    var jsonEnd = response.LastIndexOf('}') + 1;

                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                        var parsed = JsonSerializer.Deserialize<TranslationResponse>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (parsed != null)
                        {
                            return new TranslatedArticle
                            {
                                Original = article,
                                TranslatedTitle = parsed.Tytul ?? article.Title,
                                TranslatedSummary = parsed.Tresc ?? article.Summary,
                                WasTranslated = true
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claude] Translation error: {ex.Message}");
            }

            return new TranslatedArticle
            {
                Original = article,
                TranslatedTitle = article.Title,
                TranslatedSummary = article.Summary,
                WasTranslated = false
            };
        }

        /// <summary>
        /// Przetłumacz batch artykułów
        /// </summary>
        public async Task<List<RawArticle>> TranslateEnglishArticlesAsync(
            List<RawArticle> articles,
            CancellationToken ct = default)
        {
            var result = new List<RawArticle>();

            foreach (var article in articles)
            {
                ct.ThrowIfCancellationRequested();

                if (article.Language?.ToLower() == "en")
                {
                    var translated = await TranslateArticleAsync(article, ct);

                    // Update article with translated content
                    article.Title = translated.TranslatedTitle;
                    article.Summary = translated.TranslatedSummary;
                    article.Language = translated.WasTranslated ? "pl-translated" : article.Language;
                }

                result.Add(article);
            }

            return result;
        }

        #endregion

        #region API Call

        private async Task<string> CallClaudeAsync(string prompt, string model, int maxTokens, CancellationToken ct)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Claude API key not configured");
            }

            await _rateLimiter.WaitAsync(ct);
            try
            {
                // Rate limiting
                var elapsed = DateTime.Now - _lastRequestTime;
                if (elapsed < _minRequestInterval)
                {
                    await Task.Delay(_minRequestInterval - elapsed, ct);
                }

                var request = new
                {
                    model = model,
                    max_tokens = maxTokens,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ClaudeApiUrl, content, ct);
                _lastRequestTime = DateTime.Now;

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    Debug.WriteLine($"[Claude] API error {(int)response.StatusCode}: {error}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var responseObj = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return responseObj?.Content?.FirstOrDefault()?.Text;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        #endregion

        #region Helpers

        private string DetermineLocalCategory(RawArticle article)
        {
            var text = $"{article.Title} {article.Summary}".ToLowerInvariant();

            if (text.Contains("hpai") || text.Contains("grypa") || text.Contains("ognisko"))
                return "HPAI";
            if (text.Contains("cena") || text.Contains("notowania") || text.Contains("price"))
                return "Ceny";
            if (text.Contains("eksport") || text.Contains("export"))
                return "Eksport";
            if (text.Contains("import"))
                return "Import";
            if (text.Contains("cedrob") || text.Contains("superdrob") || text.Contains("animex"))
                return "Konkurencja";
            if (text.Contains("pasza") || text.Contains("kukurydz") || text.Contains("soja"))
                return "Pasze";
            if (text.Contains("regulac") || text.Contains("ustawa") || text.Contains("prawo"))
                return "Regulacje";

            return article.SourceCategory ?? "Info";
        }

        private ArticleAnalysis CreateStubAnalysis(RawArticle article)
        {
            return new ArticleAnalysis
            {
                Article = article,
                Category = DetermineLocalCategory(article),
                Severity = article.RelevanceScore >= 15 ? "warning" : "info",
                Importance = Math.Min(10, article.RelevanceScore / 3),

                // NOWE POLA - stub values
                Summary = article.Summary ?? article.Title,
                WhoIs = "Analiza AI niedostępna - sprawdź konfigurację klucza API Claude.",
                WhatItMeansForPiorkowscy = $"Artykuł wymaga ręcznego przeglądu. Źródło: {article.SourceName}.",
                StructuredActions = new List<ZalecaneDzialaniaItem>
                {
                    new ZalecaneDzialaniaItem
                    {
                        Priorytet = "DO_ROZWAŻENIA",
                        Dzialanie = "Przeczytaj artykuł źródłowy i oceń wpływ na firmę",
                        Odpowiedzialny = "CEO",
                        Termin = "ten tydzień"
                    }
                },
                SourcesToMonitor = new List<string>(),

                // Stare pola (kompatybilność wsteczna)
                CeoAnalysis = $"Artykuł wymaga przeglądu. Źródło: {article.SourceName}.",
                SalesAnalysis = "Brak automatycznej analizy - skonfiguruj klucz API Claude.",
                BuyerAnalysis = "Brak automatycznej analizy - skonfiguruj klucz API Claude.",

                CeoRecommendations = new List<string> { "Przeczytaj artykuł źródłowy" },
                SalesRecommendations = new List<string>(),
                BuyerRecommendations = new List<string>(),

                EducationalContent = "Analiza AI niedostępna - sprawdź konfigurację klucza API.",
                KeyNumbers = new List<KeyNumber>(),
                RelatedTopics = article.MatchedKeywords?.ToList() ?? new List<string>(),

                AnalyzedAt = DateTime.Now,
                Model = "local-fallback"
            };
        }

        private DailySummary CreateStubSummary(List<ArticleAnalysis> analyses)
        {
            var criticalCount = analyses.Count(a => a.Severity == "critical");
            var warningCount = analyses.Count(a => a.Severity == "warning");

            return new DailySummary
            {
                Date = DateTime.Today,
                Headline = $"Poranny briefing: {analyses.Count} artykułów do przeglądu",
                CeoSummary = $"Zebrano {analyses.Count} artykułów. {criticalCount} krytycznych, {warningCount} ostrzeżeń. Skonfiguruj klucz API Claude dla pełnej analizy.",
                SalesSummary = "Analiza AI niedostępna.",
                BuyerSummary = "Analiza AI niedostępna.",
                TopAlerts = criticalCount > 0
                    ? new List<Alert> { new Alert { Category = "System", Severity = "warning", Message = $"{criticalCount} artykułów wymaga uwagi" } }
                    : new List<Alert>(),
                MarketMood = "neutral",
                MarketMoodReason = "Brak analizy AI",
                ActionItems = new List<ActionItem>
                {
                    new ActionItem { Priority = "high", Action = "Skonfiguruj klucz API Claude", Owner = "IT" }
                },
                WeeklyOutlook = "Brak prognozy bez AI",
                ArticlesAnalyzed = analyses.Count,
                GeneratedAt = DateTime.Now
            };
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }

    #region Response Models

    internal class ClaudeResponse
    {
        public string Id { get; set; }
        public string Model { get; set; }
        public List<ContentBlock> Content { get; set; }
    }

    internal class ContentBlock
    {
        public string Type { get; set; }
        public string Text { get; set; }
    }

    internal class FilterResult
    {
        public int Id { get; set; }
        public bool Relevant { get; set; }
        public string Category { get; set; }
        public int Priority { get; set; }
        public string Reason { get; set; }
    }

    internal class AnalysisResponse
    {
        public string Kategoria { get; set; }
        public string Severity { get; set; }
        public int Istotnosc { get; set; }

        [JsonPropertyName("analiza_ceo")]
        public string AnalizaCeo { get; set; }

        [JsonPropertyName("analiza_handlowiec")]
        public string AnalizaHandlowiec { get; set; }

        [JsonPropertyName("analiza_zakupowiec")]
        public string AnalizaZakupowiec { get; set; }

        [JsonPropertyName("rekomendacje_ceo")]
        public List<string> RekomendacjeCeo { get; set; }

        [JsonPropertyName("rekomendacje_handlowiec")]
        public List<string> RekomendacjeHandlowiec { get; set; }

        [JsonPropertyName("rekomendacje_zakupowiec")]
        public List<string> RekomendacjeZakupowiec { get; set; }

        public string Edukacja { get; set; }

        // NOWE POLA dla rozszerzonej analizy AI
        [JsonPropertyName("streszczenie")]
        public string Streszczenie { get; set; }

        [JsonPropertyName("kim_jest")]
        public string KimJest { get; set; }

        [JsonPropertyName("co_to_znaczy_dla_piorkowscy")]
        public string CoToZnaczyDlaPiorkowscy { get; set; }

        [JsonPropertyName("zalecane_dzialania")]
        public List<ZalecaneDzialaniaItem> ZalecaneDzialania { get; set; }

        [JsonPropertyName("zrodla_do_monitorowania")]
        public List<string> ZrodlaDoMonitorowania { get; set; }

        [JsonPropertyName("kluczowe_liczby")]
        public List<KeyNumber> KluczoweLiczby { get; set; }

        [JsonPropertyName("powiazane_tematy")]
        public List<string> PowiazaneTematy { get; set; }
    }

    internal class SummaryResponse
    {
        public string Headline { get; set; }

        [JsonPropertyName("summary_ceo")]
        public string SummaryCeo { get; set; }

        [JsonPropertyName("summary_sales")]
        public string SummarySales { get; set; }

        [JsonPropertyName("summary_buyer")]
        public string SummaryBuyer { get; set; }

        [JsonPropertyName("top_alerts")]
        public List<Alert> TopAlerts { get; set; }

        [JsonPropertyName("market_mood")]
        public string MarketMood { get; set; }

        [JsonPropertyName("market_mood_reason")]
        public string MarketMoodReason { get; set; }

        [JsonPropertyName("action_items")]
        public List<ActionItem> ActionItems { get; set; }

        [JsonPropertyName("weekly_outlook")]
        public string WeeklyOutlook { get; set; }
    }

    internal class TranslationResponse
    {
        public string Tytul { get; set; }
        public string Tresc { get; set; }
    }

    #endregion

    #region Output Models

    public class FilteredArticle
    {
        public RawArticle Article { get; set; }
        public bool IsRelevant { get; set; }
        public string Category { get; set; }
        public int Priority { get; set; }
        public string FilterReason { get; set; }
    }

    public class TranslatedArticle
    {
        public RawArticle Original { get; set; }
        public string TranslatedTitle { get; set; }
        public string TranslatedSummary { get; set; }
        public bool WasTranslated { get; set; }
    }

    public class ArticleAnalysis
    {
        public RawArticle Article { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public int Importance { get; set; }

        // NOWE POLA dla rozszerzonej analizy AI
        /// <summary>Szczegółowe streszczenie z faktami</summary>
        public string Summary { get; set; }

        /// <summary>Kim są podmioty wymienione w artykule</summary>
        public string WhoIs { get; set; }

        /// <summary>Co to znaczy dla Ubojni Piórkowscy</summary>
        public string WhatItMeansForPiorkowscy { get; set; }

        /// <summary>Strukturalne zalecane działania z priorytetami</summary>
        public List<ZalecaneDzialaniaItem> StructuredActions { get; set; } = new();

        /// <summary>Źródła do monitorowania</summary>
        public List<string> SourcesToMonitor { get; set; } = new();

        // Stare pola (zachowane dla kompatybilności wstecznej)
        public string CeoAnalysis { get; set; }
        public string SalesAnalysis { get; set; }
        public string BuyerAnalysis { get; set; }

        public List<string> CeoRecommendations { get; set; } = new();
        public List<string> SalesRecommendations { get; set; } = new();
        public List<string> BuyerRecommendations { get; set; } = new();

        public string EducationalContent { get; set; }
        public List<KeyNumber> KeyNumbers { get; set; } = new();
        public List<string> RelatedTopics { get; set; } = new();

        public DateTime AnalyzedAt { get; set; }
        public string Model { get; set; }
    }

    public class KeyNumber
    {
        [JsonPropertyName("nazwa")]
        public string Name { get; set; }

        [JsonPropertyName("wartosc")]
        public string Value { get; set; }

        [JsonPropertyName("zmiana")]
        public string Change { get; set; }

        [JsonPropertyName("kontekst")]
        public string Kontekst { get; set; }
    }

    /// <summary>
    /// Struktura dla zalecanych działań z priorytetami i terminami
    /// </summary>
    public class ZalecaneDzialaniaItem
    {
        [JsonPropertyName("priorytet")]
        public string Priorytet { get; set; }

        [JsonPropertyName("dzialanie")]
        public string Dzialanie { get; set; }

        [JsonPropertyName("odpowiedzialny")]
        public string Odpowiedzialny { get; set; }

        [JsonPropertyName("termin")]
        public string Termin { get; set; }
    }

    public class DailySummary
    {
        public DateTime Date { get; set; }
        public string Headline { get; set; }
        public string CeoSummary { get; set; }
        public string SalesSummary { get; set; }
        public string BuyerSummary { get; set; }
        public List<Alert> TopAlerts { get; set; } = new();
        public string MarketMood { get; set; }
        public string MarketMoodReason { get; set; }
        public List<ActionItem> ActionItems { get; set; } = new();
        public string WeeklyOutlook { get; set; }
        public int ArticlesAnalyzed { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class Alert
    {
        public string Category { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
    }

    public class ActionItem
    {
        public string Priority { get; set; }
        public string Action { get; set; }
        public string Owner { get; set; }
    }

    /// <summary>
    /// Kontekst biznesowy firmy do analizy AI
    /// </summary>
    public class BusinessContext
    {
        public CompanyInfo Company { get; set; }
        public List<SupplierInfo> TopSuppliers { get; set; } = new();
        public List<CustomerInfo> TopCustomers { get; set; } = new();
        public List<PriceInfo> CurrentPrices { get; set; } = new();
        public List<string> Competitors { get; set; } = new();
        public List<CompetitorInfo> CompetitorsDetailed { get; set; } = new();
        public ThreatsAndOpportunities Alerts { get; set; }
    }

    /// <summary>
    /// Informacje o firmie - ROZSZERZONE o sytuację kryzysową
    /// </summary>
    public class CompanyInfo
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public string Voivodeship { get; set; }
        public int DailyCapacity { get; set; }
        public int DailyTonnage { get; set; }
        public string Specialization { get; set; }

        // SYTUACJA KRYZYSOWA
        public string CurrentSituation { get; set; }
        public decimal MonthlySalesTarget { get; set; }
        public decimal CurrentMonthlySales { get; set; }
        public decimal MonthlyLoss { get; set; }

        // Zespół
        public List<string> SalesReps { get; set; } = new();

        // Hodowcy
        public int TotalFarmers { get; set; }
        public List<string> FarmerRegions { get; set; } = new();

        // Aktualne ceny
        public decimal LiveChickenPrice { get; set; }
        public decimal CarcassWholesalePrice { get; set; }
        public decimal FiletWholesalePrice { get; set; }
        public decimal DrumstickPrice { get; set; }
        public decimal LiveToFeedRatio { get; set; }
    }

    /// <summary>
    /// Informacje o hodowcy (dostawcy żywca)
    /// </summary>
    public class SupplierInfo
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public string Category { get; set; } // A/B/C
        public int DistanceKm { get; set; }
        public int Coops { get; set; } // Liczba kurników
        public string Notes { get; set; }
    }

    /// <summary>
    /// Informacje o kliencie
    /// </summary>
    public class CustomerInfo
    {
        public string Name { get; set; }
        public decimal VolumePallets { get; set; }
        public string SalesRep { get; set; }
        public string Notes { get; set; } // Uwagi, alarmy, potencjał
    }

    /// <summary>
    /// Informacje o cenie produktu
    /// </summary>
    public class PriceInfo
    {
        public string Product { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; }
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// Szczegółowe informacje o konkurencie
    /// </summary>
    public class CompetitorInfo
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public string Owner { get; set; }
        public string Status { get; set; }
        public string Threat { get; set; } // CRITICAL, HIGH, MEDIUM, LOW
    }

    /// <summary>
    /// Zagrożenia i szanse do monitorowania
    /// </summary>
    public class ThreatsAndOpportunities
    {
        public List<string> CriticalThreats { get; set; } = new()
        {
            "HPAI: 19 ognisk w PL, 2 w łódzkim (NASZ REGION!)",
            "Mrozy: -30°C, transport +15-20% kosztów",
            "Import: Brazylia filet 13 zł vs nasze 15-17 zł",
            "Mercosur: 180k ton duty-free drób do UE",
            "KSeF: obowiązkowy 01.04.2026 (integracja z Sage!)",
            "Nadpodaż Q2: relacja żywiec/pasza 4.24 → hodowcy zwiększają stada"
        };

        public List<string> Opportunities { get; set; } = new()
        {
            "Dino: 300 nowych sklepów - preferuje lokalnych dostawców",
            "Chata Polska: 210 sklepów w łódzkim - NOWY klient potencjalny",
            "Chorten: 3000+ sklepów - dynamiczny rozwój",
            "Spadek cen pasz → niższe koszty hodowców → argument do negocjacji cen skupu w dół"
        };
    }

    #endregion
}
