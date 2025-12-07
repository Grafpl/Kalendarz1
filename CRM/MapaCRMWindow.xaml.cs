using Microsoft.Data.SqlClient;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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
        private DataTable? dtKontakty;
        private List<MapKontakt> kontaktyNaMapie = new List<MapKontakt>();
        private bool isLoading = false;
        private bool isWebViewReady = false;

        // HTTP dla geokodowania
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
            Loaded += MapaCRMWindow_Loaded;
        }

        private async void MapaCRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InicjalizujFiltry();

            try
            {
                txtLoadingStatus.Text = "Inicjalizacja WebView2...";
                await webView.EnsureCoreWebView2Async();
                isWebViewReady = true;
                await OdswiezMapeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji WebView2: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
                txtLoadingStatus.Text = "Pobieranie danych...";

                await Task.Run(() => WczytajDaneZBazy());
                WypelnijFiltrBranz();

                txtLoadingStatus.Text = "Filtrowanie...";
                var przefiltrowane = await FiltrujKontaktyAsync();

                txtLoadingStatus.Text = $"Renderowanie mapy ({przefiltrowane.Count} punktów)...";
                var html = GenerujHtmlMapy(przefiltrowane);
                webView.NavigateToString(html);

                kontaktyNaMapie = przefiltrowane;
                listaKontaktow.ItemsSource = kontaktyNaMapie.Take(100).ToList();
                txtLiczbaKontaktow.Text = $"{kontaktyNaMapie.Count} kontaktów";

                AktualizujStatystyki(przefiltrowane);
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
            finally
            {
                isLoading = false;
            }
        }

        private void WczytajDaneZBazy()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Prosty JOIN z tabelą KodyPocztowe - współrzędne pobierane z gotowej tabeli
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
                    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    LEFT JOIN PriorytetoweBranzeCRM pb ON o.PKD_Opis = pb.PKD_Opis
                    LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
                    WHERE (w.OperatorID = @OperatorID OR w.OperatorID IS NULL)
                        AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@OperatorID", operatorID);
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
                cmbBranza.SelectedIndex = currentIndex >= 0 && currentIndex < cmbBranza.Items.Count ? currentIndex : 0;
            });
        }

        private async Task<List<MapKontakt>> FiltrujKontaktyAsync()
        {
            if (dtKontakty == null) return new List<MapKontakt>();

            var wynik = new List<MapKontakt>();
            string filtrWoj = "", filtrBranza = "";
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
                if (status == "Nowy" || string.IsNullOrWhiteSpace(status))
                    status = "Do zadzwonienia";

                if (!statusyDoPokazania.Contains(status)) continue;

                var woj = row["Wojewodztwo"]?.ToString()?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(filtrWoj) && !woj.Contains(filtrWoj)) continue;

                var branza = row["PKD_Opis"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(filtrBranza) && branza != filtrBranza) continue;

                var czyPriorytetowa = Convert.ToInt32(row["CzyPriorytetowa"] ?? 0) == 1;
                if (tylkoPriorytetowe && !czyPriorytetowa) continue;

                // Sprawdź współrzędne z JOIN
                var latVal = row["Latitude"];
                var lngVal = row["Longitude"];

                if (latVal == DBNull.Value || lngVal == DBNull.Value) continue;

                if (!double.TryParse(latVal?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                    !double.TryParse(lngVal?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng))
                    continue;

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
                    Lng = lng
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
                kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
                kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563"));
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
            var dataJson = JsonSerializer.Serialize(kontakty.Select(k => new
            {
                k.ID, k.Nazwa, k.Miasto, k.Ulica, k.Telefon, k.Email,
                k.Status, k.Branza, k.CzyPriorytetowa, k.KolorHex, k.Lat, k.Lng
            }), new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css'/>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body, #map {{ height: 100%; width: 100%; font-family: 'Segoe UI', sans-serif; }}
        .leaflet-popup-content-wrapper {{ border-radius: 10px; box-shadow: 0 4px 15px rgba(0,0,0,0.15); }}
        .leaflet-popup-content {{ margin: 12px; min-width: 220px; }}
        .p-title {{ font-weight: 700; font-size: 13px; color: #111; margin-bottom: 6px; }}
        .p-info {{ font-size: 11px; color: #666; margin: 3px 0; }}
        .p-status {{ display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 10px; font-weight: 600; margin-top: 6px; }}
        .p-btn {{ display: inline-block; padding: 8px 12px; border-radius: 6px; font-size: 11px; font-weight: 600; text-decoration: none; margin-top: 8px; margin-right: 4px; }}
        .p-btn-green {{ background: #16A34A; color: white; }}
        .p-btn-gray {{ background: #E5E7EB; color: #374151; }}
        .marker-cluster-small {{ background-color: rgba(22, 163, 74, 0.6); }}
        .marker-cluster-small div {{ background-color: rgba(22, 163, 74, 0.9); color: white; }}
        .marker-cluster-medium {{ background-color: rgba(245, 158, 11, 0.6); }}
        .marker-cluster-medium div {{ background-color: rgba(245, 158, 11, 0.9); color: white; }}
        .marker-cluster-large {{ background-color: rgba(239, 68, 68, 0.6); }}
        .marker-cluster-large div {{ background-color: rgba(239, 68, 68, 0.9); color: white; }}
    </style>
</head>
<body>
<div id='map'></div>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<script src='https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js'></script>
<script>
var data = {dataJson};
var map = L.map('map').setView([52.0, 19.0], 6);

L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
    attribution: '© OpenStreetMap', maxZoom: 18
}}).addTo(map);

var markers = L.markerClusterGroup({{ maxClusterRadius: 50, disableClusteringAtZoom: 13 }});

var statusBg = {{'Do zadzwonienia':'#F1F5F9','Próba kontaktu':'#FFEDD5','Nawiązano kontakt':'#DCFCE7','Zgoda na dalszy kontakt':'#CCFBF1','Do wysłania oferta':'#CFFAFE','Nie zainteresowany':'#FEE2E2'}};
var statusTxt = {{'Do zadzwonienia':'#475569','Próba kontaktu':'#9A3412','Nawiązano kontakt':'#166534','Zgoda na dalszy kontakt':'#0D9488','Do wysłania oferta':'#155E75','Nie zainteresowany':'#991B1B'}};

for (var i = 0; i < data.length; i++) {{
    var p = data[i];
    var sz = p.CzyPriorytetowa ? 16 : 12;
    var bw = p.CzyPriorytetowa ? 3 : 2;
    var bc = p.CzyPriorytetowa ? '#DC2626' : '#333';

    var icon = L.divIcon({{
        html: '<div style=""width:'+sz+'px;height:'+sz+'px;border-radius:50%;background:'+p.KolorHex+';border:'+bw+'px solid '+bc+';box-shadow:0 2px 4px rgba(0,0,0,0.3)""></div>',
        className: '', iconSize: [sz, sz], iconAnchor: [sz/2, sz/2]
    }});

    var adr = [p.Ulica, p.Miasto].filter(Boolean).join(', ');
    var popup = '<div class=""p-title"">'+p.Nazwa+'</div>'+
        '<div class=""p-info"">📍 '+adr+'</div>'+
        '<div class=""p-info"">📞 <b>'+p.Telefon+'</b></div>'+
        (p.Email ? '<div class=""p-info"">✉️ '+p.Email+'</div>' : '')+
        '<div><span class=""p-status"" style=""background:'+(statusBg[p.Status]||'#eee')+';color:'+(statusTxt[p.Status]||'#333')+'"">'+p.Status+'</span></div>'+
        '<div><a class=""p-btn p-btn-green"" href=""tel:'+p.Telefon.replace(/\s/g,'')+'"">📞 Zadzwoń</a>'+
        '<a class=""p-btn p-btn-gray"" href=""https://www.google.com/maps/dir//'+encodeURIComponent(adr)+'"" target=""_blank"">🗺️ Trasa</a></div>';

    var m = L.marker([p.Lat, p.Lng], {{ icon: icon }}).bindPopup(popup);
    markers.addLayer(m);
}}

map.addLayer(markers);
if (data.length > 0) map.fitBounds(markers.getBounds().pad(0.1));

window.setView = function(lat, lng, z) {{ map.setView([lat, lng], z || 15); }};
</script>
</body>
</html>";
        }

        // ===== GEOKODOWANIE TABELI KODYPOCZTOWE (jednorazowe) =====
        private async void BtnGeokoduj_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;

            // Sprawdź ile kodów nie ma współrzędnych
            int bezWspolrzednych = 0;
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT COUNT(*) FROM KodyPocztowe WHERE Latitude IS NULL OR Longitude IS NULL", conn);
                bezWspolrzednych = (int)await cmd.ExecuteScalarAsync();
            }

            if (bezWspolrzednych == 0)
            {
                MessageBox.Show("Wszystkie kody pocztowe mają już współrzędne!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Znaleziono {bezWspolrzednych} kodów pocztowych bez współrzędnych.\n\n" +
                $"Pobieranie współrzędnych zajmie około {bezWspolrzednych / 2} sekund.\n" +
                $"(To operacja jednorazowa - potem mapa będzie działać błyskawicznie)\n\n" +
                $"Kontynuować?",
                "Uzupełnij współrzędne", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            isLoading = true;
            loadingOverlay.Visibility = Visibility.Visible;
            btnGeokoduj.IsEnabled = false;

            int sukces = 0, bledy = 0;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Pobierz kody bez współrzędnych
                    var cmdSelect = new SqlCommand(
                        "SELECT TOP 500 Kod, miej FROM KodyPocztowe WHERE Latitude IS NULL OR Longitude IS NULL", conn);

                    var kodyDoGeokodowania = new List<(string kod, string miasto)>();
                    using (var reader = await cmdSelect.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            kodyDoGeokodowania.Add((reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
                        }
                    }

                    for (int i = 0; i < kodyDoGeokodowania.Count; i++)
                    {
                        var (kod, miasto) = kodyDoGeokodowania[i];
                        txtLoadingStatus.Text = $"Geokodowanie {i + 1}/{kodyDoGeokodowania.Count}: {kod} {miasto}...";

                        var coords = await GeokodujKodAsync(kod, miasto);

                        if (coords.HasValue)
                        {
                            var cmdUpdate = new SqlCommand(
                                "UPDATE KodyPocztowe SET Latitude = @lat, Longitude = @lng WHERE Kod = @kod", conn);
                            cmdUpdate.Parameters.AddWithValue("@lat", coords.Value.lat);
                            cmdUpdate.Parameters.AddWithValue("@lng", coords.Value.lng);
                            cmdUpdate.Parameters.AddWithValue("@kod", kod);
                            await cmdUpdate.ExecuteNonQueryAsync();
                            sukces++;
                        }
                        else
                        {
                            bledy++;
                        }

                        // Opóźnienie dla API (Nominatim wymaga max 1 req/s)
                        await Task.Delay(500);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            loadingOverlay.Visibility = Visibility.Collapsed;
            btnGeokoduj.IsEnabled = true;
            isLoading = false;

            MessageBox.Show($"Zakończono!\n\nZnalezione: {sukces}\nBłędy: {bledy}", "Geokodowanie", MessageBoxButton.OK, MessageBoxImage.Information);

            // Odśwież mapę
            dtKontakty = null;
            await OdswiezMapeAsync();
        }

        private async Task<(double lat, double lng)?> GeokodujKodAsync(string kod, string miasto)
        {
            try
            {
                http.DefaultRequestHeaders.UserAgent.Clear();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("CRMApp/1.0");

                // Próba 1: kod + miasto + Poland
                var query = !string.IsNullOrEmpty(miasto)
                    ? $"{kod} {miasto} Poland"
                    : $"{kod} Poland";

                var url = $"https://nominatim.openstreetmap.org/search?q={HttpUtility.UrlEncode(query)}&format=json&limit=1&countrycodes=pl";

                using (var resp = await http.GetAsync(url))
                {
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var arr = doc.RootElement;
                            if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                            {
                                var first = arr[0];
                                var lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
                                var lng = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
                                return (lat, lng);
                            }
                        }
                    }
                }

                // Próba 2: tylko miasto jeśli kod nie znaleziony
                if (!string.IsNullOrEmpty(miasto))
                {
                    await Task.Delay(500);
                    url = $"https://nominatim.openstreetmap.org/search?q={HttpUtility.UrlEncode(miasto + " Poland")}&format=json&limit=1&countrycodes=pl";

                    using (var resp = await http.GetAsync(url))
                    {
                        if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadAsStringAsync();
                            using (var doc = JsonDocument.Parse(json))
                            {
                                var arr = doc.RootElement;
                                if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                                {
                                    var first = arr[0];
                                    var lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
                                    var lng = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
                                    return (lat, lng);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Geocode error for {kod}: {ex.Message}");
            }
            return null;
        }

        private async void Filtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!isLoading && dtKontakty != null && isWebViewReady)
                await OdswiezMapeAsync();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            dtKontakty = null;
            await OdswiezMapeAsync();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        private async void KontaktItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is MapKontakt k && isWebViewReady)
            {
                try
                {
                    await webView.ExecuteScriptAsync($"setView({k.Lat.ToString(CultureInfo.InvariantCulture)}, {k.Lng.ToString(CultureInfo.InvariantCulture)}, 15);");
                }
                catch { }
            }
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
        public string KolorHex { get; set; } = "#9CA3AF";
        public SolidColorBrush KolorStatusu { get; set; } = Brushes.Gray;
        public SolidColorBrush TloStatusu { get; set; } = Brushes.LightGray;
        public SolidColorBrush KolorStatusuTekst { get; set; } = Brushes.DarkGray;
    }
}
