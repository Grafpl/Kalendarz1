using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
    /// <summary>Panel z włączonym double-buffering — eliminuje miganie podczas częstych Invalidate.</summary>
    internal class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }
    }

    /// <summary>Nowoczesna paleta dla ContextMenu - flat, jasna.</summary>
    internal class ModernMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(241, 245, 249);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(241, 245, 249);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(241, 245, 249);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(226, 232, 240);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(226, 232, 240);
        public override Color MenuItemBorder => Color.FromArgb(37, 99, 235);
        public override Color MenuBorder => Color.FromArgb(226, 232, 240);
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
        public override Color SeparatorDark => Color.FromArgb(226, 232, 240);
        public override Color SeparatorLight => Color.FromArgb(241, 245, 249);
    }

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

        // Moving average toggles dla wykresów (7d / 30d)
        private CheckBox chkMaPurchase7;
        private CheckBox chkMaPurchase30;
        private CheckBox chkMaSales7;
        private CheckBox chkMaSales30;

        // KPI strip — etykiety wartości + delty d-1 / d-7
        private Panel kpiStripPanel;
        private readonly Dictionary<string, Label> _kpiValueLabels = new();
        private readonly Dictionary<string, Label> _kpiDeltaDayLabels = new();
        private readonly Dictionary<string, Label> _kpiDeltaWeekLabels = new();

        // Quick period preset toggle buttons
        private FlowLayoutPanel presetsPanel;
        private readonly List<Button> _presetButtons = new();

        // Wyszukiwarka w siatce
        private TextBox txtSearchBox;
        private string _searchText = string.Empty;

        // Tryb porównania (z poprzednim okresem)
        private CheckBox chkCompareToPrevious;

        // Pokaż / ukryj KPI strip
        private CheckBox chkShowKpi;
        private TableLayoutPanel _outerLayout; // referencja do mainContainer dla zmiany wysokości KPI rzędu

        // Tryb agregacji wykresów
        private enum AggregationMode { Day, Week, Month, Quarter, Year }
        private AggregationMode _aggMode = AggregationMode.Day;
        private FlowLayoutPanel _aggButtonsHost;
        private readonly List<Button> _aggButtons = new();

        // Wykres przebitka (marża)
        private Chart marginChart;
        private CheckBox chkMarginRolnicza;
        private CheckBox chkMarginWolnorynkowa;

        // Etykiety na każdym punkcie (per wykres)
        private CheckBox chkLabelsPurchase;
        private CheckBox chkLabelsSales;
        private CheckBox chkLabelsMargin;

        // Wykres przebitka - dodatkowa seria: Nasza Tuszka - Średnia ze wszystkich potwierdzonych dostaw
        private CheckBox chkMarginAllConfirmed;

        // Współdzielony ToolTip dla kart KPI
        private ToolTip _kpiTooltipProvider;

        // Referencje do paneli zakładek (zamiast indeksów)
        private FlowLayoutPanel _statsPanel;

        // Wykres łączony (overlay z dwiema osiami Y)
        private Chart overlayChart;
        private CheckBox chkOverlayMin, chkOverlayLacz, chkOverlayRoln, chkOverlayWolny;
        private CheckBox chkOverlayZrzesz, chkOverlayNasza, chkOverlaySrAll;
        private CheckBox chkOverlayMargin1, chkOverlayMargin2;
        private CheckBox chkLabelsOverlay;

        // Sezonowość (heatmap roczny)
        private Panel sezonowoscPanel;
        private ComboBox sezonColumnSelector;

        // Klienci (top-N z bazy Handel)
        private DataGridView klienciGrid;
        private Label klienciStatusLabel;
        private Button klienciRefreshButton;

        // Tygodnie - analiza dni tygodnia per wybrana cena
        private Panel tygodnieChartPanel;
        private ComboBox tygodnieColumnSelector;
        private CheckBox tygodnieIncludeWeekends;
        private Label tygodnieSummaryLabel;
        private DataGridView tygodnieTable;

        // YoY - porównanie rok do roku
        private Panel yoyChartPanel;
        private ComboBox yoyColumnSelector;
        private NumericUpDown yoyYearFrom;
        private NumericUpDown yoyYearTo;
        private CheckBox yoyShowLabels;
        private AggregationMode _yoyAggMode = AggregationMode.Month;
        private readonly List<Button> _yoyAggButtons = new();

        // Prognoza
        private Panel prognozaChartPanel;
        private ComboBox prognozaColumnSelector;
        private NumericUpDown prognozaHorizon;

        // Kontrakty vs Wolny rynek — pola i metody w WidokCenWszystkich.Kontrakty.cs (partial class)

        // Pasze → żywiec
        private Panel paszeChartPanel;
        private Label paszeStatusLabel;
        private DataTable paszeData;
        private DateTimePicker paszeDateFrom;
        private DateTimePicker paszeDateTo;

        // Alerty cenowe
        private FlowLayoutPanel alertyPanel;

        // Kolory motywu Material Design
        private readonly Color primaryColor = Color.FromArgb(33, 150, 243);    // Niebieski
        private readonly Color accentColor = Color.FromArgb(255, 152, 0);      // Pomarańczowy
        private readonly Color successColor = Color.FromArgb(76, 175, 80);     // Zielony
        private readonly Color dangerColor = Color.FromArgb(244, 67, 54);      // Czerwony
        private readonly Color backgroundColor = Color.FromArgb(245, 247, 251);
        private readonly Color cardBackgroundColor = Color.White;
        private readonly Color subtleBorderColor = Color.FromArgb(226, 232, 240);

        // Kolumny cenowe — wspólna lista używana przez heat map, KPI, statystyki
        private static readonly string[] _priceColumns =
        {
            "Minister", "Laczona", "Rolnicza", "Wolnorynkowa",
            "Tuszka Zrzeszenia", "Nasza Tuszka"
        };
        private static readonly string[] _diffColumns = { "Rolnicza-Wolny", "Różnica Tuszek" };

        #endregion

        #region Konstruktor i inicjalizacja

        public WidokCenWszystkich()
        {
            try
            {
                InitializeComponent();
                try { WindowIconHelper.SetIcon(this); } catch { /* ikona nie krytyczna */ }
                InitializeUI();
            }
            catch (Exception ex)
            {
                // Pełna diagnostyka jeśli coś rzuci w konstruktorze
                MessageBox.Show(
                    $"Krytyczny błąd konstruktora WidokCenWszystkich:\n\n" +
                    $"Typ: {ex.GetType().FullName}\n" +
                    $"Komunikat: {ex.Message}\n\n" +
                    $"InnerException: {(ex.InnerException == null ? "(brak)" : ex.InnerException.GetType().Name + ": " + ex.InnerException.Message)}\n\n" +
                    $"StackTrace:\n{ex.StackTrace}",
                    "Błąd otwierania okna 'Pokaż ceny'",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw; // przekaż dalej, aby caller (toast w WidokKalendarza) też zobaczył
            }
        }

        private void InitializeUI()
        {
            try
            {
                // Ustawienia formularza
                this.Text = "System Analizy Cen - Panel Główny";
                this.Size = new Size(1500, 920);
                this.MinimumSize = new Size(1100, 720);
                this.StartPosition = FormStartPosition.CenterScreen;
                // Maksymalizacja do obszaru roboczego (pasek zadań Windows pozostaje widoczny)
                this.WindowState = FormWindowState.Maximized;
                this.Font = new Font("Segoe UI", 9.5F);
                this.BackColor = backgroundColor;
                this.KeyPreview = true;
                this.KeyDown += WidokCenWszystkich_KeyDown;

                // Włącz double buffering dla płynniejszego renderowania
                this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                             ControlStyles.UserPaint |
                             ControlStyles.DoubleBuffer, true);

                // Ukryj stare kontrolki
                HideOriginalControls();

                // Utwórz nowy interfejs
                CreateModernUI();

                // Załaduj dane asynchronicznie
                this.Load += async (s, e) =>
                {
                    try { await LoadDataAsync(); }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas ładowania danych po otwarciu okna:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                            "Błąd Load", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
            }
            catch (Exception ex)
            {
                // Diagnostyczny dialog - aby okno się jednak otwarło i widać było co poszło nie tak
                MessageBox.Show(
                    $"Błąd inicjalizacji okna 'System Analizy Cen':\n\n" +
                    $"{ex.GetType().Name}: {ex.Message}\n\n" +
                    $"Lokalizacja:\n{ex.StackTrace}",
                    "Błąd inicjalizacji",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void WidokCenWszystkich_KeyDown(object sender, KeyEventArgs e)
        {
            // F5 — odśwież
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                await LoadDataAsync();
                return;
            }

            // Ctrl+F — fokus do wyszukiwarki
            if (e.Control && e.KeyCode == Keys.F)
            {
                e.Handled = true;
                txtSearchBox?.Focus();
                txtSearchBox?.SelectAll();
                return;
            }

            // Ctrl+E — eksport CSV bezpośrednio
            if (e.Control && e.KeyCode == Keys.E)
            {
                e.Handled = true;
                _ = ExportToCSVAsync();
                return;
            }

            // Ctrl+C — kopiuj zaznaczone wiersze do schowka
            if (e.Control && e.KeyCode == Keys.C && dataGridView1?.Focused == true)
            {
                e.Handled = true;
                CopySelectedRows();
                return;
            }

            // Ctrl+1..4 — przełącz zakładkę
            if (e.Control && (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D4))
            {
                int idx = e.KeyCode - Keys.D1;
                if (mainTabControl != null && idx < mainTabControl.TabCount)
                {
                    e.Handled = true;
                    mainTabControl.SelectedIndex = idx;
                }
            }
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
            // Główny kontener: Header / KPI strip / Filter / Tabs / Status
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(0),
                BackColor = backgroundColor
            };
            _outerLayout = mainContainer;

            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // Header (kompaktowy z subtelnym cieniem)
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));   // Filter Panel (2 czytelne rzędy 40+40 + paddingi)
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Content (wykresy)
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // Status

            // 1. Header Panel
            var headerPanel = CreateHeaderPanel();
            mainContainer.Controls.Add(headerPanel, 0, 0);

            // 2. Filter Panel + KPI chips (KPI tworzymy najpierw, potem filter panel je używa)
            kpiStripPanel = CreateKpiStrip();
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

        #region KPI strip — snapshot dnia + delty

        // Schemat kolorów per karta KPI
        private static readonly Dictionary<string, (Color Bg, Color Fg, string Emoji, string Title)> _kpiTheme = new()
        {
            ["Minister"]          = (Color.FromArgb(37, 99, 235),   Color.White, "🏛", "Ministerialna"),
            ["Laczona"]           = (Color.FromArgb(124, 58, 237),  Color.White, "⚖",  "Łączona"),
            ["Rolnicza"]          = (Color.FromArgb(22, 163, 74),   Color.White, "🌾", "Rolnicza"),
            ["Wolnorynkowa"]      = (Color.FromArgb(234, 179, 8),   Color.Black, "💱", "Wolnorynkowa"),
            ["Tuszka Zrzeszenia"] = (Color.FromArgb(234, 88, 12),   Color.White, "🏭", "Tuszka Zrzeszenia"),
            ["Nasza Tuszka"]      = (Color.FromArgb(13, 148, 136),  Color.White, "🍗", "Nasza Tuszka"),
        };

        private Panel CreateKpiStrip()
        {
            // Poziomy flow chipsów - placement obok presetów w filter panel
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                Height = 28,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = backgroundColor,
                Margin = new Padding(8, 0, 0, 0)
            };

            for (int i = 0; i < _priceColumns.Length; i++)
            {
                var col = _priceColumns[i];
                var card = CreateKpiCard(col);
                flow.Controls.Add(card);
            }

            return flow;
        }

        private Panel CreateKpiCard(string columnKey)
        {
            var (accent, _, emoji, _) = _kpiTheme[columnKey];

            // Skrócone nazwy aby zmieścić się na chipie
            var shortNames = new Dictionary<string, string>
            {
                ["Minister"]          = "Min.",
                ["Laczona"]           = "Łącz.",
                ["Rolnicza"]          = "Roln.",
                ["Wolnorynkowa"]      = "Wolny",
                ["Tuszka Zrzeszenia"] = "Zrzesz.",
                ["Nasza Tuszka"]      = "Nasza"
            };
            var shortName = shortNames.TryGetValue(columnKey, out var sn) ? sn : columnKey;

            var card = new BufferedPanel
            {
                AutoSize = false,
                Width = 132,
                Height = 30,
                Margin = new Padding(3, 0, 3, 0),
                Padding = new Padding(0),
                BackColor = backgroundColor,
                Cursor = Cursors.Hand
            };
            card.Tag = columnKey;

            // Profesjonalny chip z subtelnym tłem, kolorowym dotem, zaokrąglonymi rogami
            card.Paint += (s, e) =>
            {
                try
                {
                    if (card.Width < 4 || card.Height < 4) return;
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = GetRoundedRectangle(rect, 6);

                    // Białe tło
                    using var fillBg = new SolidBrush(Color.White);
                    e.Graphics.FillPath(fillBg, path);

                    // Subtelne obramowanie (jaśniejsze niż primary)
                    using var pen = new Pen(Color.FromArgb(225, 230, 236), 1);
                    e.Graphics.DrawPath(pen, path);

                    // Mała kropka akcentu po lewej (8x8) - identyfikator koloru cenowego
                    using var dotBrush = new SolidBrush(accent);
                    e.Graphics.FillEllipse(dotBrush, 8, (card.Height - 8) / 2, 8, 8);
                }
                catch { /* Paint nie może rzucać */ }
            };

            // Skrócona nazwa po lewej (kolor akcentu)
            var lblTitle = new Label
            {
                Text = shortName,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 52,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(22, 2)
            };

            // Wartość po prawej (ciemny granat, większa czcionka)
            var lblValue = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 52,
                Height = 26,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(74, 2)
            };

            // Strzałka delty schowana
            var lblDeltaDay = new Label { Visible = false, AutoSize = true };
            var lblDeltaWeek = new Label { Visible = false, AutoSize = true };

            card.Controls.AddRange(new Control[] { lblTitle, lblValue, lblDeltaDay, lblDeltaWeek });
            _kpiValueLabels[columnKey] = lblValue;
            _kpiDeltaDayLabels[columnKey] = lblDeltaDay;
            _kpiDeltaWeekLabels[columnKey] = lblDeltaWeek;

            // Hover efekt - przyciemnione tło
            card.MouseEnter += (s, e) =>
            {
                card.BackColor = Color.FromArgb(241, 245, 249);
                card.Invalidate();
            };
            card.MouseLeave += (s, e) =>
            {
                card.BackColor = backgroundColor;
                card.Invalidate();
            };

            return card;
        }

        private void UpdateKpiStrip()
        {
            if (originalData == null || originalData.Rows.Count == 0) return;

            // Wykorzystaj cache zamiast re-sortowania
            var orderedByDate = _orderedByDateDesc ?? new List<DataRow>();
            if (orderedByDate.Count == 0) return;

            foreach (var col in _priceColumns)
            {
                if (!_kpiValueLabels.ContainsKey(col)) continue;
                var theme = _kpiTheme[col];
                // Chip ma białe tło - wartość ciemny granat, label kolorem akcentowym
                var valueFg = Color.FromArgb(15, 23, 42);

                // Najświeższa niepusta wartość (NIE imputed) — KPI pokazuje surowe dane
                DataRow latestRow = orderedByDate.FirstOrDefault(r =>
                    r[col] != DBNull.Value
                    && (col != "Wolnorynkowa" || !originalData.Columns.Contains("WolnorynkowaImputed") || r["WolnorynkowaImputed"] == DBNull.Value || !Convert.ToBoolean(r["WolnorynkowaImputed"])));

                if (latestRow == null)
                {
                    _kpiValueLabels[col].Text = "—";
                    _kpiValueLabels[col].ForeColor = Color.FromArgb(148, 163, 184);
                    _kpiDeltaDayLabels[col].Text = "";
                    _kpiDeltaWeekLabels[col].Text = "";
                    continue;
                }

                var latestValue = Convert.ToDecimal(latestRow[col]);
                var latestDate = Convert.ToDateTime(latestRow["DataRaw"]).Date;

                _kpiValueLabels[col].Text = $"{latestValue:N2}";
                _kpiValueLabels[col].ForeColor = valueFg;

                string sourceTip = col switch
                {
                    "Minister"          => "CenaMinisterialna (LibraNet)",
                    "Laczona"           => "(Min + Roln) / 2",
                    "Rolnicza"          => "CenaRolnicza (LibraNet)",
                    "Wolnorynkowa"      => "Średnia ważona z dostaw potwierdzonych (typ wolnyrynek)",
                    "Tuszka Zrzeszenia" => "CenaTuszki (LibraNet)",
                    "Nasza Tuszka"      => "Średnia sprzedaży Kurczak A (Handel)",
                    _ => col
                };

                // Δ d-1
                string deltaDayLine = "Δ d-1: brak danych";
                var prevDayRow = orderedByDate
                    .Where(r => r[col] != DBNull.Value
                                && Convert.ToDateTime(r["DataRaw"]).Date < latestDate)
                    .FirstOrDefault();
                if (prevDayRow != null)
                {
                    var prevValue = Convert.ToDecimal(prevDayRow[col]);
                    var diff = latestValue - prevValue;
                    var pct = prevValue == 0 ? 0 : (diff / prevValue) * 100m;
                    var arrow = diff > 0 ? "▲" : diff < 0 ? "▼" : "→";
                    deltaDayLine = $"Δ d-1: {arrow} {diff:+0.00;-0.00;0.00} zł ({pct:+0.0;-0.0;0.0}%)";
                }

                // Δ vs ~7 dni temu
                string deltaWeekLine = "Δ tydz: brak danych";
                var weekTarget = latestDate.AddDays(-7);
                var weekRow = orderedByDate
                    .Where(r => r[col] != DBNull.Value
                                && Convert.ToDateTime(r["DataRaw"]).Date <= weekTarget)
                    .FirstOrDefault();
                if (weekRow != null)
                {
                    var prevWeek = Convert.ToDecimal(weekRow[col]);
                    var diff = latestValue - prevWeek;
                    var pct = prevWeek == 0 ? 0 : (diff / prevWeek) * 100m;
                    var arrow = diff > 0 ? "▲" : diff < 0 ? "▼" : "→";
                    deltaWeekLine = $"Δ 7d: {arrow} {pct:+0.0;-0.0;0.0}% (vs {prevWeek:N2} zł)";
                }

                var tip = $"{theme.Title}\n" +
                          $"Data: {latestDate:dd.MM.yyyy ddd}\n" +
                          $"Wartość: {latestValue:N2} zł\n" +
                          $"{deltaDayLine}\n" +
                          $"{deltaWeekLine}\n" +
                          $"Źródło: {sourceTip}";

                if (_kpiValueLabels[col].Parent is Panel cardPanel)
                {
                    if (_kpiTooltipProvider == null)
                        _kpiTooltipProvider = new ToolTip { ShowAlways = true, AutoPopDelay = 10000, InitialDelay = 200, ReshowDelay = 100 };
                    _kpiTooltipProvider.SetToolTip(cardPanel, tip);
                    foreach (Control c in cardPanel.Controls)
                        _kpiTooltipProvider.SetToolTip(c, tip);
                }
            }
        }

        #endregion

        private Panel CreateHeaderPanel()
        {
            var headerPanel = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = primaryColor,
                Padding = new Padding(20, 0, 16, 0)
            };

            // Diagonalny gradient z subtelnym highlightem u góry (efekt premium)
            headerPanel.Paint += (s, e) =>
            {
                try
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = headerPanel.ClientRectangle;

                    // Główny gradient lewa→prawa, jaśniejszy → ciemniejszy
                    using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        rect,
                        Color.FromArgb(30, 121, 234),  // primary nieco jaśniejszy
                        Color.FromArgb(29, 78, 216),   // primary nieco ciemniejszy
                        System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                    e.Graphics.FillRectangle(brush, rect);

                    // Subtelny gradient na górze (highlight) - białe alpha 30→0 wertykalnie
                    var topRect = new Rectangle(0, 0, rect.Width, rect.Height / 2);
                    using var topGrad = new System.Drawing.Drawing2D.LinearGradientBrush(
                        topRect,
                        Color.FromArgb(40, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255),
                        System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                    e.Graphics.FillRectangle(topGrad, topRect);

                    // Cienka jasna linia u góry (highlight edge)
                    using var topPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1);
                    e.Graphics.DrawLine(topPen, 0, 0, rect.Width, 0);

                    // Dolny cień (drop shadow effect)
                    using var shadowPen = new Pen(Color.FromArgb(80, 0, 0, 0), 1);
                    e.Graphics.DrawLine(shadowPen, 0, rect.Height - 1, rect.Width, rect.Height - 1);
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            // Tytuł — większa typografia + subtitle
            var lblTitle = new Label
            {
                Text = "📊  System Analizy Cen",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 8)
            };
            var lblSubtitle = new Label
            {
                Text = "Ceny skupu • Ceny tuszki • Marża • Sezonowość",
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = Color.FromArgb(200, 220, 255),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(22, 30)
            };
            var headerTip = new ToolTip();
            headerTip.SetToolTip(lblTitle, "Skróty:  F5 odśwież  •  Ctrl+F szukaj  •  Ctrl+E eksport  •  Ctrl+1..8 zakładki  •  Enter szczegóły");

            // Przyciski akcji
            var flowPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                AutoSize = true,
                Padding = new Padding(0, 10, 0, 10),
                BackColor = Color.Transparent
            };

            var btnRefresh = CreateHeaderButton("🔄  Odśwież", async (s, e) => await LoadDataAsync());
            var btnExport = CreateHeaderButton("📤  Eksport", ShowExportMenu);
            var btnSettings = CreateHeaderButton("⚙️  Pomoc", ShowSettings);

            flowPanel.Controls.AddRange(new[] { btnSettings, btnExport, btnRefresh });

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(flowPanel);

            return headerPanel;
        }

        private Panel CreateGlobalFilterPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backgroundColor,
                Padding = new Padding(16, 8, 16, 8)
            };
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(subtleBorderColor, 1);
                e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
            };

            // Górny rząd: presety + KPI chipsy (większa wysokość)
            presetsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = backgroundColor,
                AutoSize = false,
                Padding = new Padding(0, 0, 0, 4)
            };

            var lblPresets = new Label
            {
                Text = "📅 Okres",
                Font = new Font("Segoe UI Semibold", 9.25F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Margin = new Padding(0, 9, 10, 0)
            };
            presetsPanel.Controls.Add(lblPresets);

            (string label, Func<(DateTime From, DateTime To)> resolver)[] presets =
            {
                ("Dziś",  () => (DateTime.Today, DateTime.Today)),
                ("7 dni", () => (DateTime.Today.AddDays(-7), DateTime.Today)),
                ("14 dni",() => (DateTime.Today.AddDays(-14), DateTime.Today)),
                ("30 dni",() => (DateTime.Today.AddDays(-30), DateTime.Today)),
                ("3 mies",() => (DateTime.Today.AddMonths(-3), DateTime.Today)),
                ("6 mies",() => (DateTime.Today.AddMonths(-6), DateTime.Today)),
                ("Rok",   () => (DateTime.Today.AddYears(-1), DateTime.Today)),
                ("YTD",   () => (new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today)),
                ("Wszystko", () => (new DateTime(2023, 1, 1), DateTime.Today)),
            };

            foreach (var preset in presets)
            {
                var btn = CreatePresetButton(preset.label);
                var resolver = preset.resolver; // capture
                btn.Click += async (s, e) =>
                {
                    var range = resolver();
                    SetActivePreset(preset.label);
                    // Tymczasowo wyłącz handlery żeby nie odpalić RefreshAllDataAsync 2 razy
                    mainDateFrom.ValueChanged -= MainDateRange_ValueChanged;
                    mainDateTo.ValueChanged -= MainDateRange_ValueChanged;
                    mainDateFrom.Value = range.From;
                    mainDateTo.Value = range.To;
                    mainDateFrom.ValueChanged += MainDateRange_ValueChanged;
                    mainDateTo.ValueChanged += MainDateRange_ValueChanged;
                    await RefreshAllDataAsync();
                };
                _presetButtons.Add(btn);
                presetsPanel.Controls.Add(btn);
            }

            // KPI chipsy obok presetów (jeśli zostały utworzone wcześniej)
            if (kpiStripPanel != null)
                presetsPanel.Controls.Add(kpiStripPanel);

            // Dolny rząd: zakres dat + weekendy + szukaj + porównaj (lepsze odstępy, vertical centered)
            var bottomFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = backgroundColor,
                Padding = new Padding(0, 6, 0, 0)
            };

            // Spójne marginesy: górny ~9 (centruje 23px control w 40px row), padding poziomy stały
            var lblFrom = new Label
            {
                Text = "Od:",
                AutoSize = true,
                Margin = new Padding(0, 9, 6, 0),
                Font = new Font("Segoe UI Semibold", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            mainDateFrom = new DateTimePicker
            {
                Width = 116,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(-3),
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 0)
            };
            mainDateFrom.ValueChanged += MainDateRange_ValueChanged;

            var lblTo = new Label
            {
                Text = "Do:",
                AutoSize = true,
                Margin = new Padding(14, 9, 6, 0),
                Font = new Font("Segoe UI Semibold", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            mainDateTo = new DateTimePicker
            {
                Width = 116,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 0)
            };
            mainDateTo.ValueChanged += MainDateRange_ValueChanged;

            chkShowWeekends = new CheckBox
            {
                Text = "🗓 Weekendy",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(18, 8, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };
            chkShowWeekends.CheckedChanged += async (s, e) => await RefreshAllDataAsync();

            chkShowKpi = new CheckBox
            {
                Text = "📊 KPI",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(12, 8, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };
            chkShowKpi.CheckedChanged += (s, e) =>
            {
                if (kpiStripPanel == null) return;
                kpiStripPanel.Visible = chkShowKpi.Checked;
            };

            // Checkbox "Porównaj okresy" usunięty - logika przeniesiona do dedykowanej zakładki / kart porównawczych

            // Wyszukiwarka - z ikoną w polu
            var lblSearch = new Label
            {
                Text = "🔍",
                AutoSize = true,
                Margin = new Padding(20, 9, 4, 0),
                Font = new Font("Segoe UI", 10F)
            };
            txtSearchBox = new TextBox
            {
                Width = 200,
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "data lub wartość…",
                Margin = new Padding(0, 6, 0, 0)
            };
            // Debounce 300ms - filter dopiero po przerwie w pisaniu
            _searchDebounceTimer = new Timer { Interval = 300 };
            _searchDebounceTimer.Tick += async (s, e) =>
            {
                _searchDebounceTimer.Stop();
                await FilterDataAsync();
            };
            txtSearchBox.TextChanged += (s, e) =>
            {
                _searchText = txtSearchBox.Text.Trim();
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            };

            bottomFlow.Controls.AddRange(new Control[]
            {
                lblFrom, mainDateFrom, lblTo, mainDateTo,
                chkShowWeekends, chkShowKpi,
                lblSearch, txtSearchBox
            });

            // Vertical separator między grupami (płaski - rysowany w Paint)
            // (Można dodać visual separator jako narrow Panel jeśli chce się akcent)

            panel.Controls.Add(bottomFlow);
            panel.Controls.Add(presetsPanel);

            // Domyślny aktywny preset
            SetActivePreset("3 mies");

            return panel;
        }

        private async void MainDateRange_ValueChanged(object sender, EventArgs e)
        {
            SetActivePreset(null); // ręczna zmiana → odznacz preset
            await RefreshAllDataAsync();
            // Reload klientów jeśli już byli załadowani
            if (klienciGrid?.Rows.Count > 0) _ = LoadKlienciAsync();
        }

        private Button CreatePresetButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                Tag = text,
                AutoSize = false,
                Size = new Size(54, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Margin = new Padding(2, 1, 2, 1),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = subtleBorderColor;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            return btn;
        }

        private void SetActivePreset(string activeLabel)
        {
            foreach (var btn in _presetButtons)
            {
                bool active = activeLabel != null && (string)btn.Tag == activeLabel;
                btn.BackColor = active ? primaryColor : Color.White;
                btn.ForeColor = active ? Color.White : Color.FromArgb(71, 85, 105);
                btn.FlatAppearance.BorderColor = active ? primaryColor : subtleBorderColor;
            }
        }

        private Button CreateHeaderButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 255, 255, 255),
                Size = new Size(108, 30),
                Margin = new Padding(4, 4, 0, 0),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 8.75F),
                TextAlign = ContentAlignment.MiddleCenter
            };

            button.FlatAppearance.BorderColor = Color.FromArgb(110, 255, 255, 255);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 255, 255, 255);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(120, 255, 255, 255);
            button.Click += onClick;

            return button;
        }

        // Generuje ikonę 18x18 z emoji w kolorowym kółku
        private static Image RenderTabIcon(string emoji, Color tint)
        {
            int size = 18;
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using var emojiFont = new Font("Segoe UI Emoji", 12F, FontStyle.Regular, GraphicsUnit.Pixel);
                var sz = g.MeasureString(emoji, emojiFont);
                float x = (size - sz.Width) / 2f;
                float y = (size - sz.Height) / 2f;
                g.DrawString(emoji, emojiFont, Brushes.Black, x, y);
            }
            return bmp;
        }

        private TabControl CreateTabControl()
        {
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Padding = new Point(14, 6),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(150, 30),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Multiline = true   // 14 zakładek - automatycznie rozkłada na 2 linie gdy nie mieści się
            };

            // ImageList z ikonkami zakładek - prawidłowe renderowanie emoji
            var tabIcons = new ImageList
            {
                ImageSize = new Size(18, 18),
                ColorDepth = ColorDepth.Depth32Bit
            };
            tabIcons.Images.Add("dane",       RenderTabIcon("📋", primaryColor));
            tabIcons.Images.Add("zakup",      RenderTabIcon("📈", primaryColor));
            tabIcons.Images.Add("sprzedaz",   RenderTabIcon("💰", primaryColor));
            tabIcons.Images.Add("przebitka",  RenderTabIcon("🎯", primaryColor));
            tabIcons.Images.Add("laczony",    RenderTabIcon("🔗", primaryColor));
            tabIcons.Images.Add("sezon",      RenderTabIcon("📆", primaryColor));
            tabIcons.Images.Add("tygodnie",   RenderTabIcon("🗓", primaryColor));
            tabIcons.Images.Add("yoy",        RenderTabIcon("📈", primaryColor));
            tabIcons.Images.Add("prognoza",   RenderTabIcon("🔮", primaryColor));
            tabIcons.Images.Add("kontrakty",  RenderTabIcon("📋", primaryColor));
            tabIcons.Images.Add("pasze",      RenderTabIcon("🌾", primaryColor));
            tabIcons.Images.Add("alerty",     RenderTabIcon("🔔", primaryColor));
            tabIcons.Images.Add("klienci",    RenderTabIcon("📦", primaryColor));
            tabIcons.Images.Add("statystyki", RenderTabIcon("📊", primaryColor));
            tabControl.ImageList = tabIcons;
            // Auto-load klientów gdy użytkownik kliknie zakładkę (raz)
            tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (tabControl.SelectedTab?.Text?.Contains("Klienci") == true
                    && klienciGrid?.Rows.Count == 0
                    && klienciRefreshButton?.Enabled == true)
                {
                    _ = LoadKlienciAsync();
                }
            };

            // Flat tabs z aktywnym podkreśleniem w kolorze primary
            tabControl.DrawItem += (s, e) =>
            {
                try
                {
                    var tc = (TabControl)s;
                    if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
                    var page = tc.TabPages[e.Index];
                    bool isActive = e.Index == tc.SelectedIndex;
                    var bounds = e.Bounds;

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    // Tło - aktywne białe, nieaktywne lekko szare
                    Color bg = isActive ? Color.White : Color.FromArgb(248, 250, 252);
                    using (var bgBrush = new SolidBrush(bg))
                        e.Graphics.FillRectangle(bgBrush, bounds);

                    // Subtelny separator między tabami (pionowa kreska po prawej, jeśli nie ostatni)
                    if (!isActive && e.Index < tc.TabPages.Count - 1)
                    {
                        using var sepPen = new Pen(Color.FromArgb(232, 236, 244), 1);
                        e.Graphics.DrawLine(sepPen, bounds.Right - 1, bounds.Y + 8, bounds.Right - 1, bounds.Bottom - 8);
                    }

                    // Aktywna zakładka: gruby pasek primary + drobna jasna linia podświetlenia powyżej (efekt "pillow")
                    if (isActive)
                    {
                        // Top accent (3 px kolor primary - reading direction up-down emphasis)
                        using var topAccent = new SolidBrush(primaryColor);
                        e.Graphics.FillRectangle(topAccent, bounds.X, bounds.Y, bounds.Width, 3);
                        // Subtle bottom shadow (zmiękczenie krawędzi)
                        using var bottomShadow = new Pen(Color.FromArgb(30, 0, 0, 0), 1);
                        e.Graphics.DrawLine(bottomShadow, bounds.X, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                    }

                    // Ikona z ImageList po lewej (centrowana wertykalnie)
                    int iconSize = 18;
                    int iconX = bounds.X + 12;
                    int iconY = bounds.Y + (bounds.Height - iconSize) / 2;
                    int textX = iconX + 24;
                    if (tc.ImageList != null && e.Index < tc.ImageList.Images.Count)
                    {
                        e.Graphics.DrawImage(tc.ImageList.Images[e.Index], iconX, iconY, iconSize, iconSize);
                    }

                    // Tekst tabu - pomiń emoji prefix
                    string text = page.Text;
                    int spaceIdx = text.IndexOf(' ');
                    if (spaceIdx > 0 && spaceIdx < 4) text = text.Substring(spaceIdx + 1);

                    Color fg = isActive ? primaryColor : Color.FromArgb(75, 85, 105);
                    using var textBrush = new SolidBrush(fg);
                    using var font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular);
                    var textRect = new Rectangle(textX, bounds.Y, bounds.Width - (textX - bounds.X) - 6, bounds.Height);
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    e.Graphics.DrawString(text, font, textBrush, textRect, format);
                }
                catch { /* Paint nie może rzucać */ }
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

            // Tab 4: Wykres Przebitka (marża)
            var tabMargin = new TabPage("🎯 Wykres Przebitka")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateMarginChartTab(tabMargin);

            // Tab 5: Wykres Łączony (overlay z dwiema osiami Y)
            var tabOverlay = new TabPage("📈 Wykres Łączony")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateOverlayChartTab(tabOverlay);

            // Tab 6: Sezonowość (heatmap roczny)
            var tabSezonowosc = new TabPage("📆 Sezonowość")
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            CreateSezonowoscTab(tabSezonowosc);

            // Tab 7: Tygodnie - analiza dni tygodnia per cena
            var tabTygodnie = new TabPage("🗓 Tygodnie") { BackColor = Color.White, Padding = new Padding(10) };
            CreateTygodnieTab(tabTygodnie);

            // Tab 8: YoY - porównanie rok do roku
            var tabYoY = new TabPage("📈 YoY") { BackColor = Color.White, Padding = new Padding(10) };
            CreateYoYTab(tabYoY);

            // Tab 9: Kontrakty vs Wolny rynek
            var tabKontrakty = new TabPage("📋 Kontrakty") { BackColor = Color.White, Padding = new Padding(10) };
            CreateKontraktyTab(tabKontrakty);

            // Tab 10: Pasze → żywiec
            var tabPasze = new TabPage("🌾 Pasze") { BackColor = Color.White, Padding = new Padding(10) };
            CreatePaszeTab(tabPasze);

            // Tab 11: Klienci (top-N z Handel)
            var tabKlienci = new TabPage("📦 Klienci Top-N") { BackColor = Color.White, Padding = new Padding(10) };
            CreateKlienciTab(tabKlienci);

            tabControl.TabPages.AddRange(new[] {
                tabData, tabPurchaseChart, tabSalesChart, tabMargin,
                tabOverlay, tabSezonowosc, tabTygodnie,
                tabYoY, tabKontrakty, tabPasze,
                tabKlienci
            });

            return tabControl;
        }

        // Footer agregatów: niestandardowe rysowanie wyrównane do kolumn DataGridView
        private Panel _aggregatesFooterPanel;
        private readonly Dictionary<string, (decimal Avg, decimal Min, decimal Max)?> _aggValues = new();

        private void CreateDataTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

            var grid = CreateModernDataGrid();
            container.Controls.Add(grid, 0, 0);

            _aggregatesFooterPanel = CreateAggregatesFooter();
            container.Controls.Add(_aggregatesFooterPanel, 0, 1);

            // Re-render footer gdy kolumny zmieniają szerokość lub gdy grid scrolluje
            dataGridView1.ColumnWidthChanged += (s, e) => _aggregatesFooterPanel.Invalidate();
            dataGridView1.Scroll += (s, e) => _aggregatesFooterPanel.Invalidate();
            dataGridView1.SizeChanged += (s, e) => _aggregatesFooterPanel.Invalidate();
            dataGridView1.ColumnDisplayIndexChanged += (s, e) => _aggregatesFooterPanel.Invalidate();

            tab.Controls.Add(container);
        }

        private Panel CreateAggregatesFooter()
        {
            var panel = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            panel.Paint += AggregatesFooter_Paint;
            return panel;
        }

        private void AggregatesFooter_Paint(object sender, PaintEventArgs e)
        {
            try
            {
            if (!(sender is Panel panel)) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Górna linia
            using (var pen = new Pen(subtleBorderColor, 1))
                g.DrawLine(pen, 0, 0, panel.Width, 0);

            using var fontLabel = new Font("Segoe UI Semibold", 8F);
            using var fontValue = new Font("Segoe UI", 9F, FontStyle.Bold);
            using var brushLabelGrey = new SolidBrush(Color.FromArgb(71, 85, 105));
            using var brushDanger = new SolidBrush(dangerColor);
            using var brushSuccess = new SolidBrush(successColor);
            using var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));

            int rowH = (panel.Height - 6) / 3;
            int rowAvg = 4;
            int rowMin = 4 + rowH;
            int rowMax = 4 + 2 * rowH;

            // Etykiety wierszy (po lewej, jeżeli grid scrolluje to nakładamy się na kolumnę "Data")
            // Rysujemy etykiety bezpośrednio przy lewej krawędzi panelu, NIE w pozycji kolumny.
            g.DrawString("Średnia", fontLabel, brushLabelGrey, 6, rowAvg);
            g.DrawString("Min",     fontLabel, brushDanger,    6, rowMin);
            g.DrawString("Max",     fontLabel, brushSuccess,   6, rowMax);

            if (dataGridView1 == null || _aggValues.Count == 0) return;

            // Dla każdej kolumny cenowej znajdź realny X w siatce i tam narysuj wartość
            foreach (var col in _priceColumns)
            {
                if (!dataGridView1.Columns.Contains(col)) continue;
                var dgvCol = dataGridView1.Columns[col];
                if (!dgvCol.Visible) continue;

                Rectangle rect;
                try
                {
                    rect = dataGridView1.GetColumnDisplayRectangle(dgvCol.Index, false);
                }
                catch { continue; }

                if (rect.Width <= 0) continue;

                if (!_aggValues.TryGetValue(col, out var aggOpt) || !aggOpt.HasValue)
                {
                    // Brak danych
                    DrawRightAligned(g, "—", fontValue, brushDark,    rect.X, rowAvg, rect.Width);
                    DrawRightAligned(g, "—", fontValue, brushDanger,  rect.X, rowMin, rect.Width);
                    DrawRightAligned(g, "—", fontValue, brushSuccess, rect.X, rowMax, rect.Width);
                    continue;
                }

                var agg = aggOpt.Value;
                DrawRightAligned(g, $"{agg.Avg:N2} zł", fontValue, brushDark,    rect.X, rowAvg, rect.Width);
                DrawRightAligned(g, $"{agg.Min:N2} zł", fontValue, brushDanger,  rect.X, rowMin, rect.Width);
                DrawRightAligned(g, $"{agg.Max:N2} zł", fontValue, brushSuccess, rect.X, rowMax, rect.Width);
            }
            }
            catch { /* Paint nie może rzucać */ }
        }

        private static void DrawRightAligned(Graphics g, string text, Font font, Brush brush, int colX, int y, int colWidth)
        {
            const int padding = 6;
            var sz = g.MeasureString(text, font);
            float x = colX + colWidth - sz.Width - padding;
            g.DrawString(text, font, brush, x, y);
        }

        private void UpdateAggregatesFooter()
        {
            if (_aggregatesFooterPanel == null || filteredData == null) return;

            _aggValues.Clear();
            foreach (var col in _priceColumns)
            {
                if (!filteredData.Columns.Contains(col))
                {
                    _aggValues[col] = null;
                    continue;
                }

                var values = filteredData.AsEnumerable()
                    .Where(r => r[col] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r[col]))
                    .ToList();

                _aggValues[col] = values.Count == 0
                    ? null
                    : ((decimal Avg, decimal Min, decimal Max)?)(values.Average(), values.Min(), values.Max());
            }

            _aggregatesFooterPanel.Invalidate();
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

            // Cache fontów - utworzony raz, używany w CellFormatting
            _gridFontBold = new Font(dataGridView1.Font, FontStyle.Bold);
            _gridFontItalicBold = new Font(dataGridView1.Font, FontStyle.Italic | FontStyle.Bold);

            // Włącz double-buffering dla DataGridView (redukuje miganie podczas scrollu)
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, dataGridView1, new object[] { true });

            // Menu kontekstowe
            CreateContextMenu();

            // Zdarzenia
            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            dataGridView1.SelectionChanged += DataGridView1_SelectionChanged;
            dataGridView1.Visible = true;

            return dataGridView1;
        }

        private void DataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            var selectedCount = dataGridView1.SelectedRows.Count;
            if (selectedCount == 0)
            {
                UpdateStatusMessage(filteredData != null
                    ? $"Wyświetlono {filteredData.Rows.Count} z {originalData?.Rows.Count ?? 0} rekordów"
                    : "Gotowy");
                return;
            }

            // Wylicz sumy dla zaznaczonych wierszy w kolumnach cenowych
            var avgs = new List<string>();
            foreach (var col in _priceColumns)
            {
                if (!filteredData.Columns.Contains(col)) continue;
                var vals = new List<decimal>();
                foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                {
                    if (r.Cells[col].Value != null && r.Cells[col].Value != DBNull.Value
                        && decimal.TryParse(r.Cells[col].Value.ToString().Replace(" zł", "").Replace(",", "."),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal v))
                    {
                        vals.Add(v);
                    }
                }
                if (vals.Count > 0)
                    avgs.Add($"{col.Substring(0, Math.Min(col.Length, 4))}: {vals.Average():N2}");
            }

            string avgText = avgs.Count > 0 ? "  |  Śr. " + string.Join(" • ", avgs) : "";
            UpdateStatusMessage($"Zaznaczono {selectedCount} wierszy{avgText}");
        }

        private void CreateContextMenu()
        {
            var menu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI Emoji", 9.5F),
                ShowImageMargin = false,
                BackColor = Color.White,
                Renderer = new ToolStripProfessionalRenderer(new ModernMenuColors())
            };

            var miCopyRow = new ToolStripMenuItem("📋 Kopiuj wiersz(e)") { ShortcutKeyDisplayString = "Ctrl+C" };
            miCopyRow.Click += (s, e) => CopySelectedRows();

            var miCopyCell = new ToolStripMenuItem("📎 Kopiuj komórkę");
            miCopyCell.Click += (s, e) =>
            {
                if (dataGridView1.CurrentCell?.Value != null)
                {
                    Clipboard.SetText(dataGridView1.CurrentCell.Value.ToString());
                    UpdateStatusMessage("Skopiowano komórkę do schowka");
                }
            };

            var miDetails = new ToolStripMenuItem("🔍 Pokaż szczegóły dnia") { ShortcutKeyDisplayString = "Enter" };
            miDetails.Click += (s, e) =>
            {
                if (dataGridView1.CurrentRow != null) ShowDetailedInfo(dataGridView1.CurrentRow);
            };

            var miSeparator1 = new ToolStripSeparator();
            var miCsv = new ToolStripMenuItem("📊 Eksport do CSV") { ShortcutKeyDisplayString = "Ctrl+E" };
            miCsv.Click += async (s, e) => await ExportToCSVAsync();

            var miExcel = new ToolStripMenuItem("📈 Otwórz w Excel");
            miExcel.Click += async (s, e) => await OpenInExcelAsync();

            var miJson = new ToolStripMenuItem("📑 Eksport do JSON");
            miJson.Click += async (s, e) => await ExportToJSONAsync();

            menu.Items.AddRange(new ToolStripItem[]
            {
                miDetails, miSeparator1, miCopyRow, miCopyCell, new ToolStripSeparator(), miCsv, miExcel, miJson
            });

            dataGridView1.ContextMenuStrip = menu;

            // Enter na zaznaczonym wierszu = pokaż szczegóły
            dataGridView1.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && dataGridView1.CurrentRow != null)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    ShowDetailedInfo(dataGridView1.CurrentRow);
                }
            };
        }

        private async Task OpenInExcelAsync()
        {
            try
            {
                if (filteredData == null || filteredData.Rows.Count == 0)
                {
                    MessageBox.Show("Brak danych do otwarcia.", "Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string tempFile = Path.Combine(Path.GetTempPath(), $"Ceny_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                await Task.Run(() =>
                {
                    var csv = new StringBuilder();
                    // BOM dla Excel + Unicode + ; jako separator (polski locale)
                    var headers = filteredData.Columns.Cast<DataColumn>()
                        .Where(c => c.ColumnName != "DataRaw")
                        .Select(c => c.ColumnName);
                    csv.AppendLine(string.Join(";", headers));
                    foreach (DataRow row in filteredData.Rows)
                    {
                        var fields = headers.Select(h =>
                        {
                            var val = row[h]?.ToString() ?? "";
                            // Escape średnika i nowych linii
                            if (val.Contains(';') || val.Contains('"') || val.Contains('\n'))
                                val = "\"" + val.Replace("\"", "\"\"") + "\"";
                            return val;
                        });
                        csv.AppendLine(string.Join(";", fields));
                    }
                    File.WriteAllText(tempFile, csv.ToString(), new UTF8Encoding(true));
                });

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });
                UpdateStatusMessage("Otwieranie w Excel…");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć w Excel:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                BackColor = Color.FromArgb(248, 250, 252),
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
                Text = "📊 Serie:",
                AutoSize = true,
                Margin = new Padding(0, 9, 10, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
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

            chkLabelsPurchase = new CheckBox
            {
                Text = "🏷 Etykiety punktów",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(14, 8, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };
            chkLabelsPurchase.CheckedChanged += (s, e) => UpdatePurchaseChart();

            // Separator + agregacja
            var aggButtonsPurchase = BuildAggregationButtonsRow();

            flowPanel.Controls.AddRange(new Control[] {
                lblSeries, chkMinister, chkLaczona, chkRolnicza, chkWolnorynkowa,
                chkLabelsPurchase, aggButtonsPurchase
            });

            controlPanel.Controls.Add(flowPanel);

            // Wykres zakupowy
            purchaseChart = CreateEnhancedChart("Wykres Zakupowy — Ceny Skupu");
            purchaseChart.ChartAreas[0].AxisY.Title = "Cena skupu (PLN/kg)";

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
                BackColor = Color.FromArgb(248, 250, 252),
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
                Text = "📊 Serie:",
                AutoSize = true,
                Margin = new Padding(0, 9, 10, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
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

            chkLabelsSales = new CheckBox
            {
                Text = "🏷 Etykiety punktów",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(14, 8, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };
            chkLabelsSales.CheckedChanged += (s, e) => UpdateSalesChart();

            var aggButtonsSales = BuildAggregationButtonsRow();

            flowPanel.Controls.AddRange(new Control[] {
                lblSeries, chkTuszkaZrzeszenia, chkNaszaTuszka,
                chkLabelsSales, aggButtonsSales
            });

            controlPanel.Controls.Add(flowPanel);

            // Wykres sprzedażowy
            salesChart = CreateEnhancedChart("Wykres Sprzedażowy — Ceny Tuszki");
            salesChart.ChartAreas[0].AxisY.Title = "Cena tuszki (PLN/kg)";

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(salesChart, 0, 1);

            tab.Controls.Add(container);
        }

        private void CreateMarginChartTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
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
                Text = "📊 Serie:",
                AutoSize = true,
                Margin = new Padding(0, 9, 10, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            chkMarginRolnicza = new CheckBox
            {
                Text = "Tuszka Zrzeszenia − Rolnicza zakupu",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 14, 0)
            };
            chkMarginRolnicza.CheckedChanged += (s, e) => UpdateMarginChart();

            chkMarginWolnorynkowa = new CheckBox
            {
                Text = "Nasza Tuszka − Wolnorynkowa (z imputacją)",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkMarginWolnorynkowa.CheckedChanged += (s, e) => UpdateMarginChart();

            chkMarginAllConfirmed = new CheckBox
            {
                Text = "Nasza Tuszka − Średnia wszystkich potwierdzonych dostaw",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0)
            };
            chkMarginAllConfirmed.CheckedChanged += (s, e) => UpdateMarginChart();

            chkLabelsMargin = new CheckBox
            {
                Text = "🏷 Etykiety punktów",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(14, 8, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };
            chkLabelsMargin.CheckedChanged += (s, e) => UpdateMarginChart();

            var aggButtonsMargin = BuildAggregationButtonsRow();

            flowPanel.Controls.AddRange(new Control[]
            {
                lblSeries, chkMarginRolnicza, chkMarginWolnorynkowa, chkMarginAllConfirmed,
                chkLabelsMargin, aggButtonsMargin
            });

            controlPanel.Controls.Add(flowPanel);

            // Wykres przebitki — różnice (PLN/kg)
            marginChart = CreateEnhancedChart("Wykres Przebitka — Marża pomiędzy ceną sprzedaży a zakupu");
            marginChart.ChartAreas[0].AxisY.Title = "Przebitka (PLN/kg)";
            // Dodaj poziomą linię przy 0 zł — strefa zysku/straty
            marginChart.ChartAreas[0].AxisY.StripLines.Add(new StripLine
            {
                IntervalOffset = 0,
                StripWidth = 0.001,
                BackColor = Color.FromArgb(80, 100, 100, 100),
                BorderColor = Color.FromArgb(120, 100, 100, 100),
                BorderWidth = 1
            });

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(marginChart, 0, 1);

            tab.Controls.Add(container);
        }

        private void UpdateMarginChart()
        {
            if (marginChart == null || filteredData == null || filteredData.Rows.Count == 0) return;

            marginChart.Series.Clear();

            // Seria 1: Tuszka Zrzeszenia − Rolnicza
            if (chkMarginRolnicza?.Checked == true
                && filteredData.Columns.Contains("Tuszka Zrzeszenia")
                && filteredData.Columns.Contains("Rolnicza"))
            {
                var diff = AggregateForChart("__margin_zrzeszenie_rolnicza", row =>
                {
                    if (row["Tuszka Zrzeszenia"] == DBNull.Value || row["Rolnicza"] == DBNull.Value) return null;
                    return (double)(Convert.ToDecimal(row["Tuszka Zrzeszenia"]) - Convert.ToDecimal(row["Rolnicza"]));
                });

                var s = new Series("Zrzeszenie − Rolnicza")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 3,
                    Color = Color.FromArgb(234, 88, 12), // pomarańczowy (Zrzeszenie)
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerBorderColor = Color.White,
                    MarkerBorderWidth = 2,
                    ToolTip = "Zrzeszenie − Rolnicza\n" + GetAggregationXLabel() + ": #VALX{" + GetAggregationXFormat() + "}\nMarża: #VALY{#,##0.00} zł",
                    IsVisibleInLegend = true
                };
                foreach (var (date, value, _) in diff)
                    s.Points.AddXY(date, Math.Round(value, 2));
                if (s.Points.Count > 0)
                {
                    if (chkLabelsMargin?.Checked == true) ApplyAllPointLabels(s);
                    else AddEndLabel(s);
                    marginChart.Series.Add(s);
                }
            }

            // Seria 2: Nasza Tuszka − Wolnorynkowa (z imputacją)
            if (chkMarginWolnorynkowa?.Checked == true
                && filteredData.Columns.Contains("Nasza Tuszka")
                && filteredData.Columns.Contains("Wolnorynkowa"))
            {
                var diff = AggregateForChart("__margin_nasza_wolny", row =>
                {
                    if (row["Nasza Tuszka"] == DBNull.Value || row["Wolnorynkowa"] == DBNull.Value) return null;
                    return (double)(Convert.ToDecimal(row["Nasza Tuszka"]) - Convert.ToDecimal(row["Wolnorynkowa"]));
                }, imputedSourceColumn: "Wolnorynkowa");

                var s = new Series("Nasza Tuszka − Wolnorynkowa")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 3,
                    Color = Color.FromArgb(13, 148, 136), // teal (Nasza Tuszka)
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerBorderColor = Color.White,
                    MarkerBorderWidth = 2,
                    ToolTip = "Nasza Tuszka − Wolnorynkowa\n" + GetAggregationXLabel() + ": #VALX{" + GetAggregationXFormat() + "}\nMarża: #VALY{#,##0.00} zł",
                    IsVisibleInLegend = true
                };
                foreach (var (date, value, imputedAny) in diff)
                {
                    int idx = s.Points.AddXY(date, Math.Round(value, 2));
                    if (imputedAny)
                    {
                        var pt = s.Points[idx];
                        pt.MarkerColor = Color.FromArgb(37, 99, 235);
                        pt.MarkerStyle = MarkerStyle.Diamond;
                        pt.MarkerSize = 8;
                        pt.ToolTip = $"Nasza Tuszka − Wolnorynkowa (Wolnorynkowa uzupełniona)\n{GetAggregationXLabel()}: {date.ToString(GetAggregationXFormat())}\nMarża: {value:N2} zł";
                    }
                }
                if (s.Points.Count > 0)
                {
                    if (chkLabelsMargin?.Checked == true) ApplyAllPointLabels(s);
                    else AddEndLabel(s);
                    marginChart.Series.Add(s);
                }
            }

            // Seria 3: Nasza Tuszka − Średnia wszystkich potwierdzonych dostaw
            if (chkMarginAllConfirmed?.Checked == true
                && filteredData.Columns.Contains("Nasza Tuszka")
                && filteredData.Columns.Contains("Śr. Wszystkich Dostaw"))
            {
                var diff = AggregateForChart("__margin_nasza_all", row =>
                {
                    if (row["Nasza Tuszka"] == DBNull.Value || row["Śr. Wszystkich Dostaw"] == DBNull.Value) return null;
                    return (double)(Convert.ToDecimal(row["Nasza Tuszka"]) - Convert.ToDecimal(row["Śr. Wszystkich Dostaw"]));
                });

                var s = new Series("Nasza Tuszka − Wszystkie potwierdzone")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 3,
                    Color = Color.FromArgb(124, 58, 237), // fioletowy - jak Łączona w KPI
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerBorderColor = Color.White,
                    MarkerBorderWidth = 2,
                    ToolTip = "Nasza Tuszka − Średnia wszystkich potwierdzonych\n" + GetAggregationXLabel() + ": #VALX{" + GetAggregationXFormat() + "}\nMarża: #VALY{#,##0.00} zł",
                    IsVisibleInLegend = true
                };
                foreach (var (date, value, _) in diff)
                    s.Points.AddXY(date, Math.Round(value, 2));
                if (s.Points.Count > 0)
                {
                    if (chkLabelsMargin?.Checked == true) ApplyAllPointLabels(s);
                    else AddEndLabel(s);
                    marginChart.Series.Add(s);
                }
            }

            AdjustChartScale(marginChart);
        }

        // ═════════════════ WYKRES ŁĄCZONY (overlay z dwiema osiami Y) ═════════════════

        private void CreateOverlayChartTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10, 5, 10, 5)
            };

            var flow1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            var flow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            (CheckBox cb, string label, bool defaultChecked, Color color)[] purchase =
            {
                (chkOverlayMin     = new CheckBox(), "Ministerialna",  false, Color.FromArgb(37, 99, 235)),
                (chkOverlayLacz    = new CheckBox(), "Łączona",        false, Color.FromArgb(124, 58, 237)),
                (chkOverlayRoln    = new CheckBox(), "Rolnicza",       true,  Color.FromArgb(22, 163, 74)),
                (chkOverlayWolny   = new CheckBox(), "Wolnorynkowa",   true,  Color.FromArgb(234, 179, 8)),
                (chkOverlaySrAll   = new CheckBox(), "Średnia wszystkich", false, Color.FromArgb(100, 116, 139))
            };
            (CheckBox cb, string label, bool defaultChecked, Color color)[] sales =
            {
                (chkOverlayZrzesz  = new CheckBox(), "Tuszka Zrzeszenia", true, Color.FromArgb(234, 88, 12)),
                (chkOverlayNasza   = new CheckBox(), "Nasza Tuszka",      true, Color.FromArgb(13, 148, 136))
            };

            var lblZakup = new Label
            {
                Text = "🌾 Zakup (oś Y lewa):",
                AutoSize = true,
                Margin = new Padding(0, 9, 8, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            flow1.Controls.Add(lblZakup);
            foreach (var (cb, lbl, def, color) in purchase)
            {
                cb.Text = lbl;
                cb.Checked = def;
                cb.AutoSize = true;
                cb.Margin = new Padding(0, 7, 12, 0);
                cb.ForeColor = color;
                cb.CheckedChanged += (s, e) => UpdateOverlayChart();
                flow1.Controls.Add(cb);
            }

            var lblSprzedaz = new Label
            {
                Text = "🍗 Sprzedaż (oś Y prawa):",
                AutoSize = true,
                Margin = new Padding(0, 9, 8, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            flow2.Controls.Add(lblSprzedaz);
            foreach (var (cb, lbl, def, color) in sales)
            {
                cb.Text = lbl;
                cb.Checked = def;
                cb.AutoSize = true;
                cb.Margin = new Padding(0, 7, 12, 0);
                cb.ForeColor = color;
                cb.CheckedChanged += (s, e) => UpdateOverlayChart();
                flow2.Controls.Add(cb);
            }

            chkOverlayMargin1 = new CheckBox
            {
                Text = "Marża (Tuszka−Rolnicza)",
                AutoSize = true,
                Margin = new Padding(8, 7, 12, 0),
                ForeColor = Color.FromArgb(234, 88, 12),
                Checked = false
            };
            chkOverlayMargin1.CheckedChanged += (s, e) => UpdateOverlayChart();
            chkOverlayMargin2 = new CheckBox
            {
                Text = "Marża (Nasza−Wszystkie pot.)",
                AutoSize = true,
                Margin = new Padding(8, 7, 12, 0),
                ForeColor = Color.FromArgb(124, 58, 237),
                Checked = false
            };
            chkOverlayMargin2.CheckedChanged += (s, e) => UpdateOverlayChart();
            chkLabelsOverlay = new CheckBox
            {
                Text = "🏷 Etykiety punktów",
                AutoSize = true,
                Margin = new Padding(8, 8, 0, 0),
                Checked = false,
                Font = new Font("Segoe UI Emoji", 9F)
            };
            chkLabelsOverlay.CheckedChanged += (s, e) => UpdateOverlayChart();

            flow2.Controls.AddRange(new Control[] { chkOverlayMargin1, chkOverlayMargin2, chkLabelsOverlay, BuildAggregationButtonsRow() });

            controlPanel.Controls.Add(flow2);
            controlPanel.Controls.Add(flow1);

            // Wykres z dwiema osiami Y
            overlayChart = CreateEnhancedChart("Wykres Łączony — Zakup vs Sprzedaż");
            // Lewa Y - zakup (4-6 zł), Prawa Y - sprzedaż (7-9 zł)
            var ax = overlayChart.ChartAreas[0];
            ax.AxisY.Title = "Cena zakupu (PLN/kg)";
            ax.AxisY2.Title = "Cena sprzedaży / Marża (PLN/kg)";
            ax.AxisY2.TitleFont = ax.AxisY.TitleFont;
            ax.AxisY2.LabelStyle.Format = "#,##0.00 zł";
            ax.AxisY2.LabelStyle.Font = ax.AxisY.LabelStyle.Font;
            ax.AxisY2.LabelStyle.ForeColor = ax.AxisY.LabelStyle.ForeColor;
            ax.AxisY2.LineColor = Color.FromArgb(180, 180, 180);
            ax.AxisY2.MajorGrid.Enabled = false; // żeby się nie nakładało z lewą
            ax.AxisY2.Enabled = AxisEnabled.True;
            ax.AxisY2.IsStartedFromZero = false;

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(overlayChart, 0, 1);

            tab.Controls.Add(container);
        }

        private void UpdateOverlayChart()
        {
            if (overlayChart == null || filteredData == null || filteredData.Rows.Count == 0) return;

            overlayChart.Series.Clear();

            (CheckBox cb, string name, string col, Color color, bool secondaryY)[] purchaseConfigs =
            {
                (chkOverlayMin,    "Ministerialna",      "Minister",      Color.FromArgb(37, 99, 235),   false),
                (chkOverlayLacz,   "Łączona",            "Laczona",       Color.FromArgb(124, 58, 237),  false),
                (chkOverlayRoln,   "Rolnicza",           "Rolnicza",      Color.FromArgb(22, 163, 74),   false),
                (chkOverlayWolny,  "Wolnorynkowa",       "Wolnorynkowa",  Color.FromArgb(234, 179, 8),   false),
                (chkOverlaySrAll,  "Śr. Wszystkich Dostaw", "Śr. Wszystkich Dostaw", Color.FromArgb(100, 116, 139), false)
            };
            (CheckBox cb, string name, string col, Color color, bool secondaryY)[] salesConfigs =
            {
                (chkOverlayZrzesz, "Tuszka Zrzeszenia", "Tuszka Zrzeszenia", Color.FromArgb(234, 88, 12),  true),
                (chkOverlayNasza,  "Nasza Tuszka",      "Nasza Tuszka",      Color.FromArgb(13, 148, 136), true)
            };

            void AddPriceSeries((CheckBox cb, string name, string col, Color color, bool secondaryY) cfg)
            {
                if (cfg.cb?.Checked != true) return;
                if (!filteredData.Columns.Contains(cfg.col)) return;

                var s = CreateChartSeries(cfg.name, cfg.col, cfg.color);
                if (s == null || s.Points.Count == 0) return;
                if (cfg.secondaryY) s.YAxisType = AxisType.Secondary;

                if (chkLabelsOverlay?.Checked == true) ApplyAllPointLabels(s);
                else AddEndLabel(s);
                overlayChart.Series.Add(s);
            }

            foreach (var cfg in purchaseConfigs) AddPriceSeries(cfg);
            foreach (var cfg in salesConfigs) AddPriceSeries(cfg);

            // Marża 1: Tuszka Zrzeszenia − Rolnicza (na osi Y2 - to też w okolicach 2-4 zł, mniejsza skala)
            if (chkOverlayMargin1?.Checked == true
                && filteredData.Columns.Contains("Tuszka Zrzeszenia")
                && filteredData.Columns.Contains("Rolnicza"))
            {
                var diff = AggregateForChart("__overlay_m1", row =>
                {
                    if (row["Tuszka Zrzeszenia"] == DBNull.Value || row["Rolnicza"] == DBNull.Value) return null;
                    return (double)(Convert.ToDecimal(row["Tuszka Zrzeszenia"]) - Convert.ToDecimal(row["Rolnicza"]));
                });
                var s = new Series("Marża Zrzesz.−Roln.")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    BorderDashStyle = ChartDashStyle.Dash,
                    Color = Color.FromArgb(180, 234, 88, 12),
                    MarkerStyle = MarkerStyle.None,
                    YAxisType = AxisType.Secondary,
                    IsVisibleInLegend = true
                };
                foreach (var (date, value, _) in diff)
                    s.Points.AddXY(date, Math.Round(value, 2));
                if (s.Points.Count > 0)
                {
                    if (chkLabelsOverlay?.Checked == true) ApplyAllPointLabels(s);
                    else AddEndLabel(s);
                    overlayChart.Series.Add(s);
                }
            }

            // Marża 2: Nasza Tuszka − Średnia wszystkich
            if (chkOverlayMargin2?.Checked == true
                && filteredData.Columns.Contains("Nasza Tuszka")
                && filteredData.Columns.Contains("Śr. Wszystkich Dostaw"))
            {
                var diff = AggregateForChart("__overlay_m2", row =>
                {
                    if (row["Nasza Tuszka"] == DBNull.Value || row["Śr. Wszystkich Dostaw"] == DBNull.Value) return null;
                    return (double)(Convert.ToDecimal(row["Nasza Tuszka"]) - Convert.ToDecimal(row["Śr. Wszystkich Dostaw"]));
                });
                var s = new Series("Marża Nasza−Wszyst. pot.")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    BorderDashStyle = ChartDashStyle.Dash,
                    Color = Color.FromArgb(180, 124, 58, 237),
                    MarkerStyle = MarkerStyle.None,
                    YAxisType = AxisType.Secondary,
                    IsVisibleInLegend = true
                };
                foreach (var (date, value, _) in diff)
                    s.Points.AddXY(date, Math.Round(value, 2));
                if (s.Points.Count > 0)
                {
                    if (chkLabelsOverlay?.Checked == true) ApplyAllPointLabels(s);
                    else AddEndLabel(s);
                    overlayChart.Series.Add(s);
                }
            }

            AdjustChartScale(overlayChart);
        }

        // ═════════════════ SEZONOWOŚĆ (heatmap 7 × 52) ═════════════════

        private void CreateSezonowoscTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10, 5, 10, 5)
            };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            var lblPick = new Label
            {
                Text = "📊 Cena:",
                AutoSize = true,
                Margin = new Padding(0, 12, 8, 0),
                Font = new Font("Segoe UI Emoji", 9.25F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            sezonColumnSelector = new ComboBox
            {
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 9, 16, 0),
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat
            };
            sezonColumnSelector.Items.AddRange(new object[]
            {
                "Minister", "Laczona", "Rolnicza", "Wolnorynkowa",
                "Tuszka Zrzeszenia", "Nasza Tuszka",
                "Śr. Wszystkich Dostaw"
            });
            sezonColumnSelector.SelectedIndex = 5; // Nasza Tuszka domyślnie
            sezonColumnSelector.SelectedIndexChanged += (s, e) => sezonowoscPanel?.Invalidate();

            var lblHint = new Label
            {
                Text = "💡 Heatmap roczny: 7 dni × 52 tygodnie • każdy kafelek = 1 dzień (lata uśrednione)",
                AutoSize = true,
                Margin = new Padding(8, 12, 0, 0),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI Emoji", 8.5F, FontStyle.Italic)
            };

            flow.Controls.AddRange(new Control[] { lblPick, sezonColumnSelector, lblHint });
            controlPanel.Controls.Add(flow);

            sezonowoscPanel = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            sezonowoscPanel.Paint += SezonowoscPanel_Paint;
            sezonowoscPanel.Resize += (s, e) => sezonowoscPanel.Invalidate();
            sezonowoscPanel.MouseMove += SezonowoscPanel_MouseMove;
            sezonowoscPanel.MouseLeave += (s, e) =>
            {
                _sezonowoscTooltip?.Hide(sezonowoscPanel);
            };

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(sezonowoscPanel, 0, 1);

            tab.Controls.Add(container);
        }

        /// <summary>Buduje cache (DoY → średnia cena) per kolumna. Wywoływane raz po LoadDataAsync.</summary>
        private void BuildSezonowoscCache()
        {
            _sezonowoscCache.Clear();
            _sezonowoscRange.Clear();
            if (originalData == null) return;

            string[] cols = { "Minister", "Laczona", "Rolnicza", "Wolnorynkowa",
                              "Tuszka Zrzeszenia", "Nasza Tuszka", "Śr. Wszystkich Dostaw" };

            // Jeden przelot przez originalData zamiast osobno per kolumna
            var perCol = new Dictionary<string, Dictionary<int, (double Sum, int Count)>>();
            foreach (var c in cols)
                if (originalData.Columns.Contains(c))
                    perCol[c] = new Dictionary<int, (double, int)>();

            foreach (DataRow r in originalData.Rows)
            {
                if (r["DataRaw"] == DBNull.Value) continue;
                int doy = Convert.ToDateTime(r["DataRaw"]).DayOfYear;
                foreach (var c in perCol.Keys.ToList())
                {
                    if (r[c] == DBNull.Value) continue;
                    double v = (double)Convert.ToDecimal(r[c]);
                    if (perCol[c].TryGetValue(doy, out var agg))
                        perCol[c][doy] = (agg.Sum + v, agg.Count + 1);
                    else
                        perCol[c][doy] = (v, 1);
                }
            }

            foreach (var (c, agg) in perCol)
            {
                var dict = agg.ToDictionary(kv => kv.Key, kv => kv.Value.Sum / kv.Value.Count);
                _sezonowoscCache[c] = dict;
                if (dict.Count > 0)
                    _sezonowoscRange[c] = (dict.Values.Min(), dict.Values.Max());
            }
        }

        private void SezonowoscPanel_Paint(object sender, PaintEventArgs e)
        {
            try
            {
            if (!(sender is Panel panel)) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            string col = sezonColumnSelector?.SelectedItem?.ToString() ?? "Nasza Tuszka";
            if (originalData == null || !originalData.Columns.Contains(col))
            {
                using var emptyFont = new Font("Segoe UI", 11F, FontStyle.Italic);
                g.DrawString("Brak danych", emptyFont,
                    Brushes.Gray, panel.ClientRectangle, new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                return;
            }

            // Użyj cache zamiast LINQ-grupowania na każdy paint
            if (!_sezonowoscCache.TryGetValue(col, out var byDayOfYear)
                || byDayOfYear.Count == 0)
            {
                using var emptyFont = new Font("Segoe UI", 11F, FontStyle.Italic);
                g.DrawString("Brak danych dla tej kolumny", emptyFont,
                    Brushes.Gray, panel.ClientRectangle, new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                return;
            }

            var (minV, maxV) = _sezonowoscRange[col];
            double rangeV = Math.Max(0.01, maxV - minV);

            // Marginesy
            int leftMargin = 38;
            int topMargin = 30;
            int bottomMargin = 70;
            int rightMargin = 80;

            int availW = panel.Width - leftMargin - rightMargin;
            int availH = panel.Height - topMargin - bottomMargin;

            int cells = 53; // ISO weeks 1..53
            int cellW = Math.Max(8, availW / cells - 1);
            int cellH = Math.Max(14, availH / 7 - 1);

            // Zapamiętaj geometrię dla MouseMove
            _sezonGeometry = (leftMargin, topMargin, cellW, cellH);

            string[] dniSkrot = { "Pn", "Wt", "Śr", "Cz", "Pt", "Sb", "Nd" };
            using var labelFont = new Font("Segoe UI", 8F);
            using var labelBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
            using var monthFont = new Font("Segoe UI Semibold", 8F);

            // Tytuł
            using var titleFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
            g.DrawString($"Heatmap roczny: {col}    (zakres {minV:N2} → {maxV:N2} zł/kg)",
                titleFont, titleBrush, 12, 6);

            // Etykiety dni tygodnia (lewa)
            for (int dow = 0; dow < 7; dow++)
            {
                int y = topMargin + dow * (cellH + 1) + cellH / 2 - 6;
                g.DrawString(dniSkrot[dow], labelFont, labelBrush, 8, y);
            }

            // Dla każdego dnia roku (1..366) wylicz pozycję komórki i kolor
            var refYear = DateTime.Today.Year;
            for (int doy = 1; doy <= 366; doy++)
            {
                DateTime date;
                try { date = new DateTime(refYear, 1, 1).AddDays(doy - 1); }
                catch { continue; }
                if (date.Year != refYear) break; // 366 nie istnieje w nieprzestępnym roku

                int week = System.Globalization.ISOWeek.GetWeekOfYear(date);
                int dow = ((int)date.DayOfWeek + 6) % 7; // 0=Pn..6=Nd

                int x = leftMargin + (week - 1) * (cellW + 1);
                int y = topMargin + dow * (cellH + 1);

                Color cellBg;
                if (byDayOfYear.TryGetValue(doy, out var v))
                {
                    double t = (v - minV) / rangeV;
                    cellBg = SezonHeatColor(t);
                }
                else
                {
                    cellBg = Color.FromArgb(241, 245, 249); // brak danych - jasnoszary
                }
                using var cellBrush = new SolidBrush(cellBg);
                g.FillRectangle(cellBrush, x, y, cellW, cellH);

                // Subtelne ramki
                using var cellPen = new Pen(Color.FromArgb(245, 245, 245), 1);
                g.DrawRectangle(cellPen, x, y, cellW, cellH);
            }

            // Etykiety miesięcy (góra)
            for (int month = 1; month <= 12; month++)
            {
                var d = new DateTime(refYear, month, 1);
                int week = System.Globalization.ISOWeek.GetWeekOfYear(d);
                int x = leftMargin + (week - 1) * (cellW + 1);
                var monthName = d.ToString("MMM", new System.Globalization.CultureInfo("pl-PL"));
                g.DrawString(monthName, monthFont, labelBrush, x, topMargin - 18);
            }

            // Legenda (po prawej)
            int legendX = panel.Width - rightMargin + 8;
            int legendY = topMargin;
            int legendH = 7 * (cellH + 1);
            int legendW = 16;

            for (int i = 0; i < legendH; i++)
            {
                double t = 1.0 - (double)i / legendH;
                using var legendBrush = new SolidBrush(SezonHeatColor(t));
                g.FillRectangle(legendBrush, legendX, legendY + i, legendW, 1);
            }
            using var legendPen = new Pen(subtleBorderColor, 1);
            g.DrawRectangle(legendPen, legendX, legendY, legendW, legendH);
            g.DrawString($"{maxV:N2}", labelFont, labelBrush, legendX + legendW + 4, legendY - 4);
            g.DrawString($"{minV:N2}", labelFont, labelBrush, legendX + legendW + 4, legendY + legendH - 8);
            g.DrawString($"{(minV + maxV) / 2:N2}", labelFont, labelBrush, legendX + legendW + 4, legendY + legendH / 2 - 6);
            }
            catch { /* Paint nie może rzucać */ }
        }

        private void SezonowoscPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (sezonowoscPanel == null) return;
            var (lm, tm, cw, ch) = _sezonGeometry;
            if (cw == 0 || ch == 0) return;

            int x = e.X - lm;
            int y = e.Y - tm;
            if (x < 0 || y < 0) { _sezonowoscTooltip?.Hide(sezonowoscPanel); return; }

            int week = x / (cw + 1) + 1;
            int dow = y / (ch + 1);
            if (week < 1 || week > 53 || dow < 0 || dow > 6) { _sezonowoscTooltip?.Hide(sezonowoscPanel); return; }

            string col = sezonColumnSelector?.SelectedItem?.ToString() ?? "Nasza Tuszka";
            if (!_sezonowoscCache.TryGetValue(col, out var byDoy)) return;

            // Mapowanie (week, dow) → DayOfYear w roku referencyjnym
            int refYear = DateTime.Today.Year;
            DateTime jan1 = new DateTime(refYear, 1, 1);
            // Iteracja ~370 dni żeby znaleźć ten DoY który ma tę kombinację (week, dow)
            DateTime? targetDate = null;
            for (int doy = 1; doy <= 366; doy++)
            {
                DateTime d;
                try { d = jan1.AddDays(doy - 1); } catch { break; }
                if (d.Year != refYear) break;
                int w = System.Globalization.ISOWeek.GetWeekOfYear(d);
                int dw = ((int)d.DayOfWeek + 6) % 7;
                if (w == week && dw == dow)
                {
                    targetDate = d;
                    break;
                }
            }
            if (!targetDate.HasValue) { _sezonowoscTooltip?.Hide(sezonowoscPanel); return; }

            var d2 = targetDate.Value;
            string tip;
            if (byDoy.TryGetValue(d2.DayOfYear, out double v))
                tip = $"{d2:dd.MM} ({d2:dddd})  •  Tydz {week}\n{col}: {v:N2} zł/kg (śr. wszystkich lat)";
            else
                tip = $"{d2:dd.MM} ({d2:dddd})  •  Tydz {week}\nBrak danych";

            if (_sezonowoscTooltip == null)
                _sezonowoscTooltip = new ToolTip { ShowAlways = true, AutoPopDelay = 8000, InitialDelay = 100, ReshowDelay = 100 };
            _sezonowoscTooltip.Show(tip, sezonowoscPanel, e.X + 14, e.Y + 18, 8000);
        }

        private static Color SezonHeatColor(double t)
        {
            // 0 → niebieski (zimno/tanio), 0.5 → biały, 1 → czerwony (gorąco/drogo)
            t = Math.Max(0, Math.Min(1, t));
            if (t < 0.5)
            {
                double k = t * 2; // 0..1
                int r = (int)(220 + 35 * k);
                int g = (int)(235 + 20 * k);
                int b = 255;
                return Color.FromArgb(r, g, b);
            }
            else
            {
                double k = (t - 0.5) * 2;
                int r = 255;
                int g = (int)(255 - 100 * k);
                int b = (int)(255 - 180 * k);
                return Color.FromArgb(r, g, b);
            }
        }

        // ═════════════════ KLIENCI TOP-N (Symfonia Handel 192.168.0.112) ═════════════════

        // ═════════════════ TYGODNIE - analiza dni tygodnia per cena ═════════════════

        // Hero KPI labels for Tygodnie - aktualizowane w UpdateTygodnieView
        private Label _tygHeroSelected;
        private Label _tygHeroSelectedValue;
        private Label _tygHeroSelectedSub;
        private Label _tygHeroBestDay;
        private Label _tygHeroBestValue;
        private Label _tygHeroBestSub;
        private Label _tygHeroWorstDay;
        private Label _tygHeroWorstValue;
        private Label _tygHeroWorstSub;

        private void CreateTygodnieTab(TabPage tab)
        {
            // Layout: 3 rzędy - top controls, hero strip, dolny split (chart 65% | table 35%)
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = backgroundColor
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));    // controls
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));   // hero strip
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // chart + table side by side

            // ROW 1 — Controls panel (z headerem i help button)
            var controls = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 0, 20, 0)
            };
            controls.Paint += (s, e) =>
            {
                using var pen = new Pen(subtleBorderColor, 1);
                e.Graphics.DrawLine(pen, 0, controls.Height - 1, controls.Width, controls.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "🗓  Analiza dni tygodnia",
                Font = new Font("Segoe UI Emoji", 14F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false,
                Width = 280,
                Height = 60,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(0, 0)
            };

            var lblPick = new Label
            {
                Text = "📊 Cena:",
                Font = new Font("Segoe UI Emoji", 10F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(290, 22),
                AutoSize = true
            };
            tygodnieColumnSelector = new ComboBox
            {
                Width = 240,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5F),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(360, 18)
            };
            tygodnieColumnSelector.Items.AddRange(new object[]
            {
                "Nasza Tuszka",          // domyślna - najważniejsza dla biznesu
                "Wolnorynkowa",
                "Rolnicza",
                "Minister",
                "Laczona",
                "Tuszka Zrzeszenia",
                "Śr. Wszystkich Dostaw"
            });
            tygodnieColumnSelector.SelectedIndex = 0; // = Nasza Tuszka
            tygodnieColumnSelector.SelectedIndexChanged += (s, e) => UpdateTygodnieView();

            tygodnieIncludeWeekends = new CheckBox
            {
                Text = "🗓 Uwzględnij weekendy (Sb/Nd)",
                Checked = false,
                AutoSize = true,
                Font = new Font("Segoe UI Emoji", 9.5F),
                Location = new Point(620, 22)
            };
            tygodnieIncludeWeekends.CheckedChanged += (s, e) => UpdateTygodnieView();

            // Help button po prawej
            var helpBtn = new Button
            {
                Text = "?",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Size = new Size(32, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = primaryColor,
                Cursor = Cursors.Hand,
                TabStop = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            helpBtn.Location = new Point(controls.Width - 50, 14);
            helpBtn.FlatAppearance.BorderColor = primaryColor;
            helpBtn.FlatAppearance.BorderSize = 1;
            helpBtn.FlatAppearance.MouseOverBackColor = primaryColor;
            helpBtn.MouseEnter += (s, e) => helpBtn.ForeColor = Color.White;
            helpBtn.MouseLeave += (s, e) => helpBtn.ForeColor = primaryColor;
            helpBtn.Click += (s, e) => ShowHelpDialog("Tygodnie - analiza dni tygodnia",
                "Zakładka pomaga sprawdzić jak wybrana cena zachowuje się w poszczególne dni tygodnia.\n\n" +
                "🎯 PRZYKŁAD UŻYCIA:\n" +
                "Wybierz 'Wolnorynkowa' i okres ostatnich 5 miesięcy.\n" +
                "Patrząc na wykres możesz porównać czy WTOREK jest tańszy od PIĄTKU - jeśli tak, planuj zakupy żywca na wtorki!\n\n" +
                "📊 ELEMENTY:\n" +
                "• Wybór ceny — 7 dostępnych kolumn (Wolnorynkowa, Rolnicza, Min, ...)\n" +
                "• Toggle 'Uwzględnij weekendy' — domyślnie Pn-Pt (rzadziej kupuje się w weekendy)\n" +
                "• 3 karty Hero — Średnia ogółem, Najtańszy dzień, Najdroższy dzień\n" +
                "• Wykres słupkowy:\n" +
                "    🟢 zielony słupek — najtańszy dzień tygodnia\n" +
                "    🔴 czerwony — najdroższy\n" +
                "    🔵 niebieskie — pozostałe\n" +
                "    pionowa biała linia w słupku = zakres min‑max dla danego dnia\n" +
                "    pomarańczowa linia przerywana = średnia ogółem\n" +
                "• Tabela szczegółów — n, średnia, min, max, mediana, odchylenie, vs śr. ogółem\n\n" +
                "💡 BIZNESOWO:\n" +
                "Jeśli zauważysz powtarzalny wzorzec (np. wtorki o 20gr taniej) → planuj zakupy w te dni.\n" +
                "Przy 200 t/dzień różnica 0,20 zł/kg = 40 000 zł oszczędności dziennie.");

            controls.Resize += (s, e) => helpBtn.Location = new Point(controls.Width - 50, 14);

            controls.Controls.AddRange(new Control[] { lblTitle, lblPick, tygodnieColumnSelector, tygodnieIncludeWeekends, helpBtn });
            container.Controls.Add(controls, 0, 0);

            // ROW 2 — HERO STRIP (3 karty)
            var heroStrip = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = backgroundColor,
                Padding = new Padding(16, 12, 16, 6)
            };
            heroStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            heroStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            heroStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

            // Karta 1: Wybrana cena
            var heroSelected = BuildTygodnieHeroCard(primaryColor, "📊", "WYBRANA CENA",
                out _tygHeroSelected, out _tygHeroSelectedValue, out _tygHeroSelectedSub);
            heroStrip.Controls.Add(heroSelected, 0, 0);

            // Karta 2: Najlepszy (zielony - dla zakupu = najtańszy, dla sprzedaży = najdroższy)
            var heroBest = BuildTygodnieHeroCard(successColor, "✓", "NAJLEPSZY DZIEŃ",
                out _tygHeroBestDay, out _tygHeroBestValue, out _tygHeroBestSub);
            heroStrip.Controls.Add(heroBest, 1, 0);

            // Karta 3: Najgorszy (czerwony - dla zakupu = najdroższy, dla sprzedaży = najtańszy)
            var heroWorst = BuildTygodnieHeroCard(dangerColor, "✗", "NAJGORSZY DZIEŃ",
                out _tygHeroWorstDay, out _tygHeroWorstValue, out _tygHeroWorstSub);
            heroStrip.Controls.Add(heroWorst, 2, 0);

            container.Controls.Add(heroStrip, 0, 1);

            // (zachowujemy summary label dla kompatybilności ze starym kodem ale ukryty)
            tygodnieSummaryLabel = new Label { Visible = false };

            // ROW 3 — Split: Chart (lewa, 64%) + Tabela (prawa, 36%)
            var bottomSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = backgroundColor,
                Padding = new Padding(16, 6, 16, 12)
            };
            bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
            container.Controls.Add(bottomSplit, 0, 2);

            // CHART (lewa kolumna) — sam rysuje rounded background
            tygodnieChartPanel = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 8, 0)
            };
            tygodnieChartPanel.Paint += TygodnieChart_Paint;
            tygodnieChartPanel.Resize += (s, e) => tygodnieChartPanel.Invalidate();
            bottomSplit.Controls.Add(tygodnieChartPanel, 0, 0);

            // TABLE (prawa kolumna) - z headerem nad tabelą
            var tableContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.White,
                Margin = new Padding(8, 0, 0, 0)
            };
            tableContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            tableContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tableContainer.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, tableContainer.Width - 1, tableContainer.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.White);
                e.Graphics.FillPath(bg, path);
                using var border = new Pen(subtleBorderColor, 1);
                e.Graphics.DrawPath(border, path);
            };

            var tableHeader = new Label
            {
                Text = "📋  Szczegóły per dzień tygodnia",
                Font = new Font("Segoe UI Emoji", 11F),
                ForeColor = Color.FromArgb(15, 23, 42),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.Transparent
            };
            tableContainer.Controls.Add(tableHeader, 0, 0);

            tygodnieTable = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(241, 245, 249),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 30 },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 250, 252),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(248, 250, 252),
                    SelectionForeColor = Color.FromArgb(71, 85, 105),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(4)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(15, 23, 42),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(241, 245, 249),
                    SelectionForeColor = Color.FromArgb(15, 23, 42),
                    Padding = new Padding(2)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(252, 253, 255)
                }
            };
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, tygodnieTable, new object[] { true });
            tygodnieTable.CellFormatting += TygodnieTable_CellFormatting;
            tableContainer.Controls.Add(tygodnieTable, 0, 1);

            bottomSplit.Controls.Add(tableContainer, 1, 0);

            tab.Controls.Add(container);
        }

        // Cache najtańszego/najdroższego dnia dla CellFormatting
        private string _tygodnieBestDayName = "";
        private string _tygodnieWorstDayName = "";

        // Ceny sprzedażowe - dla nich kolory ODWRÓCONE (najtańszy=czerwony, najdroższy=zielony)
        private static readonly HashSet<string> _salesColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "Nasza Tuszka",
            "Tuszka Zrzeszenia"
        };

        /// <summary>Color coding tabeli Tygodnie - kolorowane wiersze dla najtańszego/najdroższego dnia + Vs średnia.</summary>
        private void TygodnieTable_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var col = tygodnieTable.Columns[e.ColumnIndex];
                var row = tygodnieTable.Rows[e.RowIndex];
                var dayName = row.Cells[0].Value?.ToString() ?? "";
                bool isBest = dayName == _tygodnieBestDayName;
                bool isWorst = dayName == _tygodnieWorstDayName;

                // Tło wiersza - jasnozielone dla najtańszego, jasnoróżowe dla najdroższego
                if (isBest)
                {
                    e.CellStyle.BackColor = Color.FromArgb(220, 252, 231);   // zielony pastelowy
                    e.CellStyle.SelectionBackColor = Color.FromArgb(187, 247, 208);
                }
                else if (isWorst)
                {
                    e.CellStyle.BackColor = Color.FromArgb(254, 226, 226);   // czerwony pastelowy
                    e.CellStyle.SelectionBackColor = Color.FromArgb(254, 202, 202);
                }

                // Pierwsza kolumna (nazwa dnia) - semantyczna ikona ✓ (dobry) / ✗ (zły)
                if (col.Name == "Dzień")
                {
                    if (isBest)
                    {
                        e.Value = "✓ " + dayName;
                        e.FormattingApplied = true;
                        e.CellStyle.ForeColor = Color.FromArgb(22, 101, 52); // ciemnozielony
                        e.CellStyle.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                    }
                    else if (isWorst)
                    {
                        e.Value = "✗ " + dayName;
                        e.FormattingApplied = true;
                        e.CellStyle.ForeColor = Color.FromArgb(127, 29, 29); // ciemnoczerwony
                        e.CellStyle.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                    }
                    else
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(15, 23, 42);
                        e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    }
                }

                // Vs średnia ogółem - zielony jeśli niżej (taniej), czerwony jeśli wyżej
                if (col.Name == "Vs średnia ogółem (zł)" && e.Value is decimal d)
                {
                    e.CellStyle.ForeColor = d < 0 ? successColor : (d > 0 ? dangerColor : Color.Gray);
                    e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }

                // Średnia w kolumnie - większa
                if (col.Name == "Średnia" && e.Value is decimal)
                {
                    if (isBest) e.CellStyle.ForeColor = Color.FromArgb(22, 101, 52);
                    else if (isWorst) e.CellStyle.ForeColor = Color.FromArgb(127, 29, 29);
                    else e.CellStyle.ForeColor = Color.FromArgb(15, 23, 42);
                    e.CellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                }

                // Liczba dni - subtelnie wyróżnione
                if (col.Name == "Liczba dni" && e.Value != DBNull.Value)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(71, 85, 105);
                    e.CellStyle.Font = new Font("Segoe UI", 9.5F);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
        }

        /// <summary>Tworzy hero card dla zakładki Tygodnie z 3 etykietami (zwracane przez out).</summary>
        private Panel BuildTygodnieHeroCard(Color accent, string icon, string label,
            out Label outBig, out Label outValue, out Label outSub)
        {
            var card = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 6, 0),
                BackColor = Color.White
            };

            card.Paint += (s, e) =>
            {
                try
                {
                    if (card.Width < 4 || card.Height < 4) return;
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    using var bg = new SolidBrush(Color.White);
                    e.Graphics.FillPath(bg, path);

                    // Pasek akcentu z lewej (4 px)
                    e.Graphics.SetClip(path);
                    using var accentBrush = new SolidBrush(accent);
                    e.Graphics.FillRectangle(accentBrush, 0, 0, 4, card.Height);
                    e.Graphics.ResetClip();

                    using var border = new Pen(subtleBorderColor, 1);
                    e.Graphics.DrawPath(border, path);
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            // Kompaktowy layout - całość mieści się w ~100 px (większe odstępy żeby tekst się nie zasłaniał)
            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 18F),
                ForeColor = accent,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 40,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(12, 12)
            };

            var lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 220,
                Height = 14,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(58, 8)
            };

            // Nazwa wybranej ceny / dnia - większy odstęp od labelu nad
            // Font Segoe UI Emoji (Regular) - renderuje zarówno tekst jak i emoji prawidłowo
            var lblBig = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI Emoji", 11F),
                ForeColor = accent,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 280,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(58, 26)
            };

            // Duża wartość - z większym oddechem
            var lblValue = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 220,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(56, 50)
            };

            var lblSub = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI Emoji", 8.5F),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 280,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(58, 86)
            };

            card.Controls.AddRange(new Control[] { lblIcon, lblLabel, lblBig, lblValue, lblSub });
            outBig = lblBig;
            outValue = lblValue;
            outSub = lblSub;
            return card;
        }

        private string GetTygodnieColumn() =>
            tygodnieColumnSelector?.SelectedItem?.ToString() ?? "Wolnorynkowa";

        /// <summary>Wylicza statystyki per dzień tygodnia dla wybranej kolumny.</summary>
        private List<(int Dow, string Name, List<decimal> Values)> ComputeTygodnieStats()
        {
            var list = new List<(int Dow, string Name, List<decimal> Values)>();
            string[] dniSkrot = { "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota", "Niedziela" };
            for (int i = 0; i < 7; i++)
                list.Add((i, dniSkrot[i], new List<decimal>()));

            string col = GetTygodnieColumn();
            if (filteredData == null || !filteredData.Columns.Contains(col)) return list;

            foreach (DataRow r in filteredData.Rows)
            {
                if (r["DataRaw"] == DBNull.Value || r[col] == DBNull.Value) continue;
                var date = Convert.ToDateTime(r["DataRaw"]).Date;
                int dow = ((int)date.DayOfWeek + 6) % 7; // 0=Pn..6=Nd
                list[dow].Values.Add(Convert.ToDecimal(r[col]));
            }

            return list;
        }

        public void UpdateTygodnieView()
        {
            if (tygodnieChartPanel == null) return;

            string col = GetTygodnieColumn();
            var stats = ComputeTygodnieStats();
            var includeWk = tygodnieIncludeWeekends?.Checked ?? false;
            var visibleStats = includeWk ? stats : stats.Where(s => s.Dow < 5).ToList();

            int totalDays = visibleStats.Sum(s => s.Values.Count);
            var withData = visibleStats.Where(s => s.Values.Count > 0).ToList();
            bool isSales = _salesColumns.Contains(col);

            // Cache nazw najtańszego/najdroższego dnia dla CellFormatting tabeli
            // _tygodnieBestDayName = "ZIELONY" wiersz (semantycznie dobry), WorstDay = "CZERWONY"
            if (withData.Any())
            {
                if (isSales)
                {
                    // Dla sprzedaży: NAJWYŻSZA cena = dobra (zielona), NAJNIŻSZA = zła (czerwona)
                    _tygodnieBestDayName = withData.OrderByDescending(s => s.Values.Average()).First().Name;
                    _tygodnieWorstDayName = withData.OrderBy(s => s.Values.Average()).First().Name;
                }
                else
                {
                    // Dla zakupu: NAJNIŻSZA cena = dobra (zielona), NAJWYŻSZA = zła (czerwona)
                    _tygodnieBestDayName = withData.OrderBy(s => s.Values.Average()).First().Name;
                    _tygodnieWorstDayName = withData.OrderByDescending(s => s.Values.Average()).First().Name;
                }
            }
            else
            {
                _tygodnieBestDayName = "";
                _tygodnieWorstDayName = "";
            }

            // Wypełnij 3 hero cards
            if (_tygHeroSelected != null)
            {
                if (withData.Any())
                {
                    decimal overallAvg = withData.SelectMany(s => s.Values).Average();
                    decimal overallMin = withData.SelectMany(s => s.Values).Min();
                    decimal overallMax = withData.SelectMany(s => s.Values).Max();

                    // Card 1: Wybrana cena
                    // lblBig: tylko nazwa ceny (czysty tekst, bez emoji)
                    // lblSub: opis z emoji (Segoe UI Emoji Regular - renderuje emoji)
                    _tygHeroSelected.Text = col;
                    _tygHeroSelectedValue.Text = $"{overallAvg:N2} zł";
                    string priceTypeHint = isSales ? "💰 cena sprzedaży" : "🌾 cena zakupu";
                    _tygHeroSelectedSub.Text = $"{priceTypeHint}  •  📅 {totalDays} dni  •  zakres {overallMin:N2}–{overallMax:N2}";

                    // Card 2: NAJLEPSZY (semantycznie korzystny)
                    var best = isSales
                        ? withData.OrderByDescending(s => s.Values.Average()).First()
                        : withData.OrderBy(s => s.Values.Average()).First();
                    decimal bestAvg = best.Values.Average();
                    decimal bestDiff = bestAvg - overallAvg;
                    string bestRole = isSales ? "💰 sprzedajemy drogo" : "💰 kupujemy tanio";
                    // lblBig: tylko dzień tygodnia (kolor akcentu z karty)
                    _tygHeroBestDay.Text = best.Name;
                    _tygHeroBestValue.Text = $"{bestAvg:N2} zł";
                    // lblSub: rola + diff vs średnia (z emoji - Segoe UI Emoji renderuje)
                    _tygHeroBestSub.Text = $"{bestRole}  •  {bestDiff:+0.00;-0.00;0.00} zł vs śr.  •  n = {best.Values.Count}";

                    // Card 3: NAJGORSZY
                    var worst = isSales
                        ? withData.OrderBy(s => s.Values.Average()).First()
                        : withData.OrderByDescending(s => s.Values.Average()).First();
                    decimal worstAvg = worst.Values.Average();
                    decimal worstDiff = worstAvg - overallAvg;
                    string worstRole = isSales ? "⚠ sprzedajemy tanio" : "⚠ kupujemy drogo";
                    _tygHeroWorstDay.Text = worst.Name;
                    _tygHeroWorstValue.Text = $"{worstAvg:N2} zł";
                    _tygHeroWorstSub.Text = $"{worstRole}  •  {worstDiff:+0.00;-0.00;0.00} zł vs śr.  •  n = {worst.Values.Count}";
                }
                else
                {
                    _tygHeroSelected.Text = col;
                    _tygHeroSelectedValue.Text = "—";
                    _tygHeroSelectedSub.Text = "Brak danych w okresie";
                    _tygHeroBestDay.Text = "—";
                    _tygHeroBestValue.Text = "—";
                    _tygHeroBestSub.Text = "Brak danych";
                    _tygHeroWorstDay.Text = "—";
                    _tygHeroWorstValue.Text = "—";
                    _tygHeroWorstSub.Text = "Brak danych";
                }
            }

            // Update table
            UpdateTygodnieTable(visibleStats);

            // Repaint chart
            tygodnieChartPanel.Invalidate();
        }

        private void UpdateTygodnieTable(List<(int Dow, string Name, List<decimal> Values)> stats)
        {
            var dt = new DataTable();
            dt.Columns.Add("Dzień", typeof(string));
            dt.Columns.Add("Liczba dni", typeof(int));
            dt.Columns.Add("Średnia", typeof(decimal));
            dt.Columns.Add("Min", typeof(decimal));
            dt.Columns.Add("Max", typeof(decimal));
            dt.Columns.Add("Mediana", typeof(decimal));
            dt.Columns.Add("Odch.std", typeof(decimal));
            dt.Columns.Add("Vs średnia ogółem (zł)", typeof(decimal));

            decimal overallAvg = stats.Any(s => s.Values.Count > 0)
                ? stats.SelectMany(s => s.Values).Average()
                : 0;

            foreach (var (dow, name, values) in stats)
            {
                if (values.Count == 0)
                {
                    dt.Rows.Add(name, 0, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
                }
                else
                {
                    var avg = values.Average();
                    dt.Rows.Add(name, values.Count,
                        Math.Round(avg, 2),
                        Math.Round(values.Min(), 2),
                        Math.Round(values.Max(), 2),
                        Math.Round(CalculateMedian(values), 2),
                        Math.Round(CalculateStandardDeviation(values), 2),
                        Math.Round(avg - overallAvg, 2));
                }
            }

            tygodnieTable.DataSource = dt;
            foreach (DataGridViewColumn c in tygodnieTable.Columns)
            {
                c.SortMode = DataGridViewColumnSortMode.NotSortable;
                if (c.ValueType == typeof(decimal))
                    c.DefaultCellStyle.Format = "0.00 zł";
                if (c.Name == "Liczba dni" || c.Name == "Dzień")
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                else
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        private void TygodnieChart_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (!(sender is Panel panel)) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Rounded white card background z subtelnym obramowaniem
                var cardRect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (var cardPath = GetRoundedRectangle(cardRect, 10))
                {
                    using var bgBrush = new SolidBrush(Color.White);
                    g.FillPath(bgBrush, cardPath);
                    using var borderPen = new Pen(subtleBorderColor, 1);
                    g.DrawPath(borderPen, cardPath);
                }

                string col = GetTygodnieColumn();
                bool isSales = _salesColumns.Contains(col);
                var stats = ComputeTygodnieStats();
                var includeWk = tygodnieIncludeWeekends?.Checked ?? false;
                var visibleStats = includeWk ? stats : stats.Where(s => s.Dow < 5).ToList();

                if (!visibleStats.Any(s => s.Values.Count > 0))
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 14F, FontStyle.Italic);
                    using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString($"📊  Brak danych dla '{col}' w wybranym okresie\nRozszerz zakres dat lub wybierz inną cenę.",
                        emptyFont, grayBrush, panel.ClientRectangle,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                // Większe marginesy + miejsce na większe nazwy dni i daty
                int padding = 28;
                int chartLeft = padding + 64;
                int chartRight = panel.Width - padding - 16;
                int chartTop = padding + 36;
                int chartBottom = panel.Height - padding - 60;
                int chartWidth = chartRight - chartLeft;
                int chartHeight = chartBottom - chartTop;

                // Skala Y
                var allValues = visibleStats.SelectMany(s => s.Values).ToList();
                decimal minV = allValues.Min();
                decimal maxV = allValues.Max();
                decimal margin = (maxV - minV) * 0.15m;
                if (margin == 0) margin = 0.5m;
                decimal yMin = Math.Max(0, minV - margin);
                decimal yMax = maxV + margin;
                decimal yRange = yMax - yMin;
                if (yRange == 0) yRange = 1;

                int YForValue(decimal v) => chartTop + (int)((double)((yMax - v) / yRange) * chartHeight);

                // Tytuł chartu (większy)
                using var titleFont = new Font("Segoe UI Emoji", 12F);
                using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                string priceTypeIcon = isSales ? "💰" : "🌾";
                string priceTypeLabel = isSales ? "(cena sprzedaży)" : "(cena zakupu)";
                g.DrawString($"📊  Średnia '{col}' per dzień tygodnia  {priceTypeIcon} {priceTypeLabel}",
                    titleFont, titleBrush, chartLeft, padding - 6);

                // Legenda - rysowana ręcznie z kolorowymi kółkami (nie polega na renderowaniu emoji)
                using var legendFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var legendTextBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                int lgX = chartLeft;
                int lgY = padding + 18;
                int dotSize = 12;

                // Kółko 1: najlepszy (zielony dla zakupu, zielony dla sprzedaży gdy najdroższy)
                string bestText = isSales ? "najdroższy = sprzedajemy drogo" : "najtańszy = kupujemy tanio";
                using (var dotBrush1 = new SolidBrush(successColor))
                    g.FillEllipse(dotBrush1, lgX, lgY + 1, dotSize, dotSize);
                g.DrawString("✓ " + bestText, legendFont, legendTextBrush, lgX + dotSize + 6, lgY);
                var size1 = g.MeasureString("✓ " + bestText, legendFont);
                lgX += dotSize + 6 + (int)size1.Width + 24;

                // Kółko 2: najgorszy (czerwony dla zakupu, czerwony dla sprzedaży gdy najtańszy)
                string worstText = isSales ? "najtańszy = sprzedajemy tanio" : "najdroższy = kupujemy drogo";
                using (var dotBrush2 = new SolidBrush(dangerColor))
                    g.FillEllipse(dotBrush2, lgX, lgY + 1, dotSize, dotSize);
                g.DrawString("✗ " + worstText, legendFont, legendTextBrush, lgX + dotSize + 6, lgY);
                var size2 = g.MeasureString("✗ " + worstText, legendFont);
                lgX += dotSize + 6 + (int)size2.Width + 24;

                // Kółko 3: pozostałe
                using (var dotBrush3 = new SolidBrush(primaryColor))
                    g.FillEllipse(dotBrush3, lgX, lgY + 1, dotSize, dotSize);
                g.DrawString("pozostałe", legendFont, legendTextBrush, lgX + dotSize + 6, lgY);

                // Grid + osie Y
                using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);
                using var axisFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                for (int i = 0; i <= 5; i++)
                {
                    decimal v = yMin + yRange * i / 5;
                    int y = YForValue(v);
                    g.DrawLine(gridPen, chartLeft, y, chartRight, y);
                    g.DrawString($"{v:N2}", axisFont, axisBrush, padding - 4, y - 8);
                }

                // Linia osi Y (pionowa) i osi X (pozioma)
                using var axisLinePen = new Pen(Color.FromArgb(200, 210, 224), 1.5f);
                g.DrawLine(axisLinePen, chartLeft, chartTop, chartLeft, chartBottom);
                g.DrawLine(axisLinePen, chartLeft, chartBottom, chartRight, chartBottom);

                // Średnia ogółem - przerywana pomarańczowa linia
                decimal overallAvg = allValues.Average();
                int yOverall = YForValue(overallAvg);
                using var overallPen = new Pen(Color.FromArgb(220, 234, 88, 12), 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(overallPen, chartLeft, yOverall, chartRight, yOverall);
                using var overallBrush = new SolidBrush(Color.FromArgb(234, 88, 12));
                using var overallLabelFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                string overallText = $"⚖ średnia ogółem  {overallAvg:N2} zł";
                var overallSize = g.MeasureString(overallText, overallLabelFont);
                // Plakietka z białym tłem przy linii
                var overallRect = new RectangleF(chartRight - overallSize.Width - 16, yOverall - 12, overallSize.Width + 12, 24);
                using var overallBgBrush = new SolidBrush(Color.White);
                g.FillRectangle(overallBgBrush, overallRect);
                using var overallBorderPen = new Pen(Color.FromArgb(234, 88, 12), 1);
                g.DrawRectangle(overallBorderPen, overallRect.X, overallRect.Y, overallRect.Width, overallRect.Height);
                g.DrawString(overallText, overallLabelFont, overallBrush, overallRect.X + 6, overallRect.Y + 4);

                // Słupki per dzień tygodnia - dużo większe + lepszy gap
                int dayCount = visibleStats.Count;
                float barW = (float)chartWidth / dayCount;
                float barGap = barW * 0.22f;
                float actualBarW = barW - barGap;

                using var valueFont = new Font("Segoe UI", 14F, FontStyle.Bold);
                using var smallFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var dowFont = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold);
                var labelFormat = new System.Globalization.CultureInfo("pl-PL").NumberFormat;

                var withData = visibleStats.Where(s => s.Values.Count > 0).ToList();
                // bestDow = z NAJNIŻSZĄ średnią ceną
                // worstDow = z NAJWYŻSZĄ średnią ceną
                int bestDow = withData.Any() ? withData.OrderBy(s => s.Values.Average()).First().Dow : -1;
                int worstDow = withData.Any() ? withData.OrderByDescending(s => s.Values.Average()).First().Dow : -1;

                // Dla cen SPRZEDAŻOWYCH: niska cena = ŹLE (czerwony), wysoka = DOBRZE (zielony) — odwracamy
                Color goodColor = isSales ? dangerColor : successColor;
                Color goodColorTop = isSales ? Color.FromArgb(248, 113, 113) : Color.FromArgb(34, 197, 94);
                Color goodColorBottom = isSales ? Color.FromArgb(220, 38, 38) : Color.FromArgb(22, 163, 74);
                Color badColor = isSales ? successColor : dangerColor;
                Color badColorTop = isSales ? Color.FromArgb(34, 197, 94) : Color.FromArgb(248, 113, 113);
                Color badColorBottom = isSales ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);

                for (int idx = 0; idx < dayCount; idx++)
                {
                    var (dow, name, values) = visibleStats[idx];
                    bool hasData = values.Count > 0;
                    decimal avg = hasData ? values.Average() : 0;

                    int x = chartLeft + (int)(idx * barW + barGap / 2);
                    int yVal = YForValue(avg);
                    int barHeight = Math.Abs(yVal - YForValue(yMin));
                    int barY = yVal;

                    Color barColorTop, barColorBottom;
                    if (!hasData)
                    {
                        barColorTop = Color.FromArgb(241, 245, 249);
                        barColorBottom = Color.FromArgb(241, 245, 249);
                    }
                    else if (dow == bestDow)
                    {
                        // bestDow = NAJNIŻSZA cena. Dla zakupowych = dobre (zielony). Dla sprzedażowych = złe (czerwony).
                        barColorTop = goodColorTop;
                        barColorBottom = goodColorBottom;
                    }
                    else if (dow == worstDow)
                    {
                        // worstDow = NAJWYŻSZA cena. Dla zakupowych = złe (czerwony). Dla sprzedażowych = dobre (zielony).
                        barColorTop = badColorTop;
                        barColorBottom = badColorBottom;
                    }
                    else
                    {
                        barColorTop = Color.FromArgb(96, 165, 250);
                        barColorBottom = Color.FromArgb(37, 99, 235);
                    }

                    if (hasData && barHeight > 0)
                    {
                        var barRect = new Rectangle(x, barY, (int)actualBarW, Math.Max(1, barHeight));
                        using var gradBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                            barRect, barColorTop, barColorBottom,
                            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                        g.FillRectangle(gradBrush, barRect);

                        // (Whisker usunięty - prosta linia min/max nie jest tu potrzebna)

                        // Wartość średnia w środku słupka (duża, biała) lub pod słupkiem (granat)
                        string vText = avg.ToString("N2", labelFormat) + " zł";
                        var vSize = g.MeasureString(vText, valueFont);
                        float vX = x + actualBarW / 2 - vSize.Width / 2;
                        if (barHeight >= vSize.Height + 12)
                        {
                            float vY = barY + 10;
                            g.DrawString(vText, valueFont, Brushes.White, vX, vY);
                        }
                        else
                        {
                            float vY = barY - vSize.Height - 2;
                            using var darkBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                            g.DrawString(vText, valueFont, darkBrush, vX, vY);
                        }
                    }

                    // Etykieta dnia tygodnia (kolorowana wg semantyki - dobry/zły dla danej kategorii ceny)
                    string dowLabel = name;
                    var dowSize = g.MeasureString(dowLabel, dowFont);
                    float dowX = x + actualBarW / 2 - dowSize.Width / 2;
                    Color dowColor = !hasData ? Color.FromArgb(148, 163, 184)
                                  : (dow == bestDow ? goodColor
                                  : (dow == worstDow ? badColor
                                  : Color.FromArgb(15, 23, 42)));
                    using var dowBrush = new SolidBrush(dowColor);
                    g.DrawString(dowLabel, dowFont, dowBrush, dowX, chartBottom + 6);

                    // Liczba dni pod nazwą + ikonka i kolor (semantycznie)
                    if (hasData)
                    {
                        string prefix = dow == bestDow ? (isSales ? "📉 " : "🥇 ")
                                       : dow == worstDow ? (isSales ? "🥇 " : "📉 ")
                                       : "";
                        string countLabel = $"{prefix}n = {values.Count}";
                        var countSize = g.MeasureString(countLabel, smallFont);
                        float countX = x + actualBarW / 2 - countSize.Width / 2;
                        Color countColor = dow == bestDow ? goodColor
                                          : dow == worstDow ? badColor
                                          : Color.FromArgb(100, 116, 139);
                        using var countBrush = new SolidBrush(countColor);
                        g.DrawString(countLabel, smallFont, countBrush, countX, chartBottom + 28);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
        }

        // ═════════════════ YoY (rok do roku) ═════════════════

        private void CreateYoYTab(TabPage tab)
        {
            var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = backgroundColor };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));  // 2-row controls
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var controls = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 0, 20, 0) };
            controls.Paint += (s, e) => { using var p = new Pen(subtleBorderColor, 1); e.Graphics.DrawLine(p, 0, controls.Height - 1, controls.Width, controls.Height - 1); };

            // === ROW 1: Tytuł + Cena + Zakres lat ===
            var lblTitle = new Label
            {
                Text = "📈  Porównanie rok‑do‑roku",
                Font = new Font("Segoe UI Emoji", 13F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false, Width = 250, Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(0, 4)
            };

            var lblPick = new Label
            {
                Text = "📊 Cena:",
                Font = new Font("Segoe UI Emoji", 9.5F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(260, 18), AutoSize = true
            };
            yoyColumnSelector = new ComboBox
            {
                Width = 200, Height = 26, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat,
                Location = new Point(325, 14)
            };
            yoyColumnSelector.Items.AddRange(new object[] {
                "Nasza Tuszka", "Wolnorynkowa", "Rolnicza", "Minister", "Laczona",
                "Tuszka Zrzeszenia", "Śr. Wszystkich Dostaw",
                "Przebitka: Zrzeszenie − Rolnicza",
                "Przebitka: Nasza Tuszka − Wolnorynkowa"
            });
            yoyColumnSelector.SelectedIndex = 0;
            yoyColumnSelector.Width = 280;
            yoyColumnSelector.SelectedIndexChanged += (s, e) => yoyChartPanel?.Invalidate();

            // Zakres lat
            int curYear = DateTime.Today.Year;
            var lblFromY = new Label
            {
                Text = "📅 Lata od:", Font = new Font("Segoe UI Emoji", 9.5F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(620, 18), AutoSize = true
            };
            yoyYearFrom = new NumericUpDown
            {
                Minimum = 2020, Maximum = curYear, Value = Math.Max(2020, curYear - 2),
                Width = 70, Height = 26, Font = new Font("Segoe UI", 10F),
                Location = new Point(695, 14)
            };
            yoyYearFrom.ValueChanged += (s, e) =>
            {
                if (yoyYearTo != null && yoyYearFrom.Value > yoyYearTo.Value)
                    yoyYearTo.Value = yoyYearFrom.Value;
                yoyChartPanel?.Invalidate();
            };

            var lblToY = new Label
            {
                Text = "do:", Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(775, 18), AutoSize = true
            };
            yoyYearTo = new NumericUpDown
            {
                Minimum = 2020, Maximum = curYear, Value = curYear,
                Width = 70, Height = 26, Font = new Font("Segoe UI", 10F),
                Location = new Point(800, 14)
            };
            yoyYearTo.ValueChanged += (s, e) =>
            {
                if (yoyYearFrom != null && yoyYearTo.Value < yoyYearFrom.Value)
                    yoyYearFrom.Value = yoyYearTo.Value;
                yoyChartPanel?.Invalidate();
            };

            yoyShowLabels = new CheckBox
            {
                Text = "🏷 Etykiety", Checked = false, AutoSize = true,
                Font = new Font("Segoe UI Emoji", 9F),
                Location = new Point(895, 18)
            };
            yoyShowLabels.CheckedChanged += (s, e) => yoyChartPanel?.Invalidate();

            // === ROW 2: Agregacja okresu ===
            var lblAggLbl = new Label
            {
                Text = "📅 Agregacja:",
                Font = new Font("Segoe UI Emoji", 9.5F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(0, 70), AutoSize = true
            };
            (string Label, AggregationMode Mode)[] aggOptions =
            {
                ("Dzień",   AggregationMode.Day),
                ("Tydzień", AggregationMode.Week),
                ("Miesiąc", AggregationMode.Month),
                ("Kwartał", AggregationMode.Quarter)
            };
            int xAgg = 90;
            foreach (var (lbl, mode) in aggOptions)
            {
                var btn = new Button
                {
                    Text = lbl, Tag = mode,
                    Width = 80, Height = 28,
                    Location = new Point(xAgg, 66),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(71, 85, 105)
                };
                btn.FlatAppearance.BorderColor = subtleBorderColor;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
                if (mode == _yoyAggMode)
                {
                    btn.BackColor = primaryColor;
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.BorderColor = primaryColor;
                }
                btn.Click += (s, e) =>
                {
                    _yoyAggMode = mode;
                    foreach (var b in _yoyAggButtons)
                    {
                        bool active = b.Tag is AggregationMode m && m == mode;
                        b.BackColor = active ? primaryColor : Color.White;
                        b.ForeColor = active ? Color.White : Color.FromArgb(71, 85, 105);
                        b.FlatAppearance.BorderColor = active ? primaryColor : subtleBorderColor;
                    }
                    yoyChartPanel?.Invalidate();
                };
                _yoyAggButtons.Add(btn);
                controls.Controls.Add(btn);
                xAgg += 86;
            }

            var lblHint = new Label
            {
                Text = "💡 Domyślnie miesiąc - czysta porównywalność rok-do-roku",
                Font = new Font("Segoe UI Emoji", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(xAgg + 16, 72), AutoSize = true
            };
            controls.Controls.Add(lblAggLbl);
            controls.Controls.Add(lblHint);

            var helpBtn = BuildSmallHelpButton(controls,
                "Porównanie YoY (rok‑do‑roku)",
                "Wykres pokazuje wybraną cenę w 3 ostatnich latach na jednej osi 'dzień roku' (1-365).\n\n" +
                "📊 ELEMENTY:\n" +
                "• Niebieska linia — bieżący rok (2026)\n" +
                "• Pomarańczowa linia — poprzedni rok (2025)\n" +
                "• Szara linia — 2 lata wstecz (2024)\n" +
                "• Pionowa zielona kreska — dzisiejszy dzień roku\n\n" +
                "💡 BIZNESOWO:\n" +
                "• Czy w tym roku ceny są wyższe/niższe niż w zeszłym o tej samej porze?\n" +
                "• Sezonowe wzorce - czy luty zawsze tani? Czy lipiec zawsze drogi?\n" +
                "• Anomalie roku - dni gdzie linia bieżącego roku odjeżdża od zeszłorocznej\n\n" +
                "Niezależne od filtra dat - zawsze pokazuje pełne 3 lata.");

            controls.Controls.AddRange(new Control[] {
                lblTitle, lblPick, yoyColumnSelector,
                lblFromY, yoyYearFrom, lblToY, yoyYearTo,
                yoyShowLabels, helpBtn
            });
            container.Controls.Add(controls, 0, 0);

            yoyChartPanel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(16, 16, 16, 16) };
            yoyChartPanel.Paint += YoYChart_Paint;
            yoyChartPanel.Resize += (s, e) => yoyChartPanel.Invalidate();
            container.Controls.Add(yoyChartPanel, 0, 1);

            tab.Controls.Add(container);
        }

        private void YoYChart_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (!(sender is Panel panel) || originalData == null) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Rounded card background
                var cardRect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using var cardPath = GetRoundedRectangle(cardRect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, cardPath);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, cardPath);

                string col = yoyColumnSelector?.SelectedItem?.ToString() ?? "Nasza Tuszka";

                // Tryb "Przebitka: A − B" → liczymy różnicę dwóch kolumn na poziomie wiersza
                string? przebitkaA = null;
                string? przebitkaB = null;
                bool isPrzebitka = false;
                if (col == "Przebitka: Zrzeszenie − Rolnicza")
                {
                    przebitkaA = "Tuszka Zrzeszenia"; przebitkaB = "Rolnicza"; isPrzebitka = true;
                }
                else if (col == "Przebitka: Nasza Tuszka − Wolnorynkowa")
                {
                    przebitkaA = "Nasza Tuszka"; przebitkaB = "Wolnorynkowa"; isPrzebitka = true;
                }

                if (isPrzebitka)
                {
                    if (!originalData.Columns.Contains(przebitkaA!) || !originalData.Columns.Contains(przebitkaB!)) return;
                }
                else if (!originalData.Columns.Contains(col)) return;

                int yFrom = (int)(yoyYearFrom?.Value ?? DateTime.Today.Year - 2);
                int yTo = (int)(yoyYearTo?.Value ?? DateTime.Today.Year);
                if (yFrom > yTo) yFrom = yTo;
                int yearCount = yTo - yFrom + 1;
                int[] years = Enumerable.Range(yFrom, yearCount).ToArray();

                // Generujemy paletę: od szarego (najstarszy) przez pomarańczowy do niebieskiego (najnowszy)
                Color[] yearColors = new Color[years.Length];
                for (int i = 0; i < years.Length; i++)
                {
                    if (years.Length == 1)
                    {
                        yearColors[i] = Color.FromArgb(37, 99, 235); // jeden rok = niebieski
                    }
                    else
                    {
                        // Interpolacja: szary → pomarańczowy → niebieski
                        double t = i / (double)(years.Length - 1);
                        if (t < 0.5)
                        {
                            double k = t * 2;
                            yearColors[i] = Color.FromArgb(
                                (int)(148 + (234 - 148) * k),
                                (int)(163 + (88 - 163) * k),
                                (int)(184 + (12 - 184) * k));
                        }
                        else
                        {
                            double k = (t - 0.5) * 2;
                            yearColors[i] = Color.FromArgb(
                                (int)(234 + (37 - 234) * k),
                                (int)(88 + (99 - 88) * k),
                                (int)(12 + (235 - 12) * k));
                        }
                    }
                }

                int currentYear = DateTime.Today.Year;

                // Wyznacz max okresów dla wybranej agregacji
                int maxPeriod;
                Func<DateTime, int> periodOf;
                Func<int, int, string> periodLabel;
                int[] axisLabelPositions;
                string[] axisLabels;

                switch (_yoyAggMode)
                {
                    case AggregationMode.Day:
                        maxPeriod = 366;
                        periodOf = d => d.DayOfYear;
                        periodLabel = (p, year) => {
                            try { return new DateTime(year, 1, 1).AddDays(p - 1).ToString("dd.MM"); }
                            catch { return p.ToString(); }
                        };
                        axisLabelPositions = new[] { 1, 32, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335 };
                        axisLabels = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };
                        break;
                    case AggregationMode.Week:
                        maxPeriod = 53;
                        periodOf = d => System.Globalization.ISOWeek.GetWeekOfYear(d);
                        periodLabel = (p, year) => $"T{p:00}";
                        axisLabelPositions = new[] { 1, 5, 9, 13, 17, 21, 26, 30, 34, 39, 43, 48, 52 };
                        axisLabels = new[] { "T01", "T05", "T09", "T13", "T17", "T21", "T26", "T30", "T34", "T39", "T43", "T48", "T52" };
                        break;
                    case AggregationMode.Quarter:
                        maxPeriod = 4;
                        periodOf = d => (d.Month - 1) / 3 + 1;
                        periodLabel = (p, year) => $"Q{p}";
                        axisLabelPositions = new[] { 1, 2, 3, 4 };
                        axisLabels = new[] { "Q1 (Sty‑Mar)", "Q2 (Kwi‑Cze)", "Q3 (Lip‑Wrz)", "Q4 (Paź‑Gru)" };
                        break;
                    case AggregationMode.Month:
                    default:
                        maxPeriod = 12;
                        periodOf = d => d.Month;
                        periodLabel = (p, year) => new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" }[p - 1];
                        axisLabelPositions = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
                        axisLabels = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };
                        break;
                }

                // Zbierz wartości per (rok, period) z agregacją AVG
                var perYearAgg = new Dictionary<int, Dictionary<int, (decimal Sum, int Count)>>();
                foreach (var y in years) perYearAgg[y] = new Dictionary<int, (decimal, int)>();
                foreach (DataRow r in originalData.Rows)
                {
                    if (r["DataRaw"] == DBNull.Value) continue;
                    decimal v;
                    if (isPrzebitka)
                    {
                        if (r[przebitkaA!] == DBNull.Value || r[przebitkaB!] == DBNull.Value) continue;
                        v = Convert.ToDecimal(r[przebitkaA!]) - Convert.ToDecimal(r[przebitkaB!]);
                    }
                    else
                    {
                        if (r[col] == DBNull.Value) continue;
                        v = Convert.ToDecimal(r[col]);
                    }
                    var d = Convert.ToDateTime(r["DataRaw"]).Date;
                    if (!perYearAgg.ContainsKey(d.Year)) continue;
                    int p = periodOf(d);
                    if (perYearAgg[d.Year].TryGetValue(p, out var agg))
                        perYearAgg[d.Year][p] = (agg.Sum + v, agg.Count + 1);
                    else
                        perYearAgg[d.Year][p] = (v, 1);
                }
                // Convert to averaged values
                var perYear = perYearAgg.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToDictionary(p => p.Key, p => p.Value.Sum / p.Value.Count));

                bool anyData = perYear.Values.Any(d => d.Count > 0);
                if (!anyData)
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 13F, FontStyle.Italic);
                    using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString($"📊  Brak danych dla '{col}' w wybranym zakresie lat",
                        emptyFont, grayBrush, panel.ClientRectangle,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                int padding = 24;
                int chartLeft = padding + 64;
                int chartRight = panel.Width - padding - 16;
                int chartTop = padding + 44;
                int chartBottom = panel.Height - padding - 36;
                int chartW = chartRight - chartLeft;
                int chartH = chartBottom - chartTop;

                // Skala Y (wspólna)
                var allValues = perYear.Values.SelectMany(d => d.Values).ToList();
                decimal yMin = allValues.Min();
                decimal yMax = allValues.Max();
                decimal yMargin = (yMax - yMin) * 0.1m;
                if (yMargin == 0) yMargin = 0.5m;
                yMin -= yMargin; yMax += yMargin;
                decimal yRange = yMax - yMin;
                if (yRange == 0) yRange = 1;

                int YForValue(decimal v) => chartTop + (int)((double)((yMax - v) / yRange) * chartH);
                int XForPeriod(int p) => chartLeft + (int)((p - 1.0) / Math.Max(1, maxPeriod - 1) * chartW);

                // Tytuł
                using var titleFont = new Font("Segoe UI Emoji", 12F);
                using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                string aggLabel = _yoyAggMode switch
                {
                    AggregationMode.Day => "dziennie",
                    AggregationMode.Week => "tygodniowo",
                    AggregationMode.Quarter => "kwartalnie",
                    _ => "miesięcznie"
                };
                g.DrawString($"📈  YoY: {col}  •  agregacja {aggLabel}",
                    titleFont, titleBrush, chartLeft, padding - 4);

                // Grid Y + etykiety
                using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);
                using var axisFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                for (int i = 0; i <= 5; i++)
                {
                    decimal v = yMin + yRange * i / 5;
                    int yL = YForValue(v);
                    g.DrawLine(gridPen, chartLeft, yL, chartRight, yL);
                    g.DrawString($"{v:N2}", axisFont, axisBrush, padding - 4, yL - 8);
                }

                // Etykiety osi X (zależne od trybu)
                for (int i = 0; i < axisLabelPositions.Length && i < axisLabels.Length; i++)
                {
                    int p = axisLabelPositions[i];
                    if (p < 1 || p > maxPeriod) continue;
                    int xM = XForPeriod(p);
                    g.DrawLine(gridPen, xM, chartTop, xM, chartBottom);
                    g.DrawString(axisLabels[i], axisFont, axisBrush, xM + 2, chartBottom + 4);
                }

                // Linia osi X i Y (mocniejsze niż grid)
                using var axisLine = new Pen(Color.FromArgb(200, 210, 224), 1.5f);
                g.DrawLine(axisLine, chartLeft, chartTop, chartLeft, chartBottom);
                g.DrawLine(axisLine, chartLeft, chartBottom, chartRight, chartBottom);

                bool showLabels = yoyShowLabels?.Checked == true;
                using var labelFont = new Font("Segoe UI", 8F, FontStyle.Bold);

                // Linie per rok + markery
                for (int yi = 0; yi < years.Length; yi++)
                {
                    var year = years[yi];
                    var color = yearColors[yi];
                    var data = perYear[year];
                    if (data.Count == 0) continue;

                    var sortedP = data.Keys.OrderBy(k => k).ToList();
                    var pts = sortedP.Select(p => new PointF(XForPeriod(p), YForValue(data[p]))).ToArray();

                    using var pen = new Pen(color, year == currentYear ? 3f : 2f);
                    if (year < currentYear - 1) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    if (pts.Length >= 2) g.DrawLines(pen, pts);

                    // Markery na każdym punkcie (większe dla agregacji niskoczęstotliwościowej)
                    int markerSize = _yoyAggMode == AggregationMode.Quarter ? 12
                                   : _yoyAggMode == AggregationMode.Month ? 10
                                   : _yoyAggMode == AggregationMode.Week ? 6
                                   : 4;
                    using var markerBrush = new SolidBrush(color);
                    using var markerEdge = new Pen(Color.White, 1.5f);
                    foreach (var p in pts)
                    {
                        g.FillEllipse(markerBrush, p.X - markerSize / 2f, p.Y - markerSize / 2f, markerSize, markerSize);
                        g.DrawEllipse(markerEdge, p.X - markerSize / 2f, p.Y - markerSize / 2f, markerSize, markerSize);
                    }

                    // Etykiety wartości
                    if (showLabels)
                    {
                        using var labelBrush = new SolidBrush(color);
                        using var labelBg = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                        // Dla agregacji rzadkich (kwartał/miesiąc) - na każdym punkcie
                        // Dla week/day - co N-ty
                        int step = _yoyAggMode == AggregationMode.Quarter || _yoyAggMode == AggregationMode.Month
                            ? 1
                            : Math.Max(1, sortedP.Count / 24);
                        for (int i = 0; i < sortedP.Count; i += step)
                        {
                            decimal v = data[sortedP[i]];
                            string text = v.ToString("0.00");
                            var size = g.MeasureString(text, labelFont);
                            float lx = pts[i].X - size.Width / 2;
                            float ly = pts[i].Y - size.Height - 8;
                            g.FillRectangle(labelBg, lx - 2, ly, size.Width + 4, size.Height);
                            g.DrawString(text, labelFont, labelBrush, lx, ly);
                        }
                    }
                }

                // Pionowa linia "dzisiaj" (jeśli bieżący rok jest w zakresie)
                if (years.Contains(currentYear))
                {
                    int todayPeriod = periodOf(DateTime.Today);
                    if (todayPeriod >= 1 && todayPeriod <= maxPeriod)
                    {
                        int xToday = XForPeriod(todayPeriod);
                        using var todayPen = new Pen(Color.FromArgb(200, 22, 163, 74), 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                        g.DrawLine(todayPen, xToday, chartTop, xToday, chartBottom);
                        using var todayBrush = new SolidBrush(successColor);
                        g.DrawString("dziś", axisFont, todayBrush, xToday - 14, chartTop - 16);
                    }
                }

                // Legenda lat (kolorowe kółka, nie emoji)
                using var legendFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
                int legendX = chartLeft + 16;
                int legendY = padding + 22;
                for (int yi = 0; yi < years.Length; yi++)
                {
                    using var dot = new SolidBrush(yearColors[yi]);
                    g.FillEllipse(dot, legendX, legendY, 14, 14);
                    using var textBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                    string yearText = years[yi].ToString();
                    if (years[yi] == currentYear) yearText += " (bieżący)";
                    g.DrawString(yearText, legendFont, textBrush, legendX + 20, legendY - 2);
                    var sz = g.MeasureString(yearText, legendFont);
                    legendX += 20 + (int)sz.Width + 16;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
        }

        // ═════════════════ PROGNOZA ═════════════════

        private void CreatePrognozaTab(TabPage tab)
        {
            var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = backgroundColor };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var controls = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 0, 20, 0) };
            controls.Paint += (s, e) => { using var p = new Pen(subtleBorderColor, 1); e.Graphics.DrawLine(p, 0, controls.Height - 1, controls.Width, controls.Height - 1); };

            var lblTitle = new Label
            {
                Text = "🔮  Prognoza cen (regresja liniowa)",
                Font = new Font("Segoe UI Emoji", 13F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false, Width = 380, Height = 60,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(0, 0)
            };

            var lblPick = new Label
            {
                Text = "📊 Cena:",
                Font = new Font("Segoe UI Emoji", 10F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(390, 22), AutoSize = true
            };
            prognozaColumnSelector = new ComboBox
            {
                Width = 220, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5F), FlatStyle = FlatStyle.Flat,
                Location = new Point(460, 18)
            };
            prognozaColumnSelector.Items.AddRange(new object[] {
                "Nasza Tuszka", "Wolnorynkowa", "Rolnicza", "Minister", "Laczona",
                "Tuszka Zrzeszenia", "Śr. Wszystkich Dostaw"
            });
            prognozaColumnSelector.SelectedIndex = 0;
            prognozaColumnSelector.SelectedIndexChanged += (s, e) => prognozaChartPanel?.Invalidate();

            var lblHorizon = new Label
            {
                Text = "Horyzont (dni):",
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(700, 22), AutoSize = true
            };
            prognozaHorizon = new NumericUpDown
            {
                Minimum = 7, Maximum = 60, Value = 14, Width = 70, Height = 28,
                Font = new Font("Segoe UI", 10F),
                Location = new Point(800, 18)
            };
            prognozaHorizon.ValueChanged += (s, e) => prognozaChartPanel?.Invalidate();

            var helpBtn = BuildSmallHelpButton(controls,
                "Prognoza cen",
                "Wykres przewiduje cenę na najbliższe N dni (7-60) na bazie REGRESJI LINIOWEJ z ostatnich 30 dni.\n\n" +
                "📊 ELEMENTY:\n" +
                "• Niebieska linia — rzeczywiste ceny historyczne\n" +
                "• Czerwona linia (przerywana) — prognoza\n" +
                "• Szare pasmo wokół prognozy — zakres niepewności (±1 odchylenie standardowe)\n" +
                "• Pionowa zielona kreska — dzisiaj (granica historii i prognozy)\n\n" +
                "⚠ OGRANICZENIA:\n" +
                "Prosty model — zakłada liniową kontynuację trendu.\n" +
                "Nie uwzględnia: sezonowości, paszy, czynników rynkowych.\n\n" +
                "💡 ZASTOSOWANIE:\n" +
                "• Trend rosnący → wcześniejszy zakup może być korzystny\n" +
                "• Trend płaski → poczekaj na okazję\n" +
                "• Wysoka niepewność (szerokie pasmo) → ostrożnie z planowaniem");

            controls.Controls.AddRange(new Control[] { lblTitle, lblPick, prognozaColumnSelector, lblHorizon, prognozaHorizon, helpBtn });
            container.Controls.Add(controls, 0, 0);

            prognozaChartPanel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(16, 16, 16, 16) };
            prognozaChartPanel.Paint += PrognozaChart_Paint;
            prognozaChartPanel.Resize += (s, e) => prognozaChartPanel.Invalidate();
            container.Controls.Add(prognozaChartPanel, 0, 1);

            tab.Controls.Add(container);
        }

        private void PrognozaChart_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (!(sender is Panel panel) || originalData == null) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var cardRect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using var cardPath = GetRoundedRectangle(cardRect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, cardPath);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, cardPath);

                string col = prognozaColumnSelector?.SelectedItem?.ToString() ?? "Nasza Tuszka";
                int horizon = (int)(prognozaHorizon?.Value ?? 14);

                if (!originalData.Columns.Contains(col)) return;

                // Ostatnie 90 dni historii (do regresji ostatnie 30)
                var hist = originalData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r[col] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r[col])))
                    .OrderBy(t => t.Date)
                    .ToList();

                if (hist.Count < 5)
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 13F, FontStyle.Italic);
                    using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString($"🔮  Za mało danych do prognozy '{col}' (potrzeba min. 5 punktów)",
                        emptyFont, grayBrush, panel.ClientRectangle,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                // Pokaż ostatnie ~90 dni
                var displayHist = hist.TakeLast(90).ToList();
                // Regresja na ostatnich 30 dniach
                var regrSrc = hist.TakeLast(30).ToList();

                // Linear regression: y = a*x + b (x = days since regrSrc[0].Date)
                var firstDate = regrSrc.First().Date;
                int n = regrSrc.Count;
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = (regrSrc[i].Date - firstDate).TotalDays;
                    double y = (double)regrSrc[i].V;
                    sumX += x; sumY += y; sumXY += x * y; sumX2 += x * x;
                }
                double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                if (double.IsNaN(slope) || double.IsInfinity(slope)) slope = 0;
                double intercept = (sumY - slope * sumX) / n;

                // RMSE residuum
                double sse = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = (regrSrc[i].Date - firstDate).TotalDays;
                    double pred = slope * x + intercept;
                    double err = (double)regrSrc[i].V - pred;
                    sse += err * err;
                }
                double rmse = Math.Sqrt(sse / n);

                // Prognoza punkty
                var lastDate = hist.Last().Date;
                var forecast = new List<(DateTime Date, double V)>();
                for (int d = 1; d <= horizon; d++)
                {
                    var fd = lastDate.AddDays(d);
                    double xx = (fd - firstDate).TotalDays;
                    double pred = slope * xx + intercept;
                    forecast.Add((fd, pred));
                }

                int padding = 24;
                int chartLeft = padding + 56;
                int chartRight = panel.Width - padding - 16;
                int chartTop = padding + 36;
                int chartBottom = panel.Height - padding - 32;
                int chartW = chartRight - chartLeft;
                int chartH = chartBottom - chartTop;

                // Skala
                var allDates = displayHist.Select(t => t.Date).Concat(forecast.Select(t => t.Date)).ToList();
                var allValues = displayHist.Select(t => (double)t.V).Concat(forecast.Select(t => t.V)).ToList();
                DateTime xMin = allDates.Min();
                DateTime xMax = allDates.Max();
                double yMin = allValues.Min() - rmse * 1.2;
                double yMax = allValues.Max() + rmse * 1.2;
                double yRange = yMax - yMin;
                if (yRange == 0) yRange = 1;
                double xRange = (xMax - xMin).TotalDays;
                if (xRange == 0) xRange = 1;

                int YForValue(double v) => chartTop + (int)((yMax - v) / yRange * chartH);
                int XForDate(DateTime d) => chartLeft + (int)((d - xMin).TotalDays / xRange * chartW);

                // Tytuł
                using var titleFont = new Font("Segoe UI Emoji", 12F);
                using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                double slopeYearly = slope * 365;
                g.DrawString($"🔮  Prognoza '{col}' na {horizon} dni  •  trend: {slope:+0.0000;-0.0000;0.0000} zł/dzień  •  RMSE = {rmse:N3} zł",
                    titleFont, titleBrush, chartLeft, padding - 6);

                // Grid Y
                using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);
                using var axisFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                for (int i = 0; i <= 5; i++)
                {
                    double v = yMin + yRange * i / 5;
                    int yL = YForValue(v);
                    g.DrawLine(gridPen, chartLeft, yL, chartRight, yL);
                    g.DrawString($"{v:N2}", axisFont, axisBrush, padding - 4, yL - 8);
                }

                // Confidence band (szare pasmo ±RMSE wokół prognozy)
                if (forecast.Count >= 2)
                {
                    var upperPts = forecast.Select(t => new PointF(XForDate(t.Date), YForValue(t.V + rmse))).ToArray();
                    var lowerPts = forecast.Select(t => new PointF(XForDate(t.Date), YForValue(t.V - rmse))).ToArray();
                    var bandPts = upperPts.Concat(lowerPts.Reverse()).ToArray();
                    using var bandBrush = new SolidBrush(Color.FromArgb(50, 220, 38, 38));
                    g.FillPolygon(bandBrush, bandPts);
                }

                // Linia historyczna (niebieska)
                if (displayHist.Count >= 2)
                {
                    var histPts = displayHist.Select(t => new PointF(XForDate(t.Date), YForValue((double)t.V))).ToArray();
                    using var histPen = new Pen(primaryColor, 2.5f);
                    g.DrawLines(histPen, histPts);
                    // Marker ostatniego punktu
                    var last = histPts.Last();
                    using var lastBrush = new SolidBrush(primaryColor);
                    g.FillEllipse(lastBrush, last.X - 5, last.Y - 5, 10, 10);
                }

                // Linia prognozy (czerwona przerywana)
                if (forecast.Count >= 2)
                {
                    var forePts = new[] { new PointF(XForDate(lastDate), YForValue((double)hist.Last().V)) }
                        .Concat(forecast.Select(t => new PointF(XForDate(t.Date), YForValue(t.V)))).ToArray();
                    using var forePen = new Pen(Color.FromArgb(220, 38, 38), 2.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                    g.DrawLines(forePen, forePts);
                    // Marker końcowy z wartością
                    var endPt = forePts.Last();
                    using var endBrush = new SolidBrush(Color.FromArgb(220, 38, 38));
                    g.FillEllipse(endBrush, endPt.X - 5, endPt.Y - 5, 10, 10);
                    using var endLabelFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                    g.DrawString($"{forecast.Last().V:N2} zł", endLabelFont, endBrush, endPt.X + 8, endPt.Y - 14);
                }

                // Pionowa linia "dzisiaj"
                int xToday = XForDate(lastDate);
                using var todayPen = new Pen(Color.FromArgb(150, 22, 163, 74), 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(todayPen, xToday, chartTop, xToday, chartBottom);
                using var todayBrush = new SolidBrush(successColor);
                g.DrawString("dziś", axisFont, todayBrush, xToday - 12, chartTop - 14);

                // Etykiety osi X (kilka dat)
                int labelCount = 6;
                for (int i = 0; i <= labelCount; i++)
                {
                    DateTime d = xMin.AddDays(xRange * i / labelCount);
                    int xL = XForDate(d);
                    g.DrawString(d.ToString("dd.MM"), axisFont, axisBrush, xL - 14, chartBottom + 4);
                }

                // Legenda
                int legendY = padding + 16;
                int legendX = chartRight - 220;
                using var legFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                using var legBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                using var dotPrim = new SolidBrush(primaryColor);
                g.FillRectangle(dotPrim, legendX, legendY, 12, 3);
                g.DrawString("historia", legFont, legBrush, legendX + 18, legendY - 5);
                using var dotRed = new SolidBrush(Color.FromArgb(220, 38, 38));
                g.FillRectangle(dotRed, legendX + 100, legendY, 12, 3);
                g.DrawString("prognoza", legFont, legBrush, legendX + 118, legendY - 5);
            }
            catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
        }

        // ═════════════════ PASZE → ŻYWIEC (cykl 35-42 dni) ═════════════════

        private void CreatePaszeTab(TabPage tab)
        {
            var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = backgroundColor };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));    // controls
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));    // info bar
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // 2 charts

            // ── Controls
            var controls = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 0, 20, 0) };
            controls.Paint += (s, e) => { using var p = new Pen(subtleBorderColor, 1); e.Graphics.DrawLine(p, 0, controls.Height - 1, controls.Width, controls.Height - 1); };

            var lblTitle = new Label
            {
                Text = "🌾  Pasze → Żywiec",
                Font = new Font("Segoe UI Emoji", 13F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false, Width = 240, Height = 64,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(0, 0)
            };

            // Zakres dat (osobny od głównego filtra - może obejmować większy okres)
            var lblFrom = new Label
            {
                Text = "Od:", Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(250, 24), AutoSize = true
            };
            paszeDateFrom = new DateTimePicker
            {
                Width = 120, Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddYears(-1),
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(285, 20)
            };
            paszeDateFrom.ValueChanged += (s, e) => paszeChartPanel?.Invalidate();

            var lblTo = new Label
            {
                Text = "Do:", Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(420, 24), AutoSize = true
            };
            paszeDateTo = new DateTimePicker
            {
                Width = 120, Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(450, 20)
            };
            paszeDateTo.ValueChanged += (s, e) => paszeChartPanel?.Invalidate();

            var btnLoad = new Button
            {
                Text = "🔄  Załaduj pasze",
                Size = new Size(160, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = primaryColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Emoji", 9.5F),
                Cursor = Cursors.Hand,
                Location = new Point(590, 16)
            };
            btnLoad.FlatAppearance.BorderSize = 0;
            btnLoad.Click += async (s, e) =>
            {
                btnLoad.Enabled = false;
                paszeStatusLabel.Text = "⏳  Ładowanie pasz z Handel…";
                paszeStatusLabel.ForeColor = primaryColor;
                await LoadPaszeDataAsync();
                btnLoad.Enabled = true;
                paszeChartPanel?.Invalidate();
            };

            var helpBtn = BuildSmallHelpButton(controls,
                "Pasze → Żywiec - jak to czytać?",
                "🌾 IDEA:\n" +
                "Hodowca dziś kupuje paszę. Karmi nią kurczaka przez 35-42 dni. Po tym czasie kurczak idzie do uboju i sprzedaje go nam jako żywiec.\n\n" +
                "Czyli CENA PASZY DZIŚ wpływa na CENĘ ŻYWCA za ~5 TYGODNI.\n\n" +
                "📊 WYKRESY:\n" +
                "• GÓRNY = cena paszy w czasie (PLN/tona, TASOMIX/De Heus/Ekoplon)\n" +
                "• DOLNY = cena żywca w czasie (PLN/kg Wolnorynkowa)\n" +
                "Te same daty na osi X - możesz porównać kierunek zmian.\n\n" +
                "🔗 KORELACJA:\n" +
                "W górnym info-pasku liczba `r` pokazuje jak silnie pasza(t) ↔ żywiec(t+38).\n" +
                "• r > 0,7 = silny związek - pasza tania → za 5 tyg żywiec też tani\n" +
                "• r < 0,3 = słaby związek - inne czynniki rządzą ceną żywca\n\n" +
                "💡 KORZYŚĆ:\n" +
                "Wiedząc cenę paszy DZIŚ, możesz przewidzieć cenę żywca za 5 tygodni!");

            controls.Controls.AddRange(new Control[] { lblTitle, lblFrom, paszeDateFrom, lblTo, paszeDateTo, btnLoad, helpBtn });
            container.Controls.Add(controls, 0, 0);

            // ── Info bar - status + correlation hint
            paszeStatusLabel = new Label
            {
                Text = "💡 Kliknij '🔄 Załaduj pasze' aby pobrać dane z bazy Handel (kategoria 65883)",
                Font = new Font("Segoe UI Emoji", 10F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 20, 0)
            };
            container.Controls.Add(paszeStatusLabel, 0, 1);

            // ── Chart panel - 2 stacked charts (pasza góra, żywiec dół)
            paszeChartPanel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(16, 16, 16, 16) };
            paszeChartPanel.Paint += PaszeChart_Paint;
            paszeChartPanel.Resize += (s, e) => paszeChartPanel.Invalidate();
            container.Controls.Add(paszeChartPanel, 0, 2);

            tab.Controls.Add(container);
        }

        private async Task LoadPaszeDataAsync()
        {
            try
            {
                var sql = @"
                    SELECT
                        CAST(DP.[data] AS DATE) AS Data,
                        CAST(SUM(DP.[wartNetto]) / NULLIF(SUM(DP.[ilosc]), 0) AS DECIMAL(10, 2)) AS CenaPaszy,
                        CAST(SUM(DP.[ilosc]) AS DECIMAL(12, 2)) AS Wolumen
                    FROM [HANDEL].[HM].[DP] DP
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id]
                    WHERE TW.[katalog] = 65883
                        AND DP.[data] >= '2023-01-01'
                        AND DP.[ilosc] > 0
                    GROUP BY CAST(DP.[data] AS DATE)
                    ORDER BY CAST(DP.[data] AS DATE)";

                var dt = new DataTable();
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionStringHandel);
                    using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                    using var adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);
                });
                paszeData = dt;
                paszeStatusLabel.Text = $"✅  Załadowano {dt.Rows.Count} dni cen paszy";
                paszeStatusLabel.ForeColor = successColor;
            }
            catch (Exception ex)
            {
                paszeStatusLabel.Text = $"❌  Błąd: {ex.Message.Split('\n').FirstOrDefault()}";
                paszeStatusLabel.ForeColor = dangerColor;
                paszeData = null;
            }
        }

        private void PaszeChart_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (!(sender is Panel panel)) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var cardRect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using var cardPath = GetRoundedRectangle(cardRect, 10);
                using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, cardPath);
                using (var border = new Pen(subtleBorderColor, 1)) g.DrawPath(border, cardPath);

                if (paszeData == null || paszeData.Rows.Count == 0)
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 14F, FontStyle.Italic);
                    using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString("🌾  Wybierz zakres dat i kliknij 'Załaduj pasze'",
                        emptyFont, grayBrush, panel.ClientRectangle,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                // Filtr danych zgodnie z paszeDateFrom/To
                DateTime dFrom = paszeDateFrom?.Value.Date ?? DateTime.Today.AddYears(-1);
                DateTime dTo = paszeDateTo?.Value.Date ?? DateTime.Today;

                var pasze = paszeData.AsEnumerable()
                    .Select(r => (Date: Convert.ToDateTime(r["Data"]).Date, V: Convert.ToDecimal(r["CenaPaszy"])))
                    .Where(p => p.Date >= dFrom && p.Date <= dTo)
                    .OrderBy(p => p.Date)
                    .ToList();

                var zywiec = originalData?.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r["Wolnorynkowa"] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r["Wolnorynkowa"])))
                    .Where(p => p.Date >= dFrom && p.Date <= dTo)
                    .OrderBy(p => p.Date)
                    .ToList() ?? new List<(DateTime, decimal)>();

                if (pasze.Count < 2)
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 13F, FontStyle.Italic);
                    using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString("🌾  Brak danych paszy w wybranym okresie - rozszerz zakres lub załaduj ponownie",
                        emptyFont, grayBrush, panel.ClientRectangle,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                // Wspólna oś X dla obu wykresów
                DateTime xMin = pasze.Min(p => p.Date);
                DateTime xMax = pasze.Max(p => p.Date);
                if (zywiec.Any())
                {
                    if (zywiec.Min(p => p.Date) < xMin) xMin = zywiec.Min(p => p.Date);
                    if (zywiec.Max(p => p.Date) > xMax) xMax = zywiec.Max(p => p.Date);
                }
                double xRange = (xMax - xMin).TotalDays;
                if (xRange == 0) xRange = 1;

                int padding = 20;
                int chartLeft = padding + 64;
                int chartRight = panel.Width - padding - 16;
                int chartW = chartRight - chartLeft;

                int totalH = panel.Height - padding * 2 - 50;
                int chartH1 = totalH * 47 / 100;  // pasza
                int chartH2 = totalH * 47 / 100;  // żywiec
                int chartGap = totalH - chartH1 - chartH2;
                int chart1Top = padding + 30;
                int chart1Bottom = chart1Top + chartH1;
                int chart2Top = chart1Bottom + chartGap + 20;
                int chart2Bottom = chart2Top + chartH2;

                int XForDate(DateTime d) => chartLeft + (int)((d - xMin).TotalDays / xRange * chartW);

                using var titleFont = new Font("Segoe UI Emoji", 11.5F);
                using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                using var subTitleBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                using var subTitleFont = new Font("Segoe UI", 9F, FontStyle.Italic);
                using var axisFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);

                // ═════ GÓRNY WYKRES: PASZA ═════
                decimal pMin = pasze.Min(p => p.V), pMax = pasze.Max(p => p.V);
                decimal pM = (pMax - pMin) * 0.1m;
                if (pM == 0) pM = 0.5m;
                pMin -= pM; pMax += pM;
                decimal pRng = pMax - pMin;
                if (pRng == 0) pRng = 1;
                int YForPasza(decimal v) => chart1Top + (int)((double)((pMax - v) / pRng) * chartH1);

                g.DrawString("🌾  Cena paszy w czasie  (PLN/tona)",
                    titleFont, titleBrush, chartLeft, padding);
                decimal pAvg = pasze.Average(p => p.V);
                g.DrawString($"średnia: {pAvg:N2} PLN/t  •  zakres: {pMin + pM:N0} – {pMax - pM:N0}  •  {pasze.Count} dni z danymi",
                    subTitleFont, subTitleBrush, chartLeft, padding + 16);

                // Grid Y dla paszy
                for (int i = 0; i <= 4; i++)
                {
                    int yL = chart1Top + chartH1 * i / 4;
                    g.DrawLine(gridPen, chartLeft, yL, chartRight, yL);
                    decimal v = pMax - pRng * i / 4;
                    g.DrawString($"{v:N0}", axisFont, axisBrush, padding - 4, yL - 8);
                }
                using var axisLine = new Pen(Color.FromArgb(200, 210, 224), 1.5f);
                g.DrawLine(axisLine, chartLeft, chart1Top, chartLeft, chart1Bottom);
                g.DrawLine(axisLine, chartLeft, chart1Bottom, chartRight, chart1Bottom);

                // Linia paszy z gradientem fillowym
                var paszaPts = pasze.Select(p => new PointF(XForDate(p.Date), YForPasza(p.V))).ToArray();
                var fillPts = new List<PointF>(paszaPts);
                fillPts.Add(new PointF(paszaPts.Last().X, chart1Bottom));
                fillPts.Add(new PointF(paszaPts.First().X, chart1Bottom));
                using (var fillBrush = new SolidBrush(Color.FromArgb(60, 37, 99, 235)))
                    g.FillPolygon(fillBrush, fillPts.ToArray());
                using (var pen = new Pen(primaryColor, 2.5f))
                    g.DrawLines(pen, paszaPts);

                // ═════ DOLNY WYKRES: ŻYWIEC ═════
                if (zywiec.Count < 2)
                {
                    using var emptyFont = new Font("Segoe UI Emoji", 11F, FontStyle.Italic);
                    g.DrawString("🐔  Brak danych żywca w wybranym okresie",
                        emptyFont, axisBrush, chartLeft, chart2Top + chartH2 / 2 - 8);
                }
                else
                {
                    decimal zMin = zywiec.Min(p => p.V), zMax = zywiec.Max(p => p.V);
                    decimal zM = (zMax - zMin) * 0.1m;
                    if (zM == 0) zM = 0.1m;
                    zMin -= zM; zMax += zM;
                    decimal zRng = zMax - zMin;
                    if (zRng == 0) zRng = 1;
                    int YForZywiec(decimal v) => chart2Top + (int)((double)((zMax - v) / zRng) * chartH2);

                    // Korelacja pasza(t) ↔ żywiec(t+38)
                    int lagDays = 38;
                    var zywiecByDate = zywiec.ToDictionary(p => p.Date, p => p.V);
                    var lagPairs = new List<(decimal X, decimal Y)>();
                    foreach (var (pd, pv) in pasze)
                    {
                        if (zywiecByDate.TryGetValue(pd.AddDays(lagDays), out var zv))
                            lagPairs.Add((pv, zv));
                    }
                    double? r = lagPairs.Count >= 5
                        ? ComputePearson(lagPairs.Select(p => p.X).ToArray(), lagPairs.Select(p => p.Y).ToArray())
                        : (double?)null;
                    string corrLabel = r.HasValue
                        ? $"  •  korelacja z paszą sprzed {lagDays} dni: r = {r.Value:+0.00;-0.00;0.00}"
                        : "";

                    g.DrawString($"🐔  Cena żywca w czasie  (PLN/kg, Wolnorynkowa){corrLabel}",
                        titleFont, titleBrush, chartLeft, chart1Bottom + chartGap + 2);
                    decimal zAvg = zywiec.Average(p => p.V);
                    g.DrawString($"średnia: {zAvg:N2} PLN/kg  •  zakres: {zMin + zM:N2} – {zMax - zM:N2}  •  {zywiec.Count} dni z danymi",
                        subTitleFont, subTitleBrush, chartLeft, chart1Bottom + chartGap + 18);

                    // Grid Y żywca
                    for (int i = 0; i <= 4; i++)
                    {
                        int yL = chart2Top + chartH2 * i / 4;
                        g.DrawLine(gridPen, chartLeft, yL, chartRight, yL);
                        decimal v = zMax - zRng * i / 4;
                        g.DrawString($"{v:N2}", axisFont, axisBrush, padding - 4, yL - 8);
                    }
                    g.DrawLine(axisLine, chartLeft, chart2Top, chartLeft, chart2Bottom);
                    g.DrawLine(axisLine, chartLeft, chart2Bottom, chartRight, chart2Bottom);

                    var zywiecPts = zywiec.Select(p => new PointF(XForDate(p.Date), YForZywiec(p.V))).ToArray();
                    var zFillPts = new List<PointF>(zywiecPts);
                    zFillPts.Add(new PointF(zywiecPts.Last().X, chart2Bottom));
                    zFillPts.Add(new PointF(zywiecPts.First().X, chart2Bottom));
                    using (var fillBrush = new SolidBrush(Color.FromArgb(60, 234, 88, 12)))
                        g.FillPolygon(fillBrush, zFillPts.ToArray());
                    using (var pen = new Pen(Color.FromArgb(234, 88, 12), 2.5f))
                        g.DrawLines(pen, zywiecPts);
                }

                // Etykiety osi X (wspólne, na dole)
                int xLabelY = chart2Bottom + 6;
                int labelCount = 8;
                for (int i = 0; i <= labelCount; i++)
                {
                    DateTime d = xMin.AddDays(xRange * i / labelCount);
                    int xL = XForDate(d);
                    g.DrawString(d.ToString("MM.yy"), axisFont, axisBrush, xL - 14, xLabelY);
                }

                // Status update z korelacją
                if (paszeStatusLabel != null)
                {
                    decimal pAvgFinal = pasze.Average(p => p.V);
                    string statusText = $"✅ {pasze.Count} dni paszy załadowanych  •  średnia paszy: {pAvgFinal:N0} PLN/t  •  zakres: {dFrom:dd.MM.yyyy} → {dTo:dd.MM.yyyy}";
                    if (paszeStatusLabel.Text != statusText)
                    {
                        paszeStatusLabel.BeginInvoke(new Action(() => {
                            try { paszeStatusLabel.Text = statusText; paszeStatusLabel.ForeColor = successColor; } catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
                        }));
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
        }

        // ═════════════════ ALERTY CENOWE ═════════════════

        private void CreateAlertyTab(TabPage tab)
        {
            var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = backgroundColor };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var controls = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 0, 20, 0) };
            controls.Paint += (s, e) => { using var p = new Pen(subtleBorderColor, 1); e.Graphics.DrawLine(p, 0, controls.Height - 1, controls.Width, controls.Height - 1); };

            var lblTitle = new Label
            {
                Text = "🔔  Alerty cenowe",
                Font = new Font("Segoe UI Emoji", 13F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false, Width = 280, Height = 60,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(0, 0)
            };

            var lblHint = new Label
            {
                Text = "💡 Automatyczne sygnały kupna i sprzedaży na podstawie dynamiki cen w wybranym okresie.\n   Każdy alert ma kategorię (krytyczny/ostrzeżenie/info) + akcję.",
                Font = new Font("Segoe UI Emoji", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(290, 8), AutoSize = true
            };

            var helpBtn = BuildSmallHelpButton(controls,
                "Alerty cenowe",
                "Lista AUTOMATYCZNYCH alertów wygenerowanych z aktualnych danych okresu.\n\n" +
                "🚨 KATEGORIE:\n" +
                "• KRYTYCZNE (czerwone) — wymagają natychmiastowej akcji (np. marża < 1,50)\n" +
                "• OSTRZEŻENIA (pomarańczowe) — warto się przyjrzeć (trend spadkowy, anomalie)\n" +
                "• INFORMACYJNE (niebieskie) — interesujące fakty (najtańszy dzień, korelacja)\n" +
                "• POZYTYWNE (zielone) — okazje do wykorzystania\n\n" +
                "📋 RODZAJE ALERTÓW:\n" +
                "• Spadki/wzrosty cen > 5%\n" +
                "• Marża poza progiem 2,50 zł/kg\n" +
                "• Wolatylność wzrosła\n" +
                "• Wolumen się załamał lub eksplodował\n" +
                "• Rzadkie ekstrema (>2σ)\n\n" +
                "💡 JAK UŻYWAĆ:\n" +
                "Czytaj listę codziennie rano. Krytyczne wymagają decyzji w ten dzień.");

            controls.Controls.AddRange(new Control[] { lblTitle, lblHint, helpBtn });
            container.Controls.Add(controls, 0, 0);

            alertyPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = backgroundColor,
                Padding = new Padding(16)
            };
            container.Controls.Add(alertyPanel, 0, 1);

            tab.Controls.Add(container);
        }

        public void UpdateAlerty()
        {
            if (alertyPanel == null || originalData == null) return;
            alertyPanel.SuspendLayout();
            alertyPanel.Controls.Clear();

            int contentWidth = Math.Max(800, alertyPanel.ClientSize.Width - 40);
            var alerts = ComputeAlerty();

            if (alerts.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "✅  Brak aktywnych alertów - wszystko wygląda stabilnie!",
                    Font = new Font("Segoe UI Emoji", 13F),
                    ForeColor = successColor,
                    AutoSize = false,
                    Width = contentWidth,
                    Height = 80,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.White,
                    Margin = new Padding(0, 30, 0, 0)
                };
                alertyPanel.Controls.Add(lblEmpty);
                alertyPanel.ResumeLayout();
                return;
            }

            // Header z licznikiem
            int crit = alerts.Count(a => a.Severity == "critical");
            int warn = alerts.Count(a => a.Severity == "warning");
            int info = alerts.Count(a => a.Severity == "info");
            int good = alerts.Count(a => a.Severity == "success");

            var lblCounter = new Label
            {
                Text = $"🚨 {crit} krytycznych  •  ⚠ {warn} ostrzeżeń  •  ℹ {info} informacyjnych  •  ✅ {good} pozytywnych",
                Font = new Font("Segoe UI Emoji", 11F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false,
                Width = contentWidth,
                Height = 36,
                Padding = new Padding(8, 8, 0, 0),
                Margin = new Padding(0, 0, 0, 8)
            };
            alertyPanel.Controls.Add(lblCounter);

            // Karty alertów
            foreach (var alert in alerts)
                alertyPanel.Controls.Add(BuildAlertCard(alert, contentWidth));

            alertyPanel.ResumeLayout();
        }

        private List<(string Severity, string Icon, string Title, string Description, string Action)> ComputeAlerty()
        {
            var result = new List<(string, string, string, string, string)>();
            if (filteredData == null || originalData == null) return result;

            var margins = ComputeDailyMargins();
            if (margins.Count == 0) return result;

            // 1. Marża poniżej krytycznego progu
            var lastMargin = margins.Last();
            if (lastMargin.Margin < 1.5m)
            {
                result.Add(("critical", "🚨",
                    $"Marża aktualna: {lastMargin.Margin:N2} zł/kg < 1,50 zł",
                    $"Dzień {lastMargin.Date:dd.MM.yyyy} - marża jest poniżej krytycznego progu.",
                    "Eskalacja do Zarządu. Sprawdź ceny zakupu i sprzedaży."));
            }
            else if (lastMargin.Margin < MARGIN_TARGET)
            {
                result.Add(("warning", "⚠",
                    $"Marża aktualna: {lastMargin.Margin:N2} zł poniżej celu {MARGIN_TARGET:N2}",
                    $"Cel firmowy nie jest osiągany.",
                    "Negocjuj lepsze ceny zakupu lub szukaj okazji sprzedaży."));
            }
            else
            {
                result.Add(("success", "✅",
                    $"Marża osiąga cel: {lastMargin.Margin:N2} zł ≥ {MARGIN_TARGET:N2} zł",
                    "Biznes działa zgodnie z procedurami firmowymi.",
                    "Kontynuuj politykę zakupową."));
            }

            // 2. Trend marży
            int streakDown = 0;
            for (int i = margins.Count - 1; i > 0; i--)
            {
                if (margins[i].Margin < margins[i - 1].Margin) streakDown++;
                else break;
            }
            if (streakDown >= 3)
            {
                result.Add(("warning", "📉",
                    $"Marża SPADA już {streakDown} dni z rzędu",
                    $"Ostatni wzrost marży: {margins[margins.Count - streakDown - 1].Date:dd.MM.yyyy}.",
                    "Przeanalizuj co powoduje spadek - ceny zakupu czy sprzedaży?"));
            }

            // 3. Najtańsza Wolnorynkowa - ostatnie 7 dni
            if (filteredData.Columns.Contains("Wolnorynkowa"))
            {
                var wolny = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r["Wolnorynkowa"] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r["Wolnorynkowa"])))
                    .OrderByDescending(t => t.Date)
                    .Take(7)
                    .ToList();
                if (wolny.Count >= 3)
                {
                    var cheapest = wolny.OrderBy(t => t.V).First();
                    var avg = wolny.Average(t => t.V);
                    var pct = avg == 0 ? 0 : (cheapest.V - avg) / avg * 100;
                    if (pct < -5)
                    {
                        result.Add(("info", "💰",
                            $"Wolnorynkowa najtańsza w 7 dniach: {cheapest.V:N2} zł ({cheapest.Date:dd.MM})",
                            $"To {pct:N1}% taniej niż średnia z tygodnia ({avg:N2} zł).",
                            "Rozważ większe zakupy w tym dniu tygodnia w przyszłości."));
                    }
                }
            }

            // 4. Anomalia - dzień z marżą >2σ od średniej
            if (margins.Count >= 5)
            {
                decimal avgM = margins.Average(t => t.Margin);
                double variance = margins.Sum(t => Math.Pow((double)(t.Margin - avgM), 2)) / (margins.Count - 1);
                double sd = Math.Sqrt(variance);
                if (sd > 0)
                {
                    var anomalies = margins.Where(t => Math.Abs((double)(t.Margin - avgM)) > 2.0 * sd).OrderBy(t => t.Margin).ToList();
                    if (anomalies.Any())
                    {
                        var worstAn = anomalies.First();
                        if (worstAn.Margin < avgM)
                        {
                            result.Add(("warning", "⚡",
                                $"Anomalia marży: {worstAn.Date:dd.MM.yyyy} = {worstAn.Margin:N2} zł",
                                $"Wartość poza ±2σ ({sd:N2}) od średniej ({avgM:N2} zł).",
                                "Sprawdź szczegóły dnia - możliwy błąd lub awaria."));
                        }
                    }
                }
            }

            // 5. Korelacja silna
            var strongest = ComputeStrongestCorrelation();
            if (strongest.HasValue && Math.Abs(strongest.Value.R) >= 0.85)
            {
                result.Add(("info", "🔗",
                    $"Bardzo silna korelacja: {strongest.Value.A} ↔ {strongest.Value.B} (r = {strongest.Value.R:+0.00;-0.00})",
                    "Te dwie ceny chodzą wspólnie - jak jedna rośnie, druga też.",
                    "Możesz traktować je jako parę przy planowaniu."));
            }

            // 6. Wolumen - znaczna zmiana
            if (filteredData.Columns.Contains("Wolumen kg"))
            {
                var volumes = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r["Wolumen kg"] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r["Wolumen kg"])))
                    .OrderBy(t => t.Date)
                    .ToList();
                if (volumes.Count >= 6)
                {
                    var firstHalf = volumes.Take(volumes.Count / 2).Average(t => t.V);
                    var secondHalf = volumes.Skip(volumes.Count / 2).Average(t => t.V);
                    if (firstHalf > 0)
                    {
                        var pct = (secondHalf - firstHalf) / firstHalf * 100;
                        if (pct < -20)
                        {
                            result.Add(("warning", "📉",
                                $"Wolumen zakupów MALEJE o {pct:N0}%",
                                $"Pierwsza połowa: {firstHalf:#,0} kg/dzień, druga: {secondHalf:#,0} kg/dzień.",
                                "Sprawdź dostawców - może tracimy kontrakty?"));
                        }
                        else if (pct > 20)
                        {
                            result.Add(("success", "📈",
                                $"Wolumen zakupów ROŚNIE +{pct:N0}%",
                                $"Pierwsza połowa: {firstHalf:#,0} kg/dzień, druga: {secondHalf:#,0} kg/dzień.",
                                "Dobry znak - zwiększona produkcja."));
                        }
                    }
                }
            }

            return result;
        }

        private Panel BuildAlertCard((string Severity, string Icon, string Title, string Description, string Action) alert, int width)
        {
            Color accent = alert.Severity switch
            {
                "critical" => dangerColor,
                "warning"  => Color.FromArgb(234, 88, 12),
                "success"  => successColor,
                _          => primaryColor
            };
            Color bgTint = alert.Severity switch
            {
                "critical" => Color.FromArgb(254, 242, 242),
                "warning"  => Color.FromArgb(255, 247, 237),
                "success"  => Color.FromArgb(240, 253, 244),
                _          => Color.FromArgb(239, 246, 255)
            };

            var card = new BufferedPanel
            {
                Width = width,
                Height = 92,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(16, 10, 16, 10)
            };
            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var bg = new SolidBrush(bgTint);
                    e.Graphics.FillPath(bg, path);
                    using var border = new Pen(accent, 1);
                    e.Graphics.DrawPath(border, path);
                    // Pasek akcentu z lewej
                    e.Graphics.SetClip(path);
                    using var accentBrush = new SolidBrush(accent);
                    e.Graphics.FillRectangle(accentBrush, 0, 0, 5, card.Height);
                    e.Graphics.ResetClip();
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            var lblIcon = new Label
            {
                Text = alert.Icon,
                Font = new Font("Segoe UI Emoji", 22F),
                ForeColor = accent,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 50, Height = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(8, 18)
            };
            var lblTitle = new Label
            {
                Text = alert.Title,
                Font = new Font("Segoe UI Emoji", 11F),
                ForeColor = accent,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(70, 12)
            };
            var lblDesc = new Label
            {
                Text = alert.Description,
                Font = new Font("Segoe UI Emoji", 9.5F),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(70, 36)
            };
            var lblAction = new Label
            {
                Text = "▶ Akcja: " + alert.Action,
                Font = new Font("Segoe UI Emoji", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(70, 56)
            };
            card.Controls.AddRange(new Control[] { lblIcon, lblTitle, lblDesc, lblAction });
            return card;
        }

        // ═════════════════ HELPER: small help button ═════════════════

        private Button BuildSmallHelpButton(Panel host, string title, string content)
        {
            var btn = new Button
            {
                Text = "?",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Size = new Size(32, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = primaryColor,
                Cursor = Cursors.Hand,
                TabStop = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btn.Location = new Point(host.Width - 50, 14);
            btn.FlatAppearance.BorderColor = primaryColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = primaryColor;
            btn.MouseEnter += (s, e) => btn.ForeColor = Color.White;
            btn.MouseLeave += (s, e) => btn.ForeColor = primaryColor;
            btn.Click += (s, e) => ShowHelpDialog(title, content);
            host.Resize += (s, e) => btn.Location = new Point(host.Width - 50, 14);
            return btn;
        }

        private void CreateKlienciTab(TabPage tab)
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10, 5, 10, 5)
            };
            var hint = new Label
            {
                Text = "💼 Top kontrahenci wg sprzedaży tuszki w wybranym okresie • źródło: Symfonia Handel (HM.DK + HM.DP + STContractors)",
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            klienciRefreshButton = new Button
            {
                Text = "🔄  Załaduj klientów",
                Size = new Size(180, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = primaryColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Emoji", 9.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 0, 0)
            };
            klienciRefreshButton.FlatAppearance.BorderSize = 0;
            klienciRefreshButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            klienciRefreshButton.Click += async (s, e) => await LoadKlienciAsync();
            klienciStatusLabel = new Label
            {
                Text = "",
                AutoSize = true,
                Margin = new Padding(20, 12, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            var headerFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            headerFlow.Controls.Add(klienciRefreshButton);
            headerFlow.Controls.Add(klienciStatusLabel);
            controlPanel.Controls.Add(headerFlow);
            controlPanel.Controls.Add(hint);

            klienciGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 32 },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = primaryColor,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    SelectionBackColor = primaryColor,
                    SelectionForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(5)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 250, 252)
                }
            };
            klienciGrid.CellFormatting += KlienciGrid_CellFormatting;

            // Empty-state placeholder rysowany ZA siatką
            klienciGrid.Paint += (s, e) =>
            {
                try
                {
                    if (klienciGrid.RowCount == 0)
                    {
                        using var font = new Font("Segoe UI", 12F, FontStyle.Italic);
                        using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));
                        var rect = klienciGrid.ClientRectangle;
                        var msg = klienciRefreshButton?.Enabled == true
                            ? "📦  Kliknij \"Załaduj klientów\" lub przełącz zakładkę aby pobrać dane"
                            : "⏳  Trwa ładowanie z bazy Handel…";
                        e.Graphics.DrawString(msg, font, brush, rect, new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        });
                    }
                }
                catch { /* Paint nie może rzucać - cicho */ }
            };

            // Włącz double-buffering dla siatki klientów
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, klienciGrid, new object[] { true });

            var footerHint = new Label
            {
                Text = "💡 Sortowanie: kliknij nagłówek kolumny  •  🟠 ostatnia sprzedaż >14d  •  🔴 >30d (alarm spadku częstotliwości)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Emoji", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            container.Controls.Add(controlPanel, 0, 0);
            container.Controls.Add(klienciGrid, 0, 1);
            container.Controls.Add(footerHint, 0, 2);

            tab.Controls.Add(container);
        }

        private void KlienciGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = klienciGrid.Columns[e.ColumnIndex].Name;

            // Formatowanie liczbowe per kolumna
            if (col == "WolumenKg" || col == "WartoscNetto")
            {
                if (e.Value is decimal d)
                {
                    e.Value = d.ToString("#,##0.00");
                    e.FormattingApplied = true;
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
            else if (col == "SredniaCena")
            {
                if (e.Value is decimal d2)
                {
                    e.Value = d2.ToString("0.00 zł");
                    e.FormattingApplied = true;
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    // Kolorowanie: czerwony jeśli poniżej średniej, zielony powyżej
                    if (klienciGrid.DataSource is DataTable dt && dt.Rows.Count > 0)
                    {
                        var avg = dt.AsEnumerable()
                            .Where(r => r["SredniaCena"] != DBNull.Value)
                            .Average(r => Convert.ToDecimal(r["SredniaCena"]));
                        e.CellStyle.ForeColor = d2 >= avg ? successColor : dangerColor;
                        e.CellStyle.Font = new Font(klienciGrid.Font, FontStyle.Bold);
                    }
                }
            }
            else if (col == "OstatniaSprzedaz")
            {
                if (e.Value is DateTime dt2)
                {
                    var days = (DateTime.Today - dt2.Date).Days;
                    e.Value = $"{dt2:dd.MM.yyyy} ({days}d temu)";
                    e.FormattingApplied = true;
                    if (days > 30)
                    {
                        e.CellStyle.ForeColor = dangerColor;
                        e.CellStyle.Font = new Font(klienciGrid.Font, FontStyle.Bold);
                    }
                    else if (days > 14)
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(234, 88, 12);
                    }
                }
            }
        }

        private async Task LoadKlienciAsync()
        {
            if (mainDateFrom == null || mainDateTo == null) return;
            klienciRefreshButton.Enabled = false;
            klienciStatusLabel.Text = "Ładowanie z bazy Handel...";

            DataTable result = null;
            string error = null;

            await Task.Run(() =>
            {
                try
                {
                    using var conn = new SqlConnection(connectionStringHandel);
                    conn.Open();

                    // FVS/sWZ = sprzedaż / wydanie zewnętrzne; filtr na Kurczak A (kod = 'Kurczak A', katalog = 67095)
                    // Pattern jak w AnalizaTygodniowaService: HM.DK + HM.DP + SSCommon.STContractors via DK.khid = C.id
                    var sql = @"
                        SELECT TOP 50
                            ISNULL(C.shortcut, '(brak nazwy)') AS Klient,
                            ISNULL(C.NIP, '') AS NIP,
                            CAST(SUM(DP.ilosc) AS decimal(12,2))    AS WolumenKg,
                            CAST(SUM(DP.wartNetto) AS decimal(12,2)) AS WartoscNetto,
                            CAST(SUM(DP.wartNetto) / NULLIF(SUM(DP.ilosc), 0) AS decimal(10,2)) AS SredniaCena,
                            COUNT(DISTINCT DP.data) AS Dni,
                            MAX(DP.data) AS OstatniaSprzedaz
                        FROM [HANDEL].[HM].[DP] DP
                        INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                        WHERE DP.kod = 'Kurczak A'
                            AND TW.katalog = 67095
                            AND DP.data >= @from
                            AND DP.data <= @to
                            AND DP.ilosc > 0
                            AND DK.anulowany = 0
                        GROUP BY C.shortcut, C.NIP
                        ORDER BY WartoscNetto DESC";

                    using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                    cmd.Parameters.AddWithValue("@from", mainDateFrom.Value.Date);
                    cmd.Parameters.AddWithValue("@to", mainDateTo.Value.Date);

                    var adapter = new SqlDataAdapter(cmd);
                    result = new DataTable();
                    adapter.Fill(result);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            });

            klienciRefreshButton.Enabled = true;
            if (error != null)
            {
                klienciStatusLabel.Text = $"❌ Błąd: {error}";
                klienciStatusLabel.ForeColor = dangerColor;
                return;
            }
            if (result == null || result.Rows.Count == 0)
            {
                klienciStatusLabel.Text = "Brak danych w wybranym okresie.";
                klienciStatusLabel.ForeColor = Color.Gray;
                klienciGrid.DataSource = null;
                klienciGrid.Invalidate(); // wymuś re-paint empty-state
                return;
            }

            klienciGrid.DataSource = result;
            // Włącz sortowanie po kliknięciu w nagłówek kolumny
            foreach (DataGridViewColumn dgc in klienciGrid.Columns)
                dgc.SortMode = DataGridViewColumnSortMode.Automatic;

            // Custom headers + tooltips
            void SetCol(string name, string header, string tip = null,
                        DataGridViewAutoSizeColumnMode mode = DataGridViewAutoSizeColumnMode.Fill,
                        int? fillWeight = null)
            {
                if (!klienciGrid.Columns.Contains(name)) return;
                var col = klienciGrid.Columns[name];
                col.HeaderText = header;
                if (tip != null) col.ToolTipText = tip;
                col.AutoSizeMode = mode;
                if (fillWeight.HasValue) col.FillWeight = fillWeight.Value;
            }
            SetCol("Klient", "Klient", "Nazwa skrócona kontrahenta (C.shortcut)", fillWeight: 200);
            SetCol("NIP", "NIP", "NIP kontrahenta", fillWeight: 90);
            SetCol("WolumenKg", "Wolumen [kg]", "Suma sprzedanego towaru w wybranym okresie", fillWeight: 90);
            SetCol("WartoscNetto", "Wartość [zł]", "Suma wartości netto sprzedaży", fillWeight: 100);
            SetCol("SredniaCena", "Śr. cena [zł/kg]", "Średnia cena ważona wolumenem.\nKolor: zielony = powyżej średniej rynkowej, czerwony = poniżej", fillWeight: 90);
            SetCol("Dni", "Dni z sprzedażą", "Ile różnych dat klient miał sprzedaż w okresie", fillWeight: 70);
            SetCol("OstatniaSprzedaz", "Ostatnia sprzedaż", "Data ostatniej sprzedaży.\nKolor: pomarańczowy >14 dni, czerwony >30 dni — sygnał alarmowy spadku częstotliwości", fillWeight: 130);

            decimal totalKg = result.AsEnumerable()
                .Where(r => r["WolumenKg"] != DBNull.Value)
                .Sum(r => Convert.ToDecimal(r["WolumenKg"]));
            decimal totalNetto = result.AsEnumerable()
                .Where(r => r["WartoscNetto"] != DBNull.Value)
                .Sum(r => Convert.ToDecimal(r["WartoscNetto"]));

            klienciStatusLabel.Text = $"✅ {result.Rows.Count} klientów  •  {totalKg:#,0} kg  •  {totalNetto:#,0} zł";
            klienciStatusLabel.ForeColor = successColor;
        }

        private Chart CreateEnhancedChart(string title)
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderlineColor = Color.Transparent,
                BorderlineWidth = 0,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            // Ulepszony obszar wykresu
            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.FromArgb(252, 253, 255),
                BackSecondaryColor = Color.White,
                BackGradientStyle = GradientStyle.TopBottom,
                ShadowColor = Color.Transparent,
                ShadowOffset = 0
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

            // Marginesy - dolna 25% rezerwa na osi X (rotated labels potrzebują miejsca)
            chartArea.Position = new ElementPosition(3, 3, 94, 92);
            chartArea.InnerPlotPosition = new ElementPosition(8, 6, 88, 75);

            // Crosshair / cursor — tylko wizualny "gdzie jestem", bez zoom-by-selection
            chartArea.CursorX.IsUserEnabled = true;
            chartArea.CursorX.IsUserSelectionEnabled = false;
            chartArea.CursorX.LineColor = Color.FromArgb(100, 33, 150, 243);
            chartArea.CursorX.LineWidth = 1;
            chartArea.CursorX.LineDashStyle = ChartDashStyle.Dash;
            chartArea.CursorY.IsUserEnabled = true;
            chartArea.CursorY.IsUserSelectionEnabled = false;
            chartArea.CursorY.LineColor = Color.FromArgb(60, 33, 150, 243);
            chartArea.CursorY.LineWidth = 1;
            chartArea.CursorY.LineDashStyle = ChartDashStyle.Dash;

            chart.ChartAreas.Add(chartArea);

            // Empty-state title (pokazujemy tylko gdy brak serii z punktami)
            var emptyTitle = new Title
            {
                Name = "EmptyTitle",
                Text = "Brak danych w wybranym okresie",
                Font = new Font("Segoe UI", 11F, FontStyle.Italic),
                ForeColor = Color.FromArgb(148, 163, 184),
                Docking = Docking.Bottom,
                Visible = false,
                IsDockedInsideChartArea = true,
                Alignment = ContentAlignment.MiddleCenter
            };
            chart.Titles.Add(emptyTitle);

            // Ulepszona legenda — flat, bez ramki
            var legend = new Legend
            {
                Name = "MainLegend",
                Docking = Docking.Top,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5F),
                ForeColor = Color.FromArgb(40, 40, 40),
                Alignment = StringAlignment.Center,
                BorderColor = Color.Transparent,
                BorderWidth = 0,
                ShadowOffset = 0
            };
            chart.Legends.Add(legend);

            // Tytuł wykresu — DockingOffset>0 aby nie zachodził na obszar wykresu
            var chartTitle = new Title
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 13F),
                ForeColor = Color.FromArgb(40, 40, 40),
                Docking = Docking.Top,
                DockingOffset = 4
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
                Padding = new Padding(16)
            };

            tab.Controls.Add(panel);
            tab.Tag = panel;
            _statsPanel = panel;
        }

        // Cel marży zakup→produkt z 08_Sprzedaz_ceny.md
        private const decimal MARGIN_TARGET = 2.50m;

        private StatusStrip CreateStatusBar()
        {
            var statusBar = new StatusStrip
            {
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.White,
                SizingGrip = false,
                Padding = new Padding(14, 0, 14, 0),
                Font = new Font("Segoe UI Emoji", 9.25F)
            };

            // Ikonka statusu (zielona kropka = OK)
            var lblStatusIcon = new ToolStripStatusLabel
            {
                Name = "lblStatusIcon",
                Text = "●",
                ForeColor = Color.FromArgb(74, 222, 128),  // zielony pulse
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Padding = new Padding(0, 0, 8, 0)
            };

            var lblStatus = new ToolStripStatusLabel
            {
                Name = "lblStatus",
                Text = "Gotowy",
                Spring = true,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblRecords = new ToolStripStatusLabel
            {
                Name = "lblRecords",
                Text = "📊 Rekordów: 0",
                ForeColor = Color.FromArgb(186, 230, 253),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                Padding = new Padding(14, 0, 14, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };

            var lblTime = new ToolStripStatusLabel
            {
                Name = "lblTime",
                Text = "🕐 " + DateTime.Now.ToString("HH:mm:ss"),
                ForeColor = Color.FromArgb(148, 163, 184),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                Padding = new Padding(14, 0, 0, 0),
                Font = new Font("Segoe UI Emoji", 9F)
            };

            statusBar.Items.AddRange(new ToolStripItem[] { lblStatusIcon, lblStatus, lblRecords, lblTime });

            // Timer do aktualizacji czasu (z emoji prefix)
            var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) => lblTime.Text = "🕐 " + DateTime.Now.ToString("HH:mm:ss");
            timer.Start();

            return statusBar;
        }

        #endregion

        #region Ładowanie i przetwarzanie danych

        private async Task LoadDataAsync()
        {
            try
            {
                UpdateStatusMessage("⏳ Ładowanie danych z LibraNet + Handel…");
                // Visual feedback - wyłącz interakcję na czas ładowania
                this.UseWaitCursor = true;

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

                // Imputacja Wolnorynkowej (interpolacja liniowa po dniach)
                ImputeMissingValues(originalData, "Wolnorynkowa");

                // Po imputacji przelicz "Rolnicza-Wolny" tam gdzie wcześniej było puste
                RecomputeRolniczaWolny(originalData);

                // Cache uporządkowanej listy rzędów (do KPI strip i innych widoków)
                _orderedByDateDesc = originalData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value)
                    .OrderByDescending(r => Convert.ToDateTime(r["DataRaw"]))
                    .ToList();

                // Cache sezonowości — wyliczany raz, używany w Paint
                BuildSezonowoscCache();

                // Zastosuj filtry od razu
                await FilterDataAsync();

                UpdateStatusMessage($"✅ Załadowano {originalData.Rows.Count} rekordów");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusMessage("❌ Błąd ładowania — załadowano dane przykładowe");

                // Załaduj przykładowe dane
                LoadSampleData();
                await FilterDataAsync();
            }
            finally
            {
                this.UseWaitCursor = false;
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
    ),
    CTE_HarmonogramAll AS (
        SELECT
            DataOdbioru AS Data,
            CAST(
                SUM(CAST(Cena AS DECIMAL(18, 4)) * CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18, 2)))
                / NULLIF(SUM(CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18, 2))), 0)
            AS DECIMAL(10, 2)) AS SredniaCena,
            CAST(SUM(CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18, 2)) * CAST(ISNULL(WagaDek, 0) AS DECIMAL(18, 2))) AS DECIMAL(12, 2)) AS WolumenKg
        FROM [LibraNet].[dbo].[HarmonogramDostaw]
        WHERE Bufor = 'Potwierdzony'
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
        CAST(HDA.SredniaCena AS DECIMAL(10,2)) AS [Śr. Wszystkich Dostaw],
        CAST(HDA.WolumenKg AS DECIMAL(12,2)) AS [Wolumen kg],
        CAST(NULL AS DECIMAL(10,2)) AS [Nasza Tuszka],
        CAST(NULL AS DECIMAL(10,2)) AS [Różnica Tuszek]
    FROM CTE_Ministerialna M
    FULL OUTER JOIN CTE_Rolnicza R ON M.Data = R.Data
    FULL OUTER JOIN CTE_Tuszka T ON COALESCE(M.Data, R.Data) = T.Data
    LEFT JOIN CTE_Harmonogram HD ON COALESCE(M.Data, R.Data, T.Data) = HD.Data
    LEFT JOIN CTE_HarmonogramAll HDA ON COALESCE(M.Data, R.Data, T.Data) = HDA.Data
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

        /// <summary>
        /// Wypełnia luki w kolumnie cenowej interpolacją liniową po dacie.
        /// Dodaje kolumnę "<col>Imputed" (bool) oznaczającą uzupełnione wartości.
        /// Nie wypełnia wartości na początku/końcu zakresu (ekstrapolacji nie robimy).
        /// </summary>
        private void ImputeMissingValues(DataTable table, string colName)
        {
            if (table == null || !table.Columns.Contains(colName)) return;
            string flagCol = colName + "Imputed";
            if (!table.Columns.Contains(flagCol))
            {
                var c = new DataColumn(flagCol, typeof(bool)) { DefaultValue = false };
                table.Columns.Add(c);
            }

            // Posortuj rzędy po dacie rosnąco - nie modyfikuje DataTable, tylko utworzy listę
            var ordered = table.AsEnumerable()
                .Where(r => r["DataRaw"] != DBNull.Value)
                .OrderBy(r => Convert.ToDateTime(r["DataRaw"]))
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                if (row[colName] != DBNull.Value) continue;

                // Znajdź najbliższy poprzedni z wartością
                DataRow prev = null;
                DateTime prevDate = DateTime.MinValue;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (ordered[j][colName] != DBNull.Value)
                    {
                        prev = ordered[j];
                        prevDate = Convert.ToDateTime(prev["DataRaw"]).Date;
                        break;
                    }
                }
                // Znajdź najbliższy następny z wartością
                DataRow next = null;
                DateTime nextDate = DateTime.MinValue;
                for (int j = i + 1; j < ordered.Count; j++)
                {
                    if (ordered[j][colName] != DBNull.Value)
                    {
                        next = ordered[j];
                        nextDate = Convert.ToDateTime(next["DataRaw"]).Date;
                        break;
                    }
                }

                if (prev == null || next == null) continue; // brzeg - nie ekstrapolujemy

                var d = Convert.ToDateTime(row["DataRaw"]).Date;
                var prevVal = Convert.ToDecimal(prev[colName]);
                var nextVal = Convert.ToDecimal(next[colName]);
                var spanDays = (nextDate - prevDate).TotalDays;
                if (spanDays <= 0) continue;
                var t = (decimal)((d - prevDate).TotalDays / spanDays);
                var interpolated = prevVal + (nextVal - prevVal) * t;

                row[colName] = Math.Round(interpolated, 2);
                row[flagCol] = true;
            }
        }

        private void RecomputeRolniczaWolny(DataTable table)
        {
            if (table == null) return;
            if (!table.Columns.Contains("Rolnicza") || !table.Columns.Contains("Wolnorynkowa") || !table.Columns.Contains("Rolnicza-Wolny"))
                return;

            foreach (DataRow row in table.Rows)
            {
                if (row["Rolnicza"] != DBNull.Value && row["Wolnorynkowa"] != DBNull.Value)
                {
                    var diff = Convert.ToDecimal(row["Rolnicza"]) - Convert.ToDecimal(row["Wolnorynkowa"]);
                    row["Rolnicza-Wolny"] = Math.Round(diff, 2);
                }
            }
        }

        /// <summary>Czy w danym wierszu kolumna jest uzupełniona (imputed).</summary>
        private static bool IsImputed(DataRow row, string colName)
        {
            string flagCol = colName + "Imputed";
            if (row.Table.Columns.Contains(flagCol)
                && row[flagCol] != DBNull.Value
                && Convert.ToBoolean(row[flagCol]))
                return true;
            return false;
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
            originalData.Columns.Add("Śr. Wszystkich Dostaw", typeof(decimal));
            originalData.Columns.Add("Wolumen kg", typeof(decimal));
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

                var srWszystkich = Math.Round((rolnicza + wolnorynkowa) / 2m, 2);
                var wolumen = Math.Round((decimal)(random.NextDouble() * 30000 + 50000), 2);
                originalData.Rows.Add(
                    date,
                    date.ToString("yyyy-MM-dd ddd"),
                    minister,
                    laczona,
                    rolnicza,
                    wolnorynkowa,
                    Math.Round(rolnicza - wolnorynkowa, 2),
                    tuszkaZrzesz,
                    srWszystkich,
                    wolumen,
                    naszaTuszka,
                    Math.Round(naszaTuszka - tuszkaZrzesz, 2)
                );
            }
        }

        // Cache min/max per kolumna do heat-mapy (przeliczany przy formatowaniu)
        private Dictionary<string, (decimal Min, decimal Max)> _heatMapRange = new();

        // Cache fontów dla CellFormatting (gorący hot-path) - zapobiega tworzeniu Font na każdą komórkę
        private Font _gridFontBold;
        private Font _gridFontItalicBold;

        // Cache indeksów kolumn dla DataGridView1_CellFormatting
        private int _idxDataRaw = -1;
        private int _idxRolniczaWolny = -1;
        private int _idxRoznicaTuszek = -1;
        private int _idxWolnorynkowa = -1;
        private int _idxWolnorynkowaImputed = -1;

        // Cache uporządkowanej listy rzędów dla UpdateKpiStrip (sortowanie odwracalne)
        private List<DataRow> _orderedByDateDesc;

        // Debounce timer dla wyszukiwarki (avoid lag przy każdym znaku)
        private Timer _searchDebounceTimer;

        // Cache sezonowości per kolumna (DoY → średnia cena)
        private readonly Dictionary<string, Dictionary<int, double>> _sezonowoscCache = new();
        private readonly Dictionary<string, (double Min, double Max)> _sezonowoscRange = new();

        // Cache dla tooltipa na heatmapie
        private ToolTip _sezonowoscTooltip;
        private (int LeftMargin, int TopMargin, int CellW, int CellH) _sezonGeometry;

        private void FormatDataGrid()
        {
            if (dataGridView1.Columns.Count == 0) return;

            // Ukryj kolumnę DataRaw + flagę imputacji
            if (dataGridView1.Columns.Contains("DataRaw"))
                dataGridView1.Columns["DataRaw"].Visible = false;
            if (dataGridView1.Columns.Contains("WolnorynkowaImputed"))
                dataGridView1.Columns["WolnorynkowaImputed"].Visible = false;

            // Cache indeksów kolumn (uniknięcie string-lookups w CellFormatting)
            _idxDataRaw = dataGridView1.Columns.Contains("DataRaw") ? dataGridView1.Columns["DataRaw"].Index : -1;
            _idxRolniczaWolny = dataGridView1.Columns.Contains("Rolnicza-Wolny") ? dataGridView1.Columns["Rolnicza-Wolny"].Index : -1;
            _idxRoznicaTuszek = dataGridView1.Columns.Contains("Różnica Tuszek") ? dataGridView1.Columns["Różnica Tuszek"].Index : -1;
            _idxWolnorynkowa = dataGridView1.Columns.Contains("Wolnorynkowa") ? dataGridView1.Columns["Wolnorynkowa"].Index : -1;
            _idxWolnorynkowaImputed = dataGridView1.Columns.Contains("WolnorynkowaImputed") ? dataGridView1.Columns["WolnorynkowaImputed"].Index : -1;

            // Cache nazw kolumn po indeksie (do CellFormatting bez string-lookup)
            _columnNamesByIndex = new string[dataGridView1.Columns.Count];
            for (int i = 0; i < dataGridView1.Columns.Count; i++)
                _columnNamesByIndex[i] = dataGridView1.Columns[i].Name;

            // Przelicz zakresy heat mapy z aktualnie wyświetlonych danych
            _heatMapRange.Clear();
            if (filteredData != null && filteredData.Rows.Count > 0)
            {
                foreach (var col in _priceColumns)
                {
                    if (!filteredData.Columns.Contains(col)) continue;
                    var values = filteredData.AsEnumerable()
                        .Where(r => r[col] != DBNull.Value)
                        .Select(r => Convert.ToDecimal(r[col]))
                        .ToList();
                    if (values.Count > 1)
                        _heatMapRange[col] = (values.Min(), values.Max());
                }
            }

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
                var search = (_searchText ?? string.Empty).Trim().ToLowerInvariant();

                var filtered = originalData.AsEnumerable().Where(row =>
                {
                    if (row["DataRaw"] != DBNull.Value)
                    {
                        var date = Convert.ToDateTime(row["DataRaw"]);
                        if (date < dateFrom || date > dateTo)
                            return false;

                        if (!showWeekends && (date.DayOfWeek == DayOfWeek.Saturday ||
                                             date.DayOfWeek == DayOfWeek.Sunday))
                            return false;
                    }

                    // Wyszukiwanie po dowolnej kolumnie (toString)
                    if (!string.IsNullOrEmpty(search))
                    {
                        bool anyMatch = false;
                        foreach (DataColumn col in originalData.Columns)
                        {
                            if (col.ColumnName == "DataRaw") continue;
                            var val = row[col]?.ToString();
                            if (!string.IsNullOrEmpty(val) && val.ToLowerInvariant().Contains(search))
                            {
                                anyMatch = true;
                                break;
                            }
                        }
                        if (!anyMatch) return false;
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
                    UpdateMarginChart();
                    UpdateOverlayChart();
                    sezonowoscPanel?.Invalidate();
                    UpdateTygodnieView();
                    yoyChartPanel?.Invalidate();
                    _ = LoadKontraktyDataAsync().ContinueWith(_ =>
                    {
                        try
                        {
                            if (kontraktyChartPanel != null && !kontraktyChartPanel.IsDisposed)
                                kontraktyChartPanel.BeginInvoke(new Action(() =>
                                {
                                    try { kontraktyChartPanel?.Invalidate(); } catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
                                }));
                        }
                        catch { /* form mogło zostać zamknięte */ }
                    });
                    paszeChartPanel?.Invalidate();
                    UpdateStatistics();
                    UpdateKpiStrip();
                    UpdateAggregatesFooter();
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
                    if (chkLabelsPurchase?.Checked == true) ApplyAllPointLabels(series);
                    else AddEndLabel(series);
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
                    if (chkLabelsSales?.Checked == true) ApplyAllPointLabels(series);
                    else AddEndLabel(series);
                    salesChart.Series.Add(series);
                }
            }

            AdjustChartScale(salesChart);
        }

        private FlowLayoutPanel BuildAggregationButtonsRow()
        {
            var host = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(20, 4, 0, 0),
                BackColor = Color.Transparent
            };

            var lblAgg = new Label
            {
                Text = "📅 Agregacja:",
                AutoSize = true,
                Margin = new Padding(0, 9, 8, 0),
                Font = new Font("Segoe UI Emoji", 9F),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            host.Controls.Add(lblAgg);

            (string Label, AggregationMode Mode)[] options =
            {
                ("Dzień",    AggregationMode.Day),
                ("Tydzień",  AggregationMode.Week),
                ("Miesiąc",  AggregationMode.Month),
                ("Kwartał",  AggregationMode.Quarter),
                ("Rok",      AggregationMode.Year)
            };

            foreach (var (label, mode) in options)
            {
                var btn = CreatePresetButton(label);
                btn.Tag = mode; // re-tag z preset name na enum
                btn.Width = 70;
                if (mode == _aggMode)
                {
                    btn.BackColor = primaryColor;
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.BorderColor = primaryColor;
                }
                btn.Click += (s, e) =>
                {
                    _aggMode = mode;
                    SetActiveAggButton(mode);
                    UpdatePurchaseChart();
                    UpdateSalesChart();
                    UpdateMarginChart();
                    UpdateOverlayChart();
                };
                _aggButtons.Add(btn);
                host.Controls.Add(btn);
            }

            return host;
        }

        private void SetActiveAggButton(AggregationMode mode)
        {
            foreach (var btn in _aggButtons)
            {
                bool active = btn.Tag is AggregationMode m && m == mode;
                btn.BackColor = active ? primaryColor : Color.White;
                btn.ForeColor = active ? Color.White : Color.FromArgb(71, 85, 105);
                btn.FlatAppearance.BorderColor = active ? primaryColor : subtleBorderColor;
            }
        }

        /// <summary>
        /// Agreguje pary (data, wartość) zgodnie z _aggMode.
        /// Day → bez zmian. Week/Month/Quarter/Year → grupuje i wylicza średnią.
        /// imputedSourceColumn: jeśli ustawione, dla każdego rzędu sprawdza flagę &lt;col&gt;Imputed
        /// </summary>
        private List<(DateTime Date, double Value, bool ImputedAny)> AggregateForChart(
            string columnName, Func<DataRow, double?> valueSelector, string imputedSourceColumn = null)
        {
            if (filteredData == null || filteredData.Rows.Count == 0)
                return new List<(DateTime, double, bool)>();

            string flagColumn = imputedSourceColumn ?? (columnName == "Wolnorynkowa" ? "Wolnorynkowa" : null);

            var rows = filteredData.AsEnumerable()
                .Where(r => r["DataRaw"] != DBNull.Value)
                .Select(r =>
                {
                    var d = Convert.ToDateTime(r["DataRaw"]).Date;
                    var v = valueSelector(r);
                    bool imputed = flagColumn != null && IsImputed(r, flagColumn);
                    return (Date: d, Value: v, Imputed: imputed);
                })
                .Where(t => t.Value.HasValue)
                .OrderBy(t => t.Date)
                .ToList();

            if (_aggMode == AggregationMode.Day)
                return rows.Select(t => (t.Date, t.Value.Value, t.Imputed)).ToList();

            DateTime BucketStart(DateTime d)
            {
                return _aggMode switch
                {
                    AggregationMode.Week    => StartOfIsoWeek(d),
                    AggregationMode.Month   => new DateTime(d.Year, d.Month, 1),
                    AggregationMode.Quarter => new DateTime(d.Year, ((d.Month - 1) / 3) * 3 + 1, 1),
                    AggregationMode.Year    => new DateTime(d.Year, 1, 1),
                    _ => d
                };
            }

            return rows
                .GroupBy(t => BucketStart(t.Date))
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Average(x => x.Value.Value), g.Any(x => x.Imputed)))
                .ToList();
        }

        private static DateTime StartOfIsoWeek(DateTime date)
        {
            int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return date.AddDays(-diff).Date;
        }

        private string GetAggregationXLabel()
        {
            return _aggMode switch
            {
                AggregationMode.Week    => "Tydzień (od poniedziałku)",
                AggregationMode.Month   => "Miesiąc",
                AggregationMode.Quarter => "Kwartał",
                AggregationMode.Year    => "Rok",
                _ => "Data"
            };
        }

        private string GetAggregationXFormat()
        {
            return _aggMode switch
            {
                AggregationMode.Week    => "dd.MM.yy",
                AggregationMode.Month   => "MMM yyyy",
                AggregationMode.Quarter => "MM.yyyy",
                AggregationMode.Year    => "yyyy",
                _ => "dd.MM"
            };
        }

        /// <summary>
        /// Buduje czytelny tekst etykiety dla danej daty wg _aggMode.
        /// Stosowane w CustomLabels osi X (tydzień/kwartał - bardziej opisowe niż format).
        /// </summary>
        private string FormatXLabel(DateTime d)
        {
            return _aggMode switch
            {
                AggregationMode.Week =>
                    "T" + System.Globalization.ISOWeek.GetWeekOfYear(d).ToString("00") +
                    "  " + d.ToString("dd.MM"),
                AggregationMode.Month   => d.ToString("MMM yyyy", new System.Globalization.CultureInfo("pl-PL")),
                AggregationMode.Quarter => "Q" + (((d.Month - 1) / 3) + 1) + " " + d.Year,
                AggregationMode.Year    => d.Year.ToString(),
                _ => d.ToString("dd.MM")
            };
        }

        private Series CreateMovingAverageSeries(Series sourceSeries, string name, int window, Color baseColor)
        {
            var maSeries = new Series(name)
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 2,
                BorderDashStyle = ChartDashStyle.Dash,
                Color = Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B),
                MarkerStyle = MarkerStyle.None,
                IsVisibleInLegend = true,
                ToolTip = $"{name}\nData: #VALX{{dd.MM.yyyy}}\nŚrednia: #VALY{{#,##0.00}} zł"
            };

            var pts = sourceSeries.Points.ToList();
            for (int i = 0; i < pts.Count; i++)
            {
                if (i < window - 1) continue; // brak pełnego okna
                double sum = 0;
                int count = 0;
                for (int j = i - window + 1; j <= i; j++)
                {
                    sum += pts[j].YValues[0];
                    count++;
                }
                double avg = sum / count;
                maSeries.Points.AddXY(DateTime.FromOADate(pts[i].XValue), Math.Round(avg, 2));
            }

            return maSeries;
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
                ToolTip = "#SERIESNAME\n" + GetAggregationXLabel() + ": #VALX{" + GetAggregationXFormat() + "}\nCena: #VALY{#,##0.00} zł",
                IsVisibleInLegend = true,
                IsValueShownAsLabel = false
            };

            series["ShadowOffset"] = "1";
            series["EmptyPointValue"] = "Average";

            var aggregated = AggregateForChart(column, r => r[column] != DBNull.Value ? (double?)Convert.ToDouble(r[column]) : null);

            foreach (var (date, value, imputedAny) in aggregated)
            {
                int idx = series.Points.AddXY(date, Math.Round(value, 2));
                // Punkt z imputowaną wartością — niebieski marker
                if (column == "Wolnorynkowa" && imputedAny)
                {
                    var pt = series.Points[idx];
                    pt.MarkerColor = Color.FromArgb(37, 99, 235);
                    pt.MarkerStyle = MarkerStyle.Diamond;
                    pt.MarkerSize = 8;
                    pt.ToolTip = $"{name} (uzupełnione)\n{GetAggregationXLabel()}: {date.ToString(GetAggregationXFormat())}\nCena: {value:N2} zł";
                }
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

        /// <summary>
        /// Włącza etykiety wartości dla każdego punktu serii (kolor zgodny z serią,
        /// kontur biały dla czytelności na różnych tłach).
        /// </summary>
        private void ApplyAllPointLabels(Series series)
        {
            if (series.Points.Count == 0) return;

            for (int i = 0; i < series.Points.Count; i++)
            {
                var pt = series.Points[i];
                pt.IsValueShownAsLabel = true;
                pt.LabelFormat = "0.00";
                pt.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                pt.LabelForeColor = series.Color;
                pt.LabelBackColor = Color.FromArgb(220, 255, 255, 255);
                pt.LabelBorderColor = Color.Transparent;
                pt.LabelBorderWidth = 0;
            }
        }

        private void AdjustChartScale(Chart chart)
        {
            // Empty-state — pokaż label, ukryj reszta tytułów
            bool hasData = chart.Series.Count > 0 && chart.Series.SelectMany(s => s.Points).Any();
            var emptyTitle = chart.Titles.FindByName("EmptyTitle");
            if (emptyTitle != null) emptyTitle.Visible = !hasData;
            if (!hasData)
            {
                // Wyczyść CustomLabels żeby nie pokazywać starych
                if (chart.ChartAreas.Count > 0)
                    chart.ChartAreas[0].AxisX.CustomLabels.Clear();
                chart.Invalidate();
                return;
            }

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

            // Dostosuj oś X w zależności od trybu agregacji
            if (filteredData != null && filteredData.Rows.Count > 0)
            {
                var ax = chart.ChartAreas[0].AxisX;
                ax.Title = GetAggregationXLabel();

                // Standardowy format (gdy CustomLabels się nie zaaplikuje)
                ax.LabelStyle.Format = GetAggregationXFormat();
                ax.LabelStyle.Angle = -45;
                ax.LabelStyle.IsEndLabelVisible = true;
                ax.IsLabelAutoFit = true;

                // CustomLabels — czytelne etykiety per punkt dla agregacji week/month/quarter/year
                ax.CustomLabels.Clear();

                switch (_aggMode)
                {
                    case AggregationMode.Day:
                        var daysDiff = (mainDateTo.Value - mainDateFrom.Value).Days;
                        ax.IntervalType = DateTimeIntervalType.Days;
                        // Większe odstępy między etykietami - cel ~12-14 etykiet maks
                        if (daysDiff <= 7) ax.Interval = 1;
                        else if (daysDiff <= 14) ax.Interval = 2;
                        else if (daysDiff <= 30) ax.Interval = 3;
                        else if (daysDiff <= 60) ax.Interval = 5;
                        else if (daysDiff <= 90) ax.Interval = 7;
                        else if (daysDiff <= 180) ax.Interval = 14;
                        else if (daysDiff <= 365) ax.Interval = 30;
                        else ax.Interval = 60;
                        ax.LabelStyle.Angle = -45;
                        ax.LabelStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        ax.LabelStyle.ForeColor = Color.FromArgb(71, 85, 105);
                        break;
                    case AggregationMode.Week:
                        ax.IntervalType = DateTimeIntervalType.Weeks;
                        ax.Interval = 1;
                        ApplyCustomLabelsFromSeries(chart);
                        break;
                    case AggregationMode.Month:
                        ax.IntervalType = DateTimeIntervalType.Months;
                        ax.Interval = 1;
                        ApplyCustomLabelsFromSeries(chart);
                        break;
                    case AggregationMode.Quarter:
                        ax.IntervalType = DateTimeIntervalType.Months;
                        ax.Interval = 3;
                        ApplyCustomLabelsFromSeries(chart);
                        break;
                    case AggregationMode.Year:
                        ax.IntervalType = DateTimeIntervalType.Years;
                        ax.Interval = 1;
                        ApplyCustomLabelsFromSeries(chart);
                        break;
                }
            }

            chart.Invalidate();
        }

        /// <summary>
        /// Generuje CustomLabels osi X na podstawie unikalnych X-ów w seriach.
        /// Dla trybu Week, Month, Quarter, Year — bo formaty DateTime nie wystarczą.
        /// Pomija etykiety gdy ich liczba przekracza próg (zbyt gęste) — pokazuje co N-tą.
        /// </summary>
        private void ApplyCustomLabelsFromSeries(Chart chart)
        {
            var ax = chart.ChartAreas[0].AxisX;
            ax.CustomLabels.Clear();

            // Zbierz unikalne X (jako DateTime) ze wszystkich serii
            var dates = chart.Series
                .SelectMany(s => s.Points)
                .Select(p => DateTime.FromOADate(p.XValue).Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (dates.Count == 0) return;

            // Szerokość przedziału etykiety zależy od kroku
            double halfStep = _aggMode switch
            {
                AggregationMode.Week    => 3.5,    // dni (pół tygodnia)
                AggregationMode.Month   => 14.0,   // dni (pół miesiąca)
                AggregationMode.Quarter => 45.0,   // dni (pół kwartału)
                AggregationMode.Year    => 180.0,
                _ => 0.5
            };

            // SKIP FACTOR — pomijamy co N-tą etykietę gdy jest ich za dużo
            // Cel: ~10-14 widocznych etykiet, niezależnie od szerokości okresu
            int targetLabels = _aggMode switch
            {
                AggregationMode.Week    => 14,
                AggregationMode.Month   => 12,
                AggregationMode.Quarter => 12,
                AggregationMode.Year    => 10,
                _ => 14
            };
            int skipFactor = Math.Max(1, (int)Math.Ceiling((double)dates.Count / targetLabels));

            for (int i = 0; i < dates.Count; i++)
            {
                // Pomijamy etykiety całkowicie (nie dodajemy CustomLabel) - oś X auto rozłoży się
                bool showLabel = (i % skipFactor == 0) || (i == dates.Count - 1);
                if (!showLabel) continue;

                var d = dates[i];
                var from = d.AddDays(-halfStep).ToOADate();
                var to = d.AddDays(halfStep).ToOADate();
                ax.CustomLabels.Add(new CustomLabel(from, to, FormatXLabel(d), 0, LabelMarkStyle.SideMark));
            }

            // Mniejszy kąt obrotu (lepiej czytelne) + większa czcionka
            ax.LabelStyle.Angle = -35;
            ax.LabelStyle.Font = new Font("Segoe UI", 9.25F, FontStyle.Bold);
            ax.LabelStyle.ForeColor = Color.FromArgb(51, 65, 85);
            ax.LabelAutoFitMaxFontSize = 11;
            ax.LabelAutoFitMinFontSize = 8;
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
            var statsPanel = _statsPanel;
            if (statsPanel == null || filteredData == null) return;

            statsPanel.SuspendLayout();
            statsPanel.Controls.Clear();

            int contentWidth = Math.Max(800, statsPanel.ClientSize.Width - 40);

            // SEKCJA 1: HERO 4 KARTY
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "🎯  Kluczowe wskaźniki",
                contentWidth,
                "Kluczowe wskaźniki",
                "Cztery główne wskaźniki Twojej działalności w wybranym okresie:\n\n" +
                "📈 MARŻA AKTUALNA\n" +
                "Najnowsza wartość różnicy: cena sprzedaży tuszki minus średnia ważona ceny zakupu wszystkich potwierdzonych dostaw.\n" +
                "Pasek progress pokazuje gdzie jesteś względem CELU 2,50 zł/kg (z procedur firmowych).\n" +
                "Kolory: zielony ≥ 2,50 (cel) • pomarańczowy 1,50–2,50 • czerwony < 1,50.\n\n" +
                "📊 MARŻA ŚREDNIA\n" +
                "Średnia marża dzienna w całym wybranym okresie. Jeśli > 2,50 zł/kg → biznes idzie wg planu.\n\n" +
                "💰 WOLUMEN ZAKUPÓW\n" +
                "Łącznie ile ton kurczaka żywego kupiono w okresie (suma SztDek × WagaDek z tabeli HarmonogramDostaw, tylko dostawy potwierdzone).\n" +
                "Dni potwierdzonych = w ile różnych dni odebrano dostawy.\n\n" +
                "🏆 SKRAJNE DNI\n" +
                "Najlepszy i najgorszy dzień marży w okresie (zielony / czerwony) z datami. Kliknij w bardziej szczegółowej sekcji niżej aby zobaczyć szczegóły.\n\n" +
                "💡 JAK INTERPRETOWAĆ:\n" +
                "• Marża aktualna spada poniżej 2,00 zł → sygnał alarmowy, sprawdź ceny zakupu i sprzedaży\n" +
                "• Trend ujemny w marży → przejrzyj wolumeny i może zacznij negocjować ceny zakupu\n" +
                "• Wolumen wysoki + niska marża → kupujesz dużo drogo, szukaj tańszych dostawców"));
            statsPanel.Controls.Add(BuildMarginHeroStrip(contentWidth));

            // SEKCJA 1B: INTELIGENTNE WNIOSKI - auto-generowane insights z danych
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "💡  Inteligentne wnioski (auto-analiza)",
                contentWidth,
                "Inteligentne wnioski",
                "Sekcja AUTOMATYCZNIE analizuje dane z wybranego okresu i generuje wnioski w punktach.\n\n" +
                "🔍 CO ANALIZUJEMY:\n" +
                "• Trend marży (kierunek + długość ciągu)\n" +
                "• Alarmy: marża poniżej celu 2,50 zł/kg\n" +
                "• Trend wolumenu (rosnący / spadający / stabilny)\n" +
                "• Najtańsza cena zakupu w okresie i jej dzień\n" +
                "• Najwyższa cena sprzedaży i jej dzień\n" +
                "• Najsilniejsza korelacja między cenami\n" +
                "• Anomalie / ekstrema\n\n" +
                "💡 JAK WYKORZYSTAĆ:\n" +
                "Czytaj wnioski codziennie rano. Każdy bullet to potencjalna decyzja:\n" +
                "• ▼ Marża spada N dni → przejrzyj ceny zakupu/sprzedaży, eskalacja jeśli >5 dni\n" +
                "• 🚨 Marża < 2,50 → alert dla zarządu, sprawdź czy wynagradza Cię wolumen\n" +
                "• 💰 Tani zakup w X dniu → zapamiętaj wzór, można powtórzyć\n" +
                "• 📊 Korelacja ≥ 0,8 → dwie ceny chodzą razem, można je traktować łącznie"));
            statsPanel.Controls.Add(BuildSmartInsightsCard(contentWidth));

            // SEKCJA 1C: MARŻA PER DZIEŃ TYGODNIA
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "📅  Marża per dzień tygodnia",
                contentWidth,
                "Marża per dzień tygodnia",
                "Wykres słupkowy pokazujący ŚREDNIĄ MARŻĘ dla każdego dnia tygodnia w wybranym okresie.\n\n" +
                "📊 CO POKAZUJE:\n" +
                "Każdy słupek = jeden dzień tygodnia (Pn, Wt, Śr, Cz, Pt, Sb, Nd).\n" +
                "Wysokość = średnia marża w zł/kg w tym dniu tygodnia.\n" +
                "W etykiecie: średnia + liczba dni w analizie.\n\n" +
                "🎨 KOLORY (jak w bar chart marży dziennej):\n" +
                "• Zielony — średnia ≥ cel 2,50\n" +
                "• Żółty — 1,50–2,50\n" +
                "• Pomarańczowy — 0–1,50\n" +
                "• Czerwony — ujemna\n\n" +
                "💡 BIZNESOWO:\n" +
                "• Jeśli czwartek systematycznie ma niską marżę → pytanie czemu? Pewnie konkretny klient/kontrakt?\n" +
                "• Jeśli sobota ma wysoką marżę → planuj duże zakupy/sprzedaż na sobotę\n" +
                "• Stabilność wszystkich dni → biznes przewidywalny\n" +
                "• Duża wariancja → ryzyko, eskalacja"));
            statsPanel.Controls.Add(BuildDayOfWeekChart(contentWidth));

            // SEKCJA 2: BAR CHART MARŻY DZIENNEJ
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "📊  Marża dzienna względem celu 2,50 zł/kg",
                contentWidth,
                "Marża dzienna - wykres słupkowy",
                "Każdy słupek = jeden dzień ubojowy w okresie.\n" +
                "Wysokość = marża w zł/kg (Nasza Tuszka − średnia ważona dostaw potwierdzonych).\n\n" +
                "🎨 KODOWANIE KOLORAMI:\n" +
                "• Zielony — marża ≥ 2,50 zł/kg (cel firmowy osiągnięty)\n" +
                "• Żółty — marża 1,50–2,50 zł/kg (akceptowalna)\n" +
                "• Pomarańczowy — marża 0–1,50 zł/kg (niska, do poprawy)\n" +
                "• Czerwony — marża ujemna (TRACIMY)\n\n" +
                "📍 LINIE ODNIESIENIA:\n" +
                "• Linia ciągła pozioma na 0 zł\n" +
                "• Linia przerywana (zielona) na poziomie celu 2,50 zł/kg\n\n" +
                "🔢 ETYKIETY:\n" +
                "Wewnątrz każdego słupka znajduje się wartość marży w zł/kg (bez 'zł', np. 2,15).\n\n" +
                "💡 JAK CZYTAĆ:\n" +
                "• Dużo zielonych słupków → biznes na plus\n" +
                "• Mieszanka kolorów → marża nieprzewidywalna, sprawdź co się dzieje w gorsze dni\n" +
                "• Wszystkie słupki pomarańczowe/czerwone → coś jest źle z cenami, eskalacja do Sergiusza"));
            statsPanel.Controls.Add(BuildDailyMarginBarChart(contentWidth));

            // SEKCJA 3: TOP / BOTTOM dni wg marży
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "🏆  Najlepsze i najgorsze dni marży (kliknij dzień → szczegóły)",
                contentWidth,
                "Top 10 / Bottom 10 dni",
                "Dwa rankingi pomagające szybko zidentyfikować skrajne dni:\n\n" +
                "🥇 TOP 10 NAJLEPSZYCH DNI\n" +
                "Posortowane od najwyższej marży. Te dni warto przeanalizować — czemu się udało?\n" +
                "Może niska cena zakupu? Wysoka cena sprzedaży? Konkretny hodowca?\n\n" +
                "📉 TOP 10 NAJGORSZYCH DNI\n" +
                "Posortowane od najniższej marży. Te dni są ostrzeżeniem — co poszło nie tak?\n" +
                "Może drogie zakupy? Dumping cen sprzedaży? Awarie?\n\n" +
                "🖱 INTERAKCJA:\n" +
                "Kliknij na dowolny wiersz w tabeli → otwiera się dialog 'Szczegóły dnia' z pełnymi cenami w danym dniu (Ministerialna, Łączona, Rolnicza, Wolnorynkowa, Tuszka Zrzeszenia, Nasza Tuszka, Δ vs poprzedni dzień).\n\n" +
                "💡 JAK WYKORZYSTAĆ:\n" +
                "• Sprawdzaj TOP 10 najgorszych co tydzień — może powtarza się jeden hodowca lub dzień tygodnia\n" +
                "• Najlepsze dni to wzór — staraj się replikować warunki\n" +
                "• Skok marży w jeden dzień >0,30 zł vs średnia → coś nadzwyczajnego się stało"));
            statsPanel.Controls.Add(BuildTopBottomDaysCard(contentWidth));

            // SEKCJA 4: ROZKŁAD CEN
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "📏  Rozkład cen (zakres P10—mediana—P90)",
                contentWidth,
                "Box plot rozkładu cen",
                "Box plot pokazuje rozkład cen dla każdej z 6 kolumn cenowych w wybranym okresie.\n\n" +
                "📊 ELEMENTY KAŻDEGO PASKA:\n" +
                "• Cienka linia pozioma (wąsy) — od MIN do MAX zaobserwowanego w okresie\n" +
                "• Pionowe kreski na końcach wąsów — granice min/max\n" +
                "• KOLOROWE PUDEŁKO — pasmo P10–P90 (80% wszystkich cen mieści się w nim)\n" +
                "• GRANATOWA PIONOWA KRESKA — MEDIANA (P50, środek rozkładu)\n" +
                "• Wartość po prawej — wartość mediany w zł\n\n" +
                "🎨 KOLORY PUDEŁKA:\n" +
                "Każda cena ma swój charakterystyczny kolor (Min=niebieski, Łącz=fiolet, Roln=zielony, Wolny=żółty, Zrzesz=pomarańcz, Nasza=teal). Pomaga to wzrokowo identyfikować serię.\n\n" +
                "💡 JAK CZYTAĆ:\n" +
                "• Wąskie pudełko (P10≈P90) → cena była stabilna w okresie\n" +
                "• Szerokie pudełko → cena bardzo skakała, ryzyko biznesowe\n" +
                "• Mediana po lewej (bliżej P10) → przewaga niskich cen\n" +
                "• Mediana po prawej (bliżej P90) → przewaga wysokich cen\n" +
                "• Długi wąs po jednej stronie → pojedyncze ekstrema\n\n" +
                "🔍 PRZYKŁAD:\n" +
                "Jeśli mediana 'Wolny' jest dużo niższa niż 'Roln' → opłaca się kupować wolnorynkowy."));
            statsPanel.Controls.Add(BuildPriceDistributionCard(contentWidth));

            // SEKCJA 5: KORELACJE
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "🔗  Korelacje pomiędzy cenami (Pearson r, |r| ≥ 0,7 wyróżnione)",
                contentWidth,
                "Macierz korelacji",
                "Macierz pokazuje jak silnie poszczególne ceny są ze sobą powiązane (skorelowane) w czasie.\n\n" +
                "📐 WARTOŚCI:\n" +
                "Współczynnik korelacji Pearsona r ∈ [−1, +1]:\n" +
                "• r = +1,00 — idealna korelacja dodatnia (gdy jedna rośnie, druga też)\n" +
                "• r = 0,00 — brak korelacji\n" +
                "• r = −1,00 — idealna korelacja ujemna (gdy jedna rośnie, druga maleje)\n\n" +
                "🎨 KODOWANIE KOLORAMI:\n" +
                "• Zielony — korelacja dodatnia (im ciemniejszy, tym silniejsza)\n" +
                "• Biały — brak korelacji\n" +
                "• Czerwony — korelacja ujemna\n" +
                "• Komórki z |r| ≥ 0,7 mają GRUBY OBRYS — silne korelacje\n\n" +
                "📍 PRZEKĄTNA (diagonal):\n" +
                "Każda cena z samą sobą = 1,00 (zawsze).\n\n" +
                "💡 INTERPRETACJA BIZNESOWA:\n" +
                "• 'Roln' i 'Wolny' silnie skorelowane → rynek lokalny zachowuje się spójnie\n" +
                "• 'Tuszka Zrzeszenia' i 'Nasza Tuszka' niski r → twoje ceny sprzedaży się odklejają\n" +
                "• 'Min' i 'Roln' wysoki r → minister i izby rolnicze dają zbieżne sygnały\n\n" +
                "⚠ OSTROŻNIE:\n" +
                "Korelacja ≠ przyczynowość. Jeśli r=0,9 to nie znaczy że jedna cena 'powoduje' drugą — może być wspólny czynnik (np. sezon)."));
            statsPanel.Controls.Add(BuildCorrelationsCard(contentWidth));

            // SEKCJA 6: KARTY PER-SERIA
            statsPanel.Controls.Add(BuildSectionHeaderWithHelp(
                "🔍  Szczegółowe statystyki per cena (percentyle, trend, porównanie)",
                contentWidth,
                "Karty per-cena",
                "Każda karta = pełny przegląd statystyczny jednej kolumny cenowej.\n\n" +
                "📊 ELEMENTY KARTY:\n" +
                "• PASEK AKCENTU (4 px na górze) — kolor identyfikujący cenę\n" +
                "• TYTUŁ — nazwa ceny\n" +
                "• DUŻA WARTOŚĆ (po lewej) — średnia w okresie\n" +
                "• TREND (po prawej) — % zmiany od początku do końca okresu (regresja liniowa)\n" +
                "• 6 ZNACZNIKÓW STATYSTYCZNYCH (3×2 grid):\n" +
                "  🎯 MEDIANA — środkowa wartość\n" +
                "  📉 MIN — najniższa\n" +
                "  📈 MAX — najwyższa\n" +
                "  📊 P10–P90 — pasmo 80% wartości\n" +
                "  📐 ODCH.STD — odchylenie standardowe\n" +
                "  📅 DNI — liczba dni z danymi\n" +
                "• SPARKLINE — mini wykres trendu z markerami min ▼ / max ▲ / ostatni\n\n" +
                "💡 JAK SZYBKO CZYTAĆ:\n" +
                "• Zerknij na DUŻĄ WARTOŚĆ (średnia) i TREND (% zmiany)\n" +
                "• Trend zielony ▲ = cena rośnie • czerwony ▼ = spada • szary → = stabilna\n" +
                "• Jeśli odchyl.std blisko zera → cena niemal niezmienna w okresie"));

            // Wylicz dane porównawcze (poprzedni okres tej samej długości) jeśli włączone
            Dictionary<string, List<decimal>> prevValues = null;
            if (chkCompareToPrevious?.Checked == true && originalData != null && mainDateFrom != null && mainDateTo != null)
            {
                var span = (mainDateTo.Value.Date - mainDateFrom.Value.Date).Days;
                var prevTo = mainDateFrom.Value.Date.AddDays(-1);
                var prevFrom = prevTo.AddDays(-span);
                bool showWeekends = chkShowWeekends?.Checked ?? false;

                prevValues = new Dictionary<string, List<decimal>>();
                foreach (var col in _priceColumns)
                {
                    if (!originalData.Columns.Contains(col)) continue;
                    prevValues[col] = originalData.AsEnumerable()
                        .Where(r =>
                        {
                            if (r["DataRaw"] == DBNull.Value) return false;
                            var d = Convert.ToDateTime(r["DataRaw"]).Date;
                            if (d < prevFrom || d > prevTo) return false;
                            if (!showWeekends && (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)) return false;
                            return r[col] != DBNull.Value;
                        })
                        .Select(r => Convert.ToDecimal(r[col]))
                        .ToList();
                }
            }

            // Karty per-seria (FlowLayoutPanel wewnętrzny żeby się zawijały)
            var seriesContainer = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Width = contentWidth,
                Margin = new Padding(0, 0, 0, 16)
            };
            // Karta zakresu - małe info
            seriesContainer.Controls.Add(CreateDateRangeCard());

            foreach (var colName in _priceColumns)
            {
                if (!filteredData.Columns.Contains(colName)) continue;

                var values = filteredData.AsEnumerable()
                    .Where(r => r[colName] != DBNull.Value)
                    .Select(r => Convert.ToDecimal(r[colName]))
                    .ToList();

                if (values.Count == 0) continue;

                List<decimal> prev = null;
                prevValues?.TryGetValue(colName, out prev);

                var statsCard = CreateStatsCard(colName, values, prev);
                seriesContainer.Controls.Add(statsCard);
            }

            statsPanel.Controls.Add(seriesContainer);
            statsPanel.ResumeLayout();
        }

        // ───────────── Sekcja: nagłówek ─────────────

        private Panel BuildSectionHeader(string text, int width)
        {
            // Panel z akcentem koloru po lewej
            var host = new Panel
            {
                Width = width,
                Height = 38,
                Margin = new Padding(0, 16, 0, 6),
                BackColor = backgroundColor
            };
            host.Paint += (s, e) =>
            {
                try
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    // Kolorowy pionowy pasek 4px po lewej
                    using var accentBrush = new SolidBrush(primaryColor);
                    e.Graphics.FillRectangle(accentBrush, 0, 8, 4, 22);
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Emoji", 12.5F),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = width - 14,
                Height = 36,
                Padding = new Padding(0, 8, 0, 0),
                Location = new Point(14, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            host.Controls.Add(lbl);
            return host;
        }

        /// <summary>Tworzy nagłówek sekcji z przyciskiem pomocy ❓ obok i kolorowym paskiem akcentu po lewej.</summary>
        private Panel BuildSectionHeaderWithHelp(string text, int width, string helpTitle, string helpContent)
        {
            var host = new Panel
            {
                Width = width,
                Height = 38,
                Margin = new Padding(0, 16, 0, 6),
                BackColor = backgroundColor
            };
            host.Paint += (s, e) =>
            {
                try
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var accentBrush = new SolidBrush(primaryColor);
                    e.Graphics.FillRectangle(accentBrush, 0, 8, 4, 22);
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Emoji", 12.5F),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = width - 60,
                Height = 36,
                Padding = new Padding(0, 8, 0, 0),
                Location = new Point(14, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var helpBtn = new Button
            {
                Text = "?",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Size = new Size(32, 32),
                Location = new Point(width - 36, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = primaryColor,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            helpBtn.FlatAppearance.BorderColor = primaryColor;
            helpBtn.FlatAppearance.BorderSize = 1;
            helpBtn.FlatAppearance.MouseOverBackColor = primaryColor;
            helpBtn.MouseEnter += (s, e) => helpBtn.ForeColor = Color.White;
            helpBtn.MouseLeave += (s, e) => helpBtn.ForeColor = primaryColor;
            helpBtn.Click += (s, e) => ShowHelpDialog(helpTitle, helpContent);

            var helpTip = new ToolTip();
            helpTip.SetToolTip(helpBtn, "Kliknij aby zobaczyć poradnik tej sekcji");

            host.Controls.Add(lbl);
            host.Controls.Add(helpBtn);
            return host;
        }

        /// <summary>Otwiera dialog pomocy z tytułem i sformatowaną treścią poradnika.</summary>
        private void ShowHelpDialog(string title, string content)
        {
            var form = new Form
            {
                Text = $"❓ Poradnik: {title}",
                Size = new Size(620, 520),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = backgroundColor,
                FormBorderStyle = FormBorderStyle.Sizable,
                MaximizeBox = false,
                MinimizeBox = false,
                MinimumSize = new Size(480, 360),
                ShowInTaskbar = false
            };
            WindowIconHelper.SetIcon(form);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20),
                BackColor = backgroundColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            var lblTitle = new Label
            {
                Text = $"❓  {title}",
                Font = new Font("Segoe UI Emoji", 14F),
                ForeColor = primaryColor,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(lblTitle, 0, 0);

            var contentBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Segoe UI Emoji", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Text = content,
                Margin = new Padding(0, 4, 0, 4)
            };
            root.Controls.Add(contentBox, 0, 1);

            var btnClose = new Button
            {
                Text = "Zamknij (Esc)",
                Size = new Size(140, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = primaryColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10F),
                DialogResult = DialogResult.OK,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            btnClose.FlatAppearance.BorderSize = 0;

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = backgroundColor
            };
            footer.Controls.Add(btnClose);
            root.Controls.Add(footer, 0, 2);

            form.AcceptButton = btnClose;
            form.KeyPreview = true;
            form.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) form.Close(); };

            form.Controls.Add(root);
            form.ShowDialog();
        }

        // ───────────── Sekcja 1: Hero strip marży ─────────────

        /// <summary>
        /// Wylicza dzienną marżę (Nasza Tuszka − Średnia Wszystkich Dostaw) — pomija dni z brakami.
        /// </summary>
        private List<(DateTime Date, decimal Margin)> ComputeDailyMargins()
        {
            if (filteredData == null) return new();
            if (!filteredData.Columns.Contains("Nasza Tuszka") || !filteredData.Columns.Contains("Śr. Wszystkich Dostaw"))
                return new();

            return filteredData.AsEnumerable()
                .Where(r => r["DataRaw"] != DBNull.Value
                            && r["Nasza Tuszka"] != DBNull.Value
                            && r["Śr. Wszystkich Dostaw"] != DBNull.Value)
                .Select(r => (
                    Date: Convert.ToDateTime(r["DataRaw"]).Date,
                    Margin: Convert.ToDecimal(r["Nasza Tuszka"]) - Convert.ToDecimal(r["Śr. Wszystkich Dostaw"])
                ))
                .OrderBy(t => t.Date)
                .ToList();
        }

        private Panel BuildMarginHeroStrip(int width)
        {
            var host = new TableLayoutPanel
            {
                Width = width,
                Height = 165,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = backgroundColor
            };
            for (int i = 0; i < 4; i++)
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            var margins = ComputeDailyMargins();
            decimal? current = margins.LastOrDefault().Date != default ? margins.Last().Margin : (decimal?)null;
            decimal? avg = margins.Count > 0 ? margins.Average(t => t.Margin) : (decimal?)null;
            (DateTime, decimal)? best = margins.Count > 0 ? margins.OrderByDescending(t => t.Margin).First() : null;
            (DateTime, decimal)? worst = margins.Count > 0 ? margins.OrderBy(t => t.Margin).First() : null;

            // Sprawdź czy ostatnia wartość Wolnorynkowej (która wpływa na "Średnią Wszystkich") jest imputowana
            string currentSubtitleExtra = "";
            if (current.HasValue && filteredData != null && filteredData.Columns.Contains("WolnorynkowaImputed"))
            {
                var latestDate = margins.Last().Date;
                var latestRow = filteredData.AsEnumerable()
                    .FirstOrDefault(r => r["DataRaw"] != DBNull.Value
                                         && Convert.ToDateTime(r["DataRaw"]).Date == latestDate);
                if (latestRow != null && latestRow["WolnorynkowaImputed"] != DBNull.Value
                    && Convert.ToBoolean(latestRow["WolnorynkowaImputed"]))
                {
                    currentSubtitleExtra = "  •  ⚠ z imputacją Wolnorynkowej";
                }
            }

            host.Controls.Add(BuildHeroCard(
                "📈 Marża aktualna",
                current.HasValue ? $"{current.Value:N2} zł/kg" : "—",
                current.HasValue
                    ? $"vs cel {MARGIN_TARGET:N2}: {(current.Value - MARGIN_TARGET):+0.00;-0.00;0.00} zł/kg{currentSubtitleExtra}"
                    : "brak danych",
                current.HasValue ? GetMarginColor(current.Value) : Color.Gray,
                current.HasValue ? (double)current.Value : 0
            ), 0, 0);

            host.Controls.Add(BuildHeroCard(
                "📊 Marża średnia w okresie",
                avg.HasValue ? $"{avg.Value:N2} zł/kg" : "—",
                avg.HasValue ? $"n = {margins.Count} dni  •  vs cel {(avg.Value - MARGIN_TARGET):+0.00;-0.00;0.00}" : "brak danych",
                avg.HasValue ? GetMarginColor(avg.Value) : Color.Gray,
                avg.HasValue ? (double)avg.Value : 0
            ), 1, 0);

            // Karta 3: Wolumen zakupów
            decimal volumeKg = 0;
            int daysWithVolume = 0;
            if (filteredData != null && filteredData.Columns.Contains("Wolumen kg"))
            {
                foreach (DataRow r in filteredData.Rows)
                {
                    if (r["Wolumen kg"] != DBNull.Value)
                    {
                        volumeKg += Convert.ToDecimal(r["Wolumen kg"]);
                        daysWithVolume++;
                    }
                }
            }
            host.Controls.Add(BuildHeroCard(
                "💰 Wolumen zakupów",
                volumeKg > 0 ? $"{volumeKg / 1000m:N1} t" : "—",
                volumeKg > 0 ? $"{volumeKg:#,0} kg • {daysWithVolume} dni potwierdzonych" : "brak potwierdzonych dostaw",
                Color.FromArgb(13, 148, 136), // teal
                0 // bez progress baru (inna jednostka)
            ), 2, 0);

            // Karta 4: kombinowana - najlepszy + najgorszy dzień
            string bestText = best.HasValue ? $"{best.Value.Item2:+0.00;-0.00;0.00}" : "—";
            string worstText = worst.HasValue ? $"{worst.Value.Item2:+0.00;-0.00;0.00}" : "—";
            string bestDate = best.HasValue ? best.Value.Item1.ToString("dd.MM") : "—";
            string worstDate = worst.HasValue ? worst.Value.Item1.ToString("dd.MM") : "—";

            host.Controls.Add(BuildHeroCard(
                "🏆 Skrajne dni",
                $"{bestText} / {worstText}",
                $"🥇 {bestDate}    📉 {worstDate}",
                Color.FromArgb(124, 58, 237), // fioletowy
                0
            ), 3, 0);

            return host;
        }

        private Color GetMarginColor(decimal m)
        {
            if (m >= MARGIN_TARGET) return successColor;
            if (m >= 1.5m) return Color.FromArgb(234, 88, 12); // pomarańczowy
            return dangerColor;
        }

        private Panel BuildHeroCard(string title, string bigValue, string subtitle, Color accent, double targetIndicator)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 4, 0),
                BackColor = Color.White
            };

            card.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.White);
                e.Graphics.FillPath(bg, path);
                using var pen = new Pen(subtleBorderColor, 1);
                e.Graphics.DrawPath(pen, path);
                // Pasek akcentowy z lewej (4px)
                using var accentBrush = new SolidBrush(accent);
                e.Graphics.SetClip(path);
                e.Graphics.FillRectangle(accentBrush, 0, 0, 4, card.Height);
                e.Graphics.ResetClip();

                // Pasek progress względem celu marży 2.50 (tylko dla kart z marżą)
                if (targetIndicator > 0)
                {
                    int barY = card.Height - 28;
                    int barH = 8;
                    int barX = 12;
                    int barW = card.Width - 24;
                    using var bgBar = new SolidBrush(Color.FromArgb(241, 245, 249));
                    e.Graphics.FillRectangle(bgBar, barX, barY, barW, barH);

                    double maxScale = 3.5; // skala 0..3.5 zł/kg
                    double pct = Math.Min(1.0, targetIndicator / maxScale);
                    using var fillBar = new SolidBrush(accent);
                    e.Graphics.FillRectangle(fillBar, barX, barY, (int)(barW * pct), barH);

                    // Marker celu (2.50 zł/kg)
                    int targetX = barX + (int)(barW * (double)MARGIN_TARGET / maxScale);
                    using var targetPen = new Pen(Color.FromArgb(15, 23, 42), 2);
                    e.Graphics.DrawLine(targetPen, targetX, barY - 3, targetX, barY + barH + 3);

                    // Etykiety pod paskiem
                    using var smallFont = new Font("Segoe UI", 7.5F);
                    using var smallBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                    e.Graphics.DrawString("0", smallFont, smallBrush, barX, barY + barH + 4);
                    e.Graphics.DrawString($"cel {MARGIN_TARGET:N2}", smallFont, smallBrush, targetX - 22, barY + barH + 4);
                    e.Graphics.DrawString($"{maxScale:N1}", smallFont, smallBrush, barX + barW - 18, barY + barH + 4);
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                // Segoe UI Emoji - emoji renderuje się ZAMIAST kwadratu
                Font = new Font("Segoe UI Emoji", 10F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 10)
            };
            var lblValue = new Label
            {
                Text = bigValue,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(12, 32)
            };
            var lblSub = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI Emoji", 9.5F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 76)
            };
            card.Controls.AddRange(new Control[] { lblTitle, lblValue, lblSub });
            return card;
        }

        // ───────────── Sekcja: Inteligentne wnioski ─────────────

        /// <summary>Generuje karty z auto-wnioskami z aktualnych danych.</summary>
        private Panel BuildSmartInsightsCard(int width)
        {
            var card = new BufferedPanel
            {
                Width = width,
                Height = 230,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White,
                Padding = new Padding(20, 16, 20, 16)
            };

            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var bg = new SolidBrush(Color.White);
                    e.Graphics.FillPath(bg, path);
                    using var border = new Pen(subtleBorderColor, 1);
                    e.Graphics.DrawPath(border, path);

                    // Pasek akcentu z lewej strony
                    e.Graphics.SetClip(path);
                    using var accentBrush = new SolidBrush(Color.FromArgb(234, 179, 8));
                    e.Graphics.FillRectangle(accentBrush, 0, 0, 5, card.Height);
                    e.Graphics.ResetClip();
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            var insights = ComputeSmartInsights();

            // Layout: 2-3 kolumny insightów
            var insightsHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = (insights.Count + 1) / 2,
                BackColor = Color.Transparent,
                Padding = new Padding(8, 0, 0, 0)
            };
            insightsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            insightsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int r = 0; r < insightsHost.RowCount; r++)
                insightsHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / insightsHost.RowCount));

            for (int i = 0; i < insights.Count; i++)
            {
                var (icon, text, color) = insights[i];
                var insightLabel = new Label
                {
                    Text = $"{icon}  {text}",
                    Font = new Font("Segoe UI Emoji", 10F),
                    ForeColor = color,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 4, 8, 4),
                    BackColor = Color.Transparent
                };
                insightsHost.Controls.Add(insightLabel, i % 2, i / 2);
            }

            card.Controls.Add(insightsHost);
            return card;
        }

        private List<(string Icon, string Text, Color Color)> ComputeSmartInsights()
        {
            var result = new List<(string Icon, string Text, Color Color)>();
            var margins = ComputeDailyMargins();
            if (margins.Count == 0)
            {
                result.Add(("⏳", "Brak danych w wybranym okresie - rozszerz zakres.", Color.Gray));
                return result;
            }

            // 1. Trend marży - ile ostatnich dni rośnie/spada
            int streakUp = 0, streakDown = 0;
            for (int i = margins.Count - 1; i > 0; i--)
            {
                if (margins[i].Margin > margins[i - 1].Margin) streakUp++;
                else break;
            }
            for (int i = margins.Count - 1; i > 0; i--)
            {
                if (margins[i].Margin < margins[i - 1].Margin) streakDown++;
                else break;
            }
            if (streakUp >= 2)
                result.Add(("▲", $"Marża rośnie {streakUp} dni z rzędu — pozytywny trend.", successColor));
            else if (streakDown >= 2)
                result.Add(("▼", $"Marża SPADA {streakDown} dni z rzędu — przejrzyj ceny.", dangerColor));

            // 2. Alarm marża < cel
            decimal currentMargin = margins.Last().Margin;
            if (currentMargin < 1.5m)
                result.Add(("🚨", $"Aktualna marża {currentMargin:N2} zł/kg < 1,50 — eskalacja!", dangerColor));
            else if (currentMargin < MARGIN_TARGET)
                result.Add(("⚠", $"Marża {currentMargin:N2} zł poniżej celu {MARGIN_TARGET:N2} zł.", Color.FromArgb(234, 88, 12)));
            else
                result.Add(("✅", $"Marża {currentMargin:N2} zł osiąga cel {MARGIN_TARGET:N2}.", successColor));

            // 3. Najlepszy dzień marży
            var best = margins.OrderByDescending(t => t.Margin).First();
            result.Add(("🏆", $"Najwyższa marża: {best.Margin:N2} zł ({best.Date:dd.MM.yyyy ddd}).", successColor));

            // 4. Wolumen - czy rośnie?
            if (filteredData != null && filteredData.Columns.Contains("Wolumen kg"))
            {
                var volByDate = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r["Wolumen kg"] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r["Wolumen kg"])))
                    .OrderBy(t => t.Date)
                    .ToList();
                if (volByDate.Count >= 6)
                {
                    var firstHalf = volByDate.Take(volByDate.Count / 2).Average(t => t.V);
                    var secondHalf = volByDate.Skip(volByDate.Count / 2).Average(t => t.V);
                    var diff = secondHalf - firstHalf;
                    var pct = firstHalf == 0 ? 0 : (diff / firstHalf) * 100;
                    if (pct > 10)
                        result.Add(("📈", $"Wolumen zakupów ROŚNIE +{pct:N0}% (pierwsza vs druga połowa okresu).", successColor));
                    else if (pct < -10)
                        result.Add(("📉", $"Wolumen zakupów MALEJE {pct:N0}% — sprawdź czy nie tracimy dostawców.", dangerColor));
                    else
                        result.Add(("➡", $"Wolumen stabilny ({pct:+0.0;-0.0;0.0}% zmiana).", Color.FromArgb(100, 116, 139)));
                }
            }

            // 5. Najtańsza Wolnorynkowa
            if (filteredData != null && filteredData.Columns.Contains("Wolnorynkowa"))
            {
                var wolny = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r["Wolnorynkowa"] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r["Wolnorynkowa"])))
                    .OrderBy(t => t.V)
                    .FirstOrDefault();
                if (wolny.Date != default)
                {
                    result.Add(("💰", $"Najtańsza Wolnorynkowa: {wolny.V:N2} zł ({wolny.Date:dd.MM.yyyy ddd}).", primaryColor));
                }
            }

            // 6. Najsilniejsza korelacja
            var strongestCorr = ComputeStrongestCorrelation();
            if (strongestCorr.HasValue)
            {
                var (a, b, r) = strongestCorr.Value;
                if (Math.Abs(r) >= 0.7)
                {
                    var arrow = r > 0 ? "↗" : "↘";
                    var clr = r > 0 ? successColor : dangerColor;
                    result.Add(("🔗", $"Silna korelacja {arrow} {a} ↔ {b}  (r = {r:+0.00;-0.00;0.00}).", clr));
                }
            }

            // 7. Anomalie - dni z marżą poza 2σ od średniej
            if (margins.Count >= 5)
            {
                decimal avgM = margins.Average(t => t.Margin);
                double variance = margins.Sum(t => Math.Pow((double)(t.Margin - avgM), 2)) / (margins.Count - 1);
                double sd = Math.Sqrt(variance);
                if (sd > 0)
                {
                    var anomalies = margins.Where(t => Math.Abs((double)(t.Margin - avgM)) > 2.0 * sd).ToList();
                    if (anomalies.Count > 0)
                    {
                        var worstAn = anomalies.OrderBy(t => t.Margin).First();
                        if (worstAn.Margin < avgM)
                            result.Add(("⚡", $"Anomalia: {worstAn.Date:dd.MM} marża {worstAn.Margin:N2} zł (>2σ od średniej).", dangerColor));
                    }
                }
            }

            return result;
        }

        /// <summary>Znajduje najsilniejszą korelację (max |r|) wśród par cenowych.</summary>
        private (string A, string B, double R)? ComputeStrongestCorrelation()
        {
            if (filteredData == null) return null;
            var colData = new Dictionary<string, decimal[]>();
            foreach (var c in _priceColumns)
            {
                if (!filteredData.Columns.Contains(c)) continue;
                var rows = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value)
                    .OrderBy(r => Convert.ToDateTime(r["DataRaw"]))
                    .Select(r => r[c] != DBNull.Value ? (decimal?)Convert.ToDecimal(r[c]) : (decimal?)null)
                    .ToArray();
                colData[c] = rows.Select(v => v ?? decimal.MinValue).ToArray();
            }

            (string A, string B, double R)? best = null;
            for (int i = 0; i < _priceColumns.Length; i++)
            {
                for (int j = i + 1; j < _priceColumns.Length; j++)
                {
                    if (!colData.ContainsKey(_priceColumns[i]) || !colData.ContainsKey(_priceColumns[j])) continue;
                    var r = ComputePearson(colData[_priceColumns[i]], colData[_priceColumns[j]]);
                    if (r.HasValue && (best == null || Math.Abs(r.Value) > Math.Abs(best.Value.R)))
                    {
                        best = (_priceColumns[i], _priceColumns[j], r.Value);
                    }
                }
            }
            return best;
        }

        // ───────────── Sekcja: Marża per dzień tygodnia ─────────────

        private Panel BuildDayOfWeekChart(int width)
        {
            var card = new BufferedPanel
            {
                Width = width,
                Height = 200,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };

            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    using var bg = new SolidBrush(Color.White);
                    e.Graphics.FillPath(bg, path);
                    using var border = new Pen(subtleBorderColor, 1);
                    e.Graphics.DrawPath(border, path);

                    var margins = ComputeDailyMargins();
                    if (margins.Count == 0)
                    {
                        using var emptyFont = new Font("Segoe UI Emoji", 11F, FontStyle.Italic);
                        using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                        e.Graphics.DrawString("📅  Brak danych marży", emptyFont, grayBrush, card.ClientRectangle,
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        return;
                    }

                    // Grupuj po dniu tygodnia
                    var byDow = margins
                        .GroupBy(t => ((int)t.Date.DayOfWeek + 6) % 7) // 0=Pn, 6=Nd
                        .ToDictionary(g => g.Key, g => (Avg: g.Average(x => x.Margin), Count: g.Count()));

                    string[] dniSkrot = { "Pn", "Wt", "Śr", "Cz", "Pt", "Sb", "Nd" };

                    int padding = 24;
                    int chartLeft = padding + 40;
                    int chartRight = card.Width - padding - 8;
                    int chartTop = padding + 8;
                    int chartBottom = card.Height - padding - 16;
                    int chartWidth = chartRight - chartLeft;
                    int chartHeight = chartBottom - chartTop;

                    decimal minV = byDow.Values.Any() ? Math.Min(byDow.Values.Min(v => v.Avg), 0) : 0;
                    decimal maxV = byDow.Values.Any() ? Math.Max(byDow.Values.Max(v => v.Avg), MARGIN_TARGET) : MARGIN_TARGET;
                    decimal rangeV = maxV - minV;
                    if (rangeV == 0) rangeV = 1;

                    int YForValue(decimal v) => chartTop + (int)((double)((maxV - v) / rangeV) * chartHeight);

                    // Linie odniesienia
                    using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);
                    int yZero = YForValue(0);
                    int yTarget = YForValue(MARGIN_TARGET);
                    e.Graphics.DrawLine(gridPen, chartLeft, yZero, chartRight, yZero);
                    using var targetPen = new Pen(Color.FromArgb(180, 22, 163, 74), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                    e.Graphics.DrawLine(targetPen, chartLeft, yTarget, chartRight, yTarget);

                    using var axisFont = new Font("Segoe UI Semibold", 9F);
                    using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));

                    // Etykieta celu po prawej
                    using var targetBrush = new SolidBrush(successColor);
                    e.Graphics.DrawString($"cel {MARGIN_TARGET:N2}", axisFont, targetBrush, chartRight - 60, yTarget - 14);

                    // 5 podziałek na osi Y
                    for (int i = 0; i <= 4; i++)
                    {
                        decimal v = minV + rangeV * i / 4;
                        int y = YForValue(v);
                        e.Graphics.DrawString($"{v:N2}", axisFont, axisBrush, padding - 4, y - 8);
                        e.Graphics.DrawLine(gridPen, chartLeft, y, chartRight, y);
                    }

                    // 7 słupków per dzień tygodnia
                    float barW = (float)chartWidth / 7;
                    float barGap = barW * 0.15f;
                    float actualBarW = barW - barGap;

                    using var labelFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                    using var dowFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                    var labelFormat = new System.Globalization.CultureInfo("pl-PL").NumberFormat;

                    for (int dow = 0; dow < 7; dow++)
                    {
                        bool hasData = byDow.TryGetValue(dow, out var info);
                        decimal avg = hasData ? info.Avg : 0;
                        int count = hasData ? info.Count : 0;

                        int x = chartLeft + (int)(dow * barW + barGap / 2);
                        int yVal = YForValue(avg);
                        int barHeight = Math.Abs(yVal - yZero);
                        int barY = avg >= 0 ? yVal : yZero;

                        Color barColor;
                        if (!hasData) barColor = Color.FromArgb(241, 245, 249);
                        else if (avg >= MARGIN_TARGET) barColor = successColor;
                        else if (avg >= 1.5m) barColor = Color.FromArgb(234, 179, 8);
                        else if (avg >= 0) barColor = Color.FromArgb(234, 88, 12);
                        else barColor = dangerColor;

                        if (hasData)
                        {
                            using var barBrush = new SolidBrush(barColor);
                            e.Graphics.FillRectangle(barBrush, x, barY, actualBarW, Math.Max(1, barHeight));

                            // Wartość wewnątrz/nad słupkiem
                            string vText = avg.ToString("N2", labelFormat);
                            var vSize = e.Graphics.MeasureString(vText, labelFont);
                            float vX = x + actualBarW / 2 - vSize.Width / 2;
                            if (barHeight >= vSize.Height + 4)
                            {
                                float vY = avg >= 0 ? barY + 3 : barY + barHeight - vSize.Height - 3;
                                var brushToUse = (barColor == Color.FromArgb(234, 179, 8))
                                    ? Brushes.Black : Brushes.White;
                                e.Graphics.DrawString(vText, labelFont, brushToUse, vX, vY);
                            }
                            else
                            {
                                float vY = avg >= 0 ? barY - vSize.Height - 1 : barY + barHeight + 1;
                                using var darkBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                                e.Graphics.DrawString(vText, labelFont, darkBrush, vX, vY);
                            }
                        }

                        // Etykieta dnia tygodnia + count
                        string dowLabel = dniSkrot[dow];
                        var dowSize = e.Graphics.MeasureString(dowLabel, dowFont);
                        float dowX = x + actualBarW / 2 - dowSize.Width / 2;
                        e.Graphics.DrawString(dowLabel, dowFont, axisBrush, dowX, chartBottom + 1);

                        if (hasData)
                        {
                            string countLabel = $"({count})";
                            using var countFont = new Font("Segoe UI", 7.5F);
                            var countSize = e.Graphics.MeasureString(countLabel, countFont);
                            float countX = x + actualBarW / 2 - countSize.Width / 2;
                            using var countBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                            e.Graphics.DrawString(countLabel, countFont, countBrush, countX, chartBottom + 14);
                        }
                    }

                    // Tytuł
                    using var titleFont = new Font("Segoe UI Emoji", 10F);
                    using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                    e.Graphics.DrawString("📅  Średnia marża per dzień tygodnia (liczby pod słupkami = liczba dni w analizie)",
                        titleFont, titleBrush, chartLeft, padding - 8);
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            return card;
        }

        // ───────────── Sekcja: Bar chart marży dziennej ─────────────

        private Panel BuildDailyMarginBarChart(int width)
        {
            var card = new BufferedPanel
            {
                Width = width,
                Height = 260,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };

            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    using var bg = new SolidBrush(Color.White);
                    e.Graphics.FillPath(bg, path);
                    using var border = new Pen(subtleBorderColor, 1);
                    e.Graphics.DrawPath(border, path);

                    var margins = ComputeDailyMargins();
                    if (margins.Count == 0)
                    {
                        using var emptyFont = new Font("Segoe UI Emoji", 11F, FontStyle.Italic);
                        using var grayBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                        e.Graphics.DrawString("📊  Brak danych marży w wybranym okresie",
                            emptyFont, grayBrush, card.ClientRectangle,
                            new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            });
                        return;
                    }

                    int padding = 24;
                    int chartLeft = padding + 50;  // miejsce na oś Y labels
                    int chartRight = card.Width - padding - 8;
                    int chartTop = padding + 8;
                    int chartBottom = card.Height - padding - 18; // miejsce na oś X labels
                    int chartWidth = chartRight - chartLeft;
                    int chartHeight = chartBottom - chartTop;

                    decimal minM = Math.Min(margins.Min(t => t.Margin), 0);
                    decimal maxM = Math.Max(margins.Max(t => t.Margin), MARGIN_TARGET);
                    decimal rangeM = maxM - minM;
                    if (rangeM == 0) rangeM = 1;

                    // Helper: y position dla wartości
                    int YForValue(decimal v) => chartTop + (int)((double)((maxM - v) / rangeM) * chartHeight);

                    // Linie odniesienia
                    using var gridPen = new Pen(Color.FromArgb(241, 245, 249), 1);
                    int yZero = YForValue(0);
                    int yTarget = YForValue(MARGIN_TARGET);
                    e.Graphics.DrawLine(gridPen, chartLeft, yZero, chartRight, yZero);
                    using var targetPen = new Pen(Color.FromArgb(150, 22, 163, 74), 1) { DashStyle = ChartDashStyle.Dash == ChartDashStyle.Dash ? System.Drawing.Drawing2D.DashStyle.Dash : System.Drawing.Drawing2D.DashStyle.Solid };
                    e.Graphics.DrawLine(targetPen, chartLeft, yTarget, chartRight, yTarget);

                    // Etykieta celu po prawej
                    using var labelFont = new Font("Segoe UI", 8F, FontStyle.Bold);
                    using var targetBrush = new SolidBrush(successColor);
                    e.Graphics.DrawString($"cel {MARGIN_TARGET:N2} →", labelFont, targetBrush, chartRight - 60, yTarget - 14);

                    // Osie Y - 4 podziałki (większe, czytelne)
                    using var axisFont = new Font("Segoe UI Semibold", 9F);
                    using var axisBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                    for (int i = 0; i <= 4; i++)
                    {
                        decimal v = minM + rangeM * i / 4;
                        int y = YForValue(v);
                        e.Graphics.DrawString($"{v:N2}", axisFont, axisBrush, padding, y - 8);
                        e.Graphics.DrawLine(gridPen, chartLeft, y, chartRight, y);
                    }

                    // Słupki marży
                    int barCount = margins.Count;
                    float barW = (float)chartWidth / barCount;
                    float barGap = Math.Max(1f, barW * 0.15f);
                    float actualBarW = Math.Max(2f, barW - barGap);

                    using var barLabelFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    using var barLabelBrushWhite = new SolidBrush(Color.White);
                    using var barLabelBrushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
                    var labelFormat = new System.Globalization.CultureInfo("pl-PL").NumberFormat;

                    for (int i = 0; i < barCount; i++)
                    {
                        var (date, margin) = margins[i];
                        int x = chartLeft + (int)(i * barW + barGap / 2);
                        int yVal = YForValue(margin);
                        int barHeight = Math.Abs(yVal - yZero);
                        int barY = margin >= 0 ? yVal : yZero;

                        Color barColor;
                        if (margin >= MARGIN_TARGET) barColor = successColor;
                        else if (margin >= 1.5m) barColor = Color.FromArgb(234, 179, 8); // żółty
                        else if (margin >= 0) barColor = Color.FromArgb(234, 88, 12); // pomarańczowy
                        else barColor = dangerColor;

                        using var barBrush = new SolidBrush(barColor);
                        e.Graphics.FillRectangle(barBrush, x, barY, actualBarW, Math.Max(1, barHeight));

                        // Etykieta z wartością wewnątrz słupka (bez "zł")
                        string labelText = margin.ToString("N2", labelFormat);
                        var labelSize = e.Graphics.MeasureString(labelText, barLabelFont);
                        if (actualBarW >= labelSize.Width + 2)
                        {
                            float labelX = x + actualBarW / 2 - labelSize.Width / 2;
                            // Białe tło słupka → biała czcionka. Dla bardzo małych słupków → czarna pod słupkiem.
                            if (barHeight >= labelSize.Height + 4)
                            {
                                // Wystarczy miejsca w środku słupka
                                float labelY = margin >= 0
                                    ? barY + 3
                                    : barY + barHeight - labelSize.Height - 3;
                                // Dla pomarańczowo-żółtych (jaśniejszych) tekst ciemny dla kontrastu
                                var brushToUse = (barColor == Color.FromArgb(234, 179, 8))
                                    ? barLabelBrushDark : barLabelBrushWhite;
                                e.Graphics.DrawString(labelText, barLabelFont, brushToUse, labelX, labelY);
                            }
                            else
                            {
                                // Słupek za mały - etykieta NAD lub POD słupkiem
                                float labelY = margin >= 0
                                    ? barY - labelSize.Height - 1
                                    : barY + barHeight + 1;
                                e.Graphics.DrawString(labelText, barLabelFont, barLabelBrushDark, labelX, labelY);
                            }
                        }
                    }

                    // Etykiety daty co N słupków (żeby się nie nakładały)
                    int labelStep = Math.Max(1, barCount / 12);
                    for (int i = 0; i < barCount; i += labelStep)
                    {
                        var (date, _) = margins[i];
                        int x = chartLeft + (int)(i * barW + barW / 2);
                        e.Graphics.DrawString(date.ToString("dd.MM"), axisFont, axisBrush, x - 14, chartBottom + 2);
                    }

                    // Tytuł
                    using var titleFont = new Font("Segoe UI Emoji", 10F);
                    using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                    e.Graphics.DrawString($"💹  {barCount} dni  •  zielone = ≥ cel  •  żółte = ≥ 1,50  •  czerwone = ujemne",
                        titleFont, titleBrush, chartLeft, padding - 8);
                }
                catch { /* Paint nie może rzucać */ }
            };

            return card;
        }

        // ───────────── Sekcja: Box plot rozkładu cen ─────────────

        private Panel BuildPriceDistributionCard(int width)
        {
            var card = new BufferedPanel
            {
                Width = width,
                Height = 240,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };

            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    using var bg = new SolidBrush(Color.White);
                    e.Graphics.FillPath(bg, path);
                    using var border = new Pen(subtleBorderColor, 1);
                    e.Graphics.DrawPath(border, path);

                    if (filteredData == null) return;

                    // Zbierz statystyki per kolumna + outliery
                    var stats = new List<(string Label, decimal Min, decimal P10, decimal P50, decimal P90, decimal Max, Color Accent, List<decimal> Outliers)>();
                    foreach (var col in _priceColumns)
                    {
                        if (!filteredData.Columns.Contains(col)) continue;
                        var values = filteredData.AsEnumerable()
                            .Where(r => r[col] != DBNull.Value)
                            .Select(r => Convert.ToDecimal(r[col]))
                            .ToList();
                        if (values.Count < 3) continue;

                        var theme = _kpiTheme[col];
                        var p10v = CalculatePercentile(values, 0.10);
                        var p90v = CalculatePercentile(values, 0.90);
                        var iqr = p90v - p10v;
                        var outlierLow = p10v - iqr * 1.5m;
                        var outlierHigh = p90v + iqr * 1.5m;
                        var outliers = values.Where(v => v < outlierLow || v > outlierHigh).Distinct().ToList();

                        stats.Add((
                            col,
                            values.Min(),
                            p10v,
                            CalculateMedian(values),
                            p90v,
                            values.Max(),
                            theme.Bg,
                            outliers
                        ));
                    }
                    if (stats.Count == 0) return;

                    // Globalne min/max dla skali X
                    decimal globMin = stats.Min(s => s.Min) - 0.1m;
                    decimal globMax = stats.Max(s => s.Max) + 0.1m;
                    decimal globRange = globMax - globMin;
                    if (globRange == 0) globRange = 1;

                    int leftLabel = 110;
                    int rightLabel = 80;
                    int topPad = 24;
                    int barLeft = leftLabel;
                    int barRight = card.Width - rightLabel - 16;
                    int barWidth = barRight - barLeft;
                    int rowHeight = (card.Height - topPad - 16) / Math.Max(1, stats.Count);
                    int barH = Math.Max(8, rowHeight - 10);

                    int XForValue(decimal v) => barLeft + (int)((double)((v - globMin) / globRange) * barWidth);

                    using var labelFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                    using var valueFont = new Font("Consolas", 9.5F, FontStyle.Bold);
                    using var grayBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                    using var navyBrush = new SolidBrush(Color.FromArgb(15, 23, 42));

                    // Tytuł
                    using var titleFont = new Font("Segoe UI Emoji", 10F);
                    e.Graphics.DrawString("📏  Min ─ P10 ▓▓ P50 ▓▓ P90 ─ Max  •  🔴 czerwone kropki = wartości ekstremalne (>1,5×IQR)",
                        titleFont, navyBrush, leftLabel, 6);

                    int displayNamesIndex = 0;
                    var shortNames = new Dictionary<string, string>
                    {
                        ["Minister"] = "Ministerialna",
                        ["Laczona"] = "Łączona",
                        ["Rolnicza"] = "Rolnicza",
                        ["Wolnorynkowa"] = "Wolnorynkowa",
                        ["Tuszka Zrzeszenia"] = "Tuszka Zrzesz.",
                        ["Nasza Tuszka"] = "Nasza Tuszka"
                    };

                    foreach (var (label, min, p10, p50, p90, max, accent, outliers) in stats)
                    {
                        int rowY = topPad + displayNamesIndex * rowHeight;
                        int centerY = rowY + rowHeight / 2;

                        // Label po lewej
                        var dispName = shortNames.TryGetValue(label, out var sn) ? sn : label;
                        e.Graphics.DrawString(dispName, labelFont, navyBrush, 16, centerY - 7);

                        // Linia "wąsa" min-max
                        int xMin = XForValue(min);
                        int xP10 = XForValue(p10);
                        int xP50 = XForValue(p50);
                        int xP90 = XForValue(p90);
                        int xMax = XForValue(max);

                        using var whiskerPen = new Pen(Color.FromArgb(180, 188, 200), 1.5f);
                        e.Graphics.DrawLine(whiskerPen, xMin, centerY, xMax, centerY);
                        // Końce wąsa (pionowe kreski)
                        e.Graphics.DrawLine(whiskerPen, xMin, centerY - 4, xMin, centerY + 4);
                        e.Graphics.DrawLine(whiskerPen, xMax, centerY - 4, xMax, centerY + 4);

                        // Pudełko P10-P90 w kolorze akcentu
                        var boxRect = new Rectangle(xP10, centerY - barH / 2, xP90 - xP10, barH);
                        using var boxBrush = new SolidBrush(Color.FromArgb(150, accent.R, accent.G, accent.B));
                        e.Graphics.FillRectangle(boxBrush, boxRect);
                        using var boxPen = new Pen(accent, 1);
                        e.Graphics.DrawRectangle(boxPen, boxRect);

                        // Mediana - granatowa kreska pionowa
                        using var medianPen = new Pen(Color.FromArgb(15, 23, 42), 2);
                        e.Graphics.DrawLine(medianPen, xP50, centerY - barH / 2, xP50, centerY + barH / 2);

                        // OUTLIERY - czerwone kropki dla ekstremalnych wartości (poza P10-P90 ± 1.5×IQR)
                        if (outliers != null && outliers.Count > 0)
                        {
                            using var outlierBrush = new SolidBrush(Color.FromArgb(220, 38, 38));
                            using var outlierEdge = new Pen(Color.White, 1.5f);
                            foreach (var o in outliers)
                            {
                                int xO = XForValue(o);
                                e.Graphics.FillEllipse(outlierBrush, xO - 4, centerY - 4, 8, 8);
                                e.Graphics.DrawEllipse(outlierEdge, xO - 4, centerY - 4, 8, 8);
                            }
                        }

                        // Wartość po prawej + (n outlierów)
                        string rightText = outliers != null && outliers.Count > 0
                            ? $"{p50:N2} zł  •  {outliers.Count} outlier{(outliers.Count == 1 ? "" : "ów")}"
                            : $"{p50:N2} zł";
                        e.Graphics.DrawString(rightText, valueFont, navyBrush, barRight + 8, centerY - 7);

                        displayNamesIndex++;
                    }

                    // Skala X po dole
                    int axisY = topPad + stats.Count * rowHeight + 4;
                    using var axisFont = new Font("Segoe UI", 7.5F);
                    for (int i = 0; i <= 4; i++)
                    {
                        decimal v = globMin + globRange * i / 4;
                        int x = barLeft + (int)((double)i / 4 * barWidth);
                        e.Graphics.DrawString($"{v:N2}", axisFont, grayBrush, x - 12, axisY);
                    }
                }
                catch { /* Paint nie może rzucać */ }
            };

            return card;
        }

        // ───────────── Sekcja 2: Top/Bottom dni ─────────────

        private Panel BuildTopBottomDaysCard(int width)
        {
            var host = new TableLayoutPanel
            {
                Width = width,
                Height = 320,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = backgroundColor
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var margins = ComputeDailyMargins();
            var top = margins.OrderByDescending(t => t.Margin).Take(10).ToList();
            var bottom = margins.OrderBy(t => t.Margin).Take(10).ToList();

            host.Controls.Add(BuildRankingCard("🥇 Top 10 najlepszych dni marży", top, successColor), 0, 0);
            host.Controls.Add(BuildRankingCard("📉 Top 10 najgorszych dni marży", bottom, dangerColor), 1, 0);

            return host;
        }

        private Panel BuildRankingCard(string title, List<(DateTime Date, decimal Margin)> items, Color accent)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 4, 0),
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12)
            };
            card.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.White);
                e.Graphics.FillPath(bg, path);
                using var pen = new Pen(subtleBorderColor, 1);
                e.Graphics.DrawPath(pen, path);
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = accent,
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblTitle);

            if (items == null || items.Count == 0)
            {
                var empty = new Label
                {
                    Text = "Brak danych — załaduj pełniejszy okres",
                    Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                    ForeColor = Color.Gray,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                card.Controls.Add(empty);
                return card;
            }

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = items.Count + 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // Nagłówki
            grid.Controls.Add(MakeRankCellHeader("#"), 0, 0);
            grid.Controls.Add(MakeRankCellHeader("Data"), 1, 0);
            grid.Controls.Add(MakeRankCellHeader("Marża"), 2, 0);

            for (int i = 0; i < items.Count; i++)
            {
                var (date, margin) = items[i];
                var c1 = MakeRankCell((i + 1).ToString() + ".", false, Color.FromArgb(100, 116, 139));
                var c2 = MakeRankCell(date.ToString("dd.MM.yyyy ddd"), false, Color.FromArgb(15, 23, 42));
                var c3 = MakeRankCell($"{margin:N2} zł/kg", true, accent);
                // Cały wiersz klikalny → szczegóły dnia
                var capturedDate = date;
                EventHandler openDay = (s, e) =>
                {
                    var dr = filteredData?.AsEnumerable()
                        .FirstOrDefault(r => r["DataRaw"] != DBNull.Value
                                             && Convert.ToDateTime(r["DataRaw"]).Date == capturedDate);
                    if (dr != null)
                    {
                        var dgRow = new DataGridViewRow();
                        // Stwórz lekki proxy do ShowDetailedInfo - musi mieć dostęp do nazwanych kolumn,
                        // więc używamy dataGridView1 i znajdujemy odpowiedni wiersz
                        if (dataGridView1.DataSource is DataTable dt)
                        {
                            for (int rIdx = 0; rIdx < dataGridView1.Rows.Count; rIdx++)
                            {
                                var rowVal = dataGridView1.Rows[rIdx].Cells[_idxDataRaw].Value;
                                if (rowVal != null && rowVal != DBNull.Value
                                    && Convert.ToDateTime(rowVal).Date == capturedDate)
                                {
                                    ShowDetailedInfo(dataGridView1.Rows[rIdx]);
                                    return;
                                }
                            }
                        }
                    }
                };
                c1.Click += openDay; c2.Click += openDay; c3.Click += openDay;
                c1.Cursor = c2.Cursor = c3.Cursor = Cursors.Hand;
                // Hover effect
                EventHandler onHover = (s, e) => { ((Control)s).BackColor = Color.FromArgb(241, 245, 249); };
                EventHandler onLeave = (s, e) => { ((Control)s).BackColor = Color.Transparent; };
                c1.MouseEnter += onHover; c1.MouseLeave += onLeave;
                c2.MouseEnter += onHover; c2.MouseLeave += onLeave;
                c3.MouseEnter += onHover; c3.MouseLeave += onLeave;

                grid.Controls.Add(c1, 0, i + 1);
                grid.Controls.Add(c2, 1, i + 1);
                grid.Controls.Add(c3, 2, i + 1);
            }
            card.Controls.Add(grid);
            return card;
        }

        private Label MakeRankCellHeader(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Padding = new Padding(2, 0, 0, 4)
            };
        }

        private Label MakeRankCell(string text, bool bold, Color fore)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.25F, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = fore,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Padding = new Padding(2, 0, 0, 0)
            };
        }

        // ───────────── Sekcja 3: Korelacje ─────────────

        private Panel BuildCorrelationsCard(int width)
        {
            var card = new BufferedPanel
            {
                Width = width,
                Height = 640,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12)
            };
            card.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = GetRoundedRectangle(rect, 10);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.White);
                e.Graphics.FillPath(bg, path);
                using var pen = new Pen(subtleBorderColor, 1);
                e.Graphics.DrawPath(pen, path);
            };

            // Zbierz wartości każdej kolumny i wylicz korelacje
            var colData = new Dictionary<string, decimal[]>();
            foreach (var col in _priceColumns)
            {
                if (!filteredData.Columns.Contains(col)) continue;
                var rows = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value)
                    .OrderBy(r => Convert.ToDateTime(r["DataRaw"]))
                    .Select(r => r[col] != DBNull.Value ? (decimal?)Convert.ToDecimal(r[col]) : (decimal?)null)
                    .ToArray();
                colData[col] = rows.Select(v => v ?? decimal.MinValue).ToArray(); // sentinel
            }

            // Korelacje rysuję ręcznie w Paint dla pełnej kontroli
            card.Paint += (s, e) =>
            {
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Tytuł + legenda
                using var titleFont = new Font("Segoe UI Emoji", 11F);
                using var titleBrush = new SolidBrush(primaryColor);
                g.DrawString("🔗 Macierz korelacji Pearson r",
                    titleFont, titleBrush, 14, 8);

                // Legenda kolorów (gradient)
                using var legendFont = new Font("Segoe UI Semibold", 9F);
                using var legendTextBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                int legendY = 12;
                int legendX = 320;
                int legendW = card.Width - legendX - 20;
                int legendH = 16;
                // Gradient od czerwonego do zielonego
                for (int px = 0; px < legendW; px++)
                {
                    double t = (double)px / legendW;
                    double r = -1 + t * 2;
                    using var lb = new SolidBrush(CorrelationColor(r));
                    g.FillRectangle(lb, legendX + px, legendY, 1, legendH);
                }
                using var legendBorderPen = new Pen(Color.FromArgb(220, 226, 232), 1);
                g.DrawRectangle(legendBorderPen, legendX, legendY, legendW, legendH);
                g.DrawString("−1", legendFont, legendTextBrush, legendX - 22, legendY);
                g.DrawString("0", legendFont, legendTextBrush, legendX + legendW / 2 - 4, legendY + legendH);
                g.DrawString("+1", legendFont, legendTextBrush, legendX + legendW + 4, legendY);

                // Dynamiczne wyliczenie cellSize - dopasuj się do dostępnej wysokości
                int leftLabelW = 130;   // miejsce na nazwy wierszy
                int topLabelH = 40;     // miejsce na nazwy kolumn
                int titleArea = 50;     // tytuł + legenda
                int padding = 16;

                int availW = card.Width - leftLabelW - padding * 2;
                int availH = card.Height - topLabelH - titleArea - padding;

                int cellSize = Math.Min(availW / _priceColumns.Length, availH / _priceColumns.Length);
                cellSize = Math.Max(50, Math.Min(90, cellSize));

                int totalGridW = cellSize * _priceColumns.Length;
                int startX = padding + leftLabelW + (availW - totalGridW) / 2;
                int startY = titleArea + topLabelH;

                using var headerFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                using var headerBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                using var cellFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);

                var shortNames = new Dictionary<string, string>
                {
                    ["Minister"] = "Ministerialna",
                    ["Laczona"] = "Łączona",
                    ["Rolnicza"] = "Rolnicza",
                    ["Wolnorynkowa"] = "Wolnorynkowa",
                    ["Tuszka Zrzeszenia"] = "Tusz. Zrzesz.",
                    ["Nasza Tuszka"] = "Nasza Tuszka"
                };

                // Nagłówki kolumn (góra) - obrócone -45° aby się zmieściły
                for (int j = 0; j < _priceColumns.Length; j++)
                {
                    var col = _priceColumns[j];
                    var name = shortNames.TryGetValue(col, out var sn) ? sn : col;
                    int cx = startX + j * cellSize + cellSize / 2;
                    int cy = startY - 8;
                    var state = g.Save();
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(-30);
                    using var sfHeader = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    g.DrawString(name, headerFont, headerBrush, 0, 0, sfHeader);
                    g.Restore(state);
                }

                // Etykiety wierszy + komórki
                for (int i = 0; i < _priceColumns.Length; i++)
                {
                    var rowCol = _priceColumns[i];
                    var rowName = shortNames.TryGetValue(rowCol, out var sn2) ? sn2 : rowCol;
                    var rowLabelRect = new RectangleF(padding, startY + i * cellSize, leftLabelW - 8, cellSize);
                    using var rf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(rowName, headerFont, headerBrush, rowLabelRect, rf);

                    for (int j = 0; j < _priceColumns.Length; j++)
                    {
                        var cellRect = new Rectangle(startX + j * cellSize, startY + i * cellSize, cellSize - 2, cellSize - 2);

                        if (!colData.ContainsKey(_priceColumns[i]) || !colData.ContainsKey(_priceColumns[j]))
                        {
                            using var noBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                            g.FillRectangle(noBrush, cellRect);
                            continue;
                        }

                        var r = ComputePearson(colData[_priceColumns[i]], colData[_priceColumns[j]]);
                        Color bgColor = r.HasValue ? CorrelationColor(r.Value) : Color.FromArgb(241, 245, 249);

                        using var cellBrush = new SolidBrush(bgColor);
                        g.FillRectangle(cellBrush, cellRect);

                        // Diagonal - wyraźna ramka żeby zaznaczyć "to ten sam"
                        if (i == j)
                        {
                            using var diagPen = new Pen(Color.FromArgb(148, 163, 184), 2);
                            g.DrawRectangle(diagPen, cellRect);
                        }
                        else
                        {
                            using var cellPen = new Pen(Color.FromArgb(220, 220, 220), 1);
                            g.DrawRectangle(cellPen, cellRect);
                        }

                        if (r.HasValue)
                        {
                            // Silna korelacja (|r| >= 0.7) - dodatkowy gruby obrys
                            if (Math.Abs(r.Value) >= 0.7 && i != j)
                            {
                                using var strongPen = new Pen(r.Value > 0 ? successColor : dangerColor, 2.5f);
                                g.DrawRectangle(strongPen, cellRect);
                            }

                            // Tekst kontrastowy
                            var bright = bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114;
                            var fg = bright < 140 ? Color.White : Color.FromArgb(15, 23, 42);
                            using var textBrush = new SolidBrush(fg);
                            using var sfm = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString(r.Value.ToString("+0.00;-0.00;0.00"), cellFont, textBrush, cellRect, sfm);
                        }
                    }
                }

                // TOP 3 NAJSILNIEJSZE KORELACJE - lista pod macierzą
                int hintY = startY + _priceColumns.Length * cellSize + 16;
                using var topHeaderFont = new Font("Segoe UI Emoji", 10F);
                using var topBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                g.DrawString("🔝 TOP 3 najsilniejsze korelacje:", topHeaderFont, topBrush, padding + 8, hintY);

                var allPairs = new List<(string A, string B, double R)>();
                for (int i = 0; i < _priceColumns.Length; i++)
                {
                    for (int j = i + 1; j < _priceColumns.Length; j++)
                    {
                        if (!colData.ContainsKey(_priceColumns[i]) || !colData.ContainsKey(_priceColumns[j])) continue;
                        var pr = ComputePearson(colData[_priceColumns[i]], colData[_priceColumns[j]]);
                        if (pr.HasValue)
                        {
                            allPairs.Add((
                                shortNames.TryGetValue(_priceColumns[i], out var aN) ? aN : _priceColumns[i],
                                shortNames.TryGetValue(_priceColumns[j], out var bN) ? bN : _priceColumns[j],
                                pr.Value));
                        }
                    }
                }
                var top3 = allPairs.OrderByDescending(p => Math.Abs(p.R)).Take(3).ToList();

                using var pairFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                using var rFont = new Font("Consolas", 9.5F, FontStyle.Bold);
                int yPair = hintY + 22;
                int idx = 1;
                foreach (var (a, b, r) in top3)
                {
                    string emoji = Math.Abs(r) >= 0.85 ? "🔥" : Math.Abs(r) >= 0.7 ? "💪" : "🔗";
                    string arrow = r > 0 ? "↗" : "↘";
                    Color clr = r > 0 ? successColor : dangerColor;
                    using var pairBrush = new SolidBrush(clr);

                    string text = $"{idx}.  {emoji}  {a}  {arrow}  {b}";
                    g.DrawString(text, pairFont, topBrush, padding + 16, yPair);

                    var sizeText = g.MeasureString(text, pairFont);
                    g.DrawString($"r = {r:+0.000;-0.000;0.000}", rFont, pairBrush, padding + 16 + sizeText.Width + 8, yPair);

                    yPair += 22;
                    idx++;
                }

                // Stopka z hintem
                using var hintFont = new Font("Segoe UI Emoji", 8.75F, FontStyle.Italic);
                using var hintBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                g.DrawString("💡 Pogrubione obrysy = silne korelacje (|r| ≥ 0,7)  •  Diagonal (szara ramka) = zawsze 1,00",
                    hintFont, hintBrush, padding + 8, yPair + 6);
                }
                catch { /* Paint nie może rzucać */ }
            };

            return card;
        }

        private static double? ComputePearson(decimal[] xs, decimal[] ys)
        {
            if (xs == null || ys == null) return null;
            int n = Math.Min(xs.Length, ys.Length);
            // Skip sentinel (decimal.MinValue) — pary z brakami
            var pairs = new List<(double x, double y)>(n);
            for (int i = 0; i < n; i++)
            {
                if (xs[i] == decimal.MinValue || ys[i] == decimal.MinValue) continue;
                pairs.Add(((double)xs[i], (double)ys[i]));
            }
            if (pairs.Count < 3) return null;

            double meanX = pairs.Average(p => p.x);
            double meanY = pairs.Average(p => p.y);
            double sumXY = 0, sumX2 = 0, sumY2 = 0;
            foreach (var (x, y) in pairs)
            {
                var dx = x - meanX;
                var dy = y - meanY;
                sumXY += dx * dy;
                sumX2 += dx * dx;
                sumY2 += dy * dy;
            }
            if (sumX2 == 0 || sumY2 == 0) return null;
            return sumXY / Math.Sqrt(sumX2 * sumY2);
        }

        private static Color CorrelationColor(double r)
        {
            // r ∈ [-1, +1]. -1 → czerwony, 0 → biały, +1 → zielony
            r = Math.Max(-1, Math.Min(1, r));
            if (r >= 0)
            {
                int g = 255;
                int rch = (int)(255 - r * 80);  // im bliżej +1, tym ciemniejszy
                int b = (int)(255 - r * 200);
                return Color.FromArgb(rch, g, b);
            }
            else
            {
                double t = -r;
                int rch = 255;
                int g = (int)(255 - t * 200);
                int b = (int)(255 - t * 200);
                return Color.FromArgb(rch, g, b);
            }
        }

        private Panel CreateDateRangeCard()
        {
            int days = Math.Max(1, (mainDateTo.Value.Date - mainDateFrom.Value.Date).Days + 1);
            var card = new BufferedPanel
            {
                Size = new Size(400, 340),
                BackColor = primaryColor,
                Margin = new Padding(8, 8, 8, 8)
            };
            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var fill = new SolidBrush(primaryColor);
                    e.Graphics.FillPath(fill, path);

                    // Subtelny gradient od jaśniejszego u góry do ciemniejszego na dole
                    using var gradBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        rect,
                        Color.FromArgb(60, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255),
                        System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                    e.Graphics.SetClip(path);
                    e.Graphics.FillRectangle(gradBrush, rect);
                    e.Graphics.ResetClip();
                }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };

            // Tytuł — Segoe UI Emoji aby renderować 📅
            var lblTitle = new Label
            {
                Text = "📅  Zakres analizy",
                Font = new Font("Segoe UI Emoji", 13F),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(16, 14),
                AutoSize = true
            };

            // Etykieta NAD dużą cyfrą (mniejszy nacisk niż dotąd, oddech między elementami)
            var lblDaysUnit = new Label
            {
                Text = days == 1 ? "dzień w okresie" : "dni w okresie",
                Font = new Font("Segoe UI Emoji", 9.5F),
                ForeColor = Color.FromArgb(220, 240, 255),
                BackColor = Color.Transparent,
                Location = new Point(16, 50),
                AutoSize = true
            };

            // DUŻA wartość - liczba dni - PONIŻEJ etykiety z większym oddechem
            var lblDays = new Label
            {
                Text = days.ToString(),
                Font = new Font("Segoe UI", 36F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(14, 70),
                AutoSize = false,
                Width = 200,
                Height = 56,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Ikonka po prawej z większym emoji 📅
            var lblBigIcon = new Label
            {
                Text = "📅",
                Font = new Font("Segoe UI Emoji", 40F),
                ForeColor = Color.FromArgb(180, 255, 255, 255),
                BackColor = Color.Transparent,
                Location = new Point(card.Width - 80, 60),
                AutoSize = false,
                Width = 70,
                Height = 70,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Pozioma linia separator - przesunięta niżej żeby dać oddech od dużej cyfry
            var sepPanel = new Panel
            {
                BackColor = Color.FromArgb(80, 255, 255, 255),
                Location = new Point(16, 144),
                Size = new Size(card.Width - 32, 1)
            };

            // Etykiety zakresu z ikonami - poniżej separatora
            int yRow = 158;
            void AddInfoRow(string emoji, string label, string value, ref int y)
            {
                var ico = new Label
                {
                    Text = emoji,
                    Font = new Font("Segoe UI Emoji", 12F),
                    ForeColor = Color.FromArgb(220, 240, 255),
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    Width = 26,
                    Height = 22,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(16, y)
                };
                var lbl = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(220, 240, 255),
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    Width = 80,
                    Height = 22,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(42, y)
                };
                var val = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Location = new Point(122, y)
                };
                card.Controls.Add(ico);
                card.Controls.Add(lbl);
                card.Controls.Add(val);
                y += 28;
            }

            AddInfoRow("▶", "Od:", $"{mainDateFrom.Value:dd.MM.yyyy ddd}", ref yRow);
            AddInfoRow("⏹", "Do:", $"{mainDateTo.Value:dd.MM.yyyy ddd}", ref yRow);
            AddInfoRow("🗓", "Weekendy:", chkShowWeekends?.Checked == true ? "Pokazane" : "Ukryte", ref yRow);
            AddInfoRow("📊", "Agregacja:", GetAggregationXLabel().Split(' ')[0], ref yRow);

            card.Controls.AddRange(new Control[] { lblTitle, lblDays, lblDaysUnit, lblBigIcon, sepPanel });

            return card;
        }

        private Panel CreateStatsCard(string title, List<decimal> values, List<decimal> previousValues = null)
        {
            // Kolor akcentu z _kpiTheme jeśli pasujący
            Color accentColor = _kpiTheme.TryGetValue(title, out var theme) ? theme.Bg : primaryColor;

            var card = new BufferedPanel
            {
                Size = new Size(400, 340),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(8, 8, 8, 8)
            };

            card.Paint += (s, e) =>
            {
                try
                {
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using var path = GetRoundedRectangle(rect, 10);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    // Subtelny cień
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(15, 0, 0, 0)))
                    {
                        e.Graphics.TranslateTransform(2, 3);
                        e.Graphics.FillPath(shadowBrush, path);
                        e.Graphics.ResetTransform();
                    }
                    using (var bgBrush = new SolidBrush(Color.White))
                        e.Graphics.FillPath(bgBrush, path);
                    using (var pen = new Pen(subtleBorderColor, 1))
                        e.Graphics.DrawPath(pen, path);

                    // Pasek akcentu na górze (4 px) - identyfikator cenowy
                    e.Graphics.SetClip(path);
                    using var accentBrush = new SolidBrush(accentColor);
                    e.Graphics.FillRectangle(accentBrush, 0, 0, card.Width, 4);
                    e.Graphics.ResetClip();
                }
                catch { /* Paint nie może rzucać */ }
            };

            // Statystyki
            var avg = values.Average();
            var min = values.Min();
            var max = values.Max();
            var stdDev = CalculateStandardDeviation(values);
            var median = CalculateMedian(values);
            var volatilityPct = avg == 0 ? 0 : (stdDev / avg) * 100m;
            var p10 = CalculatePercentile(values, 0.10);
            var p90 = CalculatePercentile(values, 0.90);
            var trendPct = CalculateTrendPercent(values);

            // Tytuł z kolorowym tekstem (kolor akcentu)
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = Color.Transparent,
                Location = new Point(16, 12),
                AutoSize = true
            };
            card.Controls.Add(lblTitle);

            // Etykieta "średnia" PRZED dużą wartością (żeby się nie nakładało)
            var lblBigAvgLabel = new Label
            {
                Text = "📊 średnia w okresie",
                Font = new Font("Segoe UI Emoji", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.Transparent,
                Location = new Point(16, 42),
                AutoSize = true
            };
            card.Controls.Add(lblBigAvgLabel);

            // DUŻA wartość - mocno odsunięta od etykiety i od gridu
            var lblBigAvg = new Label
            {
                Text = $"{avg:N2} zł",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.Transparent,
                Location = new Point(14, 64),
                AutoSize = false,
                Width = 200,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft
            };
            card.Controls.Add(lblBigAvg);

            // TREND - po prawej u góry, etykieta NAD wartością
            string trendArrow = trendPct > 0 ? "▲" : trendPct < 0 ? "▼" : "→";
            Color trendColor = trendPct > 0 ? successColor : trendPct < 0 ? dangerColor : Color.Gray;

            var lblTrendLabel = new Label
            {
                Text = "📈 trend okresu",
                Font = new Font("Segoe UI Emoji", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 130,
                Height = 16,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(card.Width - 146, 42)
            };
            card.Controls.Add(lblTrendLabel);

            var lblTrend = new Label
            {
                Text = $"{trendArrow}  {trendPct:+0.0;-0.0;0.0}%",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = trendColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 130,
                Height = 40,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(card.Width - 146, 64)
            };
            card.Controls.Add(lblTrend);

            // Grid statystyk - przesunięty niżej żeby nie nakładał się na dużą cenę
            int gridY = 124;
            int colW = (card.Width - 32) / 3;
            void AddStatBox(int col, int row, string emoji, string label, string value, Color valueColor)
            {
                int x = 16 + col * colW;
                int y = gridY + row * 38;
                // Emoji - fixed width żeby nie nakładał się z tekstem
                var lblE = new Label
                {
                    Text = emoji,
                    Font = new Font("Segoe UI Emoji", 14F),
                    AutoSize = false,
                    Width = 26,
                    Height = 32,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(x, y + 2),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    BackColor = Color.Transparent
                };
                var lblL = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    AutoSize = false,
                    Width = colW - 30,
                    Height = 14,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(x + 30, y),
                    BackColor = Color.Transparent
                };
                var lblV = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
                    ForeColor = valueColor,
                    AutoSize = false,
                    Width = colW - 30,
                    Height = 18,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(x + 30, y + 16),
                    BackColor = Color.Transparent
                };
                card.Controls.AddRange(new Control[] { lblE, lblL, lblV });
            }

            AddStatBox(0, 0, "🎯", "MEDIANA", $"{median:N2}", Color.FromArgb(15, 23, 42));
            AddStatBox(1, 0, "📉", "MIN",     $"{min:N2}",    dangerColor);
            AddStatBox(2, 0, "📈", "MAX",     $"{max:N2}",    successColor);
            AddStatBox(0, 1, "📊", "P10–P90", $"{p10:N2}–{p90:N2}", Color.FromArgb(15, 23, 42));
            AddStatBox(1, 1, "📐", "ODCH.STD", $"{stdDev:N2}", Color.FromArgb(71, 85, 105));
            AddStatBox(2, 1, "📅", "DNI",     $"{values.Count}",    Color.FromArgb(71, 85, 105));

            // Porównanie z poprzednim okresem (jeśli włączone)
            if (previousValues != null && previousValues.Count > 0)
            {
                var prevAvg = previousValues.Average();
                var diff = avg - prevAvg;
                var diffPct = prevAvg == 0 ? 0 : (diff / prevAvg) * 100m;
                var arrow = diff > 0 ? "▲" : diff < 0 ? "▼" : "→";
                var lblCompare = new Label
                {
                    Text = $"{arrow}  vs poprzedni okres: {diff:+0.00;-0.00;0.00} zł  ({diffPct:+0.0;-0.0;0.0}%) — śr. {prevAvg:N2} zł, n={previousValues.Count}",
                    Font = new Font("Segoe UI Emoji", 9F),
                    AutoSize = true,
                    Location = new Point(16, 210),
                    ForeColor = diff > 0 ? successColor : diff < 0 ? dangerColor : Color.Gray
                };
                card.Controls.Add(lblCompare);
            }

            // Sparkline na dole karty z datami pod osią X
            // Zbierz wartości + daty (zsynchronizowane) z filteredData dla tej kolumny
            List<DateTime> sparkDates = null;
            List<decimal> sparkValues = values;
            if (filteredData != null && filteredData.Columns.Contains(title))
            {
                var pairs = filteredData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value && r[title] != DBNull.Value)
                    .Select(r => (Date: Convert.ToDateTime(r["DataRaw"]).Date, V: Convert.ToDecimal(r[title])))
                    .OrderBy(t => t.Date)
                    .ToList();
                if (pairs.Count >= 2)
                {
                    sparkDates = pairs.Select(p => p.Date).ToList();
                    sparkValues = pairs.Select(p => p.V).ToList();
                }
            }

            var spark = new Panel
            {
                Location = new Point(16, 250),
                Size = new Size(card.Width - 32, 76),
                BackColor = Color.White
            };
            spark.Paint += (s, e) =>
            {
                try { DrawSparkline(e.Graphics, spark.ClientRectangle, sparkValues, sparkDates); }
                catch (Exception ex) { Debug.WriteLine($"[WidokCen] {ex.GetType().Name}: {ex.Message}"); }
            };
            card.Controls.Add(spark);

            // Mini-label nad sparkline
            var lblSparkLabel = new Label
            {
                Text = "📈 Trend w czasie  •  ▲ max  ▼ min  ● ostatni",
                Font = new Font("Segoe UI Emoji", 8.75F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(16, 232)
            };
            card.Controls.Add(lblSparkLabel);

            return card;
        }

        private static decimal CalculateMedian(List<decimal> values)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;
        }

        private static decimal CalculatePercentile(List<decimal> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            double rank = percentile * (sorted.Count - 1);
            int lower = (int)Math.Floor(rank);
            int upper = (int)Math.Ceiling(rank);
            if (lower == upper) return sorted[lower];
            decimal frac = (decimal)(rank - lower);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * frac;
        }

        private static decimal CalculateTrendPercent(List<decimal> values)
        {
            // Slope linii regresji znormalizowany jako % zmiany od początku do końca okresu
            if (values == null || values.Count < 2) return 0;
            int n = values.Count;
            decimal sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXY += i * values[i];
                sumX2 += i * i;
            }
            decimal denom = n * sumX2 - sumX * sumX;
            if (denom == 0) return 0;
            decimal slope = (n * sumXY - sumX * sumY) / denom;
            decimal startEstimate = sumY / n - slope * (sumX / n) + slope * 0;
            decimal totalChange = slope * (n - 1);
            return startEstimate == 0 ? 0 : (totalChange / startEstimate) * 100m;
        }

        private void DrawSparkline(Graphics g, Rectangle bounds, List<decimal> values, List<DateTime> dates = null)
        {
            if (values == null || values.Count < 2) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var min = values.Min();
            var max = values.Max();
            var range = max - min;
            if (range == 0) range = 1;

            int padding = 4;
            int labelArea = (dates != null && dates.Count == values.Count) ? 18 : 0; // miejsce na daty na dole
            float w = bounds.Width - padding * 2;
            float h = bounds.Height - padding * 2 - labelArea;

            var points = new PointF[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                float x = padding + (i / (float)(values.Count - 1)) * w;
                float y = padding + (1f - (float)((values[i] - min) / range)) * h;
                points[i] = new PointF(x, y);
            }

            float lineBottom = padding + h;

            // Wypełniony obszar pod linią
            var fillPoints = new List<PointF>(points);
            fillPoints.Add(new PointF(points[points.Length - 1].X, lineBottom));
            fillPoints.Add(new PointF(points[0].X, lineBottom));
            using (var fillBrush = new SolidBrush(Color.FromArgb(40, primaryColor)))
                g.FillPolygon(fillBrush, fillPoints.ToArray());

            // Linia
            using (var pen = new Pen(primaryColor, 1.5f))
                g.DrawLines(pen, points);

            // Min / max markery
            int idxMin = values.IndexOf(min);
            int idxMax = values.IndexOf(max);
            using (var brushMin = new SolidBrush(dangerColor))
                g.FillEllipse(brushMin, points[idxMin].X - 3, points[idxMin].Y - 3, 6, 6);
            using (var brushMax = new SolidBrush(successColor))
                g.FillEllipse(brushMax, points[idxMax].X - 3, points[idxMax].Y - 3, 6, 6);

            // Last value marker
            using (var brushLast = new SolidBrush(primaryColor))
                g.FillEllipse(brushLast, points[points.Length - 1].X - 3, points[points.Length - 1].Y - 3, 6, 6);

            // Etykiety osi X — daty (3 pozycje: początek, środek, koniec)
            if (dates != null && dates.Count == values.Count && dates.Count >= 2)
            {
                using var labelFont = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
                using var labelBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                float labelY = lineBottom + 4;

                int[] indices;
                if (dates.Count <= 3) indices = Enumerable.Range(0, dates.Count).ToArray();
                else indices = new[] { 0, dates.Count / 2, dates.Count - 1 };

                foreach (int idx in indices)
                {
                    string text = dates[idx].ToString("dd.MM");
                    var sz = g.MeasureString(text, labelFont);
                    float lx = points[idx].X - sz.Width / 2;
                    // Zapobiegaj wyjściu poza bounds
                    if (lx < 0) lx = 0;
                    if (lx + sz.Width > bounds.Width) lx = bounds.Width - sz.Width;
                    g.DrawString(text, labelFont, labelBrush, lx, labelY);
                }
            }
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

        // Cache nazw kolumn po indeksie (zapełniony w FormatDataGrid)
        private string[] _columnNamesByIndex = Array.Empty<string>();

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            int rowIdx = e.RowIndex, colIdx = e.ColumnIndex;
            if (rowIdx < 0 || colIdx < 0) return;

            var row = dataGridView1.Rows[rowIdx];
            string colName = colIdx < _columnNamesByIndex.Length
                ? _columnNamesByIndex[colIdx]
                : dataGridView1.Columns[colIdx].Name;

            // Podświetl dzisiejszą datę / weekendy - tylko raz per wiersz (gdy ColumnIndex == _idxDataRaw odpowiednika)
            // Sprawdzaj DataRaw przez cache indeksu
            if (_idxDataRaw >= 0)
            {
                var dataRawVal = row.Cells[_idxDataRaw].Value;
                if (dataRawVal != null && dataRawVal != DBNull.Value)
                {
                    var date = Convert.ToDateTime(dataRawVal);
                    if (date.Date == DateTime.Today)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 196);
                        row.DefaultCellStyle.Font = _gridFontBold;
                    }
                    else if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                    }
                }
            }

            // Kolorowanie różnic (po indeksie kolumny — szybciej niż string compare)
            if (colIdx == _idxRolniczaWolny || colIdx == _idxRoznicaTuszek)
            {
                if (e.Value is decimal dv)
                {
                    e.CellStyle.ForeColor = dv >= 0 ? successColor : dangerColor;
                    e.CellStyle.Font = _gridFontBold;
                }
                else if (e.Value != null && e.Value != DBNull.Value &&
                         decimal.TryParse(e.Value.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                {
                    e.CellStyle.ForeColor = parsed >= 0 ? successColor : dangerColor;
                    e.CellStyle.Font = _gridFontBold;
                }
            }

            // Heat-mapa dla kolumn cenowych
            if (_heatMapRange != null && e.Value != null && e.Value != DBNull.Value
                && _heatMapRange.TryGetValue(colName, out var range))
            {
                decimal cellVal;
                if (e.Value is decimal d) cellVal = d;
                else if (!decimal.TryParse(e.Value.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out cellVal))
                {
                    return;
                }
                var span = range.Max - range.Min;
                if (span > 0)
                {
                    var t = (double)((cellVal - range.Min) / span);
                    e.CellStyle.BackColor = HeatMapColor(t);
                }
            }

            // Wartości imputowane — niebieski kursywą
            if (colIdx == _idxWolnorynkowa && _idxWolnorynkowaImputed >= 0)
            {
                var imputedVal = row.Cells[_idxWolnorynkowaImputed].Value;
                if (imputedVal != null && imputedVal != DBNull.Value && (bool)imputedVal)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(37, 99, 235);
                    e.CellStyle.Font = _gridFontItalicBold;
                    e.CellStyle.BackColor = Color.FromArgb(219, 234, 254);
                }
            }
        }

        private static Color HeatMapColor(double t)
        {
            // 0 → bardzo jasny czerwony, 0.5 → jasnożółty, 1 → bardzo jasny zielony (subtelny)
            t = Math.Max(0, Math.Min(1, t));
            int r, g, b;
            if (t < 0.5)
            {
                // Red → Yellow
                double k = t * 2;
                r = 255;
                g = (int)(220 * k + 235 * (1 - k));   // 235 → 220
                b = (int)(225 * (1 - k) + 200 * k);   // 225 → 200
                // Mix do jasnego: blend z białym 65%
                r = MixWithWhite(r, 0.65); g = MixWithWhite(g, 0.65); b = MixWithWhite(b, 0.65);
                return Color.FromArgb(r, g, b);
            }
            else
            {
                // Yellow → Green
                double k = (t - 0.5) * 2;
                r = (int)(255 * (1 - k) + 200 * k);   // 255 → 200
                g = 230;
                b = (int)(200 * (1 - k) + 220 * k);   // 200 → 220
                r = MixWithWhite(r, 0.65); g = MixWithWhite(g, 0.65); b = MixWithWhite(b, 0.65);
                return Color.FromArgb(r, g, b);
            }
        }

        private static int MixWithWhite(int c, double weightWhite)
        {
            return (int)(255 * weightWhite + c * (1 - weightWhite));
        }

        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridView1.Rows[e.RowIndex];
            ShowDetailedInfo(row);
        }

        private void ShowDetailedInfo(DataGridViewRow row)
        {
            DateTime? rowDate = null;
            if (row.Cells["DataRaw"].Value != DBNull.Value)
                rowDate = Convert.ToDateTime(row.Cells["DataRaw"].Value);

            // Znajdź poprzedni dzień z danymi
            DataRow prevRow = null;
            if (rowDate.HasValue && originalData != null)
            {
                prevRow = originalData.AsEnumerable()
                    .Where(r => r["DataRaw"] != DBNull.Value
                                && Convert.ToDateTime(r["DataRaw"]).Date < rowDate.Value.Date)
                    .OrderByDescending(r => Convert.ToDateTime(r["DataRaw"]))
                    .FirstOrDefault();
            }

            var form = new Form
            {
                Text = $"Szczegóły dnia – {row.Cells["Data"].Value}",
                Size = new Size(960, 760),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = backgroundColor,
                FormBorderStyle = FormBorderStyle.Sizable,
                MaximizeBox = true,
                MinimizeBox = false,
                MinimumSize = new Size(720, 540)
            };
            WindowIconHelper.SetIcon(form);

            var rootPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20),
                BackColor = backgroundColor
            };
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // Nagłówek z ikonką (Segoe UI Emoji żeby się wyświetlała)
            var lblHeader = new Label
            {
                Text = $"📅  {row.Cells["Data"].Value}" + (prevRow != null
                    ? $"     ⟵ poprzedni dzień: {prevRow["Data"]}"
                    : ""),
                Font = new Font("Segoe UI Emoji", 14F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            rootPanel.Controls.Add(lblHeader, 0, 0);

            // Karty cen
            var cardsHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = backgroundColor,
                Padding = new Padding(0, 4, 0, 0)
            };

            var groups = new (string Group, string Emoji, (string Col, string Label)[] Items)[]
            {
                ("CENY SKUPU", "💰", new[]
                {
                    ("Minister", "Ministerialna"),
                    ("Laczona", "Łączona"),
                    ("Rolnicza", "Rolnicza"),
                    ("Wolnorynkowa", "Wolnorynkowa")
                }),
                ("CENY TUSZKI", "🏭", new[]
                {
                    ("Tuszka Zrzeszenia", "Tuszka Zrzeszenia"),
                    ("Nasza Tuszka", "Nasza Tuszka")
                }),
                ("RÓŻNICE", "📊", new[]
                {
                    ("Rolnicza-Wolny", "Rolnicza − Wolnorynkowa"),
                    ("Różnica Tuszek", "Nasza Tuszka − Zrzeszenia")
                })
            };

            foreach (var grp in groups)
            {
                cardsHost.Controls.Add(CreateGroupHeader($"{grp.Emoji}  {grp.Group}"));
                foreach (var (col, label) in grp.Items)
                {
                    if (!filteredData.Columns.Contains(col)) continue;
                    var card = CreateDetailCard(label, row.Cells[col].Value, prevRow?[col]);
                    cardsHost.Controls.Add(card);
                }
            }

            rootPanel.Controls.Add(cardsHost, 0, 1);

            // Stopka z przyciskiem zamknij
            var btnClose = new Button
            {
                Text = "Zamknij (Esc)",
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = primaryColor,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => form.Close();

            var btnCopy = new Button
            {
                Text = "📋 Kopiuj",
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(71, 85, 105),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderColor = subtleBorderColor;
            btnCopy.Click += (s, e) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Data: {row.Cells["Data"].Value}");
                foreach (var grp in groups)
                {
                    sb.AppendLine();
                    sb.AppendLine(grp.Group);
                    foreach (var (col, label) in grp.Items)
                    {
                        if (!filteredData.Columns.Contains(col)) continue;
                        var val = row.Cells[col].Value;
                        sb.AppendLine($"  {label}: {(val == null || val == DBNull.Value ? "—" : Convert.ToDecimal(val).ToString("N2") + " zł")}");
                    }
                }
                Clipboard.SetText(sb.ToString());
            };

            var footerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = backgroundColor
            };
            footerPanel.Controls.Add(btnClose);
            footerPanel.Controls.Add(btnCopy);
            rootPanel.Controls.Add(footerPanel, 0, 2);

            form.AcceptButton = btnClose;
            form.KeyPreview = true;
            form.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) form.Close(); };

            form.Controls.Add(rootPanel);
            form.ShowDialog();
        }

        private Label CreateGroupHeader(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Emoji", 11F),
                ForeColor = primaryColor,
                AutoSize = false,
                Width = 920,
                Height = 32,
                Margin = new Padding(2, 12, 0, 6),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Panel CreateDetailCard(string title, object currentValue, object previousValue)
        {
            var card = new Panel
            {
                Size = new Size(440, 110),
                BackColor = Color.White,
                Margin = new Padding(6, 4, 6, 4)
            };
            card.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = GetRoundedRectangle(rect, 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var bg = new SolidBrush(Color.White))
                    e.Graphics.FillPath(bg, path);
                using (var pen = new Pen(subtleBorderColor, 1))
                    e.Graphics.DrawPath(pen, path);
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(14, 10)
            };

            string valueText;
            decimal? cur = TryParseDecimal(currentValue);
            decimal? prev = TryParseDecimal(previousValue);

            if (cur.HasValue) valueText = $"{cur.Value:N2} zł";
            else valueText = "— brak danych";

            var lblValue = new Label
            {
                Text = valueText,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = cur.HasValue ? Color.FromArgb(15, 23, 42) : Color.LightGray,
                AutoSize = true,
                Location = new Point(14, 32)
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblValue);

            // Δ vs poprzedni dzień - z Segoe UI Emoji
            if (cur.HasValue && prev.HasValue)
            {
                var diff = cur.Value - prev.Value;
                var pct = prev.Value == 0 ? 0 : (diff / prev.Value) * 100m;
                var arrow = diff > 0 ? "▲" : diff < 0 ? "▼" : "→";
                var lblDelta = new Label
                {
                    Text = $"{arrow} {diff:+0.00;-0.00;0.00} zł  ({pct:+0.0;-0.0;0.0}%)  vs poprzedni dzień: {prev.Value:N2} zł",
                    Font = new Font("Segoe UI Emoji", 9F),
                    AutoSize = true,
                    Location = new Point(14, 80),
                    ForeColor = diff > 0 ? successColor : diff < 0 ? dangerColor : Color.Gray
                };
                card.Controls.Add(lblDelta);
            }

            return card;
        }

        private static decimal? TryParseDecimal(object val)
        {
            if (val == null || val == DBNull.Value) return null;
            var s = val.ToString().Replace(" zł", "").Replace(",", ".");
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal d))
                return d;
            return null;
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
            _ = OpenInExcelAsync();
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
                Text = "Połączenie i informacje",
                Size = new Size(540, 480),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = backgroundColor,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowIcon = false
            };
            WindowIconHelper.SetIcon(form);

            var rootPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(16),
                BackColor = backgroundColor
            };
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20),
                AutoScroll = true
            };
            content.Paint += (s, ev) =>
            {
                var rect = new Rectangle(0, 0, content.Width - 1, content.Height - 1);
                using var path = GetRoundedRectangle(rect, 8);
                using var pen = new Pen(subtleBorderColor, 1);
                ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                ev.Graphics.DrawPath(pen, path);
            };

            int y = 8;

            // Sekcja: Połączenia
            content.Controls.Add(new Label
            {
                Text = "🔌 Źródła danych",
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = primaryColor,
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 28;

            string[] connInfo =
            {
                "LibraNet (192.168.0.109): CenaMinisterialna, CenaRolnicza, CenaTuszki, HarmonogramDostaw",
                "Handel (192.168.0.112): HM.DP — średnia sprzedaży Kurczak A (kat. 67095)"
            };
            foreach (var line in connInfo)
            {
                content.Controls.Add(new Label
                {
                    Text = "•  " + line,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    AutoSize = true,
                    Location = new Point(8, y)
                });
                y += 22;
            }

            var btnTest = new Button
            {
                Text = "🔄 Test połączeń",
                Location = new Point(8, y + 8),
                Size = new Size(160, 32),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5F)
            };
            btnTest.FlatAppearance.BorderSize = 0;

            var lblTestResult = new Label
            {
                Location = new Point(180, y + 14),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Gray
            };
            content.Controls.Add(btnTest);
            content.Controls.Add(lblTestResult);
            btnTest.Click += async (s, ev) =>
            {
                btnTest.Enabled = false;
                btnTest.Text = "Testowanie…";
                lblTestResult.Text = "";

                bool libraOk = false, handelOk = false;
                string libraErr = null, handelErr = null;
                await Task.Run(() =>
                {
                    try { using var c = new SqlConnection(connectionString); c.Open(); libraOk = true; }
                    catch (Exception exc) { libraErr = exc.Message; }
                    try { using var c = new SqlConnection(connectionStringHandel); c.Open(); handelOk = true; }
                    catch (Exception exc) { handelErr = exc.Message; }
                });

                lblTestResult.Text =
                    (libraOk ? "✅ LibraNet OK" : "❌ LibraNet: " + libraErr?.Split('\n').FirstOrDefault())
                    + "    "
                    + (handelOk ? "✅ Handel OK" : "❌ Handel: " + handelErr?.Split('\n').FirstOrDefault());
                lblTestResult.ForeColor = (libraOk && handelOk) ? successColor : dangerColor;

                btnTest.Text = "🔄 Test połączeń";
                btnTest.Enabled = true;
            };
            y += 60;

            // Sekcja: Skróty klawiszowe
            content.Controls.Add(new Label
            {
                Text = "⌨ Skróty klawiszowe",
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = primaryColor,
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 28;

            (string Key, string Action)[] shortcuts =
            {
                ("F5",       "Odśwież dane"),
                ("Ctrl+F",   "Fokus do wyszukiwarki"),
                ("Ctrl+E",   "Eksport CSV"),
                ("Ctrl+C",   "Kopiuj zaznaczone wiersze (na siatce)"),
                ("Ctrl+1..4","Przełącz zakładkę"),
                ("Enter",    "Pokaż szczegóły dnia (na siatce)"),
                ("Esc",      "Zamknij dialog"),
            };
            foreach (var (k, a) in shortcuts)
            {
                content.Controls.Add(new Label
                {
                    Text = k,
                    Font = new Font("Cascadia Mono", 9F, FontStyle.Bold),
                    ForeColor = primaryColor,
                    AutoSize = false,
                    Width = 90,
                    Location = new Point(12, y)
                });
                content.Controls.Add(new Label
                {
                    Text = a,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    AutoSize = true,
                    Location = new Point(108, y)
                });
                y += 22;
            }
            y += 8;

            // Sekcja: O programie
            content.Controls.Add(new Label
            {
                Text = "ℹ Informacje",
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = primaryColor,
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 28;
            content.Controls.Add(new Label
            {
                Text = "System Analizy Cen — moduł obserwacji cen skupu i sprzedaży kurczaka.\nDane: LibraNet + Handel.  Imputacja Wolnorynkowej: interpolacja liniowa.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Location = new Point(8, y)
            });

            rootPanel.Controls.Add(content, 0, 0);

            // Stopka z przyciskiem Zamknij
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = backgroundColor
            };
            var btnClose = new Button
            {
                Text = "Zamknij",
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = primaryColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnClose.FlatAppearance.BorderSize = 0;
            footer.Controls.Add(btnClose);
            rootPanel.Controls.Add(footer, 0, 1);

            form.AcceptButton = btnClose;
            form.KeyPreview = true;
            form.KeyDown += (s, ev) => { if (ev.KeyCode == Keys.Escape) form.Close(); };

            form.Controls.Add(rootPanel);
            form.ShowDialog();
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
                    recordsLabel.Text = $"📊 Rekordów: {filteredData.Rows.Count}";
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

            // Dispose cached fontów
            _gridFontBold?.Dispose();
            _gridFontItalicBold?.Dispose();

            // Dispose timera debounce
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();

            // Dispose KPI tooltipa
            _kpiTooltipProvider?.Dispose();
            _sezonowoscTooltip?.Dispose();

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
