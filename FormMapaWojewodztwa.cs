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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Drawing;

// Aliasy dla czytelności
using WinFormsButton = System.Windows.Forms.Button;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;

namespace Kalendarz1
{
    public partial class FormMapaWojewodztwa : Form
    {
        // === KONFIGURACJA ===
        private const bool EnableGeocoding = true;           // geokoduj brakujące lat/lng (ustaw na false, jeśli nie chcesz)
        private static readonly TimeSpan GeocodeDelay = TimeSpan.FromSeconds(1); // Nominatim: 1 request / s
        private readonly string connectionString;
        private readonly string operatorID;

        // UI
        private WebBrowser webBrowser;
        private Panel panelFiltr;
        private ComboBox comboBoxWojewodztwo;
        private ComboBox comboBoxStatus;
        private ComboBox comboBoxRodzaj;
        private ComboBox comboBoxSort;
        private Label labelLicznik;
        private Label labelDebug;
        private WinFormsButton btnOdswiez;

        // HTTP (geokodowanie)
        private static readonly HttpClient http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        private DateTime lastGeocode = DateTime.MinValue;

        public FormMapaWojewodztwa(string connString, string opID)
        {
            connectionString = connString;
            operatorID = opID;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            StworzKontrolki();

            webBrowser.DocumentCompleted += (s, e) =>
            {
                labelDebug.Text = "Status: Mapa załadowana";
                labelDebug.ForeColor = DrawingColor.Green;
            };

            // Poprawny User-Agent (ASCII) – brak wyjątków FormatException
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PiorkowscyCRM", "1.0"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+contact:it@piorkowscy.pl)"));

            // najpierw pokaż pustą mapę (UX), potem doładuj markery
            PokazPustaMape();
            _ = OdswiezAsync();
        }

        private void StworzKontrolki()
        {
            panelFiltr = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = DrawingColor.FromArgb(248, 249, 252),
                Padding = new Padding(10)
            };

            var label1 = new Label
            {
                Text = "Województwo:",
                Location = new Point(20, 14),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxWojewodztwo = new ComboBox
            {
                Location = new Point(120, 10),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxWojewodztwo.Items.AddRange(new object[] {
                "Wszystkie",
                "dolnośląskie","kujawsko-pomorskie","lubelskie","lubuskie","łódzkie","małopolskie",
                "mazowieckie","opolskie","podkarpackie","podlaskie","pomorskie","śląskie",
                "świętokrzyskie","warmińsko-mazurskie","wielkopolskie","zachodniopomorskie"
            });
            comboBoxWojewodztwo.SelectedIndex = 0;
            comboBoxWojewodztwo.SelectedIndexChanged += async (s, e) => await OdswiezAsync();

            var label2 = new Label
            {
                Text = "Status:",
                Location = new Point(320, 14),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxStatus = new ComboBox
            {
                Location = new Point(380, 10),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxStatus.Items.AddRange(new object[] {
                "Wszystkie statusy",
                "Do zadzwonienia","Próba kontaktu","Nawiązano kontakt","Zgoda na dalszy kontakt",
                "Do wysłania oferta","Nie zainteresowany","Poprosił o usunięcie","Błędny rekord (do raportu)"
            });
            comboBoxStatus.SelectedIndex = 0;
            comboBoxStatus.SelectedIndexChanged += async (s, e) => await OdswiezAsync();

            var label3 = new Label
            {
                Text = "Rodzaj:",
                Location = new Point(600, 14),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxRodzaj = new ComboBox
            {
                Location = new Point(660, 10),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxRodzaj.Items.AddRange(new object[] {
                "Wszystkie rodzaje",
                "Sklep","Hurtownia","Gastronomia","Przetwórnia","Eksport","Inne"
            });
            comboBoxRodzaj.SelectedIndex = 0;
            comboBoxRodzaj.SelectedIndexChanged += async (s, e) => await OdswiezAsync();

            var label4 = new Label
            {
                Text = "Sortuj po:",
                Location = new Point(20, 50),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxSort = new ComboBox
            {
                Location = new Point(120, 46),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxSort.Items.AddRange(new object[] { "Nazwa", "Rodzaj", "Status", "Miasto" });
            comboBoxSort.SelectedIndex = 0;
            comboBoxSort.SelectedIndexChanged += async (s, e) => await OdswiezAsync();

            labelLicznik = new Label
            {
                Text = "Klientów: 0",
                Location = new Point(320, 50),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = DrawingColor.FromArgb(41, 128, 185)
            };

            labelDebug = new Label
            {
                Text = "Status: Ładowanie...",
                Location = new Point(460, 52),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8F),
                ForeColor = DrawingColor.Gray
            };

            btnOdswiez = new WinFormsButton
            {
                Text = "Odśwież",
                Location = new Point(750, 44),
                Size = new Size(90, 30),
                BackColor = DrawingColor.FromArgb(41, 128, 185),
                ForeColor = DrawingColor.White,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += async (s, e) => await OdswiezAsync();

            panelFiltr.Controls.AddRange(new Control[] {
                label1, comboBoxWojewodztwo, label2, comboBoxStatus, label3, comboBoxRodzaj,
                label4, comboBoxSort, labelLicznik, btnOdswiez, labelDebug
            });

            webBrowser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = true,
                AllowNavigation = true
            };

            Controls.Add(webBrowser);
            Controls.Add(panelFiltr);
        }

        // === PUSTA MAPA (szybkie wyświetlenie) ===
        private void PokazPustaMape()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge' />
<meta name='viewport' content='width=device-width, initial-scale=1.0'/>
<title>Mapa CRM</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.7.1/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.7.1/dist/leaflet.js'></script>
<style>
  html, body { margin:0; padding:0; height:100%; }
  #map { height:100vh; width:100%; }
</style>
</head>
<body>
<div id='map'></div>
<script>
  var map = L.map('map').setView([52.0, 19.0], 6);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
     attribution: '© OpenStreetMap contributors', maxZoom: 18
  }).addTo(map);
</script>
</body>
</html>");
            string temp = Path.Combine(Path.GetTempPath(), "mapa_crm_blank.html");
            File.WriteAllText(temp, sb.ToString(), Encoding.UTF8);
            webBrowser.Navigate(temp);
            labelDebug.Text = "Status: Pusta mapa załadowana";
            labelDebug.ForeColor = DrawingColor.DarkSeaGreen;
        }

        // === GŁÓWNY PRZEPŁYW ===
        private async Task OdswiezAsync()
        {
            try
            {
                UstawStatus("Pobieranie danych...", DrawingColor.DarkOrange);
                var dt = await PobierzDaneKlientowAsync();

                // FILTRY
                DataView dv = dt.DefaultView;
                var filters = new List<string>();

                if (comboBoxWojewodztwo.SelectedIndex > 0)
                    filters.Add($"WojNorm = '{EscapeForRowFilter(comboBoxWojewodztwo.SelectedItem.ToString().Trim().ToLowerInvariant())}'");
                if (comboBoxStatus.SelectedIndex > 0)
                    filters.Add($"StatusNorm = '{EscapeForRowFilter(comboBoxStatus.SelectedItem.ToString().Trim().ToLowerInvariant())}'");
                if (comboBoxRodzaj.SelectedIndex > 0)
                    filters.Add($"RodzajNorm = '{EscapeForRowFilter(comboBoxRodzaj.SelectedItem.ToString().Trim().ToLowerInvariant())}'");

                dv.RowFilter = string.Join(" AND ", filters);

                // SORTOWANIE
                string sortCol = comboBoxSort.SelectedItem?.ToString() ?? "Nazwa";
                dv.Sort = $"{sortCol} ASC";

                var filtered = dv.ToTable();

                // GEOKODOWANIE (opcjonalnie)
                if (EnableGeocoding)
                {
                    UstawStatus("Geokodowanie brakujących adresów...", DrawingColor.DarkOrange);
                    await GeocodeMissingAsync(filtered);
                }

                labelLicznik.Text = $"Klientów: {filtered.Rows.Count}";

                // HTML MAPY
                UstawStatus("Renderowanie mapy...", DrawingColor.DarkOrange);
                string html = GenerujHtmlMapy(filtered);
                string tempPath = Path.Combine(Path.GetTempPath(), "mapa_crm.html");
                File.WriteAllText(tempPath, html, Encoding.UTF8);
                webBrowser.Navigate(tempPath);

                UstawStatus("Mapa załadowana", DrawingColor.ForestGreen);
            }
            catch (Exception ex)
            {
                UstawStatus($"BŁĄD: {ex.Message}", DrawingColor.Red);
                MessageBox.Show($"Błąd:\n\n{ex}", "Mapa CRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // === DANE: prosto z OdbiorcyCRM (bez procedury) ===
        private async Task<DataTable> PobierzDaneKlientowAsync()
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Bierzemy kolumny z Twojej listy + ewentualne współrzędne
            var sql = @"
SELECT
    ID,
    NAZWA,
    KOD,
    MIASTO,
    ULICA,
    NUMER,
    NR_LOK,
    TELEFON_K,
    Wojewodztwo,
    Powiat,
    Gmina,
    Status,
    PKD,
    PKD_Opis,
    /* działa na starszych wersjach SQL Server */
    CASE WHEN ISNUMERIC(CAST(Latitude  AS NVARCHAR(64)))  = 1 THEN CAST(Latitude  AS FLOAT) ELSE NULL END AS Latitude,
    CASE WHEN ISNUMERIC(CAST(Longitude AS NVARCHAR(64))) = 1 THEN CAST(Longitude AS FLOAT) ELSE NULL END AS Longitude
FROM dbo.OdbiorcyCRM";


            var dt = new DataTable();
            using (var da = new SqlDataAdapter(sql, conn))
            {
                da.Fill(dt);
            }

            // Normalizacje nazw na potrzeby generowania popupów/mapy
            if (!dt.Columns.Contains("Nazwa")) dt.Columns.Add("Nazwa", typeof(string));
            if (!dt.Columns.Contains("Miasto")) dt.Columns.Add("Miasto", typeof(string));
            if (!dt.Columns.Contains("UlicaFull")) dt.Columns.Add("UlicaFull", typeof(string)); // Ulica + Numer + NR_LOK
            if (!dt.Columns.Contains("KodPocztowy")) dt.Columns.Add("KodPocztowy", typeof(string));
            if (!dt.Columns.Contains("Telefon_K")) dt.Columns.Add("Telefon_K", typeof(string));
            if (!dt.Columns.Contains("Rodzaj")) dt.Columns.Add("Rodzaj", typeof(string));

            // Kolumny pomocnicze do filtrów
            if (!dt.Columns.Contains("WojNorm")) dt.Columns.Add("WojNorm", typeof(string));
            if (!dt.Columns.Contains("StatusNorm")) dt.Columns.Add("StatusNorm", typeof(string));
            if (!dt.Columns.Contains("RodzajNorm")) dt.Columns.Add("RodzajNorm", typeof(string));

            foreach (DataRow r in dt.Rows)
            {
                r["Nazwa"] = (r["NAZWA"]?.ToString() ?? "").Trim();
                r["Miasto"] = (r["MIASTO"]?.ToString() ?? "").Trim();
                r["KodPocztowy"] = (r["KOD"]?.ToString() ?? "").Trim();
                r["Telefon_K"] = (r["TELEFON_K"]?.ToString() ?? "").Trim();

                var ulica = (r["ULICA"]?.ToString() ?? "").Trim();
                var numer = (r["NUMER"]?.ToString() ?? "").Trim();
                var nrlok = (r["NR_LOK"]?.ToString() ?? "").Trim();
                var ulFull = new StringBuilder();
                if (!string.IsNullOrEmpty(ulica)) ulFull.Append(ulica);
                if (!string.IsNullOrEmpty(numer)) ulFull.Append(ulFull.Length > 0 ? $" {numer}" : numer);
                if (!string.IsNullOrEmpty(nrlok)) ulFull.Append($"/{nrlok}");
                r["UlicaFull"] = ulFull.ToString();

                // Status: zamiana „Nowy” → „Do zadzwonienia” (jak w CRM)
                var st = (r["Status"]?.ToString() ?? "").Trim();
                if (string.Equals(st, "Nowy", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(st))
                    r["Status"] = "Do zadzwonienia";

                // Wyprowadź Rodzaj z PKD_Opis (heurystyka)
                r["Rodzaj"] = WyliczRodzaj(r["PKD_Opis"]?.ToString());

                // Normy do filtrów
                r["WojNorm"] = (r["Wojewodztwo"]?.ToString() ?? "").Trim().ToLowerInvariant();
                r["StatusNorm"] = (r["Status"]?.ToString() ?? "").Trim().ToLowerInvariant();
                r["RodzajNorm"] = (r["Rodzaj"]?.ToString() ?? "").Trim().ToLowerInvariant();
            }

            return dt;
        }

        private static string WyliczRodzaj(string pkdOpis)
        {
            var s = (pkdOpis ?? "").ToLowerInvariant();

            if (s.Contains("hurt")) return "Hurtownia";
            if (s.Contains("restaur") || s.Contains("gastron")) return "Gastronomia";
            if (s.Contains("przetwór") || s.Contains("przetwor") || s.Contains("produkc")
                || s.Contains("ubój") || s.Contains("uboj")) return "Przetwórnia";
            if (s.Contains("sklep") || s.Contains("detaliczn")) return "Sklep";
            if (s.Contains("eksport")) return "Eksport";
            return "Inne";
        }

        // === GEOKODOWANIE / CACHE ===
        private async Task GeocodeMissingAsync(DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                bool hasLat = double.TryParse(row["Latitude"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat);
                bool hasLng = double.TryParse(row["Longitude"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng);

                if (hasLat && hasLng && Math.Abs(lat) > 0.0001 && Math.Abs(lng) > 0.0001)
                    continue;

                string address = BuildAddress(row);
                if (string.IsNullOrWhiteSpace(address))
                    continue;

                var cached = await TryGetFromCacheAsync(address);
                if (cached != null)
                {
                    row["Latitude"] = cached.Value.lat;
                    row["Longitude"] = cached.Value.lng;
                    continue;
                }

                var wait = GeocodeDelay - (DateTime.UtcNow - lastGeocode);
                if (wait > TimeSpan.Zero) await Task.Delay(wait);
                lastGeocode = DateTime.UtcNow;

                var geo = await GeocodeAsync(address);
                if (geo != null)
                {
                    row["Latitude"] = geo.Value.lat;
                    row["Longitude"] = geo.Value.lng;
                    await UpsertCacheAsync(address, geo.Value.lat, geo.Value.lng);
                }
                else
                {
                    await UpsertCacheAsync(address, null, null);
                }
            }
        }

        private string BuildAddress(DataRow row)
        {
            // Ulica + Numer[/NR_LOK], Kod, Miasto, Województwo, Polska
            var parts = new List<string>();
            string ul = (row["UlicaFull"]?.ToString() ?? "").Trim();
            if (!string.IsNullOrEmpty(ul)) parts.Add(ul);

            string kod = (row["KodPocztowy"]?.ToString() ?? "").Trim();
            if (!string.IsNullOrEmpty(kod)) parts.Add(kod);

            string miasto = (row["Miasto"]?.ToString() ?? "").Trim();
            if (!string.IsNullOrEmpty(miasto)) parts.Add(miasto);

            string woj = (row["Wojewodztwo"]?.ToString() ?? "").Trim();
            if (!string.IsNullOrEmpty(woj)) parts.Add(woj);

            parts.Add("Polska");

            string address = string.Join(", ", parts);
            address = Regex.Replace(address, @"\s+", " ").Trim();
            return address;
        }

        private static string Sha1(string text)
        {
            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = sha1.ComputeHash(bytes);
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        private async Task<(double lat, double lng)?> TryGetFromCacheAsync(string address)
        {
            // Cache opcjonalny: dbo.GeoCache(AddressHash, AddressText, Latitude float, Longitude float, LastUpdate datetime2)
            string hash = Sha1(address);

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            string sql = @"IF OBJECT_ID('dbo.GeoCache','U') IS NULL
    SELECT CAST(NULL AS float) AS Latitude, CAST(NULL AS float) AS Longitude
ELSE
    SELECT Latitude, Longitude FROM dbo.GeoCache WHERE AddressHash = @h";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@h", hash);

            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                if (!rdr.IsDBNull(0) && !rdr.IsDBNull(1))
                    return (Convert.ToDouble(rdr.GetValue(0)), Convert.ToDouble(rdr.GetValue(1)));
                return null;
            }
            return null;
        }

        private async Task UpsertCacheAsync(string address, double? lat, double? lng)
        {
            string hash = Sha1(address);
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            string sql = @"
IF OBJECT_ID('dbo.GeoCache','U') IS NOT NULL
BEGIN
MERGE dbo.GeoCache AS tgt
USING (SELECT @h AS AddressHash) AS src
ON (tgt.AddressHash = src.AddressHash)
WHEN MATCHED THEN
    UPDATE SET AddressText=@a, Latitude=@lat, Longitude=@lng, LastUpdate=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (AddressHash, AddressText, Latitude, Longitude, LastUpdate)
    VALUES (@h, @a, @lat, @lng, SYSUTCDATETIME());
END";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@h", hash);
            cmd.Parameters.AddWithValue("@a", address);
            cmd.Parameters.AddWithValue("@lat", (object?)lat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lng", (object?)lng ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<(double lat, double lng)?> GeocodeAsync(string address)
        {
            try
            {
                string url = "https://nominatim.openstreetmap.org/search?" +
                             $"q={HttpUtility.UrlEncode(address)}&format=json&limit=1&addressdetails=0&countrycodes=pl";

                using var resp = await http.GetAsync(url);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement;
                if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                {
                    var first = arr[0];
                    double lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
                    double lon = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
                    return (lat, lon);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Geocode error: " + ex.Message);
            }
            return null;
        }

        // === GENEROWANIE HTML (Leaflet 1.7.1 + MarkerCluster) ===
        private string GenerujHtmlMapy(DataTable klienci)
        {
            var list = new List<MapPoint>();
            foreach (DataRow r in klienci.Rows)
            {
                if (!double.TryParse(r["Latitude"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) continue;
                if (!double.TryParse(r["Longitude"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)) continue;
                if (Math.Abs(lat) < 0.0001 || Math.Abs(lng) < 0.0001) continue;

                list.Add(new MapPoint
                {
                    Nazwa = SafeStr(r, "Nazwa"),
                    Miasto = SafeStr(r, "Miasto"),
                    Telefon = SafeStr(r, "Telefon_K"),
                    PKD = SafeStr(r, "PKD_Opis"),
                    Status = NormalizeStatus(SafeStr(r, "Status")),
                    Rodzaj = SafeStr(r, "Rodzaj"),
                    Lat = lat,
                    Lng = lng
                });
            }

            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

            var sb = new StringBuilder();
            sb.AppendLine(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge' />
<meta name='viewport' content='width=device-width, initial-scale=1.0'/>
<title>Mapa CRM</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.7.1/dist/leaflet.css'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css'/>
<script src='https://unpkg.com/leaflet@1.7.1/dist/leaflet.js'></script>
<script src='https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js'></script>
<style>
  html, body { margin:0; padding:0; height:100%; font-family:'Segoe UI', Arial; }
  #map { height:100vh; width:100%; }
  .popup-title { font-weight:bold; color:#2c3e50; margin-bottom:5px; font-size:14px; }
  .popup-status { display:inline-block; padding:3px 8px; border-radius:3px; font-size:11px; margin-top:5px; font-weight:bold; }
  .status-nowy { background:#ecf0f1; color:#34495e; }
  .status-proba { background:#aed6f1; color:#1a5490; }
  .status-nawiazano { background:#85c1e9; color:#1a5490; }
  .status-zgoda { background:#a9dfbf; color:#196f3d; }
  .status-oferta { background:#fadbd8; color:#922b21; }
  .status-nie { background:#f5b7b1; color:#922b21; }
  .status-usuniecie { background:#f1948a; color:#7b241c; }
  .status-bledny { background:#f8c471; color:#7d6608; }
  .legend { position: absolute; top:10px; right:10px; background:white; padding:8px 10px; border-radius:6px; box-shadow:0 2px 8px rgba(0,0,0,.15); font-size:12px; }
  .legend-row { display:flex; align-items:center; margin-bottom:4px; }
  .legend-dot { width:12px; height:12px; border-radius:50%; margin-right:6px; border:2px solid rgba(0,0,0,.2); }
  .nodata { position:absolute; top:10px; left:10px; background:#fff; padding:6px 8px; border-radius:6px; box-shadow:0 2px 8px rgba(0,0,0,.15); font-size:12px; color:#4b5563; }
</style>
</head>
<body>
<div id='map'></div>
<div class='legend'>
  <div class='legend-row'><div class='legend-dot' style='background:#3b82f6'></div>Sklep</div>
  <div class='legend-row'><div class='legend-dot' style='background:#10b981'></div>Hurtownia</div>
  <div class='legend-row'><div class='legend-dot' style='background:#f59e0b'></div>Gastronomia</div>
  <div class='legend-row'><div class='legend-dot' style='background:#ef4444'></div>Przetwórnia</div>
  <div class='legend-row'><div class='legend-dot' style='background:#8b5cf6'></div>Eksport</div>
  <div class='legend-row'><div class='legend-dot' style='background:#6b7280'></div>Inne</div>
</div>
<script>");
            sb.Append("var data = ");
            sb.Append(json);
            sb.AppendLine(";");

            sb.AppendLine(@"
function colorForRodzaj(r) {
  r = (r||'').toLowerCase();
  if (r === 'sklep') return '#3b82f6';
  if (r === 'hurtownia') return '#10b981';
  if (r === 'gastronomia') return '#f59e0b';
  if (r === 'przetwórnia') return '#ef4444';
  if (r === 'eksport') return '#8b5cf6';
  return '#6b7280';
}
function statusClass(s) {
  s = (s||'').toLowerCase();
  if (s === 'do zadzwonienia') return 'status-nowy';
  if (s === 'próba kontaktu')  return 'status-proba';
  if (s === 'nawiązano kontakt') return 'status-nawiazano';
  if (s === 'zgoda na dalszy kontakt') return 'status-zgoda';
  if (s === 'do wysłania oferta') return 'status-oferta';
  if (s === 'nie zainteresowany') return 'status-nie';
  if (s === 'błędny rekord (do raportu)') return 'status-bledny';
  return 'status-nowy';
}

var map = L.map('map').setView([52.0, 19.0], 6);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  attribution: '© OpenStreetMap contributors', maxZoom: 18
}).addTo(map);

var markers = L.markerClusterGroup({ showCoverageOnHover:false, maxClusterRadius:45 });

for (var i=0; i<data.length; i++) {
  var p = data[i];
  var color = colorForRodzaj(p.Rodzaj);
  var border = '#111827';
  var icon = L.divIcon({
    html: '<div style=""width:16px;height:16px;border-radius:50%;background:'+color+';border:2px solid '+border+';box-shadow:0 1px 3px rgba(0,0,0,.4)""></div>',
    className: '', iconSize: [16,16]
  });

  var title = (p.Nazwa || 'Brak nazwy');
  var lines = [];
  if (p.Miasto)  lines.push('📍 ' + p.Miasto);
  if (p.Telefon) lines.push('📞 ' + p.Telefon);
  if (p.PKD)     lines.push('🏢 ' + p.PKD);

  var popup = [
    ""<div style='min-width:240px'>"",
    ""  <div class='popup-title'>""+title+""</div>"",
    ""  <div style='font-size:12px;color:#6b7280;line-height:1.4'>""+lines.join('<br/>')+""</div>"",
    ""  <div class='popup-status ""+statusClass(p.Status)+""' style='margin-top:6px'>""+(p.Status||'')+""</div>"",
    ""  <div style='font-size:11px;color:#6b7280;margin-top:6px'>Rodzaj: ""+(p.Rodzaj||'—')+""</div>"",
    ""</div>""
  ].join('');

  var m = L.marker([p.Lat, p.Lng], { icon: icon });
  m.bindPopup(popup);
  markers.addLayer(m);
}

map.addLayer(markers);
if (data.length) {
  var grp = new L.featureGroup(markers.getLayers());
  map.fitBounds(grp.getBounds().pad(0.2));
} else {
  var note = document.createElement('div');
  note.className = 'nodata';
  note.textContent = 'Brak punktów do wyświetlenia';
  document.body.appendChild(note);
}
</script>
</body>
</html>");
            return sb.ToString();
        }

        // === POMOCNICZE ===
        private static string NormalizeStatus(string s)
        {
            if (string.Equals(s, "Nowy", StringComparison.OrdinalIgnoreCase)) return "Do zadzwonienia";
            return s ?? "";
        }

        private static string SafeStr(DataRow r, string col)
        {
            return (r.Table.Columns.Contains(col) ? r[col]?.ToString() : "")?.Trim() ?? "";
        }

        private static string EscapeForRowFilter(string s) => (s ?? "").Replace("'", "''");

        private void UstawStatus(string txt, DrawingColor col)
        {
            labelDebug.Text = $"Status: {txt}";
            labelDebug.ForeColor = col;
            Application.DoEvents();
        }

        private sealed class MapPoint
        {
            public string Nazwa { get; set; } = "";
            public string Miasto { get; set; } = "";
            public string Telefon { get; set; } = "";
            public string PKD { get; set; } = "";
            public string Status { get; set; } = "";
            public string Rodzaj { get; set; } = "";
            public double Lat { get; set; }
            public double Lng { get; set; }
        }
    }
}
