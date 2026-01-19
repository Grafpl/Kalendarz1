using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Services;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Okno zgłaszania wydania odpadów poubojowych do IRZplus
    /// Uboczne produkty pochodzenia zwierzęcego (UPPZ) - KAT1, KAT2, KAT3
    /// </summary>
    public partial class IRZplusOdpadyWindow : Window
    {
        private readonly IRZplusService _service;
        private ObservableCollection<OdpadDoIRZplus> _odpady;
        private string _currentKategoria = "KAT3";

        public IRZplusOdpadyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _service = new IRZplusService();
            _odpady = new ObservableCollection<OdpadDoIRZplus>();
            _odpady.CollectionChanged += (s, e) => UpdateSummary();

            dgOdpady.ItemsSource = _odpady;
            dateWydania.SelectedDate = DateTime.Today;

            UpdateEnvironmentDisplay();
            UpdateRodzajeComboBox();
            UpdateSummary();
        }

        private void UpdateEnvironmentDisplay()
        {
            var settings = _service.GetSettings();
            if (settings.UseTestEnvironment)
            {
                txtEnvironment.Text = "TESTOWE";
                borderEnv.Background = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                txtEnvironment.Text = "PRODUKCJA";
                borderEnv.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 175, 80));
            }
        }

        private void UpdateRodzajeComboBox()
        {
            // Aktualizuj dostępne rodzaje odpadów dla wybranej kategorii
            var rodzaje = KategorieOdpadow.GetRodzajeForKategoria(_currentKategoria);

            // Dla DataGridComboBoxColumn musimy ustawić ItemsSource przez Style
            // To jest uproszczona wersja - w pełnej implementacji można użyć BindingList
        }

        private void UpdateSummary()
        {
            var wybrane = _odpady.Where(o => o.Wybrana).ToList();
            txtLiczbaPozycji.Text = wybrane.Count.ToString();
            txtSumaKg.Text = wybrane.Sum(o => o.IloscKg).ToString("N2");

            var sumaKat3 = wybrane.Where(o => o.KategoriaOdpadu == "KAT3").Sum(o => o.IloscKg);
            txtSumaKat3.Text = $"{sumaKat3:N2} kg";

            btnWyslij.IsEnabled = wybrane.Count > 0;
        }

        private void DateWydania_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Można tu załadować wcześniej zapisane dane dla wybranej daty
            txtStatus.Text = $"Data: {dateWydania.SelectedDate:dd.MM.yyyy}";
        }

        private void CmbKategoria_Changed(object sender, SelectionChangedEventArgs e)
        {
            var selected = cmbKategoria.SelectedItem as ComboBoxItem;
            if (selected != null)
            {
                _currentKategoria = selected.Tag?.ToString() ?? "KAT3";
                UpdateRodzajeComboBox();
            }
        }

        private void BtnDodajPozycje_Click(object sender, RoutedEventArgs e)
        {
            var rodzaje = KategorieOdpadow.GetRodzajeForKategoria(_currentKategoria);
            var domyslnyRodzaj = rodzaje.FirstOrDefault() ?? "Inne";

            var nowyOdpad = new OdpadDoIRZplus
            {
                Id = _odpady.Count + 1,
                DataWydania = dateWydania.SelectedDate ?? DateTime.Today,
                KategoriaOdpadu = _currentKategoria,
                RodzajOdpadu = domyslnyRodzaj,
                IloscKg = 0,
                Wybrana = true
            };

            _odpady.Add(nowyOdpad);
            UpdateSummary();

            // Zaznacz nowy wiersz
            dgOdpady.SelectedItem = nowyOdpad;
            dgOdpady.ScrollIntoView(nowyOdpad);
        }

        private void BtnUsunPozycje_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var odpad = button?.Tag as OdpadDoIRZplus;

            if (odpad != null)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno usunąć pozycję?\n\n{odpad.KategoriaOdpadu} - {odpad.RodzajOdpadu}\nIlość: {odpad.IloscKg:N2} kg",
                    "Potwierdzenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _odpady.Remove(odpad);
                    UpdateSummary();
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new IRZplusSettingsWindow(_service);
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                UpdateEnvironmentDisplay();
            }
        }

        private void BtnHistoria_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Otwórz folder z lokalnymi kopiami
                var exportPath = _service.GetSettings().LocalExportPath;
                if (Directory.Exists(exportPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", exportPath);
                }
                else
                {
                    MessageBox.Show("Brak zapisanych wydań odpadów.", "Informacja",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania historii:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnWczytajSzablon_Click(object sender, RoutedEventArgs e)
        {
            // Przykładowy szablon z domyślnymi odbiorcami
            var result = MessageBox.Show(
                "Wczytać szablon z domyślnymi pozycjami?\n\n" +
                "Zostaną dodane typowe rodzaje odpadów KAT3:\n" +
                "- Pierze\n" +
                "- Krew\n" +
                "- Wnętrzności",
                "Wczytaj szablon",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var dataWydania = dateWydania.SelectedDate ?? DateTime.Today;

                var szablonOdpady = new[]
                {
                    new OdpadDoIRZplus { KategoriaOdpadu = "KAT3", RodzajOdpadu = "Pierze", IloscKg = 0, DataWydania = dataWydania, Wybrana = true },
                    new OdpadDoIRZplus { KategoriaOdpadu = "KAT3", RodzajOdpadu = "Krew", IloscKg = 0, DataWydania = dataWydania, Wybrana = true },
                    new OdpadDoIRZplus { KategoriaOdpadu = "KAT3", RodzajOdpadu = "Wnętrzności", IloscKg = 0, DataWydania = dataWydania, Wybrana = true },
                    new OdpadDoIRZplus { KategoriaOdpadu = "KAT3", RodzajOdpadu = "Tłuszcz", IloscKg = 0, DataWydania = dataWydania, Wybrana = true },
                };

                foreach (var odpad in szablonOdpady)
                {
                    odpad.Id = _odpady.Count + 1;
                    _odpady.Add(odpad);
                }

                UpdateSummary();
                txtStatus.Text = "Wczytano szablon";
            }
        }

        private void BtnZapiszLokalnie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wybrane = _odpady.Where(o => o.Wybrana).ToList();
                if (wybrane.Count == 0)
                {
                    MessageBox.Show("Brak pozycji do zapisania.", "Uwaga",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var exportPath = _service.GetSettings().LocalExportPath;
                if (string.IsNullOrEmpty(exportPath))
                {
                    exportPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "IRZplus_Export");
                }

                if (!Directory.Exists(exportPath))
                    Directory.CreateDirectory(exportPath);

                var dataWyd = dateWydania.SelectedDate ?? DateTime.Today;
                var fileName = $"Odpady_{dataWyd:yyyy-MM-dd}_{DateTime.Now:HHmmss}.json";
                var filePath = Path.Combine(exportPath, fileName);

                var exportData = new
                {
                    DataWydania = dataWyd.ToString("yyyy-MM-dd"),
                    DataZapisu = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    LiczbaPozycji = wybrane.Count,
                    SumaKg = wybrane.Sum(o => o.IloscKg),
                    Pozycje = wybrane.Select(o => new
                    {
                        o.KategoriaOdpadu,
                        o.RodzajOdpadu,
                        o.IloscKg,
                        o.OdbiorcaNazwa,
                        o.OdbiorcaNIP,
                        o.OdbiorcaWetNr,
                        o.NumerDokumentu,
                        o.NumerRejestracyjny,
                        o.Uwagi
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                MessageBox.Show($"Zapisano do pliku:\n{filePath}", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                txtStatus.Text = $"Zapisano: {fileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnWyslij_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = _odpady.Where(o => o.Wybrana).ToList();
            if (wybrane.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jedną pozycję.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Walidacja
            var bezIlosci = wybrane.Where(o => o.IloscKg <= 0).ToList();
            if (bezIlosci.Any())
            {
                MessageBox.Show($"Uzupełnij ilość dla {bezIlosci.Count} pozycji.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Wysłać zgłoszenie wydania odpadów?\n\n" +
                $"Data wydania: {dateWydania.SelectedDate:dd.MM.yyyy}\n" +
                $"Pozycji: {wybrane.Count}\n" +
                $"Suma KG: {wybrane.Sum(o => o.IloscKg):N2}\n\n" +
                $"Środowisko: {(_service.GetSettings().UseTestEnvironment ? "TESTOWE" : "PRODUKCJA")}",
                "Potwierdzenie wysyłki",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                btnWyslij.IsEnabled = false;
                txtStatus.Text = "Wysyłanie...";

                // Najpierw zapisz lokalną kopię
                BtnZapiszLokalnie_Click(null, null);

                // TODO: Implementacja rzeczywistej wysyłki do API IRZplus
                // Na razie symulacja
                await System.Threading.Tasks.Task.Delay(1500);

                MessageBox.Show(
                    $"Zgłoszenie wydania odpadów zostało zapisane.\n\n" +
                    $"Data: {dateWydania.SelectedDate:dd.MM.yyyy}\n" +
                    $"Pozycji: {wybrane.Count}\n" +
                    $"Suma: {wybrane.Sum(o => o.IloscKg):N2} kg\n\n" +
                    "UWAGA: Wysyłka do API IRZplus wymaga dodatkowej konfiguracji.",
                    "Zapisano",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                txtStatus.Text = "Zapisano pomyślnie";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wysyłania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Błąd";
            }
            finally
            {
                btnWyslij.IsEnabled = true;
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            if (_odpady.Any(o => o.IloscKg > 0))
            {
                var result = MessageBox.Show(
                    "Masz niezapisane dane. Czy na pewno zamknąć?",
                    "Potwierdzenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _service?.Dispose();
        }
    }
}
