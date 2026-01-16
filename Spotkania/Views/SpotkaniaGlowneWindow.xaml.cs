using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class SpotkaniaGlowneWindow : Window
    {
        private readonly string _userID;
        private readonly SpotkaniaService _spotkaniaService;
        private readonly FirefliesService _firefliesService;
        private readonly NotyfikacjeService _notyfikacjeService;

        private List<SpotkanieListItem> _spotkania = new();
        private List<SpotkanieListItem> _nadchodzace = new();

        public SpotkaniaGlowneWindow(string userID)
        {
            InitializeComponent();
            _userID = userID;

            // Inicjalizacja serwisów
            _notyfikacjeService = NotyfikacjeManager.GetInstance(userID);
            _spotkaniaService = new SpotkaniaService(_notyfikacjeService);
            _firefliesService = new FirefliesService();

            // Subskrypcja eventów powiadomień
            _notyfikacjeService.ZmianaLicznika += OnZmianaLicznika;
            _notyfikacjeService.SpotkanieZaChwile += OnSpotkanieZaChwile;

            // Ustawienia domyślne filtrów
            DpOdDaty.SelectedDate = DateTime.Today.AddMonths(-1);
            DpDoDaty.SelectedDate = DateTime.Today.AddMonths(1);

            Loaded += async (s, e) => await LoadDataAsync();
            Closing += (s, e) =>
            {
                _notyfikacjeService.ZmianaLicznika -= OnZmianaLicznika;
                _notyfikacjeService.SpotkanieZaChwile -= OnSpotkanieZaChwile;
            };
        }

        #region Ładowanie danych

        private async Task LoadDataAsync()
        {
            try
            {
                // Uruchom powiadomienia
                _notyfikacjeService.Start();

                // Ładuj dane równolegle
                await Task.WhenAll(
                    LoadNadchodzaceAsync(),
                    LoadSpotkaniaAsync(),
                    LoadTranskrypcjeAsync(),
                    LoadNotatkiAsync()
                );

                // Aktualizuj licznik powiadomień
                await _notyfikacjeService.SprawdzPowiadomieniaAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadNadchodzaceAsync()
        {
            int dni = CmbZakresNadchodzace.SelectedIndex switch
            {
                0 => 1,  // Dzisiaj
                1 => 7,  // Tydzień
                2 => 30, // Miesiąc
                _ => 7
            };

            _nadchodzace = await _spotkaniaService.PobierzNadchodzaceSpotkania(_userID, dni);
            ListaNadchodzace.ItemsSource = _nadchodzace;
        }

        private async Task LoadSpotkaniaAsync()
        {
            string? typFiltr = (CmbTypFiltr.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (typFiltr == "Wszystkie") typFiltr = null;

            string? statusFiltr = (CmbStatusFiltr.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (statusFiltr == "Wszystkie") statusFiltr = null;

            _spotkania = await _spotkaniaService.PobierzListeSpotkań(
                operatorId: _userID,
                odDaty: DpOdDaty.SelectedDate,
                doDaty: DpDoDaty.SelectedDate,
                typSpotkania: typFiltr,
                status: statusFiltr,
                tylkoMojeSpotkania: ChkMojeSpotkania.IsChecked ?? false
            );

            GridSpotkania.ItemsSource = _spotkania;
        }

        private async Task LoadTranskrypcjeAsync()
        {
            try
            {
                var transkrypcje = await _firefliesService.PobierzTranskrypcjeZBazy();
                GridTranskrypcje.ItemsSource = transkrypcje;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania transkrypcji: {ex.Message}");
            }
        }

        private async Task LoadNotatkiAsync()
        {
            try
            {
                // Pobierz notatki z istniejącego modułu
                // Na razie puste - będzie zasilane z modułu NotatkiZeSpotkan
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania notatek: {ex.Message}");
            }
        }

        #endregion

        #region Eventy powiadomień

        private void OnZmianaLicznika(object? sender, NotyfikacjeLicznik licznik)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLicznikNotyfikacji.Text = licznik.LicznikDisplay;
                BadgeNotyfikacje.Visibility = licznik.MaNotyfikacje ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void OnSpotkanieZaChwile(object? sender, NotyfikacjaModel notyfikacja)
        {
            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"{notyfikacja.Tresc}\n\n{(notyfikacja.LinkSpotkania != null ? "Czy chcesz otworzyć link do spotkania?" : "")}",
                    notyfikacja.Tytul ?? "Spotkanie za chwilę!",
                    notyfikacja.LinkSpotkania != null ? MessageBoxButton.YesNo : MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(notyfikacja.LinkSpotkania))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(notyfikacja.LinkSpotkania) { UseShellExecute = true });
                    }
                    catch { }
                }
            });
        }

        #endregion

        #region Przyciski nagłówka

        private async void BtnNotyfikacje_Click(object sender, RoutedEventArgs e)
        {
            var window = new NotyfikacjeWindow(_userID);
            window.Owner = this;
            window.ShowDialog();

            // Odśwież licznik
            await _notyfikacjeService.SprawdzPowiadomieniaAsync();
        }

        private void BtnFireflies_Click(object sender, RoutedEventArgs e)
        {
            var window = new FirefliesKonfiguracjaWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnNoweSpotkanie_Click(object sender, RoutedEventArgs e)
        {
            var editor = new EdytorSpotkania(_userID);
            editor.Owner = this;

            if (editor.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        #endregion

        #region Panel nadchodzących

        private void CmbZakresNadchodzace_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _ = LoadNadchodzaceAsync();
            }
        }

        private void ListaNadchodzace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaNadchodzace.SelectedItem is SpotkanieListItem item)
            {
                // Znajdź i zaznacz w głównej liście
                var spotkanie = _spotkania.FirstOrDefault(s => s.SpotkaniID == item.SpotkaniID);
                if (spotkanie != null)
                {
                    GridSpotkania.SelectedItem = spotkanie;
                    GridSpotkania.ScrollIntoView(spotkanie);
                }
            }
        }

        #endregion

        #region Zakładka: Wszystkie spotkania

        private async void BtnFiltruj_Click(object sender, RoutedEventArgs e)
        {
            await LoadSpotkaniaAsync();
        }

        private void GridSpotkania_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridSpotkania.SelectedItem is SpotkanieListItem item)
            {
                OtworzPodgladSpotkania(item.SpotkaniID);
            }
        }

        private void BtnPodglad_Click(object sender, RoutedEventArgs e)
        {
            if (GridSpotkania.SelectedItem is SpotkanieListItem item)
            {
                OtworzPodgladSpotkania(item.SpotkaniID);
            }
            else
            {
                MessageBox.Show("Wybierz spotkanie z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (GridSpotkania.SelectedItem is SpotkanieListItem item)
            {
                var editor = new EdytorSpotkania(_userID, item.SpotkaniID);
                editor.Owner = this;

                if (editor.ShowDialog() == true)
                {
                    _ = LoadDataAsync();
                }
            }
            else
            {
                MessageBox.Show("Wybierz spotkanie z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            if (GridSpotkania.SelectedItem is SpotkanieListItem item)
            {
                if (item.Status == "Anulowane")
                {
                    MessageBox.Show("To spotkanie jest już anulowane.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Czy na pewno chcesz anulować spotkanie \"{item.Tytul}\"?\n\nWszyscy uczestnicy zostaną powiadomieni.",
                    "Potwierdź anulowanie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _spotkaniaService.AnulujSpotkanie(item.SpotkaniID);
                        MessageBox.Show("Spotkanie zostało anulowane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd anulowania spotkania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Wybierz spotkanie z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OtworzPodgladSpotkania(long spotkaniId)
        {
            var window = new PodgladSpotkania(_userID, spotkaniId);
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnNotatka_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long spotkaniId)
            {
                // Otwórz notatkę powiązaną ze spotkaniem
                // TODO: Implementacja
            }
        }

        private void BtnTranskrypcja_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long spotkaniId)
            {
                // Otwórz transkrypcję powiązaną ze spotkaniem
                // TODO: Implementacja
            }
        }

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long spotkaniId)
            {
                var spotkanie = _spotkania.FirstOrDefault(s => s.SpotkaniID == spotkaniId);
                // Pobierz link i otwórz
                // TODO: Implementacja
            }
        }

        #endregion

        #region Zakładka: Notatki

        private void BtnOtworzNotatki_Click(object sender, RoutedEventArgs e)
        {
            var window = new NotatkiZeSpotkan.NotatkirGlownyWindow(_userID);
            window.Show();
        }

        #endregion

        #region Zakładka: Transkrypcje

        private async void BtnSynchronizuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnSynchronizuj.IsEnabled = false;
                ProgressSync.Visibility = Visibility.Visible;
                TxtStatusSync.Text = "Synchronizacja...";

                var progress = new Progress<FirefliesSyncStatus>(status =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatusSync.Text = status.AktualnyEtap;
                        if (status.MaksymalnyPostep > 0)
                        {
                            ProgressSync.Maximum = status.MaksymalnyPostep;
                            ProgressSync.Value = status.Postep;
                        }
                    });
                });

                var result = await _firefliesService.SynchronizujTranskrypcje(progress);

                if (result.MaBlad)
                {
                    TxtStatusSync.Text = $"Błąd: {result.BladMessage}";
                }
                else
                {
                    TxtStatusSync.Text = $"Zaimportowano: {result.ZaimportowanoTranskrypcji} transkrypcji";
                    await LoadTranskrypcjeAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd synchronizacji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatusSync.Text = "Błąd synchronizacji";
            }
            finally
            {
                BtnSynchronizuj.IsEnabled = true;
                ProgressSync.Visibility = Visibility.Collapsed;
            }
        }

        private void GridTranskrypcje_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridTranskrypcje.SelectedItem is FirefliesTranskrypcjaListItem item)
            {
                OtworzEdytorTranskrypcji(item);
            }
        }

        private void BtnEdytujTranskrypcje_Click(object sender, RoutedEventArgs e)
        {
            if (GridTranskrypcje.SelectedItem is FirefliesTranskrypcjaListItem item)
            {
                OtworzEdytorTranskrypcji(item);
            }
            else
            {
                MessageBox.Show("Wybierz transkrypcję z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OtworzEdytorTranskrypcji(FirefliesTranskrypcjaListItem item)
        {
            var window = new TranskrypcjaSzczegolyWindow(item.FirefliesID, item.TranskrypcjaID);
            window.Owner = this;

            if (window.ShowDialog() == true)
            {
                // Odśwież listę po zapisaniu zmian
                _ = LoadTranskrypcjeAsync();
            }
        }

        private async void BtnUtworzNotatkeZTranskrypcji_Click(object sender, RoutedEventArgs e)
        {
            if (GridTranskrypcje.SelectedItem is FirefliesTranskrypcjaListItem item)
            {
                if (item.MaNotatke)
                {
                    MessageBox.Show("Ta transkrypcja ma już powiązaną notatkę.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // TODO: Otwórz edytor notatki z danymi z transkrypcji
                MessageBox.Show("Funkcja w przygotowaniu", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Wybierz transkrypcję z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnPowiazTranskrypcje_Click(object sender, RoutedEventArgs e)
        {
            if (GridTranskrypcje.SelectedItem is FirefliesTranskrypcjaListItem item)
            {
                if (item.MaSpotkanie)
                {
                    MessageBox.Show("Ta transkrypcja jest już powiązana ze spotkaniem.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // TODO: Wybór spotkania do powiązania
                MessageBox.Show("Funkcja w przygotowaniu", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Wybierz transkrypcję z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnGlobalneSzukanie_Click(object sender, RoutedEventArgs e)
        {
            var searchWindow = new GlobalneSzukanieWindow(_firefliesService);
            searchWindow.Owner = this;
            searchWindow.TranskrypcjaOtwarta += (firefliesId, transkrypcjaId) =>
            {
                var window = new TranskrypcjaSzczegolyWindow(firefliesId, transkrypcjaId);
                window.Owner = this;
                if (window.ShowDialog() == true)
                {
                    _ = LoadTranskrypcjeAsync();
                }
            };
            searchWindow.Show();
        }

        #endregion
    }
}
