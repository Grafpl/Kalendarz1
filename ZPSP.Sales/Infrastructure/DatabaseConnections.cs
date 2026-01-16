using System;

namespace ZPSP.Sales.Infrastructure
{
    /// <summary>
    /// Centralne zarządzanie connection strings do wszystkich baz danych.
    /// Eliminuje hardkodowanie connection strings w całym kodzie.
    /// </summary>
    public sealed class DatabaseConnections
    {
        private static readonly Lazy<DatabaseConnections> _instance =
            new Lazy<DatabaseConnections>(() => new DatabaseConnections());

        public static DatabaseConnections Instance => _instance.Value;

        /// <summary>
        /// LibraNet - główna baza operacyjna (zamówienia, harmonogramy, konfiguracja)
        /// </summary>
        public string LibraNet { get; private set; }

        /// <summary>
        /// Handel - baza Sage Symfonia (kontrahenci, faktury, wydania WZ)
        /// </summary>
        public string Handel { get; private set; }

        /// <summary>
        /// TransportPL - baza logistyczna (kursy, kierowcy, pojazdy)
        /// </summary>
        public string TransportPL { get; private set; }

        private DatabaseConnections()
        {
            // Domyślne connection strings - w produkcji powinny być ładowane z konfiguracji
            LibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connect Timeout=30;";
            Handel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connect Timeout=30;";
            TransportPL = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connect Timeout=30;";
        }

        /// <summary>
        /// Konfiguruje connection strings z zewnętrznego źródła (np. appsettings.json)
        /// </summary>
        /// <param name="libraNet">Connection string do LibraNet</param>
        /// <param name="handel">Connection string do Handel</param>
        /// <param name="transportPL">Connection string do TransportPL</param>
        public void Configure(string libraNet, string handel, string transportPL)
        {
            if (!string.IsNullOrWhiteSpace(libraNet))
                LibraNet = libraNet;

            if (!string.IsNullOrWhiteSpace(handel))
                Handel = handel;

            if (!string.IsNullOrWhiteSpace(transportPL))
                TransportPL = transportPL;
        }

        /// <summary>
        /// Ładuje konfigurację z pliku JSON (opcjonalne)
        /// </summary>
        /// <param name="configPath">Ścieżka do pliku konfiguracyjnego</param>
        public void LoadFromFile(string configPath)
        {
            if (!System.IO.File.Exists(configPath))
                return;

            try
            {
                var json = System.IO.File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ConnectionStringsConfig>(json);

                if (config != null)
                {
                    Configure(config.LibraNet, config.Handel, config.TransportPL);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania konfiguracji: {ex.Message}");
            }
        }

        private class ConnectionStringsConfig
        {
            public string LibraNet { get; set; }
            public string Handel { get; set; }
            public string TransportPL { get; set; }
        }
    }
}
