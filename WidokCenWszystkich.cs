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
        private DataTable originalData;
        private DataTable filteredData;
        private Chart priceChart;
        private TabControl mainTabControl;

        // Kontrolki filtrowania
        private TextBox txtSearchFilter;
        private DateTimePicker dateFromFilter;
        private DateTimePicker dateToFilter;
        private CheckBox chkShowWeekendsFilter;

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
                RowCount = 3,
                Padding = new Padding(0),
                BackColor = backgroundColor
            };

            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Header
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Content
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Status

            // 1. Header Panel
            var headerPanel = CreateHeaderPanel();
            mainContainer.Controls.Add(headerPanel, 0, 0);

            // 2. Content (TabControl)
            mainTabControl = CreateTabControl();
            mainContainer.Controls.Add(mainTabControl, 0, 1);

            // 3. Status Bar
            var statusBar = CreateStatusBar();
            mainContainer.Controls.Add(statusBar, 0, 2);

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

            // Tab 2: Wykres
            var tabChart = new TabPage("📈 Wykres")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateChartTab(tabChart);

            // Tab 3: Statystyki
            var tabStats = new TabPage("📊 Statystyki")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateStatsTab(tabStats);

            tabControl.TabPages.AddRange(new[] { tabData, tabChart, tabStats });

            return tabControl;
        }

        private void CreateDataTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.White
            };

            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Panel filtrów
            var filterPanel = CreateFilterPanel();
            container.Controls.Add(filterPanel, 0, 0);

            // DataGridView
            var grid = CreateModernDataGrid();
            container.Controls.Add(grid, 0, 1);

            tab.Controls.Add(container);
        }

        private Panel CreateFilterPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(15, 10, 15, 10)
            };

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // Wyszukiwanie
            var searchGroup = CreateFilterGroup("Szukaj:", out txtSearchFilter);
            txtSearchFilter.Width = 200;
            txtSearchFilter.TextChanged += async (s, e) => await FilterDataAsync();

            // Data od
            var dateFromGroup = CreateDatePickerGroup("Od:", out dateFromFilter);
            dateFromFilter.Value = DateTime.Today.AddMonths(-1);
            dateFromFilter.ValueChanged += async (s, e) => await FilterDataAsync();

            // Data do
            var dateToGroup = CreateDatePickerGroup("Do:", out dateToFilter);
            dateToFilter.Value = DateTime.Today;
            dateToFilter.ValueChanged += async (s, e) => await FilterDataAsync();

            // Weekendy
            chkShowWeekendsFilter = new CheckBox
            {
                Text = "Pokaż weekendy",
                AutoSize = true,
                Margin = new Padding(20, 20, 0, 0)
            };
            chkShowWeekendsFilter.CheckedChanged += async (s, e) => await FilterDataAsync();

            flowPanel.Controls.AddRange(new Control[] {
                searchGroup, dateFromGroup, dateToGroup, chkShowWeekendsFilter
            });

            panel.Controls.Add(flowPanel);
            return panel;
        }

        private Panel CreateFilterGroup(string label, out TextBox textBox)
        {
            var panel = new Panel { AutoSize = true };

            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(0, 3)
            };

            textBox = new TextBox
            {
                Location = new Point(50, 0),
                Width = 150,
                Font = new Font("Segoe UI", 9F)
            };

            panel.Controls.Add(lbl);
            panel.Controls.Add(textBox);
            panel.Height = textBox.Height;

            return panel;
        }

        private Panel CreateDatePickerGroup(string label, out DateTimePicker picker)
        {
            var panel = new Panel { AutoSize = true, Margin = new Padding(20, 0, 0, 0) };

            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(0, 3)
            };

            picker = new DateTimePicker
            {
                Location = new Point(30, 0),
                Width = 110,
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 9F)
            };

            panel.Controls.Add(lbl);
            panel.Controls.Add(picker);
            panel.Height = picker.Height;

            return panel;
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
            menu.Items.Add("-");
            menu.Items.Add("📈 Pokaż na wykresie", null, (s, e) => ShowInChart());

            dataGridView1.ContextMenuStrip = menu;
        }

        private void CreateChartTab(TabPage tab)
        {
            priceChart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderlineColor = Color.FromArgb(230, 230, 230),
                BorderlineDashStyle = ChartDashStyle.Solid,
                BorderlineWidth = 1
            };

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.White,
                BackSecondaryColor = Color.FromArgb(250, 250, 250),
                BackGradientStyle = GradientStyle.TopBottom,
                BorderColor = Color.FromArgb(200, 200, 200),
                BorderDashStyle = ChartDashStyle.Solid,
                BorderWidth = 0
            };

            // Oś X
            chartArea.AxisX.Title = "Data";
            chartArea.AxisX.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            chartArea.AxisX.LabelStyle.Format = "dd.MM";
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.LineColor = Color.FromArgb(180, 180, 180);

            // Oś Y
            chartArea.AxisY.Title = "Cena (zł)";
            chartArea.AxisY.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            chartArea.AxisY.LabelStyle.Format = "N2";
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.LineColor = Color.FromArgb(180, 180, 180);

            priceChart.ChartAreas.Add(chartArea);

            // Legenda
            var legend = new Legend
            {
                Docking = Docking.Top,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F),
                Alignment = StringAlignment.Center
            };
            priceChart.Legends.Add(legend);

            tab.Controls.Add(priceChart);
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
            tab.Tag = panel; // Zapisz referencję do późniejszego użycia
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
                Name = "lblStatus",  // Dodaj Name
                Text = "Gotowy",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblRecords = new ToolStripStatusLabel
            {
                Name = "lblRecords",  // Dodaj Name
                Text = "Rekordów: 0",
                BorderSides = ToolStripStatusLabelBorderSides.Left
            };

            var lblTime = new ToolStripStatusLabel
            {
                Name = "lblTime",  // Dodaj Name
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
                UpdateStatus("Ładowanie danych...");

                await Task.Run(() =>
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        var query = GetOptimizedQuery();
                        var adapter = new SqlDataAdapter(query, connection);
                        originalData = new DataTable();
                        adapter.Fill(originalData);
                    }
                });

                // Zmień nazwy kolumn na bardziej czytelne
                RenameColumns();

                filteredData = originalData.Copy();
                dataGridView1.DataSource = filteredData;

                FormatDataGrid();
                UpdateChart();
                UpdateStatistics();

                UpdateStatus($"Załadowano {originalData.Rows.Count} rekordów");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Błąd ładowania");

                // Załaduj przykładowe dane
                LoadSampleData();
            }
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
    CTE_HANDEL AS (
        SELECT 
            CONVERT(DATE, DP.[data]) AS Data,
            ROUND(SUM(DP.[wartNetto]) / SUM(DP.[ilosc]), 2) AS Cena
        FROM [RemoteServer].[HANDEL].[HM].[DP] DP 
        INNER JOIN [RemoteServer].[HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
        INNER JOIN [RemoteServer].[HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
        WHERE DP.[kod] = 'Kurczak A' 
            AND TW.[katalog] = 67095
            AND DP.[data] >= '2023-01-01'
        GROUP BY CONVERT(DATE, DP.[data])
    ),
    CTE_Harmonogram AS (
        SELECT 
            DataOdbioru AS Data,
            ROUND(AVG(CAST(Cena AS DECIMAL(10, 2))), 2) AS SredniaCena
        FROM [LibraNet].[dbo].[HarmonogramDostaw]
        WHERE (TypCeny = 'Wolnorynkowa' OR TypCeny = 'wolnorynkowa')
            AND Bufor = 'Potwierdzony'
            AND DataOdbioru >= '2023-01-01'
        GROUP BY DataOdbioru
    )
    SELECT 
        COALESCE(M.Data, R.Data, H.Data, T.Data) AS DataRaw,
        FORMAT(COALESCE(M.Data, R.Data, H.Data), 'yyyy-MM-dd') + ' ' + 
            LEFT(DATENAME(WEEKDAY, COALESCE(M.Data, R.Data, H.Data)), 3) AS Data,
        ROUND(M.CenaMinisterialna, 2) AS Minister,
        ROUND(R.CenaRolnicza, 2) AS Rolnicza,
        ROUND(T.CenaTuszki, 2) AS Tuszka,
        ROUND(T.CenaTuszki, 2) AS Zrzeszenie,  -- Używamy tej samej wartości tymczasowo
        ROUND(HD.SredniaCena, 2) AS Wolnorynkowa,
        ROUND((ISNULL(M.CenaMinisterialna, 0) + ISNULL(R.CenaRolnicza, 0)) / 2.0, 2) AS Laczona,
        ROUND(R.CenaRolnicza - HD.SredniaCena, 2) AS [Rolnicza-Wolny],
        ROUND(T.CenaTuszki - T.CenaTuszki, 2) AS [Nasza-Zrzeszenie]
    FROM CTE_Ministerialna M
    FULL OUTER JOIN CTE_Rolnicza R ON M.Data = R.Data
    FULL OUTER JOIN CTE_Tuszka T ON COALESCE(M.Data, R.Data) = T.Data
    FULL OUTER JOIN CTE_HANDEL H ON COALESCE(M.Data, R.Data) = H.Data
    LEFT JOIN CTE_Harmonogram HD ON COALESCE(M.Data, R.Data, H.Data) = HD.Data
    WHERE COALESCE(M.Data, R.Data, H.Data) >= '2023-01-01'
    ORDER BY DataRaw DESC";
        }

        private void RenameColumns()
        {
            if (originalData == null) return;

            var columnNames = new Dictionary<string, string>
            {
                ["NaszaCena"] = "Nasza Cena",
                ["CenaZrzeszenia"] = "Cena Zrzeszenia",
                ["Laczona"] = "Łączona",
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
            originalData.Columns.Add("Rolnicza", typeof(decimal));
            originalData.Columns.Add("Tuszka", typeof(decimal));
            originalData.Columns.Add("Zrzeszenie", typeof(decimal));
            originalData.Columns.Add("Wolnorynkowa", typeof(decimal));
            originalData.Columns.Add("Laczona", typeof(decimal));
            originalData.Columns.Add("Rolnicza-Wolny", typeof(decimal));
            originalData.Columns.Add("Nasza-Zrzeszenie", typeof(decimal));

            var random = new Random();
            for (int i = 0; i < 30; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var basePrice = 7.50m;

                originalData.Rows.Add(
                    date,
                    date.ToString("yyyy-MM-dd ddd"),
                    Math.Round(basePrice + (decimal)(random.NextDouble() * 0.3 - 0.15), 2),
                    Math.Round(basePrice + 0.10m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2),
                    Math.Round(basePrice + 0.30m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2),
                    Math.Round(basePrice + 0.25m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2),
                    Math.Round(basePrice - 0.10m + (decimal)(random.NextDouble() * 0.2 - 0.1), 2),
                    Math.Round(basePrice + 0.05m, 2),
                    Math.Round(0.20m, 2),
                    Math.Round(0.05m, 2)
                );
            }

            filteredData = originalData.Copy();
            dataGridView1.DataSource = filteredData;
        }

        private void FormatDataGrid()
        {
            if (dataGridView1.Columns.Count == 0) return;

            // Ukryj kolumnę DataRaw
            if (dataGridView1.Columns.Contains("DataRaw"))
                dataGridView1.Columns["DataRaw"].Visible = false;

            // Formatowanie kolumn liczbowych na 2 miejsca po przecinku
            string[] numericColumns = { "Minister", "Rolnicza", "Tuszka", "Zrzeszenie",
                               "Wolnorynkowa", "Laczona", "Rolnicza-Wolny", "Nasza-Zrzeszenie" };

            foreach (var colName in numericColumns)
            {
                if (dataGridView1.Columns.Contains(colName))
                {
                    var col = dataGridView1.Columns[colName];
                    col.DefaultCellStyle.Format = "0.00";  // Dokładnie 2 miejsca po przecinku
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }

            // Szerokość kolumny Data
            if (dataGridView1.Columns.Contains("Data"))
            {
                dataGridView1.Columns["Data"].Width = 140;
            }
        }

        #endregion

        #region Filtrowanie danych

        private async Task FilterDataAsync()
        {
            if (originalData == null) return;

            await Task.Run(() =>
            {
                var searchText = txtSearchFilter?.Text?.ToLower() ?? "";
                var dateFrom = dateFromFilter?.Value.Date ?? DateTime.MinValue;
                var dateTo = dateToFilter?.Value.Date ?? DateTime.MaxValue;
                var showWeekends = chkShowWeekendsFilter?.Checked ?? false;

                var filtered = originalData.AsEnumerable().Where(row =>
                {
                    // Filtr tekstowy
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        var dataStr = row["Data"]?.ToString().ToLower() ?? "";
                        if (!dataStr.Contains(searchText))
                            return false;
                    }

                    // Filtr dat
                    if (row["DataRaw"] != DBNull.Value)
                    {
                        var date = Convert.ToDateTime(row["DataRaw"]);
                        if (date < dateFrom || date > dateTo)
                            return false;

                        // Filtr weekendów
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
                    UpdateChart();
                    UpdateStatistics();
                    UpdateStatus($"Wyświetlono {filteredData.Rows.Count} z {originalData.Rows.Count} rekordów");
                }));
            });
        }

        #endregion

        #region Wykresy

        private void UpdateChart()
        {
            if (priceChart == null || filteredData == null || filteredData.Rows.Count == 0) return;

            priceChart.Series.Clear();

            // Konfiguracja serii z gradientami
            var seriesConfig = new[]
            {
        new { Name = "Minister", Column = "Minister", Color = Color.FromArgb(41, 128, 185) },
        new { Name = "Rolnicza", Column = "Rolnicza", Color = Color.FromArgb(46, 204, 113) },
        new { Name = "Tuszka", Column = "Tuszka", Color = Color.FromArgb(241, 196, 15) },
        new { Name = "Wolnorynkowa", Column = "Wolnorynkowa", Color = Color.FromArgb(231, 76, 60) }
    };

            foreach (var config in seriesConfig)
            {
                if (!filteredData.Columns.Contains(config.Column)) continue;

                var series = new Series(config.Name)
                {
                    ChartType = SeriesChartType.Spline,  // Wygładzone linie
                    BorderWidth = 3,
                    Color = config.Color,
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerColor = config.Color,
                    MarkerBorderColor = Color.White,
                    MarkerBorderWidth = 2,
                    IsValueShownAsLabel = false,
                    ToolTip = "#SERIESNAME\nData: #VALX{dd.MM.yyyy}\nCena: #VALY{0.00} zł"
                };

                // Dodaj cień
                series["ShadowOffset"] = "2";

                var validRows = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r[config.Column] != DBNull.Value)
                    .OrderBy(r => r["DataRaw"]);

                foreach (var row in validRows)
                {
                    var value = Convert.ToDouble(row[config.Column]);
                    series.Points.AddXY(Convert.ToDateTime(row["DataRaw"]), Math.Round(value, 2));
                }

                if (series.Points.Count > 0)
                    priceChart.Series.Add(series);
            }

            // Dodaj animację
            foreach (var series in priceChart.Series)
            {
                series["DrawingStyle"] = "Cylinder";
            }
        }

        #endregion

        #region Statystyki

        private void UpdateStatistics()
        {
            var statsPanel = mainTabControl?.TabPages[2]?.Tag as FlowLayoutPanel;
            if (statsPanel == null || filteredData == null) return;

            statsPanel.Controls.Clear();

            var priceColumns = new[] { "Minister", "Rolnicza", "Nasza Cena", "Cena Zrzeszenia", "Wolnorynkowa" };

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

        private Panel CreateStatsCard(string title, List<decimal> values)
        {
            var card = new Panel
            {
                Size = new Size(300, 150),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(10)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = primaryColor,
                Location = new Point(10, 10),
                AutoSize = true
            };

            var avg = values.Average();
            var min = values.Min();
            var max = values.Max();

            var lblStats = new Label
            {
                Text = $"Średnia: {avg:N2} zł\n" +
                       $"Minimum: {min:N2} zł\n" +
                       $"Maximum: {max:N2} zł\n" +
                       $"Rozstęp: {(max - min):N2} zł",
                Location = new Point(10, 40),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblStats);

            return card;
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
                dataGridView1.Columns[e.ColumnIndex].Name == "Nasza-Zrzeszenie")
            {
                if (e.Value != null && decimal.TryParse(e.Value.ToString(), out decimal val))
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
                Size = new Size(500, 400),
                StartPosition = FormStartPosition.CenterParent,
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
            textBox.AppendText("💰 CENY PODSTAWOWE:\n");
            textBox.AppendText($"   • Ministerialna: {row.Cells["Minister"].Value:N2} zł\n");
            textBox.AppendText($"   • Rolnicza: {row.Cells["Rolnicza"].Value:N2} zł\n");
            textBox.AppendText($"   • Wolnorynkowa: {row.Cells["Wolnorynkowa"].Value:N2} zł\n\n");

            textBox.AppendText("🏭 NASZE CENY:\n");
            textBox.AppendText($"   • Nasza Cena: {row.Cells["Nasza Cena"].Value:N2} zł\n");
            textBox.AppendText($"   • Cena Zrzeszenia: {row.Cells["Cena Zrzeszenia"].Value:N2} zł\n\n");

            textBox.AppendText("📊 RÓŻNICE:\n");
            textBox.AppendText($"   • Rolnicza - Wolny: {row.Cells["Rolnicza-Wolny"].Value:N2} zł\n");
            textBox.AppendText($"   • Nasza - Zrzeszenie: {row.Cells["Nasza-Zrzeszenie"].Value:N2} zł\n");

            form.Controls.Add(textBox);
            form.ShowDialog();
        }

        #endregion

        #region Eksport i narzędzia

        private void ShowExportMenu(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("📄 Eksport do CSV", null, async (s, ev) => await ExportToCSVAsync());
            menu.Items.Add("📊 Eksport do Excel", null, (s, ev) => ExportToExcel());
            menu.Items.Add("📑 Eksport do JSON", null, async (s, ev) => await ExportToJSONAsync());

            var button = sender as Button;
            menu.Show(button, new Point(0, button.Height));
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
                WriteIndented = true
            });
        }

        private void ExportToExcel()
        {
            MessageBox.Show("Eksport do Excel wymaga dodatkowej biblioteki (np. EPPlus).\n" +
                          "Użyj eksportu CSV, który można otworzyć w Excelu.",
                          "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CopySelectedRows()
        {
            if (dataGridView1.SelectedRows.Count == 0) return;

            var data = new StringBuilder();

            foreach (DataGridViewRow row in dataGridView1.SelectedRows)
            {
                var values = row.Cells.Cast<DataGridViewCell>()
                    .Where(c => c.OwningColumn.Name != "DataRaw")
                    .Select(c => c.Value?.ToString() ?? "");
                data.AppendLine(string.Join("\t", values));
            }

            Clipboard.SetText(data.ToString());
        }

        private void ShowInChart()
        {
            mainTabControl.SelectedIndex = 1;
            UpdateChart();
        }

        private void ShowSettings(object sender, EventArgs e)
        {
            var form = new Form
            {
                Text = "Ustawienia",
                Size = new Size(400, 300),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White
            };

            var lblInfo = new Label
            {
                Text = "Ustawienia aplikacji",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            form.Controls.Add(lblInfo);
            form.ShowDialog();
        }

        private void UpdateStatus(string message)
        {
            var statusBar = this.Controls.OfType<StatusStrip>().FirstOrDefault();
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
            dataGridView1?.Dispose();
            priceChart?.Dispose();
            originalData?.Dispose();
            filteredData?.Dispose();

            base.OnFormClosed(e);
        }

        #endregion
    }
}