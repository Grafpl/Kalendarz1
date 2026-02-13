using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kalendarz1
{
    public partial class WidokCenWszystkich : Form
    {
        #region Pola prywatne

        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private DataTable originalData;
        private DataTable filteredData;
        private Chart purchaseChart; // Wykres zakupowy
        private Chart salesChart;     // Wykres sprzedażowy
        private TabControl mainTabControl;

        // Główne kontrolki filtrowania (dla wszystkich zakładek)
        private DateTimePicker mainDateFrom;
        private DateTimePicker mainDateTo;
        private CheckBox chkShowWeekends;

        // Checkboxy dla wykresów
        private CheckBox chkMinister;
        private CheckBox chkLaczona;
        private CheckBox chkRolnicza;
        private CheckBox chkWolnorynkowa;
        private CheckBox chkNaszaTuszka;
        private CheckBox chkTuszkaZrzeszenia;

        // Kolory motywu Material Design
        private readonly Color primaryColor = Color.FromArgb(33, 150, 243);    // Niebieski
        private readonly Color accentColor = Color.FromArgb(255, 152, 0);      // Pomarańczowy
        private readonly Color successColor = Color.FromArgb(76, 175, 80);     // Zielony
        private readonly Color dangerColor = Color.FromArgb(244, 67, 54);      // Czerwony
        private readonly Color backgroundColor = Color.FromArgb(250, 250, 250);

        #endregion

        #region Konstruktor i inicjalizacja

        public WidokCenWszystkich()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Ustawienia formularza
            this.Text = "System Analizy Cen - Panel Główny";
            this.Size = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5F);
            this.BackColor = backgroundColor;
            this.Icon = SystemIcons.Application;

            // Włącz double buffering dla płynniejszego renderowania
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer, true);

            // Ukryj stare kontrolki
            HideOriginalControls();

            // Utwórz nowy interfejs
            CreateModernUI();

            // Załaduj dane asynchronicznie
            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void HideOriginalControls()
        {
            if (textBox1 != null) textBox1.Visible = false;
            if (label18 != null) label18.Visible = false;
            if (ExcelButton != null) ExcelButton.Visible = false;
            if (chkShowWeekend != null) chkShowWeekend.Visible = false;
            if (dataGridView1 != null) dataGridView1.Visible = false;
        }

        #endregion

        #region Tworzenie UI

        private void CreateModernUI()
        {
            // Główny kontener z marginesami
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0),
                BackColor = backgroundColor
            };

            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Header
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Global Filter Panel
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Status

            // 1. Header Panel
            var headerPanel = CreateHeaderPanel();
            mainContainer.Controls.Add(headerPanel, 0, 0);

            // 2. Global Filter Panel (dla wszystkich zakładek)
            var globalFilterPanel = CreateGlobalFilterPanel();
            mainContainer.Controls.Add(globalFilterPanel, 0, 1);

            // 3. Content (TabControl)
            mainTabControl = CreateTabControl();
            mainContainer.Controls.Add(mainTabControl, 0, 2);

            // 4. Status Bar
            var statusBar = CreateStatusBar();
            mainContainer.Controls.Add(statusBar, 0, 3);

            this.Controls.Clear();
            this.Controls.Add(mainContainer);
        }

        private Panel CreateHeaderPanel()
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = primaryColor,
                Padding = new Padding(20, 10, 20, 10)
            };

            // Logo i tytuł
            var lblTitle = new Label
            {
                Text = "📊 SYSTEM ANALIZY CEN",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            // Przyciski akcji
            var flowPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                AutoSize = true,
                Padding = new Padding(0, 10, 0, 0)
            };

            var btnRefresh = CreateHeaderButton("🔄 Odśwież", async (s, e) => await LoadDataAsync());
            var btnExport = CreateHeaderButton("📤 Eksport", ShowExportMenu);
            var btnSettings = CreateHeaderButton("⚙️ Ustawienia", ShowSettings);

            flowPanel.Controls.AddRange(new[] { btnSettings, btnExport, btnRefresh });

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(flowPanel);

            return headerPanel;
        }

        private Panel CreateGlobalFilterPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(20, 15, 20, 15)
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // Label główny
            var lblFilter = new Label
            {
                Text = "🔍 FILTRUJ DANE:",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = primaryColor,
                AutoSize = true,
                Margin = new Padding(0, 5, 20, 0)
            };

            // Data od
            var lblFrom = new Label
            {
                Text = "Od:",
                AutoSize = true,
                Margin = new Padding(0, 8, 5, 0)
            };

            mainDateFrom = new DateTimePicker
            {
                Width = 110,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(-3), // 3 miesiące wstecz
                Font = new Font("Segoe UI", 9F)
            };
            mainDateFrom.ValueChanged += async (s, e) => await RefreshAllDataAsync();

            // Data do
            var lblTo = new Label
            {
                Text = "Do:",
                AutoSize = true,
                Margin = new Padding(20, 8, 5, 0)
            };

            mainDateTo = new DateTimePicker
            {
                Width = 110,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 9F)
            };
            mainDateTo.ValueChanged += async (s, e) => await RefreshAllDataAsync();

            // Weekendy
            chkShowWeekends = new CheckBox
            {
                Text = "Pokaż weekendy",
                AutoSize = true,
                Checked = false, // Domyślnie bez weekendów
                Margin = new Padding(30, 7, 0, 0)
            };
            chkShowWeekends.CheckedChanged += async (s, e) => await RefreshAllDataAsync();

            flowPanel.Controls.AddRange(new Control[] {
                lblFilter, lblFrom, mainDateFrom, lblTo, mainDateTo, chkShowWeekends
            });

            panel.Controls.Add(flowPanel);
            return panel;
        }

        private Button CreateHeaderButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(100, 35),
                Margin = new Padding(5, 0, 5, 0),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderColor = Color.White;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 255, 255, 255);
            button.Click += onClick;

            return button;
        }

        private TabControl CreateTabControl()
        {
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Padding = new Point(20, 10)
            };

            // Tab 1: Dane
            var tabData = new TabPage("📋 Dane")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateDataTab(tabData);

            // Tab 2: Wykres Zakupowy
            var tabPurchaseChart = new TabPage("📈 Wykres Zakupowy")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreatePurchaseChartTab(tabPurchaseChart);

            // Tab 3: Wykres Sprzedażowy
            var tabSalesChart = new TabPage("💰 Wykres Sprzedażowy")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateSalesChartTab(tabSalesChart);

            // Tab 4: Statystyki
            var tabStats = new TabPage("📊 Statystyki")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateStatsTab(tabStats);

            tabControl.TabPages.AddRange(new[] { tabData, tabPurchaseChart, tabSalesChart, tabStats });

            return tabControl;
        }

        private void CreateDataTab(TabPage tab)
        {
            // DataGridView bez dodatkowego panelu filtrów (używamy globalnego)
            var grid = CreateModernDataGrid();
            tab.Controls.Add(grid);
        }

        private DataGridView CreateModernDataGrid()
        {
            if (dataGridView1 == null)
                dataGridView1 = new DataGridView();

            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridView1.GridColor = Color.FromArgb(230, 230, 230);
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = true;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Style nagłówków
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = primaryColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                SelectionBackColor = primaryColor,
                SelectionForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(5)
            };
            dataGridView1.ColumnHeadersHeight = 40;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Style wierszy
            dataGridView1.DefaultCellStyle = new DataGridViewCellStyle
            {
                SelectionBackColor = Color.FromArgb(230, 240, 255),
                SelectionForeColor = Color.Black,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(5, 3, 5, 3)
            };

            dataGridView1.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 248, 248)
            };

            dataGridView1.RowTemplate.Height = 35;

            // Menu kontekstowe
            CreateContextMenu();

            // Zdarzenia
            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            dataGridView1.Visible = true;

            return dataGridView1;
        }

        private void CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("📋 Kopiuj", null, (s, e) => CopySelectedRows());
            menu.Items.Add("📊 Eksport do CSV", null, async (s, e) => await ExportToCSVAsync());

            dataGridView1.ContextMenuStrip = menu;
        }

        private void CreatePurchaseChartTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };

            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel kontroli wykresu zakupowego
            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(10, 5, 10, 5)
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblSeries = new Label
            {
                Text = "Wyświetl serie:",
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            // Checkboxy dla cen zakupowych
            chkMinister = new CheckBox
            {
                Text = "Ministerialna",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkMinister.CheckedChanged += (s, e) => UpdatePurchaseChart();

            chkLaczona = new CheckBox
            {
                Text = "Łączona",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkLaczona.CheckedChanged += (s, e) => UpdatePurchaseChart();

            chkRolnicza = new CheckBox
            {
                Text = "Rolnicza",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkRolnicza.CheckedChanged += (s, e) => UpdatePurchaseChart();

            chkWolnorynkowa = new CheckBox
            {
                Text = "Wolnorynkowa",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkWolnorynkowa.CheckedChanged += (s, e) => UpdatePurchaseChart();

            flowPanel.Controls.AddRange(new Control[] {
                lblSeries, chkMinister, chkLaczona, chkRolnicza, chkWolnorynkowa
            });

            controlPanel.Controls.Add(flowPanel);

            // Wykres zakupowy
            purchaseChart = CreateEnhancedChart("Wykres Zakupowy - Ceny Skupu");

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(purchaseChart, 0, 1);

            tab.Controls.Add(container);
        }

        private void CreateSalesChartTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };

            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel kontroli wykresu sprzedażowego
            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(10, 5, 10, 5)
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblSeries = new Label
            {
                Text = "Wyświetl serie:",
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            // Checkboxy dla cen sprzedażowych
            chkTuszkaZrzeszenia = new CheckBox
            {
                Text = "Tuszka Zrzeszenia (CenaTuszki)",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkTuszkaZrzeszenia.CheckedChanged += (s, e) => UpdateSalesChart();

            chkNaszaTuszka = new CheckBox
            {
                Text = "Nasza Tuszka (śr. sprzedaży)",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkNaszaTuszka.CheckedChanged += (s, e) => UpdateSalesChart();

            flowPanel.Controls.AddRange(new Control[] {
                lblSeries, chkTuszkaZrzeszenia, chkNaszaTuszka
            });

            controlPanel.Controls.Add(flowPanel);

            // Wykres sprzedażowy
            salesChart = CreateEnhancedChart("Wykres Sprzedażowy - Ceny Tuszki");

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(salesChart, 0, 1);

            tab.Controls.Add(container);
        }

        private Chart CreateEnhancedChart(string title)
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderlineColor = Color.FromArgb(200, 200, 200),
                BorderlineDashStyle = ChartDashStyle.Solid,
                BorderlineWidth = 1,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            // Ulepszony obszar wykresu
            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.White,
                BackSecondaryColor = Color.FromArgb(250, 250, 250),
                BackGradientStyle = GradientStyle.TopBottom,
                ShadowColor = Color.FromArgb(30, 0, 0, 0),
                ShadowOffset = 2
            };

            // Ulepszona oś X
            chartArea.AxisX.Title = "Data";
            chartArea.AxisX.TitleFont = new Font("Segoe UI Semibold", 11F);
            chartArea.AxisX.TitleForeColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisX.LabelStyle.Format = "dd.MM";
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 9F);
            chartArea.AxisX.LabelStyle.ForeColor = Color.FromArgb(80, 80, 80);
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisX.MinorGrid.Enabled = true;
            chartArea.AxisX.MinorGrid.LineColor = Color.FromArgb(240, 240, 240);
            chartArea.AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea.AxisX.LabelStyle.Angle = -45;
            chartArea.AxisX.LineColor = Color.FromArgb(180, 180, 180);
            chartArea.AxisX.MajorTickMark.LineColor = Color.FromArgb(180, 180, 180);

            // Ulepszona oś Y
            chartArea.AxisY.Title = "Cena (PLN)";
            chartArea.AxisY.TitleFont = new Font("Segoe UI Semibold", 11F);
            chartArea.AxisY.TitleForeColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisY.LabelStyle.Format = "#,##0.00 zł";
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 9F);
            chartArea.AxisY.LabelStyle.ForeColor = Color.FromArgb(80, 80, 80);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisY.MinorGrid.Enabled = true;
            chartArea.AxisY.MinorGrid.LineColor = Color.FromArgb(240, 240, 240);
            chartArea.AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.LineColor = Color.FromArgb(180, 180, 180);
            chartArea.AxisY.MajorTickMark.LineColor = Color.FromArgb(180, 180, 180);
            chartArea.AxisY.IsStartedFromZero = false;

            // Marginesy dla lepszej czytelności
            chartArea.Position = new ElementPosition(5, 5, 90, 85);
            chartArea.InnerPlotPosition = new ElementPosition(10, 5, 85, 85);

            chart.ChartAreas.Add(chartArea);

            // Ulepszona legenda
            var legend = new Legend
            {
                Name = "MainLegend",
                Docking = Docking.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Alignment = StringAlignment.Center,
                BorderColor = Color.FromArgb(200, 200, 200),
                BorderDashStyle = ChartDashStyle.Solid,
                BorderWidth = 1,
                ShadowOffset = 1,
                ShadowColor = Color.FromArgb(20, 0, 0, 0)
            };
            chart.Legends.Add(legend);

            // Tytuł wykresu
            var chartTitle = new Title
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 14F),
                ForeColor = Color.FromArgb(40, 40, 40),
                Docking = Docking.Top,
                DockingOffset = -5
            };
            chart.Titles.Add(chartTitle);

            return chart;
        }

        private void CreateStatsTab(TabPage tab)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20)
            };

            tab.Controls.Add(panel);
            tab.Tag = panel;
        }

        private StatusStrip CreateStatusBar()
        {
            var statusBar = new StatusStrip
            {
                BackColor = Color.FromArgb(240, 240, 240),
                SizingGrip = false
            };

            var lblStatus = new ToolStripStatusLabel
            {
                Name = "lblStatus",
                Text = "Gotowy",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblRecords = new ToolStripStatusLabel
            {
                Name = "lblRecords",
                Text = "Rekordów: 0",
                BorderSides = ToolStripStatusLabelBorderSides.Left
            };

            var lblTime = new ToolStripStatusLabel
            {
                Name = "lblTime",
                Text = DateTime.Now.ToString("HH:mm:ss"),
                BorderSides = ToolStripStatusLabelBorderSides.Left
            };

            statusBar.Items.AddRange(new ToolStripItem[] { lblStatus, lblRecords, lblTime });

            // Timer do aktualizacji czasu
            var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) => lblTime.Text = DateTime.Now.ToString("HH:mm:ss");
            timer.Start();

            return statusBar;
        }

        #endregion

        #region Ładowanie i przetwarzanie danych

        private async Task LoadDataAsync()
        {
            try
            {
                UpdateStatusMessage("Ładowanie danych...");

                DataTable libraData = null;
                Dictionary<DateTime, decimal> naszaTuszkaData = null;

                await Task.Run(() =>
                {
                    // 1. Ładuj dane z LibraNet (ceny skupu + tuszka zrzeszenia)
                    using (var connection = new SqlConnection(connectionString))
                    {
                        var query = GetOptimizedQuery();
                        var adapter = new SqlDataAdapter(query, connection);
                        libraData = new DataTable();
                        adapter.Fill(libraData);
                    }

                    // 2. Ładuj średnią cenę sprzedaży tuszki z bazy Handel
                    naszaTuszkaData = LoadNaszaTuszkaFromHandel();
                });

                // 3. Uzupełnij kolumny "Nasza Tuszka" i "Różnica Tuszek" danymi z Handel
                if (naszaTuszkaData != null && naszaTuszkaData.Count > 0 && libraData != null)
                {
                    foreach (DataRow row in libraData.Rows)
                    {
                        if (row["DataRaw"] != DBNull.Value)
                        {
                            var date = Convert.ToDateTime(row["DataRaw"]).Date;
                            if (naszaTuszkaData.TryGetValue(date, out decimal naszaCena))
                            {
                                row["Nasza Tuszka"] = naszaCena;
                                if (row["Tuszka Zrzeszenia"] != DBNull.Value)
                                {
                                    row["Różnica Tuszek"] = naszaCena - Convert.ToDecimal(row["Tuszka Zrzeszenia"]);
                                }
                            }
                        }
                    }
                }

                originalData = libraData;

                // Zmień nazwy kolumn na bardziej czytelne
                RenameColumns();

                // Zastosuj filtry od razu
                await FilterDataAsync();

                UpdateStatusMessage($"Załadowano {originalData.Rows.Count} rekordów");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusMessage("Błąd ładowania");

                // Załaduj przykładowe dane
                LoadSampleData();
                await FilterDataAsync();
            }
        }

        private Dictionary<DateTime, decimal> LoadNaszaTuszkaFromHandel()
        {
            var result = new Dictionary<DateTime, decimal>();
            try
            {
                using (var conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();
                    string query = @"
                        SELECT
                            CAST(DP.[data] AS DATE) AS Data,
                            CAST(ROUND(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0), 2) AS DECIMAL(10,2)) AS NaszaTuszka
                        FROM [HANDEL].[HM].[DP] DP
                        INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id]
                        WHERE DP.[kod] = 'Kurczak A'
                            AND TW.[katalog] = 67095
                            AND DP.[data] >= '2023-01-01'
                            AND DP.[ilosc] > 0
                        GROUP BY CAST(DP.[data] AS DATE)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 30;
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                                {
                                    result[reader.GetDateTime(0)] = reader.GetDecimal(1);
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Handel DB niedostępny - kolumna zostanie pusta */ }
            return result;
        }

        private async Task RefreshAllDataAsync()
        {
            await FilterDataAsync();
        }

        private string GetOptimizedQuery()
        {
            return @"
    WITH CTE_Ministerialna AS (
        SELECT [Data], CAST([Cena] AS DECIMAL(10, 2)) AS CenaMinisterialna
        FROM [LibraNet].[dbo].[CenaMinisterialna]
        WHERE [Data] >= '2023-01-01'
    ),
    CTE_Rolnicza AS (
        SELECT [Data], CAST([Cena] AS DECIMAL(10, 2)) AS CenaRolnicza
        FROM [LibraNet].[dbo].[CenaRolnicza]
        WHERE [Data] >= '2023-01-01'
    ),
    CTE_Tuszka AS (
        SELECT [Data], CAST([Cena] AS DECIMAL(10, 2)) AS CenaTuszki
        FROM [LibraNet].[dbo].[CenaTuszki]
        WHERE [Data] >= '2023-01-01'
    ),
    CTE_Harmonogram AS (
        SELECT
            DataOdbioru AS Data,
            CAST(
                SUM(CAST(Cena AS DECIMAL(18, 4)) * CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18, 2)))
                / NULLIF(SUM(CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18, 2))), 0)
            AS DECIMAL(10, 2)) AS SredniaCena
        FROM [LibraNet].[dbo].[HarmonogramDostaw]
        WHERE LOWER(TypCeny) IN ('wolnyrynek', 'wolnorynkowa')
            AND Bufor = 'Potwierdzony'
            AND DataOdbioru >= '2023-01-01'
            AND Cena IS NOT NULL AND Cena > 0
            AND SztukiDek IS NOT NULL AND SztukiDek > 0
        GROUP BY DataOdbioru
    )
    SELECT
        COALESCE(M.Data, R.Data, T.Data) AS DataRaw,
        FORMAT(COALESCE(M.Data, R.Data, T.Data), 'yyyy-MM-dd ddd', 'pl-PL') AS Data,
        CAST(M.CenaMinisterialna AS DECIMAL(10,2)) AS Minister,
        CASE
            WHEN M.CenaMinisterialna IS NOT NULL AND R.CenaRolnicza IS NOT NULL
            THEN CAST((M.CenaMinisterialna + R.CenaRolnicza) / 2.0 AS DECIMAL(10,2))
            ELSE NULL
        END AS Laczona,
        CAST(R.CenaRolnicza AS DECIMAL(10,2)) AS Rolnicza,
        CAST(HD.SredniaCena AS DECIMAL(10,2)) AS Wolnorynkowa,
        CASE
            WHEN R.CenaRolnicza IS NOT NULL AND HD.SredniaCena IS NOT NULL
            THEN CAST(R.CenaRolnicza - HD.SredniaCena AS DECIMAL(10,2))
            ELSE NULL
        END AS [Rolnicza-Wolny],
        CAST(T.CenaTuszki AS DECIMAL(10,2)) AS [Tuszka Zrzeszenia],
        CAST(NULL AS DECIMAL(10,2)) AS [Nasza Tuszka],
        CAST(NULL AS DECIMAL(10,2)) AS [Różnica Tuszek]
    FROM CTE_Ministerialna M
    FULL OUTER JOIN CTE_Rolnicza R ON M.Data = R.Data
    FULL OUTER JOIN CTE_Tuszka T ON COALESCE(M.Data, R.Data) = T.Data
    LEFT JOIN CTE_Harmonogram HD ON COALESCE(M.Data, R.Data, T.Data) = HD.Data
    WHERE COALESCE(M.Data, R.Data, T.Data) >= '2023-01-01'
    ORDER BY DataRaw DESC";
        }

        private void RenameColumns()
        {
            if (originalData == null) return;

            var columnNames = new Dictionary<string, string>
            {
                ["NaszaCena"] = "Nasza Cena",
                ["CenaZrzeszenia"] = "Cena Zrzeszenia",
                ["RolniczaWolny"] = "Rolnicza-Wolny",
                ["NaszaZrzeszenie"] = "Nasza-Zrzeszenie"
            };

            foreach (var kvp in columnNames)
            {
                if (originalData.Columns.Contains(kvp.Key))
                {
                    originalData.Columns[kvp.Key].ColumnName = kvp.Value;
                }
            }
        }

        private void LoadSampleData()
        {
            originalData = new DataTable();
            originalData.Columns.Add("DataRaw", typeof(DateTime));
            originalData.Columns.Add("Data", typeof(string));
            originalData.Columns.Add("Minister", typeof(decimal));
            originalData.Columns.Add("Laczona", typeof(decimal));
            originalData.Columns.Add("Rolnicza", typeof(decimal));
            originalData.Columns.Add("Wolnorynkowa", typeof(decimal));
            originalData.Columns.Add("Rolnicza-Wolny", typeof(decimal));
            originalData.Columns.Add("Tuszka Zrzeszenia", typeof(decimal));
            originalData.Columns.Add("Nasza Tuszka", typeof(decimal));
            originalData.Columns.Add("Różnica Tuszek", typeof(decimal));

            var random = new Random();
            for (int i = 0; i < 120; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var basePrice = 7.50m + (decimal)(random.NextDouble() * 2 - 1);

                var minister = Math.Round(basePrice + (decimal)(random.NextDouble() * 0.3 - 0.15), 2);
                var rolnicza = Math.Round(basePrice + 0.10m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2);
                var laczona = Math.Round((minister + rolnicza) / 2, 2);
                var wolnorynkowa = Math.Round(basePrice - 0.10m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2);
                var tuszkaZrzesz = Math.Round(basePrice + 0.25m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2);
                var naszaTuszka = Math.Round(basePrice + 0.30m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2);

                originalData.Rows.Add(
                    date,
                    date.ToString("yyyy-MM-dd ddd"),
                    minister,
                    laczona,
                    rolnicza,
                    wolnorynkowa,
                    Math.Round(rolnicza - wolnorynkowa, 2),
                    tuszkaZrzesz,
                    naszaTuszka,
                    Math.Round(naszaTuszka - tuszkaZrzesz, 2)
                );
            }
        }

        private void FormatDataGrid()
        {
            if (dataGridView1.Columns.Count == 0) return;

            // Ukryj kolumnę DataRaw
            if (dataGridView1.Columns.Contains("DataRaw"))
                dataGridView1.Columns["DataRaw"].Visible = false;

            // Czytelne nagłówki kolumn
            var headerNames = new Dictionary<string, string>
            {
                ["Data"] = "Data",
                ["Minister"] = "Ministerialna",
                ["Laczona"] = "Łączona",
                ["Rolnicza"] = "Rolnicza",
                ["Wolnorynkowa"] = "Wolnorynkowa",
                ["Rolnicza-Wolny"] = "Roln. - Wolny",
                ["Tuszka Zrzeszenia"] = "Tusz. Zrzeszenia",
                ["Nasza Tuszka"] = "Nasza Tuszka",
                ["Różnica Tuszek"] = "Różnica Tuszek"
            };

            foreach (var kvp in headerNames)
            {
                if (dataGridView1.Columns.Contains(kvp.Key))
                    dataGridView1.Columns[kvp.Key].HeaderText = kvp.Value;
            }

            // Tooltips z opisami źródeł danych
            var tooltips = new Dictionary<string, string>
            {
                ["Minister"] = "Cena ministerialna (tabela CenaMinisterialna)",
                ["Laczona"] = "Średnia z ceny ministerialnej i rolniczej",
                ["Rolnicza"] = "Cena rolnicza (tabela CenaRolnicza)",
                ["Wolnorynkowa"] = "Średnia ważona ilością sztuk z dostaw wolnorynkowych",
                ["Rolnicza-Wolny"] = "Różnica: Rolnicza minus Wolnorynkowa",
                ["Tuszka Zrzeszenia"] = "Cena tuszki zrzeszenia (tabela CenaTuszki)",
                ["Nasza Tuszka"] = "Średnia cena sprzedaży Kurczak A (system Handel)",
                ["Różnica Tuszek"] = "Różnica: Nasza Tuszka minus Tuszka Zrzeszenia"
            };

            foreach (var kvp in tooltips)
            {
                if (dataGridView1.Columns.Contains(kvp.Key))
                    dataGridView1.Columns[kvp.Key].HeaderCell.ToolTipText = kvp.Value;
            }

            // Formatowanie kolumn numerycznych
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (col.ValueType == typeof(decimal) || col.ValueType == typeof(double) || col.ValueType == typeof(float))
                {
                    col.DefaultCellStyle.Format = "0.00 zł";
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }

            // Szerokość kolumny Data
            if (dataGridView1.Columns.Contains("Data"))
                dataGridView1.Columns["Data"].Width = 150;

            // Ustaw kolejność kolumn
            SetColumnOrder();
        }

        private void SetColumnOrder()
        {
            var columnOrder = new[] {
                "Data", "Minister", "Laczona", "Rolnicza", "Wolnorynkowa", "Rolnicza-Wolny",
                "Tuszka Zrzeszenia", "Nasza Tuszka", "Różnica Tuszek"
            };

            int displayIndex = 0;
            foreach (var colName in columnOrder)
            {
                if (dataGridView1.Columns.Contains(colName))
                {
                    dataGridView1.Columns[colName].DisplayIndex = displayIndex++;
                }
            }
        }

        #endregion

        #region Filtrowanie danych

        private async Task FilterDataAsync()
        {
            if (originalData == null) return;

            await Task.Run(() =>
            {
                var dateFrom = mainDateFrom?.Value.Date ?? DateTime.MinValue;
                var dateTo = mainDateTo?.Value.Date ?? DateTime.MaxValue;
                var showWeekends = chkShowWeekends?.Checked ?? false;

                var filtered = originalData.AsEnumerable().Where(row =>
                {
                    // Filtr dat
                    if (row["DataRaw"] != DBNull.Value)
                    {
                        var date = Convert.ToDateTime(row["DataRaw"]);
                        if (date < dateFrom || date > dateTo)
                            return false;

                        // Filtr weekendów (domyślnie ukryte)
                        if (!showWeekends && (date.DayOfWeek == DayOfWeek.Saturday ||
                                             date.DayOfWeek == DayOfWeek.Sunday))
                            return false;
                    }

                    return true;
                });

                this.Invoke(new Action(() =>
                {
                    filteredData = filtered.Any() ? filtered.CopyToDataTable() : originalData.Clone();
                    dataGridView1.DataSource = filteredData;
                    FormatDataGrid();
                    UpdatePurchaseChart();
                    UpdateSalesChart();
                    UpdateStatistics();
                    UpdateStatusMessage($"Wyświetlono {filteredData.Rows.Count} z {originalData.Rows.Count} rekordów");
                }));
            });
        }

        #endregion

        #region Wykresy

        private void UpdatePurchaseChart()
        {
            if (purchaseChart == null || filteredData == null || filteredData.Rows.Count == 0) return;

            purchaseChart.Series.Clear();

            // Konfiguracja serii dla wykresu zakupowego
            var seriesConfig = new[]
            {
                new { Name = "Minister", Column = "Minister", Color = Color.FromArgb(41, 128, 185), Check = chkMinister },
                new { Name = "Łączona", Column = "Laczona", Color = Color.FromArgb(155, 89, 182), Check = chkLaczona },
                new { Name = "Rolnicza", Column = "Rolnicza", Color = Color.FromArgb(46, 204, 113), Check = chkRolnicza },
                new { Name = "Wolnorynkowa", Column = "Wolnorynkowa", Color = Color.FromArgb(231, 76, 60), Check = chkWolnorynkowa }
            };

            foreach (var config in seriesConfig)
            {
                if (config.Check == null || !config.Check.Checked) continue;
                if (!filteredData.Columns.Contains(config.Column)) continue;

                var series = CreateChartSeries(config.Name, config.Column, config.Color);
                if (series != null && series.Points.Count > 0)
                {
                    // Dodaj etykietę z wartością na końcu linii
                    AddEndLabel(series);
                    purchaseChart.Series.Add(series);
                }
            }

            AdjustChartScale(purchaseChart);
        }

        private void UpdateSalesChart()
        {
            if (salesChart == null || filteredData == null || filteredData.Rows.Count == 0) return;

            salesChart.Series.Clear();

            // Konfiguracja serii dla wykresu sprzedażowego
            var seriesConfig = new[]
            {
                new { Name = "Tuszka Zrzeszenia", Column = "Tuszka Zrzeszenia", Color = Color.FromArgb(230, 126, 34), Check = chkTuszkaZrzeszenia },
                new { Name = "Nasza Tuszka", Column = "Nasza Tuszka", Color = Color.FromArgb(46, 204, 113), Check = chkNaszaTuszka }
            };

            foreach (var config in seriesConfig)
            {
                if (config.Check == null || !config.Check.Checked) continue;
                if (!filteredData.Columns.Contains(config.Column)) continue;

                var series = CreateChartSeries(config.Name, config.Column, config.Color);
                if (series != null && series.Points.Count > 0)
                {
                    // Dodaj etykietę z wartością na końcu linii
                    AddEndLabel(series);
                    salesChart.Series.Add(series);
                }
            }

            AdjustChartScale(salesChart);
        }

        private Series CreateChartSeries(string name, string column, Color color)
        {
            var series = new Series(name)
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                Color = color,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 6,
                MarkerColor = color,
                MarkerBorderColor = Color.White,
                MarkerBorderWidth = 2,
                ToolTip = "#SERIESNAME\nData: #VALX{dd.MM.yyyy}\nCena: #VALY{#,##0.00} zł",
                IsVisibleInLegend = true,
                IsValueShownAsLabel = false
            };

            // Dodaj efekty wizualne
            series["ShadowOffset"] = "1";
            series["EmptyPointValue"] = "Average";

            var validRows = filteredData.AsEnumerable()
                .Where(r => r["DataRaw"] != DBNull.Value && r[column] != DBNull.Value)
                .OrderBy(r => r["DataRaw"])
                .ToList();

            foreach (var row in validRows)
            {
                var value = Convert.ToDouble(row[column]);
                series.Points.AddXY(Convert.ToDateTime(row["DataRaw"]), Math.Round(value, 2));
            }

            return series;
        }

        private void AddEndLabel(Series series)
        {
            if (series.Points.Count == 0) return;

            var lastPoint = series.Points[series.Points.Count - 1];
            lastPoint.IsValueShownAsLabel = true;
            lastPoint.LabelFormat = "0.00 zł";
            lastPoint.LabelBackColor = Color.White;
            lastPoint.LabelBorderColor = series.Color;
            lastPoint.LabelBorderWidth = 1;
            lastPoint.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lastPoint.LabelForeColor = series.Color;
        }

        private void AdjustChartScale(Chart chart)
        {
            if (chart.Series.Count == 0 || !chart.Series.SelectMany(s => s.Points).Any()) return;

            var allValues = chart.Series.SelectMany(s => s.Points).Select(p => p.YValues[0]).ToList();
            if (allValues.Any())
            {
                var min = allValues.Min();
                var max = allValues.Max();
                var range = max - min;
                // Minimalny margines 0.1 gdy wszystkie wartości są identyczne
                var margin = Math.Max(0.1, range * 0.15); // 15% margines

                chart.ChartAreas[0].AxisY.Minimum = Math.Floor((min - margin) * 100) / 100;
                chart.ChartAreas[0].AxisY.Maximum = Math.Ceiling((max + margin) * 100) / 100;

                // Inteligentny interwał
                var interval = CalculateOptimalInterval(range);
                chart.ChartAreas[0].AxisY.Interval = interval;
            }

            // Dostosuj interwał osi X
            if (filteredData != null && filteredData.Rows.Count > 0)
            {
                var dateFrom = mainDateFrom.Value;
                var dateTo = mainDateTo.Value;
                var daysDiff = (dateTo - dateFrom).Days;

                if (daysDiff <= 7)
                    chart.ChartAreas[0].AxisX.Interval = 1;
                else if (daysDiff <= 30)
                    chart.ChartAreas[0].AxisX.Interval = 2;
                else if (daysDiff <= 90)
                    chart.ChartAreas[0].AxisX.Interval = 7;
                else
                    chart.ChartAreas[0].AxisX.Interval = 14;

                chart.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Days;
            }

            chart.Invalidate();
        }

        private double CalculateOptimalInterval(double range)
        {
            var targetLines = 8;
            var rawInterval = range / targetLines;
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawInterval)));
            var normalized = rawInterval / magnitude;

            double niceInterval;
            if (normalized <= 1) niceInterval = 1;
            else if (normalized <= 2) niceInterval = 2;
            else if (normalized <= 5) niceInterval = 5;
            else niceInterval = 10;

            return niceInterval * magnitude;
        }

        #endregion

        #region Statystyki

        private void UpdateStatistics()
        {
            var statsPanel = mainTabControl?.TabPages[3]?.Tag as FlowLayoutPanel;
            if (statsPanel == null || filteredData == null) return;

            statsPanel.Controls.Clear();

            // Dodaj informację o zakresie dat
            var dateRangeCard = CreateDateRangeCard();
            statsPanel.Controls.Add(dateRangeCard);

            var priceColumns = new[] { "Minister", "Laczona", "Rolnicza", "Wolnorynkowa",
                                       "Tuszka Zrzeszenia", "Nasza Tuszka" };

            foreach (var colName in priceColumns)
            {
                if (!filteredData.Columns.Contains(colName)) continue;

                var values = filteredData.AsEnumerable()
                    .Where(r => r[colName] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r[colName]))
                    .ToList();

                if (values.Count == 0) continue;

                var statsCard = CreateStatsCard(colName, values);
                statsPanel.Controls.Add(statsCard);
            }
        }

        private Panel CreateDateRangeCard()
        {
            var card = new Panel
            {
                Size = new Size(320, 80),
                BackColor = primaryColor,
                Margin = new Padding(10)
            };

            var lblTitle = new Label
            {
                Text = "📅 Zakres analizy",
                Font = new Font("Segoe UI Semibold", 12F),
                ForeColor = Color.White,
                Location = new Point(15, 15),
                AutoSize = true
            };

            var lblRange = new Label
            {
                Text = $"Od: {mainDateFrom.Value:dd.MM.yyyy}\n" +
                       $"Do: {mainDateTo.Value:dd.MM.yyyy}",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.White,
                Location = new Point(15, 40),
                AutoSize = true
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblRange);

            return card;
        }

        private Panel CreateStatsCard(string title, List<decimal> values)
        {
            var card = new Panel
            {
                Size = new Size(320, 180),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(10)
            };

            // Dodaj efekt cienia
            card.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (var path = GetRoundedRectangle(rect, 8))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    // Cień
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                    {
                        e.Graphics.TranslateTransform(2, 2);
                        e.Graphics.FillPath(shadowBrush, path);
                        e.Graphics.ResetTransform();
                    }

                    // Tło karty
                    using (var bgBrush = new SolidBrush(Color.White))
                    {
                        e.Graphics.FillPath(bgBrush, path);
                    }

                    // Obramowanie
                    using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 12F),
                ForeColor = primaryColor,
                Location = new Point(15, 15),
                AutoSize = true
            };

            var avg = values.Average();
            var min = values.Min();
            var max = values.Max();
            var stdDev = CalculateStandardDeviation(values);

            var lblStats = new Label
            {
                Text = $"📊 Średnia: {avg:N2} zł\n" +
                       $"📉 Minimum: {min:N2} zł\n" +
                       $"📈 Maximum: {max:N2} zł\n" +
                       $"📏 Rozstęp: {(max - min):N2} zł\n" +
                       $"📐 Odch. std.: {stdDev:N2} zł",
                Location = new Point(15, 45),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F)
            };

            // Trend indicator
            var trend = CalculateTrend(values);
            var lblTrend = new Label
            {
                Text = trend > 0 ? "↗ Trend wzrostowy" : trend < 0 ? "↘ Trend spadkowy" : "→ Trend stabilny",
                Location = new Point(15, 140),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = trend > 0 ? successColor : trend < 0 ? dangerColor : Color.Gray
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblStats);
            card.Controls.Add(lblTrend);

            return card;
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private decimal CalculateStandardDeviation(List<decimal> values)
        {
            if (values.Count <= 1) return 0;

            var avg = values.Average();
            var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumOfSquares / (values.Count - 1)));
        }

        private decimal CalculateTrend(List<decimal> values)
        {
            if (values.Count < 2) return 0;

            var firstHalf = values.Take(values.Count / 2).Average();
            var secondHalf = values.Skip(values.Count / 2).Average();
            return secondHalf - firstHalf;
        }

        #endregion

        #region Zdarzenia UI

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridView1.Rows[e.RowIndex];

            // Podświetl dzisiejszą datę
            if (row.Cells["DataRaw"].Value != DBNull.Value)
            {
                var date = Convert.ToDateTime(row.Cells["DataRaw"].Value);
                if (date.Date == DateTime.Today)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 196);
                    row.DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                }
            }

            // Kolorowanie różnic
            if (dataGridView1.Columns[e.ColumnIndex].Name == "Rolnicza-Wolny" ||
                dataGridView1.Columns[e.ColumnIndex].Name == "Różnica Tuszek")
            {
                if (e.Value != null && e.Value.ToString().Replace(" zł", "").Replace(",", ".") is string valStr &&
                    decimal.TryParse(valStr, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out decimal val))
                {
                    e.CellStyle.ForeColor = val >= 0 ? successColor : dangerColor;
                    e.CellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                }
            }
        }

        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridView1.Rows[e.RowIndex];
            ShowDetailedInfo(row);
        }

        private void ShowDetailedInfo(DataGridViewRow row)
        {
            var form = new Form
            {
                Text = $"Szczegóły - {row.Cells["Data"].Value}",
                Size = new Size(550, 450),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.White
            };

            var textBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };

            textBox.AppendText($"📅 Data: {row.Cells["Data"].Value}\n\n");
            textBox.AppendText("💰 CENY SKUPU:\n");
            AppendFormattedPrice(textBox, "Ministerialna", row.Cells["Minister"].Value);
            AppendFormattedPrice(textBox, "Łączona (śr. minister. + roln.)", row.Cells["Laczona"].Value);
            AppendFormattedPrice(textBox, "Rolnicza", row.Cells["Rolnicza"].Value);
            AppendFormattedPrice(textBox, "Wolnorynkowa (śr. ważona szt.)", row.Cells["Wolnorynkowa"].Value);

            textBox.AppendText("\n🏭 CENY TUSZKI:\n");
            AppendFormattedPrice(textBox, "Tuszka Zrzeszenia (CenaTuszki)", row.Cells["Tuszka Zrzeszenia"].Value);
            AppendFormattedPrice(textBox, "Nasza Tuszka (śr. sprzedaży)", row.Cells["Nasza Tuszka"].Value);

            textBox.AppendText("\n📊 RÓŻNICE:\n");
            AppendFormattedPrice(textBox, "Rolnicza - Wolnorynkowa", row.Cells["Rolnicza-Wolny"].Value);
            AppendFormattedPrice(textBox, "Nasza Tuszka - Zrzeszenia", row.Cells["Różnica Tuszek"].Value);

            panel.Controls.Add(textBox);
            form.Controls.Add(panel);
            form.ShowDialog();
        }

        private void AppendFormattedPrice(RichTextBox rtb, string label, object value)
        {
            rtb.AppendText($"   • {label}: ");
            if (value != null && value != DBNull.Value)
            {
                var strValue = value.ToString().Replace(" zł", "");
                if (decimal.TryParse(strValue, out decimal decValue))
                {
                    rtb.AppendText($"{decValue:N2} zł\n");
                }
                else
                {
                    rtb.AppendText($"{value}\n");
                }
            }
            else
            {
                rtb.AppendText("brak danych\n");
            }
        }

        #endregion

        #region Eksport i narzędzia

        private void ShowExportMenu(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("📄 Eksport do CSV", null, async (s, ev) => await ExportToCSVAsync());
            menu.Items.Add("📊 Eksport do Excel", null, (s, ev) => ExportToExcel());
            menu.Items.Add("📑 Eksport do JSON", null, async (s, ev) => await ExportToJSONAsync());
            menu.Items.Add("🖼️ Eksport wykresów", null, (s, ev) => ExportCharts());

            var button = sender as Button;
            menu.Show(button, new Point(0, button.Height));
        }

        private void ExportCharts()
        {
            MessageBox.Show("Wybierz wykres do eksportu z odpowiedniej zakładki,\n" +
                          "a następnie użyj prawego przycisku myszy na wykresie.",
                          "Eksport wykresów", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task ExportToCSVAsync()
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV Files|*.csv";
                saveDialog.FileName = $"CenyAnaliza_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            var csv = new StringBuilder();

                            // Nagłówki
                            var headers = filteredData.Columns.Cast<DataColumn>()
                                .Where(c => c.ColumnName != "DataRaw")
                                .Select(c => c.ColumnName);
                            csv.AppendLine(string.Join(",", headers));

                            // Dane
                            foreach (DataRow row in filteredData.Rows)
                            {
                                var fields = headers.Select(h => row[h]?.ToString() ?? "");
                                csv.AppendLine(string.Join(",", fields));
                            }

                            File.WriteAllText(saveDialog.FileName, csv.ToString());
                        });

                        MessageBox.Show("Dane wyeksportowane pomyślnie!", "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async Task ExportToJSONAsync()
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON Files|*.json";
                saveDialog.FileName = $"CenyAnaliza_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            var json = DataTableToJSON(filteredData);
                            File.WriteAllText(saveDialog.FileName, json);
                        });

                        MessageBox.Show("Dane wyeksportowane pomyślnie!", "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string DataTableToJSON(DataTable table)
        {
            var rows = new List<Dictionary<string, object>>();

            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    if (col.ColumnName != "DataRaw")
                        dict[col.ColumnName] = row[col];
                }
                rows.Add(dict);
            }

            return System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private void ExportToExcel()
        {
            MessageBox.Show("Dla eksportu do Excel zalecamy użycie opcji 'Eksport do CSV',\n" +
                          "która utworzy plik możliwy do otwarcia w programie Excel.\n\n" +
                          "Dla pełnej funkcjonalności Excel można dodać bibliotekę EPPlus.",
                          "Eksport do Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CopySelectedRows()
        {
            if (dataGridView1.SelectedRows.Count == 0) return;

            var data = new StringBuilder();

            // Dodaj nagłówki
            var headers = dataGridView1.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Visible)
                .Select(c => c.HeaderText);
            data.AppendLine(string.Join("\t", headers));

            // Dodaj dane
            foreach (DataGridViewRow row in dataGridView1.SelectedRows)
            {
                var values = row.Cells.Cast<DataGridViewCell>()
                    .Where(c => c.OwningColumn.Visible)
                    .Select(c => c.Value?.ToString() ?? "");
                data.AppendLine(string.Join("\t", values));
            }

            Clipboard.SetText(data.ToString());
            UpdateStatusMessage($"Skopiowano {dataGridView1.SelectedRows.Count} wierszy do schowka");
        }

        private void ShowSettings(object sender, EventArgs e)
        {
            var form = new Form
            {
                Text = "Ustawienia",
                Size = new Size(450, 400),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F)
            };

            // Tab: Połączenie
            var tabConnection = new TabPage("Połączenie")
            {
                Padding = new Padding(10)
            };

            var lblServer = new Label
            {
                Text = "Serwer:",
                Location = new Point(10, 20),
                AutoSize = true
            };

            var txtServer = new TextBox
            {
                Text = "192.168.0.109",
                Location = new Point(100, 18),
                Width = 250
            };

            var lblDatabase = new Label
            {
                Text = "Baza danych:",
                Location = new Point(10, 50),
                AutoSize = true
            };

            var txtDatabase = new TextBox
            {
                Text = "LibraNet",
                Location = new Point(100, 48),
                Width = 250
            };

            var btnTestConnection = new Button
            {
                Text = "Test połączenia",
                Location = new Point(100, 85),
                Size = new Size(120, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };

            btnTestConnection.Click += async (s, ev) =>
            {
                btnTestConnection.Enabled = false;
                btnTestConnection.Text = "Testowanie...";

                await Task.Run(() =>
                {
                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                        }
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show("Połączenie działa poprawnie!", "Sukces",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show($"Błąd połączenia:\n{ex.Message}", "Błąd",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                });

                btnTestConnection.Text = "Test połączenia";
                btnTestConnection.Enabled = true;
            };

            tabConnection.Controls.AddRange(new Control[] {
                lblServer, txtServer, lblDatabase, txtDatabase, btnTestConnection
            });

            // Tab: Wygląd
            var tabAppearance = new TabPage("Wygląd")
            {
                Padding = new Padding(10)
            };

            var chkDarkMode = new CheckBox
            {
                Text = "Tryb ciemny (w przygotowaniu)",
                Location = new Point(10, 20),
                AutoSize = true,
                Enabled = false
            };

            tabAppearance.Controls.Add(chkDarkMode);

            tabControl.TabPages.AddRange(new[] { tabConnection, tabAppearance });

            // Przyciski dolne
            var panelButtons = new Panel
            {
                Height = 50,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(245, 245, 245)
            };

            var btnSave = new Button
            {
                Text = "Zapisz",
                DialogResult = DialogResult.OK,
                Location = new Point(250, 10),
                Size = new Size(80, 30),
                BackColor = successColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };

            var btnCancel = new Button
            {
                Text = "Anuluj",
                DialogResult = DialogResult.Cancel,
                Location = new Point(340, 10),
                Size = new Size(80, 30),
                BackColor = Color.Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };

            panelButtons.Controls.AddRange(new[] { btnSave, btnCancel });

            form.Controls.Add(tabControl);
            form.Controls.Add(panelButtons);

            if (form.ShowDialog() == DialogResult.OK)
            {
                UpdateStatusMessage("Ustawienia zapisane");
            }
        }

        private void UpdateStatusMessage(string message)
        {
            var statusBar = this.Controls.OfType<TableLayoutPanel>()
                .FirstOrDefault()?.Controls.OfType<StatusStrip>().FirstOrDefault();

            if (statusBar != null)
            {
                var statusLabel = statusBar.Items["lblStatus"] as ToolStripStatusLabel;
                var recordsLabel = statusBar.Items["lblRecords"] as ToolStripStatusLabel;

                if (statusLabel != null)
                    statusLabel.Text = message;

                if (recordsLabel != null && filteredData != null)
                    recordsLabel.Text = $"Rekordów: {filteredData.Rows.Count}";
            }
        }

        #endregion

        #region Cleanup

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Zwolnij zasoby
            if (dataGridView1 != null)
            {
                dataGridView1.DataSource = null;
                dataGridView1.Dispose();
            }

            if (purchaseChart != null)
            {
                purchaseChart.Series.Clear();
                purchaseChart.Dispose();
            }

            if (salesChart != null)
            {
                salesChart.Series.Clear();
                salesChart.Dispose();
            }

            originalData?.Dispose();
            filteredData?.Dispose();

            // Zwolnij timery
            foreach (var control in this.Controls)
            {
                if (control is Timer timer)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            }

            base.OnFormClosed(e);
        }

        #endregion
    }
}
