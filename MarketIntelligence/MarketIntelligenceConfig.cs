using System;
using System.Diagnostics;

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
        private static bool _warnedInsecureHandel;

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
        /// 2026-05-25 (Faza A): przywrócono łańcuch env → secrets.json → hardcoded fallback.
        /// Hardcoded `sa` to OSTATNIA deska ratunku — żeby z niego zejść, wpisz poprawne dane
        /// (najlepiej read-only user `zpsp_intel_reader`) do secrets.json pod kluczem
        /// HANDEL_CONNECTION_STRING. Patrz MarketIntelligence/README.md.
        /// </summary>
        public static string HandelConnectionString
        {
            get
            {
                var fromEnv = Environment.GetEnvironmentVariable("HANDEL_CONNECTION_STRING");
                if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

                var fromSecrets = SecretsLoader.Get("HANDEL_CONNECTION_STRING");
                if (!string.IsNullOrWhiteSpace(fromSecrets)) return fromSecrets;

                WarnInsecureHandel();
                return "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            }
        }

        /// <summary>Ostrzeżenie raz na uruchomienie aplikacji gdy używany jest hardcoded `sa`.</summary>
        private static void WarnInsecureHandel()
        {
            if (_warnedInsecureHandel) return;
            _warnedInsecureHandel = true;
            Debug.WriteLine("⚠ SECURITY: hardcoded `sa` credentials used for HANDEL. " +
                            "Move to secrets.json key HANDEL_CONNECTION_STRING (read-only user). " +
                            "See MarketIntelligence/README.md.");
        }
    }
}
