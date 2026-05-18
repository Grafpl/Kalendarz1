using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Kalendarz1.MarketIntelligence
{
    /// <summary>
    /// Ładuje sekrety (API keys, conn stringi) z pliku JSON w %LOCALAPPDATA%.
    /// **Plik jest poza repo** — nigdy się nie commituje, bezpieczny dla GitHub.
    ///
    /// Ścieżka: %LOCALAPPDATA%\Kalendarz1\MarketIntelligence\secrets.json
    ///
    /// Format pliku:
    /// {
    ///   "ANTHROPIC_API_KEY": "sk-ant-...",
    ///   "PERPLEXITY_API_KEY": "pplx-...",
    ///   "LIBRANET_CONNECTION_STRING": null,    // null = użyj default
    ///   "HANDEL_CONNECTION_STRING": null,
    ///   "PERPLEXITY_DAILY_LIMIT": null
    /// }
    /// </summary>
    internal static class SecretsLoader
    {
        public static string SecretsFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "MarketIntelligence", "secrets.json");

        private static Dictionary<string, string> _cache;
        private static DateTime _lastLoad = DateTime.MinValue;
        private static readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(60);
        private static readonly object _lock = new();

        /// <summary>
        /// Zwraca wartość sekretu, lub null jeśli nie ma.
        /// Kolejność: secrets.json → null.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            lock (_lock)
            {
                EnsureLoaded();
                if (_cache != null && _cache.TryGetValue(key, out var val))
                {
                    return string.IsNullOrWhiteSpace(val) ? null : val;
                }
                return null;
            }
        }

        /// <summary>Czy plik secrets.json istnieje i jest wczytany?</summary>
        public static bool Exists => File.Exists(SecretsFilePath);

        /// <summary>Lista kluczy obecnych w pliku (bez wartości — do diagnostyki).</summary>
        public static List<string> GetKeyNames()
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _cache != null ? new List<string>(_cache.Keys) : new List<string>();
            }
        }

        /// <summary>Wymuś ponowne wczytanie pliku przy następnym Get().</summary>
        public static void Invalidate()
        {
            lock (_lock) { _lastLoad = DateTime.MinValue; _cache = null; }
        }

        private static void EnsureLoaded()
        {
            if (_cache != null && DateTime.UtcNow - _lastLoad < _cacheLifetime) return;

            try
            {
                if (!File.Exists(SecretsFilePath))
                {
                    _cache = new Dictionary<string, string>();
                    _lastLoad = DateTime.UtcNow;
                    return;
                }

                var json = File.ReadAllText(SecretsFilePath);
                using var doc = JsonDocument.Parse(json);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    dict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                }
                _cache = dict;
                _lastLoad = DateTime.UtcNow;
                Debug.WriteLine($"[SecretsLoader] Wczytano {dict.Count} sekretów z {SecretsFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretsLoader] Błąd wczytywania {SecretsFilePath}: {ex.Message}");
                _cache = new Dictionary<string, string>();
                _lastLoad = DateTime.UtcNow;
            }
        }
    }
}
