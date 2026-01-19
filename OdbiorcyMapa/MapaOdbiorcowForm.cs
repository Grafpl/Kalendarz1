// =============== MapaOdbiorcowForm.cs - PEŁNA WERSJA Z POPRAWKAMI ===============
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace Kalendarz1
{
    public partial class MapaOdbiorcowForm : Form
    {
        private readonly string connectionString;
        private readonly string userId;
        private readonly List<string> domyslniHandlowcy;
        private GMapControl mapControl;
        private GMapOverlay markersOverlay;
        private GMapOverlay labelsOverlay;
        private DataGridView dgvDuplikaty;
        private Panel panelFilters;
        private CheckedListBox clbHandlowcy;
        private TextBox txtSearch;
        private Panel panelLegenda;
        private Label lblStatystyki;
        private ProgressBar progressBar;
        private OdbiorcyRepository repository;
        private HandlowiecColorProvider colorProvider;
        private DuplicateAddressGrouper duplicateGrouper;
        private MapMarkerFactory markerFactory;
        private MarkerClusterer clusterer;
        private List<OdbiorcaDto> wszystkieOdbiorcy;
        private Dictionary<string, PointLatLng> ostatnieKoordynaty;
        private Label lblMapStatus;
        private ComboBox cmbMapProvider;
        private CancellationTokenSource loadCancellation;
        private System.Windows.Forms.Timer debounceTimer;
        private double lastZoomLevel = 6;
        private CoordinatesCache coordinatesCache;

        public MapaOdbiorcowForm(string connString, string userID, List<string> zaznaczeniHandlowcy)
        {
            connectionString = connString;
            userId = userID;
            domyslniHandlowcy = zaznaczeniHandlowcy ?? new List<string>();

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            InitializeServices();
            LoadMapSettings();
        }

        private void InitializeServices()
        {
            try
            {
                repository = new OdbiorcyRepository(connectionString);
                colorProvider = new HandlowiecColorProvider();
                duplicateGrouper = new DuplicateAddressGrouper();
                markerFactory = new MapMarkerFactory(colorProvider);
                clusterer = new MarkerClusterer();
                coordinatesCache = new CoordinatesCache();
                ostatnieKoordynaty = new Dictionary<string, PointLatLng>();
                wszystkieOdbiorcy = new List<OdbiorcaDto>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "🗺 Mapa Odbiorców - System Zarządzania";
            this.WindowState = FormWindowState.Maximized;
            this.Font = new Font("Segoe UI", 9F);
            this.MinimumSize = new Size(1200, 700);

            var mainPanel = new Panel { Dock = DockStyle.Fill };

            panelFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(10)
            };

            var lblHandlowcy = new Label
            {
                Text = "👥 Handlowcy:",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            clbHandlowcy = new CheckedListBox
            {
                Location = new Point(10, 30),
                Size = new Size(200, 60),
                CheckOnClick = true
            };

            var lblSearch = new Label
            {
                Text = "🔍 Szukaj odbiorcy:",
                Location = new Point(220, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            txtSearch = new TextBox
            {
                Location = new Point(220, 30),
                Size = new Size(250, 23),
                Font = new Font("Segoe UI", 9F)
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            var lblProvider = new Label
            {
                Text = "🗺 Provider:",
                Location = new Point(490, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            cmbMapProvider = new ComboBox
            {
                Location = new Point(490, 30),
                Size = new Size(150, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMapProvider.Items.AddRange(new string[] {
                "OpenStreetMap",
                "Google Maps",
                "Google Satellite",
                "Bing Maps"
            });
            cmbMapProvider.SelectedIndex = 0;
            cmbMapProvider.SelectedIndexChanged += CmbMapProvider_SelectedIndexChanged;

            var btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Location = new Point(650, 28),
                Size = new Size(90, 28),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += async (s, e) => await LoadMapDataAsync();

            var btnResetView = new Button
            {
                Text = "🏠 Polska",
                Location = new Point(750, 28),
                Size = new Size(80, 28),
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResetView.FlatAppearance.BorderSize = 0;
            btnResetView.Click += (s, e) => ResetMapView();

            var btnGeokoduj = new Button
            {
                Text = "📍 Geokoduj",
                Location = new Point(840, 28),
                Size = new Size(100, 28),
                BackColor = ColorTranslator.FromHtml("#e67e22"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGeokoduj.FlatAppearance.BorderSize = 0;
            btnGeokoduj.Click += async (s, e) => await GeokodujBrakujaceAsync();

            var btnDiagnostyka = new Button
            {
                Text = "🔍 Diagnostyka",
                Location = new Point(950, 28),
                Size = new Size(110, 28),
                BackColor = ColorTranslator.FromHtml("#8e44ad"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDiagnostyka.FlatAppearance.BorderSize = 0;
            btnDiagnostyka.Click += async (s, e) => await DiagnozujBrakWspolrzednychAsync();

            lblMapStatus = new Label
            {
                Location = new Point(10, 75),
                Size = new Size(600, 20),
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTranslator.FromHtml("#2c3e50")
            };

            progressBar = new ProgressBar
            {
                Location = new Point(620, 75),
                Size = new Size(200, 20),
                Visible = false
            };

            lblStatystyki = new Label
            {
                Location = new Point(850, 60),
                Size = new Size(250, 35),
                Font = new Font("Segoe UI", 8F),
                ForeColor = ColorTranslator.FromHtml("#2c3e50")
            };

            panelFilters.Controls.AddRange(new Control[] {
                lblHandlowcy, clbHandlowcy, lblSearch, txtSearch,
                lblProvider, cmbMapProvider, btnOdswiez, btnResetView,
                btnGeokoduj, btnDiagnostyka,
                lblMapStatus, progressBar, lblStatystyki
            });

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 900
            };

            mapControl = new GMapControl
            {
                Dock = DockStyle.Fill,
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 6,
                ShowCenter = false,
                DragButton = MouseButtons.Left,
                CanDragMap = true,
                MarkersEnabled = true,
                ShowTileGridLines = false
            };

            mapControl.OnMapZoomChanged += OnMapZoomChanged;
            mapControl.OnPositionChanged += OnMapPositionChanged;

            var panelRight = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            panelLegenda = new Panel
            {
                Dock = DockStyle.Top,
                Height = 200,
                BackColor = ColorTranslator.FromHtml("#f8f9fa"),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };

            var lblLegendaTitle = new Label
            {
                Text = "🎨 LEGENDA KOLORÓW",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#34495e"),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelLegenda.Controls.Add(lblLegendaTitle);

            var lblDuplikatyTitle = new Label
            {
                Text = "⚠ DUPLIKATY ADRESÓW",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#e74c3c"),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            dgvDuplikaty = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false
            };

            dgvDuplikaty.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Adres",
                DataPropertyName = "Adres",
                HeaderText = "📍 Adres",
                Width = 200,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            dgvDuplikaty.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Liczba",
                DataPropertyName = "Liczba",
                HeaderText = "🔢",
                Width = 50
            });

            dgvDuplikaty.CellDoubleClick += DgvDuplikaty_CellDoubleClick;

            var panelDuplikaty = new Panel { Dock = DockStyle.Fill };
            panelDuplikaty.Controls.Add(dgvDuplikaty);
            panelDuplikaty.Controls.Add(lblDuplikatyTitle);

            panelRight.Controls.Add(panelDuplikaty);
            panelRight.Controls.Add(panelLegenda);

            splitContainer.Panel1.Controls.Add(mapControl);
            splitContainer.Panel2.Controls.Add(panelRight);

            mainPanel.Controls.Add(splitContainer);
            this.Controls.Add(mainPanel);
            this.Controls.Add(panelFilters);

            this.Load += MapaOdbiorcowForm_Load;
        }

        private async void MapaOdbiorcowForm_Load(object sender, EventArgs e)
        {
            try
            {
                UpdateMapStatus("⏳ Inicjalizacja mapy...");
                ConfigureMap();
                await LoadHandlowcyAsync();
                await LoadMapDataAsync();
                UpdateMapStatus("✓ Mapa gotowa");
            }
            catch (Exception ex)
            {
                UpdateMapStatus($"❌ Błąd: {ex.Message}");
            }
        }

        private void ConfigureMap()
        {
            try
            {
                string cachePath = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "GMapCache");
                if (!Directory.Exists(cachePath))
                    Directory.CreateDirectory(cachePath);
                mapControl.CacheLocation = cachePath;

                GMaps.Instance.Mode = AccessMode.ServerAndCache;
                SetMapProvider("OpenStreetMap");
                mapControl.Position = new PointLatLng(52.0, 19.0);
                mapControl.MinZoom = 2;
                mapControl.MaxZoom = 18;
                mapControl.Zoom = 6;

                markersOverlay = new GMapOverlay("markers");
                labelsOverlay = new GMapOverlay("labels");
                mapControl.Overlays.Add(markersOverlay);
                mapControl.Overlays.Add(labelsOverlay);

                mapControl.OnMarkerClick += MapControl_OnMarkerClick;
                mapControl.ReloadMap();
            }
            catch (Exception ex)
            {
                UpdateMapStatus($"❌ Błąd konfiguracji: {ex.Message}");
            }
        }

        private void SetMapProvider(string providerName)
        {
            try
            {
                switch (providerName)
                {
                    case "Google Maps":
                        mapControl.MapProvider = GoogleMapProvider.Instance;
                        break;
                    case "Google Satellite":
                        mapControl.MapProvider = GoogleSatelliteMapProvider.Instance;
                        break;
                    case "Bing Maps":
                        mapControl.MapProvider = BingMapProvider.Instance;
                        break;
                    default:
                        mapControl.MapProvider = OpenStreetMapProvider.Instance;
                        break;
                }
                mapControl.ReloadMap();
            }
            catch { }
        }

        private void CmbMapProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbMapProvider.SelectedItem != null)
            {
                SetMapProvider(cmbMapProvider.SelectedItem.ToString());
            }
        }

        private void UpdateMapStatus(string message)
        {
            if (lblMapStatus.InvokeRequired)
            {
                lblMapStatus.BeginInvoke(new Action(() => lblMapStatus.Text = message));
            }
            else
            {
                lblMapStatus.Text = message;
            }
        }

        private void OnMapZoomChanged()
        {
            if (Math.Abs(mapControl.Zoom - lastZoomLevel) > 0.5)
            {
                lastZoomLevel = mapControl.Zoom;
                DebouncedUpdateMarkers();
            }
        }

        private void OnMapPositionChanged(PointLatLng point)
        {
            DebouncedUpdateMarkers();
        }

        private void DebouncedUpdateMarkers()
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
            }

            debounceTimer = new System.Windows.Forms.Timer();
            debounceTimer.Interval = 300;
            debounceTimer.Tick += async (s, e) =>
            {
                debounceTimer.Stop();
                await UpdateMapMarkersAsync();
            };
            debounceTimer.Start();
        }

        private async Task LoadHandlowcyAsync()
        {
            try
            {
                var handlowcy = await repository.GetDistinctHandlowcyAsync();

                clbHandlowcy.Items.Clear();
                foreach (var handlowiec in handlowcy)
                {
                    clbHandlowcy.Items.Add(handlowiec);
                    if (domyslniHandlowcy.Contains(handlowiec))
                    {
                        int index = clbHandlowcy.Items.IndexOf(handlowiec);
                        if (index >= 0)
                            clbHandlowcy.SetItemChecked(index, true);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateMapStatus($"❌ Błąd ładowania handlowców: {ex.Message}");
            }
        }

        private async Task LoadMapDataAsync()
        {
            loadCancellation?.Cancel();
            loadCancellation = new CancellationTokenSource();
            var token = loadCancellation.Token;

            try
            {
                progressBar.Visible = true;
                progressBar.Value = 0;

                var filter = new OdbiorcaFilter
                {
                    Handlowcy = clbHandlowcy.CheckedItems.Cast<string>().ToList(),
                    SearchText = txtSearch.Text
                };

                progressBar.Value = 20;

                wszystkieOdbiorcy = await Task.Run(() =>
                    repository.GetAllAsync(filter), token) ?? new List<OdbiorcaDto>();

                if (token.IsCancellationRequested) return;

                progressBar.Value = 50;
                await UpdateMapMarkersAsync();

                progressBar.Value = 80;
                await Task.Run(() => {
                    UpdateLegenda();
                    UpdateDuplikaty();
                    UpdateStatystyki();
                }, token);

                progressBar.Value = 100;
            }
            catch (OperationCanceledException)
            {
                // Anulowano - ok
            }
            catch (Exception ex)
            {
                UpdateMapStatus($"❌ Błąd: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task UpdateMapMarkersAsync()
        {
            try
            {
                if (markersOverlay == null) return;

                await Task.Run(() =>
                {
                    var visibleBounds = mapControl.ViewArea;

                    // Filtruj tylko widoczne punkty
                    var visibleOdbiorcy = wszystkieOdbiorcy.Where(o =>
                        o.Latitude.HasValue && o.Longitude.HasValue &&
                        visibleBounds.Contains(new PointLatLng(o.Latitude.Value, o.Longitude.Value))
                    ).ToList();

                    // Klastruj markery
                    var clusters = clusterer.CreateClusters(visibleOdbiorcy, mapControl.Zoom);

                    this.BeginInvoke(new Action(() =>
                    {
                        markersOverlay.Markers.Clear();
                        labelsOverlay.Markers.Clear();

                        // Ogranicz liczbę markerów
                        int maxMarkers = mapControl.Zoom < 10 ? 100 : 500;

                        foreach (var cluster in clusters.Take(maxMarkers))
                        {
                            var marker = markerFactory.CreateMarker(cluster.Items);
                            if (marker != null)
                            {
                                markersOverlay.Markers.Add(marker);

                                // Dodaj etykietę dla grup przy wysokim zoom
                                if (cluster.Items.Count > 1 && mapControl.Zoom > 12)
                                {
                                    var names = string.Join(", ", cluster.Items.Take(2).Select(o => o.Nazwa));
                                    if (cluster.Items.Count > 2)
                                        names += "...";

                                    var label = new GMapMarkerLabel(
                                        marker.Position,
                                        names,
                                        colorProvider.GetColor(cluster.Items.First().HandlowiecNazwa)
                                    );
                                    labelsOverlay.Markers.Add(label);
                                }
                            }
                        }

                        UpdateMapStatus($"✓ Wyświetlono {markersOverlay.Markers.Count} markerów");
                        mapControl.Refresh();
                    }));
                });
            }
            catch (Exception ex)
            {
                UpdateMapStatus($"❌ Błąd aktualizacji markerów: {ex.Message}");
            }
        }

        private async Task DiagnozujBrakWspolrzednychAsync()
        {
            var bezKoordynatow = wszystkieOdbiorcy
                .Where(o => !o.Latitude.HasValue || !o.Longitude.HasValue)
                .ToList();

            var raport = new StringBuilder();
            raport.AppendLine($"📊 ANALIZA BRAKUJĄCYCH WSPÓŁRZĘDNYCH:");
            raport.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            raport.AppendLine($"Wszystkich odbiorców: {wszystkieOdbiorcy.Count}");
            raport.AppendLine($"Bez współrzędnych: {bezKoordynatow.Count} ({bezKoordynatow.Count * 100.0 / wszystkieOdbiorcy.Count:F1}%)");
            raport.AppendLine();

            // Analiza adresów
            raport.AppendLine("📍 ANALIZA ADRESÓW:");
            var bezAdresu = bezKoordynatow.Where(o => string.IsNullOrWhiteSpace(o.AdresPelny)).ToList();
            raport.AppendLine($"• Bez adresu: {bezAdresu.Count}");

            var tylkoNazwa = bezKoordynatow.Where(o => o.AdresPelny == o.Nazwa).ToList();
            raport.AppendLine($"• Tylko nazwa firmy jako adres: {tylkoNazwa.Count}");

            var bezUlicy = bezKoordynatow.Where(o =>
                !string.IsNullOrWhiteSpace(o.AdresPelny) &&
                !o.AdresPelny.Contains("ul.") &&
                !o.AdresPelny.Contains("ulica") &&
                !o.AdresPelny.Contains("al.") &&
                !o.AdresPelny.Contains(",")
            ).ToList();
            raport.AppendLine($"• Niepełny adres (brak ulicy): {bezUlicy.Count}");

            // Przykłady problematycznych adresów
            raport.AppendLine();
            raport.AppendLine("🔍 PRZYKŁADY ADRESÓW BEZ WSPÓŁRZĘDNYCH:");
            foreach (var o in bezKoordynatow.Take(10))
            {
                raport.AppendLine($"• ID: {o.Id}, Nazwa: {o.Nazwa}");
                raport.AppendLine($"  Adres: '{o.AdresPelny}'");
            }

            // Cache - POPRAWKA: bez await
            var cacheCount = coordinatesCache.GetCachedCount();  // ✅ BEZ await
            raport.AppendLine();
            raport.AppendLine($"💾 CACHE WSPÓŁRZĘDNYCH:");
            raport.AppendLine($"• Zapisanych w cache: {cacheCount}");
            raport.AppendLine($"• Do geokodowania: {bezKoordynatow.Count}");

            // Rekomendacje
            raport.AppendLine();
            raport.AppendLine("💡 REKOMENDACJE:");
            if (bezKoordynatow.Count > 0)
            {
                raport.AppendLine("1. Kliknij 'Geokoduj' aby spróbować znaleźć współrzędne");
                raport.AppendLine("2. Sprawdź czy adresy w bazie są kompletne");
                raport.AppendLine("3. Dla adresów bez ulicy dodaj przynajmniej miejscowość");
            }

            MessageBox.Show(raport.ToString(), "Diagnostyka współrzędnych",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private async Task GeokodujBrakujaceAsync()
        {
            var bezKoordynatow = wszystkieOdbiorcy
                .Where(o => !o.Latitude.HasValue || !o.Longitude.HasValue)
                .Where(o => !string.IsNullOrWhiteSpace(o.AdresPelny))
                .ToList();

            if (bezKoordynatow.Count == 0)
            {
                MessageBox.Show("Wszystkie adresy mają już współrzędne lub brak adresów do geokodowania!",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Znaleziono {bezKoordynatow.Count} adresów bez współrzędnych.\n\n" +
                $"Geokodowanie może potrwać około {bezKoordynatow.Count * 1.5} sekund.\n\n" +
                $"Kontynuować?",
                "Geokodowanie adresów",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            progressBar.Visible = true;
            progressBar.Maximum = bezKoordynatow.Count;
            progressBar.Value = 0;

            var geocoder = new GeocodingService();
            int success = 0;
            int failed = 0;
            var errors = new List<string>();

            foreach (var odbiorca in bezKoordynatow)
            {
                try
                {
                    UpdateMapStatus($"Geokodowanie [{progressBar.Value + 1}/{bezKoordynatow.Count}]: {odbiorca.Nazwa}");
                    Application.DoEvents();

                    var coords = await geocoder.GeocodeAddressAsync(odbiorca.AdresPelny);

                    if (coords.HasValue)
                    {
                        await repository.SaveCoordinatesLocallyAsync(odbiorca.Id, coords.Value.lat, coords.Value.lng);
                        odbiorca.Latitude = coords.Value.lat;
                        odbiorca.Longitude = coords.Value.lng;
                        success++;
                    }
                    else
                    {
                        // Spróbuj z fallback
                        var fallback = geocoder.GetFallbackCoordinates(odbiorca.AdresPelny);
                        await repository.SaveCoordinatesLocallyAsync(odbiorca.Id, fallback.lat, fallback.lng);
                        odbiorca.Latitude = fallback.lat;
                        odbiorca.Longitude = fallback.lng;
                        success++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{odbiorca.Nazwa}: {ex.Message}");
                }

                progressBar.Value++;

                // Delay między requestami
                if (progressBar.Value % 10 == 0)
                    await Task.Delay(1000);
                else
                    await Task.Delay(100);
            }

            progressBar.Visible = false;

            string message = $"📊 WYNIKI GEOKODOWANIA:\n\n" +
                            $"✅ Sukces: {success}\n" +
                            $"❌ Błąd: {failed}\n\n";

            if (errors.Count > 0 && errors.Count <= 5)
            {
                message += "Błędy:\n" + string.Join("\n", errors.Take(5));
            }

            MessageBox.Show(message, "Wynik geokodowania", MessageBoxButtons.OK,
                success > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (success > 0)
            {
                await LoadMapDataAsync();
            }
        }

        private void UpdateLegenda()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateLegenda));
                return;
            }

            panelLegenda.Controls.Clear();

            var lblTitle = new Label
            {
                Text = "🎨 LEGENDA",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#34495e"),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelLegenda.Controls.Add(lblTitle);

            var handlowcy = wszystkieOdbiorcy
                .Select(o => o.HandlowiecNazwa)
                .Distinct()
                .OrderBy(h => h)
                .ToList();

            int y = 35;
            foreach (var handlowiec in handlowcy)
            {
                var color = colorProvider.GetColor(handlowiec);

                var colorBox = new Panel
                {
                    Location = new Point(10, y),
                    Size = new Size(20, 20),
                    BackColor = color,
                    BorderStyle = BorderStyle.FixedSingle
                };

                var lblHandlowiec = new Label
                {
                    Text = $"{handlowiec} ({wszystkieOdbiorcy.Count(o => o.HandlowiecNazwa == handlowiec)})",
                    Location = new Point(35, y),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F)
                };

                panelLegenda.Controls.Add(colorBox);
                panelLegenda.Controls.Add(lblHandlowiec);

                y += 25;
            }
        }

        private void UpdateDuplikaty()
        {
            if (dgvDuplikaty.InvokeRequired)
            {
                dgvDuplikaty.BeginInvoke(new Action(UpdateDuplikaty));
                return;
            }

            var duplikaty = duplicateGrouper.GroupByLocation(wszystkieOdbiorcy)
                .Where(g => g.Count > 1)
                .Select(g => new
                {
                    Adres = g.First().AdresPelny,
                    Liczba = g.Count
                })
                .Take(50)
                .ToList();

            dgvDuplikaty.DataSource = duplikaty;
        }

        private void UpdateStatystyki()
        {
            if (lblStatystyki.InvokeRequired)
            {
                lblStatystyki.BeginInvoke(new Action(UpdateStatystyki));
                return;
            }

            int total = wszystkieOdbiorcy.Count;
            int zKoordynatami = wszystkieOdbiorcy.Count(o => o.Latitude.HasValue);
            int bezKoordynatow = total - zKoordynatami;

            lblStatystyki.Text = $"📊 Odbiorców: {total} | 📍 OK: {zKoordynatami} | ❌ Brak: {bezKoordynatow}";
        }

        private void DgvDuplikaty_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            dynamic duplikat = dgvDuplikaty.Rows[e.RowIndex].DataBoundItem;
            if (duplikat != null)
            {
                var odbiorca = wszystkieOdbiorcy.FirstOrDefault(o => o.AdresPelny == duplikat.Adres.ToString());
                if (odbiorca != null && odbiorca.Latitude.HasValue && odbiorca.Longitude.HasValue)
                {
                    mapControl.Position = new PointLatLng(odbiorca.Latitude.Value, odbiorca.Longitude.Value);
                    mapControl.Zoom = 16;
                }
            }
        }

        private void MapControl_OnMarkerClick(GMapMarker marker, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var customMarker = marker as CustomMapMarker;
                if (customMarker != null && customMarker.Odbiorcy.Count > 1)
                {
                    mapControl.Position = marker.Position;
                    mapControl.Zoom = Math.Min(mapControl.Zoom + 2, 18);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                var customMarker = marker as CustomMapMarker;
                if (customMarker != null)
                {
                    var menu = new ContextMenuStrip();

                    foreach (var odbiorca in customMarker.Odbiorcy.Take(10))
                    {
                        menu.Items.Add($"📊 {odbiorca.Nazwa}", null, (s, ev) =>
                        {
                            MessageBox.Show(
                                $"🏢 {odbiorca.Nazwa}\n" +
                                $"📍 {odbiorca.AdresPelny}\n" +
                                $"👤 {odbiorca.HandlowiecNazwa}\n" +
                                $"📌 {odbiorca.Latitude:F6}, {odbiorca.Longitude:F6}",
                                "Szczegóły odbiorcy",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        });
                    }

                    if (customMarker.Odbiorcy.Count > 10)
                    {
                        menu.Items.Add($"... +{customMarker.Odbiorcy.Count - 10} więcej");
                    }

                    menu.Show(mapControl, e.Location);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Dispose();
            }

            debounceTimer = new System.Windows.Forms.Timer();
            debounceTimer.Interval = 500;
            debounceTimer.Tick += async (s, ev) =>
            {
                debounceTimer.Stop();
                await LoadMapDataAsync();
            };
            debounceTimer.Start();
        }

        private void ResetMapView()
        {
            mapControl.Position = new PointLatLng(52.0, 19.0);
            mapControl.Zoom = 6;
            UpdateMapStatus("✓ Reset widoku");
        }

        private void LoadMapSettings()
        {
            if (File.Exists("mapsettings.json"))
            {
                try
                {
                    var json = File.ReadAllText("mapsettings.json");
                    dynamic settings = JsonConvert.DeserializeObject(json);
                    mapControl.Position = new PointLatLng((double)settings.Lat, (double)settings.Lng);
                    mapControl.Zoom = (double)settings.Zoom;
                }
                catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            loadCancellation?.Cancel();
            markerFactory?.ClearCache();

            var settings = new
            {
                Lat = mapControl.Position.Lat,
                Lng = mapControl.Position.Lng,
                Zoom = mapControl.Zoom
            };
            File.WriteAllText("mapsettings.json", JsonConvert.SerializeObject(settings));

            base.OnFormClosing(e);
        }
    }
}