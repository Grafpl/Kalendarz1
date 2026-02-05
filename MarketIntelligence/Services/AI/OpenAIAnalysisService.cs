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
using Kalendarz1.MarketIntelligence.Config;

namespace Kalendarz1.MarketIntelligence.Services.AI
{
    /// <summary>
    /// Serwis do analizy artykulow przez OpenAI API (GPT-4o)
    /// Zastepuje ClaudeAnalysisService - ta sama funkcjonalnosc, inne API
    /// </summary>
    public class OpenAIAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        // Model OpenAI - GPT-4o (najnowszy, najszybszy)
        public const string DefaultModel = "gpt-4o";
        public const string MiniModel = "gpt-4o-mini"; // Tani model do filtrowania

        private const string ApiUrl = "https://api.openai.com/v1/chat/completions";

        // Koszty za 1M tokenow (szacunkowe, USD) - GPT-4o
        private const decimal Gpt4oInputCost = 2.50m;
        private const decimal Gpt4oOutputCost = 10.00m;
        private const decimal Gpt4oMiniInputCost = 0.15m;
        private const decimal Gpt4oMiniOutputCost = 0.60m;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
        public string ApiKeyPreview => IsConfigured ? _apiKey.Substring(0, Math.Min(15, _apiKey.Length)) + "..." : "BRAK";
        public string CurrentModel => DefaultModel;

        public OpenAIAnalysisService()
        {
            _httpClient = new HttpClient();

            // WAŻNE: Ustawiamy bardzo długi timeout na HttpClient (10 minut)
            // Faktyczny timeout będzie kontrolowany per-request przez CancellationToken
            // z wartością z ConfigService (dynamicznie, nie tylko przy starcie)
            _httpClient.Timeout = TimeSpan.FromMinutes(10);

            // Priorytet: 1) ConfigService, 2) zmienna srodowiskowa, 3) App.config
            _apiKey = ConfigService.Instance?.Current?.System?.OpenAiApiKey;
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                          ?? ConfigurationManager.AppSettings["OpenAiApiKey"]
                          ?? ConfigurationManager.AppSettings["OpenAIApiKey"]
                          ?? "";
            }

            if (IsConfigured)
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        /// <summary>
        /// Pobiera aktualny timeout z konfiguracji (dynamicznie)
        /// </summary>
        private int OpenAiTimeoutSeconds =>
            ConfigService.Instance?.Current?.System?.OpenAiTimeoutSeconds ?? 300;

        /// <summary>
        /// Testuje polaczenie z OpenAI API
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return (false, "Brak klucza API OpenAI. Ustaw OPENAI_API_KEY lub OpenAiApiKey w App.config");
            }

            try
            {
                var response = await CallOpenAIAsync("Odpowiedz tylko: OK", MiniModel, 50, ct);
                if (response.Contains("OK"))
                {
                    return (true, $"Polaczenie OK. Model: {MiniModel}");
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
                throw new InvalidOperationException(
                    "Klucz API OpenAI nie jest skonfigurowany. " +
                    "Prosze uzupelnic OpenAiApiKey w App.config lub ustawic zmienna srodowiskowa OPENAI_API_KEY.");
            }

            var prompt = CreateAnalysisPrompt(title, content, source, businessContext);

            try
            {
                // ZWIĘKSZONO max_tokens do 8000 aby uniknąć obcięcia JSON
                var response = await CallOpenAIAsync(prompt, DefaultModel, 8000, ct);
                return ParseAnalysisResponse(response, title);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAIAnalysis] Blad analizy: {ex.Message}");
                // Zamiast zwracac blad, zwracamy "surowy" artykul
                return CreateRawArticleResult(title, content, source, ex.Message);
            }
        }

        /// <summary>
        /// Szybkie filtrowanie artykulow przez GPT-4o-mini
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
                var response = await CallOpenAIAsync(prompt, MiniModel, 2000, ct);
                return ParseFilterResponse(response, articles);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAIFilter] Blad filtrowania: {ex.Message}");
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
                var response = await CallOpenAIAsync(prompt, MiniModel, 2000, ct);
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
                Debug.WriteLine($"[OpenAITranslate] Blad tlumaczenia: {ex.Message}");
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
            foreach (var a in articles.Take(20))
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
                var response = await CallOpenAIAsync(prompt, DefaultModel, 1500, ct);
                return ParseDailySummaryResponse(response);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAISummary] Blad generowania streszczenia: {ex.Message}");
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

        // Semaphore do kontroli rate limitingu - max 1 request na raz
        private static readonly SemaphoreSlim _rateLimitSemaphore = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;

        // Rate limiting - pobierane z ConfigService
        private int MinDelayBetweenRequestsMs =>
            ConfigService.Instance?.Current?.System?.MinDelayBetweenRequestsMs ?? 3000;
        private int MaxRetries =>
            ConfigService.Instance?.Current?.System?.MaxRetries ?? 3;

        private async Task<string> CallOpenAIAsync(string prompt, string model, int maxTokens, CancellationToken ct)
        {
            // System prompt z ConfigService lub nowy SUPER-PROMPT
            var systemPrompt = ConfigService.Instance?.Current?.Prompts?.SystemPrompt;
            if (string.IsNullOrEmpty(systemPrompt))
            {
                systemPrompt = GetSuperPrompt();
            }

            // RATE LIMITING: Czekaj na semafor i dodaj opóźnienie między requestami
            await _rateLimitSemaphore.WaitAsync(ct);
            try
            {
                // Oblicz ile trzeba poczekać od ostatniego requestu
                var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
                var delayNeeded = MinDelayBetweenRequestsMs - (int)timeSinceLastRequest.TotalMilliseconds;
                if (delayNeeded > 0)
                {
                    Debug.WriteLine($"[OpenAI] Rate limiting: czekam {delayNeeded}ms przed kolejnym requestem");
                    await Task.Delay(delayNeeded, ct);
                }

                return await CallOpenAIWithRetryAsync(prompt, model, maxTokens, systemPrompt, ct);
            }
            finally
            {
                _lastRequestTime = DateTime.Now;
                _rateLimitSemaphore.Release();
            }
        }

        private async Task<string> CallOpenAIWithRetryAsync(string prompt, string model, int maxTokens, string systemPrompt, CancellationToken ct)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var requestBody = new
                    {
                        model = model,
                        max_tokens = maxTokens,
                        temperature = 0.7,
                        response_format = new { type = "json_object" }, // WYMUSZA JSON!
                        messages = new object[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = prompt }
                        }
                    };

                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    Debug.WriteLine($"[OpenAI] Wysylam request do {model}, prompt length: {prompt.Length}, proba {attempt}/{MaxRetries}, timeout: {OpenAiTimeoutSeconds}s");

                    // Użyj per-request timeout z konfiguracji (dynamicznie)
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(OpenAiTimeoutSeconds));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                    var response = await _httpClient.PostAsync(ApiUrl, content, linkedCts.Token);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[OpenAI] Error response: {responseText}");

                        // Sprawdź czy to rate limit error
                        if (responseText.Contains("rate_limit") || responseText.Contains("Rate limit") ||
                            responseText.Contains("429") || responseText.Contains("TooManyRequests"))
                        {
                            // Parsuj sugerowany czas oczekiwania
                            var waitSeconds = ExtractRetryAfterSeconds(responseText);
                            var waitMs = Math.Max(waitSeconds * 1000, 5000); // Minimum 5 sekund

                            Debug.WriteLine($"[OpenAI] Rate limit hit! Czekam {waitMs}ms przed retry...");
                            await Task.Delay(waitMs, ct);
                            continue; // Retry
                        }

                        throw new Exception($"OpenAI API error {response.StatusCode}: {responseText}");
                    }

                    using var doc = JsonDocument.Parse(responseText);
                    var root = doc.RootElement;

                    // OpenAI response format: { choices: [{ message: { content: "..." } }] }
                    var textContent = root
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    Debug.WriteLine($"[OpenAI] Otrzymano odpowiedz, length: {textContent?.Length ?? 0}");

                    return textContent ?? "";
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // To jest TIMEOUT HttpClient, a NIE user cancellation - PONÓW PRÓBĘ!
                    lastException = new TimeoutException($"OpenAI request timeout (próba {attempt}/{MaxRetries})");
                    Debug.WriteLine($"[OpenAI] TIMEOUT przy próbie {attempt}/{MaxRetries} - request trwał za długo");

                    if (attempt < MaxRetries)
                    {
                        // Przy timeout czekamy dłużej przed kolejną próbą (15s, 30s, 45s, 60s)
                        var retryDelay = attempt * 15000;
                        Debug.WriteLine($"[OpenAI] Retry za {retryDelay / 1000}s po timeout...");
                        await Task.Delay(retryDelay, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    // User-requested cancellation - nie ponawiaj
                    Debug.WriteLine($"[OpenAI] Operacja anulowana przez użytkownika");
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.WriteLine($"[OpenAI] Blad przy probie {attempt}: {ex.Message}");

                    if (attempt < MaxRetries)
                    {
                        var retryDelay = attempt * 2000; // 2s, 4s, 6s
                        Debug.WriteLine($"[OpenAI] Retry za {retryDelay}ms...");
                        await Task.Delay(retryDelay, ct);
                    }
                }
            }

            throw lastException ?? new Exception("OpenAI request failed after all retries");
        }

        /// <summary>
        /// Wyciąga sugerowany czas oczekiwania z błędu rate limit
        /// </summary>
        private int ExtractRetryAfterSeconds(string errorResponse)
        {
            try
            {
                // Szukamy wzorca "Please try again in X.XXXs" lub "Please retry after X seconds"
                var match = System.Text.RegularExpressions.Regex.Match(errorResponse, @"try again in (\d+\.?\d*)s");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var seconds))
                {
                    return (int)Math.Ceiling(seconds) + 1; // +1 dla pewności
                }

                match = System.Text.RegularExpressions.Regex.Match(errorResponse, @"retry after (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var secs))
                {
                    return secs + 1;
                }
            }
            catch { }

            return 10; // Domyślnie 10 sekund
        }

        private string CreateAnalysisPrompt(string title, string content, string source, string businessContext)
        {
            return $@"Jestes STARSZYM ANALITYKIEM RYNKU DROBIARSKIEGO z 20-letnim doswiadczeniem, pracujacym dla Ubojni Drobiu Piorkowscy w Brzezinach.
Twoja rola to dostarczanie KOMPLEKSOWEJ, EDUKACYJNEJ i STRATEGICZNEJ analizy - tak jakbys pisal raport dla zarzadu.

TWOJE ZADANIE: Napisz BARDZO SZCZEGOLOWA analize (minimum 3000 slow). Nie oszczedzaj miejsca - im wiecej tresci, tym lepiej!

KONTEKST BIZNESOWY FIRMY:
{businessContext}

ARTYKUL DO ANALIZY:
Zrodlo: {source}
Tytul: {title}
Tresc: {content}

Odpowiedz TYLKO w formacie JSON (bez markdown, bez ```):
{{
  ""smart_title"": ""Krotki biznesowy naglowek (max 60 znakow) - esencja newsa dla prezesa, np. 'HPAI: 2 nowe ogniska w lodzkim' lub 'Cedrob podnosi ceny o 8%'"",

  ""sentiment_score"": 0.0,
  ""_sentiment_comment"": ""Liczba od -1.0 do +1.0. Negatywne dla ubojni Piorkowscy: HPAI w regionie (-0.8), wzrost cen pasz (-0.5), import brazylijski (-0.7). Pozytywne: wzrost cen sprzedazy (+0.6), HPAI u konkurencji (+0.3), nowy klient (+0.5). Neutralne: 0.0"",

  ""impact"": ""Medium"",
  ""_impact_comment"": ""Low=informacyjny, Medium=warto obserwowac, High=wymaga uwagi, Critical=natychmiastowa reakcja (HPAI w promieniu 50km, utrata kluczowego klienta, awaria)"",

  ""streszczenie"": ""NAPISZ MINIMUM 10-15 ZDAN szczegolowego streszczenia. Podaj WSZYSTKIE fakty, liczby, daty, nazwy z artykulu. UZUPELNIJ wlasnymi informacjami o temacie bazujac na wiedzy o branzy drobiarskiej - kontekst historyczny, trendy, porownania z innymi rynkami. Wyjasni CO, GDZIE, KIEDY, DLACZEGO, JAKIE KONSEKWENCJE. Napisz tak, jakbys tlumaczyl temat osobie spoza branzy."",

  ""kontekst_rynkowy"": ""NAPISZ 8-10 ZDAN o szerszym kontekscie rynkowym. Jak ta informacja wpisuje sie w obecna sytuacje na rynku drobiarskim? Jakie sa trendy cenowe ostatnich miesiecy? Jak wyglada sytuacja w Polsce vs UE vs swiat? Jakie czynniki makroekonomiczne wplywaja na rynek (inflacja, kursy walut, ceny energii, ceny zboz)?"",

  ""kim_jest"": ""DLA KAZDEGO podmiotu wspomnianego w artykule napisz MINIMUM 4-6 ZDAN edukacyjnego opisu:
• NAZWA FIRMY/ORGANIZACJI — pelna nazwa, rok zalozenia, siedziba, wlasciciele/akcjonariusze, skala dzialalnosci (obroty, zatrudnienie, moce produkcyjne), pozycja rynkowa w Polsce i UE, kluczowe produkty/uslugi, ostatnie wazne wydarzenia, powiazania kapitalowe z innymi firmami, znaczenie dla branzy drobiarskiej.
• OSOBA — pelne imie i nazwisko, stanowisko, wyksztalcenie i doswiadczenie zawodowe, wczesniejsze role, znaczenie w branzy, kontrowersje jesli sa.
• ORGANIZACJA BRANZOWA — pelna nazwa, rok powstania, cel dzialania, czlonkowie, wplyw na regulacje, kluczowe publikacje i raporty.
Jesli w artykule nie ma podmiotow, opisz podmioty ZWIAZANE z tematem (np. dla cen drobiu opisz KRD-IG, GUS, MRiRW, glownych producentow)."",

  ""tlumaczenie_pojec"": ""Wyjasni WSZYSTKIE specjalistyczne pojecia i terminy dla osoby spoza branzy. Minimum 5 pojec. Format:
• TERMIN — definicja prostym jezykiem, znaczenie dla branzy, przyklad uzycia, dlaczego to wazne dla Piorkowscy."",

  ""analiza_ceo"": ""NAPISZ MINIMUM 12-15 ZDAN kompleksowej analizy strategicznej dla CEO/wlasciciela Justyny Chrostowskiej:
1. WPLYW NA FIRME: Jak ta informacja bezposrednio wplywa na Piorkowscy? Jakie sa konsekwencje finansowe (przychody, koszty, marza)?
2. ANALIZA RYZYKA: Jakie ryzyka stwarza ta sytuacja? Prawdopodobienstwo i potencjalny wplyw kazdego ryzyka. Scenariusze pesymistyczny/optymistyczny.
3. SZANSE STRATEGICZNE: Jakie szanse mozna wykorzystac? Jak wyprzedzic konkurencje?
4. POZYCJA KONKURENCYJNA: Jak to wplywa na pozycje wobec Cedrob, SuperDrob, Plukon, System-Drob?
5. PERSPEKTYWA 3/6/12 MIESIECY: Co moze sie wydarzyc w krotkim/srednim/dlugim terminie?
6. DECYZJE DO PODJECIA: Jakie konkretne decyzje musi podjac CEO? Z jakimi trade-offami?
Odnosic sie do konkretnej sytuacji: straty 2M PLN/mies, spadek sprzedazy z 25M do 15M, import brazylijski 13 zl/kg, HPAI 19 ognisk w Polsce (2 w lodzkim), break-even spread 2.50 zl."",

  ""analiza_handlowiec"": ""NAPISZ MINIMUM 12-15 ZDAN kompleksowej analizy dla dzialu handlowego:
1. WPLYW NA CENY: Jak ta informacja wplywa na ceny sprzedazy? O ile mozna/trzeba podniesc ceny? Jakie sa granice cenowe?
2. ARGUMENTACJA DLA KLIENTOW: Konkretne argumenty do rozmow z kazdym klientem:
   - Makro: jak przekonac do akceptacji wyzszych cen? Jakie alternatywy maja?
   - Biedronka DC: jak renegocjowac kontrakty? Jakie sa ich priorytety?
   - Dino: jak wykorzystac ich ekspansje (300 nowych sklepow)?
   - Selgros, Carrefour, Stokrotka, Auchan: indywidualne podejscie
3. SZANSE SPRZEDAZOWE: Nowi klienci do pozyskania? Nowe kanaly? Nowe produkty?
4. ZAGROZENIA: Ryzyko utraty klientow? Dzialania konkurencji?
5. TAKTYKI NEGOCJACYJNE: Konkretne techniki negocjacyjne, timing rozmow
Przypisz dzialania do konkretnych handlowcow: Jola (Dino, Biedronka), Maja (Makro, Selgros), Radek (sieci lokalne), Teresa (RADDROB, hurt), Ania (Carrefour, Stokrotka)."",

  ""analiza_zakupowiec"": ""NAPISZ MINIMUM 12-15 ZDAN kompleksowej analizy dla dzialu zakupow:
1. WPLYW NA DOSTAWCOW: Jak ta informacja wplywa na hodowcow (Sukiennikowa 20km, Kaczmarek 20km, Wojciechowski 7km)?
2. CENY ZYWCA: Jak zmienia sie cena skupu? Jaki spread zywiec/pasza? Czy hodowcom sie oplaca?
3. RYNEK PASZ: Ceny kukurydzy, pszenicy, soi, sruty. Prognozy. Wplyw na koszty tuczu.
4. RYZYKO UTRATY DOSTAWCOW: Czy hodowcy moga przejsc do konkurencji (Plukon Sieradz 80km, System-Drob, Exdrob Kutno 100km)?
5. BIOSEKURNOSC I HPAI: Stan zabezpieczen u hodowcow, ryzyko wybiecia stad, plan awaryjny
6. NEGOCJACJE: Konkretne stawki do zaproponowania, argumenty, timing
7. DYWERSYFIKACJA: Nowi hodowcy do pozyskania? Alternatywne zrodla zywca?"",

  ""lekcja_branzowa"": ""NAPISZ 8-10 ZDAN edukacyjnych o tym, czego ta sytuacja uczy o branzy drobiarskiej. Jakie sa mechanizmy rynkowe? Jak dziala lancuch dostaw drobiu? Jakie sa typowe cykle cenowe? Co warto wiedziec o ekonomice ubojni/hodowli?"",

  ""dzialania_ceo"": [
    ""[PILNE] Szczegolowy opis dzialania (2-3 zdania co dokladnie zrobic). Odpowiedzialny: imie i nazwisko/stanowisko. Termin: konkretna data. Oczekiwany rezultat: co ma byc efektem."",
    ""[PILNE] Kolejne pilne dzialanie..."",
    ""[WAZNE] Dzialanie wazne ale nie pilne..."",
    ""[WAZNE] Kolejne..."",
    ""[DO_ROZWAZENIA] Dzialanie do przemyslenia..."",
    ""[DO_ROZWAZENIA] Kolejne...""
  ],

  ""dzialania_handlowiec"": [
    ""[PILNE] Szczegolowe dzialanie dla handlowca. Odpowiedzialny: Jola/Maja/Radek/Teresa/Ania. Klient: nazwa. Termin: data. Cel: co osiagnac."",
    ""[PILNE] Kolejne..."",
    ""[WAZNE] Kolejne..."",
    ""[WAZNE] Kolejne...""
  ],

  ""dzialania_zakupowiec"": [
    ""[PILNE] Szczegolowe dzialanie dla zakupowca. Dostawca: nazwa. Termin: data. Stawka/warunki do wynegocjowania."",
    ""[PILNE] Kolejne..."",
    ""[WAZNE] Kolejne..."",
    ""[WAZNE] Kolejne...""
  ],

  ""pytania_do_przemyslenia"": [
    ""Strategiczne pytanie 1, ktore CEO powinien rozwazyc?"",
    ""Pytanie 2 dotyczace dlugookresowej strategii?"",
    ""Pytanie 3 o pozycjonowanie firmy?""
  ],

  ""zrodla_do_monitorowania"": [
    ""Zrodlo 1: co monitorowac i dlaczego"",
    ""Zrodlo 2: wskaznik do sledzenia"",
    ""Zrodlo 3: portal/raport do sprawdzania""
  ],

  ""kategoria"": ""HPAI|Ceny|Konkurencja|Regulacje|Eksport|Import|Klienci|Koszty|Pogoda|Logistyka|Inwestycje"",
  ""severity"": ""critical|warning|positive|info"",
  ""tagi"": [""tag1"", ""tag2"", ""tag3"", ""tag4"", ""tag5""]
}}

KRYTYCZNE WYMAGANIA:
- Kazda sekcja tekstowa MINIMUM 10 zdan (streszczenie, kontekst, analizy)
- Sekcja kim_jest MINIMUM 4 podmioty, kazdy opisany w 4-6 zdaniach
- Sekcja tlumaczenie_pojec MINIMUM 5 terminow z pelnym wyjasnieniem
- Kazda lista dzialan MINIMUM 4-6 pozycji z pelnym opisem
- ZERO ogolnikow typu 'monitoruj sytuacje', 'badz czujny' - TYLKO KONKRETY
- Jesli artykul jest krotki - UZUPELNIJ wlasna wiedza o branzy!
- Pisz jak ekspert dla eksperta, ale tlumacz jak dla laika";
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
            if (string.IsNullOrWhiteSpace(response))
            {
                Debug.WriteLine("[OpenAIAnalysis] Pusta odpowiedz od OpenAI");
                return CreateStubAnalysis(originalTitle, "Pusta odpowiedz od OpenAI API");
            }

            Debug.WriteLine($"[OpenAIAnalysis] Raw response length: {response.Length}");

            var parsingErrors = new List<string>();

            var attempts = new List<(string name, Func<string> getJson)>
            {
                ("Original", () => ExtractJson(response)),
                ("After TryFix", () => ExtractJson(TryFixCommonJsonErrors(response))),
                ("Repair truncated", () => TryRepairTruncatedJson(ExtractJson(response))),
                ("Direct (JSON mode)", () => response.Trim()) // OpenAI JSON mode - odpowiedz POWINNA byc czystym JSON
            };

            foreach (var (name, getJson) in attempts)
            {
                try
                {
                    var json = getJson();
                    if (string.IsNullOrWhiteSpace(json) || json == "{}")
                    {
                        parsingErrors.Add($"[{name}] Pusty JSON");
                        continue;
                    }

                    Debug.WriteLine($"[OpenAIAnalysis] Trying {name}, JSON length: {json.Length}");

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var result = new ArticleAnalysisResult
                    {
                        SmartTitle = GetStringProperty(root, "smart_title", ""),
                        SentimentScore = GetDoubleProperty(root, "sentiment_score", 0.0),
                        Impact = GetStringProperty(root, "impact", "Medium"),

                        Summary = GetStringProperty(root, "streszczenie", "Brak streszczenia"),
                        MarketContext = GetStringProperty(root, "kontekst_rynkowy", ""),
                        WhoIs = GetStringProperty(root, "kim_jest", ""),
                        TermsExplanation = GetStringProperty(root, "tlumaczenie_pojec", ""),
                        AnalysisCeo = GetStringProperty(root, "analiza_ceo", ""),
                        AnalysisSales = GetStringProperty(root, "analiza_handlowiec", ""),
                        AnalysisBuyer = GetStringProperty(root, "analiza_zakupowiec", ""),
                        IndustryLesson = GetStringProperty(root, "lekcja_branzowa", ""),
                        ActionsCeo = GetStringArrayProperty(root, "dzialania_ceo"),
                        ActionsSales = GetStringArrayProperty(root, "dzialania_handlowiec"),
                        ActionsBuyer = GetStringArrayProperty(root, "dzialania_zakupowiec"),
                        StrategicQuestions = GetStringArrayProperty(root, "pytania_do_przemyslenia"),
                        SourcesToMonitor = GetStringArrayProperty(root, "zrodla_do_monitorowania"),
                        Category = GetStringProperty(root, "kategoria", "Info"),
                        Severity = GetStringProperty(root, "severity", "info"),
                        Tags = GetStringArrayProperty(root, "tagi")
                    };

                    Debug.WriteLine($"[OpenAIAnalysis] SUCCESS with {name}!");
                    return result;
                }
                catch (Exception ex)
                {
                    parsingErrors.Add($"[{name}] {ex.Message}");
                    Debug.WriteLine($"[OpenAIAnalysis] {name} failed: {ex.Message}");
                }
            }

            // Wszystkie proby zawiodly
            Debug.WriteLine($"[OpenAIAnalysis] All parsing attempts failed");

            var responsePreview = response.Length > 500
                ? response.Substring(0, 500) + "... [OBCIETE]"
                : response;

            var errorDetails = new StringBuilder();
            errorDetails.AppendLine("BLAD PARSOWANIA JSON - szczegoly:");
            errorDetails.AppendLine($"Dlugosc odpowiedzi: {response.Length} znakow");
            errorDetails.AppendLine();
            errorDetails.AppendLine("Proby parsowania:");
            foreach (var err in parsingErrors)
            {
                errorDetails.AppendLine($"  - {err}");
            }
            errorDetails.AppendLine();
            errorDetails.AppendLine("Poczatek odpowiedzi OpenAI:");
            errorDetails.AppendLine("---");
            errorDetails.AppendLine(responsePreview);
            errorDetails.AppendLine("---");

            LastRawResponse = response;
            LastParsingError = errorDetails.ToString();

            return CreateStubAnalysis(originalTitle, errorDetails.ToString());
        }

        public string LastRawResponse { get; private set; }
        public string LastParsingError { get; private set; }

        private string TryFixCommonJsonErrors(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var result = text;

            var jsonStart = result.IndexOf('{');
            if (jsonStart > 0)
            {
                result = result.Substring(jsonStart);
            }

            var jsonEnd = result.LastIndexOf('}');
            if (jsonEnd > 0 && jsonEnd < result.Length - 1)
            {
                result = result.Substring(0, jsonEnd + 1);
            }

            result = System.Text.RegularExpressions.Regex.Replace(result, @",(\s*[}\]])", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @",\s*,", ",");

            return result;
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
                Debug.WriteLine($"[OpenAIFilter] Blad parsowania: {ex.Message}");
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
                Debug.WriteLine($"[OpenAISummary] Blad parsowania: {ex.Message}");
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

            text = text.TrimStart('\uFEFF', '\u200B', '\u200C', '\u200D', '\uFFFE');

            // Usun bloki markdown
            while (text.Contains("```"))
            {
                var start = text.IndexOf("```");
                var endMarker = text.IndexOf("```", start + 3);

                if (endMarker > start)
                {
                    var blockContent = text.Substring(start + 3, endMarker - start - 3);
                    blockContent = StripMarkdownLanguageTag(blockContent);
                    text = text.Substring(0, start) + blockContent + text.Substring(endMarker + 3);
                }
                else
                {
                    var blockContent = text.Substring(start + 3);
                    blockContent = StripMarkdownLanguageTag(blockContent);
                    text = text.Substring(0, start) + blockContent;
                }
            }

            text = text.Replace("`", "");

            var jsonStart = text.IndexOfAny(new[] { '{', '[' });
            if (jsonStart >= 0)
            {
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

            return text.Trim();
        }

        private string StripMarkdownLanguageTag(string blockContent)
        {
            if (string.IsNullOrEmpty(blockContent))
                return blockContent;

            blockContent = blockContent.TrimStart();

            var languageTags = new[] { "json", "javascript", "js", "python", "py", "csharp", "cs", "xml", "html" };
            foreach (var tag in languageTags)
            {
                if (blockContent.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                {
                    blockContent = blockContent.Substring(tag.Length).TrimStart();
                    break;
                }
            }

            return blockContent;
        }

        private string TryRepairTruncatedJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "{}";

            // Krok 1: Znajdź ostatnią kompletną właściwość
            var lastCompletePropertyIndex = FindLastCompleteProperty(json);
            if (lastCompletePropertyIndex > 0 && lastCompletePropertyIndex < json.Length - 10)
            {
                json = json.Substring(0, lastCompletePropertyIndex);
                Debug.WriteLine($"[JSON Repair] Obcięto do ostatniej kompletnej właściwości na pozycji {lastCompletePropertyIndex}");
            }

            int braceCount = 0;
            int bracketCount = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

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
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;
                    else if (c == '[') bracketCount++;
                    else if (c == ']') bracketCount--;
                }
            }

            var sb = new StringBuilder(json);

            // Zamknij otwarty string
            if (inString)
            {
                sb.Append("\"");
            }

            // Usuń trailing garbage
            var trimmed = sb.ToString().TrimEnd();
            while (trimmed.EndsWith(":") || trimmed.EndsWith(",") || trimmed.EndsWith("\"\""))
            {
                if (trimmed.EndsWith("\"\""))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 2);
                }
                else
                {
                    trimmed = trimmed.TrimEnd(':', ',');
                }
            }
            sb = new StringBuilder(trimmed);

            // Przelicz nawiasy po cleanup
            braceCount = 0;
            bracketCount = 0;
            inString = false;
            escaped = false;

            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\' && inString) { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (!inString)
                {
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;
                    else if (c == '[') bracketCount++;
                    else if (c == ']') bracketCount--;
                }
            }

            // Dodaj brakujące zamknięcia
            for (int i = 0; i < bracketCount; i++)
                sb.Append(']');
            for (int i = 0; i < braceCount; i++)
                sb.Append('}');

            return sb.ToString();
        }

        /// <summary>
        /// Znajduje pozycję końca ostatniej kompletnej właściwości JSON
        /// </summary>
        private int FindLastCompleteProperty(string json)
        {
            // Szukamy wzorca: "property": value (gdzie value kończy się przecinkiem lub jest ostatnia)
            // Szukamy od końca ostatniego przecinka przed obciętym tekstem

            int lastCommaPos = -1;
            bool inString = false;
            bool escaped = false;
            int depth = 0;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped) { escaped = false; continue; }
                if (c == '\\' && inString) { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }

                if (!inString)
                {
                    if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;
                    else if (c == ',' && depth == 1) // Przecinek na głównym poziomie
                    {
                        lastCommaPos = i + 1;
                    }
                }
            }

            return lastCommaPos;
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

        private double GetDoubleProperty(JsonElement element, string propertyName, double defaultValue)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return prop.GetDouble();
                }
                if (prop.ValueKind == JsonValueKind.String)
                {
                    if (double.TryParse(prop.GetString(), out var result))
                    {
                        return result;
                    }
                }
            }
            return defaultValue;
        }

        private ArticleAnalysisResult CreateStubAnalysis(string title, string message)
        {
            return new ArticleAnalysisResult
            {
                SmartTitle = title?.Length > 60 ? title.Substring(0, 57) + "..." : title ?? "Brak tytulu",
                SentimentScore = 0.0,
                Impact = "Medium",

                Summary = message,
                MarketContext = "Informacja niedostepna.",
                WhoIs = "Informacja niedostepna - sprawdz konfiguracje klucza API OpenAI.",
                TermsExplanation = "Informacja niedostepna.",
                AnalysisCeo = message,
                AnalysisSales = message,
                AnalysisBuyer = message,
                IndustryLesson = "Informacja niedostepna.",
                ActionsCeo = new List<string> { "Skonfiguruj klucz API OpenAI w App.config lub zmiennej srodowiskowej OPENAI_API_KEY" },
                ActionsSales = new List<string>(),
                ActionsBuyer = new List<string>(),
                StrategicQuestions = new List<string>(),
                SourcesToMonitor = new List<string>(),
                Category = "Info",
                Severity = "info",
                Tags = new List<string>()
            };
        }

        private ArticleAnalysisResult CreateRawArticleResult(string title, string content, string source, string errorMessage)
        {
            var truncatedContent = content?.Length > 1000
                ? content.Substring(0, 1000) + "..."
                : content ?? "";

            return new ArticleAnalysisResult
            {
                SmartTitle = title?.Length > 60 ? title.Substring(0, 57) + "..." : title ?? "News",
                SentimentScore = 0.0,
                Impact = "Medium",

                Summary = $"[ARTYKUL BEZ ANALIZY AI - timeout/blad]\n\n{truncatedContent}",
                MarketContext = $"Analiza niedostepna (blad: {errorMessage}). Przeczytaj artykul zrodlowy.",
                WhoIs = "Analiza podmiotow niedostepna.",
                TermsExplanation = "Tlumaczenie terminow niedostepne.",
                AnalysisCeo = "Analiza dla CEO niedostepna - przeczytaj tresc artykulu powyzej i wyciagnij wnioski samodzielnie.",
                AnalysisSales = "Analiza dla Handlowca niedostepna.",
                AnalysisBuyer = "Analiza dla Zakupowca niedostepna.",
                IndustryLesson = "Lekcja branzowa niedostepna.",

                ActionsCeo = new List<string> { "[Przeczytaj artykul i okreslic akcje samodzielnie]" },
                ActionsSales = new List<string>(),
                ActionsBuyer = new List<string>(),
                StrategicQuestions = new List<string> { "Czy ten news wymaga natychmiastowej reakcji?" },
                SourcesToMonitor = new List<string> { source ?? "Zrodlo nieznane" },

                Category = "Info",
                Severity = "info",
                Tags = new List<string> { "raw", "no-ai-analysis" },

                IsRawArticle = true,
                RawArticleError = errorMessage
            };
        }

        /// <summary>
        /// SUPER-PROMPT: Nowy, rozbudowany system prompt dla glębokiej analizy biznesowej
        /// Eliminuje "szkolne" odpowiedzi, wymusza konkrety i wiedze ekspercka
        /// </summary>
        private string GetSuperPrompt()
        {
            return @"Jestes Starszym Doradca Strategicznym dla Zarzadu duzej ubojni drobiu 'Piorkowscy' w Polsce. Twoim szefem jest Prezes, ktory oczekuje KONKRETOW, a nie lania wody.
Twoim zadaniem jest analiza dostarczonego tekstu (lub fragmentu) i wygenerowanie raportu w formacie JSON.

WYMAGANIA DOTYCZACE TRESCI:
1. JEZYK: Uzywaj jezyka prostego, meskiego, konkretnego. Jak w rozmowie biznesowej. Unikaj korpo-belkotu i ogolnikow typu 'nalezy monitorowac sytuacje'.
2. WIEDZA ZEWNETRZNA: Jesli w tekscie pojawia sie nazwa firmy (np. Cedrob, SuperDrob, Drosed, Animex) lub pojecie (np. Mercosur, HPAI, KSeF), a w tekscie nie ma wyjasnienia - UZYJ SWOJEJ WIEDZY, aby to wyjasnic. Prezes ma wiedziec, czy to konkurencja, zagrozenie czy szansa.
3. KONKRETY: Jesli piszesz 'wzrost cen', dopisz 'co sugeruje podniesienie cennika o 10-15 groszy/kg'. Jesli piszesz 'ryzyko', oszacuj prawdopodobienstwo i potencjalne straty w PLN.
4. LICZBY I FAKTY: Zawsze podawaj konkretne liczby, procenty, kwoty. 'Duzy wzrost' to nie informacja - '15% wzrost QoQ' to informacja.
5. PERSPEKTYWA PIORKOWSCY: Wszystko analizuj przez pryzmat sredniej ubojni drobiu w Brzezinach (woj. lodzkie), 70k kurcząt/dzien, strata 2M PLN/mies, konkurencja z Cedrob i importem brazylijskim.

SEKCJE RAPORTU (wszystkie wymagane, minimum 3-5 zdan kazda):

EXECUTIVE SUMMARY (smart_title + streszczenie):
- 3 zdania dla Prezesa: CO sie stalo, DLACZEGO to wazne, CO robic TERAZ.
- Naglowek max 60 znakow, chwytliwy, biznesowy.

KIM JEST / CO TO JEST (kim_jest):
- Wyjasni KAZDY podmiot i pojecie z tekstu ORAZ powiazane z tematem.
- Dla firm: pozycja rynkowa, wlasciciel, obroty, czy to konkurencja/partner/zagrozenie.
- Dla pojec: definicja prosta + dlaczego wazne dla ubojni.
- UZYJ SWOJEJ WIEDZY jesli brak w tekscie!

LEKCJA BRANZOWA (lekcja_branzowa):
- Edukacja: jak ten news wpisuje sie w mechanizmy rynku drobiarskiego?
- Cykle cenowe, sezonowosc, zaleznosci popyt/podaz, wplyw makroekonomii.

ANALIZA CEO (analiza_ceo):
- Strategia: czy wchodzic w inwestycje? Ciac koszty? Konsolidowac?
- Ryzyko vs szansa - oszacuj prawdopodobienstwo i impact.
- Konkretne kwoty: 'przy wzroscie cen pasz o 8% tracimy 150k PLN/mies na marzy'.

ANALIZA HANDLOWIEC (analiza_handlowiec):
- Konkretne argumenty do negocjacji z Biedronka, Dino, Makro, Lidl.
- Czy straszyc brakiem towaru? Czy prosic o podwyzke?
- Timing: kiedy rozmawiac, z kim, jakim tonem?

ANALIZA ZAKUPOWIEC (analiza_zakupowiec):
- Co robic z hodowcami (Sukiennikowa, Kaczmarek, Wojciechowski)?
- Kupowac zywiec na zapas czy czekac? Jaka cena jest akceptowalna?
- Pasze: kukurydza, soja - trendy cenowe, kiedy kupic?

AKCJE (dzialania_ceo, dzialania_handlowiec, dzialania_zakupowiec):
- Konkretne zadania: KTO robi, CO robi, DO KIEDY, JAKI CEL.
- Priorytet: [PILNE], [WAZNE], [DO_ROZWAZENIA]

FORMAT WYJSCIOWY:
Zwroc TYLKO czysty JSON (bez markdown, bez ```). Pierwszy znak to {, ostatni to }.
Uzyj DOKLADNIE tych kluczy:
{
  ""smart_title"": ""Max 60 znakow, chwytliwy"",
  ""sentiment_score"": -0.5,
  ""impact"": ""High"",
  ""streszczenie"": ""Pelne streszczenie z faktami i liczbami..."",
  ""kontekst_rynkowy"": ""Szerszy kontekst rynku drobiarskiego..."",
  ""kim_jest"": ""Definicje podmiotow i pojec - UZYJ WIEDZY ZEWNETRZNEJ..."",
  ""tlumaczenie_pojec"": ""Wyjasnienia specjalistycznych terminow..."",
  ""lekcja_branzowa"": ""Edukacyjny fragment o mechanizmach rynku..."",
  ""analiza_ceo"": ""Strategiczna analiza dla Prezesa z liczbami..."",
  ""analiza_handlowiec"": ""Konkretne argumenty negocjacyjne..."",
  ""analiza_zakupowiec"": ""Taktyka zakupowa z cenami..."",
  ""dzialania_ceo"": [""[PILNE] Konkretne zadanie..."", ""[WAZNE] Kolejne...""],
  ""dzialania_handlowiec"": [""[PILNE] Zadzwonic do Biedronki...""],
  ""dzialania_zakupowiec"": [""[PILNE] Negocjacje z hodowca...""],
  ""pytania_do_przemyslenia"": [""Strategiczne pytanie 1?""],
  ""zrodla_do_monitorowania"": [""Co sledzic i dlaczego""],
  ""kategoria"": ""HPAI|Ceny|Konkurencja|Regulacje|Eksport|Import|Klienci|Koszty"",
  ""severity"": ""critical|warning|positive|info"",
  ""tagi"": [""tag1"", ""tag2""]
}

KRYTYCZNE: NIGDY nie zwracaj pustych sekcji. Jesli brak danych - WYMYSL na podstawie wiedzy o branzy. Prezes woli 80% trafna analize niz pusta sekcje.";
        }

        #endregion

        #region Cost Estimation

        public decimal EstimateCost(int inputTokens, int outputTokens, bool isMainModel)
        {
            if (isMainModel)
            {
                return (inputTokens / 1000000m) * Gpt4oInputCost + (outputTokens / 1000000m) * Gpt4oOutputCost;
            }
            else
            {
                return (inputTokens / 1000000m) * Gpt4oMiniInputCost + (outputTokens / 1000000m) * Gpt4oMiniOutputCost;
            }
        }

        #endregion
    }
}
