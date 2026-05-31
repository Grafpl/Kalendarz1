using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Kalendarz1.DashboardPrzychodu.Services
{
    /// <summary>
    /// Ustawienia uzytkownika dla Dashboard Przychod Zywca LIVE - persystowane w JSON
    /// w %APPDATA%\Kalendarz1\dashboard_przychodu.json. Wczytywane przy starcie okna,
    /// zapisywane przy zamykaniu i przy kazdej zmianie (logo/ikony/tryb planu).
    /// </summary>
    public class DashboardSettings
    {
        /// <summary>Tryb planu: "Stare" / "Nowe". Default "Nowe" (per-auto plan z SztukiExcel).</summary>
        public string TrybPlanu { get; set; } = "Nowe";

        /// <summary>Logo: emoji lub absolutna sciezka do pliku. Null = default 🏭.</summary>
        public string LogoEmoji { get; set; }
        public string LogoFilePath { get; set; }

        /// <summary>Ikony emoji per kafelek KPI (klucz = tag kafelka: plan/zwazone/...).</summary>
        public Dictionary<string, string> KpiIkony { get; set; } = new();

        /// <summary>Okno dnia roboczego do liczenia pace vs plan (#7). Default 06:00-14:00.</summary>
        public int WorkdayStartHour { get; set; } = 6;
        public int WorkdayStartMin { get; set; } = 0;
        public int WorkdayEndHour { get; set; } = 14;
        public int WorkdayEndMin { get; set; } = 0;

        [JsonIgnore]
        public TimeSpan WorkdayStart => new TimeSpan(WorkdayStartHour, WorkdayStartMin, 0);

        [JsonIgnore]
        public TimeSpan WorkdayEnd => new TimeSpan(WorkdayEndHour, WorkdayEndMin, 0);
    }

    /// <summary>
    /// Service IO ustawien - load/save z %APPDATA%\Kalendarz1\dashboard_przychodu.json.
    /// </summary>
    public static class DashboardSettingsService
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kalendarz1",
            "dashboard_przychodu.json");

        public static DashboardSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Debug.WriteLine($"[DashboardSettings] Brak pliku ustawien {FilePath} - default settings");
                    return new DashboardSettings();
                }
                var json = File.ReadAllText(FilePath);
                var settings = JsonConvert.DeserializeObject<DashboardSettings>(json) ?? new DashboardSettings();
                Debug.WriteLine($"[DashboardSettings] Wczytano ustawienia z {FilePath}");
                return settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardSettings] Blad wczytania: {ex.Message} - default");
                return new DashboardSettings();
            }
        }

        public static void Save(DashboardSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(FilePath, json);
                Debug.WriteLine($"[DashboardSettings] Zapisano ustawienia do {FilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardSettings] Blad zapisu: {ex.Message}");
            }
        }
    }
}
