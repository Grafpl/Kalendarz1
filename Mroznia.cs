using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using ChartSeries = System.Windows.Forms.DataVisualization.Charting.Series;
using System.Windows.Forms.Integration;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Text.Json;
using LiveCharts;
using LiveCharts.Wpf;

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
        private Button btnEksport, btnSzybkiRaport, btnMapowanie;
        private DataGridView dgvDzienne, dgvStanMagazynu, dgvZamowienia;
        private DataGridView dgvMroznieZewnetrzne, dgvWydaniaZewnetrzne, dgvStanMrozniZewnetrznych;
        private TabControl tabControl;
        private ComboBox cmbPredkosc, cmbFiltrMroznia;
        private Label lblStanSuma, lblStanWartosc, lblStanProdukty, lblStanRezerwacje;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripProgressBar progressBar;
        private ElementHost chartHost;
        private CartesianChart liveChart;
        private ToolTip toolTip;
        private CheckBox chkGrupowanie;
        private Timer autoLoadTimer;

        // === UX: Toast + kolorowy pasek zakładek ===
        private Panel toastPanel;
        private Label toastLabel;
        private Timer toastTimer;
        private Panel tabIndicatorPanel;
        private FlowLayoutPanel mroznieCardsPanel;

        // === CACHE STATYSTYK (zastępują usunięte karty) ===
        private decimal lastWydano, lastPrzyjeto;
        private int lastDni;

        // Kolory zakładek (1a - kolorowy wskaźnik)
        private readonly Color[] TabColors = new Color[]
        {
            Color.FromArgb(0, 120, 212),   // Stan mroźni - niebieski
            Color.FromArgb(220, 53, 69),   // Rezerwacje - czerwony
            Color.FromArgb(16, 124, 16),   // Mroźnie zewnętrzne - zielony
            Color.FromArgb(255, 140, 0),   // Przegląd dzienny - pomarańczowy
            Color.FromArgb(140, 20, 252)   // Wykresy - fioletowy
        };

        // === STAŁE BIZNESOWE ===
        private const int MagazynMroznia = 65552;
        internal const string KatalogSwiezy = "67095";
        internal const string KatalogMrozony = "67153";
        private const string DataStartowa = "2020-01-07";

        private static readonly string SQL_FILTR_SERII =
            "(mg.seria = 'sMM+' OR mg.seria = 'sMM-' OR mg.seria = 'sMK-' OR mg.seria = 'sMK+')";

        private static readonly string SQL_GRUPOWANIE_PRODUKTU = @"CASE
                        WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                        WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                        WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                        WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                        WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                        WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                        WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                        WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                        ELSE MZ.kod
                    END";

        // === DANE CACHE ===
        private DataTable cachedDzienneData;
        private DateTime lastAnalysisDate = DateTime.MinValue;
        private bool isUpdatingFiltrMroznia = false;

        public Mroznia()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
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
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 4F));   // kolorowy wskaźnik
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // zakładki
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));  // status bar

            // === 1a: KOLOROWY PASEK WSKAŹNIKA ZAKŁADKI ===
            tabIndicatorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TabColors[0],
                Margin = new Padding(0)
            };

            // === GŁÓWNA ZAWARTOŚĆ ===
            tabControl = CreateTabControl();
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // === STATUS BAR ===
            CreateStatusBar();

            mainLayout.Controls.Add(tabIndicatorPanel, 0, 0);
            mainLayout.Controls.Add(tabControl, 0, 1);
            mainLayout.Controls.Add(statusStrip, 0, 2);

            this.Controls.Add(mainLayout);

            // === 7bb: TOAST NOTIFICATION SYSTEM ===
            InitializeToastSystem();
        }

        private Panel CreateAnalysisToolbar()
        {
            Panel toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Color.FromArgb(250, 251, 253),
                Padding = new Padding(0)
            };
            toolbar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            Label lblOd = new Label
            {
                Text = "Od:",
                Location = new Point(15, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            dtpOd = new DateTimePicker
            {
                Location = new Point(42, 14),
                Width = 115,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddDays(-30),
                Font = new Font("Segoe UI", 9.5F)
            };

            Label lblDo = new Label
            {
                Text = "Do:",
                Location = new Point(168, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            dtpDo = new DateTimePicker
            {
                Location = new Point(195, 14),
                Width = 115,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now,
                Font = new Font("Segoe UI", 9.5F)
            };

            cmbPredkosc = new ComboBox
            {
                Location = new Point(325, 14),
                Width = 155,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat
            };
            cmbPredkosc.Items.AddRange(new object[] {
                "Dowolny", "Dziś", "Wczoraj", "Ostatnie 7 dni", "Ostatnie 30 dni",
                "Bieżący miesiąc", "Poprzedni miesiąc", "Ostatnie 3 miesiące"
            });
            cmbPredkosc.SelectedIndex = 0;
            cmbPredkosc.SelectedIndexChanged += CmbPredkosc_SelectedIndexChanged;

            btnSzybkiRaport = CreateModernButton("Raport", 500, 10, 85, InfoColor);
            btnEksport = CreateModernButton("Eksport", 595, 10, 85, DangerColor);

            toolTip.SetToolTip(btnSzybkiRaport, "Generuj szybki raport PDF");
            toolTip.SetToolTip(btnEksport, "Eksportuj dane do pliku Excel");
            toolTip.SetToolTip(dtpOd, "Data początkowa zakresu");
            toolTip.SetToolTip(dtpDo, "Data końcowa zakresu");

            // Auto-load: przeładuj dane po zmianie daty (z debounce 500ms)
            autoLoadTimer = new Timer { Interval = 500 };
            autoLoadTimer.Tick += (s, e) => { autoLoadTimer.Stop(); AutoLoadData(); };

            dtpOd.ValueChanged += (s, e) => { autoLoadTimer.Stop(); autoLoadTimer.Start(); };
            dtpDo.ValueChanged += (s, e) => { autoLoadTimer.Stop(); autoLoadTimer.Start(); };

            Label lblHint = new Label
            {
                Text = "2x klik na wiersz = szczegóły dnia",
                Location = new Point(695, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(150, 150, 150)
            };

            toolbar.Controls.AddRange(new Control[] {
                lblOd, dtpOd, lblDo, dtpDo, cmbPredkosc,
                btnSzybkiRaport, btnEksport, lblHint
            });

            return toolbar;
        }

        // ============================================
        // 1a: KOLOROWY PASEK WSKAŹNIKA ZAKŁADKI
        // ============================================

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = tabControl.SelectedIndex;
            if (idx >= 0 && idx < TabColors.Length)
            {
                Color target = TabColors[idx];
                tabIndicatorPanel.BackColor = target;
            }

            // 1d: Aktualizuj badge'e na zakładkach
            UpdateTabBadges();
        }

        // ============================================
        // 1d: BADGE'E NA ZAKŁADKACH
        // ============================================

        private void UpdateTabBadges()
        {
            try
            {
                // Tab 0 - Stan mroźni: liczba produktów
                if (dgvStanMagazynu?.DataSource is DataTable dtStan && dtStan.Rows.Count > 0)
                {
                    int count = dtStan.Rows.Count;
                    if (dtStan.Rows[0]["Kod/Produkt"]?.ToString() == "SUMA")
                        count--;
                    tabControl.TabPages[0].Text = $"  Stan mroźni ({count})  ";
                }

                // Tab 1 - Rezerwacje: liczba rezerwacji
                if (dgvZamowienia?.DataSource is DataTable dtRez && dtRez.Rows.Count > 0)
                    tabControl.TabPages[1].Text = $"  Rezerwacje ({dtRez.Rows.Count})  ";
                else
                    tabControl.TabPages[1].Text = "  Rezerwacje  ";

                // Tab 2 - Mroźnie zewnętrzne: liczba mroźni
                if (dgvMroznieZewnetrzne?.DataSource is DataTable dtMr)
                    tabControl.TabPages[2].Text = $"  Mroźnie zewnętrzne ({dtMr.Rows.Count})  ";

                // Tab 3 - Przegląd dzienny: liczba dni
                if (dgvDzienne?.DataSource is DataTable dtDz && dtDz.Rows.Count > 0)
                    tabControl.TabPages[3].Text = $"  Przegląd dzienny ({dtDz.Rows.Count})  ";
                else
                    tabControl.TabPages[3].Text = "  Przegląd dzienny  ";

                // Tab 4 - Wykresy: bez badge'a
            }
            catch { }
        }

        // ============================================
        // 7bb: TOAST NOTIFICATION SYSTEM
        // ============================================

        private void InitializeToastSystem()
        {
            toastPanel = new Panel
            {
                Size = new Size(380, 44),
                BackColor = SuccessColor,
                Visible = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            toastPanel.Paint += (s, e) =>
            {
                using (var path = GetRoundedRectangle(new Rectangle(0, 0, toastPanel.Width - 1, toastPanel.Height - 1), 8))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(toastPanel.BackColor))
                        e.Graphics.FillPath(brush, path);
                }
            };

            toastLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            toastPanel.Controls.Add(toastLabel);

            toastTimer = new Timer { Interval = 3000 };
            toastTimer.Tick += (s, e) =>
            {
                toastTimer.Stop();
                toastPanel.Visible = false;
            };

            this.Controls.Add(toastPanel);
            toastPanel.BringToFront();
        }

        private void ShowToast(string message, Color? color = null)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ShowToast(message, color)));
                return;
            }

            toastPanel.BackColor = color ?? SuccessColor;
            toastLabel.Text = message;
            toastPanel.Location = new Point(
                this.ClientSize.Width - toastPanel.Width - 20,
                this.ClientSize.Height - toastPanel.Height - 40);
            toastPanel.Visible = true;
            toastPanel.BringToFront();
            toastTimer.Stop();
            toastTimer.Start();
        }

        // ============================================
        // 7cc: PODŚWIETLENIE ZMIENIONEGO WIERSZA
        // ============================================

        private void HighlightChangedRows(DataGridView dgv, HashSet<int> changedRowIndices)
        {
            if (changedRowIndices == null || changedRowIndices.Count == 0) return;

            Color highlightColor = Color.FromArgb(255, 255, 200); // jasny żółty

            foreach (int idx in changedRowIndices)
            {
                if (idx >= 0 && idx < dgv.Rows.Count)
                    dgv.Rows[idx].DefaultCellStyle.BackColor = highlightColor;
            }

            // Timer do wygaszenia po 2.5s
            Timer fadeTimer = new Timer { Interval = 2500 };
            fadeTimer.Tick += (s, e) =>
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();

                try
                {
                    foreach (int idx in changedRowIndices)
                    {
                        if (idx >= 0 && idx < dgv.Rows.Count)
                        {
                            dgv.Rows[idx].DefaultCellStyle.BackColor = Color.Empty;
                        }
                    }
                    dgv.Invalidate();
                }
                catch { }
            };
            fadeTimer.Start();
        }

        // ============================================
        // 6z: KARTY MROŹNI ZEWNĘTRZNYCH
        // ============================================

        private FlowLayoutPanel CreateMroznieCardsPanel()
        {
            mroznieCardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackgroundColor,
                Padding = new Padding(5)
            };
            return mroznieCardsPanel;
        }

        private Panel CreateMrozniaCard(string id, string nazwa, string adres, string kontakty, decimal stan)
        {
            Panel card = new Panel
            {
                Size = new Size(260, 140),
                Margin = new Padding(8),
                BackColor = CardColor,
                Cursor = Cursors.Hand,
                Tag = id
            };

            // Kolor lewej krawędzi w zależności od stanu
            Color accentColor = stan > 500 ? SuccessColor : stan > 0 ? WarningColor : SecondaryTextColor;

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Tło z zaokrąglonymi rogami
                using (var path = GetRoundedRectangle(card.ClientRectangle, 10))
                {
                    using (var brush = new LinearGradientBrush(
                        card.ClientRectangle, Color.White, Color.FromArgb(250, 252, 255),
                        LinearGradientMode.Vertical))
                        g.FillPath(brush, path);

                    // Border
                    using (var pen = new Pen(Color.FromArgb(225, 228, 232), 1))
                        g.DrawPath(pen, path);
                }

                // Lewa krawędź kolorowa
                using (var brush = new SolidBrush(accentColor))
                    g.FillRectangle(brush, 0, 12, 4, card.Height - 24);
            };

            // Ikona + nazwa
            Label lblNazwa = new Label
            {
                Text = $"\u2744 {nazwa}",
                Location = new Point(14, 10),
                Size = new Size(230, 24),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextColor,
                BackColor = Color.Transparent
            };

            // Adres
            Label lblAdres = new Label
            {
                Text = adres,
                Location = new Point(14, 36),
                Size = new Size(230, 18),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = SecondaryTextColor,
                BackColor = Color.Transparent
            };

            // Kontakt
            Label lblKontakt = new Label
            {
                Text = kontakty,
                Location = new Point(14, 56),
                Size = new Size(230, 18),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = SecondaryTextColor,
                BackColor = Color.Transparent
            };

            // Separator
            Panel separator = new Panel
            {
                Location = new Point(14, 80),
                Size = new Size(232, 1),
                BackColor = Color.FromArgb(230, 232, 235)
            };

            // Stan - duży, wyraźny
            Label lblStan = new Label
            {
                Text = $"{stan:N0} kg",
                Location = new Point(14, 88),
                Size = new Size(150, 30),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = Color.Transparent
            };

            // Status badge
            string statusText = stan > 500 ? "AKTYWNA" : stan > 0 ? "NISKA" : "PUSTA";
            Color badgeBg = stan > 500 ? Color.FromArgb(220, 245, 220) :
                            stan > 0 ? Color.FromArgb(255, 245, 220) :
                            Color.FromArgb(240, 240, 240);
            Color badgeFg = stan > 500 ? SuccessColor : stan > 0 ? WarningColor : SecondaryTextColor;

            Label lblStatus = new Label
            {
                Text = statusText,
                Location = new Point(170, 96),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = badgeFg,
                BackColor = badgeBg,
                Padding = new Padding(6, 2, 6, 2)
            };

            card.Controls.AddRange(new Control[] { lblNazwa, lblAdres, lblKontakt, separator, lblStan, lblStatus });

            // Hover effect
            Action<Control, bool> setHover = null;
            setHover = (ctrl, hover) =>
            {
                card.BackColor = hover ? Color.FromArgb(245, 248, 255) : CardColor;
            };

            foreach (Control c in card.Controls)
            {
                c.MouseEnter += (s, e) => setHover(card, true);
                c.MouseLeave += (s, e) => setHover(card, false);
                c.Click += (s, e) => SelectMrozniaCard(card);
            }
            card.MouseEnter += (s, e) => setHover(card, true);
            card.MouseLeave += (s, e) => setHover(card, false);
            card.Click += (s, e) => SelectMrozniaCard(card);

            return card;
        }

        private Panel selectedMrozniaCard;

        private void SelectMrozniaCard(Panel card)
        {
            // Odznacz poprzednio wybraną
            if (selectedMrozniaCard != null)
                selectedMrozniaCard.BackColor = CardColor;

            selectedMrozniaCard = card;
            card.BackColor = Color.FromArgb(230, 240, 255);

            // Znajdź odpowiadający wiersz w dgvMroznieZewnetrzne i zaznacz go
            string id = card.Tag?.ToString();
            if (string.IsNullOrEmpty(id) || dgvMroznieZewnetrzne?.DataSource == null) return;

            foreach (DataGridViewRow row in dgvMroznieZewnetrzne.Rows)
            {
                if (row.Cells["ID"].Value?.ToString() == id)
                {
                    dgvMroznieZewnetrzne.ClearSelection();
                    row.Selected = true;
                    break;
                }
            }
        }

        private void RefreshMroznieCards()
        {
            if (mroznieCardsPanel == null) return;

            mroznieCardsPanel.SuspendLayout();
            mroznieCardsPanel.Controls.Clear();

            var mroznie = WczytajMroznieZewnetrzne();
            foreach (var m in mroznie)
            {
                decimal stan = m.Wydania?.Where(w => w.Typ == "Przyjęcie").Sum(w => w.Ilosc) ?? 0;
                stan -= m.Wydania?.Where(w => w.Typ == "Wydanie").Sum(w => w.Ilosc) ?? 0;

                string kontaktyStr = "";
                if (m.Kontakty != null && m.Kontakty.Count > 0)
                {
                    var pierwszy = m.Kontakty[0];
                    kontaktyStr = $"{pierwszy.Imie}: {pierwszy.Telefon}";
                    if (m.Kontakty.Count > 1)
                        kontaktyStr += $" (+{m.Kontakty.Count - 1})";
                }

                Panel card = CreateMrozniaCard(m.Id, m.Nazwa, m.Adres, kontaktyStr, stan);
                mroznieCardsPanel.Controls.Add(card);
            }

            mroznieCardsPanel.ResumeLayout();
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
            tab1.Padding = new Padding(0);

            Panel dziennyPanel = new Panel { Dock = DockStyle.Fill };

            // Pasek narzędzi analizy (daty, przyciski)
            Panel analysisToolbar = CreateAnalysisToolbar();

            dgvDzienne = CreateStyledDataGridView();
            dgvDzienne.DoubleClick += DgvDzienne_DoubleClick;

            dziennyPanel.Controls.Add(dgvDzienne);
            dziennyPanel.Controls.Add(analysisToolbar);
            tab1.Controls.Add(dziennyPanel);

            // === ZAKŁADKA 3: WYKRESY ===
            TabPage tab3 = new TabPage("  Wykresy  ");
            tab3.BackColor = BackgroundColor;
            tab3.Padding = new Padding(0);

            // Pełnoekranowy wykres trendu
            Panel chartPanel1 = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            liveChart = new CartesianChart
            {
                DisableAnimations = false,
                Hoverable = true,
                DataTooltip = new DefaultTooltip
                {
                    SelectionMode = TooltipSelectionMode.SharedXValues
                },
                LegendLocation = LegendLocation.Bottom,
                Background = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            chartHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = liveChart
            };

            chartPanel1.Controls.Add(chartHost);
            tab3.Controls.Add(chartPanel1);

            // === ZAKŁADKA 4: STAN MAGAZYNU (KOMPAKTOWY LAYOUT) ===
            TabPage tab4 = new TabPage("  Stan mroźni  ");
            tab4.BackColor = BackgroundColor;
            tab4.Padding = new Padding(0);

            // Główny layout: Toolbar + SplitContainer
            Panel stanMainPanel = new Panel { Dock = DockStyle.Fill };

            // === TOOLBAR (nowoczesny z kartami statystyk) ===
            Panel stanToolbar = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(250, 251, 253) };
            stanToolbar.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    e.Graphics.DrawLine(pen, 0, stanToolbar.Height - 1, stanToolbar.Width, stanToolbar.Height - 1);
            };

            dtpStanMagazynu = new DateTimePicker
            {
                Location = new Point(15, 17),
                Width = 115,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now,
                Font = new Font("Segoe UI", 9.5F)
            };
            dtpStanMagazynu.ValueChanged += (s, e) => BtnStanMagazynu_Click(null, null);

            btnMapowanie = CreateModernButton("Mapowanie", 140, 14, 95, InfoColor);
            btnMapowanie.Click += BtnMapowanie_Click;

            // Karta statystyk: Stan
            Panel cardStan = new Panel
            {
                Location = new Point(255, 6),
                Size = new Size(180, 44),
                BackColor = Color.White
            };
            cardStan.Paint += (s, e) => {
                using (var pen = new Pen(PrimaryColor, 2))
                    e.Graphics.DrawLine(pen, 0, 0, 0, cardStan.Height);
                using (var pen = new Pen(Color.FromArgb(230, 230, 230)))
                    e.Graphics.DrawRectangle(pen, 0, 0, cardStan.Width - 1, cardStan.Height - 1);
            };
            Label lblStanLabel = new Label
            {
                Text = "STAN MAGAZYNU",
                Location = new Point(10, 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            lblStanSuma = new Label
            {
                Text = "0 kg",
                Location = new Point(10, 19),
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = PrimaryColor
            };
            cardStan.Controls.AddRange(new Control[] { lblStanLabel, lblStanSuma });

            // Karta statystyk: Rezerwacje
            Panel cardRez = new Panel
            {
                Location = new Point(450, 6),
                Size = new Size(180, 44),
                BackColor = Color.White
            };
            cardRez.Paint += (s, e) => {
                using (var pen = new Pen(DangerColor, 2))
                    e.Graphics.DrawLine(pen, 0, 0, 0, cardRez.Height);
                using (var pen = new Pen(Color.FromArgb(230, 230, 230)))
                    e.Graphics.DrawRectangle(pen, 0, 0, cardRez.Width - 1, cardRez.Height - 1);
            };
            Label lblRezLabel = new Label
            {
                Text = "ZAREZERWOWANO",
                Location = new Point(10, 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            lblStanRezerwacje = new Label
            {
                Text = "0 kg",
                Location = new Point(10, 19),
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = DangerColor
            };
            cardRez.Controls.AddRange(new Control[] { lblRezLabel, lblStanRezerwacje });

            lblStanWartosc = new Label { Visible = false };
            lblStanProdukty = new Label { Visible = false };
            chkGrupowanie = new CheckBox { Checked = false, Visible = false };

            stanToolbar.Controls.AddRange(new Control[] {
                dtpStanMagazynu, btnMapowanie,
                cardStan, cardRez, lblStanWartosc, lblStanProdukty, chkGrupowanie
            });

            // === STAN MAGAZYNU - pełnoekranowa tabela ===
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

            stanMainPanel.Controls.Add(dgvStanMagazynu);
            stanMainPanel.Controls.Add(stanToolbar);

            tab4.Controls.Add(stanMainPanel);

            // === ZAKŁADKA REZERWACJE ===
            TabPage tabRez = new TabPage("  Rezerwacje  ");
            tabRez.BackColor = BackgroundColor;
            tabRez.Padding = new Padding(0);

            Panel rezMainPanel = new Panel { Dock = DockStyle.Fill };

            // Toolbar rezerwacji
            Panel rezToolbar = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(250, 251, 253) };
            rezToolbar.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    e.Graphics.DrawLine(pen, 0, rezToolbar.Height - 1, rezToolbar.Width, rezToolbar.Height - 1);
            };

            Button btnDodajRez = CreateModernButton("+ Dodaj rezerwację", 15, 10, 145, SuccessColor);
            btnDodajRez.Click += (s, e) => DodajRezerwacjeZKarty();

            Button btnEdytujRez = CreateModernButton("Edytuj", 170, 10, 80, InfoColor);
            btnEdytujRez.Click += (s, e) => EdytujWybranaRezerwacje();

            Button btnUsunRez = CreateModernButton("Usuń", 260, 10, 80, DangerColor);
            btnUsunRez.Click += (s, e) => UsunWybranaRezerwacje();

            Label lblRezInfo = new Label
            {
                Text = "Kliknij dwukrotnie wiersz aby anulować  |  PPM = menu kontekstowe",
                Location = new Point(360, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(140, 140, 140)
            };

            rezToolbar.Controls.AddRange(new Control[] { btnDodajRez, btnEdytujRez, btnUsunRez, lblRezInfo });

            dgvZamowienia = CreateStyledDataGridView();
            dgvZamowienia.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(192, 40, 55);
            dgvZamowienia.CellDoubleClick += DgvRezerwacje_CellDoubleClick;

            // Menu kontekstowe rezerwacji
            ContextMenuStrip ctxMenuRez = new ContextMenuStrip();
            ctxMenuRez.Items.Add("Dodaj rezerwację", null, (s, e) => DodajRezerwacjeZKarty());
            ctxMenuRez.Items.Add("Edytuj rezerwację", null, (s, e) => EdytujWybranaRezerwacje());
            ctxMenuRez.Items.Add(new ToolStripSeparator());
            ctxMenuRez.Items.Add("Usuń rezerwację", null, (s, e) => UsunWybranaRezerwacje());
            dgvZamowienia.ContextMenuStrip = ctxMenuRez;

            rezMainPanel.Controls.Add(dgvZamowienia);
            rezMainPanel.Controls.Add(rezToolbar);

            tabRez.Controls.Add(rezMainPanel);

            // === ZAKŁADKA 5: MROŹNIE ZEWNĘTRZNE ===
            TabPage tab5 = new TabPage("  Mroźnie zewnętrzne  ");
            tab5.BackColor = BackgroundColor;
            tab5.Padding = new Padding(0);

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
                    splitZewn.SplitterDistance = (int)(splitZewn.Width * 0.40);
            };

            // Lewa: Split - Lista mroźni górna / Stan zbiorczy dolny
            Panel zewnLeftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            SplitContainer splitLeftMroznia = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                BackColor = BackgroundColor
            };
            splitLeftMroznia.SizeChanged += (s, e) => {
                if (splitLeftMroznia.Height > 0)
                    splitLeftMroznia.SplitterDistance = (int)(splitLeftMroznia.Height * 0.40);
            };

            // Górna część: Lista mroźni (6z - karty zamiast tabeli)
            Panel panelListaMrozni = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Panel zewnLeftHeader = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = SuccessColor };
            Label lblZewnLeft = new Label
            {
                Text = "\u2744 MROŹNIE ZEWNĘTRZNE",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            zewnLeftHeader.Controls.Add(lblZewnLeft);

            // Ukryty grid (dane wewnętrzne - używany do selekcji i wyszukiwania ID)
            dgvMroznieZewnetrzne = CreateStyledDataGridView();
            dgvMroznieZewnetrzne.ColumnHeadersDefaultCellStyle.BackColor = SuccessColor;
            dgvMroznieZewnetrzne.SelectionChanged += DgvMroznieZewnetrzne_SelectionChanged;
            dgvMroznieZewnetrzne.Visible = false;
            dgvMroznieZewnetrzne.Dock = DockStyle.None;
            dgvMroznieZewnetrzne.Size = new Size(0, 0);

            // Panel kart mroźni (6z)
            FlowLayoutPanel cardsPanel = CreateMroznieCardsPanel();

            panelListaMrozni.Controls.Add(cardsPanel);
            panelListaMrozni.Controls.Add(dgvMroznieZewnetrzne);
            panelListaMrozni.Controls.Add(zewnLeftHeader);

            // Dolna część: Stan zbiorczy wszystkich mroźni
            Panel panelStanZbiorczy = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Panel stanZbiorczyHeader = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(108, 117, 125) };
            Label lblStanZbiorczy = new Label
            {
                Text = "📦 STAN WSZYSTKICH MROŹNI ZEWNĘTRZNYCH",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            stanZbiorczyHeader.Controls.Add(lblStanZbiorczy);

            dgvStanMrozniZewnetrznych = CreateStyledDataGridView();
            dgvStanMrozniZewnetrznych.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(108, 117, 125);

            Panel stanZbiorczyGrid = new Panel { Dock = DockStyle.Fill };
            stanZbiorczyGrid.Controls.Add(dgvStanMrozniZewnetrznych);
            panelStanZbiorczy.Controls.Add(stanZbiorczyGrid);
            panelStanZbiorczy.Controls.Add(stanZbiorczyHeader);

            splitLeftMroznia.Panel1.Controls.Add(panelListaMrozni);
            splitLeftMroznia.Panel2.Controls.Add(panelStanZbiorczy);
            zewnLeftPanel.Controls.Add(splitLeftMroznia);

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

            // Panel filtra mroźni
            Panel filterPanelMroznia = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };
            Label lblFiltrMroznia = new Label
            {
                Text = "Filtruj ruchy:",
                Location = new Point(10, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            cmbFiltrMroznia = new ComboBox
            {
                Location = new Point(95, 8),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbFiltrMroznia.Items.Add("Wszystkie");
            cmbFiltrMroznia.SelectedIndex = 0;
            cmbFiltrMroznia.SelectedIndexChanged += (s, e) => DgvMroznieZewnetrzne_SelectionChanged(null, null);

            Label lblFiltrInfo = new Label
            {
                Text = "← wybierz mroźnię aby zobaczyć tylko jej ruchy",
                Location = new Point(305, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.Gray
            };

            filterPanelMroznia.Controls.AddRange(new Control[] { lblFiltrMroznia, cmbFiltrMroznia, lblFiltrInfo });

            dgvWydaniaZewnetrzne = CreateStyledDataGridView();
            dgvWydaniaZewnetrzne.ColumnHeadersDefaultCellStyle.BackColor = PrimaryColor;

            Panel zewnRightGrid = new Panel { Dock = DockStyle.Fill };
            zewnRightGrid.Controls.Add(dgvWydaniaZewnetrzne);
            zewnRightPanel.Controls.Add(zewnRightGrid);
            zewnRightPanel.Controls.Add(filterPanelMroznia);
            zewnRightPanel.Controls.Add(zewnRightHeader);

            splitZewn.Panel1.Controls.Add(zewnLeftPanel);
            splitZewn.Panel2.Controls.Add(zewnRightPanel);

            zewnMainPanel.Controls.Add(splitZewn);
            zewnMainPanel.Controls.Add(zewnToolbar);
            tab5.Controls.Add(zewnMainPanel);

            // Stan magazynu jako pierwsza zakładka
            tc.TabPages.AddRange(new TabPage[] { tab4, tabRez, tab5, tab1, tab3 });
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
                AllowUserToResizeRows = false,
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
            btnSzybkiRaport.Click += BtnSzybkiRaport_Click;
            btnEksport.Click += BtnEksport_Click;

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

            // Automatycznie załaduj przegląd dzienny i wykresy
            AutoLoadData();
        }

        private void LoadInitialData()
        {
        }

        // ============================================
        // GŁÓWNE FUNKCJE ANALITYCZNE
        // ============================================

        private async void AutoLoadData()
        {
            if (dtpOd.Value > dtpDo.Value) return;

            statusLabel.Text = "Ładowanie danych...";
            progressBar.Visible = true;
            progressBar.Value = 0;
            this.Cursor = Cursors.WaitCursor;
            DisableButtons();

            DateTime od = dtpOd.Value.Date;
            DateTime doDaty = dtpDo.Value.Date;

            try
            {
                progressBar.Value = 10;

                DataTable dtDzienne = null, dtTrendy = null;
                decimal wydanoSuma = 0, przyjetoSuma = 0;
                int dniSuma = 0;

                await Task.Run(() =>
                {
                    dtDzienne = FetchDzienneZestawienie(od, doDaty);
                    dtTrendy = FetchTrendyData(od, doDaty);
                    FetchKartyStatystyk(od, doDaty, out wydanoSuma, out przyjetoSuma, out dniSuma);
                });

                progressBar.Value = 50;
                DisplayDzienneZestawienie(dtDzienne);

                progressBar.Value = 70;
                DisplayTrendy(dtTrendy);

                progressBar.Value = 90;
                DisplayKartyStatystyk(wydanoSuma, przyjetoSuma, dniSuma);

                progressBar.Value = 100;
                lastAnalysisDate = DateTime.Now;
                statusLabel.Text = $"Dane za {od:dd.MM} - {doDaty:dd.MM.yyyy} | {DateTime.Now:HH:mm:ss}";

                ShowToast($"Załadowano dane ({dtDzienne?.Rows.Count ?? 0} dni)", SuccessColor);
                UpdateTabBadges();
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Błąd: {ex.Message}";
            }
            finally
            {
                progressBar.Visible = false;
                this.Cursor = Cursors.Default;
                EnableButtons();
            }
        }

        private DataTable FetchDzienneZestawienie(DateTime od, DateTime doDaty)
        {
            string query = $@"
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
                WHERE MG.magazyn = {MagazynMroznia}
                AND {SQL_FILTR_SERII}
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
                return dt;
            }
        }

        private void DisplayDzienneZestawienie(DataTable dt)
        {
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
                // Koloruj bilans
                if (row.Cells["Bilans"].Value != null)
                {
                    decimal bilans = Convert.ToDecimal(row.Cells["Bilans"].Value);
                    if (bilans < 0)
                    {
                        row.Cells["Bilans"].Style.ForeColor = SuccessColor;
                        row.Cells["Bilans"].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    }
                    else if (bilans > 0)
                    {
                        row.Cells["Bilans"].Style.ForeColor = DangerColor;
                        row.Cells["Bilans"].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    }
                }

                // Delikatne tło weekendów
                string dzien = row.Cells["DzienTygodnia"].Value?.ToString() ?? "";
                if (dzien == "Saturday" || dzien == "Sunday")
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 240);
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
            decimal wartosc = dgvStanMagazynu.Columns.Contains("Wartość (zł)")
                ? Convert.ToDecimal(row.Cells["Wartość (zł)"].Value ?? 0)
                : 0;

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
            string query = $@"
                SELECT
                    {SQL_GRUPOWANIE_PRODUKTU} AS Produkt,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) -
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Roznica,
                    COUNT(*) AS Operacje
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = {MagazynMroznia}
                AND {SQL_FILTR_SERII}
                AND CAST(MG.[Data] AS DATE) = @Data
                GROUP BY {SQL_GRUPOWANIE_PRODUKTU}
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
                        ShowToast("Dane wyeksportowane pomyślnie!", SuccessColor);
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

            ChartSeries series = new ChartSeries("Wydano")
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
            string query = $@"
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
                    WHERE MZ.magazyn = {MagazynMroznia}
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

            Chart chart = CreateStyledChart($"Trend stanu magazynowego: {produkt}");

            string query = $@"
                SELECT
                    MZ.[Data] AS Data,
                    MZ.iloscwp AS Stan
                FROM [HANDEL].[HM].[MZ]
                WHERE MZ.magazyn = {MagazynMroznia}
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
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);
                    cmd.Parameters.AddWithValue("@Produkt", produkt);
                    cmd.Parameters.AddWithValue("@ProduktPattern", produkt + "%");

                    ChartSeries seriesStan = new ChartSeries("Stan magazynowy")
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

                    ChartSeries seriesZero = new ChartSeries("Poziom zerowy")
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
            }

            chartForm.Controls.Add(chart);
            chartForm.ShowDialog();
        }

        private DataTable FetchTrendyData(DateTime od, DateTime doDaty)
        {
            string query = $@"
                SELECT
                    MG.[Data] AS Data,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = {MagazynMroznia}
                AND {SQL_FILTR_SERII}
                AND MG.[Data] BETWEEN @Od AND @Do
                GROUP BY MG.[Data]
                ORDER BY MG.[Data]";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlDataAdapter adapter = new SqlDataAdapter(query, conn))
                {
                    adapter.SelectCommand.Parameters.AddWithValue("@Od", od);
                    adapter.SelectCommand.Parameters.AddWithValue("@Do", doDaty);

                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        private void DisplayTrendy(DataTable dt)
        {
            if (liveChart == null) return;

            liveChart.Series.Clear();
            liveChart.AxisX.Clear();
            liveChart.AxisY.Clear();

            if (dt == null || dt.Rows.Count == 0) return;

            // Zbierz dane z bazy do słownika (dzień -> wartości)
            var dataMap = new Dictionary<DateTime, (double wydano, double przyjeto)>();
            DateTime minDate = DateTime.MaxValue, maxDate = DateTime.MinValue;

            foreach (DataRow row in dt.Rows)
            {
                DateTime data = Convert.ToDateTime(row["Data"]).Date;
                double wydano = Convert.ToDouble(row["Wydano"]);
                double przyjeto = Convert.ToDouble(row["Przyjeto"]);
                dataMap[data] = (wydano, przyjeto);

                if (data < minDate) minDate = data;
                if (data > maxDate) maxDate = data;
            }

            var wydaneValues = new ChartValues<double>();
            var przyjeciaValues = new ChartValues<double>();
            var labels = new List<string>();

            // Wypełnij KAŻDY dzień
            for (DateTime d = minDate; d <= maxDate; d = d.AddDays(1))
            {
                double wydano = 0, przyjeto = 0;
                if (dataMap.ContainsKey(d))
                {
                    wydano = dataMap[d].wydano;
                    przyjeto = dataMap[d].przyjeto;
                }
                wydaneValues.Add(wydano);
                przyjeciaValues.Add(przyjeto);
                labels.Add(d.ToString("dd.MM"));
            }

            // Seria Wydane - kolumny niebieskie
            var colWydane = new ColumnSeries
            {
                Title = "Wydane (kg)",
                Values = wydaneValues,
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(220, 41, 128, 185)),
                MaxColumnWidth = 30,
                ColumnPadding = 2,
                LabelPoint = p => p.Y >= 1000 ? (p.Y / 1000).ToString("0.#") + "k" : p.Y > 0 ? p.Y.ToString("N0") : "",
                DataLabels = true,
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(41, 128, 185))
            };

            // Seria Przyjęte - kolumny zielone
            var colPrzyjete = new ColumnSeries
            {
                Title = "Przyjęte (kg)",
                Values = przyjeciaValues,
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 46, 204, 113)),
                MaxColumnWidth = 30,
                ColumnPadding = 2,
                LabelPoint = p => p.Y >= 1000 ? (p.Y / 1000).ToString("0.#") + "k" : p.Y > 0 ? p.Y.ToString("N0") : "",
                DataLabels = true,
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(46, 204, 113))
            };

            liveChart.Series.Add(colWydane);
            liveChart.Series.Add(colPrzyjete);

            // Oś X - każdy dzień
            liveChart.AxisX.Add(new Axis
            {
                Labels = labels,
                LabelsRotation = -45,
                FontSize = 9,
                Separator = new Separator { Step = 1, IsEnabled = false },
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(100, 100, 100))
            });

            // Oś Y - skrócone etykiety (10k, 50k)
            liveChart.AxisY.Add(new Axis
            {
                Title = "kg",
                LabelFormatter = val => val >= 1000 ? (val / 1000).ToString("0.#") + "k" : val.ToString("N0"),
                FontSize = 10,
                Separator = new Separator
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(235, 237, 240)),
                    StrokeThickness = 1
                },
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(100, 100, 100))
            });
        }

        private void ResetChartZoom()
        {
            // LiveCharts nie wymaga ręcznego resetu zoom
        }

        private void FetchKartyStatystyk(DateTime od, DateTime doDaty, out decimal wydano, out decimal przyjeto, out int dni)
        {
            string query = $@"
                SELECT
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Wydano,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Przyjeto,
                    COUNT(DISTINCT MG.[Data]) AS Dni
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = {MagazynMroznia}
                AND {SQL_FILTR_SERII}
                AND MG.[Data] BETWEEN @Od AND @Do";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Od", od);
                    cmd.Parameters.AddWithValue("@Do", doDaty);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            wydano = Convert.ToDecimal(reader[0]);
                            przyjeto = Convert.ToDecimal(reader[1]);
                            dni = Convert.ToInt32(reader[2]);
                        }
                        else
                        {
                            wydano = 0;
                            przyjeto = 0;
                            dni = 0;
                        }
                    }
                }
            }
        }

        private void DisplayKartyStatystyk(decimal wydano, decimal przyjeto, int dni)
        {
            lastWydano = wydano;
            lastPrzyjeto = przyjeto;
            lastDni = dni;
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
                string query = $@"
                    SELECT kod,
                           CAST(ABS(SUM([iloscwp])) AS DECIMAL(18,3)) AS SumaIlosc,
                           CAST(ABS(SUM([wartNetto])) AS DECIMAL(18,2)) AS SumaWartosc
                    FROM [HANDEL].[HM].[MZ]
                    WHERE [data] >= '{DataStartowa}'
                      AND [data] <= @EndDate
                      AND [magazyn] = {MagazynMroznia}
                      AND typ = '0'
                    GROUP BY kod
                    HAVING ABS(SUM([iloscwp])) <> 0
                    ORDER BY SumaIlosc DESC";

                // Zapytanie dla stanu tydzień wcześniej
                string queryPoprzedni = $@"
                    SELECT kod,
                           CAST(ABS(SUM([iloscwp])) AS DECIMAL(18,3)) AS SumaIlosc
                    FROM [HANDEL].[HM].[MZ]
                    WHERE [data] >= '{DataStartowa}'
                      AND [data] <= @EndDate
                      AND [magazyn] = {MagazynMroznia}
                      AND typ = '0'
                    GROUP BY kod
                    HAVING ABS(SUM([iloscwp])) <> 0";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz aktualny stan
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@EndDate", dataStan);

                    DataTable dtRaw = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dtRaw);
                    }

                    // Pobierz stan z tygodnia wcześniej
                    SqlCommand cmdPoprzedni = new SqlCommand(queryPoprzedni, conn);
                    cmdPoprzedni.Parameters.AddWithValue("@EndDate", dataPoprzedni);

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

                        // Pobierz stany mroźni zewnętrznych per produkt
                        var stanyMrozniZewn = GetStanMrozniZewnetrznychPerProdukt();
                        var nazwyMrozniZewn = stanyMrozniZewn.Values
                            .SelectMany(d => d.Keys)
                            .Distinct()
                            .OrderBy(n => n)
                            .ToList();
                        bool maJakasZewnetrznaMroznia = nazwyMrozniZewn.Count > 0;

                        dtFinal = new DataTable();
                        dtFinal.Columns.Add("Kod", typeof(string));
                        dtFinal.Columns.Add("Stan (kg)", typeof(decimal));

                        // Dodaj kolumny dla mroźni zewnętrznych jeśli są
                        if (maJakasZewnetrznaMroznia)
                        {
                            foreach (var nazwaMrozni in nazwyMrozniZewn)
                            {
                                dtFinal.Columns.Add($"MZ: {nazwaMrozni}", typeof(decimal));
                            }
                            dtFinal.Columns.Add("Suma Stan", typeof(decimal));
                        }

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

                            var row = dtFinal.NewRow();
                            row["Kod"] = kod;
                            row["Stan (kg)"] = stan;

                            // Wypełnij stany mroźni zewnętrznych
                            decimal sumaMrozniZewn = 0;
                            if (maJakasZewnetrznaMroznia)
                            {
                                foreach (var nazwaMrozni in nazwyMrozniZewn)
                                {
                                    decimal stanMrozni = 0;
                                    if (stanyMrozniZewn.ContainsKey(kod) && stanyMrozniZewn[kod].ContainsKey(nazwaMrozni))
                                        stanMrozni = stanyMrozniZewn[kod][nazwaMrozni];
                                    row[$"MZ: {nazwaMrozni}"] = stanMrozni;
                                    sumaMrozniZewn += stanMrozni;
                                }
                                row["Suma Stan"] = stan + sumaMrozniZewn;
                            }

                            row["Rez. (kg)"] = rezerwacja;
                            row["Zmiana"] = zmiana;
                            row["Status"] = status;

                            dtFinal.Rows.Add(row);
                        }

                        // Dodaj produkty które są tylko na mroźniach zewnętrznych
                        foreach (var kodZewn in stanyMrozniZewn.Keys)
                        {
                            if (!scaloneDane.ContainsKey(kodZewn))
                            {
                                var row = dtFinal.NewRow();
                                row["Kod"] = kodZewn;
                                row["Stan (kg)"] = 0m;

                                decimal sumaMrozniZewn = 0;
                                foreach (var nazwaMrozni in nazwyMrozniZewn)
                                {
                                    decimal stanMrozni = stanyMrozniZewn[kodZewn].ContainsKey(nazwaMrozni)
                                        ? stanyMrozniZewn[kodZewn][nazwaMrozni]
                                        : 0;
                                    row[$"MZ: {nazwaMrozni}"] = stanMrozni;
                                    sumaMrozniZewn += stanMrozni;
                                }
                                row["Suma Stan"] = sumaMrozniZewn;
                                row["Rez. (kg)"] = 0m;
                                row["Zmiana"] = "";
                                row["Status"] = sumaMrozniZewn > 0 ? "OK" : "";

                                if (sumaMrozniZewn != 0)
                                    dtFinal.Rows.Add(row);
                            }
                        }
                    }

                    dgvStanMagazynu.DataSource = dtFinal;

                    // Formatowanie
                    string colName = isGrupowanie ? "Produkt" : "Kod";
                    FormatujKolumne(dgvStanMagazynu, "Stan (kg)", "Stan", "N0");
                    FormatujKolumne(dgvStanMagazynu, "Rez. (kg)", "Rez.", "N0");
                    FormatujKolumne(dgvStanMagazynu, "Zmiana", "Zmiana");

                    // Formatuj kolumny mroźni zewnętrznych
                    foreach (DataGridViewColumn col in dgvStanMagazynu.Columns)
                    {
                        if (col.Name.StartsWith("MZ: "))
                        {
                            col.DefaultCellStyle.Format = "N0";
                            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                            col.DefaultCellStyle.BackColor = Color.FromArgb(255, 253, 240);
                            col.HeaderCell.Style.BackColor = Color.FromArgb(255, 193, 7);
                            col.HeaderCell.Style.ForeColor = Color.Black;
                        }
                    }

                    // Formatuj kolumnę Suma Stan
                    if (dgvStanMagazynu.Columns.Contains("Suma Stan"))
                    {
                        var colSuma = dgvStanMagazynu.Columns["Suma Stan"];
                        colSuma.DefaultCellStyle.Format = "N0";
                        colSuma.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        colSuma.DefaultCellStyle.BackColor = Color.FromArgb(230, 247, 255);
                        colSuma.HeaderCell.Style.BackColor = Color.FromArgb(0, 123, 255);
                        colSuma.HeaderCell.Style.ForeColor = Color.White;
                    }

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

                    // 7cc: Podświetl wiersze ze zmianami
                    var changedRows = new HashSet<int>();
                    for (int i = 1; i < dgvStanMagazynu.Rows.Count; i++)
                    {
                        string zmiana = dgvStanMagazynu.Rows[i].Cells["Zmiana"].Value?.ToString();
                        if (!string.IsNullOrEmpty(zmiana) && !zmiana.Contains("bez zmian"))
                            changedRows.Add(i);
                    }
                    if (changedRows.Count > 0)
                        HighlightChangedRows(dgvStanMagazynu, changedRows);
                }

                statusLabel.Text = $"Stan magazynu na {dataStan:yyyy-MM-dd} (porównanie z {dataPoprzedni:yyyy-MM-dd})";

                // 1d: Aktualizuj badge'e
                UpdateTabBadges();
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
            decimal srednia = lastDni > 0 ? lastWydano / lastDni : 0;
            raport.AppendLine($"Wydano:          {lastWydano:N0} kg");
            raport.AppendLine($"Przyjęto:        {lastPrzyjeto:N0} kg");
            raport.AppendLine($"Średnio/dzień:   {srednia:N0} kg");
            raport.AppendLine($"Status:          {lastDni} dni analizy");
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
                ShowToast("Raport skopiowany do schowka!", PrimaryColor);
            };

            btnPanel.Controls.AddRange(new Control[] { btnZamknij, btnKopiuj });

            raportForm.Controls.Add(txtRaport);
            raportForm.Controls.Add(btnPanel);
            raportForm.ShowDialog();
        }

        private void BtnEksport_Click(object sender, EventArgs e)
        {
            if (dgvDzienne.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu.",
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
                        ExportToCSV(dgvDzienne, sfd.FileName);

                        ShowToast($"Wyeksportowano: {Path.GetFileName(sfd.FileName)}", SuccessColor);
                        statusLabel.Text = $"Wyeksportowano do: {Path.GetFileName(sfd.FileName)}";

                        if (MessageBox.Show($"Czy otworzyć plik?",
                            "Eksport zakończony", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                        }
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
            btnSzybkiRaport.Enabled = false;
            btnEksport.Enabled = false;
        }

        private void EnableButtons()
        {
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
                    string query = $@"SELECT Id, Kod, Nazwa, katalog
                                    FROM [HANDEL].[HM].[TW]
                                    WHERE katalog IN ('{KatalogSwiezy}', '{KatalogMrozony}')
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

                            if (towar.Katalog == KatalogSwiezy)
                                towarySwiezy.Add(towar);
                            else if (towar.Katalog == KatalogMrozony)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania rezerwacji: {ex.Message}");
            }
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
                string json = JsonSerializer.Serialize(rezerwacje, options);

                // Atomowy zapis - najpierw do pliku tymczasowego, potem zamiana
                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu rezerwacji: {ex.Message}\n\nDane mogły nie zostać zapisane.",
                    "Błąd zapisu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                lblStanRezerwacje.Text = $"{sumaRez:N0} kg";
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
                ShowToast($"Anulowano rezerwację: {produkt}", WarningColor);
            }
        }

        /// <summary>
        /// Dodaje nową rezerwację z zakładki Rezerwacje (wpisanie kodu produktu ręcznie)
        /// </summary>
        private void DodajRezerwacjeZKarty()
        {
            using (var dlg = new RezerwacjaEditorDialog(null, GetCurrentHandlowiec(), connectionString))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var rezerwacje = WczytajRezerwacje();
                    var nowa = new RezerwacjaItem
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                        KodProduktu = dlg.KodProduktu,
                        Ilosc = dlg.Ilosc,
                        Handlowiec = dlg.Handlowiec,
                        DataRezerwacji = DateTime.Now,
                        DataWaznosci = dlg.DataWaznosci,
                        Uwagi = dlg.Uwagi
                    };
                    rezerwacje.Add(nowa);
                    ZapiszRezerwacje(rezerwacje);

                    BtnStanMagazynu_Click(null, null);
                    ShowToast($"Dodano rezerwację: {dlg.KodProduktu} ({dlg.Ilosc:N0} kg)", SuccessColor);
                }
            }
        }

        /// <summary>
        /// Edytuje wybraną rezerwację z tabeli
        /// </summary>
        private void EdytujWybranaRezerwacje()
        {
            if (dgvZamowienia.SelectedRows.Count == 0)
            {
                ShowToast("Zaznacz rezerwację do edycji", WarningColor);
                return;
            }

            var row = dgvZamowienia.SelectedRows[0];
            string id = row.Cells["ID"].Value?.ToString() ?? "";

            var rezerwacje = WczytajRezerwacje();
            var rez = rezerwacje.FirstOrDefault(r => r.Id == id);
            if (rez == null) return;

            using (var dlg = new RezerwacjaEditorDialog(rez, GetCurrentHandlowiec(), connectionString))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    rez.KodProduktu = dlg.KodProduktu;
                    rez.Ilosc = dlg.Ilosc;
                    rez.Handlowiec = dlg.Handlowiec;
                    rez.DataWaznosci = dlg.DataWaznosci;
                    rez.Uwagi = dlg.Uwagi;
                    ZapiszRezerwacje(rezerwacje);

                    BtnStanMagazynu_Click(null, null);
                    ShowToast($"Zaktualizowano rezerwację: {dlg.KodProduktu}", InfoColor);
                }
            }
        }

        /// <summary>
        /// Usuwa wybraną rezerwację z tabeli
        /// </summary>
        private void UsunWybranaRezerwacje()
        {
            if (dgvZamowienia.SelectedRows.Count == 0)
            {
                ShowToast("Zaznacz rezerwację do usunięcia", WarningColor);
                return;
            }

            var row = dgvZamowienia.SelectedRows[0];
            string id = row.Cells["ID"].Value?.ToString() ?? "";
            string produkt = row.Cells["Produkt"].Value?.ToString() ?? "";
            decimal ilosc = Convert.ToDecimal(row.Cells["Ilość kg"].Value ?? 0);

            var result = MessageBox.Show(
                $"Usunąć rezerwację?\n\nProdukt: {produkt}\nIlość: {ilosc:N0} kg",
                "Potwierdź usunięcie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                var rezerwacje = WczytajRezerwacje();
                rezerwacje.RemoveAll(r => r.Id == id);
                ZapiszRezerwacje(rezerwacje);

                BtnStanMagazynu_Click(null, null);
                ShowToast($"Usunięto rezerwację: {produkt}", DangerColor);
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
                    // Walidacja danych rezerwacji
                    if (dlg.Ilosc <= 0)
                    {
                        MessageBox.Show("Ilość musi być większa od zera.", "Błąd walidacji",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (dlg.Ilosc > dostepne)
                    {
                        MessageBox.Show($"Ilość ({dlg.Ilosc:N0} kg) przekracza dostępne ({dostepne:N0} kg).",
                            "Przekroczenie limitu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (dlg.DataWaznosci < DateTime.Today)
                    {
                        MessageBox.Show("Data ważności nie może być w przeszłości.", "Błąd walidacji",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

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
                    ShowToast($"Zarezerwowano {dlg.Ilosc:N0} kg {kodProduktu}", SuccessColor);
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
                ShowToast($"Usunięto rezerwacje: {kodProduktu}", DangerColor);
            }
        }

        private void PokazHistorieWybranegoProduktu()
        {
            if (dgvStanMagazynu.SelectedRows.Count == 0) return;

            DataGridViewRow row = dgvStanMagazynu.SelectedRows[0];
            string kodProduktu = row.Cells["Kod"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(kodProduktu) || kodProduktu == "SUMA") return;

            decimal stan = Convert.ToDecimal(row.Cells["Stan (kg)"].Value ?? 0);
            decimal wartosc = dgvStanMagazynu.Columns.Contains("Wartość (zł)")
                ? Convert.ToDecimal(row.Cells["Wartość (zł)"].Value ?? 0)
                : 0;

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
            lblStanSuma.Text = $"{sumaStan:N0} kg";
            var rezerwacje = WczytajRezerwacje();
            decimal sumaRezerwacji = rezerwacje.Sum(r => r.Ilosc);
            lblStanRezerwacje.Text = $"{sumaRezerwacji:N0} kg";
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

            // 6z: Odśwież karty mroźni
            RefreshMroznieCards();

            // Załaduj stan zbiorczy wszystkich mroźni
            LoadStanZbiorczyMrozniZewnetrznych(mroznie);

            // 1d: Aktualizuj badge'e
            UpdateTabBadges();
        }

        private void LoadStanZbiorczyMrozniZewnetrznych(List<MrozniaZewnetrzna> mroznie)
        {
            // Słownik: kod produktu -> (nazwa produktu, słownik: nazwa mroźni -> ilość)
            var stanPerProdukt = new Dictionary<string, (string Nazwa, Dictionary<string, decimal> StanyPerMroznia)>(StringComparer.OrdinalIgnoreCase);

            foreach (var mroznia in mroznie)
            {
                if (mroznia.Wydania == null) continue;

                foreach (var w in mroznia.Wydania)
                {
                    string kod = w.KodProduktu ?? "";
                    if (string.IsNullOrEmpty(kod)) continue;

                    if (!stanPerProdukt.ContainsKey(kod))
                        stanPerProdukt[kod] = (w.Produkt ?? kod, new Dictionary<string, decimal>());

                    var stanyMrozni = stanPerProdukt[kod].StanyPerMroznia;
                    if (!stanyMrozni.ContainsKey(mroznia.Nazwa))
                        stanyMrozni[mroznia.Nazwa] = 0;

                    if (w.Typ == "Przyjęcie")
                        stanyMrozni[mroznia.Nazwa] += w.Ilosc;
                    else if (w.Typ == "Wydanie")
                        stanyMrozni[mroznia.Nazwa] -= w.Ilosc;
                }
            }

            // Utwórz DataTable z kolumnami dla każdej mroźni
            DataTable dt = new DataTable();
            dt.Columns.Add("Kod", typeof(string));

            // Dodaj kolumny dla każdej mroźni (z sufiksem kg)
            var nazwyMrozni = mroznie.Select(m => m.Nazwa).OrderBy(n => n).ToList();
            foreach (var nazwa in nazwyMrozni)
            {
                dt.Columns.Add(nazwa + " (kg)", typeof(decimal));
            }
            dt.Columns.Add("SUMA (kg)", typeof(decimal));

            // Wypełnij danymi
            foreach (var kvp in stanPerProdukt.OrderByDescending(x => x.Value.StanyPerMroznia.Values.Sum()))
            {
                var row = dt.NewRow();
                row["Kod"] = kvp.Key;

                decimal suma = 0;
                foreach (var nazwa in nazwyMrozni)
                {
                    decimal stanMrozni = kvp.Value.StanyPerMroznia.ContainsKey(nazwa) ? kvp.Value.StanyPerMroznia[nazwa] : 0;
                    row[nazwa + " (kg)"] = stanMrozni;
                    suma += stanMrozni;
                }
                row["SUMA (kg)"] = suma;

                // Dodaj tylko jeśli jest jakiś stan
                if (suma != 0)
                    dt.Rows.Add(row);
            }

            dgvStanMrozniZewnetrznych.DataSource = dt;

            // Formatowanie
            dgvStanMrozniZewnetrznych.BeginInvoke(new Action(() =>
            {
                try
                {
                    foreach (DataGridViewColumn col in dgvStanMrozniZewnetrznych.Columns)
                    {
                        if (col.ValueType == typeof(decimal))
                        {
                            col.DefaultCellStyle.Format = "N0";
                            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        }
                    }

                    // Wyróżnij kolumnę SUMA (kg)
                    if (dgvStanMrozniZewnetrznych.Columns["SUMA (kg)"] != null)
                    {
                        dgvStanMrozniZewnetrznych.Columns["SUMA (kg)"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        dgvStanMrozniZewnetrznych.Columns["SUMA (kg)"].DefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255);
                    }
                }
                catch { }
            }));
        }

        /// <summary>
        /// Pobiera stan mroźni zewnętrznych per produkt (dla integracji ze stanem magazynu)
        /// </summary>
        private Dictionary<string, Dictionary<string, decimal>> GetStanMrozniZewnetrznychPerProdukt()
        {
            var mroznie = WczytajMroznieZewnetrzne();
            var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mroznia in mroznie)
            {
                if (mroznia.Wydania == null) continue;

                foreach (var w in mroznia.Wydania)
                {
                    string kod = w.KodProduktu ?? "";
                    if (string.IsNullOrEmpty(kod)) continue;

                    if (!result.ContainsKey(kod))
                        result[kod] = new Dictionary<string, decimal>();

                    if (!result[kod].ContainsKey(mroznia.Nazwa))
                        result[kod][mroznia.Nazwa] = 0;

                    if (w.Typ == "Przyjęcie")
                        result[kod][mroznia.Nazwa] += w.Ilosc;
                    else if (w.Typ == "Wydanie")
                        result[kod][mroznia.Nazwa] -= w.Ilosc;
                }
            }

            return result;
        }

        private void DgvMroznieZewnetrzne_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvMroznieZewnetrzne.SelectedRows.Count == 0)
            {
                dgvWydaniaZewnetrzne.DataSource = null;
                UpdateFiltrMrozniComboBox();
                return;
            }

            string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            var mroznie = WczytajMroznieZewnetrzne();
            var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
            if (mroznia == null) return;

            // Aktualizuj ComboBox filtra
            UpdateFiltrMrozniComboBox();

            // Pobierz filtr
            string filtrMroznia = cmbFiltrMroznia?.SelectedItem?.ToString() ?? "Wszystkie";

            DataTable dt = new DataTable();
            dt.Columns.Add("Data", typeof(DateTime));
            dt.Columns.Add("Typ", typeof(string));
            dt.Columns.Add("Trasa", typeof(string));
            dt.Columns.Add("Kod", typeof(string));
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Ilość (kg)", typeof(decimal));
            dt.Columns.Add("Klient", typeof(string));

            if (mroznia.Wydania != null)
            {
                foreach (var w in mroznia.Wydania.OrderByDescending(x => x.Data))
                {
                    // Filtruj po mroźni jeśli wybrano konkretną
                    if (filtrMroznia != "Wszystkie")
                    {
                        bool pasuje = w.ZrodloNazwa?.Contains(filtrMroznia) == true ||
                                      w.CelNazwa?.Contains(filtrMroznia) == true ||
                                      mroznia.Nazwa == filtrMroznia;
                        if (!pasuje) continue;
                    }

                    string trasa = w.Trasa;
                    if (string.IsNullOrEmpty(trasa))
                        trasa = w.Typ == "Przyjęcie" ? "Ubojnia → Mroźnia" : "Mroźnia → Ubojnia";

                    string klient = !string.IsNullOrEmpty(w.KlientId) ? w.CelNazwa : "";

                    dt.Rows.Add(w.Data, w.Typ, trasa, w.KodProduktu ?? "", w.Produkt, w.Ilosc, klient);
                }
            }

            dgvWydaniaZewnetrzne.DataSource = dt;
            dgvWydaniaZewnetrzne.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Formatowanie kolumn - opóźnione przez BeginInvoke
            dgvWydaniaZewnetrzne.BeginInvoke(new Action(() =>
            {
                try
                {
                    foreach (DataGridViewColumn col in dgvWydaniaZewnetrzne.Columns)
                    {
                        if (col.Name == "Data")
                            col.DefaultCellStyle.Format = "dd.MM.yyyy";
                        else if (col.Name == "Ilość (kg)")
                            col.DefaultCellStyle.Format = "N0";
                    }

                    foreach (DataGridViewRow row in dgvWydaniaZewnetrzne.Rows)
                    {
                        if (row.IsNewRow) continue;
                        var typCell = row.Cells["Typ"];
                        if (typCell == null) continue;
                        string typ = typCell.Value?.ToString();
                        if (typ == "Przyjęcie")
                            row.DefaultCellStyle.ForeColor = Color.FromArgb(40, 167, 69);
                        else if (typ == "Wydanie")
                            row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 53, 69);
                    }
                }
                catch { }
            }));
        }

        private void UpdateFiltrMrozniComboBox()
        {
            if (cmbFiltrMroznia == null || isUpdatingFiltrMroznia) return;

            isUpdatingFiltrMroznia = true;
            try
            {
                string selected = cmbFiltrMroznia.SelectedItem?.ToString() ?? "Wszystkie";
                cmbFiltrMroznia.Items.Clear();
                cmbFiltrMroznia.Items.Add("Wszystkie");

                var mroznie = WczytajMroznieZewnetrzne();
                foreach (var m in mroznie)
                {
                    cmbFiltrMroznia.Items.Add(m.Nazwa);
                }

                int idx = cmbFiltrMroznia.Items.IndexOf(selected);
                cmbFiltrMroznia.SelectedIndex = idx >= 0 ? idx : 0;
            }
            finally
            {
                isUpdatingFiltrMroznia = false;
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
                    ShowToast($"Dodano mroźnię: {dlg.NazwaMrozni}", SuccessColor);
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
                    ShowToast($"Zaktualizowano: {dlg.NazwaMrozni}", PrimaryColor);
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
                ShowToast($"Usunięto mroźnię: {nazwa}", DangerColor);
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
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Pozycje.Count > 0)
                {
                    var mroznie = WczytajMroznieZewnetrzne();
                    var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
                    if (mroznia != null)
                    {
                        if (mroznia.Wydania == null)
                            mroznia.Wydania = new List<WydanieZewnetrzne>();

                        // Dodaj wszystkie pozycje z dokumentu
                        decimal sumaIlosc = 0;
                        foreach (var poz in dlg.Pozycje)
                        {
                            mroznia.Wydania.Add(new WydanieZewnetrzne
                            {
                                Id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                                Data = dlg.Data,
                                Typ = dlg.Typ,
                                KodProduktu = poz.KodProduktu,
                                Produkt = poz.Nazwa,
                                Ilosc = poz.Ilosc,
                                Uwagi = dlg.Uwagi,
                                // Trasa
                                ZrodloTyp = dlg.ZrodloTyp,
                                ZrodloNazwa = dlg.ZrodloNazwa,
                                CelTyp = dlg.CelTyp,
                                CelNazwa = dlg.CelNazwa,
                                KlientId = dlg.KlientId
                            });
                            sumaIlosc += poz.Ilosc;
                        }

                        ZapiszMroznieZewnetrzne(mroznie);
                        LoadMroznieZewnetrzneDoTabeli();
                        DgvMroznieZewnetrzne_SelectionChanged(null, null);
                        statusLabel.Text = $"{dlg.Typ}: {dlg.Pozycje.Count} pozycji, razem {sumaIlosc:N0} kg ({nazwa})";
                        Color toastColor = dlg.Typ == "Przyjęcie" ? SuccessColor : WarningColor;
                        ShowToast($"{dlg.Typ}: {sumaIlosc:N0} kg ({nazwa})", toastColor);
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
    /// Dialog dodawania/edycji rezerwacji z ComboBox produktów z bazy
    /// </summary>
    public class RezerwacjaEditorDialog : Form
    {
        public string KodProduktu { get; private set; } = "";
        public decimal Ilosc { get; private set; }
        public string Handlowiec { get; private set; } = "";
        public DateTime DataWaznosci { get; private set; }
        public string Uwagi { get; private set; } = "";

        private ComboBox cmbProdukt;
        private List<ProduktMrozony> produkty = new List<ProduktMrozony>();

        public RezerwacjaEditorDialog(RezerwacjaItem existing, string defaultHandlowiec, string connectionString)
        {
            bool isEdit = existing != null;
            this.Text = isEdit ? "Edytuj rezerwację" : "Nowa rezerwacja";
            this.Size = new Size(480, 370);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);

            // Załaduj produkty z bazy
            LoadProduktyZBazy(connectionString);

            int y = 15;
            int labelX = 15;
            int inputX = 140;
            int inputW = 300;

            // Produkt (ComboBox z wyszukiwaniem)
            Label lblKod = new Label { Text = "Produkt:", Location = new Point(labelX, y + 3), AutoSize = true };
            cmbProdukt = new ComboBox
            {
                Location = new Point(inputX, y),
                Size = new Size(inputW, 28),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Font = new Font("Segoe UI", 10F)
            };
            cmbProdukt.DropDownWidth = 450;

            foreach (var p in produkty)
                cmbProdukt.Items.Add($"{p.Kod} - {p.Nazwa}");

            // Ustaw wybraną wartość przy edycji
            if (isEdit && !string.IsNullOrEmpty(existing.KodProduktu))
            {
                var match = produkty.FindIndex(p => p.Kod == existing.KodProduktu);
                if (match >= 0)
                    cmbProdukt.SelectedIndex = match;
                else
                    cmbProdukt.Text = existing.KodProduktu;
            }
            y += 40;

            // Ilość
            Label lblIlosc = new Label { Text = "Ilość (kg):", Location = new Point(labelX, y + 3), AutoSize = true };
            NumericUpDown nudIlosc = new NumericUpDown
            {
                Location = new Point(inputX, y),
                Size = new Size(120, 28),
                Minimum = 1,
                Maximum = 999999,
                Value = Math.Max(1, existing?.Ilosc ?? 100),
                DecimalPlaces = 0,
                Font = new Font("Segoe UI", 10F)
            };
            y += 40;

            // Handlowiec
            Label lblHandlowiec = new Label { Text = "Handlowiec:", Location = new Point(labelX, y + 3), AutoSize = true };
            TextBox txtHandlowiec = new TextBox
            {
                Location = new Point(inputX, y),
                Size = new Size(inputW, 28),
                Text = existing?.Handlowiec ?? defaultHandlowiec,
                Font = new Font("Segoe UI", 10F)
            };
            y += 40;

            // Data ważności
            Label lblWaznosc = new Label { Text = "Ważna do:", Location = new Point(labelX, y + 3), AutoSize = true };
            DateTimePicker dtpWaznosc = new DateTimePicker
            {
                Location = new Point(inputX, y),
                Size = new Size(140, 28),
                Format = DateTimePickerFormat.Short,
                Value = existing?.DataWaznosci ?? DateTime.Today.AddDays(7),
                MinDate = DateTime.Today,
                Font = new Font("Segoe UI", 10F)
            };
            y += 40;

            // Uwagi
            Label lblUwagi = new Label { Text = "Uwagi:", Location = new Point(labelX, y + 3), AutoSize = true };
            TextBox txtUwagi = new TextBox
            {
                Location = new Point(inputX, y),
                Size = new Size(inputW, 28),
                Text = existing?.Uwagi ?? "",
                Font = new Font("Segoe UI", 10F)
            };
            y += 50;

            // Przyciski
            Button btnOK = new Button
            {
                Text = isEdit ? "Zapisz" : "Dodaj rezerwację",
                Location = new Point(inputX, y),
                Size = new Size(130, 36),
                DialogResult = DialogResult.OK,
                BackColor = isEdit ? Color.FromArgb(0, 120, 212) : Color.FromArgb(16, 124, 16),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;

            Button btnCancel = new Button
            {
                Text = "Anuluj",
                Location = new Point(inputX + 140, y),
                Size = new Size(80, 36),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };

            // Walidacja
            btnOK.Click += (s, e) =>
            {
                string wybranyKod = GetSelectedKod();
                if (string.IsNullOrWhiteSpace(wybranyKod))
                {
                    MessageBox.Show("Wybierz produkt z listy.", "Brak produktu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtHandlowiec.Text))
                {
                    MessageBox.Show("Wpisz handlowca.", "Brak handlowca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                KodProduktu = wybranyKod;
                Ilosc = nudIlosc.Value;
                Handlowiec = txtHandlowiec.Text.Trim();
                DataWaznosci = dtpWaznosc.Value.Date;
                Uwagi = txtUwagi.Text.Trim();
            };

            this.Controls.AddRange(new Control[] {
                lblKod, cmbProdukt, lblIlosc, nudIlosc, lblHandlowiec, txtHandlowiec,
                lblWaznosc, dtpWaznosc, lblUwagi, txtUwagi, btnOK, btnCancel
            });
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private string GetSelectedKod()
        {
            if (cmbProdukt.SelectedIndex >= 0 && cmbProdukt.SelectedIndex < produkty.Count)
                return produkty[cmbProdukt.SelectedIndex].Kod;

            // Spróbuj dopasować wpisany tekst
            string text = cmbProdukt.Text.Trim();
            var match = produkty.FirstOrDefault(p =>
                text.StartsWith(p.Kod, StringComparison.OrdinalIgnoreCase) ||
                text.Equals($"{p.Kod} - {p.Nazwa}", StringComparison.OrdinalIgnoreCase));
            return match?.Kod ?? "";
        }

        private void LoadProduktyZBazy(string connectionString)
        {
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                        $"SELECT Id, Kod, Nazwa FROM [HANDEL].[HM].[TW] WHERE katalog IN ('{Mroznia.KatalogSwiezy}', '{Mroznia.KatalogMrozony}') ORDER BY Nazwa ASC", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            produkty.Add(new ProduktMrozony
                            {
                                Id = rd.GetInt32(0),
                                Kod = rd["Kod"]?.ToString() ?? "",
                                Nazwa = rd["Nazwa"]?.ToString() ?? ""
                            });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania produktów: {ex.Message}");
            }
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

        // Trasa - od kogo do kogo
        public string ZrodloTyp { get; set; } = "";
        public string ZrodloNazwa { get; set; } = "";
        public string CelTyp { get; set; } = "";
        public string CelNazwa { get; set; } = "";
        public string KlientId { get; set; } = "";

        // Helper do wyświetlania trasy
        public string Trasa => !string.IsNullOrEmpty(ZrodloNazwa) && !string.IsNullOrEmpty(CelNazwa)
            ? $"{ZrodloNazwa} → {CelNazwa}"
            : "";
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
    /// Dialog wydania/przyjęcia - WIELOPRODUKTOWY Z TRASĄ (OD KOGO -> DO KOGO)
    /// </summary>
    public class WydanieZewnetrzneDialog : Form
    {
        // Wyniki
        public List<PozycjaDokumentu> Pozycje { get; private set; } = new List<PozycjaDokumentu>();
        public DateTime Data { get; private set; }
        public string Typ { get; private set; } = "";
        public string Uwagi { get; private set; } = "";

        // Źródło i cel
        public string ZrodloTyp { get; private set; } = "";
        public string ZrodloNazwa { get; private set; } = "";
        public string CelTyp { get; private set; } = "";
        public string CelNazwa { get; private set; } = "";
        public string KlientId { get; private set; } = "";

        // Dla kompatybilności wstecznej
        public string KodProduktu => Pozycje.Count > 0 ? Pozycje[0].KodProduktu : "";
        public string Produkt => Pozycje.Count > 0 ? Pozycje[0].Nazwa : "";
        public decimal Ilosc => Pozycje.Count > 0 ? Pozycje[0].Ilosc : 0;

        // Kontrolki
        private Panel headerPanel;
        private Label lblHeader, lblTrasa;
        private DateTimePicker dtpData;
        private TextBox txtSearch, txtUwagi;
        private FlowLayoutPanel flowProdukty;
        private Label lblProduktyInfo;
        private NumericUpDown nudIlosc;
        private Button btnZapisz, btnDodajDoKoszyka;
        private DataGridView dgvKoszyk;
        private Label lblKoszykSuma, lblKoszykPozycje;

        // Źródło/Cel
        private ComboBox cmbZrodloTyp, cmbZrodloNazwa, cmbCelTyp, cmbCelNazwa;
        private Label lblZrodloInfo, lblCelInfo;

        // Dane
        private List<ProduktMrozony> produkty = new List<ProduktMrozony>();
        private List<KlientSymfonia> klienci = new List<KlientSymfonia>();
        private List<MrozniaZewnetrzna> mroznieZewnetrzne = new List<MrozniaZewnetrzna>();
        private ProduktMrozony? selectedProdukt = null;
        private Panel? selectedCard = null;
        private string connectionString;
        private string nazwaMrozniAktualnej;

        private readonly Color colorPrzyjecie = Color.FromArgb(40, 167, 69);
        private readonly Color colorWydanie = Color.FromArgb(220, 53, 69);
        private readonly Color colorKlient = Color.FromArgb(155, 89, 182);
        private readonly Color[] cardColors = new[] {
            Color.FromArgb(52, 152, 219), Color.FromArgb(155, 89, 182),
            Color.FromArgb(230, 126, 34), Color.FromArgb(26, 188, 156),
            Color.FromArgb(241, 196, 15), Color.FromArgb(231, 76, 60)
        };

        public WydanieZewnetrzneDialog(string nazwaMrozni, string connString)
        {
            this.connectionString = connString;
            this.nazwaMrozniAktualnej = nazwaMrozni;
            this.Text = $"📋 Nowy dokument - {nazwaMrozni}";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(240, 244, 248);
            this.Font = new Font("Segoe UI", 10F);

            LoadProdukty(connString);
            LoadKlienci(connString);
            LoadMroznieZewnetrzne();

            BuildUI();
        }

        private void BuildUI()
        {
            // === HEADER ===
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(44, 62, 80)
            };

            lblHeader = new Label
            {
                Text = "📋  NOWY DOKUMENT MAGAZYNOWY",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true
            };

            lblTrasa = new Label
            {
                Text = "Wybierz skąd → dokąd przenosisz towar",
                Font = new Font("Segoe UI", 10F, FontStyle.Italic),
                ForeColor = Color.FromArgb(189, 195, 199),
                Location = new Point(20, 48),
                AutoSize = true
            };

            // Data w headerze
            Label lblDataH = new Label { Text = "Data:", Location = new Point(1150, 25), AutoSize = true, ForeColor = Color.White };
            dtpData = new DateTimePicker
            {
                Location = new Point(1195, 22),
                Size = new Size(130, 28),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10F)
            };

            headerPanel.Controls.AddRange(new Control[] { lblHeader, lblTrasa, lblDataH, dtpData });
            this.Controls.Add(headerPanel);

            // === PANEL TRASY (OD -> DO) ===
            Panel trasaPanel = new Panel
            {
                Location = new Point(15, 95),
                Size = new Size(1355, 130),
                BackColor = Color.White
            };
            trasaPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(52, 152, 219), 2))
                    e.Graphics.DrawRectangle(pen, 0, 0, trasaPanel.Width - 1, trasaPanel.Height - 1);
            };
            this.Controls.Add(trasaPanel);

            // ŹRÓDŁO (OD)
            Label lblZrodlo = new Label
            {
                Text = "📤 SKĄD (źródło)",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60)
            };

            Label lblZrodloTypLabel = new Label { Text = "Typ:", Location = new Point(20, 50), AutoSize = true };
            cmbZrodloTyp = new ComboBox
            {
                Location = new Point(60, 47),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbZrodloTyp.Items.AddRange(new[] { "🏭 Ubojnia", "❄️ Mroźnia zewnętrzna", "👤 Klient" });
            cmbZrodloTyp.SelectedIndex = 0;
            cmbZrodloTyp.SelectedIndexChanged += CmbZrodloTyp_Changed;

            Label lblZrodloNazwaLabel = new Label { Text = "Nazwa:", Location = new Point(280, 50), AutoSize = true };
            cmbZrodloNazwa = new ComboBox
            {
                Location = new Point(340, 47),
                Size = new Size(300, 28),
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            lblZrodloInfo = new Label
            {
                Location = new Point(20, 85),
                Size = new Size(620, 30),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(127, 140, 141)
            };

            // STRZAŁKA
            Label lblArrow = new Label
            {
                Text = "➡️",
                Location = new Point(665, 50),
                AutoSize = true,
                Font = new Font("Segoe UI", 24F)
            };

            // CEL (DO)
            Label lblCel = new Label
            {
                Text = "📥 DOKĄD (cel)",
                Location = new Point(720, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(39, 174, 96)
            };

            Label lblCelTypLabel = new Label { Text = "Typ:", Location = new Point(720, 50), AutoSize = true };
            cmbCelTyp = new ComboBox
            {
                Location = new Point(760, 47),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbCelTyp.Items.AddRange(new[] { "🏭 Ubojnia", "❄️ Mroźnia zewnętrzna", "👤 Klient (przechowanie)" });
            cmbCelTyp.SelectedIndex = 1;
            cmbCelTyp.SelectedIndexChanged += CmbCelTyp_Changed;

            Label lblCelNazwaLabel = new Label { Text = "Nazwa:", Location = new Point(980, 50), AutoSize = true };
            cmbCelNazwa = new ComboBox
            {
                Location = new Point(1040, 47),
                Size = new Size(300, 28),
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            lblCelInfo = new Label
            {
                Location = new Point(720, 85),
                Size = new Size(620, 30),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(127, 140, 141)
            };

            trasaPanel.Controls.AddRange(new Control[] {
                lblZrodlo, lblZrodloTypLabel, cmbZrodloTyp, lblZrodloNazwaLabel, cmbZrodloNazwa, lblZrodloInfo,
                lblArrow,
                lblCel, lblCelTypLabel, cmbCelTyp, lblCelNazwaLabel, cmbCelNazwa, lblCelInfo
            });

            // Załaduj domyślne wartości
            RefreshZrodloNazwa();
            RefreshCelNazwa();

            // === PANEL LEWY - PRODUKTY ===
            Panel leftPanel = new Panel
            {
                Location = new Point(15, 240),
                Size = new Size(650, 550),
                BackColor = Color.White
            };
            leftPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, leftPanel.Width - 1, leftPanel.Height - 1);
            };
            this.Controls.Add(leftPanel);

            int y = 15;
            Label lblTytulProdukty = new Label
            {
                Text = "📦 WYBIERZ PRODUKTY",
                Location = new Point(15, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            leftPanel.Controls.Add(lblTytulProdukty);
            y += 35;

            // Wyszukiwanie
            txtSearch = new TextBox
            {
                Location = new Point(15, y),
                Size = new Size(620, 32),
                Font = new Font("Segoe UI", 11F)
            };
            txtSearch.TextChanged += (s, e) => RefreshProductCards();
            txtSearch.GotFocus += (s, e) => txtSearch.BackColor = Color.FromArgb(255, 255, 230);
            txtSearch.LostFocus += (s, e) => txtSearch.BackColor = Color.White;
            leftPanel.Controls.Add(txtSearch);
            y += 40;

            // Karty produktów
            flowProdukty = new FlowLayoutPanel
            {
                Location = new Point(15, y),
                Size = new Size(620, 280),
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };
            leftPanel.Controls.Add(flowProdukty);
            y += 285;

            lblProduktyInfo = new Label
            {
                Location = new Point(15, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.Gray
            };
            leftPanel.Controls.Add(lblProduktyInfo);
            y += 25;

            // Panel dodawania
            Panel addPanel = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(620, 120),
                BackColor = Color.FromArgb(232, 245, 233)
            };
            addPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(76, 175, 80), 2))
                    e.Graphics.DrawRectangle(pen, 0, 0, addPanel.Width - 1, addPanel.Height - 1);
            };

            Label lblWybrany = new Label
            {
                Text = "Kliknij na kartę produktu powyżej aby wybrać",
                Location = new Point(15, 12),
                Size = new Size(590, 22),
                Font = new Font("Segoe UI", 10F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Tag = "wybrany"
            };
            addPanel.Controls.Add(lblWybrany);

            Label lblIloscAdd = new Label { Text = "Ilość (kg):", Location = new Point(15, 50), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            nudIlosc = new NumericUpDown
            {
                Location = new Point(100, 46),
                Size = new Size(120, 32),
                Minimum = 1, Maximum = 999999, Value = 100,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };

            int qx = 235;
            foreach (var val in new[] { +50, +100, +250, +500, +1000 })
            {
                Button qbtn = new Button
                {
                    Text = $"+{val}", Location = new Point(qx, 46), Size = new Size(60, 32),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(200, 230, 200),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand
                };
                qbtn.FlatAppearance.BorderColor = Color.FromArgb(100, 180, 100);
                int addVal = val;
                qbtn.Click += (s, e) => { if (nudIlosc.Value + addVal <= nudIlosc.Maximum) nudIlosc.Value += addVal; };
                addPanel.Controls.Add(qbtn);
                qx += 65;
            }

            btnDodajDoKoszyka = new Button
            {
                Text = "➕ DODAJ DO LISTY",
                Location = new Point(15, 85),
                Size = new Size(590, 30),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnDodajDoKoszyka.FlatAppearance.BorderSize = 0;
            btnDodajDoKoszyka.Click += BtnDodajDoKoszyka_Click;

            addPanel.Controls.AddRange(new Control[] { lblIloscAdd, nudIlosc, btnDodajDoKoszyka });
            leftPanel.Controls.Add(addPanel);

            // === PANEL PRAWY - KOSZYK ===
            Panel rightPanel = new Panel
            {
                Location = new Point(680, 240),
                Size = new Size(690, 550),
                BackColor = Color.White
            };
            rightPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, rightPanel.Width - 1, rightPanel.Height - 1);
            };
            this.Controls.Add(rightPanel);

            Label lblTytulKoszyk = new Label
            {
                Text = "🛒 LISTA PRODUKTÓW W DOKUMENCIE",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            rightPanel.Controls.Add(lblTytulKoszyk);

            // Grid koszyka
            dgvKoszyk = new DataGridView
            {
                Location = new Point(15, 50),
                Size = new Size(660, 380),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EnableHeadersVisualStyles = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                Font = new Font("Segoe UI", 10F),
                RowTemplate = { Height = 35 }
            };
            dgvKoszyk.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgvKoszyk.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvKoszyk.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvKoszyk.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);

            dgvKoszyk.Columns.Add("Lp", "Lp");
            dgvKoszyk.Columns.Add("Kod", "Kod");
            dgvKoszyk.Columns.Add("Nazwa", "Nazwa produktu");
            dgvKoszyk.Columns.Add("Ilosc", "Ilość (kg)");
            dgvKoszyk.Columns["Lp"].Width = 45;
            dgvKoszyk.Columns["Kod"].Width = 110;
            dgvKoszyk.Columns["Nazwa"].Width = 380;
            dgvKoszyk.Columns["Ilosc"].Width = 100;

            DataGridViewButtonColumn btnCol = new DataGridViewButtonColumn
            {
                Name = "Usun", HeaderText = "", Text = "🗑", UseColumnTextForButtonValue = true, Width = 45
            };
            dgvKoszyk.Columns.Add(btnCol);
            dgvKoszyk.CellClick += DgvKoszyk_CellClick;
            rightPanel.Controls.Add(dgvKoszyk);

            // Podsumowanie
            Panel sumPanel = new Panel
            {
                Location = new Point(15, 440),
                Size = new Size(660, 95),
                BackColor = Color.FromArgb(236, 240, 241)
            };
            sumPanel.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(52, 73, 94), 2))
                    e.Graphics.DrawRectangle(pen, 0, 0, sumPanel.Width - 1, sumPanel.Height - 1);
            };

            lblKoszykPozycje = new Label
            {
                Text = "Pozycji: 0",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 12F)
            };

            lblKoszykSuma = new Label
            {
                Text = "RAZEM: 0 kg",
                Location = new Point(20, 50),
                AutoSize = true,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            // Uwagi
            Label lblUwagiLabel = new Label { Text = "Uwagi:", Location = new Point(350, 15), AutoSize = true };
            txtUwagi = new TextBox
            {
                Location = new Point(350, 40),
                Size = new Size(295, 45),
                Font = new Font("Segoe UI", 10F),
                Multiline = true
            };

            sumPanel.Controls.AddRange(new Control[] { lblKoszykPozycje, lblKoszykSuma, lblUwagiLabel, txtUwagi });
            rightPanel.Controls.Add(sumPanel);

            // === PRZYCISKI NA DOLE ===
            btnZapisz = new Button
            {
                Text = "✓ ZAPISZ DOKUMENT",
                Location = new Point(1100, 805),
                Size = new Size(180, 50),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                DialogResult = DialogResult.OK,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            Button btnAnuluj = new Button
            {
                Text = "Anuluj",
                Location = new Point(1290, 805),
                Size = new Size(90, 50),
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;

            Button btnWyczysc = new Button
            {
                Text = "🗑 Wyczyść listę",
                Location = new Point(680, 805),
                Size = new Size(140, 50),
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand
            };
            btnWyczysc.FlatAppearance.BorderSize = 0;
            btnWyczysc.Click += (s, e) => {
                if (Pozycje.Count > 0 && MessageBox.Show("Usunąć wszystkie pozycje?", "Potwierdź", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Pozycje.Clear();
                    RefreshKoszyk();
                }
            };

            this.Controls.AddRange(new Control[] { btnZapisz, btnAnuluj, btnWyczysc });
            this.AcceptButton = btnZapisz;
            this.CancelButton = btnAnuluj;

            RefreshProductCards();
        }

        private void CmbZrodloTyp_Changed(object sender, EventArgs e)
        {
            RefreshZrodloNazwa();
            UpdateTrasaInfo();
        }

        private void CmbCelTyp_Changed(object sender, EventArgs e)
        {
            RefreshCelNazwa();
            UpdateTrasaInfo();
        }

        private void RefreshZrodloNazwa()
        {
            cmbZrodloNazwa.Items.Clear();
            int idx = cmbZrodloTyp.SelectedIndex;

            if (idx == 0) // Ubojnia
            {
                cmbZrodloNazwa.Items.Add("Ubojnia - magazyn główny");
                cmbZrodloNazwa.Items.Add("Ubojnia - hala produkcyjna");
                cmbZrodloNazwa.Items.Add("Ubojnia - chłodnia");
                cmbZrodloNazwa.DropDownStyle = ComboBoxStyle.DropDownList;
                lblZrodloInfo.Text = "Towar wychodzi z ubojni";
            }
            else if (idx == 1) // Mroźnia zewnętrzna
            {
                foreach (var m in mroznieZewnetrzne)
                    cmbZrodloNazwa.Items.Add($"❄️ {m.Nazwa}");
                if (cmbZrodloNazwa.Items.Count == 0)
                    cmbZrodloNazwa.Items.Add("(brak mroźni zewnętrznych)");
                cmbZrodloNazwa.DropDownStyle = ComboBoxStyle.DropDownList;
                lblZrodloInfo.Text = "Towar wychodzi z mroźni zewnętrznej";
            }
            else // Klient
            {
                foreach (var k in klienci.Take(100))
                    cmbZrodloNazwa.Items.Add($"👤 {k.Nazwa}");
                cmbZrodloNazwa.DropDownStyle = ComboBoxStyle.DropDown;
                cmbZrodloNazwa.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                cmbZrodloNazwa.AutoCompleteSource = AutoCompleteSource.ListItems;
                lblZrodloInfo.Text = "Towar zwracany przez klienta";
            }

            if (cmbZrodloNazwa.Items.Count > 0)
                cmbZrodloNazwa.SelectedIndex = 0;
        }

        private void RefreshCelNazwa()
        {
            cmbCelNazwa.Items.Clear();
            int idx = cmbCelTyp.SelectedIndex;

            if (idx == 0) // Ubojnia
            {
                cmbCelNazwa.Items.Add("Ubojnia - magazyn główny");
                cmbCelNazwa.Items.Add("Ubojnia - hala produkcyjna");
                cmbCelNazwa.Items.Add("Ubojnia - chłodnia");
                cmbCelNazwa.DropDownStyle = ComboBoxStyle.DropDownList;
                lblCelInfo.Text = "Towar wraca do ubojni";
            }
            else if (idx == 1) // Mroźnia zewnętrzna
            {
                foreach (var m in mroznieZewnetrzne)
                    cmbCelNazwa.Items.Add($"❄️ {m.Nazwa}");
                if (cmbCelNazwa.Items.Count == 0)
                    cmbCelNazwa.Items.Add($"❄️ {nazwaMrozniAktualnej}");
                cmbCelNazwa.DropDownStyle = ComboBoxStyle.DropDownList;
                lblCelInfo.Text = "Towar trafia do mroźni zewnętrznej";
            }
            else // Klient (przechowanie)
            {
                foreach (var k in klienci.Take(100))
                    cmbCelNazwa.Items.Add($"👤 {k.Nazwa}");
                cmbCelNazwa.DropDownStyle = ComboBoxStyle.DropDown;
                cmbCelNazwa.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                cmbCelNazwa.AutoCompleteSource = AutoCompleteSource.ListItems;
                lblCelInfo.Text = "Towar dla klienta, przechowywany w mroźni";
            }

            if (cmbCelNazwa.Items.Count > 0)
                cmbCelNazwa.SelectedIndex = 0;
        }

        private void UpdateTrasaInfo()
        {
            string zrodlo = cmbZrodloTyp.SelectedItem?.ToString()?.Replace("🏭 ", "").Replace("❄️ ", "").Replace("👤 ", "") ?? "";
            string cel = cmbCelTyp.SelectedItem?.ToString()?.Replace("🏭 ", "").Replace("❄️ ", "").Replace("👤 ", "") ?? "";
            lblTrasa.Text = $"{zrodlo} ➡️ {cel}";
        }

        private void RefreshProductCards()
        {
            flowProdukty.SuspendLayout();
            flowProdukty.Controls.Clear();
            string filter = txtSearch.Text.Trim().ToLower();
            int count = 0, colorIndex = 0;

            foreach (var p in produkty)
            {
                if (!string.IsNullOrEmpty(filter) && !p.Nazwa.ToLower().Contains(filter) && !p.Kod.ToLower().Contains(filter))
                    continue;
                count++;
                flowProdukty.Controls.Add(CreateProductCard(p, cardColors[colorIndex++ % cardColors.Length]));
                if (count >= 80) break;
            }
            flowProdukty.ResumeLayout();
            lblProduktyInfo.Text = $"Wyświetlono: {count} z {produkty.Count} produktów";
        }

        private Panel CreateProductCard(ProduktMrozony produkt, Color tagColor)
        {
            Panel card = new Panel { Size = new Size(145, 55), BackColor = Color.White, Margin = new Padding(3), Cursor = Cursors.Hand, Tag = produkt };
            Panel tag = new Panel { Location = new Point(0, 0), Size = new Size(4, 55), BackColor = tagColor };
            Label lblKod = new Label { Text = produkt.Kod, Location = new Point(8, 4), Size = new Size(130, 16), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = tagColor };
            Label lblNazwa = new Label { Text = produkt.Nazwa.Length > 25 ? produkt.Nazwa.Substring(0, 22) + "..." : produkt.Nazwa, Location = new Point(8, 22), Size = new Size(130, 30), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(60, 60, 60) };
            card.Controls.AddRange(new Control[] { tag, lblKod, lblNazwa });

            card.Paint += (s, e) => {
                using (var pen = new Pen(card == selectedCard ? Color.FromArgb(39, 174, 96) : Color.FromArgb(220, 220, 220), card == selectedCard ? 2 : 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            EventHandler click = (s, e) => SelectCard(card, produkt);
            card.Click += click;
            foreach (Control c in card.Controls) c.Click += click;
            card.MouseEnter += (s, e) => { if (card != selectedCard) card.BackColor = Color.FromArgb(245, 250, 255); };
            card.MouseLeave += (s, e) => { if (card != selectedCard) card.BackColor = Color.White; };
            return card;
        }

        private void SelectCard(Panel card, ProduktMrozony produkt)
        {
            if (selectedCard != null) { selectedCard.BackColor = Color.White; selectedCard.Invalidate(); }
            selectedCard = card;
            selectedProdukt = produkt;
            card.BackColor = Color.FromArgb(232, 245, 233);
            card.Invalidate();

            var lbl = this.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Panel>()).SelectMany(p => p.Controls.OfType<Label>()).FirstOrDefault(l => l.Tag?.ToString() == "wybrany");
            if (lbl != null)
            {
                lbl.Text = $"✓ {produkt.Kod} - {produkt.Nazwa}";
                lbl.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                lbl.ForeColor = Color.FromArgb(39, 174, 96);
            }
            btnDodajDoKoszyka.Enabled = true;
        }

        private void BtnDodajDoKoszyka_Click(object sender, EventArgs e)
        {
            if (selectedProdukt == null) return;

            var existing = Pozycje.FirstOrDefault(p => p.KodProduktu == selectedProdukt.Kod);
            if (existing != null)
                existing.Ilosc += nudIlosc.Value;
            else
                Pozycje.Add(new PozycjaDokumentu { KodProduktu = selectedProdukt.Kod, Nazwa = selectedProdukt.Nazwa, Ilosc = nudIlosc.Value });

            RefreshKoszyk();

            if (selectedCard != null) { selectedCard.BackColor = Color.White; selectedCard.Invalidate(); }
            selectedCard = null;
            selectedProdukt = null;
            btnDodajDoKoszyka.Enabled = false;

            var lbl = this.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Panel>()).SelectMany(p => p.Controls.OfType<Label>()).FirstOrDefault(l => l.Tag?.ToString() == "wybrany");
            if (lbl != null)
            {
                lbl.Text = "Kliknij na kartę produktu powyżej aby wybrać";
                lbl.Font = new Font("Segoe UI", 10F, FontStyle.Italic);
                lbl.ForeColor = Color.Gray;
            }
        }

        private void RefreshKoszyk()
        {
            dgvKoszyk.Rows.Clear();
            int lp = 1;
            decimal suma = 0;
            foreach (var poz in Pozycje)
            {
                dgvKoszyk.Rows.Add(lp++, poz.KodProduktu, poz.Nazwa, poz.Ilosc.ToString("N0"));
                suma += poz.Ilosc;
            }
            lblKoszykPozycje.Text = $"Pozycji: {Pozycje.Count}";
            lblKoszykSuma.Text = $"RAZEM: {suma:N0} kg";
            btnZapisz.Enabled = Pozycje.Count > 0;
        }

        private void DgvKoszyk_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgvKoszyk.Columns["Usun"].Index)
            {
                string kod = dgvKoszyk.Rows[e.RowIndex].Cells["Kod"].Value?.ToString() ?? "";
                Pozycje.RemoveAll(p => p.KodProduktu == kod);
                RefreshKoszyk();
            }
        }

        private void LoadProdukty(string connStr)
        {
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand($"SELECT Id, Kod, Nazwa FROM [HANDEL].[HM].[TW] WHERE katalog = '{Mroznia.KatalogMrozony}' ORDER BY Nazwa ASC", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            produkty.Add(new ProduktMrozony { Id = rd.GetInt32(0), Kod = rd["Kod"]?.ToString() ?? "", Nazwa = rd["Nazwa"]?.ToString() ?? "" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania produktów: {ex.Message}");
            }
        }

        private void LoadKlienci(string connStr)
        {
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP 500 Id, Kod, Nazwa FROM [HANDEL].[HM].[KONTRAH] WHERE Typ = 1 ORDER BY Nazwa ASC", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            klienci.Add(new KlientSymfonia { Id = rd.GetInt32(0), Kod = rd["Kod"]?.ToString() ?? "", Nazwa = rd["Nazwa"]?.ToString() ?? "" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania klientów: {ex.Message}");
            }
        }

        private void LoadMroznieZewnetrzne()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string path = Path.Combine(appData, "OfertaHandlowa", "mroznie_zewnetrzne.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    mroznieZewnetrzne = System.Text.Json.JsonSerializer.Deserialize<List<MrozniaZewnetrzna>>(json) ?? new List<MrozniaZewnetrzna>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania mroźni zewnętrznych: {ex.Message}");
            }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            if (Pozycje.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt.", "Brak pozycji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            Data = dtpData.Value.Date;

            // Określ typ na podstawie kierunku
            int zrodloIdx = cmbZrodloTyp.SelectedIndex;
            int celIdx = cmbCelTyp.SelectedIndex;

            if (zrodloIdx == 0) // Z ubojni
                Typ = "Przyjęcie";
            else
                Typ = "Wydanie";

            ZrodloTyp = cmbZrodloTyp.SelectedItem?.ToString()?.Replace("🏭 ", "").Replace("❄️ ", "").Replace("👤 ", "") ?? "";
            ZrodloNazwa = cmbZrodloNazwa.Text.Replace("❄️ ", "").Replace("👤 ", "");
            CelTyp = cmbCelTyp.SelectedItem?.ToString()?.Replace("🏭 ", "").Replace("❄️ ", "").Replace("👤 ", "").Replace(" (przechowanie)", "") ?? "";
            CelNazwa = cmbCelNazwa.Text.Replace("❄️ ", "").Replace("👤 ", "");

            // Jeśli cel to klient, zapisz ID
            if (celIdx == 2)
            {
                var klient = klienci.FirstOrDefault(k => cmbCelNazwa.Text.Contains(k.Nazwa));
                KlientId = klient?.Id.ToString() ?? "";
            }

            Uwagi = txtUwagi.Text.Trim();
        }
    }

    /// <summary>
    /// Klient z Symfonii
    /// </summary>
    public class KlientSymfonia
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
    }

    /// <summary>
    /// Pozycja dokumentu wydania/przyjęcia
    /// </summary>
    public class PozycjaDokumentu
    {
        public string KodProduktu { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal Ilosc { get; set; }
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
