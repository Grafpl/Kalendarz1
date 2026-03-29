using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
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
        SaldoOpakowania _saldo;
        Image _avatarHandl;

        // Controls
        Panel _header, _cardStrip, _infoPanel, _bottomBar;
        Label[] _crdVal = new Label[5];
        Label[] _crdDelta = new Label[5];
        DataGridView _grid;
        Label _lblCount, _lblSaldoEnd;
        Label _lblInfoNazwa, _lblInfoHandl, _lblInfoEmail, _lblInfoTel;
        Label _lblStatWyd, _lblStatPrzyj, _lblStatDni, _lblStatPotw;

        public SzczegolyKontrahentaForm(int id, string nazwa, string handlowiec, SaldoOpakowania saldo, string userId, OpakowaniaDataService ds)
        {
            _id = id; _nazwa = nazwa; _handlowiec = handlowiec ?? "-"; _saldo = saldo; _userId = userId; _ds = ds;
            BuildUI();
            Load += async (_, __) => await LoadAsync();
        }

        void BuildUI()
        {
            Text = $"{_nazwa} — Opakowania";
            Size = new Size(1300, 800);
            MinimumSize = new Size(950, 550);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = Color.FromArgb(246, 248, 250);
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // === Tworzenie kontrolek (jeszcze nie dodajemy do Controls) ===

            _header = new Panel { Dock = DockStyle.Top, Height = 70 };
            _header.Paint += PaintHeader;

            _cardStrip = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White };
            _cardStrip.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(226, 232, 240)); e.Graphics.DrawLine(p, 0, _cardStrip.Height - 1, _cardStrip.Width, _cardStrip.Height - 1); };
            string[] cn = { "E2", "H1", "EURO", "PCV", "DREW" };
            Color[] cc = { Color.FromArgb(59,130,246), Color.FromArgb(249,115,22), Color.FromArgb(16,185,129), Color.FromArgb(139,92,246), Color.FromArgb(245,158,11) };
            for (int i = 0; i < 5; i++)
            {
                var p = new Panel { BackColor = Color.Transparent, Tag = cc[i] };
                p.Paint += (s, e) =>
                {
                    var cl = (Color)((Panel)s).Tag;
                    using (var br = new SolidBrush(cl)) e.Graphics.FillEllipse(br, 10, 10, 10, 10);
                };
                var lN = new Label { Text = cn[i], Location = new Point(26, 6), AutoSize = true, ForeColor = Color.FromArgb(100,116,139), Font = new Font("Segoe UI", 8, FontStyle.Bold), BackColor = Color.Transparent };
                _crdVal[i] = new Label { Text = "--", Location = new Point(26, 22), AutoSize = true, ForeColor = cc[i], Font = new Font("Segoe UI", 16, FontStyle.Bold), BackColor = Color.Transparent };
                _crdDelta[i] = new Label { Text = "", Location = new Point(90, 28), AutoSize = true, ForeColor = Color.FromArgb(100,116,139), Font = new Font("Segoe UI", 8), BackColor = Color.Transparent };
                p.Controls.AddRange(new Control[] { lN, _crdVal[i], _crdDelta[i] });
                _cardStrip.Controls.Add(p);
            }
            _cardStrip.Resize += (_, __) =>
            {
                var cs = _cardStrip.Controls.OfType<Panel>().ToArray();
                int w = (_cardStrip.ClientSize.Width - 10) / 5;
                for (int j = 0; j < cs.Length; j++) cs[j].SetBounds(j * w, 0, w, _cardStrip.Height - 1);
            };

            _bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.White };
            _bottomBar.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(226, 232, 240)); e.Graphics.DrawLine(p, 0, 0, _bottomBar.Width, 0); };
            _lblSaldoEnd = new Label { Dock = DockStyle.Fill, Text = "", ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
            var btnClose = new Button { Text = "Zamknij", Dock = DockStyle.Right, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(100, 116, 139), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => Close();
            _bottomBar.Controls.Add(_lblSaldoEnd);
            _bottomBar.Controls.Add(btnClose);

            _infoPanel = new Panel { Dock = DockStyle.Right, Width = 260, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(16, 12, 16, 12) };
            _infoPanel.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(226, 232, 240)); e.Graphics.DrawLine(p, 0, 0, 0, _infoPanel.Height); };
            BuildInfoPanel();

            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _lblCount = new Label { Dock = DockStyle.Top, Height = 28, Text = "  Dokumenty", ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.FromArgb(248, 250, 252) };
            _grid = MkGrid();
            _grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Typ", HeaderText = "", Width = 6 },
                new DataGridViewTextBoxColumn { Name = "NrDok", HeaderText = "Dokument", DataPropertyName = "NrDok", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120 },
                new DataGridViewTextBoxColumn { Name = "Data", HeaderText = "Data", DataPropertyName = "DataText", Width = 85 },
                new DataGridViewTextBoxColumn { Name = "Dzien", HeaderText = "Dzien", DataPropertyName = "DzienTyg", Width = 50 },
                RCol("E2", 60), RCol("H1", 60), RCol("EURO", 60), RCol("PCV", 60), RCol("DREW", 60),
            });
            _grid.CellFormatting += FmtGrid;
            _grid.CellPainting += PaintTypCol;
            gridHost.Controls.Add(_grid);
            gridHost.Controls.Add(_lblCount);

            // === Dodanie do Controls w POPRAWNEJ kolejnosci ===
            // WinForms Dock: Fill PIERWSZY, potem Bottom, Right, Top (odwrotnie niz intuicja)
            Controls.Add(gridHost);      // Fill — musi byc pierwszy
            Controls.Add(_infoPanel);    // Right
            Controls.Add(_bottomBar);    // Bottom
            Controls.Add(_cardStrip);    // Top (pod headerem)
            Controls.Add(_header);       // Top (na samej gorze)
        }

        void BuildInfoPanel()
        {
            int y = 0;
            // Kontrahent
            AddInfoSection("KONTRAHENT", ref y);
            _lblInfoNazwa = AddInfoValue(_nazwa, ref y, new Font("Segoe UI", 10, FontStyle.Bold));
            y += 8;

            // Handlowiec
            AddInfoSection("HANDLOWIEC", ref y);
            _lblInfoHandl = AddInfoValue(_handlowiec, ref y);
            y += 8;

            // Kontakt
            AddInfoSection("KONTAKT", ref y);
            _lblInfoEmail = AddInfoValue("-", ref y);
            _lblInfoTel = AddInfoValue("-", ref y);
            y += 8;

            // Separator
            _infoPanel.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(260, 1), BackColor = Color.FromArgb(226, 232, 240) });
            y += 12;

            // Statystyki
            AddInfoSection("STATYSTYKI (3 mies.)", ref y);
            _lblStatWyd = AddInfoValue("Wydania: --", ref y);
            _lblStatPrzyj = AddInfoValue("Przyjecia: --", ref y);
            _lblStatDni = AddInfoValue("Dni od ost. dok.: --", ref y);
            _lblStatPotw = AddInfoValue("Potwierdzenia: --", ref y);
        }

        void AddInfoSection(string title, ref int y)
        {
            var l = new Label { Text = title, Location = new Point(16, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), BackColor = Color.Transparent };
            _infoPanel.Controls.Add(l);
            y += 16;
        }

        Label AddInfoValue(string text, ref int y, Font font = null)
        {
            var l = new Label { Text = text, Location = new Point(16, y), AutoSize = true, MaximumSize = new Size(228, 0), ForeColor = Color.FromArgb(15, 23, 42), Font = font ?? new Font("Segoe UI", 9.5f), BackColor = Color.Transparent };
            _infoPanel.Controls.Add(l);
            y += (int)(l.Font.Size * 2.2f) + 2;
            return l;
        }

        DataGridViewTextBoxColumn RCol(string name, int w) => new DataGridViewTextBoxColumn
        {
            Name = name, HeaderText = name, DataPropertyName = name, Width = w,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) },
            SortMode = DataGridViewColumnSortMode.Automatic
        };

        DataGridView MkGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, AutoGenerateColumns = false, RowHeadersVisible = false,
                BackgroundColor = Color.White, GridColor = Color.FromArgb(241, 245, 249),
                BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), SelectionBackColor = Color.FromArgb(248, 250, 252) },
                ColumnHeadersHeight = 34, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9.5f), Padding = new Padding(4, 2, 4, 2), SelectionBackColor = Color.FromArgb(209, 250, 229), SelectionForeColor = Color.FromArgb(15, 23, 42) },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(249, 250, 251) },
                RowTemplate = { Height = 34 }, EnableHeadersVisualStyles = false
            };
            // DoubleBuffered
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(g, true);
            // Hover
            int hov = -1;
            g.CellMouseEnter += (_, e) => { if (e.RowIndex >= 0) { hov = e.RowIndex; g.InvalidateRow(e.RowIndex); } };
            g.CellMouseLeave += (_, e) => { if (e.RowIndex >= 0) { var p = hov; hov = -1; if (p >= 0 && p < g.Rows.Count) g.InvalidateRow(p); } };
            g.RowPrePaint += (_, e) => { if (e.RowIndex == hov && !e.State.HasFlag(DataGridViewElementStates.Selected)) { using var br = new SolidBrush(Color.FromArgb(236, 253, 245)); e.Graphics.FillRectangle(br, e.RowBounds); } };
            return g;
        }

        // Kolorowy pasek po lewej stronie wiersza
        void PaintTypCol(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.RowIndex < 0) return;
            e.Handled = true;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not DokumentOpakowania d) return;

            Color barColor;
            if (d.JestSaldem) barColor = Color.FromArgb(250, 204, 21); // zolty = saldo
            else if (d.TypDokumentu == "MW1") barColor = Color.FromArgb(220, 38, 38); // czerwony = wydanie
            else barColor = Color.FromArgb(22, 163, 74); // zielony = przyjecie

            bool sel = e.State.HasFlag(DataGridViewElementStates.Selected);
            using (var bg = new SolidBrush(sel ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor))
                e.Graphics.FillRectangle(bg, e.CellBounds);
            using (var br = new SolidBrush(barColor))
                e.Graphics.FillRectangle(br, e.CellBounds.X, e.CellBounds.Y + 2, 4, e.CellBounds.Height - 4);
        }

        void FmtGrid(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var cn = _grid.Columns[e.ColumnIndex].Name;
            if (cn is "E2" or "H1" or "EURO" or "PCV" or "DREW" && e.Value is int v)
            {
                e.CellStyle.ForeColor = v > 0 ? Color.FromArgb(220, 38, 38) : v < 0 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(200, 200, 200);
                if (v == 0) e.Value = "-";
            }
            if (_grid.Rows[e.RowIndex].DataBoundItem is DokumentOpakowania d && d.JestSaldem)
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 251, 235);
                e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new LinearGradientBrush(new Rectangle(0, 0, _header.Width, _header.Height), Color.FromArgb(15, 23, 42), Color.FromArgb(30, 58, 95), LinearGradientMode.Horizontal);
            g.FillRectangle(bg, 0, 0, _header.Width, _header.Height);

            // Avatar kontrahenta
            int s = 44, x = 20, y = (_header.Height - s) / 2;
            using (var br = new SolidBrush(Color.FromArgb(34, 197, 94))) g.FillEllipse(br, x, y, s, s);
            var ini = _nazwa.Length >= 2 ? _nazwa[..2].ToUpper() : "?";
            using (var f = new Font("Segoe UI", 14, FontStyle.Bold)) { var sz = g.MeasureString(ini, f); g.DrawString(ini, f, Brushes.White, x + (s - sz.Width) / 2, y + (s - sz.Height) / 2); }

            int tx = x + s + 14;
            using (var f = new Font("Segoe UI", 14, FontStyle.Bold)) g.DrawString(_nazwa, f, Brushes.White, tx, 10);

            // Handlowiec
            int my = 40;
            if (_avatarHandl != null)
                g.DrawImage(_avatarHandl, tx, my, 18, 18);
            else
            {
                using (var br = new SolidBrush(AvCol(_handlowiec))) g.FillEllipse(br, tx, my, 18, 18);
                var hi = Ini(_handlowiec);
                using var hf = new Font("Segoe UI", 6, FontStyle.Bold);
                var hs = g.MeasureString(hi, hf);
                g.DrawString(hi, hf, Brushes.White, tx + (18 - hs.Width) / 2, my + (18 - hs.Height) / 2);
            }
            using (var f = new Font("Segoe UI", 9)) using (var br = new SolidBrush(Color.FromArgb(148, 163, 184))) g.DrawString(_handlowiec, f, br, tx + 22, my + 1);

            // ID
            using (var f = new Font("Segoe UI", 8)) using (var br = new SolidBrush(Color.FromArgb(100, 116, 139))) g.DrawString($"ID: {_id}", f, br, _header.Width - 80, 10);
        }

        async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                // Avatar (w tle)
                _ = LoadAvatarAsync();

                // Saldo
                if (_saldo == null) _saldo = await _ds.PobierzSaldaWszystkichOpakowannAsync(_id, DateTime.Today);
                if (_saldo != null)
                {
                    int[] v = { _saldo.SaldoE2, _saldo.SaldoH1, _saldo.SaldoEURO, _saldo.SaldoPCV, _saldo.SaldoDREW };
                    for (int i = 0; i < 5; i++)
                    {
                        _crdVal[i].Text = v[i].ToString();
                        _crdVal[i].ForeColor = VCol(v[i]);
                        _crdDelta[i].Text = v[i] > 0 ? "winni" : v[i] < 0 ? "zwrot" : "";
                        _crdDelta[i].ForeColor = VCol(v[i]);
                    }

                    _lblInfoEmail.Text = _saldo.Email ?? "-";
                    _lblInfoTel.Text = _saldo.Telefon ?? "-";
                }

                // Dokumenty
                var docs = await _ds.PobierzSaldoKontrahentaAsync(_id, DateTime.Today.AddMonths(-3), DateTime.Today);
                _grid.SuspendLayout();
                _grid.DataSource = new BindingList<DokumentOpakowania>(docs);
                _grid.ResumeLayout();

                int docCount = docs.Count(d => !d.JestSaldem);
                int saldoCount = docs.Count(d => d.JestSaldem);
                int wydania = docs.Count(d => !d.JestSaldem && d.TypDokumentu == "MW1");
                int przyjecia = docs.Count(d => !d.JestSaldem && d.TypDokumentu == "MP");
                _lblCount.Text = $"  Dokumenty ({docCount})  |  Wydania: {wydania}  Przyjecia: {przyjecia}";

                // Saldo koncowe
                var lastSaldo = docs.FirstOrDefault(d => d.JestSaldem);
                if (lastSaldo != null)
                    _lblSaldoEnd.Text = $"  Saldo:  E2={lastSaldo.E2}   H1={lastSaldo.H1}   EURO={lastSaldo.EURO}   PCV={lastSaldo.PCV}   DREW={lastSaldo.DREW}";

                // Statystyki
                _lblStatWyd.Text = $"Wydania: {wydania}";
                _lblStatPrzyj.Text = $"Przyjecia: {przyjecia}";
                var lastDoc = docs.Where(d => !d.JestSaldem).FirstOrDefault();
                _lblStatDni.Text = lastDoc != null ? $"Ost. dok.: {(DateTime.Today - (lastDoc.Data ?? DateTime.Today)).Days} dni temu" : "Ost. dok.: -";

                // Potwierdzenia
                var potw = await _ds.PobierzPotwierdzeniaDlaKontrahentaAsync(_id);
                int potwOk = potw.Count(p => p.StatusPotwierdzenia == "Potwierdzone");
                _lblStatPotw.Text = $"Potwierdzenia: {potwOk}/{potw.Count}";
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
                using (var cmd = new SqlCommand("SELECT TOP 1 UserID FROM UserHandlowcy WHERE HandlowiecName = @N", conn))
                { cmd.Parameters.AddWithValue("@N", _handlowiec); uid = (await cmd.ExecuteScalarAsync())?.ToString(); }
                if (uid == null)
                    using (var cmd = new SqlCommand("SELECT TOP 1 ID FROM operators WHERE Name = @N", conn))
                    { cmd.Parameters.AddWithValue("@N", _handlowiec); uid = (await cmd.ExecuteScalarAsync())?.ToString(); }
                uid ??= _handlowiec;
                _avatarHandl = UserAvatarManager.HasAvatar(uid) ? UserAvatarManager.GetAvatarRounded(uid, 18) : UserAvatarManager.GenerateDefaultAvatar(_handlowiec, uid, 18);
                _header.Invalidate();
            }
            catch { }
        }

        static Color VCol(int v) => v > 0 ? Color.FromArgb(220, 38, 38) : v < 0 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(156, 163, 175);
        static readonly Color[] _ac = { Color.FromArgb(59,130,246), Color.FromArgb(249,115,22), Color.FromArgb(16,185,129), Color.FromArgb(139,92,246), Color.FromArgb(236,72,153), Color.FromArgb(245,158,11) };
        static Color AvCol(string n) => _ac[Math.Abs((n ?? "").GetHashCode()) % _ac.Length];
        static string Ini(string n) { if (string.IsNullOrWhiteSpace(n) || n == "-") return "?"; var p = n.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries); return p.Length >= 2 ? $"{p[0][0]}{p[1][0]}".ToUpper() : n[..Math.Min(2, n.Length)].ToUpper(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == Keys.Escape) { Close(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        protected override void OnFormClosed(FormClosedEventArgs e) { _avatarHandl?.Dispose(); base.OnFormClosed(e); }
    }
}
