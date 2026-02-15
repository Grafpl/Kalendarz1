using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kalendarz1.Transport.Controls;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Transport.Services;
using Kalendarz1.Transport.Theme;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport.Formularze
{
    /// <summary>
    /// Nowy edytor kursu transportowego — layout 52/48, ciemny/jasny motyw,
    /// custom kontrolki: CapacityBar, RoutePills, Timeline, ConflictPanel.
    /// </summary>
    public class KursEditorForm : Form
    {
        // ═══════════════════════════════════════════
        //  POLA
        // ═══════════════════════════════════════════
        private readonly TransportRepozytorium _repozytorium;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private long? _kursId;
        private readonly string _uzytkownik;
        private Kurs _kurs;
        private List<LadunekRow> _ladunki = new();
        private List<Kierowca> _kierowcy = new();
        private List<Pojazd> _pojazdy = new();
        private List<ZamowienieRow> _wolneZamowienia = new();
        private List<ZamowienieRow> _zamowieniaDoDodania = new();
        private List<int> _zamowieniaDoUsuniecia = new();
        private bool _dataLoaded;
        private bool _isUpdating;
        private Timer _autoUpdateTimer;
        private readonly ConflictDetectionService _conflictService = new();

        // Undo
        private Stack<Action> _undoStack = new();

        // ── Kontrolki lewego panelu ──
        private ComboBox cboKierowca, cboPojazd;
        private DateTimePicker dtpData, dtpGodzWyjazdu, dtpGodzPowrotu;
        private Button btnNowyKierowca, btnNowyPojazd;
        private RoutePillsControl routePills;
        private CapacityBarControl capacityBar;
        private TimelineControl timeline;
        private ConflictPanelControl conflictPanel;
        private DataGridView dgvLadunki;
        private Label lblLadunkiCount;
        private Button btnMoveUp, btnMoveDown, btnSortuj;
        private Label lblSumPalety, lblSumPojemniki, lblSumWaga;
        private Button btnZapisz, btnAnuluj;

        // ── Kontrolki prawego panelu ──
        private DataGridView dgvZamowienia;
        private TextBox txtSzukaj;
        private DateTimePicker dtpZamowieniaDate;
        private Label lblZamowieniaCount;
        private Button btnDodajZaznaczone;
        private RadioButton rbDataUboju, rbDataOdbioru;
        private bool UzywajDataUboju => rbDataUboju?.Checked ?? true;
        private string _filtrZamowien = "";

        // ═══════════════════════════════════════════
        //  MODELE WEWNĘTRZNE
        // ═══════════════════════════════════════════
        public class LadunekRow
        {
            public long LadunekID { get; set; }
            public long KursID { get; set; }
            public int Kolejnosc { get; set; }
            public string? KodKlienta { get; set; }
            public decimal Palety { get; set; }
            public int PojemnikiE2 { get; set; }
            public string? Uwagi { get; set; }
            public string? Adres { get; set; }
            public bool TrybE2 { get; set; }
            public byte? PlanE2NaPaleteOverride { get; set; }
            public string? NazwaKlienta { get; set; }
            public bool ZmienionyWZamowieniu { get; set; }
            public bool AnulowanyWZamowieniu { get; set; }
            public decimal PoprzedniePalety { get; set; }
            public int PoprzedniePojemniki { get; set; }
            public DateTime? DataUboju { get; set; }
            public string? Handlowiec { get; set; }
        }

        public class ZamowienieRow
        {
            public int ZamowienieId { get; set; }
            public int KlientId { get; set; }
            public string KlientNazwa { get; set; } = "";
            public decimal IloscKg { get; set; }
            public decimal Palety { get; set; }
            public int Pojemniki { get; set; }
            public bool TrybE2 { get; set; }
            public DateTime DataPrzyjazdu { get; set; }
            public DateTime DataOdbioru { get; set; }
            public DateTime? DataUboju { get; set; }
            public string GodzinaStr => DataPrzyjazdu.ToString("HH:mm");
            public string Status { get; set; } = "Nowe";
            public string TransportStatus { get; set; } = "Oczekuje";
            public string Handlowiec { get; set; } = "";
            public string Adres { get; set; } = "";
        }

        // ═══════════════════════════════════════════
        //  KONSTRUKTORY (kompatybilne z istniejącym panelem)
        // ═══════════════════════════════════════════
        public KursEditorForm(TransportRepozytorium repozytorium, DateTime data, string uzytkownik)
            : this(repozytorium, null, data, uzytkownik) { }

        public KursEditorForm(TransportRepozytorium repozytorium, Kurs kurs, string uzytkownik)
            : this(repozytorium, kurs?.KursID, kurs?.DataKursu, uzytkownik)
        {
            _kurs = kurs;
        }

        private KursEditorForm(TransportRepozytorium repozytorium, long? kursId, DateTime? data, string uzytkownik)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _kursId = kursId;
            _uzytkownik = uzytkownik ?? Environment.UserName;

            BuildUI();
            WindowIconHelper.SetIcon(this);
            dtpData.Value = data ?? DateTime.Today;
            dtpZamowieniaDate.Value = data ?? DateTime.Today;

            InitAutoUpdateTimer();
            _ = LoadDataAsync();
        }

        // ═══════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════
        private void BuildUI()
        {
            Text = _kursId.HasValue ? "\U0001f4e6 Edycja kursu transportowego" : "\U0001f4e6 Nowy kurs transportowy";
            Size = new Size(1500, 900);
            MinimumSize = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            BackColor = ZpspColors.PanelDark;
            Font = new Font("Segoe UI", 10F);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += Form_KeyDown;

            // Główny podział 52/48
            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ── Lewa kolumna (ciemna) ──
            var leftPanel = BuildLeftPanel();
            mainTable.Controls.Add(leftPanel, 0, 0);

            // ── Prawa kolumna (jasna) ──
            var rightPanel = BuildRightPanel();
            mainTable.Controls.Add(rightPanel, 1, 0);

            Controls.Add(mainTable);
        }

        // ═══════════════════════════════════════════
        //  LEWY PANEL
        // ═══════════════════════════════════════════
        private Panel BuildLeftPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZpspColors.PanelDark,
                Padding = Padding.Empty
            };

            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = ZpspColors.PanelDark,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 0: Header
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 1: Route pills
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 2: Capacity bar
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 3: Timeline
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 4: Conflicts
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 5: Cargo (FILL!)
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 6: Buttons

            // ── ROW 0: Nagłówek kursu ──
            leftLayout.Controls.Add(BuildHeaderSection(), 0, 0);

            // ── ROW 1: Trasa pills ──
            routePills = new RoutePillsControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 36),
                Margin = new Padding(10, 4, 10, 4)
            };
            leftLayout.Controls.Add(routePills, 0, 1);

            // ── ROW 2: Capacity bar ──
            capacityBar = new CapacityBarControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 50),
                Margin = new Padding(10, 4, 10, 4)
            };
            leftLayout.Controls.Add(capacityBar, 0, 2);

            // ── ROW 3: Timeline (stała 65px) ──
            timeline = new TimelineControl
            {
                Dock = DockStyle.Fill,
                Height = 65,
                MinimumSize = new Size(0, 65),
                MaximumSize = new Size(0, 65),
                Margin = new Padding(10, 4, 10, 4)
            };
            leftLayout.Controls.Add(timeline, 0, 3);

            // ── ROW 4: Konflikty ──
            conflictPanel = new ConflictPanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 32),
                Margin = new Padding(10, 4, 10, 4)
            };
            leftLayout.Controls.Add(conflictPanel, 0, 4);

            // ── ROW 5: Ładunki — FILL ──
            leftLayout.Controls.Add(BuildCargoSection(), 0, 5);

            // ── ROW 6: Przyciski ──
            leftLayout.Controls.Add(BuildButtonsSection(), 0, 6);

            panel.Controls.Add(leftLayout);
            return panel;
        }

        private Panel BuildHeaderSection()
        {
            var section = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ZpspColors.PanelDark,
                Padding = new Padding(10, 8, 10, 8)
            };
            int y = 0;

            // Wiersz 1: KIEROWCA + POJAZD
            var lblK = MakeLabel("KIEROWCA:", ZpspFonts.Label8Bold, ZpspColors.TextMuted, 0, y + 4);
            section.Controls.Add(lblK);
            cboKierowca = new ComboBox
            {
                Location = new Point(82, y), Width = 180, Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ZpspColors.Green, ForeColor = Color.White,
                Font = ZpspFonts.ComboDriver, FlatStyle = FlatStyle.Flat
            };
            cboKierowca.SelectedIndexChanged += (s, e) => OnCourseChanged();
            section.Controls.Add(cboKierowca);
            btnNowyKierowca = MakeSquareButton("+", ZpspColors.Blue, new Point(268, y), 26);
            btnNowyKierowca.Click += BtnNowyKierowca_Click;
            section.Controls.Add(btnNowyKierowca);

            var lblP = MakeLabel("POJAZD:", ZpspFonts.Label8Bold, ZpspColors.TextMuted, 310, y + 4);
            section.Controls.Add(lblP);
            cboPojazd = new ComboBox
            {
                Location = new Point(375, y), Width = 180, Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ZpspColors.PanelDarkAlt, ForeColor = ZpspColors.TextLight,
                Font = ZpspFonts.Text10, FlatStyle = FlatStyle.Flat
            };
            cboPojazd.SelectedIndexChanged += (s, e) => OnCourseChanged();
            section.Controls.Add(cboPojazd);
            btnNowyPojazd = MakeSquareButton("+", ZpspColors.Green, new Point(561, y), 26);
            btnNowyPojazd.Click += BtnNowyPojazd_Click;
            section.Controls.Add(btnNowyPojazd);
            y += 32;

            // Wiersz 2: DATA + GODZINY
            var lblD = MakeLabel("DATA:", ZpspFonts.Label8Bold, ZpspColors.TextMuted, 0, y + 3);
            section.Controls.Add(lblD);
            dtpData = new DateTimePicker
            {
                Location = new Point(45, y), Width = 110, Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy", Font = ZpspFonts.Text10
            };
            section.Controls.Add(dtpData);

            var lblG = MakeLabel("GODZINY:", ZpspFonts.Label8Bold, ZpspColors.TextMuted, 175, y + 3);
            section.Controls.Add(lblG);
            dtpGodzWyjazdu = new DateTimePicker
            {
                Location = new Point(252, y), Width = 80, Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm", ShowUpDown = true, Font = ZpspFonts.TimePill
            };
            dtpGodzWyjazdu.ValueChanged += (s, e) => OnCourseChanged();
            section.Controls.Add(dtpGodzWyjazdu);

            var lblArrow = MakeLabel("\u2192", ZpspFonts.Header13Bold, ZpspColors.TextMuted, 337, y + 2);
            section.Controls.Add(lblArrow);
            dtpGodzPowrotu = new DateTimePicker
            {
                Location = new Point(362, y), Width = 80, Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm", ShowUpDown = true, Font = ZpspFonts.TimePill
            };
            dtpGodzPowrotu.ValueChanged += (s, e) => OnCourseChanged();
            section.Controls.Add(dtpGodzPowrotu);
            y += 34;

            // Separator
            var sep = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(2000, 1),
                BackColor = ZpspColors.PanelDarkBorder,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            section.Controls.Add(sep);

            return section;
        }

        private Panel BuildCargoSection()
        {
            var section = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZpspColors.PanelDark,
                Padding = new Padding(10, 0, 10, 0)
            };

            // Nagłówek ładunków (Dock=Top)
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = ZpspColors.PanelDark };
            headerPanel.Controls.Add(MakeLabel("\U0001f69a ŁADUNKI W KURSIE", ZpspFonts.Header11Bold, ZpspColors.TextWhite, 0, 4));
            lblLadunkiCount = MakeLabel("[0]", ZpspFonts.Label10Bold, ZpspColors.Green, 185, 6);
            headerPanel.Controls.Add(lblLadunkiCount);
            headerPanel.Controls.Add(MakeLabel("KOLEJNOŚĆ:", ZpspFonts.Label8Bold, ZpspColors.TextMuted, 240, 8));
            btnMoveUp = MakeSquareButton("\u25b2", ZpspColors.Blue, new Point(325, 4), 24);
            btnMoveUp.Click += BtnMoveUp_Click;
            headerPanel.Controls.Add(btnMoveUp);
            btnMoveDown = MakeSquareButton("\u25bc", ZpspColors.Blue, new Point(353, 4), 24);
            btnMoveDown.Click += BtnMoveDown_Click;
            headerPanel.Controls.Add(btnMoveDown);
            btnSortuj = new Button
            {
                Text = "Sortuj", Location = new Point(383, 4), Size = new Size(65, 24),
                FlatStyle = FlatStyle.Flat, BackColor = ZpspColors.Purple,
                ForeColor = Color.White, Font = ZpspFonts.Text8, Cursor = Cursors.Hand
            };
            btnSortuj.FlatAppearance.BorderSize = 0;
            btnSortuj.Click += BtnSortuj_Click;
            headerPanel.Controls.Add(btnSortuj);

            // Podsumowanie (Dock=Bottom)
            var summaryPanel = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = ZpspColors.PanelDark };
            lblSumPalety = MakeLabel("\u03a3 Palety: 0", ZpspFonts.Label9Bold, ZpspColors.Orange, 4, 4);
            lblSumPojemniki = MakeLabel("\u03a3 Pojemniki: 0", ZpspFonts.Label9Bold, ZpspColors.Green, 150, 4);
            lblSumWaga = MakeLabel("\u03a3 Waga: 0 kg", ZpspFonts.Label9Bold, ZpspColors.TextLight, 320, 4);
            summaryPanel.Controls.AddRange(new Control[] { lblSumPalety, lblSumPojemniki, lblSumWaga });

            // DataGridView (Dock=Fill — wypełnia RESZTĘ)
            dgvLadunki = BuildDgvLadunki();
            dgvLadunki.Dock = DockStyle.Fill;

            // Dodaj w kolejności, potem BringToFront dla poprawnego dock
            section.Controls.Add(headerPanel);
            section.Controls.Add(summaryPanel);
            section.Controls.Add(dgvLadunki);
            summaryPanel.BringToFront();
            headerPanel.BringToFront();

            return section;
        }

        private FlowLayoutPanel BuildButtonsSection()
        {
            var section = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = ZpspColors.PanelDark,
                Padding = new Padding(10, 6, 10, 6)
            };

            btnZapisz = new Button
            {
                Text = "\u2713 ZAPISZ KURS", Size = new Size(180, 38),
                FlatStyle = FlatStyle.Flat, BackColor = ZpspColors.Green,
                ForeColor = Color.White, Font = ZpspFonts.Header13Bold,
                Cursor = Cursors.Hand, Margin = new Padding(4)
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Text = "ANULUJ", Size = new Size(90, 38),
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = ZpspColors.TextMuted, Font = ZpspFonts.Header11Bold,
                Cursor = Cursors.Hand, Margin = new Padding(4)
            };
            btnAnuluj.FlatAppearance.BorderColor = ZpspColors.PanelDarkBorder;
            btnAnuluj.Click += (s, e) => Close();

            section.Controls.Add(btnZapisz);
            section.Controls.Add(btnAnuluj);
            return section;
        }

        // ═══════════════════════════════════════════
        //  PRAWY PANEL (jasny — zamówienia)
        // ═══════════════════════════════════════════
        private Panel BuildRightPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZpspColors.PanelLight,
                Padding = new Padding(0)
            };

            // ── Nagłówek zielony ──
            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 42,
                BackColor = ZpspColors.Green
            };

            var lblTitle = MakeLabel("\U0001f4cb ZAMÓWIENIA", ZpspFonts.Header12Bold, Color.White, 10, 10);
            header.Controls.Add(lblTitle);

            lblZamowieniaCount = MakeLabel("[0 zam.]", ZpspFonts.Label10Bold,
                Color.FromArgb(180, 255, 255, 255), 170, 12);
            header.Controls.Add(lblZamowieniaCount);

            // Toggle Ubój / Odbiór
            rbDataUboju = new RadioButton
            {
                Text = "Ubój", Checked = true, AutoSize = true,
                Location = new Point(280, 10),
                ForeColor = Color.White, Font = ZpspFonts.Label9Bold,
                FlatStyle = FlatStyle.Flat
            };
            rbDataUboju.CheckedChanged += async (s, e) => { if (rbDataUboju.Checked) await LoadWolneZamowieniaAsync(); };
            header.Controls.Add(rbDataUboju);

            rbDataOdbioru = new RadioButton
            {
                Text = "Odbiór", AutoSize = true,
                Location = new Point(340, 10),
                ForeColor = Color.White, Font = ZpspFonts.Label9Bold,
                FlatStyle = FlatStyle.Flat
            };
            rbDataOdbioru.CheckedChanged += async (s, e) => { if (rbDataOdbioru.Checked) await LoadWolneZamowieniaAsync(); };
            header.Controls.Add(rbDataOdbioru);

            // Szukaj
            txtSzukaj = new TextBox
            {
                Location = new Point(420, 8), Width = 140, Height = 26,
                Font = ZpspFonts.Text9, BackColor = Color.White,
                ForeColor = ZpspColors.TextMedium
            };
            txtSzukaj.PlaceholderText = "\U0001f50d Szukaj...";
            txtSzukaj.TextChanged += (s, e) =>
            {
                _filtrZamowien = txtSzukaj.Text.Trim();
                ShowZamowieniaInGrid();
            };
            header.Controls.Add(txtSzukaj);

            // Data
            dtpZamowieniaDate = new DateTimePicker
            {
                Location = new Point(570, 8), Width = 100,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM", Font = ZpspFonts.Text9,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            dtpZamowieniaDate.ValueChanged += async (s, e) => await LoadWolneZamowieniaAsync();
            header.Controls.Add(dtpZamowieniaDate);

            panel.Controls.Add(header);

            // ── DataGridView zamówień ──
            dgvZamowienia = BuildDgvZamowienia();
            dgvZamowienia.Dock = DockStyle.Fill;
            panel.Controls.Add(dgvZamowienia);

            // ── Footer — Dodaj zaznaczone ──
            var footer = new Panel
            {
                Dock = DockStyle.Bottom, Height = 44,
                BackColor = ZpspColors.GreenBg
            };

            btnDodajZaznaczone = new Button
            {
                Text = "\u2b07 Dodaj zaznaczone do kursu (0)",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = ZpspColors.Green,
                ForeColor = Color.White,
                Font = ZpspFonts.Header11Bold,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 4, 8, 4)
            };
            btnDodajZaznaczone.FlatAppearance.BorderSize = 0;
            btnDodajZaznaczone.Click += BtnDodajZaznaczone_Click;
            footer.Controls.Add(btnDodajZaznaczone);

            panel.Controls.Add(footer);

            // Porządek: footer na dole, header na górze, dgv w środku
            footer.BringToFront();
            header.BringToFront();

            return panel;
        }

        // ═══════════════════════════════════════════
        //  BUDOWA GRIDÓW
        // ═══════════════════════════════════════════
        private DataGridView BuildDgvLadunki()
        {
            var dgv = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                BackgroundColor = ZpspColors.PanelDark,
                GridColor = ZpspColors.PanelDarkBorder
            };

            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ZpspColors.PanelDarkBorder,
                ForeColor = ZpspColors.TextMuted,
                Font = ZpspFonts.DgvHeader,
                SelectionBackColor = ZpspColors.PanelDarkBorder,
                SelectionForeColor = ZpspColors.TextMuted,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            };

            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ZpspColors.PanelDark,
                ForeColor = ZpspColors.TextLight,
                SelectionBackColor = ZpspColors.PurpleSelection,
                SelectionForeColor = Color.White,
                Font = ZpspFonts.Text10
            };

            dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ZpspColors.PanelDarkAlt,
                ForeColor = ZpspColors.TextLight,
                SelectionBackColor = ZpspColors.PurpleSelection,
                SelectionForeColor = Color.White
            };

            dgv.RowTemplate.Height = 36;
            dgv.ColumnHeadersHeight = 32;

            dgv.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "Lp", HeaderText = "Lp.", Width = 40, DefaultCellStyle = new DataGridViewCellStyle { Font = ZpspFonts.DgvLpBold, ForeColor = ZpspColors.Green, Alignment = DataGridViewContentAlignment.MiddleCenter } },
                new DataGridViewTextBoxColumn { Name = "Klient", HeaderText = "Klient", Width = 160, DefaultCellStyle = new DataGridViewCellStyle { Font = ZpspFonts.DgvClientBold, ForeColor = Color.White } },
                new DataGridViewTextBoxColumn { Name = "DataUboju", HeaderText = "Data uboju", Width = 90 },
                new DataGridViewTextBoxColumn { Name = "Palety", HeaderText = "Palety", Width = 65, DefaultCellStyle = new DataGridViewCellStyle { Font = ZpspFonts.DgvPaletyBold, ForeColor = ZpspColors.OrangeLight, Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Pojemniki", HeaderText = "Poj.", Width = 65, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ZpspColors.Green, Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Adres", HeaderText = "Adres", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ZpspColors.TextMuted } },
                new DataGridViewTextBoxColumn { Name = "Uwagi", HeaderText = "Uwagi", Width = 180 }
            );

            // Drag & drop ładunki → zamówienia
            dgv.AllowDrop = true;
            dgv.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            dgv.KeyDown += DgvLadunki_KeyDown;

            return dgv;
        }

        private DataGridView BuildDgvZamowienia()
        {
            var dgv = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                BackgroundColor = ZpspColors.PanelLight,
                GridColor = ZpspColors.PanelLightBorder
            };

            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ZpspColors.PanelLightAlt,
                ForeColor = ZpspColors.TextGray,
                Font = ZpspFonts.DgvHeader,
                SelectionBackColor = ZpspColors.PanelLightAlt,
                SelectionForeColor = ZpspColors.TextGray,
                Padding = new Padding(4, 0, 4, 0)
            };

            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ZpspColors.PanelLight,
                ForeColor = ZpspColors.TextDark,
                SelectionBackColor = ZpspColors.PurpleRow,
                SelectionForeColor = ZpspColors.Purple,
                Font = ZpspFonts.Text10
            };

            dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ZpspColors.PanelLightAlt,
                ForeColor = ZpspColors.TextDark,
                SelectionBackColor = ZpspColors.PurpleRow,
                SelectionForeColor = ZpspColors.Purple
            };

            dgv.RowTemplate.Height = 34;
            dgv.ColumnHeadersHeight = 30;

            dgv.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "ID", HeaderText = "ID", Width = 50, Visible = false },
                new DataGridViewTextBoxColumn { Name = "Odbiór", HeaderText = "Odbiór", Width = 70 },
                new DataGridViewTextBoxColumn { Name = "Godz", HeaderText = "Godz.", Width = 55, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ZpspColors.Purple, Font = ZpspFonts.Label9Bold } },
                new DataGridViewTextBoxColumn { Name = "Palety", HeaderText = "Palety", Width = 55, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ZpspColors.Orange, Font = ZpspFonts.DgvPaletyBold, Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Pojemniki", HeaderText = "Poj.", Width = 55, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
                new DataGridViewTextBoxColumn { Name = "Klient", HeaderText = "Klient", Width = 160, DefaultCellStyle = new DataGridViewCellStyle { Font = ZpspFonts.DgvClientBold } },
                new DataGridViewTextBoxColumn { Name = "Adres", HeaderText = "Adres", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ZpspColors.TextGray, Font = ZpspFonts.Text9 } }
            );

            dgv.CellDoubleClick += DgvZamowienia_CellDoubleClick;

            // Drag & drop zamówienia → ładunki
            dgv.AllowDrop = true;
            dgv.DragOver += (s, e) => e.Effect = DragDropEffects.Move;

            return dgv;
        }

        // ═══════════════════════════════════════════
        //  ŁADOWANIE DANYCH
        // ═══════════════════════════════════════════
        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                _kierowcy = await _repozytorium.PobierzKierowcowAsync();
                _pojazdy = await _repozytorium.PobierzPojazdyAsync();

                cboKierowca.Items.Clear();
                cboKierowca.Items.Add("(brak)");
                foreach (var k in _kierowcy.Where(k => k.Aktywny))
                    cboKierowca.Items.Add(k);

                cboPojazd.Items.Clear();
                cboPojazd.Items.Add("(brak)");
                foreach (var p in _pojazdy.Where(p => p.Aktywny))
                    cboPojazd.Items.Add(p);

                if (_kurs != null)
                {
                    // Edycja istniejącego kursu
                    var kierowca = _kierowcy.FirstOrDefault(k => k.KierowcaID == _kurs.KierowcaID);
                    if (kierowca != null) cboKierowca.SelectedItem = kierowca;
                    else cboKierowca.SelectedIndex = 0;

                    var pojazd = _pojazdy.FirstOrDefault(p => p.PojazdID == _kurs.PojazdID);
                    if (pojazd != null) cboPojazd.SelectedItem = pojazd;
                    else cboPojazd.SelectedIndex = 0;

                    if (_kurs.GodzWyjazdu.HasValue)
                        dtpGodzWyjazdu.Value = DateTime.Today.Add(_kurs.GodzWyjazdu.Value);
                    if (_kurs.GodzPowrotu.HasValue)
                        dtpGodzPowrotu.Value = DateTime.Today.Add(_kurs.GodzPowrotu.Value);

                    // Załaduj ładunki
                    await LoadLadunkiFromDb();
                }
                else
                {
                    cboKierowca.SelectedIndex = 0;
                    cboPojazd.SelectedIndex = 0;
                    dtpGodzWyjazdu.Value = DateTime.Today.AddHours(6);
                    dtpGodzPowrotu.Value = DateTime.Today.AddHours(18);
                }

                await LoadWolneZamowieniaAsync();
                _dataLoaded = true;
                OnCourseChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] LoadDataAsync error: {ex.Message}");
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadLadunkiFromDb()
        {
            if (!_kursId.HasValue) return;

            try
            {
                var dbLadunki = await _repozytorium.PobierzLadunkiAsync(_kursId.Value);
                _ladunki.Clear();

                foreach (var l in dbLadunki)
                {
                    string? nazwaKlienta = null;
                    string? adres = null;

                    // Pobierz nazwę klienta z zamówienia lub kontrahenta
                    if (l.KodKlienta?.StartsWith("ZAM_") == true && int.TryParse(l.KodKlienta.Substring(4), out int zamId))
                    {
                        var info = await PobierzInfoZamowienia(zamId);
                        nazwaKlienta = info.nazwa;
                        adres = info.adres;
                    }

                    var planE2 = l.PlanE2NaPaleteOverride ?? 36;
                    decimal palety = l.TrybE2 && planE2 > 0 ? (decimal)l.PojemnikiE2 / planE2 : (l.PaletyH1 ?? 0);

                    _ladunki.Add(new LadunekRow
                    {
                        LadunekID = l.LadunekID,
                        KursID = l.KursID,
                        Kolejnosc = l.Kolejnosc,
                        KodKlienta = l.KodKlienta,
                        Palety = palety,
                        PojemnikiE2 = l.PojemnikiE2,
                        Uwagi = l.Uwagi,
                        Adres = adres,
                        TrybE2 = l.TrybE2,
                        PlanE2NaPaleteOverride = l.PlanE2NaPaleteOverride,
                        NazwaKlienta = nazwaKlienta
                    });
                }

                RefreshLadunkiGrid();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] LoadLadunkiFromDb error: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task<(string? nazwa, string? adres)> PobierzInfoZamowienia(int zamId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = @"SELECT TOP 1 z.Uwagi, c.Name1,
                            ISNULL(a.ZipCode,'') + ' ' + ISNULL(a.City,'')
                            FROM ZamowieniaMieso z
                            LEFT JOIN [192.168.0.112].Handel.SSCommon.STContractors c ON z.KlientId = c.Id
                            LEFT JOIN [192.168.0.112].Handel.SSCommon.STPostOfficeAddresses a ON c.Id = a.ContractorId AND a.IsDefault = 1
                            WHERE z.Id = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamId);
                await using var rdr = await cmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    return (rdr.IsDBNull(1) ? null : rdr.GetString(1),
                            rdr.IsDBNull(2) ? null : rdr.GetString(2).Trim());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] PobierzInfoZamowienia error: {ex.Message}");
            }
            return (null, null);
        }

        private async System.Threading.Tasks.Task LoadWolneZamowieniaAsync()
        {
            try
            {
                var targetDate = dtpZamowieniaDate?.Value ?? dtpData.Value;
                string dateColumn = UzywajDataUboju ? "DataUboju" : "DataPrzyjazdu";

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = $@"SELECT z.Id, z.KlientId, ISNULL(c.Name1, 'Klient #' + CAST(z.KlientId AS VARCHAR)) AS KlientNazwa,
                            ISNULL(z.LiczbaPojemnikow, 0) AS Pojemniki,
                            ISNULL(z.LiczbaPalet, 0) AS Palety,
                            ISNULL(z.TrybE2, 0) AS TrybE2,
                            z.DataPrzyjazdu, z.DataUboju,
                            ISNULL(z.TransportStatus, 'Oczekuje') AS TransportStatus,
                            ISNULL(a.ZipCode,'') + ' ' + ISNULL(a.City,'') AS Adres,
                            ISNULL(wym.CDim_Handlowiec_Val, '') AS Handlowiec
                          FROM ZamowieniaMieso z
                          LEFT JOIN [192.168.0.112].Handel.SSCommon.STContractors c ON z.KlientId = c.Id
                          LEFT JOIN [192.168.0.112].Handel.SSCommon.STPostOfficeAddresses a ON c.Id = a.ContractorId AND a.IsDefault = 1
                          LEFT JOIN [192.168.0.112].Handel.SSCommon.ContractorClassification wym ON c.Id = wym.ElementId
                          WHERE CAST(z.{dateColumn} AS DATE) = @TargetDate
                            AND ISNULL(z.TransportStatus, 'Oczekuje') = 'Oczekuje'
                            AND ISNULL(z.CzyZrealizowane, 0) = 0
                            AND z.TransportKursID IS NULL
                          ORDER BY z.DataPrzyjazdu";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@TargetDate", targetDate.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();

                _wolneZamowienia.Clear();
                while (await rdr.ReadAsync())
                {
                    _wolneZamowienia.Add(new ZamowienieRow
                    {
                        ZamowienieId = rdr.GetInt32(0),
                        KlientId = rdr.GetInt32(1),
                        KlientNazwa = rdr.GetString(2),
                        Pojemniki = rdr.GetInt32(3),
                        Palety = rdr.GetDecimal(4),
                        TrybE2 = rdr.GetBoolean(5),
                        DataPrzyjazdu = rdr.IsDBNull(6) ? DateTime.Today : rdr.GetDateTime(6),
                        DataUboju = rdr.IsDBNull(7) ? null : rdr.GetDateTime(7),
                        TransportStatus = rdr.GetString(8),
                        Adres = rdr.IsDBNull(9) ? "" : rdr.GetString(9).Trim(),
                        Handlowiec = rdr.IsDBNull(10) ? "" : rdr.GetString(10)
                    });
                }

                // Odfiltruj zamówienia już dodane do tego kursu
                var dodaneIds = _zamowieniaDoDodania.Select(z => z.ZamowienieId).ToHashSet();
                var ladunkiZamIds = _ladunki
                    .Where(l => l.KodKlienta?.StartsWith("ZAM_") == true)
                    .Select(l => int.TryParse(l.KodKlienta!.Substring(4), out var id) ? id : -1)
                    .Where(id => id > 0)
                    .ToHashSet();

                _wolneZamowienia.RemoveAll(z => dodaneIds.Contains(z.ZamowienieId) || ladunkiZamIds.Contains(z.ZamowienieId));

                ShowZamowieniaInGrid();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] LoadWolneZamowienia error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════
        //  ODŚWIEŻANIE GRIDÓW
        // ═══════════════════════════════════════════
        private void RefreshLadunkiGrid()
        {
            dgvLadunki.Rows.Clear();
            foreach (var l in _ladunki.OrderBy(l => l.Kolejnosc))
            {
                var idx = dgvLadunki.Rows.Add(
                    l.Kolejnosc,
                    l.NazwaKlienta ?? l.KodKlienta ?? "?",
                    l.DataUboju?.ToString("dd.MM") ?? "",
                    l.Palety.ToString("N1"),
                    l.PojemnikiE2.ToString(),
                    l.Adres ?? "",
                    l.Uwagi ?? ""
                );
                dgvLadunki.Rows[idx].Tag = l;

                if (l.AnulowanyWZamowieniu)
                    dgvLadunki.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(60, 229, 57, 53);
                else if (l.ZmienionyWZamowieniu)
                    dgvLadunki.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(60, 245, 158, 11);
            }

            lblLadunkiCount.Text = $"[{_ladunki.Count}]";
            RefreshSummary();
        }

        private void ShowZamowieniaInGrid()
        {
            dgvZamowienia.Rows.Clear();

            var filtered = string.IsNullOrEmpty(_filtrZamowien)
                ? _wolneZamowienia
                : _wolneZamowienia.Where(z =>
                    z.KlientNazwa.Contains(_filtrZamowien, StringComparison.OrdinalIgnoreCase) ||
                    z.Adres.Contains(_filtrZamowien, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var z in filtered.OrderBy(z => z.DataPrzyjazdu))
            {
                var idx = dgvZamowienia.Rows.Add(
                    z.ZamowienieId,
                    z.DataOdbioru.ToString("dd.MM"),
                    z.GodzinaStr,
                    z.Palety.ToString("N1"),
                    z.Pojemniki.ToString(),
                    z.KlientNazwa,
                    z.Adres
                );
                dgvZamowienia.Rows[idx].Tag = z;
            }

            lblZamowieniaCount.Text = $"[{filtered.Count} zam.]";
            UpdateBtnDodajCount();
        }

        private void RefreshSummary()
        {
            decimal sumPalet = _ladunki.Sum(l => l.Palety);
            int sumPoj = _ladunki.Sum(l => l.PojemnikiE2);
            // Szacunkowa waga: ~5.4 kg/pojemnik E2
            int sumaWaga = (int)(sumPoj * 5.4);

            lblSumPalety.Text = $"\u03a3 Palety: {sumPalet:N1}";
            lblSumPojemniki.Text = $"\u03a3 Pojemniki: {sumPoj:N0}";
            lblSumWaga.Text = $"\u03a3 Waga: {sumaWaga:N0} kg";
        }

        // ═══════════════════════════════════════════
        //  OnCourseChanged — centralny punkt aktualizacji
        // ═══════════════════════════════════════════
        private void OnCourseChanged()
        {
            if (!_dataLoaded) return;

            try
            {
                // 1. Sumy
                RefreshSummary();

                // 2. Capacity bar
                int maxPalet = (cboPojazd.SelectedItem is Pojazd p) ? p.PaletyH1 : 33;
                decimal sumPalet = _ladunki.Sum(l => l.Palety);
                int sumPoj = _ladunki.Sum(l => l.PojemnikiE2);
                int sumaWaga = (int)(sumPoj * 5.4);
                capacityBar.SetCapacity(sumPalet, maxPalet, sumPoj, sumaWaga);

                // 3. Route pills
                var stops = _ladunki.OrderBy(l => l.Kolejnosc)
                    .Where(l => !string.IsNullOrEmpty(l.NazwaKlienta))
                    .Select(l => l.NazwaKlienta!)
                    .ToArray();
                routePills.SetRoute(stops);

                // 4. Timeline
                var timeStops = _ladunki.OrderBy(l => l.Kolejnosc)
                    .Select(l => new TimelineControl.TimelineStop
                    {
                        ClientName = l.NazwaKlienta ?? "?",
                        PlannedArrival = TimeSpan.Zero // TODO: calculate from order data
                    }).ToList();
                timeline.SetCourse(
                    dtpGodzWyjazdu.Value.TimeOfDay,
                    dtpGodzPowrotu.Value.TimeOfDay,
                    timeStops);

                // 5. Konflikty
                var courseData = new ConflictDetectionService.CourseData
                {
                    KursId = _kursId,
                    DataKursu = dtpData.Value,
                    KierowcaId = (cboKierowca.SelectedItem is Kierowca k) ? k.KierowcaID : null,
                    PojazdId = (cboPojazd.SelectedItem is Pojazd pv) ? pv.PojazdID : null,
                    PaletyPojazdu = maxPalet,
                    GodzinaWyjazdu = dtpGodzWyjazdu.Value.TimeOfDay,
                    GodzinaPowrotu = dtpGodzPowrotu.Value.TimeOfDay,
                    SumaPalet = sumPalet,
                    SumaPojemnikow = sumPoj,
                    SumaWagaKg = sumaWaga,
                    Ladunki = _ladunki.Select(l => new ConflictDetectionService.LoadItem
                    {
                        KodKlienta = l.KodKlienta,
                        NazwaKlienta = l.NazwaKlienta,
                        Adres = l.Adres,
                        Handlowiec = l.Handlowiec,
                        Palety = l.Palety
                    }).ToList()
                };
                var conflicts = _conflictService.DetectAll(courseData);
                conflictPanel.SetConflicts(conflicts);

                // 6. Przycisk Zapisz
                bool hasErrors = conflicts.Any(c => c.Level == ConflictLevel.Error);
                btnZapisz.BackColor = hasErrors ? ZpspColors.Orange : ZpspColors.Green;
                btnZapisz.Text = hasErrors ? "\u26a0 ZAPISZ KURS (z ostrzeżeniami)" : "\u2713 ZAPISZ KURS";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] OnCourseChanged error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════
        //  EVENT HANDLERS
        // ═══════════════════════════════════════════
        private void DgvZamowienia_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvZamowienia.Rows[e.RowIndex];
            if (row.Tag is ZamowienieRow zam)
                DodajZamowienieDoKursu(zam);
        }

        private void BtnDodajZaznaczone_Click(object? sender, EventArgs e)
        {
            var zaznaczone = dgvZamowienia.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => r.Tag is ZamowienieRow)
                .Select(r => (ZamowienieRow)r.Tag)
                .ToList();

            foreach (var zam in zaznaczone)
                DodajZamowienieDoKursu(zam);
        }

        private void DodajZamowienieDoKursu(ZamowienieRow zamowienie)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                _zamowieniaDoDodania.Add(zamowienie);
                _zamowieniaDoUsuniecia.Remove(zamowienie.ZamowienieId);

                _ladunki.Add(new LadunekRow
                {
                    LadunekID = -(zamowienie.ZamowienieId + 1000),
                    KursID = _kursId ?? 0,
                    KodKlienta = $"ZAM_{zamowienie.ZamowienieId}",
                    Palety = zamowienie.Palety,
                    PojemnikiE2 = zamowienie.Pojemniki,
                    TrybE2 = zamowienie.TrybE2,
                    Uwagi = $"{zamowienie.KlientNazwa} ({zamowienie.GodzinaStr})",
                    Adres = zamowienie.Adres,
                    NazwaKlienta = zamowienie.KlientNazwa,
                    DataUboju = zamowienie.DataUboju,
                    Handlowiec = zamowienie.Handlowiec,
                    Kolejnosc = _ladunki.Count + 1
                });

                _wolneZamowienia.Remove(zamowienie);
                RefreshLadunkiGrid();
                ShowZamowieniaInGrid();
                OnCourseChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UsunLadunekZKursu(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvLadunki.Rows.Count) return;
            if (dgvLadunki.Rows[rowIndex].Tag is not LadunekRow ladunek) return;
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                _ladunki.Remove(ladunek);
                for (int i = 0; i < _ladunki.Count; i++)
                    _ladunki[i].Kolejnosc = i + 1;

                if (ladunek.KodKlienta?.StartsWith("ZAM_") == true &&
                    int.TryParse(ladunek.KodKlienta.Substring(4), out var zamId))
                {
                    var zamDoDodania = _zamowieniaDoDodania.FirstOrDefault(z => z.ZamowienieId == zamId);
                    if (zamDoDodania != null)
                        _zamowieniaDoDodania.Remove(zamDoDodania);
                    else if (ladunek.LadunekID > 0)
                        _zamowieniaDoUsuniecia.Add(zamId);
                }

                RefreshLadunkiGrid();
                OnCourseChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void DgvLadunki_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && dgvLadunki.CurrentRow != null)
            {
                UsunLadunekZKursu(dgvLadunki.CurrentRow.Index);
                e.Handled = true;
            }
        }

        private void BtnMoveUp_Click(object? sender, EventArgs e)
        {
            if (dgvLadunki.CurrentRow == null) return;
            int idx = dgvLadunki.CurrentRow.Index;
            if (idx <= 0 || idx >= _ladunki.Count) return;

            (_ladunki[idx - 1], _ladunki[idx]) = (_ladunki[idx], _ladunki[idx - 1]);
            for (int i = 0; i < _ladunki.Count; i++) _ladunki[i].Kolejnosc = i + 1;
            RefreshLadunkiGrid();
            dgvLadunki.ClearSelection();
            dgvLadunki.Rows[idx - 1].Selected = true;
            dgvLadunki.CurrentCell = dgvLadunki.Rows[idx - 1].Cells[0];
            OnCourseChanged();
        }

        private void BtnMoveDown_Click(object? sender, EventArgs e)
        {
            if (dgvLadunki.CurrentRow == null) return;
            int idx = dgvLadunki.CurrentRow.Index;
            if (idx < 0 || idx >= _ladunki.Count - 1) return;

            (_ladunki[idx], _ladunki[idx + 1]) = (_ladunki[idx + 1], _ladunki[idx]);
            for (int i = 0; i < _ladunki.Count; i++) _ladunki[i].Kolejnosc = i + 1;
            RefreshLadunkiGrid();
            dgvLadunki.ClearSelection();
            dgvLadunki.Rows[idx + 1].Selected = true;
            dgvLadunki.CurrentCell = dgvLadunki.Rows[idx + 1].Cells[0];
            OnCourseChanged();
        }

        private void BtnSortuj_Click(object? sender, EventArgs e)
        {
            _ladunki = _ladunki.OrderBy(l => l.DataUboju).ThenBy(l => l.NazwaKlienta).ToList();
            for (int i = 0; i < _ladunki.Count; i++) _ladunki[i].Kolejnosc = i + 1;
            RefreshLadunkiGrid();
            OnCourseChanged();
        }

        private async void BtnZapisz_Click(object? sender, EventArgs e)
        {
            // Sprawdź konflikty
            if (conflictPanel.HasErrors)
            {
                var result = MessageBox.Show(
                    $"Kurs ma {conflictPanel.ErrorCount} błędów. Czy na pewno chcesz zapisać?",
                    "Konflikty", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }

            try
            {
                btnZapisz.Enabled = false;
                btnZapisz.Text = "Zapisywanie...";

                var kurs = new Kurs
                {
                    KursID = _kursId ?? 0,
                    DataKursu = dtpData.Value.Date,
                    KierowcaID = (cboKierowca.SelectedItem is Kierowca k) ? k.KierowcaID : null,
                    PojazdID = (cboPojazd.SelectedItem is Pojazd p) ? p.PojazdID : null,
                    Trasa = BuildTrasaString(),
                    GodzWyjazdu = dtpGodzWyjazdu.Value.TimeOfDay,
                    GodzPowrotu = dtpGodzPowrotu.Value.TimeOfDay,
                    Status = "Planowany"
                };

                if (_kursId.HasValue)
                {
                    await _repozytorium.AktualizujNaglowekKursuAsync(kurs, _uzytkownik);
                }
                else
                {
                    _kursId = await _repozytorium.DodajKursAsync(kurs, _uzytkownik);
                    kurs.KursID = _kursId.Value;
                }

                // Zapisz ładunki
                foreach (var ladunek in _ladunki)
                {
                    if (ladunek.LadunekID <= 0)
                    {
                        // Nowy ładunek
                        var dbLadunek = new Ladunek
                        {
                            KursID = _kursId.Value,
                            Kolejnosc = ladunek.Kolejnosc,
                            KodKlienta = ladunek.KodKlienta,
                            PojemnikiE2 = ladunek.PojemnikiE2,
                            PaletyH1 = ladunek.TrybE2 ? null : (int?)ladunek.Palety,
                            PlanE2NaPaleteOverride = ladunek.PlanE2NaPaleteOverride,
                            Uwagi = ladunek.Uwagi,
                            TrybE2 = ladunek.TrybE2
                        };
                        await _repozytorium.DodajLadunekAsync(dbLadunek);

                        // Aktualizuj status zamówienia
                        if (ladunek.KodKlienta?.StartsWith("ZAM_") == true &&
                            int.TryParse(ladunek.KodKlienta.Substring(4), out int zamId))
                        {
                            await AktualizujStatusZamowienia(zamId, _kursId.Value);
                        }
                    }
                    else
                    {
                        // Aktualizuj kolejność istniejącego ładunku
                        var dbLad = new Ladunek
                        {
                            LadunekID = ladunek.LadunekID,
                            KursID = _kursId.Value,
                            Kolejnosc = ladunek.Kolejnosc,
                            KodKlienta = ladunek.KodKlienta,
                            PojemnikiE2 = ladunek.PojemnikiE2,
                            PaletyH1 = ladunek.TrybE2 ? null : (int?)ladunek.Palety,
                            PlanE2NaPaleteOverride = ladunek.PlanE2NaPaleteOverride,
                            Uwagi = ladunek.Uwagi,
                            TrybE2 = ladunek.TrybE2
                        };
                        await _repozytorium.AktualizujLadunekAsync(dbLad);
                    }
                }

                // Przywróć status zamówień zwróconych do puli
                foreach (var zamId in _zamowieniaDoUsuniecia)
                {
                    await PrzywrocStatusZamowienia(zamId);
                }

                // Aktualizuj trasę
                kurs.Trasa = BuildTrasaString();
                await _repozytorium.AktualizujNaglowekKursuAsync(kurs, _uzytkownik);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] BtnZapisz error: {ex.Message}");
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnZapisz.Enabled = true;
                btnZapisz.Text = "\u2713 ZAPISZ KURS";
            }
        }

        private async System.Threading.Tasks.Task AktualizujStatusZamowienia(int zamId, long kursId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = "UPDATE ZamowieniaMieso SET TransportKursID = @KursID, TransportStatus = 'Przypisany' WHERE Id = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KursID", kursId);
                cmd.Parameters.AddWithValue("@Id", zamId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] AktualizujStatusZamowienia error: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task PrzywrocStatusZamowienia(int zamId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = "UPDATE ZamowieniaMieso SET TransportKursID = NULL, TransportStatus = 'Oczekuje' WHERE Id = @Id";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KursEditor] PrzywrocStatusZamowienia error: {ex.Message}");
            }
        }

        private async void BtnNowyKierowca_Click(object? sender, EventArgs e)
        {
            using var dlg = new EdytorKursuWithPalety.DodajKierowceDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.NowyKierowca != null)
            {
                try
                {
                    await _repozytorium.DodajKierowceAsync(dlg.NowyKierowca);
                    _kierowcy = await _repozytorium.PobierzKierowcowAsync();
                    cboKierowca.Items.Clear();
                    cboKierowca.Items.Add("(brak)");
                    foreach (var k in _kierowcy.Where(k => k.Aktywny))
                        cboKierowca.Items.Add(k);
                    cboKierowca.SelectedItem = _kierowcy.FirstOrDefault(k => k.Imie == dlg.NowyKierowca.Imie && k.Nazwisko == dlg.NowyKierowca.Nazwisko);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd dodawania kierowcy: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnNowyPojazd_Click(object? sender, EventArgs e)
        {
            using var dlg = new EdytorKursuWithPalety.DodajPojazdDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.NowyPojazd != null)
            {
                try
                {
                    await _repozytorium.DodajPojazdAsync(dlg.NowyPojazd);
                    _pojazdy = await _repozytorium.PobierzPojazdyAsync();
                    cboPojazd.Items.Clear();
                    cboPojazd.Items.Add("(brak)");
                    foreach (var p in _pojazdy.Where(p => p.Aktywny))
                        cboPojazd.Items.Add(p);
                    cboPojazd.SelectedItem = _pojazdy.FirstOrDefault(p => p.Rejestracja == dlg.NowyPojazd.Rejestracja);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd dodawania pojazdu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ═══════════════════════════════════════════
        //  SKRÓTY KLAWISZOWE
        // ═══════════════════════════════════════════
        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                BtnZapisz_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                txtSzukaj?.Focus();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                _ = LoadWolneZamowieniaAsync();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Up)
            {
                BtnMoveUp_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Down)
            {
                BtnMoveDown_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Z && _undoStack.Count > 0)
            {
                _undoStack.Pop().Invoke();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && dgvZamowienia.Focused && dgvZamowienia.CurrentRow?.Tag is ZamowienieRow zam)
            {
                DodajZamowienieDoKursu(zam);
                e.Handled = true;
            }
        }

        // ═══════════════════════════════════════════
        //  AUTO UPDATE TIMER
        // ═══════════════════════════════════════════
        private void InitAutoUpdateTimer()
        {
            _autoUpdateTimer = new Timer { Interval = 10000 };
            _autoUpdateTimer.Tick += async (s, e) =>
            {
                if (_isUpdating || !_dataLoaded) return;
                _autoUpdateTimer.Stop();
                try
                {
                    await LoadWolneZamowieniaAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[KursEditor] Auto-update error: {ex.Message}");
                }
                finally
                {
                    if (!IsDisposed)
                        _autoUpdateTimer.Start();
                }
            };
            _autoUpdateTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _autoUpdateTimer?.Stop();
            _autoUpdateTimer?.Dispose();
            base.OnFormClosed(e);
        }

        // ═══════════════════════════════════════════
        //  POMOCNICZE
        // ═══════════════════════════════════════════
        private string BuildTrasaString()
        {
            var names = _ladunki.OrderBy(l => l.Kolejnosc)
                .Where(l => !string.IsNullOrEmpty(l.NazwaKlienta))
                .Select(l => l.NazwaKlienta!)
                .ToList();
            return names.Count > 0 ? string.Join(" → ", names) : "";
        }

        private void UpdateBtnDodajCount()
        {
            int count = dgvZamowienia.SelectedRows.Count;
            btnDodajZaznaczone.Text = $"\u2b07 Dodaj zaznaczone do kursu ({count})";
        }

        private static Label MakeLabel(string text, Font font, Color color, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                Location = new Point(x, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        private static Button MakeSquareButton(string text, Color bgColor, Point location, int size)
        {
            var btn = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(size, size),
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
