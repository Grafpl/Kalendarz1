using Microsoft.Data.SqlClient;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class MapaCRMWindow : Window
    {
        private readonly string connectionString;
        private readonly string operatorID;
        private DataTable dtKontakty;
        private List<MapKontakt> kontaktyNaMapie = new List<MapKontakt>();
        private bool isLoading = false;
        private bool isWebViewReady = false;

        // WSPÓŁRZĘDNE BAZY: Koziołki 40, 95-061 Dmosin
        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;

        private static readonly HttpClient http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public MapaCRMWindow(string connString, string opID)
        {
            InitializeComponent();
            connectionString = connString;
            operatorID = opID;
            this.WindowState = WindowState.Maximized;
            Loaded += MapaCRMWindow_Loaded;
        }

        private async void MapaCRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InicjalizujFiltry();
            try
            {
                txtLoadingStatus.Text = "Inicjalizacja mapy...";
                await webView.EnsureCoreWebView2Async();
                isWebViewReady = true;
                await Task.Delay(500);
                await OdswiezMapeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji mapy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void InicjalizujFiltry()
        {
            cmbWojewodztwo.Items.Clear();
            cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = "Wszystkie" });
            var wojewodztwa = new[] { "dolnośląskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
                "łódzkie", "małopolskie", "mazowieckie", "opolskie", "podkarpackie", "podlaskie",
                "pomorskie", "śląskie", "świętokrzyskie", "warmińsko-mazurskie", "wielkopolskie", "zachodniopomorskie" };
            foreach (var woj in wojewodztwa)
                cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = woj });
            cmbWojewodztwo.SelectedIndex = 0;

            cmbBranza.Items.Clear();
            cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
            cmbBranza.SelectedIndex = 0;
        }

        private async Task OdswiezMapeAsync()
        {
            if (isLoading || !isWebViewReady) return;
            isLoading = true;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                txtLoadingStatus.Text = "Pobieranie bazy danych...";

                await Task.Run(() => WczytajDaneZBazy());
                WypelnijFiltrBranz();

                txtLoadingStatus.Text = "Obliczanie dystansów...";
                var przefiltrowane = await FiltrujKontaktyAsync();

                txtLoadingStatus.Text = $"Generowanie mapy ({przefiltrowane.Count} punktów)...";
                var htmlContent = GenerujHtmlMapy(przefiltrowane);

                var tempPath = Path.Combine(Path.GetTempPath(), "mapa_crm_trasa.html");
                File.WriteAllText(tempPath, htmlContent);
                webView.CoreWebView2.Navigate(tempPath);

                kontaktyNaMapie = przefiltrowane;

                // Sortowanie po odległości (najbliżsi na górze)
                var listaDoWyswietlenia = kontaktyNaMapie.OrderBy(k => k.DystansKm).Take(100).ToList();
                listaKontaktow.ItemsSource = listaDoWyswietlenia;

                txtLiczbaKontaktow.Text = $"{kontaktyNaMapie.Count} kontaktów";
                AktualizujStatystyki(przefiltrowane);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odświeżania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void WczytajDaneZBazy()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var sql = @"
                    SELECT
                        o.ID,
                        o.Nazwa as NAZWA,
                        o.KOD,
                        o.MIASTO,
                        o.ULICA,
                        o.Telefon_K as TELEFON_K,
                        o.Email,
                        o.Wojewodztwo,
                        o.PKD_Opis,
                        ISNULL(o.Status, 'Do zadzwonienia') as Status,
                        CASE WHEN pb.PKD_Opis IS NOT NULL THEN 1 ELSE 0 END as CzyPriorytetowa,
                        kp.Latitude,
                        kp.Longitude
                    FROM OdbiorcyCRM o
                    LEFT JOIN PriorytetoweBranzeCRM pb ON o.PKD_Opis = pb.PKD_Opis
                    INNER JOIN KodyPocztowe kp ON REPLACE(o.KOD, '-', '') = REPLACE(kp.Kod, '-', '')
                    WHERE ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')
                      AND kp.Latitude IS NOT NULL 
                      AND kp.Longitude IS NOT NULL";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    var adapter = new SqlDataAdapter(cmd);
                    dtKontakty = new DataTable();
                    adapter.Fill(dtKontakty);
                }
            }
        }

        private void WypelnijFiltrBranz()
        {
            if (dtKontakty == null) return;

            Dispatcher.Invoke(() =>
            {
                var branze = dtKontakty.AsEnumerable()
                    .Select(r => r.Field<string>("PKD_Opis"))
                    .Where(b => !string.IsNullOrEmpty(b))
                    .Distinct()
                    .OrderBy(b => b)
                    .ToList();

                var currentIndex = cmbBranza.SelectedIndex;
                cmbBranza.Items.Clear();
                cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
                foreach (var branza in branze)
                    cmbBranza.Items.Add(new ComboBoxItem { Content = branza, Tag = branza });

                if (currentIndex > 0 && currentIndex < cmbBranza.Items.Count)
                    cmbBranza.SelectedIndex = currentIndex;
                else
                    cmbBranza.SelectedIndex = 0;
            });
        }

        // Funkcja obliczająca odległość w linii prostej (Haversine Formula)
        private double ObliczDystans(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Promień ziemi w km
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Dystans w km
            return Math.Round(d, 1);
        }

        private double Deg2Rad(double deg)
        {
            return deg * (Math.PI / 180);
        }

        private async Task<List<MapKontakt>> FiltrujKontaktyAsync()
        {
            if (dtKontakty == null) return new List<MapKontakt>();

            var wynik = new List<MapKontakt>();
            string filtrWoj = "", filtrBranza = "", szukanaFraza = "";
            bool tylkoPriorytetowe = false;
            var statusyDoPokazania = new List<string>();

            await Dispatcher.InvokeAsync(() =>
            {
                if (cmbWojewodztwo.SelectedIndex > 0)
                    filtrWoj = (cmbWojewodztwo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "";

                if (cmbBranza.SelectedIndex > 0)
                {
                    var item = cmbBranza.SelectedItem as ComboBoxItem;
                    filtrBranza = item?.Tag?.ToString() ?? item?.Content?.ToString() ?? "";
                }

                szukanaFraza = txtSzukaj.Text?.Trim().ToLower() ?? "";
                tylkoPriorytetowe = chkTylkoPriorytetowe.IsChecked == true;

                if (chkDoZadzwonienia.IsChecked == true) statusyDoPokazania.Add("Do zadzwonienia");
                if (chkProba.IsChecked == true) statusyDoPokazania.Add("Próba kontaktu");
                if (chkNawiazano.IsChecked == true) statusyDoPokazania.Add("Nawiązano kontakt");
                if (chkZgoda.IsChecked == true) statusyDoPokazania.Add("Zgoda na dalszy kontakt");
                if (chkOferta.IsChecked == true) statusyDoPokazania.Add("Do wysłania oferta");
                if (chkNieZainteresowany.IsChecked == true) statusyDoPokazania.Add("Nie zainteresowany");
            });

            foreach (DataRow row in dtKontakty.Rows)
            {
                var status = row["Status"]?.ToString() ?? "Do zadzwonienia";
                if (status == "Nowy" || string.IsNullOrWhiteSpace(status)) status = "Do zadzwonienia";

                if (!statusyDoPokazania.Contains(status)) continue;

                var woj = row["Wojewodztwo"]?.ToString()?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(filtrWoj) && !woj.Contains(filtrWoj)) continue;

                var branza = row["PKD_Opis"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(filtrBranza) && branza != filtrBranza) continue;

                var czyPriorytetowa = Convert.ToInt32(row["CzyPriorytetowa"] ?? 0) == 1;
                if (tylkoPriorytetowe && !czyPriorytetowa) continue;

                if (!string.IsNullOrEmpty(szukanaFraza))
                {
                    string nazwa = row["NAZWA"]?.ToString()?.ToLower() ?? "";
                    string miasto = row["MIASTO"]?.ToString()?.ToLower() ?? "";
                    string telefon = row["TELEFON_K"]?.ToString()?.ToLower() ?? "";
                    string ulica = row["ULICA"]?.ToString()?.ToLower() ?? "";

                    if (!nazwa.Contains(szukanaFraza) &&
                        !miasto.Contains(szukanaFraza) &&
                        !telefon.Contains(szukanaFraza) &&
                        !ulica.Contains(szukanaFraza))
                        continue;
                }

                if (row["Latitude"] == DBNull.Value || row["Longitude"] == DBNull.Value) continue;

                double lat = Convert.ToDouble(row["Latitude"]);
                double lng = Convert.ToDouble(row["Longitude"]);

                if (Math.Abs(lat) < 0.001 || Math.Abs(lng) < 0.001) continue;

                var kontakt = new MapKontakt
                {
                    ID = Convert.ToInt32(row["ID"]),
                    Nazwa = row["NAZWA"]?.ToString() ?? "",
                    Miasto = row["MIASTO"]?.ToString() ?? "",
                    Ulica = row["ULICA"]?.ToString() ?? "",
                    Telefon = row["TELEFON_K"]?.ToString() ?? "",
                    Email = row["Email"]?.ToString() ?? "",
                    Wojewodztwo = row["Wojewodztwo"]?.ToString() ?? "",
                    Branza = branza,
                    Status = status,
                    CzyPriorytetowa = czyPriorytetowa,
                    Lat = lat,
                    Lng = lng,
                    // Wyliczenie dystansu
                    DystansKm = ObliczDystans(BazaLat, BazaLng, lat, lng)
                };

                UstawKoloryStatusu(kontakt);
                wynik.Add(kontakt);
            }

            return wynik;
        }

        private void UstawKoloryStatusu(MapKontakt kontakt)
        {
            var kolory = new Dictionary<string, (string hex, string bg, string txt)>
            {
                ["Do zadzwonienia"] = ("#64748B", "#F1F5F9", "#475569"),
                ["Próba kontaktu"] = ("#F97316", "#FFEDD5", "#9A3412"),
                ["Nawiązano kontakt"] = ("#22C55E", "#DCFCE7", "#166534"),
                ["Zgoda na dalszy kontakt"] = ("#14B8A6", "#CCFBF1", "#0D9488"),
                ["Do wysłania oferta"] = ("#0891B2", "#CFFAFE", "#155E75"),
                ["Nie zainteresowany"] = ("#EF4444", "#FEE2E2", "#991B1B")
            };

            if (kolory.TryGetValue(kontakt.Status, out var k))
            {
                kontakt.KolorHex = k.hex;
                kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString(k.hex));
                kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString(k.bg));
                kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString(k.txt));
            }
            else
            {
                kontakt.KolorHex = "#9CA3AF";
                kontakt.KolorStatusu = Brushes.Gray;
                kontakt.TloStatusu = Brushes.LightGray;
                kontakt.KolorStatusuTekst = Brushes.DarkGray;
            }
        }

        private void AktualizujStatystyki(List<MapKontakt> kontakty)
        {
            txtNaMapie.Text = kontakty.Count.ToString();
            txtDoZadzwonienia.Text = kontakty.Count(k => k.Status == "Do zadzwonienia").ToString();
            txtKontakt.Text = kontakty.Count(k => k.Status == "Nawiązano kontakt" || k.Status == "Zgoda na dalszy kontakt").ToString();
            txtPriorytet.Text = kontakty.Count(k => k.CzyPriorytetowa).ToString();
        }

        private string GenerujHtmlMapy(List<MapKontakt> kontakty)
        {
            string apiKey = "";
            Dispatcher.Invoke(() => apiKey = txtApiKey.Text?.Trim() ?? "");

            var dataJson = JsonSerializer.Serialize(kontakty.Select(k => new
            {
                k.ID,
                k.Nazwa,
                k.Miasto,
                k.Ulica,
                k.Telefon,
                k.Email,
                k.Status,
                k.Branza,
                k.CzyPriorytetowa,
                k.KolorHex,
                k.Lat,
                k.Lng,
                k.DystansKm // Dodano dystans do JSON
            }), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='utf-8'/>");
            sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'/>");
            sb.AppendLine("<style>");
            sb.AppendLine("html, body, #map { height: 100%; width: 100%; margin: 0; padding: 0; font-family: 'Segoe UI', sans-serif; overflow: hidden; }");
            sb.AppendLine(".gm-style-iw { max-width: 300px !important; font-family: 'Segoe UI', sans-serif; }");
            sb.AppendLine(".p-title { font-weight: 700; font-size: 14px; margin-bottom: 5px; }");
            sb.AppendLine(".p-info { font-size: 12px; margin: 2px 0; }");
            sb.AppendLine(".btn-row { display:flex; gap:5px; margin-top:8px; }");
            sb.AppendLine(".p-btn { flex:1; text-align:center; padding: 6px 4px; color: white; text-decoration: none; border-radius: 4px; margin-top: 4px; font-size: 11px; border:none; cursor:pointer; }");
            sb.AppendLine(".btn-add { background: #0F172A; } .btn-add:hover { background: #1E293B; }");
            sb.AppendLine("#route-panel { position: absolute; right: 0; top: 0; bottom: 0; width: 320px; background: white; box-shadow: -2px 0 10px rgba(0,0,0,0.1); z-index: 1000; transform: translateX(320px); transition: transform 0.3s ease; display: flex; flex-direction: column; }");
            sb.AppendLine("#route-panel.open { transform: translateX(0); }");
            sb.AppendLine(".rp-header { background: #16A34A; color: white; padding: 15px; font-weight: bold; display: flex; justify-content: space-between; align-items: center; }");
            sb.AppendLine(".rp-content { flex: 1; overflow-y: auto; padding: 10px; }");
            sb.AppendLine(".rp-item { background: #F1F5F9; border-radius: 6px; padding: 10px; margin-bottom: 8px; font-size: 12px; position: relative; border-left: 4px solid #16A34A; }");
            sb.AppendLine(".rp-remove { position: absolute; top: 5px; right: 5px; color: #EF4444; cursor: pointer; font-weight: bold; padding: 4px; }");
            sb.AppendLine(".rp-footer { padding: 15px; border-top: 1px solid #E2E8F0; background: #F8FAF8; }");
            sb.AppendLine(".btn-calc { width: 100%; background: #2563EB; color: white; padding: 12px; border: none; border-radius: 6px; font-weight: bold; cursor: pointer; }");
            sb.AppendLine(".btn-calc:hover { background: #1D4ED8; }");
            sb.AppendLine("#toggle-panel { position: absolute; right: 20px; top: 20px; z-index: 900; background: white; padding: 10px 15px; border-radius: 30px; box-shadow: 0 4px 10px rgba(0,0,0,0.2); cursor: pointer; font-weight: bold; color: #16A34A; }");
            sb.AppendLine("#route-summary { margin-top: 10px; font-size: 13px; font-weight: bold; color: #16A34A; text-align: center; display: none; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div id='map'></div>");
            sb.AppendLine("<div id='toggle-panel' onclick='togglePanel()'>📋 Twoja trasa (<span id='count-badge'>0</span>)</div>");
            sb.AppendLine("<div id='route-panel'>");
            sb.AppendLine("  <div class='rp-header'><span>PLANOWANIE TRASY</span><span style='cursor:pointer' onclick='togglePanel()'>✕</span></div>");
            sb.AppendLine("  <div class='rp-content' id='route-list'><div style='text-align:center;color:#999;margin-top:20px;'>Brak punktów.<br>Kliknij na mapie klienta i wybierz<br><b>+ Dodaj do trasy</b></div></div>");
            sb.AppendLine("  <div class='rp-footer'>");
            sb.AppendLine("    <button class='btn-calc' onclick='calculateRoute()'>🚀 Wyznacz optymalną trasę</button>");
            sb.AppendLine("    <div id='route-summary'></div>");
            sb.AppendLine("    <div style='text-align:center; margin-top:10px;'><a href='#' onclick='clearRoute()' style='color:#666;font-size:11px;'>Wyczyść trasę</a></div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<script>");
            sb.Append("var data = ");
            sb.Append(dataJson);
            sb.AppendLine(";");
            sb.AppendLine("var map, markers = [], infoWindow, directionsService, directionsRenderer;");
            sb.AppendLine("var routePoints = [];");
            // BAZA: KOZIOŁKI 40, DMOSIN
            sb.AppendLine("var startPos = { lat: 51.907335, lng: 19.678605 };");

            sb.AppendLine("function initMap() {");
            sb.AppendLine("  var centerPos = data.length > 0 ? { lat: data[0].Lat, lng: data[0].Lng } : startPos;");
            sb.AppendLine("  map = new google.maps.Map(document.getElementById('map'), { center: centerPos, zoom: 6, fullscreenControl: true, streetViewControl: false, mapTypeControl: false });");
            sb.AppendLine("  infoWindow = new google.maps.InfoWindow();");
            sb.AppendLine("  directionsService = new google.maps.DirectionsService();");
            sb.AppendLine("  directionsRenderer = new google.maps.DirectionsRenderer({ map: map, suppressMarkers: false });");
            sb.AppendLine("  new google.maps.Marker({ position: startPos, map: map, label: '🏠', title: 'BAZA: Koziołki 40' });");
            sb.AppendLine("  var bounds = new google.maps.LatLngBounds();");
            sb.AppendLine("  bounds.extend(startPos);");
            sb.AppendLine("  for (var i = 0; i < data.length; i++) {");
            sb.AppendLine("    var p = data[i];");
            sb.AppendLine("    var pos = { lat: p.Lat, lng: p.Lng };");
            sb.AppendLine("    bounds.extend(pos);");
            sb.AppendLine("    var sz = p.CzyPriorytetowa ? 14 : 10;");
            sb.Append("    var svgIcon = { url: 'data:image/svg+xml;charset=UTF-8,' + encodeURIComponent('<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"' + sz + '\" height=\"' + sz + '\"><circle cx=\"' + (sz/2) + '\" cy=\"' + (sz/2) + '\" r=\"' + ((sz/2)-1) + '\" fill=\"' + p.KolorHex + '\" stroke=\"#fff\" stroke-width=\"1\"/></svg>'), scaledSize: new google.maps.Size(sz, sz), anchor: new google.maps.Point(sz/2, sz/2) };");
            sb.AppendLine("    var marker = new google.maps.Marker({ position: pos, map: map, icon: svgIcon, title: p.Nazwa });");
            sb.AppendLine("    marker.kontakt = p;");
            sb.AppendLine("    marker.addListener('click', function() {");
            sb.AppendLine("      var k = this.kontakt;");
            // WYŚWIETLANIE ODLEGŁOŚCI W DYMKU
            sb.AppendLine("      var content = '<div class=\"p-title\">' + k.Nazwa + '</div>' +");
            sb.AppendLine("                    '<div class=\"p-info\">📍 ' + k.Miasto + ' (' + k.DystansKm + ' km)</div>' +");
            sb.AppendLine("                    '<div class=\"p-info\">📞 ' + k.Telefon + '</div>' +");
            sb.AppendLine("                    '<div class=\"btn-row\">' +");
            sb.AppendLine("                    '<button class=\"p-btn btn-add\" onclick=\"addToRoute(' + k.ID + ')\">➕ Dodaj do trasy</button>' +");
            sb.AppendLine("                    '<a class=\"p-btn\" style=\"background:#16A34A\" href=\"tel:' + k.Telefon + '\">📞 Zadzwoń</a>' +");
            sb.AppendLine("                    '</div>';");
            sb.AppendLine("      infoWindow.setContent(content);");
            sb.AppendLine("      infoWindow.open(map, this);");
            sb.AppendLine("    });");
            sb.AppendLine("  }");
            sb.AppendLine("  if (data.length > 0) map.fitBounds(bounds);");
            sb.AppendLine("  var script = document.createElement('script');");
            sb.AppendLine("  script.src = 'https://unpkg.com/@googlemaps/markerclusterer@2.5.3/dist/index.min.js';");
            sb.AppendLine("  script.onload = function() { try { new markerClusterer.MarkerClusterer({ map: map, markers: markerObjects }); } catch(e){} };");
            sb.AppendLine("  document.head.appendChild(script);");
            sb.AppendLine("}");
            sb.AppendLine("function addToRoute(id) {");
            sb.AppendLine("  var k = data.find(x => x.ID == id);");
            sb.AppendLine("  if(!k) return;");
            sb.AppendLine("  if(routePoints.find(x => x.ID == id)) { alert('Ten klient jest już na liście!'); return; }");
            sb.AppendLine("  routePoints.push(k);");
            sb.AppendLine("  renderRouteList();");
            sb.AppendLine("  document.getElementById('route-panel').classList.add('open');");
            sb.AppendLine("}");
            sb.AppendLine("function removeFromRoute(id) {");
            sb.AppendLine("  routePoints = routePoints.filter(x => x.ID != id);");
            sb.AppendLine("  renderRouteList();");
            sb.AppendLine("}");
            sb.AppendLine("function renderRouteList() {");
            sb.AppendLine("  var div = document.getElementById('route-list');");
            sb.AppendLine("  document.getElementById('count-badge').innerText = routePoints.length;");
            sb.AppendLine("  if(routePoints.length === 0) { div.innerHTML = '<div style=\"text-align:center;color:#999;margin-top:20px;\">Brak punktów.</div>'; return; }");
            sb.AppendLine("  var html = '';");
            sb.AppendLine("  for(var i=0; i<routePoints.length; i++) {");
            sb.AppendLine("    var p = routePoints[i];");
            sb.AppendLine("    html += '<div class=\"rp-item\">' +");
            sb.AppendLine("            '<div class=\"rp-remove\" onclick=\"removeFromRoute(' + p.ID + ')\">✕</div>' +");
            sb.AppendLine("            '<b>' + (i+1) + '. ' + p.Nazwa + '</b><br>' + p.Miasto + ' (' + p.DystansKm + ' km)</div>';");
            sb.AppendLine("  }");
            sb.AppendLine("  div.innerHTML = html;");
            sb.AppendLine("}");
            sb.AppendLine("function togglePanel() {");
            sb.AppendLine("  document.getElementById('route-panel').classList.toggle('open');");
            sb.AppendLine("}");
            sb.AppendLine("function calculateRoute() {");
            sb.AppendLine("  if(routePoints.length === 0) { alert('Dodaj najpierw punkty do listy!'); return; }");
            sb.AppendLine("  if(routePoints.length > 23) { alert('Limit 23 punktów!'); return; }");
            sb.AppendLine("  var waypts = [];");
            sb.AppendLine("  for (var i = 0; i < routePoints.length; i++) {");
            sb.AppendLine("    waypts.push({ location: { lat: routePoints[i].Lat, lng: routePoints[i].Lng }, stopover: true });");
            sb.AppendLine("  }");
            sb.AppendLine("  var request = {");
            sb.AppendLine("    origin: startPos,");
            sb.AppendLine("    destination: startPos,");
            sb.AppendLine("    waypoints: waypts,");
            sb.AppendLine("    optimizeWaypoints: true,");
            sb.AppendLine("    travelMode: 'DRIVING'");
            sb.AppendLine("  };");
            sb.AppendLine("  directionsService.route(request, function(result, status) {");
            sb.AppendLine("    if (status == 'OK') {");
            sb.AppendLine("      directionsRenderer.setDirections(result);");
            sb.AppendLine("      var dist = 0; var time = 0;");
            sb.AppendLine("      var legs = result.routes[0].legs;");
            sb.AppendLine("      for(var i=0; i<legs.length; i++) { dist += legs[i].distance.value; time += legs[i].duration.value; }");
            sb.AppendLine("      var distKm = (dist/1000).toFixed(1) + ' km';");
            sb.AppendLine("      var timeH = Math.floor(time/3600) + 'h ' + Math.floor((time%3600)/60) + 'm';");
            sb.AppendLine("      var summary = document.getElementById('route-summary');");
            sb.AppendLine("      summary.style.display = 'block';");
            sb.AppendLine("      summary.innerHTML = '🏁 Cała trasa: ' + distKm + ' (' + timeH + ')';");
            sb.AppendLine("    } else {");
            sb.AppendLine("      alert('Błąd trasy: ' + status);");
            sb.AppendLine("    }");
            sb.AppendLine("  });");
            sb.AppendLine("}");
            sb.AppendLine("function clearRoute() {");
            sb.AppendLine("  routePoints = [];");
            sb.AppendLine("  renderRouteList();");
            sb.AppendLine("  directionsRenderer.set('directions', null);");
            sb.AppendLine("  document.getElementById('route-summary').style.display = 'none';");
            sb.AppendLine("}");
            sb.AppendLine("window.setView = function(lat, lng, z) { if(map) { map.setCenter({ lat: lat, lng: lng }); map.setZoom(z||15); } };");
            sb.AppendLine("</script>");

            sb.Append("<script async defer src=\"https://maps.googleapis.com/maps/api/js?key=");
            sb.Append(apiKey);
            sb.AppendLine("&callback=initMap\"></script>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private async void BtnGeokoduj_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            var apiKey = txtApiKey.Text?.Trim();
            if (string.IsNullOrEmpty(apiKey)) { MessageBox.Show("Wpisz klucz API!", "Info", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            isLoading = true;
            loadingOverlay.Visibility = Visibility.Visible;
            btnGeokoduj.IsEnabled = false;

            int sukces = 0, bledy = 0;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    await new SqlCommand("IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KodyPocztowe') CREATE TABLE KodyPocztowe (Kod NVARCHAR(10) PRIMARY KEY, miej NVARCHAR(100), Latitude FLOAT, Longitude FLOAT)", conn).ExecuteNonQueryAsync();

                    await new SqlCommand(@"
                        INSERT INTO KodyPocztowe (Kod, miej)
                        SELECT DISTINCT o.KOD, MAX(o.MIASTO) FROM OdbiorcyCRM o
                        WHERE o.KOD IS NOT NULL AND o.KOD <> '' AND NOT EXISTS (SELECT 1 FROM KodyPocztowe kp WHERE REPLACE(kp.Kod, '-', '') = REPLACE(o.KOD, '-', ''))
                        GROUP BY o.KOD", conn).ExecuteNonQueryAsync();

                    var cmdGet = new SqlCommand("SELECT TOP 50000 Kod, miej FROM KodyPocztowe WHERE Latitude IS NULL", conn);
                    var lista = new List<(string k, string m)>();
                    using (var r = await cmdGet.ExecuteReaderAsync()) while (await r.ReadAsync()) lista.Add((r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));

                    if (lista.Count == 0)
                    {
                        MessageBox.Show("Wszystkie kody są już przeliczone!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var delayMs = 15;
                        foreach (var (kod, miasto) in lista)
                        {
                            txtLoadingStatus.Text = $"Geokodowanie bazy: {kod} ({sukces + 1}/{lista.Count})...";
                            var coords = await GeokodujGoogle(kod, miasto, apiKey);

                            if (coords.HasValue)
                            {
                                var cmdUpd = new SqlCommand("UPDATE KodyPocztowe SET Latitude=@lat, Longitude=@lng WHERE Kod=@kod", conn);
                                cmdUpd.Parameters.AddWithValue("@lat", coords.Value.lat);
                                cmdUpd.Parameters.AddWithValue("@lng", coords.Value.lng);
                                cmdUpd.Parameters.AddWithValue("@kod", kod);
                                await cmdUpd.ExecuteNonQueryAsync();
                                sukces++;
                            }
                            else bledy++;
                            await Task.Delay(delayMs);
                        }
                        MessageBox.Show($"Zakończono geokodowanie!\nPrzeliczono: {sukces}\nBłędy: {bledy}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }

            loadingOverlay.Visibility = Visibility.Collapsed;
            btnGeokoduj.IsEnabled = true;
            isLoading = false;
            await OdswiezMapeAsync();
        }

        private async Task<(double lat, double lng)?> GeokodujGoogle(string kod, string miasto, string apiKey)
        {
            try
            {
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={HttpUtility.UrlEncode(kod + " " + miasto + " Poland")}&key={apiKey}";
                using (var resp = await http.GetAsync(url))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    var json = await resp.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.GetProperty("status").GetString() == "OK")
                        {
                            var loc = doc.RootElement.GetProperty("results")[0].GetProperty("geometry").GetProperty("location");
                            return (loc.GetProperty("lat").GetDouble(), loc.GetProperty("lng").GetDouble());
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private async void Filtr_Changed(object sender, RoutedEventArgs e) => await OdswiezMapeAsync();
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) { dtKontakty = null; await OdswiezMapeAsync(); }
        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
        private async void KontaktItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is MapKontakt k && isWebViewReady)
                await webView.ExecuteScriptAsync($"setView({k.Lat.ToString(CultureInfo.InvariantCulture)}, {k.Lng.ToString(CultureInfo.InvariantCulture)}, 15);");
        }
    }

    public class MapKontakt
    {
        public int ID { get; set; }
        public string Nazwa { get; set; } = "";
        public string Miasto { get; set; } = "";
        public string Ulica { get; set; } = "";
        public string Telefon { get; set; } = "";
        public string Email { get; set; } = "";
        public string Wojewodztwo { get; set; } = "";
        public string Branza { get; set; } = "";
        public string Status { get; set; } = "";
        public bool CzyPriorytetowa { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double DystansKm { get; set; } // Nowe pole
        public string KolorHex { get; set; } = "#9CA3AF";
        public SolidColorBrush KolorStatusu { get; set; }
        public SolidColorBrush TloStatusu { get; set; }
        public SolidColorBrush KolorStatusuTekst { get; set; }
    }
}