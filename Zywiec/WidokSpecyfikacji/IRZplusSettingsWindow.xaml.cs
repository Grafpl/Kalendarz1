using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Services;
using Microsoft.Win32;

namespace Kalendarz1
{
    public partial class IRZplusSettingsWindow : Window
    {
        private readonly IRZplusService _service;

        public IRZplusSettingsWindow(IRZplusService service)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _service = service;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _service.GetSettings();

            txtNumerUbojni.Text = settings.NumerUbojni;
            txtNazwaUbojni.Text = settings.NazwaUbojni;
            txtClientId.Text = settings.ClientId;
            txtClientSecret.Password = settings.ClientSecret;
            txtUsername.Text = settings.Username;
            txtPassword.Password = settings.Password;

            rbTestEnv.IsChecked = settings.UseTestEnvironment;
            rbProdEnv.IsChecked = !settings.UseTestEnvironment;

            chkSaveLocalCopy.IsChecked = settings.SaveLocalCopy;
            chkAutoSend.IsChecked = settings.AutoSendOnSave;
            txtExportPath.Text = settings.LocalExportPath;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisywania lokalnych kopii",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(txtExportPath.Text))
            {
                dialog.SelectedPath = txtExportPath.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtExportPath.Text = dialog.SelectedPath;
            }
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            btnTestConnection.IsEnabled = false;
            txtTestResult.Text = "Testowanie...";
            txtTestResult.Foreground = Brushes.Gray;

            try
            {
                // Zapisz tymczasowo ustawienia do testu
                var tempSettings = new IRZplusSettings
                {
                    NumerUbojni = txtNumerUbojni.Text,
                    NazwaUbojni = txtNazwaUbojni.Text,
                    ClientId = txtClientId.Text,
                    ClientSecret = txtClientSecret.Password,
                    Username = txtUsername.Text,
                    Password = txtPassword.Password,
                    UseTestEnvironment = rbTestEnv.IsChecked == true,
                    SaveLocalCopy = chkSaveLocalCopy.IsChecked == true,
                    AutoSendOnSave = chkAutoSend.IsChecked == true,
                    LocalExportPath = txtExportPath.Text
                };

                _service.SaveSettings(tempSettings);

                var result = await _service.TestConnectionAsync();

                if (result.Success)
                {
                    txtTestResult.Text = "Polaczenie OK!";
                    txtTestResult.Foreground = Brushes.Green;
                }
                else
                {
                    txtTestResult.Text = $"Blad: {result.Message}";
                    txtTestResult.Foreground = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                txtTestResult.Text = $"Blad: {ex.Message}";
                txtTestResult.Foreground = Brushes.Red;
            }
            finally
            {
                btnTestConnection.IsEnabled = true;
            }
        }

        /// <summary>
        /// Testuje polaczenie z API IRZplus przez OAuth2
        /// </summary>
        private async void BtnTestApi_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtUsername.Text) || string.IsNullOrEmpty(txtPassword.Password))
            {
                txtApiStatus.Text = "Uzupelnij login i haslo";
                txtApiStatus.Foreground = Brushes.Red;
                return;
            }

            btnTestApi.IsEnabled = false;
            txtApiStatus.Text = "Testowanie API...";
            txtApiStatus.Foreground = Brushes.Gray;

            try
            {
                using (var apiService = new IRZplusApiService())
                {
                    var result = await apiService.AuthenticateAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        txtApiStatus.Text = "Polaczenie API OK! Token uzyskany.";
                        txtApiStatus.Foreground = Brushes.Green;
                    }
                    else
                    {
                        txtApiStatus.Text = $"Blad: {result.Message}";
                        txtApiStatus.Foreground = Brushes.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                txtApiStatus.Text = $"Wyjatek: {ex.Message}";
                txtApiStatus.Foreground = Brushes.Red;
            }
            finally
            {
                btnTestApi.IsEnabled = true;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNumerUbojni.Text))
            {
                MessageBox.Show("Podaj numer weterynaryjny ubojni.", "Walidacja",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNumerUbojni.Focus();
                return;
            }

            if (rbProdEnv.IsChecked == true)
            {
                var result = MessageBox.Show(
                    "Wybrales srodowisko PRODUKCYJNE!\n\n" +
                    "Wszystkie zgloszenia beda wysylane do oficjalnego systemu ARiMR.\n\n" +
                    "Czy na pewno chcesz kontynuowac?",
                    "Ostrzezenie - Srodowisko produkcyjne",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                var settings = new IRZplusSettings
                {
                    NumerUbojni = txtNumerUbojni.Text.Trim(),
                    NazwaUbojni = txtNazwaUbojni.Text.Trim(),
                    ClientId = txtClientId.Text.Trim(),
                    ClientSecret = txtClientSecret.Password,
                    Username = txtUsername.Text.Trim(),
                    Password = txtPassword.Password,
                    UseTestEnvironment = rbTestEnv.IsChecked == true,
                    SaveLocalCopy = chkSaveLocalCopy.IsChecked == true,
                    AutoSendOnSave = chkAutoSend.IsChecked == true,
                    LocalExportPath = txtExportPath.Text.Trim()
                };

                _service.SaveSettings(settings);

                MessageBox.Show("Ustawienia zostaly zapisane.", "Zapisano",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu ustawien:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
