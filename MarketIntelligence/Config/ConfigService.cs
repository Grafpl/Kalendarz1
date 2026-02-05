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
                // === HPAI / Choroby (KRYTYCZNE) ===
                new SearchQueryDefinition { Phrase = "ptasia grypa Polska 2026", Category = "HPAI", IsQuickMode = true, Priority = 1 },
                new SearchQueryDefinition { Phrase = "HPAI ogniska Europa", Category = "HPAI", IsQuickMode = true, Priority = 2 },
                new SearchQueryDefinition { Phrase = "ASF świnie Polska", Category = "HPAI", Priority = 10 },

                // === Ceny i rynek ===
                new SearchQueryDefinition { Phrase = "ceny drobiu Polska", Category = "Ceny", IsQuickMode = true, Priority = 5 },
                new SearchQueryDefinition { Phrase = "ceny kurczaka hurt", Category = "Ceny", Priority = 15 },
                new SearchQueryDefinition { Phrase = "ceny żywca drobiowego", Category = "Ceny", Priority = 16 },
                new SearchQueryDefinition { Phrase = "ceny pasz kukurydza pszenica", Category = "Koszty", Priority = 20 },

                // === Konkurencja ===
                new SearchQueryDefinition { Phrase = "Cedrob przejęcie", Category = "Konkurencja", IsQuickMode = true, Priority = 8 },
                new SearchQueryDefinition { Phrase = "SuperDrob LipCo", Category = "Konkurencja", Priority = 25 },
                new SearchQueryDefinition { Phrase = "Drosed Indykpol", Category = "Konkurencja", Priority = 26 },
                new SearchQueryDefinition { Phrase = "Animex drób", Category = "Konkurencja", Priority = 27 },

                // === Eksport/Import ===
                new SearchQueryDefinition { Phrase = "eksport drobiu Polska", Category = "Eksport", IsQuickMode = true, Priority = 12 },
                new SearchQueryDefinition { Phrase = "import drobiu Brazylia Ukraina", Category = "Import", Priority = 30 },
                new SearchQueryDefinition { Phrase = "Mercosur drób UE", Category = "Import", Priority = 31 },
                new SearchQueryDefinition { Phrase = "Chiny polski drób", Category = "Eksport", Priority = 32 },

                // === Regulacje ===
                new SearchQueryDefinition { Phrase = "KSeF faktury 2026", Category = "Regulacje", Priority = 40 },
                new SearchQueryDefinition { Phrase = "dobrostan drobiu przepisy", Category = "Regulacje", Priority = 41 },
                new SearchQueryDefinition { Phrase = "weterynaryjne regulacje drób", Category = "Regulacje", Priority = 42 },

                // === Sieci handlowe (Klienci) ===
                new SearchQueryDefinition { Phrase = "Biedronka dostawcy mięso", Category = "Klienci", Priority = 50 },
                new SearchQueryDefinition { Phrase = "Dino sklepy ekspansja", Category = "Klienci", Priority = 51 },
                new SearchQueryDefinition { Phrase = "Lidl Kaufland drób", Category = "Klienci", Priority = 52 },

                // === Ogólne branżowe ===
                new SearchQueryDefinition { Phrase = "branża drobiarska Polska", Category = "Info", IsQuickMode = true, Priority = 60 },
                new SearchQueryDefinition { Phrase = "produkcja drobiu GUS", Category = "Info", Priority = 61 },
                new SearchQueryDefinition { Phrase = "ubojnie drobiu inwestycje", Category = "Inwestycje", Priority = 62 }
            };
        }

        private AiPromptsConfig GetDefaultPrompts()
        {
            return new AiPromptsConfig
            {
                OpenAiModel = "gpt-4o",
                MaxTokens = 8000,
                Temperature = 0.3,

                // SystemPrompt - PUSTY oznacza uzycie nowego SUPER-PROMPT z OpenAIAnalysisService
                // Uzytkownik moze nadpisac w Panelu Admina
                SystemPrompt = "",

                AnalysisPromptTemplate = @"Jesteś STARSZYM ANALITYKIEM RYNKU DROBIARSKIEGO z 20-letnim doświadczeniem, pracującym dla {CompanyName} w {Location}.
Twoja rola to dostarczanie KOMPLEKSOWEJ, EDUKACYJNEJ i STRATEGICZNEJ analizy - tak jakbyś pisał raport dla zarządu.

TWOJE ZADANIE: Napisz BARDZO SZCZEGÓŁOWĄ analizę (minimum 3000 słów). Nie oszczędzaj miejsca - im więcej treści, tym lepiej!

KONTEKST BIZNESOWY FIRMY:
{BusinessContext}

ARTYKUŁ DO ANALIZY:
Źródło: {Source}
Tytuł: {Title}
Treść: {Content}

Odpowiedz TYLKO w formacie JSON (bez markdown, bez ```):
{
  ""smart_title"": ""Krótki biznesowy nagłówek (max 60 znaków)"",
  ""sentiment_score"": 0.0,
  ""impact"": ""Medium"",
  ""streszczenie"": ""MINIMUM 10-15 ZDAŃ szczegółowego streszczenia..."",
  ""kontekst_rynkowy"": ""MINIMUM 8-10 ZDAŃ o szerszym kontekście rynkowym..."",
  ""kim_jest"": ""DLA KAŻDEGO podmiotu MINIMUM 4-6 ZDAŃ edukacyjnego opisu..."",
  ""tlumaczenie_pojec"": ""Wyjaśnij WSZYSTKIE specjalistyczne pojęcia. Minimum 5 pojęć..."",
  ""analiza_ceo"": ""MINIMUM 12-15 ZDAŃ kompleksowej analizy strategicznej..."",
  ""analiza_handlowiec"": ""MINIMUM 12-15 ZDAŃ analizy dla działu handlowego..."",
  ""analiza_zakupowiec"": ""MINIMUM 12-15 ZDAŃ analizy dla działu zakupów..."",
  ""lekcja_branzowa"": ""MINIMUM 8-10 ZDAŃ edukacyjnych o branży..."",
  ""dzialania_ceo"": [""[PILNE] Działanie..."", ""[WAŻNE] Działanie...""],
  ""dzialania_handlowiec"": [""[PILNE] Działanie...""],
  ""dzialania_zakupowiec"": [""[PILNE] Działanie...""],
  ""pytania_do_przemyslenia"": [""Pytanie strategiczne 1?""],
  ""zrodla_do_monitorowania"": [""Źródło 1: co monitorować""],
  ""kategoria"": ""HPAI|Ceny|Konkurencja|Regulacje|Eksport|Import|Klienci|Koszty|Pogoda|Logistyka|Inwestycje|Info"",
  ""severity"": ""critical|warning|positive|info"",
  ""tagi"": [""tag1"", ""tag2""]
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

        private SystemSettings GetDefaultSystemSettings()
        {
            return new SystemSettings
            {
                // Klucze API - puste, zostaną uzupełnione z App.config/env
                OpenAiApiKey = "",
                BraveApiKey = "",

                // Timeouty
                OpenAiTimeoutSeconds = 120,
                BraveTimeoutSeconds = 30,
                ContentFetchTimeoutSeconds = 30,
                PuppeteerTimeoutSeconds = 60,

                // Rate limiting
                MinDelayBetweenRequestsMs = 3000,
                MaxRetries = 3,
                RetryBaseDelaySeconds = 5,

                // Limity artykułów
                MaxArticles = 25,
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
                ProductionScale = "średnia ubojnia regionalna",

                SalesTeam = new List<SalesPersonConfig>
                {
                    new SalesPersonConfig { Name = "Jola", Clients = "Dino, Biedronka", Region = "łódzkie", IsActive = true },
                    new SalesPersonConfig { Name = "Maja", Clients = "Makro, Selgros", Region = "mazowieckie", IsActive = true },
                    new SalesPersonConfig { Name = "Radek", Clients = "sieci lokalne, hurt", Region = "łódzkie", IsActive = true },
                    new SalesPersonConfig { Name = "Teresa", Clients = "RADDROB, hurt", Region = "śląskie", IsActive = true },
                    new SalesPersonConfig { Name = "Ania", Clients = "Carrefour, Stokrotka, Auchan", Region = "mazowieckie", IsActive = true }
                },

                Farmers = new List<FarmerConfig>
                {
                    new FarmerConfig { Name = "Sukiennikowa", DistanceKm = 20, Location = "okolice Brzezin", ProductionType = "brojlery", IsActive = true },
                    new FarmerConfig { Name = "Kaczmarek", DistanceKm = 20, Location = "okolice Brzezin", ProductionType = "brojlery", IsActive = true },
                    new FarmerConfig { Name = "Wojciechowski", DistanceKm = 7, Location = "okolice Brzezin", ProductionType = "brojlery", IsActive = true }
                },

                Competitors = new List<string>
                {
                    "Cedrob (lider rynku, przejęcia)",
                    "SuperDrob/LipCo",
                    "Drosed",
                    "Indykpol",
                    "Animex/Smithfield",
                    "Plukon Sieradz (80km)",
                    "System-Drob",
                    "Exdrob Kutno (100km)"
                },

                MainClients = new List<string>
                {
                    "Biedronka DC",
                    "Dino (ekspansja 300 sklepów/rok)",
                    "Makro",
                    "Selgros",
                    "Carrefour",
                    "Stokrotka",
                    "Auchan",
                    "Lidl",
                    "Kaufland"
                },

                FinancialContext = @"SYTUACJA FINANSOWA:
- Straty: ~2M PLN/miesiąc
- Spadek sprzedaży: z 25M do 15M PLN/miesiąc
- Import brazylijski: 13 zł/kg (niska konkurencja)
- HPAI: 19 ognisk w Polsce (2 w łódzkim) - ryzyko
- Break-even spread żywiec/produkt: 2.50 zł/kg
- Główne wyzwanie: odbudowa marży i wolumenów"
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
