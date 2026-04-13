using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Forms
{
    public class SortableBindingList<T> : BindingList<T>
    {
        private bool _sorted;
        private PropertyDescriptor _sortProp;
        private ListSortDirection _sortDir;

        public SortableBindingList(IList<T> list) : base(list) { }

        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => _sorted;
        protected override PropertyDescriptor SortPropertyCore => _sortProp;
        protected override ListSortDirection SortDirectionCore => _sortDir;

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            _sortProp = prop;
            _sortDir = direction;
            _sorted = true;

            var items = Items as List<T>;
            if (items == null) return;

            items.Sort((a, b) =>
            {
                var va = prop.GetValue(a);
                var vb = prop.GetValue(b);
                int cmp;
                if (va == null && vb == null) cmp = 0;
                else if (va == null) cmp = -1;
                else if (vb == null) cmp = 1;
                else if (va is IComparable ca) cmp = ca.CompareTo(vb);
                else cmp = string.Compare(va.ToString(), vb.ToString(), StringComparison.OrdinalIgnoreCase);
                return direction == ListSortDirection.Descending ? -cmp : cmp;
            });

            ResetBindings();
        }

        protected override void RemoveSortCore()
        {
            _sorted = false;
            _sortProp = null;
        }
    }

    public class OpakowaniaForm : Form
    {
        // ==============================================================
        // DESIGN TOKENS — paleta, typografia, ikony
        // ==============================================================
        static class T
        {
            // Tla i powierzchnie
            public static readonly Color Bg = Color.FromArgb(249, 250, 251);            // gray-50
            public static readonly Color Surface = Color.White;
            public static readonly Color SurfaceAlt = Color.FromArgb(249, 250, 251);    // gray-50
            public static readonly Color SurfaceHover = Color.FromArgb(243, 244, 246);  // gray-100
            public static readonly Color SurfaceSelected = Color.FromArgb(238, 242, 255); // indigo-50

            // Ramki
            public static readonly Color Border = Color.FromArgb(229, 231, 235);        // gray-200
            public static readonly Color BorderStrong = Color.FromArgb(209, 213, 219);  // gray-300

            // Tekst
            public static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);      // gray-900
            public static readonly Color TextSecondary = Color.FromArgb(75, 85, 99);    // gray-600
            public static readonly Color TextMuted = Color.FromArgb(156, 163, 175);     // gray-400
            public static readonly Color TextInverse = Color.White;

            // Akcent — indygo
            public static readonly Color Accent = Color.FromArgb(79, 70, 229);          // indigo-600
            public static readonly Color AccentHover = Color.FromArgb(67, 56, 202);     // indigo-700
            public static readonly Color AccentLight = Color.FromArgb(238, 242, 255);   // indigo-50
            public static readonly Color AccentBorder = Color.FromArgb(199, 210, 254);  // indigo-200

            // Semantyczne
            public static readonly Color Success = Color.FromArgb(16, 185, 129);        // emerald-500
            public static readonly Color SuccessBg = Color.FromArgb(236, 253, 245);     // emerald-50
            public static readonly Color Danger = Color.FromArgb(239, 68, 68);          // red-500
            public static readonly Color DangerBg = Color.FromArgb(254, 242, 242);      // red-50
            public static readonly Color Warning = Color.FromArgb(245, 158, 11);        // amber-500
            public static readonly Color WarningBg = Color.FromArgb(255, 251, 235);     // amber-50

            // Ciemny header
            public static readonly Color HeaderDark = Color.FromArgb(17, 24, 39);       // gray-900
            public static readonly Color HeaderDark2 = Color.FromArgb(31, 41, 55);      // gray-800

            // Typy opakowan (akcenty karty)
            public static readonly Color KodE2 = Color.FromArgb(59, 130, 246);          // blue-500
            public static readonly Color KodH1 = Color.FromArgb(249, 115, 22);          // orange-500
            public static readonly Color KodEURO = Color.FromArgb(16, 185, 129);        // emerald-500
            public static readonly Color KodPCV = Color.FromArgb(139, 92, 246);         // violet-500
            public static readonly Color KodDREW = Color.FromArgb(245, 158, 11);        // amber-500

            // ==========================================================
            // TYPOGRAFIA
            // ==========================================================
            public static readonly Font Body = new Font("Segoe UI", 9f, FontStyle.Regular);
            public static readonly Font BodySemi = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            public static readonly Font Small = new Font("Segoe UI", 8f, FontStyle.Regular);
            public static readonly Font SmallSemi = new Font("Segoe UI Semibold", 8f, FontStyle.Regular);
            public static readonly Font Caption = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            public static readonly Font Value = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            public static readonly Font ValueBig = new Font("Segoe UI", 16f, FontStyle.Bold);
            public static readonly Font Heading = new Font("Segoe UI Semibold", 14f, FontStyle.Regular);
            public static readonly Font Display = new Font("Segoe UI", 18f, FontStyle.Bold);
            public static readonly Font CardTitle = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular);

            // ==========================================================
            // IKONY — Segoe MDL2 Assets
            // ==========================================================
            public static readonly Font IconFont = new Font("Segoe MDL2 Assets", 11f);
            public static readonly Font IconFontSmall = new Font("Segoe MDL2 Assets", 9f);
            public static readonly Font IconFontBig = new Font("Segoe MDL2 Assets", 14f);
            public const string IconRefresh = "\uE72C";
            public const string IconPdf = "\uEA90";
            public const string IconExcel = "\uE9F9";
            public const string IconPrint = "\uE749";
            public const string IconEmail = "\uE715";
            public const string IconPhone = "\uE717";
            public const string IconSearch = "\uE721";
            public const string IconChevron = "\uE76C";
            public const string IconFilter = "\uE71C";
            public const string IconAdd = "\uE710";
            public const string IconGroup = "\uE902";
            public const string IconDetails = "\uE8A9";
            public const string IconCalendar = "\uE787";
            public const string IconCheck = "\uE73E";
            public const string IconClose = "\uE711";
            public const string IconMinimize = "\uE738";
            public const string IconMaximize = "\uE739";
            public const string IconWarning = "\uE7BA";
            public const string IconSort = "\uE8CB";
        }

        // Services
        readonly OpakowaniaDataService _dataService = new();
        readonly SaldaService _saldaService = new();
        readonly ExportService _exportService = new();
        readonly string _userId;
        string _handlowiecFilter;
        bool _isAdmin;

        // State
        List<SaldoOpakowania> _saldaData = new();
        bool _loading, _suppress, _grouped;
        DateTime? _lastRefresh;
        SortKind _sortKind = SortKind.SaldoE2;
        enum SortKind { SaldoE2, Nazwa, Handlowiec, WiekDesc }

        // Details debounce
        CancellationTokenSource _detailsCts;
        SaldoOpakowania _currentDetails;

        // Title drag
        bool _drag; Point _dragPt;

        // Avatar
        Dictionary<string, string> _handlowiecMap;
        readonly Dictionary<string, Image> _avatarCache = new();
        static readonly StringFormat _fmtLeftMid = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        static readonly StringFormat _fmtRightMid = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        static readonly StringFormat _fmtCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Controls — toolbar/grid/status
        SplitContainer _split;
        Panel _titleBar, _toolbar, _statusBar, _loadingPanel;
        DateTimePicker _dtDataDo;
        ComboBox _cbHandler;
        TextBox _txtSearch;
        Button _btnRefresh, _btnPdf, _btnExcel, _btnDruk, _btnGroup;
        CheckBox _chkTylkoZSaldem, _chkZaleglosci;
        DataGridView _grid;
        Label _lblStatus, _lblLastRefresh, _emptyState;
        int _hover = -1;

        // Details panel
        Panel _details, _detBody, _detHeaderBg;
        Label _detHeader, _detSub, _detContact;
        Label[] _detSaldoLabels;
        Label[] _detSaldoUnits;
        Panel[] _detSaldoCards;
        DataGridView _gridDocs;
        Button _btnDetAdd, _btnDetPrint, _btnDetEmail, _btnDetFull;
        Label _detPlaceholder;

        // Timers
        System.Windows.Forms.Timer _refreshLabelTimer;

        public OpakowaniaForm()
        {
            _userId = App.UserID ?? "11111";
            _isAdmin = _userId == "11111";
            Build();
            Load += OnLoadAsync;
        }

        async void OnLoadAsync(object sender, EventArgs e)
        {
            // Filtr handlowca
            _handlowiecFilter = _isAdmin ? null : await _dataService.PobierzHandlowcaPoUserIdAsync(_userId);
            _cbHandler.Visible = _isAdmin;

            await Reload();
        }

        // ==============================================================
        // DRAW HELPERS
        // ==============================================================

        static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void FillRounded(Graphics g, Rectangle r, int radius, Color fill)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = RoundedRect(r, radius);
            using var br = new SolidBrush(fill);
            g.FillPath(br, p);
        }

        static void DrawRoundedBorder(Graphics g, Rectangle r, int radius, Color color, int width = 1)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = RoundedRect(r, radius);
            using var pen = new Pen(color, width);
            g.DrawPath(pen, p);
        }

        static void DrawShadow(Graphics g, Rectangle r, int radius = 8, int spread = 4, int alpha = 12)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = spread; i > 0; i--)
            {
                var rr = new Rectangle(r.X - i / 2, r.Y + 1, r.Width + i, r.Height + i);
                using var p = RoundedRect(rr, radius + i);
                using var br = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                g.FillPath(br, p);
            }
        }

        // ==============================================================
        // AVATAR
        // ==============================================================

        #region Avatar

        async Task LoadHandlowiecMapAsync()
        {
            if (_handlowiecMap != null) return;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cn = new SqlConnection("Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;");
                await cn.OpenAsync();
                using (var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        map[r.GetString(0)] = r.GetString(1);
                using (var cmd2 = new SqlCommand("SELECT ID, ISNULL(Name, ID) FROM operators WHERE Name IS NOT NULL AND Name <> ''", cn))
                using (var r2 = await cmd2.ExecuteReaderAsync())
                    while (await r2.ReadAsync())
                    {
                        var name = r2.GetString(1);
                        if (!map.ContainsKey(name)) map[name] = r2.GetString(0);
                    }
            }
            catch { }
            _handlowiecMap = map;
        }

        Image GetHandlowiecAvatar(string handlowiecName, int size)
        {
            if (_handlowiecMap != null && _handlowiecMap.TryGetValue(handlowiecName, out var uid))
                return GetOrCreateAvatar(uid, handlowiecName, size);
            return GetOrCreateAvatar(handlowiecName, handlowiecName, size);
        }

        Image GetOrCreateAvatar(string usrId, string displayName, int size)
        {
            var key = $"{usrId}_{size}";
            if (_avatarCache.TryGetValue(key, out var cached)) return cached;
            Image avatar = null;
            try { if (UserAvatarManager.HasAvatar(usrId)) avatar = UserAvatarManager.GetAvatarRounded(usrId, size); } catch { }
            avatar ??= UserAvatarManager.GenerateDefaultAvatar(displayName ?? usrId, usrId, size);
            _avatarCache[key] = avatar;
            return avatar;
        }

        #endregion

        // ==============================================================
        // BUILD
        // ==============================================================

        void Build()
        {
            Text = "Opakowania Zwrotne";
            Size = new Size(1620, 900);
            MinimumSize = new Size(1200, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Maximized;
            BackColor = T.Bg;
            Font = T.Body;
            DoubleBuffered = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 2,
                Panel1MinSize = 50,
                Panel2MinSize = 50,
                BackColor = T.Border
            };

            BuildStatus();
            BuildToolbar();
            BuildGrid();
            BuildLoading();
            BuildDetails();

            _split.Panel1.Controls.Add(_grid);
            _split.Panel1.Controls.Add(_emptyState);
            _split.Panel1.Controls.Add(_loadingPanel);
            _split.Panel1.Controls.Add(_toolbar);
            _split.Panel2.Controls.Add(_details);

            Controls.Add(_split);
            Controls.Add(_statusBar);

            Shown += (_, __) => TrySetSplitter(700);
            _split.Resize += (_, __) => { if (_split.Panel2.Width > 10 && _split.Panel2.Width < 200) TrySetSplitter(700); };

            _refreshLabelTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _refreshLabelTimer.Tick += (_, __) => UpdateLastRefreshLabel();
            _refreshLabelTimer.Start();
        }

        void TrySetSplitter(int rightPanelWidth)
        {
            try
            {
                if (_split == null || !_split.IsHandleCreated || _split.Width < 100) return;
                var maxDist = _split.Width - _split.Panel2MinSize - _split.SplitterWidth - 1;
                var minDist = _split.Panel1MinSize;
                if (maxDist <= minDist) return;
                var target = _split.Width - rightPanelWidth - _split.SplitterWidth;
                var dist = Math.Min(maxDist, Math.Max(minDist, target));
                if (dist != _split.SplitterDistance) _split.SplitterDistance = dist;
            }
            catch (Exception ex) { Debug.WriteLine("[Split] " + ex.Message); }
        }

        void BuildTitle()
        {
            _titleBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = T.HeaderDark };
            _titleBar.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, _titleBar.Width), 42), T.HeaderDark, T.HeaderDark2, 0f))
                    g.FillRectangle(b, 0, 0, _titleBar.Width, 42);
                using (var accBr = new SolidBrush(T.Accent))
                    g.FillRectangle(accBr, 0, 0, 4, 42);
                // Uwaga: T.Heading i T.Small to STATYCZNE Fonty — NIE dispose'owac
                g.DrawString("Opakowania zwrotne", T.Heading, Brushes.White, 18, 10);
                var titleW = TextRenderer.MeasureText("Opakowania zwrotne", T.Heading).Width;
                using (var subBr = new SolidBrush(Color.FromArgb(156, 163, 175)))
                    g.DrawString("Salda odbiorcow", T.Small, subBr, 18 + titleW + 12, 16);
            };
            _titleBar.MouseDown += (_, e) => { if (e.Clicks == 2) WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; else { _drag = true; _dragPt = e.Location; } };
            _titleBar.MouseMove += (_, e) => { if (_drag) { var p = PointToScreen(e.Location); Location = new Point(p.X - _dragPt.X, p.Y - _dragPt.Y); } };
            _titleBar.MouseUp += (_, __) => _drag = false;
            var fl = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false };
            fl.Controls.Add(TBtn(T.IconMinimize, Color.FromArgb(45, 55, 72), () => WindowState = FormWindowState.Minimized));
            fl.Controls.Add(TBtn(T.IconMaximize, Color.FromArgb(45, 55, 72), () => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized));
            fl.Controls.Add(TBtn(T.IconClose, Color.FromArgb(185, 28, 28), Close));
            _titleBar.Controls.Add(fl);
        }

        Button TBtn(string icon, Color hover, Action a)
        {
            var b = new Button { Text = icon, Size = new Size(46, 42), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(209, 213, 219), BackColor = Color.Transparent, Font = T.IconFontSmall, Cursor = Cursors.Hand, TabStop = false };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.Click += (_, __) => a();
            return b;
        }

        void BuildStatus()
        {
            _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = T.SurfaceAlt };
            _statusBar.Paint += (_, e) => { using var p = new Pen(T.Border); e.Graphics.DrawLine(p, 0, 0, _statusBar.Width, 0); };
            _lblStatus = new Label { Dock = DockStyle.Fill, Text = "  F5 Odswiez    Ctrl+F Szukaj    Enter Szczegoly    Esc Zamknij", ForeColor = T.TextMuted, Font = T.Small, TextAlign = ContentAlignment.MiddleLeft };
            _statusBar.Controls.Add(_lblStatus);
        }

        void BuildLoading()
        {
            _loadingPanel = new Panel { Dock = DockStyle.Top, Height = 3, Visible = false, BackColor = T.Border };
            var pb = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            _loadingPanel.Controls.Add(pb);

            _emptyState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ladowanie salda kontrahentow...",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = T.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = T.Surface,
                Visible = true
            };
        }

        void BuildToolbar()
        {
            _toolbar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = T.Surface };
            _toolbar.Paint += (_, e) => { using var p = new Pen(T.Border); e.Graphics.DrawLine(p, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1); };

            var lf = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(14, 10, 0, 0) };

            var lblData = new Label { Text = "Saldo na:", AutoSize = true, ForeColor = T.TextSecondary, Font = T.BodySemi, Margin = new Padding(0, 9, 6, 0) };
            _dtDataDo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Font = T.Body, Value = DateTime.Today, Margin = new Padding(0, 5, 0, 0) };
            _dtDataDo.ValueChanged += async (_, __) => { if (!_suppress) await Reload(); };

            var s1 = new Panel { Size = new Size(1, 32), Margin = new Padding(12, 2, 12, 0), BackColor = T.Border };

            _txtSearch = new TextBox { Width = 240, Font = new Font("Segoe UI", 10), PlaceholderText = "Szukaj kontrahenta...", Margin = new Padding(0, 5, 0, 0) };
            _txtSearch.TextChanged += (_, __) => ApplyFilter();

            _cbHandler = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Font = T.Body, Visible = false, Margin = new Padding(8, 5, 0, 0), FlatStyle = FlatStyle.Flat };
            _cbHandler.Items.Add("Wszyscy handlowcy");
            _cbHandler.SelectedIndex = 0;
            _cbHandler.SelectedIndexChanged += async (_, __) => { if (!_suppress) await Reload(); };


            var s2 = new Panel { Size = new Size(1, 32), Margin = new Padding(12, 2, 12, 0), BackColor = T.Border };
            _chkTylkoZSaldem = new CheckBox { Text = "Z saldem", AutoSize = true, Checked = false, Margin = new Padding(0, 11, 0, 0), Font = T.Body, ForeColor = T.TextSecondary };
            _chkTylkoZSaldem.CheckedChanged += (_, __) => ApplyFilter();
            _chkZaleglosci = new CheckBox { Text = ">30 dni bez ruchu", AutoSize = true, Checked = false, Margin = new Padding(8, 11, 0, 0), Font = T.Body, ForeColor = T.TextSecondary };
            _chkZaleglosci.CheckedChanged += (_, __) => ApplyFilter();

            lf.Controls.AddRange(new Control[] { lblData, _dtDataDo, s1, _txtSearch, _cbHandler, s2, _chkTylkoZSaldem, _chkZaleglosci });

            _btnRefresh = IconBtn(T.IconRefresh, "Odswiez", T.Accent); _btnRefresh.Click += async (_, __) => await Reload();
            _btnGroup = IconBtn(T.IconGroup, "Grupuj", T.TextSecondary); _btnGroup.Click += (_, __) => ToggleGroup();
            _btnPdf = IconBtn(T.IconPdf, "PDF", T.Danger); _btnPdf.Click += async (_, __) => await DoPdf();
            _btnExcel = IconBtn(T.IconExcel, "Excel", T.Success); _btnExcel.Click += async (_, __) => await DoExcel();
            _btnDruk = IconBtn(T.IconPrint, "Drukuj", T.TextSecondary); _btnDruk.Click += (_, __) => DoPrint();

            _lblLastRefresh = new Label { Text = "—", AutoSize = true, ForeColor = T.TextMuted, Font = T.Small, Margin = new Padding(10, 13, 10, 0) };

            var rf = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0, 10, 14, 0) };
            rf.Controls.AddRange(new Control[] { _lblLastRefresh, _btnRefresh, _btnGroup, _btnPdf, _btnExcel, _btnDruk });

            _toolbar.Controls.Add(lf);
            _toolbar.Controls.Add(rf);
        }

        // Przycisk z ikona i tekstem (textless icon + label below = "pill")
        Button IconBtn(string icon, string label, Color accent)
        {
            var b = new Button
            {
                Text = " " + icon + "  " + label,
                AutoSize = false,
                Size = new Size(88, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = T.Surface,
                ForeColor = T.TextPrimary,
                Font = T.BodySemi,
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = false
            };
            b.FlatAppearance.BorderColor = T.Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = T.SurfaceHover;

            // Owner-draw: ikona (Segoe MDL2) + label w osobnych fontach
            b.Text = "";
            b.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                var rect = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
                using var p = RoundedRect(rect, 6);
                using var bg = new SolidBrush(b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)) ? T.SurfaceHover : T.Surface);
                g.FillPath(bg, p);
                using var bor = new Pen(T.Border);
                g.DrawPath(bor, p);
                // Ikona
                using var iconBr = new SolidBrush(accent);
                g.DrawString(icon, T.IconFont, iconBr, new RectangleF(8, 0, 20, b.Height), _fmtCenter);
                // Label
                using var textBr = new SolidBrush(T.TextPrimary);
                g.DrawString(label, T.BodySemi, textBr, new RectangleF(28, 0, b.Width - 32, b.Height), _fmtLeftMid);
            };
            b.MouseEnter += (_, __) => b.Invalidate();
            b.MouseLeave += (_, __) => b.Invalidate();
            return b;
        }

        // ==============================================================
        // GRID KONTRAHENTOW — KOLUMNY Z SORTOWANIEM
        // ==============================================================

        void BuildGrid()
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                ColumnHeadersVisible = true,
                BackgroundColor = T.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                GridColor = T.Border,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = T.HeaderDark,
                    ForeColor = T.TextInverse,
                    Font = new Font("Segoe UI Semibold", 10f),
                    SelectionBackColor = T.HeaderDark,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(4)
                },
                ColumnHeadersHeight = 42,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 10.5f),
                    Padding = new Padding(8, 4, 8, 4),
                    ForeColor = T.TextPrimary,
                    BackColor = T.Surface,
                    SelectionBackColor = T.SurfaceSelected,
                    SelectionForeColor = T.TextPrimary
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = T.SurfaceAlt },
                RowTemplate = { Height = 40 },
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Both
            };
            EnableDoubleBuffering(_grid);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Kontrahent", HeaderText = "KONTRAHENT", DataPropertyName = "Kontrahent",
                MinimumWidth = 220, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = { Font = new Font("Segoe UI Semibold", 10.5f), Padding = new Padding(14, 4, 4, 4) },
                SortMode = DataGridViewColumnSortMode.Automatic
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Handlowiec", HeaderText = "HANDLOWIEC", DataPropertyName = "Handlowiec",
                Width = 170, MinimumWidth = 120,
                SortMode = DataGridViewColumnSortMode.Automatic
            });

            foreach (var (name, prop) in new[] { ("E2", "SaldoE2"), ("H1", "SaldoH1"), ("EURO", "SaldoEURO"), ("PCV", "SaldoPCV"), ("DREW", "SaldoDREW") })
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name, HeaderText = name, DataPropertyName = prop,
                    Width = 110, MinimumWidth = 80,
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 11.5f, FontStyle.Bold) },
                    SortMode = DataGridViewColumnSortMode.Automatic
                });
            }

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OstDokInfo", HeaderText = "OST. DOKUMENT", DataPropertyName = "OstatniDokumentInfo",
                Width = 140, MinimumWidth = 100,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) },
                SortMode = DataGridViewColumnSortMode.Automatic
            });

            _grid.CellFormatting += GridCellFormatting;
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenFullDetails(); };
            _grid.SelectionChanged += (_, __) => ScheduleDetailsLoad();
            _grid.ContextMenuStrip = BuildCtxMenu();
        }

        void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName is "E2" or "H1" or "EURO" or "PCV" or "DREW" && e.Value is int v)
            {
                if (v == 0) { e.Value = "0"; e.CellStyle.ForeColor = T.TextMuted; }
                else { e.Value = v.ToString("N0"); e.CellStyle.ForeColor = v < 0 ? T.Danger : T.Success; }
                e.FormattingApplied = true;
            }
            if (colName == "OstDokInfo" && e.Value is string info)
            {
                if (info.Contains("Wydanie") || info.Contains("MW")) e.CellStyle.ForeColor = T.Danger;
                else if (info.Contains("Przyj") || info.Contains("MP")) e.CellStyle.ForeColor = T.Success;
                else e.CellStyle.ForeColor = T.TextMuted;
            }
        }

        // (dead code — zachowane dla kompatybilnosci)
        void PaintContractorCard(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.DataBoundItem is not SaldoOpakowania s) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Pelny obszar wiersza — karta z marginem 6px dookola
            var full = new Rectangle(e.RowBounds.X + 8, e.RowBounds.Y + 4, e.RowBounds.Width - 16, e.RowBounds.Height - 8);

            // Wyczysc tlo wiersza (bo grid jest Bg)
            using (var bgBr = new SolidBrush(T.Bg))
                g.FillRectangle(bgBr, e.RowBounds);

            bool selected = row.Selected;
            bool hovered = e.RowIndex == _hover;

            // Tlo karty
            var cardColor = selected ? T.AccentLight : T.Surface;
            var borderColor = selected ? T.Accent : (hovered ? T.BorderStrong : T.Border);
            int radius = 8;

            // Delikatny cien (tylko gdy nie zaznaczony — niech selekcja dominuje)
            if (!selected && !hovered)
            {
                using var shPath = RoundedRect(new Rectangle(full.X, full.Y + 1, full.Width, full.Height), radius);
                using var shBr = new SolidBrush(Color.FromArgb(10, 0, 0, 0));
                g.FillPath(shBr, shPath);
            }

            FillRounded(g, full, radius, cardColor);
            DrawRoundedBorder(g, full, radius, borderColor, selected ? 2 : 1);

            // Avatar (lewo)
            int avSize = 40;
            int avX = full.X + 14;
            int avY = full.Y + (full.Height - avSize) / 2;
            var img = GetHandlowiecAvatar(s.Handlowiec ?? "-", avSize);
            if (img != null)
            {
                using var pen = new Pen(Color.FromArgb(220, 220, 220), 1);
                g.DrawEllipse(pen, avX - 1, avY - 1, avSize + 1, avSize + 1);
                g.DrawImage(img, avX, avY, avSize, avSize);
            }

            // Nazwa + handlowiec (obok avatara)
            int textX = avX + avSize + 14;
            int nameW = 240;
            var nameRect = new RectangleF(textX, full.Y + 12, nameW, 22);
            using (var brName = new SolidBrush(T.TextPrimary))
                g.DrawString(s.Kontrahent ?? "-", T.CardTitle, brName, nameRect, _fmtLeftMid);
            var subRect = new RectangleF(textX, full.Y + 34, nameW, 18);
            using (var brSub = new SolidBrush(T.TextMuted))
                g.DrawString(s.Handlowiec ?? "-", T.Small, brSub, subRect, _fmtLeftMid);

            // 5 mini-sald — srodek/prawo
            int kolStart = textX + nameW + 8;
            int kolW = 82;
            (string kod, int v, Color col)[] kody = {
                ("E2", s.SaldoE2, T.KodE2),
                ("H1", s.SaldoH1, T.KodH1),
                ("EURO", s.SaldoEURO, T.KodEURO),
                ("PCV", s.SaldoPCV, T.KodPCV),
                ("DREW", s.SaldoDREW, T.KodDREW)
            };
            int availForKody = full.Right - kolStart - 60; // 60px na chevron i ost.dok
            int actualKolW = Math.Min(kolW, availForKody / 5);
            if (actualKolW < 50) actualKolW = 50;

            for (int i = 0; i < 5; i++)
            {
                int x = kolStart + i * actualKolW;
                if (x + actualKolW > full.Right - 50) break;
                var kodRect = new RectangleF(x, full.Y + 10, actualKolW, 16);
                var valRect = new RectangleF(x, full.Y + 26, actualKolW, 26);

                using (var brK = new SolidBrush(kody[i].col))
                    g.DrawString(kody[i].kod, T.Caption, brK, kodRect, _fmtCenter);

                var v = kody[i].v;
                Color valColor = v == 0 ? T.TextMuted : v < 0 ? T.Danger : T.Success;
                string valTxt = v == 0 ? "—" : FormatValShort(v);
                using var brV = new SolidBrush(valColor);
                g.DrawString(valTxt, T.Value, brV, valRect, _fmtCenter);
            }

            // Ost. dok — maly chip po prawej (nad chevronem)
            if (s.DataOstatniegoDokumentu.HasValue)
            {
                var dni = (DateTime.Today - s.DataOstatniegoDokumentu.Value).Days;
                string chipText = dni == 0 ? "dzis" : dni == 1 ? "1 dz." : dni < 30 ? $"{dni} dni" : dni < 365 ? $"{dni / 30} mc." : ">1 rok";
                Color chipFg = dni > 60 ? T.Danger : dni > 30 ? T.Warning : T.TextMuted;
                Color chipBg = dni > 60 ? T.DangerBg : dni > 30 ? T.WarningBg : T.SurfaceAlt;
                var chipSize = TextRenderer.MeasureText(chipText, T.Small);
                var chipRect = new Rectangle(full.Right - 80, full.Y + (full.Height - 22) / 2, chipSize.Width + 18, 22);
                FillRounded(g, chipRect, 11, chipBg);
                using (var brC = new SolidBrush(chipFg))
                    g.DrawString(chipText, T.Small, brC, new RectangleF(chipRect.X, chipRect.Y, chipRect.Width, chipRect.Height), _fmtCenter);
            }

            // Chevron po prawej
            using (var brCh = new SolidBrush(selected ? T.Accent : T.TextMuted))
                g.DrawString(T.IconChevron, T.IconFontSmall, brCh, new RectangleF(full.Right - 26, full.Y, 20, full.Height), _fmtCenter);
        }

        static string FormatValShort(int v)
        {
            return v.ToString("N0");
        }

        static void EnableDoubleBuffering(DataGridView dgv)
        {
            var prop = typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(dgv, true);
        }

        ContextMenuStrip BuildCtxMenu()
        {
            var m = new ContextMenuStrip { Font = T.Body };
            m.Items.Add("Pelne szczegoly (F4)", null, (_, __) => OpenFullDetails());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Eksport PDF", null, async (_, __) => await DoPdf());
            m.Items.Add("Eksport Excel", null, async (_, __) => await DoExcel());
            m.Items.Add("Drukuj karte", null, (_, __) => DoPrint());
            return m;
        }

        static string FmtSaldo(int v, string unit = "")
        {
            var num = Math.Abs(v).ToString("N0");
            if (v < 0) return $"Winny {num}{unit}";
            if (v > 0) return $"Wisimy {num}{unit}";
            return "0";
        }

        // ==============================================================
        // DETAILS PANEL
        // ==============================================================

        void BuildDetails()
        {
            _details = new Panel { Dock = DockStyle.Fill, BackColor = T.Surface };
            _details.Paint += (_, e) =>
            {
                using var p = new Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 0, 0, _details.Height);
            };

            _detPlaceholder = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Wybierz kontrahenta z listy\naby zobaczyc szczegoly",
                Font = new Font("Segoe UI", 11),
                ForeColor = T.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = T.Surface,
                Visible = true
            };

            _detBody = new Panel { Dock = DockStyle.Fill, BackColor = T.Surface, Visible = false, AutoScroll = true };

            // Header panel z gradientem
            _detHeaderBg = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = T.HeaderDark };
            _detHeaderBg.Paint += (_, e) =>
            {
                var g = e.Graphics;
                var w = Math.Max(1, _detHeaderBg.Width);
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, 104), T.HeaderDark, T.HeaderDark2, 45f))
                    g.FillRectangle(b, 0, 0, w, 104);
                using (var acc = new SolidBrush(T.Accent))
                    g.FillRectangle(acc, 0, 0, 4, 104);
            };
            _detHeader = new Label { Text = "—", AutoSize = false, Dock = DockStyle.Top, Height = 42, Font = new Font("Segoe UI Semibold", 15), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 14, 12, 0), BackColor = Color.Transparent };
            _detSub = new Label { Text = "", AutoSize = false, Dock = DockStyle.Top, Height = 22, Font = T.Body, ForeColor = Color.FromArgb(203, 213, 225), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 12, 0), BackColor = Color.Transparent };
            _detContact = new Label { Text = "", AutoSize = false, Dock = DockStyle.Top, Height = 20, Font = T.Small, ForeColor = Color.FromArgb(156, 163, 175), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 12, 0), BackColor = Color.Transparent };
            _detHeaderBg.Controls.Add(_detContact);
            _detHeaderBg.Controls.Add(_detSub);
            _detHeaderBg.Controls.Add(_detHeader);

            // Actions bar
            var actionsBar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = T.SurfaceAlt, Padding = new Padding(14, 10, 14, 10) };
            actionsBar.Paint += (_, e) => { using var p = new Pen(T.Border); e.Graphics.DrawLine(p, 0, actionsBar.Height - 1, actionsBar.Width, actionsBar.Height - 1); };
            _btnDetFull = IconBtn(T.IconDetails, "Szczegoly", T.Accent);
            _btnDetFull.Click += (_, __) => OpenFullDetails();
            _btnDetAdd = IconBtn(T.IconAdd, "Potwierdz", T.Success);
            _btnDetAdd.Click += (_, __) => AddConfirmForCurrent();
            _btnDetEmail = IconBtn(T.IconEmail, "Email", T.Accent);
            _btnDetEmail.Click += (_, __) => EmailSaldoForCurrent();
            _btnDetPrint = IconBtn(T.IconPrint, "Drukuj", T.TextSecondary);
            _btnDetPrint.Click += (_, __) => PrintDetailsCard();
            var af = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent, WrapContents = false };
            af.Controls.AddRange(new Control[] { _btnDetFull, _btnDetAdd, _btnDetEmail, _btnDetPrint });
            actionsBar.Controls.Add(af);

            // 5 kart sald — TableLayout
            var cardsHost = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = T.Surface, Padding = new Padding(14, 14, 14, 6) };
            var cardsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent };
            for (int i = 0; i < 5; i++) cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            cardsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            string[] kody = { "E2", "H1", "EURO", "PCV", "DREW" };
            string[] unity = { "szt", "pal", "pal", "pal", "pal" };
            Color[] kodColors = { T.KodE2, T.KodH1, T.KodEURO, T.KodPCV, T.KodDREW };
            _detSaldoCards = new Panel[5];
            _detSaldoLabels = new Label[5];
            _detSaldoUnits = new Label[5];

            for (int i = 0; i < 5; i++)
            {
                var c = i;
                var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4), BackColor = T.Surface };
                card.Paint += (_, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    FillRounded(g, rect, 8, T.Surface);
                    DrawRoundedBorder(g, rect, 8, T.Border);
                    // Pasek akcentu gora
                    var accRect = new Rectangle(0, 0, card.Width, 4);
                    using var p = RoundedRect(accRect, 2);
                    using var br = new SolidBrush(kodColors[c]);
                    g.FillPath(br, p);
                };
                var lblKod = new Label { Text = kody[i], Dock = DockStyle.Top, Height = 22, Font = T.Caption, ForeColor = kodColors[i], TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
                var lblUnit = new Label { Text = unity[i], Dock = DockStyle.Bottom, Height = 18, Font = T.Small, ForeColor = T.TextMuted, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
                var lblVal = new Label { Text = "—", Dock = DockStyle.Fill, Font = T.ValueBig, ForeColor = T.TextPrimary, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, AutoEllipsis = true };
                card.Controls.Add(lblVal);
                card.Controls.Add(lblUnit);
                card.Controls.Add(lblKod);
                _detSaldoCards[i] = card;
                _detSaldoLabels[i] = lblVal;
                _detSaldoUnits[i] = lblUnit;
                cardsTable.Controls.Add(card, i, 0);
            }
            cardsHost.Controls.Add(cardsTable);

            // Grid dokumentow
            var docsLabel = new Label { Text = "  OSTATNIE DOKUMENTY", Dock = DockStyle.Top, Height = 30, Font = T.Caption, ForeColor = T.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, BackColor = T.SurfaceAlt, Padding = new Padding(18, 0, 0, 0) };
            docsLabel.Paint += (_, e) => { using var p = new Pen(T.Border); e.Graphics.DrawLine(p, 0, docsLabel.Height - 1, docsLabel.Width, docsLabel.Height - 1); };

            _gridDocs = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoGenerateColumns = false, RowHeadersVisible = false,
                BackgroundColor = T.Surface, GridColor = T.Border,
                BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = T.SurfaceAlt, ForeColor = T.TextSecondary, Font = T.Caption, SelectionBackColor = T.SurfaceAlt, Padding = new Padding(8, 0, 0, 0) },
                ColumnHeadersHeight = 30, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle { Font = T.Body, Padding = new Padding(8, 2, 6, 2), ForeColor = T.TextPrimary, SelectionBackColor = T.SurfaceSelected, SelectionForeColor = T.TextPrimary },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = T.SurfaceAlt },
                RowTemplate = { Height = 28 }, EnableHeadersVisualStyles = false
            };
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Data", HeaderText = "DATA", DataPropertyName = "Data", Width = 82, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Format = "dd.MM.yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dzien", HeaderText = "DZIEN", DataPropertyName = "DzienTygodnia", Width = 55, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(100, 116, 139) } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nr", HeaderText = "NR DOKUMENTU", DataPropertyName = "NrDokumentu", MinimumWidth = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "E2", HeaderText = "E2", DataPropertyName = "E2", Width = 78, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = T.Value } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "H1", HeaderText = "H1", DataPropertyName = "H1", Width = 78, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = T.Value } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "EURO", HeaderText = "EURO", DataPropertyName = "EURO", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = T.Value } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "PCV", HeaderText = "PCV", DataPropertyName = "PCV", Width = 76, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = T.Value } });
            _gridDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "DREW", HeaderText = "DREW", DataPropertyName = "DREW", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = T.Value } });
            _gridDocs.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.Value is int iv && _gridDocs.Columns[e.ColumnIndex].Name is "E2" or "H1" or "EURO" or "PCV" or "DREW")
                {
                    if (iv == 0) { e.Value = "0"; e.CellStyle.ForeColor = T.TextMuted; }
                    else { e.CellStyle.ForeColor = iv < 0 ? T.Danger : T.Success; e.Value = (iv > 0 ? "+" : "") + iv.ToString("N0"); }
                    e.FormattingApplied = true;
                }
                // Wiersze salda — zolte tlo, bold
                if (_gridDocs.Rows[e.RowIndex].DataBoundItem is DokumentSalda ds && ds.JestSaldem)
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 251, 235);
                    e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }
            };
            EnableDoubleBuffering(_gridDocs);

            // Kolejnosc Dock: Fill (grid) na dnie, potem Top kolejno — ostatni dodany jest najwyzej
            _detBody.Controls.Add(_gridDocs);
            _detBody.Controls.Add(docsLabel);
            _detBody.Controls.Add(cardsHost);
            _detBody.Controls.Add(actionsBar);
            _detBody.Controls.Add(_detHeaderBg);

            _details.Controls.Add(_detBody);
            _details.Controls.Add(_detPlaceholder);
        }

        void ScheduleDetailsLoad()
        {
            _detailsCts?.Cancel();
            _detailsCts = new CancellationTokenSource();
            var token = _detailsCts.Token;

            if (!(_grid.CurrentRow?.DataBoundItem is SaldoOpakowania r) || r.KontrahentId <= 0)
            {
                ShowDetailsPlaceholder();
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150, token);
                    if (token.IsCancellationRequested || IsDisposed) return;
                    BeginInvoke(new Action(async () => await LoadDetailsAsync(r, token)));
                }
                catch (TaskCanceledException) { }
            });
        }

        async Task LoadDetailsAsync(SaldoOpakowania s, CancellationToken token)
        {
            if (IsDisposed || token.IsCancellationRequested) return;
            _currentDetails = s;

            _detHeader.Text = s.Kontrahent;
            _detSub.Text = "Handlowiec  ·  " + (s.Handlowiec ?? "—");
            var contact = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(s.Telefon)) contact.Append(T.IconPhone).Append("  ").Append(s.Telefon);
            if (!string.IsNullOrWhiteSpace(s.Email)) { if (contact.Length > 0) contact.Append("    "); contact.Append(T.IconEmail).Append("  ").Append(s.Email); }
            _detContact.Text = contact.Length > 0 ? contact.ToString() : "brak danych kontaktowych";
            _detContact.Font = T.Small;

            int[] vals = { s.SaldoE2, s.SaldoH1, s.SaldoEURO, s.SaldoPCV, s.SaldoDREW };
            for (int i = 0; i < 5; i++)
            {
                _detSaldoLabels[i].Text = vals[i] == 0 ? "—" : FormatValShort(vals[i]);
                _detSaldoLabels[i].ForeColor = vals[i] == 0 ? T.TextMuted : vals[i] < 0 ? T.Danger : T.Success;
                _detSaldoUnits[i].Text = vals[i] == 0 ? "—" : vals[i] < 0 ? "winny" : "wisimy";
            }

            _detPlaceholder.Visible = false;
            _detBody.Visible = true;
            _detBody.BringToFront();

            // Dokumenty — najpierw cache batch, inaczej per-kontrahent
            try
            {
                var batch = _dataService.TryGetDokumentyZBatch(s.KontrahentId, _dtDataDo.Value, GetFilter());
                if (batch != null)
                {
                    var detDocs = batch.OrderByDescending(d => d.Data).Take(30).ToList();
                    _gridDocs.DataSource = new SortableBindingList<DokumentSalda>(detDocs);
                    return;
                }

                var from = DateTime.Today.AddMonths(-6);
                var to = DateTime.Today;
                var allDocs = await _dataService.PobierzDokumentySaldaAsync(s.KontrahentId, from, to);
                if (token.IsCancellationRequested || _currentDetails != s) return;
                var detDocsFallback = allDocs.OrderByDescending(d => d.Data).Take(30).ToList();
                _gridDocs.DataSource = new SortableBindingList<DokumentSalda>(detDocsFallback);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DETAILS] BLAD: {ex.Message}");
            }
        }

        void ShowDetailsPlaceholder()
        {
            _currentDetails = null;
            _detBody.Visible = false;
            _detPlaceholder.Visible = true;
            _detPlaceholder.BringToFront();
        }

        void OpenFullDetails()
        {
            if (_grid.CurrentRow?.DataBoundItem is SaldoOpakowania s && s.KontrahentId > 0)
            {
                using var f = new SzczegolyKontrahentaForm(s.KontrahentId, s.Kontrahent, s.Handlowiec, s, _userId, _dataService);
                f.ShowDialog(this);
                _ = Reload();
            }
        }

        void AddConfirmForCurrent()
        {
            if (_currentDetails is not SaldoOpakowania s || s.KontrahentId <= 0) return;
            var typ = TypOpakowania.WszystkieTypy.FirstOrDefault(t => t.Kod == "E2");
            if (typ == null) return;
            var dlg = new Views.DodajPotwierdzenieWindow(s.KontrahentId, s.Kontrahent, s.Kontrahent, typ, s.SaldoE2, _userId);
            if (dlg.ShowDialog() == true) _ = Reload();
        }

        void EmailSaldoForCurrent()
        {
            if (_currentDetails is not SaldoOpakowania s) return;
            try
            {
                var subj = Uri.EscapeDataString($"Saldo opakowan zwrotnych - {s.Kontrahent}");
                var body = new StringBuilder();
                body.AppendLine($"Szanowni Panstwo,");
                body.AppendLine();
                body.AppendLine($"Stan sald opakowan zwrotnych na dzien {_dtDataDo.Value:dd.MM.yyyy}:");
                body.AppendLine();
                body.AppendLine($"  E2   : {FmtSaldo(s.SaldoE2, " szt")}");
                body.AppendLine($"  H1   : {FmtSaldo(s.SaldoH1, " pal")}");
                body.AppendLine($"  EURO : {FmtSaldo(s.SaldoEURO, " pal")}");
                body.AppendLine($"  PCV  : {FmtSaldo(s.SaldoPCV, " pal")}");
                body.AppendLine($"  DREW : {FmtSaldo(s.SaldoDREW, " pal")}");
                body.AppendLine();
                body.AppendLine("Prosimy o potwierdzenie salda.");
                var bodyEnc = Uri.EscapeDataString(body.ToString());
                var to = s.Email ?? "";
                var url = $"mailto:{to}?subject={subj}&body={bodyEnc}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Nie udalo sie uruchomic klienta email: " + ex.Message); }
        }

        void PrintDetailsCard()
        {
            if (_currentDetails is not SaldoOpakowania s) return;
            try
            {
                var doc = new System.Drawing.Printing.PrintDocument();
                doc.PrintPage += (_, e) =>
                {
                    var g = e.Graphics;
                    var mb = e.MarginBounds;
                    using var hFont = new Font("Segoe UI Semibold", 18);
                    using var sFont = new Font("Segoe UI", 11);
                    using var kFont = new Font("Segoe UI Semibold", 12);
                    using var vFont = new Font("Segoe UI", 14, FontStyle.Bold);
                    int y = mb.Top;
                    g.DrawString("KARTA SALDA OPAKOWAN", hFont, Brushes.Black, mb.Left, y); y += 32;
                    g.DrawString(s.Kontrahent, new Font("Segoe UI", 14, FontStyle.Bold), Brushes.Black, mb.Left, y); y += 26;
                    g.DrawString("Handlowiec: " + (s.Handlowiec ?? "—"), sFont, Brushes.DarkSlateGray, mb.Left, y); y += 20;
                    g.DrawString("Data: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"), sFont, Brushes.DarkSlateGray, mb.Left, y); y += 30;
                    using var pen = new Pen(Color.LightGray);
                    g.DrawLine(pen, mb.Left, y, mb.Right, y); y += 14;
                    (string, int, string)[] rows = { ("E2", s.SaldoE2, "szt"), ("H1", s.SaldoH1, "palet"), ("EURO", s.SaldoEURO, "palet"), ("PCV", s.SaldoPCV, "palet"), ("DREW", s.SaldoDREW, "palet") };
                    foreach (var (kod, v, u) in rows)
                    {
                        g.DrawString(kod, kFont, Brushes.Black, mb.Left + 20, y);
                        g.DrawString(FmtSaldo(v, " " + u), vFont, v < 0 ? Brushes.Firebrick : v > 0 ? Brushes.ForestGreen : Brushes.Gray, mb.Left + 120, y);
                        y += 30;
                    }
                    y += 10;
                    g.DrawLine(pen, mb.Left, y, mb.Right, y);
                    g.DrawString("Podpis: ......................................................", sFont, Brushes.Black, mb.Left + 20, y + 40);
                };
                var dlg = new PrintPreviewDialog { Document = doc, Width = 900, Height = 700 };
                dlg.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ==============================================================
        // DATA — Reload z rownoleglym batch preload (Szybkosc #2)
        // ==============================================================

        async Task Reload()
        {
            if (_loading) return;
            _loading = true;
            _loadingPanel.Visible = true;
            try
            {
                Cursor = Cursors.WaitCursor;
                var sw = Stopwatch.StartNew();

                // Salda + batch dokumentow — rownolegle
                var taskSalda = _saldaService.PobierzWszystkieSaldaAsync(_dtDataDo.Value, GetFilter());
                var taskBatch = _dataService.PreloadDokumentyBatchAsync(_dtDataDo.Value, GetFilter());

                var data = await taskSalda;
                _saldaData = data.Select(k => new SaldoOpakowania
                {
                    Kontrahent = k.Kontrahent,
                    KontrahentId = k.Id,
                    Handlowiec = k.Handlowiec ?? "-",
                    SaldoE2 = k.E2,
                    SaldoH1 = k.H1,
                    SaldoEURO = k.EURO,
                    SaldoPCV = k.PCV,
                    SaldoDREW = k.DREW,
                    Email = k.Email,
                    Telefon = k.Telefon,
                    DataOstatniegoDokumentu = k.OstatniDokument,
                    TypOstatniegoDok = k.TypOstatniegoDok
                }).ToList();

                _suppress = true;
                var hl = new HashSet<string> { "Wszyscy handlowcy" };
                foreach (var ss in _saldaData) if (!string.IsNullOrEmpty(ss.Handlowiec) && ss.Handlowiec != "-") hl.Add(ss.Handlowiec);
                var sel = _cbHandler.SelectedItem?.ToString(); _cbHandler.Items.Clear();
                foreach (var h in hl.OrderBy(x => x == "Wszyscy handlowcy" ? "" : x)) _cbHandler.Items.Add(h);
                _cbHandler.SelectedItem = sel != null && _cbHandler.Items.Contains(sel) ? sel : "Wszyscy handlowcy";
                _suppress = false;

                ApplyFilter();
                sw.Stop();

                _lastRefresh = DateTime.Now;
                _lblStatus.Text = $"  {_saldaData.Count} kontrahentow w {sw.ElapsedMilliseconds} ms    F5 Odswiez    Ctrl+F Szukaj    Esc Zamknij";
                UpdateLastRefreshLabel();

                // taskBatch leci dalej w tle (fire-and-forget) — nie czekamy
                _ = taskBatch;
            }
            catch (Exception ex) { _lblStatus.Text = "  BLAD: " + ex.Message; Debug.WriteLine($"[RELOAD] {ex}"); }
            finally
            {
                _loading = false;
                Cursor = Cursors.Default;
                _loadingPanel.Visible = false;
                if (_emptyState != null && _emptyState.Visible)
                {
                    _emptyState.Visible = false;
                    _emptyState.SendToBack();
                }
            }
        }

        void UpdateLastRefreshLabel()
        {
            if (_lblLastRefresh == null) return;
            if (_lastRefresh == null) { _lblLastRefresh.Text = "—"; return; }
            var age = DateTime.Now - _lastRefresh.Value;
            string txt;
            if (age.TotalSeconds < 60) txt = $"{(int)age.TotalSeconds}s temu";
            else if (age.TotalMinutes < 60) txt = $"{(int)age.TotalMinutes} min temu";
            else txt = $"{(int)age.TotalHours} h temu";
            _lblLastRefresh.Text = txt;
            _lblLastRefresh.ForeColor = age.TotalMinutes < 10 ? T.TextMuted : age.TotalMinutes < 60 ? T.Warning : T.Danger;
        }

        string GetFilter()
        {
            if (!_isAdmin) return _handlowiecFilter;
            if (_cbHandler.SelectedItem != null && _cbHandler.SelectedItem.ToString() != "Wszyscy handlowcy") return _cbHandler.SelectedItem.ToString();
            return null;
        }

        void ApplyFilter()
        {
            var q = _txtSearch?.Text?.ToLower();
            IEnumerable<SaldoOpakowania> f = _saldaData;
            if (!string.IsNullOrWhiteSpace(q))
                f = f.Where(s => s.Kontrahent.ToLower().Contains(q) || (s.Handlowiec?.ToLower().Contains(q) ?? false));
            if (_chkTylkoZSaldem.Checked)
                f = f.Where(s => s.SaldoCalkowite != 0);
            if (_chkZaleglosci.Checked)
                f = f.Where(s => s.DataOstatniegoDokumentu == null || (DateTime.Today - s.DataOstatniegoDokumentu.Value).TotalDays > 30);

            IOrderedEnumerable<SaldoOpakowania> ordered = _sortKind switch
            {
                SortKind.Nazwa => f.OrderBy(s => s.Kontrahent),
                SortKind.Handlowiec => f.OrderBy(s => s.Handlowiec ?? "zzz").ThenBy(s => s.Kontrahent),
                SortKind.WiekDesc => f.OrderByDescending(s => s.DataOstatniegoDokumentu ?? DateTime.MinValue).ThenBy(s => s.Kontrahent),
                _ => f.OrderBy(s => s.SaldoE2).ThenBy(s => s.Kontrahent) // najwiekszy dlug E2 pierwszy
            };

            var list = _grouped ? ordered.OrderBy(s => s.Handlowiec ?? "zzz").ThenBy(s => s.Kontrahent).ToList() : ordered.ToList();

            _grid.SuspendLayout();
            _grid.DataSource = new SortableBindingList<SaldoOpakowania>(list);
            _grid.ResumeLayout();
        }

        void ToggleGroup()
        {
            _grouped = !_grouped;
            ApplyFilter();
        }

        // ==============================================================
        // EXPORT / PRINT
        // ==============================================================

        void DoPrint()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var bmp = new System.Drawing.Bitmap(_grid.Width, _grid.Height);
                _grid.DrawToBitmap(bmp, new Rectangle(0, 0, _grid.Width, _grid.Height));
                var doc = new System.Drawing.Printing.PrintDocument();
                doc.PrintPage += (_, e) => { e.Graphics.DrawImage(bmp, 50, 50, e.MarginBounds.Width, (int)(bmp.Height * ((float)e.MarginBounds.Width / bmp.Width))); };
                var dlg = new PrintPreviewDialog { Document = doc, Width = 900, Height = 700 };
                dlg.ShowDialog(this);
                bmp.Dispose();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { Cursor = Cursors.Default; }
        }

        async Task DoPdf()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var d = (_grid.DataSource as BindingList<SaldoOpakowania>)?.ToList() ?? _saldaData;
                var p = await _exportService.EksportujSaldaWszystkichDoPDFAsync(d, _dtDataDo.Value, null);
                if (MessageBox.Show($"Zapisano:\n{p}\n\nOtworzyc?", "PDF", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _exportService.OtworzPlik(p);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { Cursor = Cursors.Default; }
        }

        async Task DoExcel()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var d = (_grid.DataSource as BindingList<SaldoOpakowania>)?.ToList() ?? _saldaData;
                var p = await _exportService.EksportujDoExcelAsync(d, _dtDataDo.Value, null);
                if (MessageBox.Show($"Zapisano:\n{p}\n\nOtworzyc?", "Excel", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _exportService.OtworzPlik(p);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { Cursor = Cursors.Default; }
        }

        // ==============================================================
        // KEYS
        // ==============================================================

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F5) { _ = Reload(); return true; }
            if (keyData == Keys.F4) { OpenFullDetails(); return true; }
            if (keyData == (Keys.Control | Keys.F)) { _txtSearch?.Focus(); _txtSearch?.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.P)) { DoPrint(); return true; }
            if (keyData == (Keys.Control | Keys.E)) { _ = DoExcel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshLabelTimer?.Stop(); _refreshLabelTimer?.Dispose();
            _detailsCts?.Cancel(); _detailsCts?.Dispose();
            foreach (var img in _avatarCache.Values) img?.Dispose();
            _avatarCache.Clear();
            base.OnFormClosed(e);
        }
    }
}
