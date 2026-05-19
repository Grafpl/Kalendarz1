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
        /// 2026-05-19: HARDCODED ZAWSZE (user request 'wklep mi te dane sztywno w kod').
        /// secrets.json i env IGNOROWANE bo user'a secrets.json miał stary pronova/pronova
        /// który nadpisywał hardcoded sa i wywalał login HANDEL na każdym fetchu.
        /// </summary>
        public static string HandelConnectionString =>
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
    }
}
