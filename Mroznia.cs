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
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly Dictionary<string, System.Drawing.Image> _produktImagesWF = new(StringComparer.OrdinalIgnoreCase);

        // Dane pozycji dziennych do CellPainting (index wiersza → lista pozycji)
        private List<List<(string KodOriginal, string DisplayName, decimal Qty, bool IsWydanie)>> _dziennePozycjeData;

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
        private ElementHost stanChartHost;
        private CartesianChart stanChart;
        private Label lblStanChartTitle;
        private string _stanChartFilteredProduct;
        private ToolTip toolTip;
        private CheckBox chkGrupowanie;
        private Timer autoLoadTimer;

        // === UX: Toast + kolorowy pasek zakładek ===
        private Panel toastPanel;
        private Label toastLabel;
        private Timer toastTimer;
        private Panel tabIndicatorPanel;
        private FlowLayoutPanel mroznieCardsPanel;
        private Label lblDzienneSumaWydano, lblDzienneSumaPrzyjeto, lblDzienneBilans;
        private CheckBox chkUkryjZewnetrzne;

        // === ZAKŁADKA ZAMÓWIENIA MROŻONKI ===
        private DateTimePicker dtpZamMrozOd;
        private Button btnZamMrozRefresh;
        private DataGridView dgvZamMrozOrders, dgvZamMrozPozycje;
        private Label lblZamMrozKlient, lblZamMrozHandlowiec, lblZamMrozWyjazd;
        private Label lblZamMrozPojazd, lblZamMrozKierowca, lblZamMrozStatus, lblZamMrozUwagi;
        private Label lblZamMrozSumaKg, lblZamMrozLiczbaZam, lblZamMrozDni;
        private Label lblZamMrozTitle;
        private Panel _zamMrozHandlowiecAvatar;
        private List<ZamowienieMrozoneInfo> _zamowieniaMrozone = new();
        private HashSet<int> _frozenProductIds = new();
        private Dictionary<int, string> _frozenProductNames = new();
        private bool _zamMrozLoaded = false;
        private Timer _zamMrozAutoRefreshTimer;
        private readonly Dictionary<string, Color> _handlowiecColors = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _handlowiecMapowanie; // HandlowiecName → UserID
        private readonly Dictionary<string, Image> _handlowiecAvatarCache = new(StringComparer.OrdinalIgnoreCase);

        // === ZAKŁADKA PRZESUNIĘCIA ZEW. ===
        private DataGridView dgvPrzesunięcia;
        private Panel panelSzczegolyPrzesun;
        private Label lblPrzesunZrodlo, lblPrzesunCel, lblPrzesunPozycje, lblPrzesunUwagi, lblPrzesunStatus;
        private Button btnRealizujPrzesun, btnAnulujPrzesun;
        private List<PrzesuniecieMrozni> _listaPrzesuniec = new();

        // === CACHE STATYSTYK (zastępują usunięte karty) ===
        private decimal lastWydano, lastPrzyjeto;
        private int lastDni;

        // === LOADING OVERLAY ===
        private Panel loadingOverlayPanel;
        private Label loadingLabel;
        private System.Windows.Forms.Timer spinnerTimer;
        private int spinnerAngle;
        private Panel spinnerPanel;

        // Kolory zakładek (1a - kolorowy wskaźnik)
        private readonly Color[] TabColors = new Color[]
        {
            Color.FromArgb(0, 150, 136),   // Zamówienia mrożonki - teal (PIERWSZA)
            Color.FromArgb(0, 120, 212),   // Stan mroźni - niebieski
            Color.FromArgb(220, 53, 69),   // Rezerwacje - czerwony
            Color.FromArgb(16, 124, 16),   // Mroźnie zewnętrzne - zielony
            Color.FromArgb(128, 90, 213),  // Przesunięcia zew. - fioletowy
            Color.FromArgb(255, 140, 0)    // Przegląd dzienny - pomarańczowy
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

            // === LOADING OVERLAY ===
            InitializeLoadingOverlay();

            // === 7bb: TOAST NOTIFICATION SYSTEM ===
            InitializeToastSystem();
        }

        private void InitializeLoadingOverlay()
        {
            loadingOverlayPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(160, 20, 20, 25),
                Visible = false
            };

            // Centered card panel
            spinnerPanel = new Panel
            {
                Size = new Size(300, 160),
                BackColor = Color.Transparent
            };
            spinnerPanel.Paint += SpinnerPanel_Paint;

            loadingLabel = new Label
            {
                Text = "Ładowanie...",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular),
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(300, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 120)
            };

            spinnerPanel.Controls.Add(loadingLabel);
            loadingOverlayPanel.Controls.Add(spinnerPanel);
            this.Controls.Add(loadingOverlayPanel);
            loadingOverlayPanel.BringToFront();

            // Spinner animation timer
            spinnerTimer = new System.Windows.Forms.Timer { Interval = 30 };
            spinnerTimer.Tick += (s, e) =>
            {
                spinnerAngle = (spinnerAngle + 4) % 360;
                spinnerPanel.Invalidate(new Rectangle(
                    spinnerPanel.Width / 2 - 40, 10, 80, 80));
            };
        }

        private void SpinnerPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var panel = (Panel)sender;
            int w = panel.Width, h = panel.Height;

            // Background rounded rect
            using (var path = CreateRoundedRect(0, 0, w, h, 12))
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(37, 42, 49)))
                    g.FillPath(bgBrush, path);
                using (var borderPen = new Pen(Color.FromArgb(48, 54, 61), 1))
                    g.DrawPath(borderPen, path);
            }

            // Rotating snowflake ❄ in center
            int cx = w / 2, cy = 50;
            string snowflake = "\u2744";
            using (var snowFont = new Font("Segoe UI", 36F, FontStyle.Regular))
            using (var snowBrush = new SolidBrush(Color.FromArgb(180, 210, 255)))
            {
                var state = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(spinnerAngle);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(snowflake, snowFont, snowBrush, 0, 0, sf);
                g.Restore(state);
            }
        }

        private static GraphicsPath CreateRoundedRect(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2 - 1, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2 - 1, y + h - r * 2 - 1, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2 - 1, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ShowLoading(string message = "Ładowanie danych...")
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ShowLoading(message)));
                return;
            }
            loadingLabel.Text = message;
            // Center spinner panel
            spinnerPanel.Location = new Point(
                (loadingOverlayPanel.Width - spinnerPanel.Width) / 2,
                (loadingOverlayPanel.Height - spinnerPanel.Height) / 2);
            loadingOverlayPanel.Visible = true;
            loadingOverlayPanel.BringToFront();
            spinnerTimer.Start();
            Application.DoEvents();
        }

        private void HideLoading()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => HideLoading()));
                return;
            }
            spinnerTimer.Stop();
            loadingOverlayPanel.Visible = false;
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

            // Lazy load zamówienia mrożonki przy pierwszym wybraniu
            if (idx == 0 && !_zamMrozLoaded)
            {
                _zamMrozLoaded = true;
                _ = LoadZamowieniaMrozonkiAsync();
            }

            // Zapewnij mapowanie handlowców dla zakładki rezerwacji
            if (idx == 2)
                _ = EnsureHandlowiecMappingLoadedAsync();

            // Auto-refresh timer: aktywny tylko gdy zakładka zamówień mrożonek jest widoczna
            if (_zamMrozAutoRefreshTimer != null)
                _zamMrozAutoRefreshTimer.Enabled = (idx == 0);

            // 1d: Aktualizuj badge'e na zakładkach
            UpdateTabBadges();
        }

        // ============================================
        // 1d: BADGE'E NA ZAKŁADKACH
        // ============================================

        private void UpdateTabBadges()
        {
            // Statyczne nazwy zakładek bez numerów
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
                bool isSelected = (card == selectedMrozniaCard);

                // Tło z zaokrąglonymi rogami
                using (var path = GetRoundedRectangle(card.ClientRectangle, 10))
                {
                    Color bgTop = isSelected ? Color.FromArgb(220, 235, 255) : Color.White;
                    Color bgBottom = isSelected ? Color.FromArgb(235, 245, 255) : Color.FromArgb(250, 252, 255);
                    using (var brush = new LinearGradientBrush(
                        card.ClientRectangle, bgTop, bgBottom,
                        LinearGradientMode.Vertical))
                        g.FillPath(brush, path);

                    // Border - grubszy i niebieski gdy zaznaczony
                    Color borderColor = isSelected ? PrimaryColor : Color.FromArgb(225, 228, 232);
                    float borderWidth = isSelected ? 2.5f : 1f;
                    using (var pen = new Pen(borderColor, borderWidth))
                        g.DrawPath(pen, path);
                }

                // Lewa krawędź kolorowa
                Color leftEdge = isSelected ? PrimaryColor : accentColor;
                using (var brush = new SolidBrush(leftEdge))
                    g.FillRectangle(brush, 0, 12, isSelected ? 5 : 4, card.Height - 24);
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

            // Hover effect - repaint only, respects selection
            foreach (Control c in card.Controls)
            {
                c.MouseEnter += (s, e) => { card.Cursor = Cursors.Hand; card.Invalidate(); };
                c.MouseLeave += (s, e) => card.Invalidate();
                c.Click += (s, e) => SelectMrozniaCard(card);
            }
            card.MouseEnter += (s, e) => card.Invalidate();
            card.MouseLeave += (s, e) => card.Invalidate();
            card.Click += (s, e) => SelectMrozniaCard(card);

            return card;
        }

        private Panel selectedMrozniaCard;
        private Label lblWydaniaHeaderNazwa;

        private void SelectMrozniaCard(Panel card)
        {
            // Odznacz poprzednio wybraną - wymuś odświeżenie
            Panel prevCard = selectedMrozniaCard;
            selectedMrozniaCard = card;

            if (prevCard != null && prevCard != card)
                prevCard.Invalidate();
            card.Invalidate();

            // Znajdź odpowiadający wiersz w dgvMroznieZewnetrzne i zaznacz go
            string id = card.Tag?.ToString();
            if (string.IsNullOrEmpty(id) || dgvMroznieZewnetrzne?.DataSource == null) return;

            // Aktualizuj nagłówek "Stan i wydania" z nazwą mroźni
            string nazwa = null;
            foreach (DataGridViewRow row in dgvMroznieZewnetrzne.Rows)
            {
                if (row.Cells["ID"].Value?.ToString() == id)
                {
                    nazwa = row.Cells["Nazwa"].Value?.ToString();
                    dgvMroznieZewnetrzne.ClearSelection();
                    row.Selected = true;
                    break;
                }
            }

            if (lblWydaniaHeaderNazwa != null)
            {
                lblWydaniaHeaderNazwa.Text = string.IsNullOrEmpty(nazwa)
                    ? "STAN I WYDANIA MROŹNI"
                    : $"STAN I WYDANIA MROŹNI: {nazwa.ToUpper()}";
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

            // Nagłówek zakładki
            Panel dziennyHeader = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = WarningColor };
            Label lblDziennyTitle = new Label
            {
                Text = "📋 PRZEGLĄD DZIENNY MROŹNI",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            dziennyHeader.Controls.Add(lblDziennyTitle);

            // Pasek statystyk
            Panel dziennyStatsBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(250, 251, 253) };
            dziennyStatsBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                    e.Graphics.DrawLine(pen, 0, dziennyStatsBar.Height - 1, dziennyStatsBar.Width, dziennyStatsBar.Height - 1);
            };

            lblDzienneSumaWydano = new Label
            {
                Text = "0 kg",
                Location = new Point(110, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = DangerColor
            };
            Label lblDzienneWydanoLabel = new Label
            {
                Text = "WYDANO ŁĄCZNIE",
                Location = new Point(110, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            Panel cardDzWydano = new Panel { Location = new Point(10, 4), Size = new Size(220, 44), BackColor = Color.White };
            cardDzWydano.Paint += (s, e) =>
            {
                using (var pen = new Pen(DangerColor, 2)) e.Graphics.DrawLine(pen, 0, 0, 0, cardDzWydano.Height);
                using (var pen = new Pen(Color.FromArgb(230, 230, 230))) e.Graphics.DrawRectangle(pen, 0, 0, cardDzWydano.Width - 1, cardDzWydano.Height - 1);
            };
            lblDzienneSumaWydano.Location = new Point(10, 19);
            lblDzienneWydanoLabel.Location = new Point(10, 3);
            cardDzWydano.Controls.AddRange(new Control[] { lblDzienneWydanoLabel, lblDzienneSumaWydano });

            lblDzienneSumaPrzyjeto = new Label
            {
                Text = "0 kg",
                Location = new Point(10, 19),
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = SuccessColor
            };
            Label lblDziennePrzyjetoLabel = new Label
            {
                Text = "PRZYJĘTO ŁĄCZNIE",
                Location = new Point(10, 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            Panel cardDzPrzyjeto = new Panel { Location = new Point(245, 4), Size = new Size(220, 44), BackColor = Color.White };
            cardDzPrzyjeto.Paint += (s, e) =>
            {
                using (var pen = new Pen(SuccessColor, 2)) e.Graphics.DrawLine(pen, 0, 0, 0, cardDzPrzyjeto.Height);
                using (var pen = new Pen(Color.FromArgb(230, 230, 230))) e.Graphics.DrawRectangle(pen, 0, 0, cardDzPrzyjeto.Width - 1, cardDzPrzyjeto.Height - 1);
            };
            cardDzPrzyjeto.Controls.AddRange(new Control[] { lblDziennePrzyjetoLabel, lblDzienneSumaPrzyjeto });

            lblDzienneBilans = new Label
            {
                Text = "0 kg",
                Location = new Point(10, 19),
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = PrimaryColor
            };
            Label lblDzienneBilansLabel = new Label
            {
                Text = "BILANS",
                Location = new Point(10, 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            Panel cardDzBilans = new Panel { Location = new Point(480, 4), Size = new Size(220, 44), BackColor = Color.White };
            cardDzBilans.Paint += (s, e) =>
            {
                using (var pen = new Pen(PrimaryColor, 2)) e.Graphics.DrawLine(pen, 0, 0, 0, cardDzBilans.Height);
                using (var pen = new Pen(Color.FromArgb(230, 230, 230))) e.Graphics.DrawRectangle(pen, 0, 0, cardDzBilans.Width - 1, cardDzBilans.Height - 1);
            };
            cardDzBilans.Controls.AddRange(new Control[] { lblDzienneBilansLabel, lblDzienneBilans });

            dziennyStatsBar.Controls.AddRange(new Control[] { cardDzWydano, cardDzPrzyjeto, cardDzBilans });

            // Pasek narzędzi analizy (daty, przyciski)
            Panel analysisToolbar = CreateAnalysisToolbar();

            dgvDzienne = CreateStyledDataGridView();
            dgvDzienne.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvDzienne.RowTemplate.Height = 26;
            dgvDzienne.RowTemplate.MinimumHeight = 22;
            dgvDzienne.ColumnHeadersHeight = 30;
            dgvDzienne.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dgvDzienne.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            dgvDzienne.CellPainting += DgvDzienne_CellPainting;
            dgvDzienne.DoubleClick += DgvDzienne_DoubleClick;

            // SplitContainer: tabela u góry, wykres na dole
            SplitContainer splitDzienny = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                BackColor = BackgroundColor
            };
            splitDzienny.SizeChanged += (s, e) => {
                if (splitDzienny.Height > 0)
                    splitDzienny.SplitterDistance = (int)(splitDzienny.Height * 0.50);
            };

            // Górna część: tabela
            splitDzienny.Panel1.Controls.Add(dgvDzienne);

            // Dolna część: wykres trendu
            Panel chartPanel1 = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            // Nagłówek wykresu
            Panel chartHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.FromArgb(140, 20, 252) };
            Label lblChartTitle = new Label
            {
                Text = "📊 TREND WYDAŃ I PRZYJĘĆ",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            chartHeader.Controls.Add(lblChartTitle);

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

            Panel chartContentPanel = new Panel { Dock = DockStyle.Fill };
            chartContentPanel.Controls.Add(chartHost);

            splitDzienny.Panel2.Controls.Add(chartContentPanel);
            splitDzienny.Panel2.Controls.Add(chartHeader);

            dziennyPanel.Controls.Add(splitDzienny);
            dziennyPanel.Controls.Add(analysisToolbar);
            dziennyPanel.Controls.Add(dziennyStatsBar);
            dziennyPanel.Controls.Add(dziennyHeader);
            tab1.Controls.Add(dziennyPanel);

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
                Size = new Size(220, 44),
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
                Location = new Point(490, 6),
                Size = new Size(220, 44),
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

            chkUkryjZewnetrzne = new CheckBox
            {
                Text = "Ukryj mroźnie zewnętrzne",
                Location = new Point(730, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(80, 80, 80),
                Checked = false
            };
            chkUkryjZewnetrzne.CheckedChanged += ChkUkryjZewnetrzne_CheckedChanged;

            stanToolbar.Controls.AddRange(new Control[] {
                dtpStanMagazynu, btnMapowanie,
                cardStan, cardRez, chkUkryjZewnetrzne, lblStanWartosc, lblStanProdukty, chkGrupowanie
            });

            // === STAN MAGAZYNU - tabela + wykres ===
            dgvStanMagazynu = CreateStyledDataGridView();
            dgvStanMagazynu.DoubleClick += DgvStanMagazynu_DoubleClick;
            dgvStanMagazynu.MouseClick += DgvStanMagazynu_MouseClick;
            dgvStanMagazynu.CellClick += DgvStanMagazynu_CellClick;
            dgvStanMagazynu.CellPainting += DgvStanMagazynu_CellPainting;
            dgvStanMagazynu.RowTemplate.Height = 40;

            // Menu kontekstowe
            ContextMenuStrip ctxMenuStan = new ContextMenuStrip();
            ctxMenuStan.Items.Add("Rezerwuj", null, (s, e) => RezerwujWybranyProdukt());
            ctxMenuStan.Items.Add("Usuń rezerwację", null, (s, e) => UsunRezerwacjeProduktu());
            ctxMenuStan.Items.Add(new ToolStripSeparator());
            ctxMenuStan.Items.Add("Historia", null, (s, e) => PokazHistorieWybranegoProduktu());
            dgvStanMagazynu.ContextMenuStrip = ctxMenuStan;

            // SplitContainer: tabela po lewej, wykres per produkt po prawej
            SplitContainer splitStan = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 4,
                BackColor = BackgroundColor
            };
            splitStan.SizeChanged += (s, e) => {
                if (splitStan.Width > 0)
                    splitStan.SplitterDistance = (int)(splitStan.Width * 0.50);
            };

            splitStan.Panel1.Controls.Add(dgvStanMagazynu);

            // Wykres stanu magazynu - słupkowy
            Panel stanChartPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Panel stanChartHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = PrimaryColor };
            lblStanChartTitle = new Label
            {
                Text = "📊 STAN MROŹNI - OSTATNIE 30 DNI",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            stanChartHeader.Controls.Add(lblStanChartTitle);

            stanChart = new CartesianChart
            {
                DisableAnimations = false,
                Hoverable = true,
                DataTooltip = new DefaultTooltip
                {
                    SelectionMode = TooltipSelectionMode.OnlySender
                },
                LegendLocation = LegendLocation.None,
                Background = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };
            stanChartHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = stanChart
            };

            Panel stanChartContent = new Panel { Dock = DockStyle.Fill };
            stanChartContent.Controls.Add(stanChartHost);

            stanChartPanel.Controls.Add(stanChartContent);
            stanChartPanel.Controls.Add(stanChartHeader);
            splitStan.Panel2.Controls.Add(stanChartPanel);

            stanMainPanel.Controls.Add(splitStan);
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
            dgvZamowienia.RowTemplate.Height = 44;
            dgvZamowienia.CellPainting += DgvRezerwacje_CellPainting;
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

            Button btnInwentaryzacja = CreateModernButton("\U0001f4cb Inwentaryzacja", 470, 10, 140, WarningColor);
            btnInwentaryzacja.Click += BtnInwentaryzacja_Click;

            zewnToolbar.Controls.AddRange(new Control[] { btnDodajMroznieZewn, btnEdytujMroznieZewn, btnUsunMroznieZewn, btnDodajWydanieZewn, btnInwentaryzacja });

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
            lblWydaniaHeaderNazwa = new Label
            {
                Text = "STAN I WYDANIA MROŹNI",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            zewnRightHeader.Controls.Add(lblWydaniaHeaderNazwa);

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

            // === ZAKŁADKA: PRZESUNIĘCIA ZEW. ===
            var PurpleColor = Color.FromArgb(128, 90, 213);
            TabPage tabPrzesun = new TabPage("  Przesunięcia zew.  ");
            tabPrzesun.BackColor = BackgroundColor;
            tabPrzesun.Padding = new Padding(0);

            Panel przesunMainPanel = new Panel { Dock = DockStyle.Fill };

            // Nagłówek
            Panel przesunHeader = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = PurpleColor };
            Label lblPrzesunTitle = new Label
            {
                Text = "↔ PRZESUNIĘCIA MIĘDZY MROŹNIAMI",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0)
            };
            Button btnDodajPrzesun = new Button
            {
                Text = "+ Nowe zlecenie",
                Size = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = PurpleColor,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right,
                Location = new Point(przesunHeader.Width - 160, 7)
            };
            btnDodajPrzesun.FlatAppearance.BorderSize = 0;
            btnDodajPrzesun.Click += BtnDodajPrzesuniecieClick;
            przesunHeader.Resize += (s, ev) => { btnDodajPrzesun.Location = new Point(przesunHeader.Width - 155, 7); };
            przesunHeader.Controls.Add(btnDodajPrzesun);
            przesunHeader.Controls.Add(lblPrzesunTitle);

            // Toolbar filtra
            Panel przesunToolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(250, 251, 253) };
            przesunToolbar.Paint += (s, ev) =>
            {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    ev.Graphics.DrawLine(pen, 0, przesunToolbar.Height - 1, przesunToolbar.Width, przesunToolbar.Height - 1);
            };

            Label lblFiltrPrzesun = new Label
            {
                Text = "Status:",
                Location = new Point(15, 13),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextColor
            };

            var cmbFiltrPrzesun = new ComboBox
            {
                Location = new Point(70, 9),
                Size = new Size(180, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F)
            };
            cmbFiltrPrzesun.Items.AddRange(new object[] { "Wszystkie", "Nowe", "Zrealizowane", "Anulowane" });
            cmbFiltrPrzesun.SelectedIndex = 0;
            cmbFiltrPrzesun.SelectedIndexChanged += (s, ev) =>
            {
                string filtr = cmbFiltrPrzesun.SelectedItem?.ToString() ?? "Wszystkie";
                if (dgvPrzesunięcia.DataSource is DataTable dtSrc)
                {
                    if (filtr == "Wszystkie")
                        dtSrc.DefaultView.RowFilter = "";
                    else
                        dtSrc.DefaultView.RowFilter = $"Status = '{filtr}'";
                }
            };

            przesunToolbar.Controls.AddRange(new Control[] { lblFiltrPrzesun, cmbFiltrPrzesun });

            // Split: Grid górny | Szczegóły dolny
            SplitContainer splitPrzesun = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                BackColor = BackgroundColor
            };
            splitPrzesun.SizeChanged += (s, ev) =>
            {
                try
                {
                    if (splitPrzesun.Height > 100)
                        splitPrzesun.SplitterDistance = (int)(splitPrzesun.Height * 0.60);
                }
                catch { }
            };

            // Grid przesunięć
            dgvPrzesunięcia = CreateStyledDataGridView();
            dgvPrzesunięcia.ColumnHeadersDefaultCellStyle.BackColor = PurpleColor;
            dgvPrzesunięcia.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvPrzesunięcia.EnableHeadersVisualStyles = false;
            dgvPrzesunięcia.SelectionChanged += DgvPrzesunieciaSelectionChanged;
            dgvPrzesunięcia.CellPainting += DgvPrzesunięcia_CellPainting;

            splitPrzesun.Panel1.Controls.Add(dgvPrzesunięcia);

            // Panel szczegółów
            panelSzczegolyPrzesun = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15), Visible = false };
            panelSzczegolyPrzesun.Paint += (s, ev) =>
            {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    ev.Graphics.DrawRectangle(pen, 0, 0, panelSzczegolyPrzesun.Width - 1, panelSzczegolyPrzesun.Height - 1);
            };

            // Nagłówek szczegółów
            Panel przesunDetailHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(245, 243, 252) };
            Label lblPrzesunDetailTitle = new Label
            {
                Text = "SZCZEGÓŁY PRZESUNIĘCIA",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = PurpleColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            przesunDetailHeader.Controls.Add(lblPrzesunDetailTitle);

            Panel przesunDetailContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15, 10, 15, 10) };

            int dY = 5;
            Label lblZrodloLabel = new Label { Text = "Źródło:", Location = new Point(0, dY), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = SecondaryTextColor };
            lblPrzesunZrodlo = new Label { Text = "-", Location = new Point(100, dY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = DangerColor };
            dY += 28;

            Label lblArrow = new Label { Text = "→", Location = new Point(40, dY - 8), AutoSize = true, Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = PurpleColor };

            Label lblCelLabel = new Label { Text = "Cel:", Location = new Point(0, dY), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = SecondaryTextColor };
            lblPrzesunCel = new Label { Text = "-", Location = new Point(100, dY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = SuccessColor };
            dY += 28;

            Label lblStatusLabel = new Label { Text = "Status:", Location = new Point(0, dY), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = SecondaryTextColor };
            lblPrzesunStatus = new Label { Text = "-", Location = new Point(100, dY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = PrimaryColor };
            dY += 28;

            Label lblUwagiLabel = new Label { Text = "Uwagi:", Location = new Point(0, dY), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = SecondaryTextColor };
            lblPrzesunUwagi = new Label { Text = "-", Location = new Point(100, dY), AutoSize = true, MaximumSize = new Size(500, 0), Font = new Font("Segoe UI", 9F), ForeColor = TextColor };
            dY += 28;

            Label lblPozLabel = new Label { Text = "Pozycje:", Location = new Point(0, dY), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = SecondaryTextColor };
            lblPrzesunPozycje = new Label { Text = "-", Location = new Point(100, dY), AutoSize = true, MaximumSize = new Size(500, 0), Font = new Font("Segoe UI", 9.5F), ForeColor = TextColor };
            dY += 60;

            btnRealizujPrzesun = CreateModernButton("Realizuj", 100, dY, 110, SuccessColor);
            btnRealizujPrzesun.Click += BtnRealizujPrzesuniecieClick;

            btnAnulujPrzesun = CreateModernButton("Anuluj", 220, dY, 90, DangerColor);
            btnAnulujPrzesun.Click += BtnAnulujPrzesuniecieClick;

            przesunDetailContent.Controls.AddRange(new Control[]
            {
                lblZrodloLabel, lblPrzesunZrodlo, lblArrow,
                lblCelLabel, lblPrzesunCel,
                lblStatusLabel, lblPrzesunStatus,
                lblUwagiLabel, lblPrzesunUwagi,
                lblPozLabel, lblPrzesunPozycje,
                btnRealizujPrzesun, btnAnulujPrzesun
            });

            panelSzczegolyPrzesun.Controls.Add(przesunDetailContent);
            panelSzczegolyPrzesun.Controls.Add(przesunDetailHeader);

            splitPrzesun.Panel2.Controls.Add(panelSzczegolyPrzesun);

            przesunMainPanel.Controls.Add(splitPrzesun);
            przesunMainPanel.Controls.Add(przesunToolbar);
            przesunMainPanel.Controls.Add(przesunHeader);
            tabPrzesun.Controls.Add(przesunMainPanel);

            // === ZAKŁADKA 1 (PIERWSZA): ZAMÓWIENIA MROŻONKI ===
            var TealColor = Color.FromArgb(0, 150, 136);
            var TealDark = Color.FromArgb(0, 121, 107);
            TabPage tabZamMroz = new TabPage("  Zamówienia mrożonki  ");
            tabZamMroz.BackColor = BackgroundColor;
            tabZamMroz.Padding = new Padding(0);

            Panel zamMrozMainPanel = new Panel { Dock = DockStyle.Fill };

            // === NAGŁÓWEK 48px z gradient teal ===
            Panel zamMrozHeader = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = TealColor };
            zamMrozHeader.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(zamMrozHeader.ClientRectangle,
                    TealColor, TealDark, LinearGradientMode.Horizontal))
                    e.Graphics.FillRectangle(brush, zamMrozHeader.ClientRectangle);
            };

            Label lblSnowflake = new Label
            {
                Text = "\u2744",
                Location = new Point(14, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 20F),
                ForeColor = Color.FromArgb(180, 255, 255),
                BackColor = Color.Transparent
            };

            lblZamMrozTitle = new Label
            {
                Text = "ZAMÓWIENIA MROŻONKI",
                Location = new Point(52, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            // 3 stat cards in header (right-aligned)
            Panel cardSumaKg = new Panel { Size = new Size(160, 36), BackColor = Color.Transparent };
            cardSumaKg.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundedRect(0, 0, cardSumaKg.Width, cardSumaKg.Height, 6))
                using (var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    e.Graphics.FillPath(brush, path);
            };
            Label lblSumaKgLabel = new Label { Text = "SUMA KG", Location = new Point(10, 2), AutoSize = true, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 255, 255), BackColor = Color.Transparent };
            lblZamMrozSumaKg = new Label { Text = "0 kg", Location = new Point(10, 16), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            cardSumaKg.Controls.AddRange(new Control[] { lblSumaKgLabel, lblZamMrozSumaKg });

            Panel cardLiczbaZam = new Panel { Size = new Size(110, 36), BackColor = Color.Transparent };
            cardLiczbaZam.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundedRect(0, 0, cardLiczbaZam.Width, cardLiczbaZam.Height, 6))
                using (var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    e.Graphics.FillPath(brush, path);
            };
            Label lblLiczbaZamLabel = new Label { Text = "ZAMÓWIEŃ", Location = new Point(10, 2), AutoSize = true, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 255, 255), BackColor = Color.Transparent };
            lblZamMrozLiczbaZam = new Label { Text = "0", Location = new Point(10, 16), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            cardLiczbaZam.Controls.AddRange(new Control[] { lblLiczbaZamLabel, lblZamMrozLiczbaZam });

            Panel cardDni = new Panel { Size = new Size(110, 36), BackColor = Color.Transparent };
            cardDni.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundedRect(0, 0, cardDni.Width, cardDni.Height, 6))
                using (var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    e.Graphics.FillPath(brush, path);
            };
            Label lblDniLabel = new Label { Text = "DNI", Location = new Point(10, 2), AutoSize = true, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 255, 255), BackColor = Color.Transparent };
            lblZamMrozDni = new Label { Text = "21", Location = new Point(10, 16), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            cardDni.Controls.AddRange(new Control[] { lblDniLabel, lblZamMrozDni });

            zamMrozHeader.Resize += (s, e) =>
            {
                cardDni.Location = new Point(zamMrozHeader.Width - cardDni.Width - 12, 6);
                cardLiczbaZam.Location = new Point(zamMrozHeader.Width - cardDni.Width - cardLiczbaZam.Width - 18, 6);
                cardSumaKg.Location = new Point(zamMrozHeader.Width - cardDni.Width - cardLiczbaZam.Width - cardSumaKg.Width - 24, 6);
            };
            cardDni.Location = new Point(600, 6);
            cardLiczbaZam.Location = new Point(480, 6);
            cardSumaKg.Location = new Point(310, 6);

            zamMrozHeader.Controls.AddRange(new Control[] { lblSnowflake, lblZamMrozTitle, cardSumaKg, cardLiczbaZam, cardDni });

            // === TOOLBAR (50px) — DatePicker "od" + Odśwież ===
            Panel zamMrozToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(252, 253, 255) };
            zamMrozToolbar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(228, 230, 235), 1))
                    e.Graphics.DrawLine(pen, 0, zamMrozToolbar.Height - 1, zamMrozToolbar.Width, zamMrozToolbar.Height - 1);
            };

            Label lblOdLabel = new Label
            {
                Text = "Zamówienia od:",
                Location = new Point(14, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            dtpZamMrozOd = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Location = new Point(130, 11),
                Size = new Size(130, 30),
                Font = new Font("Segoe UI", 10F),
                Value = DateTime.Today
            };
            dtpZamMrozOd.ValueChanged += async (s, e) => await LoadZamowieniaMrozonkiAsync();

            Label lblDoLabel = new Label
            {
                Text = "(+ 3 tygodnie)",
                Location = new Point(268, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(140, 140, 140)
            };

            Button btnDzisiaj = CreateModernButton("Dziś", 380, 8, 65, TealColor);
            btnDzisiaj.Click += (s, e) => { dtpZamMrozOd.Value = DateTime.Today; };

            btnZamMrozRefresh = CreateModernButton("Odśwież", 455, 8, 90, InfoColor);
            btnZamMrozRefresh.Click += async (s, e) => await LoadZamowieniaMrozonkiAsync();

            zamMrozToolbar.Controls.AddRange(new Control[] { lblOdLabel, dtpZamMrozOd, lblDoLabel, btnDzisiaj, btnZamMrozRefresh });

            // === SPLIT: lewa = grid zamówień (60%), prawa = szczegóły + pozycje ===
            SplitContainer splitZamMroz = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 4,
                BackColor = BackgroundColor
            };
            splitZamMroz.SizeChanged += (s, e) =>
            {
                try
                {
                    if (splitZamMroz.Width > 100)
                        splitZamMroz.SplitterDistance = (int)(splitZamMroz.Width * 0.60);
                }
                catch { }
            };

            // === LEWA STRONA — grid zamówień ===
            Panel zamMrozLeftPanel = new Panel { Dock = DockStyle.Fill };

            dgvZamMrozOrders = CreateStyledDataGridView();
            dgvZamMrozOrders.ColumnHeadersDefaultCellStyle.BackColor = TealColor;
            dgvZamMrozOrders.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvZamMrozOrders.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvZamMrozOrders.EnableHeadersVisualStyles = false;
            dgvZamMrozOrders.ColumnHeadersHeight = 36;
            dgvZamMrozOrders.RowTemplate.Height = 48;
            dgvZamMrozOrders.SelectionChanged += DgvZamMrozOrders_SelectionChanged;

            // CellPainting: left-border color by status + handlowiec avatar
            dgvZamMrozOrders.CellPainting += DgvZamMrozOrders_CellPainting;

            zamMrozLeftPanel.Controls.Add(dgvZamMrozOrders);

            // === PRAWA STRONA — szczegóły (góra) + pozycje (dół) ===
            Panel zamMrozRightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 249, 250) };

            SplitContainer splitZamMrozRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 3,
                BackColor = Color.FromArgb(248, 249, 250)
            };
            splitZamMrozRight.SizeChanged += (s, e) =>
            {
                try
                {
                    if (splitZamMrozRight.Height > 100)
                        splitZamMrozRight.SplitterDistance = (int)(splitZamMrozRight.Height * 0.50);
                }
                catch { }
            };

            // Prawa-góra: szczegóły z avatarem handlowca
            Panel detailCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 249, 250), Padding = new Padding(6) };
            Panel detailInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            detailInner.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, detailInner.Width - 1, detailInner.Height - 1);
            };

            Panel detailHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(55, 71, 79) };
            Label lblDetailTitle = new Label
            {
                Text = "  SZCZEGÓŁY ZAMÓWIENIA",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };
            detailHeader.Controls.Add(lblDetailTitle);

            Panel detailContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 8, 14, 8), AutoScroll = true };

            // Handlowiec avatar photo (top of detail panel)
            _zamMrozHandlowiecAvatar = new Panel
            {
                Size = new Size(56, 56),
                Location = new Point(14, 6),
                BackColor = Color.Transparent,
                Tag = "(Brak)" // store handlowiec name for avatar lookup
            };
            _zamMrozHandlowiecAvatar.Paint += (s, e) =>
            {
                var p = (Panel)s;
                string handlName = p.Tag?.ToString() ?? "(Brak)";

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var avatarImg = GetHandlowiecAvatarImage(handlName, 56);
                if (avatarImg != null)
                    e.Graphics.DrawImage(avatarImg, 0, 0, 56, 56);
            };

            detailContent.Controls.Add(_zamMrozHandlowiecAvatar);

            int detailY = 68;
            lblZamMrozKlient = AddInfoRow(detailContent, "Klient:", ref detailY);
            lblZamMrozHandlowiec = AddInfoRow(detailContent, "Handlowiec:", ref detailY);
            lblZamMrozWyjazd = AddInfoRow(detailContent, "Wyjazd:", ref detailY);
            lblZamMrozPojazd = AddInfoRow(detailContent, "Pojazd:", ref detailY);
            lblZamMrozKierowca = AddInfoRow(detailContent, "Kierowca:", ref detailY);
            lblZamMrozStatus = AddInfoRow(detailContent, "Status:", ref detailY);
            lblZamMrozUwagi = AddInfoRow(detailContent, "Uwagi:", ref detailY);

            detailInner.Controls.Add(detailContent);
            detailInner.Controls.Add(detailHeader);
            detailCard.Controls.Add(detailInner);
            splitZamMrozRight.Panel1.Controls.Add(detailCard);

            // Prawa-dół: pozycje mrożone
            Panel pozycjeCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 249, 250), Padding = new Padding(6) };
            Panel pozycjeInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            pozycjeInner.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, pozycjeInner.Width - 1, pozycjeInner.Height - 1);
            };

            Panel pozycjeHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(38, 50, 56) };
            Label lblPozycjeTitle = new Label
            {
                Text = "  POZYCJE MROŻONE",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pozycjeHeader.Controls.Add(lblPozycjeTitle);

            dgvZamMrozPozycje = CreateStyledDataGridView();
            dgvZamMrozPozycje.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(38, 50, 56);
            dgvZamMrozPozycje.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvZamMrozPozycje.EnableHeadersVisualStyles = false;
            dgvZamMrozPozycje.RowTemplate.Height = 42;

            pozycjeInner.Controls.Add(dgvZamMrozPozycje);
            pozycjeInner.Controls.Add(pozycjeHeader);
            pozycjeCard.Controls.Add(pozycjeInner);
            splitZamMrozRight.Panel2.Controls.Add(pozycjeCard);

            zamMrozRightPanel.Controls.Add(splitZamMrozRight);

            splitZamMroz.Panel1.Controls.Add(zamMrozLeftPanel);
            splitZamMroz.Panel2.Controls.Add(zamMrozRightPanel);

            zamMrozMainPanel.Controls.Add(splitZamMroz);
            zamMrozMainPanel.Controls.Add(zamMrozToolbar);
            zamMrozMainPanel.Controls.Add(zamMrozHeader);
            tabZamMroz.Controls.Add(zamMrozMainPanel);

            // Zamówienia mrożonki jako pierwsza zakładka (najważniejsza)
            tc.TabPages.AddRange(new TabPage[] { tabZamMroz, tab4, tabRez, tab5, tabPrzesun, tab1 });
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

        private Label AddInfoRow(Panel parent, string labelText, ref int y)
        {
            Label lbl = new Label
            {
                Text = labelText,
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            Label val = new Label
            {
                Text = "-",
                Location = new Point(95, y),
                AutoSize = true,
                MaximumSize = new Size(280, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = TextColor
            };
            parent.Controls.Add(lbl);
            parent.Controls.Add(val);
            y += 28;
            return val;
        }

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

            // Auto-refresh zamówień mrożonek co 2 minuty
            _zamMrozAutoRefreshTimer = new Timer { Interval = 120000, Enabled = false };
            _zamMrozAutoRefreshTimer.Tick += async (s, e) =>
            {
                if (tabControl.SelectedIndex == 0 && _zamMrozLoaded)
                    await LoadZamowieniaMrozonkiAsync();
            };

            this.Load += Mroznia_Load;
        }

        private async void Mroznia_Load(object sender, EventArgs e)
        {
            dtpOd.Value = DateTime.Now.AddDays(-30);
            dtpDo.Value = DateTime.Now;

            try
            {
                ShowLoading("Ładowanie zamówień mrożonek...");

                // Zamówienia mrożonki — teraz pierwsza zakładka, auto-load
                _zamMrozLoaded = true;
                await LoadZamowieniaMrozonkiAsync();
                _zamMrozAutoRefreshTimer.Enabled = true;

                // Załaduj mroźnie zewnętrzne
                LoadMroznieZewnetrzneDoTabeli();

                // Załaduj przesunięcia między mroźniami
                try { LoadPrzesunieciaDoTabeli(); } catch { }

                // Załaduj stan magazynu w tle
                BtnStanMagazynu_Click(null, null);

                // Automatycznie załaduj przegląd dzienny i wykresy
                AutoLoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd Mroznia_Load: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadInitialData()
        {
            LoadProductImagesForGrids();
        }

        private void LoadProductImagesForGrids()
        {
            try
            {
                // Krok 1: Pobierz zdjecia z LibraNet (TowarId -> bytes)
                var imgBytes = new Dictionary<int, byte[]>();
                using (var cn = new SqlConnection(_connLibra))
                {
                    cn.Open();
                    var cmdCheck = new SqlCommand("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END", cn);
                    if ((int)cmdCheck.ExecuteScalar() == 0) return;

                    var cmd = new SqlCommand("SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1", cn);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        if (!rdr.IsDBNull(1))
                            imgBytes[rdr.GetInt32(0)] = (byte[])rdr[1];
                    }
                }
                if (imgBytes.Count == 0) return;

                // Krok 2: Pobierz mapowanie ID -> kod z Handel (oba katalogi)
                var idToKod = new Dictionary<int, string>();
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    var ids = string.Join(",", imgBytes.Keys);
                    var cmd = new SqlCommand($"SELECT ID, kod FROM HM.TW WHERE ID IN ({ids})", cn);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                        idToKod[rdr.GetInt32(0)] = rdr.GetString(1);
                }

                // Krok 3: Utworz slownik kod -> Image
                foreach (var kvp in imgBytes)
                {
                    if (!idToKod.TryGetValue(kvp.Key, out string kod)) continue;
                    try
                    {
                        using var ms = new MemoryStream(kvp.Value);
                        var img = System.Drawing.Image.FromStream(ms);
                        // Skaluj do 36x36
                        var thumb = new Bitmap(36, 36);
                        using (var g = Graphics.FromImage(thumb))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(img, 0, 0, 36, 36);
                        }
                        _produktImagesWF[kod] = thumb;
                        img.Dispose();
                    }
                    catch { }
                }

                // Krok 4: Dodaj obrazki pod kodami świeżymi (mapowanie mrożony→świeży)
                try
                {
                    var mapowanie = WczytajMapowaniaMrozonyNaSwiezy();
                    foreach (var kvp in mapowanie)
                    {
                        // kvp.Key = kodMrożony, kvp.Value = kodŚwieży
                        if (_produktImagesWF.ContainsKey(kvp.Key) && !_produktImagesWF.ContainsKey(kvp.Value))
                            _produktImagesWF[kvp.Value] = _produktImagesWF[kvp.Key];
                    }
                }
                catch { }

                // Krok 5: Podepnij DataBindingComplete do wszystkich DataGridView
                if (dgvStanMagazynu != null) dgvStanMagazynu.DataBindingComplete += Dgv_DataBindingComplete;
                if (dgvDzienne != null) dgvDzienne.DataBindingComplete += Dgv_DataBindingComplete;
                if (dgvZamowienia != null) dgvZamowienia.DataBindingComplete += Dgv_DataBindingComplete;
                if (dgvZamMrozPozycje != null) dgvZamMrozPozycje.DataBindingComplete += Dgv_DataBindingComplete;
            }
            catch { }
        }

        private bool _isAddingImageColumn = false;

        private void Dgv_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (_isAddingImageColumn) return;
            if (sender is DataGridView dgv)
            {
                // dgvStanMagazynu uses CellPainting for inline images instead of _Img column
                if (dgv == dgvStanMagazynu) return;
                AddProductImagesToGrid(dgv);
            }
        }

        private void AddProductImagesToGrid(DataGridView dgv)
        {
            if (_produktImagesWF.Count == 0) return;

            // Znajdz kolumne z nazwa produktu
            string prodCol = null;
            foreach (var name in new[] { "Produkt", "kod", "Kod", "KodProduktu" })
            {
                if (dgv.Columns.Contains(name)) { prodCol = name; break; }
            }
            if (prodCol == null) return;

            _isAddingImageColumn = true;
            try
            {
                // Usun stara kolumne obrazkow
                if (dgv.Columns.Contains("_Img"))
                    dgv.Columns.Remove("_Img");

                // Dodaj kolumne obrazkow na pozycji 0
                var imgCol = new DataGridViewImageColumn
                {
                    Name = "_Img",
                    HeaderText = "",
                    Width = 42,
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    DisplayIndex = 0
                };
                imgCol.DefaultCellStyle.NullValue = null;
                dgv.Columns.Insert(0, imgCol);

                // Wypelnij obrazkami
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;
                    string produktName = row.Cells[prodCol]?.Value?.ToString();
                    if (string.IsNullOrEmpty(produktName)) continue;

                    // Dokladne dopasowanie
                    if (_produktImagesWF.TryGetValue(produktName, out var img))
                    {
                        row.Cells["_Img"].Value = img;
                        continue;
                    }
                    // Dopasowanie czesciowe (np. "Kurczak A" pasuje do "Kurczak A klasa mrozony")
                    var match = _produktImagesWF.FirstOrDefault(kv =>
                        produktName.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                        kv.Key.StartsWith(produktName, StringComparison.OrdinalIgnoreCase));
                    if (match.Value != null)
                        row.Cells["_Img"].Value = match.Value;
                }

                // Ustaw wiersz nieco wyzszy aby zmiescil obrazek
                if (dgv.RowTemplate.Height < 40)
                    dgv.RowTemplate.Height = 40;
                foreach (DataGridViewRow row in dgv.Rows)
                    if (row.Height < 40) row.Height = 40;
            }
            finally
            {
                _isAddingImageColumn = false;
            }
        }

        // ============================================
        // GŁÓWNE FUNKCJE ANALITYCZNE
        // ============================================

        private async void AutoLoadData()
        {
            if (dtpOd.Value > dtpDo.Value) return;

            statusLabel.Text = "Ładowanie danych...";
            try { progressBar.Visible = true; progressBar.Value = 0; } catch { }
            DisableButtons();

            DateTime od = dtpOd.Value.Date;
            DateTime doDaty = dtpDo.Value.Date;

            try
            {
                try { progressBar.Value = 10; } catch { }

                DataTable dtDzienne = null, dtTrendy = null;
                decimal wydanoSuma = 0, przyjetoSuma = 0;
                int dniSuma = 0;

                await Task.Run(() =>
                {
                    dtDzienne = FetchDzienneZestawienie(od, doDaty);
                    dtTrendy = FetchTrendyData(od, doDaty);
                    FetchKartyStatystyk(od, doDaty, out wydanoSuma, out przyjetoSuma, out dniSuma);
                });

                try { progressBar.Value = 50; } catch { }
                DisplayDzienneZestawienie(dtDzienne);

                try { progressBar.Value = 70; } catch { }
                DisplayTrendy(dtTrendy);

                try { progressBar.Value = 90; } catch { }
                DisplayKartyStatystyk(wydanoSuma, przyjetoSuma, dniSuma);

                try { progressBar.Value = 100; } catch { }
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
                try { progressBar.Visible = false; } catch { }
                EnableButtons();
            }
        }

        private DataTable FetchDzienneZestawienie(DateTime od, DateTime doDaty)
        {
            // sMM- / sMK- = wydanie z mroźni (ilosc > 0 w bazie)
            // sMM+ / sMK+ = przyjęcie do mroźni (ilosc < 0 w bazie)
            string query = $@"
                ;WITH Dzienne AS (
                    SELECT
                        MG.[Data] AS Data,
                        SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Wydano,
                        ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Przyjeto,
                        SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) -
                        ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Bilans,
                        COUNT(DISTINCT MZ.kod) AS LiczbaPozycji
                    FROM [HANDEL].[HM].[MG]
                    JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                    WHERE MG.magazyn = {MagazynMroznia}
                    AND {SQL_FILTR_SERII}
                    AND MG.[Data] BETWEEN @Od AND @Do
                    GROUP BY MG.[Data]
                )
                SELECT d.Data, d.Wydano, d.Przyjeto, d.Bilans, d.LiczbaPozycji,
                       w.ProduktyWydaniaRaw, pr.ProduktyPrzyjeciaRaw
                FROM Dzienne d
                OUTER APPLY (
                    SELECT STRING_AGG(s.kod + '|' + CAST(CAST(s.qty AS DECIMAL(18,1)) AS VARCHAR(30)), ';;')
                        WITHIN GROUP (ORDER BY s.qty DESC) AS ProduktyWydaniaRaw
                    FROM (
                        SELECT MZ2.kod, SUM(MZ2.ilosc) AS qty
                        FROM [HANDEL].[HM].[MG] MG2
                        JOIN [HANDEL].[HM].[MZ] MZ2 ON MG2.ID = MZ2.super
                        WHERE MG2.magazyn = {MagazynMroznia}
                        AND {SQL_FILTR_SERII.Replace("mg.", "MG2.")}
                        AND MG2.[Data] = d.Data
                        AND MZ2.ilosc > 0
                        GROUP BY MZ2.kod
                    ) s
                ) w
                OUTER APPLY (
                    SELECT STRING_AGG(s.kod + '|' + CAST(CAST(s.qty AS DECIMAL(18,1)) AS VARCHAR(30)), ';;')
                        WITHIN GROUP (ORDER BY s.qty DESC) AS ProduktyPrzyjeciaRaw
                    FROM (
                        SELECT MZ2.kod, ABS(SUM(MZ2.ilosc)) AS qty
                        FROM [HANDEL].[HM].[MG] MG2
                        JOIN [HANDEL].[HM].[MZ] MZ2 ON MG2.ID = MZ2.super
                        WHERE MG2.magazyn = {MagazynMroznia}
                        AND {SQL_FILTR_SERII.Replace("mg.", "MG2.")}
                        AND MG2.[Data] = d.Data
                        AND MZ2.ilosc < 0
                        GROUP BY MZ2.kod
                    ) s
                ) pr
                ORDER BY d.Data DESC";

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

        private List<(string KodOriginal, string DisplayName, decimal Qty, bool IsWydanie)> ParseProduktyRaw(
            string raw, bool isWydanie, Dictionary<string, string> mapowanie)
        {
            var result = new List<(string KodOriginal, string DisplayName, decimal Qty, bool IsWydanie)>();
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var entry in raw.Split(new[] { ";;" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int sep = entry.LastIndexOf('|');
                if (sep <= 0) continue;
                string kod = entry.Substring(0, sep).Trim();
                if (!decimal.TryParse(entry.Substring(sep + 1).Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal qty)) continue;

                string displayName = mapowanie.TryGetValue(kod, out string mapped) ? mapped : kod;
                result.Add((kod, displayName, qty, isWydanie));
            }
            return result;
        }

        private void DisplayDzienneZestawienie(DataTable dt)
        {
            cachedDzienneData = dt;

            // Wczytaj mapowanie mrożony→świeży dla krótszych nazw
            var mapowanieMrozonySwiezy = WczytajMapowaniaMrozonyNaSwiezy();

            // Buduj dane strukturalne pozycji per wiersz
            _dziennePozycjeData = new List<List<(string KodOriginal, string DisplayName, decimal Qty, bool IsWydanie)>>();

            // Dodaj kolumnę "Data i dzień" łączącą datę z polską nazwą dnia
            var plCulture = new System.Globalization.CultureInfo("pl-PL");
            if (!dt.Columns.Contains("DataDzien"))
            {
                dt.Columns.Add("DataDzien", typeof(string));
                dt.Columns.Add("Pozycje", typeof(string));
                foreach (DataRow row in dt.Rows)
                {
                    DateTime data = Convert.ToDateTime(row["Data"]);
                    string dzienSkrot = data.ToString("ddd", plCulture);
                    dzienSkrot = char.ToUpper(dzienSkrot[0]) + dzienSkrot.Substring(1);
                    row["DataDzien"] = $"{data:dd.MM} {dzienSkrot}";

                    // Parsuj wydania i przyjęcia
                    string rawWydania = row.Table.Columns.Contains("ProduktyWydaniaRaw")
                        ? row["ProduktyWydaniaRaw"]?.ToString() : "";
                    string rawPrzyjecia = row.Table.Columns.Contains("ProduktyPrzyjeciaRaw")
                        ? row["ProduktyPrzyjeciaRaw"]?.ToString() : "";

                    var pozycje = new List<(string KodOriginal, string DisplayName, decimal Qty, bool IsWydanie)>();
                    pozycje.AddRange(ParseProduktyRaw(rawWydania, true, mapowanieMrozonySwiezy));
                    pozycje.AddRange(ParseProduktyRaw(rawPrzyjecia, false, mapowanieMrozonySwiezy));
                    _dziennePozycjeData.Add(pozycje);

                    // Buduj tekst Pozycje (fallback / tooltip)
                    var parts = new List<string>();
                    foreach (var p in pozycje)
                    {
                        string prefix = p.IsWydanie ? "↑" : "↓";
                        parts.Add($"{prefix} {p.DisplayName}: {p.Qty:N0} kg");
                    }
                    row["Pozycje"] = string.Join(", ", parts);
                }
                // Przenieś kolumnę na początek
                dt.Columns["DataDzien"].SetOrdinal(0);
            }

            dgvDzienne.DataSource = dt;

            // Ukryj kolumny pomocnicze
            if (dgvDzienne.Columns["Data"] != null)
                dgvDzienne.Columns["Data"].Visible = false;
            if (dgvDzienne.Columns["LiczbaPozycji"] != null)
                dgvDzienne.Columns["LiczbaPozycji"].Visible = false;
            if (dgvDzienne.Columns["ProduktyWydaniaRaw"] != null)
                dgvDzienne.Columns["ProduktyWydaniaRaw"].Visible = false;
            if (dgvDzienne.Columns["ProduktyPrzyjeciaRaw"] != null)
                dgvDzienne.Columns["ProduktyPrzyjeciaRaw"].Visible = false;

            FormatujKolumne(dgvDzienne, "DataDzien", "Data");
            FormatujKolumne(dgvDzienne, "Wydano", "Wydano", "#,##0' kg'");
            FormatujKolumne(dgvDzienne, "Przyjeto", "Przyjęto", "#,##0' kg'");
            FormatujKolumne(dgvDzienne, "Bilans", "Bilans", "#,##0' kg'");
            FormatujKolumne(dgvDzienne, "Pozycje", "Pozycje");

            // Szerokości kolumn — Data/Wydano/Przyjęto/Bilans dopasowane do zawartości, Pozycje wypełnia resztę
            if (dgvDzienne.Columns["DataDzien"] != null)
            {
                dgvDzienne.Columns["DataDzien"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvDzienne.Columns["DataDzien"].DefaultCellStyle.Font = new Font("Segoe UI", 7.5F);
            }
            if (dgvDzienne.Columns["Wydano"] != null)
            {
                dgvDzienne.Columns["Wydano"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvDzienne.Columns["Wydano"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvDzienne.Columns["Wydano"].DefaultCellStyle.Font = new Font("Segoe UI", 7.5F);
            }
            if (dgvDzienne.Columns["Przyjeto"] != null)
            {
                dgvDzienne.Columns["Przyjeto"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvDzienne.Columns["Przyjeto"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvDzienne.Columns["Przyjeto"].DefaultCellStyle.Font = new Font("Segoe UI", 7.5F);
            }
            if (dgvDzienne.Columns["Bilans"] != null)
            {
                dgvDzienne.Columns["Bilans"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvDzienne.Columns["Bilans"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvDzienne.Columns["Bilans"].DefaultCellStyle.Font = new Font("Segoe UI", 7.5F);
            }
            if (dgvDzienne.Columns["Pozycje"] != null)
            {
                dgvDzienne.Columns["Pozycje"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            decimal sumaWydano = 0, sumaPrzyjeto = 0;

            foreach (DataGridViewRow row in dgvDzienne.Rows)
            {
                // Koloruj bilans
                if (row.Cells["Bilans"].Value != null && row.Cells["Bilans"].Value != DBNull.Value)
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

                // Koloruj wydano/przyjęto: wydania=zielone, przyjęcia=czerwone
                if (row.Cells["Wydano"].Value != null && row.Cells["Wydano"].Value != DBNull.Value)
                {
                    decimal w = Convert.ToDecimal(row.Cells["Wydano"].Value);
                    sumaWydano += w;
                    row.Cells["Wydano"].Style.ForeColor = SuccessColor;
                }
                if (row.Cells["Przyjeto"].Value != null && row.Cells["Przyjeto"].Value != DBNull.Value)
                {
                    decimal p = Convert.ToDecimal(row.Cells["Przyjeto"].Value);
                    sumaPrzyjeto += p;
                    row.Cells["Przyjeto"].Style.ForeColor = DangerColor;
                }

                // Wyróżnij dzisiejszy dzień + tło weekendów
                if (row.Cells["Data"].Value != null && row.Cells["Data"].Value != DBNull.Value)
                {
                    DateTime data = Convert.ToDateTime(row.Cells["Data"].Value);
                    if (data.Date == DateTime.Today)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 224);
                        row.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        row.Cells["DataDzien"].Style.ForeColor = Color.FromArgb(200, 100, 0);
                    }
                    else if (data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 240);
                    }
                }

                // Naprzemienne tło (zebra) - ale nie nadpisuj weekendów
                if (row.Index % 2 == 1 && row.DefaultCellStyle.BackColor == Color.Empty)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
                }
            }

            // Oblicz wysokość wierszy — mierz rzeczywistą szerokość pozycji
            const int lineH = 18;
            const int rowPad = 6;
            const int minRowH = 26;
            const int imgSize2 = 14;
            int pozColWidth = dgvDzienne.Columns.Contains("Pozycje")
                ? dgvDzienne.Columns["Pozycje"].Width : 400;
            if (pozColWidth < 100) pozColWidth = 400;

            using (var measureFont = new Font("Segoe UI", 7.5F))
            using (var measureG = dgvDzienne.CreateGraphics())
            {
                for (int i = 0; i < dgvDzienne.Rows.Count; i++)
                {
                    if (i >= _dziennePozycjeData.Count || _dziennePozycjeData[i].Count == 0)
                    {
                        dgvDzienne.Rows[i].Height = minRowH;
                        continue;
                    }

                    var pozycje = _dziennePozycjeData[i];
                    int nLines = 1;
                    int tempX = 2;
                    int maxX = pozColWidth - 2;

                    foreach (var poz in pozycje)
                    {
                        string txt = $"{(poz.IsWydanie ? "↑" : "↓")} {poz.DisplayName}: {poz.Qty:N0} kg";
                        bool hasImg = FindProductImage(poz.KodOriginal, poz.DisplayName) != null;
                        int w = (hasImg ? imgSize2 + 2 : 0) + (int)measureG.MeasureString(txt, measureFont).Width + 6;
                        if (tempX + w > maxX && tempX > 2) { nLines++; tempX = 2; }
                        tempX += w;
                    }

                    int h = nLines * lineH + rowPad;
                    dgvDzienne.Rows[i].Height = Math.Max(minRowH, h);
                }
            }

            // Aktualizuj karty statystyk
            if (lblDzienneSumaWydano != null)
                lblDzienneSumaWydano.Text = $"{sumaWydano:N0} kg";
            if (lblDzienneSumaPrzyjeto != null)
                lblDzienneSumaPrzyjeto.Text = $"{sumaPrzyjeto:N0} kg";
            if (lblDzienneBilans != null)
            {
                decimal bilansTotal = sumaWydano - sumaPrzyjeto;
                lblDzienneBilans.Text = $"{bilansTotal:N0} kg";
                lblDzienneBilans.ForeColor = bilansTotal > 0 ? DangerColor : bilansTotal < 0 ? SuccessColor : PrimaryColor;
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
                    MIN(MZ.kod) AS KodRaw,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Wydano,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Przyjeto,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) -
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Roznica,
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

                // Ukryj kolumnę surowego kodu (używana do mapowania obrazków)
                if (dgv.Columns["KodRaw"] != null)
                    dgv.Columns["KodRaw"].Visible = false;

                FormatujKolumne(dgv, "Wydano", "Wydano (kg)", "#,##0' kg'");
                FormatujKolumne(dgv, "Przyjeto", "Przyjęto (kg)", "#,##0' kg'");
                FormatujKolumne(dgv, "Roznica", "Różnica (kg)", "#,##0' kg'");
                FormatujKolumne(dgv, "Operacje", "Liczba operacji");

                // Kolumna Wydano/Przyjeto kolorowanie
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.Cells["Wydano"].Value != null && row.Cells["Wydano"].Value != DBNull.Value)
                    {
                        decimal w = Convert.ToDecimal(row.Cells["Wydano"].Value);
                        if (w > 0) row.Cells["Wydano"].Style.ForeColor = SuccessColor;
                    }
                    if (row.Cells["Przyjeto"].Value != null && row.Cells["Przyjeto"].Value != DBNull.Value)
                    {
                        decimal p = Convert.ToDecimal(row.Cells["Przyjeto"].Value);
                        if (p > 0) row.Cells["Przyjeto"].Style.ForeColor = DangerColor;
                    }
                    if (row.Cells["Roznica"].Value != null)
                    {
                        decimal roznica = Convert.ToDecimal(row.Cells["Roznica"].Value);
                        row.Cells["Roznica"].Style.ForeColor = roznica < 0 ? SuccessColor : DangerColor;
                    }
                }

                // CellPainting — obrazek produktu obok nazwy w kolumnie Produkt
                dgv.RowTemplate.Height = 38;
                foreach (DataGridViewRow row in dgv.Rows)
                    row.Height = 38;

                dgv.CellPainting += DgvSzczegolyDnia_CellPainting;
            }
        }

        private void DgvSzczegolyDnia_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dgv = (DataGridView)sender;
            if (!dgv.Columns.Contains("Produkt") || !dgv.Columns.Contains("KodRaw")) return;
            if (e.ColumnIndex != dgv.Columns["Produkt"].Index) return;

            string produkt = dgv.Rows[e.RowIndex].Cells["Produkt"].Value?.ToString() ?? "";
            string kodRaw = dgv.Rows[e.RowIndex].Cells["KodRaw"].Value?.ToString() ?? "";

            e.PaintBackground(e.CellBounds, true);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const int imgSize = 28;
            int x = e.CellBounds.X + 6;
            int yCenter = e.CellBounds.Y + (e.CellBounds.Height - imgSize) / 2;

            var productImg = FindProductImage(kodRaw, produkt);
            if (productImg != null)
            {
                g.DrawImage(productImg, x, yCenter, imgSize, imgSize);
                x += imgSize + 6;
            }

            var font = e.CellStyle.Font ?? dgv.DefaultCellStyle.Font;
            var brush = e.CellStyle.SelectionForeColor != Color.Empty &&
                        (e.State & DataGridViewElementStates.Selected) != 0
                ? new SolidBrush(e.CellStyle.SelectionForeColor)
                : new SolidBrush(e.CellStyle.ForeColor);

            float textY = e.CellBounds.Y + (e.CellBounds.Height - g.MeasureString(produkt, font).Height) / 2f;
            g.DrawString(produkt, font, brush, x, textY);
            brush.Dispose();

            e.Handled = true;
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
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Wydano,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Przyjeto
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

        private List<DateTime> chartDateMap = new List<DateTime>();

        private void DisplayTrendy(DataTable dt)
        {
            if (liveChart == null) return;

            liveChart.Series.Clear();
            liveChart.AxisX.Clear();
            liveChart.AxisY.Clear();
            chartDateMap.Clear();

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
            var weekSections = new List<int>();

            // Wypełnij KAŻDY dzień
            int idx = 0;
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
                chartDateMap.Add(d);

                // Etykieta z dniem tygodnia (Pn, Wt, Śr...)
                string dzienSkrot = d.ToString("ddd", new System.Globalization.CultureInfo("pl-PL")).Substring(0, 2);
                labels.Add($"{d:dd.MM} {dzienSkrot}");

                // Separator tygodniowy - niedziela/poniedziałek
                if (d.DayOfWeek == DayOfWeek.Monday && idx > 0)
                    weekSections.Add(idx);

                idx++;
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

            // Oś X - każdy dzień (separatory tygodniowe dodane jako AxisSection)
            var xAxis = new LiveCharts.Wpf.Axis
            {
                Labels = labels,
                LabelsRotation = -45,
                FontSize = 8.5,
                Separator = new Separator { Step = 1, IsEnabled = false },
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(100, 100, 100))
            };
            foreach (int si in weekSections)
            {
                xAxis.Sections.Add(new AxisSection
                {
                    Value = si - 0.5,
                    SectionWidth = 0,
                    Stroke = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(100, 180, 60, 60)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 3 }
                });
            }
            liveChart.AxisX.Add(xAxis);

            // Oś Y - skrócone etykiety (10k, 50k)
            liveChart.AxisY.Add(new LiveCharts.Wpf.Axis
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

            // Kliknięcie na słupek = pokaż szczegóły dnia
            liveChart.DataClick -= LiveChart_DataClick;
            liveChart.DataClick += LiveChart_DataClick;
        }

        private void LiveChart_DataClick(object sender, ChartPoint chartPoint)
        {
            int pointIdx = (int)chartPoint.X;
            if (pointIdx < 0 || pointIdx >= chartDateMap.Count) return;

            DateTime clickedDate = chartDateMap[pointIdx];

            // Pobierz szczegóły produktów dla tego dnia
            try
            {
                DataTable dtSzczegoly = FetchSzczegolyDnia(clickedDate);

                if (dtSzczegoly == null || dtSzczegoly.Rows.Count == 0)
                {
                    ShowToast($"Brak danych za {clickedDate:dd.MM.yyyy}", WarningColor);
                    return;
                }

                // Pokaż modal ze szczegółami
                ShowChartDayDetailModal(clickedDate, dtSzczegoly);
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Błąd: {ex.Message}";
            }
        }

        private DataTable FetchSzczegolyDnia(DateTime data)
        {
            string query = $@"
                SELECT
                    MZ.kod AS Kod,
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS [Wydano (kg)],
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS [Przyjęto (kg)]
                FROM [HANDEL].[HM].[MG]
                JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
                WHERE MG.magazyn = {MagazynMroznia}
                AND {SQL_FILTR_SERII}
                AND MG.[Data] = @Data
                GROUP BY MZ.kod
                HAVING ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) > 0
                    OR SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) > 0
                ORDER BY ABS(SUM(MZ.ilosc)) DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlDataAdapter adapter = new SqlDataAdapter(query, conn))
                {
                    adapter.SelectCommand.Parameters.AddWithValue("@Data", data);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        private void ShowChartDayDetailModal(DateTime data, DataTable dtSzczegoly)
        {
            string dzien = data.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));

            Form modal = new Form
            {
                Text = $"Szczegóły: {data:dd.MM.yyyy} ({dzien})",
                Size = new Size(650, 500),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            // Nagłówek
            Panel header = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(41, 128, 185) };
            Label lblTitle = new Label
            {
                Text = $"  {data:dd.MM.yyyy}  ({dzien})",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Podsumowanie
            decimal sumaWydano = 0, sumaPrzyjeto = 0;
            foreach (DataRow row in dtSzczegoly.Rows)
            {
                sumaWydano += Convert.ToDecimal(row["Wydano (kg)"] ?? 0);
                sumaPrzyjeto += Convert.ToDecimal(row["Przyjęto (kg)"] ?? 0);
            }
            Label lblSuma = new Label
            {
                Text = $"Wydano: {sumaWydano:N0} kg  |  Przyjęto: {sumaPrzyjeto:N0} kg  |  Pozycji: {dtSzczegoly.Rows.Count}  ",
                Dock = DockStyle.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(220, 255, 255, 255),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0)
            };
            header.Controls.AddRange(new Control[] { lblSuma, lblTitle });

            // Tabela
            DataGridView dgv = CreateStyledDataGridView();
            dgv.DataSource = dtSzczegoly;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(41, 128, 185);

            dgv.DataBindingComplete += (s, e) =>
            {
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (col.ValueType == typeof(decimal))
                    {
                        col.DefaultCellStyle.Format = "#,##0' kg'";
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                }
            };

            modal.Controls.Add(dgv);
            modal.Controls.Add(header);
            modal.ShowDialog();
        }

        private void ResetChartZoom()
        {
            // LiveCharts nie wymaga ręcznego resetu zoom
        }

        private void FetchKartyStatystyk(DateTime od, DateTime doDaty, out decimal wydano, out decimal przyjeto, out int dni)
        {
            string query = $@"
                SELECT
                    SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END) AS Wydano,
                    ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Przyjeto,
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
            ShowLoading("Obliczam stan magazynu...");

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
                    FormatujKolumne(dgvStanMagazynu, colName, "Kod / Produkt");
                    FormatujKolumne(dgvStanMagazynu, "Stan (kg)", "Stan", "#,##0' kg'");
                    FormatujKolumne(dgvStanMagazynu, "Rez. (kg)", "Rezerwacja", "#,##0' kg'");
                    FormatujKolumne(dgvStanMagazynu, "Zmiana", "Zmiana 7d");

                    // Szerokości kolumn
                    if (dgvStanMagazynu.Columns[colName] != null)
                        dgvStanMagazynu.Columns[colName].FillWeight = 35;
                    if (dgvStanMagazynu.Columns["Stan (kg)"] != null)
                    {
                        dgvStanMagazynu.Columns["Stan (kg)"].FillWeight = 18;
                        dgvStanMagazynu.Columns["Stan (kg)"].DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                        dgvStanMagazynu.Columns["Stan (kg)"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    if (dgvStanMagazynu.Columns["Rez. (kg)"] != null)
                    {
                        dgvStanMagazynu.Columns["Rez. (kg)"].FillWeight = 18;
                        dgvStanMagazynu.Columns["Rez. (kg)"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    if (dgvStanMagazynu.Columns["Zmiana"] != null)
                    {
                        dgvStanMagazynu.Columns["Zmiana"].FillWeight = 15;
                        dgvStanMagazynu.Columns["Zmiana"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    // Formatuj kolumny mroźni zewnętrznych
                    foreach (DataGridViewColumn col in dgvStanMagazynu.Columns)
                    {
                        if (col.Name.StartsWith("MZ: "))
                        {
                            col.FillWeight = 14;
                            col.DefaultCellStyle.Format = "#,##0' kg'";
                            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                            col.DefaultCellStyle.BackColor = Color.FromArgb(255, 253, 240);
                            col.HeaderCell.Style.BackColor = Color.FromArgb(255, 185, 0);
                            col.HeaderCell.Style.ForeColor = Color.White;
                            col.HeaderCell.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        }
                    }

                    // Formatuj kolumnę Suma Stan
                    if (dgvStanMagazynu.Columns.Contains("Suma Stan"))
                    {
                        var colSuma = dgvStanMagazynu.Columns["Suma Stan"];
                        colSuma.FillWeight = 18;
                        colSuma.DefaultCellStyle.Format = "#,##0' kg'";
                        colSuma.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                        colSuma.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        colSuma.DefaultCellStyle.BackColor = Color.FromArgb(232, 245, 255);
                        colSuma.HeaderCell.Style.BackColor = Color.FromArgb(0, 105, 217);
                        colSuma.HeaderCell.Style.ForeColor = Color.White;
                    }

                    // Ukryj kolumnę Status
                    if (dgvStanMagazynu.Columns.Contains("Status"))
                        dgvStanMagazynu.Columns["Status"].Visible = false;

                    // Oblicz sumy
                    decimal sumaStan = dtFinal.AsEnumerable().Sum(r => Convert.ToDecimal(r["Stan (kg)"]));
                    decimal sumaRezerwacja = dtFinal.Columns.Contains("Rez. (kg)")
                        ? dtFinal.AsEnumerable().Sum(r => Convert.ToDecimal(r["Rez. (kg)"]))
                        : 0;

                    // Ukryj kolumnę rezerwacji jeśli brak jakichkolwiek rezerwacji
                    if (sumaRezerwacja == 0 && dgvStanMagazynu.Columns.Contains("Rez. (kg)"))
                        dgvStanMagazynu.Columns["Rez. (kg)"].Visible = false;

                    // Wstaw wiersz sumy NA GÓRĘ tabeli
                    DataRow sumRow = dtFinal.NewRow();
                    sumRow[colName] = "SUMA";
                    sumRow["Stan (kg)"] = sumaStan;
                    if (dtFinal.Columns.Contains("Rez. (kg)"))
                        sumRow["Rez. (kg)"] = sumaRezerwacja;
                    sumRow["Zmiana"] = "";
                    sumRow["Status"] = "";
                    dtFinal.Rows.InsertAt(sumRow, 0);

                    // Wiersz sumy - gradient ciemnoniebieski
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.Font =
                        new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.BackColor =
                        Color.FromArgb(30, 60, 114);
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.ForeColor = Color.White;
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.SelectionBackColor =
                        Color.FromArgb(30, 60, 114);
                    dgvStanMagazynu.Rows[0].DefaultCellStyle.SelectionForeColor = Color.White;
                    dgvStanMagazynu.Rows[0].Height = 40;

                    // Kolorowanie wierszy produktów
                    for (int i = 1; i < dgvStanMagazynu.Rows.Count; i++)
                    {
                        var row = dgvStanMagazynu.Rows[i];
                        string status = row.Cells["Status"].Value?.ToString();

                        if (status == "Krytyczny")
                        {
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 238);
                            row.Cells[colName].Style.ForeColor = Color.FromArgb(183, 28, 28);
                            row.Cells[colName].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        }
                        else if (status == "Poważny")
                        {
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 225);
                        }
                        else if (i % 2 == 0)
                        {
                            row.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
                        }

                        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 220, 255);
                        row.DefaultCellStyle.SelectionForeColor = TextColor;

                        // Koloruj kolumnę zmiany
                        string zmiana = row.Cells["Zmiana"].Value?.ToString();
                        if (zmiana?.Contains("\u2191") == true)
                        {
                            row.Cells["Zmiana"].Style.ForeColor = Color.FromArgb(220, 53, 69);
                            row.Cells["Zmiana"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        }
                        else if (zmiana?.Contains("\u2193") == true)
                        {
                            row.Cells["Zmiana"].Style.ForeColor = Color.FromArgb(40, 167, 69);
                            row.Cells["Zmiana"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        }
                        else
                            row.Cells["Zmiana"].Style.ForeColor = Color.FromArgb(180, 180, 180);

                        // Koloruj rezerwację
                        if (dgvStanMagazynu.Columns.Contains("Rez. (kg)"))
                        {
                            var rezVal = row.Cells["Rez. (kg)"].Value;
                            if (rezVal != null && rezVal != DBNull.Value)
                            {
                                decimal rez = Convert.ToDecimal(rezVal);
                                if (rez > 0)
                                {
                                    row.Cells["Rez. (kg)"].Style.ForeColor = Color.FromArgb(220, 53, 69);
                                    row.Cells["Rez. (kg)"].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                                }
                            }
                        }

                        // Koloruj stan - zielony gdy wysoki, pomarańczowy gdy niski
                        var stanVal = row.Cells["Stan (kg)"].Value;
                        if (stanVal != null && stanVal != DBNull.Value)
                        {
                            decimal stan = Convert.ToDecimal(stanVal);
                            if (stan > 500)
                                row.Cells["Stan (kg)"].Style.ForeColor = Color.FromArgb(27, 94, 32);
                            else if (stan > 0 && stan <= 100)
                                row.Cells["Stan (kg)"].Style.ForeColor = Color.FromArgb(230, 126, 34);
                        }
                    }

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

                // Zastosuj stan checkboxa ukrywania mroźni zewnętrznych
                if (chkUkryjZewnetrzne != null && chkUkryjZewnetrzne.Checked)
                    ChkUkryjZewnetrzne_CheckedChanged(null, null);

                statusLabel.Text = $"Stan magazynu na {dataStan:yyyy-MM-dd} (porównanie z {dataPoprzedni:yyyy-MM-dd})";

                UpdateTabBadges();

                // Załaduj wykres stanu na ostatnie 30 dni
                LoadStanMagazynuChart(dataStan);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}\n\nSzczegóły: {ex.StackTrace}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Błąd obliczania stanu";
            }
            finally
            {
                HideLoading();
            }
        }

        private void LoadStanMagazynuChart(DateTime dataDo)
        {
            if (stanChart == null) return;

            try
            {
                stanChart.Series.Clear();
                stanChart.AxisX.Clear();
                stanChart.AxisY.Clear();

                DateTime dataOd = dataDo.AddDays(-30);

                string query = $@"
                    SELECT d.Dzien AS Data,
                           ISNULL(ABS(SUM(mz.iloscwp)), 0) AS Stan
                    FROM (
                        SELECT CAST(DATEADD(DAY, number, @DataOd) AS DATE) AS Dzien
                        FROM master..spt_values
                        WHERE type = 'P' AND number BETWEEN 0 AND DATEDIFF(DAY, @DataOd, @DataDo)
                    ) d
                    LEFT JOIN [HANDEL].[HM].[MZ] mz
                        ON mz.[data] >= '{DataStartowa}'
                        AND mz.[data] <= d.Dzien
                        AND mz.[magazyn] = {MagazynMroznia}
                        AND mz.typ = '0'
                    GROUP BY d.Dzien
                    ORDER BY d.Dzien";

                var stanValues = new ChartValues<double>();
                var weekendValues = new ChartValues<double>();
                var todayValues = new ChartValues<double>();
                var labels = new List<string>();
                var plCulture = new System.Globalization.CultureInfo("pl-PL");
                DateTime dzisiaj = DateTime.Today;
                int todayIndex = -1;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        int idx = 0;
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime data = reader.GetDateTime(0);
                                double stan = Convert.ToDouble(reader[1]);
                                bool isToday = data.Date == dzisiaj;
                                bool isWeekend = data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday;

                                stanValues.Add(!isToday && !isWeekend ? stan : 0);
                                weekendValues.Add(!isToday && isWeekend ? stan : 0);
                                todayValues.Add(isToday ? stan : 0);

                                if (isToday) todayIndex = idx;

                                string dzienSkrot = data.ToString("ddd", plCulture).Substring(0, 2);
                                labels.Add(isToday ? $"► {data:dd.MM} {dzienSkrot}" : $"{data:dd.MM} {dzienSkrot}");
                                idx++;
                            }
                        }
                    }
                }

                if (stanValues.Count == 0) return;

                // Kolejność: najnowszy dzień na górze (ostatni index = góra wykresu)
                // SQL zwraca ORDER BY d.Dzien (najstarszy→najnowszy), co daje najnowszy na górze RowSeries

                Func<ChartPoint, string> labelFmt = p =>
                    p.X >= 1000 ? (p.X / 1000).ToString("0.#") + "k" : p.X > 0 ? p.X.ToString("N0") : "";

                var rowStan = new RowSeries
                {
                    Title = "Dni robocze",
                    Values = stanValues,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(200, 0, 120, 212)),
                    DataLabels = true,
                    LabelPoint = labelFmt,
                    FontSize = 8,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0, 120, 212))
                };

                var rowWeekend = new RowSeries
                {
                    Title = "Weekend",
                    Values = weekendValues,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(150, 180, 180, 180)),
                    DataLabels = true,
                    LabelPoint = labelFmt,
                    FontSize = 8,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(150, 150, 150))
                };

                var rowToday = new RowSeries
                {
                    Title = "Dziś",
                    Values = todayValues,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(255, 255, 140, 0)),
                    DataLabels = true,
                    LabelPoint = labelFmt,
                    FontSize = 8,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(200, 100, 0))
                };

                stanChart.Series.Add(rowStan);
                stanChart.Series.Add(rowWeekend);
                stanChart.Series.Add(rowToday);

                // Oś Y = daty (etykiety)
                var axisY = new LiveCharts.Wpf.Axis
                {
                    Labels = labels,
                    FontSize = 8,
                    Separator = new Separator { Step = 1, IsEnabled = false },
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(100, 100, 100))
                };

                // Podświetl pasek dzisiejszego dnia na osi Y
                if (todayIndex >= 0)
                {
                    axisY.Sections = new SectionsCollection
                    {
                        new AxisSection
                        {
                            Value = todayIndex - 0.5,
                            SectionWidth = 1,
                            Fill = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromArgb(25, 255, 140, 0))
                        }
                    };
                }

                stanChart.AxisY.Add(axisY);

                // Oś X = kg
                stanChart.AxisX.Add(new LiveCharts.Wpf.Axis
                {
                    Title = "kg",
                    LabelFormatter = val => val >= 1000 ? (val / 1000).ToString("0.#") + "k" : val.ToString("N0"),
                    FontSize = 9,
                    MinValue = 0,
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania wykresu stanu: {ex.Message}");
            }
        }

        private void LoadStanMagazynuChartForProduct(string kod, DateTime dataDo)
        {
            if (stanChart == null) return;

            try
            {
                stanChart.Series.Clear();
                stanChart.AxisX.Clear();
                stanChart.AxisY.Clear();

                DateTime dataOd = dataDo.AddDays(-30);

                string query = $@"
                    SELECT d.Dzien AS Data,
                           ISNULL(ABS(SUM(mz.iloscwp)), 0) AS Stan
                    FROM (
                        SELECT CAST(DATEADD(DAY, number, @DataOd) AS DATE) AS Dzien
                        FROM master..spt_values
                        WHERE type = 'P' AND number BETWEEN 0 AND DATEDIFF(DAY, @DataOd, @DataDo)
                    ) d
                    LEFT JOIN [HANDEL].[HM].[MZ] mz
                        ON mz.[data] >= '{DataStartowa}'
                        AND mz.[data] <= d.Dzien
                        AND mz.[magazyn] = {MagazynMroznia}
                        AND mz.typ = '0'
                        AND mz.kod = @Kod
                    GROUP BY d.Dzien
                    ORDER BY d.Dzien";

                var stanValues = new ChartValues<double>();
                var todayValues = new ChartValues<double>();
                var labels = new List<string>();
                var plCulture = new System.Globalization.CultureInfo("pl-PL");
                DateTime dzisiaj = DateTime.Today;
                int todayIndex = -1;
                int idx = 0;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);
                        cmd.Parameters.AddWithValue("@Kod", kod);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime data = reader.GetDateTime(0);
                                double stan = Convert.ToDouble(reader[1]);
                                bool isToday = data.Date == dzisiaj;

                                stanValues.Add(isToday ? 0 : stan);
                                todayValues.Add(isToday ? stan : 0);
                                if (isToday) todayIndex = idx;

                                string dzienSkrot = data.ToString("ddd", plCulture).Substring(0, 2);
                                labels.Add(isToday ? $"► {data:dd.MM} {dzienSkrot}" : $"{data:dd.MM} {dzienSkrot}");
                                idx++;
                            }
                        }
                    }
                }

                if (stanValues.Count == 0) return;

                Func<ChartPoint, string> labelFmt = p =>
                    p.X >= 1000 ? (p.X / 1000).ToString("0.#") + "k" : p.X > 0 ? p.X.ToString("N0") : "";

                stanChart.Series.Add(new RowSeries
                {
                    Title = kod,
                    Values = stanValues,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(200, 76, 175, 80)),
                    DataLabels = true,
                    LabelPoint = labelFmt,
                    FontSize = 8,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(56, 142, 60))
                });

                stanChart.Series.Add(new RowSeries
                {
                    Title = "Dziś",
                    Values = todayValues,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(255, 255, 140, 0)),
                    DataLabels = true,
                    LabelPoint = labelFmt,
                    FontSize = 8,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(200, 100, 0))
                });

                var axisY = new LiveCharts.Wpf.Axis
                {
                    Labels = labels,
                    FontSize = 8,
                    Separator = new Separator { Step = 1, IsEnabled = false },
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(100, 100, 100))
                };

                if (todayIndex >= 0)
                {
                    axisY.Sections = new SectionsCollection
                    {
                        new AxisSection
                        {
                            Value = todayIndex - 0.5,
                            SectionWidth = 1,
                            Fill = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromArgb(25, 255, 140, 0))
                        }
                    };
                }

                stanChart.AxisY.Add(axisY);

                stanChart.AxisX.Add(new LiveCharts.Wpf.Axis
                {
                    Title = "kg",
                    LabelFormatter = val => val >= 1000 ? (val / 1000).ToString("0.#") + "k" : val.ToString("N0"),
                    FontSize = 9,
                    MinValue = 0,
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania wykresu produktu: {ex.Message}");
            }
        }

        private void ChkUkryjZewnetrzne_CheckedChanged(object sender, EventArgs e)
        {
            if (dgvStanMagazynu?.DataSource == null) return;

            bool ukryj = chkUkryjZewnetrzne.Checked;

            foreach (DataGridViewColumn col in dgvStanMagazynu.Columns)
            {
                if (col.Name.StartsWith("MZ: ") || col.Name == "Suma Stan")
                {
                    col.Visible = !ukryj;
                }
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

        private Dictionary<string, string> WczytajMapowaniaMrozonyNaSwiezy()
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
                            if (!string.IsNullOrEmpty(item.KodMrozony) && !string.IsNullOrEmpty(item.KodSwiezy))
                            {
                                mapowanie[item.KodMrozony] = item.KodSwiezy;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania mapowań mrożony→świeży: {ex.Message}");
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
                dgvZamowienia.Columns["Ilość kg"].DefaultCellStyle.Format = "#,##0' kg'";
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

        private void DgvRezerwacje_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvZamowienia.Columns.Contains("Handlowiec") && e.ColumnIndex == dgvZamowienia.Columns["Handlowiec"].Index)
            {
                e.PaintBackground(e.CellBounds, true);

                string handlowiec = e.Value?.ToString() ?? "";
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int avatarSize = 32;
                int cx = e.CellBounds.X + 6;
                int cy = e.CellBounds.Y + (e.CellBounds.Height - avatarSize) / 2;

                var avatarImg = GetHandlowiecAvatarImage(handlowiec, avatarSize);
                if (avatarImg != null)
                    g.DrawImage(avatarImg, cx, cy, avatarSize, avatarSize);

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "(Brak)")
                {
                    using (var font = new Font("Segoe UI", 9F))
                    using (var brush = new SolidBrush(TextColor))
                    {
                        var textRect = new RectangleF(cx + avatarSize + 8, e.CellBounds.Y, e.CellBounds.Width - avatarSize - 20, e.CellBounds.Height);
                        var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        g.DrawString(handlowiec, font, brush, textRect, sf);
                    }
                }

                e.Handled = true;
            }
        }

        private void DgvStanMagazynu_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dgv = (DataGridView)sender;

            // Pasek postępu w kolumnie "Stan (kg)"
            if (dgv.Columns.Contains("Stan (kg)") && e.ColumnIndex == dgv.Columns["Stan (kg)"].Index
                && e.RowIndex > 0) // pomijamy wiersz SUMA
            {
                e.PaintBackground(e.CellBounds, true);

                var val = e.Value;
                if (val != null && val != DBNull.Value)
                {
                    decimal stan = Convert.ToDecimal(val);
                    // Znajdź max stan (z wiersza SUMA, index 0)
                    decimal maxStan = 0;
                    try
                    {
                        var sumaVal = dgv.Rows[0].Cells["Stan (kg)"].Value;
                        if (sumaVal != null && sumaVal != DBNull.Value)
                            maxStan = Convert.ToDecimal(sumaVal);
                    }
                    catch { }

                    if (maxStan > 0 && stan > 0)
                    {
                        float ratio = (float)Math.Min((double)(stan / maxStan), 1.0);
                        int barHeight = 6;
                        int barMaxWidth = e.CellBounds.Width - 12;
                        int barWidth = Math.Max(2, (int)(barMaxWidth * ratio));
                        int barX = e.CellBounds.X + 6;
                        int barY = e.CellBounds.Bottom - barHeight - 4;

                        // Kolor: zielony gdy dużo, pomarańczowy gdy średnio, czerwony gdy mało
                        Color barColor;
                        if (ratio > 0.15f) barColor = Color.FromArgb(60, 16, 124, 16);
                        else if (ratio > 0.05f) barColor = Color.FromArgb(60, 255, 140, 0);
                        else barColor = Color.FromArgb(60, 220, 53, 69);

                        using (var barBrush = new SolidBrush(barColor))
                        {
                            e.Graphics.FillRectangle(barBrush, barX, barY, barWidth, barHeight);
                        }
                    }

                    // Rysuj tekst wartości
                    var cellStyle = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].InheritedStyle;
                    bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                    Color textColor = isSelected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
                    Font font = cellStyle.Font ?? dgv.DefaultCellStyle.Font ?? dgv.Font;
                    string text = stan.ToString("#,##0") + " kg";
                    using (var brush = new SolidBrush(textColor))
                    {
                        var textRect = new RectangleF(e.CellBounds.X + 2, e.CellBounds.Y, e.CellBounds.Width - 8, e.CellBounds.Height - 8);
                        var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                        e.Graphics.DrawString(text, font, brush, textRect, sf);
                    }
                }

                e.Handled = true;
                return;
            }

            // Obrazki inline w kolumnie Kod/Produkt
            if (_produktImagesWF.Count == 0) return;
            string colName = dgv.Columns.Contains("Produkt") ? "Produkt" : "Kod";
            if (!dgv.Columns.Contains(colName)) return;
            if (e.ColumnIndex != dgv.Columns[colName].Index) return;

            e.PaintBackground(e.CellBounds, true);

            string produktName = e.Value?.ToString() ?? "";
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int imgSize = 30;
            int offsetX = e.CellBounds.X + 4;
            int offsetY = e.CellBounds.Y + (e.CellBounds.Height - imgSize) / 2;
            int textOffsetX = offsetX + 4; // default: no image

            // Find matching product image
            System.Drawing.Image img = null;
            if (!string.IsNullOrEmpty(produktName) && produktName != "SUMA")
            {
                if (_produktImagesWF.TryGetValue(produktName, out var exactImg))
                {
                    img = exactImg;
                }
                else
                {
                    var match = _produktImagesWF.FirstOrDefault(kv =>
                        produktName.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                        kv.Key.StartsWith(produktName, StringComparison.OrdinalIgnoreCase));
                    if (match.Value != null)
                        img = match.Value;
                }
            }

            if (img != null)
            {
                g.DrawImage(img, offsetX, offsetY, imgSize, imgSize);
                textOffsetX = offsetX + imgSize + 6;
            }

            // Draw text
            if (!string.IsNullOrEmpty(produktName))
            {
                var cellStyle = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].InheritedStyle;
                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                Color textColor = isSelected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
                Font font = cellStyle.Font ?? dgv.DefaultCellStyle.Font ?? dgv.Font;
                float textWidth = e.CellBounds.Right - textOffsetX - 2;
                if (textWidth > 0)
                {
                    using (var brush = new SolidBrush(textColor))
                    {
                        var textRect = new RectangleF(textOffsetX, e.CellBounds.Y, textWidth, e.CellBounds.Height);
                        var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        g.DrawString(produktName, font, brush, textRect, sf);
                    }
                }
            }

            e.Handled = true;
        }

        private System.Drawing.Image FindProductImage(string kodOriginal, string displayName)
        {
            if (_produktImagesWF.Count == 0) return null;
            // 1. Dokładne dopasowanie
            if (_produktImagesWF.TryGetValue(kodOriginal, out var img)) return img;
            if (kodOriginal != displayName && _produktImagesWF.TryGetValue(displayName, out var img2)) return img2;

            // 2. StartsWith w obie strony
            var match = _produktImagesWF.FirstOrDefault(kv =>
                kodOriginal.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.StartsWith(kodOriginal, StringComparison.OrdinalIgnoreCase) ||
                displayName.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.StartsWith(displayName, StringComparison.OrdinalIgnoreCase));
            if (match.Value != null) return match.Value;

            // 3. Contains — kod z dokumentu może zawierać część kodu z TW lub odwrotnie
            match = _produktImagesWF.FirstOrDefault(kv =>
                kodOriginal.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains(kodOriginal, StringComparison.OrdinalIgnoreCase) ||
                displayName.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains(displayName, StringComparison.OrdinalIgnoreCase));
            if (match.Value != null) return match.Value;

            // 4. Porównanie po bazowej części kodu (bez prefiksów M-, S- i sufiksów wagowych)
            string NormalizeKod(string k)
            {
                if (string.IsNullOrEmpty(k)) return "";
                k = k.Trim();
                // Usuń prefix M- / S- / m- / s-
                if (k.Length > 2 && (k.StartsWith("M-", StringComparison.OrdinalIgnoreCase) ||
                                     k.StartsWith("S-", StringComparison.OrdinalIgnoreCase)))
                    k = k.Substring(2);
                // Usuń sufiksy wagowe (-600g, -1kg, itp.)
                int dashIdx = k.LastIndexOf('-');
                if (dashIdx > 0)
                {
                    string suffix = k.Substring(dashIdx + 1).ToLower();
                    if (suffix.EndsWith("g") || suffix.EndsWith("kg"))
                        k = k.Substring(0, dashIdx);
                }
                return k;
            }

            string normOrig = NormalizeKod(kodOriginal);
            string normDisp = NormalizeKod(displayName);

            if (!string.IsNullOrEmpty(normOrig) || !string.IsNullOrEmpty(normDisp))
            {
                match = _produktImagesWF.FirstOrDefault(kv =>
                {
                    string normKey = NormalizeKod(kv.Key);
                    if (string.IsNullOrEmpty(normKey)) return false;
                    return normKey.Equals(normOrig, StringComparison.OrdinalIgnoreCase) ||
                           normKey.Equals(normDisp, StringComparison.OrdinalIgnoreCase) ||
                           normKey.StartsWith(normOrig, StringComparison.OrdinalIgnoreCase) ||
                           normOrig.StartsWith(normKey, StringComparison.OrdinalIgnoreCase) ||
                           normKey.StartsWith(normDisp, StringComparison.OrdinalIgnoreCase) ||
                           normDisp.StartsWith(normKey, StringComparison.OrdinalIgnoreCase);
                });
                if (match.Value != null) return match.Value;
            }

            return null;
        }

        private void DgvDzienne_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dgv = (DataGridView)sender;
            if (!dgv.Columns.Contains("Pozycje")) return;
            if (e.ColumnIndex != dgv.Columns["Pozycje"].Index) return;
            if (_dziennePozycjeData == null || e.RowIndex >= _dziennePozycjeData.Count) return;

            var pozycje = _dziennePozycjeData[e.RowIndex];
            if (pozycje == null || pozycje.Count == 0) return;

            e.PaintBackground(e.CellBounds, true);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const int imgSize = 14;
            const int lineH = 18;
            int padLeft = e.CellBounds.X + 2;
            int maxX = e.CellBounds.Right - 2;

            // Oblicz ile linii potrzeba (pre-pass)
            using var font = new Font("Segoe UI", 7.5F);
            int nLines = 1;
            int tempX = padLeft;
            foreach (var poz in pozycje)
            {
                string txt = $"{(poz.IsWydanie ? "↑" : "↓")} {poz.DisplayName}: {poz.Qty:N0} kg";
                bool hasImg = FindProductImage(poz.KodOriginal, poz.DisplayName) != null;
                int w = (hasImg ? imgSize + 2 : 0) + (int)g.MeasureString(txt, font).Width + 6;
                if (tempX + w > maxX && tempX > padLeft) { nLines++; tempX = padLeft; }
                tempX += w;
            }

            // Wycentruj zawartość pionowo w komórce
            int totalContentH = nLines * lineH;
            int startY = e.CellBounds.Y + Math.Max(1, (e.CellBounds.Height - totalContentH) / 2);

            int x = padLeft;
            int y = startY;

            using var brushW = new SolidBrush(Color.FromArgb(40, 167, 69));
            using var brushP = new SolidBrush(Color.FromArgb(220, 53, 69));

            foreach (var poz in pozycje)
            {
                string arrow = poz.IsWydanie ? "↑" : "↓";
                string text = $"{arrow} {poz.DisplayName}: {poz.Qty:N0} kg";
                var brush = poz.IsWydanie ? brushW : brushP;

                var productImg = FindProductImage(poz.KodOriginal, poz.DisplayName);
                int itemImgW = productImg != null ? imgSize + 2 : 0;
                var textSize = g.MeasureString(text, font);
                int itemW = itemImgW + (int)textSize.Width + 6;

                if (x + itemW > maxX && x > padLeft) { x = padLeft; y += lineH; }

                if (productImg != null)
                {
                    g.DrawImage(productImg, x, y + (lineH - imgSize) / 2, imgSize, imgSize);
                    x += imgSize + 2;
                }

                g.DrawString(text, font, brush, x, y + (lineH - textSize.Height) / 2f);
                x += (int)textSize.Width + 6;
            }

            e.Handled = true;
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
            if (e.RowIndex < 0) return;

            string colName = dgvStanMagazynu.Columns.Contains("Produkt") ? "Produkt" : "Kod";
            string kod = dgvStanMagazynu.Rows[e.RowIndex].Cells[colName]?.Value?.ToString() ?? "";

            if (kod == "SUMA" || e.RowIndex == 0)
            {
                // Klik na SUMA = wróć do widoku ogólnego
                _stanChartFilteredProduct = null;
                if (lblStanChartTitle != null)
                    lblStanChartTitle.Text = "📊 STAN MROŹNI - OSTATNIE 30 DNI";
                LoadStanMagazynuChart(dtpStanMagazynu?.Value.Date ?? DateTime.Today);
            }
            else if (!string.IsNullOrEmpty(kod) && kod != _stanChartFilteredProduct)
            {
                _stanChartFilteredProduct = kod;
                if (lblStanChartTitle != null)
                    lblStanChartTitle.Text = $"📊 {kod}";
                LoadStanMagazynuChartForProduct(kod, dtpStanMagazynu?.Value.Date ?? DateTime.Today);
            }
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

        #region Przesunięcia między mroźniami

        private string GetPrzesunieciaFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "OfertaHandlowa");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "przesunięcia_mrozni.json");
        }

        private List<PrzesuniecieMrozni> WczytajPrzesunięcia()
        {
            try
            {
                string path = GetPrzesunieciaFilePath();
                if (!File.Exists(path)) return new List<PrzesuniecieMrozni>();
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<PrzesuniecieMrozni>>(json) ?? new List<PrzesuniecieMrozni>();
            }
            catch
            {
                return new List<PrzesuniecieMrozni>();
            }
        }

        private void ZapiszPrzesunięcia(List<PrzesuniecieMrozni> lista)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(lista, options);
            File.WriteAllText(GetPrzesunieciaFilePath(), json);
        }

        private void LoadPrzesunieciaDoTabeli()
        {
            if (dgvPrzesunięcia == null) return;
            _listaPrzesuniec = WczytajPrzesunięcia();

            DataTable dt = new DataTable();
            dt.Columns.Add("Id", typeof(string));
            dt.Columns.Add("Data zlecenia", typeof(string));
            dt.Columns.Add("Źródło", typeof(string));
            dt.Columns.Add("Cel", typeof(string));
            dt.Columns.Add("Produkty", typeof(string));
            dt.Columns.Add("Łącznie kg", typeof(decimal));
            dt.Columns.Add("Zlecający", typeof(string));
            dt.Columns.Add("Status", typeof(string));

            foreach (var p in _listaPrzesuniec.OrderByDescending(x => x.DataZlecenia))
            {
                string produkty = "";
                if (p.Pozycje.Count > 0)
                {
                    produkty = p.Pozycje[0].Nazwa;
                    if (p.Pozycje.Count > 1)
                        produkty += $" +{p.Pozycje.Count - 1}";
                }
                decimal suma = p.Pozycje.Sum(x => x.Ilosc);
                dt.Rows.Add(p.Id, p.DataZlecenia.ToString("dd.MM.yyyy HH:mm"), p.MrozniaZrodloNazwa, p.MrozniaCelNazwa, produkty, suma, p.Zlecajacy, p.Status);
            }

            dgvPrzesunięcia.DataSource = dt;
            if (dgvPrzesunięcia.Columns.Contains("Id"))
                dgvPrzesunięcia.Columns["Id"].Visible = false;
            if (dgvPrzesunięcia.Columns.Contains("Łącznie kg"))
                dgvPrzesunięcia.Columns["Łącznie kg"].DefaultCellStyle.Format = "#,##0.0' kg'";
        }

        private void BtnDodajPrzesuniecieClick(object sender, EventArgs e)
        {
            var mroznie = WczytajMroznieZewnetrzne();
            if (mroznie.Count < 2)
            {
                MessageBox.Show("Potrzebujesz co najmniej 2 mroźnie zewnętrzne aby utworzyć przesunięcie.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new PrzesuniecieDialog(mroznie, connectionString))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var przesun = new PrzesuniecieMrozni
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        DataZlecenia = DateTime.Now,
                        Status = "Nowe",
                        MrozniaZrodloId = dlg.MrozniaZrodloId,
                        MrozniaZrodloNazwa = dlg.MrozniaZrodloNazwa,
                        MrozniaCelId = dlg.MrozniaCelId,
                        MrozniaCelNazwa = dlg.MrozniaCelNazwa,
                        Zlecajacy = App.UserFullName ?? "Nieznany",
                        Uwagi = dlg.UwagiText,
                        Pozycje = dlg.Pozycje
                    };

                    _listaPrzesuniec.Add(przesun);
                    ZapiszPrzesunięcia(_listaPrzesuniec);
                    LoadPrzesunieciaDoTabeli();
                    ShowToast("Utworzono nowe zlecenie przesunięcia", SuccessColor);
                }
            }
        }

        private void BtnRealizujPrzesuniecieClick(object sender, EventArgs e)
        {
            if (dgvPrzesunięcia.CurrentRow == null) return;
            string id = dgvPrzesunięcia.CurrentRow.Cells["Id"]?.Value?.ToString() ?? "";
            var przesun = _listaPrzesuniec.FirstOrDefault(p => p.Id == id);
            if (przesun == null || przesun.Status != "Nowe") return;

            if (MessageBox.Show($"Czy na pewno zrealizować przesunięcie?\n\n{przesun.MrozniaZrodloNazwa} → {przesun.MrozniaCelNazwa}\nPozycji: {przesun.Pozycje.Count}, łącznie {przesun.Pozycje.Sum(p => p.Ilosc):N1} kg",
                "Potwierdzenie realizacji", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            przesun.Status = "Zrealizowane";
            przesun.DataRealizacji = DateTime.Now;

            // Dodaj wydania/przyjęcia w mroźniach zewnętrznych
            var mroznie = WczytajMroznieZewnetrzne();
            var zrodlo = mroznie.FirstOrDefault(m => m.Id == przesun.MrozniaZrodloId);
            var cel = mroznie.FirstOrDefault(m => m.Id == przesun.MrozniaCelId);

            foreach (var poz in przesun.Pozycje)
            {
                if (zrodlo != null)
                {
                    if (zrodlo.Wydania == null) zrodlo.Wydania = new List<WydanieZewnetrzne>();
                    zrodlo.Wydania.Add(new WydanieZewnetrzne
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Data = DateTime.Now,
                        Typ = "Wydanie",
                        KodProduktu = poz.KodProduktu,
                        Produkt = poz.Nazwa,
                        Ilosc = poz.Ilosc,
                        Uwagi = $"Przesunięcie → {przesun.MrozniaCelNazwa}",
                        ZrodloNazwa = przesun.MrozniaZrodloNazwa,
                        CelNazwa = przesun.MrozniaCelNazwa
                    });
                }

                if (cel != null)
                {
                    if (cel.Wydania == null) cel.Wydania = new List<WydanieZewnetrzne>();
                    cel.Wydania.Add(new WydanieZewnetrzne
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Data = DateTime.Now,
                        Typ = "Przyjęcie",
                        KodProduktu = poz.KodProduktu,
                        Produkt = poz.Nazwa,
                        Ilosc = poz.Ilosc,
                        Uwagi = $"Przesunięcie ← {przesun.MrozniaZrodloNazwa}",
                        ZrodloNazwa = przesun.MrozniaZrodloNazwa,
                        CelNazwa = przesun.MrozniaCelNazwa
                    });
                }
            }

            ZapiszMroznieZewnetrzne(mroznie);
            ZapiszPrzesunięcia(_listaPrzesuniec);
            LoadPrzesunieciaDoTabeli();
            LoadMroznieZewnetrzneDoTabeli();
            ShowToast("Przesunięcie zrealizowane — wydania i przyjęcia utworzone", SuccessColor);
        }

        private void BtnAnulujPrzesuniecieClick(object sender, EventArgs e)
        {
            if (dgvPrzesunięcia.CurrentRow == null) return;
            string id = dgvPrzesunięcia.CurrentRow.Cells["Id"]?.Value?.ToString() ?? "";
            var przesun = _listaPrzesuniec.FirstOrDefault(p => p.Id == id);
            if (przesun == null || przesun.Status == "Zrealizowane" || przesun.Status == "Anulowane") return;

            if (MessageBox.Show("Czy na pewno anulować to zlecenie przesunięcia?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            przesun.Status = "Anulowane";
            ZapiszPrzesunięcia(_listaPrzesuniec);
            LoadPrzesunieciaDoTabeli();
            ShowToast("Zlecenie przesunięcia anulowane", WarningColor);
        }

        private void DgvPrzesunieciaSelectionChanged(object sender, EventArgs e)
        {
            try
            {
            if (dgvPrzesunięcia == null || dgvPrzesunięcia.CurrentRow == null || panelSzczegolyPrzesun == null) return;
            if (!dgvPrzesunięcia.Columns.Contains("Id")) return;
            string id = dgvPrzesunięcia.CurrentRow.Cells["Id"]?.Value?.ToString() ?? "";
            var przesun = _listaPrzesuniec.FirstOrDefault(p => p.Id == id);
            if (przesun == null)
            {
                panelSzczegolyPrzesun.Visible = false;
                return;
            }

            panelSzczegolyPrzesun.Visible = true;
            lblPrzesunZrodlo.Text = przesun.MrozniaZrodloNazwa;
            lblPrzesunCel.Text = przesun.MrozniaCelNazwa;
            lblPrzesunUwagi.Text = string.IsNullOrEmpty(przesun.Uwagi) ? "(brak)" : przesun.Uwagi;
            lblPrzesunStatus.Text = przesun.Status;
            lblPrzesunStatus.ForeColor = przesun.Status switch
            {
                "Nowe" => PrimaryColor,
                "Zrealizowane" => SuccessColor,
                "Anulowane" => SecondaryTextColor,
                _ => TextColor
            };

            // Pozycje
            var sb = new StringBuilder();
            foreach (var poz in przesun.Pozycje)
                sb.AppendLine($"  • {poz.Nazwa}    {poz.Ilosc:N1} kg");
            lblPrzesunPozycje.Text = sb.ToString().TrimEnd();

            // Buttons visibility
            bool canRealize = przesun.Status == "Nowe";
            bool canCancel = przesun.Status == "Nowe";
            btnRealizujPrzesun.Visible = canRealize;
            btnAnulujPrzesun.Visible = canCancel;
            }
            catch { }
        }

        private void DgvPrzesunięcia_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            try
            {
            var dgv = (DataGridView)sender;

            // Status badge coloring
            if (dgv.Columns[e.ColumnIndex].HeaderText == "Status" && e.Value != null)
            {
                e.PaintBackground(e.ClipBounds, true);
                string status = e.Value.ToString();
                Color badgeColor = status switch
                {
                    "Nowe" => Color.FromArgb(0, 120, 212),
                    "Zrealizowane" => Color.FromArgb(16, 124, 16),
                    "Anulowane" => Color.FromArgb(150, 150, 150),
                    _ => Color.FromArgb(100, 100, 100)
                };

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var font = new Font("Segoe UI", 8F, FontStyle.Bold))
                {
                    var textSize = g.MeasureString(status, font);
                    int bw = (int)textSize.Width + 16;
                    int bh = 22;
                    int bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2;
                    int by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;

                    using (var brush = new SolidBrush(Color.FromArgb(30, badgeColor)))
                    using (var path = CreateRoundedRect(bx, by, bw, bh, 4))
                        g.FillPath(brush, path);

                    using (var textBrush = new SolidBrush(badgeColor))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(status, font, textBrush, new RectangleF(bx, by, bw, bh), sf);
                    }
                }
                e.Handled = true;
                return;
            }

            // Left border coloring by status — first visible column
            int firstVisibleCol = -1;
            for (int i = 0; i < dgv.Columns.Count; i++)
            {
                if (dgv.Columns[i].Visible && dgv.Columns[i].Displayed) { firstVisibleCol = i; break; }
            }
            if (e.ColumnIndex == firstVisibleCol && e.RowIndex >= 0 && dgv.Columns.Contains("Status"))
            {
                e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
                string status = dgv.Rows[e.RowIndex].Cells["Status"]?.Value?.ToString() ?? "";
                Color borderColor = status switch
                {
                    "Nowe" => Color.FromArgb(0, 120, 212),
                    "Zrealizowane" => Color.FromArgb(16, 124, 16),
                    "Anulowane" => Color.FromArgb(150, 150, 150),
                    _ => Color.Transparent
                };

                if (borderColor != Color.Transparent)
                {
                    using (var pen = new Pen(borderColor, 4))
                        e.Graphics.DrawLine(pen, e.CellBounds.Left + 1, e.CellBounds.Top, e.CellBounds.Left + 1, e.CellBounds.Bottom - 1);
                }
                e.Handled = true;
            }
            }
            catch { }
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
            dgvMroznieZewnetrzne.Columns["Stan (kg)"].DefaultCellStyle.Format = "#,##0' kg'";

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

            // Dodaj kolumny dla każdej mroźni (z sufiksem kg), unikaj duplikatów
            var nazwyMrozni = mroznie.Select(m => m.Nazwa).OrderBy(n => n).ToList();
            foreach (var nazwa in nazwyMrozni)
            {
                string colName = nazwa + " (kg)";
                if (!dt.Columns.Contains(colName))
                    dt.Columns.Add(colName, typeof(decimal));
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
                            col.DefaultCellStyle.Format = "#,##0' kg'";
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
                if (lblWydaniaHeaderNazwa != null)
                    lblWydaniaHeaderNazwa.Text = "STAN I WYDANIA MROŹNI";
                return;
            }

            string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            var mroznie = WczytajMroznieZewnetrzne();
            var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
            if (mroznia == null) return;

            // Aktualizuj nagłówek z nazwą wybranej mroźni
            if (lblWydaniaHeaderNazwa != null)
                lblWydaniaHeaderNazwa.Text = $"STAN I WYDANIA MROŹNI: {mroznia.Nazwa.ToUpper()}";

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
                            col.DefaultCellStyle.Format = "#,##0' kg'";
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

        private void BtnResetMroznieZewnetrzne_Click(object sender, EventArgs e)
        {
            var mroznie = WczytajMroznieZewnetrzne();
            if (mroznie.Count == 0)
            {
                MessageBox.Show("Brak mroźni zewnętrznych do wyczyszczenia.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int totalWydania = mroznie.Sum(m => m.Wydania?.Count ?? 0);

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć WSZYSTKIE obroty magazynowe mroźni zewnętrznych?\n\n" +
                $"Mroźni: {mroznie.Count}\n" +
                $"Łączna liczba wpisów (wydania/przyjęcia): {totalWydania}\n\n" +
                "Uwaga: Struktura mroźni (nazwy, adresy, kontakty) zostanie zachowana.\n" +
                "Usunięte zostaną tylko obroty i stany magazynowe.\n\n" +
                "Tej operacji NIE MOŻNA cofnąć!",
                "Potwierdź wyczyszczenie danych",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // Wyczyść wydania we wszystkich mroźniach
                foreach (var m in mroznie)
                {
                    m.Wydania = new List<WydanieZewnetrzne>();
                }
                ZapiszMroznieZewnetrzne(mroznie);
                LoadMroznieZewnetrzneDoTabeli();

                statusLabel.Text = $"Wyczyszczono {totalWydania} wpisów z {mroznie.Count} mroźni zewnętrznych";
                ShowToast($"Wyczyszczono dane: {totalWydania} wpisów", DangerColor);

                // Odśwież stan magazynu (może zawierać dane z mroźni zewnętrznych)
                BtnStanMagazynu_Click(null, null);
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

        private void BtnInwentaryzacja_Click(object sender, EventArgs e)
        {
            if (dgvMroznieZewnetrzne.SelectedRows.Count == 0)
            {
                MessageBox.Show("Najpierw wybierz mroźnię z listy.", "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string id = dgvMroznieZewnetrzne.SelectedRows[0].Cells["ID"].Value?.ToString();
            string nazwa = dgvMroznieZewnetrzne.SelectedRows[0].Cells["Nazwa"].Value?.ToString();

            var mroznie = WczytajMroznieZewnetrzne();
            var mroznia = mroznie.FirstOrDefault(m => m.Id == id);
            if (mroznia == null) return;

            using (var dlg = new InwentaryzacjaDialog(mroznia, connectionString))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Korekty.Count > 0)
                {
                    if (mroznia.Wydania == null)
                        mroznia.Wydania = new List<WydanieZewnetrzne>();

                    foreach (var korekta in dlg.Korekty)
                    {
                        mroznia.Wydania.Add(korekta);
                    }

                    ZapiszMroznieZewnetrzne(mroznie);
                    LoadMroznieZewnetrzneDoTabeli();
                    DgvMroznieZewnetrzne_SelectionChanged(null, null);

                    statusLabel.Text = $"Inwentaryzacja: {dlg.Korekty.Count} korekt ({nazwa})";
                    ShowToast($"Inwentaryzacja zapisana: {dlg.Korekty.Count} korekt ({nazwa})", WarningColor);
                }
            }
        }

        #endregion

        #region === ZAMÓWIENIA MROŻONKI ===

        // === AVATARY HANDLOWCÓW ===

        private static readonly Color[] _avatarPalette = new Color[]
        {
            Color.FromArgb(0, 150, 136),   // teal
            Color.FromArgb(33, 150, 243),  // blue
            Color.FromArgb(156, 39, 176),  // purple
            Color.FromArgb(233, 30, 99),   // pink
            Color.FromArgb(255, 152, 0),   // orange
            Color.FromArgb(76, 175, 80),   // green
            Color.FromArgb(121, 85, 72),   // brown
            Color.FromArgb(63, 81, 181),   // indigo
            Color.FromArgb(0, 188, 212),   // cyan
            Color.FromArgb(244, 67, 54),   // red
            Color.FromArgb(139, 195, 74),  // light green
            Color.FromArgb(255, 87, 34)    // deep orange
        };

        private Color GetHandlowiecColor(string handlowiec)
        {
            if (string.IsNullOrEmpty(handlowiec) || handlowiec == "(Brak)") return Color.FromArgb(158, 158, 158);
            if (_handlowiecColors.TryGetValue(handlowiec, out var c)) return c;
            int idx = Math.Abs(handlowiec.GetHashCode()) % _avatarPalette.Length;
            _handlowiecColors[handlowiec] = _avatarPalette[idx];
            return _avatarPalette[idx];
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "(Brak)") return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
        }

        private async Task EnsureHandlowiecMappingLoadedAsync()
        {
            if (_handlowiecMapowanie != null) return;
            _handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cnLib = new SqlConnection(_connLibra);
                await cnLib.OpenAsync();
                using var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cnLib);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlName = reader.GetString(0);
                    var userId = reader.GetString(1);
                    _handlowiecMapowanie[handlName] = userId;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Mroznia] Błąd ładowania mapowania handlowców: {ex.Message}");
            }
        }

        private Image GetHandlowiecAvatarImage(string handlowiecName, int size)
        {
            if (string.IsNullOrWhiteSpace(handlowiecName) || handlowiecName == "(Brak)")
                return UserAvatarManager.GenerateDefaultAvatar("?", "unknown", size);

            var cacheKey = $"{handlowiecName}_{size}";
            if (_handlowiecAvatarCache.TryGetValue(cacheKey, out var cached))
                return cached;

            Image avatar = null;
            string userId = handlowiecName;

            if (_handlowiecMapowanie != null && _handlowiecMapowanie.TryGetValue(handlowiecName, out var uid))
                userId = uid;

            try
            {
                if (UserAvatarManager.HasAvatar(userId))
                    avatar = UserAvatarManager.GetAvatarRounded(userId, size);
            }
            catch { }

            if (avatar == null)
                avatar = UserAvatarManager.GenerateDefaultAvatar(handlowiecName, userId, size);

            _handlowiecAvatarCache[cacheKey] = avatar;
            return avatar;
        }

        private void DgvZamMrozOrders_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Helper: get status for current row
            string GetRowStatus()
            {
                try { return dgvZamMrozOrders.Rows[e.RowIndex].Cells["Status"].Value?.ToString() ?? ""; } catch { return ""; }
            }

            // === Kolumna "Handlowiec" — avatar photo + tekst ===
            if (dgvZamMrozOrders.Columns.Contains("Handlowiec") && e.ColumnIndex == dgvZamMrozOrders.Columns["Handlowiec"].Index)
            {
                e.PaintBackground(e.CellBounds, true);

                string handlowiec = e.Value?.ToString() ?? "";
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int avatarSize = 36;
                int cx = e.CellBounds.X + 6;
                int cy = e.CellBounds.Y + (e.CellBounds.Height - avatarSize) / 2;

                var avatarImg = GetHandlowiecAvatarImage(handlowiec, avatarSize);
                if (avatarImg != null)
                {
                    g.DrawImage(avatarImg, cx, cy, avatarSize, avatarSize);
                }

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "(Brak)")
                {
                    using (var font = new Font("Segoe UI", 9F))
                    using (var brush = new SolidBrush(TextColor))
                    {
                        var textRect = new RectangleF(cx + avatarSize + 8, e.CellBounds.Y, e.CellBounds.Width - avatarSize - 20, e.CellBounds.Height);
                        var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        g.DrawString(handlowiec, font, brush, textRect, sf);
                    }
                }

                e.Handled = true;
                return;
            }

            // === Kolumna "Status" — kolorowy badge ===
            if (dgvZamMrozOrders.Columns.Contains("Status") && e.ColumnIndex == dgvZamMrozOrders.Columns["Status"].Index)
            {
                e.PaintBackground(e.CellBounds, true);
                string status = e.Value?.ToString() ?? "";

                if (!string.IsNullOrEmpty(status))
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    Color badgeBg, badgeFg;
                    if (status == "Wydany") { badgeBg = Color.FromArgb(220, 245, 220); badgeFg = SuccessColor; }
                    else if (status == "Nowe") { badgeBg = Color.FromArgb(220, 235, 255); badgeFg = PrimaryColor; }
                    else if (status == "W realizacji") { badgeBg = Color.FromArgb(255, 240, 220); badgeFg = WarningColor; }
                    else { badgeBg = Color.FromArgb(240, 240, 240); badgeFg = SecondaryTextColor; }

                    using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                    {
                        var textSize = g.MeasureString(status, font);
                        int bw = (int)textSize.Width + 14;
                        int bh = 22;
                        int bx = e.CellBounds.X + (e.CellBounds.Width - bw) / 2;
                        int by = e.CellBounds.Y + (e.CellBounds.Height - bh) / 2;

                        using (var path = CreateRoundedRect(bx, by, bw, bh, 4))
                        using (var brush = new SolidBrush(badgeBg))
                            g.FillPath(brush, path);

                        using (var brush = new SolidBrush(badgeFg))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(status, font, brush, new RectangleF(bx, by, bw, bh), sf);
                        }
                    }
                }

                e.Handled = true;
                return;
            }

            // === Pierwsza widoczna kolumna — left border wg statusu ===
            int firstVisibleCol = -1;
            for (int i = 0; i < dgvZamMrozOrders.Columns.Count; i++)
            {
                if (dgvZamMrozOrders.Columns[i].Visible) { firstVisibleCol = i; break; }
            }

            if (e.ColumnIndex == firstVisibleCol)
            {
                e.PaintBackground(e.CellBounds, true);
                e.PaintContent(e.CellBounds);

                string status = GetRowStatus();
                Color borderColor = status == "Wydany" ? SuccessColor :
                                    status == "Nowe" ? PrimaryColor :
                                    status == "W realizacji" ? WarningColor : Color.Gray;

                using (var brush = new SolidBrush(borderColor))
                    e.Graphics.FillRectangle(brush, e.CellBounds.X, e.CellBounds.Y, 4, e.CellBounds.Height);

                e.Handled = true;
            }
        }

        // === DANE ===

        private async Task LoadZamowieniaMrozonkiAsync()
        {
            try
            {
                statusLabel.Text = "Ładowanie zamówień mrożonek...";
                await EnsureHandlowiecMappingLoadedAsync();
                var dateFrom = dtpZamMrozOd.Value.Date;
                var dateTo = dateFrom.AddDays(21);

                // 1. Pobierz zamówienia z LibraNet (zakres 3 tygodnie)
                var orders = new List<(int Id, int KlientId, string Uwagi, string Status, decimal TotalIlosc,
                    long? TransportKursId, bool WlasnyTransport, DateTime? DataPrzyjazdu, DateTime DataUboju)>();
                var orderTowarMap = new Dictionary<int, List<(int TowarId, decimal Ilosc)>>();

                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    // Zamówienia w zakresie dat
                    var cmdOrders = new SqlCommand(@"
                        SELECT z.Id, z.KlientId, ISNULL(z.Uwagi,'') AS Uwagi, ISNULL(z.Status,'Nowe') AS Status,
                               (SELECT ISNULL(SUM(ISNULL(t.Ilosc,0)),0) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id) AS TotalIlosc,
                               z.TransportKursID,
                               CAST(CASE WHEN z.TransportStatus = 'Wlasny' THEN 1 ELSE 0 END AS BIT) AS WlasnyTransport,
                               z.DataPrzyjazdu,
                               z.DataUboju
                        FROM dbo.ZamowieniaMieso z
                        WHERE z.DataUboju >= @DFrom AND z.DataUboju < @DTo
                              AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')", cn);
                    cmdOrders.Parameters.AddWithValue("@DFrom", dateFrom);
                    cmdOrders.Parameters.AddWithValue("@DTo", dateTo);

                    using (var rdr = await cmdOrders.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            orders.Add((
                                rdr.GetInt32(0), rdr.GetInt32(1), rdr.GetString(2), rdr.GetString(3),
                                rdr.IsDBNull(4) ? 0m : rdr.GetDecimal(4),
                                rdr.IsDBNull(5) ? null : rdr.GetInt64(5),
                                rdr.IsDBNull(6) ? false : rdr.GetBoolean(6),
                                rdr.IsDBNull(7) ? null : rdr.GetDateTime(7),
                                rdr.GetDateTime(8)
                            ));
                        }
                    }

                    if (orders.Count == 0)
                    {
                        _zamowieniaMrozone = new List<ZamowienieMrozoneInfo>();
                        PopulateZamMrozOrdersGrid();
                        statusLabel.Text = $"Brak zamówień w okresie {dateFrom:dd.MM} - {dateTo.AddDays(-1):dd.MM.yyyy}";
                        return;
                    }

                    // 2. Pobierz pozycje towarowe
                    var orderIds = string.Join(",", orders.Select(o => o.Id));
                    var cmdTowary = new SqlCommand($@"
                        SELECT ZamowienieId, KodTowaru, ISNULL(Ilosc,0) AS Ilosc
                        FROM dbo.ZamowieniaMiesoTowar
                        WHERE ZamowienieId IN ({orderIds})", cn);

                    using (var rdr = await cmdTowary.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            int zamId = rdr.GetInt32(0);
                            int towarId = rdr.GetInt32(1);
                            decimal ilosc = rdr.GetDecimal(2);
                            if (!orderTowarMap.ContainsKey(zamId))
                                orderTowarMap[zamId] = new List<(int, decimal)>();
                            orderTowarMap[zamId].Add((towarId, ilosc));
                        }
                    }
                }

                // 3. Sprawdź które towary są mrożone (katalog=67153) w Handel
                var allTowarIds = orderTowarMap.Values.SelectMany(v => v.Select(x => x.TowarId)).Distinct().ToList();
                if (allTowarIds.Count > 0)
                {
                    _frozenProductIds = await GetFrozenProductIdsAsync(allTowarIds);
                    _frozenProductNames = await GetProductNamesAsync(allTowarIds.Where(id => _frozenProductIds.Contains(id)).ToList());
                }
                else
                {
                    _frozenProductIds = new HashSet<int>();
                    _frozenProductNames = new Dictionary<int, string>();
                }

                // 4. Filtruj do zamówień z ≥1 mrożonym produktem
                var filteredOrders = new List<ZamowienieMrozoneInfo>();
                foreach (var o in orders)
                {
                    if (!orderTowarMap.ContainsKey(o.Id)) continue;
                    var frozenItems = orderTowarMap[o.Id].Where(t => _frozenProductIds.Contains(t.TowarId)).ToList();
                    if (frozenItems.Count == 0) continue;

                    decimal totalFrozen = frozenItems.Sum(f => f.Ilosc);
                    filteredOrders.Add(new ZamowienieMrozoneInfo
                    {
                        Id = o.Id,
                        KlientId = o.KlientId,
                        Status = o.Status,
                        Uwagi = o.Uwagi,
                        TotalIloscMrozone = totalFrozen,
                        TransportKursId = o.TransportKursId,
                        WlasnyTransport = o.WlasnyTransport,
                        DataPrzyjazdu = o.DataPrzyjazdu,
                        DataUboju = o.DataUboju
                    });
                }

                // 5. Załaduj transport
                if (filteredOrders.Any(o => o.TransportKursId.HasValue))
                    await LoadZamMrozTransportAsync(filteredOrders);

                // 6. Załaduj nazwy kontrahentów
                var clientIds = filteredOrders.Select(o => o.KlientId).Distinct().ToList();
                if (clientIds.Count > 0)
                    await LoadZamMrozContractorsAsync(filteredOrders, clientIds);

                _zamowieniaMrozone = filteredOrders;
                PopulateZamMrozOrdersGrid();

                var distinctDays = filteredOrders.Select(o => o.DataUboju.Date).Distinct().Count();
                lblZamMrozDni.Text = distinctDays.ToString();
                statusLabel.Text = $"Załadowano {filteredOrders.Count} zamówień z mrożonkami ({dateFrom:dd.MM} - {dateTo.AddDays(-1):dd.MM.yyyy})";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Błąd ładowania zamówień mrożonek: {ex.Message}";
            }
        }

        private async Task<HashSet<int>> GetFrozenProductIdsAsync(List<int> towarIds)
        {
            var result = new HashSet<int>();
            if (towarIds.Count == 0) return result;

            using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();

            // Batch po 500
            for (int i = 0; i < towarIds.Count; i += 500)
            {
                var batch = towarIds.Skip(i).Take(500).ToList();
                var paramNames = new List<string>();
                var cmd = new SqlCommand();
                cmd.Connection = cn;

                for (int j = 0; j < batch.Count; j++)
                {
                    paramNames.Add($"@p{j}");
                    cmd.Parameters.AddWithValue($"@p{j}", batch[j]);
                }

                cmd.CommandText = $"SELECT ID FROM HM.TW WHERE ID IN ({string.Join(",", paramNames)}) AND katalog={KatalogMrozony}";

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    result.Add(rdr.GetInt32(0));
            }

            return result;
        }

        private async Task<Dictionary<int, string>> GetProductNamesAsync(List<int> towarIds)
        {
            var result = new Dictionary<int, string>();
            if (towarIds.Count == 0) return result;

            using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();

            for (int i = 0; i < towarIds.Count; i += 500)
            {
                var batch = towarIds.Skip(i).Take(500).ToList();
                var paramNames = new List<string>();
                var cmd = new SqlCommand();
                cmd.Connection = cn;

                for (int j = 0; j < batch.Count; j++)
                {
                    paramNames.Add($"@p{j}");
                    cmd.Parameters.AddWithValue($"@p{j}", batch[j]);
                }

                cmd.CommandText = $"SELECT ID, kod FROM HM.TW WHERE ID IN ({string.Join(",", paramNames)})";

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    result[rdr.GetInt32(0)] = rdr.GetString(1);
            }

            return result;
        }

        private async Task LoadZamMrozTransportAsync(List<ZamowienieMrozoneInfo> orders)
        {
            try
            {
                using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                // Pobierz kursy
                var kursy = new Dictionary<long, (DateTime DataKursu, TimeSpan? GodzWyjazdu, string Kierowca, string Rejestracja)>();
                var cmd = new SqlCommand(@"
                    SELECT k.KursID, k.DataKursu, k.GodzWyjazdu,
                           ISNULL(kie.Imie + ' ' + kie.Nazwisko, 'Nie przypisano') AS Kierowca,
                           ISNULL(p.Rejestracja, '') AS Rejestracja
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca kie ON k.KierowcaID = kie.KierowcaID
                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID", cn);

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        kursy[rdr.GetInt64(0)] = (
                            rdr.GetDateTime(1),
                            rdr.IsDBNull(2) ? null : rdr.GetTimeSpan(2),
                            rdr.GetString(3),
                            rdr.GetString(4)
                        );
                    }
                }

                // Pobierz mapping Ladunek → ZAM_
                var ladunekMap = new Dictionary<long, List<int>>(); // KursID → list of ZamowienieId
                var cmdLad = new SqlCommand("SELECT KursID, KodKlienta FROM dbo.Ladunek WHERE KodKlienta LIKE 'ZAM_%'", cn);
                using (var rdr = await cmdLad.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        long kursId = rdr.GetInt64(0);
                        string kodKlienta = rdr.GetString(1);
                        if (kodKlienta.StartsWith("ZAM_") && int.TryParse(kodKlienta.Substring(4), out int zamId))
                        {
                            if (!ladunekMap.ContainsKey(kursId))
                                ladunekMap[kursId] = new List<int>();
                            ladunekMap[kursId].Add(zamId);
                        }
                    }
                }

                // Przypisz transport do zamówień
                // Buduj odwrotny mapping: zamId → kursId
                var zamToKurs = new Dictionary<int, long>();
                foreach (var kv in ladunekMap)
                    foreach (var zamId in kv.Value)
                        zamToKurs[zamId] = kv.Key;

                foreach (var order in orders)
                {
                    long? kursId = order.TransportKursId;
                    if (!kursId.HasValue && zamToKurs.ContainsKey(order.Id))
                        kursId = zamToKurs[order.Id];

                    if (kursId.HasValue && kursy.ContainsKey(kursId.Value))
                    {
                        var kurs = kursy[kursId.Value];
                        order.DataKursu = kurs.DataKursu;
                        order.CzasWyjazdu = kurs.GodzWyjazdu;
                        order.Kierowca = kurs.Kierowca;
                        order.NumerRejestracyjny = kurs.Rejestracja;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadZamMrozTransport error: {ex.Message}");
            }
        }

        private async Task LoadZamMrozContractorsAsync(List<ZamowienieMrozoneInfo> orders, List<int> clientIds)
        {
            try
            {
                using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();

                var paramNames = new List<string>();
                var cmd = new SqlCommand();
                cmd.Connection = cn;

                for (int i = 0; i < clientIds.Count; i++)
                {
                    paramNames.Add($"@c{i}");
                    cmd.Parameters.AddWithValue($"@c{i}", clientIds[i]);
                }

                cmd.CommandText = $@"
                    SELECT c.Id, ISNULL(c.Shortcut, 'KH ' + CAST(c.Id AS varchar(10))) AS Nazwa,
                           ISNULL(w.CDim_Handlowiec_Val, '(Brak)') AS Handlowiec
                    FROM SSCommon.STContractors c
                    LEFT JOIN SSCommon.ContractorClassification w ON c.Id = w.ElementId
                    WHERE c.Id IN ({string.Join(",", paramNames)})";

                var contractors = new Dictionary<int, (string Nazwa, string Handlowiec)>();
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                        contractors[rdr.GetInt32(0)] = (rdr.GetString(1), rdr.GetString(2));
                }

                foreach (var order in orders)
                {
                    if (contractors.ContainsKey(order.KlientId))
                    {
                        var c = contractors[order.KlientId];
                        order.Klient = c.Nazwa;
                        order.Handlowiec = c.Handlowiec;
                    }
                    else
                    {
                        order.Klient = $"KH {order.KlientId}";
                        order.Handlowiec = "(Brak)";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadZamMrozContractors error: {ex.Message}");
            }
        }

        private static readonly string[] _polishDayShort = { "Nd", "Pn", "Wt", "Śr", "Cz", "Pt", "So" };

        private void PopulateZamMrozOrdersGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Data", typeof(string));
            dt.Columns.Add("Klient", typeof(string));
            dt.Columns.Add("Handlowiec", typeof(string));
            dt.Columns.Add("Kg mrożone", typeof(decimal));
            dt.Columns.Add("Wyjazd", typeof(string));
            dt.Columns.Add("Pojazd", typeof(string));
            dt.Columns.Add("Kierowca", typeof(string));
            dt.Columns.Add("Status", typeof(string));

            // Sort by date first, then by departure time
            var sorted = _zamowieniaMrozone
                .OrderBy(o => o.DataUboju)
                .ThenBy(o => o.CzasWyjazdu ?? TimeSpan.MaxValue)
                .ThenBy(o => o.Klient)
                .ToList();

            foreach (var o in sorted)
            {
                string dayShort = _polishDayShort[(int)o.DataUboju.DayOfWeek];
                string dateStr = $"{dayShort} {o.DataUboju:dd.MM}";

                dt.Rows.Add(
                    o.Id,
                    dateStr,
                    o.Klient ?? $"KH {o.KlientId}",
                    o.Handlowiec ?? "(Brak)",
                    Math.Round(o.TotalIloscMrozone, 1),
                    o.CzasWyjazdu.HasValue ? o.CzasWyjazdu.Value.ToString(@"hh\:mm") : "-",
                    string.IsNullOrEmpty(o.NumerRejestracyjny) ? "-" : o.NumerRejestracyjny,
                    string.IsNullOrEmpty(o.Kierowca) ? "-" : o.Kierowca,
                    o.Status
                );
            }

            dgvZamMrozOrders.DataSource = dt;

            // Ukryj kolumnę ID
            if (dgvZamMrozOrders.Columns.Contains("ID"))
                dgvZamMrozOrders.Columns["ID"].Visible = false;

            // Style kolumn
            if (dgvZamMrozOrders.Columns.Contains("Data"))
            {
                dgvZamMrozOrders.Columns["Data"].Width = 75;
                dgvZamMrozOrders.Columns["Data"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgvZamMrozOrders.Columns["Data"].DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                dgvZamMrozOrders.Columns["Data"].DefaultCellStyle.ForeColor = Color.FromArgb(80, 80, 80);
            }
            if (dgvZamMrozOrders.Columns.Contains("Klient"))
                dgvZamMrozOrders.Columns["Klient"].DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            if (dgvZamMrozOrders.Columns.Contains("Handlowiec"))
            {
                dgvZamMrozOrders.Columns["Handlowiec"].MinimumWidth = 140;
            }
            if (dgvZamMrozOrders.Columns.Contains("Kg mrożone"))
            {
                dgvZamMrozOrders.Columns["Kg mrożone"].Width = 90;
                dgvZamMrozOrders.Columns["Kg mrożone"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgvZamMrozOrders.Columns["Kg mrożone"].DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                dgvZamMrozOrders.Columns["Kg mrożone"].DefaultCellStyle.ForeColor = Color.FromArgb(0, 150, 136);
                dgvZamMrozOrders.Columns["Kg mrożone"].DefaultCellStyle.Format = "#,##0.#";
            }
            if (dgvZamMrozOrders.Columns.Contains("Wyjazd"))
            {
                dgvZamMrozOrders.Columns["Wyjazd"].Width = 65;
                dgvZamMrozOrders.Columns["Wyjazd"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }
            if (dgvZamMrozOrders.Columns.Contains("Status"))
            {
                dgvZamMrozOrders.Columns["Status"].Width = 85;
                dgvZamMrozOrders.Columns["Status"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }

            // Kolorowanie statusów per row + podświetlenie dzisiejszych
            foreach (DataGridViewRow row in dgvZamMrozOrders.Rows)
            {
                string status = row.Cells["Status"].Value?.ToString() ?? "";
                if (status == "Wydany")
                    row.DefaultCellStyle.ForeColor = SuccessColor;
                else if (status == "Nowe")
                    row.DefaultCellStyle.ForeColor = PrimaryColor;
                else if (status == "W realizacji")
                    row.DefaultCellStyle.ForeColor = WarningColor;
            }

            // Statystyki z separatorami tysięcy
            decimal sumaKg = _zamowieniaMrozone.Sum(o => o.TotalIloscMrozone);
            lblZamMrozSumaKg.Text = $"{sumaKg:#,##0.#} kg";
            lblZamMrozLiczbaZam.Text = _zamowieniaMrozone.Count.ToString("#,##0");
        }

        private void DgvZamMrozOrders_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvZamMrozOrders.SelectedRows.Count == 0 || !dgvZamMrozOrders.Columns.Contains("ID"))
            {
                return;
            }

            var row = dgvZamMrozOrders.SelectedRows[0];
            if (row.Cells["ID"].Value == null || row.Cells["ID"].Value == DBNull.Value) return;

            int zamId = (int)row.Cells["ID"].Value;
            var order = _zamowieniaMrozone.FirstOrDefault(o => o.Id == zamId);
            if (order == null) { ClearZamMrozDetails(); return; }

            lblZamMrozKlient.Text = order.Klient ?? $"KH {order.KlientId}";
            lblZamMrozHandlowiec.Text = order.Handlowiec ?? "(Brak)";
            lblZamMrozWyjazd.Text = order.CzasWyjazdu.HasValue ? order.CzasWyjazdu.Value.ToString(@"hh\:mm") : "-";
            lblZamMrozPojazd.Text = string.IsNullOrEmpty(order.NumerRejestracyjny) ? "-" : order.NumerRejestracyjny;
            lblZamMrozKierowca.Text = string.IsNullOrEmpty(order.Kierowca) ? "-" : order.Kierowca;
            lblZamMrozStatus.Text = order.Status;
            lblZamMrozUwagi.Text = string.IsNullOrEmpty(order.Uwagi) ? "-" : order.Uwagi;

            // Koloruj status
            lblZamMrozStatus.ForeColor = order.Status == "Wydany" ? SuccessColor :
                                          order.Status == "Nowe" ? PrimaryColor : TextColor;

            // Avatar handlowca (real photo)
            _zamMrozHandlowiecAvatar.Tag = order.Handlowiec ?? "(Brak)";
            _zamMrozHandlowiecAvatar.Invalidate();

            _ = LoadZamMrozPozycjeAsync(zamId);
        }

        private void ClearZamMrozDetails()
        {
            lblZamMrozKlient.Text = "-";
            lblZamMrozHandlowiec.Text = "-";
            lblZamMrozWyjazd.Text = "-";
            lblZamMrozPojazd.Text = "-";
            lblZamMrozKierowca.Text = "-";
            lblZamMrozStatus.Text = "-";
            lblZamMrozUwagi.Text = "-";
            dgvZamMrozPozycje.DataSource = null;
            _zamMrozHandlowiecAvatar.Tag = "(Brak)";
            _zamMrozHandlowiecAvatar.Invalidate();
        }

        private async Task LoadZamMrozPozycjeAsync(int zamowienieId)
        {
            try
            {
                var pozycje = new List<(int TowarId, decimal Ilosc)>();

                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var cmd = new SqlCommand(@"
                        SELECT KodTowaru, ISNULL(Ilosc,0)
                        FROM dbo.ZamowieniaMiesoTowar
                        WHERE ZamowienieId=@Id", cn);
                    cmd.Parameters.AddWithValue("@Id", zamowienieId);

                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                        pozycje.Add((rdr.GetInt32(0), rdr.GetDecimal(1)));
                }

                // Filtruj do mrożonych
                var frozenPozycje = pozycje.Where(p => _frozenProductIds.Contains(p.TowarId)).ToList();

                // Pobierz nazwy (użyj cache lub doładuj)
                var missingNames = frozenPozycje.Select(p => p.TowarId).Where(id => !_frozenProductNames.ContainsKey(id)).ToList();
                if (missingNames.Count > 0)
                {
                    var newNames = await GetProductNamesAsync(missingNames);
                    foreach (var kv in newNames)
                        _frozenProductNames[kv.Key] = kv.Value;
                }

                var dt = new DataTable();
                dt.Columns.Add("Produkt", typeof(string));
                dt.Columns.Add("Ilość (kg)", typeof(decimal));

                foreach (var p in frozenPozycje.OrderBy(p => _frozenProductNames.GetValueOrDefault(p.TowarId, $"ID:{p.TowarId}")))
                {
                    dt.Rows.Add(
                        _frozenProductNames.GetValueOrDefault(p.TowarId, $"ID:{p.TowarId}"),
                        Math.Round(p.Ilosc, 1)
                    );
                }

                dgvZamMrozPozycje.DataSource = dt;

                // Formatowanie kolumny ilości z separatorem tysięcy
                if (dgvZamMrozPozycje.Columns.Contains("Ilość (kg)"))
                {
                    dgvZamMrozPozycje.Columns["Ilość (kg)"].DefaultCellStyle.Format = "#,##0.#";
                    dgvZamMrozPozycje.Columns["Ilość (kg)"].DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    dgvZamMrozPozycje.Columns["Ilość (kg)"].DefaultCellStyle.ForeColor = Color.FromArgb(0, 150, 136);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadZamMrozPozycje error: {ex.Message}");
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
                dgvKoszyk.Rows.Add(lp++, poz.KodProduktu, poz.Nazwa, $"{poz.Ilosc:N0} kg");
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

    /// <summary>
    /// Dialog inwentaryzacji mroźni zewnętrznej - pozwala ustawić rzeczywisty stan i generuje korekty
    /// </summary>
    public class InwentaryzacjaDialog : Form
    {
        public List<WydanieZewnetrzne> Korekty { get; private set; } = new List<WydanieZewnetrzne>();

        private MrozniaZewnetrzna mroznia;
        private string connectionString;
        private DataGridView dgvInwentaryzacja;
        private DateTimePicker dtpData;
        private Button btnZapisz, btnDodajProdukt;

        private readonly Color HeaderColor = Color.FromArgb(44, 62, 80);
        private readonly Color WarningColor = Color.FromArgb(255, 140, 0);
        private readonly Color SuccessColor = Color.FromArgb(16, 124, 16);
        private readonly Color DangerColor = Color.FromArgb(232, 17, 35);

        public InwentaryzacjaDialog(MrozniaZewnetrzna mroznia, string connString)
        {
            this.mroznia = mroznia;
            this.connectionString = connString;

            this.Text = $"\U0001f4cb Inwentaryzacja - {mroznia.Nazwa}";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(240, 244, 248);
            this.Font = new Font("Segoe UI", 10F);

            BuildUI();
            LoadDane();
        }

        private void BuildUI()
        {
            // === HEADER ===
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = HeaderColor
            };

            var lblHeader = new Label
            {
                Text = $"\U0001f4cb  INWENTARYZACJA - {mroznia.Nazwa.ToUpper()}",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 10),
                AutoSize = true
            };

            var lblSubHeader = new Label
            {
                Text = "Wpisz rzeczywisty stan każdego produktu. Różnice zostaną zapisane jako korekty.",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(189, 195, 199),
                Location = new Point(20, 40),
                AutoSize = true
            };

            headerPanel.Controls.AddRange(new Control[] { lblHeader, lblSubHeader });

            // === TOOLBAR (data + przyciski) ===
            var toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.White,
                Padding = new Padding(10, 8, 10, 8)
            };

            var lblData = new Label
            {
                Text = "Data inwentaryzacji:",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F)
            };

            dtpData = new DateTimePicker
            {
                Location = new Point(170, 12),
                Width = 130,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };

            btnDodajProdukt = new Button
            {
                Text = "+ Dodaj produkt",
                Location = new Point(330, 10),
                Size = new Size(130, 30),
                BackColor = SuccessColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajProdukt.FlatAppearance.BorderSize = 0;
            btnDodajProdukt.Click += BtnDodajProdukt_Click;

            btnZapisz = new Button
            {
                Text = "\u2714 Zapisz korekty",
                Location = new Point(730, 10),
                Size = new Size(140, 30),
                BackColor = WarningColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            toolbarPanel.Controls.AddRange(new Control[] { lblData, dtpData, btnDodajProdukt, btnZapisz });

            // === DATAGRIDVIEW ===
            dgvInwentaryzacja = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F),
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderColor,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 35 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(220, 235, 252),
                    SelectionForeColor = Color.Black
                }
            };

            // Kolumny
            dgvInwentaryzacja.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KodProduktu",
                HeaderText = "Kod produktu",
                ReadOnly = true,
                FillWeight = 25
            });
            dgvInwentaryzacja.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Nazwa",
                HeaderText = "Nazwa",
                ReadOnly = true,
                FillWeight = 30
            });
            dgvInwentaryzacja.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StanObliczony",
                HeaderText = "Stan obliczony (kg)",
                ReadOnly = true,
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "#,##0.0",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            dgvInwentaryzacja.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StanRzeczywisty",
                HeaderText = "Stan rzeczywisty (kg)",
                ReadOnly = false,
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "#,##0.0",
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = Color.FromArgb(255, 255, 230)
                }
            });
            dgvInwentaryzacja.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Roznica",
                HeaderText = "Różnica (kg)",
                ReadOnly = true,
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "#,##0.0",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dgvInwentaryzacja.CellEndEdit += DgvInwentaryzacja_CellEndEdit;
            dgvInwentaryzacja.CellFormatting += DgvInwentaryzacja_CellFormatting;

            this.Controls.Add(dgvInwentaryzacja);
            this.Controls.Add(toolbarPanel);
            this.Controls.Add(headerPanel);
        }

        private void LoadDane()
        {
            // Oblicz aktualny stan per produkt z wydań/przyjęć
            var stanPerProdukt = new Dictionary<string, (string Nazwa, decimal Stan)>(StringComparer.OrdinalIgnoreCase);

            if (mroznia.Wydania != null)
            {
                foreach (var w in mroznia.Wydania)
                {
                    string kod = w.KodProduktu ?? "";
                    if (string.IsNullOrEmpty(kod)) continue;

                    if (!stanPerProdukt.ContainsKey(kod))
                        stanPerProdukt[kod] = (w.Produkt ?? kod, 0m);

                    var current = stanPerProdukt[kod];
                    if (w.Typ == "Przyjęcie")
                        stanPerProdukt[kod] = (current.Nazwa, current.Stan + w.Ilosc);
                    else if (w.Typ == "Wydanie")
                        stanPerProdukt[kod] = (current.Nazwa, current.Stan - w.Ilosc);
                }
            }

            dgvInwentaryzacja.Rows.Clear();

            // Dodaj produkty z niezerowym stanem
            foreach (var kvp in stanPerProdukt.OrderByDescending(x => x.Value.Stan))
            {
                if (kvp.Value.Stan == 0) continue;
                int idx = dgvInwentaryzacja.Rows.Add();
                var row = dgvInwentaryzacja.Rows[idx];
                row.Cells["KodProduktu"].Value = kvp.Key;
                row.Cells["Nazwa"].Value = kvp.Value.Nazwa;
                row.Cells["StanObliczony"].Value = kvp.Value.Stan;
                row.Cells["StanRzeczywisty"].Value = kvp.Value.Stan;
                row.Cells["Roznica"].Value = 0m;
            }
        }

        private void DgvInwentaryzacja_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvInwentaryzacja.Columns[e.ColumnIndex].Name != "StanRzeczywisty") return;

            var row = dgvInwentaryzacja.Rows[e.RowIndex];
            decimal stanObl = 0, stanRzecz = 0;

            if (row.Cells["StanObliczony"].Value != null)
                decimal.TryParse(row.Cells["StanObliczony"].Value.ToString(), out stanObl);
            if (row.Cells["StanRzeczywisty"].Value != null)
                decimal.TryParse(row.Cells["StanRzeczywisty"].Value.ToString(), out stanRzecz);

            row.Cells["Roznica"].Value = stanRzecz - stanObl;
        }

        private void DgvInwentaryzacja_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvInwentaryzacja.Columns[e.ColumnIndex].Name == "Roznica" && e.Value != null)
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal roznica))
                {
                    if (roznica > 0)
                        e.CellStyle.ForeColor = SuccessColor;
                    else if (roznica < 0)
                        e.CellStyle.ForeColor = DangerColor;
                    else
                        e.CellStyle.ForeColor = Color.Gray;

                    e.CellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                }
            }
        }

        private void BtnDodajProdukt_Click(object sender, EventArgs e)
        {
            // Wczytaj produkty mrożone z bazy
            var produkty = new List<ProduktMrozony>();
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                        $"SELECT Id, Kod, Nazwa FROM [HANDEL].[HM].[TW] WHERE katalog = '{Mroznia.KatalogMrozony}' ORDER BY Nazwa ASC", conn))
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            produkty.Add(new ProduktMrozony { Id = rd.GetInt32(0), Kod = rd["Kod"]?.ToString() ?? "", Nazwa = rd["Nazwa"]?.ToString() ?? "" });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania produktów: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Filtruj produkty już dodane
            var istniejaceKody = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in dgvInwentaryzacja.Rows)
            {
                var kod = row.Cells["KodProduktu"].Value?.ToString();
                if (!string.IsNullOrEmpty(kod)) istniejaceKody.Add(kod);
            }

            var dostepne = produkty.Where(p => !istniejaceKody.Contains(p.Kod)).ToList();
            if (dostepne.Count == 0)
            {
                MessageBox.Show("Wszystkie produkty są już na liście.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Prosty dialog wyboru produktu
            using (var wyborDlg = new Form())
            {
                wyborDlg.Text = "Dodaj produkt do inwentaryzacji";
                wyborDlg.Size = new Size(500, 450);
                wyborDlg.StartPosition = FormStartPosition.CenterParent;
                wyborDlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                wyborDlg.MaximizeBox = false;
                wyborDlg.MinimizeBox = false;
                wyborDlg.Font = new Font("Segoe UI", 10F);

                var txtSzukaj = new TextBox
                {
                    Dock = DockStyle.Top,
                    Height = 30,
                    PlaceholderText = "Szukaj produktu...",
                    Font = new Font("Segoe UI", 11F)
                };

                var lstProdukty = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 10F)
                };

                foreach (var p in dostepne)
                    lstProdukty.Items.Add($"{p.Kod} - {p.Nazwa}");

                txtSzukaj.TextChanged += (s2, e2) =>
                {
                    lstProdukty.Items.Clear();
                    string filtr = txtSzukaj.Text.Trim().ToLower();
                    foreach (var p in dostepne)
                    {
                        if (string.IsNullOrEmpty(filtr) || p.Kod.ToLower().Contains(filtr) || p.Nazwa.ToLower().Contains(filtr))
                            lstProdukty.Items.Add($"{p.Kod} - {p.Nazwa}");
                    }
                };

                var btnDodaj = new Button
                {
                    Text = "Dodaj",
                    Dock = DockStyle.Bottom,
                    Height = 40,
                    BackColor = Color.FromArgb(16, 124, 16),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                btnDodaj.FlatAppearance.BorderSize = 0;
                btnDodaj.Click += (s2, e2) =>
                {
                    if (lstProdukty.SelectedIndex < 0) return;
                    string wybrany = lstProdukty.SelectedItem.ToString();
                    string kodWybrany = wybrany.Split(new[] { " - " }, StringSplitOptions.None)[0];
                    var prod = dostepne.FirstOrDefault(p => p.Kod == kodWybrany);
                    if (prod != null)
                    {
                        int idx = dgvInwentaryzacja.Rows.Add();
                        var row = dgvInwentaryzacja.Rows[idx];
                        row.Cells["KodProduktu"].Value = prod.Kod;
                        row.Cells["Nazwa"].Value = prod.Nazwa;
                        row.Cells["StanObliczony"].Value = 0m;
                        row.Cells["StanRzeczywisty"].Value = 0m;
                        row.Cells["Roznica"].Value = 0m;
                        wyborDlg.DialogResult = DialogResult.OK;
                    }
                };

                lstProdukty.DoubleClick += (s2, e2) => btnDodaj.PerformClick();

                wyborDlg.Controls.Add(lstProdukty);
                wyborDlg.Controls.Add(txtSzukaj);
                wyborDlg.Controls.Add(btnDodaj);
                wyborDlg.ShowDialog();
            }
        }

        private void BtnZapisz_Click(object sender, EventArgs e)
        {
            var korekty = new List<WydanieZewnetrzne>();
            DateTime dataInw = dtpData.Value.Date;

            foreach (DataGridViewRow row in dgvInwentaryzacja.Rows)
            {
                decimal roznica = 0;
                if (row.Cells["Roznica"].Value != null)
                    decimal.TryParse(row.Cells["Roznica"].Value.ToString(), out roznica);

                if (roznica == 0) continue;

                string kod = row.Cells["KodProduktu"].Value?.ToString() ?? "";
                string nazwa = row.Cells["Nazwa"].Value?.ToString() ?? "";

                korekty.Add(new WydanieZewnetrzne
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    Data = dataInw,
                    Typ = roznica > 0 ? "Przyjęcie" : "Wydanie",
                    KodProduktu = kod,
                    Produkt = nazwa,
                    Ilosc = Math.Abs(roznica),
                    Uwagi = $"Inwentaryzacja z dnia {dataInw:dd.MM.yyyy} - korekta stanu"
                });
            }

            if (korekty.Count == 0)
            {
                MessageBox.Show("Brak różnic do zapisania. Stan rzeczywisty = stan obliczony.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Podsumowanie
            int przyjecia = korekty.Count(k => k.Typ == "Przyjęcie");
            int wydania = korekty.Count(k => k.Typ == "Wydanie");
            decimal sumaPlus = korekty.Where(k => k.Typ == "Przyjęcie").Sum(k => k.Ilosc);
            decimal sumaMinus = korekty.Where(k => k.Typ == "Wydanie").Sum(k => k.Ilosc);

            string msg = $"Podsumowanie inwentaryzacji:\n\n" +
                         $"Korekty dodatnie (przyjęcia): {przyjecia} pozycji, +{sumaPlus:N1} kg\n" +
                         $"Korekty ujemne (wydania): {wydania} pozycji, -{sumaMinus:N1} kg\n\n" +
                         $"Czy zapisać korekty?";

            if (MessageBox.Show(msg, "Potwierdzenie inwentaryzacji", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                this.Korekty = korekty;
                this.DialogResult = DialogResult.OK;
            }
        }
    }

    public class ZamowienieMrozoneInfo
    {
        public int Id { get; set; }
        public int KlientId { get; set; }
        public string Klient { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string Uwagi { get; set; } = "";
        public string Status { get; set; } = "Nowe";
        public decimal TotalIloscMrozone { get; set; }
        public DateTime DataUboju { get; set; }
        public TimeSpan? CzasWyjazdu { get; set; }
        public DateTime? DataKursu { get; set; }
        public string NumerRejestracyjny { get; set; } = "";
        public string Kierowca { get; set; } = "";
        public long? TransportKursId { get; set; }
        public bool WlasnyTransport { get; set; }
        public DateTime? DataPrzyjazdu { get; set; }
    }

    public class PozycjaMrozona
    {
        public int TowarId { get; set; }
        public string Nazwa { get; set; } = "";
        public decimal Ilosc { get; set; }
    }

    public class PrzesuniecieMrozni
    {
        public string Id { get; set; } = "";
        public DateTime DataZlecenia { get; set; }
        public DateTime? DataRealizacji { get; set; }
        public string Status { get; set; } = "Nowe";
        public string MrozniaZrodloId { get; set; } = "";
        public string MrozniaZrodloNazwa { get; set; } = "";
        public string MrozniaCelId { get; set; } = "";
        public string MrozniaCelNazwa { get; set; } = "";
        public string Zlecajacy { get; set; } = "";
        public string Uwagi { get; set; } = "";
        public List<PozycjaPrzesun> Pozycje { get; set; } = new();
    }

    public class PozycjaPrzesun
    {
        public string KodProduktu { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal Ilosc { get; set; }
    }

    public class PrzesuniecieDialog : Form
    {
        public string MrozniaZrodloId { get; private set; } = "";
        public string MrozniaZrodloNazwa { get; private set; } = "";
        public string MrozniaCelId { get; private set; } = "";
        public string MrozniaCelNazwa { get; private set; } = "";
        public string UwagiText { get; private set; } = "";
        public List<PozycjaPrzesun> Pozycje { get; private set; } = new();

        private readonly List<MrozniaZewnetrzna> _mroznie;
        private readonly string _connString;
        private ComboBox cmbZrodlo, cmbCel;
        private DataGridView dgvPozycje;
        private TextBox txtUwagi;

        private static readonly Color PurpleColor = Color.FromArgb(128, 90, 213);
        private static readonly Color LightBg = Color.FromArgb(248, 249, 250);

        public PrzesuniecieDialog(List<MrozniaZewnetrzna> mroznie, string connString)
        {
            _mroznie = mroznie;
            _connString = connString;
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Nowe zlecenie przesunięcia";
            this.Size = new Size(620, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = LightBg;
            this.Font = new Font("Segoe UI", 9.5F);

            // Header
            Panel header = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = PurpleColor };
            Label lblTitle = new Label
            {
                Text = "↔ NOWE ZLECENIE PRZESUNIĘCIA",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            header.Controls.Add(lblTitle);

            // Content panel
            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 15, 20, 15) };

            int y = 5;

            // Mroźnia źródłowa
            Label lblZrodlo = new Label { Text = "Mroźnia źródłowa:", Location = new Point(0, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            y += 22;
            cmbZrodlo = new ComboBox
            {
                Location = new Point(0, y),
                Size = new Size(360, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            foreach (var m in _mroznie) cmbZrodlo.Items.Add(m.Nazwa);
            cmbZrodlo.SelectedIndexChanged += CmbZrodlo_SelectedIndexChanged;
            y += 35;

            // Mroźnia docelowa
            Label lblCel = new Label { Text = "Mroźnia docelowa:", Location = new Point(0, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            y += 22;
            cmbCel = new ComboBox
            {
                Location = new Point(0, y),
                Size = new Size(360, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            y += 38;

            // Pozycje
            Label lblPoz = new Label { Text = "Pozycje do przesunięcia:", Location = new Point(0, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            Button btnDodajPoz = new Button
            {
                Text = "+ Dodaj pozycję",
                Location = new Point(370, y - 3),
                Size = new Size(120, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = PurpleColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDodajPoz.FlatAppearance.BorderSize = 0;
            btnDodajPoz.Click += BtnDodajPozycje_Click;
            y += 25;

            dgvPozycje = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(555, 180),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = PurpleColor,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                },
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 35,
                RowTemplate = { Height = 30 }
            };
            dgvPozycje.Columns.Add("KodProduktu", "Kod produktu");
            dgvPozycje.Columns.Add("Nazwa", "Nazwa");
            dgvPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ilosc",
                HeaderText = "Ilość (kg)",
                ValueType = typeof(decimal)
            });
            dgvPozycje.Columns["KodProduktu"].FillWeight = 30;
            dgvPozycje.Columns["Nazwa"].FillWeight = 50;
            dgvPozycje.Columns["Ilosc"].FillWeight = 20;
            y += 185;

            // Uwagi
            Label lblUwagi = new Label { Text = "Uwagi:", Location = new Point(0, y), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            y += 22;
            txtUwagi = new TextBox
            {
                Location = new Point(0, y),
                Size = new Size(555, 50),
                Multiline = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            y += 60;

            // Buttons
            Button btnOK = new Button
            {
                Text = "Utwórz zlecenie",
                Location = new Point(310, y),
                Size = new Size(130, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = PurpleColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += BtnOK_Click;

            Button btnCancel = new Button
            {
                Text = "Anuluj",
                Location = new Point(450, y),
                Size = new Size(105, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; };

            content.Controls.AddRange(new Control[]
            {
                lblZrodlo, cmbZrodlo, lblCel, cmbCel,
                lblPoz, btnDodajPoz, dgvPozycje,
                lblUwagi, txtUwagi,
                btnOK, btnCancel
            });

            this.Controls.Add(content);
            this.Controls.Add(header);
        }

        private void CmbZrodlo_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbCel.Items.Clear();
            string selectedZrodlo = cmbZrodlo.SelectedItem?.ToString() ?? "";
            foreach (var m in _mroznie)
            {
                if (m.Nazwa != selectedZrodlo)
                    cmbCel.Items.Add(m.Nazwa);
            }
            if (cmbCel.Items.Count > 0) cmbCel.SelectedIndex = 0;
        }

        private void BtnDodajPozycje_Click(object sender, EventArgs e)
        {
            // Pobierz produkty ze stanu mroźni źródłowej
            string zrodloNazwa = cmbZrodlo.SelectedItem?.ToString() ?? "";
            var mroznia = _mroznie.FirstOrDefault(m => m.Nazwa == zrodloNazwa);

            var stanProdukty = new Dictionary<string, (string Nazwa, decimal Stan)>(StringComparer.OrdinalIgnoreCase);

            if (mroznia?.Wydania != null)
            {
                foreach (var w in mroznia.Wydania)
                {
                    string kod = w.KodProduktu ?? "";
                    if (string.IsNullOrEmpty(kod)) continue;
                    if (!stanProdukty.ContainsKey(kod))
                        stanProdukty[kod] = (w.Produkt ?? kod, 0);
                    var current = stanProdukty[kod];
                    if (w.Typ == "Przyjęcie")
                        stanProdukty[kod] = (current.Nazwa, current.Stan + w.Ilosc);
                    else if (w.Typ == "Wydanie")
                        stanProdukty[kod] = (current.Nazwa, current.Stan - w.Ilosc);
                }
            }

            // Filtruj produkty z dodatnim stanem
            var dostepne = stanProdukty.Where(kvp => kvp.Value.Stan > 0).ToList();

            if (dostepne.Count == 0)
            {
                // Pozwól dodać ręcznie
                dgvPozycje.Rows.Add("", "", 0m);
                return;
            }

            // Dialog wyboru produktu
            using (var dlg = new Form())
            {
                dlg.Text = "Wybierz produkt";
                dlg.Size = new Size(450, 350);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedSingle;
                dlg.MaximizeBox = false;
                dlg.BackColor = Color.White;
                dlg.Font = new Font("Segoe UI", 9.5F);

                var lbProdukty = new ListBox
                {
                    Dock = DockStyle.Top,
                    Height = 230,
                    Font = new Font("Segoe UI", 10F)
                };
                foreach (var p in dostepne.OrderBy(x => x.Value.Nazwa))
                    lbProdukty.Items.Add($"{p.Key} | {p.Value.Nazwa} | Stan: {p.Value.Stan:N1} kg");

                if (lbProdukty.Items.Count > 0) lbProdukty.SelectedIndex = 0;

                Label lblIlosc = new Label { Text = "Ilość (kg):", Location = new Point(15, 240), AutoSize = true };
                NumericUpDown nudIlosc = new NumericUpDown
                {
                    Location = new Point(100, 238),
                    Size = new Size(120, 28),
                    DecimalPlaces = 1,
                    Minimum = 0.1m,
                    Maximum = 99999m,
                    Value = 100m
                };
                Button btnWybierz = new Button
                {
                    Text = "Dodaj",
                    Location = new Point(250, 236),
                    Size = new Size(80, 30),
                    BackColor = PurpleColor,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                };
                btnWybierz.FlatAppearance.BorderSize = 0;
                btnWybierz.Click += (s2, e2) =>
                {
                    if (lbProdukty.SelectedIndex < 0) return;
                    var selected = dostepne.OrderBy(x => x.Value.Nazwa).ElementAt(lbProdukty.SelectedIndex);
                    dgvPozycje.Rows.Add(selected.Key, selected.Value.Nazwa, nudIlosc.Value);
                    dlg.DialogResult = DialogResult.OK;
                };

                dlg.Controls.AddRange(new Control[] { lbProdukty, lblIlosc, nudIlosc, btnWybierz });
                dlg.ShowDialog(this);
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (cmbZrodlo.SelectedIndex < 0 || cmbCel.SelectedIndex < 0)
            {
                MessageBox.Show("Wybierz mroźnię źródłową i docelową.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dgvPozycje.Rows.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jedną pozycję.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string zrodloNazwa = cmbZrodlo.SelectedItem.ToString();
            string celNazwa = cmbCel.SelectedItem.ToString();

            var zrodlo = _mroznie.FirstOrDefault(m => m.Nazwa == zrodloNazwa);
            var cel = _mroznie.FirstOrDefault(m => m.Nazwa == celNazwa);

            MrozniaZrodloId = zrodlo?.Id ?? "";
            MrozniaZrodloNazwa = zrodloNazwa;
            MrozniaCelId = cel?.Id ?? "";
            MrozniaCelNazwa = celNazwa;
            UwagiText = txtUwagi.Text;

            Pozycje = new List<PozycjaPrzesun>();
            foreach (DataGridViewRow row in dgvPozycje.Rows)
            {
                string kod = row.Cells["KodProduktu"]?.Value?.ToString() ?? "";
                string nazwa = row.Cells["Nazwa"]?.Value?.ToString() ?? "";
                decimal ilosc = 0;
                if (row.Cells["Ilosc"]?.Value != null)
                    decimal.TryParse(row.Cells["Ilosc"].Value.ToString(), out ilosc);

                if (ilosc > 0)
                    Pozycje.Add(new PozycjaPrzesun { KodProduktu = kod, Nazwa = nazwa, Ilosc = ilosc });
            }

            if (Pozycje.Count == 0)
            {
                MessageBox.Show("Żadna pozycja nie ma ilości większej od 0.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
        }
    }
}
