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
    /// Glowny orchestrator pipeline'u pobierania i analizy wiadomosci
    /// </summary>
    public class NewsFetchOrchestrator
    {
        private readonly ClaudeAnalysisService _claudeService;
        private readonly PerplexitySearchService _perplexityService;
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
            _perplexityService = new PerplexitySearchService();
            _enrichmentService = new ContentEnrichmentService();
            _filterService = new ContentFilterService();
            _dbService = new MarketIntelligenceService(connectionString);

            // Inicjalizuj diagnostyke
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

            Diagnostics.IsPerplexityConfigured = _perplexityService.IsConfigured;
            Diagnostics.PerplexityApiKeyPreview = _perplexityService.ApiKeyPreview;
        }

        /// <summary>
        /// Testuje polaczenie z API
        /// </summary>
        public async Task<(bool ClaudeOk, string ClaudeMsg, bool PerplexityOk, string PerplexityMsg)> TestConnectionsAsync(CancellationToken ct = default)
        {
            var claudeTask = _claudeService.TestConnectionAsync(ct);
            var perplexityTask = _perplexityService.TestConnectionAsync(ct);

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
                var queries = quickMode ? _perplexityService.GetQuickQueries() : _perplexityService.GetAllQueries();
                Diagnostics.PerplexityQueriesTotal = queries.Count;

                var perplexityProgress = new Progress<(int completed, int total, string query)>(p =>
                {
                    Diagnostics.PerplexityQueriesCompleted = p.completed;
                    var pct = (double)p.completed / p.total * 30; // 0-30%
                    progress?.Report(("Perplexity", pct, $"Zapytanie {p.completed}/{p.total}: {p.query}"));
                });

                var perplexityArticles = await _perplexityService.FetchAllNewsAsync(perplexityProgress, quickMode, ct);
                perplexityStopwatch.Stop();

                Diagnostics.PerplexityArticlesCount = perplexityArticles.Count;
                Diagnostics.PerplexityTime = perplexityStopwatch.Elapsed;
                Diagnostics.PerplexityCostEstimate = _perplexityService.EstimateTotalCost(queries.Count);
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
                        fullContent = enriched.Content;
                    }

                    // Jesli tresc za krotka, dodaj instrukcje dla Claude
                    if (string.IsNullOrEmpty(fullContent) || fullContent.Length < 200)
                    {
                        fullContent = $"{article.Title}\n\n{article.Content}\n\n[Artykul krotki - uzupelnij analiza wlasna wiedza o temacie]";
                    }

                    // Analiza AI
                    var analysisResult = await _claudeService.AnalyzeArticleAsync(
                        article.Title,
                        fullContent,
                        "Perplexity / " + GetDomain(article.Url),
                        BusinessContext,
                        ct);

                    // Konwertuj na BriefingArticle
                    var briefingArticle = new BriefingArticle
                    {
                        Id = analyzed + 1,
                        Title = article.Title,
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
        /// </summary>
        public async Task<BriefingArticle> TestSingleArticlePipelineAsync(CancellationToken ct = default)
        {
            Diagnostics.Reset();
            Diagnostics.IsRunning = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // ═══════════════════════════════════════════════════════════
                // ETAP 1: Pojedyncze zapytanie Perplexity
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 1: PERPLEXITY ===");
                Diagnostics.AddLog("Wysylam testowe zapytanie: 'ceny drobiu Polska luty 2026'");

                var perplexityStopwatch = Stopwatch.StartNew();
                var articles = await _perplexityService.SearchAsync("ceny drobiu Polska luty 2026", ct);
                perplexityStopwatch.Stop();

                Diagnostics.PerplexityTime = perplexityStopwatch.Elapsed;
                Diagnostics.PerplexityArticlesCount = articles.Count;
                Diagnostics.AddLog($"Perplexity odpowiedzial w {perplexityStopwatch.ElapsedMilliseconds}ms");
                Diagnostics.AddLog($"Znaleziono {articles.Count} artykulow");

                if (articles.Count == 0)
                {
                    Diagnostics.AddError("Perplexity nie zwrocil zadnych artykulow!");
                    return null;
                }

                // Pokaz wszystkie znalezione artykuly
                for (int i = 0; i < Math.Min(articles.Count, 5); i++)
                {
                    var a = articles[i];
                    Diagnostics.AddLog($"  [{i + 1}] {TruncateTitle(a.Title, 60)}");
                    Diagnostics.AddLog($"      URL: {a.Url ?? "brak"}");
                    Diagnostics.AddLog($"      Zrodlo: {a.Source}");
                }

                // Wybierz pierwszy artykul z URL
                var testArticle = articles.FirstOrDefault(a => !string.IsNullOrEmpty(a.Url) && a.Url.StartsWith("http"));
                if (testArticle == null)
                {
                    testArticle = articles.First();
                    Diagnostics.AddWarning("Brak artykulu z URL - uzywam pierwszego");
                }

                Diagnostics.AddSuccess($"Wybralem artykul: {TruncateTitle(testArticle.Title, 80)}");

                // ═══════════════════════════════════════════════════════════
                // ETAP 2: Filtrowanie lokalne
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 2: FILTROWANIE LOKALNE ===");

                var filterInput = new List<(string title, string content, string url)>
                {
                    (testArticle.Title, testArticle.Snippet, testArticle.Url)
                };

                var filtered = _filterService.FilterArticles(filterInput);
                Diagnostics.FilteredCount = filtered.Count;

                if (filtered.Count == 0)
                {
                    Diagnostics.AddWarning("Artykul zostal odfiltrowany przez blacklist!");
                    Diagnostics.AddLog("Pomijam filtr i kontynuuje...");
                }
                else
                {
                    Diagnostics.AddSuccess("Artykul przeszedl filtrowanie lokalne");
                }

                // ═══════════════════════════════════════════════════════════
                // ETAP 3: Wzbogacanie tresci (Content Enrichment)
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 3: WZBOGACANIE TRESCI ===");

                string fullContent = testArticle.Snippet;

                if (!string.IsNullOrEmpty(testArticle.Url) && testArticle.Url.StartsWith("http"))
                {
                    Diagnostics.AddLog($"Pobieram pelna tresc z: {testArticle.Url}");

                    var enrichResult = await _enrichmentService.EnrichSingleAsync(testArticle.Url, ct);

                    if (enrichResult.Success)
                    {
                        fullContent = enrichResult.Content;
                        Diagnostics.EnrichedCount = 1;
                        Diagnostics.AddSuccess($"Pobrano tresc: {fullContent.Length} znakow");
                        Diagnostics.AddLog($"Pierwsze 300 znakow: {TruncateContent(fullContent, 300)}");
                    }
                    else
                    {
                        Diagnostics.EnrichmentFailedCount = 1;
                        Diagnostics.AddWarning($"Nie udalo sie pobrac tresci: {enrichResult.Error}");
                        Diagnostics.AddLog("Uzywam snippetu z Perplexity jako tresc");
                    }
                }
                else
                {
                    Diagnostics.AddLog("Brak URL - uzywam snippetu z Perplexity");
                }

                // ═══════════════════════════════════════════════════════════
                // ETAP 4: Analiza AI (Claude Sonnet)
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 4: ANALIZA CLAUDE SONNET ===");
                Diagnostics.AddLog($"Model: {ClaudeAnalysisService.SonnetModel}");
                Diagnostics.AddLog($"Wysylam do analizy {fullContent.Length} znakow tresci");

                if (!_claudeService.IsConfigured)
                {
                    Diagnostics.AddError("Claude API nie skonfigurowane!");
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

                Diagnostics.AddLog($"Claude odpowiedzial w {analysisStopwatch.ElapsedMilliseconds}ms");

                if (!string.IsNullOrEmpty(analysisResult.Summary) && !analysisResult.Summary.StartsWith("Blad"))
                {
                    Diagnostics.AnalyzedCount = 1;
                    Diagnostics.AddSuccess("Analiza zakonczona sukcesem!");

                    // Pokaz wyniki analizy
                    Diagnostics.AddLog("--- PODSUMOWANIE ---");
                    Diagnostics.AddLog(TruncateContent(analysisResult.Summary, 200));

                    Diagnostics.AddLog("--- KIM JEST ---");
                    Diagnostics.AddLog(TruncateContent(analysisResult.WhoIs, 150));

                    Diagnostics.AddLog("--- ANALIZA CEO ---");
                    Diagnostics.AddLog(TruncateContent(analysisResult.AnalysisCeo, 150));

                    Diagnostics.AddLog("--- AKCJE CEO ---");
                    foreach (var action in analysisResult.ActionsCeo.Take(3))
                    {
                        Diagnostics.AddLog($"  • {action}");
                    }

                    Diagnostics.AddLog($"--- KATEGORIA: {analysisResult.Category} ---");
                    Diagnostics.AddLog($"--- SEVERITY: {analysisResult.Severity} ---");
                }
                else
                {
                    Diagnostics.AddError($"Blad analizy: {analysisResult.Summary}");
                    return null;
                }

                // ═══════════════════════════════════════════════════════════
                // ETAP 5: Tworzenie BriefingArticle
                // ═══════════════════════════════════════════════════════════
                Diagnostics.AddLog("=== ETAP 5: TWORZENIE ARTYKULU ===");

                var briefingArticle = new BriefingArticle
                {
                    Id = 1,
                    Title = testArticle.Title,
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
                    Source = GetDomain(testArticle.Url) ?? "Perplexity",
                    SourceUrl = testArticle.Url,
                    PublishDate = DateTime.Today,
                    Severity = ParseSeverity(analysisResult.Severity ?? "Info"),
                    Tags = analysisResult.Tags ?? new List<string>(),
                    IsFeatured = true
                };

                stopwatch.Stop();
                Diagnostics.AddSuccess($"=== PIPELINE UKONCZONY w {stopwatch.Elapsed.TotalSeconds:N1}s ===");

                return briefingArticle;
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

        #endregion
    }
}
