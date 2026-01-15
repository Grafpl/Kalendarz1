using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class FirefliesKonfiguracjaWindow : Window
    {
        private readonly FirefliesService _firefliesService;
        private FirefliesKonfiguracja? _konfiguracja;

        public FirefliesKonfiguracjaWindow()
        {
            InitializeComponent();
            _firefliesService = new FirefliesService();

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _konfiguracja = await _firefliesService.PobierzKonfiguracje();

                if (_konfiguracja != null)
                {
                    // Wypełnij formularz
                    TxtApiKey.Password = _konfiguracja.ApiKey ?? "";
                    ChkAutoSync.IsChecked = _konfiguracja.AutoSynchronizacja;
                    ChkAutoImport.IsChecked = _konfiguracja.AutoImportNotatek;
                    ChkAktywna.IsChecked = _konfiguracja.Aktywna;

                    // Interwał
                    CmbInterwal.SelectedIndex = _konfiguracja.InterwalSynchronizacjiMin switch
                    {
                        5 => 0,
                        15 => 1,
                        30 => 2,
                        60 => 3,
                        _ => 1
                    };

                    // Minimalny czas
                    CmbMinCzas.SelectedIndex = _konfiguracja.MinimalnyCzasSpotkaniaSek switch
                    {
                        30 => 0,
                        60 => 1,
                        300 => 2,
                        600 => 3,
                        _ => 1
                    };

                    if (_konfiguracja.ImportujOdDaty.HasValue)
                        DpImportOd.SelectedDate = _konfiguracja.ImportujOdDaty;

                    // Status
                    AktualizujStatus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania konfiguracji: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AktualizujStatus()
        {
            if (_konfiguracja == null || !_konfiguracja.MaKlucz)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Szary
                TxtStatus.Text = "Brak konfiguracji";
                TxtOstatniaSynch.Text = "Wprowadź klucz API aby rozpocząć";
            }
            else if (!_konfiguracja.Aktywna)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Szary
                TxtStatus.Text = "Wyłączone";
                TxtOstatniaSynch.Text = "Integracja jest nieaktywna";
            }
            else if (_konfiguracja.MaBlad)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Czerwony
                TxtStatus.Text = "Błąd synchronizacji";
                TxtOstatniaSynch.Text = _konfiguracja.OstatniBladSynchronizacji;
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Zielony
                TxtStatus.Text = "Aktywne";
                TxtOstatniaSynch.Text = $"Ostatnia synchronizacja: {_konfiguracja.OstatniaSynchronizacjaDisplay}";
            }
        }

        private async void BtnTestuj_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtApiKey.Password))
            {
                TxtTestWynik.Text = "Wprowadź klucz API";
                TxtTestWynik.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                return;
            }

            try
            {
                BtnTestuj.IsEnabled = false;
                TxtTestWynik.Text = "Testowanie połączenia...";
                TxtTestWynik.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));

                var (success, message, user) = await _firefliesService.TestujPolaczenie(TxtApiKey.Password);

                if (success)
                {
                    TxtTestWynik.Text = $"✓ {message}";
                    TxtTestWynik.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                    // Pokaż info o koncie
                    if (user != null)
                    {
                        PanelKonto.Visibility = Visibility.Visible;
                        TxtKontoInfo.Text = $"Email: {user.Email}\n" +
                                           $"Nazwa: {user.Name}\n" +
                                           $"Wykorzystane minuty: {user.MinutesConsumedDisplay}";
                    }
                }
                else
                {
                    TxtTestWynik.Text = $"✗ {message}";
                    TxtTestWynik.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    PanelKonto.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                TxtTestWynik.Text = $"✗ Błąd: {ex.Message}";
                TxtTestWynik.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
            finally
            {
                BtnTestuj.IsEnabled = true;
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = _konfiguracja ?? new FirefliesKonfiguracja();

                config.ApiKey = TxtApiKey.Password;
                config.AutoSynchronizacja = ChkAutoSync.IsChecked ?? false;
                config.AutoImportNotatek = ChkAutoImport.IsChecked ?? false;
                config.Aktywna = ChkAktywna.IsChecked ?? true;

                config.InterwalSynchronizacjiMin = CmbInterwal.SelectedIndex switch
                {
                    0 => 5,
                    1 => 15,
                    2 => 30,
                    3 => 60,
                    _ => 15
                };

                config.MinimalnyCzasSpotkaniaSek = CmbMinCzas.SelectedIndex switch
                {
                    0 => 30,
                    1 => 60,
                    2 => 300,
                    3 => 600,
                    _ => 60
                };

                config.ImportujOdDaty = DpImportOd.SelectedDate;

                bool success = await _firefliesService.ZapiszKonfiguracje(config);

                if (success)
                {
                    MessageBox.Show("Konfiguracja została zapisana.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Nie udało się zapisać konfiguracji.", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnDiagnostyka_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtApiKey.Password))
            {
                MessageBox.Show("Wprowadź klucz API przed uruchomieniem diagnostyki.",
                    "Brak klucza API", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var diagnostyka = new FirefliesDiagnostyka(TxtApiKey.Password);
            diagnostyka.Owner = this;
            diagnostyka.ShowDialog();
        }
    }
}
