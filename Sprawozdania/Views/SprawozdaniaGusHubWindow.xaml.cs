using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class SprawozdaniaGusHubWindow : Window
    {
        private readonly GusSubmissionsRepo _repo = new();

        public SprawozdaniaGusHubWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await OdswiezStatusyAsync();
        }

        // Pokazuje na P-02 kafelku status ostatniego wygenerowanego sprawozdania
        private async Task OdswiezStatusyAsync()
        {
            try
            {
                var lista = await _repo.GetRecentAsync("P-02", 1);
                if (lista.Count == 0)
                {
                    p02StatusLabel.Text = "Brak historii";
                    p02StatusHint.Text = "Nigdy nie wygenerowano P-02";
                    return;
                }
                var ost = lista[0];
                string okres = ost.Miesiac.HasValue
                    ? $"{NazwaMiesiaca(ost.Miesiac.Value)} {ost.Rok}"
                    : ost.Rok.ToString();
                string statusText = ost.Status switch
                {
                    "Sent" => $"✓ Wysłane · {okres}",
                    "Generated" => $"📄 Wygenerowane · {okres}",
                    "Exported" => $"📤 Wyeksportowane · {okres}",
                    "Failed" => $"✗ Błąd · {okres}",
                    _ => $"{ost.Status} · {okres}"
                };
                p02StatusLabel.Text = statusText;
                p02StatusHint.Text = $"{ost.GeneratedAt:dd.MM.yyyy HH:mm}" +
                    (ost.GeneratedByImie != null ? $" · {ost.GeneratedByImie}" : "");
            }
            catch
            {
                p02StatusLabel.Text = "Status niedostępny";
                p02StatusHint.Text = "";
            }
        }

        private static string NazwaMiesiaca(int m)
        {
            string[] n = { "", "styczeń","luty","marzec","kwiecień","maj","czerwiec",
                "lipiec","sierpień","wrzesień","październik","listopad","grudzień" };
            return (m >= 1 && m <= 12) ? n[m] : $"mc{m}";
        }

        private async void P02_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var win = new P02Window { Owner = this };
                win.Show();
                win.Closed += async (_, __) => await OdswiezStatusyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwarcia okna P-02:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void R09U_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var win = new R09UWindow { Owner = this };
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwarcia okna R-09U:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new GusSettingsDialog { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwarcia konfiguracji:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new HistoriaSprawozdanGusWindow { Owner = this };
                dlg.ShowDialog();
                await OdswiezStatusyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwarcia historii:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
