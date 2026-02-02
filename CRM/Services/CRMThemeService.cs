using System;
using System.IO;
using System.Text.Json;

namespace Kalendarz1.CRM.Services
{
    public enum CRMThemeMode
    {
        Dark,
        Light
    }

    public class CRMThemeSettings
    {
        public string ThemeMode { get; set; } = "Dark";
    }

    public static class CRMThemeService
    {
        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kalendarz1",
            "CRMThemeSettings.json"
        );

        private static CRMThemeMode _currentTheme = CRMThemeMode.Dark;

        public static CRMThemeMode CurrentTheme => _currentTheme;

        public static void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<CRMThemeSettings>(json);
                    if (settings?.ThemeMode == "Light")
                        _currentTheme = CRMThemeMode.Light;
                    else
                        _currentTheme = CRMThemeMode.Dark;
                }
            }
            catch
            {
                _currentTheme = CRMThemeMode.Dark;
            }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var settings = new CRMThemeSettings
                {
                    ThemeMode = _currentTheme == CRMThemeMode.Light ? "Light" : "Dark"
                };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        public static void SetTheme(CRMThemeMode theme)
        {
            _currentTheme = theme;
            Save();
        }

        public static void Toggle()
        {
            _currentTheme = _currentTheme == CRMThemeMode.Dark ? CRMThemeMode.Light : CRMThemeMode.Dark;
            Save();
        }
    }
}
