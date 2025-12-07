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

        // Diagnostyka bazy danych - wywoływana gdy mapa jest pusta
        private (int wszystkich, int zKodem, bool tabelaIstnieje, int kodowWTabeli, int kodyZeWsp, int kodyBezWsp, int gotowyNaMape, string problem) PobierzDiagnostykeZBazy()
        {
            int wszystkich = 0, zKodem = 0, kodowWTabeli = 0, kodyZeWsp = 0, kodyBezWsp = 0, gotowyNaMape = 0;
            bool tabelaIstnieje = false;
            string problem = "";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    var cmdTabela = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = 'KodyPocztowe'", conn);
                    tabelaIstnieje = (int)cmdTabela.ExecuteScalar() > 0;

                    // Odbiorcy
                    var cmdOdbiorcy = new SqlCommand("SELECT COUNT(*) FROM OdbiorcyCRM", conn);
                    wszystkich = (int)cmdOdbiorcy.ExecuteScalar();

                    var cmdZKodem = new SqlCommand("SELECT COUNT(*) FROM OdbiorcyCRM WHERE KOD IS NOT NULL AND KOD <> ''", conn);
                    zKodem = (int)cmdZKodem.ExecuteScalar();

                    if (tabelaIstnieje)
                    {
                        var cmdKodyWTabeli = new SqlCommand("SELECT COUNT(*) FROM KodyPocztowe", conn);
                        kodowWTabeli = (int)cmdKodyWTabeli.ExecuteScalar();

                        var cmdKodyZeWsp = new SqlCommand("SELECT COUNT(*) FROM KodyPocztowe WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", conn);
                        kodyZeWsp = (int)cmdKodyZeWsp.ExecuteScalar();

                        var cmdKodyBezWsp = new SqlCommand(@"
                            SELECT COUNT(DISTINCT kp.Kod)
                            FROM KodyPocztowe kp
                            INNER JOIN OdbiorcyCRM o ON o.KOD = kp.Kod
                            WHERE kp.Latitude IS NULL OR kp.Longitude IS NULL", conn);
                        kodyBezWsp = (int)cmdKodyBezWsp.ExecuteScalar();

                        var cmdGotowy = new SqlCommand(@"
                            SELECT COUNT(*) FROM OdbiorcyCRM o
                            INNER JOIN KodyPocztowe kp ON o.KOD = kp.Kod
                            WHERE kp.Latitude IS NOT NULL AND kp.Longitude IS NOT NULL", conn);
                        gotowyNaMape = (int)cmdGotowy.ExecuteScalar();

                        // Sprawdź czy kody pasują
                        var cmdNiePasujace = new SqlCommand(@"
                            SELECT COUNT(*) FROM OdbiorcyCRM o
                            WHERE o.KOD IS NOT NULL AND o.KOD <> ''
                              AND NOT EXISTS (SELECT 1 FROM KodyPocztowe kp WHERE kp.Kod = o.KOD)", conn);
                        int niePasujace = (int)cmdNiePasujace.ExecuteScalar();

                        if (niePasujace > 0)
                            problem = $"UWAGA: {niePasujace} odbiorców ma kody, które nie istnieją w tabeli KodyPocztowe!";
                    }

                    // Ustal główny problem
                    if (!tabelaIstnieje)
                        problem = "Tabela KodyPocztowe nie istnieje! Kliknij Geokoduj aby ją utworzyć.";
                    else if (kodowWTabeli == 0)
                        problem = "Tabela KodyPocztowe jest pusta! Kliknij Geokoduj aby dodać kody.";
                    else if (kodyZeWsp == 0)
                        problem = "Żaden kod pocztowy nie ma współrzędnych! Kliknij Geokoduj.";
                    else if (gotowyNaMape == 0 && kodyZeWsp > 0)
                        problem = "Kody mają współrzędne, ale nie pasują do kodów odbiorców (sprawdź format XX-XXX)";

                    Debug.WriteLine($"[DIAG] Wszystkich: {wszystkich}, ZKodem: {zKodem}, TabelaIstnieje: {tabelaIstnieje}, " +
                        $"KodówWTabeli: {kodowWTabeli}, ZeWsp: {kodyZeWsp}, BezWsp: {kodyBezWsp}, GotowyNaMape: {gotowyNaMape}");
                }
            }
            catch (Exception ex)
            {
                problem = $"Błąd diagnostyki: {ex.Message}";
                Debug.WriteLine($"[DIAG] BŁĄD: {ex}");
            }

            return (wszystkich, zKodem, tabelaIstnieje, kodowWTabeli, kodyZeWsp, kodyBezWsp, gotowyNaMape, problem);
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

            // Jeśli brak danych - pokaż komunikat pomocniczy z diagnostyką
            if (kontakty.Count == 0)
            {
                // Pobierz diagnostykę z bazy
                var diag = PobierzDiagnostykeZBazy();
                return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #F8FAF8; }}
        .msg {{ text-align: center; padding: 40px; background: white; border-radius: 16px; box-shadow: 0 4px 20px rgba(0,0,0,0.1); max-width: 600px; }}
        h2 {{ color: #DC2626; margin-bottom: 16px; }}
        p {{ color: #666; line-height: 1.6; margin: 8px 0; }}
        .diag {{ text-align: left; background: #FEF3C7; padding: 16px; border-radius: 8px; margin: 16px 0; border-left: 4px solid #F59E0B; }}
        .diag-row {{ display: flex; justify-content: space-between; padding: 4px 0; border-bottom: 1px solid #FDE68A; }}
        .diag-label {{ color: #92400E; }}
        .diag-value {{ font-weight: bold; color: #78350F; }}
        .steps {{ text-align: left; background: #F1F5F9; padding: 16px; border-radius: 8px; margin-top: 16px; }}
        .steps li {{ margin: 8px 0; color: #374151; }}
        code {{ background: #E5E7EB; padding: 2px 6px; border-radius: 4px; font-size: 12px; }}
        .problem {{ background: #FEE2E2; color: #991B1B; padding: 8px 12px; border-radius: 6px; margin-top: 12px; font-weight: bold; }}
    </style>
</head>
<body>
<div class='msg'>
    <h2>Brak kontaktów do wyświetlenia na mapie</h2>
    <div class='diag'>
        <strong style='color:#92400E;'>DIAGNOSTYKA BAZY DANYCH:</strong>
        <div class='diag-row'><span class='diag-label'>Wszystkich odbiorców CRM:</span><span class='diag-value'>{diag.wszystkich}</span></div>
        <div class='diag-row'><span class='diag-label'>Z kodem pocztowym:</span><span class='diag-value'>{diag.zKodem}</span></div>
        <div class='diag-row'><span class='diag-label'>Tabela KodyPocztowe istnieje:</span><span class='diag-value'>{(diag.tabelaIstnieje ? "TAK" : "NIE")}</span></div>
        <div class='diag-row'><span class='diag-label'>Kodów w tabeli:</span><span class='diag-value'>{diag.kodowWTabeli}</span></div>
        <div class='diag-row'><span class='diag-label'>Kodów ze współrzędnymi:</span><span class='diag-value'>{diag.kodyZeWsp}</span></div>
        <div class='diag-row'><span class='diag-label'>Kodów BEZ współrzędnych:</span><span class='diag-value'>{diag.kodyBezWsp}</span></div>
        <div class='diag-row' style='border:none;'><span class='diag-label'>Odbiorców gotowych na mapę:</span><span class='diag-value' style='color:{(diag.gotowyNaMape > 0 ? "#16A34A" : "#DC2626")};font-size:16px;'>{diag.gotowyNaMape}</span></div>
    </div>
    {(diag.problem != "" ? $"<div class='problem'>{diag.problem}</div>" : "")}
    <div class='steps'>
        <strong>Co zrobić:</strong>
        <ol>
            <li>Kliknij przycisk <b>📍 Geokoduj</b> w nagłówku</li>
            <li>Poczekaj aż system pobierze współrzędne (limit: 50 kodów w trybie testowym)</li>
            <li>Kliknij <b>🔄 Odśwież</b> aby zobaczyć mapę</li>
        </ol>
    </div>
</div>
</body>
</html>";
            }

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body, #map {{ height: 100%; width: 100%; font-family: 'Segoe UI', sans-serif; }}
        .gm-style-iw {{ max-width: 300px !important; }}
        .p-title {{ font-weight: 700; font-size: 14px; color: #111; margin-bottom: 8px; }}
        .p-info {{ font-size: 12px; color: #666; margin: 4px 0; }}
        .p-status {{ display: inline-block; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 600; margin-top: 8px; }}
        .p-priority {{ display: inline-block; padding: 2px 6px; border-radius: 4px; font-size: 9px; font-weight: 700; background: #FEE2E2; color: #DC2626; margin-left: 6px; }}
        .p-branza {{ font-size: 10px; color: #9CA3AF; margin-top: 4px; }}
        .p-btn {{ display: inline-block; padding: 10px 14px; border-radius: 8px; font-size: 12px; font-weight: 600; text-decoration: none; margin-top: 10px; margin-right: 6px; }}
        .p-btn-green {{ background: #16A34A; color: white; }}
        .p-btn-green:hover {{ background: #15803D; }}
        .p-btn-blue {{ background: #0891B2; color: white; }}
        .p-btn-blue:hover {{ background: #0E7490; }}
        .p-btn-gray {{ background: #E5E7EB; color: #374151; }}
        .p-btn-gray:hover {{ background: #D1D5DB; }}
        .error-msg {{ display: flex; flex-direction: column; justify-content: center; align-items: center; height: 100%; font-size: 16px; color: #DC2626; text-align: center; padding: 40px; }}
        .error-msg h2 {{ margin-bottom: 16px; }}
        .error-msg p {{ color: #666; margin: 8px 0; }}
    </style>
</head>
<body>
<div id='map'></div>
<script>
var data = {dataJson};
var map, markers = [], markerObjects = [], infoWindow, clusterer = null;

var statusBg = {{'Do zadzwonienia':'#F1F5F9','Próba kontaktu':'#FFEDD5','Nawiązano kontakt':'#DCFCE7','Zgoda na dalszy kontakt':'#CCFBF1','Do wysłania oferta':'#CFFAFE','Nie zainteresowany':'#FEE2E2'}};
var statusTxt = {{'Do zadzwonienia':'#475569','Próba kontaktu':'#9A3412','Nawiązano kontakt':'#166534','Zgoda na dalszy kontakt':'#0D9488','Do wysłania oferta':'#155E75','Nie zainteresowany':'#991B1B'}};

function initMap() {{
    try {{
        map = new google.maps.Map(document.getElementById('map'), {{
            center: {{ lat: 52.0, lng: 19.0 }},
            zoom: 6,
            mapTypeControl: true,
            streetViewControl: false,
            fullscreenControl: true,
            styles: [
                {{ featureType: 'poi', elementType: 'labels', stylers: [{{ visibility: 'off' }}] }},
                {{ featureType: 'transit', stylers: [{{ visibility: 'off' }}] }}
            ]
        }});

        infoWindow = new google.maps.InfoWindow();
        var bounds = new google.maps.LatLngBounds();

        for (var i = 0; i < data.length; i++) {{
            var p = data[i];
            var pos = {{ lat: p.Lat, lng: p.Lng }};
            bounds.extend(pos);

            var sz = p.CzyPriorytetowa ? 18 : 14;
            var bw = p.CzyPriorytetowa ? 3 : 2;

            var svgIcon = {{
                url: 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns=""http://www.w3.org/2000/svg"" width=""'+sz+'"" height=""'+sz+'""><circle cx=""'+(sz/2)+'"" cy=""'+(sz/2)+'"" r=""'+((sz/2)-1)+'"" fill=""'+p.KolorHex+'"" stroke=""'+(p.CzyPriorytetowa?'#DC2626':'#333')+'"" stroke-width=""'+bw+'""/></svg>'),
                scaledSize: new google.maps.Size(sz, sz),
                anchor: new google.maps.Point(sz/2, sz/2)
            }};

            var marker = new google.maps.Marker({{
                position: pos,
                map: map,
                icon: svgIcon,
                title: p.Nazwa,
                zIndex: p.CzyPriorytetowa ? 1000 : 1
            }});

            marker.kontakt = p;
            markerObjects.push(marker);

            marker.addListener('click', function() {{
                var k = this.kontakt;
                var adr = [k.Ulica, k.Miasto].filter(Boolean).join(', ');
                var content = '<div class=""p-title"">'+k.Nazwa+(k.CzyPriorytetowa?'<span class=""p-priority"">PRIORYTET</span>':'')+'</div>'+
                    '<div class=""p-info"">📍 '+adr+'</div>'+
                    '<div class=""p-info"">📞 <b>'+k.Telefon+'</b></div>'+
                    (k.Email ? '<div class=""p-info"">✉️ '+k.Email+'</div>' : '')+
                    (k.Branza ? '<div class=""p-branza"">🏭 '+k.Branza+'</div>' : '')+
                    '<div><span class=""p-status"" style=""background:'+(statusBg[k.Status]||'#eee')+';color:'+(statusTxt[k.Status]||'#333')+'"">'+k.Status+'</span></div>'+
                    '<div style=""margin-top:12px;"">'+
                    '<a class=""p-btn p-btn-green"" href=""tel:'+k.Telefon.replace(/\s/g,'')+'"">📞 Zadzwoń</a>'+
                    '<a class=""p-btn p-btn-blue"" href=""https://www.google.com/maps/dir//'+encodeURIComponent(adr)+'"" target=""_blank"">🗺️ Nawiguj</a>'+
                    '</div>';
                infoWindow.setContent(content);
                infoWindow.open(map, this);
            }});
        }}

        if (data.length > 0) {{
            map.fitBounds(bounds);
            if (data.length === 1) map.setZoom(14);
        }}

        // Załaduj MarkerClusterer
        loadMarkerClusterer();

        console.log('Mapa załadowana pomyślnie. Punktów: ' + data.length);
    }} catch (e) {{
        console.error('Błąd inicjalizacji mapy:', e);
        document.getElementById('map').innerHTML = '<div class=""error-msg""><h2>Błąd ładowania mapy</h2><p>'+e.message+'</p></div>';
    }}
}}

function loadMarkerClusterer() {{
    var script = document.createElement('script');
    script.src = 'https://unpkg.com/@googlemaps/markerclusterer@2.5.3/dist/index.min.js';
    script.onload = function() {{
        try {{
            if (typeof markerClusterer !== 'undefined' && markerClusterer.MarkerClusterer) {{
                clusterer = new markerClusterer.MarkerClusterer({{
                    map: map,
                    markers: markerObjects,
                    algorithmOptions: {{ maxZoom: 14 }}
                }});
                console.log('MarkerClusterer załadowany');
            }}
        }} catch(e) {{
            console.warn('MarkerClusterer niedostępny:', e);
        }}
    }};
    script.onerror = function() {{
        console.warn('Nie udało się załadować MarkerClusterer - markery bez grupowania');
    }};
    document.head.appendChild(script);
}}

window.setView = function(lat, lng, z) {{
    if (map) {{
        map.setCenter({{ lat: lat, lng: lng }});
        map.setZoom(z || 15);
        // Znajdź i otwórz marker w tym miejscu
        for (var i = 0; i < markerObjects.length; i++) {{
            var pos = markerObjects[i].getPosition();
            if (Math.abs(pos.lat() - lat) < 0.0001 && Math.abs(pos.lng() - lng) < 0.0001) {{
                google.maps.event.trigger(markerObjects[i], 'click');
                break;
            }}
        }}
    }}
}};

window.gm_authFailure = function() {{
    document.getElementById('map').innerHTML = '<div class=""error-msg""><h2>Błąd autoryzacji Google Maps API</h2><p>Klucz API jest nieprawidłowy lub wygasł.</p><p>Sprawdź czy masz włączone w Google Cloud Console:</p><p><b>Maps JavaScript API</b> oraz <b>Geocoding API</b></p></div>';
}};
</script>
<script async defer src=""https://maps.googleapis.com/maps/api/js?key={apiKey}&callback=initMap"" onerror=""document.getElementById('map').innerHTML='<div class=error-msg><h2>Nie można połączyć z Google Maps</h2><p>Sprawdź połączenie internetowe i klucz API</p></div>';""></script>
</body>
</html>";
        }

        // ===== GEOKODOWANIE TABELI KODYPOCZTOWE (jednorazowe) =====
        private async void BtnGeokoduj_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;

            var apiKey = txtApiKey.Text?.Trim();
            bool uzywanieGoogleApi = !string.IsNullOrEmpty(apiKey);

            // Sprawdź ile kodów UŻYWANYCH PRZEZ ODBIORCÓW nie ma współrzędnych
            int bezWspolrzednych = 0;
            int brakWTabeli = 0;
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // Sprawdź czy tabela KodyPocztowe istnieje
                var cmdCheck = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KodyPocztowe')
                    BEGIN
                        CREATE TABLE KodyPocztowe (
                            Kod NVARCHAR(10) PRIMARY KEY,
                            miej NVARCHAR(100),
                            Latitude FLOAT,
                            Longitude FLOAT
                        );
                    END", conn);
                await cmdCheck.ExecuteNonQueryAsync();

                // Dodaj brakujące kody z OdbiorcyCRM do tabeli KodyPocztowe
                var cmdInsert = new SqlCommand(@"
                    INSERT INTO KodyPocztowe (Kod, miej)
                    SELECT DISTINCT o.KOD, o.MIASTO
                    FROM OdbiorcyCRM o
                    WHERE o.KOD IS NOT NULL AND o.KOD <> ''
                      AND NOT EXISTS (SELECT 1 FROM KodyPocztowe kp WHERE kp.Kod = o.KOD)", conn);
                brakWTabeli = await cmdInsert.ExecuteNonQueryAsync();

                // Policz ile kodów nie ma współrzędnych
                var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT kp.Kod)
                    FROM KodyPocztowe kp
                    INNER JOIN OdbiorcyCRM o ON o.KOD = kp.Kod
                    WHERE kp.Latitude IS NULL OR kp.Longitude IS NULL", conn);
                bezWspolrzednych = (int)await cmd.ExecuteScalarAsync();
            }

            if (bezWspolrzednych == 0)
            {
                MessageBox.Show("Wszystkie kody pocztowe używane przez odbiorców mają już współrzędne!\n\nKliknij 'Odśwież' aby zobaczyć mapę.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string msg;
            if (uzywanieGoogleApi)
            {
                msg = $"Znaleziono {bezWspolrzednych} kodów pocztowych bez współrzędnych.\n";
                if (brakWTabeli > 0) msg += $"(Dodano {brakWTabeli} nowych kodów do tabeli)\n\n";
                msg += "Wybrany sposób: Google Geocoding API (szybko, ~50 req/s)\n";
                msg += "Koszt: ~$5 za 1000 kodów (pierwsze $200/mies. gratis)\n\n";
                msg += "Kontynuować?";
            }
            else
            {
                msg = $"Znaleziono {bezWspolrzednych} kodów pocztowych bez współrzędnych.\n";
                if (brakWTabeli > 0) msg += $"(Dodano {brakWTabeli} nowych kodów do tabeli)\n\n";
                msg += "Wybrany sposób: Nominatim (DARMOWE, ale wolniej ~1 req/s)\n";
                msg += "Brak klucza Google API - używam darmowej usługi OpenStreetMap.\n\n";
                msg += $"Szacowany czas: ~{bezWspolrzednych} sekund ({bezWspolrzednych / 60} min)\n\n";
                msg += "Kontynuować?";
            }

            var result = MessageBox.Show(msg, "Uzupełnij współrzędne kodów pocztowych", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            isLoading = true;
            loadingOverlay.Visibility = Visibility.Visible;
            btnGeokoduj.IsEnabled = false;

            int sukces = 0, bledy = 0;
            var listaZgeokodowanych = new List<string>();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // LIMIT 50 - tryb testowy, zmień na więcej po potwierdzeniu działania
                    var cmdSelect = new SqlCommand(@"
                        SELECT TOP 50 kp.Kod, kp.miej, COUNT(*) as Ile
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

                    Debug.WriteLine($"[MAPA] Znaleziono {kodyDoGeokodowania.Count} kodów do geokodowania");

                    for (int i = 0; i < kodyDoGeokodowania.Count; i++)
                    {
                        var (kod, miasto) = kodyDoGeokodowania[i];
                        txtLoadingStatus.Text = $"Geokodowanie {i + 1}/{kodyDoGeokodowania.Count}: {kod} {miasto}..." +
                            (uzywanieGoogleApi ? "" : " (darmowe API)");

                        // Użyj Google API lub darmowego Nominatim
                        var coords = uzywanieGoogleApi
                            ? await GeokodujKodGoogleAsync(kod, miasto, apiKey)
                            : await GeokodujKodNominatimAsync(kod, miasto);

                        if (coords.HasValue)
                        {
                            var cmdUpdate = new SqlCommand(
                                "UPDATE KodyPocztowe SET Latitude = @lat, Longitude = @lng WHERE Kod = @kod", conn);
                            cmdUpdate.Parameters.AddWithValue("@lat", coords.Value.lat);
                            cmdUpdate.Parameters.AddWithValue("@lng", coords.Value.lng);
                            cmdUpdate.Parameters.AddWithValue("@kod", kod);
                            await cmdUpdate.ExecuteNonQueryAsync();
                            sukces++;
                            listaZgeokodowanych.Add($"{kod} -> {coords.Value.lat:F4}, {coords.Value.lng:F4}");
                            Debug.WriteLine($"[MAPA] OK: {kod} ({miasto}) -> {coords.Value.lat}, {coords.Value.lng}");
                        }
                        else
                        {
                            bledy++;
                            Debug.WriteLine($"[MAPA] BŁĄD: {kod} ({miasto}) - nie znaleziono współrzędnych");
                        }

                        // Google: 50 req/s (25ms), Nominatim: 1 req/s (1100ms)
                        await Task.Delay(uzywanieGoogleApi ? 25 : 1100);
                    }

                    // Diagnostyka - ile odbiorców teraz może być na mapie
                    txtLoadingStatus.Text = "Sprawdzanie wyników...";
                    var cmdDiag = new SqlCommand(@"
                        SELECT
                            (SELECT COUNT(*) FROM OdbiorcyCRM) as Wszystkich,
                            (SELECT COUNT(*) FROM OdbiorcyCRM WHERE KOD IS NOT NULL AND KOD <> '') as ZKodem,
                            (SELECT COUNT(*) FROM KodyPocztowe WHERE Latitude IS NOT NULL) as KodyZeWsp,
                            (SELECT COUNT(*) FROM OdbiorcyCRM o
                             INNER JOIN KodyPocztowe kp ON o.KOD = kp.Kod
                             WHERE kp.Latitude IS NOT NULL) as GotowyNaMape", conn);

                    using (var reader = await cmdDiag.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var wszystkich = reader.GetInt32(0);
                            var zKodem = reader.GetInt32(1);
                            var kodyZeWsp = reader.GetInt32(2);
                            var gotowyNaMape = reader.GetInt32(3);

                            var metodaNazwa = uzywanieGoogleApi ? "Google" : "Nominatim (darmowe)";
                            var diagnostyka = $"=== DIAGNOSTYKA MAPY ===\n\n" +
                                $"Geokodowanie ({metodaNazwa}):\n" +
                                $"  Sukces: {sukces}\n" +
                                $"  Błędy: {bledy}\n\n" +
                                $"Stan bazy danych:\n" +
                                $"  Wszystkich odbiorców: {wszystkich}\n" +
                                $"  Z kodem pocztowym: {zKodem}\n" +
                                $"  Kodów ze współrzędnymi: {kodyZeWsp}\n" +
                                $"  GOTOWYCH NA MAPĘ: {gotowyNaMape}\n\n";

                            if (gotowyNaMape > 0)
                            {
                                diagnostyka += $"Mapa powinna pokazać {gotowyNaMape} kontaktów.\n" +
                                    $"Kliknij 'Odśwież' aby zobaczyć mapę.";
                            }
                            else
                            {
                                diagnostyka += "UWAGA: Żaden odbiorca nie jest gotowy do wyświetlenia!\n" +
                                    "Możliwe przyczyny:\n" +
                                    "1. Kody pocztowe w OdbiorcyCRM nie pasują do KodyPocztowe\n" +
                                    "2. Geokodowanie nie znalazło współrzędnych\n" +
                                    "3. Format kodów jest inny (np. '00001' vs '00-001')";
                            }

                            if (sukces > 0 && listaZgeokodowanych.Count <= 10)
                            {
                                diagnostyka += $"\n\nPrzykładowe zgeokodowane:\n" + string.Join("\n", listaZgeokodowanych.Take(10));
                            }

                            MessageBox.Show(diagnostyka, "Wynik geokodowania", MessageBoxButton.OK,
                                gotowyNaMape > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAPA] WYJĄTEK: {ex}");
                MessageBox.Show($"Błąd: {ex.Message}\n\nSzczegóły: {ex}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            loadingOverlay.Visibility = Visibility.Collapsed;
            btnGeokoduj.IsEnabled = true;
            isLoading = false;

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

        // DARMOWE geokodowanie przez Nominatim (OpenStreetMap)
        private async Task<(double lat, double lng)?> GeokodujKodNominatimAsync(string kod, string miasto)
        {
            try
            {
                // Nominatim wymaga User-Agent
                if (!http.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    http.DefaultRequestHeaders.Add("User-Agent", "CRMMapaKontaktow/1.0");
                }

                // Format zapytania dla Nominatim
                var query = !string.IsNullOrEmpty(miasto)
                    ? $"{kod}, {miasto}, Poland"
                    : $"{kod}, Poland";

                var url = $"https://nominatim.openstreetmap.org/search?q={HttpUtility.UrlEncode(query)}&format=json&countrycodes=pl&limit=1";

                using (var resp = await http.GetAsync(url))
                {
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            if (root.GetArrayLength() > 0)
                            {
                                var first = root[0];
                                if (first.TryGetProperty("lat", out var latProp) && first.TryGetProperty("lon", out var lonProp))
                                {
                                    if (double.TryParse(latProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                                        double.TryParse(lonProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng))
                                    {
                                        return (lat, lng);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Nominatim Geocode error for {kod}: {ex.Message}");
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
