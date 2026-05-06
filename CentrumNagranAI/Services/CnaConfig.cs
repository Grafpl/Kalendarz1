using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Konfiguracja modułu Centrum nagrań AI (CNA). Lazy-load z pliku secrets.json
    /// w %LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\ — POZA repo Git, by uniknąć wycieku
    /// klucza Anthropic i danych logowania do NVR.
    /// </summary>
    public static class CnaConfig
    {
        public static string AnthropicApiKey { get; private set; } = string.Empty;
        public static List<CnaCameraEndpoint> Kamery { get; private set; } = new();

        // Stałe interwały i progi PoC. Docelowo można wciągnąć z appsettings.json.
        public const int InterwalKlatkiSekund = 10;
        public const int RetencjaDni = 3;
        public const int TopKCandydatow = 50;
        public const int TopNFinalnych = 10;

        // Ścieżki — wszystkie pochodne BaseDir, %LOCALAPPDATA%\Kalendarz1\CentrumNagranAI.
        public static string BaseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "CentrumNagranAI");
        public static string SecretsPath => Path.Combine(BaseDir, "secrets.json");
        public static string DbPath => Path.Combine(BaseDir, "index.db");
        public static string FramesDir => Path.Combine(BaseDir, "frames");
        public static string AuditDir => Path.Combine(BaseDir, "audit");

        // FFmpeg leży w tools/ffmpeg/ (gitignored). Szukamy ffmpeg.exe rekurencyjnie.
        public static string FfmpegExePath { get; private set; } = string.Empty;

        private static bool _zaladowano;
        private static readonly object _lock = new();

        public static void ZaladujJesliTrzeba()
        {
            if (_zaladowano) return;
            lock (_lock)
            {
                if (_zaladowano) return;
                Zaladuj();
                _zaladowano = true;
            }
        }

        private static void Zaladuj()
        {
            // 1) Klucz Anthropic + lista kamer z secrets.json (priorytet) lub ENV (fallback).
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
                            int defaultStream = nvr["DefaultStreamType"]?.Value<int?>() ?? 0; // 0=main, 1=sub
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
            catch
            {
                // Plik niedostępny / niepoprawny JSON — fallback do ENV.
            }

            if (string.IsNullOrEmpty(AnthropicApiKey))
            {
                AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
            }

            // 2) Lokalizacja ffmpeg.exe — szukamy w tools/ffmpeg/ obok exe lub w katalogu projektu.
            FfmpegExePath = ZnajdzFfmpeg() ?? string.Empty;

            // 3) Stwórz katalogi danych.
            Directory.CreateDirectory(FramesDir);
            Directory.CreateDirectory(AuditDir);
        }

        private static string? ZnajdzFfmpeg()
        {
            var kandydaci = new[]
            {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory,
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..") // dev: bin\Debug\net8.0-windows7.0 → root
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

    /// <summary>
    /// Pojedyncza kamera = jeden kanał w danym NVR. Pełny URL RTSP budowany w RtspFrameGrabber
    /// żeby login/hasło nie wisiały w polach POCO dłużej niż konieczne.
    /// </summary>
    public class CnaCameraEndpoint
    {
        public string Id { get; set; } = string.Empty;
        public string NvrName { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int RtspPort { get; set; } = 554;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Channel { get; set; }

        // 0 = main stream (zawsze dostępny w Internec, ~2-4 Mbps),
        // 1 = sub stream (lżejszy, ale nie każdy kanał go ma).
        public int StreamType { get; set; }
    }
}
