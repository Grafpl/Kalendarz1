using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Kalendarz1
{
    public partial class WidokWszystkichDostaw : Form
    {
        private static readonly string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DataTable originalData;
        private Timer refreshTimer;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private SplitContainer mainSplitContainer;
        private TabControl tabControl;
        private Panel filterPanel;
        private Panel summaryPanel;
        private Chart performanceChart;
        private DataGridView dataGridViewSummary;
        private bool isLoading = false;

        // Filtry dodatkowe (checkBoxGroupBySupplier już istnieje w Designer.cs)
        private DateTimePicker dateFromPicker;
        private DateTimePicker dateToPicker;
        private ComboBox comboBoxDostawca;
        private ComboBox comboBoxStatus;
        private CheckBox checkBoxShowStatistics;

        public string TextBoxValue { get; set; }

        public WidokWszystkichDostaw()
        {
            InitializeComponent();
            SetupBasicUI();
            InitializeCustomComponents();
            LoadDataAsync();
        }

        private void SetupBasicUI()
        {
            // Optymalizacja wydajności DataGridView
            dataGridView1.DoubleBuffered(true);
            dataGridView1.SuspendLayout();

            // Podstawowe ustawienia dla szybszego renderowania
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridView1.StandardTab = true;

            dataGridView1.ResumeLayout();
        }

        private void InitializeCustomComponents()
        {
            // Główny layout
            this.Text = "System Zarządzania Dostawami Drobiu - Widok Analityczny";
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(245, 245, 245);

            // ToolStrip z nowoczesnym wyglądem
            toolStrip = new ToolStrip();
            toolStrip.BackColor = Color.White;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Padding = new Padding(5);
            toolStrip.Font = new Font("Segoe UI", 9F);

            var btnRefresh = CreateToolButton("🔄 Odśwież", (s, e) => LoadDataAsync());
            var btnExport = CreateToolButton("📊 Eksport", (s, e) => ExportToExcel());
            var btnPrint = CreateToolButton("🖨️ Drukuj", (s, e) => PrintData());
            var btnStats = CreateToolButton("📈 Statystyki", (s, e) => ShowStatistics());

            toolStrip.Items.AddRange(new ToolStripItem[] {
                btnRefresh,
                new ToolStripSeparator(),
                btnExport,
                btnPrint,
                new ToolStripSeparator(),
                btnStats
            });

            this.Controls.Add(toolStrip);

            // StatusStrip
            statusStrip = new StatusStrip();
            statusStrip.BackColor = Color.White;
            statusStrip.Items.Add(new ToolStripStatusLabel("Gotowy"));
            statusStrip.Items.Add(new ToolStripStatusLabel() { Spring = true });
            statusStrip.Items.Add(new ToolStripStatusLabel("Ostatnia aktualizacja: -"));
            this.Controls.Add(statusStrip);

            // Panel filtrów
            CreateFilterPanel();

            // SplitContainer dla głównego widoku
            mainSplitContainer = new SplitContainer();
            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.Orientation = Orientation.Horizontal;
            mainSplitContainer.SplitterDistance = 500;
            mainSplitContainer.BackColor = Color.FromArgb(245, 245, 245);
            mainSplitContainer.Panel1.Padding = new Padding(5);
            mainSplitContainer.Panel2.Padding = new Padding(5);

            // TabControl dla różnych widoków
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Segoe UI", 9F);

            // Tab: Wszystkie dostawy
            TabPage tabAllDeliveries = new TabPage("📋 Wszystkie Dostawy");
            tabAllDeliveries.BackColor = Color.White;
            tabAllDeliveries.Controls.Add(dataGridView1);
            tabControl.TabPages.Add(tabAllDeliveries);

            // Tab: Analiza dostawców
            TabPage tabSupplierAnalysis = new TabPage("👥 Analiza Dostawców");
            tabSupplierAnalysis.BackColor = Color.White;
            CreateSupplierAnalysisTab(tabSupplierAnalysis);
            tabControl.TabPages.Add(tabSupplierAnalysis);

            // Tab: Statystyki
            TabPage tabStatistics = new TabPage("📊 Statystyki i KPI");
            tabStatistics.BackColor = Color.White;
            CreateStatisticsTab(tabStatistics);
            tabControl.TabPages.Add(tabStatistics);

            mainSplitContainer.Panel1.Controls.Add(tabControl);

            // Panel podsumowania
            CreateSummaryPanel();
            mainSplitContainer.Panel2.Controls.Add(summaryPanel);

            this.Controls.Add(mainSplitContainer);
            mainSplitContainer.BringToFront();
        }

        private ToolStripButton CreateToolButton(string text, EventHandler handler)
        {
            var button = new ToolStripButton(text);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.Padding = new Padding(10, 2, 10, 2);
            button.Click += handler;
            return button;
        }

        private void CreateFilterPanel()
        {
            filterPanel = new Panel();
            filterPanel.Height = 85;
            filterPanel.Dock = DockStyle.Top;
            filterPanel.BackColor = Color.White;
            filterPanel.BorderStyle = BorderStyle.None;
            filterPanel.Padding = new Padding(10);

            // Grupa filtrów głównych
            GroupBox filterGroup = new GroupBox();
            filterGroup.Text = "Filtry";
            filterGroup.Dock = DockStyle.Fill;
            filterGroup.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Panel wewnętrzny dla kontrolek
            Panel innerPanel = new Panel();
            innerPanel.Dock = DockStyle.Fill;
            filterGroup.Controls.Add(innerPanel);

            // Label i TextBox dla wyszukiwania
            Label lblSearch = new Label();
            lblSearch.Text = "Szukaj:";
            lblSearch.Location = new Point(10, 20);
            lblSearch.Width = 50;
            lblSearch.Font = new Font("Segoe UI", 9F);
            innerPanel.Controls.Add(lblSearch);

            textBox1.Location = new Point(65, 18);
            textBox1.Width = 150;
            textBox1.Font = new Font("Segoe UI", 9F);
            innerPanel.Controls.Add(textBox1);

            // Data od
            Label lblDateFrom = new Label();
            lblDateFrom.Text = "Od:";
            lblDateFrom.Location = new Point(230, 20);
            lblDateFrom.Width = 30;
            lblDateFrom.Font = new Font("Segoe UI", 9F);
            innerPanel.Controls.Add(lblDateFrom);

            dateFromPicker = new DateTimePicker();
            dateFromPicker.Location = new Point(265, 18);
            dateFromPicker.Width = 100;
            dateFromPicker.Format = DateTimePickerFormat.Short;
            dateFromPicker.Value = DateTime.Now.AddMonths(-1);
            dateFromPicker.Font = new Font("Segoe UI", 9F);
            dateFromPicker.ValueChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            innerPanel.Controls.Add(dateFromPicker);

            // Data do
            Label lblDateTo = new Label();
            lblDateTo.Text = "Do:";
            lblDateTo.Location = new Point(375, 20);
            lblDateTo.Width = 30;
            lblDateTo.Font = new Font("Segoe UI", 9F);
            innerPanel.Controls.Add(lblDateTo);

            dateToPicker = new DateTimePicker();
            dateToPicker.Location = new Point(410, 18);
            dateToPicker.Width = 100;
            dateToPicker.Format = DateTimePickerFormat.Short;
            dateToPicker.Font = new Font("Segoe UI", 9F);
            dateToPicker.ValueChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            innerPanel.Controls.Add(dateToPicker);

            // ComboBox dla dostawcy
            Label lblDostawca = new Label();
            lblDostawca.Text = "Dostawca:";
            lblDostawca.Location = new Point(525, 20);
            lblDostawca.Width = 65;
            lblDostawca.Font = new Font("Segoe UI", 9F);
            innerPanel.Controls.Add(lblDostawca);

            comboBoxDostawca = new ComboBox();
            comboBoxDostawca.Location = new Point(595, 18);
            comboBoxDostawca.Width = 200;
            comboBoxDostawca.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxDostawca.Font = new Font("Segoe UI", 9F);
            comboBoxDostawca.SelectedIndexChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            innerPanel.Controls.Add(comboBoxDostawca);

            // ComboBox dla statusu
            Label lblStatus = new Label();
            lblStatus.Text = "Status:";
            lblStatus.Location = new Point(810, 20);
            lblStatus.Width = 50;
            lblStatus.Font = new Font("Segoe UI", 9F);
            innerPanel.Controls.Add(lblStatus);

            comboBoxStatus = new ComboBox();
            comboBoxStatus.Location = new Point(865, 18);
            comboBoxStatus.Width = 130;
            comboBoxStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStatus.Font = new Font("Segoe UI", 9F);
            comboBoxStatus.Items.AddRange(new[] { "Wszystkie", "Potwierdzony", "Anulowany", "Sprzedany", "B.Kontr.", "B.Wolny.", "Do wykupienia" });
            comboBoxStatus.SelectedIndex = 0;
            comboBoxStatus.SelectedIndexChanged += (s, e) => { if (!isLoading) ApplyFilters(); };
            innerPanel.Controls.Add(comboBoxStatus);

            // Używamy istniejącego checkBoxGroupBySupplier z Designer.cs
            checkBoxGroupBySupplier.Text = "Grupuj po dostawcy";
            checkBoxGroupBySupplier.Location = new Point(10, 48);
            checkBoxGroupBySupplier.Width = 150;
            checkBoxGroupBySupplier.Font = new Font("Segoe UI", 9F);
            checkBoxGroupBySupplier.CheckedChanged += CheckBoxGroupBySupplier_CheckedChanged;
            innerPanel.Controls.Add(checkBoxGroupBySupplier);

            // CheckBox statystyki
            checkBoxShowStatistics = new CheckBox();
            checkBoxShowStatistics.Text = "Pokaż statystyki";
            checkBoxShowStatistics.Location = new Point(170, 48);
            checkBoxShowStatistics.Width = 120;
            checkBoxShowStatistics.Font = new Font("Segoe UI", 9F);
            checkBoxShowStatistics.Checked = true;
            checkBoxShowStatistics.CheckedChanged += (s, e) => ToggleStatistics();
            innerPanel.Controls.Add(checkBoxShowStatistics);

            // Przycisk czyszczenia filtrów
            Button btnClearFilters = new Button();
            btnClearFilters.Text = "🔄 Wyczyść filtry";
            btnClearFilters.Location = new Point(300, 45);
            btnClearFilters.Width = 110;
            btnClearFilters.Height = 25;
            btnClearFilters.Font = new Font("Segoe UI", 9F);
            btnClearFilters.FlatStyle = FlatStyle.Flat;
            btnClearFilters.BackColor = Color.FromArgb(240, 240, 240);
            btnClearFilters.Click += (s, e) => ClearFilters();
            innerPanel.Controls.Add(btnClearFilters);

            filterPanel.Controls.Add(filterGroup);
            this.Controls.Add(filterPanel);
            filterPanel.BringToFront();
        }

        private void CreateSummaryPanel()
        {
            summaryPanel = new Panel();
            summaryPanel.Dock = DockStyle.Fill;
            summaryPanel.BackColor = Color.FromArgb(250, 250, 250);

            TableLayoutPanel tlp = new TableLayoutPanel();
            tlp.Dock = DockStyle.Fill;
            tlp.ColumnCount = 3;
            tlp.RowCount = 1;
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            // Panel KPI
            GroupBox kpiGroup = new GroupBox();
            kpiGroup.Text = "📊 Kluczowe Wskaźniki (KPI)";
            kpiGroup.Dock = DockStyle.Fill;
            kpiGroup.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            kpiGroup.ForeColor = Color.FromArgb(50, 50, 50);

            FlowLayoutPanel kpiFlow = new FlowLayoutPanel();
            kpiFlow.Dock = DockStyle.Fill;
            kpiFlow.FlowDirection = FlowDirection.TopDown;
            kpiFlow.Padding = new Padding(10);

            kpiFlow.Controls.Add(CreateKPILabel("Łączna liczba dostaw:", "0", Color.FromArgb(52, 152, 219)));
            kpiFlow.Controls.Add(CreateKPILabel("Łączna waga (kg):", "0", Color.FromArgb(46, 204, 113)));
            kpiFlow.Controls.Add(CreateKPILabel("Średnia waga/dostawę:", "0", Color.FromArgb(155, 89, 182)));
            kpiFlow.Controls.Add(CreateKPILabel("Łączna liczba sztuk:", "0", Color.FromArgb(241, 196, 15)));
            kpiFlow.Controls.Add(CreateKPILabel("Średnia cena:", "0", Color.FromArgb(231, 76, 60)));
            kpiFlow.Controls.Add(CreateKPILabel("Liczba dostawców:", "0", Color.FromArgb(52, 73, 94)));
            kpiFlow.Controls.Add(CreateKPILabel("Wskaźnik realizacji:", "0%", Color.FromArgb(26, 188, 156)));

            kpiGroup.Controls.Add(kpiFlow);
            tlp.Controls.Add(kpiGroup, 0, 0);

            // Wykres wydajności
            GroupBox chartGroup = new GroupBox();
            chartGroup.Text = "📈 Wydajność Tygodniowa";
            chartGroup.Dock = DockStyle.Fill;
            chartGroup.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            chartGroup.ForeColor = Color.FromArgb(50, 50, 50);

            performanceChart = new Chart();
            performanceChart.Dock = DockStyle.Fill;
            performanceChart.BackColor = Color.White;

            ChartArea chartArea = new ChartArea();
            chartArea.BackColor = Color.White;
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            performanceChart.ChartAreas.Add(chartArea);

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            series.Name = "Dostawy";
            series.Color = Color.FromArgb(52, 152, 219);
            performanceChart.Series.Add(series);

            chartGroup.Controls.Add(performanceChart);
            tlp.Controls.Add(chartGroup, 1, 0);

            // Top dostawcy
            GroupBox topSuppliersGroup = new GroupBox();
            topSuppliersGroup.Text = "🏆 Top 5 Dostawców";
            topSuppliersGroup.Dock = DockStyle.Fill;
            topSuppliersGroup.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            topSuppliersGroup.ForeColor = Color.FromArgb(50, 50, 50);

            dataGridViewSummary = new DataGridView();
            dataGridViewSummary.Dock = DockStyle.Fill;
            dataGridViewSummary.AllowUserToAddRows = false;
            dataGridViewSummary.ReadOnly = true;
            dataGridViewSummary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewSummary.BackgroundColor = Color.White;
            dataGridViewSummary.BorderStyle = BorderStyle.None;
            dataGridViewSummary.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewSummary.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dataGridViewSummary.GridColor = Color.FromArgb(230, 230, 230);
            dataGridViewSummary.RowHeadersVisible = false;

            topSuppliersGroup.Controls.Add(dataGridViewSummary);
            tlp.Controls.Add(topSuppliersGroup, 2, 0);

            summaryPanel.Controls.Add(tlp);
        }

        private Label CreateKPILabel(string title, string value, Color color)
        {
            Label lbl = new Label();
            lbl.Text = $"{title} {value}";
            lbl.AutoSize = true;
            lbl.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            lbl.ForeColor = color;
            lbl.Margin = new Padding(5, 3, 5, 3);
            return lbl;
        }

        private void CreateSupplierAnalysisTab(TabPage tab)
        {
            SplitContainer splitContainer = new SplitContainer();
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.BackColor = Color.White;

            DataGridView supplierGrid = new DataGridView();
            supplierGrid.Dock = DockStyle.Fill;
            supplierGrid.AllowUserToAddRows = false;
            supplierGrid.ReadOnly = true;
            supplierGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            supplierGrid.BackgroundColor = Color.White;
            supplierGrid.BorderStyle = BorderStyle.None;
            supplierGrid.GridColor = Color.FromArgb(230, 230, 230);
            splitContainer.Panel1.Controls.Add(supplierGrid);

            Panel detailsPanel = new Panel();
            detailsPanel.Dock = DockStyle.Fill;
            detailsPanel.AutoScroll = true;
            detailsPanel.BackColor = Color.White;

            Label lblDetails = new Label();
            lblDetails.Text = "Wybierz dostawcę aby zobaczyć szczegóły";
            lblDetails.Dock = DockStyle.Fill;
            lblDetails.TextAlign = ContentAlignment.MiddleCenter;
            lblDetails.Font = new Font("Segoe UI", 10F);
            lblDetails.ForeColor = Color.Gray;
            detailsPanel.Controls.Add(lblDetails);

            splitContainer.Panel2.Controls.Add(detailsPanel);
            tab.Controls.Add(splitContainer);
        }

        private void CreateStatisticsTab(TabPage tab)
        {
            TableLayoutPanel tlp = new TableLayoutPanel();
            tlp.Dock = DockStyle.Fill;
            tlp.ColumnCount = 2;
            tlp.RowCount = 2;
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlp.Padding = new Padding(10);
            tlp.BackColor = Color.White;

            Chart trendChart = CreateChart("Trend Dostaw", SeriesChartType.Line);
            tlp.Controls.Add(trendChart, 0, 0);

            Chart statusChart = CreateChart("Podział wg Statusu", SeriesChartType.Pie);
            tlp.Controls.Add(statusChart, 1, 0);

            Chart comparisonChart = CreateChart("Porównanie Dostawców", SeriesChartType.Bar);
            tlp.Controls.Add(comparisonChart, 0, 1);

            Chart priceChart = CreateChart("Analiza Cenowa", SeriesChartType.Area);
            tlp.Controls.Add(priceChart, 1, 1);

            tab.Controls.Add(tlp);
        }

        private Chart CreateChart(string title, SeriesChartType chartType)
        {
            Chart chart = new Chart();
            chart.Dock = DockStyle.Fill;
            chart.BackColor = Color.White;

            ChartArea chartArea = new ChartArea();
            chartArea.BackColor = Color.White;
            chart.ChartAreas.Add(chartArea);

            Series series = new Series();
            series.ChartType = chartType;
            series.Name = title;
            chart.Series.Add(series);

            var chartTitle = new Title(title);
            chartTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            chart.Titles.Add(chartTitle);

            return chart;
        }

        private async void LoadDataAsync()
        {
            try
            {
                isLoading = true;
                UpdateStatus("Ładowanie danych...");

                // Wyłącz formatowanie podczas ładowania
                dataGridView1.CellFormatting -= DataGridView1_CellFormatting;

                await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionPermission))
                    {
                        string query = @"
                            SELECT 
                                LP, DostawcaID, DataOdbioru, Dostawca, Auta, 
                                SztukiDek, WagaDek, SztSzuflada, TypUmowy, TypCeny, 
                                Cena, PaszaPisklak, Bufor, UWAGI, LpW, LpP1, LpP2,
                                Utworzone, Wysłane, Otrzymane, PotwWaga, KtoWaga, 
                                KiedyWaga, PotwSztuki, KtoSztuki, KiedySztuki, 
                                PotwCena, Dodatek, Kurnik, KmK, KmH, Ubiorka, 
                                Ubytek, DataUtw, KtoStwo, DataMod, KtoMod, 
                                CzyOdznaczoneWstawienie
                            FROM [LibraNet].[dbo].[HarmonogramDostaw] 
                            ORDER BY LP DESC";

                        SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                        DataTable table = new DataTable();
                        adapter.Fill(table);

                        originalData = table.Copy();

                        this.Invoke(new Action(() =>
                        {
                            dataGridView1.SuspendLayout();
                            dataGridView1.DataSource = table;
                            ConfigureDataGridView();
                            LoadComboBoxData();
                            dataGridView1.ResumeLayout();

                            UpdateStatistics();
                            UpdateStatus($"Załadowano {table.Rows.Count} rekordów");
                        }));
                    }
                });

                statusStrip.Items[2].Text = $"Ostatnia aktualizacja: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Błąd ładowania danych");
            }
            finally
            {
                isLoading = false;
                // Włącz formatowanie po załadowaniu
                dataGridView1.CellFormatting += DataGridView1_CellFormatting;
                dataGridView1.Refresh();
            }
        }

        private void ConfigureDataGridView()
        {
            // Stylizacja DataGridView
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridView1.GridColor = Color.FromArgb(230, 230, 230);
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = true;
            dataGridView1.RowHeadersWidth = 30;
            dataGridView1.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridView1.ColumnHeadersHeight = 35;
            dataGridView1.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dataGridView1.DefaultCellStyle.SelectionBackColor = Color.FromArgb(155, 89, 182);
            dataGridView1.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            dataGridView1.EnableHeadersVisualStyles = false;

            // Konfiguracja kolumn
            ConfigureColumns();

            // Obsługa zdarzeń - dodaj tylko raz
            dataGridView1.ColumnHeaderMouseClick -= DataGridView1_ColumnHeaderMouseClick;
            dataGridView1.ColumnHeaderMouseClick += DataGridView1_ColumnHeaderMouseClick;

            dataGridView1.SelectionChanged -= DataGridView1_SelectionChanged;
            dataGridView1.SelectionChanged += DataGridView1_SelectionChanged;

            // Ustawienie wysokości wierszy
            SetRowHeights(25);
        }

        private void ConfigureColumns()
        {
            var columnConfigs = new Dictionary<string, (string HeaderText, int Width, bool Visible)>
            {
                {"Lp", ("Lp", 50, true)},
                {"DostawcaID", ("ID", 50, true)},
                {"DataOdbioru", ("Data Odb.", 85, true)},
                {"Dostawca", ("Dostawca", 180, true)},
                {"Auta", ("Auta", 50, true)},
                {"SztukiDek", ("Szt.Dek", 70, true)},
                {"WagaDek", ("Waga", 70, true)},
                {"SztSzuflada", ("Szt.Sz", 60, true)},
                {"TypUmowy", ("Umowa", 70, true)},
                {"TypCeny", ("Typ Ceny", 70, true)},
                {"Cena", ("Cena", 70, true)},
                {"PaszaPisklak", ("Pasza", 70, false)},
                {"Bufor", ("Status", 100, true)},
                {"UWAGI", ("Uwagi", 150, true)},
                {"LpW", ("LpW", 50, false)},
                {"LpP1", ("LpP1", 50, false)},
                {"LpP2", ("LpP2", 50, false)},
                {"Utworzone", ("Utw", 40, true)},
                {"Wysłane", ("Wys", 40, true)},
                {"Otrzymane", ("Otrz", 40, true)},
                {"PotwWaga", ("P.Waga", 60, true)},
                {"KtoWaga", ("Kto W.", 80, false)},
                {"KiedyWaga", ("Kiedy W.", 80, false)},
                {"PotwSztuki", ("P.Szt", 60, true)},
                {"KtoSztuki", ("Kto S.", 80, false)},
                {"KiedySztuki", ("Kiedy S.", 80, false)},
                {"PotwCena", ("P.Cena", 60, true)},
                {"Dodatek", ("Dod.", 60, true)},
                {"Kurnik", ("Kurnik", 100, true)},
                {"KmK", ("KmK", 60, true)},
                {"KmH", ("KmH", 60, true)},
                {"Ubiorka", ("Ubiorka", 70, false)},
                {"Ubytek", ("Ubytek %", 70, true)},
                {"DataUtw", ("Data Utw.", 85, false)},
                {"KtoStwo", ("Utworzył", 80, false)},
                {"DataMod", ("Data Mod.", 85, false)},
                {"KtoMod", ("Zmodyfikował", 80, false)},
                {"CzyOdznaczoneWstawienie", ("Ozn.", 40, true)}
            };

            foreach (var config in columnConfigs)
            {
                if (dataGridView1.Columns.Contains(config.Key))
                {
                    var column = dataGridView1.Columns[config.Key];
                    column.HeaderText = config.Value.HeaderText;
                    column.Width = config.Value.Width;
                    column.Visible = config.Value.Visible;

                    // Formatowanie dla kolumn liczbowych
                    if (config.Key == "WagaDek" || config.Key == "Cena")
                    {
                        column.DefaultCellStyle.Format = "N2";
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    else if (config.Key == "SztukiDek" || config.Key == "Auta")
                    {
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }
                    else if (config.Key == "DataOdbioru" || config.Key == "DataUtw" || config.Key == "DataMod")
                    {
                        column.DefaultCellStyle.Format = "dd.MM.yyyy";
                    }
                }
            }
        }

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.RowIndex >= dataGridView1.Rows.Count) return;

                DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
                if (row == null) return;

                // Kolorowanie według statusu - zabezpieczone przed nullami
                if (dataGridView1.Columns[e.ColumnIndex].Name == "Bufor")
                {
                    var buforCell = row.Cells["Bufor"];
                    if (buforCell != null && buforCell.Value != null && buforCell.Value != DBNull.Value)
                    {
                        string status = buforCell.Value.ToString();
                        if (!string.IsNullOrEmpty(status))
                        {
                            Color bgColor = Color.White;
                            Color fgColor = Color.Black;
                            FontStyle fontStyle = FontStyle.Regular;

                            switch (status.Trim())
                            {
                                case "Potwierdzony":
                                    bgColor = Color.FromArgb(198, 239, 206);
                                    fontStyle = FontStyle.Bold;
                                    break;
                                case "Anulowany":
                                    bgColor = Color.FromArgb(255, 199, 199);
                                    break;
                                case "Sprzedany":
                                    bgColor = Color.FromArgb(199, 223, 255);
                                    break;
                                case "B.Kontr.":
                                    bgColor = Color.FromArgb(147, 112, 219);
                                    fgColor = Color.White;
                                    break;
                                case "B.Wolny.":
                                    bgColor = Color.FromArgb(255, 253, 184);
                                    break;
                                case "Do wykupienia":
                                    bgColor = Color.FromArgb(245, 245, 245);
                                    break;
                            }

                            row.DefaultCellStyle.BackColor = bgColor;
                            row.DefaultCellStyle.ForeColor = fgColor;
                            if (fontStyle != FontStyle.Regular)
                            {
                                row.DefaultCellStyle.Font = new Font(dataGridView1.Font.FontFamily, 9, fontStyle);
                            }
                        }
                    }
                }

                // Wyróżnienie przekroczonych terminów - zabezpieczone
                if (dataGridView1.Columns[e.ColumnIndex].Name == "DataOdbioru")
                {
                    if (e.Value != null && e.Value != DBNull.Value && e.Value is DateTime)
                    {
                        DateTime dataOdbioru = (DateTime)e.Value;
                        var buforCell = row.Cells["Bufor"];
                        string status = buforCell?.Value?.ToString() ?? "";

                        if (dataOdbioru < DateTime.Now.Date && status != "Potwierdzony")
                        {
                            e.CellStyle.ForeColor = Color.Red;
                            e.CellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                        }
                    }
                }

                // Formatowanie wartości null
                if (e.Value == null || e.Value == DBNull.Value)
                {
                    e.Value = "-";
                    e.CellStyle.ForeColor = Color.Gray;
                }
            }
            catch
            {
                // Ignoruj błędy formatowania
            }
        }

        private void DataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                string columnName = dataGridView1.Columns[e.ColumnIndex].Name;
                System.Windows.Forms.SortOrder currentOrder = dataGridView1.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection;

                foreach (DataGridViewColumn col in dataGridView1.Columns)
                {
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                }

                System.Windows.Forms.SortOrder newOrder = currentOrder == System.Windows.Forms.SortOrder.Ascending
                    ? System.Windows.Forms.SortOrder.Descending
                    : System.Windows.Forms.SortOrder.Ascending;
                dataGridView1.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = newOrder;

                DataTable dt = dataGridView1.DataSource as DataTable;
                if (dt != null)
                {
                    DataView dv = dt.DefaultView;
                    dv.Sort = columnName + (newOrder == System.Windows.Forms.SortOrder.Ascending ? " ASC" : " DESC");
                    dataGridView1.DataSource = dv.ToTable();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd sortowania: {ex.Message}");
            }
        }

        private void DataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                UpdateSelectionStatistics();
            }
        }

        private void UpdateSelectionStatistics()
        {
            try
            {
                if (dataGridView1.SelectedRows.Count == 0) return;

                decimal totalWeight = 0;
                int totalPieces = 0;
                decimal totalPrice = 0;
                int count = 0;

                foreach (DataGridViewRow row in dataGridView1.SelectedRows)
                {
                    if (row.Cells["WagaDek"]?.Value != null && row.Cells["WagaDek"].Value != DBNull.Value)
                        totalWeight += Convert.ToDecimal(row.Cells["WagaDek"].Value);

                    if (row.Cells["SztukiDek"]?.Value != null && row.Cells["SztukiDek"].Value != DBNull.Value)
                        totalPieces += Convert.ToInt32(row.Cells["SztukiDek"].Value);

                    if (row.Cells["Cena"]?.Value != null && row.Cells["Cena"].Value != DBNull.Value)
                        totalPrice += Convert.ToDecimal(row.Cells["Cena"].Value);

                    count++;
                }

                string status = $"Zaznaczono: {count} | Waga: {totalWeight:N2} kg | Sztuki: {totalPieces} | Wartość: {totalPrice:C}";
                statusStrip.Items[0].Text = status;
            }
            catch
            {
                // Ignoruj błędy statystyk
            }
        }

        private void LoadComboBoxData()
        {
            if (originalData == null) return;

            var suppliers = originalData.AsEnumerable()
                .Select(r => r.Field<string>("Dostawca"))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            comboBoxDostawca.Items.Clear();
            comboBoxDostawca.Items.Add("Wszyscy");
            comboBoxDostawca.Items.AddRange(suppliers.ToArray());
            comboBoxDostawca.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (originalData == null || isLoading) return;

            try
            {
                string filter = BuildFilterString();
                DataView dv = new DataView(originalData);
                dv.RowFilter = filter;

                dataGridView1.SuspendLayout();
                dataGridView1.DataSource = dv.ToTable();
                dataGridView1.ResumeLayout();

                UpdateStatistics();
                UpdateStatus($"Wyświetlono {dv.Count} z {originalData.Rows.Count} rekordów");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd filtrowania: {ex.Message}");
            }
        }

        private string BuildFilterString()
        {
            List<string> filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(textBox1.Text))
            {
                string searchText = textBox1.Text.Trim().Replace("'", "''");
                filters.Add($"Dostawca LIKE '%{searchText}%'");
            }

            if (dateFromPicker != null && dateToPicker != null)
            {
                filters.Add($"DataOdbioru >= #{dateFromPicker.Value.Date:yyyy-MM-dd}#");
                filters.Add($"DataOdbioru <= #{dateToPicker.Value.Date:yyyy-MM-dd}#");
            }

            if (comboBoxDostawca != null && comboBoxDostawca.SelectedIndex > 0)
            {
                string supplier = comboBoxDostawca.SelectedItem.ToString().Replace("'", "''");
                filters.Add($"Dostawca = '{supplier}'");
            }

            if (comboBoxStatus != null && comboBoxStatus.SelectedIndex > 0)
            {
                string status = comboBoxStatus.SelectedItem.ToString().Replace("'", "''");
                filters.Add($"Bufor = '{status}'");
            }

            return filters.Count > 0 ? string.Join(" AND ", filters) : "";
        }

        private void CheckBoxGroupBySupplier_CheckedChanged(object sender, EventArgs e)
        {
            if (!isLoading)
                ApplyGrouping();
        }

        private void ApplyGrouping()
        {
            if (originalData == null || isLoading) return;

            try
            {
                if (!checkBoxGroupBySupplier.Checked)
                {
                    dataGridView1.DataSource = originalData;
                    return;
                }

                var groupedData = originalData.AsEnumerable()
                    .Where(r => r.Field<string>("Dostawca") != null)
                    .GroupBy(r => r.Field<string>("Dostawca"))
                    .Select(g => new
                    {
                        Dostawca = g.Key,
                        LiczbaDostaw = g.Count(),
                        SumaWagi = g.Sum(r => r.Field<decimal?>("WagaDek") ?? 0),
                        SumaSztuk = g.Sum(r => r.Field<int?>("SztukiDek") ?? 0),
                        SredniaWaga = g.Average(r => r.Field<decimal?>("WagaDek") ?? 0),
                        SredniaCena = g.Average(r => r.Field<decimal?>("Cena") ?? 0),
                        OstatniaDostawa = g.Max(r => r.Field<DateTime?>("DataOdbioru"))
                    })
                    .OrderByDescending(x => x.LiczbaDostaw);

                DataTable groupedTable = new DataTable();
                groupedTable.Columns.Add("Dostawca", typeof(string));
                groupedTable.Columns.Add("Liczba Dostaw", typeof(int));
                groupedTable.Columns.Add("Suma Wagi (kg)", typeof(decimal));
                groupedTable.Columns.Add("Suma Sztuk", typeof(int));
                groupedTable.Columns.Add("Średnia Waga", typeof(decimal));
                groupedTable.Columns.Add("Średnia Cena", typeof(decimal));
                groupedTable.Columns.Add("Ostatnia Dostawa", typeof(DateTime));

                foreach (var item in groupedData)
                {
                    groupedTable.Rows.Add(item.Dostawca, item.LiczbaDostaw, item.SumaWagi,
                        item.SumaSztuk, item.SredniaWaga, item.SredniaCena, item.OstatniaDostawa);
                }

                dataGridView1.DataSource = groupedTable;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd grupowania: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                if (dataGridView1.DataSource == null) return;

                DataTable currentData = dataGridView1.DataSource as DataTable;
                if (currentData == null || currentData.Rows.Count == 0) return;

                // Sprawdź czy kolumny istnieją przed próbą dostępu
                bool hasRequiredColumns = currentData.Columns.Contains("WagaDek") &&
                                         currentData.Columns.Contains("SztukiDek") &&
                                         currentData.Columns.Contains("Cena") &&
                                         currentData.Columns.Contains("Bufor");

                if (!hasRequiredColumns)
                {
                    UpdateStatus("Brak wymaganych kolumn do obliczenia statystyk");
                    return;
                }

                int totalDeliveries = currentData.Rows.Count;
                decimal totalWeight = 0;
                int totalPieces = 0;
                decimal totalPrice = 0;
                int confirmedCount = 0;

                foreach (DataRow row in currentData.Rows)
                {
                    if (row["WagaDek"] != DBNull.Value)
                        totalWeight += Convert.ToDecimal(row["WagaDek"]);

                    if (row["SztukiDek"] != DBNull.Value)
                        totalPieces += Convert.ToInt32(row["SztukiDek"]);

                    if (row["Cena"] != DBNull.Value)
                        totalPrice += Convert.ToDecimal(row["Cena"]);

                    if (row["Bufor"]?.ToString() == "Potwierdzony")
                        confirmedCount++;
                }

                decimal avgWeight = totalDeliveries > 0 ? totalWeight / totalDeliveries : 0;
                decimal avgPrice = totalDeliveries > 0 ? totalPrice / totalDeliveries : 0;
                decimal realizationRate = totalDeliveries > 0 ? (confirmedCount * 100.0m / totalDeliveries) : 0;

                // Aktualizuj KPI w panelu
                UpdateKPIPanel(totalDeliveries, totalWeight, avgWeight, totalPieces, avgPrice, realizationRate, currentData);

                UpdateCharts(currentData);
                UpdateTopSuppliers(currentData);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd aktualizacji statystyk: {ex.Message}");
            }
        }

        private void UpdateKPIPanel(int totalDeliveries, decimal totalWeight, decimal avgWeight,
            int totalPieces, decimal avgPrice, decimal realizationRate, DataTable currentData)
        {
            if (summaryPanel != null && summaryPanel.Controls.Count > 0)
            {
                var tlp = summaryPanel.Controls[0] as TableLayoutPanel;
                if (tlp != null && tlp.Controls.Count > 0)
                {
                    var kpiGroup = tlp.Controls[0] as GroupBox;
                    if (kpiGroup != null && kpiGroup.Controls.Count > 0)
                    {
                        var kpiFlow = kpiGroup.Controls[0] as FlowLayoutPanel;
                        if (kpiFlow != null && kpiFlow.Controls.Count >= 7)
                        {
                            kpiFlow.Controls[0].Text = $"Łączna liczba dostaw: {totalDeliveries:N0}";
                            kpiFlow.Controls[1].Text = $"Łączna waga (kg): {totalWeight:N2}";
                            kpiFlow.Controls[2].Text = $"Średnia waga/dostawę: {avgWeight:N2}";
                            kpiFlow.Controls[3].Text = $"Łączna liczba sztuk: {totalPieces:N0}";
                            kpiFlow.Controls[4].Text = $"Średnia cena: {avgPrice:C}";
                            kpiFlow.Controls[5].Text = $"Liczba dostawców: {GetUniqueSuppliers(currentData)}";
                            kpiFlow.Controls[6].Text = $"Wskaźnik realizacji: {realizationRate:N1}%";
                        }
                    }
                }
            }
        }

        private int GetUniqueSuppliers(DataTable data)
        {
            if (!data.Columns.Contains("Dostawca")) return 0;

            return data.AsEnumerable()
                .Select(r => r.Field<string>("Dostawca"))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .Count();
        }

        private void UpdateCharts(DataTable data)
        {
            try
            {
                if (performanceChart == null || !data.Columns.Contains("DataOdbioru") || !data.Columns.Contains("WagaDek"))
                    return;

                var weeklyData = data.AsEnumerable()
                    .Where(r => r["DataOdbioru"] != DBNull.Value)
                    .GroupBy(r => GetWeekOfYear(Convert.ToDateTime(r["DataOdbioru"])))
                    .Select(g => new
                    {
                        Week = g.Key,
                        Count = g.Count(),
                        Weight = g.Sum(r => r["WagaDek"] != DBNull.Value ? Convert.ToDecimal(r["WagaDek"]) : 0)
                    })
                    .OrderBy(x => x.Week)
                    .Take(8);

                performanceChart.Series[0].Points.Clear();
                foreach (var week in weeklyData)
                {
                    performanceChart.Series[0].Points.AddXY($"Tyg. {week.Week}", week.Weight);
                }
            }
            catch
            {
                // Ignoruj błędy wykresów
            }
        }

        private int GetWeekOfYear(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private void UpdateTopSuppliers(DataTable data)
        {
            try
            {
                if (dataGridViewSummary == null || !data.Columns.Contains("Dostawca") || !data.Columns.Contains("WagaDek"))
                    return;

                var topSuppliers = data.AsEnumerable()
                    .Where(r => r["Dostawca"] != null && r["Dostawca"] != DBNull.Value)
                    .GroupBy(r => r.Field<string>("Dostawca"))
                    .Select(g => new
                    {
                        Dostawca = g.Key ?? "Nieznany",
                        Dostawy = g.Count(),
                        Waga = g.Sum(r => r["WagaDek"] != DBNull.Value ? Convert.ToDecimal(r["WagaDek"]) : 0)
                    })
                    .OrderByDescending(x => x.Waga)
                    .Take(5);

                DataTable topTable = new DataTable();
                topTable.Columns.Add("Dostawca", typeof(string));
                topTable.Columns.Add("Dostawy", typeof(int));
                topTable.Columns.Add("Waga (kg)", typeof(decimal));

                foreach (var supplier in topSuppliers)
                {
                    topTable.Rows.Add(supplier.Dostawca, supplier.Dostawy, supplier.Waga);
                }

                dataGridViewSummary.DataSource = topTable;

                if (dataGridViewSummary.Columns["Waga (kg)"] != null)
                {
                    dataGridViewSummary.Columns["Waga (kg)"].DefaultCellStyle.Format = "N2";
                }
            }
            catch
            {
                // Ignoruj błędy top dostawców
            }
        }

        private void ClearFilters()
        {
            isLoading = true;
            textBox1.Clear();
            dateFromPicker.Value = DateTime.Now.AddMonths(-1);
            dateToPicker.Value = DateTime.Now;
            if (comboBoxDostawca.Items.Count > 0)
                comboBoxDostawca.SelectedIndex = 0;
            if (comboBoxStatus.Items.Count > 0)
                comboBoxStatus.SelectedIndex = 0;
            isLoading = false;

            dataGridView1.DataSource = originalData;
            UpdateStatistics();
        }

        private void ToggleStatistics()
        {
            if (mainSplitContainer != null)
            {
                mainSplitContainer.Panel2Collapsed = !checkBoxShowStatistics.Checked;
            }
        }

        private void ExportToExcel()
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                saveDialog.FilterIndex = 1;
                saveDialog.RestoreDirectory = true;
                saveDialog.FileName = $"Dostawy_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    ExportToCSV(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportToCSV(string fileName)
        {
            StringBuilder csv = new StringBuilder();

            // Nagłówki
            for (int i = 0; i < dataGridView1.Columns.Count; i++)
            {
                if (dataGridView1.Columns[i].Visible)
                {
                    csv.Append(dataGridView1.Columns[i].HeaderText);
                    if (i < dataGridView1.Columns.Count - 1)
                        csv.Append(";");
                }
            }
            csv.AppendLine();

            // Dane
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                for (int i = 0; i < dataGridView1.Columns.Count; i++)
                {
                    if (dataGridView1.Columns[i].Visible)
                    {
                        csv.Append(row.Cells[i].Value?.ToString() ?? "");
                        if (i < dataGridView1.Columns.Count - 1)
                            csv.Append(";");
                    }
                }
                csv.AppendLine();
            }

            File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
            MessageBox.Show($"Dane zostały wyeksportowane do pliku:\n{fileName}", "Sukces",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PrintData()
        {
            MessageBox.Show("Funkcja drukowania zostanie wkrótce dodana", "Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowStatistics()
        {
            if (tabControl != null && tabControl.TabPages.Count > 2)
            {
                tabControl.SelectedIndex = 2;
            }
        }

        private void ShowCharts()
        {
            if (tabControl != null && tabControl.TabPages.Count > 2)
            {
                tabControl.SelectedIndex = 2;
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusStrip != null && statusStrip.Items.Count > 0)
            {
                statusStrip.Items[0].Text = message;
            }
        }

        private void SetRowHeights(int height)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                row.Height = height;
            }
        }

        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (!isLoading)
                ApplyFilters();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }

    // Extension method dla Double Buffering
    public static class ExtensionMethods
    {
        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            Type dgvType = dgv.GetType();
            System.Reflection.PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi.SetValue(dgv, setting, null);
        }
    }
}