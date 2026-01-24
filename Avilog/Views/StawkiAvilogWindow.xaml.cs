using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Avilog.Helpers;
using Kalendarz1.Avilog.Models;
using Kalendarz1.Avilog.Services;

namespace Kalendarz1.Avilog.Views
{
    public partial class StawkiAvilogWindow : Window
    {
        private readonly AvilogDataService _dataService;
        private ObservableCollection<AvilogSettingsModel> _stawkiCollection;

        public decimal? WybranaStawka { get; private set; }

        public StawkiAvilogWindow()
        {
            InitializeComponent();

            try { WindowIconHelper.SetIcon(this); } catch { }

            _dataService = new AvilogDataService();
            _stawkiCollection = new ObservableCollection<AvilogSettingsModel>();
            dataGridStawki.ItemsSource = _stawkiCollection;

            // Domyślna data od = dzisiaj
            dateOd.SelectedDate = DateTime.Today;

            // Event selection changed
            dataGridStawki.SelectionChanged += DataGridStawki_SelectionChanged;

            // Załaduj dane
            Loaded += async (s, e) => await LoadStawkiAsync();
        }

        private async System.Threading.Tasks.Task LoadStawkiAsync()
        {
            try
            {
                var stawki = await _dataService.GetHistoriaStawekAsync();
                _stawkiCollection.Clear();
                foreach (var s in stawki)
                {
                    _stawkiCollection.Add(s);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania stawek:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGridStawki_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnUsun.IsEnabled = dataGridStawki.SelectedItem != null;
        }

        private async void BtnDodajStawke_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja stawki
            if (!decimal.TryParse(txtNowaStawka.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal nowaStawka))
            {
                MessageBox.Show("Nieprawidłowa wartość stawki.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (nowaStawka <= 0 || nowaStawka > 10)
            {
                MessageBox.Show("Stawka musi być większa niż 0 i mniejsza niż 10 zł/kg.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Walidacja daty
            if (!dateOd.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę początkową.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dataOdValue = dateOd.SelectedDate.Value;
            var dataDoValue = dateDo.SelectedDate;

            if (dataDoValue.HasValue && dataDoValue.Value < dataOdValue)
            {
                MessageBox.Show("Data końcowa nie może być wcześniejsza niż data początkowa.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await _dataService.SaveStawkaWithDatesAsync(nowaStawka, dataOdValue, dataDoValue, Environment.UserName, txtUwagi.Text);
                MessageBox.Show("Stawka została zapisana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Wyczyść formularz
                txtNowaStawka.Text = "0.119";
                dateOd.SelectedDate = DateTime.Today;
                dateDo.SelectedDate = null;
                txtUwagi.Text = "";

                // Odśwież listę
                await LoadStawkiAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu stawki:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUsunStawke_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridStawki.SelectedItem is AvilogSettingsModel stawka)
            {
                if (stawka.JestAktywna)
                {
                    MessageBox.Show("Nie można usunąć aktywnej stawki.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Czy na pewno chcesz usunąć stawkę {stawka.StawkaZaKg:N4} zł/kg z okresu {stawka.DataOd:dd.MM.yyyy} - {(stawka.DataDo.HasValue ? stawka.DataDo.Value.ToString("dd.MM.yyyy") : "teraz")}?",
                    "Potwierdzenie usunięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _dataService.DeleteStawkaAsync(stawka.ID);
                        MessageBox.Show("Stawka została usunięta.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadStawkiAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd usuwania stawki:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadStawkiAsync();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            // Ustaw wybraną stawkę (aktywną)
            var aktywna = _stawkiCollection.FirstOrDefault(s => s.JestAktywna);
            if (aktywna != null)
            {
                WybranaStawka = aktywna.StawkaZaKg;
            }

            DialogResult = true;
            Close();
        }
    }
}
