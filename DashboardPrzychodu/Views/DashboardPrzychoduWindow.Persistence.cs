using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using Kalendarz1.DashboardPrzychodu.Models;
using Kalendarz1.DashboardPrzychodu.Services;

namespace Kalendarz1.DashboardPrzychodu.Views
{
    /// <summary>
    /// #9 - Persystencja ustawien uzytkownika do %APPDATA%\Kalendarz1\dashboard_przychodu.json.
    /// Wczytane przy starcie (z LoadSettings w InitializeAsync), zapisywane przy zmianach
    /// (tryb planu / logo / ikony KPI) i przy zamykaniu okna.
    /// </summary>
    public partial class DashboardPrzychoduWindow
    {
        private void LoadSettings()
        {
            try
            {
                _settings = DashboardSettingsService.Load();

                // Workday window -> PodsumowanieDnia static props (uzywane przez pace #7)
                PodsumowanieDnia.WorkdayStart = _settings.WorkdayStart;
                PodsumowanieDnia.WorkdayEnd = _settings.WorkdayEnd;

                // Tryb planu - zawsze "Nowy" (Stary usuniety z UI)
                _useNowyPlan = true;

                // Logo
                if (!string.IsNullOrEmpty(_settings.LogoFilePath) && File.Exists(_settings.LogoFilePath))
                {
                    SetLogoFromFile(_settings.LogoFilePath);
                }
                else if (!string.IsNullOrEmpty(_settings.LogoEmoji))
                {
                    if (!(LogoBox.Child is TextBlock))
                        LogoBox.Child = LogoIcon;
                    LogoIcon.Text = _settings.LogoEmoji;
                }

                // Ikony KPI
                foreach (var (key, emoji) in _settings.KpiIkony)
                {
                    ApplyKpiIconEmoji(key, emoji);
                }

                _settingsLoaded = true;
                Debug.WriteLine($"[DashboardPrzychodu] Ustawienia wczytane: tryb={_settings.TrybPlanu}, workday={_settings.WorkdayStart:hh\\:mm}-{_settings.WorkdayEnd:hh\\:mm}, ikon={_settings.KpiIkony.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] LoadSettings blad: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            if (!_settingsLoaded) return; // nie zapisuj jesli nawet nie wczytalismy (np. crash early)
            try
            {
                _settings.TrybPlanu = _useNowyPlan ? "Nowe" : "Stare";
                DashboardSettingsService.Save(_settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardPrzychodu] SaveSettings blad: {ex.Message}");
            }
        }

        /// <summary>
        /// Ustawia emoji w danym kafelku KPI (pomoc dla LoadSettings i Icon_SetEmoji).
        /// </summary>
        private void ApplyKpiIconEmoji(string target, string emoji)
        {
            // KPI Strip został usunięty - no-op (zachowane settings persystowane ale nie używane wizualnie)
        }
    }
}
