using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Konfiguracja modułu Centrum nagrań AI (CNA).
    /// Dwa pliki w %LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\ (poza repo Git):
    ///   - secrets.json     — klucz Anthropic + creds NVR (nigdy nie commitować, nigdy w UI)
    ///   - cna_settings.json — ustawienia edytowalne z UI (nazwy kamer, retencja, interwał)
    /// </summary>
    public static class CnaConfig
    {
        public static string AnthropicApiKey { get; private set; } = string.Empty;
        public static List<CnaCameraEndpoint> Kamery { get; private set; } = new();

        // Ustawienia edytowalne — wartości domyślne, nadpisywane z cna_settings.json.
        public static int InterwalKlatkiSekund { get; set; } = 10;
        public static int RetencjaDni { get; set; } = 3;
        public static int TopKCandydatow { get; set; } = 50;
        public static int TopNFinalnych { get; set; } = 10;

        // Mapa: cameraId → display name (np. "NVR1-Ubojnia-CH01" → "Hala uboju").
        public static Dictionary<string, string> NazwyKamer { get; set; } = new();

        public static string BaseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "CentrumNagranAI");
        public static string SecretsPath => Path.Combine(BaseDir, "secrets.json");
        public static string SettingsPath => Path.Combine(BaseDir, "cna_settings.json");
        public static string DbPath => Path.Combine(BaseDir, "index.db");
        public static string FramesDir => Path.Combine(BaseDir, "frames");
        public static string AuditDir => Path.Combine(BaseDir, "audit");

        public static string FfmpegExePath { get; private set; } = string.Empty;

        private static bool _zaladowano;
        private static readonly object _lock = new();

        public static void ZaladujJesliTrzeba()
        {
            if (_zaladowano) return;
            lock (_lock)
            {
                if (_zaladowano) return;
                ZaladujSekrety();
                ZaladujUstawienia();
                FfmpegExePath = ZnajdzFfmpeg() ?? string.Empty;
                Directory.CreateDirectory(FramesDir);
                Directory.CreateDirectory(AuditDir);
                _zaladowano = true;
            }
        }

        /// <summary>
        /// Wymuszone przeładowanie - po zapisie ustawień z UI.
        /// </summary>
        public static void Przeladuj()
        {
            lock (_lock)
            {
                _zaladowano = false;
                Kamery = new();
                NazwyKamer = new();
                ZaladujSekrety();
                ZaladujUstawienia();
                _zaladowano = true;
            }
        }

        public static string DisplayName(CnaCameraEndpoint k) =>
            NazwyKamer.TryGetValue(k.Id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : k.Id;

        public static string DisplayName(string cameraId) =>
            NazwyKamer.TryGetValue(cameraId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : cameraId;

        private static void ZaladujSekrety()
        {
            try
            {
                if (File.Exists(SecretsPath))
                {
                    var json = JObject.Parse(File.ReadAllText(SecretsPath));
                    AnthropicApiKey = json["AnthropicApiKey"]?.Value<string>() ?? string.Empty;

                    var nvrArr = json["Nvr"] as JArray;
                    if (nvrArr != null)
                    {
                        foreach (var nvr in nvrArr)
                        {
                            string name = nvr["Name"]?.Value<string>() ?? "NVR";
                            string host = nvr["Host"]?.Value<string>() ?? string.Empty;
                            int port = nvr["RtspPort"]?.Value<int?>() ?? 554;
                            string user = nvr["User"]?.Value<string>() ?? string.Empty;
                            string pass = nvr["Password"]?.Value<string>() ?? string.Empty;
                            int defaultStream = nvr["DefaultStreamType"]?.Value<int?>() ?? 0;
                            var chans = nvr["Channels"] as JArray;
                            if (chans == null) continue;

                            foreach (var c in chans)
                            {
                                int ch = c.Value<int>();
                                Kamery.Add(new CnaCameraEndpoint
                                {
                                    Id = $"{name}-CH{ch:D2}",
                                    NvrName = name,
                                    Host = host,
                                    RtspPort = port,
                                    User = user,
                                    Password = pass,
                                    Channel = ch,
                                    StreamType = defaultStream
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(AnthropicApiKey))
                AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
        }

        private static void ZaladujUstawienia()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var json = JObject.Parse(File.ReadAllText(SettingsPath));
                InterwalKlatkiSekund = json["InterwalKlatkiSekund"]?.Value<int?>() ?? InterwalKlatkiSekund;
                RetencjaDni = json["RetencjaDni"]?.Value<int?>() ?? RetencjaDni;
                TopKCandydatow = json["TopKCandydatow"]?.Value<int?>() ?? TopKCandydatow;
                TopNFinalnych = json["TopNFinalnych"]?.Value<int?>() ?? TopNFinalnych;

                var names = json["NazwyKamer"] as JObject;
                if (names != null)
                {
                    NazwyKamer = names.Properties().ToDictionary(p => p.Name, p => p.Value.Value<string>() ?? string.Empty);
                }
            }
            catch { }
        }

        /// <summary>
        /// Zapis ustawień do cna_settings.json. Wywoływane z SettingsWindow po zmianie.
        /// </summary>
        public static void ZapiszUstawienia()
        {
            var obj = new JObject
            {
                ["InterwalKlatkiSekund"] = InterwalKlatkiSekund,
                ["RetencjaDni"] = RetencjaDni,
                ["TopKCandydatow"] = TopKCandydatow,
                ["TopNFinalnych"] = TopNFinalnych,
                ["NazwyKamer"] = JObject.FromObject(NazwyKamer)
            };
            Directory.CreateDirectory(BaseDir);
            File.WriteAllText(SettingsPath, obj.ToString(Formatting.Indented), System.Text.Encoding.UTF8);
        }

        private static string? ZnajdzFfmpeg()
        {
            var kandydaci = new[]
            {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory,
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..")
            };
            foreach (var baza in kandydaci)
            {
                var toolsDir = Path.Combine(baza, "tools", "ffmpeg");
                if (!Directory.Exists(toolsDir)) continue;
                var found = Directory.GetFiles(toolsDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (found != null) return Path.GetFullPath(found);
            }
            return null;
        }
    }

    public class CnaCameraEndpoint
    {
        public string Id { get; set; } = string.Empty;
        public string NvrName { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int RtspPort { get; set; } = 554;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Channel { get; set; }
        public int StreamType { get; set; }
    }
}
