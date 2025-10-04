using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace Kalendarz1
{
    public partial class Mroznia : Form
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // === KOLORY MOTYWU ===
        private readonly Color PrimaryColor = Color.FromArgb(0, 120, 212);
        private readonly Color SuccessColor = Color.FromArgb(16, 124, 16);
        private readonly Color WarningColor = Color.FromArgb(255, 140, 0);
        private readonly Color DangerColor = Color.FromArgb(232, 17, 35);
        private readonly Color InfoColor = Color.FromArgb(142, 68, 173);
        private readonly Color BackgroundColor = Color.FromArgb(245, 246, 250);
        private readonly Color CardColor = Color.White;
        private readonly Color TextColor = Color.FromArgb(33, 33, 33);
        private readonly Color SecondaryTextColor = Color.FromArgb(99, 99, 99);

        // === KONTROLKI UI ===
        private DateTimePicker dtpOd, dtpDo, dtpStanMagazynu;
        private Button btnAnalizuj, btnWykres, btnStanMagazynu, btnEksport, btnResetFiltr, btnSzybkiRaport;
        private DataGridView dgvDzienne, dgvSzczegoly, dgvAnaliza, dgvStanMagazynu;
        private TabControl tabControl;
        private ComboBox cmbFiltrProduktu, cmbPredkosc;
        private TextBox txtSzukaj;
        private Label lblPodsumowanie, lblWydano, lblPrzyjeto, lblSrednia, lblTrendInfo;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripProgressBar progressBar;
        private Chart chartTrend, chartProdukty;
        private Panel panelKarty;
        private ToolTip toolTip;

        // === DANE CACHE ===
        private DataTable cachedDzienneData;
        private DateTime lastAnalysisDate = DateTime.MinValue;

        public Mroznia()
        {
            InitializeComponent();
            SetupEvents();
            LoadInitialData();
        }

        private void InitializeComponent()
        {
            this.Text = "🏭 Mroźnia - System Analityczny PRO";
            this.Size = new Size(1680, 1000);
            this.MinimumSize = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = BackgroundColor;
            this.Font = new Font("Segoe UI", 9F);
            this.WindowState = FormWindowState.Maximized;

            toolTip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 500 };

            // === GŁÓWNY LAYOUT ===
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

            // === GÓRNY PANEL Z KARTAMI ===
            Panel topPanel = CreateTopPanel();

            // === GŁÓWNA ZAWARTOŚĆ ===
            tabControl = CreateTabControl();

            // === STATUS BAR ===
            CreateStatusBar();

            mainLayout.Controls.Add(topPanel, 0, 0);
            mainLayout.Controls.Add(tabControl, 0, 1);
            mainLayout.Controls.Add(statusStrip, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private Panel CreateTopPanel()
        {
            Panel mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackgroundColor,
                Padding = new Padding(15, 10, 15, 10)
            };

            // === PANEL KONTROLEK ===
            Panel controlPanel = new Panel
            {
                Height = 60,
                Dock = DockStyle.Top,
                BackColor = CardColor
            };
            controlPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, controlPanel);

            // Data początkowa
            Label lblOd = CreateLabel("Data od:", 15, 18, true);
            dtpOd = new DateTimePicker
            {
                Location = new Point(75, 15),
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddDays(-30)
            };

            // Data końcowa
            Label lblDo = CreateLabel("do:", 230, 18, true);
            dtpDo = new DateTimePicker
            {
                Location = new Point(265, 15),
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };

            // Szybki wybór okresu
            cmbPredkosc = new ComboBox
            {
                Location = new Point(420, 15),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPredkosc.Items.AddRange(new object[] {
                "Dowolny", "Dziś", "Wczoraj", "Ostatnie 7 dni", "Ostatnie 30 dni",
                "Bieżący miesiąc", "Poprzedni miesiąc", "Ostatnie 3 miesiące"
            });
            cmbPredkosc.SelectedIndex = 0;
            cmbPredkosc.SelectedIndexChanged += CmbPredkosc_SelectedIndexChanged;

            // Przyciski akcji
            btnAnalizuj = CreateModernButton("🔍 Analizuj", 575, 12, 110, PrimaryColor);
            btnWykres = CreateModernButton("📊 Wykresy", 695, 12, 100, SuccessColor);
            btnStanMagazynu = CreateModernButton("📦 Stan", 805, 12, 90, WarningColor);
            btnSzybkiRaport = CreateModernButton("📄 Raport", 905, 12, 100, InfoColor);
            btnEksport = CreateModernButton("💾 Eksport", 1015, 12, 100, DangerColor);

            toolTip.SetToolTip(btnAnalizuj, "Załaduj i analizuj dane dla wybranego okresu");
            toolTip.SetToolTip(btnWykres, "Otwórz zaawansowane wykresy");
            toolTip.SetToolTip(btnStanMagazynu, "Zobacz aktualny stan magazynu");
            toolTip.SetToolTip(btnSzybkiRaport, "Generuj szybki raport PDF");
            toolTip.SetToolTip(btnEksport, "Eksportuj dane do Excel");

            controlPanel.Controls.AddRange(new Control[] {
                lblOd, dtpOd, lblDo, dtpDo, cmbPredkosc,
                btnAnalizuj, btnWykres, btnStanMagazynu, btnSzybkiRaport, btnEksport
            });

            // === PANEL KART STATYSTYK ===
            panelKarty = CreateKartyStatystyk();
            panelKarty.Top = 70;

            mainPanel.Controls.Add(panelKarty);
            mainPanel.Controls.Add(controlPanel);

            return mainPanel;
        }

        private Panel CreateKartyStatystyk()
        {
            Panel panel = new Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = BackgroundColor
            };

            // Karta 1 - Wydano
            Panel card1 = CreateStatCard("📤 WYDANO", "0 kg", PrimaryColor, 0);
            lblWydano = (Label)card1.Controls[1];

            // Karta 2 - Przyjęto
            Panel card2 = CreateStatCard("📥 PRZYJĘTO", "0 kg", SuccessColor, 1);
            lblPrzyjeto = (Label)card2.Controls[1];

            // Karta 3 - Średnia dzienna
            Panel card3 = CreateStatCard("📊 ŚREDNIO/DZIEŃ", "0 kg", WarningColor, 2);
            lblSrednia = (Label)card3.Controls[1];

            // Karta 4 - Podsumowanie
            Panel card4 = CreateStatCard("ℹ️ STATUS", "Wybierz okres", InfoColor, 3);
            lblPodsumowanie = (Label)card4.Controls[1];
            lblPodsumowanie.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            panel.Controls.AddRange(new Control[] { card1, card2, card3, card4 });

            return panel;
        }

        private Panel CreateStatCard(string title, string value, Color accentColor, int position)
        {
            int cardWidth = 320;
            int margin = 10;

            Panel card = new Panel
            {
                Location = new Point(position * (cardWidth + margin), 0),
                Size = new Size(cardWidth, 65),
                BackColor = CardColor,
                Cursor = Cursors.Hand
            };
            card.Paint += (s, e) => DrawCardBorder(e.Graphics, card, accentColor);

            Label lblTitle = new Label
            {
                Text = title,
                Location = new Point(15, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = SecondaryTextColor
            };

            Label lblValue = new Label
            {
                Text = value,
                Location = new Point(15, 28),
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = accentColor
            };

            card.Controls.AddRange(new Control[] { lblTitle, lblValue });

            return card;
        }

        private void DrawCardBorder(Graphics g, Control control, Color? accentColor = null)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = GetRoundedRectangle(control.ClientRectangle, 8))
            {
                // Cień
                using (PathGradientBrush shadowBrush = new PathGradientBrush(path))
                {
                    shadowBrush.CenterColor = Color.FromArgb(10, 0, 0, 0);
                    shadowBrush.SurroundColors = new[] { Color.Transparent };

                    Rectangle shadowRect = control.ClientRectangle;
                    shadowRect.Offset(0, 2);
                    using (GraphicsPath shadowPath = GetRoundedRectangle(shadowRect, 8))
                    {
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }

                // Tło
                g.FillPath(Brushes.White, path);

                // Border
                Color borderColor = accentColor ?? Color.FromArgb(230, 230, 230);
                using (Pen pen = new Pen(borderColor, 2))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, radius, radius, 180, 90);
            path.AddArc(bounds.Right - radius, bounds.Y, radius, radius, 270, 90);
            path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        private TabControl CreateTabControl()
        {
            TabControl tc = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Point(20, 8)
            };

            // === ZAKŁADKA 1: DZIENNE PRZEGLĄD ===
            TabPage tab1 = new TabPage("  📅 Przegląd dzienny  ");
            tab1.BackColor = BackgroundColor;

            SplitContainer split1 = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 350,
                BackColor = BackgroundColor,
                SplitterWidth = 8
            };

            // Górny panel z filtrem
            Panel topFilterPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = BackgroundColor };
            Label lblInfo = CreateLabel("Kliknij wiersz aby zobaczyć szczegóły dnia ▼", 10, 12, false);
            lblInfo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblInfo.ForeColor = PrimaryColor;
            topFilterPanel.Controls.Add(lblInfo);

            dgvDzienne = CreateStyledDataGridView();

            Panel panelTop = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            panelTop.Controls.Add(dgvDzienne);
            panelTop.Controls.Add(topFilterPanel);

            split1.Panel1.Controls.Add(panelTop);

            // Dolny panel ze szczegółami
            Panel bottomPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor, Padding = new Padding(10) };

            Panel headerPanel = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = CardColor };
            headerPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, headerPanel);

            Label lblSzczegoly = new Label
            {
                Text = "📋 Szczegółowe pozycje wybranego dnia",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = PrimaryColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0)
            };
            headerPanel.Controls.Add(lblSzczegoly);

            dgvSzczegoly = CreateStyledDataGridView();
            Panel gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            gridPanel.Controls.Add(dgvSzczegoly);

            bottomPanel.Controls.Add(gridPanel);
            bottomPanel.Controls.Add(headerPanel);

            split1.Panel2.Controls.Add(bottomPanel);
            tab1.Controls.Add(split1);

            // === ZAKŁADKA 2: ANALIZA PRODUKTÓW ===
            TabPage tab2 = new TabPage("  📊 Analiza produktów  ");
            tab2.BackColor = BackgroundColor;
            tab2.Padding = new Padding(10);

            Panel analizaPanel = new Panel { Dock = DockStyle.Fill };

            // Panel filtrowania
            Panel filterPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = CardColor };
            filterPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, filterPanel);

            Label lblFiltr = CreateLabel("🔍 Filtruj:", 15, 16, true);
            cmbFiltrProduktu = new ComboBox
            {
                Location = new Point(80, 13),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFiltrProduktu.Items.AddRange(new object[] {
                "Wszystkie produkty", "Kurczak A", "Korpus", "Ćwiartka", "Filet A",
                "Filet II", "Skrzydło I", "Trybowane bez skóry", "Trybowane ze skórą"
            });
            cmbFiltrProduktu.SelectedIndex = 0;

            Label lblSzukaj = CreateLabel("Szukaj:", 300, 16, true);
            txtSzukaj = new TextBox
            {
                Location = new Point(355, 13),
                Width = 200,
                Font = new Font("Segoe UI", 9F)
            };

            btnResetFiltr = CreateModernButton("↻ Reset", 570, 11, 80, SecondaryTextColor);

            filterPanel.Controls.AddRange(new Control[] { lblFiltr, cmbFiltrProduktu, lblSzukaj, txtSzukaj, btnResetFiltr });

            dgvAnaliza = CreateStyledDataGridView();
            Panel gridPanel2 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            gridPanel2.Controls.Add(dgvAnaliza);

            analizaPanel.Controls.Add(gridPanel2);
            analizaPanel.Controls.Add(filterPanel);
            tab2.Controls.Add(analizaPanel);

            // === ZAKŁADKA 3: WYKRESY I TRENDY ===
            TabPage tab3 = new TabPage("  📈 Wykresy i trendy  ");
            tab3.BackColor = BackgroundColor;
            tab3.Padding = new Padding(10);

            SplitContainer splitCharts = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 400,
                BackColor = BackgroundColor,
                SplitterWidth = 10
            };

            // Górny wykres - trend czasowy
            Panel chartPanel1 = new Panel { Dock = DockStyle.Fill, BackColor = CardColor };
            chartPanel1.Paint += (s, e) => DrawCardBorder(e.Graphics, chartPanel1);

            chartTrend = CreateStyledChart("📈 Trend wydań w czasie");
            chartPanel1.Controls.Add(chartTrend);
            splitCharts.Panel1.Controls.Add(chartPanel1);

            // Dolny wykres - porównanie produktów
            Panel chartPanel2 = new Panel { Dock = DockStyle.Fill, BackColor = CardColor };
            chartPanel2.Paint += (s, e) => DrawCardBorder(e.Graphics, chartPanel2);

            chartProdukty = CreateStyledChart("📊 Top 10 produktów");
            chartPanel2.Controls.Add(chartProdukty);
            splitCharts.Panel2.Controls.Add(chartPanel2);

            tab3.Controls.Add(splitCharts);

            // === ZAKŁADKA 4: STAN MAGAZYNU ===
            TabPage tab4 = new TabPage("  📦 Stan magazynu  ");
            tab4.BackColor = BackgroundColor;
            tab4.Padding = new Padding(10);

            Panel stanPanel = new Panel { Dock = DockStyle.Fill };

            Panel stanHeader = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = CardColor };
            stanHeader.Paint += (s, e) => DrawCardBorder(e.Graphics, stanHeader);

            Label lblStanNa = CreateLabel("Stan magazynu na dzień:", 15, 16, true);
            dtpStanMagazynu = new DateTimePicker
            {
                Location = new Point(180, 13),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };

            Button btnObliczStan = CreateModernButton("📊 Oblicz", 350, 11, 100, PrimaryColor);
            btnObliczStan.Click += BtnStanMagazynu_Click;

            stanHeader.Controls.AddRange(new Control[] { lblStanNa, dtpStanMagazynu, btnObliczStan });

            dgvStanMagazynu = CreateStyledDataGridView();
            Panel gridPanel3 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            gridPanel3.Controls.Add(dgvStanMagazynu);

            stanPanel.Controls.Add(gridPanel3);
            stanPanel.Controls.Add(stanHeader);
            tab4.Controls.Add(stanPanel);

            tc.TabPages.AddRange(new TabPage[] { tab1, tab2, tab3, tab4 });
            return tc;
        }

        private DataGridView CreateStyledDataGridView()
        {
            DataGridView dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeight = 45,
                RowTemplate = { Height = 35 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(240, 240, 240),
                RowHeadersVisible = false
            };

            // Nagłówki
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(41, 128, 185);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(10);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            // Komórki
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv.DefaultCellStyle.ForeColor = TextColor;
            dgv.DefaultCellStyle.Padding = new Padding(10, 5, 10, 5);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 237, 248);
            dgv.DefaultCellStyle.SelectionForeColor = TextColor;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            return dgv;
        }

        private Chart CreateStyledChart(string title)
        {
            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            Title chartTitle = new Title
            {
                Text = title,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = PrimaryColor,
                Docking = Docking.Top
            };
            chart.Titles.Add(chartTitle);

            ChartArea area = new ChartArea
            {
                BackColor = Color.White,
                BorderWidth = 0
            };
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 9F);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 9F);
            chart.ChartAreas.Add(area);

            Legend legend = new Legend
            {
                Docking = Docking.Bottom,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            };
            chart.Legends.Add(legend);

            return chart;
        }

        private Button CreateModernButton(string text, int x, int y, int width, Color color)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.1f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.1f);

            // Zaokrąglone rogi
            btn.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, btn.Width, btn.Height, 6, 6));

            return btn;
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private Label CreateLabel(string text, int x, int y, bool bold)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = TextColor
            };
        }

        private void CreateStatusBar()
        {
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(240, 240, 240),
                Font = new Font("Segoe UI", 9F)
            };

            statusLabel = new ToolStripStatusLabel
            {
                Text = "Gotowy do pracy",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressBar = new ToolStripProgressBar
            {
                Visible = false,
                Size = new Size(200, 20)
            };

            ToolStripStatusLabel lblVersion = new ToolStripStatusLabel
            {
                Text = "v2.0 PRO",
                ForeColor = SecondaryTextColor
            };

            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, progressBar, lblVersion });
        }

        private void SetupEvents()
        {
            btnAnalizuj.Click += BtnAnalizuj_Click;
            btnWykres.Click += (s, e) => tabControl.SelectedIndex = 2;
            btnStanMagazynu.Click += (s, e) => tabControl.SelectedIndex = 3;
            btnSzybkiRaport.Click += BtnSzybkiRaport_Click;
            btnEksport.Click += BtnEksport_Click;
            btnResetFiltr.Click += (s, e) => ResetujFiltry();

            dgvDzienne.SelectionChanged += DgvDzienne_SelectionChanged;
            cmbFiltrProduktu.SelectedIndexChanged += (s, e) => AplikujFiltr();
            txtSzukaj.TextChanged += (s, e) => AplikujFiltr();

            this.Load += Mroznia_Load;
        }

        private void Mroznia_Load(object sender, EventArgs e)
        {
            // Automatyczne załadowanie danych z ostatnich 30 dni
            dtpOd.Value = DateTime.Now.AddDays(-30);
            dtpDo.Value = DateTime.Now;

            // Animacja powitalna
            statusLabel.Text = "Witaj w systemie analitycznym! Kliknij 'Analizuj' aby rozpocząć.";
        }

        private void LoadInitialData()
        {
            // Można tu załadować dane wstępne
        }

        // ============================================
        // GŁÓWNE FUNKCJE ANALITYCZNE
        // ============================================

        private async void BtnAnalizuj_Click(object sender, EventArgs e)
        {
            if (dtpOd.Value > dtpDo.Value)
            {
                MessageBox.Show("Data początkowa nie może być późniejsza niż data końcowa!",
                    "Błąd dat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            statusLabel.Text = "⏳ Ładowanie danych...";
            progressBar.Visible = true;
            progressBar.Value = 0;
            this.Cursor = Cursors.WaitCursor;
            DisableButtons();

            DateTime od = dtpOd.Value.Date;
            DateTime doDaty = dtpDo.Value.Date;

            try
            {
                await Task.Run(() =>
                {
                    this.Invoke((Action)(() =>
                    {
                        progressBar.Value = 20;
                        LoadDzienneZestawienie(od, doDaty);

                        progressBar.Value = 40;
                        LoadAnalizaProduktu(od, doDaty);

                        progressBar.Value = 60;
                        LoadTrendy(od, doDaty);

                        progressBar.Value = 80;
                        LoadTopProdukty(od, doDaty);

                        progressBar.Value = 100;
                        UpdateKartyStatystyk(od, doDaty);
                    }));
                });

                lastAnalysisDate = DateTime.Now;
                statusLabel.Text = $"✅ Dane załadowane pomyślnie | {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "❌ Błąd ładowania danych";
            }
            finally
            {
                progressBar.Visible = false;
                this.Cursor = Cursors.Default;
                EnableButtons();
            }
        }

        private void LoadDzienneZestawienie(DateTime od, DateTime doDaty)
        {
            string query = @"
                SELECT
                    MG.[Data] AS Data,
                    DATENAME(dw, MG.[Data]) AS DzienTygodnia,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) - 
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Bilans,
                    COUNT(DISTINCT MZ.kod) AS Pozycje
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = 65552
                AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                AND MG.[Data] BETWEEN @Od AND @Do
                GROUP BY MG.[Data]
                ORDER BY MG.[Data] DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                adapter.SelectCommand.Parameters.AddWithValue("@Od", od);
                adapter.SelectCommand.Parameters.AddWithValue("@Do", doDaty);

                DataTable dt = new DataTable();
                adapter.Fill(dt);

                cachedDzienneData = dt;
                dgvDzienne.DataSource = dt;

                // Formatowanie kolumn
                FormatujKolumne(dgvDzienne, "Data", "Data", "yyyy-MM-dd");
                FormatujKolumne(dgvDzienne, "DzienTygodnia", "Dzień");
                FormatujKolumne(dgvDzienne, "Wydano", "Wydano (kg)", "N0");
                FormatujKolumne(dgvDzienne, "Przyjeto", "Przyjęto (kg)", "N0");
                FormatujKolumne(dgvDzienne, "Bilans", "Bilans (kg)", "N0");
                FormatujKolumne(dgvDzienne, "Pozycje", "Pozycje");

                // Kolorowanie bilansu
                foreach (DataGridViewRow row in dgvDzienne.Rows)
                {
                    if (row.Cells["Bilans"].Value != null)
                    {
                        decimal bilans = Convert.ToDecimal(row.Cells["Bilans"].Value);
                        if (bilans < 0)
                            row.Cells["Bilans"].Style.ForeColor = SuccessColor;
                        else if (bilans > 0)
                            row.Cells["Bilans"].Style.ForeColor = DangerColor;
                    }
                }
            }
        }

        private void DgvDzienne_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvDzienne.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvDzienne.SelectedRows[0];
            if (row.Cells["Data"].Value == null) return;

            DateTime wybranaData = Convert.ToDateTime(row.Cells["Data"].Value);
            LoadSzczegolyDnia(wybranaData);
        }

        private void LoadSzczegolyDnia(DateTime data)
        {
            string query = @"
                SELECT
                    CASE 
                        WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                        WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                        WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                        WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                        WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                        WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                        WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                        WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                        ELSE MZ.kod
                    END AS Produkt,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto,
                    COUNT(*) AS Operacje,
                    CASE 
                        WHEN ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) > 1000 THEN '🔥 Wysoki'
                        WHEN ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) > 500 THEN '📊 Średni'
                        ELSE '📉 Niski'
                    END AS Ruch
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = 65552
                AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                AND CAST(MG.[Data] AS DATE) = @Data
                GROUP BY 
                    CASE 
                        WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                        WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                        WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                        WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                        WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                        WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                        WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                        WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                        ELSE MZ.kod
                    END
                ORDER BY Wydano DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                adapter.SelectCommand.Parameters.AddWithValue("@Data", data.Date);

                DataTable dt = new DataTable();
                adapter.Fill(dt);

                dgvSzczegoly.DataSource = dt;

                FormatujKolumne(dgvSzczegoly, "Wydano", "Wydano (kg)", "N0");
                FormatujKolumne(dgvSzczegoly, "Przyjeto", "Przyjęto (kg)", "N0");
                FormatujKolumne(dgvSzczegoly, "Operacje", "Operacje");
            }

            statusLabel.Text = $"📋 Szczegóły dnia: {data:yyyy-MM-dd dddd}";
        }

        private void LoadAnalizaProduktu(DateTime od, DateTime doDaty)
        {
            string query = @"
                WITH Dane AS (
                    SELECT
                        CASE 
                            WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                            WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                            WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                            WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                            WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                            WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                            WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                            WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                            ELSE MZ.kod
                        END AS Produkt,
                        ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                        SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto,
                        COUNT(DISTINCT MG.[Data]) AS DniAktywnosci
                    FROM [HANDEL].[HM].[MG]
                    JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                    WHERE MG.magazyn = 65552
                    AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                    AND MG.[Data] BETWEEN @Od AND @Do
                    GROUP BY 
                        CASE 
                            WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                            WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                            WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                            WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                            WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                            WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                            WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                            WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                            ELSE MZ.kod
                        END
                )
                SELECT 
                    Produkt,
                    Wydano,
                    Przyjeto,
                    (Wydano - Przyjeto) AS Roznica,
                    DniAktywnosci AS [Dni],
                    CASE WHEN DniAktywnosci > 0 THEN Wydano / DniAktywnosci ELSE 0 END AS [Śr/dzień],
                    CASE 
                        WHEN Wydano > 5000 THEN '⭐ TOP'
                        WHEN Wydano > 2000 THEN '📊 Wysoki'
                        WHEN Wydano > 500 THEN '📈 Średni'
                        ELSE '📉 Niski'
                    END AS Kategoria
                FROM Dane
                WHERE Wydano > 0 OR Przyjeto > 0
                ORDER BY Wydano DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                adapter.SelectCommand.Parameters.AddWithValue("@Od", od);
                adapter.SelectCommand.Parameters.AddWithValue("@Do", doDaty);

                DataTable dt = new DataTable();
                adapter.Fill(dt);

                dgvAnaliza.DataSource = dt;

                FormatujKolumne(dgvAnaliza, "Wydano", "Wydano (kg)", "N0");
                FormatujKolumne(dgvAnaliza, "Przyjeto", "Przyjęto (kg)", "N0");
                FormatujKolumne(dgvAnaliza, "Roznica", "Różnica (kg)", "N0");
                FormatujKolumne(dgvAnaliza, "Śr/dzień", "Średnio/dzień", "N1");

                // Kolorowanie
                foreach (DataGridViewRow row in dgvAnaliza.Rows)
                {
                    if (row.Cells["Roznica"].Value != null)
                    {
                        decimal roznica = Convert.ToDecimal(row.Cells["Roznica"].Value);
                        row.Cells["Roznica"].Style.ForeColor = roznica < 0 ? SuccessColor : DangerColor;
                    }

                    string kategoria = row.Cells["Kategoria"].Value?.ToString();
                    if (kategoria?.Contains("TOP") == true)
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 205);
                }
            }
        }

        private void LoadTrendy(DateTime od, DateTime doDaty)
        {
            chartTrend.Series.Clear();

            string query = @"
                SELECT
                    MG.[Data] AS Data,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = 65552
                AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                AND MG.[Data] BETWEEN @Od AND @Do
                GROUP BY MG.[Data]
                ORDER BY MG.[Data]";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Od", od);
                cmd.Parameters.AddWithValue("@Do", doDaty);

                Series series = new Series("Wydania")
                {
                    ChartType = SeriesChartType.SplineArea,
                    BorderWidth = 3,
                    Color = Color.FromArgb(100, 41, 128, 185),
                    BorderColor = Color.FromArgb(41, 128, 185),
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerColor = Color.FromArgb(41, 128, 185)
                };

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime data = reader.GetDateTime(0);
                        double wydano = Convert.ToDouble(reader[1]);
                        var point = series.Points.AddXY(data, wydano);
                        series.Points[series.Points.Count - 1].ToolTip =
                            $"{data:dd MMM yyyy}\n{wydano:N0} kg";
                    }
                }

                chartTrend.Series.Add(series);
                chartTrend.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MM";
                chartTrend.ChartAreas[0].AxisY.LabelStyle.Format = "N0";
                chartTrend.ChartAreas[0].AxisX.Title = "Data";
                chartTrend.ChartAreas[0].AxisY.Title = "Wydano (kg)";
            }
        }

        private void LoadTopProdukty(DateTime od, DateTime doDaty)
        {
            chartProdukty.Series.Clear();

            string query = @"
                SELECT TOP 10
                    CASE 
                        WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                        WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                        WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                        WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                        WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                        WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                        WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                        WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                        ELSE MZ.kod
                    END AS Produkt,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = 65552
                AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                AND MG.[Data] BETWEEN @Od AND @Do
                GROUP BY 
                    CASE 
                        WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                        WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                        WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                        WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                        WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                        WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                        WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                        WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                        ELSE MZ.kod
                    END
                ORDER BY Wydano DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Od", od);
                cmd.Parameters.AddWithValue("@Do", doDaty);

                Series series = new Series("Produkty")
                {
                    ChartType = SeriesChartType.Bar,
                    Palette = ChartColorPalette.BrightPastel
                };

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string produkt = reader.GetString(0);
                        double wydano = Convert.ToDouble(reader[1]);
                        var point = series.Points.AddXY(produkt, wydano);
                        series.Points[series.Points.Count - 1].ToolTip = $"{produkt}\n{wydano:N0} kg";
                        series.Points[series.Points.Count - 1].Label = $"{wydano:N0}";
                    }
                }

                chartProdukty.Series.Add(series);
                chartProdukty.ChartAreas[0].AxisX.Interval = 1;
                chartProdukty.ChartAreas[0].AxisY.LabelStyle.Format = "N0";
            }
        }

        private void UpdateKartyStatystyk(DateTime od, DateTime doDaty)
        {
            string query = @"
                SELECT
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto,
                    COUNT(DISTINCT MG.[Data]) AS Dni
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = 65552
                AND (mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')
                AND MG.[Data] BETWEEN @Od AND @Do";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Od", od);
                cmd.Parameters.AddWithValue("@Do", doDaty);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        decimal wydano = Convert.ToDecimal(reader[0]);
                        decimal przyjeto = Convert.ToDecimal(reader[1]);
                        int dni = Convert.ToInt32(reader[2]);
                        decimal srednia = dni > 0 ? wydano / dni : 0;

                        lblWydano.Text = $"{wydano:N0} kg";
                        lblPrzyjeto.Text = $"{przyjeto:N0} kg";
                        lblSrednia.Text = $"{srednia:N0} kg";
                        lblPodsumowanie.Text = $"{dni} dni analizy";
                    }
                }
            }
        }

        private void BtnStanMagazynu_Click(object sender, EventArgs e)
        {
            DateTime dataStan = dtpStanMagazynu.Value.Date;
            statusLabel.Text = "⏳ Obliczam stan magazynu...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                string query = @"
                    WITH StanMagazynu AS (
                        SELECT
                            CASE 
                                WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                                WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                                WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                                WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                                WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                                WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                                WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                                WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                                ELSE MZ.kod
                            END AS Produkt,
                            SUM(MZ.ilosc) AS Stan
                        FROM [HANDEL].[HM].[MG]
                        JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                        WHERE MG.magazyn = 65552
                        AND MG.[Data] <= @DataStan
                        GROUP BY 
                            CASE 
                                WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                                WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                                WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                                WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                                WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                                WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                                WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                                WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                                ELSE MZ.kod
                            END
                    )
                    SELECT 
                        Produkt,
                        Stan AS [Stan (kg)],
                        CASE 
                            WHEN Stan < 0 THEN '⚠️ Ujemny'
                            WHEN Stan < 100 THEN '🔴 Krytyczny'
                            WHEN Stan < 500 THEN '🟡 Niski'
                            WHEN Stan < 1500 THEN '🟢 Dobry'
                            ELSE '🟢 Wysoki'
                        END AS Status
                    FROM StanMagazynu
                    WHERE Stan != 0
                    ORDER BY Stan DESC";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                    adapter.SelectCommand.Parameters.AddWithValue("@DataStan", dataStan);

                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    dgvStanMagazynu.DataSource = dt;

                    FormatujKolumne(dgvStanMagazynu, "Stan (kg)", "Stan (kg)", "N0");

                    // Kolorowanie statusów
                    foreach (DataGridViewRow row in dgvStanMagazynu.Rows)
                    {
                        string status = row.Cells["Status"].Value?.ToString();
                        if (status?.Contains("Krytyczny") == true || status?.Contains("Ujemny") == true)
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230);
                        else if (status?.Contains("Niski") == true)
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 200);
                        else if (status?.Contains("Wysoki") == true)
                            row.DefaultCellStyle.BackColor = Color.FromArgb(230, 255, 230);
                    }
                }

                statusLabel.Text = $"✅ Stan magazynu na {dataStan:yyyy-MM-dd}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "❌ Błąd obliczania stanu";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void BtnSzybkiRaport_Click(object sender, EventArgs e)
        {
            if (cachedDzienneData == null || cachedDzienneData.Rows.Count == 0)
            {
                MessageBox.Show("Najpierw załaduj dane klikając 'Analizuj'", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Tworzenie szybkiego raportu tekstowego
            StringBuilder raport = new StringBuilder();
            raport.AppendLine("╔═══════════════════════════════════════════════════════╗");
            raport.AppendLine("║          RAPORT MROŹNI - SZYBKIE PODSUMOWANIE        ║");
            raport.AppendLine("╚═══════════════════════════════════════════════════════╝");
            raport.AppendLine();
            raport.AppendLine($"Data raportu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            raport.AppendLine($"Okres analizy: {dtpOd.Value:yyyy-MM-dd} - {dtpDo.Value:yyyy-MM-dd}");
            raport.AppendLine();
            raport.AppendLine("─────────────────────────────────────────────────────────");
            raport.AppendLine($"Wydano:          {lblWydano.Text}");
            raport.AppendLine($"Przyjęto:        {lblPrzyjeto.Text}");
            raport.AppendLine($"Średnio/dzień:   {lblSrednia.Text}");
            raport.AppendLine($"Status:          {lblPodsumowanie.Text}");
            raport.AppendLine("─────────────────────────────────────────────────────────");

            Form raportForm = new Form
            {
                Text = "Szybki raport",
                Size = new Size(700, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = BackgroundColor
            };

            TextBox txtRaport = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10F),
                Text = raport.ToString(),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20)
            };

            Panel btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = BackgroundColor };
            Button btnZamknij = CreateModernButton("Zamknij", 10, 15, 120, DangerColor);
            btnZamknij.Click += (s, ev) => raportForm.Close();

            Button btnKopiuj = CreateModernButton("📋 Kopiuj", 140, 15, 120, PrimaryColor);
            btnKopiuj.Click += (s, ev) => {
                Clipboard.SetText(txtRaport.Text);
                MessageBox.Show("Raport skopiowany do schowka!", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnPanel.Controls.AddRange(new Control[] { btnZamknij, btnKopiuj });

            raportForm.Controls.Add(txtRaport);
            raportForm.Controls.Add(btnPanel);
            raportForm.ShowDialog();
        }

        private void BtnEksport_Click(object sender, EventArgs e)
        {
            if (dgvAnaliza.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu. Najpierw kliknij 'Analizuj'.",
                    "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt";
                sfd.FileName = $"Mroznia_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        ExportToCSV(dgvAnaliza, sfd.FileName);

                        if (MessageBox.Show($"Dane zostały wyeksportowane!\n\nCzy otworzyć plik?",
                            "Eksport zakończony", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                        }

                        statusLabel.Text = $"✅ Wyeksportowano do: {Path.GetFileName(sfd.FileName)}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportToCSV(DataGridView dgv, string filePath)
        {
            StringBuilder csv = new StringBuilder();

            // Nagłówki
            var headers = dgv.Columns.Cast<DataGridViewColumn>()
                .Select(col => col.HeaderText);
            csv.AppendLine(string.Join(",", headers));

            // Dane
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;

                var cells = row.Cells.Cast<DataGridViewCell>()
                    .Select(cell => $"\"{cell.Value?.ToString().Replace("\"", "\"\"")}\"");
                csv.AppendLine(string.Join(",", cells));
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        // ============================================
        // FUNKCJE POMOCNICZE
        // ============================================

        private void FormatujKolumne(DataGridView dgv, string kolumna, string nagłowek, string format = null)
        {
            if (dgv.Columns[kolumna] != null)
            {
                dgv.Columns[kolumna].HeaderText = nagłowek;
                if (!string.IsNullOrEmpty(format))
                    dgv.Columns[kolumna].DefaultCellStyle.Format = format;
            }
        }

        private void AplikujFiltr()
        {
            if (dgvAnaliza.DataSource == null) return;

            DataTable dt = (DataTable)dgvAnaliza.DataSource;
            string filtr = "";

            if (cmbFiltrProduktu.SelectedIndex > 0)
            {
                string wybranyProdukt = cmbFiltrProduktu.SelectedItem.ToString().Replace("Wszystkie produkty", "");
                if (!string.IsNullOrEmpty(wybranyProdukt))
                    filtr = $"Produkt LIKE '%{wybranyProdukt}%'";
            }

            if (!string.IsNullOrWhiteSpace(txtSzukaj.Text))
            {
                string szukaj = txtSzukaj.Text.Replace("'", "''");
                string filterSzukaj = $"Produkt LIKE '%{szukaj}%'";
                filtr = string.IsNullOrEmpty(filtr) ? filterSzukaj : $"{filtr} AND {filterSzukaj}";
            }

            dt.DefaultView.RowFilter = filtr;
        }

        private void ResetujFiltry()
        {
            cmbFiltrProduktu.SelectedIndex = 0;
            txtSzukaj.Clear();
        }

        private void CmbPredkosc_SelectedIndexChanged(object sender, EventArgs e)
        {
            DateTime teraz = DateTime.Now;

            switch (cmbPredkosc.SelectedIndex)
            {
                case 1: // Dziś
                    dtpOd.Value = teraz;
                    dtpDo.Value = teraz;
                    break;
                case 2: // Wczoraj
                    dtpOd.Value = teraz.AddDays(-1);
                    dtpDo.Value = teraz.AddDays(-1);
                    break;
                case 3: // Ostatnie 7 dni
                    dtpOd.Value = teraz.AddDays(-7);
                    dtpDo.Value = teraz;
                    break;
                case 4: // Ostatnie 30 dni
                    dtpOd.Value = teraz.AddDays(-30);
                    dtpDo.Value = teraz;
                    break;
                case 5: // Bieżący miesiąc
                    dtpOd.Value = new DateTime(teraz.Year, teraz.Month, 1);
                    dtpDo.Value = teraz;
                    break;
                case 6: // Poprzedni miesiąc
                    DateTime poprzedniMiesiac = teraz.AddMonths(-1);
                    dtpOd.Value = new DateTime(poprzedniMiesiac.Year, poprzedniMiesiac.Month, 1);
                    dtpDo.Value = new DateTime(poprzedniMiesiac.Year, poprzedniMiesiac.Month,
                        DateTime.DaysInMonth(poprzedniMiesiac.Year, poprzedniMiesiac.Month));
                    break;
                case 7: // Ostatnie 3 miesiące
                    dtpOd.Value = teraz.AddMonths(-3);
                    dtpDo.Value = teraz;
                    break;
            }
        }

        private void DisableButtons()
        {
            btnAnalizuj.Enabled = false;
            btnWykres.Enabled = false;
            btnStanMagazynu.Enabled = false;
            btnSzybkiRaport.Enabled = false;
            btnEksport.Enabled = false;
        }

        private void EnableButtons()
        {
            btnAnalizuj.Enabled = true;
            btnWykres.Enabled = true;
            btnStanMagazynu.Enabled = true;
            btnSzybkiRaport.Enabled = true;
            btnEksport.Enabled = true;
        }
    }
}