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

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do przeszukiwania całego internetu przez Tavily API
    /// Tavily jest zoptymalizowany dla agentów AI - zwraca czyste, relevantne wyniki
    ///
    /// ROZSZERZONY: 214 zapytań w 13 kategoriach z systemem priorytetów i rotacji
    /// </summary>
    public class TavilySearchService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string TavilyApiUrl = "https://api.tavily.com/search";

        // Rate limiting
        private readonly SemaphoreSlim _rateLimiter;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _minRequestInterval = TimeSpan.FromMilliseconds(500);

        public TavilySearchService(string apiKey = null)
        {
            _apiKey = apiKey
                ?? ConfigurationManager.AppSettings["TavilyApiKey"]
                ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY");

            if (!string.IsNullOrEmpty(_apiKey))
            {
                Debug.WriteLine($"[Tavily] API key configured: {_apiKey.Substring(0, Math.Min(15, _apiKey.Length))}...");
            }
            else
            {
                Debug.WriteLine("[Tavily] WARNING: No API key found!");
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            _rateLimiter = new SemaphoreSlim(1);
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        #region === KATEGORIE ZAPYTAŃ (214 ZAPYTAŃ) ===

        /// <summary>
        /// KATEGORIA 1: HPAI / PTASIA GRYPA (najwyższy priorytet)
        /// 15 zapytań - krytyczne dla bezpieczeństwa firmy
        /// </summary>
        public static class HpaiQueries
        {
            public static readonly string[] Polish = new[]
            {
                "HPAI ptasia grypa Polska 2026 ogniska",
                "ptasia grypa województwo łódzkie 2026",
                "ptasia grypa strefy restriction Polska",
                "GLW Główny Lekarz Weterynarii komunikat HPAI",
                "PIW Brzeziny ptasia grypa",
                "likwidacja stad drobiu Polska odszkodowania",
                "bioasekuracja fermy drobiu wymagania",
                "ptasia grypa Europa EFSA raport 2026",
                "HPAI H5N1 Francja Holandia Niemcy ogniska",
                "ptasia grypa transport drobiu zakaz przemieszczania"
            };

            public static readonly string[] English = new[]
            {
                "HPAI avian influenza Poland 2026 outbreaks",
                "avian influenza Europe EFSA 2026 report",
                "H5N1 poultry outbreaks EU January February 2026",
                "bird flu impact poultry industry Europe",
                "HPAI vaccination poultry EU policy 2026"
            };
        }

        /// <summary>
        /// KATEGORIA 2: CENY SKUPU / ELEMENTÓW / TUSZKI (14 zapytań)
        /// </summary>
        public static class PriceQueries
        {
            public static readonly string[] Polish = new[]
            {
                "ceny skupu żywca drobiowego Polska 2026",
                "cena kurczaka żywiec wolny rynek",
                "ceny skupu drobiu kontraktacja luty 2026",
                "cena tuszki kurczaka hurt Polska",
                "ceny filet z piersi kurczaka Polska hurt",
                "ceny elementów drobiowych udko skrzydło",
                "ceny drobiu giełda Poznań Łódź",
                "ceny mięsa drobiowego prognoza",
                "e-drob notowania cen drobiu",
                "KRD-IG Krajowa Rada Drobiarstwa ceny"
            };

            public static readonly string[] English = new[]
            {
                "Poland chicken wholesale price 2026",
                "EU poultry meat prices DG AGRI",
                "European Commission poultry market report",
                "broiler chicken price Europe per kg"
            };
        }

        /// <summary>
        /// KATEGORIA 3: KONKURENCJA — POLSKIE UBOJNIE (32 zapytania)
        /// Rotowane - nie wszystkie na raz
        /// </summary>
        public static class CompetitorQueries
        {
            // TOP 3 - zawsze sprawdzane
            public static readonly string[] Top3 = new[]
            {
                "Cedrob przejęcie ADQ Abu Dhabi 2026",
                "Cedrob SA wyniki finansowe inwestycje",
                "Cedrob Ujazdówek rozbudowa moce produkcyjne",
                "SuperDrob LipCo Foods inwestycje ekspansja",
                "SuperDrob Zbigniew Jagiełło rada nadzorcza",
                "SuperDrob CPF Tajlandia współpraca",
                "Drosed LDC Group Siedlce",
                "Drosed ADQ przejęcie monopol"
            };

            // Grupa A - poniedziałek, środa
            public static readonly string[] GroupA = new[]
            {
                "Animex Foods WH Group Polska",
                "Animex wyniki finansowe drób",
                "Drobimex PHW Wiesenhof Szczecin",
                "Drobimex Grupa PHW Polska"
            };

            // Grupa B - wtorek, czwartek
            public static readonly string[] GroupB = new[]
            {
                "Wipasz Olsztyn drób pasza integracja",
                "Wipasz SA wyniki inwestycje",
                "Gobarto Cedrob grupa kapitałowa",
                "Indykpol LDC Olsztyn indyki"
            };

            // Grupa C - piątek
            public static readonly string[] GroupC = new[]
            {
                "Plukon Food Group Polska Europa",
                "Plukon przejęcia ekspansja drób",
                "Roldrob Ostrów Wielkopolski",
                "System-Drob produkcja eksport",
                "Drobex przedsiębiorstwo drobiarskie"
            };

            // Ogólne - raz w tygodniu
            public static readonly string[] General = new[]
            {
                "konsolidacja rynku drobiarskiego Polska 2026",
                "ranking największych producentów drobiu Polska",
                "polskie ubojnie drobiu przejęcia fuzje"
            };

            public static readonly string[] English = new[]
            {
                "ADQ Abu Dhabi Cedrob acquisition Poland 2026",
                "ADQ LDC Drosed Poland poultry monopoly",
                "SuperDrob LipCo CPF Thailand expansion",
                "Plukon Food Group Poland acquisitions",
                "PHW Wiesenhof Drobimex Poland",
                "WH Group Animex Poland poultry",
                "Poland poultry industry consolidation M&A",
                "largest poultry producers Poland ranking"
            };
        }

        /// <summary>
        /// KATEGORIA 4: IMPORT / EKSPORT (20 zapytań)
        /// </summary>
        public static class TradeQueries
        {
            public static readonly string[] Polish = new[]
            {
                "import drobiu do Polski Brazylia Ukraina 2026",
                "Mercosur umowa handlowa drób kwoty",
                "import filet mrożony Brazylia cena Polska",
                "Ukraina eksport drobiu do UE MHP",
                "MHP Myronivsky Hliboproduct eksport Polska",
                "BRF JBS eksport drobiu Europa",
                "ceny drobiu import vs krajowy Polska",
                "eksport polskiego drobiu 2026 kierunki",
                "Polska eksport mięsa drobiowego statystyki",
                "antydumping drób import UE",
                "cło drób Brazylia UE Mercosur"
            };

            public static readonly string[] English = new[]
            {
                "Brazil poultry export EU 2026 Mercosur",
                "Mercosur EU trade deal poultry quota 180000 tons",
                "Ukraine poultry export EU duty free MHP",
                "Thailand CPF poultry export Europe",
                "Brazil frozen chicken breast price Europe",
                "EU poultry import statistics 2026",
                "Poland poultry export markets",
                "global poultry trade forecast 2026",
                "BRF JBS Marfrig poultry export EU"
            };
        }

        /// <summary>
        /// KATEGORIA 5: PASZE / MATIF (18 zapytań)
        /// </summary>
        public static class FeedQueries
        {
            public static readonly string[] Polish = new[]
            {
                "ceny pasz drobiowych Polska 2026",
                "kukurydza cena Polska giełda MATIF",
                "pszenica cena Polska prognoza",
                "soja śruta sojowa cena import",
                "rzepak cena MATIF Euronext",
                "ceny zbóż Polska luty 2026",
                "koszt produkcji kurczaka Polska pasza",
                "relacja cena żywca do paszy drobiowej",
                "zbiory kukurydzy Polska prognoza 2026",
                "import soi do Polski Argentyna Brazylia"
            };

            public static readonly string[] English = new[]
            {
                "MATIF corn price February 2026",
                "wheat futures Euronext 2026",
                "soybean meal price Europe import",
                "rapeseed MATIF price forecast",
                "feed cost broiler production Europe",
                "corn harvest forecast Europe 2026",
                "global grain market outlook 2026",
                "USDA WASDE grain report latest"
            };
        }

        /// <summary>
        /// KATEGORIA 6: REGULACJE / KSeF / UE (17 zapytań)
        /// </summary>
        public static class RegulationQueries
        {
            public static readonly string[] Polish = new[]
            {
                "KSeF Krajowy System e-Faktur obowiązkowy 2026",
                "KSeF termin wdrożenia kwiecień 2026",
                "KSeF integracja Sage Symfonia",
                "nowe przepisy weterynaryjne ubojnie drobiu 2026",
                "IFS BRC certyfikacja ubojnia drobiu wymagania",
                "UOKiK kontrola koncentracji rynek drobiu",
                "dobrostan drobiu nowe normy UE 2027",
                "inspekcja weterynaryjna kontrole ubojnie",
                "przepisy sanitarne ubojnie drobiu Polska",
                "IJHARS kontrole jakość mięso drobiowe",
                "dotacje ARiMR modernizacja ubojni 2026",
                "fundusze unijne przetwórstwo mięsa Polska"
            };

            public static readonly string[] English = new[]
            {
                "EU animal welfare legislation poultry 2027",
                "EU poultry slaughter regulations new",
                "European Commission farm to fork poultry",
                "IFS BRC certification poultry processing",
                "EU green deal impact poultry industry"
            };
        }

        /// <summary>
        /// KATEGORIA 7: SIECI HANDLOWE / KLIENCI (26 zapytań)
        /// </summary>
        public static class RetailQueries
        {
            public static readonly string[] TopRetailers = new[]
            {
                "Dino Polska nowe sklepy 2026 ekspansja",
                "Biedronka ceny mięsa drobiowego promocja",
                "Lidl Polska dostawcy mięso drobiowe",
                "Kaufland Polska lada mięsna dostawcy",
                "Netto Polska strategia ceny"
            };

            public static readonly string[] MediumRetailers = new[]
            {
                "Stokrotka rozwój sklepy 2026",
                "Polomarket wyniki sprzedaż",
                "Chata Polska sklepy łódzkie wielkopolskie",
                "Chorten sieć sklepów rozwój 2026",
                "Lewiatan Sklepy Stąd franczyza 2026",
                "Top Market rozwój sklepy",
                "Społem Łódź współpraca dostawcy"
            };

            public static readonly string[] Hypermarkets = new[]
            {
                "Carrefour Polska strategia dostawcy lokalni",
                "E.Leclerc Polska",
                "Makro Cash Carry Polska dostawcy drób",
                "Selgros Polska",
                "Auchan Intermarche Polska 2026"
            };

            public static readonly string[] MarketTrends = new[]
            {
                "sieci handlowe Polska ceny mięsa kurczak",
                "dyskontyzacja rynku spożywczego Polska",
                "marża sieci handlowych na mięsie",
                "private label mięso drobiowe Polska sieci",
                "Eurocash Delikatesy Centrum drób dostawcy"
            };

            public static readonly string[] English = new[]
            {
                "Dino Polska expansion 2026 new stores",
                "Poland grocery retail market 2026",
                "Biedronka Jeronimo Martins Poland strategy",
                "discount retail Poland growth"
            };
        }

        /// <summary>
        /// KATEGORIA 8: POGODA / TRANSPORT (11 zapytań)
        /// </summary>
        public static class WeatherTransportQueries
        {
            public static readonly string[] Polish = new[]
            {
                "prognoza pogody Polska mrozy luty 2026",
                "bestia ze wschodu mrozy Polska 2026",
                "mrozy wpływ transport żywności",
                "warunki drogowe zima Polska utrudnienia",
                "koszty transportu chłodniczego Polska",
                "ceny paliwa diesel Polska 2026",
                "Avilog transport żywca drobiu",
                "logistyka chłodnicza Polska koszty"
            };

            public static readonly string[] English = new[]
            {
                "cold wave Eastern Europe February 2026",
                "extreme cold impact poultry transport",
                "diesel fuel price Europe 2026"
            };
        }

        /// <summary>
        /// KATEGORIA 9: GLOBALNA PRODUKCJA (14 zapytań)
        /// </summary>
        public static class GlobalProductionQueries
        {
            public static readonly string[] Polish = new[]
            {
                "produkcja drobiu świat prognoza 2026",
                "światowe ceny kurczaka trend",
                "popyt na drób globalny wzrost"
            };

            public static readonly string[] English = new[]
            {
                "global poultry production forecast 2026 USDA",
                "world chicken meat production statistics",
                "USA poultry production record 2026",
                "Brazil poultry production export 2026",
                "Thailand CP Foods poultry production",
                "China poultry market 2026",
                "India poultry industry growth",
                "EU poultry production forecast 2026",
                "global poultry market trends",
                "poultry consumption per capita Europe",
                "OECD FAO agricultural outlook poultry"
            };
        }

        /// <summary>
        /// KATEGORIA 10: TECHNOLOGIA / AUTOMATYZACJA (12 zapytań)
        /// </summary>
        public static class TechnologyQueries
        {
            public static readonly string[] Polish = new[]
            {
                "automatyzacja ubojni drobiu roboty",
                "nowoczesna linia ubojowa drób technologia",
                "Marel Meyn linia ubojowa drób",
                "oprogramowanie zarządzanie ubojnią ERP",
                "IoT monitoring fermy drobiu czujniki",
                "sztuczna inteligencja AI branża mięsna",
                "traceability śledzialność mięso drobiowe QR"
            };

            public static readonly string[] English = new[]
            {
                "poultry processing automation technology 2026",
                "Marel Meyn poultry equipment innovation",
                "AI artificial intelligence poultry industry",
                "poultry traceability blockchain technology",
                "robotic deboning chicken processing"
            };
        }

        /// <summary>
        /// KATEGORIA 11: DOBROSTAN / ESG (13 zapytań)
        /// </summary>
        public static class WelfareEsgQueries
        {
            public static readonly string[] Polish = new[]
            {
                "dobrostan drobiu kurczaki Polska przepisy",
                "Europejski Trybunał Obrachunkowy dobrostan drobiu",
                "kurczaki wolnowybiegowe bio Polska rynek",
                "ślad węglowy produkcja drobiu",
                "ESG raportowanie przemysł mięsny",
                "organizacje prozwierzęce kampanie drób Polska",
                "Compassion in World Farming Polska drób"
            };

            public static readonly string[] English = new[]
            {
                "EU animal welfare broiler chicken new rules 2027",
                "European Citizens Initiative end cage age poultry",
                "poultry welfare standards EU legislation",
                "ESG reporting meat industry Europe",
                "carbon footprint chicken production",
                "Better Chicken Commitment Europe retailers"
            };
        }

        /// <summary>
        /// KATEGORIA 12: FINANSE / MAKROEKONOMIA (12 zapytań)
        /// </summary>
        public static class FinanceQueries
        {
            public static readonly string[] Polish = new[]
            {
                "kurs EUR PLN prognoza 2026",
                "stopy procentowe NBP 2026 decyzja",
                "inflacja Polska żywność mięso 2026",
                "PKB Polska konsumpcja żywności",
                "ceny energii elektrycznej przemysł Polska 2026",
                "koszty pracy Polska wzrost płac 2026",
                "płaca minimalna 2026 Polska",
                "kredyty inwestycyjne rolnictwo przetwórstwo"
            };

            public static readonly string[] English = new[]
            {
                "EUR PLN exchange rate forecast 2026",
                "Poland economy outlook 2026",
                "energy costs food industry Europe",
                "labor costs Poland manufacturing 2026"
            };
        }

        /// <summary>
        /// KATEGORIA 13: ZDROWIE / BEZPIECZEŃSTWO ŻYWNOŚCI (10 zapytań)
        /// </summary>
        public static class FoodSafetyQueries
        {
            public static readonly string[] Polish = new[]
            {
                "salmonella drób Polska kontrole RASFF",
                "bezpieczeństwo żywności mięso drobiowe Polska",
                "RASFF alert Polska drób",
                "antybiotyki w drobiu Polska zakaz",
                "kontrole weterynaryjne ubojnie wyniki",
                "listeria campylobacter drób badania"
            };

            public static readonly string[] English = new[]
            {
                "RASFF alerts poultry Poland 2026",
                "salmonella poultry EU regulations",
                "antimicrobial resistance poultry Europe",
                "food safety chicken meat EU standards"
            };
        }

        #endregion

        #region === SYSTEM PRIORYTETÓW ===

        public enum SearchPriority
        {
            Critical,      // Każde odświeżenie
            Daily,         // Raz dziennie rano
            BiWeekly,      // 2x w tygodniu
            Weekly         // Raz w tygodniu
        }

        /// <summary>
        /// Pobierz zapytania według priorytetu
        /// </summary>
        public List<string> GetQueriesByPriority(SearchPriority priority, DayOfWeek? dayOfWeek = null)
        {
            var queries = new List<string>();
            var today = dayOfWeek ?? DateTime.Now.DayOfWeek;

            switch (priority)
            {
                case SearchPriority.Critical:
                    // HPAI - kluczowe
                    queries.AddRange(HpaiQueries.Polish.Take(5));
                    queries.AddRange(HpaiQueries.English.Take(2));
                    // Ceny - kluczowe
                    queries.AddRange(PriceQueries.Polish.Take(5));
                    queries.Add(PriceQueries.English[0]); // Poland chicken wholesale
                    // Konkurencja TOP3
                    queries.AddRange(CompetitorQueries.Top3.Take(4));
                    queries.AddRange(CompetitorQueries.English.Take(2));
                    // Import zagrożenia
                    queries.AddRange(TradeQueries.Polish.Take(3));
                    queries.Add(TradeQueries.English[0]); // Brazil export
                    // Pogoda ekstremalna
                    queries.AddRange(WeatherTransportQueries.Polish.Take(2));
                    break;

                case SearchPriority.Daily:
                    // Pasze MATIF
                    queries.AddRange(FeedQueries.Polish.Take(3));
                    queries.Add(FeedQueries.English[0]); // MATIF corn
                    // Sieci - nowi klienci
                    queries.AddRange(RetailQueries.TopRetailers.Take(3));
                    queries.Add(RetailQueries.English[0]); // Dino expansion
                    // Regulacje KSeF
                    queries.AddRange(RegulationQueries.Polish.Take(3));
                    // Finanse EUR/PLN
                    queries.AddRange(FinanceQueries.Polish.Take(2));
                    break;

                case SearchPriority.BiWeekly:
                    // Konkurencja rotowana
                    queries.AddRange(GetRotatingCompetitorQueries(today));
                    // Globalna produkcja
                    queries.AddRange(GlobalProductionQueries.English.Take(5));
                    // Technologia
                    queries.AddRange(TechnologyQueries.Polish.Take(3));
                    break;

                case SearchPriority.Weekly:
                    // Dobrostan/ESG
                    queries.AddRange(WelfareEsgQueries.Polish.Take(3));
                    queries.AddRange(WelfareEsgQueries.English.Take(2));
                    // Zdrowie/bezpieczeństwo
                    queries.AddRange(FoodSafetyQueries.Polish.Take(3));
                    queries.Add(FoodSafetyQueries.English[0]); // RASFF
                    // Sieci szczegółowe
                    queries.AddRange(RetailQueries.MediumRetailers.Take(4));
                    // Konkurencja ogólna
                    queries.AddRange(CompetitorQueries.General);
                    break;
            }

            return queries;
        }

        /// <summary>
        /// Rotacja zapytań o konkurencji według dnia tygodnia
        /// </summary>
        private List<string> GetRotatingCompetitorQueries(DayOfWeek day)
        {
            var queries = new List<string>();

            // TOP3 zawsze
            queries.AddRange(CompetitorQueries.Top3.Take(3));

            switch (day)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Wednesday:
                    queries.AddRange(CompetitorQueries.GroupA);
                    break;
                case DayOfWeek.Tuesday:
                case DayOfWeek.Thursday:
                    queries.AddRange(CompetitorQueries.GroupB);
                    break;
                case DayOfWeek.Friday:
                    queries.AddRange(CompetitorQueries.GroupC);
                    // Piątek = też import międzynarodowy
                    queries.Add(TradeQueries.English[3]); // Thailand CPF
                    queries.Add(TradeQueries.English[2]); // Ukraine MHP
                    break;
                default:
                    // Weekend = minimum
                    break;
            }

            return queries;
        }

        #endregion

        #region === GŁÓWNE METODY WYSZUKIWANIA ===

        /// <summary>
        /// Wyszukaj w internecie z dowolnym zapytaniem
        /// </summary>
        public async Task<TavilySearchResult> SearchAsync(
            string query,
            SearchOptions options = null,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                Debug.WriteLine("[Tavily] API key not configured");
                return new TavilySearchResult { Success = false, Error = "API key not configured" };
            }

            options ??= SearchOptions.Default;

            await _rateLimiter.WaitAsync(ct);
            try
            {
                // Rate limiting
                var elapsed = DateTime.Now - _lastRequestTime;
                if (elapsed < _minRequestInterval)
                {
                    await Task.Delay(_minRequestInterval - elapsed, ct);
                }

                var request = new TavilyRequest
                {
                    ApiKey = _apiKey,
                    Query = query,
                    SearchDepth = options.Deep ? "advanced" : "basic",
                    IncludeAnswer = options.IncludeAiAnswer,
                    IncludeRawContent = options.IncludeRawContent,
                    MaxResults = options.MaxResults,
                    IncludeDomains = options.IncludeDomains,
                    ExcludeDomains = options.ExcludeDomains
                };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                Debug.WriteLine($"[Tavily] Searching: {query}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(TavilyApiUrl, content, ct);

                _lastRequestTime = DateTime.Now;

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    Debug.WriteLine($"[Tavily] Error {(int)response.StatusCode}: {error}");
                    return new TavilySearchResult
                    {
                        Success = false,
                        Error = $"HTTP {(int)response.StatusCode}: {error}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<TavilyResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Debug.WriteLine($"[Tavily] Found {result?.Results?.Count ?? 0} results");

                return new TavilySearchResult
                {
                    Success = true,
                    Query = query,
                    AiAnswer = result?.Answer,
                    Results = result?.Results?.Select(r => new TavilyArticle
                    {
                        Title = r.Title,
                        Url = r.Url,
                        Content = r.Content,
                        RawContent = r.RawContent,
                        Score = r.Score,
                        PublishedDate = r.PublishedDate
                    }).ToList() ?? new List<TavilyArticle>(),
                    SearchedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tavily] Exception: {ex.Message}");
                return new TavilySearchResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        /// <summary>
        /// Wyszukaj wiele zapytań równolegle
        /// </summary>
        public async Task<List<TavilySearchResult>> SearchMultipleAsync(
            IEnumerable<string> queries,
            SearchOptions options = null,
            CancellationToken ct = default)
        {
            var results = new List<TavilySearchResult>();

            foreach (var query in queries)
            {
                ct.ThrowIfCancellationRequested();
                var result = await SearchAsync(query, options, ct);
                results.Add(result);

                // Small delay between requests
                await Task.Delay(300, ct);
            }

            return results;
        }

        /// <summary>
        /// GŁÓWNA METODA: Wykonaj pełne wyszukiwanie newsów dla branży drobiarskiej
        /// Używa systemu priorytetów - Critical zawsze, Daily/BiWeekly/Weekly według harmonogramu
        /// </summary>
        public async Task<PoultryNewsSearchResult> SearchPoultryNewsAsync(
            bool includeInternational = true,
            SearchPriority maxPriority = SearchPriority.Daily,
            CancellationToken ct = default)
        {
            var result = new PoultryNewsSearchResult
            {
                SearchedAt = DateTime.Now
            };

            var options = new SearchOptions
            {
                MaxResults = 5,
                IncludeAiAnswer = true,
                Deep = false
            };

            var allQueries = new List<string>();

            // ZAWSZE: Critical queries
            allQueries.AddRange(GetQueriesByPriority(SearchPriority.Critical));

            // Daily (jeśli włączone)
            if (maxPriority >= SearchPriority.Daily)
            {
                allQueries.AddRange(GetQueriesByPriority(SearchPriority.Daily));
            }

            // BiWeekly (tylko pon/śr/pt)
            var today = DateTime.Now.DayOfWeek;
            if (maxPriority >= SearchPriority.BiWeekly &&
                (today == DayOfWeek.Monday || today == DayOfWeek.Wednesday || today == DayOfWeek.Friday))
            {
                allQueries.AddRange(GetQueriesByPriority(SearchPriority.BiWeekly));
            }

            // Weekly (tylko poniedziałek)
            if (maxPriority >= SearchPriority.Weekly && today == DayOfWeek.Monday)
            {
                allQueries.AddRange(GetQueriesByPriority(SearchPriority.Weekly));
            }

            // Usuń duplikaty
            allQueries = allQueries.Distinct().ToList();

            Debug.WriteLine($"[Tavily] Starting poultry search with {allQueries.Count} queries");

            foreach (var query in allQueries)
            {
                ct.ThrowIfCancellationRequested();
                var searchResult = await SearchAsync(query, options, ct);

                if (searchResult.Success)
                {
                    // Klasyfikuj artykuły
                    foreach (var article in searchResult.Results)
                    {
                        var lang = DetectLanguage(article.Title + " " + article.Content);
                        if (lang == "pl")
                            result.PolishNews.Add(article);
                        else
                            result.InternationalNews.Add(article);
                    }

                    if (!string.IsNullOrEmpty(searchResult.AiAnswer))
                    {
                        result.AiSummaries.Add(new AiSummary
                        {
                            Topic = query,
                            Summary = searchResult.AiAnswer
                        });
                    }
                }
            }

            // Deduplikacja zaawansowana
            result.PolishNews = DeduplicateArticles(result.PolishNews);
            result.InternationalNews = DeduplicateArticles(result.InternationalNews);

            result.TotalArticles = result.PolishNews.Count + result.InternationalNews.Count;

            Debug.WriteLine($"[Tavily] Poultry search complete: {result.TotalArticles} unique articles, {result.AiSummaries.Count} AI summaries");

            return result;
        }

        /// <summary>
        /// Szybkie wyszukiwanie - tylko CRITICAL queries
        /// </summary>
        public async Task<PoultryNewsSearchResult> QuickSearchAsync(CancellationToken ct = default)
        {
            return await SearchPoultryNewsAsync(
                includeInternational: true,
                maxPriority: SearchPriority.Critical,
                ct: ct);
        }

        /// <summary>
        /// Pełne dzienne wyszukiwanie
        /// </summary>
        public async Task<PoultryNewsSearchResult> FullDailySearchAsync(CancellationToken ct = default)
        {
            return await SearchPoultryNewsAsync(
                includeInternational: true,
                maxPriority: SearchPriority.Weekly, // Wszystkie priorytety
                ct: ct);
        }

        /// <summary>
        /// Szybkie sprawdzenie alertów HPAI
        /// </summary>
        public async Task<TavilySearchResult> SearchHpaiAlertsAsync(CancellationToken ct = default)
        {
            var queries = new[]
            {
                "HPAI ptasia grypa Polska 2026 ogniska alert",
                "ptasia grypa łódzkie wielkopolskie mazowieckie",
                "GLW komunikat HPAI strefy ochronne"
            };

            var allResults = new List<TavilyArticle>();
            string aiAnswer = null;

            foreach (var query in queries)
            {
                var result = await SearchAsync(query, new SearchOptions
                {
                    MaxResults = 5,
                    IncludeAiAnswer = true,
                    Deep = true
                }, ct);

                if (result.Success)
                {
                    allResults.AddRange(result.Results);
                    if (aiAnswer == null && !string.IsNullOrEmpty(result.AiAnswer))
                        aiAnswer = result.AiAnswer;
                }
            }

            return new TavilySearchResult
            {
                Success = true,
                Query = "HPAI Alerts Combined",
                AiAnswer = aiAnswer,
                Results = DeduplicateArticles(allResults),
                SearchedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Wyszukaj informacje o konkurencie
        /// </summary>
        public async Task<TavilySearchResult> SearchCompetitorNewsAsync(
            string competitorName,
            CancellationToken ct = default)
        {
            return await SearchAsync(
                $"{competitorName} drób ubojnia aktualności inwestycje przejęcie 2026",
                new SearchOptions
                {
                    MaxResults = 5,
                    IncludeAiAnswer = true
                },
                ct);
        }

        /// <summary>
        /// Wyszukaj aktualne ceny
        /// </summary>
        public async Task<TavilySearchResult> SearchCurrentPricesAsync(CancellationToken ct = default)
        {
            var queries = new[]
            {
                "ceny skupu żywca drobiowego Polska luty 2026",
                "ceny tuszki kurczaka hurt Polska",
                "ceny filet pierś kurczaka Polska"
            };

            var allResults = new List<TavilyArticle>();

            foreach (var query in queries)
            {
                var result = await SearchAsync(query, new SearchOptions
                {
                    MaxResults = 5,
                    IncludeAiAnswer = true,
                    Deep = true
                }, ct);

                if (result.Success)
                    allResults.AddRange(result.Results);
            }

            return new TavilySearchResult
            {
                Success = true,
                Query = "Current Prices Combined",
                Results = DeduplicateArticles(allResults),
                SearchedAt = DateTime.Now
            };
        }

        #endregion

        #region === DEDUPLIKACJA ===

        /// <summary>
        /// Zaawansowana deduplikacja artykułów
        /// Usuwa duplikaty po URL i podobieństwa tytułów
        /// </summary>
        private List<TavilyArticle> DeduplicateArticles(List<TavilyArticle> articles)
        {
            if (articles == null || !articles.Any())
                return new List<TavilyArticle>();

            var unique = new List<TavilyArticle>();
            var seenUrls = new HashSet<string>();
            var seenTitles = new List<string>();

            // Preferowane źródła (wyższy priorytet)
            var sourcePriority = new Dictionary<string, int>
            {
                { "wetgiw.gov.pl", 100 },
                { "gov.pl", 95 },
                { "farmer.pl", 90 },
                { "topagrar.pl", 85 },
                { "portalspozywczy.pl", 80 },
                { "ppr.pl", 75 },
                { "reuters.com", 70 },
                { "bloomberg.com", 68 },
                { "efsa.europa.eu", 65 },
                { "ec.europa.eu", 60 }
            };

            // Sortuj po score * priorytet źródła
            var sorted = articles
                .OrderByDescending(a =>
                {
                    var domain = GetDomain(a.Url);
                    var priority = sourcePriority.FirstOrDefault(p => domain.Contains(p.Key)).Value;
                    return a.Score * (1 + priority / 100.0);
                })
                .ToList();

            foreach (var article in sorted)
            {
                // Sprawdź URL
                var normalizedUrl = NormalizeUrl(article.Url);
                if (seenUrls.Contains(normalizedUrl))
                    continue;

                // Sprawdź podobieństwo tytułu
                var normalizedTitle = NormalizeTitle(article.Title);
                if (seenTitles.Any(t => TitleSimilarity(t, normalizedTitle) > 0.8))
                    continue;

                seenUrls.Add(normalizedUrl);
                seenTitles.Add(normalizedTitle);
                unique.Add(article);
            }

            return unique.OrderByDescending(a => a.Score).ToList();
        }

        private string GetDomain(string url)
        {
            try
            {
                return new Uri(url).Host.ToLower();
            }
            catch
            {
                return url.ToLower();
            }
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            url = url.ToLower().Trim();
            // Usuń trailing slash, query params, etc.
            var uri = url.Split('?')[0].TrimEnd('/');
            return uri;
        }

        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            return title.ToLower()
                .Replace(".", "")
                .Replace(",", "")
                .Replace(":", "")
                .Replace("!", "")
                .Replace("?", "")
                .Trim();
        }

        private double TitleSimilarity(string a, string b)
        {
            // Proste porównanie Jaccard na słowach
            var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

            if (!wordsA.Any() || !wordsB.Any()) return 0;

            var intersection = wordsA.Intersect(wordsB).Count();
            var union = wordsA.Union(wordsB).Count();

            return (double)intersection / union;
        }

        #endregion

        #region === KONWERSJA DO RawArticle ===

        /// <summary>
        /// Konwertuj wyniki Tavily do formatu RawArticle (kompatybilny z resztą systemu)
        /// </summary>
        public List<RawArticle> ConvertToRawArticles(TavilySearchResult searchResult)
        {
            if (!searchResult.Success || searchResult.Results == null)
                return new List<RawArticle>();

            return searchResult.Results.Select(r => new RawArticle
            {
                SourceId = "tavily",
                SourceName = GetSourceName(r.Url),
                SourceCategory = DetermineCategory(r.Title + " " + r.Content),
                Language = DetectLanguage(r.Title + " " + r.Content),
                Title = r.Title ?? "Bez tytułu",
                Url = r.Url,
                UrlHash = RssFeedService.ComputeHash(r.Url),
                Summary = TruncateContent(r.Content, 500),
                FullContent = r.RawContent ?? r.Content,
                PublishDate = ParsePublishDate(r.PublishedDate) ?? DateTime.Today,
                FetchedAt = DateTime.Now,
                RelevanceScore = (int)(r.Score * 30), // Convert 0-1 score to 0-30
                IsRelevant = r.Score > 0.3
            }).ToList();
        }

        private string GetSourceName(string url)
        {
            try
            {
                var host = new Uri(url).Host.Replace("www.", "");
                return $"Tavily: {host}";
            }
            catch
            {
                return "Tavily Search";
            }
        }

        private string DetermineCategory(string text)
        {
            if (string.IsNullOrEmpty(text)) return "Info";

            text = text.ToLower();

            if (text.Contains("hpai") || text.Contains("ptasia grypa") || text.Contains("avian") || text.Contains("bird flu"))
                return "HPAI";
            if (text.Contains("cen") || text.Contains("price") || text.Contains("zł/kg") || text.Contains("eur"))
                return "Ceny";
            if (text.Contains("cedrob") || text.Contains("superdrob") || text.Contains("drosed") || text.Contains("animex"))
                return "Konkurencja";
            if (text.Contains("eksport") || text.Contains("import") || text.Contains("export") || text.Contains("brazylia") || text.Contains("brazil"))
                return "Handel";
            if (text.Contains("pasza") || text.Contains("kukurydza") || text.Contains("soja") || text.Contains("matif") || text.Contains("feed"))
                return "Pasze";
            if (text.Contains("ksef") || text.Contains("regulac") || text.Contains("przepis") || text.Contains("ustawa"))
                return "Regulacje";
            if (text.Contains("biedronka") || text.Contains("lidl") || text.Contains("dino") || text.Contains("sieć"))
                return "Retail";
            if (text.Contains("pogod") || text.Contains("mróz") || text.Contains("transport"))
                return "Logistyka";

            return "Info";
        }

        private string DetectLanguage(string text)
        {
            if (string.IsNullOrEmpty(text)) return "pl";

            // Simple heuristic - check for Polish characters/words
            var polishIndicators = new[] { "ą", "ę", "ć", "ł", "ń", "ó", "ś", "ź", "ż", " i ", " w ", " na ", " z ", " do ", " dla " };
            var polishCount = polishIndicators.Count(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));

            return polishCount >= 3 ? "pl" : "en";
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return "";
            if (content.Length <= maxLength) return content;
            return content.Substring(0, maxLength - 3) + "...";
        }

        private DateTime? ParsePublishDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return null;

            if (DateTime.TryParse(dateStr, out var date))
                return date;

            return null;
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }

    #region === REQUEST/RESPONSE MODELS ===

    internal class TavilyRequest
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("search_depth")]
        public string SearchDepth { get; set; } = "basic";

        [JsonPropertyName("include_answer")]
        public bool IncludeAnswer { get; set; } = true;

        [JsonPropertyName("include_raw_content")]
        public bool IncludeRawContent { get; set; } = false;

        [JsonPropertyName("max_results")]
        public int MaxResults { get; set; } = 5;

        [JsonPropertyName("include_domains")]
        public List<string> IncludeDomains { get; set; }

        [JsonPropertyName("exclude_domains")]
        public List<string> ExcludeDomains { get; set; }
    }

    internal class TavilyResponse
    {
        public string Answer { get; set; }
        public string Query { get; set; }
        public List<TavilyResultItem> Results { get; set; }
    }

    internal class TavilyResultItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }

        [JsonPropertyName("raw_content")]
        public string RawContent { get; set; }

        public double Score { get; set; }

        [JsonPropertyName("published_date")]
        public string PublishedDate { get; set; }
    }

    #endregion

    #region === PUBLIC MODELS ===

    public class SearchOptions
    {
        public int MaxResults { get; set; } = 5;
        public bool IncludeAiAnswer { get; set; } = true;
        public bool IncludeRawContent { get; set; } = false;
        public bool Deep { get; set; } = false;
        public List<string> IncludeDomains { get; set; }
        public List<string> ExcludeDomains { get; set; }

        public static SearchOptions Default => new();

        public static SearchOptions DeepSearch => new()
        {
            MaxResults = 10,
            IncludeAiAnswer = true,
            IncludeRawContent = true,
            Deep = true
        };
    }

    public class TavilySearchResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Query { get; set; }
        public string AiAnswer { get; set; }
        public List<TavilyArticle> Results { get; set; } = new();
        public DateTime SearchedAt { get; set; }
    }

    public class TavilyArticle
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public string RawContent { get; set; }
        public double Score { get; set; }
        public string PublishedDate { get; set; }
    }

    public class PoultryNewsSearchResult
    {
        public List<TavilyArticle> PolishNews { get; set; } = new();
        public List<TavilyArticle> InternationalNews { get; set; } = new();
        public List<AiSummary> AiSummaries { get; set; } = new();
        public int TotalArticles { get; set; }
        public DateTime SearchedAt { get; set; }
    }

    public class AiSummary
    {
        public string Topic { get; set; }
        public string Summary { get; set; }
    }

    #endregion
}
