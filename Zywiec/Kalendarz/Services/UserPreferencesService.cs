using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    /// <summary>
    /// Preferencje użytkownika dla okna Kalendarz Dostaw.
    /// Zapisywane per-user w %AppData%\Kalendarz1\kalendarz_prefs_{userId}.json
    /// </summary>
    public class KalendarzUserPreferences
    {
        // Filtry checkbox
        public bool ChkAnulowane { get; set; } = false;
        public bool ChkSprzedane { get; set; } = false;
        public bool ChkDoWykupienia { get; set; } = true;
        public bool ChkPokazCeny { get; set; } = true;
        public bool ChkPokazCheckboxy { get; set; } = false;
        public bool ChkNastepnyTydzien { get; set; } = true;

        // Szerokości kolumn (Header → Width). Tylko liczbowe, gwiazdkowe pomijamy.
        public Dictionary<string, double> ColumnWidths { get; set; } = new Dictionary<string, double>();

        // Pozycja i rozmiar okna (jeśli nie maximized)
        public bool WindowMaximized { get; set; } = true;
        public double WindowWidth { get; set; } = 1920;
        public double WindowHeight { get; set; } = 1000;
        public double WindowLeft { get; set; } = -1; // -1 = nie zapisane
        public double WindowTop { get; set; } = -1;

        // Wersja schematu - dla migracji w przyszłości
        public int SchemaVersion { get; set; } = 1;
    }

    /// <summary>
    /// Serwis zapisu/odczytu preferencji użytkownika.
    /// Tolerancyjny na błędy I/O - jeśli nie da się odczytać, zwraca domyślne.
    /// </summary>
    public static class UserPreferencesService
    {
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string GetPrefsDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Kalendarz1");
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { /* tolerancyjne */ }
            return dir;
        }

        private static string GetPrefsFilePath(string userId)
        {
            string safeUserId = string.IsNullOrWhiteSpace(userId) ? "default" : userId;
            // Sanityzacja - na wypadek dziwnych znaków w UserID
            foreach (char c in Path.GetInvalidFileNameChars())
                safeUserId = safeUserId.Replace(c, '_');
            return Path.Combine(GetPrefsDirectory(), $"kalendarz_prefs_{safeUserId}.json");
        }

        /// <summary>
        /// Wczytaj preferencje użytkownika. Zwraca domyślne jeśli plik nie istnieje lub błąd.
        /// </summary>
        public static KalendarzUserPreferences Load(string userId)
        {
            try
            {
                string path = GetPrefsFilePath(userId);
                if (!File.Exists(path)) return new KalendarzUserPreferences();
                string json = File.ReadAllText(path);
                var prefs = JsonSerializer.Deserialize<KalendarzUserPreferences>(json, JsonOpts);
                return prefs ?? new KalendarzUserPreferences();
            }
            catch
            {
                return new KalendarzUserPreferences();
            }
        }

        /// <summary>
        /// Zapisz preferencje użytkownika. Cicho ignoruje błędy I/O.
        /// </summary>
        public static void Save(string userId, KalendarzUserPreferences prefs)
        {
            if (prefs == null) return;
            try
            {
                string path = GetPrefsFilePath(userId);
                string json = JsonSerializer.Serialize(prefs, JsonOpts);
                File.WriteAllText(path, json);
            }
            catch { /* tolerancyjne - nie chcemy crashować przy zamykaniu okna */ }
        }
    }
}
