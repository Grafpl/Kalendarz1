using System;

namespace Kalendarz1.MarketIntelligence
{
    /// <summary>
    /// Centralna konfiguracja modułu Market Intelligence / Poranny Briefing.
    /// Wcześniej connection string LibraNet był hardcoded w 5 plikach
    /// (BriefingDataLoaderService, NewsFetchOrchestrator, ContentFilterService,
    ///  MarketIntelligenceService, ContextBuilderService, DatabaseSetup).
    /// Teraz wszystkie konsumują tę klasę.
    /// </summary>
    internal static class MarketIntelligenceConfig
    {
        /// <summary>
        /// LibraNet — tabele intel_*, źródło danych biznesowych (klienci, dostawcy, ceny).
        /// Override: env LIBRANET_CONNECTION_STRING lub secrets.json.
        /// </summary>
        public static string LibraNetConnectionString =>
            Environment.GetEnvironmentVariable("LIBRANET_CONNECTION_STRING")
            ?? SecretsLoader.Get("LIBRANET_CONNECTION_STRING")
            ?? "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        /// <summary>
        /// HANDEL (Sage Symfonia) — schemat HM.* używany przez ContextBuilder do kontekstu klientów/handlu.
        /// Override: env HANDEL_CONNECTION_STRING lub secrets.json.
        /// </summary>
        public static string HandelConnectionString =>
            Environment.GetEnvironmentVariable("HANDEL_CONNECTION_STRING")
            ?? SecretsLoader.Get("HANDEL_CONNECTION_STRING")
            ?? "Server=192.168.0.112;Database=Handel;User Id=pronova;Password=pronova;TrustServerCertificate=True";
    }
}
