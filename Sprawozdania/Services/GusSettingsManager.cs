using System;
using System.IO;
using System.Text.Json;
using Kalendarz1.Sprawozdania.Models;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // Zapis/odczyt konfiguracji modułu GUS — JSON w %LOCALAPPDATA%.
    // To NIE secrets (brak haseł API) — to dane jednostki sprawozdawczej.
    // Trzymamy poza repo (per-user), żeby każde stanowisko mogło mieć
    // ewentualnie inną osobę odpowiedzialną.
    // ════════════════════════════════════════════════════════════════════
    public static class GusSettingsManager
    {
        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kalendarz1", "Gus");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
        }

        public static GusSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var s = JsonSerializer.Deserialize<GusSettings>(json);
                    if (s != null) return EnsureDefaults(s);
                }
            }
            catch { /* fallthrough — zwracamy defaults */ }

            return EnsureDefaults(new GusSettings());
        }

        public static void Save(GusSettings s)
        {
            var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }

        public static string DomyslnyFolderEksportu()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ZPSP", "GUS");
        }

        private static GusSettings EnsureDefaults(GusSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.FolderEksportu))
                s.FolderEksportu = DomyslnyFolderEksportu();

            try { Directory.CreateDirectory(s.FolderEksportu); } catch { }

            return s;
        }
    }
}
