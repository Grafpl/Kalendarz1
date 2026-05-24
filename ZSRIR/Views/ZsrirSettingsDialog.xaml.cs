using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.ZSRIR.Models;
using Kalendarz1.ZSRIR.Services;

namespace Kalendarz1.ZSRIR.Views
{
    public partial class ZsrirSettingsDialog : Window
    {
        public ZsrirSettingsDialog()
        {
            InitializeComponent();
            var s = ZsrirSecretsManager.Load();
            txtUsername.Text = s.Username;
            txtPassword.Password = s.Password;
            txtApiUrl.Text = s.ApiBaseUrl;
            lblSecretsPath.Text = "📁 " + ZsrirSecretsManager.GetSecretsPath();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private async void BtnFetchMeta_Click(object sender, RoutedEventArgs e)
        {
            var temp = new ZsrirSecrets
            {
                ApiBaseUrl = string.IsNullOrWhiteSpace(txtApiUrl.Text) ? "https://zsrir.minrol.gov.pl/api" : txtApiUrl.Text.Trim(),
                Username = txtUsername.Text.Trim(),
                Password = txtPassword.Password
            };
            if (!temp.IsConfigured)
            {
                MessageBox.Show("Wypełnij login i hasło przed pobraniem metadanych.",
                    "Brak loginu", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btnFetchMeta.IsEnabled = false;
            lblMetaStatus.Text = "⏳ Łączę z API...";
            try
            {
                using var api = new ZsrirApiClient(temp);
                var suppliers = await api.GetDataSuppliersAsync();
                cmbDataSupplier.ItemsSource = suppliers;
                if (suppliers.Count > 0) cmbDataSupplier.SelectedIndex = 0;

                lblMetaStatus.Text = $"✓ {suppliers.Count} dostawców. Wybierz i kliknij ponownie żeby zaczytać formularze, albo zaczekaj…";

                // Auto-zaczytaj formularze dla pierwszego dostawcy
                if (cmbDataSupplier.SelectedItem is DataSupplier ds)
                {
                    var forms = await api.GetFormsAsync(ds.Id);
                    cmbForm.ItemsSource = forms;
                    // Spróbuj wybrać "Drób rzeźny" automatycznie
                    var drob = forms.FirstOrDefault(f =>
                        (f.Name ?? "").IndexOf("drób", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (f.Name ?? "").IndexOf("drob", StringComparison.OrdinalIgnoreCase) >= 0);
                    cmbForm.SelectedItem = drob ?? forms.FirstOrDefault();
                    lblMetaStatus.Text = $"✓ {suppliers.Count} dostawców, {forms.Count} formularzy.";
                }
            }
            catch (ZsrirApiException ex)
            {
                lblMetaStatus.Text = "✗ " + ex.Message;
                MessageBox.Show("Nie udało się pobrać metadanych:\n" + ex.Message, "Błąd API",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                lblMetaStatus.Text = "✗ " + ex.Message;
                MessageBox.Show("Błąd: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { btnFetchMeta.IsEnabled = true; }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = new ZsrirSecrets
            {
                ApiBaseUrl = string.IsNullOrWhiteSpace(txtApiUrl.Text) ? "https://zsrir.minrol.gov.pl/api" : txtApiUrl.Text.Trim(),
                Username = txtUsername.Text.Trim(),
                Password = txtPassword.Password,
                DataSupplierId = (cmbDataSupplier.SelectedItem as DataSupplier)?.Id,
                FormId = (cmbForm.SelectedItem as FormInfo)?.Id
            };
            try
            {
                ZsrirSecretsManager.Save(s);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Zapis nieudany: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
