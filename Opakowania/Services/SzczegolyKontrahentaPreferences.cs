using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Kalendarz1.Opakowania.Services
{
    public class SzczegolyKontrahentaPrefs
    {
        public bool GrupujPoDniach { get; set; } = true;

        // 0 = collapsed all, 1 = expanded all, 2 = mixed (per-day state nieuzywany teraz)
        public bool ExpandAllGroups { get; set; } = true;

        // Pokazuj dni bez dokumentow jako pusty wiersz placeholder
        public bool PokazPusteDni { get; set; } = false;

        // Ostatni wybrany zakres dat (dni od dziś). null = uzyj domyslnego (3 mies.)
        public int? OstatniDniOd { get; set; } = null;
        public int? OstatniDniDo { get; set; } = null;

        // Szerokosci kolumn (Name → Width)
        public Dictionary<string, int> ColumnWidths { get; set; } = new();

        public int SchemaVersion { get; set; } = 1;
    }

    public static class SzczegolyKontrahentaPreferencesService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string GetPrefsDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Kalendarz1");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        private static string GetPrefsFilePath(string userId)
        {
            string safe = string.IsNullOrWhiteSpace(userId) ? "default" : userId;
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return Path.Combine(GetPrefsDirectory(), $"opakowania_szczegoly_{safe}.json");
        }

        public static SzczegolyKontrahentaPrefs Load(string userId)
        {
            try
            {
                string path = GetPrefsFilePath(userId);
                if (!File.Exists(path)) return new SzczegolyKontrahentaPrefs();
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SzczegolyKontrahentaPrefs>(json, JsonOpts) ?? new SzczegolyKontrahentaPrefs();
            }
            catch { return new SzczegolyKontrahentaPrefs(); }
        }

        public static void Save(string userId, SzczegolyKontrahentaPrefs prefs)
        {
            if (prefs == null) return;
            try
            {
                string path = GetPrefsFilePath(userId);
                File.WriteAllText(path, JsonSerializer.Serialize(prefs, JsonOpts));
            }
            catch { }
        }
    }
}
