using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Forms
{
    /// <summary>
    /// BindingList z sortowaniem — standardowy BindingList nie wspiera SortCore
    /// </summary>
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
        // Services
        readonly OpakowaniaDataService _dataService = new();
        readonly SaldaService _saldaService = new();
        readonly ExportService _exportService = new();
        readonly string _userId;
        string _handlowiecFilter;
        bool _isAdmin;

        // State
        bool _allMode = true;
        string _selType = "E2";
        List<SaldoOpakowania> _saldaData = new();
        List<ZestawienieSalda> _zestData = new();
        bool _loading, _suppress, _grouped;
        int _opCount;
        readonly StringBuilder _log = new();

        // Title drag
        bool _drag; Point _dragPt;

        // Avatary — identyczna logika jak TransportMainFormImproved
        Dictionary<string, string> _handlowiecMap; // HandlowiecName → UserID
        readonly Dictionary<string, Image> _avatarCache = new();

        // Controls
        SplitContainer _split;
        Panel _titleBar, _toolbar, _typeRow, _statusBar, _diagPanel, _loadingPanel;
        Button _btnAll, _btnPerTyp;
        DateTimePicker _dtFrom, _dtTo;
        ComboBox _cbQuick, _cbHandler;
        TextBox _txtSearch;
        Button _btnRefresh, _btnPdf, _btnExcel;
        Button[] _typeBtns;
        Panel _typeSep; // separator przed type buttons w toolbarze
        DataGridView _gridAll, _gridTyp;
        Label _lblStatus, _lblDiagInfo, _lblLoading;
        TextBox _txtDiagLog;
        Button _btnTest, _btnFullTest, _btnDiagCopy;
        Timer _diagTimer;

        public OpakowaniaForm()
        {
            _userId = App.UserID ?? "11111";
            _isAdmin = _userId == "11111";
            Build();
            Load += async (_, __) =>
            {
                // Rownolegle: handlowiec filter + avatar map + dane
                var taskFilter = _isAdmin ? Task.FromResult<string>(null) : _dataService.PobierzHandlowcaPoUserIdAsync(_userId);
                var taskAvatars = LoadHandlowiecMapAsync(); // w tle, nie blokuje

                _handlowiecFilter = await taskFilter;
                _cbHandler.Visible = _isAdmin;

                await Reload();

                // Avatary dojda w tle — po zaladowaniu odswierz grid zeby je pokazal
                await taskAvatars;
                (_allMode ? _gridAll : _gridTyp).Invalidate();
            };
        }

        #region Avatar — skopiowane z TransportMainFormImproved

        async Task LoadHandlowiecMapAsync()
        {
            if (_handlowiecMap != null) return;
            _handlowiecMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cn = new SqlConnection("Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;");
                await cn.OpenAsync();

                // UserHandlowcy: HandlowiecName → UserID (identycznie jak Transport)
                using (var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        _handlowiecMap[r.GetString(0)] = r.GetString(1);

                // operators: Name → ID (fallback, identycznie jak Transport)
                using (var cmd2 = new SqlCommand("SELECT ID, ISNULL(Name, ID) FROM operators WHERE Name IS NOT NULL AND Name <> ''", cn))
                using (var r2 = await cmd2.ExecuteReaderAsync())
                    while (await r2.ReadAsync())
                    {
                        var name = r2.GetString(1);
                        if (!_handlowiecMap.ContainsKey(name))
                            _handlowiecMap[name] = r2.GetString(0);
                    }
            }
            catch { }
        }

        // Identyczne jak TransportMainFormImproved.GetHandlowiecAvatar
        Image GetHandlowiecAvatar(string handlowiecName, int size)
        {
            if (_handlowiecMap != null && _handlowiecMap.TryGetValue(handlowiecName, out var uid))
                return GetOrCreateAvatar(uid, handlowiecName, size);
            return GetOrCreateAvatar(handlowiecName, handlowiecName, size);
        }

        // Identyczne jak TransportMainFormImproved.GetOrCreateAvatar
        Image GetOrCreateAvatar(string usrId, string displayName, int size)
        {
            var key = $"{usrId}_{size}";
            if (_avatarCache.TryGetValue(key, out var cached))
                return cached;
            Image avatar = null;
            try { if (UserAvatarManager.HasAvatar(usrId)) avatar = UserAvatarManager.GetAvatarRounded(usrId, size); } catch { }
            avatar ??= UserAvatarManager.GenerateDefaultAvatar(displayName ?? usrId, usrId, size);
            _avatarCache[key] = avatar;
            return avatar;
        }

        // Rysowanie avatara w komorce grida — identycznie jak Transport.CellPainting
        void PaintAvatar(DataGridViewCellPaintingEventArgs e, string handlowiec)
        {
            e.Handled = true;
            bool sel = e.State.HasFlag(DataGridViewElementStates.Selected);
            using (var bg = new SolidBrush(sel ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor))
                e.Graphics.FillRectangle(bg, e.CellBounds);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int sz = 26, ax = e.CellBounds.Left + 6, ay = e.CellBounds.Top + (e.CellBounds.Height - sz) / 2;
            var img = GetHandlowiecAvatar(handlowiec ?? "-", sz);
            if (img != null)
            {
                using (var pen = new Pen(sel ? e.CellStyle.SelectionBackColor : Color.White, 2))
                    e.Graphics.DrawEllipse(pen, ax - 1, ay - 1, sz + 1, sz + 1);
                e.Graphics.DrawImage(img, ax, ay, sz, sz);
            }

            var tx = ax + sz + 6;
            var textBr = sel ? Brushes.White : Brushes.Black;
            var rect = new RectangleF(tx, e.CellBounds.Top, e.CellBounds.Right - tx - 4, e.CellBounds.Height);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using var fnt = new Font("Segoe UI", 9f);
            e.Graphics.DrawString(handlowiec ?? "-", fnt, textBr, rect, sf);
        }

        #endregion

        #region Build UI

        void Build()
        {
            Text = "Opakowania Zwrotne";
            Size = new Size(1550, 900);
            MinimumSize = new Size(1100, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(246, 248, 250);
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel2, SplitterDistance = 1300, Panel2MinSize = 0, SplitterWidth = 1, Panel2Collapsed = true, BackColor = Color.FromArgb(226, 232, 240) };

            BuildTitle();
            BuildStatus();
            BuildToolbar();
            BuildTypeRow();
            BuildGrids();
            BuildDiag();
            BuildLoading();

            // Dodaj type buttons do toolbara (leftFlow)
            var leftFlow = _toolbar.Controls.OfType<FlowLayoutPanel>().First();
            foreach (var b in _typeBtns) leftFlow.Controls.Add(b);

            _split.Panel1.Controls.Add(_gridAll);
            _split.Panel1.Controls.Add(_gridTyp);
            _split.Panel1.Controls.Add(_toolbar);
            _split.Panel2.Controls.Add(_diagPanel);

            Controls.Add(_split);
            Controls.Add(_statusBar);
            Controls.Add(_titleBar);
            Controls.Add(_loadingPanel);
            _loadingPanel.BringToFront();

            _diagTimer = new Timer { Interval = 3000 };
            _diagTimer.Tick += (_, __) => { try { var r = PerformanceProfiler.GenerateShortReport(); var m = System.Text.RegularExpressions.Regex.Match(r, @"(\d+)% hit rate"); if (m.Success) _lblStatus.Text = $"  Cache: {m.Groups[1].Value}%  |  Operacji: {_opCount}  |  F5: Odswiez  |  ESC: Zamknij"; } catch { } };
            _diagTimer.Start();
        }

        void BuildTitle()
        {
            _titleBar = new Panel { Dock = DockStyle.Top, Height = 40 };
            _titleBar.Paint += (_, e) =>
            {
                using var b = new LinearGradientBrush(new Rectangle(0, 0, _titleBar.Width, 40), Color.FromArgb(15, 23, 42), Color.FromArgb(24, 45, 75), 0f);
                e.Graphics.FillRectangle(b, 0, 0, _titleBar.Width, 40);
                using var f = new Font("Segoe UI Semibold", 11);
                e.Graphics.DrawString("  OPAKOWANIA ZWROTNE", f, new SolidBrush(Color.FromArgb(34, 197, 94)), 6, 9);
            };
            _titleBar.MouseDown += (_, e) => { if (e.Clicks == 2) WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; else { _drag = true; _dragPt = e.Location; } };
            _titleBar.MouseMove += (_, e) => { if (_drag) { var p = PointToScreen(e.Location); Location = new Point(p.X - _dragPt.X, p.Y - _dragPt.Y); } };
            _titleBar.MouseUp += (_, __) => _drag = false;
            var fl = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false };
            fl.Controls.Add(TBtn("—", Color.FromArgb(45, 55, 72), () => WindowState = FormWindowState.Minimized));
            fl.Controls.Add(TBtn("□", Color.FromArgb(45, 55, 72), () => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized));
            fl.Controls.Add(TBtn("✕", Color.FromArgb(185, 28, 28), Close));
            _titleBar.Controls.Add(fl);
        }

        Button TBtn(string t, Color h, Action a) { var b = new Button { Text = t, Size = new Size(42, 40), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(148, 163, 184), BackColor = Color.Transparent, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand, TabStop = false }; b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = h; b.Click += (_, __) => a(); return b; }

        void BuildStatus()
        {
            _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.FromArgb(249, 250, 251) };
            _statusBar.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(226, 232, 240)); e.Graphics.DrawLine(p, 0, 0, _statusBar.Width, 0); };
            _lblStatus = new Label { Dock = DockStyle.Fill, Text = "  F5: Odswiez  |  ESC: Zamknij  |  2x klik: Szczegoly", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8), TextAlign = ContentAlignment.MiddleLeft };
            _statusBar.Controls.Add(_lblStatus);
        }

        void BuildLoading()
        {
            _loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(180, 255, 255, 255), Visible = false };
            _lblLoading = new Label { Text = "Ladowanie danych...", AutoSize = false, Size = new Size(220, 44), Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(34, 197, 94), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.White };
            _lblLoading.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(34, 197, 94)); e.Graphics.DrawRectangle(p, 0, 0, _lblLoading.Width - 1, _lblLoading.Height - 1); };
            _loadingPanel.Controls.Add(_lblLoading);
            _loadingPanel.Resize += (_, __) => _lblLoading.Location = new Point((_loadingPanel.Width - _lblLoading.Width) / 2, (_loadingPanel.Height - _lblLoading.Height) / 2);
        }

        void BuildToolbar()
        {
            _toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
            _toolbar.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(226, 232, 240)); e.Graphics.DrawLine(p, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1); };

            var lf = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(8, 6, 0, 0) };
            _btnAll = ToolBtn("Wszystkie typy", true); _btnAll.Click += async (_, __) => { if (!_suppress && !_allMode) { _allMode = true; StyleToggle(); await SwitchView(); } };
            _btnPerTyp = ToolBtn("Per typ", false); _btnPerTyp.Click += async (_, __) => { if (!_suppress && _allMode) { _allMode = false; StyleToggle(); await SwitchView(); } };
            var s1 = new Panel { Size = new Size(1, 28), Margin = new Padding(8, 2, 8, 0), BackColor = Color.FromArgb(226, 232, 240) };
            // Domyslnie: niedziela 3 tygodnie wczesniej
            var sun3w = DateTime.Today.AddDays(-21);
            sun3w = sun3w.AddDays(-(int)sun3w.DayOfWeek); // cofnij do niedzieli
            _dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 105, Font = new Font("Segoe UI", 9), Value = sun3w, Margin = new Padding(0, 1, 0, 0) };
            _dtFrom.ValueChanged += async (_, __) => { if (!_suppress && !_allMode) await Reload(); };
            var lb = new Label { Text = "—", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Margin = new Padding(4, 5, 4, 0) };
            _dtTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 105, Font = new Font("Segoe UI", 9), Value = DateTime.Today, Margin = new Padding(0, 1, 0, 0) };
            _dtTo.ValueChanged += async (_, __) => { if (!_suppress) await Reload(); };
            _cbQuick = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(8, 1, 0, 0) };
            _cbQuick.Items.AddRange(new object[] { "Szybki filtr...", "Poprz. tydz.", "Ten tydz.", "Ten mies.", "Poprz. mies.", "30 dni", "90 dni" });
            _cbQuick.SelectedIndex = 0;
            _cbQuick.SelectedIndexChanged += (_, __) => { if (!_suppress && _cbQuick.SelectedIndex > 0) ApplyQuick(); };
            var s2 = new Panel { Size = new Size(1, 28), Margin = new Padding(8, 2, 8, 0), BackColor = Color.FromArgb(226, 232, 240) };
            _txtSearch = new TextBox { Width = 180, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Szukaj kontrahenta...", Margin = new Padding(0, 2, 0, 0) };
            _txtSearch.TextChanged += (_, __) => ApplyFilter();
            _cbHandler = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Font = new Font("Segoe UI", 8.5f), Visible = false, Margin = new Padding(6, 1, 0, 0) };
            _cbHandler.Items.Add("Wszyscy"); _cbHandler.SelectedIndex = 0;
            _cbHandler.SelectedIndexChanged += async (_, __) => { if (!_suppress) await Reload(); };
            // Separator przed typ buttons
            var sTyp = new Panel { Size = new Size(1, 28), Margin = new Padding(8, 2, 4, 0), BackColor = Color.FromArgb(226, 232, 240), Visible = false };
            lf.Controls.AddRange(new Control[] { _btnAll, _btnPerTyp, s1, _dtFrom, lb, _dtTo, _cbQuick, s2, _txtSearch, _cbHandler, sTyp });
            // Type buttons sa dodane po BuildTypeRow()
            _typeSep = sTyp;

            _btnRefresh = ABtn("Odswiez", Color.FromArgb(34, 197, 94)); _btnRefresh.Click += async (_, __) => await Reload();
            var btnGroup = ABtn("Grupuj", Color.FromArgb(124, 58, 237)); btnGroup.Click += (_, __) => ToggleGroup();
            _btnPdf = ABtn("PDF", Color.FromArgb(37, 99, 235)); _btnPdf.Click += async (_, __) => await DoPdf();
            _btnExcel = ABtn("Excel", Color.FromArgb(5, 150, 105)); _btnExcel.Click += async (_, __) => await DoExcel();
            var btnDiag = ABtn("Diag", Color.FromArgb(100, 116, 139)); btnDiag.Click += (_, __) => _split.Panel2Collapsed = !_split.Panel2Collapsed;
            var rf = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0, 7, 8, 0) };
            rf.Controls.AddRange(new Control[] { _btnRefresh, btnGroup, _btnPdf, _btnExcel, btnDiag });

            _toolbar.Controls.Add(lf);
            _toolbar.Controls.Add(rf);
        }

        Button ToolBtn(string text, bool active)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(80, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, active ? FontStyle.Bold : FontStyle.Regular), BackColor = active ? Color.FromArgb(22, 163, 74) : Color.FromArgb(241, 245, 249), ForeColor = active ? Color.White : Color.FromArgb(71, 85, 105), Cursor = Cursors.Hand, Padding = new Padding(10, 0, 10, 0) };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = active ? Color.FromArgb(16, 140, 60) : Color.FromArgb(226, 232, 240);
            return b;
        }
        Button ABtn(string text, Color bg)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(56, 30), FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(3, 0, 0, 0), Padding = new Padding(8, 0, 8, 0) };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(bg, 0.12f);
            return b;
        }
        void StyleToggle()
        {
            _btnAll.BackColor = _allMode ? Color.FromArgb(22, 163, 74) : Color.FromArgb(241, 245, 249);
            _btnAll.ForeColor = _allMode ? Color.White : Color.FromArgb(71, 85, 105);
            _btnAll.Font = new Font("Segoe UI", 8.5f, _allMode ? FontStyle.Bold : FontStyle.Regular);
            _btnPerTyp.BackColor = !_allMode ? Color.FromArgb(22, 163, 74) : Color.FromArgb(241, 245, 249);
            _btnPerTyp.ForeColor = !_allMode ? Color.White : Color.FromArgb(71, 85, 105);
            _btnPerTyp.Font = new Font("Segoe UI", 8.5f, !_allMode ? FontStyle.Bold : FontStyle.Regular);
        }

        void BuildTypeRow()
        {
            // Typ buttons wbudowane w toolbar (FlowLayoutPanel) — nie zaslaniaja grida
            _typeRow = null; // nie uzywamy osobnego panelu
            string[] codes = { "E2", "H1", "EURO", "PCV", "DREW" };
            Color[] colors = { Color.FromArgb(59, 130, 246), Color.FromArgb(249, 115, 22), Color.FromArgb(16, 185, 129), Color.FromArgb(139, 92, 246), Color.FromArgb(245, 158, 11) };
            _typeBtns = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var code = codes[i]; var color = colors[i];
                var b = new Button { Text = code, Size = new Size(48, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), BackColor = i == 0 ? color : Color.White, ForeColor = i == 0 ? Color.White : color, Cursor = Cursors.Hand, Tag = code, Visible = false, Margin = new Padding(1, 0, 1, 0) };
                b.FlatAppearance.BorderColor = color; b.FlatAppearance.BorderSize = 2;
                b.Click += async (_, __) => { _selType = code; StyleTypes(); await Reload(); };
                _typeBtns[i] = b;
            }
        }
        void StyleTypes()
        {
            Color[] c = { Color.FromArgb(59, 130, 246), Color.FromArgb(249, 115, 22), Color.FromArgb(16, 185, 129), Color.FromArgb(139, 92, 246), Color.FromArgb(245, 158, 11) };
            for (int i = 0; i < _typeBtns.Length; i++) { bool s = _typeBtns[i].Tag.ToString() == _selType; _typeBtns[i].BackColor = s ? c[i] : Color.White; _typeBtns[i].ForeColor = s ? Color.White : c[i]; }
        }

        void BuildGrids()
        {
            _gridAll = MkGrid(); _gridAll.Dock = DockStyle.Fill;
            _gridAll.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenAll(); };
            _gridAll.ContextMenuStrip = CtxMenu(true);
            _gridAll.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kontrahent", HeaderText = "Kontrahent", DataPropertyName = "Kontrahent", MinimumWidth = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridAll.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", HeaderText = "Handlowiec", DataPropertyName = "Handlowiec", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 120, SortMode = DataGridViewColumnSortMode.Automatic });
            foreach (var (n, p) in new[] { ("E2", "SaldoE2"), ("H1", "SaldoH1"), ("EURO", "SaldoEURO"), ("PCV", "SaldoPCV"), ("DREW", "SaldoDREW") })
                _gridAll.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = n, DataPropertyName = p, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridAll.Columns.Add(new DataGridViewTextBoxColumn { Name = "Razem", HeaderText = "RAZEM", DataPropertyName = "SaldoCalkowite", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridAll.Columns.Add(new DataGridViewTextBoxColumn { Name = "OstDok", HeaderText = "Ost. dok.", DataPropertyName = "OstatniDokumentInfo", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 80, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridAll.Columns.Add(new DataGridViewTextBoxColumn { Name = "OstPotw", HeaderText = "Ost. potw.", DataPropertyName = "OstatniePotwierdzenieText", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 70, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridAll.CellFormatting += FmtAll;
            _gridAll.CellPainting += (_, e) => { if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && _gridAll.Columns[e.ColumnIndex].Name == "Handlowiec" && _gridAll.Rows[e.RowIndex].DataBoundItem is SaldoOpakowania r) PaintAvatar(e, r.Handlowiec); };

            _gridTyp = MkGrid(); _gridTyp.Dock = DockStyle.Fill; _gridTyp.Visible = false;
            _gridTyp.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenTyp(); };
            _gridTyp.ContextMenuStrip = CtxMenu(false);
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kontrahent", HeaderText = "Kontrahent", DataPropertyName = "Kontrahent", MinimumWidth = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", HeaderText = "Handlowiec", DataPropertyName = "Handlowiec", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 120, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "Saldo3tyg", HeaderText = "Saldo (3 tyg.)", DataPropertyName = "IloscPierwszyZakres", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f) }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "SaldoAkt", HeaderText = "Saldo akt.", DataPropertyName = "IloscDrugiZakres", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "Zmiana", HeaderText = "Zmiana", DataPropertyName = "Roznica", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "OstDok", HeaderText = "Ost. dok.", DataPropertyName = "OstatniDokumentInfo", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 80, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.Columns.Add(new DataGridViewTextBoxColumn { Name = "Potw", HeaderText = "Potwierdzone", DataPropertyName = "DataPotwierdzeniaTekst", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 70, SortMode = DataGridViewColumnSortMode.Automatic });
            _gridTyp.CellFormatting += FmtTyp;
            _gridTyp.CellPainting += (_, e) => { if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && _gridTyp.Columns[e.ColumnIndex].Name == "Handlowiec" && _gridTyp.Rows[e.RowIndex].DataBoundItem is ZestawienieSalda r) PaintAvatar(e, r.Handlowiec); };
        }

        static void EnableDoubleBuffering(DataGridView dgv)
        {
            var prop = typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(dgv, true);
        }

        int _hover = -1;
        DataGridView MkGrid()
        {
            var g = new DataGridView
            {
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoGenerateColumns = false, RowHeadersVisible = false,
                BackgroundColor = Color.White, GridColor = Color.FromArgb(180, 180, 180),
                BorderStyle = BorderStyle.FixedSingle, CellBorderStyle = DataGridViewCellBorderStyle.Single,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(240, 240, 240), ForeColor = Color.FromArgb(30, 30, 30), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), SelectionBackColor = Color.FromArgb(240, 240, 240), Padding = new Padding(6, 0, 0, 0) },
                ColumnHeadersHeight = 36, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9.5f), Padding = new Padding(6, 2, 4, 2), SelectionBackColor = Color.FromArgb(255, 255, 220), SelectionForeColor = Color.FromArgb(15, 23, 42) },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 250) },
                RowTemplate = { Height = 36 }, EnableHeadersVisualStyles = false
            };
            g.CellMouseEnter += (_, e) => { if (e.RowIndex >= 0) { _hover = e.RowIndex; g.InvalidateRow(e.RowIndex); } };
            g.CellMouseLeave += (_, e) => { if (e.RowIndex >= 0) { var p = _hover; _hover = -1; if (p >= 0 && p < g.Rows.Count) g.InvalidateRow(p); } };
            // Zolta ramka wokol zaznaczonego wiersza (zamiast zmiany koloru czcionki/tla)
            g.RowPostPaint += (_, e) =>
            {
                if (g.Rows[e.RowIndex].Selected)
                {
                    var rect = new Rectangle(e.RowBounds.X, e.RowBounds.Y, e.RowBounds.Width - 1, e.RowBounds.Height - 1);
                    using var pen = new Pen(Color.FromArgb(250, 204, 21), 2); // zolty
                    e.Graphics.DrawRectangle(pen, rect);
                }
                if (e.RowIndex == _hover && !g.Rows[e.RowIndex].Selected)
                {
                    var rect = new Rectangle(e.RowBounds.X, e.RowBounds.Y, e.RowBounds.Width - 1, e.RowBounds.Height - 1);
                    using var pen = new Pen(Color.FromArgb(209, 213, 219), 1); // szary hover
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };
            EnableDoubleBuffering(g);
            return g;
        }

        ContextMenuStrip CtxMenu(bool allMode)
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Szczegoly", null, (_, __) => { if (allMode) OpenAll(); else OpenTyp(); });
            if (!allMode) m.Items.Add("Dodaj potwierdzenie", null, (_, __) => AddConfirm());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("PDF", null, async (_, __) => await DoPdf());
            m.Items.Add("Excel", null, async (_, __) => await DoExcel());
            return m;
        }

        static string FmtSaldo(int v, string unit = "")
        {
            // Wartosci jak Symfonia: ujemne = kontrahent winien nam, dodatnie = my winni
            var num = Math.Abs(v).ToString("N0");
            if (v < 0) return $"Winny {num}{unit}";
            if (v > 0) return $"Wisimy {num}{unit}";
            return "0";
        }

        void FmtAll(object s, DataGridViewCellFormattingEventArgs e)
        {
            var cn = _gridAll.Columns[e.ColumnIndex].Name;
            if (cn is "E2" or "H1" or "EURO" or "PCV" or "DREW" or "Razem" && e.Value is int v)
            {
                // Symfonia: ujemne = winny (czerwone), dodatnie = wisimy (zielone)
                e.CellStyle.ForeColor = v < 0 ? Color.FromArgb(220, 38, 38) : v > 0 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(180, 180, 180);
                var unit = cn == "E2" ? " pojemnikow" : cn == "Razem" ? "" : " palet";
                e.Value = FmtSaldo(v, unit);
                e.FormattingApplied = true;
            }
            if (cn == "OstDok" && e.Value is string dok)
            {
                if (dok.Contains("Wydanie")) e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38);
                else if (dok.Contains("Przyjecie")) e.CellStyle.ForeColor = Color.FromArgb(22, 163, 74);
            }
            if (_gridAll.Rows[e.RowIndex].DataBoundItem is SaldoOpakowania sal)
            {
                if (sal.MaxSaldoDodatnie >= 100) _gridAll.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                else if (sal.MaxSaldoDodatnie >= 50) _gridAll.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
            }
        }
        void FmtTyp(object s, DataGridViewCellFormattingEventArgs e)
        {
            var cn = _gridTyp.Columns[e.ColumnIndex].Name;
            if (cn is "SaldoAkt" or "Saldo3tyg" && e.Value is int v)
            {
                e.CellStyle.ForeColor = v < 0 ? Color.FromArgb(220, 38, 38) : v > 0 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(180, 180, 180);
                var unit = _selType == "E2" ? " pojemnikow" : " palet";
                e.Value = FmtSaldo(v, unit);
                e.FormattingApplied = true;
            }
            if (cn == "Zmiana" && e.Value is int z2)
            {
                e.CellStyle.ForeColor = z2 < 0 ? Color.FromArgb(220, 38, 38) : z2 > 0 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(180, 180, 180);
                var num = Math.Abs(z2).ToString("N0");
                e.Value = z2 > 0 ? $"+{num}" : z2 < 0 ? $"-{num}" : "0";
                e.FormattingApplied = true;
            }
            if (cn == "OstDok" && e.Value is string dok)
            {
                if (dok.Contains("Wydanie")) e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38);
                else if (dok.Contains("Przyjecie")) e.CellStyle.ForeColor = Color.FromArgb(22, 163, 74);
            }
            if (cn == "Potw" && _gridTyp.Rows[e.RowIndex].DataBoundItem is ZestawienieSalda z && z.JestPotwierdzone)
                e.CellStyle.BackColor = Color.FromArgb(240, 253, 244);
        }

        void BuildDiag()
        {
            _diagPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), AutoScroll = true, Padding = new Padding(14) };
            int y = 10;
            _diagPanel.Controls.Add(new Label { Text = "DIAGNOSTYKA", ForeColor = Color.FromArgb(56, 189, 248), Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = true, Location = new Point(14, y), BackColor = Color.Transparent }); y += 32;
            _lblDiagInfo = new Label { Text = "-- ms | -- rek", ForeColor = Color.FromArgb(34, 197, 94), Font = new Font("Segoe UI", 9), AutoSize = true, Location = new Point(14, y), BackColor = Color.Transparent }; _diagPanel.Controls.Add(_lblDiagInfo); y += 28;
            _txtDiagLog = new TextBox { Location = new Point(14, y), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, Size = new Size(272, 500), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.FromArgb(203, 213, 225), Font = new Font("Cascadia Code,Consolas", 8.5f), BorderStyle = BorderStyle.None, WordWrap = true, Text = "Oczekiwanie..." };
            _diagPanel.Controls.Add(_txtDiagLog); y += 510;
            _btnTest = DBtn("Szybki test", Color.FromArgb(99, 102, 241), y); _btnTest.Click += async (_, __) => await QuickTest(); y += 32;
            _btnFullTest = DBtn("Pelny test", Color.FromArgb(37, 99, 235), y); _btnFullTest.Click += async (_, __) => await FullTest(); y += 32;
            _btnDiagCopy = DBtn("Kopiuj raport", Color.FromArgb(5, 150, 105), y); _btnDiagCopy.Click += (_, __) => CopyDiag(); y += 32;
            DBtn("Reset cache", Color.FromArgb(185, 28, 28), y).Click += (_, __) => ResetDiag();
        }
        Button DBtn(string t, Color bg, int y) { var b = new Button { Text = t, Location = new Point(14, y), Size = new Size(272, 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand }; b.FlatAppearance.BorderSize = 0; _diagPanel.Controls.Add(b); return b; }

        #endregion

        #region Data

        async Task Reload()
        {
            if (_loading) return; _loading = true;
            _loadingPanel.Visible = true; _loadingPanel.BringToFront();
            try { Cursor = Cursors.WaitCursor; var sw = Stopwatch.StartNew(); if (_allMode) await LoadAll(sw); else await LoadTyp(sw); }
            catch (Exception ex) { Log($"BLAD: {ex.Message}"); }
            finally { _loading = false; Cursor = Cursors.Default; _loadingPanel.Visible = false; }
        }

        async Task LoadAll(Stopwatch swT)
        {
            var swQ = Stopwatch.StartNew();
            var data = await _saldaService.PobierzWszystkieSaldaAsync(_dtTo.Value, GetFilter());
            swQ.Stop();
            _saldaData = data.Select(k => new SaldoOpakowania { Kontrahent = k.Kontrahent, KontrahentId = k.Id, Handlowiec = k.Handlowiec ?? "-", SaldoE2 = k.E2, SaldoH1 = k.H1, SaldoEURO = k.EURO, SaldoPCV = k.PCV, SaldoDREW = k.DREW, Email = k.Email, Telefon = k.Telefon, DataOstatniegoDokumentu = k.OstatniDokument, TypOstatniegoDok = k.TypOstatniegoDok }).ToList();
            _suppress = true;
            var hl = new HashSet<string> { "Wszyscy" }; foreach (var s in _saldaData) if (!string.IsNullOrEmpty(s.Handlowiec) && s.Handlowiec != "-") hl.Add(s.Handlowiec);
            var sel = _cbHandler.SelectedItem?.ToString(); _cbHandler.Items.Clear();
            foreach (var h in hl.OrderBy(x => x == "Wszyscy" ? "" : x)) _cbHandler.Items.Add(h);
            _cbHandler.SelectedItem = sel != null && _cbHandler.Items.Contains(sel) ? sel : "Wszyscy";
            _suppress = false;
            ApplyFilter();
            swT.Stop(); _opCount++;
            LogTiming("WszystkieTypy", swT.ElapsedMilliseconds, swQ.ElapsedMilliseconds, _saldaData.Count, swQ.ElapsedMilliseconds < 50);

            // Walidacja: sprawdz sumy
            var sumE2 = _saldaData.Sum(s => s.SaldoE2);
            var sumH1 = _saldaData.Sum(s => s.SaldoH1);
            var sumEURO = _saldaData.Sum(s => s.SaldoEURO);
            var sumPCV = _saldaData.Sum(s => s.SaldoPCV);
            var sumDREW = _saldaData.Sum(s => s.SaldoDREW);
            Log($"  Sumy: E2={sumE2} H1={sumH1} EU={sumEURO} PCV={sumPCV} DR={sumDREW} | {_saldaData.Count} kontr.");

            var bezHandl = _saldaData.Count(s => s.Handlowiec == "-");
            if (bezHandl > 0) Log($"  Uwaga: {bezHandl} kontrahentow bez handlowca");
        }

        async Task LoadTyp(Stopwatch swT)
        {
            var typ = TypOpakowania.WszystkieTypy.FirstOrDefault(t => t.Kod == _selType); if (typ == null) return;
            var swQ = Stopwatch.StartNew();
            _zestData = await _dataService.PobierzZestawienieSaldAsync(_dtFrom.Value, _dtTo.Value, typ.NazwaSystemowa, GetFilter());
            swQ.Stop(); ApplyFilter(); swT.Stop(); _opCount++;
            LogTiming($"PerTyp_{_selType}", swT.ElapsedMilliseconds, swQ.ElapsedMilliseconds, _zestData.Count, swQ.ElapsedMilliseconds < 50);

            // Walidacja PerTyp
            var bezSalda = _zestData.Count(z => z.Kontrahent != "Suma" && z.IloscPierwszyZakres == 0 && z.IloscDrugiZakres == 0);
            if (bezSalda > 0) Log($"  Uwaga: {bezSalda} wierszy z zerowym saldem w obu okresach");
            var sumAkt = _zestData.Where(z => z.Kontrahent != "Suma").Sum(z => z.IloscDrugiZakres);
            Log($"  Suma sald akt.: {sumAkt} | Kontrahentow: {_zestData.Count(z => z.Kontrahent != "Suma")}");
        }

        string GetFilter() { if (!_isAdmin) return _handlowiecFilter; if (_cbHandler.SelectedItem != null && _cbHandler.SelectedItem.ToString() != "Wszyscy") return _cbHandler.SelectedItem.ToString(); return null; }

        void ApplyFilter()
        {
            var q = _txtSearch.Text?.ToLower();
            if (_allMode)
            {
                var f = string.IsNullOrWhiteSpace(q) ? _saldaData : _saldaData.Where(s => s.Kontrahent.ToLower().Contains(q) || (s.Handlowiec?.ToLower().Contains(q) ?? false)).ToList();
                if (_grouped)
                    f = f.OrderBy(s => s.Handlowiec ?? "zzz").ThenBy(s => s.Kontrahent).ToList();
                _gridAll.SuspendLayout();
                _gridAll.DataSource = new SortableBindingList<SaldoOpakowania>(f);
                _gridAll.ResumeLayout();
            }
            else
            {
                var f = string.IsNullOrWhiteSpace(q) ? _zestData : _zestData.Where(z => z.Kontrahent.ToLower().Contains(q) || z.Handlowiec.ToLower().Contains(q)).ToList();
                if (_grouped)
                    f = f.OrderBy(z => z.Handlowiec ?? "zzz").ThenBy(z => z.Kontrahent).ToList();
                _gridTyp.SuspendLayout();
                _gridTyp.DataSource = new SortableBindingList<ZestawienieSalda>(f);
                _gridTyp.ResumeLayout();
            }
        }

        void ToggleGroup()
        {
            _grouped = !_grouped;
            ApplyFilter();
            // Dodaj separatory wizualne miedzy grupami handlowcow
            var grid = _allMode ? _gridAll : _gridTyp;
            if (_grouped)
            {
                string lastH = null;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    string h = null;
                    if (row.DataBoundItem is SaldoOpakowania s) h = s.Handlowiec;
                    else if (row.DataBoundItem is ZestawienieSalda z) h = z.Handlowiec;
                    if (h != null && h != lastH && lastH != null)
                        row.DividerHeight = 3; // wizualny separator
                    else
                        row.DividerHeight = 0;
                    lastH = h;
                }
            }
            else
            {
                foreach (DataGridViewRow row in grid.Rows)
                    row.DividerHeight = 0;
            }
        }

        async Task SwitchView()
        {
            _typeSep.Visible = !_allMode;
            foreach (var b in _typeBtns) b.Visible = !_allMode;
            _gridAll.Visible = _allMode;
            _gridTyp.Visible = !_allMode;
            if (!_allMode) StyleTypes();
            await Reload();
        }

        async void ApplyQuick()
        {
            _suppress = true; var d = DateTime.Today;
            switch (_cbQuick.SelectedIndex)
            {
                case 1: var p = d.AddDays(-(int)d.DayOfWeek + 1); if (d.DayOfWeek == DayOfWeek.Sunday) p = p.AddDays(-7); _dtFrom.Value = p.AddDays(-7); _dtTo.Value = p.AddDays(-1); break;
                case 2: var q = d.AddDays(-(int)d.DayOfWeek + 1); if (d.DayOfWeek == DayOfWeek.Sunday) q = q.AddDays(-7); _dtFrom.Value = q; _dtTo.Value = d; break;
                case 3: _dtFrom.Value = new DateTime(d.Year, d.Month, 1); _dtTo.Value = d; break;
                case 4: _dtFrom.Value = new DateTime(d.Year, d.Month, 1).AddMonths(-1); _dtTo.Value = new DateTime(d.Year, d.Month, 1).AddDays(-1); break;
                case 5: _dtFrom.Value = d.AddDays(-30); _dtTo.Value = d; break;
                case 6: _dtFrom.Value = d.AddDays(-90); _dtTo.Value = d; break;
            }
            _suppress = false; await Reload();
        }

        #endregion

        #region Actions

        void OpenAll() { if (_gridAll.CurrentRow?.DataBoundItem is SaldoOpakowania s && s.KontrahentId > 0) { using var f = new SzczegolyKontrahentaForm(s.KontrahentId, s.Kontrahent, s.Handlowiec, s, _userId, _dataService); f.ShowDialog(this); } }
        void OpenTyp() { if (_gridTyp.CurrentRow?.DataBoundItem is ZestawienieSalda z && z.KontrahentId > 0) { using var f = new SzczegolyKontrahentaForm(z.KontrahentId, z.Kontrahent, z.Handlowiec, null, _userId, _dataService); f.ShowDialog(this); } }
        void AddConfirm() { if (_gridTyp.CurrentRow?.DataBoundItem is ZestawienieSalda z && z.KontrahentId > 0) { var t = TypOpakowania.WszystkieTypy.FirstOrDefault(x => x.Kod == _selType); if (t != null && new Views.DodajPotwierdzenieWindow(z.KontrahentId, z.Kontrahent, z.Kontrahent, t, z.IloscDrugiZakres, _userId).ShowDialog() == true) _ = Reload(); } }
        async Task DoPdf() { try { Cursor = Cursors.WaitCursor; var d = (_gridAll.DataSource as BindingList<SaldoOpakowania>)?.ToList() ?? _saldaData; var p = await _exportService.EksportujSaldaWszystkichDoPDFAsync(d, _dtTo.Value, null); if (MessageBox.Show($"Zapisano:\n{p}\n\nOtworzyc?", "PDF", MessageBoxButtons.YesNo) == DialogResult.Yes) _exportService.OtworzPlik(p); } catch (Exception ex) { MessageBox.Show(ex.Message); } finally { Cursor = Cursors.Default; } }
        async Task DoExcel() { try { Cursor = Cursors.WaitCursor; var d = (_gridAll.DataSource as BindingList<SaldoOpakowania>)?.ToList() ?? _saldaData; var p = await _exportService.EksportujDoExcelAsync(d, _dtTo.Value, null); if (MessageBox.Show($"Zapisano:\n{p}\n\nOtworzyc?", "Excel", MessageBoxButtons.YesNo) == DialogResult.Yes) _exportService.OtworzPlik(p); } catch (Exception ex) { MessageBox.Show(ex.Message); } finally { Cursor = Cursors.Default; } }

        #endregion

        #region Diagnostics

        void LogTiming(string op, long total, long sql, int rek, bool cache)
        {
            Log($"[{op}] {rek} rek | {total}ms (SQL:{sql}ms) [{(cache ? "CACHE" : "SQL")}]");
            _lblDiagInfo.Text = $"{total} ms | {rek} rek | {(cache ? "CACHE" : "SQL")}";
            _lblDiagInfo.ForeColor = total < 200 ? Color.FromArgb(34, 197, 94) : total < 2000 ? Color.FromArgb(250, 204, 21) : Color.FromArgb(239, 68, 68);
            _lblStatus.Text = $"  {rek} rekordow w {total} ms  |  F5: Odswiez  |  ESC: Zamknij";
        }
        void Log(string msg) { _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}"); if (_log.Length > 8000) { var t = _log.ToString(); _log.Clear(); _log.Append(t[^5000..]); } if (!IsDisposed && _txtDiagLog != null) _txtDiagLog.Text = _log.ToString(); }
        async Task QuickTest() { _btnTest.Enabled = false; _log.Clear(); Log($"=== TEST: {(_allMode ? "WszystkieTypy" : _selType)} ==="); try { if (_allMode) { var sw = Stopwatch.StartNew(); var d = await _saldaService.PobierzWszystkieSaldaAsync(DateTime.Today); sw.Stop(); _opCount++; Log($"  {d.Count} rek w {sw.ElapsedMilliseconds} ms [{(sw.ElapsedMilliseconds < 100 ? "CACHE" : "SQL")}]"); } else { var t = TypOpakowania.WszystkieTypy.FirstOrDefault(x => x.Kod == _selType); if (t != null) { var sw = Stopwatch.StartNew(); var d = await _dataService.PobierzZestawienieSaldAsync(_dtFrom.Value, _dtTo.Value, t.NazwaSystemowa, null); sw.Stop(); _opCount++; Log($"  {d.Count} rek w {sw.ElapsedMilliseconds} ms [{(sw.ElapsedMilliseconds < 50 ? "CACHE" : "SQL")}]"); } } Log(SaldaService.GetCacheStatus()); } catch (Exception ex) { Log($"BLAD: {ex.Message}"); } finally { _btnTest.Enabled = true; } }
        async Task FullTest() { _btnFullTest.Enabled = false; _log.Clear(); Log("=== PELNY TEST ==="); try { var sw1 = Stopwatch.StartNew(); var s = await _saldaService.PobierzWszystkieSaldaAsync(DateTime.Today); sw1.Stop(); _opCount++; Log($"WszystkieTypy: {s.Count} rek w {sw1.ElapsedMilliseconds} ms"); foreach (var k in new[] { "E2", "H1", "EURO", "PCV", "DREW" }) { var t = TypOpakowania.WszystkieTypy.First(x => x.Kod == k); var sw = Stopwatch.StartNew(); var d = await _dataService.PobierzZestawienieSaldAsync(DateTime.Today.AddMonths(-2), DateTime.Today, t.NazwaSystemowa, null); sw.Stop(); _opCount++; Log($"  {k,-5}: {d.Count,4} rek w {sw.ElapsedMilliseconds,5} ms [{(sw.ElapsedMilliseconds < 200 ? "OK" : "WOLNE")}]"); } Log(SaldaService.GetCacheStatus()); } catch (Exception ex) { Log($"BLAD: {ex.Message}"); } finally { _btnFullTest.Enabled = true; } }
        void CopyDiag() { try { var sb = new StringBuilder(); sb.AppendLine($"=== DIAGNOSTYKA {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==="); sb.AppendLine(_txtDiagLog.Text); sb.AppendLine(); sb.AppendLine(PerformanceProfiler.GenerateReport()); sb.AppendLine(SaldaService.GetCacheStatus()); Clipboard.SetText(sb.ToString()); MessageBox.Show("Skopiowano!"); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        void ResetDiag() { if (MessageBox.Show("Zresetowac?", "Reset", MessageBoxButtons.YesNo) == DialogResult.Yes) { PerformanceProfiler.Reset(); SaldaService.InvalidateAllCaches(); OpakowaniaDataService.InvalidateZestawieniaCache(); OpakowaniaDataService.InvalidateWszystkieSaldaCache(); _log.Clear(); _opCount = 0; Log("Reset OK"); } }

        #endregion

        #region Keys

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == Keys.F5) { _ = Reload(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _diagTimer?.Stop(); _diagTimer?.Dispose();
            foreach (var img in _avatarCache.Values) img?.Dispose();
            _avatarCache.Clear();
            base.OnFormClosed(e);
        }

        #endregion
    }
}
