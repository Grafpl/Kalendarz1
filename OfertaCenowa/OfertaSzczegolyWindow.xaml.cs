using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Okno szczegółów oferty
    /// </summary>
    public partial class OfertaSzczegolyWindow : Window
    {
        private readonly OfertaRepository _repository = new();
        private readonly OfertaSzczegoly _oferta;

        public OfertaSzczegolyWindow(OfertaSzczegoly oferta)
        {
            InitializeComponent();

            _oferta = oferta;
            WypelnijDane();
            _ = LoadHistoriaAsync();
        }

        private void WypelnijDane()
        {
            // Nagłówek
            txtNumerOferty.Text = _oferta.NumerOferty;
            txtStatus.Text = $"Status: {_oferta.Status}";
            txtStatusIkona.Text = GetStatusIkona(_oferta.Status);
            txtDataWystawienia.Text = $"Data: {_oferta.DataWystawienia:dd.MM.yyyy}";
            txtDataWaznosci.Text = $"Ważna do: {_oferta.DataWaznosci:dd.MM.yyyy}";

            // Klient
            txtKlientNazwa.Text = _oferta.KlientNazwa;
            txtKlientNIP.Text = string.IsNullOrEmpty(_oferta.KlientNIP) ? "-" : _oferta.KlientNIP;
            
            string adres = "";
            if (!string.IsNullOrEmpty(_oferta.KlientAdres))
                adres = _oferta.KlientAdres;
            if (!string.IsNullOrEmpty(_oferta.KlientMiejscowosc))
                adres += (adres.Length > 0 ? ", " : "") + _oferta.KlientMiejscowosc;
            txtKlientAdres.Text = string.IsNullOrEmpty(adres) ? "-" : adres;

            // Handlowiec
            txtHandlowiecNazwa.Text = _oferta.HandlowiecNazwa;
            txtWartoscNetto.Text = $"{_oferta.WartoscNetto:N2} zł";

            // Produkty
            dgPozycje.ItemsSource = _oferta.Pozycje;

            // Notatki
            txtNotatki.Text = string.IsNullOrEmpty(_oferta.Notatki) ? "Brak notatek" : _oferta.Notatki;

            // Tytuł okna
            Title = $"📄 Szczegóły Oferty - {_oferta.NumerOferty}";
        }

        private async Task LoadHistoriaAsync()
        {
            try
            {
                var historia = await _repository.PobierzHistorieAsync(_oferta.ID);
                icHistoria.ItemsSource = historia;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania historii: {ex.Message}");
            }
        }

        private string GetStatusIkona(string status)
        {
            return status switch
            {
                "Nowa" => "📝",
                "Wyslana" => "📧",
                "Zaakceptowana" => "✅",
                "Odrzucona" => "❌",
                "Anulowana" => "🚫",
                "Wygasla" => "⏰",
                _ => "❓"
            };
        }

        private void BtnOtworzPDF_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_oferta.SciezkaPliku) || !File.Exists(_oferta.SciezkaPliku))
            {
                MessageBox.Show($"Plik PDF nie został znaleziony:\n{_oferta.SciezkaPliku}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(_oferta.SciezkaPliku) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć pliku:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOtworzFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_oferta.SciezkaPliku))
            {
                MessageBox.Show("Ścieżka do pliku nie jest dostępna.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string folder = Path.GetDirectoryName(_oferta.SciezkaPliku);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show($"Folder nie istnieje:\n{folder}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć folderu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
