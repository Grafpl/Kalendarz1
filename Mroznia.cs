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
using System.Text.Json;

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
        private Button btnAnalizuj, btnWykres, btnStanMagazynu, btnEksport, btnResetFiltr, btnSzybkiRaport, btnMapowanie;
        private DataGridView dgvDzienne, dgvAnaliza, dgvStanMagazynu, dgvZamowienia;
        private DataGridView dgvMroznieZewnetrzne, dgvWydaniaZewnetrzne;
        private TabControl tabControl;
        private ComboBox cmbFiltrProduktu, cmbPredkosc, cmbWykresTyp;
        private TextBox txtSzukaj;
        private Label lblPodsumowanie, lblWydano, lblPrzyjeto, lblSrednia, lblTrendInfo;
        private Label lblStanSuma, lblStanWartosc, lblStanProdukty, lblStanRezerwacje;
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

        private Panel CreateStanStatCard(string title, string value, Color accentColor, int position)
        {
            int cardWidth = 280;
            int margin = 15;

            Panel card = new Panel
            {
                Location = new Point(position * (cardWidth + margin) + 10, 10),
                Size = new Size(cardWidth, 70),
                BackColor = CardColor,
                Cursor = Cursors.Default
            };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Gradient tła
                using (var brush = new LinearGradientBrush(
                    card.ClientRectangle,
                    Color.White,
                    Color.FromArgb(250, 250, 255),
                    LinearGradientMode.Vertical))
                {
                    using (var path = GetRoundedRectangle(card.ClientRectangle, 10))
                    {
                        g.FillPath(brush, path);
                    }
                }

                // Lewa krawędź kolorowa
                using (var brush = new SolidBrush(accentColor))
                {
                    g.FillRectangle(brush, 0, 8, 4, card.Height - 16);
                }

                // Border
                using (var path = GetRoundedRectangle(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                {
                    g.DrawPath(pen, path);
                }
            };

            Label lblTitle = new Label
            {
                Text = title,
                Location = new Point(15, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = SecondaryTextColor,
                BackColor = Color.Transparent
            };

            Label lblValue = new Label
            {
                Text = value,
                Location = new Point(15, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = Color.Transparent
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

            // === ZAKŁADKA 4: STAN MAGAZYNU (KOMPAKTOWY LAYOUT) ===
            TabPage tab4 = new TabPage("  Stan mroźni  ");
            tab4.BackColor = BackgroundColor;
            tab4.Padding = new Padding(8);

            // Główny layout: Toolbar + SplitContainer
            Panel stanMainPanel = new Panel { Dock = DockStyle.Fill };

            // === TOOLBAR (nowoczesny) ===
            Panel stanToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.White };
            stanToolbar.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    e.Graphics.DrawLine(pen, 0, stanToolbar.Height - 1, stanToolbar.Width, stanToolbar.Height - 1);
            };

            dtpStanMagazynu = new DateTimePicker
            {
                Location = new Point(15, 12),
                Width = 110,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now,
                Font = new Font("Segoe UI", 9F)
            };
            dtpStanMagazynu.ValueChanged += (s, e) => BtnStanMagazynu_Click(null, null);

            btnMapowanie = CreateModernButton("Mapowanie", 135, 10, 85, InfoColor);
            btnMapowanie.Click += BtnMapowanie_Click;

            // Statystyki inline - bardziej widoczne
            lblStanSuma = new Label
            {
                Text = "Stan: 0 kg",
                Location = new Point(240, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = PrimaryColor
            };

            lblStanRezerwacje = new Label
            {
                Text = "Zarezerwowano: 0 kg",
                Location = new Point(380, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = DangerColor
            };

            lblStanWartosc = new Label { Visible = false };
            lblStanProdukty = new Label { Visible = false };
            chkGrupowanie = new CheckBox { Checked = false, Visible = false };

            stanToolbar.Controls.AddRange(new Control[] {
                dtpStanMagazynu, btnMapowanie,
                lblStanSuma, lblStanRezerwacje, lblStanWartosc, lblStanProdukty, chkGrupowanie
            });

            // === SPLITCONTAINER: LEWA (Stan) + PRAWA (Rezerwacje) ===
            SplitContainer splitStan = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                BackColor = BackgroundColor
            };
            // Ustaw podział 50/50 po załadowaniu
            splitStan.SizeChanged += (s, e) => {
                if (splitStan.Width > 0)
                    splitStan.SplitterDistance = splitStan.Width / 2;
            };

            // --- LEWA STRONA: STAN MAGAZYNU ---
            Panel leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };

            Panel lblLeftHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = PrimaryColor
            };
            Label lblLeftTitle = new Label
            {
                Text = "STAN MAGAZYNU",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblLeftHeader.Controls.Add(lblLeftTitle);

            dgvStanMagazynu = CreateStyledDataGridView();
            dgvStanMagazynu.DoubleClick += DgvStanMagazynu_DoubleClick;
            dgvStanMagazynu.MouseClick += DgvStanMagazynu_MouseClick;
            dgvStanMagazynu.CellClick += DgvStanMagazynu_CellClick;

            // Menu kontekstowe
            ContextMenuStrip ctxMenuStan = new ContextMenuStrip();
            ctxMenuStan.Items.Add("Rezerwuj", null, (s, e) => RezerwujWybranyProdukt());
            ctxMenuStan.Items.Add("Usuń rezerwację", null, (s, e) => UsunRezerwacjeProduktu());
            ctxMenuStan.Items.Add(new ToolStripSeparator());
            ctxMenuStan.Items.Add("Historia", null, (s, e) => PokazHistorieWybranegoProduktu());
            dgvStanMagazynu.ContextMenuStrip = ctxMenuStan;

            Panel leftGridPanel = new Panel { Dock = DockStyle.Fill };
            leftGridPanel.Controls.Add(dgvStanMagazynu);

            leftPanel.Controls.Add(leftGridPanel);
            leftPanel.Controls.Add(lblLeftHeader);

            // --- PRAWA STRONA: REZERWACJE ---
            Panel rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };

            Panel lblRightHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = DangerColor
            };
            Label lblRightTitle = new Label
            {
                Text = "REZERWACJE (2x klik = anuluj)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblRightHeader.Controls.Add(lblRightTitle);

            dgvZamowienia = CreateStyledDataGridView();
            dgvZamowienia.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 53, 69);
            dgvZamowienia.CellDoubleClick += DgvRezerwacje_CellDoubleClick;

            Panel rightGridPanel = new Panel { Dock = DockStyle.Fill };
            rightGridPanel.Controls.Add(dgvZamowienia);

            rightPanel.Controls.Add(rightGridPanel);
            rightPanel.Controls.Add(lblRightHeader);

            splitStan.Panel1.Controls.Add(leftPanel);
            splitStan.Panel2.Controls.Add(rightPanel);

            stanMainPanel.Controls.Add(splitStan);
            stanMainPanel.Controls.Add(stanToolbar);

            tab4.Controls.Add(stanMainPanel);

            // === ZAKŁADKA 5: MROŹNIE ZEWNĘTRZNE ===
            TabPage tab5 = new TabPage("  Mroźnie zewnętrzne  ");
            tab5.BackColor = BackgroundColor;
            tab5.Padding = new Padding(8);

            Panel zewnMainPanel = new Panel { Dock = DockStyle.Fill };

            // Toolbar
            Panel zewnToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.White };
            zewnToolbar.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    e.Graphics.DrawLine(pen, 0, zewnToolbar.Height - 1, zewnToolbar.Width, zewnToolbar.Height - 1);
            };

            Button btnDodajMroznieZewn = CreateModernButton("+ Dodaj mroźnię", 15, 10, 120, SuccessColor);
            btnDodajMroznieZewn.Click += BtnDodajMroznieZewnetrzna_Click;

            Button btnEdytujMroznieZewn = CreateModernButton("Edytuj", 145, 10, 70, InfoColor);
            btnEdytujMroznieZewn.Click += BtnEdytujMroznieZewnetrzna_Click;

            Button btnUsunMroznieZewn = CreateModernButton("Usuń", 225, 10, 60, DangerColor);
            btnUsunMroznieZewn.Click += BtnUsunMroznieZewnetrzna_Click;

            Button btnDodajWydanieZewn = CreateModernButton("+ Wydanie/Przyjęcie", 310, 10, 140, PrimaryColor);
            btnDodajWydanieZewn.Click += BtnDodajWydanieZewnetrzne_Click;

            zewnToolbar.Controls.AddRange(new Control[] { btnDodajMroznieZewn, btnEdytujMroznieZewn, btnUsunMroznieZewn, btnDodajWydanieZewn });

            // Split: Lista mroźni | Szczegóły/Wydania
            SplitContainer splitZewn = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                BackColor = BackgroundColor
            };
            splitZewn.SizeChanged += (s, e) => {
                if (splitZewn.Width > 0)
                    splitZewn.SplitterDistance = splitZewn.Width / 3;
            };

            // Lewa: Lista mroźni
            Panel zewnLeftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Panel zewnLeftHeader = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = SuccessColor };
            Label lblZewnLeft = new Label
            {
                Text = "LISTA MROŹNI ZEWNĘTRZNYCH",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            zewnLeftHeader.Controls.Add(lblZewnLeft);

            dgvMroznieZewnetrzne = CreateStyledDataGridView();
            dgvMroznieZewnetrzne.ColumnHeadersDefaultCellStyle.BackColor = SuccessColor;
            dgvMroznieZewnetrzne.SelectionChanged += DgvMroznieZewnetrzne_SelectionChanged;

            Panel zewnLeftGrid = new Panel { Dock = DockStyle.Fill };
            zewnLeftGrid.Controls.Add(dgvMroznieZewnetrzne);
            zewnLeftPanel.Controls.Add(zewnLeftGrid);
            zewnLeftPanel.Controls.Add(zewnLeftHeader);

            // Prawa: Szczegóły i wydania
            Panel zewnRightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Panel zewnRightHeader = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = PrimaryColor };
            Label lblZewnRight = new Label
            {
                Text = "STAN I WYDANIA MROŹNI",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            zewnRightHeader.Controls.Add(lblZewnRight);

            dgvWydaniaZewnetrzne = CreateStyledDataGridView();
            dgvWydaniaZewnetrzne.ColumnHeadersDefaultCellStyle.BackColor = PrimaryColor;

            Panel zewnRightGrid = new Panel { Dock = DockStyle.Fill };
            zewnRightGrid.Controls.Add(dgvWydaniaZewnetrzne);
            zewnRightPanel.Controls.Add(zewnRightGrid);
            zewnRightPanel.Controls.Add(zewnRightHeader);

            splitZewn.Panel1.Controls.Add(zewnLeftPanel);
            splitZewn.Panel2.Controls.Add(zewnRightPanel);

            zewnMainPanel.Controls.Add(splitZewn);
            zewnMainPanel.Controls.Add(zewnToolbar);
            tab5.Controls.Add(zewnMainPanel);

            // Stan magazynu jako pierwsza zakładka
            tc.TabPages.AddRange(new TabPage[] { tab4, tab5, tab1, tab2, tab3 });
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
            btnWykres.Click += (s, e) => tabControl.SelectedIndex = 3;
            btnStanMagazynu.Click += (s, e) => tabControl.SelectedIndex = 0;
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

            // Automatycznie oblicz stan magazynu przy starcie (pierwsza zakładka)
            BtnStanMagazynu_Click(null, null);

            // Załaduj mroźnie zewnętrzne
            LoadMroznieZewnetrzneDoTabeli();
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
            if (produkt == "SUMA") return;

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
                        // Wczytaj mapowania świeży -> mrożony
                        var mapowania = WczytajMapowaniaSwiezyMrozony();

                        // Słownik dla poprzedniego stanu (z uwzględnieniem mapowania)
                        var dictPoprzedniRaw = dtPoprzedni.AsEnumerable()
                            .ToDictionary(
                                row => row.Field<string>("kod"),
                                row => Convert.ToDecimal(row["SumaIlosc"])
                            );

                        // Scal dane poprzednie wg mapowania
                        var dictPoprzedni = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in dictPoprzedniRaw)
                        {
                            string kodDocelowy = mapowania.ContainsKey(kvp.Key) ? mapowania[kvp.Key] : kvp.Key;
                            if (!dictPoprzedni.ContainsKey(kodDocelowy))
                                dictPoprzedni[kodDocelowy] = 0;
                            dictPoprzedni[kodDocelowy] += kvp.Value;
                        }

                        // Słownik do sumowania scalonych produktów (kod mrożony -> (stan, wartość, kodyŹródłowe))
                        var scaloneDane = new Dictionary<string, (decimal Stan, decimal Wartosc, List<string> KodyZrodlowe)>(StringComparer.OrdinalIgnoreCase);

                        foreach (DataRow row in dtRaw.Rows)
                        {
                            string kodOryginalny = row["kod"].ToString();
                            decimal stan = Convert.ToDecimal(row["SumaIlosc"]);
                            decimal wartosc = Convert.ToDecimal(row["SumaWartosc"]);

                            // Sprawdź czy ten kod jest świeży i ma mapowanie na mrożony
                            string kodDocelowy = mapowania.ContainsKey(kodOryginalny) ? mapowania[kodOryginalny] : kodOryginalny;

                            if (!scaloneDane.ContainsKey(kodDocelowy))
                                scaloneDane[kodDocelowy] = (0, 0, new List<string>());

                            var dane = scaloneDane[kodDocelowy];
                            dane.Stan += stan;
                            dane.Wartosc += wartosc;
                            if (!dane.KodyZrodlowe.Contains(kodOryginalny))
                                dane.KodyZrodlowe.Add(kodOryginalny);
                            scaloneDane[kodDocelowy] = dane;
                        }

                        dtFinal = new DataTable();
                        dtFinal.Columns.Add("Kod", typeof(string));
                        dtFinal.Columns.Add("Stan (kg)", typeof(decimal));
                        dtFinal.Columns.Add("Rez. (kg)", typeof(decimal));
                        dtFinal.Columns.Add("Zmiana", typeof(string));
                        dtFinal.Columns.Add("Status", typeof(string));

                        // Pobierz rezerwacje per produkt
                        var rezerwacjePerProdukt = GetRezerwacjePoProduktach();

                        // Sortuj po stanie malejąco
                        foreach (var kvp in scaloneDane.OrderByDescending(x => x.Value.Stan))
                        {
                            string kod = kvp.Key;
                            decimal stan = kvp.Value.Stan;
                            string status = GetStatus(stan);

                            // Oblicz zmianę
                            decimal stanPoprzedni = dictPoprzedni.ContainsKey(kod) ? dictPoprzedni[kod] : 0;
                            decimal roznica = stan - stanPoprzedni;
                            string zmiana = GetZmianaStrzalka(roznica, stan);

                            // Pobierz rezerwację dla produktu
                            decimal rezerwacja = rezerwacjePerProdukt.ContainsKey(kod) ? rezerwacjePerProdukt[kod] : 0;

                            dtFinal.Rows.Add(kod, stan, rezerwacja, zmiana, status);
                        }
                    }

                    dgvStanMagazynu.DataSource = dtFinal;

                    // Formatowanie
                    string colName = isGrupowanie ? "Produkt" : "Kod";
                    FormatujKolumne(dgvStanMagazynu, "Stan (kg)", "Stan", "N0");
                    FormatujKolumne(dgvStanMagazynu, "Rez. (kg)", "Rez.", "N0");
                    FormatujKolumne(dgvStanMagazynu, "Zmiana", "Zmiana");

                    // Ukryj kolumnę Status
                    if (dgvStanMagazynu.Columns.Contains("Status"))
                        dgvStanMagazynu.Columns["Status"].Visible = false;

                    // Kolorowanie
                    foreach (DataGridViewRow row in dgvStanMagazynu.Rows)
                    {
                        string status = row.Cells["Status"].Value?.ToString();
                        Color backgroundColor = Color.White;

                        if (status == "Krytyczny")
                            backgroundColor = Color.FromArgb(255, 230, 230);
                        else if (status == "Poważny")
                            backgroundColor = Color.FromArgb(255, 250, 230);

                        row.DefaultCellStyle.BackColor = backgroundColor;
                        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 220, 255);
                        row.DefaultCellStyle.SelectionForeColor = TextColor;

                        // Koloruj kolumnę zmiany
                        string zmiana = row.Cells["Zmiana"].Value?.ToString();
                        if (zmiana?.Contains("↑") == true)
                            row.Cells["Zmiana"].Style.ForeColor = Color.FromArgb(220, 53, 69);
                        else if (zmiana?.Contains("↓") == true)
                            row.Cells["Zmiana"].Style.ForeColor = Color.FromArgb(40, 167, 69);
                        else
                            row.Cells["Zmiana"].Style.ForeColor = Color.Gray;

                        // Koloruj kolumnę Rezerwacja jeśli są zarezerwowane kg
                        if (dgvStanMagazynu.Columns.Contains("Rez. (kg)"))
                        {
                            var rezVal = row.Cells["Rez. (kg)"].Value;
                            if (rezVal != null && rezVal != DBNull.Value)
                            {
                                decimal rez = Convert.ToDecimal(rezVal);
                                if (rez > 0)
                                {
                                    row.Cells["Rez. (kg)"].Style.ForeColor = Color.FromArgb(220, 53, 69);
                                    row.Cells["Rez. (kg)"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                                }
                            }
                        }
                    }

                    // Oblicz sumy
                    decimal sumaStan = dtFinal.AsEnumerable().Sum(r => Convert.ToDecimal(r["Stan (kg)"]));
                    decimal sumaRezerwacja = dtFinal.Columns.Contains("Rez. (kg)")
                        ? dtFinal.AsEnumerable().Sum(r => Convert.ToDecimal(r["Rez. (kg)"]))
                        : 0;

                    // Wstaw wiersz sumy NA GÓRĘ tabeli
                    DataRow sumRow = dtFinal.NewRow();
                    sumRow[colName] = "SUMA";
                    sumRow["Stan (kg)"] = sumaStan;
                    if (dtFinal.Columns.Contains("Rez. (kg)"))
                        sumRow["Rez. (kg)"] = sumaRezerwacja;
                    sumRow["Zmiana"] = "";
                    sumRow["Status"] = "";
                    dtFinal.Rows.InsertAt(sumRow, 0);

                    // Pogrubienie wiersza sumy (pierwszy wiersz)
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.Font =
                        new Font("Segoe UI", 10F, FontStyle.Bold);
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.BackColor =
                        Color.FromArgb(41, 128, 185);
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.ForeColor = Color.White;
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.SelectionBackColor =
                        Color.FromArgb(41, 128, 185);

                    // Aktualizuj karty statystyk
                    int liczbaProdukow = dtFinal.Rows.Count - 1;
                    UpdateStanStatystyki(sumaStan, 0, liczbaProdukow);

                    // Załaduj rezerwacje w prawym panelu
                    LoadRezerwacjeDoTabeli();
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

        /// <summary>
        /// Wczytuje mapowania świeży-mrożony z pliku JSON
        /// </summary>
        private Dictionary<string, string> WczytajMapowaniaSwiezyMrozony()
        {
            var mapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string plikMapowan = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OfertaHandlowa", "mapowania_swiezy_mrozony.json");

                if (File.Exists(plikMapowan))
                {
                    string json = File.ReadAllText(plikMapowan);
                    var lista = JsonSerializer.Deserialize<List<MapowanieItem>>(json);
                    if (lista != null)
                    {
                        foreach (var item in lista)
                        {
                            if (!string.IsNullOrEmpty(item.KodSwiezy) && !string.IsNullOrEmpty(item.KodMrozony))
                            {
                                mapowanie[item.KodSwiezy] = item.KodMrozony;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania mapowań: {ex.Message}");
            }
            return mapowanie;
        }

        /// <summary>
        /// Otwiera okno mapowania produktów świeżych na mrożone
        /// </summary>
        private void BtnMapowanie_Click(object sender, EventArgs e)
        {
            try
            {
                // Wczytaj produkty świeże i mrożone z bazy
                var towarySwiezy = new List<OfertaCenowa.TowarOferta>();
                var towaryMrozone = new List<OfertaCenowa.TowarOferta>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT Id, Kod, Nazwa, katalog
                                    FROM [HANDEL].[HM].[TW]
                                    WHERE katalog IN ('67095', '67153')
                                    ORDER BY Kod ASC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var towar = new OfertaCenowa.TowarOferta
                            {
                                Id = rd.GetInt32(0),
                                Kod = rd["Kod"]?.ToString() ?? "",
                                Nazwa = rd["Nazwa"]?.ToString() ?? "",
                                Katalog = rd["katalog"]?.ToString() ?? ""
                            };

                            if (towar.Katalog == "67095")
                                towarySwiezy.Add(towar);
                            else if (towar.Katalog == "67153")
                                towaryMrozone.Add(towar);
                        }
                    }
                }

                // Otwórz okno WPF mapowania
                var okno = new OfertaCenowa.MapowanieSwiezyMrozonyWindow(towarySwiezy, towaryMrozone);
                okno.ShowDialog();

                statusLabel.Text = "Mapowanie zaktualizowane. Kliknij 'Oblicz stan' aby odświeżyć.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania okna mapowania: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region System Rezerwacji i Zamówień

        private string GetRezerwacjePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OfertaHandlowa", "rezerwacje_mroznia.json");
        }

        private List<RezerwacjaItem> WczytajRezerwacje()
        {
            try
            {
                string path = GetRezerwacjePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var lista = JsonSerializer.Deserialize<List<RezerwacjaItem>>(json);
                    return lista?.Where(r => r.DataWaznosci >= DateTime.Today).ToList() ?? new List<RezerwacjaItem>();
                }
            }
            catch { }
            return new List<RezerwacjaItem>();
        }

        private void ZapiszRezerwacje(List<RezerwacjaItem> rezerwacje)
        {
            try
            {
                string path = GetRezerwacjePath();
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(rezerwacje, options));
            }
            catch { }
        }

        private string GetCurrentHandlowiec()
        {
            // Pobierz handlowca z App.UserID lub Environment.UserName
            string userId = App.UserID ?? Environment.UserName;
            var handlowcy = UserHandlowcyManager.GetUserHandlowcy(userId);
            return handlowcy.FirstOrDefault() ?? App.UserFullName ?? userId;
        }

        private void LoadRezerwacjeDoTabeli()
        {
            try
            {
                var rezerwacje = WczytajRezerwacje();

                // Filtruj przeterminowane rezerwacje
                var aktywne = rezerwacje.Where(r => r.DataWaznosci >= DateTime.Today).ToList();
                var przeterminowane = rezerwacje.Where(r => r.DataWaznosci < DateTime.Today).ToList();

                // Usuń przeterminowane
                if (przeterminowane.Any())
                {
                    ZapiszRezerwacje(aktywne);
                    statusLabel.Text = $"Usunięto {przeterminowane.Count} przeterminowanych rezerwacji";
                }

                DataTable dt = new DataTable();
                dt.Columns.Add("ID", typeof(string));
                dt.Columns.Add("Produkt", typeof(string));
                dt.Columns.Add("Ilość kg", typeof(decimal));
                dt.Columns.Add("Handlowiec", typeof(string));
                dt.Columns.Add("Ważna do", typeof(DateTime));
                dt.Columns.Add("Uwagi", typeof(string));

                foreach (var r in aktywne.OrderBy(x => x.DataWaznosci))
                {
                    dt.Rows.Add(r.Id, r.KodProduktu, r.Ilosc, r.Handlowiec, r.DataWaznosci, r.Uwagi);
                }

                dgvZamowienia.DataSource = dt;
                dgvZamowienia.Columns["ID"].Visible = false;
                dgvZamowienia.Columns["Ilość kg"].DefaultCellStyle.Format = "N0";
                dgvZamowienia.Columns["Ważna do"].DefaultCellStyle.Format = "dd.MM.yyyy";

                // Koloruj wiersze bliskie wygaśnięciu
                foreach (DataGridViewRow row in dgvZamowienia.Rows)
                {
                    var waznosc = row.Cells["Ważna do"].Value;
                    if (waznosc != null && waznosc != DBNull.Value)
                    {
                        DateTime dataWaznosci = Convert.ToDateTime(waznosc);
                        if (dataWaznosci <= DateTime.Today.AddDays(1))
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200); // Czerwony - wygasa dziś/jutro
                        else if (dataWaznosci <= DateTime.Today.AddDays(3))
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 200); // Żółty - wygasa za 2-3 dni
                    }
                }

                // Aktualizuj etykietę rezerwacji
                decimal sumaRez = aktywne.Sum(r => r.Ilosc);
                lblStanRezerwacje.Text = $"Zarezerwowano: {sumaRez:N0} kg";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Błąd ładowania rezerwacji: {ex.Message}";
            }
        }

        private void DgvRezerwacje_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvZamowienia.Rows[e.RowIndex];
            string id = row.Cells["ID"].Value?.ToString() ?? "";
            string produkt = row.Cells["Produkt"].Value?.ToString() ?? "";
            decimal ilosc = Convert.ToDecimal(row.Cells["Ilość kg"].Value ?? 0);
            string handlowiec = row.Cells["Handlowiec"].Value?.ToString() ?? "";

            var result = MessageBox.Show(
                $"Czy na pewno chcesz anulować rezerwację?\n\nProdukt: {produkt}\nIlość: {ilosc:N0} kg\nHandlowiec: {handlowiec}",
                "Anuluj rezerwację",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                var rezerwacje = WczytajRezerwacje();
                rezerwacje.RemoveAll(r => r.Id == id);
                ZapiszRezerwacje(rezerwacje);

                // Odśwież widok
                BtnStanMagazynu_Click(null, null);
                statusLabel.Text = $"Anulowano rezerwację {produkt} ({ilosc:N0} kg)";
            }
        }

        private void DgvStanMagazynu_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Pozwól na zaznaczenie wiersza
        }

        private void RezerwujWybranyProdukt()
        {
            RezerwujProdukt(GetCurrentHandlowiec(), "");
        }

        private void RezerwujProdukt(string handlowiec, string uwagi)
        {
            if (dgvStanMagazynu.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvStanMagazynu.SelectedRows[0];
            string kodProduktu = row.Cells["Kod"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(kodProduktu) || kodProduktu == "SUMA") return;

            decimal stanAktualny = Convert.ToDecimal(row.Cells["Stan (kg)"].Value ?? 0);
            var rezerwacje = WczytajRezerwacje();
            decimal juzZarezerwowano = rezerwacje.Where(r => r.KodProduktu == kodProduktu && r.DataWaznosci >= DateTime.Today).Sum(r => r.Ilosc);
            decimal dostepne = stanAktualny - juzZarezerwowano;

            if (dostepne <= 0)
            {
                MessageBox.Show("Brak dostępnego towaru do rezerwacji.", "Brak towaru", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Dialog rezerwacji z datą ważności
            using (var dlg = new RezerwacjaInputDialog(kodProduktu, dostepne, handlowiec))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var nowaRezerwacja = new RezerwacjaItem
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                        KodProduktu = kodProduktu,
                        Ilosc = dlg.Ilosc,
                        Handlowiec = handlowiec,
                        DataRezerwacji = DateTime.Now,
                        DataWaznosci = dlg.DataWaznosci,
                        Uwagi = uwagi
                    };

                    rezerwacje.Add(nowaRezerwacja);
                    ZapiszRezerwacje(rezerwacje);

                    // Odśwież widok
                    BtnStanMagazynu_Click(null, null);

                    statusLabel.Text = $"Zarezerwowano {dlg.Ilosc:N0} kg {kodProduktu} dla {handlowiec} (ważne do {dlg.DataWaznosci:dd.MM.yyyy})";
                }
            }
        }

        private void UsunRezerwacjeProduktu()
        {
            if (dgvStanMagazynu.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvStanMagazynu.SelectedRows[0];
            string kodProduktu = row.Cells["Kod"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(kodProduktu) || kodProduktu == "SUMA") return;

            var rezerwacje = WczytajRezerwacje();
            int usuniete = rezerwacje.RemoveAll(r => r.KodProduktu == kodProduktu);

            if (usuniete > 0)
            {
                ZapiszRezerwacje(rezerwacje);
                BtnStanMagazynu_Click(null, null);
                statusLabel.Text = $"Usunięto {usuniete} rezerwacji dla {kodProduktu}";
            }
        }

        private void PokazHistorieWybranegoProduktu()
        {
            if (dgvStanMagazynu.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvStanMagazynu.SelectedRows[0];
            string kodProduktu = row.Cells["Kod"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(kodProduktu) || kodProduktu == "SUMA") return;

            decimal stan = Convert.ToDecimal(row.Cells["Stan (kg)"].Value ?? 0);
            decimal wartosc = Convert.ToDecimal(row.Cells["Wartość (zł)"].Value ?? 0);

            ShowHistoriaProduktuModal(kodProduktu, stan, wartosc, dtpStanMagazynu.Value.Date);
        }

        private void DgvStanMagazynu_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hit = dgvStanMagazynu.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0)
                {
                    dgvStanMagazynu.ClearSelection();
                    dgvStanMagazynu.Rows[hit.RowIndex].Selected = true;
                }
            }
        }

        private void UpdateStanStatystyki(decimal sumaStan, decimal sumaWartosc, int liczbaProdukow)
        {
            lblStanSuma.Text = $"Stan: {sumaStan:N0} kg";
            var rezerwacje = WczytajRezerwacje();
            decimal sumaRezerwacji = rezerwacje.Sum(r => r.Ilosc);
            lblStanRezerwacje.Text = $"Zarezerwowano: {sumaRezerwacji:N0} kg";
        }

        private Dictionary<string, decimal> GetRezerwacjePoProduktach()
        {
            var rezerwacje = WczytajRezerwacje();
            return rezerwacje
                .GroupBy(r => r.KodProduktu)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Ilosc), StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Mroźnie zewnętrzne

        private string GetMroznieZewnetrzneFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "OfertaHandlowa");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "mroznie_zewnetrzne.json");
        }

        private List<MrozniaZewnetrzna> WczytajMroznieZewnetrzne()
        {
            string path = GetMroznieZewnetrzneFilePath();
            if (!File.Exists(path)) return new List<MrozniaZewnetrzna>();
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<MrozniaZewnetrzna>>(json) ?? new List<MrozniaZewnetrzna>();
        }

        private void ZapiszMroznieZewnetrzne(List<MrozniaZewnetrzna> lista)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(lista, options);
            File.WriteAllText(GetMroznieZewnetrzneFilePath(), json);
        }

        private void LoadMroznieZewnetrzneDoTabeli()
        {
            var mroznie = WczytajMroznieZewnetrzne();

            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("Nazwa", typeof(string));
            dt.Columns.Add("Adres", typeof(string));
            dt.Columns.Add("Kontakty", typeof(string));
            dt.Columns.Add("Stan (kg)", typeof(decimal));

            foreach (var m in mroznie)
            {
                decimal stan = m.Wydania?.Where(w => w.Typ == "Przyjęcie").Sum(w => w.Ilosc) ?? 0;
                stan -= m.Wydania?.Where(w => w.Typ == "Wydanie").Sum(w => w.Ilosc) ?? 0;

                // Formatuj kontakty - pokaż pierwszy kontakt + liczbę pozostałych
                string kontaktyStr = "";
                if (m.Kontakty != null && m.Kontakty.Count > 0)
                {
                    var pierwszy = m.Kontakty[0];
                    kontaktyStr = $"{pierwszy.Imie}: {pierwszy.Telefon}";
                    if (m.Kontakty.Count > 1)
                        kontaktyStr += $" (+{m.Kontakty.Count - 1})";
                }

                dt.Rows.Add(m.Id, m.Nazwa, m.Adres, kontaktyStr, stan);
            }

            dgvMroznieZewnetrzne.DataSource = dt;
            dgvMroznieZewnetrzne.Columns["ID"].Visible = false;
            dgvMroznieZewnetrzne.Columns["Stan (kg)"].DefaultCellStyle.Format = "N0";
        }

        private void DgvMroznieZewnetrzne_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvMroznieZewnetrzne.SelectedRows.Count == 0)
            {
                dgvWydaniaZewnetrzne.DataSource = null;
                return;
            }

            string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            var mroznie = WczytajMroznieZewnetrzne();
            var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
            if (mroznia == null) return;

            DataTable dt = new DataTable();
            dt.Columns.Add("Data", typeof(DateTime));
            dt.Columns.Add("Typ", typeof(string));
            dt.Columns.Add("Kod", typeof(string));
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Ilość (kg)", typeof(decimal));
            dt.Columns.Add("Uwagi", typeof(string));

            if (mroznia.Wydania != null)
            {
                foreach (var w in mroznia.Wydania.OrderByDescending(x => x.Data))
                {
                    dt.Rows.Add(w.Data, w.Typ, w.KodProduktu ?? "", w.Produkt, w.Ilosc, w.Uwagi);
                }
            }

            dgvWydaniaZewnetrzne.DataSource = dt;
            dgvWydaniaZewnetrzne.Columns["Data"].DefaultCellStyle.Format = "dd.MM.yyyy";
            dgvWydaniaZewnetrzne.Columns["Ilość (kg)"].DefaultCellStyle.Format = "N0";

            // Koloruj wydania/przyjęcia
            foreach (DataGridViewRow row in dgvWydaniaZewnetrzne.Rows)
            {
                string typ = row.Cells["Typ"].Value?.ToString();
                if (typ == "Przyjęcie")
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(40, 167, 69);
                else if (typ == "Wydanie")
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 53, 69);
            }
        }

        private void BtnDodajMroznieZewnetrzna_Click(object sender, EventArgs e)
        {
            using (var dlg = new MrozniaZewnetrznaDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var mroznie = WczytajMroznieZewnetrzne();
                    mroznie.Add(new MrozniaZewnetrzna
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                        Nazwa = dlg.NazwaMrozni,
                        Adres = dlg.Adres,
                        Email = dlg.Email,
                        Uwagi = dlg.Uwagi,
                        Kontakty = dlg.Kontakty,
                        Wydania = new List<WydanieZewnetrzne>()
                    });
                    ZapiszMroznieZewnetrzne(mroznie);
                    LoadMroznieZewnetrzneDoTabeli();
                    statusLabel.Text = $"Dodano mroźnię: {dlg.NazwaMrozni}";
                }
            }
        }

        private void BtnEdytujMroznieZewnetrzna_Click(object sender, EventArgs e)
        {
            if (dgvMroznieZewnetrzne.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz mroźnię do edycji.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
            var mroznie = WczytajMroznieZewnetrzne();
            var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
            if (mroznia == null) return;

            using (var dlg = new MrozniaZewnetrznaDialog(mroznia))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    mroznia.Nazwa = dlg.NazwaMrozni;
                    mroznia.Adres = dlg.Adres;
                    mroznia.Email = dlg.Email;
                    mroznia.Uwagi = dlg.Uwagi;
                    mroznia.Kontakty = dlg.Kontakty;
                    ZapiszMroznieZewnetrzne(mroznie);
                    LoadMroznieZewnetrzneDoTabeli();
                    statusLabel.Text = $"Zaktualizowano mroźnię: {dlg.NazwaMrozni}";
                }
            }
        }

        private void BtnUsunMroznieZewnetrzna_Click(object sender, EventArgs e)
        {
            if (dgvMroznieZewnetrzne.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz mroźnię do usunięcia.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string nazwa = dgvMroznieZewnetrzne.SelectedRows[0].Cells["Nazwa"].Value?.ToString();
            var result = MessageBox.Show($"Czy na pewno chcesz usunąć mroźnię \"{nazwa}\"?\n\nWszystkie dane o wydaniach zostaną utracone!",
                "Potwierdź usunięcie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
                var mroznie = WczytajMroznieZewnetrzne();
                mroznie.RemoveAll(m => m.Id == id);
                ZapiszMroznieZewnetrzne(mroznie);
                LoadMroznieZewnetrzneDoTabeli();
                statusLabel.Text = $"Usunięto mroźnię: {nazwa}";
            }
        }

        private void BtnDodajWydanieZewnetrzne_Click(object sender, EventArgs e)
        {
            if (dgvMroznieZewnetrzne.SelectedRows.Count == 0)
            {
                MessageBox.Show("Najpierw wybierz mroźnię z listy.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
            string nazwa = dgvMroznieZewnetrzne.SelectedRows[0].Cells["Nazwa"].Value?.ToString();

            using (var dlg = new WydanieZewnetrzneDialog(nazwa, connectionString))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var mroznie = WczytajMroznieZewnetrzne();
                    var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
                    if (mroznia != null)
                    {
                        if (mroznia.Wydania == null)
                            mroznia.Wydania = new List<WydanieZewnetrzne>();

                        mroznia.Wydania.Add(new WydanieZewnetrzne
                        {
                            Id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                            Data = dlg.Data,
                            Typ = dlg.Typ,
                            KodProduktu = dlg.KodProduktu,
                            Produkt = dlg.Produkt,
                            Ilosc = dlg.Ilosc,
                            Uwagi = dlg.Uwagi
                        });

                        ZapiszMroznieZewnetrzne(mroznie);
                        LoadMroznieZewnetrzneDoTabeli();
                        DgvMroznieZewnetrzne_SelectionChanged(null, null);
                        statusLabel.Text = $"{dlg.Typ}: {dlg.Ilosc:N0} kg {dlg.Produkt} ({nazwa})";
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Model danych mapowania świeży-mrożony (do deserializacji JSON)
    /// </summary>
    public class MapowanieItem
    {
        public int IdSwiezy { get; set; }
        public string KodSwiezy { get; set; } = "";
        public int IdMrozony { get; set; }
        public string KodMrozony { get; set; } = "";
    }

    /// <summary>
    /// Model rezerwacji towaru mroźniowego
    /// </summary>
    public class RezerwacjaItem
    {
        public string Id { get; set; } = "";
        public string KodProduktu { get; set; } = "";
        public decimal Ilosc { get; set; }
        public string Handlowiec { get; set; } = "";
        public DateTime DataRezerwacji { get; set; }
        public DateTime DataWaznosci { get; set; }
        public string Uwagi { get; set; } = "";
    }

    /// <summary>
    /// Prosty dialog rezerwacji z ilością i datą ważności
    /// </summary>
    public class RezerwacjaInputDialog : Form
    {
        public decimal Ilosc { get; private set; }
        public DateTime DataWaznosci { get; private set; }

        private NumericUpDown nudIlosc;
        private DateTimePicker dtpWaznosc;

        public RezerwacjaInputDialog(string kodProduktu, decimal dostepne, string handlowiec)
        {
            this.Text = "Rezerwacja towaru";
            this.Size = new Size(320, 220);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);

            int y = 15;

            // Info o produkcie
            Label lblInfo = new Label
            {
                Text = $"Produkt: {kodProduktu}\nDostępne: {dostepne:N0} kg\nHandlowiec: {handlowiec}",
                Location = new Point(15, y),
                Size = new Size(280, 50),
                Font = new Font("Segoe UI", 9F)
            };
            y += 55;

            // Ilość
            Label lblIlosc = new Label { Text = "Ilość (kg):", Location = new Point(15, y), AutoSize = true };
            nudIlosc = new NumericUpDown
            {
                Location = new Point(120, y - 3),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = Math.Max(1, dostepne),
                Value = Math.Min(100, Math.Max(1, dostepne)),
                DecimalPlaces = 0
            };
            y += 35;

            // Data ważności
            Label lblWaznosc = new Label { Text = "Ważna do:", Location = new Point(15, y), AutoSize = true };
            dtpWaznosc = new DateTimePicker
            {
                Location = new Point(120, y - 3),
                Size = new Size(120, 25),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(7),
                MinDate = DateTime.Today
            };
            y += 45;

            // Przyciski
            Button btnOK = new Button
            {
                Text = "Zarezerwuj",
                Location = new Point(100, y),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => { Ilosc = nudIlosc.Value; DataWaznosci = dtpWaznosc.Value.Date; };

            Button btnCancel = new Button
            {
                Text = "Anuluj",
                Location = new Point(200, y),
                Size = new Size(70, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] { lblInfo, lblIlosc, nudIlosc, lblWaznosc, dtpWaznosc, btnOK, btnCancel });
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    /// <summary>
    /// Model kontaktu w mroźni zewnętrznej
    /// </summary>
    public class KontaktMrozni
    {
        public string Imie { get; set; } = "";
        public string Telefon { get; set; } = "";
    }

    /// <summary>
    /// Model mroźni zewnętrznej
    /// </summary>
    public class MrozniaZewnetrzna
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Adres { get; set; } = "";
        public string Email { get; set; } = "";
        public string Uwagi { get; set; } = "";
        public List<KontaktMrozni> Kontakty { get; set; } = new List<KontaktMrozni>();
        public List<WydanieZewnetrzne> Wydania { get; set; } = new List<WydanieZewnetrzne>();
    }

    /// <summary>
    /// Model wydania/przyjęcia w mroźni zewnętrznej
    /// </summary>
    public class WydanieZewnetrzne
    {
        public string Id { get; set; } = "";
        public DateTime Data { get; set; }
        public string Typ { get; set; } = ""; // "Wydanie" lub "Przyjęcie"
        public string KodProduktu { get; set; } = "";
        public string Produkt { get; set; } = "";
        public decimal Ilosc { get; set; }
        public string Uwagi { get; set; } = "";
    }

    /// <summary>
    /// Dialog dodawania/edycji mroźni zewnętrznej - NOWY DESIGN
    /// </summary>
    public class MrozniaZewnetrznaDialog : Form
    {
        public string NazwaMrozni { get; private set; } = "";
        public string Adres { get; private set; } = "";
        public string Email { get; private set; } = "";
        public string Uwagi { get; private set; } = "";
        public List<KontaktMrozni> Kontakty { get; private set; } = new List<KontaktMrozni>();

        private TextBox txtNazwa, txtAdres, txtEmail, txtUwagi;
        private Panel panelKontakty;
        private List<Panel> kontaktPanels = new List<Panel>();

        // Kolory
        private readonly Color AccentColor = Color.FromArgb(0, 123, 255);
        private readonly Color SuccessColor = Color.FromArgb(40, 167, 69);
        private readonly Color DangerColor = Color.FromArgb(220, 53, 69);
        private readonly Color LightBg = Color.FromArgb(248, 249, 250);
        private readonly Color CardBg = Color.White;
        private readonly Color BorderColor = Color.FromArgb(222, 226, 230);
        private readonly Color TextMuted = Color.FromArgb(108, 117, 125);

        public MrozniaZewnetrznaDialog(MrozniaZewnetrzna mroznia = null)
        {
            bool isEdit = mroznia != null;

            this.Text = isEdit ? "Edycja mroźni" : "Nowa mroźnia zewnętrzna";
            this.Size = new Size(580, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = LightBg;
            this.Font = new Font("Segoe UI", 9.5F);

            // === NAGŁÓWEK ===
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = isEdit ? AccentColor : SuccessColor
            };

            Label lblTitle = new Label
            {
                Text = isEdit ? "EDYCJA MROŹNI ZEWNĘTRZNEJ" : "NOWA MROŹNIA ZEWNĘTRZNA",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(400, 30),
                Location = new Point(25, 12)
            };

            Label lblSubtitle = new Label
            {
                Text = isEdit ? "Modyfikuj dane mroźni i kontakty" : "Wprowadź dane nowej lokalizacji",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(220, 220, 220),
                AutoSize = false,
                Size = new Size(400, 20),
                Location = new Point(25, 42)
            };

            headerPanel.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

            // === GŁÓWNY PANEL PRZEWIJANY ===
            Panel scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(25, 20, 25, 20)
            };

            // === SEKCJA: DANE PODSTAWOWE ===
            Panel cardDane = CreateCard("DANE PODSTAWOWE", 0);
            int cardY = 45;

            // Nazwa
            cardDane.Controls.Add(CreateFieldLabel("Nazwa mroźni", 15, cardY));
            txtNazwa = CreateStyledTextBox(15, cardY + 22, 500);
            txtNazwa.Text = mroznia?.Nazwa ?? "";
            cardDane.Controls.Add(txtNazwa);
            cardY += 65;

            // Adres
            cardDane.Controls.Add(CreateFieldLabel("Adres", 15, cardY));
            txtAdres = CreateStyledTextBox(15, cardY + 22, 500);
            txtAdres.Text = mroznia?.Adres ?? "";
            cardDane.Controls.Add(txtAdres);
            cardY += 65;

            // Email
            cardDane.Controls.Add(CreateFieldLabel("Email", 15, cardY));
            txtEmail = CreateStyledTextBox(15, cardY + 22, 240);
            txtEmail.Text = mroznia?.Email ?? "";
            cardDane.Controls.Add(txtEmail);
            cardY += 65;

            // Uwagi
            cardDane.Controls.Add(CreateFieldLabel("Uwagi / Notatki", 15, cardY));
            txtUwagi = CreateStyledTextBox(15, cardY + 22, 500, 60, true);
            txtUwagi.Text = mroznia?.Uwagi ?? "";
            cardDane.Controls.Add(txtUwagi);
            cardY += 100;

            cardDane.Height = cardY + 10;

            // === SEKCJA: KONTAKTY ===
            Panel cardKontakty = CreateCard("OSOBY KONTAKTOWE", cardDane.Height + 20);

            // Przycisk dodaj kontakt
            Button btnDodajKontakt = new Button
            {
                Text = "+ Dodaj osobę",
                Size = new Size(120, 32),
                Location = new Point(395, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = SuccessColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajKontakt.FlatAppearance.BorderSize = 0;
            btnDodajKontakt.Click += (s, e) => DodajKontaktPanel();
            cardKontakty.Controls.Add(btnDodajKontakt);

            // Panel na kontakty
            panelKontakty = new Panel
            {
                Location = new Point(15, 50),
                Size = new Size(500, 150),
                AutoScroll = true,
                BackColor = LightBg
            };
            cardKontakty.Controls.Add(panelKontakty);

            // Załaduj istniejące kontakty
            if (mroznia?.Kontakty != null && mroznia.Kontakty.Count > 0)
            {
                foreach (var k in mroznia.Kontakty)
                    DodajKontaktPanel(k.Imie, k.Telefon);
            }
            else
            {
                // Dodaj pusty panel kontaktu
                DodajKontaktPanel();
            }

            cardKontakty.Height = 220;

            // === PRZYCISKI NA DOLE ===
            Panel footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = CardBg,
                Padding = new Padding(25, 15, 25, 15)
            };
            footerPanel.Paint += (s, e) => {
                using (var pen = new Pen(BorderColor))
                    e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
            };

            Button btnZapisz = new Button
            {
                Text = "ZAPISZ",
                Size = new Size(140, 40),
                Location = new Point(footerPanel.Width - 310, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = SuccessColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            Button btnAnuluj = new Button
            {
                Text = "Anuluj",
                Size = new Size(100, 40),
                Location = new Point(footerPanel.Width - 155, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            footerPanel.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });

            // Dodaj karty do scrollContainer
            scrollContainer.Controls.Add(cardKontakty);
            scrollContainer.Controls.Add(cardDane);

            this.Controls.Add(scrollContainer);
            this.Controls.Add(footerPanel);
            this.Controls.Add(headerPanel);

            this.AcceptButton = btnZapisz;
            this.CancelButton = btnAnuluj;
        }

        private Panel CreateCard(string title, int top)
        {
            Panel card = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(530, 200),
                BackColor = CardBg
            };
            card.Paint += (s, e) => {
                using (var pen = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 58, 64),
                Location = new Point(15, 12),
                AutoSize = true
            };
            card.Controls.Add(lblTitle);

            return card;
        }

        private Label CreateFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9F),
                ForeColor = TextMuted,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private TextBox CreateStyledTextBox(int x, int y, int width, int height = 32, bool multiline = false)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                Font = new Font("Segoe UI", 11F),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = multiline
            };
            if (multiline) txt.ScrollBars = ScrollBars.Vertical;
            return txt;
        }

        private void DodajKontaktPanel(string imie = "", string telefon = "")
        {
            int panelY = kontaktPanels.Count * 50;

            Panel p = new Panel
            {
                Size = new Size(480, 45),
                Location = new Point(0, panelY),
                BackColor = CardBg
            };
            p.Paint += (s, e) => {
                using (var pen = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };

            TextBox txtImie = new TextBox
            {
                Location = new Point(10, 8),
                Size = new Size(200, 28),
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                Text = imie,
                Tag = "imie"
            };
            txtImie.GotFocus += (s, e) => { if (txtImie.Text == "") txtImie.PlaceholderText = "Imię i nazwisko"; };

            TextBox txtTel = new TextBox
            {
                Location = new Point(220, 8),
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                Text = telefon,
                Tag = "telefon"
            };
            txtTel.GotFocus += (s, e) => { if (txtTel.Text == "") txtTel.PlaceholderText = "Nr telefonu"; };

            Button btnUsun = new Button
            {
                Text = "×",
                Size = new Size(32, 28),
                Location = new Point(410, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = DangerColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnUsun.FlatAppearance.BorderSize = 0;
            btnUsun.Click += (s, e) => UsunKontaktPanel(p);

            p.Controls.AddRange(new Control[] { txtImie, txtTel, btnUsun });
            kontaktPanels.Add(p);
            panelKontakty.Controls.Add(p);

            // Rozszerz panel jeśli potrzeba
            if (kontaktPanels.Count > 3)
            {
                panelKontakty.Height = kontaktPanels.Count * 50 + 10;
            }
        }

        private void UsunKontaktPanel(Panel p)
        {
            if (kontaktPanels.Count <= 1) return; // Zostaw przynajmniej jeden

            kontaktPanels.Remove(p);
            panelKontakty.Controls.Remove(p);
            p.Dispose();

            // Przenumeruj pozycje
            for (int i = 0; i < kontaktPanels.Count; i++)
            {
                kontaktPanels[i].Location = new Point(0, i * 50);
            }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNazwa.Text))
            {
                MessageBox.Show("Podaj nazwę mroźni.", "Brak nazwy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            NazwaMrozni = txtNazwa.Text.Trim();
            Adres = txtAdres.Text.Trim();
            Email = txtEmail.Text.Trim();
            Uwagi = txtUwagi.Text.Trim();

            // Pobierz kontakty
            Kontakty = new List<KontaktMrozni>();
            foreach (Panel p in kontaktPanels)
            {
                string imie = "", tel = "";
                foreach (Control c in p.Controls)
                {
                    if (c is TextBox txt)
                    {
                        if (txt.Tag?.ToString() == "imie") imie = txt.Text.Trim();
                        else if (txt.Tag?.ToString() == "telefon") tel = txt.Text.Trim();
                    }
                }
                if (!string.IsNullOrEmpty(imie) || !string.IsNullOrEmpty(tel))
                {
                    Kontakty.Add(new KontaktMrozni { Imie = imie, Telefon = tel });
                }
            }
        }
    }

    /// <summary>
    /// Dialog wydania/przyjęcia - PROSTY Z COMBOBOX
    /// </summary>
    public class WydanieZewnetrzneDialog : Form
    {
        public DateTime Data { get; private set; }
        public string Typ { get; private set; } = "";
        public string KodProduktu { get; private set; } = "";
        public string Produkt { get; private set; } = "";
        public decimal Ilosc { get; private set; }
        public string Uwagi { get; private set; } = "";

        private ComboBox cmbTyp, cmbProdukt;
        private DateTimePicker dtpData;
        private NumericUpDown nudIlosc;
        private TextBox txtUwagi;
        private List<ProduktMrozony> produkty = new List<ProduktMrozony>();

        public WydanieZewnetrzneDialog(string nazwaMrozni, string connectionString)
        {
            this.Text = $"Operacja - {nazwaMrozni}";
            this.Size = new Size(420, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);

            // Załaduj produkty
            LoadProdukty(connectionString);

            int y = 20;

            // Typ operacji
            Label lblTyp = new Label { Text = "Typ operacji:", Location = new Point(20, y), AutoSize = true };
            cmbTyp = new ComboBox
            {
                Location = new Point(20, y + 22),
                Size = new Size(360, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11F)
            };
            cmbTyp.Items.AddRange(new[] { "Przyjęcie (do mroźni)", "Wydanie (z mroźni)" });
            cmbTyp.SelectedIndex = 0;
            this.Controls.AddRange(new Control[] { lblTyp, cmbTyp });
            y += 55;

            // Data
            Label lblData = new Label { Text = "Data:", Location = new Point(20, y), AutoSize = true };
            dtpData = new DateTimePicker
            {
                Location = new Point(20, y + 22),
                Size = new Size(170, 28),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 11F)
            };
            this.Controls.AddRange(new Control[] { lblData, dtpData });
            y += 55;

            // Produkt - ComboBox z autocomplete
            Label lblProdukt = new Label { Text = "Produkt:", Location = new Point(20, y), AutoSize = true };
            cmbProdukt = new ComboBox
            {
                Location = new Point(20, y + 22),
                Size = new Size(360, 28),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Font = new Font("Segoe UI", 11F)
            };
            foreach (var p in produkty)
                cmbProdukt.Items.Add($"{p.Kod} - {p.Nazwa}");
            this.Controls.AddRange(new Control[] { lblProdukt, cmbProdukt });
            y += 55;

            // Ilość
            Label lblIlosc = new Label { Text = "Ilość (kg):", Location = new Point(20, y), AutoSize = true };
            nudIlosc = new NumericUpDown
            {
                Location = new Point(20, y + 22),
                Size = new Size(120, 28),
                Minimum = 1,
                Maximum = 999999,
                Value = 100,
                Font = new Font("Segoe UI", 11F)
            };
            this.Controls.AddRange(new Control[] { lblIlosc, nudIlosc });
            y += 55;

            // Uwagi
            Label lblUwagi = new Label { Text = "Uwagi:", Location = new Point(20, y), AutoSize = true };
            txtUwagi = new TextBox
            {
                Location = new Point(20, y + 22),
                Size = new Size(360, 28),
                Font = new Font("Segoe UI", 10F)
            };
            this.Controls.AddRange(new Control[] { lblUwagi, txtUwagi });
            y += 55;

            // Przyciski
            Button btnZapisz = new Button
            {
                Text = "Zapisz",
                Location = new Point(200, y),
                Size = new Size(85, 35),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            Button btnAnuluj = new Button
            {
                Text = "Anuluj",
                Location = new Point(295, y),
                Size = new Size(85, 35),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj });
            this.AcceptButton = btnZapisz;
            this.CancelButton = btnAnuluj;
        }

        private void LoadProdukty(string connectionString)
        {
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT Id, Kod, Nazwa FROM [HANDEL].[HM].[TW]
                                    WHERE katalog = '67153' ORDER BY Nazwa ASC";

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(query, conn))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            produkty.Add(new ProduktMrozony
                            {
                                Id = rd.GetInt32(0),
                                Kod = rd["Kod"]?.ToString() ?? "",
                                Nazwa = rd["Nazwa"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(cmbProdukt.Text))
            {
                MessageBox.Show("Wybierz lub wpisz produkt.", "Brak produktu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            Data = dtpData.Value.Date;
            Typ = cmbTyp.SelectedIndex == 0 ? "Przyjęcie" : "Wydanie";

            // Parsuj produkt (format: "KOD - Nazwa")
            string txt = cmbProdukt.Text.Trim();
            int sep = txt.IndexOf(" - ");
            if (sep > 0)
            {
                KodProduktu = txt.Substring(0, sep).Trim();
                Produkt = txt.Substring(sep + 3).Trim();
            }
            else
            {
                KodProduktu = "";
                Produkt = txt;
            }

            Ilosc = nudIlosc.Value;
            Uwagi = txtUwagi.Text.Trim();
        }
    }

    /// <summary>
    /// Model produktu mrożonego (do wyboru w dialogu)
    /// </summary>
    public class ProduktMrozony
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
    }
}