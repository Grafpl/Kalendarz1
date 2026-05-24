using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.ColdChain
{
    /// <summary>
    /// Cold Chain HACCP (#2) — dashboard CCP + wpis manualny + incydenty.
    /// Wymaga ColdChain/SQL/CreateColdChain.sql. Tryb auto (sondy) — TODO po zakupie.
    /// </summary>
    public partial class ColdChainWindow : Window
    {
        private readonly ColdChainService _service = new();

        public ColdChainWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await OdswiezAsync();
        }

        private async Task OdswiezAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var punkty = await _service.GetPunktyZOstatnimPomiaremAsync();
                lstPunkty.ItemsSource = punkty;
                cbPunkt.ItemsSource = punkty;

                bool tylkoOtwarte = chkTylkoOtwarte.IsChecked == true;
                dgIncydenty.ItemsSource = await _service.GetIncydentyAsync(tylkoOtwarte);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania:\n" + ex.Message
                    + "\n\nCzy tabele CCP_* istnieją? Uruchom ColdChain/SQL/CreateColdChain.sql.",
                    "Cold Chain", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private void CCPCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is CCPPunkt p)
            {
                cbPunkt.SelectedItem = p;
                txtWartosc.Focus();
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (cbPunkt.SelectedItem is not CCPPunkt punkt)
            {
                Komunikat("Wybierz punkt CCP.", false);
                return;
            }
            string txt = (txtWartosc.Text ?? "").Replace(',', '.').Trim();
            if (!decimal.TryParse(txt, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal wartosc))
            {
                Komunikat("Podaj poprawną wartość liczbową.", false);
                return;
            }

            try
            {
                btnZapisz.IsEnabled = false;
                string? uwagi = string.IsNullOrWhiteSpace(txtUwagi.Text) ? null : txtUwagi.Text.Trim();
                await _service.ZapiszPomiarAsync(punkt.Id, wartosc, App.UserID, uwagi);

                bool poza = (punkt.LimitDolny.HasValue && wartosc < punkt.LimitDolny.Value)
                         || (punkt.LimitGorny.HasValue && wartosc > punkt.LimitGorny.Value);
                Komunikat(poza
                    ? $"⚠ Zapisano {wartosc:N1} {punkt.Jednostka} — POZA LIMITEM! Utworzono/zaktualizowano incydent."
                    : $"✓ Zapisano {wartosc:N1} {punkt.Jednostka} — w normie.", !poza);

                txtWartosc.Clear(); txtUwagi.Clear();
                await OdswiezAsync();
            }
            catch (Exception ex)
            {
                Komunikat("Błąd zapisu: " + ex.Message, false);
            }
            finally { btnZapisz.IsEnabled = true; }
        }

        private async void BtnZamknijInc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not CCPIncydent inc) return;

            var dlg = new KorektaDialog(inc) { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Korekta))
            {
                try
                {
                    await _service.ZamknijIncydentAsync(inc.Id, dlg.Korekta, App.UserID);
                    await OdswiezAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zamykania incydentu: " + ex.Message, "Cold Chain",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await OdswiezAsync();
        private async void ChkTylkoOtwarte_Click(object sender, RoutedEventArgs e) => await OdswiezAsync();

        private void Komunikat(string tekst, bool ok)
        {
            txtKomunikat.Text = tekst;
            txtKomunikat.Foreground = ok ? BrushFromHex("#059669") : Brushes.DarkRed;
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return new SolidColorBrush(Colors.Gray); }
        }
    }
}
