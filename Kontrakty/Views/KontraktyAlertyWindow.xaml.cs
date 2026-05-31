using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Okno alertów kontraktów (dbo.KontraktyAlerty): generowanie, przegląd, oznaczanie,
    /// szybkie przejście do karty kontraktu.
    /// </summary>
    public partial class KontraktyAlertyWindow : Window
    {
        private readonly KontraktyService _svc = new();

        public KontraktyAlertyWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            var dane = await _svc.GetAlertyAsync(chkTylkoNieprzecz.IsChecked == true);
            dgAlerty.ItemsSource = dane;
            int eskal = 0; foreach (var a in dane) if (a.Eskalowany) eskal++;
            txtLicznik.Text = dane.Count > 0
                ? $"{dane.Count} alertów" + (eskal > 0 ? $" • {eskal} eskalowanych" : "")
                : "";
            txtPusto.Visibility = dane.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // banner „szanse": hodowcy o dużym wolumenie bez umowy (5.2, liczone na żywo)
            var szanse = await _svc.GetSzanseBezKontraktuAsync(minTony: 100, top: 10);
            if (szanse.Count > 0)
            {
                decimal tony = 0; foreach (var s in szanse) tony += s.WagaKg12m / 1000m;
                txtSzanse.Text = $"🟡 Szansa ARiMR: {szanse.Count} hodowców >100 t/rok BEZ umowy " +
                                 $"(łącznie ~{tony:N0} t). Top: {szanse[0].Hodowca} ({szanse[0].WagaTonyLabel}).";
                bannerSzanse.Visibility = Visibility.Visible;
            }
            else bannerSzanse.Visibility = Visibility.Collapsed;
        }

        private void BannerSzanse_Click(object sender, MouseButtonEventArgs e)
            => new KontraktyRankingWindow { Owner = this }.Show();

        private async void BtnGeneruj_Click(object sender, RoutedEventArgs e)
        {
            int nowe = await _svc.GenerujAlertyAsync();
            await ZaladujAsync();
            if (nowe > 0)
                MessageBox.Show($"Wygenerowano {nowe} nowych alertów.", "Alerty",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnOznacz_Click(object sender, RoutedEventArgs e)
        {
            if (dgAlerty.SelectedItem is not KontraktAlertItem a) return;
            await _svc.MarkAlertReadAsync(a.Id, Kalendarz1.App.UserID ?? "");
            await ZaladujAsync();
        }

        private async void BtnOtworz_Click(object sender, RoutedEventArgs e) => await OtworzAsync();
        private async void Dg_DoubleClick(object sender, MouseButtonEventArgs e) => await OtworzAsync();

        private async System.Threading.Tasks.Task OtworzAsync()
        {
            if (dgAlerty.SelectedItem is not KontraktAlertItem a) return;
            await _svc.MarkAlertReadAsync(a.Id, Kalendarz1.App.UserID ?? "");
            new KontraktyKartaWindow(a.KontraktId) { Owner = this }.ShowDialog();
            await ZaladujAsync();
        }

        private async void Filtr_Changed(object sender, RoutedEventArgs e) => await ZaladujAsync();
    }
}
