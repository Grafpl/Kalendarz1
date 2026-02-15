// ═══════════════════════════════════════════════════════════════════════
// KursEditorForm.cs — GŁÓWNA FORMA: Edytor Kursu Transportowego
// ═══════════════════════════════════════════════════════════════════════
//
// WARIANT A: Classic Improved
// ┌─────────────────────────────────────────────────────────────────────┐
// │                        TITLE BAR (system)                          │
// ├────────────────────────────┬────────────────────────────────────────┤
// │                            │                                        │
// │   CIEMNY PANEL (52%)       │   JASNY PANEL (48%)                    │
// │                            │                                        │
// │ ┌────────────────────────┐ │ ┌────────────────────────────────────┐ │
// │ │ HEADER KURSU           │ │ │ NAGŁÓWEK: "ZAMÓWIENIA" (zielony)  │ │
// │ │ • Kierowca (combo,     │ │ ├────────────────────────────────────┤ │
// │ │   zielony bg)          │ │ │ Filtry: Ubój|Odbiór  Szukaj  Data │ │
// │ │ • Pojazd (combo)       │ │ ├────────────────────────────────────┤ │
// │ │ • Data + Godziny       │ │ │ ► 16.02 poniedziałek (grupa)      │ │
// │ │ • Trasa (route pills)  │ │ │   O&M        11:00  14.8  533    │ │
// │ │ • Capacity Bar         │ │ │   Trzepałka  13:00  25.0  1000   │ │
// │ │ • Konflikty panel      │ │ │   Damak ●    14:00  33.0  1320   │ │
// │ │ • Kto utworzył          │ │ │   ...                             │ │
// │ ├────────────────────────┤ │ │ ► 17.02 wtorek (grupa)            │ │
// │ │ ŁADUNKI W KURSIE       │ │ │   EUREKA ●   05:00   2.2    80   │ │
// │ │ • Nagłówek + ▲▼ Sortuj │ │ │   ...                             │ │
// │ │ • Tabela ładunków      │ │ ├────────────────────────────────────┤ │
// │ │ • Podsumowanie Σ       │ │ │ [⬇ Dodaj zaznaczone do kursu (2)]│ │
// │ ├────────────────────────┤ │ └────────────────────────────────────┘ │
// │ │ [ANULUJ] [✓ ZAPISZ]   │ │                                        │
// │ └────────────────────────┘ │                                        │
// └────────────────────────────┴────────────────────────────────────────┘
//
// INTERAKCJE:
// - Double-click na zamówienie → dodaje do kursu
// - Zaznacz + klik "Dodaj zaznaczone" → dodaje wiele naraz
// - ▲▼ → zmienia kolejność ładunków w kursie
// - Po każdej zmianie → ConflictDetectionService odpalany automatycznie
//
// LAYOUT: TableLayoutPanel z 1 wierszem, 2 kolumnami (52% / 48%)
// Ciemny panel: TableLayoutPanel wewnętrzny z wierszami Auto/Fill/Auto
// Jasny panel: Panel z DataGridView + nagłówek + footer
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZpspTransport.Controls;
using ZpspTransport.Models;
using ZpspTransport.Services;
using ZpspTransport.Theme;

namespace ZpspTransport
{
    public partial class KursEditorForm : Form
    {
        // ─── DANE ───
        private TransportCourse _course = new();
        private List<Order> _allOrders = new();
        private List<TransportCourse> _allCourses = new();
        private List<Driver> _drivers = new();
        private List<Vehicle> _vehicles = new();

        // ─── SERWISY ───
        private readonly ConflictDetectionService _conflictService = new();

        // ─── KONTROLKI (deklaracje — tworzenie w InitializeLayout) ───
        
        // Header kursu
        private ComboBox cmbKierowca = null!;
        private ComboBox cmbPojazd = null!;
        private DateTimePicker dtpData = null!;
        private DateTimePicker dtpGodzinaStart = null!;
        private DateTimePicker dtpGodzinaEnd = null!;
        private RoutePillsControl routePills = null!;
        private CapacityBarControl capacityBar = null!;
        private ConflictPanelControl conflictPanel = null!;
        private Label lblCreatedBy = null!;

        // Ładunki w kursie
        private DataGridView dgvStops = null!;
        private Label lblStopsSummary = null!;
        private Button btnMoveUp = null!;
        private Button btnMoveDown = null!;
        private Button btnSortStops = null!;

        // Zamówienia
        private DataGridView dgvOrders = null!;
        private Label lblOrderCount = null!;
        private Button btnAddSelected = null!;
        private TextBox txtSearch = null!;

        // Akcje
        private Button btnSave = null!;
        private Button btnCancel = null!;

        // ═══════════════════════════════════════════════
        // KONSTRUKTOR
        // ═══════════════════════════════════════════════
        public KursEditorForm()
        {
            InitializeComponent(); // Jeśli masz designer — jeśli nie, zostaw puste
            InitializeLayout();
            LoadSampleData();
            BindData();
            RefreshConflicts();
        }

        // ═══════════════════════════════════════════════
        // LAYOUT — Tworzenie kontrolek i rozmieszczanie
        // ═══════════════════════════════════════════════
        private void InitializeLayout()
        {
            // ── FORMA ──
            Text = "Edycja kursu transportowego";
            Size = new Size(1500, 900);
            MinimumSize = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(228, 230, 237); // szary jak w oryginale

            // ═══════════════════════════════════════════
            // GŁÓWNY LAYOUT: 2 kolumny (52% | 48%)
            // ═══════════════════════════════════════════
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(228, 230, 237),
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(mainLayout);

            // ═══════════════════════════════════════════
            // LEWY PANEL (ciemny) — kurs + ładunki
            // ═══════════════════════════════════════════
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZpspColors.PanelDark,
                Margin = new Padding(0, 0, 3, 0),
            };

            // Wewnętrzny layout lewego panelu:
            // Wiersz 0: Header kursu (Auto)
            // Wiersz 1: Ładunki w kursie (Fill — reszta miejsca)
            // Wiersz 2: Przyciski Zapisz/Anuluj (Auto)
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = ZpspColors.PanelDark,
                Padding = new Padding(0),
            };
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // Header
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Ładunki
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // Buttons

            // ── HEADER KURSU ──
            var headerPanel = CreateCourseHeader();
            leftLayout.Controls.Add(headerPanel, 0, 0);

            // ── ŁADUNKI W KURSIE ──
            var stopsPanel = CreateStopsPanel();
            leftLayout.Controls.Add(stopsPanel, 0, 1);

            // ── PRZYCISKI ──
            var buttonsPanel = CreateButtonsPanel();
            leftLayout.Controls.Add(buttonsPanel, 0, 2);

            leftPanel.Controls.Add(leftLayout);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            // ═══════════════════════════════════════════
            // PRAWY PANEL (jasny) — zamówienia
            // ═══════════════════════════════════════════
            var rightPanel = CreateOrdersPanel();
            mainLayout.Controls.Add(rightPanel, 1, 0);
        }

        // ═══════════════════════════════════════════════
        // HEADER KURSU (lewy panel góra)
        // ═══════════════════════════════════════════════
        private Panel CreateCourseHeader()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(14, 12, 14, 8),
                BackColor = ZpspColors.PanelDark,
            };

            int y = 12; // bieżąca pozycja Y

            // ── WIERSZ 1: Kierowca + Pojazd ──
            var lblKierowca = CreateDarkLabel("KIEROWCA", 14, y);
            panel.Controls.Add(lblKierowca);
            y += 16;

            cmbKierowca = new ComboBox
            {
                Location = new Point(14, y),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ZpspFonts.FieldValue,
                BackColor = ZpspColors.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            cmbKierowca.SelectedIndexChanged += (s, e) => OnCourseChanged();
            panel.Controls.Add(cmbKierowca);

            // Przycisk + obok kierowcy
            var btnAddDriver = CreateIconButton("+", ZpspColors.Blue, new Point(220, y), new Size(28, 28));
            panel.Controls.Add(btnAddDriver);

            var lblPojazd = CreateDarkLabel("POJAZD", 260, y - 16);
            panel.Controls.Add(lblPojazd);

            cmbPojazd = new ComboBox
            {
                Location = new Point(260, y),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ZpspFonts.FieldValue,
                BackColor = ZpspColors.PanelDarkAlt,
                ForeColor = ZpspColors.TextLight,
                FlatStyle = FlatStyle.Flat,
            };
            cmbPojazd.SelectedIndexChanged += (s, e) => OnCourseChanged();
            panel.Controls.Add(cmbPojazd);

            var btnAddVehicle = CreateIconButton("+", ZpspColors.Green, new Point(466, y), new Size(28, 28));
            panel.Controls.Add(btnAddVehicle);

            y += 38;

            // ── WIERSZ 2: Data + Godziny ──
            var lblData = CreateDarkLabel("DATA", 14, y);
            panel.Controls.Add(lblData);
            y += 16;

            dtpData = new DateTimePicker
            {
                Location = new Point(14, y),
                Size = new Size(120, 28),
                Font = ZpspFonts.FieldValue,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy",
                CalendarMonthBackground = ZpspColors.PanelDarkAlt,
            };
            dtpData.ValueChanged += (s, e) => OnCourseChanged();
            panel.Controls.Add(dtpData);

            var lblGodziny = CreateDarkLabel("GODZINY", 150, y - 16);
            panel.Controls.Add(lblGodziny);

            // Godzina start — zielone tło
            dtpGodzinaStart = new DateTimePicker
            {
                Location = new Point(150, y),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
            };
            dtpGodzinaStart.ValueChanged += (s, e) => OnCourseChanged();
            panel.Controls.Add(dtpGodzinaStart);

            var lblArrow = new Label
            {
                Text = "→",
                Font = new Font("Segoe UI", 12f),
                ForeColor = ZpspColors.TextMuted,
                Location = new Point(236, y + 4),
                AutoSize = true,
            };
            panel.Controls.Add(lblArrow);

            // Godzina koniec — fioletowe tło
            dtpGodzinaEnd = new DateTimePicker
            {
                Location = new Point(258, y),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
            };
            dtpGodzinaEnd.ValueChanged += (s, e) => OnCourseChanged();
            panel.Controls.Add(dtpGodzinaEnd);

            y += 38;

            // ── WIERSZ 3: Trasa (Route Pills) ──
            var lblTrasa = CreateDarkLabel("TRASA (AUTO)", 14, y);
            panel.Controls.Add(lblTrasa);
            y += 16;

            routePills = new RoutePillsControl
            {
                Location = new Point(14, y),
                Size = new Size(480, 34),
            };
            panel.Controls.Add(routePills);
            y += 40;

            // ── WIERSZ 4: Capacity Bar ──
            var capPanel = new Panel
            {
                Location = new Point(14, y),
                Size = new Size(480, 50),
                BackColor = ZpspColors.PanelDarkAlt,
            };
            // Label nad capacity barem
            var lblCap = CreateDarkLabel("ŁADOWNOŚĆ NACZEPY", 10, 4);
            capPanel.Controls.Add(lblCap);

            capacityBar = new CapacityBarControl
            {
                Location = new Point(4, 16),
                Size = new Size(472, 32),
                BarHeight = 12,
            };
            capPanel.Controls.Add(capacityBar);
            panel.Controls.Add(capPanel);
            y += 58;

            // ── WIERSZ 5: Panel konfliktów ──
            conflictPanel = new ConflictPanelControl
            {
                Location = new Point(14, y),
                Size = new Size(480, 120),
            };
            panel.Controls.Add(conflictPanel);
            y += 126;

            // ── WIERSZ 6: Kto utworzył ──
            lblCreatedBy = new Label
            {
                Text = "Utworzył: Administrator (14.02 08:48) • Handlowcy: Maja",
                Font = ZpspFonts.Timestamp,
                ForeColor = ZpspColors.TextMuted,
                Location = new Point(14, y),
                AutoSize = true,
            };
            panel.Controls.Add(lblCreatedBy);

            panel.Height = y + 24;
            return panel;
        }

        // ═══════════════════════════════════════════════
        // ŁADUNKI W KURSIE (lewy panel środek)
        // ═══════════════════════════════════════════════
        private Panel CreateStopsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZpspColors.PanelDark,
            };

            // ── NAGŁÓWEK ──
            var headerBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = ZpspColors.PanelDark,
                Padding = new Padding(14, 0, 14, 0),
            };

            var lblTitle = new Label
            {
                Text = "🚚 ŁADUNKI W KURSIE",
                Font = ZpspFonts.SectionTitle,
                ForeColor = ZpspColors.TextWhite,
                Location = new Point(14, 8),
                AutoSize = true,
            };
            headerBar.Controls.Add(lblTitle);

            // Przyciski kolejności
            var lblKolejnosc = new Label
            {
                Text = "KOLEJNOŚĆ:",
                Font = ZpspFonts.FieldLabel,
                ForeColor = ZpspColors.TextMuted,
                Location = new Point(240, 12),
                AutoSize = true,
            };
            headerBar.Controls.Add(lblKolejnosc);

            btnMoveUp = CreateIconButton("▲", ZpspColors.Blue, new Point(310, 6), new Size(26, 24));
            btnMoveUp.Click += (s, e) => MoveStopUp();
            headerBar.Controls.Add(btnMoveUp);

            btnMoveDown = CreateIconButton("▼", ZpspColors.Blue, new Point(340, 6), new Size(26, 24));
            btnMoveDown.Click += (s, e) => MoveStopDown();
            headerBar.Controls.Add(btnMoveDown);

            btnSortStops = CreateIconButton("Sortuj", ZpspColors.Purple, new Point(374, 6), new Size(60, 24));
            btnSortStops.Click += (s, e) => SortStops();
            headerBar.Controls.Add(btnSortStops);

            panel.Controls.Add(headerBar);

            // ── TABELA ŁADUNKÓW (DataGridView) ──
            dgvStops = CreateDarkDataGridView();
            dgvStops.Dock = DockStyle.Fill;
            dgvStops.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Lp", HeaderText = "Lp.", Width = 40,
                    DefaultCellStyle = new() { Alignment = DataGridViewContentAlignment.MiddleCenter,
                        Font = ZpspFonts.StopNumber, ForeColor = ZpspColors.Green } },
                new DataGridViewTextBoxColumn { Name = "Klient", HeaderText = "Klient", Width = 160,
                    DefaultCellStyle = new() { Font = ZpspFonts.TableCellBold, ForeColor = ZpspColors.TextWhite } },
                new DataGridViewTextBoxColumn { Name = "DataUboju", HeaderText = "Data uboju", Width = 90,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.TextLight } },
                new DataGridViewTextBoxColumn { Name = "Palety", HeaderText = "Palety", Width = 65,
                    DefaultCellStyle = new() { Font = ZpspFonts.TableCellNumber, ForeColor = ZpspColors.OrangeLight,
                        Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Pojemniki", HeaderText = "Poj.", Width = 65,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.Green,
                        Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Adres", HeaderText = "Adres", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.TextMuted } },
                new DataGridViewTextBoxColumn { Name = "Uwagi", HeaderText = "Uwagi", Width = 180,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.TextLight } },
            });
            // Keydown do Delete
            dgvStops.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) RemoveSelectedStop();
            };
            panel.Controls.Add(dgvStops);

            // ── PODSUMOWANIE ──
            var summaryBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = ZpspColors.PanelDark,
                Padding = new Padding(14, 0, 14, 0),
            };
            lblStopsSummary = new Label
            {
                Text = "Σ Palety: 0  •  Σ Pojemniki: 0  •  Σ Waga: 0 kg",
                Font = ZpspFonts.Summary,
                ForeColor = ZpspColors.TextMuted,
                Location = new Point(14, 6),
                AutoSize = true,
            };
            summaryBar.Controls.Add(lblStopsSummary);
            panel.Controls.Add(summaryBar);

            // Ważna kolejność: headerBar na Top, summaryBar na Bottom, dgvStops Fill
            // W WinForms kolejność Dock ma znaczenie — dodawaj Fill na końcu
            panel.Controls.SetChildIndex(headerBar, 0);
            panel.Controls.SetChildIndex(summaryBar, 1);
            panel.Controls.SetChildIndex(dgvStops, 2);

            return panel;
        }

        // ═══════════════════════════════════════════════
        // ZAMÓWIENIA (prawy panel — jasny)
        // ═══════════════════════════════════════════════
        private Panel CreateOrdersPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZpspColors.PanelLight,
                Margin = new Padding(3, 0, 0, 0),
            };

            // ── NAGŁÓWEK ZIELONY ──
            var headerBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = ZpspColors.Green,
                Padding = new Padding(10, 0, 10, 0),
            };

            var lblTitle = new Label
            {
                Text = "📋 ZAMÓWIENIA",
                Font = ZpspFonts.SectionTitle,
                ForeColor = Color.White,
                Location = new Point(10, 10),
                AutoSize = true,
            };
            headerBar.Controls.Add(lblTitle);

            lblOrderCount = new Label
            {
                Text = "14 zam.",
                Font = ZpspFonts.Pill,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 255, 255, 255), // semi-transparent
                Location = new Point(160, 12),
                AutoSize = true,
                Padding = new Padding(4, 1, 4, 1),
            };
            headerBar.Controls.Add(lblOrderCount);

            // Szukaj
            txtSearch = new TextBox
            {
                PlaceholderText = "🔍 Szukaj klienta...",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(headerBar.Width - 200, 8),
                Size = new Size(150, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            txtSearch.TextChanged += (s, e) => FilterOrders();
            headerBar.Controls.Add(txtSearch);

            panel.Controls.Add(headerBar);

            // ── TABELA ZAMÓWIEŃ (DataGridView — jasny motyw) ──
            dgvOrders = CreateLightDataGridView();
            dgvOrders.Dock = DockStyle.Fill;
            dgvOrders.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "", Width = 28 },
                new DataGridViewTextBoxColumn { Name = "Uboj", HeaderText = "Ubój", Width = 80,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.TextGray } },
                new DataGridViewTextBoxColumn { Name = "Odbior", HeaderText = "Odbiór", Width = 80,
                    DefaultCellStyle = new() { Font = ZpspFonts.TableCell, ForeColor = ZpspColors.TextMedium } },
                new DataGridViewTextBoxColumn { Name = "Godzina", HeaderText = "Godz.", Width = 60,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.Purple,
                        BackColor = ZpspColors.PurpleBg } },
                new DataGridViewTextBoxColumn { Name = "Palety", HeaderText = "Palety", Width = 55,
                    DefaultCellStyle = new() { Font = ZpspFonts.TableCellNumber, ForeColor = ZpspColors.Orange,
                        Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Pojemniki", HeaderText = "Poj.", Width = 55,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.TextMedium,
                        Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Klient", HeaderText = "Klient", Width = 140,
                    DefaultCellStyle = new() { Font = ZpspFonts.TableCellBold, ForeColor = ZpspColors.TextDark } },
                new DataGridViewTextBoxColumn { Name = "Adres", HeaderText = "Adres", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    DefaultCellStyle = new() { ForeColor = ZpspColors.TextGray } },
            });

            // Double-click → dodaj do kursu
            dgvOrders.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) AddOrderToCourse(e.RowIndex);
            };

            // Selekcja → fioletowe tło
            dgvOrders.SelectionChanged += (s, e) =>
            {
                foreach (DataGridViewRow row in dgvOrders.Rows)
                {
                    row.DefaultCellStyle.BackColor = row.Selected
                        ? ZpspColors.PurpleRow
                        : (row.Index % 2 == 0 ? Color.White : ZpspColors.PanelLightAlt);
                }
            };

            panel.Controls.Add(dgvOrders);

            // ── FOOTER: Dodaj zaznaczone ──
            var footerBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = ZpspColors.GreenBg,
                Padding = new Padding(10),
            };

            btnAddSelected = new Button
            {
                Text = "⬇ Dodaj zaznaczone do kursu",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ZpspColors.Green,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand,
            };
            btnAddSelected.FlatAppearance.BorderSize = 0;
            btnAddSelected.Click += (s, e) => AddSelectedOrdersToCourse();
            footerBar.Controls.Add(btnAddSelected);
            panel.Controls.Add(footerBar);

            // Kolejność Dock
            panel.Controls.SetChildIndex(headerBar, 0);
            panel.Controls.SetChildIndex(footerBar, 1);
            panel.Controls.SetChildIndex(dgvOrders, 2);

            return panel;
        }

        // ═══════════════════════════════════════════════
        // PRZYCISKI (lewy panel dół)
        // ═══════════════════════════════════════════════
        private Panel CreateButtonsPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = ZpspColors.PanelDark,
            };

            btnSave = new Button
            {
                Text = "✓ ZAPISZ KURS",
                Font = ZpspFonts.ButtonLarge,
                ForeColor = Color.White,
                BackColor = ZpspColors.Green,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 38),
                Cursor = Cursors.Hand,
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveCourse();
            panel.Controls.Add(btnSave);

            btnCancel = new Button
            {
                Text = "ANULUJ",
                Font = ZpspFonts.ButtonSmall,
                ForeColor = ZpspColors.TextMuted,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 38),
                Cursor = Cursors.Hand,
            };
            btnCancel.FlatAppearance.BorderColor = ZpspColors.PanelDarkBorder;
            btnCancel.Click += (s, e) => Close();
            panel.Controls.Add(btnCancel);

            return panel;
        }

        // ═══════════════════════════════════════════════
        // TWORZENIE DataGridView — ciemny motyw
        // ═══════════════════════════════════════════════
        private DataGridView CreateDarkDataGridView()
        {
            var dgv = new DataGridView
            {
                BackgroundColor = ZpspColors.PanelDark,
                GridColor = ZpspColors.PanelDarkBorder,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                EnableHeadersVisualStyles = false,
                AutoGenerateColumns = false,
                RowTemplate = { Height = 36 },
                DefaultCellStyle =
                {
                    BackColor = ZpspColors.PanelDark,
                    ForeColor = ZpspColors.TextLight,
                    SelectionBackColor = Color.FromArgb(123, 31, 162, 60), // fiolet semi-transparent
                    SelectionForeColor = ZpspColors.TextWhite,
                    Font = ZpspFonts.TableCell,
                    Padding = new Padding(4, 0, 4, 0),
                },
                AlternatingRowsDefaultCellStyle =
                {
                    BackColor = ZpspColors.PanelDarkAlt,
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = ZpspColors.PanelDarkBorder,
                    ForeColor = ZpspColors.TextMuted,
                    Font = ZpspFonts.TableHeader,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 4, 0),
                },
                ColumnHeadersHeight = 30,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            };
            return dgv;
        }

        // ═══════════════════════════════════════════════
        // TWORZENIE DataGridView — jasny motyw
        // ═══════════════════════════════════════════════
        private DataGridView CreateLightDataGridView()
        {
            var dgv = new DataGridView
            {
                BackgroundColor = Color.White,
                GridColor = ZpspColors.PanelLightBorder,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true, // Multi-select zamówień!
                ReadOnly = true,
                EnableHeadersVisualStyles = false,
                AutoGenerateColumns = false,
                RowTemplate = { Height = 32 },
                DefaultCellStyle =
                {
                    BackColor = Color.White,
                    ForeColor = ZpspColors.TextMedium,
                    SelectionBackColor = ZpspColors.PurpleRow,
                    SelectionForeColor = ZpspColors.TextDark,
                    Font = ZpspFonts.TableCell,
                    Padding = new Padding(2, 0, 2, 0),
                },
                AlternatingRowsDefaultCellStyle =
                {
                    BackColor = ZpspColors.PanelLightAlt,
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = ZpspColors.PanelLightAlt,
                    ForeColor = ZpspColors.TextGray,
                    Font = ZpspFonts.TableHeader,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                },
                ColumnHeadersHeight = 28,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            };
            return dgv;
        }

        // ═══════════════════════════════════════════════
        // HELPERY UI
        // ═══════════════════════════════════════════════
        
        private Label CreateDarkLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = ZpspFonts.FieldLabel,
                ForeColor = ZpspColors.TextMuted,
                Location = new Point(x, y),
                AutoSize = true,
            };
        }

        private Button CreateIconButton(string text, Color bgColor, Point location, Size size)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = bgColor,
                FlatStyle = FlatStyle.Flat,
                Location = location,
                Size = size,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ═══════════════════════════════════════════════
        // DANE TESTOWE
        // ═══════════════════════════════════════════════
        private void LoadSampleData()
        {
            _drivers = new List<Driver>
            {
                new() { Id = 1, Imie = "Radosław", Nazwisko = "Czapla" },
                new() { Id = 2, Imie = "Marek", Nazwisko = "Kowalski" },
                new() { Id = 3, Imie = "Tomasz", Nazwisko = "Nowak" },
            };

            _vehicles = new List<Vehicle>
            {
                new() { Id = 1, Rejestracja = "EBR 08HY", MaxPalet = 4, MaxPojemnikow = 160, DMC_Kg = 18000 },
                new() { Id = 2, Rejestracja = "WZ 12345", MaxPalet = 6, MaxPojemnikow = 240, DMC_Kg = 18000 },
                new() { Id = 3, Rejestracja = "EL 98765", MaxPalet = 8, MaxPojemnikow = 320, DMC_Kg = 24000 },
            };

            _allOrders = new List<Order>
            {
                new() { Id=1, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(11,0,0), Palety=14.8m, Pojemniki=533, WagaKg=2840, NazwaKlienta="O&M", Adres="87-800 Juliusza Słowackiego", Priority=OrderPriority.Normal, Handlowiec="Maja" },
                new() { Id=2, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(13,0,0), Palety=25.0m, Pojemniki=1000, WagaKg=4800, NazwaKlienta="Trzepałka Mariusz", Adres="05-480 ks. kard. Stefana Wyszyńskiego", Priority=OrderPriority.Normal, Handlowiec="Maja" },
                new() { Id=3, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(14,0,0), Palety=33.0m, Pojemniki=1320, WagaKg=6200, NazwaKlienta="Damak", Adres="05-555 Aleja Krakowska", Priority=OrderPriority.High, Handlowiec="Kasia" },
                new() { Id=4, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(14,0,0), Palety=6.7m, Pojemniki=240, WagaKg=1280, NazwaKlienta="Destan", Adres="05-850 Ceramiczna", Priority=OrderPriority.Normal, Handlowiec="Maja" },
                new() { Id=5, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(16,0,0), Palety=14.5m, Pojemniki=520, WagaKg=2760, NazwaKlienta="ŁYSE", Adres="07-437 Kościelna", Priority=OrderPriority.Normal, Handlowiec="Anna" },
                new() { Id=6, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(21,0,0), Palety=8.3m, Pojemniki=300, WagaKg=1590, NazwaKlienta="BOMAFAR", Adres="66-133 Dąbrówka", Priority=OrderPriority.Low, Handlowiec="Kasia" },
                new() { Id=7, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(21,0,0), Palety=16.7m, Pojemniki=600, WagaKg=3200, NazwaKlienta="BATISTA SPÓŁKA Z O...", Adres="39-123 Czarna Sędziszowska", Priority=OrderPriority.Normal, Handlowiec="Maja" },
                new() { Id=8, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,16), GodzinaOdbioru=new(21,0,0), Palety=9.4m, Pojemniki=340, WagaKg=1800, NazwaKlienta="SMOLIŃSKI", Adres="62-730 Długa Wieś", Priority=OrderPriority.Normal, Handlowiec="Anna" },
                new() { Id=9, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,17), GodzinaOdbioru=new(5,0,0), Palety=2.2m, Pojemniki=80, WagaKg=420, NazwaKlienta="EUREKA S.C. HURTO...", Adres="05-092 Ogrodowa", Priority=OrderPriority.High, Handlowiec="Maja" },
                new() { Id=10, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,17), GodzinaOdbioru=new(6,0,0), Palety=8.3m, Pojemniki=300, WagaKg=1590, NazwaKlienta="Kaptan Food Sp. Zoo", Adres="05-830 Aleja Katowicka", Priority=OrderPriority.Normal, Handlowiec="Kasia" },
                new() { Id=11, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,17), GodzinaOdbioru=new(8,0,0), Palety=16.7m, Pojemniki=600, WagaKg=3200, NazwaKlienta="Ladros", Adres="20-258 Łuszczów Drugi", Priority=OrderPriority.Express, Handlowiec="Maja" },
                new() { Id=12, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,17), GodzinaOdbioru=new(8,0,0), Palety=33.0m, Pojemniki=1320, WagaKg=6200, NazwaKlienta="RADDROB Chlebowski", Adres="64-212", Priority=OrderPriority.High, Handlowiec="Anna" },
                new() { Id=13, DataUboju=new(2026,2,16), DataOdbioru=new(2026,2,17), GodzinaOdbioru=new(8,0,0), Palety=6.3m, Pojemniki=229, WagaKg=1200, NazwaKlienta="TWÓJ MARKET SPÓŁ...", Adres="62-530 Skiby", Priority=OrderPriority.Normal, Handlowiec="Maja" },
            };

            // Kurs z przykładowymi ładunkami
            _course = new TransportCourse
            {
                Id = 1,
                Kierowca = _drivers[0],
                Pojazd = _vehicles[0],
                DataWyjazdu = new DateTime(2026, 2, 14),
                GodzinaWyjazdu = new TimeSpan(6, 0, 0),
                GodzinaPowrotu = new TimeSpan(18, 0, 0),
                CreatedBy = "Administrator",
                CreatedAt = new DateTime(2026, 2, 14, 8, 48, 0),
                Handlowcy = new() { "Maja" },
                Stops = new()
                {
                    new CourseStop { Lp=1, OrderId=100, NazwaKlienta="LOCIV IMPEX DIA SR...", DataUboju=new(2026,2,14), Palety=2.0m, Pojemniki=72, WagaKg=384, Adres="MUN.PITESTI 00000 STR.DEPOZ...", Uwagi="LOCIV IMPEX DIA SRL Rumunia (08:00)", Status=StopStatus.Loaded, PlannedArrival=new(8,0,0) },
                    new CourseStop { Lp=2, OrderId=101, NazwaKlienta="PODOLSKI", DataUboju=new(2026,2,16), Palety=19.4m, Pojemniki=700, WagaKg=3720, Adres="64-410 Lutomek", Uwagi="PODOLSKI (18:00)", Status=StopStatus.Pending, PlannedArrival=new(18,0,0) },
                },
            };
        }

        // ═══════════════════════════════════════════════
        // BIND DATA — Wypełnij kontrolki danymi
        // ═══════════════════════════════════════════════
        private void BindData()
        {
            // Combobox kierowców
            cmbKierowca.Items.Clear();
            foreach (var d in _drivers) cmbKierowca.Items.Add(d.PelneImie);
            if (_course.Kierowca != null)
                cmbKierowca.SelectedIndex = _drivers.FindIndex(d => d.Id == _course.Kierowca.Id);

            // Combobox pojazdów
            cmbPojazd.Items.Clear();
            foreach (var v in _vehicles) cmbPojazd.Items.Add(v.DisplayName);
            if (_course.Pojazd != null)
                cmbPojazd.SelectedIndex = _vehicles.FindIndex(v => v.Id == _course.Pojazd.Id);

            // Data i godziny
            dtpData.Value = _course.DataWyjazdu;
            dtpGodzinaStart.Value = DateTime.Today.Add(_course.GodzinaWyjazdu);
            dtpGodzinaEnd.Value = DateTime.Today.Add(_course.GodzinaPowrotu);

            RefreshStopsGrid();
            RefreshOrdersGrid();
            RefreshUI();
        }

        // ═══════════════════════════════════════════════
        // ODŚWIEŻANIE UI
        // ═══════════════════════════════════════════════
        
        private void RefreshUI()
        {
            // Route pills
            routePills.SetRoute(_course.Stops.OrderBy(s => s.Lp).Select(s => s.NazwaKlienta).ToArray());

            // Capacity bar
            decimal maxPal = _course.Pojazd?.MaxPalet ?? 4;
            capacityBar.SetCapacity(_course.SumaPalet, maxPal);

            // Summary
            lblStopsSummary.Text = $"Σ Palety: {_course.SumaPalet:F1}  •  " +
                $"Σ Pojemniki: {_course.SumaPojemnikow}  •  " +
                $"Σ Waga: {_course.SumaWagaKg:F0} kg";

            // Order count
            int unassigned = _allOrders.Count(o => !o.IsAssigned);
            lblOrderCount.Text = $"{_allOrders.Count} zam. ({unassigned} wolnych)";

            // Add selected button
            int selected = dgvOrders.SelectedRows.Count;
            btnAddSelected.Text = selected > 0
                ? $"⬇ Dodaj zaznaczone do kursu ({selected})"
                : "⬇ Dodaj zaznaczone do kursu";

            // Created by
            lblCreatedBy.Text = $"Utworzył: {_course.CreatedBy} ({_course.CreatedAt:dd.MM HH:mm}) • " +
                $"Handlowcy: {string.Join(", ", _course.Handlowcy)}";
        }

        private void RefreshStopsGrid()
        {
            dgvStops.Rows.Clear();
            foreach (var stop in _course.Stops.OrderBy(s => s.Lp))
            {
                dgvStops.Rows.Add(
                    stop.Lp,
                    stop.NazwaKlienta,
                    stop.DataUboju.ToString("yyyy-MM-dd"),
                    stop.Palety.ToString("F1"),
                    stop.Pojemniki,
                    stop.Adres,
                    stop.Uwagi
                );
            }
        }

        private void RefreshOrdersGrid()
        {
            dgvOrders.Rows.Clear();
            
            // Grupuj po dacie odbioru
            var groups = _allOrders
                .Where(o => !o.IsAssigned)
                .GroupBy(o => o.DataOdbioru.Date)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                // TODO: W pełnej implementacji — dodaj wiersz "grupa" z zielonym tłem
                // (DataGridView nie wspiera natywnie grup, trzeba custom paint lub
                //  dodatkowy wiersz z merged cells)
                
                foreach (var order in group.OrderBy(o => o.GodzinaOdbioru))
                {
                    int idx = dgvOrders.Rows.Add(
                        order.Priority switch {
                            OrderPriority.High => "●",
                            OrderPriority.Express => "◆",
                            OrderPriority.Low => "○",
                            _ => "•"
                        },
                        order.DataUbojuFormatted,
                        order.DataOdbioruFormatted,
                        order.GodzinaFormatted,
                        order.Palety.ToString("F1"),
                        order.Pojemniki,
                        order.NazwaKlienta,
                        order.Adres
                    );

                    // Kolor kropki priorytetu
                    var row = dgvOrders.Rows[idx];
                    row.Tag = order; // przechowaj referencję do Order
                    
                    Color priColor = order.Priority switch
                    {
                        OrderPriority.High => ZpspColors.Red,
                        OrderPriority.Express => ZpspColors.Purple,
                        OrderPriority.Low => ZpspColors.TextFaint,
                        _ => ZpspColors.Green
                    };
                    row.Cells["Priority"].Style.ForeColor = priColor;
                    row.Cells["Priority"].Style.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
                }
            }
        }

        // ═══════════════════════════════════════════════
        // AKCJE
        // ═══════════════════════════════════════════════

        /// <summary>Dodaje zamówienie z wiersza do kursu</summary>
        private void AddOrderToCourse(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvOrders.Rows.Count) return;
            var order = dgvOrders.Rows[rowIndex].Tag as Order;
            if (order == null || order.IsAssigned) return;

            int newLp = _course.Stops.Count + 1;
            _course.Stops.Add(new CourseStop
            {
                Lp = newLp,
                OrderId = order.Id,
                NazwaKlienta = order.NazwaKlienta,
                DataUboju = order.DataUboju,
                Palety = order.Palety,
                Pojemniki = order.Pojemniki,
                WagaKg = order.WagaKg,
                Adres = order.Adres,
                Uwagi = $"{order.NazwaKlienta} ({order.GodzinaFormatted})",
                PlannedArrival = order.GodzinaOdbioru,
            });

            order.IsAssigned = true;
            order.AssignedCourseId = _course.Id;

            RefreshStopsGrid();
            RefreshOrdersGrid();
            RefreshUI();
            RefreshConflicts();
        }

        /// <summary>Dodaje wszystkie zaznaczone zamówienia</summary>
        private void AddSelectedOrdersToCourse()
        {
            var selectedRows = dgvOrders.SelectedRows.Cast<DataGridViewRow>().ToList();
            foreach (var row in selectedRows)
            {
                AddOrderToCourse(row.Index);
            }
        }

        /// <summary>Usuwa zaznaczony ładunek z kursu</summary>
        private void RemoveSelectedStop()
        {
            if (dgvStops.CurrentRow == null) return;
            int idx = dgvStops.CurrentRow.Index;
            if (idx >= 0 && idx < _course.Stops.Count)
            {
                var stop = _course.Stops.OrderBy(s => s.Lp).ElementAt(idx);
                _course.Stops.Remove(stop);

                // Oznacz zamówienie jako wolne
                var order = _allOrders.FirstOrDefault(o => o.Id == stop.OrderId);
                if (order != null)
                {
                    order.IsAssigned = false;
                    order.AssignedCourseId = 0;
                }

                // Przenumeruj Lp
                int lp = 1;
                foreach (var s in _course.Stops.OrderBy(s => s.Lp))
                    s.Lp = lp++;

                RefreshStopsGrid();
                RefreshOrdersGrid();
                RefreshUI();
                RefreshConflicts();
            }
        }

        /// <summary>Przesuwa wybrany ładunek w górę</summary>
        private void MoveStopUp()
        {
            if (dgvStops.CurrentRow == null) return;
            int idx = dgvStops.CurrentRow.Index;
            if (idx <= 0) return;

            var ordered = _course.Stops.OrderBy(s => s.Lp).ToList();
            (ordered[idx].Lp, ordered[idx - 1].Lp) = (ordered[idx - 1].Lp, ordered[idx].Lp);

            RefreshStopsGrid();
            RefreshUI();
            dgvStops.CurrentCell = dgvStops.Rows[idx - 1].Cells[0];
        }

        /// <summary>Przesuwa wybrany ładunek w dół</summary>
        private void MoveStopDown()
        {
            if (dgvStops.CurrentRow == null) return;
            int idx = dgvStops.CurrentRow.Index;
            if (idx >= _course.Stops.Count - 1) return;

            var ordered = _course.Stops.OrderBy(s => s.Lp).ToList();
            (ordered[idx].Lp, ordered[idx + 1].Lp) = (ordered[idx + 1].Lp, ordered[idx].Lp);

            RefreshStopsGrid();
            RefreshUI();
            dgvStops.CurrentCell = dgvStops.Rows[idx + 1].Cells[0];
        }

        /// <summary>Sortuje ładunki wg godziny dostawy</summary>
        private void SortStops()
        {
            int lp = 1;
            foreach (var stop in _course.Stops.OrderBy(s => s.PlannedArrival ?? TimeSpan.MaxValue))
                stop.Lp = lp++;

            RefreshStopsGrid();
            RefreshUI();
        }

        /// <summary>Filtruje zamówienia wg tekstu szukania</summary>
        private void FilterOrders()
        {
            string search = txtSearch.Text.Trim().ToLower();
            foreach (DataGridViewRow row in dgvOrders.Rows)
            {
                var order = row.Tag as Order;
                if (order == null) continue;
                
                bool visible = string.IsNullOrEmpty(search) ||
                    order.NazwaKlienta.ToLower().Contains(search) ||
                    order.Adres.ToLower().Contains(search);
                row.Visible = visible;
            }
        }

        /// <summary>Wywoływane po KAŻDEJ zmianie w kursie — odświeża konflikty</summary>
        private void OnCourseChanged()
        {
            // Zaktualizuj kurs z kontrolek
            if (cmbKierowca.SelectedIndex >= 0)
                _course.Kierowca = _drivers[cmbKierowca.SelectedIndex];
            if (cmbPojazd.SelectedIndex >= 0)
                _course.Pojazd = _vehicles[cmbPojazd.SelectedIndex];

            _course.DataWyjazdu = dtpData.Value.Date;
            _course.GodzinaWyjazdu = dtpGodzinaStart.Value.TimeOfDay;
            _course.GodzinaPowrotu = dtpGodzinaEnd.Value.TimeOfDay;

            RefreshUI();
            RefreshConflicts();
        }

        // ═══════════════════════════════════════════════
        // WYKRYWANIE KONFLIKTÓW
        // ═══════════════════════════════════════════════
        private void RefreshConflicts()
        {
            var conflicts = _conflictService.DetectAll(_course, _allOrders, _allCourses);
            conflictPanel.SetConflicts(conflicts);

            // Zmień kolor przycisku Zapisz jeśli są błędy
            bool hasErrors = conflicts.Any(c => c.Level == ConflictLevel.Error);
            btnSave.BackColor = hasErrors ? ZpspColors.Orange : ZpspColors.Green;
            btnSave.Text = hasErrors ? "⚠ ZAPISZ KURS (z ostrzeżeniami)" : "✓ ZAPISZ KURS";
        }

        /// <summary>Zapisuje kurs (placeholder — tu wstaw logikę zapisu do DB)</summary>
        private void SaveCourse()
        {
            var conflicts = _conflictService.DetectAll(_course, _allOrders, _allCourses);
            bool hasErrors = conflicts.Any(c => c.Level == ConflictLevel.Error);

            if (hasErrors)
            {
                var result = MessageBox.Show(
                    $"Wykryto {conflicts.Count(c => c.Level == ConflictLevel.Error)} błędów:\n\n" +
                    string.Join("\n", conflicts.Where(c => c.Level == ConflictLevel.Error).Select(c => $"• {c.Message}")) +
                    "\n\nCzy na pewno zapisać?",
                    "Ostrzeżenie",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No) return;
            }

            // TODO: Zapisz do bazy danych
            MessageBox.Show("Kurs zapisany!", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
