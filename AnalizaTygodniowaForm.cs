using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace Kalendarz1
{
    // ============================================================================
    // GŁÓWNY FORMULARZ ANALIZY TYGODNIOWEJ Z ROZSZERZONYMI STATYSTYKAMI
    // ============================================================================
    public partial class AnalizaTygodniowaForm : Form
    {
        private string connectionString;
        private int kontrahentId;
        private int? towarId;
        private string nazwaKontrahenta;
        private string nazwaTowaru;

        private DataGridView dataGridViewAnaliza;
        private Chart chartTrend;
        private Panel panelStats;
        private Label lblTitle;
        private Label lblSubtitle;
        private Button btnExport;
        private Button btnClose;
        private Button btnShowChart;
        private Button btnHelp;
        private Panel panelHeader;
        private Panel panelFooter;
        private SplitContainer splitContainer;

        // Statystyki
        private Label lblTotalSales;
        private Label lblAvgWeekly;
        private Label lblTrend;
        private Label lblRegularity;
        private Label lblBestWeek;
        private Label lblWorstWeek;

        // Profesjonalna kolorystyka - Corporate Blue Theme
        private readonly Color primaryColor = Color.FromArgb(44, 62, 80);
        private readonly Color secondaryColor = Color.FromArgb(52, 73, 94);
        private readonly Color accentColor = Color.FromArgb(52, 152, 219);
        private readonly Color successColor = Color.FromArgb(39, 174, 96);
        private readonly Color warningColor = Color.FromArgb(243, 156, 18);
        private readonly Color dangerColor = Color.FromArgb(231, 76, 60);
        private readonly Color lightBg = Color.FromArgb(236, 240, 241);

        private DataTable mainDataTable;

        public AnalizaTygodniowaForm(string connString, int khId, int? twId, string khNazwa, string twNazwa = null)
        {
            connectionString = connString;
            kontrahentId = khId;
            towarId = twId;
            nazwaKontrahenta = khNazwa;
            nazwaTowaru = twNazwa ?? "Wszystkie towary";

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            ConfigureForm();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.dataGridViewAnaliza = new DataGridView();
            this.chartTrend = new Chart();
            this.panelStats = new Panel();
            this.lblTitle = new Label();
            this.lblSubtitle = new Label();
            this.btnExport = new Button();
            this.btnClose = new Button();
            this.btnShowChart = new Button();
            this.btnHelp = new Button();
            this.panelHeader = new Panel();
            this.panelFooter = new Panel();
            this.splitContainer = new SplitContainer();

            // Statystyki
            this.lblTotalSales = new Label();
            this.lblAvgWeekly = new Label();
            this.lblTrend = new Label();
            this.lblRegularity = new Label();
            this.lblBestWeek = new Label();
            this.lblWorstWeek = new Label();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chartTrend)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();

            // Form
            this.Text = "📊 Zaawansowana Analiza Tygodniowa";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1400, 700);
            this.BackColor = lightBg;

            // Panel Header - LEPSZY WYGLĄD
            this.panelHeader.Dock = DockStyle.Top;
            this.panelHeader.Height = 140;
            this.panelHeader.BackColor = Color.White;
            this.panelHeader.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(panelHeader.ClientRectangle,
                    primaryColor, secondaryColor, 90f))
                {
                    e.Graphics.FillRectangle(brush, panelHeader.ClientRectangle);
                }

                // Delikatna linia na dole
                using (var pen = new Pen(Color.FromArgb(100, 255, 255, 255), 2))
                {
                    e.Graphics.DrawLine(pen, 0, panelHeader.Height - 1, panelHeader.Width, panelHeader.Height - 1);
                }
            };

            // Tytuł - WIĘKSZY I BARDZIEJ CZYTELNY
            this.lblTitle.Text = "📊 ZAAWANSOWANA ANALIZA TYGODNIOWA";
            this.lblTitle.Font = new Font("Segoe UI", 24f, FontStyle.Bold);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.Location = new Point(30, 20);
            this.lblTitle.AutoSize = true;

            // Podtytuł - LEPSZE FORMATOWANIE
            this.lblSubtitle.Font = new Font("Segoe UI", 13f, FontStyle.Regular);
            this.lblSubtitle.ForeColor = Color.FromArgb(230, 240, 250);
            this.lblSubtitle.Location = new Point(30, 65);
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Size = new Size(1200, 28);

            // Formatowanie z ikonami
            this.lblSubtitle.Text = $"👤 Kontrahent: {nazwaKontrahenta}  •  📦 Towar: {nazwaTowaru}";

            var lblHint = new Label
            {
                Text = "💡 Wskazówka: Dwukrotne kliknięcie na komórce tygodnia wyświetli szczegółową analizę",
                Font = new Font("Segoe UI", 10f, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 210, 220),
                Location = new Point(30, 100),
                AutoSize = true
            };

            // PRZYCISK POMOCY W NAGŁÓWKU
            var btnHeaderHelp = new Button
            {
                Text = "❓ POMOC",
                Size = new Size(110, 45),
                Location = new Point(30, 20),
                BackColor = Color.FromArgb(100, 255, 255, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnHeaderHelp.FlatAppearance.BorderSize = 2;
            btnHeaderHelp.FlatAppearance.BorderColor = Color.White;
            btnHeaderHelp.FlatAppearance.MouseOverBackColor = Color.FromArgb(150, 255, 255, 255);
            btnHeaderHelp.Click += (s, e) => ShowMainHelp();

            this.panelHeader.SizeChanged += (s, e) =>
            {
                btnHeaderHelp.Location = new Point(panelHeader.Width - 150, 20);
            };

            this.panelHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle, lblHint, btnHeaderHelp });

            // SplitContainer
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Orientation = Orientation.Horizontal;
            this.splitContainer.SplitterDistance = 400;
            this.splitContainer.BackColor = Color.FromArgb(220, 220, 220);

            // Panel górny (DataGridView + Statystyki)
            var topPanel = new Panel { Dock = DockStyle.Fill };
            var statsContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 1000
            };

            // DataGridView
            ConfigureDataGridView();
            statsContainer.Panel1.Controls.Add(this.dataGridViewAnaliza);

            // Panel statystyk
            ConfigureStatsPanel();
            statsContainer.Panel2.Controls.Add(this.panelStats);

            topPanel.Controls.Add(statsContainer);
            this.splitContainer.Panel1.Controls.Add(topPanel);

            // Panel dolny (Wykres)
            ConfigureChart();
            this.splitContainer.Panel2.Controls.Add(this.chartTrend);

            // Panel Footer
            ConfigureFooter();

            // Dodanie kontrolek
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.panelFooter);
            this.Controls.Add(this.panelHeader);

            ((System.ComponentModel.ISupportInitialize)(this.chartTrend)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.ResumeLayout(false);
        }

        private void ConfigureDataGridView()
        {
            this.dataGridViewAnaliza.Dock = DockStyle.Fill;
            this.dataGridViewAnaliza.BorderStyle = BorderStyle.None;
            this.dataGridViewAnaliza.BackgroundColor = Color.White;
            this.dataGridViewAnaliza.AllowUserToAddRows = false;
            this.dataGridViewAnaliza.AllowUserToDeleteRows = false;
            this.dataGridViewAnaliza.AllowUserToResizeRows = false;
            this.dataGridViewAnaliza.ReadOnly = true;
            this.dataGridViewAnaliza.RowHeadersVisible = false;
            this.dataGridViewAnaliza.SelectionMode = DataGridViewSelectionMode.CellSelect;
            this.dataGridViewAnaliza.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            this.dataGridViewAnaliza.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            this.dataGridViewAnaliza.GridColor = Color.FromArgb(240, 240, 240);

            // Stylizacja nagłówków z gradientem
            this.dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.BackColor = primaryColor;
            this.dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            this.dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            this.dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 5, 8, 5);
            this.dataGridViewAnaliza.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.dataGridViewAnaliza.ColumnHeadersHeight = 55;
            this.dataGridViewAnaliza.EnableHeadersVisualStyles = false;

            // Stylizacja komórek - alternujące wiersze
            this.dataGridViewAnaliza.DefaultCellStyle.SelectionBackColor = accentColor;
            this.dataGridViewAnaliza.DefaultCellStyle.SelectionForeColor = Color.White;
            this.dataGridViewAnaliza.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            this.dataGridViewAnaliza.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            this.dataGridViewAnaliza.DefaultCellStyle.BackColor = Color.White;
            this.dataGridViewAnaliza.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 249, 249);
            this.dataGridViewAnaliza.RowTemplate.Height = 40;

            // Efekt hover na wierszach
            this.dataGridViewAnaliza.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    dataGridViewAnaliza.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255);
                }
            };

            this.dataGridViewAnaliza.CellMouseLeave += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var isAlternate = e.RowIndex % 2 == 1;
                    dataGridViewAnaliza.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                        isAlternate ? Color.FromArgb(249, 249, 249) : Color.White;
                }
            };

            // Double-click handler
            this.dataGridViewAnaliza.CellDoubleClick += DataGridViewAnaliza_CellDoubleClick;
            this.dataGridViewAnaliza.CellFormatting += DataGridViewAnaliza_CellFormatting;
            this.dataGridViewAnaliza.DataBindingComplete += DataGridViewAnaliza_DataBindingComplete;
        }

        private void ConfigureStatsPanel()
        {
            this.panelStats.Dock = DockStyle.Fill;
            this.panelStats.BackColor = lightBg;
            this.panelStats.Padding = new Padding(15);

            var titleStats = new Label
            {
                Text = "📈 Statystyki i Wskaźniki",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = primaryColor,
                Location = new Point(15, 15),
                AutoSize = true
            };

            // Przycisk pomocy dla statystyk
            var btnStatsHelp = new Button
            {
                Text = "❓",
                Size = new Size(35, 35),
                Location = new Point(240, 12),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStatsHelp.FlatAppearance.BorderSize = 0;
            btnStatsHelp.Click += (s, e) => ShowStatsHelp();

            this.panelStats.Controls.Add(titleStats);
            this.panelStats.Controls.Add(btnStatsHelp);

            // Kontener na karty statystyk
            int yPos = 55;
            this.panelStats.Controls.Add(CreateStatCard("💰 Suma Sprzedaży", ref lblTotalSales, yPos));
            yPos += 85;
            this.panelStats.Controls.Add(CreateStatCard("📊 Średnia Tygodniowa", ref lblAvgWeekly, yPos));
            yPos += 85;
            this.panelStats.Controls.Add(CreateStatCard("📈 Trend", ref lblTrend, yPos));
            yPos += 85;
            this.panelStats.Controls.Add(CreateStatCard("🎯 Regularność", ref lblRegularity, yPos));
            yPos += 85;
            this.panelStats.Controls.Add(CreateStatCard("🏆 Najlepszy Tydzień", ref lblBestWeek, yPos));
            yPos += 85;
            this.panelStats.Controls.Add(CreateStatCard("⚠️ Najsłabszy Tydzień", ref lblWorstWeek, yPos));
        }

        private Panel CreateStatCard(string title, ref Label valueLabel, int yPos)
        {
            var card = new Panel
            {
                Width = 280,
                Height = 75,
                Location = new Point(15, yPos),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = GetRoundedRect(card.ClientRectangle, 8))
                using (var brush = new SolidBrush(Color.White))
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(15, 12),
                AutoSize = true
            };

            valueLabel = new Label
            {
                Text = "Wczytywanie...",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = primaryColor,
                Location = new Point(15, 35),
                AutoSize = true
            };

            card.Controls.AddRange(new Control[] { lblTitle, valueLabel });
            return card;
        }

        private void ConfigureChart()
        {
            this.chartTrend.Dock = DockStyle.Fill;
            this.chartTrend.BackColor = Color.White;
            this.chartTrend.BorderlineColor = Color.FromArgb(220, 220, 220);
            this.chartTrend.BorderlineDashStyle = ChartDashStyle.Solid;
            this.chartTrend.BorderlineWidth = 1;
            this.chartTrend.AntiAliasing = AntiAliasingStyles.All;
            this.chartTrend.TextAntiAliasingQuality = TextAntiAliasingQuality.High;

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.White,
                BorderWidth = 0
            };

            // Subtelniejsza siatka
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(245, 245, 245);
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            chartArea.AxisY.MajorGrid.LineWidth = 1;

            // Lepsze fonty
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 9f);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 9f);
            chartArea.AxisX.LabelStyle.ForeColor = Color.FromArgb(100, 100, 100);
            chartArea.AxisY.LabelStyle.ForeColor = Color.FromArgb(100, 100, 100);

            // Tytuły osi
            chartArea.AxisX.Title = "Tydzień";
            chartArea.AxisY.Title = "Ilość";
            chartArea.AxisX.TitleFont = new Font("Segoe UI Semibold", 10f);
            chartArea.AxisY.TitleFont = new Font("Segoe UI Semibold", 10f);
            chartArea.AxisX.TitleForeColor = primaryColor;
            chartArea.AxisY.TitleForeColor = primaryColor;

            // Osie
            chartArea.AxisX.LineColor = Color.FromArgb(200, 200, 200);
            chartArea.AxisY.LineColor = Color.FromArgb(200, 200, 200);
            chartArea.AxisX.LineWidth = 2;
            chartArea.AxisY.LineWidth = 2;

            // Margines
            chartArea.InnerPlotPosition.Auto = true;
            chartArea.Position.Auto = true;

            this.chartTrend.ChartAreas.Add(chartArea);

            var legend = new Legend("Legend")
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Center,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderColor = Color.FromArgb(220, 220, 220),
                BorderWidth = 1,
                LegendStyle = LegendStyle.Row
            };
            this.chartTrend.Legends.Add(legend);

            var title = new Title
            {
                Text = "📈 Trend Sprzedaży w Czasie",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = primaryColor,
                Docking = Docking.Top,
                Alignment = ContentAlignment.MiddleLeft
            };
            this.chartTrend.Titles.Add(title);

            // Przycisk maksymalizacji wykresu
            var btnMaximize = new Button
            {
                Text = "⛶",
                Size = new Size(40, 40),
                Location = new Point(10, 50),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMaximize.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            btnMaximize.FlatAppearance.BorderSize = 2;
            btnMaximize.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            btnMaximize.Click += (s, e) =>
            {
                var viewer = new ChartViewerForm(chartTrend, "📈 Trend Sprzedaży - Pełny Widok");
                viewer.ShowDialog(this);
            };

            // Przycisk pomocy dla wykresu
            var btnChartHelp = new Button
            {
                Text = "❓",
                Size = new Size(40, 40),
                Location = new Point(60, 50),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnChartHelp.FlatAppearance.BorderSize = 0;
            btnChartHelp.Click += (s, e) => ShowChartHelp();

            var tooltip = new ToolTip();
            tooltip.SetToolTip(btnMaximize, "Pokaż wykres na pełnym ekranie");
            tooltip.SetToolTip(btnChartHelp, "Pomoc dotycząca wykresu");

            this.chartTrend.Controls.Add(btnMaximize);
            this.chartTrend.Controls.Add(btnChartHelp);
        }

        private void ConfigureFooter()
        {
            this.panelFooter.Dock = DockStyle.Bottom;
            this.panelFooter.Height = 65;
            this.panelFooter.BackColor = Color.White;
            this.panelFooter.Padding = new Padding(20, 12, 20, 12);

            this.panelFooter.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, panelFooter.Width, 0);
                }
            };

            this.btnExport.Text = "📥 Eksportuj";
            this.btnExport.Size = new Size(140, 40);
            this.btnExport.Location = new Point(20, 12);
            this.btnExport.FlatStyle = FlatStyle.Flat;
            this.btnExport.BackColor = successColor;
            this.btnExport.ForeColor = Color.White;
            this.btnExport.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            this.btnExport.Cursor = Cursors.Hand;
            this.btnExport.FlatAppearance.BorderSize = 0;
            this.btnExport.Click += BtnExport_Click;

            this.btnShowChart.Text = "📊 Ukryj/Pokaż Wykres";
            this.btnShowChart.Size = new Size(180, 40);
            this.btnShowChart.Location = new Point(170, 12);
            this.btnShowChart.FlatStyle = FlatStyle.Flat;
            this.btnShowChart.BackColor = accentColor;
            this.btnShowChart.ForeColor = Color.White;
            this.btnShowChart.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            this.btnShowChart.Cursor = Cursors.Hand;
            this.btnShowChart.FlatAppearance.BorderSize = 0;
            this.btnShowChart.Click += (s, e) =>
            {
                splitContainer.Panel2Collapsed = !splitContainer.Panel2Collapsed;
            };

            this.btnClose.Text = "❌ Zamknij";
            this.btnClose.Size = new Size(120, 40);
            this.btnClose.FlatStyle = FlatStyle.Flat;
            this.btnClose.BackColor = dangerColor;
            this.btnClose.ForeColor = Color.White;
            this.btnClose.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            this.btnClose.Cursor = Cursors.Hand;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.DialogResult = DialogResult.Cancel;
            this.btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            this.panelFooter.SizeChanged += (s, e) =>
            {
                btnClose.Location = new Point(panelFooter.Width - 140, 12);
            };

            this.panelFooter.Controls.AddRange(new Control[] { btnExport, btnShowChart, btnClose });
        }

        private void ConfigureForm()
        {
            // Dodatkowa konfiguracja jeśli potrzebna
        }

        private void LoadData()
        {
            string query = @"
            DECLARE @TowarID INT = @pTowarID;
            
            WITH DaneZrodlowe AS (
                SELECT 
                    DP.kod AS NazwaTowaru,
                    DP.idtw AS TowarID,
                    DP.ilosc,
                    DP.cena,
                    DP.cena * DP.ilosc AS Wartosc,
                    DK.data,
                    DATEDIFF(week, DK.data, GETDATE()) AS TydzienWstecz,
                    DATENAME(weekday, DK.data) AS DzienTygodnia,
                    DK.id AS DokumentID
                FROM [HANDEL].[HM].[DP] DP 
                INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                WHERE DK.khid = @KontrahentID 
                AND DATEDIFF(week, DK.data, GETDATE()) < 10 
                AND (@TowarID IS NULL OR DP.idtw = @TowarID)
            )
            SELECT 
                DZ.NazwaTowaru,
                DZ.TowarID,
                SUM(CASE WHEN TydzienWstecz = 9 THEN ilosc ELSE 0 END) AS Tydzien_9_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 9 THEN cena ELSE NULL END) AS Tydzien_9_Cena,
                SUM(CASE WHEN TydzienWstecz = 8 THEN ilosc ELSE 0 END) AS Tydzien_8_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 8 THEN cena ELSE NULL END) AS Tydzien_8_Cena,
                SUM(CASE WHEN TydzienWstecz = 7 THEN ilosc ELSE 0 END) AS Tydzien_7_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 7 THEN cena ELSE NULL END) AS Tydzien_7_Cena,
                SUM(CASE WHEN TydzienWstecz = 6 THEN ilosc ELSE 0 END) AS Tydzien_6_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 6 THEN cena ELSE NULL END) AS Tydzien_6_Cena,
                SUM(CASE WHEN TydzienWstecz = 5 THEN ilosc ELSE 0 END) AS Tydzien_5_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 5 THEN cena ELSE NULL END) AS Tydzien_5_Cena,
                SUM(CASE WHEN TydzienWstecz = 4 THEN ilosc ELSE 0 END) AS Tydzien_4_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 4 THEN cena ELSE NULL END) AS Tydzien_4_Cena,
                SUM(CASE WHEN TydzienWstecz = 3 THEN ilosc ELSE 0 END) AS Tydzien_3_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 3 THEN cena ELSE NULL END) AS Tydzien_3_Cena,
                SUM(CASE WHEN TydzienWstecz = 2 THEN ilosc ELSE 0 END) AS Tydzien_2_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 2 THEN cena ELSE NULL END) AS Tydzien_2_Cena,
                SUM(CASE WHEN TydzienWstecz = 1 THEN ilosc ELSE 0 END) AS Tydzien_1_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 1 THEN cena ELSE NULL END) AS Tydzien_1_Cena,
                SUM(CASE WHEN TydzienWstecz = 0 THEN ilosc ELSE 0 END) AS Tydzien_0_Ilosc,
                AVG(CASE WHEN TydzienWstecz = 0 THEN cena ELSE NULL END) AS Tydzien_0_Cena
            FROM DaneZrodlowe DZ
            GROUP BY DZ.NazwaTowaru, DZ.TowarID
            HAVING SUM(ilosc) > 0 
            ORDER BY SUM(ilosc) DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@KontrahentID", kontrahentId);
                    cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value);

                    var adapter = new SqlDataAdapter(cmd);
                    mainDataTable = new DataTable();
                    adapter.Fill(mainDataTable);

                    // Dodaj wiersz podsumowania
                    AddSummaryRow(mainDataTable);

                    // Konfiguruj kolumny PRZED bindowaniem
                    ConfigureColumns();

                    // Teraz zbinduj dane
                    dataGridViewAnaliza.DataSource = mainDataTable;

                    // Oblicz statystyki i zaktualizuj wykres
                    CalculateStatistics(mainDataTable);
                    UpdateChart(mainDataTable);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureColumns()
        {
            dataGridViewAnaliza.AutoGenerateColumns = false;
            dataGridViewAnaliza.Columns.Clear();

            // KROK 1: Kolumna ID (ukryta) - musi być pierwsza
            var colId = new DataGridViewTextBoxColumn
            {
                Name = "TowarID",
                DataPropertyName = "TowarID",
                Visible = false
            };
            dataGridViewAnaliza.Columns.Add(colId);

            // KROK 2: Nazwa towaru - Frozen ustawimy PÓŹNIEJ, po bindowaniu!
            var colNazwa = new DataGridViewTextBoxColumn
            {
                Name = "NazwaTowaru",
                DataPropertyName = "NazwaTowaru",
                HeaderText = "📦 Towar",
                Width = 180,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(245, 246, 247),
                    Font = new Font("Segoe UI Semibold", 9f),
                    Padding = new Padding(10, 2, 5, 2)
                }
            };
            dataGridViewAnaliza.Columns.Add(colNazwa);

            // KROK 3: Kolumny tygodni (ilość) - widoczne
            for (int i = 9; i >= 0; i--)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = $"Tydzien_{i}_Ilosc",
                    DataPropertyName = $"Tydzien_{i}_Ilosc",
                    HeaderText = $"T-{i}\n{GetWeekDates(i)}",
                    Width = 95,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Format = "N2",
                        Alignment = DataGridViewContentAlignment.MiddleCenter
                    }
                };

                if (i == 0)
                    col.DefaultCellStyle.BackColor = Color.FromArgb(212, 239, 223);
                else if (i == 1)
                    col.DefaultCellStyle.BackColor = Color.FromArgb(214, 234, 248);

                col.Tag = i;
                dataGridViewAnaliza.Columns.Add(col);
            }

            // KROK 4: Kolumny analityczne
            var colSuma = new DataGridViewTextBoxColumn
            {
                Name = "Suma",
                HeaderText = "∑ SUMA",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(255, 248, 225),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                }
            };
            dataGridViewAnaliza.Columns.Add(colSuma);

            var colSrednia = new DataGridViewTextBoxColumn
            {
                Name = "Srednia",
                HeaderText = "Ø Średnia",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            dataGridViewAnaliza.Columns.Add(colSrednia);

            var colTrend = new DataGridViewTextBoxColumn
            {
                Name = "Trend",
                HeaderText = "📈 Trend",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                }
            };
            dataGridViewAnaliza.Columns.Add(colTrend);

            var colCV = new DataGridViewTextBoxColumn
            {
                Name = "CV",
                HeaderText = "📊 Zmienność",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "P1",
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            dataGridViewAnaliza.Columns.Add(colCV);
        }

        private string GetWeekDates(int weeksBack)
        {
            DateTime today = DateTime.Today;
            int daysOffset = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysOffset < 0) daysOffset += 7;

            DateTime weekStart = today.AddDays(-daysOffset - (weeksBack * 7));
            DateTime weekEnd = weekStart.AddDays(6);

            return $"{weekStart:dd.MM}";
        }

        private void DataGridViewAnaliza_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // Ustaw Frozen DOPIERO teraz, po zbindowaniu danych!
            if (dataGridViewAnaliza.Columns.Contains("NazwaTowaru"))
            {
                dataGridViewAnaliza.Columns["NazwaTowaru"].Frozen = true;
            }

            foreach (DataGridViewRow row in dataGridViewAnaliza.Rows)
            {
                if (row.IsNewRow || row.Cells["NazwaTowaru"].Value?.ToString() == "📊 PODSUMOWANIE")
                    continue;

                var values = new List<decimal>();
                decimal suma = 0;

                for (int i = 0; i <= 9; i++)
                {
                    var cellValue = row.Cells[$"Tydzien_{i}_Ilosc"].Value;
                    if (cellValue != null && cellValue != DBNull.Value)
                    {
                        if (decimal.TryParse(cellValue.ToString(), out decimal val))
                        {
                            suma += val;
                            values.Add(val);
                        }
                    }
                    else
                    {
                        values.Add(0);
                    }
                }

                row.Cells["Suma"].Value = suma;

                if (values.Count > 0)
                {
                    decimal srednia = values.Average();
                    row.Cells["Srednia"].Value = srednia;

                    var recentValues = values.Take(3).ToList();
                    var oldValues = values.Skip(Math.Max(0, values.Count - 3)).Take(3).ToList();

                    decimal recent = recentValues.Count > 0 ? recentValues.Average() : 0;
                    decimal old = oldValues.Count > 0 ? oldValues.Average() : 0;

                    string trend = "→ Stabilny";
                    if (old > 0)
                    {
                        if (recent > old * 1.1m) trend = "↗️ Wzrost";
                        else if (recent < old * 0.9m) trend = "↘️ Spadek";
                    }

                    row.Cells["Trend"].Value = trend;

                    if (srednia > 0 && values.Count > 1)
                    {
                        decimal variance = values.Sum(v => (v - srednia) * (v - srednia)) / values.Count;
                        decimal stdDev = (decimal)Math.Sqrt((double)variance);
                        decimal cv = stdDev / srednia;
                        row.Cells["CV"].Value = cv;
                    }
                }
            }
        }

        private void AddSummaryRow(DataTable dt)
        {
            if (dt.Rows.Count > 0)
            {
                var summaryRow = dt.NewRow();
                summaryRow["NazwaTowaru"] = "📊 PODSUMOWANIE";

                for (int i = 0; i <= 9; i++)
                {
                    decimal sumaIlosc = 0;
                    decimal sumaCena = 0;
                    int count = 0;

                    foreach (DataRow row in dt.Rows)
                    {
                        if (row[$"Tydzien_{i}_Ilosc"] != DBNull.Value)
                        {
                            sumaIlosc += Convert.ToDecimal(row[$"Tydzien_{i}_Ilosc"]);
                        }
                        if (row[$"Tydzien_{i}_Cena"] != DBNull.Value)
                        {
                            sumaCena += Convert.ToDecimal(row[$"Tydzien_{i}_Cena"]);
                            count++;
                        }
                    }

                    summaryRow[$"Tydzien_{i}_Ilosc"] = sumaIlosc;
                    summaryRow[$"Tydzien_{i}_Cena"] = count > 0 ? sumaCena / count : (object)DBNull.Value;
                }

                dt.Rows.Add(summaryRow);
            }
        }

        private void CalculateStatistics(DataTable dt)
        {
            if (dt.Rows.Count == 0)
            {
                lblTotalSales.Text = "0.00";
                lblAvgWeekly.Text = "0.00";
                lblTrend.Text = "0.0%";
                lblRegularity.Text = "Brak danych";
                lblBestWeek.Text = "Brak";
                lblWorstWeek.Text = "Brak";
                return;
            }

            var weekTotals = new List<decimal>();
            for (int i = 0; i <= 9; i++)
            {
                decimal total = 0;
                foreach (DataRow row in dt.Rows)
                {
                    if (row["NazwaTowaru"].ToString() != "📊 PODSUMOWANIE" &&
                        row[$"Tydzien_{i}_Ilosc"] != DBNull.Value)
                    {
                        total += Convert.ToDecimal(row[$"Tydzien_{i}_Ilosc"]);
                    }
                }
                weekTotals.Add(total);
            }

            decimal totalSales = weekTotals.Sum();
            decimal avgWeekly = weekTotals.Count > 0 ? weekTotals.Average() : 0;

            lblTotalSales.Text = $"{totalSales:N2}";
            lblAvgWeekly.Text = $"{avgWeekly:N2}";

            var recentWeeks = weekTotals.Take(3).ToList();
            var oldWeeks = weekTotals.Skip(7).Take(3).ToList();

            decimal recentAvg = recentWeeks.Count > 0 ? recentWeeks.Average() : 0;
            decimal oldAvg = oldWeeks.Count > 0 ? oldWeeks.Average() : 0;

            decimal trendPercent = oldAvg > 0 ? ((recentAvg - oldAvg) / oldAvg) * 100 : 0;

            lblTrend.Text = $"{(trendPercent >= 0 ? "+" : "")}{trendPercent:N1}%";
            lblTrend.ForeColor = trendPercent >= 0 ? successColor : dangerColor;

            if (avgWeekly > 0 && weekTotals.Count > 1)
            {
                decimal variance = weekTotals.Sum(w => (w - avgWeekly) * (w - avgWeekly)) / weekTotals.Count;
                decimal stdDev = (decimal)Math.Sqrt((double)variance);
                decimal cv = (stdDev / avgWeekly) * 100;

                string regularity = cv < 20 ? "Wysoka ✓" : cv < 40 ? "Średnia" : "Niska";
                lblRegularity.Text = regularity;
                lblRegularity.ForeColor = cv < 20 ? successColor : cv < 40 ? warningColor : dangerColor;
            }
            else
            {
                lblRegularity.Text = "N/A";
            }

            if (weekTotals.Count > 0)
            {
                int bestWeekIdx = weekTotals.IndexOf(weekTotals.Max());

                // WYKLUCZAMY BIEŻĄCY TYDZIEŃ (T-0) z najsłabszego
                var weeksExcludingCurrent = weekTotals.Skip(1).ToList();
                var positiveWeeks = weeksExcludingCurrent.Where(w => w > 0).ToList();

                if (positiveWeeks.Count > 0)
                {
                    decimal minValue = positiveWeeks.Min();
                    int worstWeekIdx = weekTotals.IndexOf(minValue);
                    lblWorstWeek.Text = $"T-{worstWeekIdx}: {weekTotals[worstWeekIdx]:N2}";
                }
                else
                {
                    lblWorstWeek.Text = "Brak danych";
                }

                lblBestWeek.Text = $"T-{bestWeekIdx}: {weekTotals[bestWeekIdx]:N2}";
            }
        }

        private void UpdateChart(DataTable dt)
        {
            chartTrend.Series.Clear();

            var seriesTotal = new Series("Suma Tygodniowa")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 4,
                Color = accentColor,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 10,
                MarkerColor = Color.White,
                MarkerBorderColor = accentColor,
                MarkerBorderWidth = 3,
                ShadowOffset = 2,
                ShadowColor = Color.FromArgb(50, 0, 0, 0)
            };

            var seriesMA = new Series("Średnia Krocząca (3)")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3,
                BorderDashStyle = ChartDashStyle.Dash,
                Color = successColor,
                MarkerStyle = MarkerStyle.None
            };

            var weekValues = new List<decimal>();
            var weekLabels = new List<string>();

            // ZAWSZE 10 TYGODNI - OD T-9 DO T-0
            for (int i = 9; i >= 0; i--)
            {
                decimal total = 0;
                foreach (DataRow row in dt.Rows)
                {
                    if (row["NazwaTowaru"].ToString() != "📊 PODSUMOWANIE" &&
                        row[$"Tydzien_{i}_Ilosc"] != DBNull.Value)
                    {
                        total += Convert.ToDecimal(row[$"Tydzien_{i}_Ilosc"]);
                    }
                }

                // Oblicz początek tygodnia (poniedziałek)
                DateTime today = DateTime.Today;
                int daysOffset = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
                if (daysOffset < 0) daysOffset += 7;
                DateTime weekStart = today.AddDays(-daysOffset - (i * 7));

                string label = $"Pn\n{weekStart:dd.MM}";

                var point = seriesTotal.Points.AddXY(label, total);
                seriesTotal.Points[point].ToolTip = $"Tydzień {weekStart:dd.MM.yyyy}\nSprzedaż: {total:N2} szt.";

                weekValues.Add(total);
                weekLabels.Add(label);
            }

            // Oblicz średnią kroczącą - dodaj punkty dla WSZYSTKICH tygodni
            for (int i = 0; i < weekValues.Count; i++)
            {
                if (i >= 2)
                {
                    decimal ma = (weekValues[i] + weekValues[i - 1] + weekValues[i - 2]) / 3;
                    var point = seriesMA.Points.AddXY(weekLabels[i], ma);
                    seriesMA.Points[point].ToolTip = $"Średnia krocząca (3): {ma:N2} szt.";
                }
                else
                {
                    // Dodaj przezroczysty punkt aby zachować etykiety
                    var point = seriesMA.Points.AddXY(weekLabels[i], 0);
                    seriesMA.Points[point].Color = Color.Transparent;
                    seriesMA.Points[point].MarkerStyle = MarkerStyle.None;
                    seriesMA.Points[point].BorderWidth = 0;
                }
            }

            chartTrend.Series.Add(seriesTotal);
            chartTrend.Series.Add(seriesMA);

            // WYMUSZENIE WSZYSTKICH ETYKIET
            if (chartTrend.ChartAreas.Count > 0)
            {
                chartTrend.ChartAreas[0].AxisX.Interval = 1;
                chartTrend.ChartAreas[0].AxisX.LabelStyle.Angle = -45;
                chartTrend.ChartAreas[0].AxisX.IsMarginVisible = false;

                // KLUCZOWE: ustaw minimum i maximum
                chartTrend.ChartAreas[0].AxisX.Minimum = -0.5;
                chartTrend.ChartAreas[0].AxisX.Maximum = weekLabels.Count - 0.5;

                // Formatowanie osi Y
                chartTrend.ChartAreas[0].AxisY.LabelStyle.Format = "N0";
            }
        }

        private void DataGridViewAnaliza_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridViewAnaliza.Rows[e.RowIndex];

            if (row.Cells["NazwaTowaru"].Value?.ToString() == "📊 PODSUMOWANIE")
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 248, 225);
                e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                e.CellStyle.ForeColor = primaryColor;
                return;
            }

            var columnName = dataGridViewAnaliza.Columns[e.ColumnIndex].Name;

            if (columnName == "Trend")
            {
                string trend = e.Value?.ToString() ?? "";
                if (trend.Contains("Wzrost"))
                    e.CellStyle.ForeColor = successColor;
                else if (trend.Contains("Spadek"))
                    e.CellStyle.ForeColor = dangerColor;
                else
                    e.CellStyle.ForeColor = Color.Gray;
            }
            else if (columnName == "CV" && e.Value != null)
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal cv))
                {
                    if (cv < 0.2m)
                        e.CellStyle.ForeColor = successColor;
                    else if (cv < 0.4m)
                        e.CellStyle.ForeColor = warningColor;
                    else
                        e.CellStyle.ForeColor = dangerColor;
                }
            }
            else if (columnName.Contains("Tydzien") && columnName.Contains("Ilosc"))
            {
                if (e.Value != null && e.Value != DBNull.Value)
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal value))
                    {
                        if (value == 0)
                        {
                            e.CellStyle.ForeColor = Color.LightGray;
                            e.Value = "-";
                            e.FormattingApplied = true;
                        }
                        else if (value > 100)
                        {
                            e.CellStyle.ForeColor = accentColor;
                            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                        }
                    }
                }
            }
        }

        private void DataGridViewAnaliza_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var row = dataGridViewAnaliza.Rows[e.RowIndex];
            var columnName = dataGridViewAnaliza.Columns[e.ColumnIndex].Name;

            if (!columnName.Contains("Tydzien") || !columnName.Contains("Ilosc"))
                return;

            var parts = columnName.Split('_');
            if (parts.Length < 2 || !int.TryParse(parts[1], out int weekNumber))
                return;

            var nazwaTowaru = row.Cells["NazwaTowaru"].Value?.ToString();
            var towarIdValue = row.Cells["TowarID"].Value;

            if (string.IsNullOrEmpty(nazwaTowaru) || nazwaTowaru == "📊 PODSUMOWANIE")
                return;

            int? selectedTowarId = null;
            if (towarIdValue != null && towarIdValue != DBNull.Value)
            {
                selectedTowarId = Convert.ToInt32(towarIdValue);
            }

            var detailsForm = new WeekDetailsForm(
                connectionString,
                kontrahentId,
                selectedTowarId,
                nazwaKontrahenta,
                nazwaTowaru,
                weekNumber);

            detailsForm.ShowDialog(this);
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.FileName = $"AnalizaTygodniowa_{nazwaKontrahenta}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        using (var writer = new System.IO.StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                        {
                            var headers = new System.Text.StringBuilder();
                            foreach (DataGridViewColumn col in dataGridViewAnaliza.Columns)
                            {
                                if (col.Visible)
                                {
                                    headers.Append(col.HeaderText.Replace("\n", " ").Replace(";", ","));
                                    headers.Append(";");
                                }
                            }
                            writer.WriteLine(headers.ToString().TrimEnd(';'));

                            foreach (DataGridViewRow row in dataGridViewAnaliza.Rows)
                            {
                                if (!row.IsNewRow)
                                {
                                    var line = new System.Text.StringBuilder();
                                    foreach (DataGridViewCell cell in row.Cells)
                                    {
                                        if (dataGridViewAnaliza.Columns[cell.ColumnIndex].Visible)
                                        {
                                            var value = cell.Value?.ToString() ?? "";
                                            line.Append(value.Replace(";", ","));
                                            line.Append(";");
                                        }
                                    }
                                    writer.WriteLine(line.ToString().TrimEnd(';'));
                                }
                            }
                        }

                        MessageBox.Show("Dane zostały wyeksportowane pomyślnie!", "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        // ============================================================================
        // OKNA POMOCY
        // ============================================================================
        private void ShowMainHelp()
        {
            var helpForm = new Form
            {
                Text = "📚 Pomoc - Analiza Tygodniowa",
                Size = new Size(700, 600),
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            var rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 10f),
                Padding = new Padding(15)
            };

            rtb.Text = @"═══════════════════════════════════════════════
  ZAAWANSOWANA ANALIZA TYGODNIOWA - INSTRUKCJA
═══════════════════════════════════════════════

📊 TABELA GŁÓWNA
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Kolumny T-9 do T-0 pokazują ilość sprzedaży w kolejnych tygodniach
  → T-0 = bieżący tydzień
  → T-1 = poprzedni tydzień
  → T-9 = 9 tygodni wstecz

• Kolumny tła:
  → Zielone tło = bieżący tydzień (T-0)
  → Niebieskie tło = poprzedni tydzień (T-1)
  → Szare tło = nazwa towaru (zamrożona kolumna)

• Kolumny analityczne:
  → SUMA = suma ze wszystkich 10 tygodni
  → Średnia = średnia sprzedaż tygodniowa
  → Trend = kierunek zmian (wzrost/spadek/stabilny)
  → Zmienność = jak bardzo sprzedaż się waha

🎯 INTERAKCJE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• DWUKLIKNIĘCIE na komórce tygodnia (np. T-3)
  → Otwiera szczegółową analizę CEN dla tego tygodnia
  → Możesz zobaczyć wszystkie transakcje
  → Porównanie z cenami rynkowymi

• Hover (najechanie myszką) na wiersz
  → Wiersz podświetla się na niebiesko

📈 WYKRES TRENDU
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Niebieska linia = suma sprzedaży w danym tygodniu
• Zielona linia = średnia krocząca (3 tygodnie)
• Pokazuje wyraźny trend wzrostowy lub spadkowy

🎨 KOLORY I OZNACZENIA
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Trend:
  ↗️ Zielony = wzrost sprzedaży
  ↘️ Czerwony = spadek sprzedaży
  → Szary = stabilna sprzedaż

• Zmienność:
  Zielony = niska (regularny klient)
  Żółty = średnia
  Czerwony = wysoka (nieregularny)

💾 EKSPORT
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Przycisk 'Eksportuj' zapisuje dane do CSV
• Możesz otworzyć w Excel/LibreOffice
• Format: średnik jako separator

📊 PRZYCISK 'Ukryj/Pokaż Wykres'
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Ukrywa lub pokazuje dolny panel z wykresem
• Przydatne gdy chcesz więcej miejsca na tabelę

═══════════════════════════════════════════════";

            var btnClose = new Button
            {
                Text = "Zamknij",
                Size = new Size(100, 35),
                Location = new Point(290, 10),
                DialogResult = DialogResult.OK
            };

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 55 };
            panel.Controls.Add(btnClose);

            helpForm.Controls.Add(rtb);
            helpForm.Controls.Add(panel);
            helpForm.ShowDialog(this);
        }

        private void ShowStatsHelp()
        {
            var helpText = @"═══════════════════════════════════════════
  STATYSTYKI - WYJAŚNIENIE
═══════════════════════════════════════════

💰 SUMA SPRZEDAŻY
   Całkowita ilość sprzedana w ciągu 
   ostatnich 10 tygodni

📊 ŚREDNIA TYGODNIOWA
   Średnia ilość sprzedawana każdego tygodnia

📈 TREND
   Pokazuje czy sprzedaż rośnie czy spada
   • Porównuje ostatnie 3 tygodnie z najstarszymi 3
   • + oznacza wzrost
   • - oznacza spadek

🎯 REGULARNOŚĆ
   Jak stabilna jest sprzedaż
   • Wysoka = regularny, przewidywalny klient
   • Średnia = wahania sprzedaży
   • Niska = bardzo nieregularne zakupy

🏆 NAJLEPSZY TYDZIEŃ
   Tydzień z największą sprzedażą

⚠️ NAJSŁABSZY TYDZIEŃ
   Tydzień z najmniejszą sprzedażą
   (nie uwzględnia bieżącego tygodnia T-0)

═══════════════════════════════════════════";

            MessageBox.Show(helpText, "Pomoc - Statystyki", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowChartHelp()
        {
            var helpText = @"═══════════════════════════════════════════
  WYKRES - INSTRUKCJA
═══════════════════════════════════════════

📊 CO POKAZUJE WYKRES?

• Oś X (pozioma):
  → Poniedziałki kolejnych tygodni
  → Od T-9 (9 tygodni temu) do T-0 (bieżący)

• Oś Y (pionowa):
  → Suma sprzedaży w danym tygodniu

🎨 LINIE NA WYKRESIE

• Niebieska linia (Suma Tygodniowa):
  → Rzeczywista sprzedaż w każdym tygodniu
  → Białe kółka = punkty danych
  → Najedź na punkt aby zobaczyć szczegóły

• Zielona przerywana (Średnia Krocząca):
  → Wygładzona krzywa trendu
  → Obliczana z 3 kolejnych tygodni
  → Pokazuje ogólny kierunek zmian

⛶ PRZYCISK PEŁNEGO EKRANU

• Otwiera wykres w dużym oknie
• Możesz zapisać wykres jako obraz (PNG/JPG)

💡 WSKAZÓWKI

• Jeśli linia idzie w górę = wzrost sprzedaży
• Jeśli linia idzie w dół = spadek
• Pozioma linia = stabilna sprzedaż

═══════════════════════════════════════════";

            MessageBox.Show(helpText, "Pomoc - Wykres", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ============================================================================
    // FORMULARZ SZCZEGÓŁÓW TYGODNIA - ANALIZA CEN
    // ============================================================================
    public class WeekDetailsForm : Form
    {
        private string connectionString;
        private int kontrahentId;
        private int? towarId;
        private string nazwaKontrahenta;
        private string nazwaTowaru;
        private int weekNumber;

        private DataGridView dataGridViewDetails;
        private Chart chartPrices;
        private Panel panelHeader;
        private Panel panelStats;
        private Panel panelControls;
        private Button btnClose;
        private Button btnHelp;
        private CheckBox chkShowAllItems;
        private ComboBox cmbSortOrder;
        private Label lblItemCount;

        private readonly Color primaryColor = Color.FromArgb(44, 62, 80);
        private readonly Color accentColor = Color.FromArgb(52, 152, 219);
        private readonly Color successColor = Color.FromArgb(39, 174, 96);
        private readonly Color dangerColor = Color.FromArgb(231, 76, 60);
        private readonly Color warningColor = Color.FromArgb(243, 156, 18);
        private readonly Color lightBg = Color.FromArgb(236, 240, 241);

        public WeekDetailsForm(string connString, int khId, int? twId, string khNazwa, string twNazwa, int week)
        {
            connectionString = connString;
            kontrahentId = khId;
            towarId = twId;
            nazwaKontrahenta = khNazwa;
            nazwaTowaru = twNazwa;
            weekNumber = week;

            InitializeComponent();
            LoadDetails();
        }

        private void InitializeComponent()
        {
            this.dataGridViewDetails = new DataGridView();
            this.chartPrices = new Chart();
            this.panelHeader = new Panel();
            this.panelStats = new Panel();
            this.panelControls = new Panel();
            this.btnClose = new Button();
            this.btnHelp = new Button();
            this.chkShowAllItems = new CheckBox();
            this.cmbSortOrder = new ComboBox();
            this.lblItemCount = new Label();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chartPrices)).BeginInit();

            this.Text = "📋 Szczegóły Tygodnia - Analiza Cen";
            this.Size = new Size(1500, 900);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = lightBg;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1200, 700);

            // MAKSYMALIZACJA AUTOMATYCZNA
            this.WindowState = FormWindowState.Maximized;

            // Panel Header - LEPSZY WYGLĄD
            this.panelHeader.Dock = DockStyle.Top;
            this.panelHeader.Height = 140;
            this.panelHeader.BackColor = primaryColor;
            this.panelHeader.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(panelHeader.ClientRectangle,
                    primaryColor, Color.FromArgb(52, 73, 94), 90f))
                {
                    e.Graphics.FillRectangle(brush, panelHeader.ClientRectangle);
                }

                using (var pen = new Pen(Color.FromArgb(100, 255, 255, 255), 2))
                {
                    e.Graphics.DrawLine(pen, 0, panelHeader.Height - 1, panelHeader.Width, panelHeader.Height - 1);
                }
            };

            var lblTitle = new Label
            {
                Text = $"📋 SZCZEGÓŁY TYGODNIA T-{weekNumber} - ANALIZA CEN",
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(30, 20),
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = $"👤 Kontrahent: {nazwaKontrahenta}  •  📦 Towar: {nazwaTowaru}",
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(230, 240, 250),
                Location = new Point(30, 60),
                AutoSize = true
            };

            var lblPeriod = new Label
            {
                Text = $"📅 Okres: {GetWeekDateRange(weekNumber)}",
                Font = new Font("Segoe UI", 11f, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 210, 220),
                Location = new Point(30, 95),
                AutoSize = true
            };

            // Przycisk pomocy w nagłówku
            var btnHeaderHelp = new Button
            {
                Text = "❓ POMOC",
                Size = new Size(110, 45),
                BackColor = Color.FromArgb(100, 255, 255, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnHeaderHelp.FlatAppearance.BorderSize = 2;
            btnHeaderHelp.FlatAppearance.BorderColor = Color.White;
            btnHeaderHelp.Click += (s, e) => ShowDetailsHelp();

            this.panelHeader.SizeChanged += (s, e) =>
            {
                btnHeaderHelp.Location = new Point(panelHeader.Width - 150, 20);
            };

            this.panelHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle, lblPeriod, btnHeaderHelp });

            this.panelControls.Dock = DockStyle.Top;
            this.panelControls.Height = 70;
            this.panelControls.BackColor = Color.White;
            this.panelControls.Padding = new Padding(20, 10, 20, 10);

            this.chkShowAllItems.Text = "🔍 Pokaż wszystkie towary z dokumentów (pełny koszyk)";
            this.chkShowAllItems.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            this.chkShowAllItems.ForeColor = primaryColor;
            this.chkShowAllItems.Location = new Point(20, 10);
            this.chkShowAllItems.AutoSize = true;
            this.chkShowAllItems.Cursor = Cursors.Hand;
            this.chkShowAllItems.CheckedChanged += (s, e) => LoadDetails();

            var lblSort = new Label
            {
                Text = "📊 Sortuj:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = primaryColor,
                Location = new Point(20, 40),
                AutoSize = true
            };

            this.cmbSortOrder.Location = new Point(95, 37);
            this.cmbSortOrder.Width = 200;
            this.cmbSortOrder.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbSortOrder.Font = new Font("Segoe UI", 9f);
            this.cmbSortOrder.Items.AddRange(new object[]
            {
                "Data (najnowsze)",
                "Data (najstarsze)",
                "Cena (najwyższa)",
                "Cena (najniższa)",
                "Ilość (największa)",
                "Ilość (najmniejsza)",
                "Wartość (największa)",
                "Wartość (najmniejsza)",
                "Towar (A-Z)"
            });
            this.cmbSortOrder.SelectedIndex = 0;
            this.cmbSortOrder.SelectedIndexChanged += (s, e) => ApplySorting();

            this.lblItemCount.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
            this.lblItemCount.ForeColor = Color.Gray;
            this.lblItemCount.Location = new Point(310, 40);
            this.lblItemCount.AutoSize = true;

            this.panelControls.Controls.AddRange(new Control[] { chkShowAllItems, lblSort, cmbSortOrder, lblItemCount });

            this.panelStats.Dock = DockStyle.Top;
            this.panelStats.Height = 90;
            this.panelStats.BackColor = Color.White;
            this.panelStats.Padding = new Padding(20, 10, 20, 10);

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 400
            };

            ConfigureDataGridView();
            splitContainer.Panel1.Controls.Add(this.dataGridViewDetails);

            ConfigureChart();
            splitContainer.Panel2.Controls.Add(this.chartPrices);

            var panelFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            this.btnClose.Text = "❌ Zamknij";
            this.btnClose.Size = new Size(120, 40);
            this.btnClose.Location = new Point(20, 10);
            this.btnClose.FlatStyle = FlatStyle.Flat;
            this.btnClose.BackColor = dangerColor;
            this.btnClose.ForeColor = Color.White;
            this.btnClose.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            this.btnClose.Cursor = Cursors.Hand;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.Click += (s, e) => this.Close();

            panelFooter.Controls.Add(this.btnClose);

            this.Controls.Add(splitContainer);
            this.Controls.Add(panelStats);
            this.Controls.Add(panelControls);
            this.Controls.Add(panelFooter);
            this.Controls.Add(this.panelHeader);

            ((System.ComponentModel.ISupportInitialize)(this.chartPrices)).EndInit();
            this.ResumeLayout(false);
        }

        private void ConfigureDataGridView()
        {
            this.dataGridViewDetails.Dock = DockStyle.Fill;
            this.dataGridViewDetails.BackgroundColor = Color.White;
            this.dataGridViewDetails.BorderStyle = BorderStyle.None;
            this.dataGridViewDetails.AllowUserToAddRows = false;
            this.dataGridViewDetails.AllowUserToDeleteRows = false;
            this.dataGridViewDetails.ReadOnly = true;
            this.dataGridViewDetails.RowHeadersVisible = false;
            this.dataGridViewDetails.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewDetails.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            this.dataGridViewDetails.GridColor = Color.FromArgb(220, 220, 220);
            this.dataGridViewDetails.AllowUserToResizeRows = false;

            this.dataGridViewDetails.ColumnHeadersDefaultCellStyle.BackColor = primaryColor;
            this.dataGridViewDetails.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            this.dataGridViewDetails.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            this.dataGridViewDetails.ColumnHeadersHeight = 45;
            this.dataGridViewDetails.EnableHeadersVisualStyles = false;

            this.dataGridViewDetails.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            this.dataGridViewDetails.RowTemplate.Height = 35;

            this.dataGridViewDetails.CellFormatting += DataGridViewDetails_CellFormatting;
        }

        private void ConfigureChart()
        {
            this.chartPrices.Dock = DockStyle.Fill;
            this.chartPrices.BackColor = Color.White;
            this.chartPrices.AntiAliasing = AntiAliasingStyles.All;
            this.chartPrices.TextAntiAliasingQuality = TextAntiAliasingQuality.High;

            var chartArea = new ChartArea("PriceArea")
            {
                BackColor = Color.White
            };

            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(245, 245, 245);
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            chartArea.AxisY.MajorGrid.LineWidth = 1;

            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 9f);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 9f);
            chartArea.AxisX.LabelStyle.ForeColor = Color.FromArgb(100, 100, 100);
            chartArea.AxisY.LabelStyle.ForeColor = Color.FromArgb(100, 100, 100);

            chartArea.AxisY.Title = "Cena (zł)";
            chartArea.AxisY.TitleFont = new Font("Segoe UI Semibold", 10f);
            chartArea.AxisY.TitleForeColor = primaryColor;
            chartArea.AxisX.Title = "Dzień tygodnia";
            chartArea.AxisX.TitleFont = new Font("Segoe UI Semibold", 10f);
            chartArea.AxisX.TitleForeColor = primaryColor;

            chartArea.AxisY.LabelStyle.Format = "C2";

            chartArea.AxisX.LineColor = Color.FromArgb(200, 200, 200);
            chartArea.AxisY.LineColor = Color.FromArgb(200, 200, 200);
            chartArea.AxisX.LineWidth = 2;
            chartArea.AxisY.LineWidth = 2;

            this.chartPrices.ChartAreas.Add(chartArea);

            var legend = new Legend
            {
                Docking = Docking.Top,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderColor = Color.FromArgb(220, 220, 220),
                BorderWidth = 1,
                LegendStyle = LegendStyle.Row
            };
            this.chartPrices.Legends.Add(legend);

            this.chartPrices.Titles.Add(new Title
            {
                Text = "📊 Porównanie Cen w Czasie",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = primaryColor,
                Alignment = ContentAlignment.MiddleLeft
            });

            var btnMaximize = new Button
            {
                Text = "⛶",
                Size = new Size(40, 40),
                Location = new Point(10, 50),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMaximize.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            btnMaximize.FlatAppearance.BorderSize = 2;
            btnMaximize.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            btnMaximize.Click += (s, e) =>
            {
                var viewer = new ChartViewerForm(chartPrices, "📊 Analiza Cen - Pełny Widok");
                viewer.ShowDialog(this);
            };

            var btnChartHelp = new Button
            {
                Text = "❓",
                Size = new Size(40, 40),
                Location = new Point(60, 50),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnChartHelp.FlatAppearance.BorderSize = 0;
            btnChartHelp.Click += (s, e) => ShowPriceChartHelp();

            var tooltip = new ToolTip();
            tooltip.SetToolTip(btnMaximize, "Pokaż wykres na pełnym ekranie");
            tooltip.SetToolTip(btnChartHelp, "Pomoc dotycząca wykresu cen");

            this.chartPrices.Controls.Add(btnMaximize);
            this.chartPrices.Controls.Add(btnChartHelp);
        }

        private void LoadDetails()
        {
            bool showAllItems = chkShowAllItems.Checked;

            string query = showAllItems ? @"
            DECLARE @TowarID INT = @pTowarID;
            DECLARE @WeekNumber INT = @pWeekNumber;
            
            WITH DokumentyZTowarem AS (
                SELECT DISTINCT DK.id
                FROM [HANDEL].[HM].[DP] DP 
                INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                WHERE DK.khid = @KontrahentID 
                AND DATEDIFF(week, DK.data, GETDATE()) = @WeekNumber
                AND (@TowarID IS NULL OR DP.idtw = @TowarID)
            ),
            SrednieRynkowe AS (
                SELECT 
                    DP.kod,
                    AVG(DP.cena) AS SredniaCenaRynkowa,
                    COUNT(*) AS IleTransakcji
                FROM [HANDEL].[HM].[DP] DP 
                INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                WHERE DATEDIFF(week, DK.data, GETDATE()) = @WeekNumber
                GROUP BY DP.kod
            )
            SELECT 
                DK.data AS Data,
                DATENAME(weekday, DK.data) AS DzienTygodnia,
                CAST(DK.id AS VARCHAR(20)) AS NumerDokumentu,
                DP.kod AS NazwaTowaru,
                DP.ilosc AS Ilosc,
                DP.cena AS Cena,
                DP.cena * DP.ilosc AS Wartosc,
                SR.SredniaCenaRynkowa,
                SR.IleTransakcji AS TransakcjiRynkowych,
                CASE 
                    WHEN SR.SredniaCenaRynkowa IS NOT NULL AND SR.SredniaCenaRynkowa > 0 THEN 
                        ((DP.cena - SR.SredniaCenaRynkowa) / SR.SredniaCenaRynkowa) * 100
                    ELSE NULL
                END AS OdchylenieProcentowe,
                CASE 
                    WHEN DP.cena < SR.SredniaCenaRynkowa * 0.9 THEN 'Okazja!'
                    WHEN DP.cena > SR.SredniaCenaRynkowa * 1.1 THEN 'Drogo'
                    ELSE 'OK'
                END AS OcenaCeny,
                CASE WHEN DP.idtw = @TowarID THEN 1 ELSE 0 END AS CzyWybranyTowar,
                DP.idtw AS TowarID
            FROM [HANDEL].[HM].[DP] DP 
            INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
            INNER JOIN DokumentyZTowarem DZT ON DZT.id = DK.id
            LEFT JOIN SrednieRynkowe SR ON SR.kod = DP.kod
            WHERE DK.khid = @KontrahentID"
            : @"
            DECLARE @TowarID INT = @pTowarID;
            DECLARE @WeekNumber INT = @pWeekNumber;
            
            WITH SrednieRynkowe AS (
                SELECT 
                    DP.kod,
                    AVG(DP.cena) AS SredniaCenaRynkowa,
                    COUNT(*) AS IleTransakcji
                FROM [HANDEL].[HM].[DP] DP 
                INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                WHERE DATEDIFF(week, DK.data, GETDATE()) = @WeekNumber
                AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                GROUP BY DP.kod
            )
            SELECT 
                DK.data AS Data,
                DATENAME(weekday, DK.data) AS DzienTygodnia,
                CAST(DK.id AS VARCHAR(20)) AS NumerDokumentu,
                DP.kod AS NazwaTowaru,
                DP.ilosc AS Ilosc,
                DP.cena AS Cena,
                DP.cena * DP.ilosc AS Wartosc,
                SR.SredniaCenaRynkowa,
                SR.IleTransakcji AS TransakcjiRynkowych,
                CASE 
                    WHEN SR.SredniaCenaRynkowa IS NOT NULL AND SR.SredniaCenaRynkowa > 0 THEN 
                        ((DP.cena - SR.SredniaCenaRynkowa) / SR.SredniaCenaRynkowa) * 100
                    ELSE NULL
                END AS OdchylenieProcentowe,
                CASE 
                    WHEN DP.cena < SR.SredniaCenaRynkowa * 0.9 THEN 'Okazja!'
                    WHEN DP.cena > SR.SredniaCenaRynkowa * 1.1 THEN 'Drogo'
                    ELSE 'OK'
                END AS OcenaCeny,
                1 AS CzyWybranyTowar,
                DP.idtw AS TowarID
            FROM [HANDEL].[HM].[DP] DP 
            INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
            LEFT JOIN SrednieRynkowe SR ON SR.kod = DP.kod
            WHERE DK.khid = @KontrahentID 
            AND DATEDIFF(week, DK.data, GETDATE()) = @WeekNumber
            AND (@TowarID IS NULL OR DP.idtw = @TowarID)";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@KontrahentID", kontrahentId);
                    cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pWeekNumber", weekNumber);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    ConfigureDetailsColumns();
                    dataGridViewDetails.DataSource = dt;

                    int totalItems = dt.Rows.Count;
                    int selectedItems = dt.AsEnumerable().Count(r => Convert.ToInt32(r["CzyWybranyTowar"]) == 1);
                    int uniqueProducts = dt.AsEnumerable().Select(r => r["NazwaTowaru"].ToString()).Distinct().Count();

                    lblItemCount.Text = showAllItems ?
                        $"📦 Produktów: {uniqueProducts} | Pozycji: {totalItems} (wybrany: {selectedItems})" :
                        $"📦 Pozycji: {totalItems}";

                    ApplySorting();
                    UpdateStatistics(dt);
                    UpdatePriceChart(dt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureDetailsColumns()
        {
            dataGridViewDetails.AutoGenerateColumns = false;
            dataGridViewDetails.Columns.Clear();

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn { Name = "CzyWybranyTowar", DataPropertyName = "CzyWybranyTowar", Visible = false });
            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn { Name = "TowarID", DataPropertyName = "TowarID", Visible = false });
            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn { Name = "TransakcjiRynkowych", DataPropertyName = "TransakcjiRynkowych", Visible = false });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ranking",
                HeaderText = "#",
                Width = 50,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = primaryColor
                }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Data",
                DataPropertyName = "Data",
                HeaderText = "📅 Data",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DzienTygodnia",
                DataPropertyName = "DzienTygodnia",
                HeaderText = "📆 Dzień",
                Width = 100
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NumerDokumentu",
                DataPropertyName = "NumerDokumentu",
                HeaderText = "📄 Dok.",
                Width = 80
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NazwaTowaru",
                DataPropertyName = "NazwaTowaru",
                HeaderText = "📦 Towar",
                Width = 220
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ilosc",
                DataPropertyName = "Ilosc",
                HeaderText = "Ilość",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Cena",
                DataPropertyName = "Cena",
                HeaderText = "💰 Cena",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SredniaCenaRynkowa",
                DataPropertyName = "SredniaCenaRynkowa",
                HeaderText = "Ø Śr. Rynk.",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OdchylenieProcentowe",
                DataPropertyName = "OdchylenieProcentowe",
                HeaderText = "Δ %",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OcenaCeny",
                DataPropertyName = "OcenaCeny",
                HeaderText = "💡 Ocena",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
            });

            dataGridViewDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Wartosc",
                DataPropertyName = "Wartosc",
                HeaderText = "💵 Wartość",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        }

        private void ApplySorting()
        {
            if (dataGridViewDetails.DataSource == null) return;

            var dt = (DataTable)dataGridViewDetails.DataSource;

            string sortColumn = "";
            string sortDirection = "DESC";

            switch (cmbSortOrder.SelectedIndex)
            {
                case 0: sortColumn = "Data"; sortDirection = "DESC"; break;
                case 1: sortColumn = "Data"; sortDirection = "ASC"; break;
                case 2: sortColumn = "Cena"; sortDirection = "DESC"; break;
                case 3: sortColumn = "Cena"; sortDirection = "ASC"; break;
                case 4: sortColumn = "Ilosc"; sortDirection = "DESC"; break;
                case 5: sortColumn = "Ilosc"; sortDirection = "ASC"; break;
                case 6: sortColumn = "Wartosc"; sortDirection = "DESC"; break;
                case 7: sortColumn = "Wartosc"; sortDirection = "ASC"; break;
                case 8: sortColumn = "NazwaTowaru"; sortDirection = "ASC"; break;
            }

            dt.DefaultView.Sort = $"{sortColumn} {sortDirection}";

            for (int i = 0; i < dataGridViewDetails.Rows.Count; i++)
            {
                dataGridViewDetails.Rows[i].Cells["Ranking"].Value = (i + 1).ToString();
            }
        }

        private void DataGridViewDetails_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridViewDetails.Rows[e.RowIndex];
            var czyWybrany = row.Cells["CzyWybranyTowar"].Value != null && Convert.ToInt32(row.Cells["CzyWybranyTowar"].Value) == 1;

            if (czyWybrany && chkShowAllItems.Checked)
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 248, 225);
                if (e.ColumnIndex == dataGridViewDetails.Columns["NazwaTowaru"]?.Index)
                {
                    e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }
            }

            if (e.ColumnIndex == dataGridViewDetails.Columns["OdchylenieProcentowe"]?.Index && e.Value != null && e.Value != DBNull.Value)
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal odch))
                {
                    if (odch < -10)
                        e.CellStyle.ForeColor = successColor;
                    else if (odch < 0)
                        e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                    else if (odch > 10)
                        e.CellStyle.ForeColor = dangerColor;
                    else if (odch > 0)
                        e.CellStyle.ForeColor = warningColor;
                    else
                        e.CellStyle.ForeColor = Color.Gray;

                    e.Value = $"{(odch >= 0 ? "+" : "")}{odch:N1}%";
                    e.FormattingApplied = true;
                }
            }

            if (e.ColumnIndex == dataGridViewDetails.Columns["OcenaCeny"]?.Index && e.Value != null)
            {
                string ocena = e.Value.ToString();
                if (ocena == "Okazja!")
                {
                    e.CellStyle.ForeColor = successColor;
                    e.CellStyle.BackColor = Color.FromArgb(212, 239, 223);
                }
                else if (ocena == "Drogo")
                {
                    e.CellStyle.ForeColor = dangerColor;
                    e.CellStyle.BackColor = Color.FromArgb(248, 215, 218);
                }
                else
                {
                    e.CellStyle.ForeColor = Color.Gray;
                }
            }

            if (e.ColumnIndex == dataGridViewDetails.Columns["SredniaCenaRynkowa"]?.Index)
            {
                var transakcje = row.Cells["TransakcjiRynkowych"].Value;
                if (transakcje != null && transakcje != DBNull.Value)
                {
                    row.Cells[e.ColumnIndex].ToolTipText = $"Średnia z {transakcje} transakcji";
                }
            }
        }

        private void UpdateStatistics(DataTable dt)
        {
            panelStats.Controls.Clear();

            if (dt.Rows.Count == 0)
            {
                panelStats.Controls.Add(new Label
                {
                    Text = "Brak danych",
                    Font = new Font("Segoe UI", 11f),
                    ForeColor = Color.Gray,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                return;
            }

            var items = dt.AsEnumerable().ToList();

            decimal totalQty = items.Sum(r => r["Ilosc"] != DBNull.Value ? Convert.ToDecimal(r["Ilosc"]) : 0);
            decimal totalValue = items.Sum(r => r["Wartosc"] != DBNull.Value ? Convert.ToDecimal(r["Wartosc"]) : 0);

            var prices = items.Where(r => r["Cena"] != DBNull.Value).Select(r => Convert.ToDecimal(r["Cena"])).ToList();
            decimal minPrice = prices.Any() ? prices.Min() : 0;
            decimal maxPrice = prices.Any() ? prices.Max() : 0;
            decimal avgPrice = prices.Any() ? prices.Average() : 0;

            var okazje = items.Count(r => r["OcenaCeny"].ToString() == "Okazja!");
            var drogie = items.Count(r => r["OcenaCeny"].ToString() == "Drogo");
            var uniqueDocs = items.Select(r => r["NumerDokumentu"].ToString()).Distinct().Count();

            var stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0)
            };

            stats.Controls.Add(CreateStatLabel($"📊 Pozycji: {items.Count}", primaryColor));
            stats.Controls.Add(CreateStatLabel($"📄 Dok: {uniqueDocs}", primaryColor));
            stats.Controls.Add(CreateStatLabel($"💰 Wartość: {totalValue:C2}", primaryColor));

            if (!chkShowAllItems.Checked)
            {
                stats.Controls.Add(CreateStatLabel($"📦 Ilość: {totalQty:N2}", primaryColor));
            }

            stats.Controls.Add(CreateStatLabel($"💵 {minPrice:C2} - {maxPrice:C2}", accentColor));
            stats.Controls.Add(CreateStatLabel($"Ø {avgPrice:C2}", accentColor));

            if (okazje > 0)
                stats.Controls.Add(CreateStatLabel($"🎯 Okazji: {okazje}", successColor));
            if (drogie > 0)
                stats.Controls.Add(CreateStatLabel($"⚠️ Drogich: {drogie}", dangerColor));

            panelStats.Controls.Add(stats);
        }

        private Label CreateStatLabel(string text, Color color)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = color,
                AutoSize = true,
                Padding = new Padding(0, 0, 20, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void UpdatePriceChart(DataTable dt)
        {
            chartPrices.Series.Clear();

            var items = dt.AsEnumerable()
                .Where(r => r["Cena"] != DBNull.Value)
                .OrderBy(r => Convert.ToDateTime(r["Data"]))
                .ToList();

            // Oblicz zakres tygodnia
            DateTime today = DateTime.Today;
            int daysOffset = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysOffset < 0) daysOffset += 7;
            DateTime weekStart = today.AddDays(-daysOffset - (weekNumber * 7));
            DateTime weekEnd = weekStart.AddDays(6);

            // KLUCZOWE: Przygotuj WSZYSTKIE 7 DNI TYGODNIA
            var allDayLabels = new List<string>();
            var allDates = new List<DateTime>();

            for (int dayIdx = 0; dayIdx < 7; dayIdx++)
            {
                DateTime date = weekStart.AddDays(dayIdx);
                allDates.Add(date);

                string dayName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);
                string label = $"{dayName}\n{date:dd.MM}";
                allDayLabels.Add(label);
            }

            if (chkShowAllItems.Checked)
            {
                var colors = new[]
                {
                    Color.FromArgb(52, 152, 219),
                    Color.FromArgb(39, 174, 96),
                    Color.FromArgb(155, 89, 182),
                    Color.FromArgb(230, 126, 34),
                    Color.FromArgb(231, 76, 60),
                    Color.FromArgb(26, 188, 156),
                    Color.FromArgb(241, 196, 15)
                };

                var groupedByProduct = items.GroupBy(r => r["NazwaTowaru"].ToString());
                int colorIndex = 0;

                foreach (var group in groupedByProduct.Take(7))
                {
                    var color = colors[colorIndex % colors.Length];

                    var series = new Series(group.Key)
                    {
                        ChartType = SeriesChartType.Line,
                        BorderWidth = 3,
                        Color = color,
                        MarkerStyle = MarkerStyle.Circle,
                        MarkerSize = 8,
                        MarkerColor = Color.White,
                        MarkerBorderColor = color,
                        MarkerBorderWidth = 2
                    };

                    // ZAWSZE DODAJ PUNKTY DLA WSZYSTKICH 7 DNI
                    for (int dayIdx = 0; dayIdx < 7; dayIdx++)
                    {
                        DateTime date = allDates[dayIdx];
                        string label = allDayLabels[dayIdx];

                        var dayTransactions = group.Where(r => Convert.ToDateTime(r["Data"]).Date == date.Date).ToList();

                        if (dayTransactions.Any())
                        {
                            foreach (var transaction in dayTransactions)
                            {
                                decimal cena = Convert.ToDecimal(transaction["Cena"]);
                                decimal ilosc = Convert.ToDecimal(transaction["Ilosc"]);
                                string dokument = transaction["NumerDokumentu"].ToString();

                                var pointIdx = series.Points.AddXY(label, cena);
                                series.Points[pointIdx].ToolTip = $"{group.Key}\nData: {date:dd.MM.yyyy}\nCena: {cena:C2}\nIlość: {ilosc:N2}\nDokument: {dokument}";
                            }
                        }
                        else
                        {
                            // KLUCZOWE: Dodaj przezroczysty punkt
                            var pointIdx = series.Points.AddXY(label, 0);
                            series.Points[pointIdx].Color = Color.Transparent;
                            series.Points[pointIdx].MarkerStyle = MarkerStyle.None;
                            series.Points[pointIdx].BorderWidth = 0;
                        }
                    }

                    chartPrices.Series.Add(series);
                    colorIndex++;
                }
            }
            else
            {
                // Tryb: tylko wybrany towar
                var seriesPrice = new Series("Cena zakupu")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 4,
                    Color = accentColor,
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 12,
                    MarkerColor = Color.White,
                    MarkerBorderColor = accentColor,
                    MarkerBorderWidth = 3
                };

                var seriesAvg = new Series("Średnia rynkowa")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 3,
                    BorderDashStyle = ChartDashStyle.Dash,
                    Color = successColor,
                    MarkerStyle = MarkerStyle.Diamond,
                    MarkerSize = 9,
                    MarkerColor = Color.White,
                    MarkerBorderColor = successColor,
                    MarkerBorderWidth = 2
                };

                // ZAWSZE DODAJ PUNKTY DLA WSZYSTKICH 7 DNI
                for (int dayIdx = 0; dayIdx < 7; dayIdx++)
                {
                    DateTime date = allDates[dayIdx];
                    string label = allDayLabels[dayIdx];

                    var dayTransactions = items.Where(r => Convert.ToDateTime(r["Data"]).Date == date.Date).ToList();

                    if (dayTransactions.Any())
                    {
                        foreach (var transaction in dayTransactions)
                        {
                            decimal cena = Convert.ToDecimal(transaction["Cena"]);
                            decimal ilosc = Convert.ToDecimal(transaction["Ilosc"]);
                            string dokument = transaction["NumerDokumentu"].ToString();
                            string dzien = transaction["DzienTygodnia"].ToString();

                            var pointIdx = seriesPrice.Points.AddXY(label, cena);

                            string tooltipText = $"{dzien}, {date:dd.MM.yyyy}\nCena: {cena:C2}\nIlość: {ilosc:N2}\nWartość: {(cena * ilosc):C2}\nDokument: {dokument}";

                            if (transaction["SredniaCenaRynkowa"] != DBNull.Value)
                            {
                                decimal sredniaRynkowa = Convert.ToDecimal(transaction["SredniaCenaRynkowa"]);
                                decimal roznica = cena - sredniaRynkowa;
                                decimal procentowo = sredniaRynkowa > 0 ? (roznica / sredniaRynkowa) * 100 : 0;

                                tooltipText += $"\n\nŚrednia rynkowa: {sredniaRynkowa:C2}\n";
                                tooltipText += procentowo >= 0 ?
                                    $"Drożej o {procentowo:N1}% (+{roznica:C2})" :
                                    $"Taniej o {Math.Abs(procentowo):N1}% ({roznica:C2})";

                                var avgPointIdx = seriesAvg.Points.AddXY(label, sredniaRynkowa);
                                seriesAvg.Points[avgPointIdx].ToolTip = $"Średnia rynkowa\n{date:dd.MM.yyyy}\n{sredniaRynkowa:C2}";
                            }

                            seriesPrice.Points[pointIdx].ToolTip = tooltipText;
                        }
                    }
                    else
                    {
                        // KLUCZOWE: Dodaj przezroczysty punkt
                        var pointIdx = seriesPrice.Points.AddXY(label, 0);
                        seriesPrice.Points[pointIdx].Color = Color.Transparent;
                        seriesPrice.Points[pointIdx].MarkerStyle = MarkerStyle.None;
                        seriesPrice.Points[pointIdx].BorderWidth = 0;
                    }
                }

                chartPrices.Series.Add(seriesPrice);
                if (seriesAvg.Points.Count > 0)
                    chartPrices.Series.Add(seriesAvg);
            }

            // WYMUSZENIE WSZYSTKICH 7 ETYKIET
            if (chartPrices.ChartAreas.Count > 0)
            {
                chartPrices.ChartAreas[0].AxisX.Interval = 1;
                chartPrices.ChartAreas[0].AxisX.LabelStyle.Angle = 0;
                chartPrices.ChartAreas[0].AxisX.IsMarginVisible = false;

                // KLUCZOWE: ustaw minimum i maximum
                chartPrices.ChartAreas[0].AxisX.Minimum = -0.5;
                chartPrices.ChartAreas[0].AxisX.Maximum = 6.5; // 7 dni (0-6)

                chartPrices.ChartAreas[0].AxisY.LabelStyle.Format = "C2";
            }
        }

        private string GetWeekDateRange(int weeksBack)
        {
            DateTime today = DateTime.Today;
            int daysOffset = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysOffset < 0) daysOffset += 7;

            DateTime weekStart = today.AddDays(-daysOffset - (weeksBack * 7));
            DateTime weekEnd = weekStart.AddDays(6);

            return $"{weekStart:dd.MM.yyyy} - {weekEnd:dd.MM.yyyy}";
        }

        private void ShowDetailsHelp()
        {
            var helpText = @"═════════════════════════════════════════════════
  SZCZEGÓŁY TYGODNIA - INSTRUKCJA
═════════════════════════════════════════════════

🔍 CHECKBOX 'Pokaż wszystkie towary'
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• WYŁĄCZONY (domyślnie):
  → Pokazuje tylko wybrany towar
  → Możesz zobaczyć wszystkie transakcje tego towaru
  → Wykres pokazuje ceny zakupu vs średnia rynkowa

• WŁĄCZONY:
  → Pokazuje WSZYSTKIE towary z dokumentów
  → Pozwala zobaczyć cały ""koszyk"" zakupów
  → Wykres pokazuje ceny wielu produktów
  → Żółte tło = wybrany towar oryginalny

📊 SORTOWANIE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Możesz sortować po:
  → Data (najnowsze/najstarsze)
  → Cena (najwyższa/najniższa)
  → Ilość
  → Wartość
  → Nazwa towaru (alfabetycznie)

💡 KOLUMNY TABELI
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• # = Ranking (numer pozycji)
• Data = Data transakcji
• Dzień = Dzień tygodnia
• Dok. = Numer dokumentu
• Towar = Nazwa produktu
• Cena = Cena zakupu
• Ø Śr. Rynk. = Średnia cena rynkowa w tym tygodniu
• Δ % = Odchylenie od średniej rynkowej
  → Zielony = kupiłeś taniej
  → Czerwony = kupiłeś drożej
• Ocena:
  → ""Okazja!"" = ponad 10% taniej od średniej
  → ""OK"" = ±10% od średniej
  → ""Drogo"" = ponad 10% drożej od średniej

📈 WYKRES CEN
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Pokazuje ceny w każdym dniu tygodnia (Pn-Nd)
• Linia niebieska = cena zakupu
• Linia zielona = średnia rynkowa
• Najedź na punkt aby zobaczyć szczegóły

⛶ PEŁNY EKRAN
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Przycisk ⛶ otwiera wykres w dużym oknie
• Możesz zapisać wykres jako obraz

═════════════════════════════════════════════════";

            MessageBox.Show(helpText, "Pomoc - Szczegóły Tygodnia", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPriceChartHelp()
        {
            var helpText = @"═════════════════════════════════════════════════
  WYKRES CEN - INSTRUKCJA
═════════════════════════════════════════════════

📊 CO POKAZUJE?
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Oś X: Wszystkie dni tygodnia (Pn, Wt, Śr, Czw, Pt, Sob, Nd)
• Oś Y: Cena w złotówkach (zł)

🎨 TRYB: TYLKO WYBRANY TOWAR
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Niebieska linia = Twoja cena zakupu
• Zielona przerywana = Średnia cena rynkowa
• Porównanie pokazuje czy kupiłeś tanio czy drogo

🎨 TRYB: WSZYSTKIE TOWARY
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Każdy towar ma swoją kolorową linię
• Maksymalnie 7 produktów na wykresie
• Legenda u góry pokazuje nazwę towaru

💡 TOOLTIPS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

• Najedź myszką na punkt aby zobaczyć:
  → Dokładną datę
  → Cenę
  → Ilość
  → Wartość
  → Porównanie ze średnią rynkową

═════════════════════════════════════════════════";

            MessageBox.Show(helpText, "Pomoc - Wykres Cen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ============================================================================
    // OKNO PODGLĄDU WYKRESU NA PEŁNYM EKRANIE
    // ============================================================================
    public class ChartViewerForm : Form
    {
        private Chart chartViewer;
        private Button btnClose;
        private Button btnSaveImage;

        private readonly Color primaryColor = Color.FromArgb(44, 62, 80);
        private readonly Color dangerColor = Color.FromArgb(231, 76, 60);
        private readonly Color successColor = Color.FromArgb(39, 174, 96);

        public ChartViewerForm(Chart sourceChart, string title)
        {
            this.Text = title;
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.White;
            this.KeyPreview = true;

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    this.Close();
            };

            this.chartViewer = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            foreach (var series in sourceChart.Series)
            {
                var newSeries = new Series(series.Name)
                {
                    ChartType = series.ChartType,
                    BorderWidth = series.BorderWidth + 1,
                    Color = series.Color,
                    MarkerStyle = series.MarkerStyle,
                    MarkerSize = series.MarkerSize + 2,
                    BorderDashStyle = series.BorderDashStyle
                };

                foreach (var point in series.Points)
                {
                    var newPoint = newSeries.Points.AddXY(
                        !string.IsNullOrEmpty(point.AxisLabel) ? point.AxisLabel : point.XValue,
                        point.YValues[0]
                    );

                    // Kopiuj właściwości punktu
                    if (point.Color != Color.Empty && point.Color != series.Color)
                    {
                        newSeries.Points[newSeries.Points.Count - 1].Color = point.Color;
                    }
                    if (point.MarkerStyle != MarkerStyle.None && point.MarkerStyle != series.MarkerStyle)
                    {
                        newSeries.Points[newSeries.Points.Count - 1].MarkerStyle = point.MarkerStyle;
                    }
                    if (point.BorderWidth != series.BorderWidth)
                    {
                        newSeries.Points[newSeries.Points.Count - 1].BorderWidth = point.BorderWidth;
                    }
                }

                this.chartViewer.Series.Add(newSeries);
            }

            var sourceArea = sourceChart.ChartAreas[0];
            var chartArea = new ChartArea("ViewArea")
            {
                BackColor = Color.White
            };
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 11f);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 11f);
            chartArea.AxisX.Title = sourceArea.AxisX.Title;
            chartArea.AxisY.Title = sourceArea.AxisY.Title;
            chartArea.AxisX.TitleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
            chartArea.AxisY.TitleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
            chartArea.AxisX.LineColor = Color.FromArgb(150, 150, 150);
            chartArea.AxisY.LineColor = Color.FromArgb(150, 150, 150);

            // Skopiuj format osi Y (np. waluta)
            chartArea.AxisY.LabelStyle.Format = sourceArea.AxisY.LabelStyle.Format;

            // Skopiuj ustawienia osi X
            chartArea.AxisX.Interval = sourceArea.AxisX.Interval;
            chartArea.AxisX.LabelStyle.Angle = sourceArea.AxisX.LabelStyle.Angle;
            chartArea.AxisX.IsMarginVisible = sourceArea.AxisX.IsMarginVisible;
            chartArea.AxisX.Minimum = sourceArea.AxisX.Minimum;
            chartArea.AxisX.Maximum = sourceArea.AxisX.Maximum;

            this.chartViewer.ChartAreas.Add(chartArea);

            if (sourceChart.Legends.Count > 0)
            {
                var legend = new Legend
                {
                    Docking = Docking.Top,
                    Alignment = StringAlignment.Center,
                    Font = new Font("Segoe UI", 11f)
                };
                this.chartViewer.Legends.Add(legend);
            }

            if (sourceChart.Titles.Count > 0)
            {
                var chartTitle = new Title
                {
                    Text = sourceChart.Titles[0].Text,
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = primaryColor,
                    Docking = Docking.Top
                };
                this.chartViewer.Titles.Add(chartTitle);
            }

            var panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(20, 15, 20, 15)
            };

            this.btnSaveImage = new Button
            {
                Text = "💾 Zapisz jako obraz",
                Size = new Size(180, 40),
                Location = new Point(20, 15),
                FlatStyle = FlatStyle.Flat,
                BackColor = successColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            this.btnSaveImage.FlatAppearance.BorderSize = 0;
            this.btnSaveImage.Click += BtnSaveImage_Click;

            this.btnClose = new Button
            {
                Text = "❌ Zamknij (ESC)",
                Size = new Size(160, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = dangerColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.Click += (s, e) => this.Close();

            panelButtons.SizeChanged += (s, e) =>
            {
                btnClose.Location = new Point(panelButtons.Width - 180, 15);
            };

            panelButtons.Controls.AddRange(new Control[] { btnSaveImage, btnClose });

            this.Controls.Add(chartViewer);
            this.Controls.Add(panelButtons);
        }

        private void BtnSaveImage_Click(object sender, EventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg";
                    saveDialog.FileName = $"Wykres_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        chartViewer.SaveImage(saveDialog.FileName, ChartImageFormat.Png);

                        MessageBox.Show("Wykres został zapisany pomyślnie!", "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}