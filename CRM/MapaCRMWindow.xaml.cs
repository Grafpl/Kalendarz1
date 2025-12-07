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
using System.Security.Cryptography;
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
            Timeout = TimeSpan.FromSeconds(20)
        };
        private DateTime lastGeocode = DateTime.MinValue;
        private static readonly TimeSpan GeocodeDelay = TimeSpan.FromMilliseconds(500); // Szybsze dla kodów pocztowych

        public MapaCRMWindow(string connString, string opID)
        {
            InitializeComponent();
            connectionString = connString;
            operatorID = opID;

            Loaded += MapaCRMWindow_Loaded;
        }

        private async void MapaCRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Inicjalizuj filtry
            InicjalizujFiltry();

            // Inicjalizuj WebView2
            try
            {
                txtLoadingStatus.Text = "Inicjalizacja WebView2...";
                await webView.EnsureCoreWebView2Async();
                isWebViewReady = true;

                // Wczytaj dane i pokaż mapę
                await OdswiezMapeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji WebView2: {ex.Message}\n\nUpewnij się, że masz zainstalowany WebView2 Runtime.",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
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

        private async Task OdswiezMapeAsync()
        {
            if (isLoading || !isWebViewReady) return;
            isLoading = true;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                txtLoadingStatus.Text = "Pobieranie danych z bazy...";

                // Pobierz dane z bazy
                await Task.Run(() => WczytajDaneZBazy());

                // Wypełnij filtry branż
                WypelnijFiltrBranz();

                txtLoadingStatus.Text = "Przetwarzanie kontaktów...";

                // Filtruj kontakty
                var przefiltrowane = await FiltrujKontaktyAsync();

                txtLoadingStatus.Text = $"Renderowanie mapy ({przefiltrowane.Count} punktów)...";

                // Generuj i załaduj mapę
                var html = GenerujHtmlMapy(przefiltrowane);
                webView.NavigateToString(html);

                // Aktualizuj listę boczną
                kontaktyNaMapie = przefiltrowane;
                listaKontaktow.ItemsSource = kontaktyNaMapie.Take(100).ToList();
                txtLiczbaKontaktow.Text = $"{kontaktyNaMapie.Count} kontaktów";

                // Aktualizuj statystyki
                AktualizujStatystyki(przefiltrowane);

                loadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania mapy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Sprawdź czy kolumny Latitude/Longitude istnieją
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

                var currentIndex = cmbBranza.SelectedIndex;
                cmbBranza.Items.Clear();
                cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
                foreach (var branza in branze)
                {
                    cmbBranza.Items.Add(new ComboBoxItem { Content = branza, Tag = branza });
                }
                cmbBranza.SelectedIndex = currentIndex >= 0 && currentIndex < cmbBranza.Items.Count ? currentIndex : 0;
            });
        }

        private async Task<List<MapKontakt>> FiltrujKontaktyAsync()
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
                var czyPriorytetowa = Convert.ToInt32(row["CzyPriorytetowa"] ?? 0) == 1;
                if (tylkoPriorytetowe && !czyPriorytetowa)
                    continue;

                // Sprawdź współrzędne - TYLKO te które już mają zapisane
                double lat = 0, lng = 0;
                bool hasCoords = false;

                var latVal = row["Latitude"];
                var lngVal = row["Longitude"];

                if (latVal != DBNull.Value && lngVal != DBNull.Value)
                {
                    if (double.TryParse(latVal?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
                        double.TryParse(lngVal?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lng) &&
                        Math.Abs(lat) > 0.0001 && Math.Abs(lng) > 0.0001)
                    {
                        hasCoords = true;
                    }
                }

                // Pomiń kontakty bez współrzędnych - geokodowanie w tle
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

        private async Task<(double lat, double lng)?> GeokodujPoKodziePoczAsync(string kodPocztowy, string miasto, int idOdbiorcy)
        {
            if (string.IsNullOrWhiteSpace(kodPocztowy)) return null;

            // Normalizuj kod pocztowy (usuń myślnik, spacje)
            var kodNormalized = kodPocztowy.Replace("-", "").Replace(" ", "").Trim();
            if (kodNormalized.Length < 2) return null;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Sprawdź czy tabela cache kodów pocztowych istnieje
                    var cmdCreate = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GeoCacheKodyPocztowe')
                        BEGIN
                            CREATE TABLE GeoCacheKodyPocztowe (
                                KodPocztowy NVARCHAR(10) PRIMARY KEY,
                                Latitude FLOAT,
                                Longitude FLOAT,
                                Miasto NVARCHAR(100),
                                LastUpdate DATETIME2 DEFAULT SYSUTCDATETIME()
                            )
                        END", conn);
                    await cmdCreate.ExecuteNonQueryAsync();

                    // Sprawdź cache kodów pocztowych
                    var cmdCache = new SqlCommand("SELECT Latitude, Longitude FROM GeoCacheKodyPocztowe WHERE KodPocztowy = @kod", conn);
                    cmdCache.Parameters.AddWithValue("@kod", kodNormalized);
                    using (var rdr = await cmdCache.ExecuteReaderAsync())
                    {
                        if (await rdr.ReadAsync() && !rdr.IsDBNull(0) && !rdr.IsDBNull(1))
                        {
                            var lat = rdr.GetDouble(0);
                            var lng = rdr.GetDouble(1);
                            rdr.Close();

                            // Zapisz do OdbiorcyCRM
                            var cmdUpdate = new SqlCommand("UPDATE OdbiorcyCRM SET Latitude = @lat, Longitude = @lng WHERE ID = @id", conn);
                            cmdUpdate.Parameters.AddWithValue("@id", idOdbiorcy);
                            cmdUpdate.Parameters.AddWithValue("@lat", lat);
                            cmdUpdate.Parameters.AddWithValue("@lng", lng);
                            await cmdUpdate.ExecuteNonQueryAsync();

                            return (lat, lng);
                        }
                    }

                    // Geokoduj z API używając tylko kodu pocztowego
                    var wait = GeocodeDelay - (DateTime.UtcNow - lastGeocode);
                    if (wait > TimeSpan.Zero) await Task.Delay(wait);
                    lastGeocode = DateTime.UtcNow;

                    var geo = await GeokodujKodPocztowyAsync(kodPocztowy, miasto);
                    if (geo.HasValue)
                    {
                        // Zapisz do cache kodów pocztowych
                        var cmdInsert = new SqlCommand(@"
                            MERGE GeoCacheKodyPocztowe AS tgt
                            USING (SELECT @kod AS KodPocztowy) AS src ON tgt.KodPocztowy = src.KodPocztowy
                            WHEN MATCHED THEN UPDATE SET Latitude=@lat, Longitude=@lng, Miasto=@miasto, LastUpdate=SYSUTCDATETIME()
                            WHEN NOT MATCHED THEN INSERT (KodPocztowy, Latitude, Longitude, Miasto) VALUES (@kod, @lat, @lng, @miasto);", conn);
                        cmdInsert.Parameters.AddWithValue("@kod", kodNormalized);
                        cmdInsert.Parameters.AddWithValue("@lat", geo.Value.lat);
                        cmdInsert.Parameters.AddWithValue("@lng", geo.Value.lng);
                        cmdInsert.Parameters.AddWithValue("@miasto", miasto ?? "");
                        await cmdInsert.ExecuteNonQueryAsync();

                        // Zapisz do OdbiorcyCRM
                        var cmdUpdate = new SqlCommand("UPDATE OdbiorcyCRM SET Latitude = @lat, Longitude = @lng WHERE ID = @id", conn);
                        cmdUpdate.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdUpdate.Parameters.AddWithValue("@lat", geo.Value.lat);
                        cmdUpdate.Parameters.AddWithValue("@lng", geo.Value.lng);
                        await cmdUpdate.ExecuteNonQueryAsync();

                        return geo;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Geocode error: {ex.Message}");
            }
            return null;
        }

        private async Task<(double lat, double lng)?> GeokodujKodPocztowyAsync(string kodPocztowy, string? miasto)
        {
            try
            {
                // Użyj Nominatim z parametrem postalcode dla lepszej dokładności
                string query;
                if (!string.IsNullOrWhiteSpace(miasto))
                {
                    query = $"postalcode={HttpUtility.UrlEncode(kodPocztowy)}&city={HttpUtility.UrlEncode(miasto)}&country=Poland";
                }
                else
                {
                    query = $"postalcode={HttpUtility.UrlEncode(kodPocztowy)}&country=Poland";
                }

                string url = $"https://nominatim.openstreetmap.org/search?{query}&format=json&limit=1";

                http.DefaultRequestHeaders.UserAgent.Clear();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("CRMMapApp/1.0 (contact@example.com)");

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

                // Fallback - prostsze zapytanie tylko z kodem
                url = $"https://nominatim.openstreetmap.org/search?q={HttpUtility.UrlEncode(kodPocztowy + " Poland")}&format=json&limit=1";
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
                Debug.WriteLine("Geocode API error: " + ex.Message);
            }
            return null;
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
                k.Lng
            }), new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

            // Leaflet + OpenStreetMap (bez klucza API, niezawodne)
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <title>Mapa CRM</title>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css'/>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body {{ height: 100%; font-family: 'Segoe UI', Arial, sans-serif; }}
        #map {{ height: 100%; width: 100%; }}
        .leaflet-popup-content-wrapper {{ border-radius: 12px; padding: 0; box-shadow: 0 4px 20px rgba(0,0,0,0.15); }}
        .leaflet-popup-content {{ margin: 0; min-width: 280px; }}
        .popup-container {{ padding: 16px; }}
        .popup-title {{ font-weight: 700; font-size: 14px; color: #111827; margin-bottom: 8px; line-height: 1.3; }}
        .popup-info {{ font-size: 12px; color: #6B7280; margin-bottom: 4px; }}
        .popup-info strong {{ color: #374151; }}
        .popup-status {{ display: inline-block; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 600; margin-top: 8px; }}
        .popup-priority {{ display: inline-block; background: #FEE2E2; color: #DC2626; padding: 4px 8px; border-radius: 4px; font-size: 10px; font-weight: 600; margin-left: 6px; }}
        .popup-actions {{ margin-top: 12px; padding-top: 12px; border-top: 1px solid #E5E7EB; display: flex; gap: 8px; }}
        .popup-btn {{ flex: 1; padding: 10px 12px; border: none; border-radius: 8px; font-size: 12px; font-weight: 600; cursor: pointer; text-decoration: none; text-align: center; }}
        .popup-btn-primary {{ background: #16A34A; color: white; }}
        .popup-btn-secondary {{ background: #F1F5F9; color: #475569; }}
        .marker-cluster-small {{ background-color: rgba(22, 163, 74, 0.6); }}
        .marker-cluster-small div {{ background-color: rgba(22, 163, 74, 0.8); }}
        .marker-cluster-medium {{ background-color: rgba(245, 158, 11, 0.6); }}
        .marker-cluster-medium div {{ background-color: rgba(245, 158, 11, 0.8); }}
        .marker-cluster-large {{ background-color: rgba(239, 68, 68, 0.6); }}
        .marker-cluster-large div {{ background-color: rgba(239, 68, 68, 0.8); }}
        .marker-cluster {{ color: white; font-weight: bold; }}
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
    attribution: '© OpenStreetMap',
    maxZoom: 18
}}).addTo(map);

var markers = L.markerClusterGroup({{
    showCoverageOnHover: false,
    maxClusterRadius: 60,
    spiderfyOnMaxZoom: true,
    disableClusteringAtZoom: 14
}});

function getStatusBg(status) {{
    var colors = {{
        'Do zadzwonienia': '#F1F5F9',
        'Próba kontaktu': '#FFEDD5',
        'Nawiązano kontakt': '#DCFCE7',
        'Zgoda na dalszy kontakt': '#CCFBF1',
        'Do wysłania oferta': '#CFFAFE',
        'Nie zainteresowany': '#FEE2E2'
    }};
    return colors[status] || '#F3F4F6';
}}

function getStatusColor(status) {{
    var colors = {{
        'Do zadzwonienia': '#475569',
        'Próba kontaktu': '#9A3412',
        'Nawiązano kontakt': '#166534',
        'Zgoda na dalszy kontakt': '#0D9488',
        'Do wysłania oferta': '#155E75',
        'Nie zainteresowany': '#991B1B'
    }};
    return colors[status] || '#4B5563';
}}

for (var i = 0; i < data.length; i++) {{
    var p = data[i];

    var borderColor = p.CzyPriorytetowa ? '#DC2626' : '#374151';
    var borderWidth = p.CzyPriorytetowa ? 3 : 2;
    var size = p.CzyPriorytetowa ? 18 : 14;

    var icon = L.divIcon({{
        html: '<div style=""width:' + size + 'px;height:' + size + 'px;border-radius:50%;background:' + p.KolorHex + ';border:' + borderWidth + 'px solid ' + borderColor + ';box-shadow:0 2px 6px rgba(0,0,0,0.3)""></div>',
        className: '',
        iconSize: [size, size],
        iconAnchor: [size/2, size/2]
    }});

    var adres = [p.Ulica, p.Miasto].filter(function(x) {{ return x; }}).join(', ');
    var priorityBadge = p.CzyPriorytetowa ? '<span class=""popup-priority"">PRIORYTET</span>' : '';

    var popup = '<div class=""popup-container"">' +
        '<div class=""popup-title"">' + (p.Nazwa || 'Brak nazwy') + '</div>' +
        '<div class=""popup-info"">📍 ' + (adres || 'Brak adresu') + '</div>' +
        '<div class=""popup-info"">📞 <strong>' + (p.Telefon || '-') + '</strong></div>' +
        (p.Email ? '<div class=""popup-info"">✉️ ' + p.Email + '</div>' : '') +
        (p.Branza ? '<div class=""popup-info"" style=""font-size:10px;color:#9CA3AF"">🏢 ' + (p.Branza.length > 40 ? p.Branza.substring(0, 40) + '...' : p.Branza) + '</div>' : '') +
        '<div style=""margin-top:10px"">' +
        '<span class=""popup-status"" style=""background:' + getStatusBg(p.Status) + ';color:' + getStatusColor(p.Status) + '"">' + p.Status + '</span>' +
        priorityBadge +
        '</div>' +
        '<div class=""popup-actions"">' +
        '<a class=""popup-btn popup-btn-primary"" href=""tel:' + (p.Telefon || '').replace(/\s/g, '') + '"">📞 Zadzwoń</a>' +
        '<a class=""popup-btn popup-btn-secondary"" href=""https://www.google.com/maps/dir//' + encodeURIComponent(adres) + '"" target=""_blank"">🗺️ Trasa</a>' +
        '</div></div>';

    var m = L.marker([p.Lat, p.Lng], {{ icon: icon }});
    m.bindPopup(popup, {{ maxWidth: 320 }});
    markers.addLayer(m);
}}

map.addLayer(markers);

if (data.length > 0) {{
    var group = new L.featureGroup(markers.getLayers());
    map.fitBounds(group.getBounds().pad(0.1));
}}

window.setView = function(lat, lng, zoom) {{
    map.setView([lat, lng], zoom || 16);
}};
</script>
</body>
</html>";
        }

        private async void Filtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!isLoading && dtKontakty != null && isWebViewReady)
            {
                await OdswiezMapeAsync();
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            dtKontakty = null;
            await OdswiezMapeAsync();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnGeokoduj_Click(object sender, RoutedEventArgs e)
        {
            if (dtKontakty == null || isLoading) return;

            // Znajdź kontakty bez współrzędnych ALE z kodem pocztowym
            var bezWspolrzednych = dtKontakty.AsEnumerable()
                .Where(r =>
                {
                    var lat = r["Latitude"];
                    var lng = r["Longitude"];
                    var kod = r["KOD"]?.ToString()?.Trim() ?? "";

                    // Musi mieć kod pocztowy i nie mieć współrzędnych
                    if (string.IsNullOrWhiteSpace(kod) || kod.Length < 3) return false;

                    if (lat == DBNull.Value || lng == DBNull.Value) return true;
                    if (!double.TryParse(lat?.ToString(), out var latVal) ||
                        !double.TryParse(lng?.ToString(), out var lngVal)) return true;
                    if (Math.Abs(latVal) < 0.001 || Math.Abs(lngVal) < 0.001) return true;

                    return false;
                })
                .Take(200) // Max 200 na raz (szybsze z kodami pocztowymi)
                .ToList();

            if (bezWspolrzednych.Count == 0)
            {
                // Sprawdź ile w ogóle ma kody pocztowe
                var bezKodow = dtKontakty.AsEnumerable()
                    .Count(r => string.IsNullOrWhiteSpace(r["KOD"]?.ToString()?.Trim()));

                if (bezKodow > 0)
                {
                    MessageBox.Show($"Wszystkie kontakty z kodem pocztowym mają już współrzędne!\n\n{bezKodow} kontaktów nie ma kodu pocztowego.",
                        "Geokodowanie", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Wszystkie kontakty mają już współrzędne!", "Geokodowanie", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            var result = MessageBox.Show($"Znaleziono {bezWspolrzednych.Count} kontaktów bez współrzędnych.\nGeokodowanie po kodzie pocztowym.\n\nCzy kontynuować?",
                "Geokodowanie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            isLoading = true;
            loadingOverlay.Visibility = Visibility.Visible;
            btnGeokoduj.IsEnabled = false;

            int sukces = 0;
            int bledy = 0;
            int zCache = 0;

            for (int i = 0; i < bezWspolrzednych.Count; i++)
            {
                var row = bezWspolrzednych[i];
                var kod = row["KOD"]?.ToString()?.Trim() ?? "";
                var miasto = row["MIASTO"]?.ToString()?.Trim() ?? "";

                txtLoadingStatus.Text = $"Geokodowanie {i + 1}/{bezWspolrzednych.Count}: {kod} {miasto}...";

                var geo = await GeokodujPoKodziePoczAsync(kod, miasto, Convert.ToInt32(row["ID"]));
                if (geo.HasValue)
                {
                    row["Latitude"] = geo.Value.lat;
                    row["Longitude"] = geo.Value.lng;
                    sukces++;
                }
                else
                {
                    bledy++;
                }

                // Aktualizuj UI co 10 rekordów
                if (i % 10 == 0)
                {
                    await Task.Delay(1); // pozwól UI się odświeżyć
                }
            }

            loadingOverlay.Visibility = Visibility.Collapsed;
            btnGeokoduj.IsEnabled = true;
            isLoading = false;

            MessageBox.Show($"Geokodowanie zakończone!\n\nZnalezione: {sukces}\nBłędy (brak danych): {bledy}",
                "Geokodowanie", MessageBoxButton.OK, MessageBoxImage.Information);

            // Odśwież mapę - przeładuj dane z bazy
            dtKontakty = null;
            await OdswiezMapeAsync();
        }

        private async void KontaktItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MapKontakt kontakt && isWebViewReady)
            {
                try
                {
                    var lat = kontakt.Lat.ToString(CultureInfo.InvariantCulture);
                    var lng = kontakt.Lng.ToString(CultureInfo.InvariantCulture);
                    await webView.ExecuteScriptAsync($"setView({lat}, {lng}, 16);");
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
