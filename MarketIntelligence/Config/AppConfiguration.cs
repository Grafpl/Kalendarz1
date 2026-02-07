using System;
using System.Collections.Generic;

namespace Kalendarz1.MarketIntelligence.Config
{
    /// <summary>
    /// Główny model konfiguracji aplikacji MarketIntelligence.
    /// Zapisywany jako JSON w Documents\PiorkaBriefing\config\app_settings.json
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Wersja konfiguracji (dla przyszłych migracji)
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Data ostatniej modyfikacji
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// ZWIAD - Frazy wyszukiwania Brave Search
        /// </summary>
        public List<SearchQueryDefinition> Queries { get; set; } = new List<SearchQueryDefinition>();

        /// <summary>
        /// MÓZG AI - Prompty dla OpenAI
        /// </summary>
        public AiPromptsConfig Prompts { get; set; } = new AiPromptsConfig();

        /// <summary>
        /// SYSTEM - Ustawienia techniczne (API, timeouty, limity)
        /// </summary>
        public SystemSettings System { get; set; } = new SystemSettings();

        /// <summary>
        /// KONTEKST BIZNESOWY - Dane firmy Piórkowscy
        /// </summary>
        public BusinessContext Business { get; set; } = new BusinessContext();
    }

    /// <summary>
    /// Definicja frazy wyszukiwania dla Brave Search
    /// </summary>
    public class SearchQueryDefinition
    {
        /// <summary>
        /// Fraza wyszukiwania (np. "ptasia grypa Polska 2026")
        /// </summary>
        public string Phrase { get; set; } = string.Empty;

        /// <summary>
        /// Czy fraza jest aktywna (używana w wyszukiwaniu)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Kategoria: HPAI, Ceny, Konkurencja, Regulacje, Eksport, Import, Klienci, Koszty, Pogoda, Logistyka, Inwestycje, Info
        /// </summary>
        public string Category { get; set; } = "Info";

        /// <summary>
        /// Czy używać w trybie szybkim (przycisk "Szybki")
        /// </summary>
        public bool IsQuickMode { get; set; } = false;

        /// <summary>
        /// Priorytet sortowania (niższy = ważniejszy)
        /// </summary>
        public int Priority { get; set; } = 100;
    }

    /// <summary>
    /// Konfiguracja promptów AI
    /// </summary>
    public class AiPromptsConfig
    {
        /// <summary>
        /// System prompt - instrukcje dla AI (format JSON, rola analityka)
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Szablon promptu analizy artykułu (z placeholderami {BusinessContext}, {ArticleContent})
        /// </summary>
        public string AnalysisPromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Szablon promptu filtrowania (opcjonalnie)
        /// </summary>
        public string FilterPromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Model OpenAI do użycia (np. "gpt-4o", "gpt-4o-mini")
        /// </summary>
        public string OpenAiModel { get; set; } = "gpt-4o";

        /// <summary>
        /// Maksymalna liczba tokenów odpowiedzi
        /// </summary>
        public int MaxTokens { get; set; } = 8000;

        /// <summary>
        /// Temperatura (kreatywność) - 0.0 do 1.0
        /// </summary>
        public double Temperature { get; set; } = 0.3;
    }

    /// <summary>
    /// Ustawienia systemowe (API, timeouty, limity)
    /// </summary>
    public class SystemSettings
    {
        // === KLUCZE API ===

        /// <summary>
        /// Klucz API OpenAI (sk-proj-...)
        /// </summary>
        public string OpenAiApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Klucz API Brave Search (BSA...)
        /// </summary>
        public string BraveApiKey { get; set; } = string.Empty;

        // === TIMEOUTY (sekundy) - ZWIĘKSZONE dla zapewnienia pełnych analiz ===

        /// <summary>
        /// Timeout dla zapytań OpenAI (sekundy) - 5 minut na długie analizy
        /// </summary>
        public int OpenAiTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Timeout dla zapytań Brave Search (sekundy)
        /// </summary>
        public int BraveTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Timeout dla pobierania treści artykułu HTTP (sekundy)
        /// </summary>
        public int ContentFetchTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Timeout dla Puppeteer (sekundy)
        /// </summary>
        public int PuppeteerTimeoutSeconds { get; set; } = 90;

        // === RATE LIMITING ===

        /// <summary>
        /// Minimalny odstęp między zapytaniami do OpenAI (milisekundy)
        /// </summary>
        public int MinDelayBetweenRequestsMs { get; set; } = 3000;

        /// <summary>
        /// Maksymalna liczba prób (retry) przy błędach i timeoutach
        /// </summary>
        public int MaxRetries { get; set; } = 5;

        /// <summary>
        /// Bazowy czas oczekiwania przy retry (sekundy) - podwajany przy każdej próbie
        /// </summary>
        public int RetryBaseDelaySeconds { get; set; } = 5;

        // === LIMITY ARTYKUŁÓW ===

        /// <summary>
        /// Maksymalna liczba artykułów do przetworzenia (tryb pełny)
        /// </summary>
        public int MaxArticles { get; set; } = 25;

        /// <summary>
        /// Liczba artykułów w trybie szybkim
        /// </summary>
        public int QuickModeArticles { get; set; } = 10;

        /// <summary>
        /// Liczba artykułów w trybie "1 artykuł" (testowy)
        /// </summary>
        public int SingleArticleMode { get; set; } = 1;

        /// <summary>
        /// Maksymalna liczba wyników z jednego zapytania Brave
        /// </summary>
        public int MaxResultsPerQuery { get; set; } = 10;

        // === FUNKCJE ===

        /// <summary>
        /// Czy Puppeteer jest włączony (dla trudnych stron)
        /// </summary>
        public bool PuppeteerEnabled { get; set; } = true;

        /// <summary>
        /// Czy śledzenie kosztów API jest włączone
        /// </summary>
        public bool CostTrackingEnabled { get; set; } = true;

        /// <summary>
        /// Czy zapisywać szczegółowe logi do pliku TXT
        /// </summary>
        public bool FileLoggingEnabled { get; set; } = true;

        /// <summary>
        /// Czy otwierać folder z logami po zakończeniu
        /// </summary>
        public bool OpenLogsFolderAfterSession { get; set; } = false;

        // === TEST "1 ARTYKUŁ" ===

        /// <summary>
        /// Zapytanie testowe dla trybu "1 artykuł" (można ręcznie zmienić)
        /// </summary>
        public string TestSearchQuery { get; set; } = "ceny drobiu Polska 2026";

        // === CACHE ===

        /// <summary>
        /// Czas ważności cache artykułów (godziny)
        /// </summary>
        public int CacheExpirationHours { get; set; } = 24;

        /// <summary>
        /// Czy używać cache dla wyników Brave
        /// </summary>
        public bool UseBraveCache { get; set; } = true;
    }

    /// <summary>
    /// Kontekst biznesowy firmy Piórkowscy
    /// </summary>
    public class BusinessContext
    {
        /// <summary>
        /// Nazwa firmy
        /// </summary>
        public string CompanyName { get; set; } = "Ubojnia Drobiu Piórkowscy";

        /// <summary>
        /// Lokalizacja
        /// </summary>
        public string Location { get; set; } = "Koziołek, 95-060 Brzeziny, woj. łódzkie";

        /// <summary>
        /// Dyrektor/Prezes
        /// </summary>
        public string Director { get; set; } = "Justyna Chrostowska";

        /// <summary>
        /// Województwo (ważne dla alertów HPAI)
        /// </summary>
        public string Region { get; set; } = "łódzkie";

        /// <summary>
        /// Zespół handlowy
        /// </summary>
        public List<SalesPersonConfig> SalesTeam { get; set; } = new List<SalesPersonConfig>();

        /// <summary>
        /// Hodowcy dostarczający drób
        /// </summary>
        public List<FarmerConfig> Farmers { get; set; } = new List<FarmerConfig>();

        /// <summary>
        /// Główni konkurenci
        /// </summary>
        public List<string> Competitors { get; set; } = new List<string>();

        /// <summary>
        /// Główni klienci (sieci handlowe)
        /// </summary>
        public List<string> MainClients { get; set; } = new List<string>();

        /// <summary>
        /// Dodatkowy kontekst finansowy/operacyjny
        /// </summary>
        public string FinancialContext { get; set; } = string.Empty;

        /// <summary>
        /// Specjalizacja firmy (np. "ubój i przetwórstwo drobiu")
        /// </summary>
        public string Specialization { get; set; } = "ubój i przetwórstwo drobiu";

        /// <summary>
        /// Skala produkcji (np. "średnia ubojnia")
        /// </summary>
        public string ProductionScale { get; set; } = "średnia ubojnia regionalna";
    }

    /// <summary>
    /// Konfiguracja handlowca
    /// </summary>
    public class SalesPersonConfig
    {
        /// <summary>
        /// Imię handlowca
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Obsługiwani klienci (np. "Dino, Biedronka, Lidl")
        /// </summary>
        public string Clients { get; set; } = string.Empty;

        /// <summary>
        /// Region odpowiedzialności
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Czy aktywny
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Konfiguracja hodowcy
    /// </summary>
    public class FarmerConfig
    {
        /// <summary>
        /// Nazwa/nazwisko hodowcy
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Odległość od ubojni (km)
        /// </summary>
        public int DistanceKm { get; set; } = 0;

        /// <summary>
        /// Lokalizacja (miejscowość)
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Typ produkcji (np. "brojlery", "indyki")
        /// </summary>
        public string ProductionType { get; set; } = "brojlery";

        /// <summary>
        /// Czy aktywny dostawca
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
