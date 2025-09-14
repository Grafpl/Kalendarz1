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
// Używamy aliasów dla rozwiązania konfliktów
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokCenWszystkich : Form
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DataTable originalData;
        private DataTable filteredData;
        private Chart priceChart;
        private TabControl mainTabControl;
        private Panel statsPanel;
        private Timer refreshTimer;
        private ToolStrip mainToolStrip;
        private StatusStrip mainStatusStrip;
        private ContextMenuStrip gridContextMenu;

        // Kontrolki filtrowania
        private TextBox txtSearchFilter;
        private DateTimePicker dateFromFilter;
        private DateTimePicker dateToFilter;
        private CheckBox chkShowWeekendsFilter;

        // Kontrolki wykresu
        private ComboBox cmbChartType;
        private CheckBox chkShowMinister;
        private CheckBox chkShowRolnicza;
        private CheckBox chkShowWolny;
        private CheckBox chkShowTuszka;

        // Kontrolki statusu
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel recordCountLabel;
        private ToolStripStatusLabel lastUpdateLabel;

        // Kolory motywu
        private readonly Drawing.Color primaryColor = Drawing.Color.FromArgb(41, 128, 185);
        private readonly Drawing.Color secondaryColor = Drawing.Color.FromArgb(52, 152, 219);
        private readonly Drawing.Color accentColor = Drawing.Color.FromArgb(46, 204, 113);
        private readonly Drawing.Color warningColor = Drawing.Color.FromArgb(241, 196, 15);
        private readonly Drawing.Color dangerColor = Drawing.Color.FromArgb(231, 76, 60);

        public WidokCenWszystkich()
        {
            InitializeComponent();
            SetupEnhancedUI();
            LoadData();
            SetupAutoRefresh();
        }

        private void SetupEnhancedUI()
        {
            this.Text = "System Analityki Cen - Zaawansowany Panel";
            this.Size = new Drawing.Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Drawing.Font("Segoe UI", 9F);
            this.BackColor = Drawing.Color.FromArgb(245, 245, 245);

            // Ukryj oryginalne kontrolki jeśli istnieją
            if (textBox1 != null) textBox1.Visible = false;
            if (label18 != null) label18.Visible = false;
            if (ExcelButton != null) ExcelButton.Visible = false;
            if (chkShowWeekend != null) chkShowWeekend.Visible = false;

            SetupToolStrip();
            SetupTabControl();
            SetupStatusStrip();

            // Reorganizuj layout
            this.Controls.Clear();
            this.Controls.Add(mainTabControl);
            this.Controls.Add(mainToolStrip);
            this.Controls.Add(mainStatusStrip);
        }

        private void SetupToolStrip()
        {
            mainToolStrip = new ToolStrip
            {
                Height = 40,
                BackColor = primaryColor,
                RenderMode = ToolStripRenderMode.Professional,
                GripStyle = ToolStripGripStyle.Hidden
            };

            var refreshButton = new ToolStripButton
            {
                Text = "🔄 Odśwież",
                ForeColor = Drawing.Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            refreshButton.Click += (s, e) => LoadData();

            var exportButton = new ToolStripSplitButton
            {
                Text = "📊 Eksportuj",
                ForeColor = Drawing.Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            exportButton.DropDownItems.Add("Excel", null, ExportToExcel);
            exportButton.DropDownItems.Add("CSV", null, ExportToCSV);
            exportButton.DropDownItems.Add("JSON", null, ExportToJSON);

            var printButton = new ToolStripButton
            {
                Text = "🖨️ Drukuj",
                ForeColor = Drawing.Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            printButton.Click += PrintReport;

            var settingsButton = new ToolStripButton
            {
                Text = "⚙️ Ustawienia",
                ForeColor = Drawing.Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            settingsButton.Click += ShowSettings;

            mainToolStrip.Items.AddRange(new ToolStripItem[] {
                refreshButton,
                new ToolStripSeparator(),
                exportButton,
                printButton,
                new ToolStripSeparator(),
                settingsButton
            });
        }

        private void SetupTabControl()
        {
            mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Drawing.Font("Segoe UI", 9F)
            };

            // Tab 1: Dane tabelaryczne
            var tabData = new TabPage("📋 Dane");
            tabData.BackColor = Drawing.Color.White;
            SetupDataTab(tabData);

            // Tab 2: Wykresy
            var tabCharts = new TabPage("📈 Wykresy");
            tabCharts.BackColor = Drawing.Color.White;
            SetupChartsTab(tabCharts);

            // Tab 3: Statystyki
            var tabStats = new TabPage("📊 Statystyki");
            tabStats.BackColor = Drawing.Color.White;
            SetupStatsTab(tabStats);

            // Tab 4: Analiza porównawcza
            var tabComparison = new TabPage("🔄 Porównanie");
            tabComparison.BackColor = Drawing.Color.White;
            SetupComparisonTab(tabComparison);

            // Tab 5: Prognozy
            var tabForecast = new TabPage("🔮 Prognozy");
            tabForecast.BackColor = Drawing.Color.White;
            SetupForecastTab(tabForecast);

            mainTabControl.TabPages.AddRange(new[] { tabData, tabCharts, tabStats, tabComparison, tabForecast });
        }

        private void SetupDataTab(TabPage tab)
        {
            // Panel filtów
            var filterPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Drawing.Color.White,
                Padding = new Padding(10)
            };

            var lblSearch = new Label
            {
                Text = "🔍 Wyszukiwanie:",
                Location = new Drawing.Point(10, 15),
                AutoSize = true
            };

            txtSearchFilter = new TextBox
            {
                Location = new Drawing.Point(120, 12),
                Width = 200,
                Font = new Drawing.Font("Segoe UI", 10F)
            };
            txtSearchFilter.TextChanged += FilterData;

            var lblDateFrom = new Label
            {
                Text = "Od:",
                Location = new Drawing.Point(340, 15),
                AutoSize = true
            };

            dateFromFilter = new DateTimePicker
            {
                Location = new Drawing.Point(370, 12),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(-1)
            };
            dateFromFilter.ValueChanged += FilterData;

            var lblDateTo = new Label
            {
                Text = "Do:",
                Location = new Drawing.Point(500, 15),
                AutoSize = true
            };

            dateToFilter = new DateTimePicker
            {
                Location = new Drawing.Point(530, 12),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };
            dateToFilter.ValueChanged += FilterData;

            chkShowWeekendsFilter = new CheckBox
            {
                Text = "Pokaż weekendy",
                Location = new Drawing.Point(670, 14),
                AutoSize = true,
                Checked = false
            };
            chkShowWeekendsFilter.CheckedChanged += FilterData;

            var btnAdvancedFilter = new Button
            {
                Text = "Filtry zaawansowane",
                Location = new Drawing.Point(800, 10),
                Width = 140,
                Height = 28,
                BackColor = secondaryColor,
                ForeColor = Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAdvancedFilter.FlatAppearance.BorderSize = 0;
            btnAdvancedFilter.Click += ShowAdvancedFilters;

            filterPanel.Controls.AddRange(new Forms.Control[] {
                lblSearch, txtSearchFilter, lblDateFrom, dateFromFilter,
                lblDateTo, dateToFilter, chkShowWeekendsFilter, btnAdvancedFilter
            });

            // Modyfikuj istniejący DataGridView
            if (dataGridView1 == null)
            {
                dataGridView1 = new DataGridView();
            }

            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.BackgroundColor = Drawing.Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.ColumnHeadersHeight = 40;
            dataGridView1.RowTemplate.Height = 30;

            dataGridView1.DefaultCellStyle = new DataGridViewCellStyle
            {
                SelectionBackColor = secondaryColor,
                SelectionForeColor = Drawing.Color.White
            };

            dataGridView1.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Drawing.Color.FromArgb(248, 248, 248)
            };

            dataGridView1.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = primaryColor,
                ForeColor = Drawing.Color.White,
                Font = new Drawing.Font("Segoe UI", 9F, Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            // Context menu dla DataGridView
            SetupGridContextMenu();

            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;

            tab.Controls.Add(dataGridView1);
            tab.Controls.Add(filterPanel);
        }

        private void SetupGridContextMenu()
        {
            gridContextMenu = new ContextMenuStrip();
            gridContextMenu.Items.Add("Kopiuj", null, (s, e) => CopySelectedRows());
            gridContextMenu.Items.Add("Kopiuj z nagłówkami", null, (s, e) => CopySelectedRowsWithHeaders());
            gridContextMenu.Items.Add("-");
            gridContextMenu.Items.Add("Eksportuj zaznaczone", null, (s, e) => ExportSelectedRows());
            gridContextMenu.Items.Add("Pokaż wykres dla zaznaczonych", null, (s, e) => ShowChartForSelected());

            dataGridView1.ContextMenuStrip = gridContextMenu;
        }

        private void SetupChartsTab(TabPage tab)
        {
            var chartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            priceChart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Drawing.Color.White
            };

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Drawing.Color.White,
                BackSecondaryColor = Drawing.Color.FromArgb(250, 250, 250),
                BackGradientStyle = GradientStyle.TopBottom
            };

            chartArea.AxisX.LabelStyle.Format = "dd.MM";
            chartArea.AxisX.MajorGrid.LineColor = Drawing.Color.LightGray;
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.Title = "Data";

            chartArea.AxisY.MajorGrid.LineColor = Drawing.Color.LightGray;
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.Title = "Cena (zł)";
            chartArea.AxisY.LabelStyle.Format = "N2";

            priceChart.ChartAreas.Add(chartArea);

            var legend = new Legend
            {
                Docking = Docking.Top,
                BackColor = Drawing.Color.Transparent,
                Font = new Drawing.Font("Segoe UI", 9F)
            };
            priceChart.Legends.Add(legend);

            // Panel kontroli wykresu
            var chartControlPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Drawing.Color.FromArgb(250, 250, 250),
                Padding = new Padding(10)
            };

            cmbChartType = new ComboBox
            {
                Location = new Drawing.Point(10, 12),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbChartType.Items.AddRange(new[] { "Liniowy", "Słupkowy", "Obszarowy", "Punktowy" });
            cmbChartType.SelectedIndex = 0;
            cmbChartType.SelectedIndexChanged += (s, e) => UpdateChartType();

            chkShowMinister = new CheckBox
            {
                Text = "Minister",
                Location = new Drawing.Point(180, 14),
                Checked = true,
                AutoSize = true
            };
            chkShowRolnicza = new CheckBox
            {
                Text = "Rolnicza",
                Location = new Drawing.Point(260, 14),
                Checked = true,
                AutoSize = true
            };
            chkShowWolny = new CheckBox
            {
                Text = "Wolny",
                Location = new Drawing.Point(340, 14),
                Checked = true,
                AutoSize = true
            };
            chkShowTuszka = new CheckBox
            {
                Text = "Tuszka",
                Location = new Drawing.Point(410, 14),
                Checked = true,
                AutoSize = true
            };

            chkShowMinister.CheckedChanged += (s, e) => UpdateChart();
            chkShowRolnicza.CheckedChanged += (s, e) => UpdateChart();
            chkShowWolny.CheckedChanged += (s, e) => UpdateChart();
            chkShowTuszka.CheckedChanged += (s, e) => UpdateChart();

            chartControlPanel.Controls.AddRange(new Forms.Control[] {
                cmbChartType, chkShowMinister, chkShowRolnicza, chkShowWolny, chkShowTuszka
            });

            chartPanel.Controls.Add(priceChart);
            tab.Controls.Add(chartPanel);
            tab.Controls.Add(chartControlPanel);
        }

        private void SetupStatsTab(TabPage tab)
        {
            statsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20),
                BackColor = Drawing.Color.White
            };

            tab.Controls.Add(statsPanel);
        }

        private void SetupComparisonTab(TabPage tab)
        {
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 100
            };

            // Górny panel - wybór okresów
            var periodPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Drawing.Color.White,
                Padding = new Padding(10)
            };

            var lblPeriod1 = new Label
            {
                Text = "Okres 1:",
                Location = new Drawing.Point(10, 15),
                AutoSize = true
            };

            var datePeriod1Start = new DateTimePicker
            {
                Location = new Drawing.Point(70, 12),
                Width = 100,
                Format = DateTimePickerFormat.Short
            };

            var datePeriod1End = new DateTimePicker
            {
                Location = new Drawing.Point(180, 12),
                Width = 100,
                Format = DateTimePickerFormat.Short
            };

            var lblPeriod2 = new Label
            {
                Text = "Okres 2:",
                Location = new Drawing.Point(300, 15),
                AutoSize = true
            };

            var datePeriod2Start = new DateTimePicker
            {
                Location = new Drawing.Point(360, 12),
                Width = 100,
                Format = DateTimePickerFormat.Short
            };

            var datePeriod2End = new DateTimePicker
            {
                Location = new Drawing.Point(470, 12),
                Width = 100,
                Format = DateTimePickerFormat.Short
            };

            var btnCompare = new Button
            {
                Text = "Porównaj okresy",
                Location = new Drawing.Point(590, 10),
                Width = 120,
                Height = 28,
                BackColor = accentColor,
                ForeColor = Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCompare.FlatAppearance.BorderSize = 0;

            periodPanel.Controls.AddRange(new Forms.Control[] {
                lblPeriod1, datePeriod1Start, datePeriod1End,
                lblPeriod2, datePeriod2Start, datePeriod2End,
                btnCompare
            });

            // Dolny panel - wyniki porównania
            var resultsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Drawing.Color.White,
                Padding = new Padding(20)
            };

            btnCompare.Click += (s, e) => PerformComparison(resultsPanel,
                datePeriod1Start.Value, datePeriod1End.Value,
                datePeriod2Start.Value, datePeriod2End.Value);

            splitContainer.Panel1.Controls.Add(periodPanel);
            splitContainer.Panel2.Controls.Add(resultsPanel);

            tab.Controls.Add(splitContainer);
        }

        private void SetupForecastTab(TabPage tab)
        {
            var forecastPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Drawing.Color.White
            };

            var lblTitle = new Label
            {
                Text = "🔮 Prognoza cen na kolejne dni",
                Font = new Drawing.Font("Segoe UI", 14F, Drawing.FontStyle.Bold),
                Location = new Drawing.Point(10, 10),
                AutoSize = true
            };

            var cmbForecastDays = new ComboBox
            {
                Location = new Drawing.Point(10, 50),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbForecastDays.Items.AddRange(new[] { "7 dni", "14 dni", "30 dni", "90 dni" });
            cmbForecastDays.SelectedIndex = 0;

            var btnGenerateForecast = new Button
            {
                Text = "Generuj prognozę",
                Location = new Drawing.Point(170, 48),
                Width = 150,
                Height = 28,
                BackColor = secondaryColor,
                ForeColor = Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnGenerateForecast.FlatAppearance.BorderSize = 0;

            var forecastChart = new Chart
            {
                Location = new Drawing.Point(10, 100),
                Size = new Drawing.Size(tab.Width - 60, tab.Height - 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Drawing.Color.White
            };

            var chartArea = new ChartArea("ForecastArea")
            {
                BackColor = Drawing.Color.White
            };
            chartArea.AxisX.Title = "Data";
            chartArea.AxisX.LabelStyle.Format = "dd.MM";
            chartArea.AxisY.Title = "Cena (zł)";
            chartArea.AxisY.LabelStyle.Format = "N2";

            forecastChart.ChartAreas.Add(chartArea);

            btnGenerateForecast.Click += (s, e) => GenerateForecast(forecastChart, cmbForecastDays.SelectedItem.ToString());

            forecastPanel.Controls.AddRange(new Forms.Control[] { lblTitle, cmbForecastDays, btnGenerateForecast, forecastChart });
            tab.Controls.Add(forecastPanel);
        }

        private void SetupStatusStrip()
        {
            mainStatusStrip = new StatusStrip
            {
                BackColor = Drawing.Color.FromArgb(240, 240, 240)
            };

            statusLabel = new ToolStripStatusLabel("Gotowy");
            recordCountLabel = new ToolStripStatusLabel { Spring = true, TextAlign = Drawing.ContentAlignment.MiddleRight };
            lastUpdateLabel = new ToolStripStatusLabel { Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}" };

            mainStatusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, recordCountLabel, lastUpdateLabel });
        }

        private void LoadData()
        {
            try
            {
                UpdateStatus("Ładowanie danych...");

                string query = GetMainQuery();

                using (var connection = new SqlConnection(connectionString))
                {
                    var adapter = new SqlDataAdapter(query, connection);
                    originalData = new DataTable();
                    adapter.Fill(originalData);

                    filteredData = originalData.Copy();
                    dataGridView1.DataSource = filteredData;

                    FormatDataGrid();
                    UpdateChart();
                    UpdateStatistics();
                    UpdateStatus($"Załadowano {originalData.Rows.Count} rekordów");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Błąd ładowania danych");
            }
        }

        private string GetMainQuery()
        {
            return @"
            WITH CTE_Ministerialna AS (
                SELECT [Data], CAST([Cena] AS DECIMAL(10, 2)) AS CenaMinisterialna
                FROM [LibraNet].[dbo].[CenaMinisterialna]
                WHERE [Data] >= DATEADD(MONTH, -6, GETDATE())
            ),
            CTE_Rolnicza AS (
                SELECT [Data], CAST([Cena] AS DECIMAL(10, 2)) AS CenaRolnicza
                FROM [LibraNet].[dbo].[CenaRolnicza]
                WHERE [Data] >= DATEADD(MONTH, -6, GETDATE())
            ),
            CTE_Tuszka AS (
                SELECT [Data], CAST([Cena] AS DECIMAL(10, 2)) AS CenaTuszki
                FROM [LibraNet].[dbo].[CenaTuszki]
                WHERE [Data] >= DATEADD(MONTH, -6, GETDATE())
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
                    AND DP.[data] >= DATEADD(MONTH, -6, GETDATE())
                GROUP BY CONVERT(DATE, DP.[data])
            ),
            CTE_Harmonogram AS (
                SELECT 
                    DataOdbioru AS Data,
                    SUM(CAST(Auta AS DECIMAL(10, 2)) * CAST(Cena AS DECIMAL(10, 2))) / 
                        NULLIF(SUM(CAST(Auta AS DECIMAL(10, 2))), 0) AS SredniaCena
                FROM [LibraNet].[dbo].[HarmonogramDostaw]
                WHERE (TypCeny = 'Wolnorynkowa' OR TypCeny = 'wolnorynkowa')
                    AND Bufor = 'Potwierdzony'
                GROUP BY DataOdbioru
            )
            SELECT 
                COALESCE(M.Data, R.Data, H.Data, T.Data) AS DataRaw,
                FORMAT(COALESCE(M.Data, R.Data, H.Data), 'yyyy-MM-dd') + ' ' + 
                    LEFT(DATENAME(WEEKDAY, COALESCE(M.Data, R.Data, H.Data)), 3) AS Data,
                M.CenaMinisterialna AS Minister,
                (ISNULL(M.CenaMinisterialna, 0) + ISNULL(R.CenaRolnicza, 0)) / 2.0 AS Laczona,
                R.CenaRolnicza AS Rolnicza,
                HD.SredniaCena AS Wolny,
                R.CenaRolnicza - HD.SredniaCena AS RolniczaMinusWolny,
                H.Cena AS Tuszka,
                T.CenaTuszki AS Zrzeszenie,
                H.Cena - T.CenaTuszki AS Roznica
            FROM CTE_Ministerialna M
            FULL OUTER JOIN CTE_Rolnicza R ON M.Data = R.Data
            FULL OUTER JOIN CTE_Tuszka T ON M.Data = T.Data
            FULL OUTER JOIN CTE_HANDEL H ON COALESCE(M.Data, R.Data) = H.Data
            LEFT JOIN CTE_Harmonogram HD ON COALESCE(M.Data, R.Data, H.Data) = HD.Data
            WHERE COALESCE(M.Data, R.Data, H.Data) >= DATEADD(MONTH, -6, GETDATE())
            ORDER BY DataRaw DESC";
        }

        private void FormatDataGrid()
        {
            if (dataGridView1.Columns.Count == 0) return;

            // Ukryj kolumnę DataRaw
            if (dataGridView1.Columns.Contains("DataRaw"))
                dataGridView1.Columns["DataRaw"].Visible = false;

            // Formatowanie kolumn
            var columnFormats = new Dictionary<string, DataGridViewCellStyle>
            {
                ["Minister"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["Laczona"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["Rolnicza"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["Wolny"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["RolniczaMinusWolny"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["Tuszka"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["Zrzeszenie"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
                ["Roznica"] = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            foreach (var kvp in columnFormats)
            {
                if (dataGridView1.Columns.Contains(kvp.Key))
                {
                    dataGridView1.Columns[kvp.Key].DefaultCellStyle = kvp.Value;
                }
            }

            // Automatyczne dopasowanie szerokości kolumn
            dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridView1.Rows[e.RowIndex];

            // Podświetlenie dzisiejszej daty
            if (row.Cells["Data"].Value != null)
            {
                string dateStr = row.Cells["Data"].Value.ToString();
                if (dateStr.StartsWith(DateTime.Today.ToString("yyyy-MM-dd")))
                {
                    row.DefaultCellStyle.BackColor = Drawing.Color.FromArgb(255, 243, 224);
                    row.DefaultCellStyle.Font = new Drawing.Font(dataGridView1.Font, Drawing.FontStyle.Bold);
                }
            }

            // Kolorowanie różnic
            if (e.ColumnIndex >= 0 && dataGridView1.Columns[e.ColumnIndex].Name == "RolniczaMinusWolny")
            {
                if (e.Value != null && decimal.TryParse(e.Value.ToString(), out decimal val))
                {
                    e.CellStyle.ForeColor = val >= 0 ? Drawing.Color.Green : Drawing.Color.Red;
                }
            }

            if (e.ColumnIndex >= 0 && dataGridView1.Columns[e.ColumnIndex].Name == "Roznica")
            {
                if (e.Value != null && decimal.TryParse(e.Value.ToString(), out decimal val))
                {
                    e.CellStyle.ForeColor = val >= 0 ? Drawing.Color.Green : Drawing.Color.Red;
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
            var detailForm = new Form
            {
                Text = $"Szczegóły - {row.Cells["Data"].Value}",
                Size = new Drawing.Size(500, 400),
                StartPosition = FormStartPosition.CenterParent
            };

            var textBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Drawing.Font("Segoe UI", 10F)
            };

            textBox.AppendText($"Data: {row.Cells["Data"].Value}\n\n");
            textBox.AppendText("=== CENY ===\n");
            textBox.AppendText($"Ministerialna: {row.Cells["Minister"].Value:N2} zł\n");
            textBox.AppendText($"Łączona: {row.Cells["Laczona"].Value:N2} zł\n");
            textBox.AppendText($"Rolnicza: {row.Cells["Rolnicza"].Value:N2} zł\n");
            textBox.AppendText($"Wolnorynkowa: {row.Cells["Wolny"].Value:N2} zł\n");
            textBox.AppendText($"Tuszka: {row.Cells["Tuszka"].Value:N2} zł\n");
            textBox.AppendText($"Zrzeszenie: {row.Cells["Zrzeszenie"].Value:N2} zł\n\n");
            textBox.AppendText("=== RÓŻNICE ===\n");
            textBox.AppendText($"Rolnicza - Wolny: {row.Cells["RolniczaMinusWolny"].Value:N2} zł\n");
            textBox.AppendText($"Tuszka - Zrzeszenie: {row.Cells["Roznica"].Value:N2} zł\n");

            detailForm.Controls.Add(textBox);
            detailForm.ShowDialog();
        }

        private void UpdateChart()
        {
            if (priceChart == null || filteredData == null) return;

            priceChart.Series.Clear();

            // Seria dla ceny ministerialnej
            if (chkShowMinister != null && chkShowMinister.Checked)
            {
                var seriesMinister = new Series("Ministerialna")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Drawing.Color.FromArgb(41, 128, 185),
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 5
                };

                foreach (DataRow row in filteredData.Rows)
                {
                    if (row["DataRaw"] != DBNull.Value && row["Minister"] != DBNull.Value)
                    {
                        seriesMinister.Points.AddXY(
                            Convert.ToDateTime(row["DataRaw"]),
                            Convert.ToDouble(row["Minister"])
                        );
                    }
                }
                priceChart.Series.Add(seriesMinister);
            }

            // Seria dla ceny rolniczej
            if (chkShowRolnicza != null && chkShowRolnicza.Checked)
            {
                var seriesRolnicza = new Series("Rolnicza")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Drawing.Color.FromArgb(46, 204, 113),
                    MarkerStyle = MarkerStyle.Square,
                    MarkerSize = 5
                };

                foreach (DataRow row in filteredData.Rows)
                {
                    if (row["DataRaw"] != DBNull.Value && row["Rolnicza"] != DBNull.Value)
                    {
                        seriesRolnicza.Points.AddXY(
                            Convert.ToDateTime(row["DataRaw"]),
                            Convert.ToDouble(row["Rolnicza"])
                        );
                    }
                }
                priceChart.Series.Add(seriesRolnicza);
            }

            // Seria dla ceny wolnorynkowej
            if (chkShowWolny != null && chkShowWolny.Checked)
            {
                var seriesWolny = new Series("Wolnorynkowa")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Drawing.Color.FromArgb(241, 196, 15),
                    MarkerStyle = MarkerStyle.Diamond,
                    MarkerSize = 5
                };

                foreach (DataRow row in filteredData.Rows)
                {
                    if (row["DataRaw"] != DBNull.Value && row["Wolny"] != DBNull.Value)
                    {
                        seriesWolny.Points.AddXY(
                            Convert.ToDateTime(row["DataRaw"]),
                            Convert.ToDouble(row["Wolny"])
                        );
                    }
                }
                priceChart.Series.Add(seriesWolny);
            }

            // Seria dla ceny tuszki
            if (chkShowTuszka != null && chkShowTuszka.Checked)
            {
                var seriesTuszka = new Series("Tuszka")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Drawing.Color.FromArgb(231, 76, 60),
                    MarkerStyle = MarkerStyle.Triangle,
                    MarkerSize = 5
                };

                foreach (DataRow row in filteredData.Rows)
                {
                    if (row["DataRaw"] != DBNull.Value && row["Tuszka"] != DBNull.Value)
                    {
                        seriesTuszka.Points.AddXY(
                            Convert.ToDateTime(row["DataRaw"]),
                            Convert.ToDouble(row["Tuszka"])
                        );
                    }
                }
                priceChart.Series.Add(seriesTuszka);
            }
        }

        private void UpdateChartType()
        {
            if (cmbChartType == null || priceChart == null) return;

            var selectedType = cmbChartType.SelectedItem?.ToString();

            foreach (var series in priceChart.Series)
            {
                switch (selectedType)
                {
                    case "Słupkowy":
                        series.ChartType = SeriesChartType.Column;
                        break;
                    case "Obszarowy":
                        series.ChartType = SeriesChartType.Area;
                        break;
                    case "Punktowy":
                        series.ChartType = SeriesChartType.Point;
                        break;
                    default:
                        series.ChartType = SeriesChartType.Line;
                        break;
                }
            }
        }

        private void UpdateStatistics()
        {
            if (statsPanel == null || filteredData == null) return;

            statsPanel.Controls.Clear();

            int yPos = 10;

            // Nagłówek
            var lblTitle = new Label
            {
                Text = "📊 Statystyki cenowe",
                Font = new Drawing.Font("Segoe UI", 14F, Drawing.FontStyle.Bold),
                Location = new Drawing.Point(10, yPos),
                AutoSize = true
            };
            statsPanel.Controls.Add(lblTitle);
            yPos += 40;

            // Statystyki dla każdego typu ceny
            var priceTypes = new[] { "Minister", "Rolnicza", "Wolny", "Tuszka", "Zrzeszenie" };

            foreach (var priceType in priceTypes)
            {
                if (!filteredData.Columns.Contains(priceType)) continue;

                var values = filteredData.AsEnumerable()
                    .Where(r => r[priceType] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r[priceType]))
                    .ToList();

                if (values.Count == 0) continue;

                var statsGroup = new GroupBox
                {
                    Text = priceType,
                    Location = new Drawing.Point(10, yPos),
                    Size = new Drawing.Size(350, 150),
                    Font = new Drawing.Font("Segoe UI", 10F, Drawing.FontStyle.Bold)
                };

                var avg = values.Average();
                var min = values.Min();
                var max = values.Max();
                var stdDev = CalculateStdDev(values);
                var trend = CalculateTrend(priceType);

                var lblAvg = new Label
                {
                    Text = $"Średnia: {avg:N2} zł",
                    Location = new Drawing.Point(10, 25),
                    AutoSize = true,
                    Font = new Drawing.Font("Segoe UI", 9F)
                };

                var lblMin = new Label
                {
                    Text = $"Minimum: {min:N2} zł",
                    Location = new Drawing.Point(10, 50),
                    AutoSize = true,
                    Font = new Drawing.Font("Segoe UI", 9F)
                };

                var lblMax = new Label
                {
                    Text = $"Maximum: {max:N2} zł",
                    Location = new Drawing.Point(10, 75),
                    AutoSize = true,
                    Font = new Drawing.Font("Segoe UI", 9F)
                };

                var lblStdDev = new Label
                {
                    Text = $"Odch. std.: {stdDev:N2} zł",
                    Location = new Drawing.Point(10, 100),
                    AutoSize = true,
                    Font = new Drawing.Font("Segoe UI", 9F)
                };

                var lblTrend = new Label
                {
                    Text = $"Trend: {(trend > 0 ? "↑" : trend < 0 ? "↓" : "→")} {Math.Abs(trend):P1}",
                    Location = new Drawing.Point(10, 125),
                    AutoSize = true,
                    Font = new Drawing.Font("Segoe UI", 9F),
                    ForeColor = trend > 0 ? Drawing.Color.Green : trend < 0 ? Drawing.Color.Red : Drawing.Color.Gray
                };

                statsGroup.Controls.AddRange(new Forms.Control[] { lblAvg, lblMin, lblMax, lblStdDev, lblTrend });
                statsPanel.Controls.Add(statsGroup);

                yPos += 160;
            }
        }

        private decimal CalculateStdDev(List<decimal> values)
        {
            if (values.Count <= 1) return 0;

            var avg = values.Average();
            var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumOfSquares / (values.Count - 1)));
        }

        private decimal CalculateTrend(string priceType)
        {
            var values = filteredData.AsEnumerable()
                .Where(r => r[priceType] != DBNull.Value && r["DataRaw"] != DBNull.Value)
                .OrderBy(r => r["DataRaw"])
                .Select(r => Convert.ToDecimal(r[priceType]))
                .ToList();

            if (values.Count < 2) return 0;

            var firstWeekAvg = values.Take(Math.Min(7, values.Count / 4)).Average();
            var lastWeekAvg = values.Skip(Math.Max(0, values.Count - 7)).Average();

            if (firstWeekAvg == 0) return 0;

            return (lastWeekAvg - firstWeekAvg) / firstWeekAvg;
        }

        private void PerformComparison(Panel resultsPanel, DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            resultsPanel.Controls.Clear();

            var lblResults = new Label
            {
                Text = $"Porównanie okresów:\n{start1:dd.MM.yyyy} - {end1:dd.MM.yyyy}\nvs\n{start2:dd.MM.yyyy} - {end2:dd.MM.yyyy}",
                Location = new Drawing.Point(10, 10),
                AutoSize = true,
                Font = new Drawing.Font("Segoe UI", 12F, Drawing.FontStyle.Bold)
            };

            resultsPanel.Controls.Add(lblResults);

            // Tu możesz dodać więcej logiki porównywania
        }

        private void GenerateForecast(Chart chart, string period)
        {
            chart.Series.Clear();

            var seriesHistorical = new Series("Dane historyczne")
            {
                ChartType = SeriesChartType.Line,
                Color = Drawing.Color.Blue,
                BorderWidth = 2
            };

            var seriesForecast = new Series("Prognoza")
            {
                ChartType = SeriesChartType.Line,
                Color = Drawing.Color.Red,
                BorderWidth = 2,
                BorderDashStyle = ChartDashStyle.Dash
            };

            // Przykładowe dane
            var random = new Random();
            var basePrice = 7.5;

            for (int i = -30; i <= 0; i++)
            {
                seriesHistorical.Points.AddXY(DateTime.Today.AddDays(i),
                    basePrice + random.NextDouble() * 0.5 - 0.25);
            }

            int days = int.Parse(period.Split(' ')[0]);
            for (int i = 1; i <= days; i++)
            {
                seriesForecast.Points.AddXY(DateTime.Today.AddDays(i),
                    basePrice + random.NextDouble() * 0.5 - 0.25);
            }

            chart.Series.Add(seriesHistorical);
            chart.Series.Add(seriesForecast);
            chart.Legends.Clear();
            chart.Legends.Add(new Legend { Docking = Docking.Top });
        }

        private void FilterData(object sender, EventArgs e)
        {
            if (originalData == null) return;

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
                    if (!showWeekends && (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday))
                        return false;
                }

                return true;
            });

            filteredData = filtered.Any() ? filtered.CopyToDataTable() : originalData.Clone();
            dataGridView1.DataSource = filteredData;

            UpdateChart();
            UpdateStatistics();
            UpdateStatus($"Wyświetlono {filteredData.Rows.Count} z {originalData.Rows.Count} rekordów");
        }

        private void ShowAdvancedFilters(object sender, EventArgs e)
        {
            var filterForm = new Form
            {
                Text = "Filtry zaawansowane",
                Size = new Drawing.Size(400, 500),
                StartPosition = FormStartPosition.CenterParent
            };

            // Tu możesz dodać więcej zaawansowanych filtrów

            filterForm.ShowDialog();
        }

        private void SetupAutoRefresh()
        {
            refreshTimer = new Timer { Interval = 300000 }; // 5 minut
            refreshTimer.Tick += (s, e) => LoadData();
            refreshTimer.Start();
        }

        private void ExportToExcel(object sender, EventArgs e)
        {
            MessageBox.Show("Funkcja eksportu do Excel wymaga instalacji pakietu EPPlus lub innej biblioteki Excel.",
                "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportToCSV(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV Files|*.csv";
                saveDialog.FileName = $"CenyAnaliza_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var csv = new StringBuilder();

                        // Nagłówki
                        var headers = filteredData.Columns.Cast<DataColumn>()
                            .Select(column => column.ColumnName);
                        csv.AppendLine(string.Join(",", headers));

                        // Dane
                        foreach (DataRow row in filteredData.Rows)
                        {
                            var fields = row.ItemArray.Select(field => field?.ToString() ?? "");
                            csv.AppendLine(string.Join(",", fields));
                        }

                        File.WriteAllText(saveDialog.FileName, csv.ToString());
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

        private void ExportToJSON(object sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON Files|*.json";
                saveDialog.FileName = $"CenyAnaliza_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = DataTableToJSON(filteredData);
                        File.WriteAllText(saveDialog.FileName, json);
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
            var JSONString = new StringBuilder();
            JSONString.Append("[");
            for (int i = 0; i < table.Rows.Count; i++)
            {
                JSONString.Append("{");
                for (int j = 0; j < table.Columns.Count; j++)
                {
                    JSONString.Append($"\"{table.Columns[j].ColumnName}\":\"{table.Rows[i][j]}\"");
                    if (j < table.Columns.Count - 1)
                        JSONString.Append(",");
                }
                JSONString.Append("}");
                if (i < table.Rows.Count - 1)
                    JSONString.Append(",");
            }
            JSONString.Append("]");
            return JSONString.ToString();
        }

        private void PrintReport(object sender, EventArgs e)
        {
            var printDialog = new PrintDialog();
            var printDocument = new System.Drawing.Printing.PrintDocument();

            printDocument.PrintPage += (s, ev) =>
            {
                // Podstawowa logika drukowania
                var font = new Drawing.Font("Arial", 10);
                var yPos = 100;

                ev.Graphics.DrawString("Raport Analizy Cen", new Drawing.Font("Arial", 16, Drawing.FontStyle.Bold),
                    Drawing.Brushes.Black, 100, 50);

                foreach (DataRow row in filteredData.Rows)
                {
                    if (yPos > ev.MarginBounds.Bottom)
                    {
                        ev.HasMorePages = true;
                        return;
                    }

                    ev.Graphics.DrawString(row["Data"].ToString(), font, Drawing.Brushes.Black, 100, yPos);
                    yPos += 20;
                }
            };

            printDialog.Document = printDocument;

            if (printDialog.ShowDialog() == DialogResult.OK)
            {
                printDocument.Print();
            }
        }

        private void ShowSettings(object sender, EventArgs e)
        {
            var settingsForm = new Form
            {
                Text = "Ustawienia",
                Size = new Drawing.Size(500, 400),
                StartPosition = FormStartPosition.CenterParent
            };

            var lblRefresh = new Label
            {
                Text = "Interwał odświeżania (minuty):",
                Location = new Drawing.Point(20, 20),
                AutoSize = true
            };

            var numRefresh = new NumericUpDown
            {
                Location = new Drawing.Point(20, 45),
                Minimum = 1,
                Maximum = 60,
                Value = refreshTimer.Interval / 60000
            };

            var btnSave = new Button
            {
                Text = "Zapisz",
                Location = new Drawing.Point(20, 80),
                Width = 100
            };

            btnSave.Click += (s, ev) =>
            {
                refreshTimer.Interval = (int)numRefresh.Value * 60000;
                MessageBox.Show("Ustawienia zapisane!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                settingsForm.Close();
            };

            settingsForm.Controls.AddRange(new Forms.Control[] { lblRefresh, numRefresh, btnSave });
            settingsForm.ShowDialog();
        }

        private void CopySelectedRows()
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                var data = new StringBuilder();
                foreach (DataGridViewRow row in dataGridView1.SelectedRows)
                {
                    var values = row.Cells.Cast<DataGridViewCell>()
                        .Select(cell => cell.Value?.ToString() ?? "");
                    data.AppendLine(string.Join("\t", values));
                }
                Clipboard.SetText(data.ToString());
            }
        }

        private void CopySelectedRowsWithHeaders()
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                var data = new StringBuilder();

                // Nagłówki
                var headers = dataGridView1.Columns.Cast<DataGridViewColumn>()
                    .Select(column => column.HeaderText);
                data.AppendLine(string.Join("\t", headers));

                // Dane
                foreach (DataGridViewRow row in dataGridView1.SelectedRows)
                {
                    var values = row.Cells.Cast<DataGridViewCell>()
                        .Select(cell => cell.Value?.ToString() ?? "");
                    data.AppendLine(string.Join("\t", values));
                }

                Clipboard.SetText(data.ToString());
            }
        }

        private void ExportSelectedRows()
        {
            ExportToCSV(null, null);
        }

        private void ShowChartForSelected()
        {
            UpdateChart();
            mainTabControl.SelectedIndex = 1; // Przejdź do zakładki wykresów
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null) statusLabel.Text = message;
            if (lastUpdateLabel != null) lastUpdateLabel.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";

            if (recordCountLabel != null && filteredData != null)
            {
                recordCountLabel.Text = $"Rekordów: {filteredData.Rows.Count}";
            }
        }


    }
}