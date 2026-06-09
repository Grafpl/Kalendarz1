using System;
using System.IO;
using Kalendarz1.SkrzynkaZakupu.Models;
using Newtonsoft.Json;

namespace Kalendarz1.SkrzynkaZakupu.Services
{
    /// <summary>
    /// Przechowuje dane dostępowe skrzynki LOKALNIE, poza repozytorium:
    /// %LOCALAPPDATA%\Kalendarz1\SkrzynkaZakupu\secrets.json
    /// Hasło NIGDY nie trafia do kodu ani do gita.
    /// </summary>
    public static class MailSecretsStore
    {
        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kalendarz1", "SkrzynkaZakupu");

        private static string FilePath => Path.Combine(Dir, "secrets.json");

        /// <summary>Czy hasło jest już zapisane na tym komputerze.</summary>
        public static bool MaConfig()
        {
            try
            {
                if (!File.Exists(FilePath)) return false;
                var s = Load();
                return s != null && !string.IsNullOrWhiteSpace(s.Password);
            }
            catch { return false; }
        }

        public static MailAccountSettings Load()
        {
            if (!File.Exists(FilePath))
                return new MailAccountSettings();
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonConvert.DeserializeObject<MailAccountSettings>(json) ?? new MailAccountSettings();
            }
            catch
            {
                return new MailAccountSettings();
            }
        }

        public static void Save(MailAccountSettings settings)
        {
            Directory.CreateDirectory(Dir);
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }
}
