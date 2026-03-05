using System;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Flota.Services;

namespace Kalendarz1.Flota.Windows
{
    public partial class ServiceLogDialog : Window
    {
        private readonly FlotaService _svc;
        private readonly string _carTrailerID;
        private readonly string? _preselectedType;

        public ServiceLogDialog(FlotaService svc, string carTrailerID, string displayName, string? preselectedType)
        {
            InitializeComponent();
            _svc = svc;
            _carTrailerID = carTrailerID;
            _preselectedType = preselectedType;
            TxtPojazd.Text = displayName;
            WindowIconHelper.SetIcon(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DpData.SelectedDate = DateTime.Today;

            if (_preselectedType != null)
            {
                foreach (ComboBoxItem item in CmbTyp.Items)
                {
                    if (item.Content?.ToString() == _preselectedType)
                    {
                        CmbTyp.SelectedItem = item;
                        break;
                    }
                }
            }

            UpdateVisibility();
        }

        private void CmbTyp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            string typ = (CmbTyp.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            bool showDateNext = typ == "Przeglad" || typ == "OC" || typ == "AC";
            if (PanelDataNastepne != null) PanelDataNastepne.Visibility = showDateNext ? Visibility.Visible : Visibility.Collapsed;

            bool showTankowanie = typ == "Tankowanie";
            if (PanelTankowanie != null) PanelTankowanie.Visibility = showTankowanie ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TankowanieCalc_Changed(object sender, TextChangedEventArgs e)
        {
            if (TxtLitry == null || TxtCenaLitra == null || TxtTankowanieRazem == null) return;

            if (decimal.TryParse(TxtLitry.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal litry) &&
                decimal.TryParse(TxtCenaLitra.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal cena))
            {
                decimal razem = litry * cena;
                TxtTankowanieRazem.Text = $"{razem:N2} PLN";
                TxtKoszt.Text = razem.ToString("0.00");
            }
            else
            {
                TxtTankowanieRazem.Text = "";
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            string typ = (CmbTyp.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(typ))
            {
                MessageBox.Show("Wybierz typ zdarzenia.", "Walidacja"); return;
            }
            if (!DpData.SelectedDate.HasValue)
            {
                MessageBox.Show("Podaj date.", "Walidacja"); return;
            }

            try
            {
                BtnZapisz.IsEnabled = false;
                string user = App.UserID ?? "system";

                decimal? litry = ParseDecimal(TxtLitry?.Text);
                decimal? cena = ParseDecimal(TxtCenaLitra?.Text);
                decimal? koszt = ParseDecimal(TxtKoszt.Text);
                int? przebieg = ParseInt(TxtPrzebieg.Text);

                await _svc.AddServiceLogAsync(
                    _carTrailerID, typ,
                    DpData.SelectedDate.Value,
                    DpDataNastepne?.SelectedDate,
                    string.IsNullOrWhiteSpace(TxtOpis.Text) ? null : TxtOpis.Text.Trim(),
                    koszt, przebieg, litry, cena,
                    string.IsNullOrWhiteSpace(TxtWarsztat.Text) ? null : TxtWarsztat.Text.Trim(),
                    string.IsNullOrWhiteSpace(TxtNrFaktury.Text) ? null : TxtNrFaktury.Text.Trim(),
                    string.IsNullOrWhiteSpace(TxtUwagiSerwis.Text) ? null : TxtUwagiSerwis.Text.Trim(),
                    user);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapisz.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static int? ParseInt(string? text)
        {
            return int.TryParse(text?.Trim(), out int v) ? v : null;
        }

        private static decimal? ParseDecimal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim().Replace(',', '.');
            return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : null;
        }
    }
}
