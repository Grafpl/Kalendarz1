using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Flota.Services;

namespace Kalendarz1.Flota.Windows
{
    public partial class AssignDriverDialog : Window
    {
        private readonly FlotaService _svc;
        private readonly int? _preselectedDriverGID;
        private readonly string? _preselectedVehicleID;

        public AssignDriverDialog(FlotaService svc, int? driverGID = null, string? vehicleID = null)
        {
            InitializeComponent();
            _svc = svc;
            _preselectedDriverGID = driverGID;
            _preselectedVehicleID = vehicleID;
            WindowIconHelper.SetIcon(this);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DpDataOd.SelectedDate = DateTime.Today;

            try
            {
                var drivers = await _svc.GetActiveDriversComboAsync();
                CmbKierowca.ItemsSource = drivers.DefaultView;

                var vehicles = await _svc.GetActiveVehiclesComboAsync();
                CmbPojazd.ItemsSource = vehicles.DefaultView;

                if (_preselectedDriverGID.HasValue)
                {
                    CmbKierowca.SelectedValue = _preselectedDriverGID.Value;
                    CmbKierowca.IsEnabled = false;
                }

                if (_preselectedVehicleID != null)
                {
                    CmbPojazd.SelectedValue = _preselectedVehicleID;
                    CmbPojazd.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbKierowca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PanelWarning.Visibility = Visibility.Collapsed;

            if (CmbKierowca.SelectedValue == null) return;
            int gid = Convert.ToInt32(CmbKierowca.SelectedValue);

            try
            {
                bool hasActive = await _svc.HasActiveAssignmentAsync(gid);
                if (hasActive)
                {
                    string? info = await _svc.GetActiveAssignmentInfoAsync(gid);
                    TxtWarning.Text = $"Ten kierowca ma juz przypisany pojazd: {info}\nCzy chcesz zamknac poprzednie przypisanie?";
                    PanelWarning.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private async void BtnPrzypisz_Click(object sender, RoutedEventArgs e)
        {
            if (CmbKierowca.SelectedValue == null)
            {
                MessageBox.Show("Wybierz kierowce.", "Walidacja"); return;
            }
            if (CmbPojazd.SelectedValue == null)
            {
                MessageBox.Show("Wybierz pojazd.", "Walidacja"); return;
            }
            if (!DpDataOd.SelectedDate.HasValue)
            {
                MessageBox.Show("Podaj date.", "Walidacja"); return;
            }

            try
            {
                BtnPrzypisz.IsEnabled = false;
                string user = App.UserID ?? "system";
                int gid = Convert.ToInt32(CmbKierowca.SelectedValue);
                string carId = CmbPojazd.SelectedValue.ToString()!;
                string rola = (CmbRola.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Glowny";
                bool closeExisting = ChkCloseExisting.IsChecked == true && PanelWarning.Visibility == Visibility.Visible;

                await _svc.AssignDriverAsync(gid, carId, rola,
                    DpDataOd.SelectedDate.Value,
                    string.IsNullOrWhiteSpace(TxtPowod.Text) ? null : TxtPowod.Text.Trim(),
                    closeExisting, user);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad przypisania:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnPrzypisz.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
