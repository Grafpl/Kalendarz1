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
            // Pobierz klucz API z pola tekstowego
            string apiKey = "";
            Dispatcher.Invoke(() => apiKey = txtApiKey.Text?.Trim() ?? "");

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
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body, #map {{ height: 100%; width: 100%; font-family: 'Segoe UI', sans-serif; }}
        .gm-style-iw {{ max-width: 280px !important; }}
        .p-title {{ font-weight: 700; font-size: 13px; color: #111; margin-bottom: 6px; }}
        .p-info {{ font-size: 11px; color: #666; margin: 3px 0; }}
        .p-status {{ display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 10px; font-weight: 600; margin-top: 6px; }}
        .p-btn {{ display: inline-block; padding: 8px 12px; border-radius: 6px; font-size: 11px; font-weight: 600; text-decoration: none; margin-top: 8px; margin-right: 4px; }}
        .p-btn-green {{ background: #16A34A; color: white; }}
        .p-btn-gray {{ background: #E5E7EB; color: #374151; }}
        .error-msg {{ display: flex; justify-content: center; align-items: center; height: 100%; font-size: 16px; color: #DC2626; text-align: center; padding: 20px; }}
    </style>
</head>
<body>
<div id='map'></div>
<script>
var data = {dataJson};
var map, markers = [], infoWindow;

var statusBg = {{'Do zadzwonienia':'#F1F5F9','Próba kontaktu':'#FFEDD5','Nawiązano kontakt':'#DCFCE7','Zgoda na dalszy kontakt':'#CCFBF1','Do wysłania oferta':'#CFFAFE','Nie zainteresowany':'#FEE2E2'}};
var statusTxt = {{'Do zadzwonienia':'#475569','Próba kontaktu':'#9A3412','Nawiązano kontakt':'#166534','Zgoda na dalszy kontakt':'#0D9488','Do wysłania oferta':'#155E75','Nie zainteresowany':'#991B1B'}};

function initMap() {{
    map = new google.maps.Map(document.getElementById('map'), {{
        center: {{ lat: 52.0, lng: 19.0 }},
        zoom: 6,
        mapTypeControl: true,
        streetViewControl: false,
        fullscreenControl: true
    }});

    infoWindow = new google.maps.InfoWindow();

    var bounds = new google.maps.LatLngBounds();

    for (var i = 0; i < data.length; i++) {{
        var p = data[i];
        var pos = {{ lat: p.Lat, lng: p.Lng }};
        bounds.extend(pos);

        var sz = p.CzyPriorytetowa ? 16 : 12;
        var bw = p.CzyPriorytetowa ? 3 : 2;
        var bc = p.CzyPriorytetowa ? '%23DC2626' : '%23333';

        var svgIcon = {{
            url: 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns=""http://www.w3.org/2000/svg"" width=""'+sz+'"" height=""'+sz+'""><circle cx=""'+(sz/2)+'"" cy=""'+(sz/2)+'"" r=""'+((sz/2)-1)+'"" fill=""'+p.KolorHex+'"" stroke=""'+(p.CzyPriorytetowa?'#DC2626':'#333')+'"" stroke-width=""'+bw+'""/></svg>'),
            scaledSize: new google.maps.Size(sz, sz),
            anchor: new google.maps.Point(sz/2, sz/2)
        }};

        var marker = new google.maps.Marker({{
            position: pos,
            map: map,
            icon: svgIcon,
            title: p.Nazwa
        }});

        marker.kontakt = p;
        markers.push(marker);

        marker.addListener('click', function() {{
            var k = this.kontakt;
            var adr = [k.Ulica, k.Miasto].filter(Boolean).join(', ');
            var content = '<div class=""p-title"">'+k.Nazwa+'</div>'+
                '<div class=""p-info"">📍 '+adr+'</div>'+
                '<div class=""p-info"">📞 <b>'+k.Telefon+'</b></div>'+
                (k.Email ? '<div class=""p-info"">✉️ '+k.Email+'</div>' : '')+
                '<div><span class=""p-status"" style=""background:'+(statusBg[k.Status]||'#eee')+';color:'+(statusTxt[k.Status]||'#333')+'"">'+k.Status+'</span></div>'+
                '<div><a class=""p-btn p-btn-green"" href=""tel:'+k.Telefon.replace(/\s/g,'')+'"">📞 Zadzwoń</a>'+
                '<a class=""p-btn p-btn-gray"" href=""https://www.google.com/maps/dir//'+encodeURIComponent(adr)+'"" target=""_blank"">🗺️ Trasa</a></div>';
            infoWindow.setContent(content);
            infoWindow.open(map, this);
        }});
    }}

    if (data.length > 0) {{
        map.fitBounds(bounds);
        if (data.length === 1) map.setZoom(14);
    }}

    // MarkerClusterer
    if (typeof markerClusterer !== 'undefined' && markerClusterer.MarkerClusterer) {{
        new markerClusterer.MarkerClusterer({{ map: map, markers: markers }});
    }}
}}

window.setView = function(lat, lng, z) {{
    if (map) {{
        map.setCenter({{ lat: lat, lng: lng }});
        map.setZoom(z || 15);
    }}
}};

window.gm_authFailure = function() {{
    document.getElementById('map').innerHTML = '<div class=""error-msg"">Błąd autoryzacji Google Maps API.<br/>Sprawdź klucz API i upewnij się, że masz włączone:<br/>- Maps JavaScript API<br/>- Geocoding API</div>';
}};
</script>
<script src=""https://unpkg.com/@googlemaps/markerclusterer/dist/index.min.js""></script>
<script async defer src=""https://maps.googleapis.com/maps/api/js?key={apiKey}&callback=initMap&loading=async""></script>
</body>
</html>";
        }

        // ===== GEOKODOWANIE TABELI KODYPOCZTOWE (jednorazowe) =====
        private async void BtnGeokoduj_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;

            // Sprawdź klucz API
            var apiKey = txtApiKey.Text?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Wprowadź klucz Google API w polu 'API Key' w nagłówku.\n\n" +
                    "Aby uzyskać klucz:\n" +
                    "1. Wejdź na https://console.cloud.google.com/\n" +
                    "2. Utwórz projekt\n" +
                    "3. Włącz APIs: Geocoding API i Maps JavaScript API\n" +
                    "4. Utwórz klucz w sekcji Credentials",
                    "Brak klucza API", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sprawdź ile kodów UŻYWANYCH PRZEZ ODBIORCÓW nie ma współrzędnych
            int bezWspolrzednych = 0;
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                // Tylko kody które są faktycznie używane przez odbiorców!
                var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT kp.Kod)
                    FROM KodyPocztowe kp
                    INNER JOIN OdbiorcyCRM o ON o.KOD = kp.Kod
                    WHERE kp.Latitude IS NULL OR kp.Longitude IS NULL", conn);
                bezWspolrzednych = (int)await cmd.ExecuteScalarAsync();
            }

            if (bezWspolrzednych == 0)
            {
                MessageBox.Show("Wszystkie kody pocztowe używane przez odbiorców mają już współrzędne!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Znaleziono {bezWspolrzednych} kodów pocztowych (używanych przez odbiorców) bez współrzędnych.\n\n" +
                $"Geokodowanie Google API (50 req/s) - będzie szybko!\n" +
                $"Koszt: ~$5 za 1000 kodów (pierwsze $200 miesięcznie gratis).\n\n" +
                $"Kontynuować?",
                "Uzupełnij współrzędne (Google Geocoding API)", MessageBoxButton.YesNo, MessageBoxImage.Question);

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

                    // Pobierz kody używane przez odbiorców, posortowane po liczbie odbiorców (najpopularniejsze najpierw)
                    var cmdSelect = new SqlCommand(@"
                        SELECT TOP 2000 kp.Kod, kp.miej, COUNT(*) as Ile
                        FROM KodyPocztowe kp
                        INNER JOIN OdbiorcyCRM o ON o.KOD = kp.Kod
                        WHERE kp.Latitude IS NULL OR kp.Longitude IS NULL
                        GROUP BY kp.Kod, kp.miej
                        ORDER BY COUNT(*) DESC", conn);

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

                        var coords = await GeokodujKodGoogleAsync(kod, miasto, apiKey);

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

                        // Google API pozwala na 50 req/s, ale dajmy 20ms dla bezpieczeństwa
                        await Task.Delay(25);
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

            MessageBox.Show($"Zakończono!\n\nZnalezione: {sukces}\nBłędy: {bledy}", "Geokodowanie Google", MessageBoxButton.OK, MessageBoxImage.Information);

            // Odśwież mapę
            dtKontakty = null;
            await OdswiezMapeAsync();
        }

        private async Task<(double lat, double lng)?> GeokodujKodGoogleAsync(string kod, string miasto, string apiKey)
        {
            try
            {
                // Google Geocoding API - format: kod pocztowy, miasto, Polska
                var address = !string.IsNullOrEmpty(miasto)
                    ? $"{kod}, {miasto}, Poland"
                    : $"{kod}, Poland";

                var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={HttpUtility.UrlEncode(address)}&components=country:PL&key={apiKey}";

                using (var resp = await http.GetAsync(url))
                {
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            var status = root.GetProperty("status").GetString();

                            if (status == "OK")
                            {
                                var results = root.GetProperty("results");
                                if (results.GetArrayLength() > 0)
                                {
                                    var location = results[0].GetProperty("geometry").GetProperty("location");
                                    var lat = location.GetProperty("lat").GetDouble();
                                    var lng = location.GetProperty("lng").GetDouble();
                                    return (lat, lng);
                                }
                            }
                            else if (status == "OVER_QUERY_LIMIT" || status == "REQUEST_DENIED")
                            {
                                Debug.WriteLine($"Google API error: {status}");
                                // Poczekaj chwilę przy przekroczeniu limitu
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Google Geocode error for {kod}: {ex.Message}");
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
