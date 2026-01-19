using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class PodgladSpotkania : Window
    {
        private readonly string _userID;
        private readonly long _spotkaniID;
        private readonly SpotkaniaService _spotkaniaService;

        private SpotkanieModel? _spotkanie;

        public PodgladSpotkania(string userID, long spotkaniID)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _userID = userID;
            _spotkaniID = spotkaniID;

            var notyfikacje = NotyfikacjeManager.GetInstance(userID);
            _spotkaniaService = new SpotkaniaService(notyfikacje);

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _spotkanie = await _spotkaniaService.PobierzSpotkanie(_spotkaniID);
                if (_spotkanie == null)
                {
                    MessageBox.Show("Nie znaleziono spotkania.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                WypelnijDane();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WypelnijDane()
        {
            if (_spotkanie == null) return;

            // Nagłówek
            TxtTytul.Text = _spotkanie.Tytul;
            TxtStatus.Text = _spotkanie.StatusDisplay;
            TxtTyp.Text = _spotkanie.TypSpotkaniaDisplay;

            // Kolor nagłówka
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_spotkanie.Kolor);
                HeaderBorder.Background = new SolidColorBrush(color);
            }
            catch { }

            // Data i czas
            TxtData.Text = _spotkanie.DataSpotkania.ToString("dddd, d MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));
            TxtCzas.Text = $"{_spotkanie.DataSpotkania:HH:mm} - {_spotkanie.DataSpotkania.AddMinutes(_spotkanie.CzasTrwaniaMin):HH:mm} ({_spotkanie.CzasTrwaniaTekst})";
            TxtCzasDoSpotkania.Text = _spotkanie.CzasDoSpotkaniaDisplay;
            TxtCzasDoSpotkania.Visibility = _spotkanie.CzyNadchodzace ? Visibility.Visible : Visibility.Collapsed;

            // Lokalizacja / Link
            TxtLokalizacja.Text = string.IsNullOrWhiteSpace(_spotkanie.Lokalizacja) ? "Nie podano" : _spotkanie.Lokalizacja;
            if (!string.IsNullOrWhiteSpace(_spotkanie.LinkSpotkania))
            {
                TxtLink.Text = _spotkanie.LinkSpotkania;
                TxtLink.Visibility = Visibility.Visible;
            }
            else
            {
                TxtLink.Visibility = Visibility.Collapsed;
            }

            // Kontrahent
            if (!string.IsNullOrWhiteSpace(_spotkanie.KontrahentNazwa))
            {
                PanelKontrahent.Visibility = Visibility.Visible;
                LblKontrahent.Text = _spotkanie.TypSpotkania == TypSpotkania.Odbiorca ? "🏢 Odbiorca" : "🐔 Hodowca";
                TxtKontrahent.Text = _spotkanie.KontrahentNazwa;
            }

            // Opis
            TxtOpis.Text = string.IsNullOrWhiteSpace(_spotkanie.Opis) ? "Brak opisu" : _spotkanie.Opis;

            // Uczestnicy
            ListaUczestnikow.ItemsSource = _spotkanie.Uczestnicy;

            // Organizator
            TxtOrganizator.Text = _spotkanie.OrganizatorNazwa ?? _spotkanie.OrganizatorID;

            // Powiązania
            if (_spotkanie.MaNotatke || _spotkanie.MaTranskrypcje)
            {
                PanelPowiazania.Visibility = Visibility.Visible;
                BtnNotatka.Visibility = _spotkanie.MaNotatke ? Visibility.Visible : Visibility.Collapsed;
                BtnTranskrypcja.Visibility = _spotkanie.MaTranskrypcje ? Visibility.Visible : Visibility.Collapsed;
            }

            // Przyciski akcji
            var uczestnik = _spotkanie.Uczestnicy.FirstOrDefault(u => u.OperatorID == _userID);
            if (uczestnik != null && uczestnik.StatusZaproszenia == StatusZaproszenia.Oczekuje &&
                _spotkanie.OrganizatorID != _userID)
            {
                BtnAkceptuj.Visibility = Visibility.Visible;
                BtnOdrzuc.Visibility = Visibility.Visible;
            }

            // Edycja - tylko organizator
            BtnEdytuj.Visibility = _spotkanie.OrganizatorID == _userID ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_spotkanie?.LinkSpotkania))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_spotkanie.LinkSpotkania) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można otworzyć linku: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnAkceptuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _spotkaniaService.AktualizujStatusUczestnika(_spotkaniID, _userID, StatusZaproszenia.Zaakceptowane);
                MessageBox.Show("Zaakceptowano zaproszenie.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdrzuc_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz odrzucić zaproszenie?", "Potwierdź",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _spotkaniaService.AktualizujStatusUczestnika(_spotkaniID, _userID, StatusZaproszenia.Odrzucone);
                    MessageBox.Show("Odrzucono zaproszenie.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnNotatka_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Otwórz notatkę
            MessageBox.Show("Funkcja w przygotowaniu", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnTranskrypcja_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Otwórz transkrypcję
            MessageBox.Show("Funkcja w przygotowaniu", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            var editor = new EdytorSpotkania(_userID, _spotkaniID);
            editor.Owner = this.Owner;

            if (editor.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
