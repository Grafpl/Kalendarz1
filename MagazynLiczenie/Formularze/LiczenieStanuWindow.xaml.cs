using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.MagazynLiczenie.Modele;
using Kalendarz1.MagazynLiczenie.Repozytorium;

namespace Kalendarz1.MagazynLiczenie.Formularze
{
    public partial class LiczenieStanuWindow : Window
    {
        private readonly LiczenieRepozytorium _repozytorium;
        private readonly string _uzytkownik;
        private DateTime _dataLiczenia;  // ✅ USUŃ readonly
        private List<ProduktLiczenie> _produkty;
        private ProduktLiczenie _wybranyProdukt;

        public LiczenieStanuWindow(string connLibra, string connHandel, string uzytkownik)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _repozytorium = new LiczenieRepozytorium(connLibra, connHandel);
            _uzytkownik = uzytkownik;
            _dataLiczenia = DateTime.Today;

            txtDataLiczenia.Text = $"Data liczenia: {_dataLiczenia:dddd, dd MMMM yyyy}";

            Loaded += Window_Loaded;
            keypad.ValueEntered += Keypad_ValueEntered;
            datePicker.DateSelected += DatePicker_DateSelected;
            datePicker.SetDate(_dataLiczenia);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                this.Cursor = System.Windows.Input.Cursors.Wait;

                _produkty = await _repozytorium.PobierzProduktyDoLiczeniaAsync();
                var istniejaceStany = await _repozytorium.PobierzAktualneStalyAsync(_dataLiczenia);

                foreach (var produkt in _produkty)
                {
                    if (istniejaceStany.TryGetValue(produkt.ProduktId, out decimal stan))
                    {
                        produkt.StanMagazynowy = stan;
                        produkt.JestZmodyfikowany = true;
                    }
                }

                itemsProducts.ItemsSource = _produkty;
                UpdateCounter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Błąd podczas wczytywania danych:\n\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private async void DatePicker_DateSelected(object sender, DateTime selectedDate)
        {
            if (selectedDate != _dataLiczenia)
            {
                var result = MessageBox.Show(
                    $"📅 Zmienić datę liczenia?\n\n" +
                    $"Aktualna: {_dataLiczenia:yyyy-MM-dd}\n" +
                    $"Nowa: {selectedDate:yyyy-MM-dd}\n\n" +
                    $"⚠️ Niezapisane dane zostaną utracone!",
                    "Zmiana daty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _dataLiczenia = selectedDate;  // ✅ Teraz działa
                    txtDataLiczenia.Text = $"Data liczenia: {_dataLiczenia:dddd, dd MMMM yyyy}";
                    await LoadDataAsync();
                }
                else
                {
                    datePicker.SetDate(_dataLiczenia);
                }
            }
        }

        private void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ProduktLiczenie produkt)
            {
                _wybranyProdukt = produkt;
                txtSelectedProduct.Text = produkt.KodProduktu;
                keypad.SetInitialValue(produkt.StanMagazynowy);

                foreach (var item in itemsProducts.Items)
                {
                    var container = itemsProducts.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        var btn = FindVisualChild<Button>(container);
                        if (btn != null)
                        {
                            if (item == produkt)
                            {
                                btn.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(52, 152, 219));
                            }
                            else if ((item as ProduktLiczenie)?.JestZmodyfikowany == true)
                            {
                                btn.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(39, 174, 96));
                            }
                            else
                            {
                                btn.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(189, 195, 199));
                            }
                        }
                    }
                }
            }
        }

        private void Keypad_ValueEntered(object sender, decimal value)
        {
            if (_wybranyProdukt != null)
            {
                _wybranyProdukt.StanMagazynowy = value;
                _wybranyProdukt.JestZmodyfikowany = true;

                itemsProducts.Items.Refresh();
                UpdateCounter();

                keypad.Reset();

                txtSelectedProduct.Text = $"✓ Zapisano: {_wybranyProdukt.KodProduktu} = {value:N0} kg";
                _wybranyProdukt = null;

                System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtSelectedProduct.Text = "Wybierz kolejny produkt";
                    });
                });
            }
            else
            {
                MessageBox.Show(
                    "⚠️ Najpierw wybierz produkt z listy!",
                    "Uwaga",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void UpdateCounter()
        {
            int policzonych = _produkty.Count(p => p.JestZmodyfikowany);
            int wszystkich = _produkty.Count;
            txtLicznik.Text = $"Policzono: {policzonych} / {wszystkich}";
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var zmodyfikowane = _produkty.Where(p => p.JestZmodyfikowany).ToList();

            if (!zmodyfikowane.Any())
            {
                MessageBox.Show(
                    "⚠️ Nie wprowadzono żadnych danych do zapisu!",
                    "Uwaga",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"💾 Czy na pewno chcesz zapisać stany magazynowe?\n\n" +
                $"📦 Liczba produktów: {zmodyfikowane.Count}\n" +
                $"📅 Data: {_dataLiczenia:yyyy-MM-dd}\n" +
                $"👤 Użytkownik: {_uzytkownik}\n\n" +
                $"Istniejące dane dla tej daty zostaną nadpisane.",
                "Potwierdzenie zapisu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    this.Cursor = System.Windows.Input.Cursors.Wait;

                    await _repozytorium.ZapiszStanyAsync(_produkty, _dataLiczenia, _uzytkownik);

                    MessageBox.Show(
                        $"✅ Stany magazynowe zostały zapisane!\n\n" +
                        $"Zapisano: {zmodyfikowane.Count} produktów\n" +
                        $"Data: {_dataLiczenia:yyyy-MM-dd}",
                        "Sukces",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"❌ Błąd podczas zapisywania danych:\n\n{ex.Message}",
                        "Błąd",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var zmodyfikowane = _produkty.Count(p => p.JestZmodyfikowany);

            if (zmodyfikowane > 0)
            {
                var result = MessageBox.Show(
                    $"⚠️ Masz niezapisane zmiany ({zmodyfikowane} produktów)!\n\n" +
                    $"Czy na pewno chcesz wyjść bez zapisywania?",
                    "Niezapisane zmiany",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.DialogResult = false;
            this.Close();
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}