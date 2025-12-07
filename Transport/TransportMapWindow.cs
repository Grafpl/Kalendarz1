// =====================================================================
// PLIK: Transport/TransportMapWindow.cs
// Mapa tras transportowych z wizualizacją punktów dostawy
// =====================================================================

using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport
{
    public class TransportMapWindow : Form
    {
        #region Fields

        private readonly string _connectionStringTransport;
        private readonly string _connectionStringHandel;
        private readonly string _connectionStringLibra;
        private readonly DateTime _dataKursu;
        private readonly string _userId;

        // Map controls
        private GMapControl mapControl;
        private GMapOverlay markersOverlay;
        private GMapOverlay routesOverlay;
        private GMapOverlay labelsOverlay;

        // UI Controls
        private Panel panelLeft;
        private Panel panelTop;
        private ListView lvKursy;
        private Panel panelDetails;
        private Label lblKursInfo;
        private ListView lvLadunki;
        private Label lblStatystyki;
        private ProgressBar progressBar;
        private ComboBox cmbMapProvider;
        private Button btnOdswiez;
        private Button btnResetView;
        private Button btnOptymalizuj;
        private CheckBox chkPokazTrasy;
        private CheckBox chkPokazEtykiety;
        private Panel panelLegenda;

        // Data
        private List<KursMapData> _kursy = new();
        private KursMapData? _selectedKurs;
        private Dictionary<int, KontrahentMapData> _kontrahenciCache = new();

        // Colors for routes
        private readonly Color[] _routeColors = new[]
        {
            Color.FromArgb(220, 53, 69),    // Red
            Color.FromArgb(0, 123, 255),    // Blue
            Color.FromArgb(40, 167, 69),    // Green
            Color.FromArgb(255, 193, 7),    // Yellow
            Color.FromArgb(111, 66, 193),   // Purple
            Color.FromArgb(23, 162, 184),   // Cyan
            Color.FromArgb(253, 126, 20),   // Orange
            Color.FromArgb(102, 16, 242),   // Indigo
            Color.FromArgb(232, 62, 140),   // Pink
            Color.FromArgb(32, 201, 151)    // Teal
        };

        // Company location (starting point)
        private readonly PointLatLng _companyLocation = new PointLatLng(52.0693, 19.4803); // Przykładowa lokalizacja firmy

        #endregion

        #region Constructor

        public TransportMapWindow(string connTransport, string connHandel, string connLibra, DateTime dataKursu, string userId)
        {
            _connectionStringTransport = connTransport;
            _connectionStringHandel = connHandel;
            _connectionStringLibra = connLibra;
            _dataKursu = dataKursu;
            _userId = userId;

            InitializeComponent();
            InitializeMap();
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            this.Text = $"🗺️ Mapa Transportu - {_dataKursu:dd.MM.yyyy dddd}";
            this.Size = new Size(1600, 900);
            this.MinimumSize = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);
            this.Icon = SystemIcons.Application;

            // Main layout
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 350,
                FixedPanel = FixedPanel.Panel1
            };

            // Left panel - course list and details
            panelLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            CreateLeftPanel();

            // Right panel - map
            var panelRight = new Panel { Dock = DockStyle.Fill };

            // Top toolbar
            panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(52, 58, 64),
                Padding = new Padding(10, 8, 10, 8)
            };

            CreateToolbar();

            // Map control placeholder
            var panelMap = new Panel { Dock = DockStyle.Fill };

            panelRight.Controls.Add(panelMap);
            panelRight.Controls.Add(panelTop);

            mainSplit.Panel1.Controls.Add(panelLeft);
            mainSplit.Panel2.Controls.Add(panelRight);

            this.Controls.Add(mainSplit);

            // Store reference to map panel for later
            mapControl = new GMapControl { Dock = DockStyle.Fill };
            panelMap.Controls.Add(mapControl);

            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void CreateLeftPanel()
        {
            // Header
            var lblHeader = new Label
            {
                Text = "🚚 KURSY TRANSPORTOWE",
                Dock = DockStyle.Top,
                Height = 40,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(52, 73, 94),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Course list
            lvKursy = new ListView
            {
                Dock = DockStyle.Top,
                Height = 200,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None
            };
            lvKursy.Columns.Add("Godz.", 55);
            lvKursy.Columns.Add("Kierowca", 100);
            lvKursy.Columns.Add("Pojazd", 70);
            lvKursy.Columns.Add("Pkt", 40);
            lvKursy.Columns.Add("Status", 70);
            lvKursy.SelectedIndexChanged += LvKursy_SelectedIndexChanged;

            // Details panel
            panelDetails = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            lblKursInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 80,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(232, 245, 233),
                Padding = new Padding(10),
                TextAlign = ContentAlignment.TopLeft
            };

            // Loadings list
            var lblLadunki = new Label
            {
                Text = "📦 PUNKTY DOSTAWY (kolejność załadunku)",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                BackColor = Color.FromArgb(236, 240, 241),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            lvLadunki = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            lvLadunki.Columns.Add("#", 30);
            lvLadunki.Columns.Add("Odbiorca", 150);
            lvLadunki.Columns.Add("Miasto", 80);
            lvLadunki.Columns.Add("Poj.", 45);
            lvLadunki.Columns.Add("Pal.", 40);
            lvLadunki.DoubleClick += LvLadunki_DoubleClick;

            // Legend
            panelLegenda = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = Color.FromArgb(253, 245, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };

            var lblLegendaTitle = new Label
            {
                Text = "🎨 LEGENDA",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            var lblLegendaContent = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8F),
                Text = "🏭 Firma (start)\n" +
                       "🔴🔵🟢 Punkty dostawy (kolor = kurs)\n" +
                       "① ② ③ Kolejność załadunku\n" +
                       "─── Trasa kursu"
            };

            panelLegenda.Controls.Add(lblLegendaContent);
            panelLegenda.Controls.Add(lblLegendaTitle);

            panelDetails.Controls.Add(lvLadunki);
            panelDetails.Controls.Add(lblLadunki);
            panelDetails.Controls.Add(lblKursInfo);
            panelDetails.Controls.Add(panelLegenda);

            panelLeft.Controls.Add(panelDetails);
            panelLeft.Controls.Add(lvKursy);
            panelLeft.Controls.Add(lblHeader);
        }

        private void CreateToolbar()
        {
            var lblProvider = new Label
            {
                Text = "Mapa:",
                Location = new Point(10, 12),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };

            cmbMapProvider = new ComboBox
            {
                Location = new Point(55, 9),
                Size = new Size(130, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cmbMapProvider.Items.AddRange(new[] { "OpenStreetMap", "Google Maps", "Google Satellite" });
            cmbMapProvider.SelectedIndex = 0;
            cmbMapProvider.SelectedIndexChanged += CmbMapProvider_SelectedIndexChanged;

            btnOdswiez = CreateToolbarButton("🔄 Odśwież", 200, Color.FromArgb(0, 123, 255));
            btnOdswiez.Click += async (s, e) => await LoadDataAsync();

            btnResetView = CreateToolbarButton("🏠 Polska", 300, Color.FromArgb(108, 117, 125));
            btnResetView.Click += (s, e) => ResetMapView();

            btnOptymalizuj = CreateToolbarButton("🧭 Optymalizuj", 400, Color.FromArgb(40, 167, 69));
            btnOptymalizuj.Click += (s, e) => OptymalizujTrase();

            chkPokazTrasy = new CheckBox
            {
                Text = "Pokaż trasy",
                Location = new Point(520, 12),
                AutoSize = true,
                ForeColor = Color.White,
                Checked = true,
                Font = new Font("Segoe UI", 9F)
            };
            chkPokazTrasy.CheckedChanged += (s, e) => RefreshMapDisplay();

            chkPokazEtykiety = new CheckBox
            {
                Text = "Pokaż etykiety",
                Location = new Point(620, 12),
                AutoSize = true,
                ForeColor = Color.White,
                Checked = true,
                Font = new Font("Segoe UI", 9F)
            };
            chkPokazEtykiety.CheckedChanged += (s, e) => RefreshMapDisplay();

            lblStatystyki = new Label
            {
                Location = new Point(750, 12),
                Size = new Size(400, 25),
                ForeColor = Color.FromArgb(173, 181, 189),
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressBar = new ProgressBar
            {
                Location = new Point(1150, 12),
                Size = new Size(100, 20),
                Visible = false
            };

            panelTop.Controls.AddRange(new Control[]
            {
                lblProvider, cmbMapProvider, btnOdswiez, btnResetView, btnOptymalizuj,
                chkPokazTrasy, chkPokazEtykiety, lblStatystyki, progressBar
            });
        }

        private Button CreateToolbarButton(string text, int x, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 8),
                Size = new Size(90, 28),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void InitializeMap()
        {
            try
            {
                GMapProviders.GoogleMap.ApiKey = "";
                GMaps.Instance.Mode = AccessMode.ServerAndCache;

                mapControl.MapProvider = GMapProviders.OpenStreetMap;
                mapControl.Position = _companyLocation;
                mapControl.MinZoom = 4;
                mapControl.MaxZoom = 18;
                mapControl.Zoom = 7;
                mapControl.ShowCenter = false;
                mapControl.DragButton = MouseButtons.Left;
                mapControl.CanDragMap = true;
                mapControl.MarkersEnabled = true;

                // Create overlays
                routesOverlay = new GMapOverlay("routes");
                markersOverlay = new GMapOverlay("markers");
                labelsOverlay = new GMapOverlay("labels");

                mapControl.Overlays.Add(routesOverlay);
                mapControl.Overlays.Add(markersOverlay);
                mapControl.Overlays.Add(labelsOverlay);

                // Event handlers
                mapControl.OnMarkerClick += MapControl_OnMarkerClick;
                mapControl.OnMarkerEnter += MapControl_OnMarkerEnter;
                mapControl.OnMarkerLeave += MapControl_OnMarkerLeave;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji mapy: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Data Loading

        private async Task LoadDataAsync()
        {
            try
            {
                progressBar.Visible = true;
                lblStatystyki.Text = "Ładowanie danych...";

                // Load contractors with coordinates
                await LoadKontrahenciAsync();

                // Load courses for the day
                await LoadKursyAsync();

                // Update UI
                UpdateKursyList();
                RefreshMapDisplay();

                var punkty = _kursy.Sum(k => k.Ladunki.Count);
                lblStatystyki.Text = $"Kursów: {_kursy.Count} | Punktów dostawy: {punkty} | Kontrahentów: {_kontrahenciCache.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task LoadKontrahenciAsync()
        {
            _kontrahenciCache.Clear();

            await using var cn = new SqlConnection(_connectionStringHandel);
            await cn.OpenAsync();

            var sql = @"
                SELECT
                    kh.Id,
                    kh.Shortcut,
                    kh.Name,
                    ISNULL(addr.City, '') AS Miasto,
                    ISNULL(addr.Street, '') AS Ulica,
                    ISNULL(addr.ZipCode, '') AS KodPocztowy,
                    kh.Latitude,
                    kh.Longitude
                FROM [HANDEL].[SSCommon].[STContractors] kh
                LEFT JOIN [HANDEL].[SSCommon].[STContractorsAddr] addr ON kh.Id = addr.ContractorId AND addr.IsDefault = 1
                WHERE kh.Latitude IS NOT NULL AND kh.Longitude IS NOT NULL";

            await using var cmd = new SqlCommand(sql, cn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                _kontrahenciCache[id] = new KontrahentMapData
                {
                    Id = id,
                    Shortcut = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Miasto = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Ulica = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    KodPocztowy = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Latitude = reader.IsDBNull(6) ? 0 : Convert.ToDouble(reader.GetValue(6)),
                    Longitude = reader.IsDBNull(7) ? 0 : Convert.ToDouble(reader.GetValue(7))
                };
            }
        }

        private async Task LoadKursyAsync()
        {
            _kursy.Clear();

            await using var cn = new SqlConnection(_connectionStringTransport);
            await cn.OpenAsync();

            // Load courses
            var sqlKursy = @"
                SELECT
                    k.KursID, k.DataKursu, k.KierowcaID, k.PojazdID, k.Trasa,
                    k.GodzWyjazdu, k.GodzPowrotu, k.Status,
                    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS KierowcaNazwa,
                    p.Rejestracja AS PojazdRejestracja,
                    p.PaletyH1 AS PaletyPojazdu
                FROM dbo.Kurs k
                JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                WHERE k.DataKursu = @Data
                ORDER BY k.GodzWyjazdu, k.KursID";

            await using var cmdKursy = new SqlCommand(sqlKursy, cn);
            cmdKursy.Parameters.AddWithValue("@Data", _dataKursu.Date);

            await using var readerKursy = await cmdKursy.ExecuteReaderAsync();
            while (await readerKursy.ReadAsync())
            {
                var kurs = new KursMapData
                {
                    KursID = readerKursy.GetInt64(0),
                    DataKursu = readerKursy.GetDateTime(1),
                    KierowcaID = readerKursy.GetInt32(2),
                    PojazdID = readerKursy.GetInt32(3),
                    Trasa = readerKursy.IsDBNull(4) ? "" : readerKursy.GetString(4),
                    GodzWyjazdu = readerKursy.IsDBNull(5) ? null : readerKursy.GetTimeSpan(5),
                    GodzPowrotu = readerKursy.IsDBNull(6) ? null : readerKursy.GetTimeSpan(6),
                    Status = readerKursy.GetString(7),
                    KierowcaNazwa = readerKursy.GetString(8),
                    PojazdRejestracja = readerKursy.GetString(9),
                    PaletyPojazdu = readerKursy.GetInt32(10)
                };
                _kursy.Add(kurs);
            }
            await readerKursy.CloseAsync();

            // Load loadings for each course
            foreach (var kurs in _kursy)
            {
                var sqlLadunki = @"
                    SELECT
                        l.LadunekID, l.Kolejnosc, l.KodKlienta, l.PojemnikiE2, l.PaletyH1, l.Uwagi
                    FROM dbo.Ladunek l
                    WHERE l.KursID = @KursID
                    ORDER BY l.Kolejnosc";

                await using var cmdLadunki = new SqlCommand(sqlLadunki, cn);
                cmdLadunki.Parameters.AddWithValue("@KursID", kurs.KursID);

                await using var readerLadunki = await cmdLadunki.ExecuteReaderAsync();
                while (await readerLadunki.ReadAsync())
                {
                    var ladunek = new LadunekMapData
                    {
                        LadunekID = readerLadunki.GetInt64(0),
                        Kolejnosc = readerLadunki.GetInt32(1),
                        KodKlienta = readerLadunki.IsDBNull(2) ? "" : readerLadunki.GetString(2),
                        PojemnikiE2 = readerLadunki.GetInt32(3),
                        PaletyH1 = readerLadunki.IsDBNull(4) ? 0 : readerLadunki.GetInt32(4),
                        Uwagi = readerLadunki.IsDBNull(5) ? "" : readerLadunki.GetString(5)
                    };

                    // Try to find contractor by code/shortcut
                    var kontrahent = _kontrahenciCache.Values
                        .FirstOrDefault(k => k.Shortcut.Equals(ladunek.KodKlienta, StringComparison.OrdinalIgnoreCase));

                    if (kontrahent != null)
                    {
                        ladunek.KontrahentId = kontrahent.Id;
                        ladunek.KontrahentNazwa = kontrahent.Name;
                        ladunek.Miasto = kontrahent.Miasto;
                        ladunek.Latitude = kontrahent.Latitude;
                        ladunek.Longitude = kontrahent.Longitude;
                    }

                    kurs.Ladunki.Add(ladunek);
                }
            }
        }

        #endregion

        #region UI Updates

        private void UpdateKursyList()
        {
            lvKursy.Items.Clear();

            int colorIndex = 0;
            foreach (var kurs in _kursy)
            {
                var item = new ListViewItem(kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--");
                item.SubItems.Add(kurs.KierowcaNazwa);
                item.SubItems.Add(kurs.PojazdRejestracja);
                item.SubItems.Add(kurs.Ladunki.Count.ToString());
                item.SubItems.Add(kurs.Status);
                item.Tag = kurs;
                item.BackColor = Color.FromArgb(50, _routeColors[colorIndex % _routeColors.Length]);

                lvKursy.Items.Add(item);
                colorIndex++;
            }

            if (lvKursy.Items.Count > 0)
                lvKursy.Items[0].Selected = true;
        }

        private void UpdateKursDetails(KursMapData kurs)
        {
            lblKursInfo.Text = $"🚗 {kurs.KierowcaNazwa}\n" +
                              $"🚚 {kurs.PojazdRejestracja} ({kurs.PaletyPojazdu} palet)\n" +
                              $"⏰ Wyjazd: {kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "---"} | " +
                              $"Status: {kurs.Status}\n" +
                              $"📍 Trasa: {kurs.Trasa ?? "Brak opisu"}";

            lvLadunki.Items.Clear();
            foreach (var ladunek in kurs.Ladunki.OrderBy(l => l.Kolejnosc))
            {
                var item = new ListViewItem(ladunek.Kolejnosc.ToString());
                item.SubItems.Add(string.IsNullOrEmpty(ladunek.KontrahentNazwa)
                    ? ladunek.KodKlienta
                    : ladunek.KontrahentNazwa);
                item.SubItems.Add(ladunek.Miasto);
                item.SubItems.Add(ladunek.PojemnikiE2.ToString());
                item.SubItems.Add(ladunek.PaletyH1.ToString());
                item.Tag = ladunek;

                // Color code based on coordinates availability
                if (ladunek.Latitude == 0 || ladunek.Longitude == 0)
                {
                    item.BackColor = Color.FromArgb(255, 235, 238); // Light red - no coordinates
                    item.ToolTipText = "⚠️ Brak współrzędnych GPS";
                }
                else
                {
                    item.BackColor = Color.FromArgb(232, 245, 233); // Light green - has coordinates
                }

                lvLadunki.Items.Add(item);
            }
        }

        #endregion

        #region Map Display

        private void RefreshMapDisplay()
        {
            try
            {
                mapControl.Overlays.Clear();

                routesOverlay = new GMapOverlay("routes");
                markersOverlay = new GMapOverlay("markers");
                labelsOverlay = new GMapOverlay("labels");

                // Add company marker
                AddCompanyMarker();

                // Add all courses to map
                int colorIndex = 0;
                foreach (var kurs in _kursy)
                {
                    var color = _routeColors[colorIndex % _routeColors.Length];
                    bool isSelected = _selectedKurs?.KursID == kurs.KursID;

                    AddKursToMap(kurs, color, isSelected);
                    colorIndex++;
                }

                mapControl.Overlays.Add(routesOverlay);
                mapControl.Overlays.Add(markersOverlay);

                if (chkPokazEtykiety.Checked)
                    mapControl.Overlays.Add(labelsOverlay);

                mapControl.Refresh();

                // Zoom to fit all markers if there are any
                if (markersOverlay.Markers.Count > 1)
                {
                    mapControl.ZoomAndCenterMarkers("markers");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshMapDisplay error: {ex.Message}");
            }
        }

        private void AddCompanyMarker()
        {
            var marker = new GMarkerGoogle(_companyLocation, GMarkerGoogleType.blue_pushpin)
            {
                ToolTipText = "🏭 FIRMA - Punkt startowy",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            markersOverlay.Markers.Add(marker);
        }

        private void AddKursToMap(KursMapData kurs, Color color, bool isSelected)
        {
            var points = new List<PointLatLng> { _companyLocation };

            int seq = 1;
            foreach (var ladunek in kurs.Ladunki.OrderBy(l => l.Kolejnosc))
            {
                if (ladunek.Latitude == 0 || ladunek.Longitude == 0)
                    continue;

                var point = new PointLatLng(ladunek.Latitude, ladunek.Longitude);
                points.Add(point);

                // Create custom marker with sequence number
                var marker = CreateNumberedMarker(point, seq, color, isSelected);
                marker.Tag = new MarkerData { Kurs = kurs, Ladunek = ladunek };
                marker.ToolTipText = CreateTooltipText(kurs, ladunek, seq);
                marker.ToolTipMode = MarkerTooltipMode.OnMouseOver;

                markersOverlay.Markers.Add(marker);

                // Add label if enabled
                if (chkPokazEtykiety.Checked)
                {
                    var label = CreateLabelMarker(point, ladunek.KodKlienta, color);
                    labelsOverlay.Markers.Add(label);
                }

                seq++;
            }

            // Draw route if enabled and has at least 2 points
            if (chkPokazTrasy.Checked && points.Count >= 2)
            {
                var route = new GMapRoute(points, kurs.KursID.ToString())
                {
                    Stroke = new Pen(color, isSelected ? 4 : 2)
                    {
                        DashStyle = isSelected ? DashStyle.Solid : DashStyle.Dash
                    }
                };
                routesOverlay.Routes.Add(route);
            }
        }

        private GMarkerGoogle CreateNumberedMarker(PointLatLng point, int number, Color color, bool isSelected)
        {
            // Create a custom bitmap with number
            int size = isSelected ? 32 : 26;
            var bitmap = new Bitmap(size, size);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw circle
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 1, 1, size - 2, size - 2);
                }

                // Draw border
                using (var pen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(pen, 1, 1, size - 2, size - 2);
                }

                // Draw number
                using (var font = new Font("Segoe UI", isSelected ? 12 : 10, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                {
                    var text = number.ToString();
                    var textSize = g.MeasureString(text, font);
                    var x = (size - textSize.Width) / 2;
                    var y = (size - textSize.Height) / 2;
                    g.DrawString(text, font, brush, x, y);
                }
            }

            return new GMarkerGoogle(point, bitmap)
            {
                Offset = new Point(-size / 2, -size / 2)
            };
        }

        private GMarkerGoogle CreateLabelMarker(PointLatLng point, string text, Color color)
        {
            var font = new Font("Segoe UI", 8, FontStyle.Bold);
            var textSize = TextRenderer.MeasureText(text, font);

            var bitmap = new Bitmap(textSize.Width + 10, textSize.Height + 4);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Background
                using (var brush = new SolidBrush(Color.FromArgb(220, Color.White)))
                {
                    g.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
                }

                // Border
                using (var pen = new Pen(color, 1))
                {
                    g.DrawRectangle(pen, 0, 0, bitmap.Width - 1, bitmap.Height - 1);
                }

                // Text
                using (var brush = new SolidBrush(Color.Black))
                {
                    g.DrawString(text, font, brush, 5, 2);
                }
            }

            return new GMarkerGoogle(new PointLatLng(point.Lat + 0.003, point.Lng), bitmap)
            {
                Offset = new Point(-bitmap.Width / 2, 0)
            };
        }

        private string CreateTooltipText(KursMapData kurs, LadunekMapData ladunek, int seq)
        {
            return $"📍 Punkt #{seq}\n" +
                   $"━━━━━━━━━━━━━━━━━\n" +
                   $"🏢 {(string.IsNullOrEmpty(ladunek.KontrahentNazwa) ? ladunek.KodKlienta : ladunek.KontrahentNazwa)}\n" +
                   $"📮 {ladunek.Miasto}\n" +
                   $"━━━━━━━━━━━━━━━━━\n" +
                   $"📦 Pojemniki: {ladunek.PojemnikiE2}\n" +
                   $"🎪 Palety: {ladunek.PaletyH1}\n" +
                   $"━━━━━━━━━━━━━━━━━\n" +
                   $"🚗 Kurs: {kurs.KierowcaNazwa}\n" +
                   $"🚚 Pojazd: {kurs.PojazdRejestracja}\n" +
                   $"⏰ Wyjazd: {kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "---"}";
        }

        #endregion

        #region Event Handlers

        private void LvKursy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvKursy.SelectedItems.Count == 0) return;

            _selectedKurs = lvKursy.SelectedItems[0].Tag as KursMapData;
            if (_selectedKurs != null)
            {
                UpdateKursDetails(_selectedKurs);
                RefreshMapDisplay();

                // Zoom to selected course markers
                var points = _selectedKurs.Ladunki
                    .Where(l => l.Latitude != 0 && l.Longitude != 0)
                    .Select(l => new PointLatLng(l.Latitude, l.Longitude))
                    .ToList();

                if (points.Any())
                {
                    points.Insert(0, _companyLocation);
                    ZoomToPoints(points);
                }
            }
        }

        private void LvLadunki_DoubleClick(object sender, EventArgs e)
        {
            if (lvLadunki.SelectedItems.Count == 0) return;

            var ladunek = lvLadunki.SelectedItems[0].Tag as LadunekMapData;
            if (ladunek != null && ladunek.Latitude != 0 && ladunek.Longitude != 0)
            {
                mapControl.Position = new PointLatLng(ladunek.Latitude, ladunek.Longitude);
                mapControl.Zoom = 15;
            }
        }

        private void MapControl_OnMarkerClick(GMapMarker item, MouseEventArgs e)
        {
            if (item.Tag is MarkerData data)
            {
                ShowDeliveryDetails(data);
            }
        }

        private void MapControl_OnMarkerEnter(GMapMarker item)
        {
            this.Cursor = Cursors.Hand;
        }

        private void MapControl_OnMarkerLeave(GMapMarker item)
        {
            this.Cursor = Cursors.Default;
        }

        private void CmbMapProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            mapControl.MapProvider = cmbMapProvider.SelectedIndex switch
            {
                0 => GMapProviders.OpenStreetMap,
                1 => GMapProviders.GoogleMap,
                2 => GMapProviders.GoogleSatelliteMap,
                _ => GMapProviders.OpenStreetMap
            };
            mapControl.ReloadMap();
        }

        #endregion

        #region Helper Methods

        private void ResetMapView()
        {
            mapControl.Position = new PointLatLng(52.0, 19.0); // Center of Poland
            mapControl.Zoom = 6;
        }

        private void ZoomToPoints(List<PointLatLng> points)
        {
            if (!points.Any()) return;

            var minLat = points.Min(p => p.Lat);
            var maxLat = points.Max(p => p.Lat);
            var minLng = points.Min(p => p.Lng);
            var maxLng = points.Max(p => p.Lng);

            var centerLat = (minLat + maxLat) / 2;
            var centerLng = (minLng + maxLng) / 2;

            mapControl.Position = new PointLatLng(centerLat, centerLng);

            // Calculate appropriate zoom level
            var latDiff = maxLat - minLat;
            var lngDiff = maxLng - minLng;
            var maxDiff = Math.Max(latDiff, lngDiff);

            mapControl.Zoom = maxDiff switch
            {
                > 5 => 6,
                > 2 => 7,
                > 1 => 8,
                > 0.5 => 9,
                > 0.2 => 10,
                > 0.1 => 11,
                _ => 12
            };
        }

        private void ShowDeliveryDetails(MarkerData data)
        {
            var msg = $"📍 SZCZEGÓŁY PUNKTU DOSTAWY\n" +
                     $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                     $"🏢 Odbiorca: {data.Ladunek.KontrahentNazwa}\n" +
                     $"📝 Kod: {data.Ladunek.KodKlienta}\n" +
                     $"📮 Miasto: {data.Ladunek.Miasto}\n\n" +
                     $"📦 Pojemniki E2: {data.Ladunek.PojemnikiE2}\n" +
                     $"🎪 Palety H1: {data.Ladunek.PaletyH1}\n" +
                     $"📋 Uwagi: {data.Ladunek.Uwagi ?? "Brak"}\n\n" +
                     $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                     $"🚗 Kierowca: {data.Kurs.KierowcaNazwa}\n" +
                     $"🚚 Pojazd: {data.Kurs.PojazdRejestracja}\n" +
                     $"⏰ Godz. wyjazdu: {data.Kurs.GodzWyjazdu?.ToString(@"hh\:mm") ?? "---"}";

            MessageBox.Show(msg, "Szczegóły dostawy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OptymalizujTrase()
        {
            if (_selectedKurs == null)
            {
                MessageBox.Show("Wybierz kurs do optymalizacji.", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var punktyZGPS = _selectedKurs.Ladunki.Where(l => l.Latitude != 0 && l.Longitude != 0).ToList();

            if (punktyZGPS.Count < 2)
            {
                MessageBox.Show("Za mało punktów z współrzędnymi GPS do optymalizacji (min. 2).",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Simple nearest neighbor algorithm for route optimization
            var optimized = OptimizeRouteNearestNeighbor(punktyZGPS);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧭 SUGEROWANA OPTYMALNA KOLEJNOŚĆ:");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();

            int seq = 1;
            foreach (var punkt in optimized)
            {
                sb.AppendLine($"{seq}. {punkt.KodKlienta} - {punkt.Miasto}");
                seq++;
            }

            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("💡 Algorytm: Nearest Neighbor (najbliższy sąsiad)");
            sb.AppendLine("⚠️ To jest sugestia - rzeczywista trasa może zależeć od innych czynników.");

            MessageBox.Show(sb.ToString(), "Optymalizacja trasy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private List<LadunekMapData> OptimizeRouteNearestNeighbor(List<LadunekMapData> points)
        {
            if (points.Count <= 1) return points;

            var result = new List<LadunekMapData>();
            var remaining = new List<LadunekMapData>(points);

            // Start from company location
            double currentLat = _companyLocation.Lat;
            double currentLng = _companyLocation.Lng;

            while (remaining.Any())
            {
                LadunekMapData nearest = null;
                double minDistance = double.MaxValue;

                foreach (var point in remaining)
                {
                    var distance = CalculateDistance(currentLat, currentLng, point.Latitude, point.Longitude);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = point;
                    }
                }

                if (nearest != null)
                {
                    result.Add(nearest);
                    remaining.Remove(nearest);
                    currentLat = nearest.Latitude;
                    currentLng = nearest.Longitude;
                }
            }

            return result;
        }

        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            // Haversine formula for distance calculation
            const double R = 6371; // Earth's radius in km

            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180;

        #endregion

        #region Data Classes

        private class KursMapData
        {
            public long KursID { get; set; }
            public DateTime DataKursu { get; set; }
            public int KierowcaID { get; set; }
            public int PojazdID { get; set; }
            public string Trasa { get; set; } = "";
            public TimeSpan? GodzWyjazdu { get; set; }
            public TimeSpan? GodzPowrotu { get; set; }
            public string Status { get; set; } = "";
            public string KierowcaNazwa { get; set; } = "";
            public string PojazdRejestracja { get; set; } = "";
            public int PaletyPojazdu { get; set; }
            public List<LadunekMapData> Ladunki { get; set; } = new();
        }

        private class LadunekMapData
        {
            public long LadunekID { get; set; }
            public int Kolejnosc { get; set; }
            public string KodKlienta { get; set; } = "";
            public int PojemnikiE2 { get; set; }
            public int PaletyH1 { get; set; }
            public string Uwagi { get; set; } = "";
            public int KontrahentId { get; set; }
            public string KontrahentNazwa { get; set; } = "";
            public string Miasto { get; set; } = "";
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        private class KontrahentMapData
        {
            public int Id { get; set; }
            public string Shortcut { get; set; } = "";
            public string Name { get; set; } = "";
            public string Miasto { get; set; } = "";
            public string Ulica { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        private class MarkerData
        {
            public KursMapData Kurs { get; set; }
            public LadunekMapData Ladunek { get; set; }
        }

        #endregion
    }
}
