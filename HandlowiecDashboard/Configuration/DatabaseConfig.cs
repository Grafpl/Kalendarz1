using System;
using System.Configuration;

namespace Kalendarz1.HandlowiecDashboard.Configuration
{
    /// <summary>
    /// Centralna konfiguracja połączeń z bazami danych.
    /// NIGDY nie przechowuj haseł w kodzie źródłowym!
    /// Connection stringi odczytywane z App.config lub zmiennych środowiskowych.
    /// </summary>
    public static class DatabaseConfig
    {
        private static string _handelConnectionString;
        private static string _libraNetConnectionString;

        /// <summary>
        /// Connection string do bazy Handel (192.168.0.112)
        /// Priorytet: 1) App.config 2) Zmienna środowiskowa 3) Błąd
        /// </summary>
        public static string HandelConnectionString
        {
            get
            {
                if (_handelConnectionString == null)
                {
                    _handelConnectionString =
                        ConfigurationManager.ConnectionStrings["Handel"]?.ConnectionString
                        ?? Environment.GetEnvironmentVariable("HANDEL_CONNECTION_STRING")
                        ?? throw new InvalidOperationException(
                            "Brak konfiguracji połączenia 'Handel'. " +
                            "Dodaj do App.config lub ustaw zmienną środowiskową HANDEL_CONNECTION_STRING");
                }
                return _handelConnectionString;
            }
        }

        /// <summary>
        /// Connection string do bazy LibraNet (192.168.0.109)
        /// Priorytet: 1) App.config 2) Zmienna środowiskowa 3) Błąd
        /// </summary>
        public static string LibraNetConnectionString
        {
            get
            {
                if (_libraNetConnectionString == null)
                {
                    _libraNetConnectionString =
                        ConfigurationManager.ConnectionStrings["LibraNet"]?.ConnectionString
                        ?? Environment.GetEnvironmentVariable("LIBRANET_CONNECTION_STRING")
                        ?? throw new InvalidOperationException(
                            "Brak konfiguracji połączenia 'LibraNet'. " +
                            "Dodaj do App.config lub ustaw zmienną środowiskową LIBRANET_CONNECTION_STRING");
                }
                return _libraNetConnectionString;
            }
        }

        /// <summary>
        /// Resetuje cache connection stringów (przydatne do testów)
        /// </summary>
        public static void Reset()
        {
            _handelConnectionString = null;
            _libraNetConnectionString = null;
        }
    }
}
