using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.Maps
{
    /// <summary>
    /// Konfiguracja Google Maps API (geocoding + maps embedding).
    /// Wzorzec mirror'owany z Webfleet/WebfleetConfig.
    ///
    /// Plik: %LOCALAPPDATA%\Kalendarz1\Maps\secrets.json (poza repo, .gitignore).
    /// Bootstrap z historyczną wartością z TransportMapaWindow.cs:24.
    /// </summary>
    public static class GoogleMapsConfig
    {
        // Historyczna wartość z TransportMapaWindow.cs:24 (przed Fazą 4-A).
        // Po pierwszym uruchomieniu trafia do %LOCALAPPDATA%\…\secrets.json.
        private const string DefaultApiKey = "AIzaSyCFXL2NYDnLBpiih1pG27SbsY62ZYsKdgo";

        private static string _apiKey = DefaultApiKey;

        public static string ApiKey { get { ZaladujJesliTrzeba(); return _apiKey; } }

        public static string BaseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "Maps");
        public static string SecretsPath => Path.Combine(BaseDir, "secrets.json");

        private static bool _zaladowano;
        private static readonly object _lock = new();

        public static void ZaladujJesliTrzeba()
        {
            if (_zaladowano) return;
            lock (_lock)
            {
                if (_zaladowano) return;
                try
                {
                    Directory.CreateDirectory(BaseDir);
                    if (File.Exists(SecretsPath))
                        Wczytaj();
                    else
                        Bootstrap();
                }
                catch
                {
                    // defensive: jeśli I/O zawiedzie, używamy defaults
                }
                _zaladowano = true;
            }
        }

        private static void Wczytaj()
        {
            try
            {
                var json = JObject.Parse(File.ReadAllText(SecretsPath));
                _apiKey = json["ApiKey"]?.Value<string>() ?? DefaultApiKey;
            }
            catch { /* defaults */ }
        }

        private static void Bootstrap()
        {
            try
            {
                var json = new JObject
                {
                    ["ApiKey"]   = DefaultApiKey,
                    ["_comment"] = "Google Maps API key — geocoding + maps. Plik utworzony automatycznie. " +
                                   "Po wymianie klucza zedytuj plik i wywołaj GoogleMapsConfig.Przeladuj()."
                };
                File.WriteAllText(SecretsPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch { /* read-only profile */ }
        }

        public static void Przeladuj()
        {
            lock (_lock) { _zaladowano = false; }
            ZaladujJesliTrzeba();
        }
    }
}
