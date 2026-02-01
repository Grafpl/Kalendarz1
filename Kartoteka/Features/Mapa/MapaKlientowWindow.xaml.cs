using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Mapa
{
    public partial class MapaKlientowWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private GMapControl _gmap;
        private GMapOverlay _markersOverlay;
        private List<KlientMapa> _wszyscyKlienci = new();
        private List<KlientMapa> _filtrKlienci = new();
        private GeokodowanieService _geoService;

        private static readonly string _connLibraDefault = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connHandelDefault = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public MapaKlientowWindow() : this(_connLibraDefault, _connHandelDefault) { }

        public MapaKlientowWindow(string connLibra, string connHandel)
        {
            InitializeComponent();

            _connLibra = connLibra;
            _connHandel = connHandel;
            _geoService = new GeokodowanieService(connLibra);

            InitializeMap();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private void InitializeMap()
        {
            _gmap = new GMapControl();
            _gmap.MapProvider = GMapProviders.OpenStreetMap;
            _gmap.Position = new PointLatLng(52.0, 19.5); // Centrum Polski
            _gmap.MinZoom = 5;
            _gmap.MaxZoom = 18;
            _gmap.Zoom = 7;
            _gmap.DragButton = System.Windows.Forms.MouseButtons.Left;
            _gmap.ShowCenter = false;

            _markersOverlay = new GMapOverlay("markers");
            _gmap.Overlays.Add(_markersOverlay);

            _gmap.OnMarkerClick += GMap_OnMarkerClick;

            mapHost.Child = _gmap;
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                await _geoService.EnsureColumnsExistAsync();

                var service = new MapaKlientowService(_connLibra, _connHandel);
                _wszyscyKlienci = await service.PobierzKlientowDoMapyAsync();

                // Wypełnij listę handlowców
                var handlowcy = _wszyscyKlienci
                    .Select(k => k.Handlowiec)
                    .Where(h => !string.IsNullOrEmpty(h))
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();

                cmbHandlowiec.Items.Clear();
                cmbHandlowiec.Items.Add(new ComboBoxItem { Content = "Wszyscy", IsSelected = true });
                foreach (var h in handlowcy)
                    cmbHandlowiec.Items.Add(new ComboBoxItem { Content = h });

                ApplyFilters();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _wszyscyKlienci.Where(k => k.MaWspolrzedne).ToList();

            // Filtr kategorii
            var dozwoloneKat = new HashSet<string>();
            if (chkKatA.IsChecked == true) dozwoloneKat.Add("A");
            if (chkKatB.IsChecked == true) dozwoloneKat.Add("B");
            if (chkKatC.IsChecked == true) dozwoloneKat.Add("C");
            if (chkKatD.IsChecked == true) dozwoloneKat.Add("D");

            filtered = filtered.Where(k =>
            {
                if (string.IsNullOrEmpty(k.Kategoria) || !new[] { "A", "B", "C", "D" }.Contains(k.Kategoria))
                    return chkBezKat.IsChecked == true;
                return dozwoloneKat.Contains(k.Kategoria);
            }).ToList();

            // Filtr alertów
            if (chkAlerty.IsChecked == true)
                filtered = filtered.Where(k => k.MaAlert).ToList();

            // Filtr handlowca
            if (cmbHandlowiec.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "Wszyscy")
            {
                string handlowiec = item.Content?.ToString();
                filtered = filtered.Where(k => k.Handlowiec == handlowiec).ToList();
            }

            _filtrKlienci = filtered;
            RefreshMarkers();
            UpdateClientList();
            UpdateStatistics();
        }

        private void RefreshMarkers()
        {
            _markersOverlay.Markers.Clear();

            foreach (var k in _filtrKlienci)
            {
                if (!k.Latitude.HasValue || !k.Longitude.HasValue) continue;

                var color = GetMarkerColor(k);
                var marker = new GMarkerGoogle(
                    new PointLatLng(k.Latitude.Value, k.Longitude.Value),
                    GetMarkerType(k));

                marker.ToolTipText = $"{k.NazwaFirmy}\n" +
                                     $"Miasto: {k.Miasto}\n" +
                                     $"Kategoria: {k.Kategoria ?? "Brak"}\n" +
                                     $"Obroty: {k.ObrotyMiesieczne:N0} zł/mies\n" +
                                     $"Handlowiec: {k.Handlowiec}";
                marker.ToolTipMode = MarkerTooltipMode.OnMouseOver;
                marker.Tag = k;

                _markersOverlay.Markers.Add(marker);
            }
        }

        private GMarkerGoogleType GetMarkerType(KlientMapa k)
        {
            return k.Kategoria switch
            {
                "A" => GMarkerGoogleType.green,
                "B" => GMarkerGoogleType.blue,
                "C" => GMarkerGoogleType.yellow,
                "D" => GMarkerGoogleType.gray_small,
                _ => GMarkerGoogleType.red
            };
        }

        private Color GetMarkerColor(KlientMapa k)
        {
            return k.Kategoria switch
            {
                "A" => Color.FromArgb(16, 185, 129),
                "B" => Color.FromArgb(59, 130, 246),
                "C" => Color.FromArgb(245, 158, 11),
                "D" => Color.FromArgb(107, 114, 128),
                _ => Color.FromArgb(239, 68, 68)
            };
        }

        private void UpdateClientList()
        {
            lstKlienci.Items.Clear();
            foreach (var k in _filtrKlienci.OrderBy(k => k.NazwaFirmy).Take(200))
            {
                var item = new ListBoxItem
                {
                    Content = $"[{k.Kategoria ?? "?"}] {k.Skrot ?? k.NazwaFirmy} — {k.Miasto}",
                    Tag = k,
                    FontSize = 11
                };
                lstKlienci.Items.Add(item);
            }
        }

        private void UpdateStatistics()
        {
            int zWspolrzednymi = _wszyscyKlienci.Count(k => k.MaWspolrzedne);
            txtStatKlienci.Text = $"Klientów ogółem: {_wszyscyKlienci.Count}";
            txtStatMapa.Text = $"Na mapie: {zWspolrzednymi} ({_filtrKlienci.Count} widocznych)";
            txtStatObroty.Text = $"Obroty widocznych: {_filtrKlienci.Sum(k => k.ObrotyMiesieczne):N0} zł/mies";
        }

        private void GMap_OnMarkerClick(GMapMarker item, System.Windows.Forms.MouseEventArgs e)
        {
            if (item.Tag is KlientMapa k)
            {
                _gmap.Position = item.Position;
                _gmap.Zoom = 14;
            }
        }

        private void Filtr_Changed(object sender, RoutedEventArgs e) => ApplyFilters();
        private void CmbHandlowiec_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private void LstKlienci_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstKlienci.SelectedItem is ListBoxItem item && item.Tag is KlientMapa k && k.MaWspolrzedne)
            {
                _gmap.Position = new PointLatLng(k.Latitude.Value, k.Longitude.Value);
                _gmap.Zoom = 14;
            }
        }

        private async void BtnGeokoduj_Click(object sender, RoutedEventArgs e)
        {
            var bezWspolrzednych = _wszyscyKlienci
                .Where(k => !k.MaWspolrzedne && !string.IsNullOrEmpty(k.Miasto))
                .ToList();

            if (bezWspolrzednych.Count == 0)
            {
                txtGeokodStatus.Text = "Wszyscy klienci mają współrzędne.";
                return;
            }

            var result = MessageBox.Show(
                $"Znaleziono {bezWspolrzednych.Count} klientów bez współrzędnych.\n" +
                $"Geokodowanie użyje darmowego Nominatim (1 req/sek).\n" +
                $"Szacowany czas: ~{bezWspolrzednych.Count} sekund.\n\n" +
                $"Kontynuować?",
                "Geokodowanie adresów",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            btnGeokoduj.IsEnabled = false;
            int sukces = 0, blad = 0;

            foreach (var k in bezWspolrzednych)
            {
                txtGeokodStatus.Text = $"Geokodowanie: {sukces + blad + 1}/{bezWspolrzednych.Count} — {k.Miasto}...";

                var coords = await _geoService.GeokodujAdresAsync(k.Ulica, k.Miasto, k.KodPocztowy);
                if (coords.HasValue)
                {
                    k.Latitude = coords.Value.Lat;
                    k.Longitude = coords.Value.Lng;
                    await _geoService.ZapiszWspolrzedneAsync(k.Id, coords.Value.Lat, coords.Value.Lng, "OK");
                    sukces++;
                }
                else
                {
                    await _geoService.ZapiszBladGeokodowaniaAsync(k.Id, "NotFound");
                    blad++;
                }
            }

            txtGeokodStatus.Text = $"Zakończono: {sukces} znalezionych, {blad} nieznalezionych.";
            btnGeokoduj.IsEnabled = true;

            ApplyFilters();
            UpdateStatistics();
        }
    }
}
