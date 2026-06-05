using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Mapa
{
    public partial class EdytorGpsKlientaWindow : Window
    {
        private readonly KlientMapa _klient;
        private readonly GeokodowanieService _geo;

        private double? _nowaLat;
        private double? _nowaLng;
        private string _zrodlo = "";

        public bool Zapisano { get; private set; }
        public bool Usunieto { get; private set; }
        public double? NowaLatitude { get; private set; }
        public double? NowaLongitude { get; private set; }

        private static readonly Regex _coordsRegex = new Regex(
            @"(-?\d{1,2}[.,]\d+)\s*[,;\s°NS]+\s*(-?\d{1,3}[.,]\d+)",
            RegexOptions.Compiled);

        public EdytorGpsKlientaWindow(KlientMapa klient, GeokodowanieService geo)
        {
            InitializeComponent();
            _klient = klient ?? throw new ArgumentNullException(nameof(klient));
            _geo = geo ?? throw new ArgumentNullException(nameof(geo));

            txtNazwa.Text = klient.NazwaFirmy ?? "(brak nazwy)";
            txtPodtytul.Text = $"ID {klient.Id} · {(string.IsNullOrEmpty(klient.Skrot) ? "—" : klient.Skrot)} · Kat. {klient.Kategoria ?? "?"}";

            txtUlica.Text = string.IsNullOrEmpty(klient.Ulica) ? "— (brak w Sage)" : klient.Ulica;
            txtMiasto.Text = string.IsNullOrEmpty(klient.Miasto) ? "— (brak w Sage)" : klient.Miasto;
            txtKodPocztowy.Text = string.IsNullOrEmpty(klient.KodPocztowy) ? "—" : klient.KodPocztowy;
            txtNip.Text = string.IsNullOrEmpty(klient.NIP) ? "—" : klient.NIP;
            txtHandlowiec.Text = string.IsNullOrEmpty(klient.Handlowiec) ? "—" : klient.Handlowiec;

            if (klient.MaWspolrzedne)
            {
                txtAktualneGps.Text = $"{klient.Latitude:0.000000}, {klient.Longitude:0.000000}";
                btnUsunGps.Visibility = Visibility.Visible;
            }
            else
            {
                txtAktualneGps.Text = "❌ Brak współrzędnych";
                txtAktualneGps.Foreground = Brushes.IndianRed;
            }

            txtPodglad.Text = "Wybierz źródło współrzędnych (wklej lub zgeokoduj) →";

            string podpowiedz = !string.IsNullOrEmpty(klient.Miasto)
                ? $"{klient.Miasto}, Polska"
                : "";
            txtAltAdres.Text = podpowiedz;
        }

        private void BtnOtworzGoogleMaps_Click(object sender, RoutedEventArgs e)
        {
            string query = string.Join(" ", new[] { _klient.Ulica, _klient.KodPocztowy, _klient.Miasto, "Polska" }
                .Where(s => !string.IsNullOrEmpty(s)));

            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Brak adresu w Sage — nie ma czego szukać. Wpisz tekst po prawej i kliknij Szukaj.",
                    "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string url = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć przeglądarki:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtWklejCoords_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = txtWklejCoords.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text))
            {
                txtWklejStatus.Text = "";
                ResetNowe();
                return;
            }

            var match = _coordsRegex.Match(text);
            if (!match.Success)
            {
                txtWklejStatus.Text = "❌ Nie rozpoznano formatu. Oczekiwane: 52.234567, 21.012345";
                txtWklejStatus.Foreground = Brushes.IndianRed;
                ResetNowe();
                return;
            }

            string latStr = match.Groups[1].Value.Replace(',', '.');
            string lngStr = match.Groups[2].Value.Replace(',', '.');

            if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(lngStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lng))
            {
                txtWklejStatus.Text = "❌ Niepoprawne liczby";
                txtWklejStatus.Foreground = Brushes.IndianRed;
                ResetNowe();
                return;
            }

            if (lat < 48 || lat > 56 || lng < 13 || lng > 25)
            {
                txtWklejStatus.Text = $"⚠ {lat:0.000000}, {lng:0.000000} — to nie wygląda na Polskę. Może masz odwróconą kolejność?";
                txtWklejStatus.Foreground = Brushes.DarkOrange;
            }
            else
            {
                txtWklejStatus.Text = $"✅ Rozpoznano: {lat:0.000000}, {lng:0.000000}";
                txtWklejStatus.Foreground = Brushes.DarkGreen;
            }

            UstawWspolrzedne(lat, lng, "wklejone z Google Maps");
        }

        private void TxtAltAdres_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnGeokodujAlt_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void BtnGeokodujAlt_Click(object sender, RoutedEventArgs e)
        {
            string opis = txtAltAdres.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(opis))
            {
                txtAltStatus.Text = "Wpisz opis lokalizacji.";
                txtAltStatus.Foreground = Brushes.IndianRed;
                return;
            }

            btnGeokodujAlt.IsEnabled = false;
            txtAltStatus.Text = "🔍 Szukam w Nominatim (OpenStreetMap)...";
            txtAltStatus.Foreground = Brushes.Gray;

            try
            {
                var coords = await _geo.GeokodujTekstemAsync(opis);
                if (coords.HasValue)
                {
                    UstawWspolrzedne(coords.Value.Lat, coords.Value.Lng, $"Nominatim: {opis}");
                    txtAltStatus.Text = $"✅ Znaleziono: {coords.Value.Lat:0.000000}, {coords.Value.Lng:0.000000}";
                    txtAltStatus.Foreground = Brushes.DarkGreen;
                }
                else
                {
                    txtAltStatus.Text = "❌ Nie znaleziono. Spróbuj prościej (samo miasto, znana nazwa, bez literówek).";
                    txtAltStatus.Foreground = Brushes.IndianRed;
                    ResetNowe();
                }
            }
            catch (Exception ex)
            {
                txtAltStatus.Text = $"Błąd: {ex.Message}";
                txtAltStatus.Foreground = Brushes.IndianRed;
                ResetNowe();
            }
            finally { btnGeokodujAlt.IsEnabled = true; }
        }

        private void UstawWspolrzedne(double lat, double lng, string zrodlo)
        {
            _nowaLat = lat;
            _nowaLng = lng;
            _zrodlo = zrodlo;
            txtPodglad.Text = $"➡ Nowe GPS: {lat:0.000000}, {lng:0.000000}  ({zrodlo})";
            txtPodglad.Foreground = Brushes.DarkGreen;
            btnZapisz.IsEnabled = true;
        }

        private void ResetNowe()
        {
            _nowaLat = null;
            _nowaLng = null;
            _zrodlo = "";
            txtPodglad.Text = "Wybierz źródło współrzędnych (wklej lub zgeokoduj) →";
            txtPodglad.Foreground = (Brush)new BrushConverter().ConvertFrom("#374151");
            btnZapisz.IsEnabled = false;
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!_nowaLat.HasValue || !_nowaLng.HasValue) return;

            btnZapisz.IsEnabled = false;
            try
            {
                string status = _zrodlo.StartsWith("Nominatim", StringComparison.OrdinalIgnoreCase) ? "Manual-Nominatim" : "Manual";
                await _geo.ZapiszWspolrzedneAsync(_klient.Id, _nowaLat.Value, _nowaLng.Value, status);
                Zapisano = true;
                NowaLatitude = _nowaLat;
                NowaLongitude = _nowaLng;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się zapisać:\n{ex.Message}",
                    "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Error);
                btnZapisz.IsEnabled = true;
            }
        }

        private async void BtnUsunGps_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                $"Usunąć współrzędne klienta:\n\n  {_klient.NazwaFirmy}\n\n" +
                "Po usunięciu klient zniknie z mapy do czasu ponownego geokodowania.",
                "Usunięcie GPS",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (r != MessageBoxResult.Yes) return;

            btnUsunGps.IsEnabled = false;
            try
            {
                await _geo.UsunWspolrzedneAsync(_klient.Id);
                Usunieto = true;
                NowaLatitude = null;
                NowaLongitude = null;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się usunąć:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                btnUsunGps.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
