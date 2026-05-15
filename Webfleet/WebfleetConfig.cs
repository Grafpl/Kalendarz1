using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.Webfleet
{
    /// <summary>
    /// Konfiguracja integracji Webfleet.connect. Wzorzec mirror'owany z CnaConfig
    /// (CentrumNagranAI/Services/CnaConfig.cs).
    ///
    /// Plik: %LOCALAPPDATA%\Kalendarz1\Webfleet\secrets.json (poza repo, .gitignore).
    /// Bootstrap: przy pierwszym uruchomieniu po refactorze pliku nie ma → tworzymy go
    /// z wartościami historycznie hardcoded (zachowanie back-compat).
    ///
    /// Auto-load: properties uruchamiają ZaladujJesliTrzeba() przy pierwszym dostępie,
    /// więc konsument nie musi nic wołać explicit'em.
    /// </summary>
    public static class WebfleetConfig
    {
        // Historyczne wartości — używane jako fallback i jako seed do bootstrapu.
        // Po pierwszym uruchomieniu trafiają do %LOCALAPPDATA%\…\secrets.json.
        private const string DefaultAccount = "942879";
        private const string DefaultUser    = "Administrator";
        private const string DefaultPass    = "kaazZVY5";
        private const string DefaultApiKey  = "7a538868-96cf-4149-a9db-6e090de7276c";
        private const string DefaultBaseUrl = "https://csv.webfleet.com/extern";

        private static string _account = DefaultAccount;
        private static string _user    = DefaultUser;
        private static string _pass    = DefaultPass;
        private static string _apiKey  = DefaultApiKey;
        private static string _baseUrl = DefaultBaseUrl;

        public static string Account { get { ZaladujJesliTrzeba(); return _account; } }
        public static string User    { get { ZaladujJesliTrzeba(); return _user;    } }
        public static string Pass    { get { ZaladujJesliTrzeba(); return _pass;    } }
        public static string ApiKey  { get { ZaladujJesliTrzeba(); return _apiKey;  } }
        public static string BaseUrl { get { ZaladujJesliTrzeba(); return _baseUrl; } }

        public static string BaseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "Webfleet");
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
                _account = json["Account"]?.Value<string>() ?? DefaultAccount;
                _user    = json["User"]?.Value<string>()    ?? DefaultUser;
                _pass    = json["Pass"]?.Value<string>()    ?? DefaultPass;
                _apiKey  = json["ApiKey"]?.Value<string>()  ?? DefaultApiKey;
                _baseUrl = json["BaseUrl"]?.Value<string>() ?? DefaultBaseUrl;
            }
            catch
            {
                // popsuty plik → defaults z kodu
            }
        }

        private static void Bootstrap()
        {
            try
            {
                var json = new JObject
                {
                    ["Account"]  = DefaultAccount,
                    ["User"]     = DefaultUser,
                    ["Pass"]     = DefaultPass,
                    ["ApiKey"]   = DefaultApiKey,
                    ["BaseUrl"]  = DefaultBaseUrl,
                    ["_comment"] = "Sekrety Webfleet. Plik utworzony automatycznie przy pierwszym uruchomieniu. " +
                                   "Zmiany ręczne wymagają restartu aplikacji."
                };
                File.WriteAllText(SecretsPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
                // jeśli nie da się zapisać, używamy defaults
            }
        }

        /// <summary>Wymuszone przeładowanie — po ręcznej edycji pliku.</summary>
        public static void Przeladuj()
        {
            lock (_lock) { _zaladowano = false; }
            ZaladujJesliTrzeba();
        }
    }
}
