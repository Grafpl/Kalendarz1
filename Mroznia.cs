﻿using Microsoft.Data.SqlClient;
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
        private DataGridView dgvDzienne, dgvAnaliza, dgvStanMagazynu;
        private TabControl tabControl;
        private ComboBox cmbFiltrProduktu, cmbPredkosc, cmbWykresTyp;
        private TextBox txtSzukaj;
        private Label lblPodsumowanie, lblWydano, lblPrzyjeto, lblSrednia, lblTrendInfo;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripProgressBar progressBar;
        private Chart chartTrend, chartProdukty;
        private Panel panelKarty;
        private ToolTip toolTip;
        private CheckBox chkPokazWydane, chkPokazPrzyjete, chkGrupowanie;

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
            this.Text = "Mroźnia - System Analityczny PRO";
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
            btnAnalizuj = CreateModernButton("Analizuj", 575, 12, 110, PrimaryColor);
            btnWykres = CreateModernButton("Wykresy", 695, 12, 100, SuccessColor);
            btnStanMagazynu = CreateModernButton("Stan", 805, 12, 90, WarningColor);
            btnSzybkiRaport = CreateModernButton("Raport", 905, 12, 100, InfoColor);
            btnEksport = CreateModernButton("Eksport", 1015, 12, 100, DangerColor);

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
            Panel card1 = CreateStatCard("WYDANO", "0 kg", PrimaryColor, 0);
            lblWydano = (Label)card1.Controls[1];

            // Karta 2 - Przyjęto
            Panel card2 = CreateStatCard("PRZYJĘTO", "0 kg", SuccessColor, 1);
            lblPrzyjeto = (Label)card2.Controls[1];

            // Karta 3 - Średnia dzienna
            Panel card3 = CreateStatCard("ŚREDNIO/DZIEŃ", "0 kg", WarningColor, 2);
            lblSrednia = (Label)card3.Controls[1];

            // Karta 4 - Podsumowanie
            Panel card4 = CreateStatCard("STATUS", "Wybierz okres", InfoColor, 3);
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

            // === ZAKŁADKA 1: DZIENNE PRZEGLĄD (PEŁNA TABELA) ===
            TabPage tab1 = new TabPage("  Przegląd dzienny  ");
            tab1.BackColor = BackgroundColor;
            tab1.Padding = new Padding(10);

            Panel dziennyPanel = new Panel { Dock = DockStyle.Fill };

            // Górny panel z informacją
            Panel infoPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = CardColor };
            infoPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, infoPanel);

            Label lblInfo = new Label
            {
                Text = "Kliknij dwukrotnie na wiersz aby zobaczyć szczegółowe pozycje dnia w nowym oknie",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = PrimaryColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0)
            };
            infoPanel.Controls.Add(lblInfo);

            dgvDzienne = CreateStyledDataGridView();
            dgvDzienne.DoubleClick += DgvDzienne_DoubleClick;

            Panel gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            gridPanel.Controls.Add(dgvDzienne);

            dziennyPanel.Controls.Add(gridPanel);
            dziennyPanel.Controls.Add(infoPanel);
            tab1.Controls.Add(dziennyPanel);

            // === ZAKŁADKA 2: ANALIZA PRODUKTÓW ===
            TabPage tab2 = new TabPage("  Analiza produktów  ");
            tab2.BackColor = BackgroundColor;
            tab2.Padding = new Padding(10);

            Panel analizaPanel = new Panel { Dock = DockStyle.Fill };

            // Panel filtrowania
            Panel filterPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = CardColor };
            filterPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, filterPanel);

            Label lblFiltr = CreateLabel("Filtruj:", 15, 16, true);
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

            btnResetFiltr = CreateModernButton("Reset", 570, 11, 80, SecondaryTextColor);

            filterPanel.Controls.AddRange(new Control[] { lblFiltr, cmbFiltrProduktu, lblSzukaj, txtSzukaj, btnResetFiltr });

            dgvAnaliza = CreateStyledDataGridView();
            Panel gridPanel2 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            gridPanel2.Controls.Add(dgvAnaliza);

            analizaPanel.Controls.Add(gridPanel2);
            analizaPanel.Controls.Add(filterPanel);
            tab2.Controls.Add(analizaPanel);

            // === ZAKŁADKA 3: WYKRESY INTERAKTYWNE ===
            TabPage tab3 = new TabPage("  Wykresy interaktywne  ");
            tab3.BackColor = BackgroundColor;
            tab3.Padding = new Padding(10);

            Panel chartsMainPanel = new Panel { Dock = DockStyle.Fill };

            // Panel kontroli wykresu
            Panel chartControlPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = CardColor };
            chartControlPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, chartControlPanel);

            Label lblWykresOpcje = CreateLabel("Pokaż na wykresie:", 15, 10, true);

            chkPokazWydane = new CheckBox
            {
                Text = "Wydane",
                Location = new Point(15, 32),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = PrimaryColor
            };
            chkPokazWydane.CheckedChanged += UpdateWykres;

            chkPokazPrzyjete = new CheckBox
            {
                Text = "Przyjęte",
                Location = new Point(130, 32),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = SuccessColor
            };
            chkPokazPrzyjete.CheckedChanged += UpdateWykres;

            Label lblTypWykresu = CreateLabel("Typ wykresu:", 260, 10, true);
            cmbWykresTyp = new ComboBox
            {
                Location = new Point(260, 30),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbWykresTyp.Items.AddRange(new object[] { "Liniowy", "Obszarowy", "Słupkowy", "Punktowy" });
            cmbWykresTyp.SelectedIndex = 0;
            cmbWykresTyp.SelectedIndexChanged += UpdateWykres;

            Button btnResetZoom = CreateModernButton("Reset Zoom", 430, 26, 120, InfoColor);
            btnResetZoom.Click += (s, e) => ResetChartZoom();

            chartControlPanel.Controls.AddRange(new Control[] {
                lblWykresOpcje, chkPokazWydane, chkPokazPrzyjete,
                lblTypWykresu, cmbWykresTyp, btnResetZoom
            });

            // WAŻNE: Dodawanie w odwrotnej kolejności - Top, Bottom, Fill
            chartsMainPanel.Controls.Add(chartControlPanel); // Najpierw Top

            // Dolny panel - Top produkty
            Panel bottomChartPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 300,
                BackColor = CardColor,
                Padding = new Padding(5, 10, 5, 5)
            };
            bottomChartPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, bottomChartPanel);

            chartProdukty = CreateStyledChart("Top 10 produktów");
            bottomChartPanel.Controls.Add(chartProdukty);
            chartsMainPanel.Controls.Add(bottomChartPanel); // Potem Bottom

            // Wykres trend z scroll
            Panel chartPanel1 = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardColor,
                Padding = new Padding(5, 10, 5, 10),
                MinimumSize = new Size(0, 200) // Minimalna wysokość
            };
            chartPanel1.Paint += (s, e) => DrawCardBorder(e.Graphics, chartPanel1);

            chartTrend = CreateInteractiveChart("Trend wydań i przyjęć w czasie");
            chartPanel1.Controls.Add(chartTrend);
            chartsMainPanel.Controls.Add(chartPanel1); // Na końcu Fill

            tab3.Controls.Add(chartsMainPanel);

            // === ZAKŁADKA 4: STAN MAGAZYNU ===
            TabPage tab4 = new TabPage("  Stan magazynu  ");
            tab4.BackColor = BackgroundColor;
            tab4.Padding = new Padding(10);

            Panel stanPanel = new Panel { Dock = DockStyle.Fill };

            Panel stanHeader = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = CardColor };
            stanHeader.Paint += (s, e) => DrawCardBorder(e.Graphics, stanHeader);

            Label lblInfoStan = new Label
            {
                Text = "Kliknij dwukrotnie na produkt aby zobaczyć jego historię ruchu",
                Location = new Point(15, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = PrimaryColor
            };

            Label lblStanNa = CreateLabel("Stan na dzień:", 15, 28, true);
            dtpStanMagazynu = new DateTimePicker
            {
                Location = new Point(115, 25),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };

            // Checkbox niewidoczny ale odznaczony (dla zachowania kompatybilności)
            chkGrupowanie = new CheckBox
            {
                Checked = false,
                Visible = false
            };

            Button btnObliczStan = CreateModernButton("Oblicz stan", 280, 23, 110, PrimaryColor);
            btnObliczStan.Click += BtnStanMagazynu_Click;

            stanHeader.Controls.AddRange(new Control[] { lblInfoStan, lblStanNa, dtpStanMagazynu, chkGrupowanie, btnObliczStan });

            dgvStanMagazynu = CreateStyledDataGridView();
            dgvStanMagazynu.DoubleClick += DgvStanMagazynu_DoubleClick;
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

        private Chart CreateInteractiveChart(string title)
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

            // Włącz scrollowanie i zoom
            area.CursorX.IsUserEnabled = true;
            area.CursorX.IsUserSelectionEnabled = true;
            area.CursorX.AutoScroll = true;
            area.AxisX.ScaleView.Zoomable = true;
            area.AxisX.ScrollBar.IsPositionedInside = true;
            area.AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;
            area.AxisX.ScrollBar.Size = 15;

            area.CursorY.IsUserEnabled = true;
            area.CursorY.IsUserSelectionEnabled = true;
            area.AxisY.ScaleView.Zoomable = true;

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

            cmbFiltrProduktu.SelectedIndexChanged += (s, e) => AplikujFiltr();
            txtSzukaj.TextChanged += (s, e) => AplikujFiltr();

            this.Load += Mroznia_Load;
        }

        private void Mroznia_Load(object sender, EventArgs e)
        {
            dtpOd.Value = DateTime.Now.AddDays(-30);
            dtpDo.Value = DateTime.Now;

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

            statusLabel.Text = "Ładowanie danych...";
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
                statusLabel.Text = $"Dane załadowane pomyślnie | {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Błąd ładowania danych";
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

                FormatujKolumne(dgvDzienne, "Data", "Data", "yyyy-MM-dd");
                FormatujKolumne(dgvDzienne, "DzienTygodnia", "Dzień");
                FormatujKolumne(dgvDzienne, "Wydano", "Wydano (kg)", "N0");
                FormatujKolumne(dgvDzienne, "Przyjeto", "Przyjęto (kg)", "N0");
                FormatujKolumne(dgvDzienne, "Bilans", "Bilans (kg)", "N0");
                FormatujKolumne(dgvDzienne, "Pozycje", "Pozycje");

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

        private void DgvDzienne_DoubleClick(object sender, EventArgs e)
        {
            if (dgvDzienne.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvDzienne.SelectedRows[0];
            if (row.Cells["Data"].Value == null) return;

            DateTime wybranaData = Convert.ToDateTime(row.Cells["Data"].Value);
            decimal wydano = Convert.ToDecimal(row.Cells["Wydano"].Value);
            decimal przyjeto = Convert.ToDecimal(row.Cells["Przyjeto"].Value);

            ShowSzczegolyDniaModal(wybranaData, wydano, przyjeto);
        }

        private void DgvStanMagazynu_DoubleClick(object sender, EventArgs e)
        {
            if (dgvStanMagazynu.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvStanMagazynu.SelectedRows[0];

            // Sprawdź czy istnieje kolumna "Produkt" czy "Kod"
            string columnName = dgvStanMagazynu.Columns.Contains("Produkt") ? "Produkt" : "Kod";

            if (row.Cells[columnName].Value == null) return;

            string produkt = row.Cells[columnName].Value.ToString();

            // Pomiń wiersz sumy
            if (produkt == "SUMA CAŁKOWITA") return;

            decimal stan = Convert.ToDecimal(row.Cells["Stan (kg)"].Value);
            decimal wartosc = Convert.ToDecimal(row.Cells["Wartość (zł)"].Value);

            ShowHistoriaProduktuModal(produkt, stan, wartosc, dtpStanMagazynu.Value.Date);
        }

        private void ShowSzczegolyDniaModal(DateTime data, decimal wydano, decimal przyjeto)
        {
            Form modalForm = new Form
            {
                Text = $"Szczegóły dnia: {data:yyyy-MM-dd dddd}",
                Size = new Size(1200, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = BackgroundColor,
                MinimizeBox = false,
                MaximizeBox = true,
                ShowInTaskbar = false
            };

            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = CardColor,
                Padding = new Padding(15)
            };
            headerPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, headerPanel);

            Label lblNaglowek = new Label
            {
                Text = $"Szczegółowe pozycje z dnia {data:yyyy-MM-dd} ({data:dddd})",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = PrimaryColor,
                Location = new Point(15, 10),
                AutoSize = true
            };

            Label lblPodsumowanie = new Label
            {
                Text = $"Wydano: {wydano:N0} kg  |  Przyjęto: {przyjeto:N0} kg  |  Bilans: {(wydano - przyjeto):N0} kg",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = SecondaryTextColor,
                Location = new Point(15, 42),
                AutoSize = true
            };

            headerPanel.Controls.AddRange(new Control[] { lblNaglowek, lblPodsumowanie });

            DataGridView dgvModal = CreateStyledDataGridView();
            Panel gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 15, 5)
            };
            gridPanel.Controls.Add(dgvModal);

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = BackgroundColor,
                Padding = new Padding(15, 10, 15, 10)
            };

            Button btnEksportModal = CreateModernButton("Eksportuj", 10, 10, 130, SuccessColor);
            btnEksportModal.Click += (s, e) => ExportModalData(dgvModal, data);

            Button btnWykresModal = CreateModernButton("Wykres", 150, 10, 130, InfoColor);
            btnWykresModal.Click += (s, e) => ShowModalChart(dgvModal, data);

            Button btnZamknij = CreateModernButton("Zamknij", 1040, 10, 130, DangerColor);
            btnZamknij.Click += (s, e) => modalForm.Close();

            bottomPanel.Controls.AddRange(new Control[] { btnEksportModal, btnWykresModal, btnZamknij });

            LoadSzczegolyDniaDoGrid(data, dgvModal);

            modalForm.Controls.Add(gridPanel);
            modalForm.Controls.Add(headerPanel);
            modalForm.Controls.Add(bottomPanel);

            modalForm.ShowDialog(this);
        }

        private void LoadSzczegolyDniaDoGrid(DateTime data, DataGridView dgv)
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
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) - 
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Roznica,
                    COUNT(*) AS Operacje
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

                dgv.DataSource = dt;

                FormatujKolumne(dgv, "Wydano", "Wydano (kg)", "N0");
                FormatujKolumne(dgv, "Przyjeto", "Przyjęto (kg)", "N0");
                FormatujKolumne(dgv, "Roznica", "Różnica (kg)", "N0");
                FormatujKolumne(dgv, "Operacje", "Liczba operacji");

                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.Cells["Roznica"].Value != null)
                    {
                        decimal roznica = Convert.ToDecimal(row.Cells["Roznica"].Value);
                        row.Cells["Roznica"].Style.ForeColor = roznica < 0 ? SuccessColor : DangerColor;
                    }
                }
            }
        }

        private void ExportModalData(DataGridView dgv, DateTime data)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV Files (*.csv)|*.csv";
                sfd.FileName = $"Szczegoly_{data:yyyyMMdd}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        ExportToCSV(dgv, sfd.FileName);
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

        private void ShowModalChart(DataGridView dgv, DateTime data)
        {
            if (dgv.Rows.Count == 0) return;

            Form chartForm = new Form
            {
                Text = $"Wykres produktów - {data:yyyy-MM-dd}",
                Size = new Size(1000, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = BackgroundColor
            };

            Chart chart = CreateStyledChart($"Produkty z dnia {data:yyyy-MM-dd}");

            Series series = new Series("Wydano")
            {
                ChartType = SeriesChartType.Column,
                Palette = ChartColorPalette.BrightPastel
            };

            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                string produkt = row.Cells["Produkt"].Value?.ToString();
                decimal wydano = Convert.ToDecimal(row.Cells["Wydano"].Value);

                var point = series.Points.AddXY(produkt, wydano);
                series.Points[series.Points.Count - 1].Label = $"{wydano:N0}";
            }

            chart.Series.Add(series);
            chart.ChartAreas[0].AxisX.Interval = 1;
            chart.ChartAreas[0].AxisX.LabelStyle.Angle = -45;

            chartForm.Controls.Add(chart);
            chartForm.ShowDialog();
        }

        private void ShowHistoriaProduktuModal(string produkt, decimal stan, decimal wartosc, DateTime dataDo)
        {
            Form modalForm = new Form
            {
                Text = $"Historia produktu: {produkt}",
                Size = new Size(1400, 750),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = BackgroundColor,
                MinimizeBox = false,
                MaximizeBox = true,
                ShowInTaskbar = false
            };

            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = CardColor,
                Padding = new Padding(15)
            };
            headerPanel.Paint += (s, e) => DrawCardBorder(e.Graphics, headerPanel);

            Label lblNaglowek = new Label
            {
                Text = $"Historia ruchu produktu: {produkt}",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = PrimaryColor,
                Location = new Point(15, 10),
                AutoSize = true
            };

            Label lblPodsumowanie = new Label
            {
                Text = $"Stan na dzień {dataDo:yyyy-MM-dd}: {stan:N0} kg  |  Wartość: {wartosc:N0} zł  |  Cena śr.: {(stan > 0 ? wartosc / stan : 0):N2} zł/kg",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = SecondaryTextColor,
                Location = new Point(15, 42),
                AutoSize = true
            };

            Label lblInfo = new Label
            {
                Text = "Ostatnie 50 operacji magazynowych dla tego produktu",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = SecondaryTextColor,
                Location = new Point(15, 70),
                AutoSize = true
            };

            headerPanel.Controls.AddRange(new Control[] { lblNaglowek, lblPodsumowanie, lblInfo });

            DataGridView dgvModal = CreateStyledDataGridView();
            Panel gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 15, 5)
            };
            gridPanel.Controls.Add(dgvModal);

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = BackgroundColor,
                Padding = new Padding(15, 10, 15, 10)
            };

            Button btnEksportModal = CreateModernButton("Eksportuj", 10, 10, 130, SuccessColor);
            btnEksportModal.Click += (s, e) => ExportModalData(dgvModal, dataDo);

            Button btnWykresModal = CreateModernButton("Wykres trendu", 150, 10, 150, InfoColor);
            btnWykresModal.Click += (s, e) => ShowHistoriaWykres(produkt, dataDo);

            Button btnZamknij = CreateModernButton("Zamknij", 1240, 10, 130, DangerColor);
            btnZamknij.Click += (s, e) => modalForm.Close();

            bottomPanel.Controls.AddRange(new Control[] { btnEksportModal, btnWykresModal, btnZamknij });

            LoadHistoriaProduktuDoGrid(produkt, dataDo, dgvModal);

            modalForm.Controls.Add(gridPanel);
            modalForm.Controls.Add(headerPanel);
            modalForm.Controls.Add(bottomPanel);

            modalForm.ShowDialog(this);
        }

        private void LoadHistoriaProduktuDoGrid(string produkt, DateTime dataDo, DataGridView dgv)
        {
            string query = @"
                WITH HistoriaOperacji AS (
                    SELECT
                        MZ.[Data] AS Data,
                        MZ.kod AS KodSzczegolowy,
                        MZ.ilosc AS Operacja,
                        MZ.wartNetto AS Wartosc,
                        MZ.id,
                        CASE 
                            WHEN MZ.ilosc < 0 THEN 'Przyjęcie'
                            ELSE 'Wydanie'
                        END AS Typ
                    FROM [HANDEL].[HM].[MZ]
                    WHERE MZ.magazyn = 65552
                    AND MZ.[Data] <= @DataDo
                    AND MZ.typ = '0'
                    AND (
                        MZ.kod = @Produkt
                        OR MZ.kod LIKE @ProduktPattern
                    )
                )
                SELECT TOP 50
                    Data,
                    KodSzczegolowy AS [Kod szczegółowy],
                    Operacja AS [Operacja (kg)],
                    ABS(SUM(Operacja) OVER (ORDER BY Data, id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)) AS [Stan po operacji (kg)],
                    Wartosc AS [Wartość (zł)],
                    Typ
                FROM HistoriaOperacji
                ORDER BY Data DESC, id DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                adapter.SelectCommand.Parameters.AddWithValue("@DataDo", dataDo);
                adapter.SelectCommand.Parameters.AddWithValue("@Produkt", produkt);
                adapter.SelectCommand.Parameters.AddWithValue("@ProduktPattern", produkt + "%");

                DataTable dt = new DataTable();
                adapter.Fill(dt);

                dgv.DataSource = dt;

                FormatujKolumne(dgv, "Data", "Data", "yyyy-MM-dd");
                FormatujKolumne(dgv, "Operacja (kg)", "Operacja (kg)", "#,##0.00");
                FormatujKolumne(dgv, "Stan po operacji (kg)", "Stan po operacji (kg)", "#,##0.00");
                FormatujKolumne(dgv, "Wartość (zł)", "Wartość (zł)", "#,##0.00");

                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.Cells["Operacja (kg)"].Value != null)
                    {
                        decimal operacja = Convert.ToDecimal(row.Cells["Operacja (kg)"].Value);
                        // Minus = Przyjęcie (zielony), Plus = Wydanie (czerwony)
                        if (operacja < 0)
                            row.Cells["Operacja (kg)"].Style.ForeColor = SuccessColor;
                        else
                            row.Cells["Operacja (kg)"].Style.ForeColor = DangerColor;
                    }
                }
            }
        }

        private void ShowHistoriaWykres(string produkt, DateTime dataDo)
        {
            Form chartForm = new Form
            {
                Text = $"Wykres trendu - {produkt}",
                Size = new Size(1200, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = BackgroundColor
            };

            Chart chart = CreateInteractiveChart($"Trend stanu magazynowego: {produkt}");

            string query = @"
                SELECT 
                    MZ.[Data] AS Data,
                    MZ.iloscwp AS Stan
                FROM [HANDEL].[HM].[MZ]
                WHERE MZ.magazyn = 65552
                AND MZ.[Data] <= @DataDo
                AND MZ.typ = '0'
                AND (
                    MZ.kod = @Produkt
                    OR MZ.kod LIKE @ProduktPattern
                )
                AND MZ.[Data] >= DATEADD(MONTH, -6, @DataDo)
                ORDER BY MZ.[Data], MZ.id";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@DataDo", dataDo);
                cmd.Parameters.AddWithValue("@Produkt", produkt);
                cmd.Parameters.AddWithValue("@ProduktPattern", produkt + "%");

                Series seriesStan = new Series("Stan magazynowy")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 3,
                    Color = Color.FromArgb(41, 128, 185),
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerColor = Color.FromArgb(41, 128, 185)
                };

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime data = reader.GetDateTime(0);
                        double stan = Convert.ToDouble(reader[1]);

                        seriesStan.Points.AddXY(data, stan);
                        seriesStan.Points[seriesStan.Points.Count - 1].ToolTip =
                            $"{data:dd MMM yyyy}\nStan: {stan:N2} kg";
                    }
                }

                chart.Series.Add(seriesStan);
                chart.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MM";
                chart.ChartAreas[0].AxisY.LabelStyle.Format = "N0";
                chart.ChartAreas[0].AxisX.Title = "Data";
                chart.ChartAreas[0].AxisY.Title = "Stan (kg)";

                Series seriesZero = new Series("Poziom zerowy")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    Color = Color.Red,
                    BorderDashStyle = ChartDashStyle.Dash
                };
                if (seriesStan.Points.Count > 0)
                {
                    seriesZero.Points.AddXY(seriesStan.Points[0].XValue, 0);
                    seriesZero.Points.AddXY(seriesStan.Points[seriesStan.Points.Count - 1].XValue, 0);
                    chart.Series.Add(seriesZero);
                }
            }

            chartForm.Controls.Add(chart);
            chartForm.ShowDialog();
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
                    CASE WHEN DniAktywnosci > 0 THEN Wydano / DniAktywnosci ELSE 0 END AS [Śr/dzień]
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

                foreach (DataGridViewRow row in dgvAnaliza.Rows)
                {
                    if (row.Cells["Roznica"].Value != null)
                    {
                        decimal roznica = Convert.ToDecimal(row.Cells["Roznica"].Value);
                        row.Cells["Roznica"].Style.ForeColor = roznica < 0 ? SuccessColor : DangerColor;
                    }
                }
            }
        }

        private void LoadTrendy(DateTime od, DateTime doDaty)
        {
            chartTrend.Series.Clear();

            string query = @"
                SELECT
                    MG.[Data] AS Data,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto
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

                Series seriesWydano = new Series("Wydane")
                {
                    ChartType = GetChartTypeFromComboBox(),
                    BorderWidth = 3,
                    Color = Color.FromArgb(150, 41, 128, 185),
                    BorderColor = Color.FromArgb(41, 128, 185),
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerColor = Color.FromArgb(41, 128, 185)
                };

                Series seriesPrzyjeto = new Series("Przyjęte")
                {
                    ChartType = GetChartTypeFromComboBox(),
                    BorderWidth = 3,
                    Color = Color.FromArgb(150, 16, 124, 16),
                    BorderColor = Color.FromArgb(16, 124, 16),
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerColor = Color.FromArgb(16, 124, 16)
                };

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime data = reader.GetDateTime(0);
                        double wydano = Convert.ToDouble(reader[1]);
                        double przyjeto = Convert.ToDouble(reader[2]);

                        if (chkPokazWydane.Checked)
                        {
                            seriesWydano.Points.AddXY(data, wydano);
                            seriesWydano.Points[seriesWydano.Points.Count - 1].ToolTip =
                                $"Wydano\n{data:dd MMM yyyy}\n{wydano:N0} kg";
                        }

                        if (chkPokazPrzyjete.Checked)
                        {
                            seriesPrzyjeto.Points.AddXY(data, przyjeto);
                            seriesPrzyjeto.Points[seriesPrzyjeto.Points.Count - 1].ToolTip =
                                $"Przyjęto\n{data:dd MMM yyyy}\n{przyjeto:N0} kg";
                        }
                    }
                }

                if (chkPokazWydane.Checked)
                    chartTrend.Series.Add(seriesWydano);
                if (chkPokazPrzyjete.Checked)
                    chartTrend.Series.Add(seriesPrzyjeto);

                chartTrend.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MM";
                chartTrend.ChartAreas[0].AxisY.LabelStyle.Format = "N0";
                chartTrend.ChartAreas[0].AxisX.Title = "Data";
                chartTrend.ChartAreas[0].AxisY.Title = "Ilość (kg)";
            }
        }

        private SeriesChartType GetChartTypeFromComboBox()
        {
            if (cmbWykresTyp == null) return SeriesChartType.Spline;

            switch (cmbWykresTyp.SelectedIndex)
            {
                case 0: return SeriesChartType.Spline;
                case 1: return SeriesChartType.SplineArea;
                case 2: return SeriesChartType.Column;
                case 3: return SeriesChartType.Point;
                default: return SeriesChartType.Spline;
            }
        }

        private void UpdateWykres(object sender, EventArgs e)
        {
            if (cachedDzienneData == null || cachedDzienneData.Rows.Count == 0) return;

            DateTime od = dtpOd.Value.Date;
            DateTime doDaty = dtpDo.Value.Date;
            LoadTrendy(od, doDaty);
        }

        private void ResetChartZoom()
        {
            chartTrend.ChartAreas[0].AxisX.ScaleView.ZoomReset();
            chartTrend.ChartAreas[0].AxisY.ScaleView.ZoomReset();
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
            DateTime dataPoprzedni = dataStan.AddDays(-7); // Tydzień wcześniej
            bool isGrupowanie = chkGrupowanie.Checked; // Zawsze false (checkbox niewidoczny)

            statusLabel.Text = "Obliczam stan magazynu...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                // Zapytanie dla aktualnego stanu - CAST na DECIMAL zamiast ROUND (który zwraca FLOAT)
                string query = @"
                    SELECT kod, 
                           CAST(ABS(SUM([iloscwp])) AS DECIMAL(18,3)) AS SumaIlosc, 
                           CAST(ABS(SUM([wartNetto])) AS DECIMAL(18,2)) AS SumaWartosc 
                    FROM [HANDEL].[HM].[MZ] 
                    WHERE [data] >= '2020-01-07' 
                      AND [data] <= @EndDate
                      AND [magazyn] = @Magazyn 
                      AND typ = '0' 
                    GROUP BY kod 
                    HAVING ABS(SUM([iloscwp])) <> 0 
                    ORDER BY SumaIlosc DESC";

                // Zapytanie dla stanu tydzień wcześniej
                string queryPoprzedni = @"
                    SELECT kod, 
                           CAST(ABS(SUM([iloscwp])) AS DECIMAL(18,3)) AS SumaIlosc
                    FROM [HANDEL].[HM].[MZ] 
                    WHERE [data] >= '2020-01-07' 
                      AND [data] <= @EndDate
                      AND [magazyn] = @Magazyn 
                      AND typ = '0' 
                    GROUP BY kod 
                    HAVING ABS(SUM([iloscwp])) <> 0";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz aktualny stan
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@EndDate", dataStan);
                    cmd.Parameters.AddWithValue("@Magazyn", "65552");

                    DataTable dtRaw = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dtRaw);
                    }

                    // Pobierz stan z tygodnia wcześniej
                    SqlCommand cmdPoprzedni = new SqlCommand(queryPoprzedni, conn);
                    cmdPoprzedni.Parameters.AddWithValue("@EndDate", dataPoprzedni);
                    cmdPoprzedni.Parameters.AddWithValue("@Magazyn", "65552");

                    DataTable dtPoprzedni = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmdPoprzedni))
                    {
                        adapter.Fill(dtPoprzedni);
                    }

                    DataTable dtFinal;

                    if (isGrupowanie)
                    {
                        // Grupowanie produktów
                        var grouped = dtRaw.AsEnumerable()
                            .GroupBy(row => GrupujProdukt(row.Field<string>("kod")))
                            .Select(g => new
                            {
                                Produkt = g.Key,
                                Stan = g.Sum(row => Convert.ToDecimal(row["SumaIlosc"])),
                                Wartosc = g.Sum(row => Convert.ToDecimal(row["SumaWartosc"]))
                            })
                            .OrderByDescending(x => x.Stan);

                        // Grupuj poprzedni stan
                        var groupedPoprzedni = dtPoprzedni.AsEnumerable()
                            .GroupBy(row => GrupujProdukt(row.Field<string>("kod")))
                            .ToDictionary(
                                g => g.Key,
                                g => g.Sum(row => Convert.ToDecimal(row["SumaIlosc"]))
                            );

                        dtFinal = new DataTable();
                        dtFinal.Columns.Add("Produkt", typeof(string));
                        dtFinal.Columns.Add("Stan (kg)", typeof(decimal));
                        dtFinal.Columns.Add("Wartość (zł)", typeof(decimal));
                        dtFinal.Columns.Add("Cena śr. (zł/kg)", typeof(decimal));
                        dtFinal.Columns.Add("Zmiana", typeof(string));
                        dtFinal.Columns.Add("Status", typeof(string));

                        foreach (var item in grouped)
                        {
                            decimal cena = item.Stan > 0 ? item.Wartosc / item.Stan : 0;
                            string status = GetStatus(item.Stan);

                            // Oblicz zmianę z tygodnia wcześniej
                            decimal stanPoprzedni = groupedPoprzedni.ContainsKey(item.Produkt)
                                ? groupedPoprzedni[item.Produkt]
                                : 0;
                            decimal roznica = item.Stan - stanPoprzedni;
                            string zmiana = GetZmianaStrzalka(roznica, item.Stan);

                            dtFinal.Rows.Add(item.Produkt, item.Stan, item.Wartosc, cena, zmiana, status);
                        }
                    }
                    else
                    {
                        // Bez grupowania - szczegółowe kody
                        // Słownik dla poprzedniego stanu
                        var dictPoprzedni = dtPoprzedni.AsEnumerable()
                            .ToDictionary(
                                row => row.Field<string>("kod"),
                                row => Convert.ToDecimal(row["SumaIlosc"])
                            );

                        dtFinal = new DataTable();
                        dtFinal.Columns.Add("Kod", typeof(string));
                        dtFinal.Columns.Add("Stan (kg)", typeof(decimal));
                        dtFinal.Columns.Add("Wartość (zł)", typeof(decimal));
                        dtFinal.Columns.Add("Cena śr. (zł/kg)", typeof(decimal));
                        dtFinal.Columns.Add("Zmiana", typeof(string));
                        dtFinal.Columns.Add("Status", typeof(string));

                        foreach (DataRow row in dtRaw.Rows)
                        {
                            string kod = row["kod"].ToString();
                            decimal stan = Convert.ToDecimal(row["SumaIlosc"]);
                            decimal wartosc = Convert.ToDecimal(row["SumaWartosc"]);
                            decimal cena = stan > 0 ? wartosc / stan : 0;
                            string status = GetStatus(stan);

                            // Oblicz zmianę
                            decimal stanPoprzedni = dictPoprzedni.ContainsKey(kod) ? dictPoprzedni[kod] : 0;
                            decimal roznica = stan - stanPoprzedni;
                            string zmiana = GetZmianaStrzalka(roznica, stan);

                            dtFinal.Rows.Add(kod, stan, wartosc, cena, zmiana, status);
                        }
                    }

                    dgvStanMagazynu.DataSource = dtFinal;

                    // Formatowanie
                    string colName = isGrupowanie ? "Produkt" : "Kod";
                    FormatujKolumne(dgvStanMagazynu, "Stan (kg)", "Stan (kg)", "N0");
                    FormatujKolumne(dgvStanMagazynu, "Wartość (zł)", "Wartość (zł)", "N0");
                    FormatujKolumne(dgvStanMagazynu, "Cena śr. (zł/kg)", "Cena śr. (zł/kg)", "N2");
                    FormatujKolumne(dgvStanMagazynu, "Zmiana", "Zmiana (7 dni)");

                    // Kolorowanie
                    foreach (DataGridViewRow row in dgvStanMagazynu.Rows)
                    {
                        string status = row.Cells["Status"].Value?.ToString();
                        Color backgroundColor = Color.White;

                        if (status == "Krytyczny")
                            backgroundColor = Color.FromArgb(255, 200, 200); // Czerwony
                        else if (status == "Poważny")
                            backgroundColor = Color.FromArgb(255, 255, 200); // Żółty
                        else if (status == "Dobry")
                            backgroundColor = Color.White; // Biały

                        row.DefaultCellStyle.BackColor = backgroundColor;
                        // WAŻNE: Zachowaj kolor tła także przy zaznaczeniu
                        row.DefaultCellStyle.SelectionBackColor = backgroundColor;
                        row.DefaultCellStyle.SelectionForeColor = TextColor;

                        // Koloruj kolumnę zmiany
                        string zmiana = row.Cells["Zmiana"].Value?.ToString();
                        if (zmiana?.Contains("↑") == true)
                            row.Cells["Zmiana"].Style.ForeColor = Color.Red;
                        else if (zmiana?.Contains("↓") == true)
                            row.Cells["Zmiana"].Style.ForeColor = Color.Green;
                        else
                            row.Cells["Zmiana"].Style.ForeColor = Color.Gray;
                    }

                    // Dodaj wiersz sumy
                    decimal sumaStan = dtFinal.AsEnumerable().Sum(r => Convert.ToDecimal(r["Stan (kg)"]));
                    decimal sumaWartosc = dtFinal.AsEnumerable().Sum(r => Convert.ToDecimal(r["Wartość (zł)"]));

                    DataRow sumRow = dtFinal.NewRow();
                    sumRow[colName] = "SUMA CAŁKOWITA";
                    sumRow["Stan (kg)"] = sumaStan;
                    sumRow["Wartość (zł)"] = sumaWartosc;
                    sumRow["Cena śr. (zł/kg)"] = sumaStan > 0 ? sumaWartosc / sumaStan : 0;
                    sumRow["Zmiana"] = "";
                    sumRow["Status"] = GetStatus(sumaStan);
                    dtFinal.Rows.Add(sumRow);

                    // Pogrubienie wiersza sumy
                    int lastRowIndex = dgvStanMagazynu.Rows.Count - 1;
                    dgvStanMagazynu.Rows[lastRowIndex].DefaultCellStyle.Font =
                        new Font("Segoe UI", 10F, FontStyle.Bold);
                    dgvStanMagazynu.Rows[lastRowIndex].DefaultCellStyle.BackColor =
                        Color.FromArgb(200, 220, 240);
                }

                statusLabel.Text = $"Stan magazynu na {dataStan:yyyy-MM-dd} (porównanie z {dataPoprzedni:yyyy-MM-dd})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}\n\nSzczegóły: {ex.StackTrace}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Błąd obliczania stanu";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private string GetZmianaStrzalka(decimal roznica, decimal stanAktualny)
        {
            if (roznica == 0)
                return "→ bez zmian";

            // Oblicz procent zmiany bezpiecznie
            decimal stanPoprzedni = stanAktualny - roznica;
            decimal procentZmiany = 0;

            if (stanPoprzedni != 0 && stanPoprzedni > 0)
            {
                procentZmiany = (roznica / stanPoprzedni) * 100;
            }

            if (roznica > 0)
                return $"↑ +{roznica:N0} kg ({procentZmiany:+0.0}%)";
            else
                return $"↓ {roznica:N0} kg ({procentZmiany:0.0}%)";
        }

        private string GrupujProdukt(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return "Nieznany";

            if (kod.StartsWith("Kurczak A")) return "Kurczak A";
            if (kod.StartsWith("Korpus")) return "Korpus";
            if (kod.StartsWith("Ćwiartka")) return "Ćwiartka";
            if (kod.StartsWith("Filet II")) return "Filet II";
            if (kod.StartsWith("Filet")) return "Filet A";
            if (kod.StartsWith("Skrzydło I")) return "Skrzydło I";
            if (kod.StartsWith("Trybowane bez skóry")) return "Trybowane bez skóry";
            if (kod.StartsWith("Trybowane ze skórą")) return "Trybowane ze skórą";
            if (kod.StartsWith("Żołądki")) return "Żołądki";
            if (kod.StartsWith("Serca")) return "Serca";
            if (kod.StartsWith("Wątroba")) return "Wątroba";

            return kod;
        }

        private string GetStatus(decimal stan)
        {
            if (stan <= 5000) return "Dobry";
            if (stan <= 14999) return "Poważny";
            return "Krytyczny";
        }

        private void BtnSzybkiRaport_Click(object sender, EventArgs e)
        {
            if (cachedDzienneData == null || cachedDzienneData.Rows.Count == 0)
            {
                MessageBox.Show("Najpierw załaduj dane klikając 'Analizuj'", "Brak danych",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StringBuilder raport = new StringBuilder();
            raport.AppendLine("===================================");
            raport.AppendLine("   RAPORT MROŹNI - PODSUMOWANIE   ");
            raport.AppendLine("===================================");
            raport.AppendLine();
            raport.AppendLine($"Data raportu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            raport.AppendLine($"Okres analizy: {dtpOd.Value:yyyy-MM-dd} - {dtpDo.Value:yyyy-MM-dd}");
            raport.AppendLine();
            raport.AppendLine("-----------------------------------");
            raport.AppendLine($"Wydano:          {lblWydano.Text}");
            raport.AppendLine($"Przyjęto:        {lblPrzyjeto.Text}");
            raport.AppendLine($"Średnio/dzień:   {lblSrednia.Text}");
            raport.AppendLine($"Status:          {lblPodsumowanie.Text}");
            raport.AppendLine("-----------------------------------");

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

            Button btnKopiuj = CreateModernButton("Kopiuj", 140, 15, 120, PrimaryColor);
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

                        statusLabel.Text = $"Wyeksportowano do: {Path.GetFileName(sfd.FileName)}";
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

            var headers = dgv.Columns.Cast<DataGridViewColumn>()
                .Select(col => col.HeaderText);
            csv.AppendLine(string.Join(",", headers));

            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;

                var cells = row.Cells.Cast<DataGridViewCell>()
                    .Select(cell => $"\"{cell.Value?.ToString().Replace("\"", "\"\"")}\"");
                csv.AppendLine(string.Join(",", cells));
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

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