using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class PodgladTranskrypcji : Window
    {
        private readonly long _transkrypcjaID;
        private readonly FirefliesService _firefliesService;

        private FirefliesTranskrypcja? _transkrypcja;

        public PodgladTranskrypcji(long transkrypcjaID)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _transkrypcjaID = transkrypcjaID;
            _firefliesService = new FirefliesService();

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _transkrypcja = await _firefliesService.PobierzTranskrypcjeZBazyPoId(_transkrypcjaID);
                if (_transkrypcja == null)
                {
                    MessageBox.Show("Nie znaleziono transkrypcji.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (_transkrypcja == null) return;

            // Nagłówek
            TxtTytul.Text = _transkrypcja.Tytul ?? "Transkrypcja spotkania";
            TxtData.Text = _transkrypcja.DataSpotkaniaDisplay;
            TxtCzas.Text = _transkrypcja.CzasTrwaniaDisplay;
            TxtUczestnicy.Text = $"{_transkrypcja.LiczbaUczestnikow} uczestników";

            // Transkrypcja
            TxtTranskrypcja.Text = _transkrypcja.Transkrypcja ?? "Brak transkrypcji";

            // Podsumowanie
            TxtPodsumowanie.Text = string.IsNullOrWhiteSpace(_transkrypcja.Podsumowanie)
                ? "Brak podsumowania"
                : _transkrypcja.Podsumowanie;

            // Akcje do wykonania
            if (_transkrypcja.AkcjeDoDziałania.Count > 0)
            {
                ListaAkcji.ItemsSource = _transkrypcja.AkcjeDoDziałania;
            }
            else
            {
                PanelAkcje.Visibility = Visibility.Collapsed;
            }

            // Słowa kluczowe
            if (_transkrypcja.SlowKluczowe.Count > 0)
            {
                ListaSlowKluczowych.ItemsSource = _transkrypcja.SlowKluczowe;
            }
            else
            {
                PanelSlowa.Visibility = Visibility.Collapsed;
            }

            // Następne kroki
            if (_transkrypcja.NastepneKroki.Count > 0)
            {
                ListaKrokow.ItemsSource = _transkrypcja.NastepneKroki;
            }
            else
            {
                PanelKroki.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnOtworzFireflies_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_transkrypcja?.TranskrypcjaUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_transkrypcja.TranskrypcjaUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można otworzyć linku: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Brak linku do transkrypcji w Fireflies.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUtworzNotatke_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implementacja tworzenia notatki z transkrypcji
            MessageBox.Show("Funkcja w przygotowaniu", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
