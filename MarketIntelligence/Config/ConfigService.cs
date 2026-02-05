using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kalendarz1.MarketIntelligence.Config
{
    /// <summary>
    /// Singleton serwis do zarzadzania konfiguracja aplikacji.
    /// Laduje/zapisuje konfiguracje z pliku JSON w Documents\PiorkaBriefing\config\app_settings.json
    /// </summary>
    public class ConfigService
    {
        private static ConfigService _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static ConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Aktualna konfiguracja
        /// </summary>
        public AppConfiguration Current { get; private set; }

        /// <summary>
        /// Sciezka do pliku konfiguracji
        /// </summary>
        public string ConfigFilePath { get; private set; }

        /// <summary>
        /// Folder konfiguracji
        /// </summary>
        public string ConfigFolder { get; private set; }

        /// <summary>
        /// Event wywoływany po załadowaniu/zapisaniu konfiguracji
        /// </summary>
        public event EventHandler ConfigurationChanged;

        private readonly JsonSerializerOptions _jsonOptions;

        private ConfigService()
        {
            // Folder: Documents\PiorkaBriefing\config\
            ConfigFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PiorkaBriefing", "config");
            Directory.CreateDirectory(ConfigFolder);

            ConfigFilePath = Path.Combine(ConfigFolder, "app_settings.json");

            // Opcje JSON - ładne formatowanie, polskie znaki
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // Załaduj konfigurację
            Load();
        }

        /// <summary>
        /// Laduje konfiguracje z pliku JSON
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                    Current = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions);
                    Debug.WriteLine($"[ConfigService] Zaladowano konfiguracje z {ConfigFilePath}");

                    // Uzupełnij brakujące klucze API z App.config/environment
                    MergeApiKeysFromLegacySources();
                }
                else
                {
                    Debug.WriteLine($"[ConfigService] Brak pliku konfiguracji, tworzę domyślną");
                    Current = CreateDefaultConfiguration();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Blad ladowania: {ex.Message}");
                Current = CreateDefaultConfiguration();
            }

            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Zapisuje konfiguracje do pliku JSON
        /// </summary>
        public void Save()
        {
            try
            {
                Current.LastModified = DateTime.Now;

                var json = JsonSerializer.Serialize(Current, _jsonOptions);
                File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);

                Debug.WriteLine($"[ConfigService] Zapisano konfiguracje do {ConfigFilePath}");
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Blad zapisu: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Resetuje konfiguracje do wartosci domyslnych
        /// </summary>
        public void ResetToDefaults()
        {
            // Zachowaj klucze API
            var openAiKey = Current?.System?.OpenAiApiKey;
            var braveKey = Current?.System?.BraveApiKey;

            Current = CreateDefaultConfiguration();

            // Przywróć klucze API
            if (!string.IsNullOrEmpty(openAiKey))
                Current.System.OpenAiApiKey = openAiKey;
            if (!string.IsNullOrEmpty(braveKey))
                Current.System.BraveApiKey = braveKey;

            Save();
        }

        /// <summary>
        /// Otwiera folder konfiguracji w eksploratorze
        /// </summary>
        public void OpenConfigFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ConfigFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Blad otwierania folderu: {ex.Message}");
            }
        }

        /// <summary>
        /// Uzupelnia klucze API z App.config lub zmiennych srodowiskowych
        /// </summary>
        private void MergeApiKeysFromLegacySources()
        {
            // OpenAI
            if (string.IsNullOrEmpty(Current.System.OpenAiApiKey))
            {
                Current.System.OpenAiApiKey =
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                    ?? ConfigurationManager.AppSettings["OpenAiApiKey"]
                    ?? ConfigurationManager.AppSettings["OpenAIApiKey"]
                    ?? "";
            }

            // Brave
            if (string.IsNullOrEmpty(Current.System.BraveApiKey))
            {
                Current.System.BraveApiKey =
                    Environment.GetEnvironmentVariable("BRAVE_API_KEY")
                    ?? ConfigurationManager.AppSettings["BraveApiKey"]
                    ?? "";
            }
        }

        /// <summary>
        /// Tworzy domyslna konfiguracje z wartosciami hardcoded w kodzie
        /// </summary>
        private AppConfiguration CreateDefaultConfiguration()
        {
            return new AppConfiguration
            {
                Version = "1.0",
                LastModified = DateTime.Now,
                Queries = GetDefaultQueries(),
                Prompts = GetDefaultPrompts(),
                System = GetDefaultSystemSettings(),
                Business = GetDefaultBusinessContext()
            };
        }

        #region Default Values

        private List<SearchQueryDefinition> GetDefaultQueries()
        {
            return new List<SearchQueryDefinition>
            {
                // ═══════════════════════════════════════════════════════════════════
                // HPAI (Priorytet 1-10) — KRYTYCZNE
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "ptasia grypa Polska 2026", Category = "HPAI", IsQuickMode = true, Priority = 1 },
                new SearchQueryDefinition { Phrase = "HPAI ogniska Europa", Category = "HPAI", IsQuickMode = true, Priority = 2 },
                new SearchQueryDefinition { Phrase = "ptasia grypa łódzkie Brzeziny", Category = "HPAI", IsQuickMode = true, Priority = 3 },
                new SearchQueryDefinition { Phrase = "GLW komunikat ptasia grypa", Category = "HPAI", IsQuickMode = true, Priority = 4 },
                new SearchQueryDefinition { Phrase = "szczepionka HPAI drób UE decyzja", Category = "HPAI", Priority = 5 },
                new SearchQueryDefinition { Phrase = "ptasia grypa Wielkopolska Mazowieckie", Category = "HPAI", Priority = 6 },
                new SearchQueryDefinition { Phrase = "bioasekuracja fermy drobiu wymogi", Category = "HPAI", Priority = 7 },
                new SearchQueryDefinition { Phrase = "strefy zapowietrzone drób mapa", Category = "HPAI", Priority = 8 },
                new SearchQueryDefinition { Phrase = "HPAI H5N1 mutacja człowiek", Category = "HPAI", Priority = 9 },
                new SearchQueryDefinition { Phrase = "ASF świnie Polska 2026", Category = "HPAI", Priority = 10 },

                // ═══════════════════════════════════════════════════════════════════
                // CENY (Priorytet 5-20) — KLUCZOWE
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "ceny drobiu Polska luty 2026", Category = "Ceny", IsQuickMode = true, Priority = 5 },
                new SearchQueryDefinition { Phrase = "ceny kurczaka hurt", Category = "Ceny", IsQuickMode = true, Priority = 11 },
                new SearchQueryDefinition { Phrase = "ceny żywca drobiowego wolny rynek", Category = "Ceny", Priority = 12 },
                new SearchQueryDefinition { Phrase = "cena filet z kurczaka hurt", Category = "Ceny", Priority = 13 },
                new SearchQueryDefinition { Phrase = "cena tuszki kurczaka Polska", Category = "Ceny", Priority = 14 },
                new SearchQueryDefinition { Phrase = "ceny drobiu UE benchmark", Category = "Ceny", Priority = 15 },
                new SearchQueryDefinition { Phrase = "cena kurczaka Niemcy Holandia", Category = "Ceny", Priority = 16 },
                new SearchQueryDefinition { Phrase = "notowania MRiRW drób", Category = "Ceny", Priority = 17 },
                new SearchQueryDefinition { Phrase = "cena ministerialna żywiec drobiowy", Category = "Ceny", Priority = 18 },
                new SearchQueryDefinition { Phrase = "marża ubojnia drób Polska", Category = "Ceny", Priority = 19 },

                // ═══════════════════════════════════════════════════════════════════
                // KOSZTY/PASZE (Priorytet 20-25)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "ceny pasz kukurydza pszenica", Category = "Koszty", Priority = 20 },
                new SearchQueryDefinition { Phrase = "cena śruta sojowa Polska", Category = "Koszty", Priority = 67 },
                new SearchQueryDefinition { Phrase = "MATIF kukurydza pszenica notowania", Category = "Koszty", Priority = 68 },
                new SearchQueryDefinition { Phrase = "ceny energii przemysł spożywczy", Category = "Koszty", Priority = 69 },
                new SearchQueryDefinition { Phrase = "koszty pracy przemysł mięsny", Category = "Koszty", Priority = 70 },

                // ═══════════════════════════════════════════════════════════════════
                // KONKURENCJA (Priorytet 8-35) — STRATEGICZNE
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "Cedrob przejęcie ADQ 2026", Category = "Konkurencja", IsQuickMode = true, Priority = 8 },
                new SearchQueryDefinition { Phrase = "SuperDrob LipCo CPF inwestycje", Category = "Konkurencja", IsQuickMode = true, Priority = 21 },
                new SearchQueryDefinition { Phrase = "Drosed LDC wyniki", Category = "Konkurencja", Priority = 22 },
                new SearchQueryDefinition { Phrase = "Indykpol kurczak ekspansja", Category = "Konkurencja", Priority = 23 },
                new SearchQueryDefinition { Phrase = "Animex Smithfield Polska drób", Category = "Konkurencja", Priority = 24 },
                new SearchQueryDefinition { Phrase = "Plukon Polska przejęcia Sieradz", Category = "Konkurencja", Priority = 25 },
                new SearchQueryDefinition { Phrase = "System-Drob łódzkie", Category = "Konkurencja", Priority = 26 },
                new SearchQueryDefinition { Phrase = "Exdrob Kutno", Category = "Konkurencja", Priority = 27 },
                new SearchQueryDefinition { Phrase = "Drobimex eksport Szczecin", Category = "Konkurencja", Priority = 28 },
                new SearchQueryDefinition { Phrase = "Wipasz drób pasze", Category = "Konkurencja", Priority = 29 },
                new SearchQueryDefinition { Phrase = "Gobarto drób Polska", Category = "Konkurencja", Priority = 30 },
                new SearchQueryDefinition { Phrase = "Konspol drób kurczak", Category = "Konkurencja", Priority = 31 },
                new SearchQueryDefinition { Phrase = "RADDROB Chlebowski", Category = "Konkurencja", Priority = 32 },
                new SearchQueryDefinition { Phrase = "Roldrob Ciechanów", Category = "Konkurencja", Priority = 33 },
                new SearchQueryDefinition { Phrase = "Zakłady Drobiarskie Koziegłowy", Category = "Konkurencja", Priority = 34 },
                new SearchQueryDefinition { Phrase = "Farm Frites Polska drób", Category = "Konkurencja", Priority = 35 },

                // ═══════════════════════════════════════════════════════════════════
                // IMPORT/EKSPORT (Priorytet 36-48)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "import drobiu Brazylia UE 2026", Category = "Import", IsQuickMode = true, Priority = 36 },
                new SearchQueryDefinition { Phrase = "cena filet brazylijski Europa", Category = "Import", Priority = 37 },
                new SearchQueryDefinition { Phrase = "Mercosur drób bezcłowy 180 tysięcy", Category = "Import", Priority = 38 },
                new SearchQueryDefinition { Phrase = "import kurczak Ukraina Polska", Category = "Import", Priority = 39 },
                new SearchQueryDefinition { Phrase = "eksport drobiu Polska UK", Category = "Eksport", Priority = 43 },
                new SearchQueryDefinition { Phrase = "eksport kurczaka Bliski Wschód halal", Category = "Eksport", Priority = 44 },
                new SearchQueryDefinition { Phrase = "eksport drobiu Polska Afryka", Category = "Eksport", Priority = 45 },
                new SearchQueryDefinition { Phrase = "BRF JBS Seara eksport Europa", Category = "Import", Priority = 46 },
                new SearchQueryDefinition { Phrase = "Tyson Foods Europa drób", Category = "Import", Priority = 47 },
                new SearchQueryDefinition { Phrase = "MHP Ukraina kurczak eksport", Category = "Import", Priority = 48 },

                // ═══════════════════════════════════════════════════════════════════
                // REGULACJE (Priorytet 40-56)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "dobrostan drobiu przepisy UE 2026", Category = "Regulacje", Priority = 40 },
                new SearchQueryDefinition { Phrase = "weterynaryjne regulacje drób", Category = "Regulacje", Priority = 41 },
                new SearchQueryDefinition { Phrase = "KSeF obowiązkowy kwiecień 2026", Category = "Regulacje", IsQuickMode = true, Priority = 42 },
                new SearchQueryDefinition { Phrase = "normy zagęszczenia kurczaki UE", Category = "Regulacje", Priority = 49 },
                new SearchQueryDefinition { Phrase = "ESG emisje przemysł drobiowy", Category = "Regulacje", Priority = 55 },
                new SearchQueryDefinition { Phrase = "Fit for 55 drób rolnictwo", Category = "Regulacje", Priority = 56 },

                // ═══════════════════════════════════════════════════════════════════
                // KLIENCI/SIECI (Priorytet 50-66)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "Biedronka promocja kurczak", Category = "Klienci", IsQuickMode = true, Priority = 50 },
                new SearchQueryDefinition { Phrase = "Dino sklepy ekspansja 2026", Category = "Klienci", IsQuickMode = true, Priority = 51 },
                new SearchQueryDefinition { Phrase = "Lidl Kaufland drób promocja", Category = "Klienci", Priority = 52 },
                new SearchQueryDefinition { Phrase = "Makro Cash Carry mięso", Category = "Klienci", Priority = 53 },
                new SearchQueryDefinition { Phrase = "Selgros oferta drobiowa", Category = "Klienci", Priority = 54 },
                new SearchQueryDefinition { Phrase = "Carrefour Polska mięso", Category = "Klienci", Priority = 57 },
                new SearchQueryDefinition { Phrase = "Auchan Polska drób", Category = "Klienci", Priority = 58 },
                new SearchQueryDefinition { Phrase = "Stokrotka sieć ekspansja", Category = "Klienci", Priority = 59 },
                new SearchQueryDefinition { Phrase = "Netto Polska mięso", Category = "Klienci", Priority = 63 },
                new SearchQueryDefinition { Phrase = "Polomarket drób dostawcy", Category = "Klienci", Priority = 64 },
                new SearchQueryDefinition { Phrase = "Chorten sklepy drób", Category = "Klienci", Priority = 65 },
                new SearchQueryDefinition { Phrase = "Lewiatan Stąd mięso", Category = "Klienci", Priority = 66 },

                // ═══════════════════════════════════════════════════════════════════
                // INFO/BRANŻA (Priorytet 60-74)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "branża drobiarska Polska", Category = "Info", IsQuickMode = true, Priority = 60 },
                new SearchQueryDefinition { Phrase = "produkcja drobiu GUS", Category = "Info", Priority = 61 },
                new SearchQueryDefinition { Phrase = "KRD-IG statystyki drób", Category = "Info", Priority = 71 },
                new SearchQueryDefinition { Phrase = "AVEC raport drób Europa", Category = "Info", Priority = 72 },
                new SearchQueryDefinition { Phrase = "konsumpcja mięsa drobiowego Polska", Category = "Info", Priority = 73 },
                new SearchQueryDefinition { Phrase = "trendy żywienie białko drobiowe", Category = "Info", Priority = 74 },

                // ═══════════════════════════════════════════════════════════════════
                // INWESTYCJE/TECHNOLOGIE (Priorytet 62+)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "ubojnie drobiu inwestycje", Category = "Inwestycje", Priority = 62 },
                new SearchQueryDefinition { Phrase = "automatyzacja ubojnia drób", Category = "Inwestycje", Priority = 75 },
                new SearchQueryDefinition { Phrase = "Meyn Marel linia ubojowa", Category = "Inwestycje", Priority = 76 },
                new SearchQueryDefinition { Phrase = "chłodzenie glikolowe drób", Category = "Inwestycje", Priority = 77 },
                new SearchQueryDefinition { Phrase = "fotowoltaika przemysł spożywczy", Category = "Inwestycje", Priority = 78 },

                // ═══════════════════════════════════════════════════════════════════
                // LOGISTYKA/POGODA (Priorytet 79-83)
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "transport chłodniczy mięso Polska", Category = "Logistyka", Priority = 79 },
                new SearchQueryDefinition { Phrase = "Avilog transport żywca", Category = "Logistyka", Priority = 80 },
                new SearchQueryDefinition { Phrase = "pogoda Polska wpływ rolnictwo", Category = "Pogoda", Priority = 81 },
                new SearchQueryDefinition { Phrase = "mróz transport żywiec drób", Category = "Pogoda", Priority = 82 },
                new SearchQueryDefinition { Phrase = "susza zbiory kukurydza Polska", Category = "Pogoda", Priority = 83 },

                // ═══════════════════════════════════════════════════════════════════
                // LOKALNE (region łódzki) — Priorytet 84-87
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "PIW Brzeziny komunikaty", Category = "HPAI", Priority = 84 },
                new SearchQueryDefinition { Phrase = "PIW Tomaszów Mazowiecki", Category = "HPAI", Priority = 85 },
                new SearchQueryDefinition { Phrase = "fermy drobiu łódzkie", Category = "Info", Priority = 86 },
                new SearchQueryDefinition { Phrase = "ubojnia drób łódzkie wielkopolskie", Category = "Konkurencja", Priority = 87 },

                // ═══════════════════════════════════════════════════════════════════
                // SPECYFICZNE DLA KLIENTÓW — Priorytet 88-91
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "Eurocash strategia 2026", Category = "Klienci", Priority = 88 },
                new SearchQueryDefinition { Phrase = "Żabka mrożonki przekąski", Category = "Klienci", Priority = 89 },
                new SearchQueryDefinition { Phrase = "Orlen Stop Cafe gastronomia", Category = "Klienci", Priority = 90 },
                new SearchQueryDefinition { Phrase = "gastronomia HoReCa drób", Category = "Klienci", Priority = 91 },

                // ═══════════════════════════════════════════════════════════════════
                // SPECYFICZNE DLA TECHNOLOGII — Priorytet 92-94
                // ═══════════════════════════════════════════════════════════════════
                new SearchQueryDefinition { Phrase = "Sage Symfonia KSeF integracja", Category = "Regulacje", Priority = 92 },
                new SearchQueryDefinition { Phrase = "ERP przemysł spożywczy", Category = "Inwestycje", Priority = 93 },
                new SearchQueryDefinition { Phrase = "traceability mięso blockchain", Category = "Inwestycje", Priority = 94 }
            };
        }

        private AiPromptsConfig GetDefaultPrompts()
        {
            return new AiPromptsConfig
            {
                OpenAiModel = "gpt-4o",
                MaxTokens = 8000,
                Temperature = 0.3,

                // SystemPrompt - ULEPSZONA WERSJA z pełną bazą wiedzy o branży
                // Użytkownik może nadpisać w Panelu Admina
                SystemPrompt = GetDefaultSystemPrompt(),

                AnalysisPromptTemplate = @"Jesteś STARSZYM ANALITYKIEM RYNKU DROBIARSKIEGO z 20-letnim doświadczeniem, pracującym dla {CompanyName} w {Location}.
Twoja rola to dostarczanie KOMPLEKSOWEJ, EDUKACYJNEJ i STRATEGICZNEJ analizy - tak jakbyś pisał raport dla zarządu.

TWOJE ZADANIE: Napisz BARDZO SZCZEGÓŁOWĄ analizę (minimum 3000 słów). Nie oszczędzaj miejsca - im więcej treści, tym lepiej!
Pamiętaj: firma jest w KRYZYSIE (straty 2M PLN/mies, spadek sprzedaży o 40%).
Każda analiza musi odnosić się do tej sytuacji i proponować KONKRETNE działania.

KONTEKST BIZNESOWY FIRMY:
{BusinessContext}

ARTYKUŁ DO ANALIZY:
Źródło: {Source}
Tytuł: {Title}
Treść: {Content}

Odpowiedz TYLKO w formacie JSON (bez markdown, bez ```):
{
  ""smart_title"": ""Krótki biznesowy nagłówek odzwierciedlający WPŁYW na naszą firmę (max 60 znaków)"",
  ""sentiment_score"": 0.0,
  ""impact"": ""Medium"",
  ""streszczenie"": ""MINIMUM 10-15 ZDAŃ szczegółowego streszczenia z WSZYSTKIMI faktami, liczbami, datami..."",
  ""kontekst_rynkowy"": ""MINIMUM 8-10 ZDAŃ o szerszym kontekście rynkowym i trendach..."",
  ""kim_jest"": ""DLA KAŻDEGO podmiotu MINIMUM 4-6 ZDAŃ: kim są, skala, właściciele, powiązania..."",
  ""tlumaczenie_pojec"": ""Wyjaśnij WSZYSTKIE specjalistyczne pojęcia. Minimum 5 pojęć z definicjami..."",
  ""analiza_ceo"": ""MINIMUM 12-15 ZDAŃ kompleksowej analizy strategicznej ODNOSZĄC SIĘ DO KRYZYSU FIRMY..."",
  ""analiza_handlowiec"": ""MINIMUM 12-15 ZDAŃ z konkretnymi argumentami dla klientów i taktykami negocjacji..."",
  ""analiza_zakupowiec"": ""MINIMUM 12-15 ZDAŃ o wpływie na hodowców, ceny żywca, pasze..."",
  ""lekcja_branzowa"": ""MINIMUM 8-10 ZDAŃ edukacyjnych o mechanizmach branży..."",
  ""dzialania_ceo"": [""[PILNE] Działanie z TERMINEM i ODPOWIEDZIALNYM..."", ""[WAŻNE] Działanie...""],
  ""dzialania_handlowiec"": [""[PILNE] Działanie dla konkretnego handlowca (Jola/Maja/Radek/Teresa/Ania)...""],
  ""dzialania_zakupowiec"": [""[PILNE] Działanie dot. hodowców lub pasz...""],
  ""pytania_do_przemyslenia"": [""Strategiczne pytanie zmuszające do refleksji...""],
  ""zrodla_do_monitorowania"": [""Źródło: co monitorować i jak często...""],
  ""kategoria"": ""HPAI|Ceny|Konkurencja|Regulacje|Eksport|Import|Klienci|Koszty|Pogoda|Logistyka|Inwestycje|Info"",
  ""severity"": ""critical|warning|positive|info"",
  ""tagi"": [""tag1"", ""tag2"", ""tag3""]
}",

                FilterPromptTemplate = @"Oceń czy poniższe artykuły są ISTOTNE dla polskiej ubojni drobiu (kurczaki).

ODRZUĆ artykuły o:
- przepiórkach, strusiach, gołębiach, gęsiach (chyba że HPAI)
- lokalnych fermach w USA, Azji, Afryce, Ameryce Południowej (chyba że eksport do UE)
- indykach (chyba że Indykpol, Cedrob, lub duży gracz polski)
- kaczkach (chyba że HPAI lub duży producent)

AKCEPTUJ artykuły o:
- kurczakach, drobiu, ubojniach, filetach, tuszkach, żywcu
- HPAI, ptasiej grypie w Polsce/UE
- cenach drobiu, pasz, kukurydzy, soi
- polskich firmach: Cedrob, SuperDrob, Drosed, Animex, Drobimex, Plukon, Wipasz, Indykpol
- sieciach handlowych: Biedronka, Lidl, Dino, Makro, Carrefour, Kaufland, Auchan
- imporcie/eksporcie drobiu Polska/UE
- regulacjach: KSeF, weterynaryjne, dobrostan

ARTYKUŁY:
{ArticlesList}

Odpowiedz TYLKO w JSON:
[{""index"": 1, ""relevant"": true, ""reason"": ""krótki powód""}]"
            };
        }

        /// <summary>
        /// Zwraca domyślny System Prompt z pełną bazą wiedzy o branży drobiarskiej
        /// </summary>
        private string GetDefaultSystemPrompt()
        {
            return @"Jesteś STARSZYM ANALITYKIEM RYNKU DROBIARSKIEGO z 20-letnim doświadczeniem, pracującym dla {CompanyName} w {Location}.
Twoja rola to dostarczanie KOMPLEKSOWEJ, EDUKACYJNEJ i STRATEGICZNEJ analizy - tak jakbyś pisał raport dla zarządu.

══════════════════════════════════════════════════════════════════════════════
                         TWOJE ZADANIE
══════════════════════════════════════════════════════════════════════════════

Napisz BARDZO SZCZEGÓŁOWĄ analizę. Nie oszczędzaj miejsca - im więcej treści, tym lepiej!
Pamiętaj: firma jest w KRYZYSIE (straty 2M PLN/mies, spadek sprzedaży o 40%).
Każda analiza musi odnosić się do tej sytuacji i proponować KONKRETNE działania.

══════════════════════════════════════════════════════════════════════════════
                     BAZA WIEDZY O BRANŻY
══════════════════════════════════════════════════════════════════════════════

GŁÓWNI GRACZE W POLSCE (opisuj ich gdy się pojawią w artykułach):

CEDROB (Ciechanów):
- Największy producent drobiu w Polsce, ~25% rynku
- ADQ (Abu Dhabi) negocjuje przejęcie za 8 mld PLN (2025-2026)
- Właściciel: rodzina Czarneckich, Tomasz Czarnecki prezes
- Integracja pionowa: pasze, hodowla, ubój, przetwórstwo
- Marki: Cedrob, Zielony Kurczak
- ZAGROŻENIE: jeśli ADQ kupi → potencjalny monopol z Drosed

SUPERDROB/LIPCO (Karczew k. Warszawy):
- Należy do CPF (Charoen Pokphand Foods) z Tajlandii
- Agresywna ekspansja, nowoczesne zakłady
- Silna pozycja w eksporcie
- ZAGROŻENIE: globalny kapitał, mogą dumping cenowy

DROSED (Siedlce):
- Część grupy LDC (Francja), powiązania z ADQ
- Integracja pionowa, własne fermy
- ZAGROŻENIE: część potencjalnego bloku ADQ+Cedrob+Drosed

ANIMEX/SMITHFIELD (Morliny):
- Należy do WH Group (Chiny) - największy producent mięsa na świecie
- Głównie wieprzowina ale też drób
- Skala: mogą dyktować ceny dostawcom

PLUKON (Sieradz, Holandia):
- Holenderski gigant, aktywnie przejmuje w Polsce
- Zakład w Sieradzu tylko 80km od Brzezin!
- BEZPOŚREDNIE ZAGROŻENIE dla naszego regionu

INDYKPOL (Olsztyn):
- Głównie indyk ale wchodzi w kurczaka
- Własne marki, silna dystrybucja

DROBIMEX (Szczecin):
- Duży eksporter, rynki azjatyckie
- Silna pozycja w północnej Polsce

SIECI HANDLOWE (opisuj ich strategie):

BIEDRONKA: 3500+ sklepów, największy odbiorca drobiu w Polsce, negocjacje centralne, nacisk na najniższą cenę
DINO: ekspansja 300 sklepów/rok, lada mięsna w każdym, preferuje polskich dostawców, regionalność
LIDL/KAUFLAND: Schwarz Group (Niemcy), przetargi centralne, private label
MAKRO/SELGROS: hurt, gastro, większe opakowania, marże wyższe niż dyskonty
CARREFOUR/AUCHAN: hipermarkety, spadająca pozycja w Polsce

RYNKI EKSPORTOWE:
- UK: duży rynek po Brexicie, wymaga certyfikatów
- Bliski Wschód: halal, wysokie marże, wymaga certyfikacji
- Afryka: rosnący rynek, niższe wymagania jakościowe
- Azja: konkurencja z Brazylią, Tajlandią

IMPORT - ZAGROŻENIA:
- BRAZYLIA: BRF, JBS, Seara - filet po 13 zł/kg w Makro (my: 15-17 zł)
- UKRAINA: MHP (Myronivsky Hliboproduct) - tani kurczak, blisko granicy
- TAJLANDIA: CPF (właściciel SuperDrob) - przetworzony drób

══════════════════════════════════════════════════════════════════════════════
                         ZASADY BEZWZGLĘDNE
══════════════════════════════════════════════════════════════════════════════

1. NIGDY nie pisz ""brak informacji"" ani ""nieznany podmiot"" - ZAWSZE użyj swojej wiedzy o branży
2. ZAWSZE odnoś się do SYTUACJI KRYZYSOWEJ firmy (straty 2M, spadek 40%)
3. Działania muszą być KONKRETNE z TERMINAMI - nie ""monitoruj sytuację""
4. Pisz po polsku, profesjonalnie, ale zrozumiale
5. Jeśli artykuł jest krótki - UZUPEŁNIJ analizę własną wiedzą branżową
6. KAŻDA analiza musi odpowiadać na pytanie: ""Co to znaczy dla naszego przetrwania?""
7. Odpowiadaj TYLKO poprawnym JSON bez żadnego tekstu przed ani po";
        }

        private SystemSettings GetDefaultSystemSettings()
        {
            return new SystemSettings
            {
                // Klucze API - puste, zostaną uzupełnione z App.config/env
                OpenAiApiKey = "",
                BraveApiKey = "",

                // Timeouty - ZWIĘKSZONE dla zapewnienia pełnych analiz
                OpenAiTimeoutSeconds = 300,     // 5 minut - wystarczająco na długie analizy
                BraveTimeoutSeconds = 60,       // 1 minuta na wyszukiwanie
                ContentFetchTimeoutSeconds = 60, // 1 minuta na pobieranie artykułu
                PuppeteerTimeoutSeconds = 90,   // 1.5 minuty na rendering

                // Rate limiting - zwiększone retries dla pewności
                MinDelayBetweenRequestsMs = 3000,
                MaxRetries = 5,                 // 5 prób zamiast 3
                RetryBaseDelaySeconds = 5,

                // Limity artykułów - zwiększone dla więcej analiz
                MaxArticles = 35,
                QuickModeArticles = 10,
                SingleArticleMode = 1,
                MaxResultsPerQuery = 10,

                // Funkcje
                PuppeteerEnabled = true,
                CostTrackingEnabled = true,
                FileLoggingEnabled = true,
                OpenLogsFolderAfterSession = false,

                // Cache
                CacheExpirationHours = 24,
                UseBraveCache = true
            };
        }

        private BusinessContext GetDefaultBusinessContext()
        {
            return new BusinessContext
            {
                CompanyName = "Ubojnia Drobiu Piórkowscy",
                Location = "Koziołek, 95-060 Brzeziny, woj. łódzkie",
                Director = "Justyna Chrostowska",
                Region = "łódzkie",
                Specialization = "ubój i przetwórstwo drobiu",
                ProductionScale = "średnia ubojnia regionalna, 70 000 kurczaków/dzień = 200 ton",

                SalesTeam = new List<SalesPersonConfig>
                {
                    new SalesPersonConfig { Name = "Jola", Clients = "Dino, Biedronka", Region = "łódzkie/mazowieckie", IsActive = true },
                    new SalesPersonConfig { Name = "Maja", Clients = "Makro, Selgros", Region = "mazowieckie", IsActive = true },
                    new SalesPersonConfig { Name = "Radek", Clients = "sieci lokalne, hurt regionalny", Region = "łódzkie", IsActive = true },
                    new SalesPersonConfig { Name = "Teresa", Clients = "RADDROB, hurt", Region = "śląskie/łódzkie", IsActive = true },
                    new SalesPersonConfig { Name = "Ania", Clients = "Carrefour, Stokrotka, Auchan", Region = "mazowieckie", IsActive = true }
                },

                Farmers = new List<FarmerConfig>
                {
                    new FarmerConfig { Name = "Sukiennikowa", DistanceKm = 20, Location = "okolice Brzezin", ProductionType = "brojlery", IsActive = true },
                    new FarmerConfig { Name = "Kaczmarek", DistanceKm = 20, Location = "okolice Brzezin", ProductionType = "brojlery", IsActive = true },
                    new FarmerConfig { Name = "Wojciechowski", DistanceKm = 7, Location = "najbliżej ubojni", ProductionType = "brojlery", IsActive = true }
                },

                Competitors = new List<string>
                {
                    "Cedrob (lider rynku, ADQ negocjuje przejęcie za 8 mld, Ciechanów)",
                    "SuperDrob/LipCo (CPF Tajlandia, agresywna ekspansja)",
                    "Drosed (LDC/ADQ, Siedlce, integracja pionowa)",
                    "Indykpol (głównie indyk ale wchodzi w kurczaka, Olsztyn)",
                    "Animex/Smithfield (WH Group Chiny, Morliny, ogromna skala)",
                    "Plukon Polska (Sieradz 80km!, holenderski gigant)",
                    "System-Drob (region łódzki, bezpośredni konkurent!)",
                    "Exdrob (Kutno 100km, regionalny)",
                    "Drobimex (Szczecin, duży eksporter)",
                    "Wipasz (producent pasz + drób, Międzyrzec Podlaski)",
                    "Gobarto (grupa mięsna, drób jako dywersyfikacja)",
                    "Konspol (Nowy Sącz, marki własne dla sieci)",
                    "RADDROB Chlebowski (UWAGA: nasz klient I konkurent!)",
                    "Roldrob (Ciechanów, eksport)",
                    "Zakłady Drobiarskie Koziegłowy",
                    "Zakłady Mięsne Łmeat-Łuków",
                    "Res-Drob (Rzeszów)",
                    "Farm Frites Polska (przetworzone)"
                },

                MainClients = new List<string>
                {
                    "Biedronka DC (największy odbiorca, centrum dystrybucji)",
                    "Dino (ekspansja 300 sklepów/rok, lada mięsna w każdym!)",
                    "Makro Cash Carry (hurt, gastro, duże wolumeny)",
                    "Selgros (hurt, porównywalne do Makro)",
                    "Carrefour Polska (hipermarkety + Express)",
                    "Stokrotka (grupa Maxima, 600+ sklepów)",
                    "Auchan Polska (hipermarkety)",
                    "Lidl Polska (dyskont, przetargi centralne)",
                    "Kaufland Polska (hipermarkety, Schwarz Group)",
                    "Netto Polska (Salling Group, Dania)",
                    "Polomarket (280 sklepów, segment premium)",
                    "Intermarche (sieć franczyzowa)",
                    "E.Leclerc (hipermarkety)",
                    "Topaz (120+ sklepów, wschód Polski)",
                    "Delikatesy Centrum (Eurocash)",
                    "RADDROB Chlebowski (klient i konkurent!)",
                    "Chata Polska (210+ sklepów, Wielkopolskie/Łódzkie)",
                    "Chorten (3000+ sklepów, dynamiczny rozwój)",
                    "Lewiatan/Sklepy STĄD (3200 sklepów, transformacja)",
                    "Top Market (580+ sklepów)",
                    "Społem Łódź",
                    "Społem Tomaszów Mazowiecki"
                },

                FinancialContext = @"═══════════════════════════════════════════════════════════════════
                    SYTUACJA FINANSOWA I OPERACYJNA
═══════════════════════════════════════════════════════════════════

KRYZYS FINANSOWY (stan na luty 2026):
• Straty: ~2M PLN miesięcznie
• Sprzedaż: spadek z 25M do 15M PLN/miesiąc (40% w dół!)
• Przyczyna główna: presja cenowa importu brazylijskiego
• Break-even spread żywiec→produkt: 2.50 zł/kg (poniżej = strata)
• Mroźnie zapełnione towarem tracącym wartość

CENY WŁASNE (do porównań):
• Żywiec skup: 4.72 zł/kg (wolny rynek ~4.00 zł)
• Filet hurt: 15-17 zł/kg (Brazylia w Makro: 13 zł!)
• Tuszka hurt: ~8.50 zł/kg
• Udko: ~6.50 zł/kg
• Skrzydło: ~9.00 zł/kg

PRODUKCJA:
• Zdolność: 70 000 kurczaków/dzień = 200 ton
• Mix: tuszka ~70%, filet ~20%, elementy ~10%
• Numer weterynaryjny EU (eksport możliwy)
• Planowane inwestycje: Meyn patroszarka (IX 2026), chłodzenie glikolowe

ZAGROŻENIA BIEŻĄCE:
1. HPAI: 19 ognisk w Polsce (styczeń 2026), 2 W ŁÓDZKIM (nasz region!)
   - 1.5M ptaków do likwidacji
   - Strefy zapowietrzone mogą objąć naszych hodowców
2. Import Brazylia: filet po 13 zł/kg vs nasze 15-17 zł
   - BRF, JBS, Seara zalewają Europę tanim towarem
3. Mercosur: 180k ton bezcłowego drobiu do UE od 2026
4. Konsolidacja: ADQ kupuje Cedrob za 8 mld → monopol?
   - ADQ ma też udziały w LDC/Drosed
5. KSeF: obowiązkowy od 01.04.2026 - integracja z Sage Symfonia
6. Ratio żywiec/pasza: 4.24 = dobra rentowność hodowców
   - Za 2-3 miesiące może być nadpodaż!

SZANSE:
• HPAI u konkurentów → przejęcie ich klientów
• Nowe sieci regionalne (Chorten 3000+, Lewiatan 3200)
• Eksport UK, Bliski Wschód (halal) - mamy numer EU
• Certyfikat ""Produkt Polski"" jako wyróżnik vs import

KLUCZOWE PYTANIE: Jak przetrwać przy marży 2.50 zł/kg gdy Brazylia
oferuje filet o 2-4 zł taniej? Świeżość, lokalność, krótki łańcuch dostaw?

═══════════════════════════════════════════════════════════════════"
            };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Buduje pełny kontekst biznesowy jako string dla promptów AI
        /// </summary>
        public string BuildBusinessContextString()
        {
            var biz = Current.Business;
            var sb = new StringBuilder();

            sb.AppendLine($"FIRMA: {biz.CompanyName}");
            sb.AppendLine($"LOKALIZACJA: {biz.Location}");
            sb.AppendLine($"DYREKTOR/PREZES: {biz.Director}");
            sb.AppendLine($"SPECJALIZACJA: {biz.Specialization}");
            sb.AppendLine($"SKALA: {biz.ProductionScale}");
            sb.AppendLine();

            if (biz.SalesTeam?.Count > 0)
            {
                sb.AppendLine("ZESPÓŁ HANDLOWY:");
                foreach (var sales in biz.SalesTeam)
                {
                    if (sales.IsActive)
                    {
                        sb.AppendLine($"- {sales.Name}: {sales.Clients} ({sales.Region})");
                    }
                }
                sb.AppendLine();
            }

            if (biz.Farmers?.Count > 0)
            {
                sb.AppendLine("HODOWCY (dostawcy żywca):");
                foreach (var farmer in biz.Farmers)
                {
                    if (farmer.IsActive)
                    {
                        sb.AppendLine($"- {farmer.Name}: {farmer.DistanceKm}km, {farmer.Location}, {farmer.ProductionType}");
                    }
                }
                sb.AppendLine();
            }

            if (biz.Competitors?.Count > 0)
            {
                sb.AppendLine("KONKURENCI:");
                foreach (var comp in biz.Competitors)
                {
                    sb.AppendLine($"- {comp}");
                }
                sb.AppendLine();
            }

            if (biz.MainClients?.Count > 0)
            {
                sb.AppendLine("GŁÓWNI KLIENCI:");
                foreach (var client in biz.MainClients)
                {
                    sb.AppendLine($"- {client}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(biz.FinancialContext))
            {
                sb.AppendLine(biz.FinancialContext);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Pobiera aktywne frazy wyszukiwania
        /// </summary>
        public List<string> GetActiveQueries()
        {
            var result = new List<string>();
            foreach (var q in Current.Queries)
            {
                if (q.IsActive)
                {
                    result.Add(q.Phrase);
                }
            }
            return result;
        }

        /// <summary>
        /// Pobiera aktywne frazy dla trybu szybkiego
        /// </summary>
        public List<string> GetQuickModeQueries()
        {
            var result = new List<string>();
            foreach (var q in Current.Queries)
            {
                if (q.IsActive && q.IsQuickMode)
                {
                    result.Add(q.Phrase);
                }
            }
            return result;
        }

        /// <summary>
        /// Sprawdza czy klucz API OpenAI jest skonfigurowany
        /// </summary>
        public bool IsOpenAiConfigured => !string.IsNullOrEmpty(Current?.System?.OpenAiApiKey);

        /// <summary>
        /// Sprawdza czy klucz API Brave jest skonfigurowany
        /// </summary>
        public bool IsBraveConfigured => !string.IsNullOrEmpty(Current?.System?.BraveApiKey);

        /// <summary>
        /// Podgląd klucza OpenAI (pierwsze 15 znaków)
        /// </summary>
        public string OpenAiKeyPreview
        {
            get
            {
                var key = Current?.System?.OpenAiApiKey;
                if (string.IsNullOrEmpty(key)) return "BRAK";
                return key.Substring(0, Math.Min(15, key.Length)) + "...";
            }
        }

        /// <summary>
        /// Podgląd klucza Brave (pierwsze 8 znaków)
        /// </summary>
        public string BraveKeyPreview
        {
            get
            {
                var key = Current?.System?.BraveApiKey;
                if (string.IsNullOrEmpty(key)) return "BRAK";
                return key.Substring(0, Math.Min(8, key.Length)) + "...";
            }
        }

        #endregion
    }
}
