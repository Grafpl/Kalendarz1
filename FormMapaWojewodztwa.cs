using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class FormMapaWojewodztwa : Form
    {
        private string connectionString;
        private string operatorID;
        private WebBrowser webBrowser;
        private Panel panelFiltr;
        private ComboBox comboBoxWojewodztwo;
        private ComboBox comboBoxStatus;
        private Label labelLicznik;
        private Label labelDebug;

        public FormMapaWojewodztwa(string connString, string opID)
        {
            connectionString = connString;
            operatorID = opID;
            InitializeComponent();
            StworzKontrolki();

            // Dodaj obsługę zdarzenia DocumentCompleted
            webBrowser.DocumentCompleted += (s, e) => {
                labelDebug.Text = "Status: Mapa załadowana";
                labelDebug.ForeColor = Color.Green;
            };

            WygenerujMape();
        }

        private void StworzKontrolki()
        {
            // Panel górny z filtrami
            panelFiltr = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(248, 249, 252),
                Padding = new Padding(10)
            };

            var label1 = new Label
            {
                Text = "Województwo:",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxWojewodztwo = new ComboBox
            {
                Location = new Point(120, 17),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxWojewodztwo.Items.AddRange(new object[] {
                "Wszystkie",
                "dolnośląskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
                "łódzkie", "małopolskie", "mazowieckie", "opolskie",
                "podkarpackie", "podlaskie", "pomorskie", "śląskie",
                "świętokrzyskie", "warmińsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
            });
            comboBoxWojewodztwo.SelectedIndex = 0;
            comboBoxWojewodztwo.SelectedIndexChanged += (s, e) => WygenerujMape();

            var label2 = new Label
            {
                Text = "Status:",
                Location = new Point(320, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxStatus = new ComboBox
            {
                Location = new Point(380, 17),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxStatus.Items.AddRange(new object[] {
                "Wszystkie statusy",
                "Do zadzwonienia", "Próba kontaktu", "Nawiązano kontakt",
                "Zgoda na dalszy kontakt", "Do wysłania oferta",
                "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)"
            });
            comboBoxStatus.SelectedIndex = 0;
            comboBoxStatus.SelectedIndexChanged += (s, e) => WygenerujMape();

            labelLicznik = new Label
            {
                Text = "Klientów: 0",
                Location = new Point(600, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(41, 128, 185)
            };

            labelDebug = new Label
            {
                Text = "Status: Ładowanie...",
                Location = new Point(20, 50),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };

            var btnOdswiez = new Button
            {
                Text = "Odśwież",
                Location = new Point(750, 15),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => WygenerujMape();

            panelFiltr.Controls.AddRange(new Control[] { label1, comboBoxWojewodztwo, label2, comboBoxStatus, labelLicznik, btnOdswiez, labelDebug });

            // WebBrowser dla mapy
            webBrowser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = false,
                AllowNavigation = true
            };

            this.Controls.Add(webBrowser);
            this.Controls.Add(panelFiltr);
        }

        private void WygenerujMape()
        {
            try
            {
                labelDebug.Text = "Status: Pobieranie danych...";
                labelDebug.ForeColor = Color.Orange;
                Application.DoEvents();

                var klienci = PobierzDaneKlientow();
                labelDebug.Text = $"Status: Pobrano {klienci.Rows.Count} klientów, generowanie mapy...";
                Application.DoEvents();

                var htmlMapa = GenerujHTMLMapy(klienci);

                string tempPath = Path.Combine(Path.GetTempPath(), "mapa_crm.html");
                File.WriteAllText(tempPath, htmlMapa, Encoding.UTF8);

                labelDebug.Text = $"Status: Plik zapisany: {tempPath}";
                Application.DoEvents();

                webBrowser.Navigate(tempPath);

                labelLicznik.Text = $"Klientów: {klienci.Rows.Count}";
            }
            catch (Exception ex)
            {
                labelDebug.Text = $"Status: BŁĄD - {ex.Message}";
                labelDebug.ForeColor = Color.Red;
                MessageBox.Show($"Błąd generowania mapy:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DataTable PobierzDaneKlientow()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("sp_PobierzOdbiorcow", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@OperatorID", operatorID);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                // Zamień "Nowy" na "Do zadzwonienia"
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Status"].ToString() == "Nowy")
                        row["Status"] = "Do zadzwonienia";
                }

                // Zastosuj filtry
                DataView dv = dt.DefaultView;
                var filters = new System.Collections.Generic.List<string>();

                if (comboBoxWojewodztwo.SelectedIndex > 0)
                {
                    filters.Add($"Wojewodztwo = '{comboBoxWojewodztwo.SelectedItem}'");
                }

                if (comboBoxStatus.SelectedIndex > 0)
                {
                    filters.Add($"Status = '{comboBoxStatus.SelectedItem}'");
                }

                if (filters.Count > 0)
                {
                    dv.RowFilter = string.Join(" AND ", filters);
                }

                return dv.ToTable();
            }
        }

        private string GenerujHTMLMapy(DataTable klienci)
        {
            var sb = new StringBuilder();

            sb.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Mapa CRM</title>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        body { margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; }
        #map { height: 100vh; width: 100%; }
        .leaflet-popup-content { font-family: 'Segoe UI', Arial, sans-serif; }
        .popup-title { font-weight: bold; color: #2c3e50; margin-bottom: 5px; font-size: 14px; }
        .popup-status { 
            display: inline-block;
            padding: 3px 8px;
            border-radius: 3px;
            font-size: 11px;
            margin-top: 5px;
            font-weight: bold;
        }
        .status-nowy { background-color: #ecf0f1; color: #34495e; }
        .status-proba { background-color: #aed6f1; color: #1a5490; }
        .status-nawiazano { background-color: #85c1e9; color: #1a5490; }
        .status-zgoda { background-color: #a9dfbf; color: #196f3d; }
        .status-oferta { background-color: #fadbd8; color: #922b21; }
        .status-nie { background-color: #f5b7b1; color: #922b21; }
        .status-usuniecie { background-color: #f1948a; color: #7b241c; }
        .status-bledny { background-color: #f8c471; color: #7d6608; }
        #loading { 
            position: fixed; 
            top: 50%; 
            left: 50%; 
            transform: translate(-50%, -50%);
            font-size: 20px;
            color: #3498db;
            z-index: 9999;
        }
    </style>
</head>
<body>
    <div id='loading'>Ładowanie mapy...</div>
    <div id='map'></div>
    <script>
        console.log('Inicjalizacja mapy...');
        
        setTimeout(function() {
            try {
                var map = L.map('map').setView([52.0, 19.0], 6);
                
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    attribution: '© OpenStreetMap contributors',
                    maxZoom: 18
                }).addTo(map);

                var markers = [];
                console.log('Dodawanie markerów...');
");

            var wojewodztwaCoords = new System.Collections.Generic.Dictionary<string, (double lat, double lng)>
            {
                {"dolnośląskie", (51.1, 17.0)},
                {"kujawsko-pomorskie", (53.0, 18.6)},
                {"lubelskie", (51.2, 22.9)},
                {"lubuskie", (52.4, 15.2)},
                {"łódzkie", (51.8, 19.5)},
                {"małopolskie", (50.0, 20.0)},
                {"mazowieckie", (52.2, 21.0)},
                {"opolskie", (50.7, 17.9)},
                {"podkarpackie", (50.0, 22.0)},
                {"podlaskie", (53.1, 23.2)},
                {"pomorskie", (54.4, 18.6)},
                {"śląskie", (50.3, 19.0)},
                {"świętokrzyskie", (50.9, 20.6)},
                {"warmińsko-mazurskie", (53.8, 20.5)},
                {"wielkopolskie", (52.4, 16.9)},
                {"zachodniopomorskie", (53.4, 14.5)}
            };

            var random = new Random();
            int markerCount = 0;

            foreach (DataRow row in klienci.Rows)
            {
                string woj = row["Wojewodztwo"]?.ToString()?.ToLower()?.Trim() ?? "";
                if (wojewodztwaCoords.TryGetValue(woj, out var coords))
                {
                    double lat = coords.lat + (random.NextDouble() - 0.5) * 0.5;
                    double lng = coords.lng + (random.NextDouble() - 0.5) * 0.5;

                    string nazwa = (row["Nazwa"]?.ToString() ?? "Brak nazwy")
                        .Replace("\\", "\\\\")
                        .Replace("'", "\\'")
                        .Replace("\"", "&quot;")
                        .Replace("\n", " ")
                        .Replace("\r", "");

                    string status = row["Status"]?.ToString() ?? "Do zadzwonienia";
                    string miasto = row["MIASTO"]?.ToString() ?? "";
                    string telefon = row["Telefon_K"]?.ToString() ?? "";
                    string pkd = (row["PKD_Opis"]?.ToString() ?? "").Replace("'", "\\'");

                    bool czyMoj = row["CzyMoj"]?.ToString() == "★";

                    string statusClass = status switch
                    {
                        "Do zadzwonienia" => "status-nowy",
                        "Próba kontaktu" => "status-proba",
                        "Nawiązano kontakt" => "status-nawiazano",
                        "Zgoda na dalszy kontakt" => "status-zgoda",
                        "Do wysłania oferta" => "status-oferta",
                        "Nie zainteresowany" => "status-nie",
                        "Poprosił o usunięcie" => "status-usuniecie",
                        "Błędny rekord (do raportu)" => "status-bledny",
                        _ => "status-nowy"
                    };

                    string markerColor = czyMoj ? "#f1c40f" : "#3498db";
                    string borderColor = czyMoj ? "#f39c12" : "#2980b9";

                    sb.AppendLine($@"
                var marker{markerCount} = L.marker([{lat.ToString("F6").Replace(",", ".")}, {lng.ToString("F6").Replace(",", ".")}], {{
                    icon: L.divIcon({{
                        html: '<div style=""background-color: {markerColor}; width: 16px; height: 16px; border-radius: 50%; border: 3px solid {borderColor}; box-shadow: 0 2px 4px rgba(0,0,0,0.4);""></div>',
                        className: '',
                        iconSize: [16, 16]
                    }})
                }}).addTo(map);
                
                marker{markerCount}.bindPopup(`
                    <div style='min-width: 220px;'>
                        <div class='popup-title'>{(czyMoj ? "⭐ " : "")}{nazwa}</div>
                        <div style='font-size: 12px; color: #7f8c8d; line-height: 1.4;'>
                            {(string.IsNullOrEmpty(miasto) ? "" : $"📍 {miasto}<br/>")}
                            {(string.IsNullOrEmpty(telefon) ? "" : $"📞 {telefon}<br/>")}
                            {(string.IsNullOrEmpty(pkd) ? "" : $"🏢 {pkd}<br/>")}
                        </div>
                        <div class='popup-status {statusClass}'>{status}</div>
                    </div>
                `);
                markers.push(marker{markerCount});
");
                    markerCount++;
                }
            }

            sb.AppendLine($@"
                console.log('Dodano {markerCount} markerów');
                
                if (markers.length > 0) {{
                    var group = new L.featureGroup(markers);
                    map.fitBounds(group.getBounds().pad(0.1));
                }} else {{
                    console.log('Brak markerów do wyświetlenia');
                }}
                
                document.getElementById('loading').style.display = 'none';
                console.log('Mapa załadowana pomyślnie');
                
            }} catch(e) {{
                console.error('Błąd:', e);
                document.getElementById('loading').innerHTML = 'Błąd ładowania mapy: ' + e.message;
                document.getElementById('loading').style.color = 'red';
            }}
        }}, 500);
    </script>
</body>
</html>");

            return sb.ToString();
        }
    }
}