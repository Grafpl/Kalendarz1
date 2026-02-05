using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services.AI;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Główny orchestrator pipeline'u pobierania i analizy wiadomości.
    /// Używa Brave Search API jako źródła danych.
    /// </summary>
    public class NewsFetchOrchestrator
    {
        private readonly ClaudeAnalysisService _claudeService;
        private readonly BraveSearchService _newsService;
        private readonly ContentEnrichmentService _enrichmentService;
        private readonly ContentFilterService _filterService;
        private readonly MarketIntelligenceService _dbService;

        public DiagnosticInfo Diagnostics { get; } = new DiagnosticInfo();

        // Kontekst biznesowy Piorkowscy
        private const string BusinessContext = @"
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

        public NewsFetchOrchestrator(string connectionString = null)
        {
            _claudeService = new ClaudeAnalysisService();
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
            Diagnostics.IsClaudeConfigured = _claudeService.IsConfigured;
            Diagnostics.ClaudeApiKeyPreview = _claudeService.ApiKeyPreview;
            Diagnostics.ClaudeModel = ClaudeAnalysisService.SonnetModel;

            // Brave Search (zamiast Bing/Perplexity)
            Diagnostics.IsPerplexityConfigured = _newsService.IsConfigured;
            Diagnostics.PerplexityApiKeyPreview = _newsService.ApiKeyPreview;
        }

        /// <summary>
        /// Testuje połączenie z API (Claude + Brave Search)
        /// </summary>
        public async Task<(bool ClaudeOk, string ClaudeMsg, bool PerplexityOk, string PerplexityMsg)> TestConnectionsAsync(CancellationToken ct = default)
        {
            var claudeTask = _claudeService.TestConnectionAsync(ct);
            var perplexityTask = _newsService.TestConnectionAsync(ct); // Brave Search

            await Task.WhenAll(claudeTask, perplexityTask);

            var claudeResult = await claudeTask;
            var perplexityResult = await perplexityTask;

            Diagnostics.IsClaudeConfigured = claudeResult.Success;
            Diagnostics.IsPerplexityConfigured = perplexityResult.Success;

            return (claudeResult.Success, claudeResult.Message, perplexityResult.Success, perplexityResult.Message);
        }

        /// <summary>
        /// Glowna metoda - pobiera i analizuje wszystkie wiadomosci
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
                // ETAP 1: Pobieranie z Perplexity
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Perplexity", 0, "Rozpoczynam wyszukiwanie..."));
                Diagnostics.CurrentStage = "Pobieranie z Perplexity";
                Diagnostics.AddLog("Rozpoczynam pobieranie z Perplexity");

                var perplexityStopwatch = Stopwatch.StartNew();
                var queries = quickMode ? _newsService.GetQuickQueries() : _newsService.GetAllQueries();
                Diagnostics.PerplexityQueriesTotal = queries.Count;

                var perplexityProgress = new Progress<(int completed, int total, string query)>(p =>
                {
                    Diagnostics.PerplexityQueriesCompleted = p.completed;
                    var pct = (double)p.completed / p.total * 30; // 0-30%
                    progress?.Report(("Perplexity", pct, $"Zapytanie {p.completed}/{p.total}: {p.query}"));
                });

                var perplexityArticles = await _newsService.FetchAllNewsAsync(perplexityProgress, quickMode, ct);
                perplexityStopwatch.Stop();

                Diagnostics.PerplexityArticlesCount = perplexityArticles.Count;
                Diagnostics.PerplexityTime = perplexityStopwatch.Elapsed;
                Diagnostics.PerplexityCostEstimate = _newsService.EstimateCost(queries.Count);
                Diagnostics.AddSuccess($"Perplexity: {perplexityArticles.Count} artykulow z {queries.Count} zapytan");

                if (ct.IsCancellationRequested) return allArticles;

                // ═══════════════════════════════════════════════════════════
                // ETAP 2: Filtrowanie lokalne (blacklist/whitelist)
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Filtrowanie", 35, "Filtrowanie artykulow..."));
                Diagnostics.CurrentStage = "Filtrowanie lokalne";
                Diagnostics.AddLog("Filtrowanie lokalne (blacklist/whitelist)");

                var articlesToFilter = perplexityArticles
                    .Select(a => (a.Title, a.Snippet, a.Url))
                    .ToList();

                var filteredArticles = _filterService.FilterArticles(articlesToFilter);
                Diagnostics.FilteredCount = filteredArticles.Count;
                Diagnostics.AddLog($"Po filtrowaniu: {filteredArticles.Count}/{perplexityArticles.Count} artykulow");

                if (ct.IsCancellationRequested) return allArticles;

                // ═══════════════════════════════════════════════════════════
                // ETAP 3: Filtrowanie AI (Haiku)
                // ═══════════════════════════════════════════════════════════
                if (_claudeService.IsConfigured && filteredArticles.Count > 0)
                {
                    progress?.Report(("Filtrowanie AI", 40, "Filtrowanie przez Claude Haiku..."));
                    Diagnostics.CurrentStage = "Filtrowanie AI (Haiku)";
                    Diagnostics.AddLog("Filtrowanie przez Claude Haiku");

                    var articlesForAiFilter = filteredArticles
                        .Take(50) // Max 50 do filtrowania AI
                        .Select(a => (a.Title, a.Content))
                        .ToList();

                    var aiFilterResults = await _claudeService.QuickFilterArticlesAsync(articlesForAiFilter, ct);
                    var relevantTitles = aiFilterResults.Where(r => r.IsRelevant).Select(r => r.Title).ToHashSet();

                    filteredArticles = filteredArticles.Where(a => relevantTitles.Contains(a.Title)).ToList();
                    Diagnostics.AiFilteredCount = filteredArticles.Count;
                    Diagnostics.ClaudeHaikuCostEstimate = _claudeService.EstimateCost(5000, 1000, false);
                    Diagnostics.AddLog($"Po filtrowaniu AI: {filteredArticles.Count} artykulow");
                }

                if (ct.IsCancellationRequested) return allArticles;

                // ═══════════════════════════════════════════════════════════
                // ETAP 4: Wzbogacanie tresci (Content Enrichment)
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Wzbogacanie", 50, "Pobieranie pelnej tresci artykulow..."));
                Diagnostics.CurrentStage = "Wzbogacanie tresci";
                Diagnostics.AddLog("Pobieranie pelnej tresci artykulow");

                var urlsToEnrich = filteredArticles
                    .Where(a => !string.IsNullOrEmpty(a.Url) && a.Url.StartsWith("http"))
                    .Select(a => a.Url)
                    .Distinct()
                    .Take(30) // Max 30 URL do enrichmentu
                    .ToList();

                var enrichmentProgress = new Progress<(int completed, int total, string url)>(p =>
                {
                    var pct = 50 + (double)p.completed / p.total * 15; // 50-65%
                    progress?.Report(("Wzbogacanie", pct, $"Pobieranie {p.completed}/{p.total}"));
                });

                var enrichmentResults = await _enrichmentService.EnrichArticlesAsync(urlsToEnrich, enrichmentProgress, ct);
                var enrichmentMap = enrichmentResults.Where(r => r.Success).ToDictionary(r => r.Url, r => r);

                Diagnostics.EnrichedCount = enrichmentResults.Count(r => r.Success);
                Diagnostics.EnrichmentFailedCount = enrichmentResults.Count(r => !r.Success);

                foreach (var failed in enrichmentResults.Where(r => !r.Success))
                {
                    Diagnostics.AddWarning($"Enrichment failed: {GetDomain(failed.Url)} - {failed.Error}");
                }

                Diagnostics.AddLog($"Wzbogacono {Diagnostics.EnrichedCount}/{urlsToEnrich.Count} artykulow");

                if (ct.IsCancellationRequested) return allArticles;

                // ═══════════════════════════════════════════════════════════
                // ETAP 5: Analiza AI (Claude Sonnet)
                // ═══════════════════════════════════════════════════════════
                progress?.Report(("Analiza AI", 65, "Analiza przez Claude Sonnet..."));
                Diagnostics.CurrentStage = "Analiza AI (Sonnet)";
                Diagnostics.AddLog("Analiza artykulow przez Claude Sonnet");

                int analyzed = 0;
                int maxToAnalyze = quickMode ? 10 : 25;
                var articlesToAnalyze = filteredArticles.Take(maxToAnalyze).ToList();

                foreach (var article in articlesToAnalyze)
                {
                    if (ct.IsCancellationRequested) break;

                    var pct = 65 + (double)analyzed / articlesToAnalyze.Count * 30; // 65-95%
                    progress?.Report(("Analiza AI", pct, $"Analizuje {analyzed + 1}/{articlesToAnalyze.Count}: {TruncateTitle(article.Title)}"));

                    // Pobierz pelna tresc jesli dostepna
                    string fullContent = article.Content;
                    if (enrichmentMap.TryGetValue(article.Url, out var enriched))
                    {
                        // Sprawdz czy enrichment zwrocil dobra tresc
                        if (enriched.IsLowQuality || string.IsNullOrEmpty(enriched.Content))
                        {
                            Diagnostics.AddLog($"Pomijam (niska jakosc): {TruncateTitle(article.Title, 50)}");
                            analyzed++;
                            continue;
                        }
                        fullContent = enriched.Content;
                    }

                    // Jesli tresc za krotka (min 500 znakow) - pomijamy zamiast wysylac do Claude
                    if (string.IsNullOrEmpty(fullContent) || fullContent.Length < 500)
                    {
                        Diagnostics.AddLog($"Pomijam (tresc <500 znakow): {TruncateTitle(article.Title, 50)}");
                        analyzed++;
                        continue;
                    }

                    // Analiza AI
                    var analysisResult = await _claudeService.AnalyzeArticleAsync(
                        article.Title,
                        fullContent,
                        "Perplexity / " + GetDomain(article.Url),
                        BusinessContext,
                        ct);

                    // Sprawdz czy analiza sie powiodla
                    var hasParsingError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                        (analysisResult.Summary.StartsWith("Blad") ||
                         analysisResult.Summary.StartsWith("BLAD") ||
                         analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                    if (hasParsingError)
                    {
                        Diagnostics.AddWarning($"Blad parsowania dla: {TruncateTitle(article.Title, 50)}");
                        // Dodaj szczegoly bledu do logow
                        if (!string.IsNullOrEmpty(_claudeService.LastRawResponse))
                        {
                            var rawPreview = _claudeService.LastRawResponse.Length > 500
                                ? _claudeService.LastRawResponse.Substring(0, 500) + "..."
                                : _claudeService.LastRawResponse;
                            Diagnostics.AddLog($"RAW Response (500 znakow): {rawPreview}");
                        }
                        analyzed++;
                        continue; // Pomin artykul z bledem parsowania
                    }

                    // Konwertuj na BriefingArticle
                    var briefingArticle = new BriefingArticle
                    {
                        Id = analyzed + 1,
                        Title = article.Title,
                        SmartTitle = analysisResult.SmartTitle,
                        SentimentScore = analysisResult.SentimentScore,
                        Impact = ParseImpactLevel(analysisResult.Impact),
                        ShortPreview = TruncateContent(article.Content, 150),
                        FullContent = analysisResult.Summary,
                        EducationalSection = analysisResult.WhoIs,
                        AiAnalysisCeo = analysisResult.AnalysisCeo,
                        AiAnalysisSales = analysisResult.AnalysisSales,
                        AiAnalysisBuyer = analysisResult.AnalysisBuyer,
                        RecommendedActionsCeo = string.Join("\n", analysisResult.ActionsCeo),
                        RecommendedActionsSales = string.Join("\n", analysisResult.ActionsSales),
                        RecommendedActionsBuyer = string.Join("\n", analysisResult.ActionsBuyer),
                        Category = analysisResult.Category ?? _filterService.CategorizeArticle(article.Title, fullContent),
                        Source = GetDomain(article.Url) ?? "Perplexity",
                        SourceUrl = article.Url,
                        PublishDate = DateTime.Today,
                        Severity = ParseSeverity(analysisResult.Severity ?? _filterService.DetermineSeverity(article.Title, fullContent, article.Score)),
                        Tags = analysisResult.Tags ?? new List<string>()
                    };

                    allArticles.Add(briefingArticle);
                    analyzed++;
                }

                Diagnostics.AnalyzedCount = analyzed;
                Diagnostics.ClaudeSonnetCostEstimate = _claudeService.EstimateCost(analyzed * 3000, analyzed * 2000, true);
                Diagnostics.AddSuccess($"Przeanalizowano {analyzed} artykulow");

                // ═══════════════════════════════════════════════════════════
                // ETAP 6: Finalizacja
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
                Diagnostics.AddSuccess($"Zakonczono w {stopwatch.Elapsed.TotalSeconds:N1}s. Koszt: ~${Diagnostics.TotalCostEstimate:N2}");

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

                    // KROK 2: Filtrowanie lokalne
                    var filterInput = new List<(string title, string content, string url)>
                    {
                        (testArticle.Title, testArticle.Snippet, testArticle.Url)
                    };
                    var filtered = _filterService.FilterArticles(filterInput);
                    if (filtered.Count == 0)
                    {
                        Diagnostics.AddLog($"  [SKIP] Odfiltrowany przez blacklist");
                        continue;
                    }

                    // KROK 3: Wzbogacanie tresci
                    string fullContent = testArticle.Snippet ?? "";

                    var enrichResult = await _enrichmentService.EnrichSingleAsync(testArticle.Url, ct);
                    if (enrichResult.Success)
                    {
                        fullContent = enrichResult.Content;
                        Diagnostics.AddLog($"  [OK] Pobrano tresc: {fullContent.Length} znakow");
                    }
                    else
                    {
                        Diagnostics.AddLog($"  [SKIP] Enrichment failed: {enrichResult.Error}");
                        Diagnostics.EnrichmentFailedCount++;
                        continue; // RETRY - idz do nastepnego
                    }

                    // Sprawdz dlugosc tresci
                    if (fullContent.Length < 500)
                    {
                        Diagnostics.AddLog($"  [SKIP] Tresc za krotka ({fullContent.Length} < 500 znakow)");
                        continue; // RETRY - idz do nastepnego
                    }

                    Diagnostics.EnrichedCount++;

                    // KROK 4: Analiza AI
                    if (!_claudeService.IsConfigured)
                    {
                        Diagnostics.AddError("Claude API nie skonfigurowane!");
                        return null;
                    }

                    Diagnostics.AddLog($"  Wysylam do Claude ({fullContent.Length} znakow)...");

                    var analysisStopwatch = Stopwatch.StartNew();
                    var analysisResult = await _claudeService.AnalyzeArticleAsync(
                        testArticle.Title,
                        fullContent,
                        "Brave / " + GetDomain(testArticle.Url),
                        BusinessContext,
                        ct);
                    analysisStopwatch.Stop();

                    Diagnostics.AddLog($"  Claude odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms");

                    // Sprawdz czy analiza sie powiodla
                    var summaryHasError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                        (analysisResult.Summary.StartsWith("Blad") ||
                         analysisResult.Summary.StartsWith("BLAD") ||
                         analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                    if (summaryHasError)
                    {
                        Diagnostics.AddLog($"  [SKIP] Blad parsowania odpowiedzi Claude");
                        if (!string.IsNullOrEmpty(_claudeService.LastRawResponse))
                        {
                            Diagnostics.AddLog($"  RAW (100 znakow): {TruncateContent(_claudeService.LastRawResponse, 100)}");
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
                        Category = analysisResult.Category ?? "Info",
                        Source = GetDomain(testArticle.Url) ?? "Brave",
                        SourceUrl = testArticle.Url,
                        PublishDate = DateTime.Today,
                        Severity = ParseSeverity(analysisResult.Severity ?? "Info"),
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

                    var filterInput = new List<(string title, string content, string url)>
                    {
                        (testArticle.Title, testArticle.Snippet, testArticle.Url)
                    };

                    var filtered = _filterService.FilterArticles(filterInput);

                    if (filtered.Count == 0)
                    {
                        log("  [SKIP] Odfiltrowany przez blacklist - idz do nastepnego", "WARNING");
                        Diagnostics.AddLog($"  [SKIP] Blacklist");
                        continue; // RETRY
                    }
                    log("  [OK] Przeszedl filtr", "SUCCESS");

                    // ─────────────────────────────────────────────────────
                    // KROK 3: Wzbogacanie tresci
                    // ─────────────────────────────────────────────────────
                    log("Krok 3: Pobieram pelna tresc (SmartReader)...", "INFO");
                    log($"  URL: {testArticle.Url}", "DEBUG");

                    var enrichResult = await _enrichmentService.EnrichSingleAsync(testArticle.Url, ct);

                    if (!enrichResult.Success)
                    {
                        log($"  [SKIP] Enrichment failed: {enrichResult.Error}", "WARNING");
                        log("  Idz do nastepnego artykulu...", "INFO");
                        Diagnostics.EnrichmentFailedCount++;
                        Diagnostics.AddLog($"  [SKIP] Enrichment: {TruncateContent(enrichResult.Error, 50)}");
                        continue; // RETRY
                    }

                    string fullContent = enrichResult.Content;
                    log($"  [OK] Pobrano tresc: {fullContent.Length} znakow", "SUCCESS");

                    // Pokaz poczatek tresci
                    var preview = fullContent.Length > 500 ? fullContent.Substring(0, 500) + "..." : fullContent;
                    logRaw(preview, "POBRANA TRESC (pierwsze 500 znakow)");

                    // Sprawdz dlugosc tresci
                    if (fullContent.Length < 500)
                    {
                        log($"  [SKIP] Tresc za krotka ({fullContent.Length} < 500 znakow)", "WARNING");
                        log("  Idz do nastepnego artykulu...", "INFO");
                        Diagnostics.AddLog($"  [SKIP] Za krotka ({fullContent.Length} znakow)");
                        continue; // RETRY
                    }

                    Diagnostics.EnrichedCount++;
                    Diagnostics.FilteredCount++;

                    // ─────────────────────────────────────────────────────
                    // KROK 4: Analiza AI
                    // ─────────────────────────────────────────────────────
                    log("Krok 4: Wysylam do Claude...", "INFO");
                    log($"  Model: {ClaudeAnalysisService.SonnetModel}", "INFO");
                    log($"  Dlugosc tresci: {fullContent.Length} znakow", "DEBUG");

                    if (!_claudeService.IsConfigured)
                    {
                        log("Claude API nie skonfigurowane!", "ERROR");
                        log("Sprawdz klucz API w App.config lub zmiennej srodowiskowej ANTHROPIC_API_KEY", "ERROR");
                        Diagnostics.AddError("Claude API nie skonfigurowane");
                        return null;
                    }

                    var analysisStopwatch = Stopwatch.StartNew();
                    var analysisResult = await _claudeService.AnalyzeArticleAsync(
                        testArticle.Title,
                        fullContent,
                        "Perplexity / " + GetDomain(testArticle.Url),
                        BusinessContext,
                        ct);
                    analysisStopwatch.Stop();

                    log($"  Claude odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms", "SUCCESS");
                    Diagnostics.AddLog($"  Claude: {analysisStopwatch.ElapsedMilliseconds}ms");

                    // Sprawdz czy analiza sie powiodla
                    var summaryContainsError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                        (analysisResult.Summary.StartsWith("Blad") ||
                         analysisResult.Summary.StartsWith("BLAD") ||
                         analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                    if (summaryContainsError || string.IsNullOrEmpty(analysisResult.Summary))
                    {
                        log($"  [SKIP] Blad parsowania odpowiedzi Claude", "WARNING");

                        // Szczegolowe logi bledu
                        if (!string.IsNullOrEmpty(_claudeService.LastParsingError))
                        {
                            log($"  Szczegoly: {TruncateContent(_claudeService.LastParsingError, 200)}", "DEBUG");
                        }
                        if (!string.IsNullOrEmpty(_claudeService.LastRawResponse))
                        {
                            log($"  RAW (100 znakow): {TruncateContent(_claudeService.LastRawResponse, 100)}", "DEBUG");
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
                        Category = analysisResult.Category ?? "Info",
                        Source = GetDomain(testArticle.Url) ?? "Perplexity",
                        SourceUrl = testArticle.Url,
                        PublishDate = DateTime.Today,
                        Severity = ParseSeverity(analysisResult.Severity ?? "Info"),
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
                // ETAP 2: Analiza kazdego artykulu przez Claude
                // ═══════════════════════════════════════════════════════════
                logSection("ETAP 2: ANALIZA CLAUDE AI");
                log($"Bede analizowal {demoArticles.Count} artykulow przez Claude...", "INFO");
                log($"Model: {ClaudeAnalysisService.SonnetModel}", "INFO");
                Diagnostics.AddLog("=== ETAP 2: CLAUDE ANALYSIS ===");

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

                    log($"Wysylam do Claude ({fullContent.Length} znakow)...", "INFO");

                    try
                    {
                        var analysisStopwatch = Stopwatch.StartNew();
                        var analysisResult = await _claudeService.AnalyzeArticleAsync(
                            article.Title,
                            fullContent,
                            article.Source,
                            BusinessContext,
                            ct);
                        analysisStopwatch.Stop();

                        if (analysisResult == null)
                        {
                            log($"Claude zwrocil null - blad analizy", "ERROR");
                            errors++;
                            continue;
                        }

                        log($"Claude odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms", "SUCCESS");

                        // Sprawdz czy analiza sie powiodla
                        var hasParsingError = !string.IsNullOrEmpty(analysisResult.Summary) &&
                            (analysisResult.Summary.StartsWith("Blad") ||
                             analysisResult.Summary.StartsWith("BLAD") ||
                             analysisResult.Summary.Contains("BLAD PARSOWANIA"));

                        if (hasParsingError)
                        {
                            log($"BLAD PARSOWANIA JSON dla artykulu {analyzed}!", "ERROR");
                            log(TruncateContent(analysisResult.Summary, 500), "ERROR");

                            // Pokaz surowa odpowiedz Claude
                            if (!string.IsNullOrEmpty(_claudeService.LastRawResponse))
                            {
                                log("", "ERROR");
                                log("=== SUROWA ODPOWIEDZ CLAUDE ===", "ERROR");
                                logRaw(_claudeService.LastRawResponse, "CLAUDE RAW");
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
                            Category = analysisResult.Category ?? "Info",
                            Source = article.Source ?? "Demo",
                            SourceUrl = article.Url,
                            PublishDate = DateTime.Today,
                            Severity = ParseSeverity(analysisResult.Severity ?? "Info"),
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

        #endregion
    }
}
