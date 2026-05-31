using System;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>Ustawienia konfigurowalnej numeracji kontraktów (dbo.KontraktyNumeracjaConfig).</summary>
    public partial class KontraktyNumeracjaWindow : Window
    {
        private readonly KontraktyService _svc = new();
        private NumeracjaConfig _cfg = new();

        public KontraktyNumeracjaWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            _cfg = await _svc.GetNumeracjaConfigAsync() ?? new NumeracjaConfig();
            txtFormat.Text = _cfg.FormatSzablon;
            txtRok.Text = _cfg.Rok.ToString();
            txtNext.Text = _cfg.NastepnyNumer.ToString();
            chkReset.IsChecked = _cfg.ResetRoczny;
            Odswiez();
        }

        private void Pole_Changed(object sender, RoutedEventArgs e) => Odswiez();

        private void Odswiez()
        {
            if (!IsLoaded) return;
            short rok = short.TryParse(txtRok.Text, out var rr) ? rr : (short)DateTime.Now.Year;
            int next = int.TryParse(txtNext.Text, out var nn) && nn > 0 ? nn : 1;
            string fmt = string.IsNullOrWhiteSpace(txtFormat.Text) ? "K/{ROK}/{NNNN}" : txtFormat.Text;
            txtPodglad.Text = KontraktyService.FormatujNumer(fmt, rok, next);
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFormat.Text))
            {
                MessageBox.Show("Podaj format numeru (np. K/{ROK}/{NNNN}).", "Numeracja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!short.TryParse(txtRok.Text, out var rok) || rok < 2000 || rok > 2100)
            {
                MessageBox.Show("Rok musi być z zakresu 2000–2100.", "Numeracja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtNext.Text, out var next) || next < 1)
            {
                MessageBox.Show("Następny numer musi być liczbą ≥ 1.", "Numeracja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _cfg.FormatSzablon = txtFormat.Text.Trim();
            _cfg.Rok = rok;
            _cfg.NastepnyNumer = next;
            _cfg.ResetRoczny = chkReset.IsChecked == true;
            try
            {
                await _svc.SaveNumeracjaConfigAsync(_cfg);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się zapisać: " + ex.Message, "Numeracja",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
