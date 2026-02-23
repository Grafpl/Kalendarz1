using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.Hodowcy
{
    public partial class HodowcyMapaWindow : Window
    {
        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;

        private readonly string _connectionString;
        private readonly DataTable _dtHodowcy;
        private List<MapHodowca> _allPoints = new();
        private List<MapHodowca> _filteredPoints = new();
        private bool _isWebViewReady;
        private bool _isFilterUpdating;
        private readonly int? _focusHodowcaId;

        public HodowcyMapaWindow(string connectionString, DataTable dtHodowcy, int? focusHodowcaId = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connectionString = connectionString;
            _dtHodowcy = dtHodowcy;
            _focusHodowcaId = focusHodowcaId;
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitFilters();
            try
            {
                txtLoadingStatus.Text = "Pobieranie koordynatów...";
                await LoadDataAsync();

                txtLoadingStatus.Text = "Inicjalizacja WebView2...";
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.WebMessageReceived += (s, args) =>
                {
                    try
                    {
                        var json = args.WebMessageAsJson;
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string action = root.GetProperty("action").GetString();
                        if (action == "select")
                        {
                            int id = root.GetProperty("id").GetInt32();
                            Dispatcher.Invoke(() => ScrollToHodowca(id));
                        }
                    }
                    catch { }
                };
                _isWebViewReady = true;

                txtLoadingStatus.Text = "Generowanie mapy...";
                await Task.Delay(200);

                if (_focusHodowcaId.HasValue)
                {
                    webView.CoreWebView2.NavigationCompleted += async (s2, e2) =>
                    {
                        if (!e2.IsSuccess) return;
                        await Task.Delay(500);
                        try
                        {
                            int fid = _focusHodowcaId.Value;
                            await webView.CoreWebView2.ExecuteScriptAsync($"focusMarker({fid})");
                            Dispatcher.Invoke(() => ScrollToHodowca(fid));
                        }
                        catch { }
                    };
                }

                RenderMap();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji mapy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void InitFilters()
        {
            _isFilterUpdating = true;

            cmbFilterStatus.Items.Clear();
            cmbFilterStatus.Items.Add(new ComboBoxItem { Content = "Wszystkie" });
            var statuses = new[] { "Nowy", "Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt", "Zdaje", "Nie zainteresowany", "Obcy kontrakt" };
            foreach (var s in statuses)
                cmbFilterStatus.Items.Add(new ComboBoxItem { Content = s });
            cmbFilterStatus.SelectedIndex = 0;

            cmbFilterTowar.Items.Clear();
            cmbFilterTowar.Items.Add(new ComboBoxItem { Content = "Wszystkie" });
            if (_dtHodowcy != null)
            {
                var towary = _dtHodowcy.AsEnumerable()
                    .Select(r => r["Towar"]?.ToString())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .OrderBy(t => t);
                foreach (var t in towary)
                    cmbFilterTowar.Items.Add(new ComboBoxItem { Content = t });
            }
            // Domyślnie ustaw "Kurczaki" jeśli istnieje
            int kurczakiIdx = -1;
            for (int i = 0; i < cmbFilterTowar.Items.Count; i++)
            {
                if ((cmbFilterTowar.Items[i] as ComboBoxItem)?.Content?.ToString() == "KURCZAKI")
                { kurczakiIdx = i; break; }
            }
            cmbFilterTowar.SelectedIndex = kurczakiIdx >= 0 ? kurczakiIdx : 0;

            _isFilterUpdating = false;
        }

        private async Task LoadDataAsync()
        {
            if (_dtHodowcy == null) return;

            var geoMap = new Dictionary<string, (double lat, double lng)>();
            await Task.Run(() =>
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand("SELECT Kod, Latitude, Longitude FROM KodyPocztowe WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string kod = reader["Kod"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(kod)) continue;
                    double lat = Convert.ToDouble(reader["Latitude"]);
                    double lng = Convert.ToDouble(reader["Longitude"]);
                    if (Math.Abs(lat) > 0.001 && Math.Abs(lng) > 0.001)
                        geoMap[kod.Replace("-", "")] = (lat, lng);
                }
            });

            _allPoints.Clear();
            foreach (DataRow row in _dtHodowcy.Rows)
            {
                string kodRaw = row["KodPocztowy"]?.ToString()?.Replace("-", "")?.Trim() ?? "";
                if (!geoMap.TryGetValue(kodRaw, out var coords)) continue;

                int id = row["Id"] is int i ? i : Convert.ToInt32(row["Id"]);
                string tel1 = row["Tel1Display"]?.ToString() ?? "";

                _allPoints.Add(new MapHodowca
                {
                    Id = id,
                    Nazwa = row["Dostawca"]?.ToString() ?? "",
                    Miejscowosc = row["Miejscowosc"]?.ToString() ?? "",
                    Ulica = row.Table.Columns.Contains("Ulica") ? (row["Ulica"]?.ToString() ?? "") : "",
                    Towar = row["Towar"]?.ToString() ?? "",
                    Status = row["Status"]?.ToString() ?? "",
                    Telefon = tel1,
                    Lat = coords.lat,
                    Lng = coords.lng
                });
            }
        }

        private void RenderMap()
        {
            _filteredPoints = ApplyFilter(_allPoints);
            UpdateKpis();
            UpdateSidebar();

            string json = SerializePoints(_filteredPoints);
            string html = GenerateMapHtml(json, _filteredPoints.Count);
            string tempPath = Path.Combine(Path.GetTempPath(), "mapa_hodowcow_wv2.html");
            File.WriteAllText(tempPath, html, Encoding.UTF8);
            webView.CoreWebView2.Navigate(tempPath);

            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        private List<MapHodowca> ApplyFilter(List<MapHodowca> source)
        {
            var result = source.AsEnumerable();

            string statusFilter = (cmbFilterStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Wszystkie")
                result = result.Where(h => h.Status == statusFilter);

            string towarFilter = (cmbFilterTowar.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (!string.IsNullOrEmpty(towarFilter) && towarFilter != "Wszystkie")
                result = result.Where(h => h.Towar == towarFilter);

            string search = txtSzukaj.Text?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(search))
                result = result.Where(h =>
                    h.Nazwa.ToLowerInvariant().Contains(search) ||
                    h.Miejscowosc.ToLowerInvariant().Contains(search) ||
                    (h.Ulica ?? "").ToLowerInvariant().Contains(search) ||
                    h.Telefon.Contains(search));

            return result.ToList();
        }

        private void UpdateKpis()
        {
            txtNaMapie.Text = _filteredPoints.Count.ToString();
            txtZdaje.Text = _filteredPoints.Count(p => p.Status == "Zdaje").ToString();
            txtNieZaint.Text = _filteredPoints.Count(p => p.Status == "Nie zainteresowany").ToString();
        }

        private void UpdateSidebar()
        {
            panelHodowcy.Children.Clear();
            var toShow = _filteredPoints.Take(200).ToList();

            foreach (var h in toShow)
            {
                var card = CreateHodowcaCard(h);
                panelHodowcy.Children.Add(card);
            }

            txtLiczbaLista.Text = _filteredPoints.Count > 200
                ? $"{_filteredPoints.Count} hodowców (pokazano 200)"
                : $"{_filteredPoints.Count} hodowców";
        }

        private Border CreateHodowcaCard(MapHodowca h)
        {
            var card = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(4, 2, 4, 2),
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1),
                Tag = h.Id
            };

            card.MouseLeftButtonUp += HodowcaCard_Click;
            card.MouseEnter += (s, e) => ((Border)s).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            card.MouseLeave += (s, e) => ((Border)s).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Status dot
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusColor(h.Status))),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 10, 0)
            };
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            // Info stack
            var stack = new StackPanel();
            Grid.SetColumn(stack, 1);

            stack.Children.Add(new TextBlock
            {
                Text = h.Nazwa,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(new TextBlock
            {
                Text = h.Miejscowosc,
                FontSize = 9,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(0, 2, 0, 0)
            });
            if (!string.IsNullOrWhiteSpace(h.Ulica))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = h.Ulica,
                    FontSize = 9,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                    Margin = new Thickness(0, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            var statusHex = StatusColor(h.Status);
            var statusClr = (Color)ColorConverter.ConvertFromString(statusHex);
            var statusBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(48, statusClr.R, statusClr.G, statusClr.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            statusBadge.Child = new TextBlock
            {
                Text = h.Status,
                FontSize = 8,
                Foreground = new SolidColorBrush(statusClr),
                FontWeight = FontWeights.SemiBold
            };
            statusRow.Children.Add(statusBadge);

            if (!string.IsNullOrEmpty(h.Telefon))
            {
                statusRow.Children.Add(new TextBlock
                {
                    Text = h.Telefon,
                    FontSize = 9,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            stack.Children.Add(statusRow);
            grid.Children.Add(stack);
            card.Child = grid;

            return card;
        }

        private async void HodowcaCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int id && _isWebViewReady)
            {
                try
                {
                    await webView.CoreWebView2.ExecuteScriptAsync($"focusMarker({id})");
                }
                catch { }
            }
        }

        private void ScrollToHodowca(int id)
        {
            foreach (var child in panelHodowcy.Children)
            {
                if (child is Border border)
                {
                    if (border.Tag is int cardId && cardId == id)
                    {
                        border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));
                        border.BringIntoView();

                        // Reset after 2s
                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        timer.Tick += (s, ev) =>
                        {
                            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                            timer.Stop();
                        };
                        timer.Start();
                    }
                    else
                    {
                        border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                    }
                }
            }
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isWebViewReady) return;
            ApplyFilterAndRefresh();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isFilterUpdating || !_isWebViewReady) return;
            ApplyFilterAndRefresh();
        }

        private async void ApplyFilterAndRefresh()
        {
            _filteredPoints = ApplyFilter(_allPoints);
            UpdateKpis();
            UpdateSidebar();

            // Update map markers via JS
            try
            {
                string json = SerializePoints(_filteredPoints);
                string escapedJson = json.Replace("\\", "\\\\").Replace("'", "\\'");
                await webView.CoreWebView2.ExecuteScriptAsync($"updateMarkers('{escapedJson}')");
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Helpers

        private static string StatusColor(string status)
        {
            return (status ?? "").ToLowerInvariant() switch
            {
                "nowy" => "#6b7280",
                "do zadzwonienia" => "#f59e0b",
                "próba kontaktu" => "#f97316",
                "nawiązano kontakt" => "#22c55e",
                "zdaje" => "#10b981",
                "nie zainteresowany" => "#ef4444",
                "obcy kontrakt" => "#06b6d4",
                _ => "#6b7280"
            };
        }

        private static string SerializePoints(List<MapHodowca> points)
        {
            var data = points.Select(p => new
            {
                id = p.Id,
                n = p.Nazwa,
                m = p.Miejscowosc,
                u = p.Ulica,
                t = p.Towar,
                s = p.Status,
                p = p.Telefon,
                lat = p.Lat,
                lng = p.Lng
            });
            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private string GenerateMapHtml(string json, int count)
        {
            string bazaLatStr = BazaLat.ToString(CultureInfo.InvariantCulture);
            string bazaLngStr = BazaLng.ToString(CultureInfo.InvariantCulture);

            return $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'/>
<title>Mapa Hodowców ({count})</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  html, body {{ height:100%; font-family:'Segoe UI',sans-serif; background:#f8fafc; }}
  #map {{ height:100%; }}
  .badge {{ display:inline-block; padding:2px 8px; border-radius:4px; font-size:11px; font-weight:700; color:#fff; }}
  .popup-name {{ font-size:14px; font-weight:700; margin-bottom:4px; color:#1e293b; }}
  .popup-row {{ font-size:12px; color:#555; margin:2px 0; }}
  .popup-phone {{ font-size:13px; color:#b45309; font-weight:700; margin-top:4px; }}
  .popup-route-btn {{ display:inline-block; margin-top:6px; padding:4px 12px; background:#F59E0B; color:#1e293b; font-size:11px; font-weight:700; border:none; border-radius:6px; cursor:pointer; text-decoration:none; }}
  .popup-route-btn:hover {{ background:#D97706; }}
  .legend {{ position:fixed; bottom:20px; left:20px; background:#1e293b; border-radius:10px; padding:12px 16px; box-shadow:0 2px 12px rgba(0,0,0,.5); z-index:9999; font-size:12px; color:#e2e8f0; }}
  .legend-title {{ font-weight:700; margin-bottom:6px; font-size:13px; color:#fde68a; }}
  .legend-item {{ display:flex; align-items:center; margin:3px 0; }}
  .legend-dot {{ width:12px; height:12px; border-radius:50%; margin-right:8px; border:2px solid #334155; box-shadow:0 0 3px rgba(0,0,0,.3); }}
  .leaflet-popup-content-wrapper {{ border-radius:10px !important; }}
  .route-info {{ position:fixed; bottom:20px; right:20px; background:#1e293b; border-radius:10px; padding:14px 18px; box-shadow:0 4px 20px rgba(0,0,0,.6); z-index:9999; font-size:12px; color:#e2e8f0; min-width:220px; display:none; border:1px solid #334155; }}
  .route-info-title {{ font-weight:700; font-size:13px; color:#fde68a; margin-bottom:6px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width:240px; }}
  .route-info-row {{ margin:3px 0; color:#94a3b8; }}
  .route-info-row b {{ color:#e2e8f0; }}
  .route-info-close {{ position:absolute; top:6px; right:10px; cursor:pointer; color:#94a3b8; font-size:16px; font-weight:700; line-height:1; }}
  .route-info-close:hover {{ color:#ef4444; }}
</style>
</head><body>
<div id='map'></div>
<div class='legend'>
  <div class='legend-title'>Status hodowcy</div>
  <div class='legend-item'><div class='legend-dot' style='background:#6b7280'></div>Nowy</div>
  <div class='legend-item'><div class='legend-dot' style='background:#f59e0b'></div>Do zadzwonienia</div>
  <div class='legend-item'><div class='legend-dot' style='background:#f97316'></div>Próba kontaktu</div>
  <div class='legend-item'><div class='legend-dot' style='background:#22c55e'></div>Nawiązano kontakt</div>
  <div class='legend-item'><div class='legend-dot' style='background:#10b981'></div>Zdaje</div>
  <div class='legend-item'><div class='legend-dot' style='background:#ef4444'></div>Nie zainteresowany</div>
  <div class='legend-item'><div class='legend-dot' style='background:#06b6d4'></div>Obcy kontrakt</div>
</div>
<div id='routeInfo' class='route-info'>
  <span class='route-info-close' onclick='clearRoute()'>✕</span>
  <div id='routeTitle' class='route-info-title'></div>
  <div id='routeDistance' class='route-info-row'></div>
  <div id='routeDuration' class='route-info-row'></div>
</div>
<script>
var BAZA_LAT = {bazaLatStr};
var BAZA_LNG = {bazaLngStr};

var map = L.map('map').setView([52.0,19.5],6);
L.tileLayer('https://mt1.google.com/vt/lyrs=m&hl=pl&x={{x}}&y={{y}}&z={{z}}',{{
  attribution:'Map data &copy; Google',
  maxZoom:20
}}).addTo(map);

// --- Base marker (not in cluster) ---
var bazaIcon = L.divIcon({{
  className:'',
  html:'<div style=""width:24px;height:24px;border-radius:50%;background:#F59E0B;border:3px solid #fff;box-shadow:0 0 12px rgba(245,158,11,.7);display:flex;align-items:center;justify-content:center;font-size:13px;"">🏠</div>',
  iconSize:[24,24], iconAnchor:[12,12]
}});
L.marker([BAZA_LAT, BAZA_LNG], {{icon:bazaIcon, zIndexOffset:1000}})
  .addTo(map)
  .bindPopup('<div class=""popup-name"" style=""color:#B45309"">🏠 BAZA</div><div class=""popup-row"" style=""font-weight:700"">Ubojnia Drobiu Piórkowscy</div><div class=""popup-row"">Koziółki 40, 95-061 Dmosin</div>');

var markers = {{}};
var markerData = {{}};
var markersLayer = L.layerGroup().addTo(map);

// --- Route state ---
var currentRoute = null;

function clearRoute() {{
  if(currentRoute) {{
    map.removeLayer(currentRoute);
    currentRoute = null;
  }}
  document.getElementById('routeInfo').style.display = 'none';
}}

function showRoute(id) {{
  clearRoute();
  var d = markerData[id];
  if(!d) return;

  var url = 'https://router.project-osrm.org/route/v1/driving/'
    + BAZA_LNG+','+BAZA_LAT+';'+d.lng+','+d.lat
    + '?overview=full&geometries=geojson';

  fetch(url)
    .then(function(r) {{ return r.json(); }})
    .then(function(data) {{
      if(!data.routes || data.routes.length===0) return;
      var route = data.routes[0];
      var coords = route.geometry.coordinates.map(function(c) {{ return [c[1],c[0]]; }});

      currentRoute = L.polyline(coords, {{
        color:'#F59E0B', weight:4, opacity:0.8,
        dashArray:'10,8', lineCap:'round'
      }}).addTo(map);

      var distKm = (route.distance / 1000).toFixed(1);
      var durMin = Math.round(route.duration / 60);
      var hours = Math.floor(durMin / 60);
      var mins = durMin % 60;
      var durStr = hours > 0 ? hours+'h '+mins+'min' : mins+' min';

      document.getElementById('routeTitle').textContent = d.n;
      document.getElementById('routeDistance').innerHTML = '📏 Dystans: <b>'+distKm+' km</b>';
      document.getElementById('routeDuration').innerHTML = '⏱ Czas jazdy: <b>'+durStr+'</b>';
      document.getElementById('routeInfo').style.display = 'block';
    }})
    .catch(function(err) {{ console.error('Route error:', err); }});
}}

function statusColor(s) {{
  s = (s||'').toLowerCase();
  if(s==='nowy') return '#6b7280';
  if(s==='do zadzwonienia') return '#f59e0b';
  if(s==='próba kontaktu') return '#f97316';
  if(s==='nawiązano kontakt') return '#22c55e';
  if(s==='zdaje') return '#10b981';
  if(s==='nie zainteresowany') return '#ef4444';
  if(s==='obcy kontrakt') return '#06b6d4';
  return '#6b7280';
}}

function addMarkers(data) {{
  markersLayer.clearLayers();
  markers = {{}};
  markerData = {{}};
  data.forEach(function(d) {{
    markerData[d.id] = d;
    var c = statusColor(d.s);
    var icon = L.divIcon({{
      className:'',
      html:'<div style=""width:14px;height:14px;border-radius:50%;background:'+c+';border:2px solid #fff;box-shadow:0 0 6px rgba(0,0,0,.5);""></div>',
      iconSize:[14,14], iconAnchor:[7,7]
    }});
    var popup = '<div class=""popup-name"">'+d.n+'</div>'
      +'<div class=""popup-row""><span class=""badge"" style=""background:'+c+'"">'+d.s+'</span></div>'
      +(d.u?'<div class=""popup-row"">🏠 '+d.u+'</div>':'')
      +'<div class=""popup-row"">📍 '+d.m+'</div>'
      +'<div class=""popup-row"">🐔 '+d.t+'</div>'
      +(d.p?'<div class=""popup-phone"">📞 '+d.p+'</div>':'')
      +'<div><button class=""popup-route-btn"" onclick=""showRoute('+d.id+')"">🚗 Pokaż trasę</button></div>';
    var marker = L.marker([d.lat,d.lng],{{icon:icon}}).bindPopup(popup);
    marker.on('click', function() {{
      window.chrome.webview.postMessage(JSON.stringify({{action:'select', id:d.id}}));
    }});
    markers[d.id] = marker;
    markersLayer.addLayer(marker);
  }});
  if(data.length>0) {{
    var bounds = L.latLngBounds(data.map(function(d){{ return [d.lat,d.lng]; }}));
    if(bounds.isValid()) map.fitBounds(bounds,{{padding:[30,30]}});
  }}
}}

function focusMarker(id) {{
  var m = markers[id];
  if(m) {{
    clearRoute();
    map.flyTo(m.getLatLng(), 14, {{duration:0.5}});
    setTimeout(function() {{
      m.openPopup();
      showRoute(id);
    }}, 600);
  }}
}}

function updateMarkers(jsonStr) {{
  try {{
    clearRoute();
    var data = JSON.parse(jsonStr);
    addMarkers(data);
  }} catch(e) {{ console.error(e); }}
}}

var initialData = {json};
addMarkers(initialData);
</script>
</body></html>";
        }

        #endregion

        #region Model

        private class MapHodowca
        {
            public int Id { get; set; }
            public string Nazwa { get; set; }
            public string Miejscowosc { get; set; }
            public string Ulica { get; set; }
            public string Towar { get; set; }
            public string Status { get; set; }
            public string Telefon { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
        }

        #endregion
    }
}
