using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Partie.Models;
using Kalendarz1.Partie.Services;

namespace Kalendarz1.Partie.Windows
{
    public partial class NowaPartiaDialog : Window
    {
        private readonly PartiaService _service;
        private readonly HarmonogramItem _harmonogram;

        public string CreatedPartia { get; private set; }

        public NowaPartiaDialog() : this(null) { }

        public NowaPartiaDialog(HarmonogramItem harmonogram)
        {
            InitializeComponent();
            _service = new PartiaService();
            _harmonogram = harmonogram;

            txtOtwarcie.Text = $"Otwarcie: {DateTime.Now:yyyy-MM-dd HH:mm}  |  Operator: {App.UserID ?? "Admin"}";
            txtInfo.Text = $"Data: {DateTime.Now:yyyy-MM-dd}";

            if (_harmonogram != null)
            {
                borderHarmonogram.Visibility = Visibility.Visible;
                txtHarmonogramInfo.Text = $"Lp={_harmonogram.Lp}: {_harmonogram.Dostawca} ({_harmonogram.DataOdbioru})";
                panelHarmDetails.Visibility = Visibility.Visible;
                txtHarmSztuki.Text = $"{_harmonogram.SztukiDek} szt";
                txtHarmWaga.Text = $"{_harmonogram.WagaDek:N0} kg";
                txtHarmTypCeny.Text = _harmonogram.TypCeny ?? "-";
                txtHarmCena.Text = _harmonogram.Cena.HasValue ? $"{_harmonogram.Cena:N2} PLN/kg" : "-";
            }

            Loaded += async (s, e) =>
            {
                try
                {
                    var dostawcy = await _service.GetDostawcyAsync();
                    cmbDostawca.ItemsSource = dostawcy;

                    // Auto-select dostawca from harmonogram
                    if (_harmonogram != null && !string.IsNullOrEmpty(_harmonogram.Dostawca))
                    {
                        var match = dostawcy.FirstOrDefault(d =>
                            d.Name != null && d.Name.IndexOf(_harmonogram.Dostawca, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (match != null)
                            cmbDostawca.SelectedItem = match;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad ladowania dostawcow:\n{ex.Message}", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private async void BtnUtworzPartie_Click(object sender, RoutedEventArgs e)
        {
            if (cmbDostawca.SelectedItem is not DostawcaComboItem dostawca)
            {
                MessageBox.Show("Wybierz dostawce.", "Wymagane pole",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dzialItem = cmbDzial.SelectedItem as ComboBoxItem;
            string dirId = dzialItem?.Tag?.ToString() ?? "1A";

            BtnUtworzPartie.IsEnabled = false;
            try
            {
                int? harmLp = _harmonogram?.Lp;

                CreatedPartia = await _service.CreatePartiaFromHarmonogramAsync(
                    dirId, dostawca.ID, dostawca.Name, null, App.UserID, harmLp);

                MessageBox.Show($"Utworzono partie: {CreatedPartia}" +
                    (harmLp.HasValue ? $"\n(z harmonogramu Lp={harmLp})" : ""),
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad tworzenia partii:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnUtworzPartie.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
