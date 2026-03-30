using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        // Layout
        Panel _header, _cardStrip, _infoPanel, _bottomBar;
        Label[] _crdVal = new Label[5], _crdDelta = new Label[5];
        DataGridView _grid;
        Label _lblCount, _lblSaldoEnd;

        // Info sidebar
        Label _lblInfoNazwa, _lblInfoHandl, _lblInfoEmail, _lblInfoTel;
        Label _lblStatWyd, _lblStatPrzyj, _lblStatDni, _lblStatPotw, _lblStatBilans, _lblStatZwrot;

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
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = Color.FromArgb(246, 248, 250);
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // --- Create all controls first ---
            BuildHeader();
            BuildCardStrip();
            BuildBottom();
            BuildInfoPanel();
            var gridHost = BuildGridHost();

            // --- Add in correct order: Fill first, then edges ---
            Controls.Add(gridHost);
            Controls.Add(_infoPanel);
            Controls.Add(_bottomBar);
            Controls.Add(_cardStrip);
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

        void BuildBottom()
        {
            _bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(249, 250, 251) };
            _bottomBar.Paint += (_, e) => { using var p = new Pen(CBorder); e.Graphics.DrawLine(p, 0, 0, _bottomBar.Width, 0); };

            _lblSaldoEnd = new Label { Dock = DockStyle.Fill, Text = "", ForeColor = CSlate, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };

            var flow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0, 3, 8, 0) };
            var btnPotw = MkBtn("Potwierdz saldo", Color.FromArgb(22, 163, 74)); btnPotw.Click += (_, __) => DodajPotwierdzenie();
            _btnCopy = MkBtn("Kopiuj saldo", Color.FromArgb(99, 102, 241)); _btnCopy.Click += (_, __) => CopySaldo();
            _btnPdf = MkBtn("PDF", Color.FromArgb(37, 99, 235)); _btnPdf.Click += async (_, __) => await ExportPdfAsync();
            _btnEmail = MkBtn("Email", Color.FromArgb(5, 150, 105)); _btnEmail.Click += (_, __) => SendEmail();
            var btnClose = MkBtn("Zamknij", Color.FromArgb(100, 116, 139)); btnClose.Click += (_, __) => Close();
            flow.Controls.AddRange(new Control[] { btnPotw, _btnCopy, _btnPdf, _btnEmail, btnClose });

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
                new DataGridViewTextBoxColumn { Name = "Data", HeaderText = "Data", DataPropertyName = "DataText", Width = 82 },
                new DataGridViewTextBoxColumn { Name = "Dzien", HeaderText = "Dzien", DataPropertyName = "DzienTyg", Width = 45 },
                RCol("E2", 60), RCol("H1", 60), RCol("EURO", 60), RCol("PCV", 60), RCol("DREW", 60),
            });
            _grid.CellFormatting += FmtGrid;
            _grid.CellPainting += PaintCells;
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
                Color bar = d.JestSaldem ? CYellow : d.TypDokumentu == "MW1" ? CRed : CGreen;
                bool sel = e.State.HasFlag(DataGridViewElementStates.Selected);
                using (var bg = new SolidBrush(sel ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor)) e.Graphics.FillRectangle(bg, e.CellBounds);
                using (var br = new SolidBrush(bar)) e.Graphics.FillRectangle(br, e.CellBounds.X, e.CellBounds.Y + 2, 4, e.CellBounds.Height - 4);
            }

            // Kolumna Typ — "Wydanie" czerwone, "Przyjecie" zielone, "SALDO" zolte
            if (cn == "TypTxt")
            {
                e.Handled = true;
                bool sel = e.State.HasFlag(DataGridViewElementStates.Selected);
                using (var bg = new SolidBrush(sel ? e.CellStyle.SelectionBackColor : (d.JestSaldem ? Color.FromArgb(255, 251, 235) : e.CellStyle.BackColor))) e.Graphics.FillRectangle(bg, e.CellBounds);

                string txt; Color col; Font fnt;
                if (d.JestSaldem) { txt = "SALDO"; col = Color.FromArgb(161, 98, 7); fnt = new Font("Segoe UI", 8, FontStyle.Bold); }
                else if (d.TypDokumentu == "MW1") { txt = "Wydanie"; col = CRed; fnt = new Font("Segoe UI", 8.5f, FontStyle.Bold); }
                else { txt = "Przyjecie"; col = CGreen; fnt = new Font("Segoe UI", 8.5f, FontStyle.Bold); }

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
            if (v < 0) return $"Winny {num}{unit}";
            if (v > 0) return $"Wisimy {num}{unit}";
            return "-";
        }

        void FmtGrid(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var cn = _grid.Columns[e.ColumnIndex].Name;
            if (cn is "E2" or "H1" or "EURO" or "PCV" or "DREW" && e.Value is int v)
            {
                e.CellStyle.ForeColor = v < 0 ? CRed : v > 0 ? CGreen : Color.FromArgb(200, 200, 200);
                if (v == 0) { e.Value = "-"; e.FormattingApplied = true; }
                else
                {
                    var unit = cn == "E2" ? " poj." : " pal.";
                    e.Value = FmtSaldo(v, unit);
                    e.FormattingApplied = true;
                }
            }
            if (_grid.Rows[e.RowIndex].DataBoundItem is DokumentOpakowania d)
            {
                if (d.JestSaldem)
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 251, 235);
                    e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                }
                else if (cn is "NrDok" or "Data" or "Dzien")
                {
                    if (d.TypDokumentu == "MW1") e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                    else e.CellStyle.ForeColor = Color.FromArgb(20, 83, 45);
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
                        _crdDelta[i].Text = v[i] < 0 ? $"Winny{units[i]}" : v[i] > 0 ? $"Wisimy{units[i]}" : "";
                        _crdDelta[i].ForeColor = VCol(v[i]);
                    }
                    _lblInfoEmail.Text = _saldo.Email ?? "-";
                    _lblInfoTel.Text = _saldo.Telefon ?? "-";
                }

                // Dokumenty (3 miesiace)
                _docs = await _ds.PobierzSaldoKontrahentaAsync(_id, DateTime.Today.AddMonths(-3), DateTime.Today);
                _grid.SuspendLayout();
                _grid.DataSource = new SortableBindingList<DokumentOpakowania>(_docs);
                _grid.ResumeLayout();

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
                _lblStatBilans.Text = $"Bilans: {(bilans > 0 ? "Winny" : bilans < 0 ? "Wisimy" : "")} {Math.Abs(bilans).ToString("N0")} szt.";
                _lblStatBilans.ForeColor = bilans > 0 ? CRed : bilans < 0 ? CGreen : CGray;
                _lblStatZwrot.Text = sumWyd > 0 ? $"Zwrot: {sumPrzyj * 100.0 / sumWyd:F0}%" : "Zwrot: -";

                var lastDoc = _docs.Where(d => !d.JestSaldem).FirstOrDefault();
                int dniOd = lastDoc?.Data != null ? (DateTime.Today - lastDoc.Data.Value).Days : -1;
                _lblStatDni.Text = dniOd >= 0 ? $"Ost. dokument: {dniOd} dni temu" : "Ost. dokument: -";
                _lblStatDni.ForeColor = dniOd > 30 ? CRed : dniOd > 14 ? Color.FromArgb(245, 158, 11) : CGreen;

                // Potwierdzenia
                _potw = await _ds.PobierzPotwierdzeniaDlaKontrahentaAsync(_id);
                int potwOk = _potw.Count(p => p.StatusPotwierdzenia == "Potwierdzone");
                _lblStatPotw.Text = $"Potwierdzenia: {potwOk}/{_potw.Count}";
                _lblStatPotw.ForeColor = _potw.Count == 0 ? CGray : potwOk == _potw.Count ? CGreen : Color.FromArgb(245, 158, 11);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Cursor = Cursors.Default; }
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

            // Pokaz menu z wyborem typu opakowania
            var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 10) };
            var typy = new[] { ("E2", _saldo.SaldoE2), ("H1", _saldo.SaldoH1), ("EURO", _saldo.SaldoEURO), ("PCV", _saldo.SaldoPCV), ("DREW", _saldo.SaldoDREW) };

            foreach (var (kod, saldo) in typy)
            {
                if (saldo == 0) continue; // pomijaj zerowe
                var typ = TypOpakowania.WszystkieTypy.FirstOrDefault(t => t.Kod == kod);
                if (typ == null) continue;
                var txt = $"{kod}: {Math.Abs(saldo)} ({(saldo < 0 ? "winny" : "wisimy")})";
                var k = kod; var s = saldo; var tp = typ;
                menu.Items.Add(txt, null, (_, __) =>
                {
                    var okno = new Views.DodajPotwierdzenieWindow(_id, _nazwa, _nazwa, tp, s, _userId);
                    if (okno.ShowDialog() == true)
                        _ = LoadAsync(); // odswiez po dodaniu
                });
            }

            if (menu.Items.Count == 0)
            {
                MessageBox.Show("Brak sald do potwierdzenia (wszystkie zerowe).", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            menu.Show(Cursor.Position);
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
                var path = await _export.EksportujSaldoKontrahentaDoPDFAsync(_nazwa, _id, saldo, _docs, DateTime.Today.AddMonths(-3), DateTime.Today);
                if (MessageBox.Show($"Zapisano:\n{path}\n\nOtworzyc?", "PDF", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _export.OtworzPlik(path);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { Cursor = Cursors.Default; }
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
