using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.MapaFloty
{
    public partial class AdresyKlientowWindow : Window
    {
        private readonly WebfleetOrderService _svc = new();
        private readonly List<AdresRow> _rows = new();
        private readonly List<string> _kody;

        /// <summary>
        /// Otwiera okno adresów klientów. Jeśli kody != null, ładuje tylko te kody (brakujące).
        /// </summary>
        public AdresyKlientowWindow(List<string>? brakujaceKody = null)
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            _kody = brakujaceKody ?? new();
            Loaded += async (_, _) => await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                await _svc.EnsureTablesAsync();

                // Pobierz istniejące adresy
                var existing = await _svc.PobierzAdresyAsync(_kody);

                _rows.Clear();
                foreach (var kod in _kody)
                {
                    existing.TryGetValue(kod, out var addr);
                    _rows.Add(new AdresRow
                    {
                        KodKlienta = kod,
                        Nazwa = addr?.Nazwa ?? "",
                        Ulica = addr?.Ulica ?? "",
                        Miasto = addr?.Miasto ?? "",
                        KodPocztowy = addr?.KodPocztowy ?? "",
                        Lat = addr?.Lat ?? 0,
                        Lon = addr?.Lon ?? 0,
                        Status = addr?.Lat > 0 ? "OK" : "Brak GPS"
                    });
                }

                AdresyGrid.ItemsSource = _rows;
                StatusText.Text = $"{_rows.Count} klientów, {_rows.Count(r => r.Lat > 0)} z współrzędnymi GPS";
            }
            catch (Exception ex) { StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        private async void BtnGeocodeAll_Click(object sender, RoutedEventArgs e)
        {
            int ok = 0, fail = 0;
            foreach (var row in _rows)
            {
                if (row.Lat > 0 && row.Lon > 0) continue; // już ma GPS
                if (string.IsNullOrWhiteSpace(row.Ulica) && string.IsNullOrWhiteSpace(row.Miasto))
                {
                    row.Status = "Wpisz adres!";
                    fail++;
                    continue;
                }

                try
                {
                    row.Status = "Geokodowanie...";
                    AdresyGrid.Items.Refresh();

                    var (lat, lon) = await _svc.GeokodujAdresAsync(row.Ulica, row.Miasto, row.KodPocztowy);
                    if (lat != 0 && lon != 0)
                    {
                        row.Lat = lat;
                        row.Lon = lon;
                        row.Status = "OK";
                        ok++;
                    }
                    else
                    {
                        row.Status = "Nie znaleziono";
                        fail++;
                    }

                    // Czekaj 1s między requestami (Nominatim rate limit)
                    await Task.Delay(1100);
                }
                catch (Exception ex)
                {
                    row.Status = $"Błąd: {ex.Message[..Math.Min(30, ex.Message.Length)]}";
                    fail++;
                }
            }
            AdresyGrid.Items.Refresh();
            StatusText.Text = $"Geokodowanie zakończone: {ok} OK, {fail} błędów";
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int saved = 0;
                foreach (var row in _rows)
                {
                    var addr = new WebfleetOrderService.KlientAdresInfo
                    {
                        KodKlienta = row.KodKlienta,
                        Nazwa = row.Nazwa,
                        Ulica = row.Ulica,
                        Miasto = row.Miasto,
                        KodPocztowy = row.KodPocztowy,
                        Lat = row.Lat,
                        Lon = row.Lon
                    };
                    await _svc.ZapiszAdresAsync(addr, App.UserFullName ?? "system");
                    saved++;
                }
                StatusText.Text = $"Zapisano {saved} adresów";
                MessageBox.Show($"Zapisano {saved} adresów klientów.\n\nTeraz możesz ponowić wysyłanie kursu do Webfleet.",
                    "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        public class AdresRow : INotifyPropertyChanged
        {
            public string KodKlienta { get; set; } = "";
            private string _nazwa = "", _ulica = "", _miasto = "", _kodPocztowy = "", _status = "";
            private double _lat, _lon;

            public string Nazwa { get => _nazwa; set { _nazwa = value; N(); } }
            public string Ulica { get => _ulica; set { _ulica = value; N(); } }
            public string Miasto { get => _miasto; set { _miasto = value; N(); } }
            public string KodPocztowy { get => _kodPocztowy; set { _kodPocztowy = value; N(); } }
            public double Lat { get => _lat; set { _lat = value; N(); } }
            public double Lon { get => _lon; set { _lon = value; N(); } }
            public string Status { get => _status; set { _status = value; N(); } }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void N([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }
    }
}
