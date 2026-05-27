namespace Kalendarz1.MarketIntelligence
{
    /// <summary>
    /// Centralne flagi funkcji modułu Briefing/MarketIntelligence.
    /// 2026-05-25 (Faza A): wyłączone „martwe" etapy pipeline'u które stale zwracały 0
    /// (scrapery HTML, GLW HPAI 404, scrapery cen MRiRW/MATIF). Kod scraperów ZOSTAJE
    /// w repo (do ewentualnej naprawy), ale nie jest już uruchamiany przy każdym fetchu.
    /// Aby włączyć z powrotem — ustaw flagę na true (jedno miejsce).
    /// </summary>
    internal static class BriefingFeatureFlags
    {
        /// <summary>Scrapery HTML (PIORiN/KOWR/KRIR/WIR) — zwracały 0 artykułów. Domyślnie OFF.</summary>
        public static bool EnableScrapingSources = false;

        /// <summary>Scraper alertów HPAI z GLW (wetgiw.gov.pl) — zwraca 404. Domyślnie OFF.</summary>
        public static bool EnableHpaiScraper = false;

        /// <summary>Scrapery cen (MRiRW poultry + MATIF commodity) — zwracały 0. Domyślnie OFF.</summary>
        public static bool EnablePriceScrapers = false;

        /// <summary>
        /// Wzbogacanie pełnej treści artykułu (web scraping treści). Domyślnie ON,
        /// ale ograniczone do whitelisty domen (patrz EnrichmentDomainWhitelist) + walidacja
        /// binarnego śmiecia w ContentEnrichmentService.
        /// </summary>
        public static bool EnableContentEnrichment = true;

        /// <summary>
        /// Domeny dla których content enrichment działa wiarygodnie. Reszta = skip
        /// (zapobiega wciąganiu binarnego śmiecia / nieparsowalnych stron).
        /// </summary>
        public static readonly string[] EnrichmentDomainWhitelist =
        {
            "farmer.pl",
            "portalspozywczy.pl",
            "wnp.pl",
            "cenyrolnicze.pl"
        };
    }
}
