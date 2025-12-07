using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class MapaCRMWindow : Window
    {
        private readonly string connectionString;
        private readonly string operatorID;
        private System.Windows.Forms.WebBrowser webBrowser;
        private DataTable dtKontakty;
        private List<MapKontakt> kontaktyNaMapie = new List<MapKontakt>();
        private bool isLoading = false;

        // HTTP dla geokodowania
        private static readonly HttpClient http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        private DateTime lastGeocode = DateTime.MinValue;
        private static readonly TimeSpan GeocodeDelay = TimeSpan.FromSeconds(1);

        public MapaCRMWindow(string connString, string opID)
        {
            InitializeComponent();
            connectionString = connString;
            operatorID = opID;

            // Ustaw User-Agent dla Nominatim
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CRMMap", "1.0"));

            Loaded += MapaCRMWindow_Loaded;
        }

        private async void MapaCRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Inicjalizuj WebBrowser
            webBrowser = new System.Windows.Forms.WebBrowser
            {
                ScriptErrorsSuppressed = true,
                AllowNavigation = true
            };
            mapHost.Child = webBrowser;

            // Wczytaj filtry
            InicjalizujFiltry();

            // Pokaż pustą mapę
            PokazPustaMape();

            // Wczytaj dane
            await OdswiezMapeAsync();
        }

        private void InicjalizujFiltry()
        {
            // Województwa
            cmbWojewodztwo.Items.Clear();
            cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = "Wszystkie" });
            var wojewodztwa = new[] { "dolnośląskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
                "łódzkie", "małopolskie", "mazowieckie", "opolskie", "podkarpackie", "podlaskie",
                "pomorskie", "śląskie", "świętokrzyskie", "warmińsko-mazurskie", "wielkopolskie", "zachodniopomorskie" };
            foreach (var woj in wojewodztwa)
            {
                cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = woj });
            }
            cmbWojewodztwo.SelectedIndex = 0;

            // Branże
            cmbBranza.Items.Clear();
            cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
            cmbBranza.SelectedIndex = 0;
        }

        private void PokazPustaMape()
        {
            var html = GenerujHtmlMapy(new List<MapKontakt>());
            string tempPath = Path.Combine(Path.GetTempPath(), "mapa_crm_wpf.html");
            File.WriteAllText(tempPath, html, Encoding.UTF8);
            webBrowser.Navigate(tempPath);
        }

        private async Task OdswiezMapeAsync()
        {
            if (isLoading) return;
            isLoading = true;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                txtLoadingStatus.Text = "Pobieranie danych...";

                // Pobierz dane z bazy
                await Task.Run(() => WczytajDaneZBazy());

                // Wypełnij filtry branż
                WypelnijFiltrBranz();

                txtLoadingStatus.Text = "Geokodowanie adresów...";

                // Filtruj i geokoduj
                var przefiltrowane = await FiltrujIGeokodujAsync();

                txtLoadingStatus.Text = "Renderowanie mapy...";

                // Generuj mapę
                var html = GenerujHtmlMapy(przefiltrowane);
                string tempPath = Path.Combine(Path.GetTempPath(), "mapa_crm_wpf.html");
                File.WriteAllText(tempPath, html, Encoding.UTF8);
                webBrowser.Navigate(tempPath);

                // Aktualizuj listę boczną
                kontaktyNaMapie = przefiltrowane;
                listaKontaktow.ItemsSource = kontaktyNaMapie.Take(100).ToList(); // Max 100 w liście
                txtLiczbaKontaktow.Text = $"{kontaktyNaMapie.Count} kontaktów";

                // Aktualizuj statystyki
                AktualizujStatystyki(przefiltrowane);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania mapy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                isLoading = false;
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
                        o.Latitude,
                        o.Longitude
                    FROM OdbiorcyCRM o
                    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    LEFT JOIN PriorytetoweBranzeCRM pb ON o.PKD_Opis = pb.PKD_Opis
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

                cmbBranza.Items.Clear();
                cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
                foreach (var branza in branze)
                {
                    cmbBranza.Items.Add(new ComboBoxItem { Content = branza, Tag = branza });
                }
                cmbBranza.SelectedIndex = 0;
            });
        }

        private async Task<List<MapKontakt>> FiltrujIGeokodujAsync()
        {
            if (dtKontakty == null) return new List<MapKontakt>();

            var wynik = new List<MapKontakt>();

            // Pobierz wartości filtrów
            string filtrWoj = "";
            string filtrBranza = "";
            bool tylkoPriorytetowe = false;
            var statusyDoPokazania = new List<string>();

            await Dispatcher.InvokeAsync(() =>
            {
                if (cmbWojewodztwo.SelectedIndex > 0)
                    filtrWoj = (cmbWojewodztwo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "";

                if (cmbBranza.SelectedIndex > 0)
                    filtrBranza = (cmbBranza.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

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

                // Filtr statusu
                if (!statusyDoPokazania.Contains(status))
                    continue;

                // Filtr województwa
                var woj = row["Wojewodztwo"]?.ToString()?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(filtrWoj) && !woj.Contains(filtrWoj))
                    continue;

                // Filtr branży
                var branza = row["PKD_Opis"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(filtrBranza) && branza != filtrBranza)
                    continue;

                // Filtr priorytetowe
                var czyPriorytetowa = Convert.ToInt32(row["CzyPriorytetowa"]) == 1;
                if (tylkoPriorytetowe && !czyPriorytetowa)
                    continue;

                // Sprawdź współrzędne
                double lat = 0, lng = 0;
                bool hasCoords = false;

                if (double.TryParse(row["Latitude"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
                    double.TryParse(row["Longitude"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lng) &&
                    Math.Abs(lat) > 0.0001 && Math.Abs(lng) > 0.0001)
                {
                    hasCoords = true;
                }

                // Jeśli brak współrzędnych, spróbuj geokodować
                if (!hasCoords)
                {
                    var adres = ZbudujAdres(row);
                    if (!string.IsNullOrEmpty(adres))
                    {
                        // Sprawdź cache
                        var cached = await SprawdzCacheAsync(adres);
                        if (cached.HasValue)
                        {
                            lat = cached.Value.lat;
                            lng = cached.Value.lng;
                            hasCoords = true;
                        }
                        else
                        {
                            // Geokoduj (z opóźnieniem)
                            var wait = GeocodeDelay - (DateTime.UtcNow - lastGeocode);
                            if (wait > TimeSpan.Zero) await Task.Delay(wait);
                            lastGeocode = DateTime.UtcNow;

                            var geo = await GeokodujAsync(adres);
                            if (geo.HasValue)
                            {
                                lat = geo.Value.lat;
                                lng = geo.Value.lng;
                                hasCoords = true;
                                await ZapiszDoCache(adres, lat, lng);

                                // Aktualizuj w bazie
                                await ZapiszWspolrzedneDobazyAsync(Convert.ToInt32(row["ID"]), lat, lng);
                            }
                        }
                    }
                }

                if (!hasCoords) continue;

                // Dodaj do wyników
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

                // Ustaw kolory
                UstawKoloryStatusu(kontakt);
                wynik.Add(kontakt);
            }

            return wynik;
        }

        private void UstawKoloryStatusu(MapKontakt kontakt)
        {
            switch (kontakt.Status)
            {
                case "Do zadzwonienia":
                    kontakt.KolorHex = "#64748B";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
                    break;
                case "Próba kontaktu":
                    kontakt.KolorHex = "#F97316";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A3412"));
                    break;
                case "Nawiązano kontakt":
                    kontakt.KolorHex = "#22C55E";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                    break;
                case "Zgoda na dalszy kontakt":
                    kontakt.KolorHex = "#14B8A6";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14B8A6"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFBF1"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D9488"));
                    break;
                case "Do wysłania oferta":
                    kontakt.KolorHex = "#0891B2";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0891B2"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CFFAFE"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#155E75"));
                    break;
                case "Nie zainteresowany":
                    kontakt.KolorHex = "#EF4444";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                    break;
                default:
                    kontakt.KolorHex = "#9CA3AF";
                    kontakt.KolorStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
                    kontakt.TloStatusu = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                    kontakt.KolorStatusuTekst = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563"));
                    break;
            }
        }

        private string ZbudujAdres(DataRow row)
        {
            var parts = new List<string>();
            var ulica = row["ULICA"]?.ToString()?.Trim() ?? "";
            var kod = row["KOD"]?.ToString()?.Trim() ?? "";
            var miasto = row["MIASTO"]?.ToString()?.Trim() ?? "";
            var woj = row["Wojewodztwo"]?.ToString()?.Trim() ?? "";

            if (!string.IsNullOrEmpty(ulica)) parts.Add(ulica);
            if (!string.IsNullOrEmpty(kod)) parts.Add(kod);
            if (!string.IsNullOrEmpty(miasto)) parts.Add(miasto);
            if (!string.IsNullOrEmpty(woj)) parts.Add(woj);
            parts.Add("Polska");

            return string.Join(", ", parts);
        }

        private async Task<(double lat, double lng)?> SprawdzCacheAsync(string address)
        {
            try
            {
                string hash = Sha1(address);
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    var sql = @"IF OBJECT_ID('dbo.GeoCache','U') IS NULL
                        SELECT CAST(NULL AS float) AS Latitude, CAST(NULL AS float) AS Longitude
                    ELSE
                        SELECT Latitude, Longitude FROM dbo.GeoCache WHERE AddressHash = @h";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@h", hash);
                        using (var rdr = await cmd.ExecuteReaderAsync())
                        {
                            if (await rdr.ReadAsync() && !rdr.IsDBNull(0) && !rdr.IsDBNull(1))
                            {
                                return (Convert.ToDouble(rdr.GetValue(0)), Convert.ToDouble(rdr.GetValue(1)));
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private async Task ZapiszDoCache(string address, double lat, double lng)
        {
            try
            {
                string hash = Sha1(address);
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Sprawdź czy tabela istnieje, jeśli nie - utwórz
                    var cmdCreate = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GeoCache')
                        BEGIN
                            CREATE TABLE GeoCache (
                                AddressHash NVARCHAR(50) PRIMARY KEY,
                                AddressText NVARCHAR(500),
                                Latitude FLOAT,
                                Longitude FLOAT,
                                LastUpdate DATETIME2 DEFAULT SYSUTCDATETIME()
                            )
                        END", conn);
                    cmdCreate.ExecuteNonQuery();

                    var sql = @"
                        MERGE dbo.GeoCache AS tgt
                        USING (SELECT @h AS AddressHash) AS src
                        ON (tgt.AddressHash = src.AddressHash)
                        WHEN MATCHED THEN
                            UPDATE SET AddressText=@a, Latitude=@lat, Longitude=@lng, LastUpdate=SYSUTCDATETIME()
                        WHEN NOT MATCHED THEN
                            INSERT (AddressHash, AddressText, Latitude, Longitude, LastUpdate)
                            VALUES (@h, @a, @lat, @lng, SYSUTCDATETIME());";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@h", hash);
                        cmd.Parameters.AddWithValue("@a", address);
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch { }
        }

        private async Task ZapiszWspolrzedneDobazyAsync(int id, double lat, double lng)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Sprawdź czy kolumny istnieją
                    var cmdCheck = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'Latitude')
                        BEGIN
                            ALTER TABLE OdbiorcyCRM ADD Latitude FLOAT NULL
                        END
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'Longitude')
                        BEGIN
                            ALTER TABLE OdbiorcyCRM ADD Longitude FLOAT NULL
                        END", conn);
                    cmdCheck.ExecuteNonQuery();

                    var sql = "UPDATE OdbiorcyCRM SET Latitude = @lat, Longitude = @lng WHERE ID = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch { }
        }

        private async Task<(double lat, double lng)?> GeokodujAsync(string address)
        {
            try
            {
                string url = "https://nominatim.openstreetmap.org/search?" +
                             $"q={HttpUtility.UrlEncode(address)}&format=json&limit=1&addressdetails=0&countrycodes=pl";

                using (var resp = await http.GetAsync(url))
                {
                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var arr = doc.RootElement;
                        if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                        {
                            var first = arr[0];
                            double lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
                            double lon = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
                            return (lat, lon);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Geocode error: " + ex.Message);
            }
            return null;
        }

        private static string Sha1(string text)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var hash = sha1.ComputeHash(bytes);
                return string.Concat(hash.Select(b => b.ToString("x2")));
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
            var json = JsonSerializer.Serialize(kontakty.Select(k => new
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
                k.Lng
            }), new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

            var sb = new StringBuilder();
            sb.AppendLine(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<meta name='viewport' content='width=device-width, initial-scale=1.0'/>
<title>Mapa CRM</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<script src='https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js'></script>
<style>
    html, body { margin:0; padding:0; height:100%; font-family:'Segoe UI', Arial, sans-serif; }
    #map { height:100%; width:100%; }
    .leaflet-popup-content-wrapper { border-radius: 12px; padding: 0; }
    .leaflet-popup-content { margin: 0; min-width: 280px; }
    .popup-container { padding: 16px; }
    .popup-title { font-weight: 700; font-size: 14px; color: #111827; margin-bottom: 8px; line-height: 1.3; }
    .popup-info { font-size: 12px; color: #6B7280; margin-bottom: 4px; }
    .popup-info strong { color: #374151; }
    .popup-status { display: inline-block; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 600; margin-top: 8px; }
    .popup-priority { display: inline-block; background: #FEE2E2; color: #DC2626; padding: 4px 8px; border-radius: 4px; font-size: 10px; font-weight: 600; margin-left: 6px; }
    .popup-actions { margin-top: 12px; padding-top: 12px; border-top: 1px solid #E5E7EB; display: flex; gap: 8px; }
    .popup-btn { flex: 1; padding: 8px 12px; border: none; border-radius: 8px; font-size: 11px; font-weight: 600; cursor: pointer; text-decoration: none; text-align: center; }
    .popup-btn-primary { background: #16A34A; color: white; }
    .popup-btn-primary:hover { background: #15803D; }
    .popup-btn-secondary { background: #F1F5F9; color: #475569; }
    .popup-btn-secondary:hover { background: #E2E8F0; }
</style>
</head>
<body>
<div id='map'></div>
<script>");

            sb.Append("var data = ");
            sb.Append(json);
            sb.AppendLine(";");

            sb.AppendLine(@"
var map = L.map('map').setView([52.0, 19.0], 6);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 18
}).addTo(map);

var markers = L.markerClusterGroup({
    showCoverageOnHover: false,
    maxClusterRadius: 50,
    spiderfyOnMaxZoom: true,
    disableClusteringAtZoom: 16
});

function getStatusBg(status) {
    switch(status) {
        case 'Do zadzwonienia': return '#F1F5F9';
        case 'Próba kontaktu': return '#FFEDD5';
        case 'Nawiązano kontakt': return '#DCFCE7';
        case 'Zgoda na dalszy kontakt': return '#CCFBF1';
        case 'Do wysłania oferta': return '#CFFAFE';
        case 'Nie zainteresowany': return '#FEE2E2';
        default: return '#F3F4F6';
    }
}

function getStatusColor(status) {
    switch(status) {
        case 'Do zadzwonienia': return '#475569';
        case 'Próba kontaktu': return '#9A3412';
        case 'Nawiązano kontakt': return '#166534';
        case 'Zgoda na dalszy kontakt': return '#0D9488';
        case 'Do wysłania oferta': return '#155E75';
        case 'Nie zainteresowany': return '#991B1B';
        default: return '#4B5563';
    }
}

for (var i = 0; i < data.length; i++) {
    var p = data[i];

    var borderColor = p.CzyPriorytetowa ? '#DC2626' : '#374151';
    var borderWidth = p.CzyPriorytetowa ? 3 : 2;

    var icon = L.divIcon({
        html: '<div style=""width:18px;height:18px;border-radius:50%;background:' + p.KolorHex + ';border:' + borderWidth + 'px solid ' + borderColor + ';box-shadow:0 2px 6px rgba(0,0,0,0.3)""></div>',
        className: '',
        iconSize: [18, 18]
    });

    var adres = [p.Ulica, p.Miasto].filter(x => x).join(', ');
    var priorityBadge = p.CzyPriorytetowa ? '<span class=""popup-priority"">PRIORYTET</span>' : '';

    var popup = [
        '<div class=""popup-container"">',
        '  <div class=""popup-title"">' + (p.Nazwa || 'Brak nazwy') + '</div>',
        '  <div class=""popup-info"">📍 ' + (adres || 'Brak adresu') + '</div>',
        '  <div class=""popup-info"">📞 <strong>' + (p.Telefon || '-') + '</strong></div>',
        p.Email ? '  <div class=""popup-info"">✉️ ' + p.Email + '</div>' : '',
        p.Branza ? '  <div class=""popup-info"">🏢 ' + p.Branza.substring(0, 60) + (p.Branza.length > 60 ? '...' : '') + '</div>' : '',
        '  <div style=""margin-top:10px"">',
        '    <span class=""popup-status"" style=""background:' + getStatusBg(p.Status) + ';color:' + getStatusColor(p.Status) + '"">' + p.Status + '</span>',
        priorityBadge,
        '  </div>',
        '  <div class=""popup-actions"">',
        '    <a class=""popup-btn popup-btn-primary"" href=""tel:' + (p.Telefon || '') + '"">📞 Zadzwoń</a>',
        '    <a class=""popup-btn popup-btn-secondary"" href=""https://www.google.com/maps/dir//' + encodeURIComponent(adres) + '"" target=""_blank"">🗺️ Trasa</a>',
        '  </div>',
        '</div>'
    ].join('');

    var m = L.marker([p.Lat, p.Lng], { icon: icon });
    m.bindPopup(popup, { maxWidth: 320 });
    markers.addLayer(m);
}

map.addLayer(markers);

if (data.length > 0) {
    var group = new L.featureGroup(markers.getLayers());
    map.fitBounds(group.getBounds().pad(0.1));
}
</script>
</body>
</html>");

            return sb.ToString();
        }

        private async void Filtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!isLoading && dtKontakty != null)
            {
                await OdswiezMapeAsync();
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            dtKontakty = null; // Wymuś ponowne wczytanie
            await OdswiezMapeAsync();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void KontaktItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MapKontakt kontakt)
            {
                // Wycentruj mapę na wybranym kontakcie
                var js = $"map.setView([{kontakt.Lat.ToString(CultureInfo.InvariantCulture)}, {kontakt.Lng.ToString(CultureInfo.InvariantCulture)}], 15);";
                try
                {
                    webBrowser.Document?.InvokeScript("eval", new object[] { js });
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
