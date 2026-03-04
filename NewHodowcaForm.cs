using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace Kalendarz1
{
    public partial class NewHodowcaForm : Form
    {
        private readonly string _connectionString;
        private readonly string _currentUser;

        // ── Paleta ──
        static readonly Color Navy1 = Color.FromArgb(30, 58, 95);
        static readonly Color Navy2 = Color.FromArgb(44, 62, 80);
        static readonly Color Slate = Color.FromArgb(52, 73, 94);
        static readonly Color Gold = Color.FromArgb(212, 175, 55);
        static readonly Color AccBlue = Color.FromArgb(52, 152, 219);
        static readonly Color AccGreen = Color.FromArgb(39, 174, 96);
        static readonly Color AccRed = Color.FromArgb(231, 76, 60);
        static readonly Color AccOrange = Color.FromArgb(243, 156, 18);
        static readonly Color AccPurple = Color.FromArgb(142, 68, 173);
        static readonly Color LblColor = Color.FromArgb(100, 116, 139);
        static readonly Color BgPage = Color.FromArgb(241, 245, 249);
        static readonly Color CardBg = Color.White;
        static readonly Color BorderLight = Color.FromArgb(226, 232, 240);
        static readonly Color SidebarBg = Color.FromArgb(30, 41, 59);
        static readonly Color SidebarHover = Color.FromArgb(51, 65, 85);
        static readonly Color SidebarActive = Color.FromArgb(59, 130, 246);
        static readonly Color FieldFocus = Color.FromArgb(239, 246, 255);

        // ── Wizard ──
        private int _currentStep = 0;
        private readonly Panel[] _stepPanels = new Panel[4];
        private readonly Panel _sidebarPanel;
        private readonly Panel _headerPanel;
        private readonly Panel _contentHost;
        private readonly Panel _footerPanel;
        private Label _lblStepTitle;
        private Label _lblProgress;
        private Panel _progressBar;
        private Panel _progressFill;

        // ── Step labels for sidebar ──
        private readonly Label[] _sideLabels = new Label[4];
        private readonly Panel[] _sideDots = new Panel[4];

        // Sidebar step definitions
        static readonly (string Icon, string Title, Color Accent)[] Steps =
        {
            ("\U0001F4CB", "Dane i adres",    AccBlue),
            ("\U0001F4B0", "Cennik i firma",  AccGreen),
            ("\U0001F50D", "ARiMR i uwagi",   AccOrange),
            ("\U0001F3E0", "Adresy ferm",     AccPurple)
        };

        // ── Kontrolki: Step 0 ──
        TextBox edtName, edtID, edtShortName;
        TextBox edtAddress, edtPostalCode, edtCity, edtDistance;
        ComboBox cbbProvince;
        CheckBox cbIsFarmAddress;
        TextBox edtPhone1, edtPhone2, edtPhone3, edtEmail;

        // ── Kontrolki: Step 1 ──
        ComboBox cbbPriceType;
        TextBox edtAddition, edtLoss;
        CheckBox cbIncDeadConf;
        TextBox edtNIP, edtRegon, edtPesel, edtIDCard, edtIDCardAuth;
        DateTimePicker dtpIDCardDate;

        // ── Kontrolki: Step 2 ──
        TextBox edtAnimNo, edtIRZPlus;
        TextBox edtInfo1, edtInfo2, edtInfo3;

        // ── Kontrolki: Step 3 ──
        DataGridView dgvFarmAddresses;
        readonly List<FarmAddressEntry> _farmAddresses = new();

        // ── Footer ──
        CheckBox cbHalt;
        AnimatedButton btnPrev, btnNext, btnSave;

        ErrorProvider _err;
        ToolTip _tip;

        static readonly string[] Provinces =
        {
            "(brak)", "dolnoslaskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
            "lodzkie", "malopolskie", "mazowieckie", "opolskie", "podkarpackie",
            "podlaskie", "pomorskie", "slaskie", "swietokrzyskie",
            "warminsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
        };

        public string CreatedSupplierId { get; private set; } = "";

        // ══════════════════════════════════════
        //  KONSTRUKTOR
        // ══════════════════════════════════════
        public NewHodowcaForm(string connectionString, string currentUser)
        {
            _connectionString = connectionString;
            _currentUser = string.IsNullOrWhiteSpace(currentUser) ? Environment.UserName : currentUser;

            InitializeComponent();

            // Double buffer
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            Text = "Nowy hodowca / dostawca";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(860, 620);
            ClientSize = new Size(920, 680);
            BackColor = BgPage;
            Font = new Font("Segoe UI", 10f);

            WindowIconHelper.SetIcon(this);

            _err = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _tip = new ToolTip { AutoPopDelay = 6000, InitialDelay = 300, BackColor = Navy1, ForeColor = Color.White };

            // ── Build layout skeleton ──
            _headerPanel = BuildHeader();
            _sidebarPanel = BuildSidebar();
            _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, Padding = new Padding(24, 20, 24, 12) };
            _footerPanel = BuildFooter();

            // Order matters for Dock
            Controls.Add(_contentHost);
            Controls.Add(_sidebarPanel);
            Controls.Add(_footerPanel);
            Controls.Add(_headerPanel);

            // ── Build step panels ──
            _stepPanels[0] = BuildStep0_DaneAdres();
            _stepPanels[1] = BuildStep1_CennikFirma();
            _stepPanels[2] = BuildStep2_ArimrUwagi();
            _stepPanels[3] = BuildStep3_Fermy();

            foreach (var p in _stepPanels)
            {
                p.Dock = DockStyle.Fill;
                p.Visible = false;
                _contentHost.Controls.Add(p);
            }

            ShowStep(0);

            // ── Events ──
            Load += async (_, __) =>
            {
                await LoadPriceTypesAsync();
                await SuggestNewIdAsync();
                edtName.Focus();
            };

            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
                if (e.Control && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; _ = SaveAsync(); }
                if (e.Alt && e.KeyCode == Keys.Right) { e.SuppressKeyPress = true; GoNext(); }
                if (e.Alt && e.KeyCode == Keys.Left) { e.SuppressKeyPress = true; GoPrev(); }
            };
        }

        // ══════════════════════════════════════
        //  HEADER (gradient + progress)
        // ══════════════════════════════════════
        Panel BuildHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 72 };
            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(pnl.ClientRectangle, Navy1, Slate, 0f);
                g.FillRectangle(br, pnl.ClientRectangle);

                // Gold line
                using var pen = new Pen(Gold, 3);
                g.DrawLine(pen, 0, pnl.Height - 2, pnl.Width, pnl.Height - 2);

                // Title
                using var f1 = new Font("Segoe UI", 17f, FontStyle.Bold);
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                TextRenderer.DrawText(g, "Nowy hodowca / dostawca", f1,
                    new Point(220, 10), Color.White);
            };

            // Step subtitle
            _lblStepTitle = new Label
            {
                AutoSize = true,
                Location = new Point(222, 42),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(_lblStepTitle);

            // Progress bar
            _progressBar = new Panel
            {
                Height = 4,
                BackColor = Color.FromArgb(51, 65, 85),
                Dock = DockStyle.Bottom
            };
            _progressFill = new Panel
            {
                Height = 4,
                BackColor = Gold,
                Dock = DockStyle.Left,
                Width = 0
            };
            _progressBar.Controls.Add(_progressFill);
            _progressBar.Resize += (_, __) => UpdateProgress();
            pnl.Controls.Add(_progressBar);

            return pnl;
        }

        void UpdateProgress()
        {
            float pct = (_currentStep + 1f) / Steps.Length;
            _progressFill.Width = (int)(_progressBar.Width * pct);
            _lblStepTitle.Text = $"Krok {_currentStep + 1} z {Steps.Length}  —  {Steps[_currentStep].Title}";
        }

        // ══════════════════════════════════════
        //  SIDEBAR (dark nav)
        // ══════════════════════════════════════
        Panel BuildSidebar()
        {
            var pnl = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = SidebarBg };
            pnl.Paint += (s, e) =>
            {
                // Right border
                using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
                e.Graphics.DrawLine(pen, pnl.Width - 1, 0, pnl.Width - 1, pnl.Height);
            };

            int y = 16;
            for (int i = 0; i < Steps.Length; i++)
            {
                int idx = i;
                var (icon, title, accent) = Steps[i];

                // Step row panel
                var row = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(199, 52),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                // Accent dot
                var dot = new Panel
                {
                    Size = new Size(8, 8),
                    Location = new Point(18, 22),
                    BackColor = accent
                };
                MakeRound(dot);
                _sideDots[i] = dot;

                // Label
                var lbl = new Label
                {
                    Text = $"{icon}  {title}",
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    AutoSize = false,
                    Size = new Size(155, 44),
                    Location = new Point(36, 4),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                _sideLabels[i] = lbl;

                // Hover effects
                void Enter(object s, EventArgs e) { row.BackColor = SidebarHover; }
                void Leave(object s, EventArgs e) { row.BackColor = (idx == _currentStep) ? SidebarActive : Color.Transparent; }
                void Click(object s, EventArgs e) { ShowStep(idx); }

                row.MouseEnter += Enter; row.MouseLeave += Leave; row.MouseClick += Click;
                lbl.MouseEnter += Enter; lbl.MouseLeave += Leave; lbl.MouseClick += Click;
                dot.MouseEnter += Enter; dot.MouseLeave += Leave; dot.MouseClick += Click;

                row.Controls.Add(dot);
                row.Controls.Add(lbl);
                pnl.Controls.Add(row);

                y += 52;
            }

            // Bottom info
            var lblInfo = new Label
            {
                Text = "Ctrl+S  Zapisz\nAlt+\u2190\u2192   Nawigacja\nEsc       Anuluj",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = false,
                Size = new Size(180, 60),
                Location = new Point(14, y + 30),
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(lblInfo);

            return pnl;
        }

        static void MakeRound(Panel p)
        {
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(p.BackColor);
                e.Graphics.Clear(p.Parent?.BackColor ?? Color.Transparent);
                e.Graphics.FillEllipse(br, 0, 0, p.Width - 1, p.Height - 1);
            };
        }

        // ══════════════════════════════════════
        //  FOOTER (buttons)
        // ══════════════════════════════════════
        Panel BuildFooter()
        {
            var pnl = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Color.FromArgb(248, 250, 252) };
            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderLight, 1);
                e.Graphics.DrawLine(pen, 0, 0, pnl.Width, 0);
            };

            cbHalt = new CheckBox
            {
                Text = "Wstrzymany",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = AccRed,
                Location = new Point(220, 20)
            };
            _tip.SetToolTip(cbHalt, "Dostawca tymczasowo wstrzymany");

            btnPrev = new AnimatedButton("WSTECZ", Slate) { Size = new Size(110, 36) };
            btnNext = new AnimatedButton("DALEJ  \u2192", AccBlue) { Size = new Size(120, 36) };
            btnSave = new AnimatedButton("ZAPISZ", AccGreen) { Size = new Size(130, 36) };

            btnPrev.Click += (_, __) => GoPrev();
            btnNext.Click += (_, __) => GoNext();
            btnSave.Click += async (_, __) => await SaveAsync();

            pnl.Controls.Add(cbHalt);
            pnl.Controls.Add(btnPrev);
            pnl.Controls.Add(btnNext);
            pnl.Controls.Add(btnSave);

            pnl.Resize += (_, __) =>
            {
                btnSave.Location = new Point(pnl.Width - btnSave.Width - 20, 13);
                btnNext.Location = new Point(btnSave.Left - btnNext.Width - 8, 13);
                btnPrev.Location = new Point(btnNext.Left - btnPrev.Width - 8, 13);
            };

            return pnl;
        }

        // ══════════════════════════════════════
        //  WIZARD NAVIGATION
        // ══════════════════════════════════════
        void ShowStep(int idx)
        {
            if (idx < 0 || idx >= Steps.Length) return;
            _currentStep = idx;

            for (int i = 0; i < Steps.Length; i++)
            {
                _stepPanels[i].Visible = (i == idx);
                _sideLabels[i].ForeColor = (i == idx) ? Color.White : Color.FromArgb(148, 163, 184);
                _sideLabels[i].Font = (i == idx)
                    ? new Font("Segoe UI Semibold", 10.5f)
                    : new Font("Segoe UI", 10f);

                // Sidebar row bg
                var row = _sideLabels[i].Parent;
                row.BackColor = (i == idx) ? SidebarActive : Color.Transparent;

                // Dot size
                _sideDots[i].Size = (i == idx) ? new Size(10, 10) : new Size(8, 8);
                _sideDots[i].Location = (i == idx) ? new Point(17, 21) : new Point(18, 22);
            }

            btnPrev.Visible = idx > 0;
            btnNext.Visible = idx < Steps.Length - 1;

            UpdateProgress();
        }

        void GoNext() { if (_currentStep < Steps.Length - 1) ShowStep(_currentStep + 1); }
        void GoPrev() { if (_currentStep > 0) ShowStep(_currentStep - 1); }

        // ══════════════════════════════════════
        //  STEP 0: DANE + ADRES + KONTAKT
        // ══════════════════════════════════════
        Panel BuildStep0_DaneAdres()
        {
            var host = new Panel { AutoScroll = true, BackColor = BgPage };

            edtName = Tb(100); edtID = Tb(20); edtShortName = Tb(50);
            edtAddress = Tb(100); edtPostalCode = Tb(10); edtCity = Tb(50);
            edtDistance = Tb(10); cbIsFarmAddress = Cb("Adres jest adresem fermy");
            cbbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Font };
            foreach (var p in Provinces) cbbProvince.Items.Add(p);
            cbbProvince.SelectedIndex = 0;
            edtPhone1 = Tb(20); edtPhone2 = Tb(20); edtPhone3 = Tb(20); edtEmail = Tb(100);

            AttachDecimalFilter(edtDistance);
            AttachPhoneFilter(edtPhone1); AttachPhoneFilter(edtPhone2); AttachPhoneFilter(edtPhone3);

            _tip.SetToolTip(edtName, "Pelna nazwa dostawcy/hodowcy (wymagane)");
            _tip.SetToolTip(edtID, "Unikalny symbol (np. 001) - zostanie zaproponowany automatycznie");
            _tip.SetToolTip(edtDistance, "Odleglosc od ubojni w km");

            // Card 1: Identyfikacja
            var c1 = MakeCard("Identyfikacja", AccBlue, 120);
            var g1 = Grid(6, 2);
            g1.ColumnStyles.Clear();
            g1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            g1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            g1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            g1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            g1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
            g1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            g1.Controls.Add(Lbl("Nazwa *"), 0, 0); g1.Controls.Add(S(edtName), 1, 0); g1.SetColumnSpan(edtName, 5);
            g1.Controls.Add(Lbl("Symbol *"), 0, 1); g1.Controls.Add(S(edtID), 1, 1);
            g1.Controls.Add(Lbl("Skrot"), 2, 1); g1.Controls.Add(S(edtShortName), 3, 1);
            g1.SetColumnSpan(edtShortName, 3);
            AddToCard(c1, g1);

            // Card 2: Adres
            var c2 = MakeCard("Adres", AccBlue, 155);
            var g2 = Grid(4, 3);
            g2.Controls.Add(Lbl("Ulica"), 0, 0); g2.Controls.Add(S(edtAddress), 1, 0); g2.SetColumnSpan(edtAddress, 3);
            g2.Controls.Add(Lbl("Kod poczt."), 0, 1); g2.Controls.Add(S(edtPostalCode), 1, 1);
            g2.Controls.Add(Lbl("Miejscow."), 2, 1); g2.Controls.Add(S(edtCity), 3, 1);
            g2.Controls.Add(Lbl("Wojewodzt."), 0, 2); g2.Controls.Add(S(cbbProvince), 1, 2);
            g2.Controls.Add(Lbl("KM"), 2, 2);
            var pKm = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Left | AnchorStyles.Right };
            edtDistance.Width = 70; pKm.Controls.Add(edtDistance);
            cbIsFarmAddress.Margin = new Padding(14, 4, 0, 0); pKm.Controls.Add(cbIsFarmAddress);
            g2.Controls.Add(pKm, 3, 2);
            AddToCard(c2, g2);

            // Card 3: Kontakt
            var c3 = MakeCard("Kontakt", AccBlue, 110);
            var g3 = Grid(4, 2);
            g3.Controls.Add(Lbl("Telefon 1"), 0, 0); g3.Controls.Add(S(edtPhone1), 1, 0);
            g3.Controls.Add(Lbl("Telefon 2"), 2, 0); g3.Controls.Add(S(edtPhone2), 3, 0);
            g3.Controls.Add(Lbl("Telefon 3"), 0, 1); g3.Controls.Add(S(edtPhone3), 1, 1);
            g3.Controls.Add(Lbl("Email"), 2, 1); g3.Controls.Add(S(edtEmail), 3, 1);
            AddToCard(c3, g3);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 16, 20)
            };
            flow.Controls.Add(c1); flow.Controls.Add(c2); flow.Controls.Add(c3);

            host.Controls.Add(flow);

            // Responsive width
            host.Resize += (_, __) =>
            {
                int w = Math.Max(400, host.ClientSize.Width - 48);
                foreach (Control c in flow.Controls) c.Width = w;
            };

            return host;
        }

        // ══════════════════════════════════════
        //  STEP 1: CENNIK + FIRMA
        // ══════════════════════════════════════
        Panel BuildStep1_CennikFirma()
        {
            var host = new Panel { AutoScroll = true, BackColor = BgPage };

            cbbPriceType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Font };
            edtAddition = Tb(10, "0"); edtLoss = Tb(10, "0");
            cbIncDeadConf = Cb("Padle + konfiskaty w rozliczeniu");
            edtNIP = Tb(20); edtRegon = Tb(20); edtPesel = Tb(20);
            edtIDCard = Tb(20); edtIDCardAuth = Tb(100);
            dtpIDCardDate = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Font = Font, Anchor = AnchorStyles.Left | AnchorStyles.Right };

            AttachDecimalFilter(edtAddition); AttachDecimalFilter(edtLoss);
            AttachDigitsOnlyFilter(edtNIP); AttachDigitsOnlyFilter(edtRegon); AttachDigitsOnlyFilter(edtPesel);

            _tip.SetToolTip(edtAddition, "Dodatek cenowy do bazowej ceny skupu");
            _tip.SetToolTip(edtLoss, "Procent ubytku odliczany od wagi");
            _tip.SetToolTip(edtNIP, "Numer Identyfikacji Podatkowej (10 cyfr)");
            _tip.SetToolTip(dtpIDCardDate, "Zaznacz checkbox aby ustawic date");

            // Card: Cennik
            var c1 = MakeCard("Cennik", AccGreen, 110);
            var g1 = Grid(4, 2);
            g1.Controls.Add(Lbl("Typ ceny"), 0, 0); g1.Controls.Add(S(cbbPriceType), 1, 0);
            g1.Controls.Add(Lbl("Dodatek"), 2, 0); g1.Controls.Add(S(edtAddition), 3, 0);
            g1.Controls.Add(Lbl("Ubytek"), 0, 1); g1.Controls.Add(S(edtLoss), 1, 1);
            g1.Controls.Add(cbIncDeadConf, 2, 1); g1.SetColumnSpan(cbIncDeadConf, 2);
            AddToCard(c1, g1);

            // Card: Dane firmy
            var c2 = MakeCard("Dane firmy", AccGreen, 155);
            var g2 = Grid(4, 3);
            g2.Controls.Add(Lbl("NIP"), 0, 0); g2.Controls.Add(S(edtNIP), 1, 0);
            g2.Controls.Add(Lbl("REGON"), 2, 0); g2.Controls.Add(S(edtRegon), 3, 0);
            g2.Controls.Add(Lbl("PESEL"), 0, 1); g2.Controls.Add(S(edtPesel), 1, 1);
            g2.Controls.Add(Lbl("Nr dowodu"), 2, 1); g2.Controls.Add(S(edtIDCard), 3, 1);
            g2.Controls.Add(Lbl("Wydany dnia"), 0, 2); g2.Controls.Add(dtpIDCardDate, 1, 2);
            g2.Controls.Add(Lbl("Wydany przez"), 2, 2); g2.Controls.Add(S(edtIDCardAuth), 3, 2);
            AddToCard(c2, g2);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 16, 20)
            };
            flow.Controls.Add(c1); flow.Controls.Add(c2);
            host.Controls.Add(flow);

            host.Resize += (_, __) =>
            {
                int w = Math.Max(400, host.ClientSize.Width - 48);
                foreach (Control c in flow.Controls) c.Width = w;
            };

            return host;
        }

        // ══════════════════════════════════════
        //  STEP 2: ARiMR + UWAGI
        // ══════════════════════════════════════
        Panel BuildStep2_ArimrUwagi()
        {
            var host = new Panel { AutoScroll = true, BackColor = BgPage };

            edtAnimNo = Tb(50); edtIRZPlus = Tb(50);
            edtInfo1 = Tb(200); edtInfo2 = Tb(200); edtInfo3 = Tb(200);
            AttachDigitsOnlyFilter(edtIRZPlus);

            _tip.SetToolTip(edtAnimNo, "Nr siedziby stada z rejestru ARiMR");
            _tip.SetToolTip(edtIRZPlus, "Numer w systemie IRZPlus (tylko cyfry)");

            var c1 = MakeCard("Identyfikacja ARiMR", AccOrange, 75);
            var g1 = Grid(4, 1);
            g1.Controls.Add(Lbl("Nr gosp."), 0, 0); g1.Controls.Add(S(edtAnimNo), 1, 0);
            g1.Controls.Add(Lbl("IRZPlus"), 2, 0); g1.Controls.Add(S(edtIRZPlus), 3, 0);
            AddToCard(c1, g1);

            var c2 = MakeCard("Uwagi", AccOrange, 135);
            var g2 = Grid(2, 3);
            g2.ColumnStyles.Clear();
            g2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            g2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            g2.Controls.Add(Lbl("Uwagi 1"), 0, 0); g2.Controls.Add(S(edtInfo1), 1, 0);
            g2.Controls.Add(Lbl("Uwagi 2"), 0, 1); g2.Controls.Add(S(edtInfo2), 1, 1);
            g2.Controls.Add(Lbl("Uwagi 3"), 0, 2); g2.Controls.Add(S(edtInfo3), 1, 2);
            AddToCard(c2, g2);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 16, 20)
            };
            flow.Controls.Add(c1); flow.Controls.Add(c2);
            host.Controls.Add(flow);

            host.Resize += (_, __) =>
            {
                int w = Math.Max(400, host.ClientSize.Width - 48);
                foreach (Control c in flow.Controls) c.Width = w;
            };

            return host;
        }

        // ══════════════════════════════════════
        //  STEP 3: ADRESY FERM
        // ══════════════════════════════════════
        Panel BuildStep3_Fermy()
        {
            var host = new Panel { BackColor = BgPage };

            // Info banner
            var banner = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(219, 234, 254) };
            banner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var f = new Font("Segoe UI", 9.5f);
                TextRenderer.DrawText(e.Graphics,
                    "Adresy ferm (Kind=2) dodane tutaj zostana zapisane razem z dostawca. Adres glowny (Kind=1) tworzy sie automatycznie.",
                    f, new Rectangle(12, 0, banner.Width - 24, banner.Height), Navy1, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            };

            dgvFarmAddresses = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = CardBg,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = BorderLight,
                RowTemplate = { Height = 42 }
            };
            dgvFarmAddresses.ColumnHeadersDefaultCellStyle.BackColor = Navy1;
            dgvFarmAddresses.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvFarmAddresses.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            dgvFarmAddresses.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            dgvFarmAddresses.ColumnHeadersHeight = 44;
            dgvFarmAddresses.EnableHeadersVisualStyles = false;
            dgvFarmAddresses.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
            dgvFarmAddresses.DefaultCellStyle.Padding = new Padding(10, 4, 10, 4);
            dgvFarmAddresses.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvFarmAddresses.DefaultCellStyle.SelectionForeColor = Navy1;
            dgvFarmAddresses.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            dgvFarmAddresses.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Nazwa", FillWeight = 22 },
                new DataGridViewTextBoxColumn { Name = "colAddress", HeaderText = "Adres", FillWeight = 22 },
                new DataGridViewTextBoxColumn { Name = "colCity", HeaderText = "Miejscowosc", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "colPostal", HeaderText = "Kod", FillWeight = 10 },
                new DataGridViewTextBoxColumn { Name = "colAnimNo", HeaderText = "Nr gosp.", FillWeight = 14 },
                new DataGridViewTextBoxColumn { Name = "colDistance", HeaderText = "KM", FillWeight = 8 },
                new DataGridViewCheckBoxColumn { Name = "colHalt", HeaderText = "Wstrz.", FillWeight = 7 },
            });
            dgvFarmAddresses.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditFarmAddress(); };

            // Buttons
            var pnlBtn = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(8),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            pnlBtn.Paint += (s, e) => { using var pen = new Pen(BorderLight); e.Graphics.DrawLine(pen, 0, 0, pnlBtn.Width, 0); };

            var bAdd = new AnimatedButton("Dodaj", AccGreen) { Size = new Size(90, 34) };
            var bEdit = new AnimatedButton("Edytuj", AccBlue) { Size = new Size(90, 34) };
            var bDel = new AnimatedButton("Usun", AccRed) { Size = new Size(90, 34) };
            bAdd.Click += (_, __) => AddFarmAddress();
            bEdit.Click += (_, __) => EditFarmAddress();
            bDel.Click += (_, __) => DeleteFarmAddress();
            pnlBtn.Controls.AddRange(new Control[] { bAdd, bEdit, bDel });

            host.Controls.Add(dgvFarmAddresses);
            host.Controls.Add(banner);
            host.Controls.Add(pnlBtn);

            return host;
        }

        void RefreshFarmGrid()
        {
            dgvFarmAddresses.Rows.Clear();
            foreach (var fa in _farmAddresses.Where(a => !a.Deleted))
                dgvFarmAddresses.Rows.Add(fa.Name, fa.Address, fa.City, fa.PostalCode, fa.AnimNo, fa.Distance, fa.Halt);
        }

        void AddFarmAddress()
        {
            using var dlg = new FarmAddressDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK) { _farmAddresses.Add(dlg.GetEntry()); RefreshFarmGrid(); }
        }

        void EditFarmAddress()
        {
            var active = _farmAddresses.Where(a => !a.Deleted).ToList();
            if (dgvFarmAddresses.CurrentRow == null || dgvFarmAddresses.CurrentRow.Index >= active.Count) return;
            var entry = active[dgvFarmAddresses.CurrentRow.Index];
            using var dlg = new FarmAddressDialog(entry);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var u = dlg.GetEntry();
                entry.Name = u.Name; entry.Address = u.Address; entry.PostalCode = u.PostalCode;
                entry.City = u.City; entry.ProvinceID = u.ProvinceID; entry.Distance = u.Distance;
                entry.Phone1 = u.Phone1; entry.Info1 = u.Info1; entry.AnimNo = u.AnimNo;
                entry.IRZPlus = u.IRZPlus; entry.Halt = u.Halt;
                RefreshFarmGrid();
            }
        }

        void DeleteFarmAddress()
        {
            var active = _farmAddresses.Where(a => !a.Deleted).ToList();
            if (dgvFarmAddresses.CurrentRow == null || dgvFarmAddresses.CurrentRow.Index >= active.Count) return;
            var entry = active[dgvFarmAddresses.CurrentRow.Index];
            if (MessageBox.Show($"Usunac adres \"{entry.Name}\"?", "Potwierdzenie",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            { entry.Deleted = true; RefreshFarmGrid(); }
        }

        // ══════════════════════════════════════
        //  ŁADOWANIE DANYCH
        // ══════════════════════════════════════
        async Task LoadPriceTypesAsync()
        {
            try
            {
                using var con = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("SELECT ID, Name FROM PriceType ORDER BY Name", con);
                await con.OpenAsync();
                using var rd = await cmd.ExecuteReaderAsync();
                var list = new List<KeyValuePair<int, string>>();
                while (await rd.ReadAsync())
                    list.Add(new KeyValuePair<int, string>(rd.GetInt32(0), rd.GetString(1)));
                cbbPriceType.DisplayMember = "Value";
                cbbPriceType.ValueMember = "Key";
                cbbPriceType.DataSource = list;
                if (list.Count > 0) cbbPriceType.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ToastNotification.Show(this, "Blad ladowania typow cen: " + ex.Message, ToastNotification.ToastType.Error);
            }
        }

        async Task SuggestNewIdAsync()
        {
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                edtID.Text = await GenerateSmallestFreeIdAsync(con);
            }
            catch { }
        }

        // ══════════════════════════════════════
        //  WALIDACJA
        // ══════════════════════════════════════
        bool ValidateAll()
        {
            _err.Clear();
            bool ok = true;
            void Fail(Control c, string msg, int step) { _err.SetError(c, msg); if (ok) { ShowStep(step); c.Focus(); ok = false; } }

            if (!string.IsNullOrWhiteSpace(edtAddition.Text) && !TryDec(edtAddition.Text, out _)) Fail(edtAddition, "Bledna wartosc dodatku!", 1);
            if (!string.IsNullOrWhiteSpace(edtLoss.Text) && !TryDec(edtLoss.Text, out _)) Fail(edtLoss, "Bledna wartosc ubytku!", 1);
            if (!string.IsNullOrWhiteSpace(edtDistance.Text) && !TryDec(edtDistance.Text, out _)) Fail(edtDistance, "Bledna wartosc KM!", 0);
            if (!string.IsNullOrWhiteSpace(edtEmail.Text) && !Regex.IsMatch(edtEmail.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) Fail(edtEmail, "Bledny email!", 0);
            if (string.IsNullOrWhiteSpace(edtID.Text)) Fail(edtID, "Symbol jest wymagany!", 0);
            if (string.IsNullOrWhiteSpace(edtName.Text)) Fail(edtName, "Nazwa jest wymagana!", 0);

            if (!ok) ToastNotification.Show(this, "Popraw oznaczone pola", ToastNotification.ToastType.Warning);
            return ok;
        }

        // ══════════════════════════════════════
        //  ZAPIS
        // ══════════════════════════════════════
        async Task SaveAsync()
        {
            if (!ValidateAll()) return;
            btnSave.Enabled = false;

            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                // Symbol unique
                using (var c = new SqlCommand("SELECT GID FROM Dostawcy WHERE ID = @ID", con))
                {
                    c.Parameters.AddWithValue("@ID", edtID.Text.Trim());
                    if (await c.ExecuteScalarAsync() is not null and not DBNull)
                    {
                        _err.SetError(edtID, "Symbol juz istnieje!"); ShowStep(0); edtID.Focus();
                        ToastNotification.Show(this, "Istnieje juz hodowca o takim symbolu!", ToastNotification.ToastType.Error);
                        return;
                    }
                }

                // Name warning
                using (var c = new SqlCommand("SELECT GID FROM Dostawcy WHERE Name = @Name", con))
                {
                    c.Parameters.AddWithValue("@Name", edtName.Text.Trim());
                    if (await c.ExecuteScalarAsync() is not null and not DBNull)
                    {
                        if (MessageBox.Show("Istnieje juz hodowca o takiej nazwie.\nCzy na pewno chcesz zapisac?",
                            "Ostrzezenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    }
                }

                using var tx = con.BeginTransaction();
                try
                {
                    // INSERT Dostawcy
                    const string sql = @"
INSERT INTO Dostawcy (GUID,ID,ShortName,Name,Nip,PriceTypeID,Addition,Loss,Address,PostalCode,City,ProvinceID,Distance,
    IsDeliverer,Phone1,Phone2,Phone3,Info1,Info2,Info3,IsFarmAddress,Email,AnimNo,IRZPlus,IncDeadConf,Halt,
    Regon,Pesel,IDCard,IDCardDate,IDCardAuth,Created,Modified)
VALUES (NEWID(),@ID,@ShortName,@Name,@Nip,@PriceTypeID,@Addition,@Loss,@Address,@PostalCode,@City,@ProvinceID,@Distance,
    1,@Phone1,@Phone2,@Phone3,@Info1,@Info2,@Info3,@IsFarmAddress,@Email,@AnimNo,@IRZPlus,@IncDeadConf,@Halt,
    @Regon,@Pesel,@IDCard,@IDCardDate,@IDCardAuth,GetDate(),GetDate());
SELECT SCOPE_IDENTITY();";

                    int gid;
                    using (var c = new SqlCommand(sql, con, tx))
                    {
                        c.Parameters.AddWithValue("@ID", edtID.Text.Trim());
                        c.Parameters.AddWithValue("@ShortName", Dbn(edtShortName.Text));
                        c.Parameters.AddWithValue("@Name", edtName.Text.Trim());
                        c.Parameters.AddWithValue("@Nip", Dbn(edtNIP.Text));
                        c.Parameters.AddWithValue("@PriceTypeID", cbbPriceType.SelectedValue is int pt ? pt : (object)DBNull.Value);
                        c.Parameters.AddWithValue("@Addition", Dec(edtAddition.Text));
                        c.Parameters.AddWithValue("@Loss", Dec(edtLoss.Text));
                        c.Parameters.AddWithValue("@Address", Dbn(edtAddress.Text));
                        c.Parameters.AddWithValue("@PostalCode", Dbn(edtPostalCode.Text));
                        c.Parameters.AddWithValue("@City", Dbn(edtCity.Text));
                        c.Parameters.AddWithValue("@ProvinceID", cbbProvince.SelectedIndex > 0 ? cbbProvince.SelectedIndex : 0);
                        c.Parameters.AddWithValue("@Distance", Dec(edtDistance.Text));
                        c.Parameters.AddWithValue("@Phone1", Dbn(edtPhone1.Text));
                        c.Parameters.AddWithValue("@Phone2", Dbn(edtPhone2.Text));
                        c.Parameters.AddWithValue("@Phone3", Dbn(edtPhone3.Text));
                        c.Parameters.AddWithValue("@Info1", Dbn(edtInfo1.Text));
                        c.Parameters.AddWithValue("@Info2", Dbn(edtInfo2.Text));
                        c.Parameters.AddWithValue("@Info3", Dbn(edtInfo3.Text));
                        c.Parameters.AddWithValue("@IsFarmAddress", cbIsFarmAddress.Checked ? 1 : 0);
                        c.Parameters.AddWithValue("@Email", Dbn(edtEmail.Text));
                        c.Parameters.AddWithValue("@AnimNo", Dbn(edtAnimNo.Text));
                        c.Parameters.AddWithValue("@IRZPlus", Dbn(edtIRZPlus.Text));
                        c.Parameters.AddWithValue("@IncDeadConf", cbIncDeadConf.Checked ? 1 : 0);
                        c.Parameters.AddWithValue("@Halt", cbHalt.Checked ? 1 : 0);
                        c.Parameters.AddWithValue("@Regon", Dbn(edtRegon.Text));
                        c.Parameters.AddWithValue("@Pesel", Dbn(edtPesel.Text));
                        c.Parameters.AddWithValue("@IDCard", Dbn(edtIDCard.Text));
                        c.Parameters.AddWithValue("@IDCardDate", dtpIDCardDate.Checked ? (object)dtpIDCardDate.Value : DBNull.Value);
                        c.Parameters.AddWithValue("@IDCardAuth", Dbn(edtIDCardAuth.Text));
                        gid = Convert.ToInt32(await c.ExecuteScalarAsync());
                    }

                    // Kind=1 auto-copy
                    const string sqlAddr = @"
INSERT INTO DostawcyAdresy (CustomerGID,Kind,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,Halt,Created,Modified)
SELECT @G,1,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,Halt,GetDate(),GetDate() FROM Dostawcy WHERE GID=@G;";
                    using (var c = new SqlCommand(sqlAddr, con, tx))
                    {
                        c.Parameters.AddWithValue("@G", gid);
                        await c.ExecuteNonQueryAsync();
                    }

                    // Kind=2 farm addresses
                    foreach (var fa in _farmAddresses.Where(a => !a.Deleted))
                    {
                        const string sqlFa = @"
INSERT INTO DostawcyAdresy (CustomerGID,Kind,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,Halt,Created,Modified)
VALUES (@G,2,@N,@A,@P,@C,@Pr,@D,@Ph,@I,@An,@Ir,@H,GetDate(),GetDate());";
                        using var c = new SqlCommand(sqlFa, con, tx);
                        c.Parameters.AddWithValue("@G", gid);
                        c.Parameters.AddWithValue("@N", Dbn(fa.Name)); c.Parameters.AddWithValue("@A", Dbn(fa.Address));
                        c.Parameters.AddWithValue("@P", Dbn(fa.PostalCode)); c.Parameters.AddWithValue("@C", Dbn(fa.City));
                        c.Parameters.AddWithValue("@Pr", fa.ProvinceID); c.Parameters.AddWithValue("@D", fa.Distance);
                        c.Parameters.AddWithValue("@Ph", Dbn(fa.Phone1)); c.Parameters.AddWithValue("@I", Dbn(fa.Info1));
                        c.Parameters.AddWithValue("@An", Dbn(fa.AnimNo)); c.Parameters.AddWithValue("@Ir", Dbn(fa.IRZPlus));
                        c.Parameters.AddWithValue("@H", fa.Halt ? 1 : 0);
                        await c.ExecuteNonQueryAsync();
                    }

                    tx.Commit();
                    CreatedSupplierId = edtID.Text.Trim();
                    ToastNotification.Show(this, $"Dodano: {edtName.Text.Trim()} (ID: {CreatedSupplierId})", ToastNotification.ToastType.Success);
                    await Task.Delay(600);
                    DialogResult = DialogResult.OK;
                }
                catch { try { tx.Rollback(); } catch { } throw; }
            }
            catch (Exception ex)
            {
                ShowErrorDebug(ex);
            }
            finally { btnSave.Enabled = true; }
        }

        private void ShowErrorDebug(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("══════════════════════════════════════════");
            sb.AppendLine("  DEBUGGER ZAPISU - NewHodowcaForm");
            sb.AppendLine("══════════════════════════════════════════");
            sb.AppendLine($"Czas: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Uzytkownik: {_currentUser}");
            sb.AppendLine();

            // Exception chain
            var current = ex;
            int depth = 0;
            while (current != null)
            {
                sb.AppendLine($"--- Wyjatek [{depth}]: {current.GetType().FullName} ---");
                sb.AppendLine($"Message: {current.Message}");
                sb.AppendLine($"Source: {current.Source}");

                if (current is SqlException sqlEx)
                {
                    sb.AppendLine($"SQL Number: {sqlEx.Number}");
                    sb.AppendLine($"SQL State: {sqlEx.State}");
                    sb.AppendLine($"SQL Class: {sqlEx.Class}");
                    sb.AppendLine($"SQL Server: {sqlEx.Server}");
                    sb.AppendLine($"SQL Procedure: {sqlEx.Procedure}");
                    sb.AppendLine($"SQL LineNumber: {sqlEx.LineNumber}");
                    if (sqlEx.Errors.Count > 0)
                    {
                        sb.AppendLine($"SQL Errors ({sqlEx.Errors.Count}):");
                        foreach (SqlError err in sqlEx.Errors)
                            sb.AppendLine($"  [{err.Number}] Linia {err.LineNumber}: {err.Message}");
                    }
                }

                sb.AppendLine($"StackTrace:\n{current.StackTrace}");
                sb.AppendLine();
                current = current.InnerException;
                depth++;
            }

            // Form field values for debugging
            sb.AppendLine("--- Wartosci pol formularza ---");
            sb.AppendLine($"ID: [{edtID.Text}]");
            sb.AppendLine($"Name: [{edtName.Text}]");
            sb.AppendLine($"ShortName: [{edtShortName.Text}]");
            sb.AppendLine($"NIP: [{edtNIP.Text}]");
            sb.AppendLine($"Address: [{edtAddress.Text}]");
            sb.AppendLine($"PostalCode: [{edtPostalCode.Text}]");
            sb.AppendLine($"City: [{edtCity.Text}]");
            sb.AppendLine($"Province: [{cbbProvince.SelectedIndex}]");
            sb.AppendLine($"Distance: [{edtDistance.Text}]");
            sb.AppendLine($"Phone1: [{edtPhone1.Text}] Phone2: [{edtPhone2.Text}] Phone3: [{edtPhone3.Text}]");
            sb.AppendLine($"Email: [{edtEmail.Text}]");
            sb.AppendLine($"PriceType: [{cbbPriceType.SelectedValue}] ({cbbPriceType.Text})");
            sb.AppendLine($"Addition: [{edtAddition.Text}] Loss: [{edtLoss.Text}]");
            sb.AppendLine($"IsFarmAddress: [{cbIsFarmAddress.Checked}] IncDeadConf: [{cbIncDeadConf.Checked}] Halt: [{cbHalt.Checked}]");
            sb.AppendLine($"AnimNo: [{edtAnimNo.Text}] IRZPlus: [{edtIRZPlus.Text}]");
            sb.AppendLine($"Regon: [{edtRegon.Text}] Pesel: [{edtPesel.Text}]");
            sb.AppendLine($"IDCard: [{edtIDCard.Text}] IDCardDate: [{(dtpIDCardDate.Checked ? dtpIDCardDate.Value.ToString() : "NULL")}] IDCardAuth: [{edtIDCardAuth.Text}]");
            sb.AppendLine($"Info1: [{edtInfo1.Text}]");
            sb.AppendLine($"Info2: [{edtInfo2.Text}]");
            sb.AppendLine($"Info3: [{edtInfo3.Text}]");
            sb.AppendLine($"FarmAddresses count: {_farmAddresses.Count} (active: {_farmAddresses.Count(a => !a.Deleted)})");
            sb.AppendLine();
            sb.AppendLine($"ConnectionString: {_connectionString}");
            sb.AppendLine("══════════════════════════════════════════");

            var debugText = sb.ToString();

            // Show debug dialog
            var dlg = new Form
            {
                Text = "Debugger bledu zapisu",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(750, 560),
                MinimumSize = new Size(500, 350),
                BackColor = Color.FromArgb(30, 30, 30),
                Font = new Font("Segoe UI", 10f)
            };

            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(180, 40, 40) };
            headerPanel.Paint += (s, ev) =>
            {
                using var f = new Font("Segoe UI Semibold", 14f);
                TextRenderer.DrawText(ev.Graphics, "  Blad zapisu - szczegoly debugowania", f, new Point(6, 12), Color.White);
            };
            dlg.Controls.Add(headerPanel);

            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = debugText,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 180),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None
            };
            dlg.Controls.Add(txt);

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(40, 40, 40) };

            var btnCopy = new AnimatedButton("Kopiuj do schowka", Color.FromArgb(52, 152, 219)) { Size = new Size(180, 36), Location = new Point(10, 7) };
            btnCopy.Click += (_, __) =>
            {
                Clipboard.SetText(debugText);
                ToastNotification.Show(dlg, "Skopiowano do schowka!", ToastNotification.ToastType.Success);
            };

            var btnClose = new AnimatedButton("Zamknij", Color.FromArgb(120, 120, 120)) { Size = new Size(100, 36) };
            bottomPanel.Resize += (_, __) => btnClose.Location = new Point(bottomPanel.Width - btnClose.Width - 10, 7);
            btnClose.Click += (_, __) => dlg.Close();

            bottomPanel.Controls.Add(btnCopy);
            bottomPanel.Controls.Add(btnClose);
            dlg.Controls.Add(bottomPanel);

            dlg.ShowDialog(this);
        }

        // ══════════════════════════════════════
        //  HELPERY BAZODANOWE
        // ══════════════════════════════════════
        static object Dbn(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : (object)s.Trim();
        static bool TryDec(string s, out decimal r) { if (string.IsNullOrWhiteSpace(s)) { r = 0; return true; } return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out r); }
        static decimal Dec(string s) => TryDec(s, out var d) ? d : 0m;

        static async Task<string> GenerateSmallestFreeIdAsync(SqlConnection con, int min = 1, int max = 999, int width = 3)
        {
            const string sql = @";WITH N(n) AS (SELECT @Min UNION ALL SELECT n+1 FROM N WHERE n<@Max),
D AS (SELECT id=CAST(CASE WHEN ID NOT LIKE '%[^0-9]%' THEN ID ELSE NULL END AS int) FROM Dostawcy)
SELECT TOP(1) RIGHT(REPLICATE('0',@Width)+CAST(N.n AS varchar(16)),@Width) FROM N LEFT JOIN D ON D.id=N.n WHERE D.id IS NULL ORDER BY N.n OPTION(MAXRECURSION 0);";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Min", min); cmd.Parameters.AddWithValue("@Max", max); cmd.Parameters.AddWithValue("@Width", width);
            var o = await cmd.ExecuteScalarAsync();
            if (o != null && o != DBNull.Value) return (string)o;
            const string sql2 = @"SELECT RIGHT(REPLICATE('0',@Width)+CAST(ISNULL(MAX(CAST(CASE WHEN ID NOT LIKE '%[^0-9]%' THEN ID END AS int)),0)+1 AS varchar(16)),@Width) FROM Dostawcy;";
            using var c2 = new SqlCommand(sql2, con); c2.Parameters.AddWithValue("@Width", width);
            return (string)(await c2.ExecuteScalarAsync())!;
        }

        // ══════════════════════════════════════
        //  FABRYKI UI
        // ══════════════════════════════════════

        TextBox Tb(int maxLen, string def = "")
        {
            var t = new TextBox { MaxLength = maxLen, Text = def, Font = new Font("Segoe UI", 10f), BackColor = CardBg, BorderStyle = BorderStyle.FixedSingle };
            t.Enter += (_, __) => { t.BackColor = FieldFocus; _err.SetError(t, ""); };
            t.Leave += (_, __) => t.BackColor = CardBg;
            return t;
        }

        static CheckBox Cb(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", 9.5f), ForeColor = LblColor, Padding = new Padding(0, 4, 0, 0) };
        static Label Lbl(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = LblColor, Font = new Font("Segoe UI", 9.5f), Padding = new Padding(0, 7, 0, 0) };
        static Control S(Control c) { c.Anchor = AnchorStyles.Left | AnchorStyles.Right; return c; }

        static TableLayoutPanel Grid(int cols, int rows)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = cols, RowCount = rows, Margin = new Padding(0) };
            for (int c = 0; c < cols; c++)
                t.ColumnStyles.Add(c % 2 == 0 ? new ColumnStyle(SizeType.Absolute, 85) : new ColumnStyle(SizeType.Percent, 50));
            return t;
        }

        /// <summary>Rounded card with colored top accent bar</summary>
        static Panel MakeCard(string title, Color accent, int contentHeight)
        {
            int totalH = contentHeight + 38;
            var card = new Panel { Width = 600, Height = totalH, Margin = new Padding(0, 0, 0, 12) };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);

                // Shadow
                using (var sb = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
                    g.FillRectangle(sb, new Rectangle(3, 3, rect.Width, rect.Height));

                // Card body
                using var path = RoundedRect(rect, 8);
                using (var br = new SolidBrush(CardBg)) g.FillPath(br, path);
                using (var pen = new Pen(BorderLight)) g.DrawPath(pen, path);

                // Top accent bar
                using var topPath = new GraphicsPath();
                topPath.AddArc(rect.X, rect.Y, 16, 16, 180, 90);
                topPath.AddArc(rect.Right - 16, rect.Y, 16, 16, 270, 90);
                topPath.AddLine(rect.Right, rect.Y + 4, rect.Right, rect.Y + 4);
                topPath.AddLine(rect.X, rect.Y + 4, rect.X, rect.Y + 4);
                topPath.CloseFigure();
                using (var ab = new SolidBrush(accent)) g.FillPath(ab, topPath);

                // Title
                using var f = new Font("Segoe UI Semibold", 10.5f);
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                TextRenderer.DrawText(g, title, f, new Point(14, 10), Navy1);
            };
            return card;
        }

        static void AddToCard(Panel card, Control content)
        {
            content.Location = new Point(10, 36);
            content.Size = new Size(card.Width - 22, card.Height - 46);
            content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            card.Controls.Add(content);
        }

        static GraphicsPath RoundedRect(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ── Key Filters ──
        static void AttachDigitsOnlyFilter(TextBox t) { t.KeyPress += (_, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; }; }
        static void AttachPhoneFilter(TextBox t) { t.KeyPress += (_, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar) && e.KeyChar != '-' && e.KeyChar != '+' && e.KeyChar != ' ') e.Handled = true; }; }
        static void AttachDecimalFilter(TextBox t) { t.KeyPress += (_, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.') e.Handled = true; }; }
    }

    // ══════════════════════════════════════
    //  MODEL ADRESU FERMY
    // ══════════════════════════════════════
    internal class FarmAddressEntry
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string City { get; set; } = "";
        public int ProvinceID { get; set; }
        public decimal Distance { get; set; }
        public string Phone1 { get; set; } = "";
        public string Info1 { get; set; } = "";
        public string AnimNo { get; set; } = "";
        public string IRZPlus { get; set; } = "";
        public bool Halt { get; set; }
        public bool Deleted { get; set; }
    }
}
