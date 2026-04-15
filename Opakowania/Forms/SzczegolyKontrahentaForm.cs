using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Forms
{
    public class SzczegolyKontrahentaForm : Form
    {
        readonly int _id;
        readonly string _nazwa, _handlowiec, _userId;
        readonly OpakowaniaDataService _ds;
        readonly ExportService _export = new();
        SaldoOpakowania _saldo;
        Image _avatarHandl;
        List<DokumentOpakowania> _docs = new();
        List<PotwierdzenieSalda> _potw = new();
        HashSet<DateTime> _expandedDays = new();

        // Layout
        Panel _header, _cardStrip, _infoPanel, _bottomBar, _chartHost;
        Panel _chartE2, _chartH1;
        Label[] _crdVal = new Label[5], _crdDelta = new Label[5];
        DataGridView _grid;
        List<HistoriaSaldaPunkt> _historia;
        Dictionary<string, List<HistoriaSaldaPunkt>> _historiaAll = new();
        ToolTip _chartTip;
        Panel _dateToolbar;
        DateTimePicker _dtOd, _dtDo;
        CheckBox _chkGrupujDni;
        Label _lblCount, _lblSaldoEnd;

        // Info sidebar
        Label _lblInfoNazwa, _lblInfoHandl, _lblInfoEmail, _lblInfoTel;
        Label _lblStatWyd, _lblStatPrzyj, _lblStatDni, _lblStatPotw, _lblStatBilans, _lblStatZwrot;
        Label _lblPotwStatus;
        Panel _potwHistPanel;

        // Toolbar buttons
        Button _btnCopy, _btnPdf, _btnEmail;

        // Colors
        static readonly Color CRed = Color.FromArgb(220, 38, 38);
        static readonly Color CGreen = Color.FromArgb(22, 163, 74);
        static readonly Color CGray = Color.FromArgb(156, 163, 175);
        static readonly Color CYellow = Color.FromArgb(250, 204, 21);
        static readonly Color CSlate = Color.FromArgb(15, 23, 42);
        static readonly Color CMuted = Color.FromArgb(100, 116, 139);
        static readonly Color CBorder = Color.FromArgb(226, 232, 240);
        static readonly Color CBg = Color.FromArgb(248, 250, 252);

        public SzczegolyKontrahentaForm(int id, string nazwa, string handlowiec, SaldoOpakowania saldo, string userId, OpakowaniaDataService ds)
        {
            _id = id; _nazwa = nazwa; _handlowiec = handlowiec ?? "-"; _saldo = saldo; _userId = userId; _ds = ds;
            BuildUI();
            Load += async (_, __) => await LoadAsync();
        }

        #region Build UI

        void BuildUI()
        {
            Text = $"{_nazwa} — Opakowania";
            Size = new Size(1300, 820);
            MinimumSize = new Size(950, 550);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(246, 248, 250);
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // --- Create all controls first ---
            BuildHeader();
            BuildCardStrip();
            BuildBottom();
            BuildDateToolbar();
            BuildInfoPanel();
            var gridHost = BuildGridHost();

            // --- Add in correct order: Fill first, then edges ---
            Controls.Add(gridHost);
            Controls.Add(_infoPanel);
            Controls.Add(_bottomBar);
            Controls.Add(_chartHost);
            Controls.Add(_dateToolbar);
            Controls.Add(_header);
        }

        void BuildHeader()
        {
            _header = new Panel { Dock = DockStyle.Top, Height = 58 };
            _header.Paint += (_, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new LinearGradientBrush(new Rectangle(0, 0, _header.Width, _header.Height), Color.FromArgb(15, 23, 42), Color.FromArgb(24, 45, 75), LinearGradientMode.Horizontal);
                g.FillRectangle(bg, 0, 0, _header.Width, _header.Height);

                // Avatar
                int s = 36, x = 14, y = 6;
                using (var br = new SolidBrush(Color.FromArgb(22, 163, 74))) g.FillEllipse(br, x, y, s, s);
                var ini = _nazwa.Length >= 2 ? _nazwa[..2].ToUpper() : "?";
                using (var f = new Font("Segoe UI Semibold", 12)) { var sz = g.MeasureString(ini, f); g.DrawString(ini, f, Brushes.White, x + (s - sz.Width) / 2, y + (s - sz.Height) / 2); }

                // Nazwa
                int tx = x + s + 10;
                using (var f = new Font("Segoe UI Semibold", 13)) g.DrawString(_nazwa, f, Brushes.White, tx, 4);

                // Handlowiec
                int my = 32;
                if (_avatarHandl != null) g.DrawImage(_avatarHandl, tx, my, 16, 16);
                else { using (var br = new SolidBrush(AvCol(_handlowiec))) g.FillEllipse(br, tx, my, 16, 16); var hi = Ini(_handlowiec); using var hf = new Font("Segoe UI", 5.5f, FontStyle.Bold); var hs = g.MeasureString(hi, hf); g.DrawString(hi, hf, Brushes.White, tx + (16 - hs.Width) / 2, my + (16 - hs.Height) / 2); }
                using (var f = new Font("Segoe UI", 8.5f)) using (var br = new SolidBrush(Color.FromArgb(148, 163, 184))) g.DrawString(_handlowiec, f, br, tx + 20, my);

                // Legenda + ID (prawa strona)
                using (var f = new Font("Segoe UI", 7))
                {
                    int rx = _header.Width - 190;
                    using (var br = new SolidBrush(CRed)) { g.FillRectangle(br, rx, 10, 8, 8); g.DrawString("Wydanie", f, new SolidBrush(Color.FromArgb(252,165,165)), rx + 12, 8); }
                    using (var br = new SolidBrush(CGreen)) { g.FillRectangle(br, rx, 24, 8, 8); g.DrawString("Przyjecie", f, new SolidBrush(Color.FromArgb(134,239,172)), rx + 12, 22); }
                    using (var br = new SolidBrush(CYellow)) { g.FillRectangle(br, rx, 38, 8, 8); g.DrawString("Saldo", f, new SolidBrush(Color.FromArgb(253,224,71)), rx + 12, 36); }
                    g.DrawString($"ID: {_id}", f, new SolidBrush(Color.FromArgb(71,85,105)), _header.Width - 65, 10);
                }
            };
        }

        void BuildCardStrip()
        {
            _cardStrip = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White };
            _cardStrip.Paint += (_, e) => { using var p = new Pen(CBorder); e.Graphics.DrawLine(p, 0, _cardStrip.Height - 1, _cardStrip.Width, _cardStrip.Height - 1); };
            string[] cn = { "E2", "H1", "EURO", "PCV", "DREW" };
            Color[] cc = { Color.FromArgb(59,130,246), Color.FromArgb(249,115,22), Color.FromArgb(16,185,129), Color.FromArgb(139,92,246), Color.FromArgb(245,158,11) };
            for (int i = 0; i < 5; i++)
            {
                var card = new Panel { BackColor = Color.Transparent, Tag = cc[i] };
                card.Paint += (s, e) => { var cl = (Color)((Panel)s).Tag; using var br = new SolidBrush(cl); e.Graphics.FillEllipse(br, 10, 8, 10, 10); };
                card.Controls.Add(new Label { Text = cn[i], Location = new Point(26, 4), AutoSize = true, ForeColor = CMuted, Font = new Font("Segoe UI", 8, FontStyle.Bold), BackColor = Color.Transparent });
                _crdVal[i] = new Label { Text = "--", Location = new Point(26, 20), AutoSize = true, ForeColor = cc[i], Font = new Font("Segoe UI", 15, FontStyle.Bold), BackColor = Color.Transparent };
                _crdDelta[i] = new Label { Text = "", Location = new Point(95, 26), AutoSize = true, ForeColor = CMuted, Font = new Font("Segoe UI", 8), BackColor = Color.Transparent };
                card.Controls.AddRange(new Control[] { _crdVal[i], _crdDelta[i] });
                _cardStrip.Controls.Add(card);
            }
            _cardStrip.Resize += (_, __) => { var cs = _cardStrip.Controls.OfType<Panel>().ToArray(); int w = (_cardStrip.ClientSize.Width - 10) / 5; for (int j = 0; j < cs.Length; j++) cs[j].SetBounds(j * w, 0, w, _cardStrip.Height - 1); };
        }

        void BuildDateToolbar()
        {
            _dateToolbar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = CBg };
            _dateToolbar.Paint += (_, e) => { using var p = new Pen(CBorder); e.Graphics.DrawLine(p, 0, _dateToolbar.Height - 1, _dateToolbar.Width, _dateToolbar.Height - 1); };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(14, 6, 0, 0) };
            var lblOd = new Label { Text = "Od:", AutoSize = true, ForeColor = CMuted, Font = new Font("Segoe UI Semibold", 9), Margin = new Padding(0, 5, 4, 0) };
            var trzyMiesiaceTemu = DateTime.Today.AddMonths(-3);
            var niedzielaOd = trzyMiesiaceTemu.AddDays(-(int)trzyMiesiaceTemu.DayOfWeek);
            _dtOd = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Font = new Font("Segoe UI", 9), Value = niedzielaOd, Margin = new Padding(0, 1, 10, 0) };
            var lblDo = new Label { Text = "Do:", AutoSize = true, ForeColor = CMuted, Font = new Font("Segoe UI Semibold", 9), Margin = new Padding(0, 5, 4, 0) };
            _dtDo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Font = new Font("Segoe UI", 9), Value = DateTime.Today, Margin = new Padding(0, 1, 10, 0) };
            var btnRefresh = new Button { Text = "Odswież", AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(6, 1, 0, 0), Padding = new Padding(10, 2, 10, 2) };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (_, __) => await ReloadRangeAsync();
            _dtOd.ValueChanged += async (_, __) => await ReloadRangeAsync();
            _dtDo.ValueChanged += async (_, __) => await ReloadRangeAsync();
            _chkGrupujDni = new CheckBox
            {
                Text = "  Grupuj po dniach",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(14, 6, 0, 0),
                Font = new Font("Segoe UI Semibold", 9),
                ForeColor = Color.FromArgb(67, 56, 202),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _chkGrupujDni.CheckedChanged += (_, __) => RebuildGrid();
            // Najpierw DatePickery
            flow.Controls.AddRange(new Control[] { lblOd, _dtOd, lblDo, _dtDo, btnRefresh, _chkGrupujDni });

            // Separator
            var sep = new Panel { Size = new Size(1, 26), Margin = new Padding(10, 4, 6, 0), BackColor = CBorder };
            flow.Controls.Add(sep);

            // Szybkie przyciski zakresow
            foreach (var (label, days) in new[] { ("1 tyg", 7), ("1 mies", 30), ("3 mies", 91), ("6 mies", 182), ("1 rok", 365) })
            {
                var d = days;
                var qb = new Button
                {
                    Text = label, AutoSize = false, Size = new Size(55, 26),
                    FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    Margin = new Padding(2, 2, 0, 0), Cursor = Cursors.Hand,
                    BackColor = Color.White, ForeColor = CMuted
                };
                qb.FlatAppearance.BorderColor = CBorder; qb.FlatAppearance.BorderSize = 1;
                qb.Click += async (_, __) =>
                {
                    _dtOd.Value = DateTime.Today.AddDays(-d);
                    _dtDo.Value = DateTime.Today;
                    await ReloadRangeAsync();
                };
                flow.Controls.Add(qb);
            }

            _dateToolbar.Controls.Add(flow);
        }

        void BuildBottom()
        {
            // Dwa wykresy obok siebie: E2 (czerwony) i H1 (szary)
            _chartHost = new Panel { Dock = DockStyle.Top, Height = 280, BackColor = Color.White };
            _chartHost.Paint += (_, e) => { using var p = new Pen(CBorder); e.Graphics.DrawLine(p, 0, _chartHost.Height - 1, _chartHost.Width, _chartHost.Height - 1); };

            _chartE2 = new Panel { Dock = DockStyle.Left, BackColor = Color.White, Cursor = Cursors.Cross };
            _chartE2.Paint += (_, e) => PaintSingleChart(e.Graphics, _chartE2.Width, _chartE2.Height, "E2 — Pojemniki", CRed, Color.FromArgb(254, 226, 226), _historiaAll.GetValueOrDefault("E2"));
            _chartE2.MouseMove += (s, e) => ChartPanelMouseMove(_chartE2, "E2", e);

            _chartH1 = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Cursor = Cursors.Cross };
            _chartH1.Paint += (_, e) => PaintSingleChart(e.Graphics, _chartH1.Width, _chartH1.Height, "H1 — Palety", CMuted, Color.FromArgb(229, 231, 235), _historiaAll.GetValueOrDefault("H1"));
            _chartH1.MouseMove += (s, e) => ChartPanelMouseMove(_chartH1, "H1", e);

            // Separator pionowy miedzy wykresami
            var chartSep = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = CBorder };

            _chartHost.Controls.Add(_chartH1);   // Fill
            _chartHost.Controls.Add(chartSep);    // Left (separator)
            _chartHost.Controls.Add(_chartE2);    // Left
            _chartHost.Resize += (_, __) => { _chartE2.Width = (_chartHost.Width - 1) / 2; };

            _bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(249, 250, 251) };
            _bottomBar.Paint += (_, e) => { using var p = new Pen(CBorder); e.Graphics.DrawLine(p, 0, 0, _bottomBar.Width, 0); };

            _lblSaldoEnd = new Label { Dock = DockStyle.Fill, Text = "", ForeColor = CSlate, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };

            var flow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0, 3, 8, 0) };
            var btnPotw = MkBtn("Potwierdz saldo", Color.FromArgb(22, 163, 74)); btnPotw.Click += (_, __) => DodajPotwierdzenie();
            var btnKart = MkBtn("Kartoteka", Color.FromArgb(59, 130, 246)); btnKart.Click += (_, __) => { using var f = new KartotekaPotwierdzienForm(_id, _nazwa); f.ShowDialog(this); };
            _btnCopy = MkBtn("Kopiuj saldo", Color.FromArgb(99, 102, 241)); _btnCopy.Click += (_, __) => CopySaldo();
            _btnPdf = MkBtn("PDF", Color.FromArgb(37, 99, 235)); _btnPdf.Click += async (_, __) => await ExportPdfAsync();
            _btnEmail = MkBtn("Email", Color.FromArgb(5, 150, 105)); _btnEmail.Click += (_, __) => SendEmail();
            var btnFolder = MkBtn("Folder", Color.FromArgb(234, 88, 12)); btnFolder.Click += (_, __) => OpenFolderKontrahenta();
            var btnClose = MkBtn("Zamknij", Color.FromArgb(100, 116, 139)); btnClose.Click += (_, __) => Close();
            flow.Controls.AddRange(new Control[] { btnPotw, btnKart, _btnCopy, _btnPdf, _btnEmail, btnFolder, btnClose });

            _bottomBar.Controls.Add(_lblSaldoEnd);
            _bottomBar.Controls.Add(flow);
        }

        Button MkBtn(string text, Color bg)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(70, 30), FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(3, 0, 0, 0), Padding = new Padding(8, 0, 8, 0) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        void BuildInfoPanel()
        {
            _infoPanel = new Panel { Dock = DockStyle.Right, Width = 240, BackColor = CBg, AutoScroll = true, Padding = new Padding(12, 10, 12, 10) };
            _infoPanel.Paint += (_, e) => { using var p = new Pen(CBorder); e.Graphics.DrawLine(p, 0, 0, 0, _infoPanel.Height); };

            int y = 0;
            Sec("KONTRAHENT", ref y);
            _lblInfoNazwa = Val(_nazwa, ref y, new Font("Segoe UI", 10, FontStyle.Bold));
            y += 6;
            Sec("HANDLOWIEC", ref y);
            _lblInfoHandl = Val(_handlowiec, ref y);
            y += 6;
            Sec("KONTAKT", ref y);
            _lblInfoEmail = Val("-", ref y);
            _lblInfoTel = Val("-", ref y);
            y += 4;
            _infoPanel.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(250, 1), BackColor = CBorder }); y += 10;

            Sec("STATYSTYKI (3 mies.)", ref y);
            _lblStatWyd = Val("Wydania: --", ref y); _lblStatWyd.ForeColor = CRed;
            _lblStatPrzyj = Val("Przyjecia: --", ref y); _lblStatPrzyj.ForeColor = CGreen;
            _lblStatBilans = Val("Bilans: --", ref y);
            _lblStatZwrot = Val("Zwrot: -- %", ref y);
            y += 4;
            _infoPanel.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(250, 1), BackColor = CBorder }); y += 10;

            Sec("AKTYWNOSC", ref y);
            _lblStatDni = Val("Ost. dokument: --", ref y);
            _lblStatPotw = Val("Potwierdzenia: --", ref y);
            y += 4;
            _infoPanel.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(250, 1), BackColor = CBorder }); y += 10;

            Sec("HISTORIA POTWIERDZEN", ref y);
            _lblPotwStatus = Val("Status: --", ref y);
            _potwHistPanel = new Panel { Location = new Point(14, y), Size = new Size(212, 120), BackColor = Color.Transparent, AutoScroll = true };
            _infoPanel.Controls.Add(_potwHistPanel);
        }

        void Sec(string t, ref int y) { _infoPanel.Controls.Add(new Label { Text = t, Location = new Point(14, y), AutoSize = true, ForeColor = CMuted, Font = new Font("Segoe UI", 7, FontStyle.Bold), BackColor = Color.Transparent }); y += 15; }
        Label Val(string t, ref int y, Font f = null)
        {
            var l = new Label { Text = t, Location = new Point(14, y), AutoSize = true, MaximumSize = new Size(222, 0), ForeColor = CSlate, Font = f ?? new Font("Segoe UI", 9.5f), BackColor = Color.Transparent };
            _infoPanel.Controls.Add(l); y += (int)(l.Font.Size * 2.2f) + 2; return l;
        }

        Panel BuildGridHost()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _lblCount = new Label { Dock = DockStyle.Top, Height = 28, Text = "  Dokumenty", ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = CBg };
            _grid = MkGrid();
            _grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Bar", HeaderText = "", Width = 6 },
                new DataGridViewTextBoxColumn { Name = "TypTxt", HeaderText = "Typ", Width = 65 },
                new DataGridViewTextBoxColumn { Name = "NrDok", HeaderText = "Dokument", DataPropertyName = "NrDok", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 100 },
                new DataGridViewTextBoxColumn { Name = "Data", HeaderText = "Data", DataPropertyName = "DataText", Width = 120 },
                new DataGridViewTextBoxColumn { Name = "Dzien", HeaderText = "Dzień tyg.", DataPropertyName = "DzienTyg", Width = 110 },
                RCol("E2", 60), RCol("H1", 60), RCol("EURO", 60), RCol("PCV", 60), RCol("DREW", 60),
            });
            _grid.CellFormatting += FmtGrid;
            _grid.CellPainting += PaintCells;
            _grid.CellClick += GridCellClick;
            _grid.ContextMenuStrip = BuildCtxMenu();

            host.Controls.Add(_grid);
            host.Controls.Add(_lblCount);
            return host;
        }

        DataGridViewTextBoxColumn RCol(string name, int w) => new() { Name = name, HeaderText = name, DataPropertyName = name, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = w, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic };

        DataGridView MkGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoGenerateColumns = false, RowHeadersVisible = false,
                BackgroundColor = Color.White, GridColor = Color.FromArgb(180, 180, 180), BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.Single, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(240, 240, 240), ForeColor = Color.FromArgb(30, 30, 30), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), SelectionBackColor = Color.FromArgb(240, 240, 240) },
                ColumnHeadersHeight = 34, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9.5f), Padding = new Padding(4, 2, 4, 2), SelectionBackColor = Color.FromArgb(255, 255, 220), SelectionForeColor = CSlate },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 250) },
                RowTemplate = { Height = 34 }, EnableHeadersVisualStyles = false
            };
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(g, true);
            int hov = -1;
            g.CellMouseEnter += (_, e) => { if (e.RowIndex >= 0) { hov = e.RowIndex; g.InvalidateRow(e.RowIndex); } };
            g.CellMouseLeave += (_, e) => { if (e.RowIndex >= 0) { var p = hov; hov = -1; if (p >= 0 && p < g.Rows.Count) g.InvalidateRow(p); } };
            g.RowPostPaint += (_, e) =>
            {
                if (g.Rows[e.RowIndex].Selected)
                {
                    var rect = new Rectangle(e.RowBounds.X, e.RowBounds.Y, e.RowBounds.Width - 1, e.RowBounds.Height - 1);
                    using var pen = new Pen(Color.FromArgb(250, 204, 21), 2);
                    e.Graphics.DrawRectangle(pen, rect);
                }
                if (e.RowIndex == hov && !g.Rows[e.RowIndex].Selected)
                {
                    var rect = new Rectangle(e.RowBounds.X, e.RowBounds.Y, e.RowBounds.Width - 1, e.RowBounds.Height - 1);
                    using var pen = new Pen(Color.FromArgb(209, 213, 219), 1);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };
            return g;
        }

        void RebuildGrid()
        {
            var saldoRows = _docs.Where(d => d.JestSaldem).ToList();
            var allRows = new List<DokumentOpakowania>();
            allRows.AddRange(saldoRows);

            bool grupuj = _chkGrupujDni?.Checked ?? true;
            if (!grupuj)
            {
                allRows.AddRange(_docs.Where(d => !d.JestSaldem).OrderByDescending(d => d.Data));
                _grid.SuspendLayout();
                _grid.DataSource = new SortableBindingList<DokumentOpakowania>(allRows);
                _grid.ResumeLayout();
                return;
            }

            var grouped = _docs
                .Where(d => !d.JestSaldem && d.Data.HasValue)
                .GroupBy(d => d.Data.Value.Date)
                .OrderByDescending(g => g.Key);

            foreach (var g in grouped)
            {
                bool expanded = _expandedDays.Contains(g.Key);
                int cnt = g.Count();
                string chevron = cnt > 1 ? (expanded ? "▼ " : "▶ ") : "    ";
                var grp = new DokumentOpakowania
                {
                    TypDokumentu = "GRP",
                    Data = g.Key,
                    DzienTyg = g.Key.ToString("dddd", new System.Globalization.CultureInfo("pl-PL")),
                    NrDok = cnt == 1 ? ("    " + (g.First().NrDok ?? "")) : $"{chevron}{cnt} {(cnt == 1 ? "dokument" : "dokumenty")}",
                    Dokumenty = string.Join(", ", g.Select(d => d.NrDok).Where(s => !string.IsNullOrEmpty(s))),
                    E2 = g.Sum(d => d.E2),
                    H1 = g.Sum(d => d.H1),
                    EURO = g.Sum(d => d.EURO),
                    PCV = g.Sum(d => d.PCV),
                    DREW = g.Sum(d => d.DREW),
                    JestSaldem = false
                };
                allRows.Add(grp);
                if (expanded && cnt > 1)
                    allRows.AddRange(g.OrderByDescending(d => d.Data));
            }

            _grid.SuspendLayout();
            _grid.DataSource = new SortableBindingList<DokumentOpakowania>(allRows);
            // Wyzsze wiersze dla GRP i SALDO dla czytelnosci
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].DataBoundItem is DokumentOpakowania dd)
                {
                    if (dd.TypDokumentu == "GRP") _grid.Rows[i].Height = 40;
                    else if (dd.JestSaldem) _grid.Rows[i].Height = 38;
                }
            }
            _grid.ResumeLayout();
        }

        void GridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not DokumentOpakowania d) return;
            if (d.TypDokumentu != "GRP" || !d.Data.HasValue) return;
            var cn = _grid.Columns[e.ColumnIndex].Name;
            if (cn != "NrDok" && cn != "Bar" && cn != "TypTxt") return;
            var key = d.Data.Value.Date;
            int cnt = _docs.Count(x => !x.JestSaldem && x.Data.HasValue && x.Data.Value.Date == key);
            if (cnt <= 1) return;
            if (!_expandedDays.Add(key)) _expandedDays.Remove(key);
            RebuildGrid();
        }

        ContextMenuStrip BuildCtxMenu()
        {
            var m = new ContextMenuStrip { Font = new Font("Segoe UI", 9.5f) };
            m.Items.Add("Potwierdz saldo", null, (_, __) => DodajPotwierdzenie());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Kopiuj wiersz", null, (_, __) => CopyRow());
            m.Items.Add("Kopiuj saldo do schowka", null, (_, __) => CopySaldo());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Eksport PDF", null, async (_, __) => await ExportPdfAsync());
            m.Items.Add("Wyslij email z saldem", null, (_, __) => SendEmail());
            return m;
        }

        #endregion

        #region Cell Painting

        void PaintCells(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not DokumentOpakowania d) return;
            var cn = _grid.Columns[e.ColumnIndex].Name;

            // Kolorowy pasek (kolumna 0)
            if (cn == "Bar")
            {
                e.Handled = true;
                Color bar = d.TypDokumentu == "GRP" ? Color.FromArgb(99, 102, 241) : d.JestSaldem ? CYellow : d.TypDokumentu == "MW1" ? CRed : CGreen;
                bool sel = e.State.HasFlag(DataGridViewElementStates.Selected);
                Color bgCol = sel ? e.CellStyle.SelectionBackColor
                    : d.TypDokumentu == "GRP" ? Color.FromArgb(238, 242, 255)
                    : d.JestSaldem ? Color.FromArgb(255, 251, 235)
                    : e.CellStyle.BackColor;
                using (var bg = new SolidBrush(bgCol)) e.Graphics.FillRectangle(bg, e.CellBounds);
                int barW = d.TypDokumentu == "GRP" ? 5 : 4;
                using (var br = new SolidBrush(bar)) e.Graphics.FillRectangle(br, e.CellBounds.X, e.CellBounds.Y + 2, barW, e.CellBounds.Height - 4);
            }

            // Kolumna Typ — "Wydanie" czerwone, "Przyjecie" zielone, "SALDO" zolte, "SUMA" niebieskie
            if (cn == "TypTxt")
            {
                e.Handled = true;
                bool sel = e.State.HasFlag(DataGridViewElementStates.Selected);
                Color cellBg = d.JestSaldem ? Color.FromArgb(255, 251, 235)
                    : d.TypDokumentu == "GRP" ? Color.FromArgb(238, 242, 255)
                    : e.CellStyle.BackColor;
                using (var bg = new SolidBrush(sel ? e.CellStyle.SelectionBackColor : cellBg)) e.Graphics.FillRectangle(bg, e.CellBounds);

                string txt; Color col; Font fnt;
                if (d.TypDokumentu == "GRP") { txt = "DZIEŃ"; col = Color.FromArgb(67, 56, 202); fnt = new Font("Segoe UI", 8.5f, FontStyle.Bold); }
                else if (d.JestSaldem) { txt = "SALDO"; col = Color.FromArgb(161, 98, 7); fnt = new Font("Segoe UI", 8, FontStyle.Bold); }
                else if (d.TypDokumentu == "MW1") { txt = "Wydanie"; col = CRed; fnt = new Font("Segoe UI", 8.5f, FontStyle.Bold); }
                else { txt = "Przyjęcie"; col = CGreen; fnt = new Font("Segoe UI", 8.5f, FontStyle.Bold); }

                using (fnt) using (var br = new SolidBrush(col))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(txt, fnt, br, new RectangleF(e.CellBounds.X + 6, e.CellBounds.Y, e.CellBounds.Width - 8, e.CellBounds.Height), sf);
                }
            }
        }

        static string FmtSaldo(int v, string unit = "")
        {
            var num = Math.Abs(v).ToString("N0");
            if (v < 0) return $"Wisi {num}{unit}";
            if (v > 0) return $"My winni {num}{unit}";
            return "0";
        }

        static string FmtDoc(int v, string unit = "")
        {
            var num = Math.Abs(v).ToString("N0");
            if (v < 0) return $"Wydaliśmy {num}{unit}";
            if (v > 0) return $"Oddał {num}{unit}";
            return "0";
        }

        void FmtGrid(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var cn = _grid.Columns[e.ColumnIndex].Name;
            var dok = _grid.Rows[e.RowIndex].DataBoundItem as DokumentOpakowania;

            // Kolumny numeryczne — kolor + tekst zalezny od typu wiersza
            if (cn is "E2" or "H1" or "EURO" or "PCV" or "DREW" && e.Value is int v)
            {
                if (v == 0) { e.Value = "0"; e.CellStyle.ForeColor = Color.FromArgb(200, 200, 200); }
                else
                {
                    e.CellStyle.ForeColor = v < 0 ? CRed : CGreen;
                    var unit = cn == "E2" ? " poj." : " pal.";
                    // Salda: "Wisi"/"My winni"; dokumenty i sumy dnia: "Wydalismy"/"Oddal"
                    e.Value = (dok != null && dok.JestSaldem) ? FmtSaldo(v, unit) : FmtDoc(v, unit);
                }
                e.FormattingApplied = true;
            }

            // Stylowanie wierszy
            if (dok != null)
            {
                if (dok.TypDokumentu == "GRP")
                {
                    e.CellStyle.BackColor = Color.FromArgb(238, 242, 255);
                    e.CellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                    if (cn is not "E2" and not "H1" and not "EURO" and not "PCV" and not "DREW")
                        e.CellStyle.ForeColor = Color.FromArgb(67, 56, 202);
                }
                else if (dok.JestSaldem)
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 251, 235);
                    e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                }
                else
                {
                    // Dziecko rozwinietej grupy — lekki indent + tlumione tlo
                    bool isChild = (_chkGrupujDni?.Checked ?? true) && dok.Data.HasValue && _expandedDays.Contains(dok.Data.Value.Date);
                    if (isChild)
                    {
                        e.CellStyle.BackColor = Color.FromArgb(250, 250, 252);
                        if (cn == "NrDok" && e.Value is string s && !s.StartsWith("   └"))
                            e.Value = "   └  " + s;
                    }
                    if (cn is "NrDok" or "Data" or "Dzien")
                    {
                        if (dok.TypDokumentu == "MW1") e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                        else e.CellStyle.ForeColor = Color.FromArgb(20, 83, 45);
                    }
                }
            }
        }

        #endregion

        #region Data Loading

        async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _ = LoadAvatarAsync();

                // Saldo
                if (_saldo == null) _saldo = await _ds.PobierzSaldaWszystkichOpakowannAsync(_id, DateTime.Today);
                if (_saldo != null)
                {
                    int[] v = { _saldo.SaldoE2, _saldo.SaldoH1, _saldo.SaldoEURO, _saldo.SaldoPCV, _saldo.SaldoDREW };
                    string[] units = { " poj.", " pal.", " pal.", " pal.", " pal." };
                    for (int i = 0; i < 5; i++)
                    {
                        var num = Math.Abs(v[i]).ToString("N0");
                        _crdVal[i].Text = v[i] == 0 ? "0" : num;
                        _crdVal[i].ForeColor = VCol(v[i]);
                        _crdDelta[i].Text = v[i] < 0 ? $"Wisi{units[i]}" : v[i] > 0 ? $"My winni{units[i]}" : "";
                        _crdDelta[i].ForeColor = VCol(v[i]);
                    }
                    _lblInfoEmail.Text = _saldo.Email ?? "-";
                    _lblInfoTel.Text = _saldo.Telefon ?? "-";
                }

                // Dokumenty, statystyki, wykresy — zakres dat
                await ReloadRangeAsync();

                // Potwierdzenia
                _potw = await _ds.PobierzPotwierdzeniaDlaKontrahentaAsync(_id);
                int potwOk = _potw.Count(p => p.StatusPotwierdzenia == "Potwierdzone");
                _lblStatPotw.Text = $"Potwierdzenia: {potwOk}/{_potw.Count}";
                _lblStatPotw.ForeColor = _potw.Count == 0 ? CGray : potwOk == _potw.Count ? CGreen : Color.FromArgb(245, 158, 11);

                // Status + historia potwierdzeń w panelu info
                var ostatnie = _potw.FirstOrDefault();
                if (ostatnie != null)
                {
                    int dni = (int)(DateTime.Today - ostatnie.DataPotwierdzenia).TotalDays;
                    if (dni <= 30) { _lblPotwStatus.Text = $"OK — {ostatnie.DataPotwierdzenia:dd.MM.yyyy} ({dni} dni)"; _lblPotwStatus.ForeColor = CGreen; }
                    else if (dni <= 90) { _lblPotwStatus.Text = $"Uwaga — {dni} dni od potw.!"; _lblPotwStatus.ForeColor = Color.FromArgb(245, 158, 11); }
                    else { _lblPotwStatus.Text = $"PILNE — {dni} dni od potw.!"; _lblPotwStatus.ForeColor = CRed; }
                }
                else { _lblPotwStatus.Text = "Nigdy nie potwierdzono!"; _lblPotwStatus.ForeColor = CRed; }

                // Lista historii
                _potwHistPanel.Controls.Clear();
                int py = 0;
                foreach (var p in _potw.Take(10))
                {
                    var statusCol = p.StatusPotwierdzenia == "Potwierdzone" ? CGreen : p.StatusPotwierdzenia == "Rozbieżność" ? CRed : Color.FromArgb(245, 158, 11);
                    var dot = new Panel { Location = new Point(0, py + 4), Size = new Size(8, 8), BackColor = statusCol };
                    var lbl = new Label
                    {
                        Text = $"{p.DataPotwierdzenia:dd.MM.yy} {p.KodOpakowania}: {p.IloscPotwierdzona} ({p.StatusPotwierdzenia})",
                        Location = new Point(14, py), AutoSize = true, MaximumSize = new Size(195, 0),
                        Font = new Font("Segoe UI", 8), ForeColor = CSlate, BackColor = Color.Transparent
                    };
                    var lblUser = new Label
                    {
                        Text = p.UzytkownikNazwa ?? p.UzytkownikId,
                        Location = new Point(14, py + 15), AutoSize = true,
                        Font = new Font("Segoe UI", 7, FontStyle.Italic), ForeColor = CMuted, BackColor = Color.Transparent
                    };
                    _potwHistPanel.Controls.AddRange(new Control[] { dot, lbl, lblUser });
                    py += 34;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Cursor = Cursors.Default; }
        }

        async Task ReloadRangeAsync()
        {
            var od = _dtOd?.Value.Date ?? DateTime.Today.AddMonths(-3);
            var doo = _dtDo?.Value.Date ?? DateTime.Today;

            // Dokumenty — rozliczanie każdy dzień (jeden wiersz = jeden dzień, rozwijane)
            _docs = await _ds.PobierzSaldoKontrahentaAsync(_id, od, doo);
            var plPL = new System.Globalization.CultureInfo("pl-PL");
            foreach (var d in _docs)
                if (d.Data.HasValue) d.DzienTyg = d.Data.Value.ToString("dddd", plPL);
            RebuildGrid();

            // Statystyki
            int wydania = _docs.Count(d => !d.JestSaldem && d.TypDokumentu == "MW1");
            int przyjecia = _docs.Count(d => !d.JestSaldem && d.TypDokumentu == "MP");
            int docCount = _docs.Count(d => !d.JestSaldem);
            _lblCount.Text = $"  Dokumenty ({docCount})   |   Wydania: {wydania}   Przyjecia: {przyjecia}";

            var lastSaldo = _docs.FirstOrDefault(d => d.JestSaldem);
            if (lastSaldo != null)
                _lblSaldoEnd.Text = $"  Saldo:  E2={FmtSaldo(lastSaldo.E2, " poj.")}   H1={FmtSaldo(lastSaldo.H1, " pal.")}   EURO={FmtSaldo(lastSaldo.EURO, " pal.")}   PCV={FmtSaldo(lastSaldo.PCV, " pal.")}   DREW={FmtSaldo(lastSaldo.DREW, " pal.")}";

            _lblStatWyd.Text = $"Wydania: {wydania} dok.";
            _lblStatPrzyj.Text = $"Przyjecia: {przyjecia} dok.";

            int sumWyd = _docs.Where(d => !d.JestSaldem && d.TypDokumentu == "MW1").Sum(d => Math.Abs(d.E2 + d.H1 + d.EURO + d.PCV + d.DREW));
            int sumPrzyj = _docs.Where(d => !d.JestSaldem && d.TypDokumentu == "MP").Sum(d => Math.Abs(d.E2 + d.H1 + d.EURO + d.PCV + d.DREW));
            int bilans = sumWyd - sumPrzyj;
            _lblStatBilans.Text = $"Bilans: {(bilans > 0 ? "Wisi" : bilans < 0 ? "My winni" : "")} {Math.Abs(bilans).ToString("N0")} szt.";
            _lblStatBilans.ForeColor = bilans > 0 ? CRed : bilans < 0 ? CGreen : CGray;
            _lblStatZwrot.Text = sumWyd > 0 ? $"Zwrot: {sumPrzyj * 100.0 / sumWyd:F0}%" : "Zwrot: -";

            var lastDoc = _docs.Where(d => !d.JestSaldem).FirstOrDefault();
            int dniOd = lastDoc?.Data != null ? (DateTime.Today - lastDoc.Data.Value).Days : -1;
            _lblStatDni.Text = dniOd >= 0 ? $"Ost. dokument: {dniOd} dni temu" : "Ost. dokument: -";
            _lblStatDni.ForeColor = dniOd > 30 ? CRed : dniOd > 14 ? Color.FromArgb(245, 158, 11) : CGreen;

            // Wykresy — zakres dat
            try
            {
                var typy = new[] { "E2", "H1", "EURO", "PCV", "DREW" };
                var histTasks = typy.Select(t => _ds.PobierzHistorieSaldaAsync(_id, t, od, doo)).ToArray();
                var histResults = await Task.WhenAll(histTasks);
                _historiaAll.Clear();
                for (int hi = 0; hi < typy.Length; hi++) _historiaAll[typy[hi]] = histResults[hi] ?? new();
                _historia = _historiaAll.GetValueOrDefault("E2");
                _chartE2?.Invalidate();
                _chartH1?.Invalidate();
            }
            catch { }
        }

        async Task LoadAvatarAsync()
        {
            if (_avatarHandl != null || _handlowiec == "-") return;
            try
            {
                using var conn = new SqlConnection("Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;");
                await conn.OpenAsync();
                string uid = null;
                using (var cmd = new SqlCommand("SELECT TOP 1 UserID FROM UserHandlowcy WHERE HandlowiecName = @N", conn)) { cmd.Parameters.AddWithValue("@N", _handlowiec); uid = (await cmd.ExecuteScalarAsync())?.ToString(); }
                if (uid == null) using (var cmd = new SqlCommand("SELECT TOP 1 ID FROM operators WHERE Name = @N", conn)) { cmd.Parameters.AddWithValue("@N", _handlowiec); uid = (await cmd.ExecuteScalarAsync())?.ToString(); }
                uid ??= _handlowiec;
                _avatarHandl = UserAvatarManager.HasAvatar(uid) ? UserAvatarManager.GetAvatarRounded(uid, 16) : UserAvatarManager.GenerateDefaultAvatar(_handlowiec, uid, 16);
                _header.Invalidate();
            }
            catch { }
        }

        #endregion

        #region Actions

        void DodajPotwierdzenie()
        {
            if (_saldo == null) return;
            var okno = new Views.DodajPotwierdzenieWindow(_id, _nazwa, _saldo, _userId);
            if (okno.ShowDialog() == true)
                _ = LoadAsync();
        }

        void DoPrint()
        {
            try
            {
                // Format zgodny z oryginalnym programem WidokPojemniki
                var data = _docs?.Where(d => !d.JestSaldem && d.TypDokumentu != "GRP").OrderByDescending(d => d.Data).ToList()
                    ?? new List<DokumentOpakowania>();
                int pageIdx = 0;
                bool summaryPageDone = false;
                var doc = new System.Drawing.Printing.PrintDocument();

                doc.PrintPage += (_, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    var mb = e.MarginBounds;

                    using var fCompany = new Font("Segoe UI", 10, FontStyle.Italic);
                    using var fTitle = new Font("Segoe UI", 16, FontStyle.Bold);
                    using var fBody = new Font("Segoe UI", 11);
                    using var fTable = new Font("Segoe UI", 11);
                    using var fTableBold = new Font("Segoe UI", 11, FontStyle.Bold);
                    using var fSign = new Font("Segoe UI", 11);
                    using var fNote = new Font("Segoe UI", 9, FontStyle.Italic);
                    using var fHead = new Font("Segoe UI", 9, FontStyle.Bold);
                    using var fCell = new Font("Segoe UI", 9);
                    using var fCellBold = new Font("Segoe UI", 9, FontStyle.Bold);
                    using var fPage = new Font("Segoe UI", 8);
                    using var penTable = new Pen(Color.FromArgb(160, 160, 160));

                    // =============================================
                    // STRONA 1 — Podsumowanie (jak stary program)
                    // =============================================
                    if (!summaryPageDone)
                    {
                        int y = mb.Top;
                        string dataSalda = _dtDo.Value.ToString("yyyy-MM-dd");

                        // Nagłówek firmy
                        g.DrawString("Ubojnia Drobiu \"Piórkowscy\"", fCompany, Brushes.Black, mb.Left, y); y += 16;
                        g.DrawString("Koziołki 40, 95-061 Dmosin", fCompany, Brushes.Black, mb.Left, y); y += 16;
                        g.DrawString("46 874 71 70, wew 122 Magazyn Opakowań", fCompany, Brushes.Black, mb.Left, y); y += 30;

                        // Tytuł
                        var titleTxt = $"Zestawienie Opakowań Zwrotnych dla Kontrahenta: {_nazwa}";
                        var titleRect = new RectangleF(mb.Left, y, mb.Width, 60);
                        g.DrawString(titleTxt, fTitle, Brushes.Black, titleRect, new StringFormat { Alignment = StringAlignment.Center });
                        y += (int)g.MeasureString(titleTxt, fTitle, mb.Width).Height + 14;

                        // Tekst wyjaśniający
                        var introTxt = $"W związku z koniecznością uzgodnienia salda opakowań zwrotnych na dzień {dataSalda}, " +
                            "poniżej przedstawiamy szczegółowe zestawienie opakowań zgodnie z naszą ewidencją. " +
                            "Prosimy o weryfikację przedstawionych danych oraz potwierdzenie ich zgodności.";
                        var introRect = new RectangleF(mb.Left + 20, y, mb.Width - 40, 200);
                        g.DrawString(introTxt, fBody, Brushes.Black, introRect);
                        y += (int)g.MeasureString(introTxt, fBody, (int)(mb.Width - 40)).Height + 24;

                        // Tabela podsumowania sald
                        if (_saldo != null)
                        {
                            (string nazwa, int val)[] rows =
                            {
                                ("Pojemniki E2", _saldo.SaldoE2),
                                ("Palety H1", _saldo.SaldoH1),
                                ("Palety EURO", _saldo.SaldoEURO),
                                ("Palety plastikowe", _saldo.SaldoPCV),
                                ("Palety drewniane (bez zwrotne)", _saldo.SaldoDREW),
                            };

                            int tblLeft = mb.Left + (mb.Width - 440) / 2;
                            int col1W = 220, col2W = 220, rowH = 30;

                            for (int i = 0; i < rows.Length; i++)
                            {
                                int ry = y + i * rowH;
                                g.DrawRectangle(penTable, tblLeft, ry, col1W, rowH);
                                g.DrawRectangle(penTable, tblLeft + col1W, ry, col2W, rowH);

                                // Nazwa opakowania
                                g.DrawString(rows[i].nazwa, fTable, Brushes.Black,
                                    new RectangleF(tblLeft + 6, ry, col1W - 12, rowH),
                                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

                                // Wartość z opisem
                                string valTxt;
                                if (rows[i].val > 0) valTxt = $"Kontrahent winny : {rows[i].val}";
                                else if (rows[i].val < 0) valTxt = $"Ubojnia winna : {Math.Abs(rows[i].val)}";
                                else valTxt = "0";
                                g.DrawString(valTxt, fTable, Brushes.Black,
                                    new RectangleF(tblLeft + col1W + 6, ry, col2W - 12, rowH),
                                    new StringFormat { LineAlignment = StringAlignment.Center });
                            }
                            y += rows.Length * rowH + 24;
                        }

                        // Tekst o potwierdzeniu
                        var footerTxt = "Prosimy o przesłanie potwierdzenia zgodności danych na adres e-mail: " +
                            "opakowania@piorkowscy.com.pl. W przypadku braku odpowiedzi w ciągu 7 dni " +
                            "od daty otrzymania niniejszego dokumentu, saldo przedstawione przez naszą " +
                            "firmę zostanie uznane za zgodne. W razie jakichkolwiek pytań lub wątpliwości " +
                            "prosimy o kontakt telefoniczny z naszym magazynem opakowań pod numerem " +
                            "46 874 71 70, wew. 122. Dziękujemy za współpracę.";
                        var footRect = new RectangleF(mb.Left + 20, y, mb.Width - 40, 200);
                        g.DrawString(footerTxt, fBody, Brushes.Black, footRect);
                        y += (int)g.MeasureString(footerTxt, fBody, (int)(mb.Width - 40)).Height + 50;

                        // Podpis
                        g.DrawString("Podpis kontrahenta: .......................................................", fSign, Brushes.Black,
                            new RectangleF(mb.Left, y, mb.Width, 20), new StringFormat { Alignment = StringAlignment.Center });
                        y += 40;

                        // Stopka
                        g.DrawString("Oprogramowanie utworzone przez Sergiusza Piórkowskiego", fNote, Brushes.Black,
                            new RectangleF(mb.Left, mb.Bottom - 16, mb.Width, 16), new StringFormat { Alignment = StringAlignment.Far });

                        summaryPageDone = true;
                        e.HasMorePages = data.Count > 0;
                        return;
                    }

                    // =============================================
                    // STRONY 2+ — Tabela dokumentów
                    // =============================================
                    {
                        int y = mb.Top;

                        // Notatka u góry (prawa strona)
                        g.DrawString("- wydanie do odbiorcy, + przyjęcie na ubojnię", fNote, Brushes.Black,
                            new RectangleF(mb.Left, y, mb.Width, 14), new StringFormat { Alignment = StringAlignment.Far });
                        y += 18;

                        // Kolumny: NrDok, Data, Dokumenty, E2, H1, EURO, PCV, Drew
                        int[] cw = { 80, 72, 0, 58, 50, 50, 50, 50 };
                        cw[2] = mb.Width - cw.Where((_, i) => i != 2).Sum();
                        string[] colH = { "NrDok", "Data", "Dokumenty", "E2", "H1", "EURO", "PCV", "Drew" };
                        int rowH = 22, headH = 24;

                        // Oblicz ile wierszy zmieści się
                        int availH = mb.Bottom - y - 24 - headH;
                        int rowsPerPage = Math.Max(1, availH / rowH);

                        // Na pierwszej stronie tabeli dodaj wiersz Saldo na górze (nie liczy się do pageIdx)
                        int dataPageIdx = pageIdx - 1; // bo strona 0 to podsumowanie
                        int startRow = dataPageIdx * rowsPerPage;
                        // Na pierwszej stronie tabeli odejmij 1 bo wiersz salda zajmuje miejsce
                        if (dataPageIdx == 0) startRow = 0;
                        int endRow = Math.Min(startRow + rowsPerPage, data.Count);
                        if (dataPageIdx == 0 && _saldo != null) endRow = Math.Min(startRow + rowsPerPage - 1, data.Count); // -1 bo saldo
                        int totalDataPages = Math.Max(1, (int)Math.Ceiling((double)data.Count / Math.Max(1, rowsPerPage)));

                        // Nagłówek tabeli
                        using (var hBr = new SolidBrush(Color.FromArgb(220, 220, 220)))
                            g.FillRectangle(hBr, mb.Left, y, mb.Width, headH);
                        int hx = mb.Left;
                        for (int c = 0; c < colH.Length; c++)
                        {
                            g.DrawRectangle(penTable, hx, y, cw[c], headH);
                            g.DrawString(colH[c], fHead, Brushes.Black,
                                new RectangleF(hx + 4, y, cw[c] - 8, headH),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                            hx += cw[c];
                        }
                        y += headH;

                        // Wiersz Saldo (na pierwszej stronie tabeli)
                        if (dataPageIdx == 0 && _saldo != null)
                        {
                            int cx = mb.Left;
                            // NrDok - puste
                            g.DrawRectangle(penTable, cx, y, cw[0], rowH); cx += cw[0];
                            // Data - puste
                            g.DrawRectangle(penTable, cx, y, cw[1], rowH); cx += cw[1];
                            // Dokumenty - "Saldo DD.MM.YYYY"
                            g.DrawRectangle(penTable, cx, y, cw[2], rowH);
                            g.DrawString($"Saldo {_dtDo.Value:dd.MM.yyyy}", fCellBold, Brushes.Black,
                                new RectangleF(cx + 4, y, cw[2] - 8, rowH),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                            cx += cw[2];
                            // E2, H1, EURO, PCV, DREW
                            int[] saldoVals = { _saldo.SaldoE2, _saldo.SaldoH1, _saldo.SaldoEURO, _saldo.SaldoPCV, _saldo.SaldoDREW };
                            for (int vi = 0; vi < 5; vi++)
                            {
                                g.DrawRectangle(penTable, cx, y, cw[3 + vi], rowH);
                                g.DrawString(saldoVals[vi].ToString(), fCellBold, Brushes.Black,
                                    new RectangleF(cx + 4, y, cw[3 + vi] - 8, rowH),
                                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                                cx += cw[3 + vi];
                            }
                            y += rowH;
                        }

                        // Wiersze dokumentów
                        for (int r = startRow; r < endRow; r++)
                        {
                            var d = data[r];
                            int cx = mb.Left;

                            // NrDok
                            g.DrawRectangle(penTable, cx, y, cw[0], rowH);
                            g.DrawString(d.NrDok ?? "", fCell, Brushes.Black,
                                new RectangleF(cx + 3, y, cw[0] - 6, rowH),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                            cx += cw[0];
                            // Data
                            g.DrawRectangle(penTable, cx, y, cw[1], rowH);
                            g.DrawString(d.Data?.ToString("yyyy-MM-dd") ?? "", fCell, Brushes.Black,
                                new RectangleF(cx + 3, y, cw[1] - 6, rowH),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                            cx += cw[1];
                            // Dokumenty
                            g.DrawRectangle(penTable, cx, y, cw[2], rowH);
                            g.DrawString(d.Dokumenty ?? d.NrDok ?? "", fCell, Brushes.Black,
                                new RectangleF(cx + 3, y, cw[2] - 6, rowH),
                                new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                            cx += cw[2];
                            // E2, H1, EURO, PCV, DREW
                            int[] vals = { d.E2, d.H1, d.EURO, d.PCV, d.DREW };
                            for (int vi = 0; vi < 5; vi++)
                            {
                                g.DrawRectangle(penTable, cx, y, cw[3 + vi], rowH);
                                g.DrawString(vals[vi] > 0 ? $"+{vals[vi]}" : vals[vi].ToString(), fCell, Brushes.Black,
                                    new RectangleF(cx + 3, y, cw[3 + vi] - 6, rowH),
                                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                                cx += cw[3 + vi];
                            }
                            y += rowH;
                        }

                        // Numer strony
                        g.DrawString($"Strona {pageIdx + 1}", fPage, Brushes.Gray,
                            new RectangleF(mb.Left, mb.Bottom - 14, mb.Width, 14),
                            new StringFormat { Alignment = StringAlignment.Center });

                        pageIdx++;
                        e.HasMorePages = endRow < data.Count;
                    }
                };

                var dlg = new PrintPreviewDialog { Document = doc, Width = 900, Height = 700 };
                dlg.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void CopySaldo()
        {
            if (_saldo == null) return;
            var sb = new StringBuilder();
            sb.AppendLine($"Saldo opakowan: {_nazwa}");
            sb.AppendLine($"Data: {DateTime.Today:dd.MM.yyyy}");
            sb.AppendLine($"Handlowiec: {_handlowiec}");
            sb.AppendLine();
            sb.AppendLine($"E2 Pojemnik:     {FmtSaldo(_saldo.SaldoE2, " pojemnikow")}");
            sb.AppendLine($"H1 Paleta:       {FmtSaldo(_saldo.SaldoH1, " palet")}");
            sb.AppendLine($"EURO Paleta:     {FmtSaldo(_saldo.SaldoEURO, " palet")}");
            sb.AppendLine($"PCV Plastikowa:  {FmtSaldo(_saldo.SaldoPCV, " palet")}");
            sb.AppendLine($"DREW Drewniana:  {FmtSaldo(_saldo.SaldoDREW, " palet")}");
            Clipboard.SetText(sb.ToString());
            _lblSaldoEnd.Text = "  Skopiowano do schowka!";
        }

        void CopyRow()
        {
            if (_grid.CurrentRow?.DataBoundItem is DokumentOpakowania d)
            {
                var txt = $"{d.NrDok}\t{d.DataText}\t{d.DzienTyg}\tE2={d.E2}\tH1={d.H1}\tEURO={d.EURO}\tPCV={d.PCV}\tDREW={d.DREW}";
                Clipboard.SetText(txt);
            }
        }

        async Task ExportPdfAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var saldo = _saldo ?? new SaldoOpakowania();
                var path = await _export.EksportujSaldoKontrahentaDoPDFAsync(_nazwa, _id, saldo, _docs, _dtOd.Value, _dtDo.Value);
                if (MessageBox.Show($"Zapisano:\n{path}\n\nOtworzyc?", "PDF", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _export.OtworzPlik(path);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { Cursor = Cursors.Default; }
        }

        void OpenFolderKontrahenta()
        {
            try
            {
                string bazowa = _export.GetSciezkaZapisu();
                string bezp = string.Join("_", (_nazwa ?? "").Trim().Split(Path.GetInvalidFileNameChars()));
                string folder = Path.Combine(bazowa, bezp);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\"");
            }
            catch (Exception ex) { MessageBox.Show("Nie udalo sie otworzyc folderu: " + ex.Message); }
        }

        void SendEmail()
        {
            if (_saldo == null) return;
            var subject = $"Saldo opakowan - {_nazwa}";
            var body = $"Szanowni Panstwo,\n\nPrzesylam stan sald opakowan:\n\n" +
                $"E2: {_saldo.SaldoE2}\nH1: {_saldo.SaldoH1}\nEURO: {_saldo.SaldoEURO}\nPCV: {_saldo.SaldoPCV}\nDREW: {_saldo.SaldoDREW}\n\n" +
                $"Stan na dzien: {DateTime.Today:dd.MM.yyyy}\n\nZ powazaniem";
            _export.WyslijEmail(_saldo.Email ?? "", subject, body);
        }

        #endregion

        #region Helpers

        void PaintSingleChart(Graphics g, int w, int h, string title, Color lineCol, Color areaBgCol, List<HistoriaSaldaPunkt> data)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);
            using (var borderPen = new Pen(CBorder)) g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);

            using (var titleFont = new Font("Segoe UI Semibold", 10))
                g.DrawString(title, titleFont, new SolidBrush(lineCol), 14, 8);

            if (data == null || data.Count < 2)
            {
                using var loadFont = new Font("Segoe UI", 9);
                g.DrawString("Ladowanie...", loadFont, new SolidBrush(CMuted), 14, h / 2 - 8);
                return;
            }

            // Ostatnia wartosc w naglowku — surowa liczba
            var lastVal = data[data.Count - 1].Saldo;
            using (var valFont = new Font("Segoe UI", 12, FontStyle.Bold))
            {
                string valTxt = lastVal.ToString("N0");
                var valSz = g.MeasureString(valTxt, valFont);
                using var valBr = new SolidBrush(lastVal < 0 ? CRed : lastVal > 0 ? CGreen : CGray);
                g.DrawString(valTxt, valFont, valBr, w - valSz.Width - 14, 6);
            }

            // Zakres Y dopasowany do danych (nie symetryczny)
            int dataMin = data.Min(p => p.Saldo);
            int dataMax = data.Max(p => p.Saldo);
            int yMin = Math.Min(dataMin, 0);
            int yMax = Math.Max(dataMax, 0);
            int range = Math.Max(yMax - yMin, 1);
            int yPad = Math.Max(range / 8, 1);
            yMin -= yPad;
            yMax += yPad;
            int totalRange = Math.Max(yMax - yMin, 1);

            int padL = 60, padR = 14, padT = 34, padB = 36;
            int gw = w - padL - padR, gh = h - padT - padB;
            if (gw < 40 || gh < 20) return;

            // Mapowanie wartosci na piksel Y
            float YFor(int val) => padT + (yMax - val) * (float)gh / totalRange;

            // Siatka Y — 5 krokow dopasowanych do zakresu
            using (var gridPen = new Pen(Color.FromArgb(242, 242, 242), 1))
            using (var axFont = new Font("Segoe UI", 7))
            using (var axBr = new SolidBrush(CMuted))
            {
                for (int i = 0; i <= 5; i++)
                {
                    int val = yMin + (int)((long)totalRange * i / 5);
                    float y = YFor(val);
                    g.DrawLine(gridPen, padL, y, w - padR, y);
                    var lbl = val.ToString("N0");
                    var sz = g.MeasureString(lbl, axFont);
                    g.DrawString(lbl, axFont, axBr, padL - sz.Width - 4, y - sz.Height / 2);
                }
            }

            // Linia zera (jesli widoczna w zakresie)
            float zeroY = YFor(0);
            if (zeroY >= padT && zeroY <= padT + gh)
                using (var zp = new Pen(Color.FromArgb(160, 160, 160), 1.5f)) g.DrawLine(zp, padL, zeroY, w - padR, zeroY);

            // Punkty wykresu
            var pts = new PointF[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                float x = padL + (float)i / (data.Count - 1) * gw;
                float y = YFor(data[i].Saldo);
                pts[i] = new PointF(x, Math.Max(padT, Math.Min(padT + gh, y)));
            }

            // Area fill do linii zera
            float areaBase = Math.Max(padT, Math.Min(padT + gh, zeroY));
            using (var areaPath = new GraphicsPath())
            {
                areaPath.AddLines(pts);
                areaPath.AddLine(pts[pts.Length - 1].X, pts[pts.Length - 1].Y, pts[pts.Length - 1].X, areaBase);
                areaPath.AddLine(pts[0].X, areaBase, pts[0].X, pts[0].Y);
                areaPath.CloseFigure();
                using var areaBr = new SolidBrush(Color.FromArgb(30, lineCol));
                g.FillPath(areaBr, areaPath);
            }

            // Linia wykresu
            using (var lp = new Pen(lineCol, 2.5f) { LineJoin = LineJoin.Round }) g.DrawLines(lp, pts);

            // Etykiety dat (os X) + wartosci na niedzielach
            using var dateFont = new Font("Segoe UI", 7);
            using var dateBr = new SolidBrush(CMuted);
            using var sunFont = new Font("Segoe UI", 7, FontStyle.Bold);
            using var sunBr = new SolidBrush(lineCol);
            for (int i = 0; i < data.Count; i++)
            {
                float x = pts[i].X;
                if (data[i].Data.DayOfWeek == DayOfWeek.Sunday)
                {
                    // Data na osi X
                    var dtLbl = data[i].Data.ToString("dd.MM");
                    var sz = g.MeasureString(dtLbl, dateFont);
                    g.DrawString(dtLbl, dateFont, dateBr, x - sz.Width / 2, h - padB + 6);
                    using var tickPen = new Pen(Color.FromArgb(220, 220, 220));
                    g.DrawLine(tickPen, x, h - padB, x, h - padB + 3);

                    // Wartosc — surowa liczba (pasuje do pozycji linii)
                    var vTxt = data[i].Saldo.ToString("N0");
                    var vSz = g.MeasureString(vTxt, sunFont);
                    float vy = pts[i].Y - vSz.Height - 3;
                    if (vy < padT) vy = pts[i].Y + 4;
                    g.DrawString(vTxt, sunFont, sunBr, x - vSz.Width / 2, vy);

                    // Punkt
                    using (var db = new SolidBrush(lineCol)) g.FillEllipse(db, x - 3.5f, pts[i].Y - 3.5f, 7, 7);
                    using (var wp = new Pen(Color.White, 1.5f)) g.DrawEllipse(wp, x - 3.5f, pts[i].Y - 3.5f, 7, 7);
                }
            }

            // Punkt koncowy
            var lastPt = pts[pts.Length - 1];
            using (var db2 = new SolidBrush(lineCol)) g.FillEllipse(db2, lastPt.X - 4, lastPt.Y - 4, 8, 8);
            using (var wp2 = new Pen(Color.White, 2)) g.DrawEllipse(wp2, lastPt.X - 4, lastPt.Y - 4, 8, 8);
        }

        void ChartPanelMouseMove(Panel panel, string seriesKey, MouseEventArgs e)
        {
            _chartTip ??= new ToolTip { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 5000, BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.White };
            var data = _historiaAll.GetValueOrDefault(seriesKey);
            if (data == null || data.Count < 2) { _chartTip.Hide(panel); return; }
            int w = panel.Width, padL = 60, padR = 14;
            int gw = w - padL - padR;
            if (gw < 10 || e.X < padL || e.X > w - padR) { _chartTip.Hide(panel); return; }

            float relX = (float)(e.X - padL) / gw;
            int idx = Math.Clamp((int)(relX * (data.Count - 1) + 0.5f), 0, data.Count - 1);
            var p = data[idx];
            var txt = $"{p.Data:dd.MM.yyyy} ({p.Data.ToString("ddd")})\n{seriesKey}: {FmtSaldo(p.Saldo)}";
            _chartTip.Show(txt, panel, e.X + 12, e.Y - 25, 3000);
        }

        static Color VCol(int v) => v < 0 ? CRed : v > 0 ? CGreen : CGray;
        static readonly Color[] _ac = { Color.FromArgb(59,130,246), Color.FromArgb(249,115,22), Color.FromArgb(16,185,129), Color.FromArgb(139,92,246), Color.FromArgb(236,72,153), Color.FromArgb(245,158,11) };
        static Color AvCol(string n) => _ac[Math.Abs((n ?? "").GetHashCode()) % _ac.Length];
        static string Ini(string n) { if (string.IsNullOrWhiteSpace(n) || n == "-") return "?"; var p = n.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries); return p.Length >= 2 ? $"{p[0][0]}{p[1][0]}".ToUpper() : n[..Math.Min(2, n.Length)].ToUpper(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { CopySaldo(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { _avatarHandl?.Dispose(); base.OnFormClosed(e); }

        #endregion
    }
}
