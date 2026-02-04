using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis wyszukiwania newsów przez Perplexity AI (Sonar API)
    /// Przeszukuje CAŁY polski internet w poszukiwaniu informacji o branży drobiarskiej i mięsnej
    /// Zastępuje TavilySearchService
    /// </summary>
    public class PerplexitySearchService : IDisposable
    {
        private const string ApiUrl = "https://api.perplexity.ai/chat/completions";
        private const string SonarModel = "sonar";
        private const string SonarProModel = "sonar-pro";

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly SemaphoreSlim _rateLimiter;
        private DateTime _lastRequestTime = DateTime.MinValue;

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public PerplexitySearchService(string apiKey = null)
        {
            _apiKey = apiKey
                ?? Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY")
                ?? System.Configuration.ConfigurationManager.AppSettings["PerplexityApiKey"];

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _rateLimiter = new SemaphoreSlim(1);

            if (IsConfigured)
                Debug.WriteLine($"[Perplexity] API configured: {_apiKey.Substring(0, Math.Min(20, _apiKey.Length))}...");
            else
                Debug.WriteLine("[Perplexity] WARNING: No API key found!");
        }

        #region Main Search Methods

        /// <summary>
        /// Wyszukaj newsy używając Perplexity Sonar
        /// </summary>
        public async Task<PerplexitySearchResult> SearchAsync(
            string query,
            PerplexitySearchOptions options = null,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
                return new PerplexitySearchResult { Success = false, Error = "API key not configured" };

            options ??= PerplexitySearchOptions.Default;
            await RateLimitAsync(ct);

            try
            {
                Debug.WriteLine($"[Perplexity] Searching: {query.Substring(0, Math.Min(60, query.Length))}...");

                var request = new PerplexityRequest
                {
                    Model = options.UseProModel ? SonarProModel : SonarModel,
                    Messages = new List<PerplexityMessage>
                    {
                        new() { Role = "system", Content = GetSystemPrompt() },
                        new() { Role = "user", Content = query }
                    },
                    MaxTokens = options.MaxTokens,
                    Temperature = 0.2,
                    SearchRecencyFilter = options.RecencyFilter,
                    ReturnCitations = true,
                    ReturnRelatedQuestions = false
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                var json = JsonSerializer.Serialize(request, jsonOptions);

                var response = await _httpClient.PostAsync(
                    ApiUrl,
                    new StringContent(json, Encoding.UTF8, "application/json"),
                    ct);

                var responseJson = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Perplexity] Error {response.StatusCode}: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}");
                    return new PerplexitySearchResult
                    {
                        Success = false,
                        Error = $"HTTP {response.StatusCode}",
                        Query = query
                    };
                }

                var parsed = JsonSerializer.Deserialize<PerplexityResponse>(responseJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var result = ParseResponse(parsed, query);
                Debug.WriteLine($"[Perplexity] ✓ Found {result.Articles.Count} citations for: {query.Substring(0, Math.Min(40, query.Length))}...");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Perplexity] Exception: {ex.Message}");
                return new PerplexitySearchResult { Success = false, Error = ex.Message, Query = query };
            }
        }

        /// <summary>
        /// Główna metoda - wyszukaj WSZYSTKIE newsy o branży drobiarskiej i mięsnej
        /// Przeszukuje cały polski internet + źródła międzynarodowe
        /// </summary>
        public async Task<PerplexityNewsSearchResult> SearchPoultryNewsAsync(
            bool includeInternational = true,
            SearchPriority maxPriority = SearchPriority.All,
            CancellationToken ct = default)
        {
            var result = new PerplexityNewsSearchResult
            {
                PolishNews = new List<PerplexityArticle>(),
                InternationalNews = new List<PerplexityArticle>(),
                AiSummaries = new List<PerplexityAiSummary>(),
                SearchedAt = DateTime.Now
            };

            if (!IsConfigured)
            {
                Debug.WriteLine("[Perplexity] Not configured - returning empty result");
                return result;
            }

            Debug.WriteLine($"[Perplexity] === Starting comprehensive Polish market search ===");

            // Pobierz zapytania według priorytetu
            var polishQueries = GetPolishQueries(maxPriority);
            var totalQueries = polishQueries.Count;
            var currentQuery = 0;

            // Wyszukiwania dla polskiego rynku
            foreach (var query in polishQueries)
            {
                ct.ThrowIfCancellationRequested();
                currentQuery++;
                Debug.WriteLine($"[Perplexity] [{currentQuery}/{totalQueries}] {query.Substring(0, Math.Min(50, query.Length))}...");

                var searchResult = await SearchAsync(query, new PerplexitySearchOptions
                {
                    RecencyFilter = "week",
                    MaxTokens = 2000
                }, ct);

                if (searchResult.Success)
                {
                    result.PolishNews.AddRange(searchResult.Articles);

                    // Zapisz AI summary
                    if (!string.IsNullOrEmpty(searchResult.AiAnswer))
                    {
                        result.AiSummaries.Add(new PerplexityAiSummary
                        {
                            Topic = query,
                            Summary = searchResult.AiAnswer
                        });
                    }
                }

                await Task.Delay(600, ct); // Rate limiting - 600ms between requests
            }

            // Wyszukiwania międzynarodowe
            if (includeInternational)
            {
                Debug.WriteLine($"[Perplexity] === Starting international search ===");
                var intlQueries = GetInternationalQueries(maxPriority);

                foreach (var query in intlQueries)
                {
                    ct.ThrowIfCancellationRequested();

                    var searchResult = await SearchAsync(query, new PerplexitySearchOptions
                    {
                        RecencyFilter = "week",
                        UseProModel = true, // Lepsze dla angielskich źródeł
                        MaxTokens = 2500
                    }, ct);

                    if (searchResult.Success)
                    {
                        result.InternationalNews.AddRange(searchResult.Articles);

                        if (!string.IsNullOrEmpty(searchResult.AiAnswer))
                        {
                            result.AiSummaries.Add(new PerplexityAiSummary
                            {
                                Topic = query,
                                Summary = searchResult.AiAnswer
                            });
                        }
                    }

                    await Task.Delay(600, ct);
                }
            }

            // Deduplikacja
            result.PolishNews = DeduplicateArticles(result.PolishNews);
            result.InternationalNews = DeduplicateArticles(result.InternationalNews);
            result.TotalArticles = result.PolishNews.Count + result.InternationalNews.Count;

            Debug.WriteLine($"[Perplexity] === COMPLETE: Found {result.TotalArticles} unique articles ===");
            Debug.WriteLine($"[Perplexity]   Polish: {result.PolishNews.Count}, International: {result.InternationalNews.Count}");
            Debug.WriteLine($"[Perplexity]   AI Summaries: {result.AiSummaries.Count}");

            return result;
        }

        /// <summary>
        /// Szybkie wyszukiwanie - tylko krytyczne tematy (HPAI, ceny, top konkurenci)
        /// </summary>
        public async Task<PerplexityNewsSearchResult> QuickSearchAsync(CancellationToken ct = default)
        {
            return await SearchPoultryNewsAsync(
                includeInternational: false,
                maxPriority: SearchPriority.Critical,
                ct: ct);
        }

        /// <summary>
        /// Pełne codzienne wyszukiwanie - wszystkie priorytety
        /// </summary>
        public async Task<PerplexityNewsSearchResult> FullDailySearchAsync(CancellationToken ct = default)
        {
            return await SearchPoultryNewsAsync(
                includeInternational: true,
                maxPriority: SearchPriority.All,
                ct: ct);
        }

        #endregion

        #region Query Definitions - COMPREHENSIVE

        /// <summary>
        /// 80+ zapytań pokrywających CAŁY polski rynek mięsny i drobiarski
        /// </summary>
        private List<string> GetPolishQueries(SearchPriority maxPriority = SearchPriority.All)
        {
            var allQueries = new List<(string Query, SearchPriority Priority)>
            {
                // ═══════════════════════════════════════════════════════════════
                // GRUPA 1: HPAI / CHOROBY (KRYTYCZNE - zawsze pierwsze)
                // ═══════════════════════════════════════════════════════════════
                ("Ptasia grypa HPAI Polska 2026 - ogniska, strefy zapowietrzone, restrykcje. Które województwa dotknięte? Podaj źródła.", SearchPriority.Critical),
                ("Główny Lekarz Weterynarii komunikaty HPAI 2026. Nowe ogniska ptasiej grypy, decyzje administracyjne.", SearchPriority.Critical),
                ("Salmonella w drobiu Polska 2026. Alerty RASFF, wycofania produktów z rynku.", SearchPriority.Critical),
                ("Choroby drobiu Polska - grypa ptaków, Newcastle, IB, kokcydioza. Aktualna sytuacja epidemiologiczna.", SearchPriority.Important),
                ("HPAI łódzkie mazowieckie 2026. Ogniska ptasiej grypy w regionie centralnym Polski.", SearchPriority.Critical),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 2: CENY SKUPU I HURTOWE (KRYTYCZNE)
                // ═══════════════════════════════════════════════════════════════
                ("Ceny skupu żywca drobiowego kurczak brojler Polska luty 2026. Ile ubojnie płacą za kilogram?", SearchPriority.Critical),
                ("Ceny hurtowe mięsa drobiowego - tuszka, filet z piersi, skrzydełka, udka, podudzia. Notowania luty 2026.", SearchPriority.Critical),
                ("Ceny indyka - żywiec i mięso hurtowe Polska 2026. Skup i sprzedaż hurtowa.", SearchPriority.Important),
                ("Ceny kaczki i gęsi Polska 2026. Skup żywca i hurt.", SearchPriority.Standard),
                ("Notowania giełdy drobiarskiej, rynku drobiu Polska. Aktualne ceny spot.", SearchPriority.Critical),
                ("Ceny jaj konsumpcyjnych Polska 2026. Hurt i detal, klasy wagowe.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 3: UBOJNIE - KONKURENCJA BEZPOŚREDNIA
                // ═══════════════════════════════════════════════════════════════
                ("Cedrob najnowsze wiadomości 2026 - przejęcie przez ADQ, inwestycje, zmiany w zarządzie.", SearchPriority.Critical),
                ("SuperDrob LipCo Foods aktualności 2026. CPF Charoen Pokphand, Zbigniew Jagiełło.", SearchPriority.Critical),
                ("Drosed Indykpol wiadomości 2026. LDC Group, ADQ, inwestycje.", SearchPriority.Important),
                ("Animex Foods mięso drobiowe 2026. WH Group strategia w Polsce.", SearchPriority.Important),
                ("Wipasz drób Olsztyn 2026. Inwestycje, produkcja, integracja pionowa.", SearchPriority.Important),
                ("Drobimex Szczecin wiadomości 2026. Produkcja, eksport.", SearchPriority.Standard),
                ("Plukon Food Group Polska 2026. Inwestycje, przejęcia.", SearchPriority.Standard),
                ("Sokołów Morliny drób wędliny 2026. Danish Crown strategia.", SearchPriority.Standard),
                ("Nowe ubojnie drobiu budowane w Polsce 2026. Inwestycje, lokalizacje, moce produkcyjne.", SearchPriority.Important),
                ("Zamknięcia ubojni drobiu Polska 2026. Upadłości, problemy finansowe, restrukturyzacje.", SearchPriority.Important),
                ("Ranking największych producentów drobiu w Polsce 2026. Kto jest liderem rynku?", SearchPriority.Standard),
                ("Ubojnie drobiu województwo łódzkie - lista firm, ranking, moce produkcyjne.", SearchPriority.Important),

                // BRAKUJĄCY KONKURENCI (dodane)
                ("RADDROB Chlebowski Łódź wiadomości 2026. Ubojnia, produkcja, inwestycje.", SearchPriority.Critical),  // Klient i konkurent!
                ("System-Drob firma ubojnia 2026. Produkcja, zmiany.", SearchPriority.Important),
                ("Drobex ubojnia wiadomości 2026. Inwestycje, produkcja.", SearchPriority.Standard),
                ("Roldrob Ostrów Wielkopolski 2026. Ubojnia, aktualności.", SearchPriority.Standard),
                ("Exdrob firma drobiarska 2026. Wiadomości, produkcja.", SearchPriority.Standard),
                ("Gobarto Cedrob współpraca 2026. Spółka, strategia.", SearchPriority.Important),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 4: INWESTORZY I PRZEJĘCIA (STRATEGICZNE)
                // ═══════════════════════════════════════════════════════════════
                ("ADQ Abu Dhabi przejęcie Cedrob 2026 - status negocjacji, cena transakcji, termin finalizacji.", SearchPriority.Critical),
                ("Fundusze private equity kupują polskie firmy mięsne drobiarskie 2026.", SearchPriority.Important),
                ("Konsolidacja branży drobiarskiej Polska. Fuzje i przejęcia 2026.", SearchPriority.Important),
                ("LDC Group inwestycje w Polsce - drób, indyki, przetwórstwo 2026.", SearchPriority.Important),
                ("CPF Charoen Pokphand Foods Polska - SuperDrob, plany ekspansji 2026.", SearchPriority.Important),
                ("Chińskie firmy przejmują polskie zakłady mięsne 2026. WH Group i inni.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 5: HURTOWNIE I DYSTRYBUCJA
                // ═══════════════════════════════════════════════════════════════
                ("Hurtownie mięsa drobiowego Polska 2026. Główne firmy, ceny, dystrybucja.", SearchPriority.Important),
                ("Dystrybucja mięsa drobiowego region łódzki mazowiecki. Hurtownie, dostawcy.", SearchPriority.Important),
                ("Makro Selgros Eurocash mięso drobiowe ceny hurtowe 2026.", SearchPriority.Standard),
                ("Chłodnie i magazyny mięsa Polska. Inwestycje, pojemności, koszty.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 6: SIECI HANDLOWE - DETALOWCY
                // ═══════════════════════════════════════════════════════════════
                ("Biedronka promocje mięso drobiowe luty 2026. Gazetka, ceny kurczaka fileta tuszki.", SearchPriority.Important),
                ("Lidl promocje kurczak 2026. Aktualna gazetka, ceny mięsa drobiowego.", SearchPriority.Important),
                ("Dino sklepy mięso drobiowe ceny 2026. Promocje, ekspansja, nowe sklepy.", SearchPriority.Important),
                ("Kaufland Netto ceny drobiu kurczak indyk 2026.", SearchPriority.Standard),
                ("Auchan Carrefour mięso drobiowe promocje ceny 2026.", SearchPriority.Standard),
                ("Żabka convenience mięso gotowe produkty drobiowe 2026.", SearchPriority.Standard),
                ("Private label marki własne sieci handlowych - mięso drobiowe 2026.", SearchPriority.Standard),
                ("Marże sieci handlowych na mięsie drobiowym Polska. Ile zarabiają dyskonty?", SearchPriority.Standard),
                ("Ceny detaliczne kurczaka w Polsce luty 2026 - porównanie sklepów i sieci.", SearchPriority.Important),

                // BRAKUJĄCE SIECI HANDLOWE (dodane)
                ("Selgros Polska mięso drobiowe ceny 2026. Promocje dla gastronomii.", SearchPriority.Important),
                ("Stokrotka sklepy mięso kurczak 2026. Promocje, ceny, ekspansja.", SearchPriority.Important),
                ("Polomarket sklepy spożywcze 2026. Mięso drobiowe, ekspansja.", SearchPriority.Standard),
                ("E.Leclerc Polska 2026. Hipermarkety, mięso drobiowe, ceny.", SearchPriority.Standard),
                ("Intermarche Polska 2026. Sklepy, mięso, promocje.", SearchPriority.Standard),
                ("Topaz sieć sklepów Lublin Radom 2026. Ekspansja, mięso.", SearchPriority.Standard),
                ("Aldi Polska ekspansja 2026. Nowe sklepy, produkty mięsne.", SearchPriority.Important),
                ("Spar Polska 2026. Sklepy, mięso drobiowe.", SearchPriority.Standard),
                ("Freshmarket sklepy mięso 2026. Sieć convenience.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 7: PASZE I SUROWCE
                // ═══════════════════════════════════════════════════════════════
                ("Ceny pasz drobiowych Polska luty 2026. Pasza starter grower finiszer dla brojlerów.", SearchPriority.Important),
                ("Ceny kukurydzy pszenicy jęczmienia Polska 2026. Notowania zbóż paszowych.", SearchPriority.Important),
                ("Śruta sojowa ceny Polska 2026. Import, notowania, dostawcy.", SearchPriority.Important),
                ("MATIF Euronext notowania zbóż kukurydza pszenica luty 2026.", SearchPriority.Important),
                ("Producenci pasz w Polsce - Cargill, De Heus, Agravis, Wipasz, Cedrob. Ceny 2026.", SearchPriority.Standard),
                ("Dodatki paszowe drób - aminokwasy, witaminy, premiksy. Ceny 2026.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 8: IMPORT I EKSPORT (ZAGROŻENIA)
                // ═══════════════════════════════════════════════════════════════
                ("Import drobiu z Brazylii do Polski 2026. BRF, JBS, Seara - ilości, ceny, wpływ na rynek.", SearchPriority.Critical),
                ("Import drobiu z Ukrainy MHP 2026. Wolumeny, ceny, wpływ na polskich producentów.", SearchPriority.Critical),
                ("Import mięsa drobiowego do UE z krajów trzecich 2026. Kontyngenty, cła.", SearchPriority.Important),
                ("Eksport polskiego drobiu 2026. Główne kierunki, wartość, wolumeny.", SearchPriority.Important),
                ("Mercosur umowa handlowa UE - zagrożenie dla polskiego drobiarstwa. Aktualizacja 2026.", SearchPriority.Critical),
                ("Cła antydumpingowe na drób UE 2026. Brazylia, Tajlandia, Ukraina.", SearchPriority.Important),
                ("Polska eksport mięsa drobiowego do UK po Brexicie 2026. Bariery, wolumeny.", SearchPriority.Standard),
                ("Ceny fileta mrożonego import Brazylia 2026. Ile kosztuje w Makro Selgros?", SearchPriority.Critical),  // Konkretna cena importu!

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 9: HODOWCY I FERMY
                // ═══════════════════════════════════════════════════════════════
                ("Ceny kontraktacji kurcząt brojlerów Polska 2026. Stawki dla hodowców od integratorów.", SearchPriority.Important),
                ("Problemy hodowców drobiu Polska 2026. Protesty, postulaty, negocjacje z ubojniami.", SearchPriority.Important),
                ("Nowe fermy drobiu w Polsce 2026. Inwestycje, lokalizacje, pozwolenia.", SearchPriority.Standard),
                ("Fermy drobiu łódzkie mazowieckie - lista producentów, kontraktacja.", SearchPriority.Important),
                ("Bioasekuracja ferm drobiu wymagania 2026. Koszty, kontrole weterynaryjne.", SearchPriority.Important),
                ("Dobrostan kurcząt brojlerów - nowe przepisy UE, wymogi od 2026.", SearchPriority.Important),
                ("Integratorzy drobiu Polska 2026. Kto kontraktuje hodowców? Warunki umów.", SearchPriority.Standard),

                // TRANSPORT ŻYWCA (dodane)
                ("Avilog transport żywca drobiu Polska 2026. Ceny, usługi.", SearchPriority.Important),
                ("Transport żywca drobiowego koszty Polska 2026. Stawki za km, firmy transportowe.", SearchPriority.Important),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 10: INSPEKCJA WETERYNARYJNA I REGULACJE
                // ═══════════════════════════════════════════════════════════════
                ("Inspekcja Weterynaryjna kontrole ubojni drobiu 2026. Wyniki, mandaty, zamknięcia.", SearchPriority.Important),
                ("Przepisy weterynaryjne ubojnie drobiu 2026. Nowe wymagania, zmiany w prawie.", SearchPriority.Important),
                ("KSeF faktury elektroniczne dla branży mięsnej spożywczej 2026. Terminy wdrożenia.", SearchPriority.Important),
                ("KSeF Sage Symfonia integracja ERP 2026. Wdrożenie, koszty, problemy.", SearchPriority.Critical),  // Konkretnie nasz system!
                ("PIW Brzeziny łódzkie inspekcja weterynaryjna 2026. Kontrole, decyzje.", SearchPriority.Important),
                ("Powiatowy Inspektorat Weterynarii łódzkie kontrole ferm ubojni 2026.", SearchPriority.Important),
                ("Znakowanie mięsa drobiowego przepisy UE 2026. Kraj pochodzenia, etykiety.", SearchPriority.Standard),
                ("HACCP wymagania zakłady mięsne przetwórstwo 2026. Audyty, certyfikaty.", SearchPriority.Standard),
                ("EFSA dobrostan zwierząt nowe normy drób brojlery 2026-2027.", SearchPriority.Important),
                ("Antybiotyki w hodowli drobiu - zakazy, ograniczenia, kontrole 2026.", SearchPriority.Important),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 11: RYNEK I TRENDY KONSUMENCKIE
                // ═══════════════════════════════════════════════════════════════
                ("Rynek mięsa drobiowego Polska 2026 - prognozy, analizy, raporty branżowe.", SearchPriority.Standard),
                ("Konsumpcja mięsa drobiowego per capita Polska 2026. Statystyki GUS.", SearchPriority.Standard),
                ("Produkcja drobiu Polska 2026 statystyki - ile ton rocznie? Ranking UE.", SearchPriority.Standard),
                ("Trendy - mięso bio, wolny wybieg, bez antybiotyków. Popyt w Polsce 2026.", SearchPriority.Standard),
                ("Wegetarianizm weganizm wpływ na rynek mięsa drobiowego 2026.", SearchPriority.Standard),
                ("Produkty drobiowe convenience - nuggetsy, stripsy, gotowe dania 2026.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 12: PRZETWÓRSTWO I TECHNOLOGIA
                // ═══════════════════════════════════════════════════════════════
                ("Przetwórstwo mięsa drobiowego Polska 2026. Zakłady, produkty, inwestycje.", SearchPriority.Standard),
                ("Automatyzacja ubojni drobiu - Marel, Meyn, Baader, Stork. Inwestycje 2026.", SearchPriority.Standard),
                ("Innowacje w przetwórstwie drobiu 2026. Nowe technologie, robotyzacja.", SearchPriority.Standard),
                ("Wędliny drobiowe Polska 2026. Producenci, trendy, sprzedaż.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 13: FINANSE I MAKROEKONOMIA
                // ═══════════════════════════════════════════════════════════════
                ("Kurs EUR PLN prognoza luty marzec 2026. Wpływ na eksport mięsa.", SearchPriority.Important),
                ("Inflacja żywność mięso Polska 2026. Wzrost cen, prognozy.", SearchPriority.Standard),
                ("Płaca minimalna 2026 Polska koszty pracy przetwórstwo mięsa.", SearchPriority.Standard),
                ("Kredyty dla firm mięsnych spożywczych 2026. Oprocentowanie, dostępność.", SearchPriority.Standard),
                ("Dotacje unijne przetwórstwo mięsa 2026. PROW, KPO, programy wsparcia.", SearchPriority.Standard),
                ("Ceny energii elektrycznej gazu dla przemysłu spożywczego 2026.", SearchPriority.Important),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 14: GASTRONOMIA I HORECA
                // ═══════════════════════════════════════════════════════════════
                ("Gastronomia Polska popyt na mięso drobiowe 2026. Restauracje, fast food.", SearchPriority.Standard),
                ("KFC McDonald's Burger King Polska dostawcy kurczaka 2026. Przetargi, umowy.", SearchPriority.Standard),
                ("Catering zbiorowe żywienie - drób, zamówienia publiczne 2026.", SearchPriority.Standard),

                // ═══════════════════════════════════════════════════════════════
                // GRUPA 15: LOKALNE - ŁÓDZKIE (TWÓJ REGION)
                // ═══════════════════════════════════════════════════════════════
                ("Branża mięsna drobiarska łódzkie 2026. Firmy, wydarzenia, inwestycje.", SearchPriority.Important),
                ("Ubojnie zakłady mięsne województwo łódzkie lista 2026.", SearchPriority.Important),
                ("Inwestycje przemysł spożywczy łódzkie mazowieckie 2026.", SearchPriority.Standard),
                ("Targi rolnicze spożywcze łódzkie mazowieckie 2026. Agrotech, Polagra.", SearchPriority.Standard)
            };

            return allQueries
                .Where(q => q.Priority <= maxPriority)
                .Select(q => q.Query)
                .ToList();
        }

        /// <summary>
        /// 25 zapytań międzynarodowych - globalne trendy i zagrożenia
        /// </summary>
        private List<string> GetInternationalQueries(SearchPriority maxPriority = SearchPriority.All)
        {
            var allQueries = new List<(string Query, SearchPriority Priority)>
            {
                // HPAI GLOBAL
                ("HPAI avian influenza Europe outbreaks February 2026. Which countries affected? Restrictions.", SearchPriority.Critical),
                ("Bird flu USA poultry farms 2026. Outbreaks, trade restrictions, impact.", SearchPriority.Important),
                ("HPAI Asia Pacific 2026. China, Japan, South Korea outbreaks and control measures.", SearchPriority.Standard),

                // GLOBAL TRADE - ZAGROŻENIA IMPORTOWE
                ("Brazil poultry exports EU 2026. BRF JBS Seara volumes prices market share.", SearchPriority.Critical),
                ("Ukraine poultry exports MHP 2026. EU market penetration, duty-free access.", SearchPriority.Critical),
                ("Thailand chicken exports 2026. CP Foods global expansion strategy.", SearchPriority.Important),
                ("Mercosur EU trade deal poultry chicken 2026. Tariff reductions timeline.", SearchPriority.Critical),
                ("Argentina chicken exports 2026. New markets, volumes.", SearchPriority.Standard),

                // GLOBAL PRODUCTION
                ("Global poultry production forecast 2026 USDA FAO. Growth projections.", SearchPriority.Important),
                ("EU poultry meat production 2026 statistics. Top producing countries ranking.", SearchPriority.Important),
                ("World chicken consumption trends 2026. Per capita by country.", SearchPriority.Standard),
                ("Poultry meat price index global 2026. FAO index, trends.", SearchPriority.Important),

                // FEED PRICES GLOBAL
                ("CBOT corn soybean futures prices February 2026. Outlook.", SearchPriority.Important),
                ("Global feed costs poultry production 2026. Impact on margins.", SearchPriority.Important),
                ("Brazil Argentina soybean harvest 2026. Crop estimates, impact on prices.", SearchPriority.Standard),

                // REGULATIONS EU
                ("EU animal welfare broiler chickens regulations 2026-2027. New requirements timeline.", SearchPriority.Important),
                ("European Green Deal impact poultry sector 2026. Sustainability requirements.", SearchPriority.Standard),
                ("EU ban on caged hens implementation 2026. Progress, exceptions.", SearchPriority.Standard),

                // MAJOR PLAYERS GLOBAL
                ("Tyson Foods news 2026. Strategy, financials, acquisitions.", SearchPriority.Important),
                ("JBS poultry division 2026. Pilgrim's Pride, global expansion.", SearchPriority.Important),
                ("CP Foods Charoen Pokphand global expansion 2026. Acquisitions, investments.", SearchPriority.Important),
                ("PHW Group Wiesenhof Germany poultry 2026. Strategy, production.", SearchPriority.Standard),
                ("2 Sisters Food Group UK chicken 2026. Production, contracts.", SearchPriority.Standard),

                // TECHNOLOGY
                ("Poultry processing automation Marel Meyn Baader 2026. New technologies.", SearchPriority.Standard),
                ("Cultured lab-grown chicken meat 2026. Commercial production progress.", SearchPriority.Standard)
            };

            return allQueries
                .Where(q => q.Priority <= maxPriority)
                .Select(q => q.Query)
                .ToList();
        }

        #endregion

        #region Conversion to RawArticle

        /// <summary>
        /// Konwertuj wyniki Perplexity do formatu RawArticle używanego w systemie
        /// </summary>
        public List<RawArticle> ConvertToRawArticles(PerplexityNewsSearchResult result)
        {
            var articles = new List<RawArticle>();

            foreach (var article in result.PolishNews.Concat(result.InternationalNews))
            {
                if (string.IsNullOrEmpty(article.Url)) continue;

                articles.Add(new RawArticle
                {
                    SourceId = "perplexity",
                    SourceName = $"Perplexity: {GetDomainFromUrl(article.Url)}",
                    SourceCategory = DetermineCategory(article),
                    Language = DetectLanguage(article),
                    Title = CleanTitle(article.Title),
                    Url = article.Url,
                    UrlHash = ComputeHash(article.Url),
                    Summary = article.Snippet ?? article.AiSummary,
                    FullContent = article.Content ?? article.AiSummary ?? article.Snippet,
                    PublishDate = article.PublishedDate ?? DateTime.Today,
                    FetchedAt = DateTime.Now,
                    IsRelevant = true,
                    RelevanceScore = (int)(article.Score * 30),
                    MatchedKeywords = ExtractKeywords(article)
                });
            }

            return articles;
        }

        #endregion

        #region Response Parsing

        private PerplexitySearchResult ParseResponse(PerplexityResponse response, string query)
        {
            var result = new PerplexitySearchResult
            {
                Success = true,
                Query = query,
                SearchedAt = DateTime.Now,
                Articles = new List<PerplexityArticle>()
            };

            if (response?.Choices?.Any() != true)
                return result;

            var content = response.Choices[0].Message?.Content ?? "";
            result.AiAnswer = content;

            // Wyciągnij cytowane źródła
            if (response.Citations?.Any() == true)
            {
                int citationIndex = 1;
                foreach (var citation in response.Citations.Distinct())
                {
                    if (string.IsNullOrEmpty(citation)) continue;

                    result.Articles.Add(new PerplexityArticle
                    {
                        Title = ExtractTitleFromContent(content, citationIndex) ?? ExtractTitleFromUrl(citation),
                        Url = citation,
                        Snippet = ExtractSnippetForCitation(content, citationIndex),
                        AiSummary = content,
                        Score = 0.8 - (citationIndex * 0.05), // Wyższy score dla wcześniejszych cytowań
                        PublishedDate = DateTime.Today
                    });
                    citationIndex++;
                }
            }

            return result;
        }

        private string GetSystemPrompt() => @"Jesteś ekspertem ds. branży drobiarskiej i mięsnej w Polsce.

ZADANIE: Wyszukaj i przedstaw NAJNOWSZE informacje na zadany temat.

FORMAT ODPOWIEDZI:
1. Podaj kluczowe FAKTY z każdego źródła:
   - Daty (kiedy?)
   - Liczby (ile? jakie ceny?)
   - Nazwy firm i osób (kto?)
   - Lokalizacje (gdzie?)

2. ZAWSZE cytuj źródła używając [1], [2], [3] itd.

3. Jeśli informacja dotyczy:
   - HPAI/choroby → podaj dokładną lokalizację, liczbę ognisk, strefy
   - Ceny → podaj konkretne stawki w zł/kg
   - Konkurencji → podaj nazwę firmy, typ wydarzenia, datę
   - Regulacji → podaj termin wejścia w życie

4. Odpowiadaj PO POLSKU (chyba że pytanie po angielsku)

5. Skup się na informacjach z ostatnich 7 dni

PRIORYTET ŹRÓDEŁ:
1. Oficjalne (gov.pl, wetgiw.gov.pl, minrol.gov.pl)
2. Branżowe (farmer.pl, portalspozywczy.pl, topagrar.pl)
3. Agencje prasowe (PAP, Reuters)
4. Media biznesowe (money.pl, bankier.pl)";

        #endregion

        #region Helpers

        private async Task RateLimitAsync(CancellationToken ct)
        {
            await _rateLimiter.WaitAsync(ct);
            try
            {
                var elapsed = DateTime.Now - _lastRequestTime;
                if (elapsed < TimeSpan.FromMilliseconds(600))
                    await Task.Delay(600 - (int)elapsed.TotalMilliseconds, ct);
                _lastRequestTime = DateTime.Now;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private List<PerplexityArticle> DeduplicateArticles(List<PerplexityArticle> articles)
        {
            return articles
                .Where(a => !string.IsNullOrEmpty(a.Url))
                .GroupBy(a => NormalizeUrl(a.Url))
                .Select(g => g.OrderByDescending(a => a.Score).First())
                .OrderByDescending(a => a.Score)
                .ToList();
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                var uri = new Uri(url);
                return $"{uri.Host}{uri.AbsolutePath}".ToLowerInvariant().TrimEnd('/');
            }
            catch
            {
                return url.ToLowerInvariant();
            }
        }

        private string ComputeHash(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
            return Convert.ToBase64String(bytes).Substring(0, 16);
        }

        private string GetDomainFromUrl(string url)
        {
            try
            {
                var host = new Uri(url).Host.Replace("www.", "");
                // Skróć długie domeny
                if (host.Length > 25)
                    host = host.Substring(0, 22) + "...";
                return host;
            }
            catch { return "unknown"; }
        }

        private string DetectLanguage(PerplexityArticle article)
        {
            var text = $"{article.Title} {article.Snippet}";
            var polishChars = text.Count(c => "ąęćłńóśźżĄĘĆŁŃÓŚŹŻ".Contains(c));
            return polishChars >= 2 ? "pl" : "en";
        }

        private string DetermineCategory(PerplexityArticle article)
        {
            var text = $"{article.Title} {article.Snippet}".ToLowerInvariant();

            if (text.Contains("hpai") || text.Contains("ptasia grypa") || text.Contains("avian") ||
                text.Contains("ognisko") || text.Contains("strefa zapowietrzona") || text.Contains("bird flu"))
                return "HPAI";

            if (text.Contains("cen") || text.Contains("zł") || text.Contains("price") ||
                text.Contains("notowania") || text.Contains("skup"))
                return "Ceny";

            if (text.Contains("cedrob") || text.Contains("superdrob") || text.Contains("adq") ||
                text.Contains("drosed") || text.Contains("animex") || text.Contains("wipasz") ||
                text.Contains("przejęci") || text.Contains("fuzj"))
                return "Konkurencja";

            if (text.Contains("eksport") || text.Contains("import") || text.Contains("brazylia") ||
                text.Contains("brazil") || text.Contains("ukraina") || text.Contains("ukraine") ||
                text.Contains("mercosur"))
                return "Handel";

            if (text.Contains("pasza") || text.Contains("kukurydz") || text.Contains("soja") ||
                text.Contains("matif") || text.Contains("feed") || text.Contains("zboż"))
                return "Pasze";

            if (text.Contains("biedronka") || text.Contains("lidl") || text.Contains("dino") ||
                text.Contains("kaufland") || text.Contains("promocj") || text.Contains("detal"))
                return "Retail";

            if (text.Contains("ksef") || text.Contains("regulac") || text.Contains("przepis") ||
                text.Contains("ustawa") || text.Contains("weterynary") || text.Contains("inspekcj"))
                return "Regulacje";

            if (text.Contains("hodowc") || text.Contains("ferm") || text.Contains("kontraktacj") ||
                text.Contains("integrator"))
                return "Hodowcy";

            return "Info";
        }

        private string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Bez tytułu";

            // Usuń znaki specjalne z początku
            title = title.TrimStart('-', '–', '—', '•', '*', ' ');

            // Ogranicz długość
            if (title.Length > 200)
                title = title.Substring(0, 197) + "...";

            return title;
        }

        private string ExtractTitleFromUrl(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                var slug = path.Split('/')
                    .LastOrDefault(s => !string.IsNullOrEmpty(s) && s.Length > 5) ?? "";

                // Zamień separatory na spacje
                slug = slug.Replace("-", " ").Replace("_", " ");

                // Usuń rozszerzenia plików
                if (slug.EndsWith(".html") || slug.EndsWith(".php") || slug.EndsWith(".aspx"))
                    slug = slug.Substring(0, slug.LastIndexOf('.'));

                // Kapitalizuj pierwszą literę
                if (slug.Length > 0)
                    slug = char.ToUpper(slug[0]) + slug.Substring(1);

                return slug.Length > 10 ? slug : GetDomainFromUrl(url);
            }
            catch { return GetDomainFromUrl(url); }
        }

        private string ExtractTitleFromContent(string content, int citationIndex)
        {
            // Szukaj tekstu przed cytatem [N]
            var marker = $"[{citationIndex}]";
            var index = content.IndexOf(marker);
            if (index <= 0) return null;

            // Weź tekst przed markerem (max 200 znaków)
            var before = content.Substring(Math.Max(0, index - 200), Math.Min(200, index));

            // Znajdź ostatnie zdanie lub nagłówek
            var lastPeriod = before.LastIndexOf('.');
            var lastNewline = before.LastIndexOf('\n');
            var start = Math.Max(lastPeriod, lastNewline) + 1;

            var title = before.Substring(start).Trim();

            // Zwróć tylko jeśli wygląda jak tytuł
            return title.Length >= 10 && title.Length <= 150 ? title : null;
        }

        private string ExtractSnippetForCitation(string content, int citationIndex)
        {
            var marker = $"[{citationIndex}]";
            var index = content.IndexOf(marker);
            if (index < 0) return content.Length > 300 ? content.Substring(0, 300) + "..." : content;

            // Weź tekst wokół cytatu
            var start = Math.Max(0, index - 150);
            var length = Math.Min(350, content.Length - start);
            var snippet = content.Substring(start, length);

            // Oczyść
            if (start > 0) snippet = "..." + snippet;
            if (start + length < content.Length) snippet = snippet + "...";

            return snippet.Trim();
        }

        private string[] ExtractKeywords(PerplexityArticle article)
        {
            var keywords = new List<string>();
            var text = $"{article.Title} {article.Snippet}".ToLowerInvariant();

            var keywordMap = new Dictionary<string, string>
            {
                { "hpai", "HPAI" }, { "ptasia grypa", "HPAI" }, { "bird flu", "HPAI" },
                { "cedrob", "Cedrob" }, { "superdrob", "SuperDrob" }, { "drosed", "Drosed" },
                { "animex", "Animex" }, { "wipasz", "Wipasz" }, { "adq", "ADQ" },
                { "biedronka", "Biedronka" }, { "lidl", "Lidl" }, { "dino", "Dino" },
                { "brazylia", "Brazylia" }, { "brazil", "Brazylia" },
                { "ukraina", "Ukraina" }, { "ukraine", "Ukraina" },
                { "ceny", "Ceny" }, { "cena", "Ceny" }, { "price", "Ceny" },
                { "import", "Import" }, { "eksport", "Eksport" }, { "export", "Eksport" },
                { "kukurydza", "Pasze" }, { "soja", "Pasze" }, { "pasza", "Pasze" },
                { "kurczak", "Drób" }, { "brojler", "Drób" }, { "indyk", "Indyk" }
            };

            foreach (var kv in keywordMap)
            {
                if (text.Contains(kv.Key) && !keywords.Contains(kv.Value))
                    keywords.Add(kv.Value);
            }

            return keywords.Take(5).ToArray();
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }

    #region Enums

    public enum SearchPriority
    {
        Critical = 1,    // Codziennie, kilka razy dziennie
        Important = 2,   // Codziennie lub co 2 dni
        Standard = 3,    // 2-3 razy w tygodniu
        All = 10         // Wszystkie
    }

    #endregion

    #region Models

    public class PerplexitySearchOptions
    {
        public bool UseProModel { get; set; } = false;
        public int MaxTokens { get; set; } = 2000;
        public string RecencyFilter { get; set; } = "week"; // hour, day, week, month

        public static PerplexitySearchOptions Default => new();
        public static PerplexitySearchOptions Pro => new() { UseProModel = true, MaxTokens = 4000 };
    }

    public class PerplexityRequest
    {
        public string Model { get; set; }
        public List<PerplexityMessage> Messages { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        public double Temperature { get; set; }

        [JsonPropertyName("search_recency_filter")]
        public string SearchRecencyFilter { get; set; }

        [JsonPropertyName("return_citations")]
        public bool ReturnCitations { get; set; }

        [JsonPropertyName("return_related_questions")]
        public bool ReturnRelatedQuestions { get; set; }
    }

    public class PerplexityMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class PerplexityResponse
    {
        public string Id { get; set; }
        public string Model { get; set; }
        public List<PerplexityChoice> Choices { get; set; }
        public List<string> Citations { get; set; }
    }

    public class PerplexityChoice
    {
        public int Index { get; set; }
        public PerplexityMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    public class PerplexitySearchResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Query { get; set; }
        public string AiAnswer { get; set; }
        public DateTime SearchedAt { get; set; }
        public List<PerplexityArticle> Articles { get; set; } = new();
    }

    public class PerplexityArticle
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Snippet { get; set; }
        public string Content { get; set; }
        public string AiSummary { get; set; }
        public double Score { get; set; }
        public DateTime? PublishedDate { get; set; }
    }

    public class PerplexityNewsSearchResult
    {
        public List<PerplexityArticle> PolishNews { get; set; } = new();
        public List<PerplexityArticle> InternationalNews { get; set; } = new();
        public List<PerplexityAiSummary> AiSummaries { get; set; } = new();
        public int TotalArticles { get; set; }
        public DateTime SearchedAt { get; set; }
    }

    public class PerplexityAiSummary
    {
        public string Topic { get; set; }
        public string Summary { get; set; }
    }

    #endregion
}
