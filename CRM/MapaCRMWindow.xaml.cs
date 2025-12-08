using Microsoft.Data.SqlClient;
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
                webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    try
                    {
                        var json = e.WebMessageAsJson;
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            string action = root.GetProperty("action").GetString();
                            int id = root.GetProperty("id").GetInt32();

                            if (action == "zmienStatus")
                            {
                                string nowyStatus = root.GetProperty("status").GetString();
                                ZmienStatusOdbiorcy(id, nowyStatus);
                            }
                            else if (action == "dodajNotatke")
                            {
                                string tresc = root.GetProperty("tresc").GetString();
                                DodajNotatkeOdbiorcy(id, tresc);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"WebMessage error: {ex.Message}");
                    }
                };
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
                        o.Tagi,
                        ISNULL(o.Status, 'Do zadzwonienia') as Status,
                        CASE WHEN pb.PKD_Opis IS NOT NULL THEN 1 ELSE 0 END as CzyPriorytetowa,
                        kp.Latitude,
                        kp.Longitude,
                        (SELECT TOP 1 ISNULL(op.Name, h.KtoWykonal) FROM HistoriaZmianCRM h LEFT JOIN operators op ON h.KtoWykonal = CAST(op.ID AS NVARCHAR) WHERE h.IDOdbiorcy = o.ID ORDER BY h.DataZmiany DESC) as Handlowiec,
                        (SELECT STRING_AGG(CONCAT(FORMAT(n.DataDodania, 'dd.MM'), '|', ISNULL(op.Name, 'System'), '|', LEFT(n.Tresc, 100)), ';;;') WITHIN GROUP (ORDER BY n.DataDodania DESC)
                         FROM (SELECT TOP 3 * FROM NotatkiCRM WHERE IDOdbiorcy = o.ID ORDER BY DataDodania DESC) n
                         LEFT JOIN operators op ON n.KtoDodal = CAST(op.ID AS NVARCHAR)) as Notatki
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
                    Handlowiec = row["Handlowiec"]?.ToString() ?? "",
                    Tagi = row["Tagi"]?.ToString() ?? "",
                    Notatki = row["Notatki"]?.ToString() ?? "",
                    Status = status,
                    CzyPriorytetowa = czyPriorytetowa,
                    Lat = lat,
                    Lng = lng,
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
                k.Wojewodztwo,
                k.Status,
                k.Branza,
                k.Handlowiec,
                k.CzyPriorytetowa,
                Tagi = k.Tagi ?? "",
                Notatki = k.Notatki ?? "",
                k.KolorHex,
                k.Lat,
                k.Lng,
                k.DystansKm
            }), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='utf-8'/>");
            sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'/>");
            sb.AppendLine("<style>");
            sb.AppendLine("html, body, #map { height: 100%; width: 100%; margin: 0; padding: 0; font-family: 'Segoe UI', sans-serif; overflow: hidden; }");
            sb.AppendLine(".gm-style-iw { max-width: 360px !important; font-family: 'Segoe UI', sans-serif; }");
            sb.AppendLine(".gm-style-iw-d { overflow: hidden !important; }");
            sb.AppendLine(".popup-card { padding: 4px; }");
            sb.AppendLine(".p-header { background: linear-gradient(135deg, #16A34A 0%, #22C55E 100%); color: white; padding: 12px 14px; border-radius: 8px 8px 0 0; margin: -12px -12px 12px -12px; position: relative; }");
            sb.AppendLine(".p-header-top { display: flex; align-items: flex-start; gap: 10px; }");
            sb.AppendLine(".p-avatar { width: 40px; height: 40px; background: rgba(255,255,255,0.2); border-radius: 8px; display: flex; align-items: center; justify-content: center; font-size: 20px; }");
            sb.AppendLine(".p-header-info { flex: 1; }");
            sb.AppendLine(".p-title { font-weight: 700; font-size: 14px; margin: 0; line-height: 1.3; }");
            sb.AppendLine(".p-subtitle { font-size: 11px; opacity: 0.9; margin-top: 2px; }");
            sb.AppendLine(".p-badges { display: flex; gap: 4px; margin-top: 6px; flex-wrap: wrap; }");
            sb.AppendLine(".p-badge { font-size: 9px; padding: 2px 6px; border-radius: 4px; font-weight: 700; }");
            sb.AppendLine(".badge-vip { background: #FEF3C7; color: #92400E; }");
            sb.AppendLine(".badge-pilne { background: #FEE2E2; color: #991B1B; }");
            sb.AppendLine(".badge-premium { background: #E0E7FF; color: #3730A3; }");
            sb.AppendLine(".badge-priorytet { background: #DCFCE7; color: #166534; }");
            sb.AppendLine(".p-body { padding: 0 2px; }");
            sb.AppendLine(".p-row { display: flex; align-items: center; padding: 6px 0; border-bottom: 1px solid #F1F5F9; }");
            sb.AppendLine(".p-row:last-child { border-bottom: none; }");
            sb.AppendLine(".p-icon { width: 24px; text-align: center; font-size: 14px; }");
            sb.AppendLine(".p-label { font-size: 10px; color: #64748B; text-transform: uppercase; font-weight: 600; }");
            sb.AppendLine(".p-value { font-size: 12px; color: #1E293B; font-weight: 500; display: flex; align-items: center; gap: 6px; }");
            sb.AppendLine(".copy-btn { background: #F1F5F9; border: none; padding: 3px 6px; border-radius: 4px; cursor: pointer; font-size: 10px; transition: all 0.2s; }");
            sb.AppendLine(".copy-btn:hover { background: #E2E8F0; }");
            sb.AppendLine(".p-section { margin-top: 10px; padding-top: 10px; border-top: 2px solid #F1F5F9; }");
            sb.AppendLine(".p-section-title { font-size: 10px; color: #64748B; text-transform: uppercase; font-weight: 700; margin-bottom: 8px; letter-spacing: 0.5px; }");
            sb.AppendLine(".status-grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 4px; }");
            sb.AppendLine(".s-btn { padding: 8px 4px; border: none; border-radius: 6px; cursor: pointer; font-size: 14px; transition: all 0.2s; display: flex; flex-direction: column; align-items: center; gap: 2px; }");
            sb.AppendLine(".s-btn:hover { transform: scale(1.05); box-shadow: 0 2px 8px rgba(0,0,0,0.15); }");
            sb.AppendLine(".s-btn span { font-size: 8px; font-weight: 600; }");
            sb.AppendLine(".quick-note { display: flex; gap: 6px; margin-top: 8px; }");
            sb.AppendLine(".quick-note input { flex: 1; border: 1px solid #E2E8F0; border-radius: 6px; padding: 8px 10px; font-size: 11px; }");
            sb.AppendLine(".quick-note button { background: #16A34A; color: white; border: none; border-radius: 6px; padding: 8px 12px; cursor: pointer; font-size: 11px; font-weight: 600; }");
            // Notes history styles
            sb.AppendLine(".notes-list { max-height: 120px; overflow-y: auto; margin-top: 6px; }");
            sb.AppendLine(".note-item { background: #F8FAFC; border-radius: 6px; padding: 8px 10px; margin-bottom: 6px; border-left: 3px solid #16A34A; }");
            sb.AppendLine(".note-header { display: flex; justify-content: space-between; margin-bottom: 4px; }");
            sb.AppendLine(".note-date { font-size: 9px; color: #64748B; font-weight: 600; }");
            sb.AppendLine(".note-author { font-size: 9px; color: #6366F1; font-weight: 600; }");
            sb.AppendLine(".note-text { font-size: 11px; color: #334155; line-height: 1.4; }");
            sb.AppendLine(".btn-row { display: flex; gap: 8px; margin-top: 12px; }");
            sb.AppendLine(".p-btn { flex: 1; text-align: center; padding: 10px 8px; color: white; text-decoration: none; border-radius: 6px; font-size: 12px; font-weight: 600; border: none; cursor: pointer; transition: all 0.2s; }");
            sb.AppendLine(".p-btn:hover { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0,0,0,0.15); }");
            sb.AppendLine(".btn-route { background: linear-gradient(135deg, #0F172A 0%, #1E293B 100%); }");
            sb.AppendLine(".btn-call { background: linear-gradient(135deg, #16A34A 0%, #22C55E 100%); }");
            sb.AppendLine("#map-toast { position: fixed; bottom: 30px; left: 50%; transform: translateX(-50%) translateY(100px); background: #1F2937; color: white; padding: 12px 20px; border-radius: 30px; font-size: 13px; font-weight: 600; z-index: 9999; transition: transform 0.3s ease; display: flex; align-items: center; gap: 10px; box-shadow: 0 4px 15px rgba(0,0,0,0.2); }");
            sb.AppendLine("#map-toast.show { transform: translateX(-50%) translateY(0); }");
            sb.AppendLine("#map-toast .undo-btn { background: #16A34A; border: none; color: white; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 11px; }");
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
            // Konfetti CSS
            sb.AppendLine("#confetti-container { position: fixed; top: 0; left: 0; width: 100%; height: 100%; pointer-events: none; z-index: 99999; overflow: hidden; }");
            sb.AppendLine(".confetti { position: absolute; width: 10px; height: 10px; opacity: 0; animation: confetti-fall 3s ease-out forwards; }");
            sb.AppendLine("@keyframes confetti-fall { 0% { transform: translateY(-100px) rotate(0deg); opacity: 1; } 100% { transform: translateY(100vh) rotate(720deg); opacity: 0; } }");
            // Cluster info tooltip
            sb.AppendLine("#cluster-tooltip { position: fixed; background: white; padding: 12px 16px; border-radius: 10px; box-shadow: 0 4px 20px rgba(0,0,0,0.2); z-index: 10000; pointer-events: none; display: none; font-size: 12px; min-width: 180px; }");
            sb.AppendLine("#cluster-tooltip .ct-title { font-weight: 700; color: #16A34A; margin-bottom: 8px; font-size: 13px; }");
            sb.AppendLine("#cluster-tooltip .ct-row { display: flex; justify-content: space-between; padding: 3px 0; border-bottom: 1px solid #F1F5F9; }");
            sb.AppendLine("#cluster-tooltip .ct-row:last-child { border-bottom: none; }");
            sb.AppendLine("#cluster-tooltip .ct-label { color: #64748B; }");
            sb.AppendLine("#cluster-tooltip .ct-value { font-weight: 600; color: #1E293B; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div id='map'></div>");
            sb.AppendLine("<div id='confetti-container'></div>");
            sb.AppendLine("<div id='cluster-tooltip'></div>");
            sb.AppendLine("<div id='map-toast'><span id='toast-msg'></span><button class='undo-btn' onclick='cofnijStatus()'>Cofnij</button></div>");
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
            sb.AppendLine("    markers.push(marker);");
            sb.AppendLine("    marker.addListener('click', function() {");
            sb.AppendLine("      var k = this.kontakt;");
            sb.AppendLine("      var avatarIcon = k.Branza ? (k.Branza.includes('Produkcja') ? '🏭' : k.Branza.includes('Handel') ? '🏪' : k.Branza.includes('Usługi') ? '🔧' : '🏢') : '🏢';");
            sb.AppendLine("      var badges = '';");
            sb.AppendLine("      if(k.Tagi) {");
            sb.AppendLine("        if(k.Tagi.includes('VIP')) badges += '<span class=\"p-badge badge-vip\">⭐ VIP</span>';");
            sb.AppendLine("        if(k.Tagi.includes('Pilne')) badges += '<span class=\"p-badge badge-pilne\">🔥 PILNE</span>';");
            sb.AppendLine("        if(k.Tagi.includes('Premium')) badges += '<span class=\"p-badge badge-premium\">💎 PREMIUM</span>';");
            sb.AppendLine("      }");
            sb.AppendLine("      if(k.CzyPriorytetowa) badges += '<span class=\"p-badge badge-priorytet\">✓ PRIORYTET</span>';");
            sb.AppendLine("      var content = '<div class=\"popup-card\">' +");
            sb.AppendLine("        '<div class=\"p-header\">' +");
            sb.AppendLine("          '<div class=\"p-header-top\">' +");
            sb.AppendLine("            '<div class=\"p-avatar\">' + avatarIcon + '</div>' +");
            sb.AppendLine("            '<div class=\"p-header-info\">' +");
            sb.AppendLine("              '<div class=\"p-title\">' + k.Nazwa + '</div>' +");
            sb.AppendLine("              '<div class=\"p-subtitle\">📍 ' + k.Miasto + ' • ' + k.DystansKm + ' km</div>' +");
            sb.AppendLine("              (badges ? '<div class=\"p-badges\">' + badges + '</div>' : '') +");
            sb.AppendLine("            '</div>' +");
            sb.AppendLine("          '</div>' +");
            sb.AppendLine("        '</div>' +");
            sb.AppendLine("        '<div class=\"p-body\">' +");
            sb.AppendLine("          (k.Wojewodztwo ? '<div class=\"p-row\"><span class=\"p-icon\">🗺️</span><div><div class=\"p-label\">Województwo</div><div class=\"p-value\">' + k.Wojewodztwo + '</div></div></div>' : '') +");
            sb.AppendLine("          (k.Branza ? '<div class=\"p-row\"><span class=\"p-icon\">🏭</span><div><div class=\"p-label\">Branża</div><div class=\"p-value\">' + k.Branza + '</div></div></div>' : '') +");
            sb.AppendLine("          (k.Handlowiec ? '<div class=\"p-row\"><span class=\"p-icon\">👤</span><div><div class=\"p-label\">Opiekun</div><div class=\"p-value\" style=\"color:#6366F1;font-weight:600\">' + k.Handlowiec + '</div></div></div>' : '') +");
            sb.AppendLine("          '<div class=\"p-row\"><span class=\"p-icon\">📞</span><div><div class=\"p-label\">Telefon</div><div class=\"p-value\">' + k.Telefon + '<button class=\"copy-btn\" onclick=\"kopiuj(\\x27' + k.Telefon + '\\x27)\">📋</button></div></div></div>' +");
            sb.AppendLine("          '<div class=\"p-row\"><span class=\"p-icon\">📋</span><div><div class=\"p-label\">Status</div><div class=\"p-value\">' + k.Status + '</div></div></div>' +");
            sb.AppendLine("          '<div class=\"p-section\">' +");
            sb.AppendLine("            '<div class=\"p-section-title\">Zmień status</div>' +");
            sb.AppendLine("            '<div class=\"status-grid\">' +");
            sb.AppendLine("              '<button class=\"s-btn\" style=\"background:#FFEDD5;color:#9A3412\" onclick=\"zmienStatus(' + k.ID + ',\\x27Próba kontaktu\\x27)\" title=\"Próba kontaktu\">⏳<span>Próba</span></button>' +");
            sb.AppendLine("              '<button class=\"s-btn\" style=\"background:#DCFCE7;color:#166534\" onclick=\"zmienStatus(' + k.ID + ',\\x27Nawiązano kontakt\\x27)\" title=\"Nawiązano kontakt\">✅<span>Kontakt</span></button>' +");
            sb.AppendLine("              '<button class=\"s-btn\" style=\"background:#CCFBF1;color:#0D9488\" onclick=\"zmienStatus(' + k.ID + ',\\x27Zgoda na dalszy kontakt\\x27)\" title=\"Zgoda na dalszy kontakt\">🤝<span>Zgoda</span></button>' +");
            sb.AppendLine("              '<button class=\"s-btn\" style=\"background:#DBEAFE;color:#1E40AF\" onclick=\"zmienStatus(' + k.ID + ',\\x27Do wysłania oferta\\x27)\" title=\"Do wysłania oferta\">📄<span>Oferta</span></button>' +");
            sb.AppendLine("              '<button class=\"s-btn\" style=\"background:#FEE2E2;color:#991B1B\" onclick=\"zmienStatus(' + k.ID + ',\\x27Nie zainteresowany\\x27)\" title=\"Nie zainteresowany\">❌<span>Odmowa</span></button>' +");
            sb.AppendLine("            '</div>' +");
            sb.AppendLine("          '</div>' +");
            sb.AppendLine("          '<div class=\"p-section\">' +");
            sb.AppendLine("            '<div class=\"p-section-title\">📝 Notatki</div>' +");
            sb.AppendLine("            '<div class=\"quick-note\">' +");
            sb.AppendLine("              '<input type=\"text\" id=\"qnote-' + k.ID + '\" placeholder=\"Dodaj nową notatkę...\">' +");
            sb.AppendLine("              '<button onclick=\"dodajNotatke(' + k.ID + ')\">+</button>' +");
            sb.AppendLine("            '</div>' +");
            sb.AppendLine("            (k.Notatki ? '<div class=\"notes-list\">' + renderNotatki(k.Notatki) + '</div>' : '<div style=\"font-size:10px;color:#94A3B8;margin-top:6px;text-align:center\">Brak notatek</div>') +");
            sb.AppendLine("          '</div>' +");
            sb.AppendLine("          '<div class=\"btn-row\">' +");
            sb.AppendLine("            '<button class=\"p-btn btn-route\" onclick=\"addToRoute(' + k.ID + ')\">🗺️ Do trasy</button>' +");
            sb.AppendLine("            '<a class=\"p-btn btn-call\" href=\"tel:' + k.Telefon + '\">📞 Zadzwoń</a>' +");
            sb.AppendLine("          '</div>' +");
            sb.AppendLine("        '</div>' +");
            sb.AppendLine("      '</div>';");
            sb.AppendLine("      infoWindow.setContent(content);");
            sb.AppendLine("      infoWindow.open(map, this);");
            sb.AppendLine("    });");
            sb.AppendLine("  }");
            sb.AppendLine("  if (data.length > 0) map.fitBounds(bounds);");
            sb.AppendLine("  var script = document.createElement('script');");
            sb.AppendLine("  script.src = 'https://unpkg.com/@googlemaps/markerclusterer@2.5.3/dist/index.min.js';");
            sb.AppendLine("  script.onload = function() {");
            sb.AppendLine("    try {");
            sb.AppendLine("      var cluster = new markerClusterer.MarkerClusterer({ map: map, markers: markers });");
            sb.AppendLine("      cluster.addListener('clusteringend', function() {");
            sb.AppendLine("        cluster.clusters.forEach(function(c) {");
            sb.AppendLine("          var clusterMarker = c.marker;");
            sb.AppendLine("          if(!clusterMarker._hoverAdded) {");
            sb.AppendLine("            clusterMarker._hoverAdded = true;");
            sb.AppendLine("            clusterMarker.addListener('mouseover', function(e) {");
            sb.AppendLine("              var pts = c.markers.map(m => m.kontakt).filter(k => k);");
            sb.AppendLine("              if(pts.length === 0) return;");
            sb.AppendLine("              var stats = { total: pts.length, doZadzw: 0, proba: 0, kontakt: 0, zgoda: 0, oferta: 0, nie: 0, priorytet: 0 };");
            sb.AppendLine("              pts.forEach(function(p) {");
            sb.AppendLine("                if(p.Status === 'Do zadzwonienia') stats.doZadzw++;");
            sb.AppendLine("                else if(p.Status === 'Próba kontaktu') stats.proba++;");
            sb.AppendLine("                else if(p.Status === 'Nawiązano kontakt') stats.kontakt++;");
            sb.AppendLine("                else if(p.Status === 'Zgoda na dalszy kontakt') stats.zgoda++;");
            sb.AppendLine("                else if(p.Status === 'Do wysłania oferta') stats.oferta++;");
            sb.AppendLine("                else if(p.Status === 'Nie zainteresowany') stats.nie++;");
            sb.AppendLine("                if(p.CzyPriorytetowa) stats.priorytet++;");
            sb.AppendLine("              });");
            sb.AppendLine("              var html = '<div class=\"ct-title\">📍 ' + stats.total + ' kontaktów</div>';");
            sb.AppendLine("              html += '<div class=\"ct-row\"><span class=\"ct-label\">📞 Do zadzwonienia</span><span class=\"ct-value\">' + stats.doZadzw + '</span></div>';");
            sb.AppendLine("              html += '<div class=\"ct-row\"><span class=\"ct-label\">⏳ Próba kontaktu</span><span class=\"ct-value\">' + stats.proba + '</span></div>';");
            sb.AppendLine("              html += '<div class=\"ct-row\"><span class=\"ct-label\">✅ Nawiązano</span><span class=\"ct-value\">' + stats.kontakt + '</span></div>';");
            sb.AppendLine("              html += '<div class=\"ct-row\"><span class=\"ct-label\">🤝 Zgoda</span><span class=\"ct-value\">' + stats.zgoda + '</span></div>';");
            sb.AppendLine("              html += '<div class=\"ct-row\"><span class=\"ct-label\">📄 Do oferty</span><span class=\"ct-value\">' + stats.oferta + '</span></div>';");
            sb.AppendLine("              html += '<div class=\"ct-row\"><span class=\"ct-label\">⭐ Priorytet</span><span class=\"ct-value\" style=\"color:#DC2626\">' + stats.priorytet + '</span></div>';");
            sb.AppendLine("              var tooltip = document.getElementById('cluster-tooltip');");
            sb.AppendLine("              tooltip.innerHTML = html;");
            sb.AppendLine("              tooltip.style.display = 'block';");
            sb.AppendLine("              tooltip.style.left = (e.domEvent.clientX + 15) + 'px';");
            sb.AppendLine("              tooltip.style.top = (e.domEvent.clientY + 15) + 'px';");
            sb.AppendLine("            });");
            sb.AppendLine("            clusterMarker.addListener('mouseout', function() {");
            sb.AppendLine("              document.getElementById('cluster-tooltip').style.display = 'none';");
            sb.AppendLine("            });");
            sb.AppendLine("          }");
            sb.AppendLine("        });");
            sb.AppendLine("      });");
            sb.AppendLine("    } catch(e) { console.log('Cluster error:', e); }");
            sb.AppendLine("  };");
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
            sb.AppendLine("    var branzaTxt = p.Branza ? '<br><span style=\"font-size:10px;color:#666\">🏭 ' + p.Branza + '</span>' : '';");
            sb.AppendLine("    var handlowiecTxt = p.Handlowiec ? '<br><span style=\"font-size:10px;color:#6366F1;font-weight:bold\">👤 ' + p.Handlowiec + '</span>' : '';");
            sb.AppendLine("    html += '<div class=\"rp-item\">' +");
            sb.AppendLine("            '<div class=\"rp-remove\" onclick=\"removeFromRoute(' + p.ID + ')\">✕</div>' +");
            sb.AppendLine("            '<b>' + (i+1) + '. ' + p.Nazwa + '</b><br>' + p.Miasto + ' (' + p.DystansKm + ' km)' + branzaTxt + handlowiecTxt + '</div>';");
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
            sb.AppendLine("var lastStatusChange = null;");
            sb.AppendLine("function zmienStatus(id, nowyStatus) {");
            sb.AppendLine("  var k = data.find(x => x.ID == id);");
            sb.AppendLine("  if(k) { lastStatusChange = { id: id, oldStatus: k.Status, newStatus: nowyStatus }; k.Status = nowyStatus; }");
            sb.AppendLine("  window.chrome.webview.postMessage(JSON.stringify({ action: 'zmienStatus', id: id, status: nowyStatus }));");
            sb.AppendLine("  infoWindow.close();");
            sb.AppendLine("  showToast('✅ Status: ' + nowyStatus);");
            sb.AppendLine("  if(nowyStatus === 'Nawiązano kontakt' || nowyStatus === 'Zgoda na dalszy kontakt') { showConfetti(); playSound('success'); }");
            sb.AppendLine("  else { playSound('click'); }");
            sb.AppendLine("}");
            sb.AppendLine("function cofnijStatus() {");
            sb.AppendLine("  if(!lastStatusChange) return;");
            sb.AppendLine("  var k = data.find(x => x.ID == lastStatusChange.id);");
            sb.AppendLine("  if(k) k.Status = lastStatusChange.oldStatus;");
            sb.AppendLine("  window.chrome.webview.postMessage(JSON.stringify({ action: 'zmienStatus', id: lastStatusChange.id, status: lastStatusChange.oldStatus }));");
            sb.AppendLine("  hideToast();");
            sb.AppendLine("  lastStatusChange = null;");
            sb.AppendLine("}");
            sb.AppendLine("function showToast(msg) {");
            sb.AppendLine("  document.getElementById('toast-msg').innerText = msg;");
            sb.AppendLine("  document.getElementById('map-toast').classList.add('show');");
            sb.AppendLine("  setTimeout(function() { hideToast(); }, 5000);");
            sb.AppendLine("}");
            sb.AppendLine("function hideToast() { document.getElementById('map-toast').classList.remove('show'); }");
            sb.AppendLine("function renderNotatki(notatkiStr) {");
            sb.AppendLine("  if(!notatkiStr) return '';");
            sb.AppendLine("  var html = '';");
            sb.AppendLine("  var notatki = notatkiStr.split(';;;');");
            sb.AppendLine("  for(var i = 0; i < notatki.length; i++) {");
            sb.AppendLine("    var parts = notatki[i].split('|');");
            sb.AppendLine("    if(parts.length >= 3) {");
            sb.AppendLine("      html += '<div class=\"note-item\">';");
            sb.AppendLine("      html += '<div class=\"note-header\"><span class=\"note-date\">📅 ' + parts[0] + '</span><span class=\"note-author\">👤 ' + parts[1] + '</span></div>';");
            sb.AppendLine("      html += '<div class=\"note-text\">' + parts[2] + '</div>';");
            sb.AppendLine("      html += '</div>';");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("  return html;");
            sb.AppendLine("}");
            sb.AppendLine("function kopiuj(tekst) {");
            sb.AppendLine("  navigator.clipboard.writeText(tekst).then(function() { showToast('📋 Skopiowano: ' + tekst); });");
            sb.AppendLine("}");
            sb.AppendLine("function dodajNotatke(id) {");
            sb.AppendLine("  var input = document.getElementById('qnote-' + id);");
            sb.AppendLine("  if(!input || !input.value.trim()) return;");
            sb.AppendLine("  window.chrome.webview.postMessage(JSON.stringify({ action: 'dodajNotatke', id: id, tresc: input.value.trim() }));");
            sb.AppendLine("  input.value = '';");
            sb.AppendLine("  showToast('📝 Notatka dodana!');");
            sb.AppendLine("  playSound('note');");
            sb.AppendLine("  infoWindow.close();");
            sb.AppendLine("}");
            // Sound effects
            sb.AppendLine("var audioCtx = null;");
            sb.AppendLine("function playSound(type) {");
            sb.AppendLine("  try {");
            sb.AppendLine("    if(!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();");
            sb.AppendLine("    var osc = audioCtx.createOscillator();");
            sb.AppendLine("    var gain = audioCtx.createGain();");
            sb.AppendLine("    osc.connect(gain);");
            sb.AppendLine("    gain.connect(audioCtx.destination);");
            sb.AppendLine("    gain.gain.value = 0.1;");
            sb.AppendLine("    if(type === 'success') { osc.frequency.value = 880; osc.type = 'sine'; }");
            sb.AppendLine("    else if(type === 'click') { osc.frequency.value = 600; osc.type = 'square'; gain.gain.value = 0.05; }");
            sb.AppendLine("    else if(type === 'note') { osc.frequency.value = 523; osc.type = 'triangle'; }");
            sb.AppendLine("    else if(type === 'error') { osc.frequency.value = 200; osc.type = 'sawtooth'; }");
            sb.AppendLine("    else { osc.frequency.value = 440; osc.type = 'sine'; }");
            sb.AppendLine("    osc.start();");
            sb.AppendLine("    gain.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.15);");
            sb.AppendLine("    osc.stop(audioCtx.currentTime + 0.15);");
            sb.AppendLine("  } catch(e) {}");
            sb.AppendLine("}");
            // Confetti
            sb.AppendLine("function showConfetti() {");
            sb.AppendLine("  var container = document.getElementById('confetti-container');");
            sb.AppendLine("  var colors = ['#22C55E', '#16A34A', '#FCD34D', '#F97316', '#3B82F6', '#8B5CF6', '#EC4899'];");
            sb.AppendLine("  for(var i = 0; i < 80; i++) {");
            sb.AppendLine("    var confetti = document.createElement('div');");
            sb.AppendLine("    confetti.className = 'confetti';");
            sb.AppendLine("    confetti.style.left = Math.random() * 100 + '%';");
            sb.AppendLine("    confetti.style.background = colors[Math.floor(Math.random() * colors.length)];");
            sb.AppendLine("    confetti.style.animationDelay = Math.random() * 0.5 + 's';");
            sb.AppendLine("    confetti.style.animationDuration = (2 + Math.random() * 2) + 's';");
            sb.AppendLine("    var size = 5 + Math.random() * 10;");
            sb.AppendLine("    confetti.style.width = size + 'px';");
            sb.AppendLine("    confetti.style.height = size + 'px';");
            sb.AppendLine("    confetti.style.borderRadius = Math.random() > 0.5 ? '50%' : '0';");
            sb.AppendLine("    container.appendChild(confetti);");
            sb.AppendLine("  }");
            sb.AppendLine("  setTimeout(function() { container.innerHTML = ''; }, 3500);");
            sb.AppendLine("}");
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

        private void DodajNotatkeOdbiorcy(int id, string tresc)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @op)", conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@tresc", tresc);
                    cmd.Parameters.AddWithValue("@op", operatorID);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd dodawania notatki: {ex.Message}");
            }
        }

        private void ZmienStatusOdbiorcy(int id, string nowyStatus)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmdUpdate = new SqlCommand("UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn);
                    cmdUpdate.Parameters.AddWithValue("@status", nowyStatus);
                    cmdUpdate.Parameters.AddWithValue("@id", id);
                    cmdUpdate.ExecuteNonQuery();

                    var cmdLog = new SqlCommand("INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) VALUES (@id, 'Zmiana statusu', @val, @op, GETDATE())", conn);
                    cmdLog.Parameters.AddWithValue("@id", id);
                    cmdLog.Parameters.AddWithValue("@val", nowyStatus);
                    cmdLog.Parameters.AddWithValue("@op", operatorID);
                    cmdLog.ExecuteNonQuery();
                }

                // Aktualizuj lokalną listę
                var kontakt = kontaktyNaMapie.FirstOrDefault(k => k.ID == id);
                if (kontakt != null)
                {
                    kontakt.Status = nowyStatus;
                    UstawKoloryStatusu(kontakt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public string Handlowiec { get; set; } = "";
        public string Tagi { get; set; } = "";
        public string Notatki { get; set; } = "";
        public string Status { get; set; } = "";
        public bool CzyPriorytetowa { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double DystansKm { get; set; }
        public string KolorHex { get; set; } = "#9CA3AF";
        public SolidColorBrush KolorStatusu { get; set; }
        public SolidColorBrush TloStatusu { get; set; }
        public SolidColorBrush KolorStatusuTekst { get; set; }
    }
}