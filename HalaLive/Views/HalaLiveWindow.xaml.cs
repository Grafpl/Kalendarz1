using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Kalendarz1.HalaLive.Models;
using Kalendarz1.HalaLive.Services;

namespace Kalendarz1.HalaLive.Views
{
    /// <summary>
    /// Fullscreen dashboard dla hali produkcyjnej. TV 55"+ z auto-refresh co 30s.
    /// Wszystkie dane cross-DB (UNISYSTEM RCP + LibraNet + HANDEL) ładowane równolegle.
    /// </summary>
    public partial class HalaLiveWindow : Window
    {
        private readonly HalaLiveService _service = new();
        private DispatcherTimer? _clockTimer;
        private DispatcherTimer? _refreshTimer;

        private static readonly CultureInfo PL = CultureInfo.GetCultureInfo("pl-PL");

        public HalaLiveWindow()
        {
            InitializeComponent();
            Loaded += HalaLiveWindow_Loaded;
            Closed += HalaLiveWindow_Closed;
        }

        private async void HalaLiveWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Zegar co sekundę
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, _) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            // Refresh danych co 30 sekund
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (s, _) => await RefreshAsync();
            _refreshTimer.Start();

            // Pierwsze ładowanie
            await RefreshAsync();
        }

        private void HalaLiveWindow_Closed(object? sender, EventArgs e)
        {
            _clockTimer?.Stop();
            _refreshTimer?.Stop();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            ClockText.Text = now.ToString("HH:mm:ss", PL);
            WeekdayText.Text = PL.DateTimeFormat.GetDayName(now.DayOfWeek).ToUpper(PL);
            DateSubtitle.Text = now.ToString("dddd, d MMMM yyyy", PL);
        }

        private async Task RefreshAsync()
        {
            try
            {
                StatusText.Text = "Aktualizowanie...";
                var data = await _service.LoadAllAsync();
                ApplyData(data);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
            }
        }

        private void ApplyData(HalaLiveData d)
        {
            // === PLAN PROGRESS ===
            var planProc = d.ZywiecPlanKg > 0 ? (double)(d.ZywiecKgDzis / d.ZywiecPlanKg) : 0;
            planProc = Math.Min(planProc, 1.5); // cap 150%
            PlanPercentText.Text = $"{planProc * 100:0}%";
            PlanKgText.Text = $"{d.ZywiecKgDzis / 1000m:F1} / {d.ZywiecPlanKg / 1000m:F0} t";

            // Progress bar width
            var availableWidth = Math.Max(0, ActualWidth - 64 - 200); // margin + tekst
            PlanProgressBar.Width = Math.Min(availableWidth, availableWidth * Math.Min(planProc, 1.0));

            // Kolor progresu wg statusu
            if (planProc >= 0.95)
                PlanProgressBar.Background = (System.Windows.Media.Brush)FindResource("AccentGreen");
            else if (planProc >= 0.70)
                PlanProgressBar.Background = (System.Windows.Media.Brush)FindResource("AccentAmber");
            else
                PlanProgressBar.Background = (System.Windows.Media.Brush)FindResource("AccentRed");

            // === KAFELKI ===
            PracownicyText.Text = d.PracownicyObecni.ToString();

            ZywiecText.Text = $"{d.ZywiecKgDzis / 1000m:F1}";
            ZywiecSubText.Text = $"ton  •  {d.ZywiecLiczbaSztuk:N0} wpisów";

            WydaniaText.Text = $"{d.WydaniaKgDzis / 1000m:F1}";
            WydaniaSubText.Text = $"ton  •  {d.WydaniaLiczbaDokumentow} dokumentów";

            DostawyText.Text = d.HodowcyTop.Count.ToString();

            // Klasy
            KlasaDuzyText.Text = $"{d.KlasyDuzyProc:F0}%";
            KlasaMalyText.Text = $"{d.KlasyMalyProc:F0}%";

            var klasyAvailable = 220.0; // ~tile width
            KlasaDuzyBar.Width = klasyAvailable * (double)(d.KlasyDuzyProc / 100m);
            KlasaMalyBar.Width = klasyAvailable * (double)(d.KlasyMalyProc / 100m);

            PaletyText.Text = d.PaletyDzis.ToString();
            DokumentyText.Text = d.WydaniaLiczbaDokumentow.ToString();
            WazeniaText.Text = d.WazeniaDzis.ToString();

            // === HODOWCY ===
            if (d.HodowcyTop.Count == 0)
            {
                HodowcyText.Text = "Brak dostaw dziś.";
            }
            else
            {
                var medals = new[] { "🥇", "🥈", "🥉", "🏅", "🏅" };
                var parts = new System.Text.StringBuilder();
                for (int i = 0; i < d.HodowcyTop.Count; i++)
                {
                    var h = d.HodowcyTop[i];
                    if (i > 0) parts.Append("    ");
                    parts.Append($"{medals[i]} {h.DisplayName}  ");
                    parts.Append($"({h.SumaKg / 1000m:F1} t / {h.LiczbaPartii} part.)");
                }
                HodowcyText.Text = parts.ToString();
            }

            // === FOOTER ===
            LastUpdateText.Text = $"Ostatnia aktualizacja: {d.SnapshotAt:HH:mm:ss}  •  Następna za 30 s";

            // Status — jeśli jakiekolwiek błędy
            var errors = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(d.ErrorUnisystem)) errors.Add("UNICARD");
            if (!string.IsNullOrEmpty(d.ErrorLibraNet)) errors.Add("LibraNet");
            if (!string.IsNullOrEmpty(d.ErrorHandel)) errors.Add("HANDEL");

            if (errors.Count == 0)
                StatusText.Text = "Wszystkie dane OK";
            else
                StatusText.Text = $"⚠ Problem z: {string.Join(", ", errors)}";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.F11)
            {
                WindowStyle = WindowStyle == WindowStyle.None
                    ? WindowStyle.SingleBorderWindow
                    : WindowStyle.None;
            }
            else if (e.Key == Key.F5)
            {
                _ = RefreshAsync();
            }
        }
    }
}
