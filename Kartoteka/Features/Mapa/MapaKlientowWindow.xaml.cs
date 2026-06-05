using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Mapa
{
    public partial class MapaKlientowWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private List<KlientMapa> _wszyscyKlienci = new();
        private List<KlientMapa> _filtrKlienci = new();
        private readonly GeokodowanieService _geoService;

        private string _mapFolder = "";
        private bool _mapReady;
        private bool _danePoLoad;

        private static readonly string _connLibraDefault = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connHandelDefault = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public MapaKlientowWindow() : this(_connLibraDefault, _connHandelDefault) { }

        public MapaKlientowWindow(string connLibra, string connHandel)
        {
            InitializeComponent();

            _connLibra = connLibra;
            _connHandel = connHandel;
            _geoService = new GeokodowanieService(connLibra);

            Loaded += async (s, e) =>
            {
                await InitializeWebViewAsync();
                await LoadDataAsync();
            };
        }

        // ══════════════════════════════════════════════════════════════════
        // WebView2 + Leaflet
        // ══════════════════════════════════════════════════════════════════

        private async Task InitializeWebViewAsync()
        {
            try
            {
                await mapHost.EnsureCoreWebView2Async();

                _mapFolder = Path.Combine(Path.GetTempPath(), "MapaKlientow_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(_mapFolder);
                File.WriteAllText(Path.Combine(_mapFolder, "map.html"), GenerateMapHtml(), Encoding.UTF8);

                var cw = mapHost.CoreWebView2;
                // Reflection — typy z Microsoft.Web.WebView2.Core kolidują z Uno.UI
                var accessKindType = cw.GetType().Assembly.GetType("Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind")!;
                cw.GetType().GetMethod("SetVirtualHostNameToFolderMapping")!
                    .Invoke(cw, new object[] { "mapaklientow.local", _mapFolder, Enum.Parse(accessKindType, "Allow") });

                cw.WebMessageReceived += (_, a) =>
                {
                    try
                    {
                        string raw = a.TryGetWebMessageAsString();
                        var msg = JsonConvert.DeserializeAnonymousType(raw, new { action = "", id = 0 });
                        if (msg == null) return;
                        if (msg.action == "edit")
                        {
                            var k = _wszyscyKlienci.FirstOrDefault(x => x.Id == msg.id);
                            if (k != null) _ = OtworzEdytorGpsAsync(k);
                        }
                    }
                    catch { }
                };

                mapHost.NavigationCompleted += (_, a) =>
                {
                    _mapReady = true;
                    if (a.IsSuccess && _filtrKlienci.Count > 0)
                        _ = WyslijKlientowDoMapyAsync();
                };

                cw.Navigate("https://mapaklientow.local/map.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji mapy:\n\n{ex.Message}",
                    "WebView2", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try { if (Directory.Exists(_mapFolder)) Directory.Delete(_mapFolder, true); } catch { }
        }

        // ══════════════════════════════════════════════════════════════════
        // Ładowanie danych
        // ══════════════════════════════════════════════════════════════════

        private async Task LoadDataAsync()
        {
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                await _geoService.EnsureColumnsExistAsync();

                var service = new MapaKlientowService(_connLibra, _connHandel);
                _wszyscyKlienci = await service.PobierzKlientowDoMapyAsync();

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

                _danePoLoad = true;
                ApplyFilters();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych mapy:\n\n{ex.Message}\n\n{ex.GetType().Name}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Filtry
        // ══════════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            if (!_danePoLoad) return;

            var filtered = _wszyscyKlienci.AsEnumerable();

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
            });

            if (chkAlerty.IsChecked == true)
                filtered = filtered.Where(k => k.MaAlert);

            if (cmbHandlowiec.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "Wszyscy")
            {
                string handlowiec = item.Content?.ToString() ?? "";
                filtered = filtered.Where(k => k.Handlowiec == handlowiec);
            }

            _filtrKlienci = filtered.ToList();
            _ = WyslijKlientowDoMapyAsync();
            UpdateClientList();
            UpdateStatistics();
        }

        private async Task WyslijKlientowDoMapyAsync()
        {
            if (!_mapReady) return;

            var dataDoMapy = _filtrKlienci
                .Where(k => k.MaWspolrzedne)
                .Select(k => new
                {
                    Id = k.Id,
                    Lat = k.Latitude.Value,
                    Lng = k.Longitude.Value,
                    NazwaFirmy = k.NazwaFirmy ?? "",
                    Skrot = k.Skrot ?? "",
                    Ulica = k.Ulica ?? "",
                    Miasto = k.Miasto ?? "",
                    KodPocztowy = k.KodPocztowy ?? "",
                    Kategoria = k.Kategoria ?? "",
                    Handlowiec = k.Handlowiec ?? "",
                    ObrotyMiesieczne = (double)k.ObrotyMiesieczne,
                    MaAlert = k.MaAlert
                })
                .ToList();

            string json = JsonConvert.SerializeObject(dataDoMapy);
            try { await mapHost.CoreWebView2.ExecuteScriptAsync($"updateClients({json})"); } catch { }
        }

        // ══════════════════════════════════════════════════════════════════
        // Sidebar — lista, wyszukiwarka, statystyki
        // ══════════════════════════════════════════════════════════════════

        private void UpdateClientList()
        {
            string szukaj = (txtSzukajKlient?.Text ?? "").Trim().ToLowerInvariant();

            IEnumerable<KlientMapa> pokazani = _filtrKlienci;
            if (!string.IsNullOrEmpty(szukaj))
            {
                pokazani = pokazani.Where(k =>
                    (k.NazwaFirmy ?? "").ToLowerInvariant().Contains(szukaj) ||
                    (k.Skrot ?? "").ToLowerInvariant().Contains(szukaj) ||
                    (k.Miasto ?? "").ToLowerInvariant().Contains(szukaj));
            }

            var lista = pokazani.OrderBy(k => !k.MaWspolrzedne).ThenBy(k => k.NazwaFirmy).Take(500).ToList();

            lstKlienci.Items.Clear();
            foreach (var k in lista)
            {
                string status = k.MaWspolrzedne ? "✅" : "❌";
                string gps = k.MaWspolrzedne
                    ? $"GPS: {k.Latitude:0.000000}, {k.Longitude:0.000000}"
                    : "Brak GPS — dwuklik aby ustawić";

                var item = new ListBoxItem
                {
                    Content = $"{status} [{k.Kategoria ?? "?"}] {(string.IsNullOrEmpty(k.Skrot) ? k.NazwaFirmy : k.Skrot)} — {k.Miasto}",
                    Tag = k,
                    FontSize = 11,
                    ToolTip = $"{k.NazwaFirmy}\n{k.Ulica}, {k.KodPocztowy} {k.Miasto}\nHandlowiec: {k.Handlowiec}\n{gps}"
                };
                if (!k.MaWspolrzedne)
                    item.Foreground = System.Windows.Media.Brushes.IndianRed;
                lstKlienci.Items.Add(item);
            }

            if (_filtrKlienci.Count > lista.Count)
                txtListaInfo.Text = $"{lista.Count}/{_filtrKlienci.Count}";
            else
                txtListaInfo.Text = $"{lista.Count}";

            UpdateHandlowiecStats();
        }

        private void UpdateHandlowiecStats()
        {
            if (cmbHandlowiec.SelectedItem is ComboBoxItem item && item.Content?.ToString() is string nazwa && nazwa != "Wszyscy")
            {
                int total = _filtrKlienci.Count;
                int zGps = _filtrKlienci.Count(k => k.MaWspolrzedne);
                int bezGps = total - zGps;
                txtHandlowiecStats.Text = bezGps > 0
                    ? $"📊 {total} klientów · {zGps} ✅ · {bezGps} ❌ wymaga GPS"
                    : $"📊 {total} klientów · wszyscy z GPS ✅";
            }
            else
            {
                txtHandlowiecStats.Text = "";
            }
        }

        private void UpdateStatistics()
        {
            int zWspolrzednymi = _filtrKlienci.Count(k => k.MaWspolrzedne);
            int bezWspolrzednych = _filtrKlienci.Count(k => !k.MaWspolrzedne);
            txtStatKlienci.Text = $"Klientów ogółem: {_wszyscyKlienci.Count}";
            txtStatMapa.Text = $"Na mapie: {zWspolrzednymi} pinezek, {bezWspolrzednych} bez współrzędnych";
            txtStatObroty.Text = $"Widocznych: {_filtrKlienci.Count} | Obroty: {_filtrKlienci.Sum(k => k.ObrotyMiesieczne):N0} zł/mies";
        }

        private void TxtSzukajKlient_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateClientList();
        }

        private void Filtr_Changed(object sender, RoutedEventArgs e) => ApplyFilters();
        private void CmbHandlowiec_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private async void LstKlienci_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_mapReady) return;
            if (lstKlienci.SelectedItem is ListBoxItem item && item.Tag is KlientMapa k && k.MaWspolrzedne)
            {
                try { await mapHost.CoreWebView2.ExecuteScriptAsync($"focusClient({k.Id})"); } catch { }
            }
        }

        private async void LstKlienci_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstKlienci.SelectedItem is not ListBoxItem item) return;
            if (item.Tag is not KlientMapa k) return;
            await OtworzEdytorGpsAsync(k);
        }

        private async Task OtworzEdytorGpsAsync(KlientMapa k)
        {
            var dlg = new EdytorGpsKlientaWindow(k, _geoService) { Owner = this };
            bool? wynik = dlg.ShowDialog();
            if (wynik != true) return;

            if (dlg.Zapisano)
            {
                k.Latitude = dlg.NowaLatitude;
                k.Longitude = dlg.NowaLongitude;
            }
            else if (dlg.Usunieto)
            {
                k.Latitude = null;
                k.Longitude = null;
            }

            ApplyFilters();

            if (k.MaWspolrzedne && _mapReady)
            {
                try { await mapHost.CoreWebView2.ExecuteScriptAsync($"focusClient({k.Id})"); } catch { }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Auto-geokodowanie (batch Nominatim)
        // ══════════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════════
        // HTML — Leaflet z markercluster + popupami klientów
        // ══════════════════════════════════════════════════════════════════

        private string GenerateMapHtml()
        {
            return @"<!DOCTYPE html><html lang=""pl""><head>
<meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" integrity=""sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY="" crossorigin=""""/>
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css""/>
<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"" integrity=""sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="" crossorigin=""""></script>
<script src=""https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js""></script>
<style>
*{margin:0;padding:0;box-sizing:border-box}
html,body,#map{width:100%;height:100%;background:#f0f0f0;font-family:'Segoe UI',system-ui,sans-serif}
.cm{position:relative;filter:drop-shadow(0 2px 4px rgba(0,0,0,.35))}
.cm-pin{
    width:28px;height:28px;border-radius:50%;
    color:#fff;font-weight:800;font-size:12px;
    display:flex;align-items:center;justify-content:center;
    border:2.5px solid #fff;cursor:pointer;
    transition:transform .15s;
}
.cm-pin:hover{transform:scale(1.15)}
.alert-dot{
    position:absolute;top:-3px;right:-3px;
    width:11px;height:11px;border-radius:50%;
    background:#dc2626;border:2px solid #fff;
    animation:pulse 1.4s ease infinite;
    box-shadow:0 0 6px rgba(220,38,38,.7);
}
@keyframes pulse{0%,100%{transform:scale(1);box-shadow:0 0 0 0 rgba(220,38,38,.7)}50%{transform:scale(1.25);box-shadow:0 0 0 6px rgba(220,38,38,0)}}

.cp{min-width:280px;font-family:'Segoe UI',sans-serif}
.cp-head{padding:12px 14px;color:#fff;border-radius:10px 10px 0 0;margin:-14px -20px 10px}
.cp-name{font-size:15px;font-weight:700;line-height:1.25}
.cp-sub{font-size:11px;opacity:.88;margin-top:3px}
.cp-row{display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid #f0f0f5;font-size:12px;gap:10px}
.cp-row:last-of-type{border:none}
.cp-row .k{color:#78909c;flex-shrink:0}
.cp-row .v{font-weight:600;color:#263238;text-align:right;word-break:break-word}
.cp-gps{font-family:Consolas,monospace;font-size:11px;color:#475569}
.cp-actions{display:flex;gap:6px;margin-top:10px}
.cp-btn{flex:1;padding:9px 8px;text-align:center;border:none;border-radius:6px;cursor:pointer;font-size:12px;font-weight:600;font-family:inherit}
.cp-btn-edit{background:#10b981;color:#fff}.cp-btn-edit:hover{background:#059669}
.cp-btn-gmap{background:#1d4ed8;color:#fff}.cp-btn-gmap:hover{background:#1e3a8a}
.cp-alert{background:#fef2f2;color:#b91c1c;padding:6px 10px;border-radius:6px;font-size:11px;font-weight:600;margin-bottom:8px;border:1px solid #fecaca}

.leaflet-popup-content-wrapper{border-radius:12px!important;box-shadow:0 6px 24px rgba(0,0,0,.18)!important}
.leaflet-popup-content{margin:14px 20px!important;width:auto!important}
.leaflet-popup-tip{box-shadow:0 4px 8px rgba(0,0,0,.1)!important}

/* Cluster: niebieskie kółko z liczbą */
.marker-cluster{background:none!important;border:none!important}
.marker-cluster div{
    background:linear-gradient(135deg,#1976d2,#1565c0)!important;
    color:#fff!important;border:3px solid #fff!important;
    border-radius:50%!important;font-weight:800!important;
    display:flex!important;align-items:center!important;justify-content:center!important;
    box-shadow:0 3px 12px rgba(0,0,0,.4)!important;
    font-family:'Segoe UI',sans-serif!important;
}
.marker-cluster-small div{width:34px!important;height:34px!important;font-size:13px!important}
.marker-cluster-medium div{width:40px!important;height:40px!important;font-size:14px!important;background:linear-gradient(135deg,#1565c0,#0d47a1)!important}
.marker-cluster-large div{width:48px!important;height:48px!important;font-size:16px!important;background:linear-gradient(135deg,#0d47a1,#01579b)!important}

/* Legenda — prawy dolny róg */
.map-legend{background:rgba(255,255,255,.97);border-radius:8px;box-shadow:0 2px 12px rgba(0,0,0,.18);font-size:11px;color:#37474f;border:1px solid #e0e3ea;padding:8px 12px}
.map-legend .leg-title{font-weight:700;margin-bottom:6px;color:#1a237e}
.map-legend .leg-row{display:flex;align-items:center;gap:7px;padding:2px 0;white-space:nowrap}
.map-legend .leg-dot{width:13px;height:13px;border-radius:50%;border:2px solid #fff;box-shadow:0 0 2px rgba(0,0,0,.3);flex-shrink:0}

#info-empty{position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);z-index:1000;background:rgba(255,255,255,.95);padding:24px 36px;border-radius:12px;box-shadow:0 4px 20px rgba(0,0,0,.15);font-size:14px;color:#546e7a;display:none}
</style></head><body>
<div id=""map""></div>
<div id=""info-empty"">Brak klientów z GPS w wybranym filtrze.</div>
<script>
var map = L.map('map', {zoomControl:true, zoomAnimation:true});
map.fitBounds([[49.0, 14.1], [54.9, 24.2]], {padding:[20,20]});

var osm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap', maxZoom: 19
}).addTo(map);
var dark = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '© CARTO', maxZoom: 19
});
var sat = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
    attribution: '© Esri', maxZoom: 19
});
L.control.layers({'Mapa':osm, 'Jasna':dark, 'Satelita':sat}, null, {position:'topright'}).addTo(map);
L.control.scale({imperial:false, position:'bottomleft'}).addTo(map);

// Legenda
var legend = L.control({position:'bottomright'});
legend.onAdd = function(){
    var d = L.DomUtil.create('div', 'map-legend');
    d.innerHTML = '<div class=""leg-title"">Kategorie klientów</div>'
        + '<div class=""leg-row""><span class=""leg-dot"" style=""background:#10B981""></span>A — kluczowi</div>'
        + '<div class=""leg-row""><span class=""leg-dot"" style=""background:#3B82F6""></span>B — istotni</div>'
        + '<div class=""leg-row""><span class=""leg-dot"" style=""background:#F59E0B""></span>C — średni</div>'
        + '<div class=""leg-row""><span class=""leg-dot"" style=""background:#6B7280""></span>D — pozostali</div>'
        + '<div class=""leg-row""><span class=""leg-dot"" style=""background:#EF4444""></span>brak kategorii</div>';
    L.DomEvent.disableClickPropagation(d);
    return d;
};
legend.addTo(map);

var cluster = L.markerClusterGroup({
    maxClusterRadius: 45,
    showCoverageOnHover: false,
    spiderfyOnMaxZoom: true,
    zoomToBoundsOnClick: true,
    chunkedLoading: true
});
map.addLayer(cluster);

var markers = {};

function colorFor(kat){
    var c = {A:'#10B981', B:'#3B82F6', C:'#F59E0B', D:'#6B7280'};
    return c[kat] || '#EF4444';
}
function esc(s){
    if (s == null) return '';
    return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;');
}
function formatPLN(n){
    return (n||0).toLocaleString('pl-PL', {maximumFractionDigits:0}) + ' zł';
}

function makeIcon(k){
    var color = colorFor(k.Kategoria);
    var label = k.Kategoria || '?';
    var alertHtml = k.MaAlert ? '<div class=""alert-dot""></div>' : '';
    return L.divIcon({
        className: 'cm',
        html: '<div class=""cm-pin"" style=""background:'+color+'"">'+label+'</div>' + alertHtml,
        iconSize: [28, 28],
        iconAnchor: [14, 14]
    });
}

function popupHtml(k){
    var color = colorFor(k.Kategoria);
    var alertBox = k.MaAlert
        ? '<div class=""cp-alert"">⚠ Alert — należności przekraczają limit kupiecki</div>'
        : '';
    var googleQuery = encodeURIComponent([k.Ulica, k.KodPocztowy, k.Miasto, 'Polska'].filter(Boolean).join(' '));
    var gmapUrl = 'https://www.google.com/maps/search/?api=1&query=' + googleQuery;

    return '<div class=""cp"">'
        + '<div class=""cp-head"" style=""background:'+color+'"">'
        + '<div class=""cp-name"">' + esc(k.NazwaFirmy) + '</div>'
        + '<div class=""cp-sub"">ID ' + k.Id + (k.Skrot ? ' · ' + esc(k.Skrot) : '') + ' · Kat. ' + esc(k.Kategoria || '?') + '</div>'
        + '</div>'
        + alertBox
        + '<div class=""cp-row""><span class=""k"">Adres</span><span class=""v"">' + esc((k.Ulica?k.Ulica+', ':'') + (k.KodPocztowy?k.KodPocztowy+' ':'') + (k.Miasto||'—')) + '</span></div>'
        + '<div class=""cp-row""><span class=""k"">Handlowiec</span><span class=""v"">' + esc(k.Handlowiec || '—') + '</span></div>'
        + '<div class=""cp-row""><span class=""k"">Obroty</span><span class=""v"">' + formatPLN(k.ObrotyMiesieczne) + ' / mies</span></div>'
        + '<div class=""cp-row""><span class=""k"">GPS</span><span class=""v cp-gps"">' + k.Lat.toFixed(6) + ', ' + k.Lng.toFixed(6) + '</span></div>'
        + '<div class=""cp-actions"">'
        + '<button class=""cp-btn cp-btn-edit"" onclick=""editClient(' + k.Id + ')"">✏ Ustaw GPS ręcznie</button>'
        + '<button class=""cp-btn cp-btn-gmap"" onclick=""window.open(\'' + gmapUrl + '\',\'_blank\')"">🌐 Google Maps</button>'
        + '</div>'
        + '</div>';
}

function updateClients(arr){
    cluster.clearLayers();
    markers = {};
    document.getElementById('info-empty').style.display = (arr.length === 0) ? 'block' : 'none';
    if (arr.length === 0) return;
    var batch = [];
    for (var i = 0; i < arr.length; i++) {
        var k = arr[i];
        var m = L.marker([k.Lat, k.Lng], {icon: makeIcon(k)});
        m._cData = k;
        m.bindPopup(popupHtml(k), {maxWidth: 360, autoPan: true, closeOnClick: true});
        markers[k.Id] = m;
        batch.push(m);
    }
    cluster.addLayers(batch);
}

function focusClient(id){
    var m = markers[id];
    if (!m) return;
    cluster.zoomToShowLayer(m, function(){
        m.openPopup();
    });
}

function editClient(id){
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({action: 'edit', id: id}));
    }
}

function centerAll(){
    if (cluster.getLayers().length === 0) return;
    map.fitBounds(cluster.getBounds(), {padding: [40, 40]});
}
</script>
</body></html>";
        }
    }
}
