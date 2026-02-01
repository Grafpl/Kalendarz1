namespace Kalendarz1.HandlowiecDashboard.Constants
{
    /// <summary>
    /// Stałe biznesowe używane w całej aplikacji HandlowiecDashboard
    /// </summary>
    public static class BusinessConstants
    {
        /// <summary>
        /// ID katalogów produktów w bazie Handel
        /// </summary>
        public static class Katalogi
        {
            /// <summary>Katalog produktów świeżych (mięso świeże)</summary>
            public const int Swieze = 67095;

            /// <summary>Katalog produktów mrożonych</summary>
            public const int Mrozone = 67153;
        }

        /// <summary>
        /// Teksty filtrów używane w ComboBoxach
        /// </summary>
        public static class Filtry
        {
            public const string WszyscyHandlowcy = "— Wszyscy —";
            public const string Nieprzypisany = "Nieprzypisany";

            /// <summary>Handlowcy wykluczeni z raportów (konta systemowe)</summary>
            public static readonly string[] WykluczeniHandlowcy = { "Ogolne", "Ogólne" };
        }

        /// <summary>
        /// Domyślne wartości konfiguracyjne
        /// </summary>
        public static class Defaults
        {
            /// <summary>Timeout dla komend SQL w sekundach</summary>
            public const int CommandTimeoutSeconds = 60;

            /// <summary>Czas życia cache w minutach</summary>
            public const int CacheExpiryMinutes = 5;

            /// <summary>Opóźnienie debounce dla filtrów w ms</summary>
            public const int DebounceDelayMs = 300;
        }
    }
}
