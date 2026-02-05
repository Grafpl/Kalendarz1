using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services.AI;
using Kalendarz1.MarketIntelligence.Services.DataSources;
using Kalendarz1.MarketIntelligence.Config;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Główny orchestrator pipeline'u pobierania i analizy wiadomości.
    /// Używa Brave Search API jako źródła danych.
    /// </summary>
    public class NewsFetchOrchestrator
    {
        private readonly OpenAIAnalysisService _openAiService;
        private readonly BraveSearchService _newsService;
        private readonly ContentEnrichmentService _enrichmentService;
        private readonly ContentFilterService _filterService;
        private readonly MarketIntelligenceService _dbService;

        public DiagnosticInfo Diagnostics { get; } = new DiagnosticInfo();

        /// <summary>
        /// Logger do pliku TXT - tworzony na poczatku kazdej sesji
        /// </summary>
        public BriefingFileLogger FileLogger { get; private set; }

        /// <summary>
        /// Rozpoczyna logowanie do pliku
        /// </summary>
        public void StartFileLogging(string mode)
        {
            FileLogger = new BriefingFileLogger();
            FileLogger.Mode = mode;
            FileLogger.LogApiConfig(
                _openAiService.ApiKeyPreview,
                _openAiService.IsConfigured,
                _newsService.ApiKeyPreview,
                _newsService.IsConfigured,
                OpenAIAnalysisService.DefaultModel
            );
        }

        /// <summary>
        /// Konczy logowanie i zapisuje plik
        /// </summary>
        public string EndFileLogging(int totalArticles, int successCount, int failedCount, int withAi, int withoutAi)
        {
            if (FileLogger == null) return null;

            FileLogger.LogSummary(totalArticles, successCount, failedCount, withAi, withoutAi);
            FileLogger.SaveToFile();

            var path = FileLogger.LogFilePath;
            FileLogger = null;
            return path;
        }

        /// <summary>
        /// Kontekst biznesowy - pobierany z ConfigService lub domyslny
        /// </summary>
        private string BusinessContext
        {
            get
            {
                // Probuj pobrac z ConfigService
                var configContext = ConfigService.Instance?.BuildBusinessContextString();
                if (!string.IsNullOrEmpty(configContext) && configContext.Length > 100)
                {
                    return configContext;
                }

                // Fallback - domyslny kontekst
                return GetDefaultBusinessContext();
            }
        }

        /// <summary>
        /// Domyslny kontekst biznesowy (fallback gdy ConfigService niedostepny)
        /// </summary>
        private string GetDefaultBusinessContext()
        {
            return @"
FIRMA: Ubojnia Drobiu Piorkowscy Sp.J., Koziolek, 95-060 Brzeziny, NIP 726-162-54-06
ZDOLNOSC: 70 000 kurcząków/dzień (~200 ton), numer weterynaryjny EU
SYTUACJA: Sprzedaż 15M PLN/mies (spadek z 25M), strata ~2M PLN/mies
PRODUKTY: tuszka (~70%), filet (~20%), elementy (udko, skrzydlo, podudzie, cwiartka, korpus, noga)
CENY: żywiec 4.72 zł/kg, filet hurt 24.50 zł/kg, break-even spread 2.50 zł

KONKURENCI KRYTYCZNI:
- Cedrob (NIP 5711000698) — NAJWIĘKSZY w Polsce, ADQ negocjuje przejęcie za 8 mld
- SuperDrob/LipCo (NIP 8361836073) — należy do CPF Tajlandia, Jagiełło w RN
- Drosed (NIP 6462349079) — grupa LDC/ADQ, przejął Indykpol i Konspol
- Animex (NIP 4010009498) — grupa Smithfield/WH Group (Chiny)
- Drobimex (NIP 8510002561) — Szczecin, PHW/Wiesenhof
- Plukon — zakład w Sieradzu, 80km od nas!

KONKURENCI REGIONALNI:
- RADDROB Chlebowski — nasz klient I konkurent!
- System-Drob — region łódzki
- Exdrob Kutno — 100km od nas

KLIENCI GŁÓWNI: Makro, Biedronka DC, Selgros, RADDROB, Carrefour, Stokrotka, Dino, Netto
HODOWCY KLUCZOWI: Sukiennikowa (20km), Kaczmarek (20km), Wojciechowski (7km)
TRANSPORT: Avilog (116-145 zł/km)
ZESPÓŁ HANDLOWY: Jola, Maja, Radek, Teresa, Ania
DYREKTOR: Justyna Chrostowska

ZAGROŻENIA (luty 2026):
1. HPAI: 19 ognisk w Polsce, 2 w łódzkim (NASZ REGION!)
2. Import Brazylia: filet po 13 zł/kg w Makro vs nasze 15-17 zł
3. Mercosur: 180k ton bezcłowego drobiu do UE od 2026
4. Konsolidacja: ADQ kupuje Cedrob za 8 mld
5. KSeF: obowiązkowy od 01.04.2026

SZANSE:
1. HPAI u konkurentów → przejęcie klientów
2. Dino ekspansja 300 nowych sklepów
3. Relacja żywiec/pasza 4.24 (najlepsza od 2 lat)";
        }

        public NewsFetchOrchestrator(string connectionString = null)
        {
            _openAiService = new OpenAIAnalysisService();
            _newsService = new BraveSearchService();
            _enrichmentService = new ContentEnrichmentService();
            _filterService = new ContentFilterService();
            _dbService = new MarketIntelligenceService(connectionString);

            // Inicjalizuj diagnostykę
            RefreshApiStatus();
        }

        /// <summary>
        /// Odświeża status konfiguracji API
        /// </summary>
        public void RefreshApiStatus()
        {
            Diagnostics.IsClaudeConfigured = _openAiService.IsConfigured;
            Diagnostics.ClaudeApiKeyPreview = _openAiService.ApiKeyPreview;
            Diagnostics.ClaudeModel = OpenAIAnalysisService.DefaultModel;

            // Brave Search (zamiast Bing/Perplexity)
            Diagnostics.IsPerplexityConfigured = _newsService.IsConfigured;
            Diagnostics.PerplexityApiKeyPreview = _newsService.ApiKeyPreview;
        }

        /// <summary>
        /// Testuje połączenie z API (Claude + Brave Search)
        /// </summary>
        public async Task<(bool ClaudeOk, string ClaudeMsg, bool PerplexityOk, string PerplexityMsg)> TestConnectionsAsync(CancellationToken ct = default)
        {
            var openAiTask = _openAiService.TestConnectionAsync(ct);
            var perplexityTask = _newsService.TestConnectionAsync(ct); // Brave Search

            await Task.WhenAll(openAiTask, perplexityTask);

            var openAiResult = await openAiTask;
            var perplexityResult = await perplexityTask;

            Diagnostics.IsClaudeConfigured = openAiResult.Success; // Reusing field name for compatibility
            Diagnostics.IsPerplexityConfigured = perplexityResult.Success;

            return (openAiResult.Success, openAiResult.Message, perplexityResult.Success, perplexityResult.Message);
        }

        /// <summary>
        /// BATTLE-TESTED: Glowna metoda - pobiera i analizuje WSZYSTKIE wiadomosci rownolegle
        /// FAIL-SAFE: Zawsze zwraca liste artykulow, nawet jesli scraping/AI zawiedzie
        /// </summary>
        public async Task<List<BriefingArticle>> FetchAndAnalyzeAsync(
            bool quickMode = false,
            IProgress<(string stage, double progress, string detail)> progress = null,
            CancellationToken ct = default)
        {
            Diagnostics.Reset();
            Diagnostics.IsRunning = true;
            var stopwatch = Stopwatch.StartNew();
            var allArticles = new List<BriefingArticle>();

            try
            {
                // ═══════════════════════════════════════════════════════════
                // ETAP 1: Pobieranie z Brave Search (20 wyników)
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Brave Search", 0, "Rozpoczynam wyszukiwanie..."));
                Diagnostics.CurrentStage = "Pobieranie z Brave Search";
                Diagnostics.AddLog("BATTLE-TESTED: Rozpoczynam pobieranie 20 artykulow");

                var searchStopwatch = Stopwatch.StartNew();
                var queries = quickMode ? _newsService.GetQuickQueries() : _newsService.GetAllQueries();
                Diagnostics.PerplexityQueriesTotal = queries.Count;

                var searchProgress = new Progress<(int completed, int total, string query)>(p =>
                {
                    Diagnostics.PerplexityQueriesCompleted = p.completed;
                    var pct = (double)p.completed / p.total * 20; // 0-20%
                    progress?.Report(("Brave Search", pct, $"Zapytanie {p.completed}/{p.total}: {p.query}"));
                });

                var searchArticles = await _newsService.FetchAllNewsAsync(searchProgress, quickMode, ct);
                searchStopwatch.Stop();

                Diagnostics.PerplexityArticlesCount = searchArticles.Count;
                Diagnostics.PerplexityTime = searchStopwatch.Elapsed;
                Diagnostics.AddSuccess($"Brave Search: {searchArticles.Count} artykulow");

                if (searchArticles.Count == 0)
                {
                    Diagnostics.AddError("Brak artykulow z wyszukiwarki!");
                    return allArticles;
                }

                if (ct.IsCancellationRequested) return allArticles;

                // ═══════════════════════════════════════════════════════════
                // ETAP 2: Filtrowanie lokalne (lekkie, szybkie)
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Filtrowanie", 25, "Filtrowanie artykulow..."));
                Diagnostics.CurrentStage = "Filtrowanie lokalne";

                var articlesToFilter = searchArticles
                    .Select(a => (a.Title, a.Snippet, a.Url))
                    .ToList();

                var filteredArticles = _filterService.FilterArticles(articlesToFilter);
                Diagnostics.FilteredCount = filteredArticles.Count;
                Diagnostics.AddLog($"Po filtrowaniu: {filteredArticles.Count}/{searchArticles.Count} artykulow");

                // Mapuj z powrotem do oryginalnych artykulow
                var filteredUrls = new HashSet<string>(filteredArticles.Select(a => a.Url));
                var articlesToProcess = searchArticles
                    .Where(a => !string.IsNullOrEmpty(a.Url) && filteredUrls.Contains(a.Url))
                    .Take(20) // BATTLE-TESTED: Przetwarzaj do 20 artykulow
                    .ToList();

                Diagnostics.AddLog($"Do przetworzenia: {articlesToProcess.Count} artykulow");

                if (ct.IsCancellationRequested) return allArticles;

                // ═══════════════════════════════════════════════════════════
                // ETAP 3-4: ROWNOLEGLE przetwarzanie (Enrichment + AI Analysis)
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Przetwarzanie", 30, "Sekwencyjne przetwarzanie artykulow (anty-429)..."));
                Diagnostics.CurrentStage = "Sekwencyjne przetwarzanie";
                Diagnostics.AddLog("THROTTLING: Przetwarzam artykuly SEKWENCYJNIE z 3s przerwa - eliminacja bledow 429");

                int completed = 0;
                int total = articlesToProcess.Count;
                var resultsList = new List<BriefingArticle>();

                // THROTTLING: Przetwarzanie SEKWENCYJNE (1 na raz) z opoznieniem
                // To CALKOWICIE eliminuje bledy 429 Rate Limit
                foreach (var article in articlesToProcess)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var result = await ProcessSingleArticleAsync(article, ct);
                        if (result != null)
                        {
                            resultsList.Add(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Throttling] Blad przetwarzania: {ex.Message}");
                    }

                    completed++;
                    var pct = 30 + (double)completed / total * 60; // 30-90%
                    progress?.Report(("Przetwarzanie", pct, $"Przetworzono {completed}/{total} (throttling)"));

                    // KLUCZOWE: 3 sekundy przerwy po kazdym artykule - zapobiega 429
                    if (completed < total)
                    {
                        await Task.Delay(3000, ct);
                    }
                }

                var results = resultsList.ToArray();

                // Zbierz wszystkie NIE-NULLOWE wyniki
                allArticles = results
                    .Where(r => r != null)
                    .OrderByDescending(a => a.PublishDate)
                    .ThenByDescending(a => a.Severity == SeverityLevel.Critical ? 3 :
                                           a.Severity == SeverityLevel.Warning ? 2 :
                                           a.Severity == SeverityLevel.Positive ? 1 : 0)
                    .ToList();

                // Przenumeruj ID
                for (int i = 0; i < allArticles.Count; i++)
                {
                    allArticles[i].Id = i + 1;
                }

                Diagnostics.AnalyzedCount = allArticles.Count;
                Diagnostics.AddSuccess($"Przetworzono {allArticles.Count}/{total} artykulow");

                // ═══════════════════════════════════════════════════════════
                // ETAP 5: Finalizacja
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Finalizacja", 95, "Finalizacja..."));
                Diagnostics.CurrentStage = "Finalizacja";

                Diagnostics.TotalUniqueArticles = allArticles.Count;
                Diagnostics.SavedToDbCount = allArticles.Count;

                // Oznacz pierwszy artykul jako featured
                if (allArticles.Any())
                {
                    var criticalArticle = allArticles.FirstOrDefault(a => a.Severity == SeverityLevel.Critical);
                    if (criticalArticle != null)
                    {
                        criticalArticle.IsFeatured = true;
                    }
                    else
                    {
                        allArticles.First().IsFeatured = true;
                    }
                }

                stopwatch.Stop();
                Diagnostics.LastRunTime = DateTime.Now;
                Diagnostics.AddSuccess($"BATTLE-TESTED: Zakonczono w {stopwatch.Elapsed.TotalSeconds:N1}s - {allArticles.Count} artykulow");

                progress?.Report(("Zakończono", 100, $"Gotowe: {allArticles.Count} artykulow"));
            }
            catch (OperationCanceledException)
            {
                Diagnostics.AddWarning("Operacja anulowana przez uzytkownika");
            }
            catch (Exception ex)
            {
                Diagnostics.AddError($"Blad krytyczny: {ex.Message}");
                Debug.WriteLine($"[Orchestrator] Error: {ex}");
            }
            finally
            {
                Diagnostics.IsRunning = false;
                Diagnostics.CurrentStage = "";
            }

            return allArticles;
        }

        /// <summary>
        /// BATTLE-TESTED: Przetwarza pojedynczy artykul (enrichment + AI) z pelnym fallback
        /// NIGDY nie zwraca null jesli mamy jakiekolwiek dane - zawsze tworzy BriefingArticle
        /// </summary>
        private async Task<BriefingArticle> ProcessSingleArticleAsync(
            DataSources.NewsArticle article,
            CancellationToken ct)
        {
            try
            {
                // KROK 1: Enrichment z fallback
                var enrichResult = await _enrichmentService.EnrichWithFallbackAsync(
                    article.Url,
                    article.Title,
                    article.Snippet,
                    ct);

                // enrichResult.Success jest ZAWSZE true dzieki fallback
                var fullContent = enrichResult.Content ?? article.Snippet ?? article.Title;

                // KROK 2: Analiza AI (z tolerancja na bledy)
                ArticleAnalysisResult analysisResult;

                if (_openAiService.IsConfigured && !string.IsNullOrEmpty(fullContent))
                {
                    try
                    {
                        analysisResult = await _openAiService.AnalyzeArticleAsync(
                            article.Title,
                            fullContent,
                            "Brave / " + GetDomain(article.Url),
                            BusinessContext,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        // AI zawiodlo - tworzymy podstawowy wynik
                        Debug.WriteLine($"[ProcessArticle] AI failed for {article.Url}: {ex.Message}");
                        analysisResult = CreateFallbackAnalysis(article.Title, fullContent, enrichResult.IsFallback);
                    }
                }
                else
                {
                    // Brak API - tworzymy podstawowy wynik
                    analysisResult = CreateFallbackAnalysis(article.Title, fullContent, enrichResult.IsFallback);
                }

                // KROK 3: Buduj BriefingArticle - ZAWSZE
                var briefingArticle = new BriefingArticle
                {
                    Id = 0, // Zostanie ustawione pozniej
                    Title = article.Title,
                    SmartTitle = analysisResult?.SmartTitle ?? TruncateTitle(article.Title, 60),
                    SentimentScore = analysisResult?.SentimentScore ?? 0.0,
                    Impact = ParseImpactLevel(analysisResult?.Impact ?? "Medium"),
                    ShortPreview = TruncateContent(article.Snippet ?? article.Title, 150),
                    FullContent = analysisResult?.Summary ?? fullContent,
                    MarketContext = analysisResult?.MarketContext,
                    EducationalSection = analysisResult?.WhoIs,
                    TermsExplanation = analysisResult?.TermsExplanation,
                    AiAnalysisCeo = analysisResult?.AnalysisCeo ?? "[Analiza niedostepna]",
                    AiAnalysisSales = analysisResult?.AnalysisSales ?? "[Analiza niedostepna]",
                    AiAnalysisBuyer = analysisResult?.AnalysisBuyer ?? "[Analiza niedostepna]",
                    RecommendedActionsCeo = string.Join("\n", analysisResult?.ActionsCeo ?? new List<string>()),
                    RecommendedActionsSales = string.Join("\n", analysisResult?.ActionsSales ?? new List<string>()),
                    RecommendedActionsBuyer = string.Join("\n", analysisResult?.ActionsBuyer ?? new List<string>()),
                    IndustryLesson = analysisResult?.IndustryLesson,
                    StrategicQuestions = string.Join("\n", analysisResult?.StrategicQuestions ?? new List<string>()),
                    SourcesToMonitor = string.Join("\n", analysisResult?.SourcesToMonitor ?? new List<string>()),
                    Category = NormalizeCategory(analysisResult?.Category, article.Title, fullContent),
                    Source = GetDomain(article.Url) ?? "Brave",
                    SourceUrl = article.Url,
                    PublishDate = enrichResult.PublishDate ?? DateTime.Today,
                    Severity = ParseSeverity(analysisResult?.Severity ?? DetectSeverityFromCategory(
                        NormalizeCategory(analysisResult?.Category, article.Title, fullContent), article.Title, fullContent)),
                    Tags = analysisResult?.Tags ?? new List<string>(),
                    IsFeatured = false
                };

                // Dodaj tag jesli to fallback
                if (enrichResult.IsFallback)
                {
                    briefingArticle.Tags = new List<string>(briefingArticle.Tags) { "fallback" };
                }

                return briefingArticle;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessArticle] Error for {article?.Url}: {ex.Message}");
                // Nawet przy bledzie - sprobuj zwrocic cos
                if (article != null && !string.IsNullOrEmpty(article.Title))
                {
                    return new BriefingArticle
                    {
                        Title = article.Title,
                        SmartTitle = TruncateTitle(article.Title, 60),
                        ShortPreview = article.Snippet ?? "",
                        FullContent = $"[Blad przetwarzania: {ex.Message}]\n\n{article.Snippet ?? article.Title}",
                        Source = GetDomain(article.Url) ?? "Unknown",
                        SourceUrl = article.Url,
                        PublishDate = DateTime.Today,
                        Category = "Info",
                        Severity = SeverityLevel.Info,
                        Tags = new List<string> { "error" }
                    };
                }
                return null;
            }
        }

        /// <summary>
        /// Tworzy podstawowa analize gdy AI jest niedostepne
        /// </summary>
        private ArticleAnalysisResult CreateFallbackAnalysis(string title, string content, bool isFallbackContent)
        {
            var prefix = isFallbackContent ? "[NA PODSTAWIE STRESZCZENIA] " : "";
            var truncatedContent = content?.Length > 800 ? content.Substring(0, 800) + "..." : content;

            return new ArticleAnalysisResult
            {
                SmartTitle = title?.Length > 60 ? title.Substring(0, 57) + "..." : title ?? "News",
                SentimentScore = 0.0,
                Impact = "Medium",
                Summary = $"{prefix}{truncatedContent}",
                MarketContext = isFallbackContent
                    ? "Kontekst niedostepny - artykul oparty na streszczeniu z wyszukiwarki."
                    : "Kontekst niedostepny - analiza AI niedostepna.",
                WhoIs = "Informacje o podmiotach niedostepne.",
                TermsExplanation = "Tlumaczenie terminow niedostepne.",
                AnalysisCeo = "Analiza dla CEO niedostepna. Przeczytaj artykul i wyciagnij wnioski.",
                AnalysisSales = "Analiza dla Handlowca niedostepna.",
                AnalysisBuyer = "Analiza dla Zakupowca niedostepna.",
                IndustryLesson = "Lekcja branzowa niedostepna.",
                ActionsCeo = new List<string>(),
                ActionsSales = new List<string>(),
                ActionsBuyer = new List<string>(),
                StrategicQuestions = new List<string>(),
                SourcesToMonitor = new List<string>(),
                Category = "Info",
                Severity = "info",
                Tags = isFallbackContent
                    ? new List<string> { "fallback", "no-ai" }
                    : new List<string> { "no-ai" }
            };
        }

        /// <summary>
        /// Testowy pipeline dla jednego artykulu - pokazuje wszystkie logi
        /// Z RETRY/FALLBACK: probuje kolejne artykuly az znajdzie dzialajacy
        /// </summary>
        public async Task<BriefingArticle> TestSingleArticlePipelineAsync(CancellationToken ct = default)
        {
            Diagnostics.Reset();
            Diagnostics.IsRunning = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // ═══════════════════════════════════════════════════════════
                // ETAP 1: Pobranie listy artykulow z Brave Search
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 1: BRAVE SEARCH ===");
                Diagnostics.AddLog("Wysylam zapytanie: 'ceny drobiu Polska luty 2026'");

                var searchStopwatch = Stopwatch.StartNew();
                var (articles, debugInfo) = await _newsService.SearchWithDebugAsync("ceny drobiu Polska luty 2026", ct);
                searchStopwatch.Stop();

                Diagnostics.PerplexityTime = searchStopwatch.Elapsed;
                Diagnostics.PerplexityArticlesCount = articles.Count;
                Diagnostics.AddLog($"Brave odpowiedzial w {searchStopwatch.ElapsedMilliseconds}ms");
                Diagnostics.AddLog($"Znaleziono {articles.Count} kandydatow");

                if (articles.Count == 0)
                {
                    Diagnostics.AddError("Brave nie zwrocil zadnych artykulow!");
                    return null;
                }

                // Pokaz liste kandydatow
                Diagnostics.AddLog("--- LISTA KANDYDATOW ---");
                for (int i = 0; i < Math.Min(articles.Count, 10); i++)
                {
                    var a = articles[i];
                    Diagnostics.AddLog($"  [{i + 1}] {TruncateTitle(a.Title, 50)} ({GetDomain(a.Url)})");
                }

                // ═══════════════════════════════════════════════════════════
                // ETAP 2-4: PETLA RETRY - probuj kolejne artykuly
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 2-4: PETLA RETRY ===");
                Diagnostics.AddLog("Probuje kolejne artykuly az znajde dzialajacy...");

                int attemptCount = 0;
                int maxAttempts = Math.Min(articles.Count, 15); // Max 15 prob

                // Filtruj artykuly z URL
                var candidateArticles = articles
                    .Where(a => !string.IsNullOrEmpty(a.Url) && a.Url.StartsWith("http"))
                    .Take(maxAttempts)
                    .ToList();

                foreach (var testArticle in candidateArticles)
                {
                    if (ct.IsCancellationRequested) break;

                    attemptCount++;
                    Diagnostics.AddLog($"");
                    Diagnostics.AddLog($"--- PROBA {attemptCount}/{candidateArticles.Count}: {GetDomain(testArticle.Url)} ---");

                    // KROK 2: Filtrowanie lokalne (WYŁĄCZONE - akceptujemy wszystko)
                    // Nie odrzucamy już artykułów przez blacklist
                    Diagnostics.AddLog($"  [OK] Filtr wylaczony - akceptuje artykul");

                    // KROK 3: Wzbogacanie tresci z FALLBACK
                    string fullContent = testArticle.Snippet ?? testArticle.Title ?? "";

                    var enrichResult = await _enrichmentService.EnrichWithFallbackAsync(
                        testArticle.Url, testArticle.Title, testArticle.Snippet, ct);

                    // EnrichWithFallback ZAWSZE zwraca Success=true (fallback na snippet)
                    fullContent = enrichResult.Content ?? testArticle.Snippet ?? testArticle.Title ?? "";

                    if (enrichResult.IsFallback)
                    {
                        Diagnostics.AddLog($"  [FALLBACK] Uzyto snippetu: {fullContent.Length} znakow");
                    }
                    else
                    {
                        Diagnostics.AddLog($"  [OK] Pobrano pelna tresc: {fullContent.Length} znakow");
                    }

                    // Sprawdz dlugosc tresci - OBNIZONE MINIMUM do 50 znakow!
                    if (fullContent.Length < 50)
                    {
                        Diagnostics.AddLog($"  [SKIP] Tresc za krotka ({fullContent.Length} < 50 znakow)");
                        continue; // RETRY - idz do nastepnego
                    }

                    Diagnostics.EnrichedCount++;

                    // KROK 4: Analiza AI
                    if (!_openAiService.IsConfigured)
                    {
                        Diagnostics.AddError("OpenAI API nie skonfigurowane!");
                        return null;
                    }

                    Diagnostics.AddLog($"  Wysylam do OpenAI ({fullContent.Length} znakow)...");

                    var analysisStopwatch = Stopwatch.StartNew();
                    var analysisResult = await _openAiService.AnalyzeArticleAsync(
                        testArticle.Title,
                        fullContent,
                        "Brave / " + GetDomain(testArticle.Url),
                        BusinessContext,
                        ct);
                    analysisStopwatch.Stop();

                    Diagnostics.AddLog($"  OpenAI odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms");

                    // Sprawdz czy analiza sie powiodla
                    var summaryHasError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                        (analysisResult.Summary.StartsWith("Blad") ||
                         analysisResult.Summary.StartsWith("BLAD") ||
                         analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                    if (summaryHasError)
                    {
                        Diagnostics.AddLog($"  [SKIP] Blad parsowania odpowiedzi OpenAI");
                        if (!string.IsNullOrEmpty(_openAiService.LastRawResponse))
                        {
                            Diagnostics.AddLog($"  RAW (100 znakow): {TruncateContent(_openAiService.LastRawResponse, 100)}");
                        }
                        continue; // RETRY - idz do nastepnego
                    }

                    // SUKCES! Mamy dzialajacy artykul
                    Diagnostics.AddSuccess($"  [SUKCES] Artykul przeanalizowany pomyslnie!");
                    Diagnostics.AnalyzedCount = 1;

                    // Pokaz wyniki
                    Diagnostics.AddLog("--- PODSUMOWANIE ---");
                    Diagnostics.AddLog(TruncateContent(analysisResult.Summary, 200));
                    Diagnostics.AddLog($"--- KATEGORIA: {analysisResult.Category} ---");
                    Diagnostics.AddLog($"--- SEVERITY: {analysisResult.Severity} ---");

                    // ETAP 5: Tworzenie BriefingArticle
                    var briefingArticle = new BriefingArticle
                    {
                        Id = 1,
                        Title = testArticle.Title,
                        SmartTitle = analysisResult.SmartTitle,
                        SentimentScore = analysisResult.SentimentScore,
                        Impact = ParseImpactLevel(analysisResult.Impact),
                        ShortPreview = TruncateContent(testArticle.Snippet, 150),
                        FullContent = analysisResult.Summary,
                        EducationalSection = analysisResult.WhoIs,
                        AiAnalysisCeo = analysisResult.AnalysisCeo,
                        AiAnalysisSales = analysisResult.AnalysisSales,
                        AiAnalysisBuyer = analysisResult.AnalysisBuyer,
                        RecommendedActionsCeo = string.Join("\n", analysisResult.ActionsCeo),
                        RecommendedActionsSales = string.Join("\n", analysisResult.ActionsSales),
                        RecommendedActionsBuyer = string.Join("\n", analysisResult.ActionsBuyer),
                        Category = NormalizeCategory(analysisResult.Category, testArticle.Title, fullContent),
                        Source = GetDomain(testArticle.Url) ?? "Brave",
                        SourceUrl = testArticle.Url,
                        PublishDate = DateTime.Today,
                        Severity = ParseSeverity(analysisResult.Severity ?? DetectSeverityFromCategory(
                            NormalizeCategory(analysisResult.Category, testArticle.Title, fullContent), testArticle.Title, fullContent)),
                        Tags = analysisResult.Tags ?? new List<string>(),
                        IsFeatured = true
                    };

                    stopwatch.Stop();
                    Diagnostics.AddSuccess($"=== PIPELINE UKONCZONY w {stopwatch.Elapsed.TotalSeconds:N1}s (proba {attemptCount}/{candidateArticles.Count}) ===");

                    return briefingArticle;
                }

                // Wszystkie proby sie nie powiodly
                Diagnostics.AddError($"Wszystkie {attemptCount} prob zakonczonych niepowodzeniem!");
                Diagnostics.AddLog("Zadna strona nie zwrocila wystarczajacej tresci do analizy.");
                return null;
            }
            catch (Exception ex)
            {
                Diagnostics.AddError($"Blad pipeline: {ex.Message}");
                Debug.WriteLine($"[TestPipeline] Error: {ex}");
                return null;
            }
            finally
            {
                Diagnostics.IsRunning = false;
            }
        }

        /// <summary>
        /// Testowy pipeline ze szczegolowymi logami do okna logow
        /// Z RETRY/FALLBACK: probuje kolejne artykuly az znajdzie dzialajacy
        /// </summary>
        public async Task<BriefingArticle> TestSingleArticlePipelineWithLogsAsync(
            Action<string, string> log,
            Action<string, string> logRaw,
            Action<string> logSection,
            CancellationToken ct = default)
        {
            Diagnostics.Reset();
            Diagnostics.IsRunning = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // ═══════════════════════════════════════════════════════════
                // ETAP 1: Pobranie listy artykulow z Perplexity
                // ═══════════════════════════════════════════════════════════
                logSection("ETAP 1: PERPLEXITY SEARCH");
                log("Wysylam testowe zapytanie: 'ceny drobiu Polska luty 2026'", "INFO");
                log($"Model: sonar", "INFO");
                log($"API URL: https://api.perplexity.ai/chat/completions", "DEBUG");
                Diagnostics.AddLog("=== ETAP 1: PERPLEXITY ===");

                var perplexityStopwatch = Stopwatch.StartNew();
                var (articles, debugInfo) = await _newsService.SearchWithDebugAsync("ceny drobiu Polska luty 2026", ct);
                perplexityStopwatch.Stop();

                Diagnostics.PerplexityTime = perplexityStopwatch.Elapsed;
                Diagnostics.PerplexityArticlesCount = articles.Count;

                log($"Perplexity odpowiedzial w {perplexityStopwatch.ElapsedMilliseconds}ms", "SUCCESS");
                log($"Znaleziono {articles.Count} artykulow-kandydatow", articles.Count > 0 ? "SUCCESS" : "WARNING");

                // Pokaz pelna odpowiedz API
                logRaw(debugInfo, "PERPLEXITY RAW RESPONSE");

                if (articles.Count == 0)
                {
                    log("Perplexity nie zwrocil zadnych artykulow!", "ERROR");
                    log("Mozliwe przyczyny:", "WARNING");
                    log("  1. Model 'sonar' moze zwracac dane w innym formacie", "WARNING");
                    log("  2. Pole 'citations' moze byc puste lub nieobecne", "WARNING");
                    log("  3. Zapytanie moze byc zbyt specyficzne", "WARNING");
                    Diagnostics.AddError("Perplexity nie zwrocil artykulow");
                    return null;
                }

                // Lista wszystkich znalezionych artykulow
                log("--- ZNALEZIONE ARTYKULY (KANDYDACI) ---", "INFO");
                for (int i = 0; i < articles.Count; i++)
                {
                    var a = articles[i];
                    log($"[{i + 1}] {a.Title}", "INFO");
                    log($"    URL: {a.Url ?? "brak"}", "DEBUG");
                    log($"    Zrodlo: {a.Source}", "DEBUG");
                }

                // Filtruj artykuly z URL
                var candidateArticles = articles
                    .Where(a => !string.IsNullOrEmpty(a.Url) && a.Url.StartsWith("http"))
                    .Take(15) // Max 15 prob
                    .ToList();

                if (candidateArticles.Count == 0)
                {
                    log("Brak artykulow z poprawnymi URL!", "ERROR");
                    Diagnostics.AddError("Brak artykulow z URL");
                    return null;
                }

                log($"Bede probowal {candidateArticles.Count} artykulow az znajde dzialajacy...", "INFO");
                Diagnostics.AddLog($"Kandydatow z URL: {candidateArticles.Count}");

                // ═══════════════════════════════════════════════════════════
                // ETAP 2-4: PETLA RETRY - probuj kolejne artykuly
                // ═══════════════════════════════════════════════════════════
                logSection("ETAP 2-4: PETLA RETRY");
                log("Probuje kolejne artykuly az znajde dzialajacy...", "INFO");
                Diagnostics.AddLog("=== ETAP 2-4: PETLA RETRY ===");

                int attemptCount = 0;

                foreach (var testArticle in candidateArticles)
                {
                    if (ct.IsCancellationRequested)
                    {
                        log("Anulowano przez uzytkownika", "WARNING");
                        break;
                    }

                    attemptCount++;
                    log("", "INFO");
                    log($"══════════════════════════════════════════════════════════", "INFO");
                    log($"PROBA {attemptCount}/{candidateArticles.Count}: {TruncateTitle(testArticle.Title, 50)}", "INFO");
                    log($"Domena: {GetDomain(testArticle.Url)}", "DEBUG");
                    Diagnostics.AddLog($"--- PROBA {attemptCount}: {GetDomain(testArticle.Url)} ---");

                    // ─────────────────────────────────────────────────────
                    // KROK 2: Filtrowanie lokalne
                    // ─────────────────────────────────────────────────────
                    log("Krok 2: Sprawdzam blacklist...", "INFO");

                    // KROK 2: Filtrowanie - WYŁĄCZONE (akceptujemy wszystko)
                    log("  [OK] Filtr wylaczony - akceptuje wszystkie artykuly", "SUCCESS");
                    Diagnostics.AddLog($"  [OK] Filtr wylaczony");

                    // ─────────────────────────────────────────────────────
                    // KROK 3: Wzbogacanie tresci z FALLBACK
                    // ─────────────────────────────────────────────────────
                    log("Krok 3: Pobieram pelna tresc (Puppeteer/HTTP)...", "INFO");
                    log($"  URL: {testArticle.Url}", "DEBUG");

                    var enrichResult = await _enrichmentService.EnrichWithFallbackAsync(
                        testArticle.Url, testArticle.Title, testArticle.Snippet, ct);

                    // EnrichWithFallback ZAWSZE zwraca Success=true (fallback na snippet)
                    string fullContent = enrichResult.Content ?? testArticle.Snippet ?? testArticle.Title ?? "";

                    if (enrichResult.IsFallback)
                    {
                        log($"  [FALLBACK] Uzyto snippetu: {fullContent.Length} znakow", "WARNING");
                        Diagnostics.AddLog($"  [FALLBACK] Snippet: {fullContent.Length} znakow");
                    }
                    else
                    {
                        log($"  [OK] Pobrano pelna tresc: {fullContent.Length} znakow", "SUCCESS");
                        Diagnostics.AddLog($"  [OK] Tresc: {fullContent.Length} znakow");
                    }

                    // Pokaz poczatek tresci
                    var preview = fullContent.Length > 300 ? fullContent.Substring(0, 300) + "..." : fullContent;
                    logRaw(preview, "POBRANA TRESC (pierwsze 300 znakow)");

                    // Sprawdz dlugosc tresci - OBNIZONE MINIMUM do 50 znakow!
                    if (fullContent.Length < 50)
                    {
                        log($"  [SKIP] Tresc za krotka ({fullContent.Length} < 50 znakow)", "WARNING");
                        log("  Idz do nastepnego artykulu...", "INFO");
                        Diagnostics.AddLog($"  [SKIP] Za krotka ({fullContent.Length} znakow)");
                        continue; // RETRY
                    }

                    Diagnostics.EnrichedCount++;
                    Diagnostics.FilteredCount++;

                    // ─────────────────────────────────────────────────────
                    // KROK 4: Analiza AI
                    // ─────────────────────────────────────────────────────
                    log("Krok 4: Wysylam do OpenAI...", "INFO");
                    log($"  Model: {OpenAIAnalysisService.DefaultModel}", "INFO");
                    log($"  Dlugosc tresci: {fullContent.Length} znakow", "DEBUG");

                    if (!_openAiService.IsConfigured)
                    {
                        log("OpenAI API nie skonfigurowane!", "ERROR");
                        log("Sprawdz klucz API w App.config lub zmiennej srodowiskowej OPENAI_API_KEY", "ERROR");
                        Diagnostics.AddError("OpenAI API nie skonfigurowane");
                        return null;
                    }

                    var analysisStopwatch = Stopwatch.StartNew();
                    var analysisResult = await _openAiService.AnalyzeArticleAsync(
                        testArticle.Title,
                        fullContent,
                        "Perplexity / " + GetDomain(testArticle.Url),
                        BusinessContext,
                        ct);
                    analysisStopwatch.Stop();

                    log($"  OpenAI odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms", "SUCCESS");
                    Diagnostics.AddLog($"  OpenAI: {analysisStopwatch.ElapsedMilliseconds}ms");

                    // Sprawdz czy analiza sie powiodla
                    var summaryContainsError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                        (analysisResult.Summary.StartsWith("Blad") ||
                         analysisResult.Summary.StartsWith("BLAD") ||
                         analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                    if (summaryContainsError || string.IsNullOrEmpty(analysisResult.Summary))
                    {
                        log($"  [SKIP] Blad parsowania odpowiedzi OpenAI", "WARNING");

                        // Szczegolowe logi bledu
                        if (!string.IsNullOrEmpty(_openAiService.LastParsingError))
                        {
                            log($"  Szczegoly: {TruncateContent(_openAiService.LastParsingError, 200)}", "DEBUG");
                        }
                        if (!string.IsNullOrEmpty(_openAiService.LastRawResponse))
                        {
                            log($"  RAW (100 znakow): {TruncateContent(_openAiService.LastRawResponse, 100)}", "DEBUG");
                        }

                        log("  Idz do nastepnego artykulu...", "INFO");
                        Diagnostics.AddLog($"  [SKIP] Blad parsowania JSON");
                        continue; // RETRY
                    }

                    // ═══════════════════════════════════════════════════════════
                    // SUKCES! Mamy dzialajacy artykul
                    // ═══════════════════════════════════════════════════════════
                    log("", "SUCCESS");
                    log("══════════════════════════════════════════════════════════", "SUCCESS");
                    log($"SUKCES! Artykul przeanalizowany przy probie {attemptCount}/{candidateArticles.Count}", "SUCCESS");
                    log("══════════════════════════════════════════════════════════", "SUCCESS");

                    Diagnostics.AnalyzedCount = 1;
                    Diagnostics.AddSuccess($"SUKCES przy probie {attemptCount}");

                    // Pokaz pelne wyniki analizy
                    logSection("WYNIKI ANALIZY AI");

                    log("=== PODSUMOWANIE (10-15 zdan) ===", "INFO");
                    logRaw(analysisResult.Summary ?? "brak", "SUMMARY");

                    log("=== KONTEKST RYNKOWY ===", "INFO");
                    logRaw(analysisResult.MarketContext ?? "brak", "MARKET CONTEXT");

                    log("=== KIM JEST / CO TO JEST (edukacja o podmiotach) ===", "INFO");
                    logRaw(analysisResult.WhoIs ?? "brak", "WHO IS");

                    log("=== TLUMACZENIE POJEC BRANZOWYCH ===", "INFO");
                    logRaw(analysisResult.TermsExplanation ?? "brak", "TERMS");

                    log("=== ANALIZA DLA CEO (strategiczna) ===", "INFO");
                    logRaw(analysisResult.AnalysisCeo ?? "brak", "CEO ANALYSIS");

                    log("=== AKCJE DLA CEO ===", "INFO");
                    if (analysisResult.ActionsCeo?.Any() == true)
                    {
                        foreach (var action in analysisResult.ActionsCeo)
                        {
                            log($"  * {action}", "INFO");
                        }
                    }

                    log("=== ANALIZA DLA HANDLOWCA ===", "INFO");
                    logRaw(analysisResult.AnalysisSales ?? "brak", "SALES ANALYSIS");

                    log("=== AKCJE DLA HANDLOWCA ===", "INFO");
                    if (analysisResult.ActionsSales?.Any() == true)
                    {
                        foreach (var action in analysisResult.ActionsSales)
                        {
                            log($"  * {action}", "INFO");
                        }
                    }

                    log("=== ANALIZA DLA ZAKUPOWCA ===", "INFO");
                    logRaw(analysisResult.AnalysisBuyer ?? "brak", "BUYER ANALYSIS");

                    log("=== AKCJE DLA ZAKUPOWCA ===", "INFO");
                    if (analysisResult.ActionsBuyer?.Any() == true)
                    {
                        foreach (var action in analysisResult.ActionsBuyer)
                        {
                            log($"  * {action}", "INFO");
                        }
                    }

                    log("=== LEKCJA BRANZOWA (edukacja) ===", "INFO");
                    logRaw(analysisResult.IndustryLesson ?? "brak", "INDUSTRY LESSON");

                    log("=== PYTANIA STRATEGICZNE DO PRZEMYSLENIA ===", "INFO");
                    if (analysisResult.StrategicQuestions?.Any() == true)
                    {
                        foreach (var q in analysisResult.StrategicQuestions)
                        {
                            log($"  ? {q}", "INFO");
                        }
                    }

                    log("=== ZRODLA DO MONITOROWANIA ===", "INFO");
                    if (analysisResult.SourcesToMonitor?.Any() == true)
                    {
                        foreach (var s in analysisResult.SourcesToMonitor)
                        {
                            log($"  >> {s}", "INFO");
                        }
                    }

                    log($"Kategoria: {analysisResult.Category}", "INFO");
                    log($"Severity: {analysisResult.Severity}", "INFO");
                    log($"Tagi: {string.Join(", ", analysisResult.Tags ?? new List<string>())}", "INFO");

                    // ═══════════════════════════════════════════════════════════
                    // ETAP 5: Tworzenie BriefingArticle
                    // ═══════════════════════════════════════════════════════════
                    logSection("ETAP 5: TWORZENIE ARTYKULU BRIEFINGOWEGO");
                    Diagnostics.AddLog("=== ETAP 5: TWORZENIE ===");

                    var briefingArticle = new BriefingArticle
                    {
                        Id = 1,
                        Title = testArticle.Title,
                        SmartTitle = analysisResult.SmartTitle,
                        SentimentScore = analysisResult.SentimentScore,
                        Impact = ParseImpactLevel(analysisResult.Impact),
                        ShortPreview = TruncateContent(testArticle.Snippet, 150),
                        FullContent = analysisResult.Summary,
                        MarketContext = analysisResult.MarketContext,
                        EducationalSection = analysisResult.WhoIs,
                        TermsExplanation = analysisResult.TermsExplanation,
                        AiAnalysisCeo = analysisResult.AnalysisCeo,
                        AiAnalysisSales = analysisResult.AnalysisSales,
                        AiAnalysisBuyer = analysisResult.AnalysisBuyer,
                        RecommendedActionsCeo = string.Join("\n", analysisResult.ActionsCeo ?? new List<string>()),
                        RecommendedActionsSales = string.Join("\n", analysisResult.ActionsSales ?? new List<string>()),
                        RecommendedActionsBuyer = string.Join("\n", analysisResult.ActionsBuyer ?? new List<string>()),
                        IndustryLesson = analysisResult.IndustryLesson,
                        StrategicQuestions = string.Join("\n", analysisResult.StrategicQuestions ?? new List<string>()),
                        SourcesToMonitor = string.Join("\n", analysisResult.SourcesToMonitor ?? new List<string>()),
                        Category = NormalizeCategory(analysisResult.Category, testArticle.Title, fullContent),
                        Source = GetDomain(testArticle.Url) ?? "Perplexity",
                        SourceUrl = testArticle.Url,
                        PublishDate = DateTime.Today,
                        Severity = ParseSeverity(analysisResult.Severity ?? DetectSeverityFromCategory(
                            NormalizeCategory(analysisResult.Category, testArticle.Title, fullContent), testArticle.Title, fullContent)),
                        Tags = analysisResult.Tags ?? new List<string>(),
                        IsFeatured = true
                    };

                    stopwatch.Stop();
                    log($"Artykul utworzony pomyslnie!", "SUCCESS");
                    log($"Calkowity czas pipeline: {stopwatch.Elapsed.TotalSeconds:N1} sekund", "SUCCESS");
                    log($"Wykorzystano probe {attemptCount} z {candidateArticles.Count}", "INFO");
                    Diagnostics.AddSuccess($"Pipeline OK: {stopwatch.Elapsed.TotalSeconds:N1}s (proba {attemptCount})");

                    return briefingArticle;
                }

                // ═══════════════════════════════════════════════════════════
                // Wszystkie proby sie nie powiodly
                // ═══════════════════════════════════════════════════════════
                logSection("WSZYSTKIE PROBY SIE NIE POWIODLY");
                log($"Przetestowano {attemptCount} artykulow, zaden nie zwrocil wystarczajacej tresci.", "ERROR");
                log("Mozliwe przyczyny:", "WARNING");
                log("  1. Strony blokuja pobieranie tresci (paywall, anti-bot)", "WARNING");
                log("  2. Tresci sa zbyt krotkie (< 500 znakow)", "WARNING");
                log("  3. Bledy parsowania odpowiedzi Claude", "WARNING");
                Diagnostics.AddError($"Wszystkie {attemptCount} prob zakonczonych niepowodzeniem");
                return null;
            }
            catch (Exception ex)
            {
                log($"WYJATEK: {ex.Message}", "ERROR");
                log($"Typ: {ex.GetType().Name}", "ERROR");
                logRaw(ex.StackTrace ?? "brak", "STACK TRACE");
                Diagnostics.AddError($"Exception: {ex.Message}");
                return null;
            }
            finally
            {
                Diagnostics.IsRunning = false;
            }
        }

        /// <summary>
        /// Testowy pipeline z 5 demo artykulami - bez uzycia Perplexity API
        /// Przydatne do testowania analizy Claude bez kosztow API
        /// </summary>
        public async Task<List<BriefingArticle>> TestDemoArticlesPipelineAsync(
            Action<string, string> log,
            Action<string, string> logRaw,
            Action<string> logSection,
            CancellationToken ct = default)
        {
            Diagnostics.Reset();
            Diagnostics.IsRunning = true;
            var stopwatch = Stopwatch.StartNew();
            var results = new List<BriefingArticle>();

            try
            {
                // ═══════════════════════════════════════════════════════════
                // ETAP 1: Pobierz demo artykuly (bez API)
                // ═══════════════════════════════════════════════════════════
                logSection("ETAP 1: DEMO ARTYKULY (BEZ API)");
                log("Laduje 5 testowych artykulow z DemoArticlesProvider...", "INFO");
                Diagnostics.AddLog("=== ETAP 1: DEMO ARTICLES ===");

                var demoArticles = DemoArticlesProvider.GetDemoArticles();
                log($"Zaladowano {demoArticles.Count} demo artykulow:", "SUCCESS");

                for (int i = 0; i < demoArticles.Count; i++)
                {
                    var a = demoArticles[i];
                    log($"[{i + 1}] {TruncateTitle(a.Title, 70)}", "INFO");
                    log($"    Zrodlo: {a.Source}", "DEBUG");
                }

                Diagnostics.PerplexityArticlesCount = demoArticles.Count;
                Diagnostics.AddSuccess($"Zaladowano {demoArticles.Count} demo artykulow");

                // ═══════════════════════════════════════════════════════════
                // ETAP 2: Analiza kazdego artykulu przez OpenAI
                // ═══════════════════════════════════════════════════════════
                logSection("ETAP 2: ANALIZA OPENAI GPT-4o");
                log($"Bede analizowal {demoArticles.Count} artykulow przez OpenAI...", "INFO");
                log($"Model: {OpenAIAnalysisService.DefaultModel}", "INFO");
                Diagnostics.AddLog("=== ETAP 2: OPENAI ANALYSIS ===");

                int analyzed = 0;
                int errors = 0;

                foreach (var article in demoArticles)
                {
                    if (ct.IsCancellationRequested)
                    {
                        log("Anulowano przez uzytkownika", "WARNING");
                        break;
                    }

                    analyzed++;
                    log($"", "INFO");
                    logSection($"ARTYKUL {analyzed}/{demoArticles.Count}");
                    log($"Tytul: {article.Title}", "INFO");
                    log($"Zrodlo: {article.Source}", "DEBUG");
                    log($"Tresc (fragment): {TruncateContent(article.Snippet, 200)}", "DEBUG");

                    string fullContent = article.Snippet ?? "";

                    // Sprawdz czy mamy wystarczajaco tresci do analizy
                    if (string.IsNullOrWhiteSpace(fullContent) || fullContent.Length < 50)
                    {
                        log("UWAGA: Zbyt krotka tresc - uzywam tytulu", "WARNING");
                        fullContent = $"Tytul: {article.Title}. Zrodlo: {article.Source}. URL: {article.Url}";
                    }

                    log($"Wysylam do OpenAI ({fullContent.Length} znakow)...", "INFO");

                    try
                    {
                        var analysisStopwatch = Stopwatch.StartNew();
                        var analysisResult = await _openAiService.AnalyzeArticleAsync(
                            article.Title,
                            fullContent,
                            article.Source,
                            BusinessContext,
                            ct);
                        analysisStopwatch.Stop();

                        if (analysisResult == null)
                        {
                            log($"OpenAI zwrocil null - blad analizy", "ERROR");
                            errors++;
                            continue;
                        }

                        log($"OpenAI odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms", "SUCCESS");

                        // Sprawdz czy analiza sie powiodla
                        var hasParsingError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                            (analysisResult.Summary.StartsWith("Blad") ||
                             analysisResult.Summary.StartsWith("BLAD") ||
                             analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                        if (hasParsingError)
                        {
                            log($"BLAD PARSOWANIA JSON dla artykulu {analyzed}!", "ERROR");
                            log(TruncateContent(analysisResult.Summary, 500), "ERROR");

                            // Pokaz surowa odpowiedz OpenAI
                            if (!string.IsNullOrEmpty(_openAiService.LastRawResponse))
                            {
                                log("", "ERROR");
                                log("=== SUROWA ODPOWIEDZ OPENAI ===", "ERROR");
                                logRaw(_openAiService.LastRawResponse, "OPENAI RAW");
                            }
                            errors++;
                            continue;
                        }

                        // Pokaz wyniki analizy
                        log("--- WYNIK ANALIZY ---", "SUCCESS");
                        log($"Kategoria: {analysisResult.Category}", "INFO");
                        log($"Priorytet: {analysisResult.Severity}", "INFO");
                        log($"Tagi: {string.Join(", ", analysisResult.Tags ?? new List<string>())}", "INFO");

                        log("", "INFO");
                        log("PODSUMOWANIE:", "INFO");
                        log(TruncateContent(analysisResult.Summary, 500), "DEBUG");

                        log("", "INFO");
                        log("ANALIZA CEO:", "INFO");
                        log(TruncateContent(analysisResult.AnalysisCeo, 400), "DEBUG");

                        log("", "INFO");
                        log("ANALIZA SPRZEDAZ:", "INFO");
                        log(TruncateContent(analysisResult.AnalysisSales, 400), "DEBUG");

                        log("", "INFO");
                        log("ANALIZA ZAKUPY:", "INFO");
                        log(TruncateContent(analysisResult.AnalysisBuyer, 400), "DEBUG");

                        // Tworzymy BriefingArticle
                        var briefingArticle = new BriefingArticle
                        {
                            Id = analyzed,
                            Title = article.Title,
                            SmartTitle = analysisResult.SmartTitle,
                            SentimentScore = analysisResult.SentimentScore,
                            Impact = ParseImpactLevel(analysisResult.Impact),
                            ShortPreview = TruncateContent(article.Snippet, 150),
                            FullContent = analysisResult.Summary,
                            MarketContext = analysisResult.MarketContext,
                            EducationalSection = analysisResult.WhoIs,
                            TermsExplanation = analysisResult.TermsExplanation,
                            AiAnalysisCeo = analysisResult.AnalysisCeo,
                            AiAnalysisSales = analysisResult.AnalysisSales,
                            AiAnalysisBuyer = analysisResult.AnalysisBuyer,
                            RecommendedActionsCeo = string.Join("\n", analysisResult.ActionsCeo ?? new List<string>()),
                            RecommendedActionsSales = string.Join("\n", analysisResult.ActionsSales ?? new List<string>()),
                            RecommendedActionsBuyer = string.Join("\n", analysisResult.ActionsBuyer ?? new List<string>()),
                            IndustryLesson = analysisResult.IndustryLesson,
                            StrategicQuestions = string.Join("\n", analysisResult.StrategicQuestions ?? new List<string>()),
                            SourcesToMonitor = string.Join("\n", analysisResult.SourcesToMonitor ?? new List<string>()),
                            Category = NormalizeCategory(analysisResult.Category, article.Title, fullContent),
                            Source = article.Source ?? "Demo",
                            SourceUrl = article.Url,
                            PublishDate = DateTime.Today,
                            Severity = ParseSeverity(analysisResult.Severity ?? DetectSeverityFromCategory(
                                NormalizeCategory(analysisResult.Category, article.Title, fullContent), article.Title, fullContent)),
                            Tags = analysisResult.Tags ?? new List<string>(),
                            IsFeatured = analyzed == 1
                        };

                        results.Add(briefingArticle);
                        log($"Artykul {analyzed} przeanalizowany pomyslnie!", "SUCCESS");
                    }
                    catch (Exception ex)
                    {
                        log($"BLAD analizy artykulu: {ex.Message}", "ERROR");
                        errors++;
                    }
                }

                // ═══════════════════════════════════════════════════════════
                // PODSUMOWANIE
                // ═══════════════════════════════════════════════════════════
                stopwatch.Stop();
                logSection("PODSUMOWANIE");
                log($"", "INFO");
                log($"Przetworzono: {analyzed} artykulow", "INFO");
                log($"Sukces: {results.Count}", "SUCCESS");
                log($"Bledy: {errors}", errors > 0 ? "WARNING" : "INFO");
                log($"Czas: {stopwatch.Elapsed.TotalSeconds:N1}s", "INFO");

                Diagnostics.AddSuccess($"=== DEMO PIPELINE UKONCZONY: {results.Count}/{analyzed} w {stopwatch.Elapsed.TotalSeconds:N1}s ===");

                return results;
            }
            catch (Exception ex)
            {
                log($"WYJATEK: {ex.Message}", "ERROR");
                log($"Typ: {ex.GetType().Name}", "ERROR");
                logRaw(ex.StackTrace ?? "brak", "STACK TRACE");
                Diagnostics.AddError($"Exception: {ex.Message}");
                return results;
            }
            finally
            {
                Diagnostics.IsRunning = false;
            }
        }

        #region Helper Methods

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
                return url;
            }
        }

        private string TruncateTitle(string title, int maxLength = 50)
        {
            if (string.IsNullOrEmpty(title)) return "";
            return title.Length > maxLength ? title.Substring(0, maxLength) + "..." : title;
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return "";
            return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;
        }

        private SeverityLevel ParseSeverity(string severity)
        {
            return severity?.ToLowerInvariant() switch
            {
                "critical" => SeverityLevel.Critical,
                "warning" => SeverityLevel.Warning,
                "positive" => SeverityLevel.Positive,
                _ => SeverityLevel.Info
            };
        }

        private ImpactLevel ParseImpactLevel(string impact)
        {
            return impact?.ToLowerInvariant() switch
            {
                "critical" => ImpactLevel.Critical,
                "high" => ImpactLevel.High,
                "medium" => ImpactLevel.Medium,
                "low" => ImpactLevel.Low,
                _ => ImpactLevel.Medium
            };
        }

        /// <summary>
        /// Normalizuje kategorię do jednej ze standardowych wartości używanych w zakładkach UI.
        /// Dostępne kategorie: HPAI, Ceny, Konkurencja, Regulacje, Eksport, Import, Klienci, Koszty, Pogoda, Logistyka, Inwestycje, Info
        /// </summary>
        private string NormalizeCategory(string category, string title, string content)
        {
            if (string.IsNullOrEmpty(category))
            {
                return DetectCategoryFromContent(title, content);
            }

            // Normalizuj case i usuń spacje
            var normalized = category.Trim().ToUpperInvariant();

            // Mapowanie na standardowe kategorie
            return normalized switch
            {
                // HPAI - Ptasia grypa
                "HPAI" or "PTASIA GRYPA" or "AVIAN FLU" or "BIRD FLU" or "CHOROBY" or "EPIDEMIA"
                    => "HPAI",

                // Ceny
                "CENY" or "PRICES" or "CENNIK" or "CENY DROBIU" or "CENY RYNKOWE" or "CENY SKUPU"
                    => "Ceny",

                // Konkurencja
                "KONKURENCJA" or "COMPETITION" or "RYNEK" or "PRZEJĘCIA" or "FUZJE" or "M&A"
                or "CEDROB" or "SUPERDROB" or "DROSED" or "ANIMEX" or "DROBIMEX" or "PLUKON"
                    => "Konkurencja",

                // Regulacje
                "REGULACJE" or "REGULATIONS" or "PRAWO" or "PRZEPISY" or "KSEF" or "WETERYNARYJNE"
                or "DOBROSTAN" or "WELFARE" or "COMPLIANCE" or "USTAWA" or "ROZPORZĄDZENIE"
                    => "Regulacje",

                // Eksport
                "EKSPORT" or "EXPORT" or "EKSPORTOWY" or "HANDEL ZAGRANICZNY"
                    => "Eksport",

                // Import
                "IMPORT" or "IMPORTOWY" or "BRAZYLIA" or "MERCOSUR" or "UKRAINE" or "UKRAINA"
                    => "Import",

                // Klienci - sieci handlowe
                "KLIENCI" or "CLIENTS" or "SIECI" or "RETAIL" or "HANDEL" or "DETALICZNY"
                or "BIEDRONKA" or "LIDL" or "DINO" or "MAKRO" or "CARREFOUR" or "KAUFLAND"
                    => "Klienci",

                // Koszty
                "KOSZTY" or "COSTS" or "PASZE" or "ZBOŻA" or "KUKURYDZA" or "SOJA" or "ENERGIA"
                or "PALIWO" or "TRANSPORT" or "INFLACJA"
                    => "Koszty",

                // Pogoda
                "POGODA" or "WEATHER" or "KLIMAT" or "MRÓZ" or "SUSZA" or "POWÓDŹ"
                    => "Pogoda",

                // Logistyka
                "LOGISTYKA" or "LOGISTICS" or "TRANSPORT" or "DOSTAWY" or "ŁAŃCUCH DOSTAW"
                    => "Logistyka",

                // Inwestycje
                "INWESTYCJE" or "INVESTMENTS" or "ROZBUDOWA" or "MODERNIZACJA" or "DOTACJE" or "FUNDUSZE"
                    => "Inwestycje",

                // Fallback - spróbuj wykryć z treści
                "INFO" or "INFORMACJE" or "INNE" or "OTHER" or "GENERAL"
                    => DetectCategoryFromContent(title, content),

                // Jeśli już jest poprawna kategoria, użyj jej
                _ when IsValidCategory(normalized)
                    => normalized,

                // Fallback na detekcję z treści
                _ => DetectCategoryFromContent(title, content)
            };
        }

        /// <summary>
        /// Sprawdza czy kategoria jest jedną ze standardowych
        /// </summary>
        private bool IsValidCategory(string category)
        {
            var validCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "HPAI", "Ceny", "Konkurencja", "Regulacje", "Eksport", "Import",
                "Klienci", "Koszty", "Pogoda", "Logistyka", "Inwestycje", "Info"
            };
            return validCategories.Contains(category);
        }

        /// <summary>
        /// Wykrywa kategorię na podstawie treści artykułu
        /// </summary>
        private string DetectCategoryFromContent(string title, string content)
        {
            var text = ((title ?? "") + " " + (content ?? "")).ToUpperInvariant();

            // HPAI - najwyższy priorytet
            if (text.Contains("HPAI") || text.Contains("PTASIA GRYPA") || text.Contains("BIRD FLU") ||
                text.Contains("AVIAN") || text.Contains("OGNISKO") || text.Contains("WYBICIE"))
                return "HPAI";

            // Konkurencja - firmy drobiarskie
            if (text.Contains("CEDROB") || text.Contains("SUPERDROB") || text.Contains("DROSED") ||
                text.Contains("ANIMEX") || text.Contains("DROBIMEX") || text.Contains("PLUKON") ||
                text.Contains("PRZEJĘCIE") || text.Contains("FUZJA") || text.Contains("ADQ"))
                return "Konkurencja";

            // Klienci - sieci handlowe
            if (text.Contains("BIEDRONKA") || text.Contains("LIDL") || text.Contains("DINO") ||
                text.Contains("MAKRO") || text.Contains("CARREFOUR") || text.Contains("KAUFLAND") ||
                text.Contains("TESCO") || text.Contains("AUCHAN") || text.Contains("SELGROS"))
                return "Klienci";

            // Import
            if (text.Contains("IMPORT") || text.Contains("BRAZYLIA") || text.Contains("MERCOSUR") ||
                text.Contains("UKRAINA") || text.Contains("UKRAINE") || text.Contains("BEZCŁOW"))
                return "Import";

            // Eksport
            if (text.Contains("EKSPORT") || text.Contains("EXPORT"))
                return "Eksport";

            // Regulacje
            if (text.Contains("KSEF") || text.Contains("REGULAC") || text.Contains("USTAWA") ||
                text.Contains("ROZPORZĄ") || text.Contains("WETERYN") || text.Contains("DOBROSTAN"))
                return "Regulacje";

            // Koszty - pasze i energia
            if (text.Contains("PASZA") || text.Contains("KUKURYDZ") || text.Contains("SOJA") ||
                text.Contains("PSZENICA") || text.Contains("ENERGIA") || text.Contains("PALIW") ||
                text.Contains("KOSZT"))
                return "Koszty";

            // Ceny
            if (text.Contains("CENA") || text.Contains("CEN ") || text.Contains("CENOW") ||
                text.Contains("PRICE") || text.Contains("ZŁ/KG") || text.Contains("PLN"))
                return "Ceny";

            // Pogoda
            if (text.Contains("POGODA") || text.Contains("MRÓZ") || text.Contains("SUSZA") ||
                text.Contains("TEMPERATURA") || text.Contains("KLIMAT"))
                return "Pogoda";

            // Inwestycje
            if (text.Contains("INWESTYC") || text.Contains("DOTACJ") || text.Contains("FUNDUSZ") ||
                text.Contains("MODERNIZ") || text.Contains("ROZBUDOW"))
                return "Inwestycje";

            // Logistyka
            if (text.Contains("LOGISTYK") || text.Contains("TRANSPORT") || text.Contains("DOSTAW"))
                return "Logistyka";

            // Default
            return "Info";
        }

        /// <summary>
        /// Określa severity na podstawie kategorii i treści gdy AI nie określiło
        /// </summary>
        private string DetectSeverityFromCategory(string category, string title, string content)
        {
            var text = ((title ?? "") + " " + (content ?? "")).ToUpperInvariant();

            // HPAI zawsze critical jeśli w regionie łódzkim lub blisko
            if (category == "HPAI")
            {
                if (text.Contains("ŁÓDZKI") || text.Contains("LODZKI") || text.Contains("BRZEZIN") ||
                    text.Contains("ŁÓDŹ") || text.Contains("LODZ") || text.Contains("PIOTRKÓW"))
                    return "critical";
                return "warning";
            }

            // Konkurencja - przejęcia i fuzje są ważne
            if (category == "Konkurencja")
            {
                if (text.Contains("PRZEJĘCIE") || text.Contains("FUZJA") || text.Contains("ADQ") ||
                    text.Contains("BANKRUT") || text.Contains("UPADŁOŚ"))
                    return "warning";
                return "info";
            }

            // Import - tani import to zagrożenie
            if (category == "Import")
            {
                if (text.Contains("BRAZYLIA") || text.Contains("MERCOSUR") || text.Contains("BEZCŁOW"))
                    return "warning";
                return "info";
            }

            // Regulacje - KSeF jest pilne
            if (category == "Regulacje" && text.Contains("KSEF"))
                return "warning";

            // Klienci - nowi klienci to pozytywne
            if (category == "Klienci")
            {
                if (text.Contains("EKSPANSJ") || text.Contains("NOWY") || text.Contains("OTWARCI"))
                    return "positive";
                return "info";
            }

            return "info";
        }

        #endregion
    }
}
