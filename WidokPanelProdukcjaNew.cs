using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public class WidokPanelProdukcja : Form
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public string UserID { get; set; } = string.Empty;
        private DateTime _selectedDate = DateTime.Today;

        // UI
        private Label lblData, lblUser, lblStats, lblIn0ERefresh;
        private Button btnPrev, btnNext, btnRefresh, btnClose, btnUndo, btnLive, btnSaveNotes, btnZrealizowano;
        private DataGridView dgvZamowienia;
        private DataGridView dgvPozycje;
        private TextBox txtUwagi;
        private TextBox txtNotatkiTransportu; // Nowe pole dla notatek transportu
        private DataGridView dgvPojTuszki;
        private DataGridView dgvIn0ESumy; // jeden widok przychodów
        private ComboBox cbFiltrProdukt;
        private Timer refreshTimer;
        private Panel panelSzczegolyTransportu; // Nowy panel dla szczegółów transportu

        // Cache / state
        private readonly Dictionary<int, ZamowienieInfo> _zamowienia = new();
        private bool _notesTableEnsured = false;
        private int? _filteredProductId = null;
        private Dictionary<int, string> _produktLookup = new();
        private readonly HashSet<int> _orderIdsOnGrid = new();

        private static readonly string[] OffalKeywords = { "wątroba", "watrob", "serce", "serca", "żołąd", "zolad", "żołądki", "zoladki" };

        private sealed class ZamowienieInfo
        {
            // Changed fields to properties to enable proper data binding in DataGridView
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public string Uwagi { get; set; } = "";
            public string Status { get; set; } = "";
            public decimal TotalIlosc { get; set; }
            public bool IsShipmentOnly { get; set; } = false;
            public DateTime? DataUtworzenia { get; set; } // Dodane pole DataUtworzenia
            public bool MaNotatke { get; set; } = false; // Nowe pole dla ikony notatki
        }
        private sealed class ContractorInfo { public int Id; public string Shortcut = ""; public string Handlowiec = "(Brak)"; }
        private sealed class TowarInfo { public int Id; public string Kod = ""; }
        private sealed class NodeContext { public int? OrderId; public int ClientId; public bool IsShipmentOnly; }

        public WidokPanelProdukcja()
        {
            BuildUi();
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); if (e.KeyCode == Keys.Enter) TryOpenShipmentDetails(); };
            Shown += async (s, e) => await ReloadAllAsync();
            StartAutoRefresh();
        }

        #region UI
        private void BuildUi()
        {
            BackColor = Color.FromArgb(28, 30, 38); // Ciemnoszary niebieski
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 11f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) }; // Zwiększony padding
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // TOP BAR (jedna poprawna instancja)
            var top = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Color.FromArgb(40, 42, 50) }; // Ciemny panel
            lblData = new Label { AutoSize = true, Left = 16, Top = 10, Font = new Font("Segoe UI Semibold", 30f, FontStyle.Bold) };
            lblUser = new Label { AutoSize = true, Left = 18, Top = 72, Font = new Font("Segoe UI", 11f, FontStyle.Italic), ForeColor = Color.LightGray };
            lblStats = new Label { AutoSize = true, Left = 320, Top = 76, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.Khaki };

            var lblFiltr = new Label { Text = "Towar:", Left = 320, Top = 18, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
            cbFiltrProdukt = new ComboBox { Left = 380, Top = 14, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11f) };
            cbFiltrProdukt.SelectedIndexChanged += async (s, e) => { if (cbFiltrProdukt.SelectedItem is ComboItem it) { _filteredProductId = it.Value == 0 ? null : it.Value; await LoadOrdersAsync(); await LoadPozycjeForSelectedAsync(); } };

            // Przyciski przesunięte na prawo
            int rightMargin = 15;
            btnClose = MakeTopButton("X", 0, (s, e) => Close(), Color.FromArgb(170, 40, 50));
            btnClose.Width = 70; btnClose.Height = 70; btnClose.Top = 28;
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Left = top.Width - btnClose.Width - rightMargin;

            btnZrealizowano = MakeTopButton("ZREAL.", 0, async (s, e) => await MarkOrderRealizedAsync(), Color.FromArgb(25, 135, 75));
            btnZrealizowano.Width = 120; btnZrealizowano.Height = 70; btnZrealizowano.Top = 28; btnZrealizowano.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            btnZrealizowano.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnZrealizowano.Left = btnClose.Left - btnZrealizowano.Width - rightMargin;

            btnSaveNotes = MakeTopButton("Zapisz not.", 0, async (s, e) => await SaveItemNotesAsync(), Color.FromArgb(95, 95, 95));
            btnSaveNotes.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveNotes.Left = btnZrealizowano.Left - btnSaveNotes.Width - rightMargin;

            btnUndo = MakeTopButton("Cofnij", 0, async (s, e) => await UndoRealizedAsync(), Color.FromArgb(140, 100, 60));
            btnUndo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUndo.Left = btnSaveNotes.Left - btnUndo.Width - rightMargin;

            btnLive = MakeTopButton("Live", 0, (s, e) => OpenLiveWindow(), Color.FromArgb(85, 90, 140));
            btnLive.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLive.Left = btnUndo.Left - btnLive.Width - rightMargin;

            btnRefresh = MakeTopButton("Odśwież", 0, async (s, e) => await ReloadAllAsync(), Color.FromArgb(55, 110, 160));
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Left = btnLive.Left - btnRefresh.Width - rightMargin;

            // Nowy przycisk "Dziś"
            Button btnToday = MakeTopButton("Dziś", 0, (s, e) => { _selectedDate = DateTime.Today; _ = ReloadAllAsync(); }, Color.FromArgb(60, 70, 110));
            btnToday.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnToday.Left = btnRefresh.Left - btnToday.Width - rightMargin;

            btnNext = MakeTopButton("▶", 0, (s, e) => { _selectedDate = _selectedDate.AddDays(1); _ = ReloadAllAsync(); }, Color.FromArgb(60, 70, 110));
            btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNext.Left = btnToday.Left - btnNext.Width - rightMargin;

            btnPrev = MakeTopButton("◀", 0, (s, e) => { _selectedDate = _selectedDate.AddDays(-1); _ = ReloadAllAsync(); }, Color.FromArgb(60, 70, 110));
            btnPrev.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPrev.Left = btnNext.Left - btnPrev.Width - rightMargin;

            top.Controls.AddRange(new Control[] { lblData, lblUser, lblStats, lblFiltr, cbFiltrProdukt, btnPrev, btnNext, btnToday, btnRefresh, btnLive, btnUndo, btnSaveNotes, btnZrealizowano, btnClose });
            root.Controls.Add(top, 0, 0);

            // MAIN – przychody, zamówienia, pozycje + notatki
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(4) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47)); // Zwiększono dla zamówień
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); // Zmniejszono dla notatek
            root.Controls.Add(main, 0, 1);

            // Kolumna 1: Przychody i panel tuszek
            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(34, 36, 46), ColumnCount = 1, RowCount = 2 };
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); // Przychody
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); // Panel Poj tuszki

            dgvIn0ESumy = CreateGrid(true);
            dgvIn0ESumy.Dock = DockStyle.Fill;
            dgvIn0ESumy.RowTemplate.Height = 32;
            dgvIn0ESumy.AllowUserToResizeRows = false;
            bottom.Controls.Add(dgvIn0ESumy, 0, 0);

            // Panel Poj tuszki - przeniesiony tutaj
            var panelPoj = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 47, 58), Padding = new Padding(4) };
            var lblPoj = new Label { Text = "Tuszek poj/palety", AutoSize = true, Left = 4, Top = 2, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.LightSkyBlue };
            dgvPojTuszki = new DataGridView { Left = 4, Top = 22, Width = panelPoj.Width - 8, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ColumnHeadersVisible = true, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(45, 47, 58), BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f), ScrollBars = ScrollBars.Vertical, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, EnableHeadersVisualStyles = false };
            dgvPojTuszki.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(80, 82, 98);
            dgvPojTuszki.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvPojTuszki.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgvPojTuszki.DefaultCellStyle.BackColor = Color.FromArgb(60, 62, 75);
            dgvPojTuszki.DefaultCellStyle.ForeColor = Color.White;
            dgvPojTuszki.DefaultCellStyle.SelectionBackColor = Color.FromArgb(90, 120, 200);
            dgvPojTuszki.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(55, 57, 70);
            dgvPojTuszki.RowTemplate.Height = 22; // Zmniejszono wysokość wiersza
            dgvPojTuszki.AllowUserToResizeRows = false;
            panelPoj.Controls.Add(lblPoj);
            panelPoj.Controls.Add(dgvPojTuszki);
            panelPoj.Resize += (s, e) => { dgvPojTuszki.Width = panelPoj.Width - 8; dgvPojTuszki.Height = panelPoj.Height - 26; };
            bottom.Controls.Add(panelPoj, 0, 1);

            main.Controls.Add(bottom, 0, 0);

            // Kolumna 2: Zamówienia (wcześniej pozycje)
            dgvZamowienia = CreateGrid(true);
            dgvZamowienia.SelectionChanged += async (s, e) => await LoadPozycjeForSelectedAsync();
            dgvZamowienia.AutoGenerateColumns = false;
            dgvZamowienia.AllowUserToResizeRows = false; // Blokada zmiany wysokości wierszy
            dgvZamowienia.RowTemplate.Height = 28; // Ustawienie wysokości wierszy

            // Kolumna z ikoną notatki - USUNIĘTA
            /*
            var colNotatka = new DataGridViewImageColumn
            {
                Name = "IkonaNotatki",
                HeaderText = "",
                Width = 30,
                ReadOnly = true,
                Image = CreateNotebookIcon(),
                ImageLayout = DataGridViewImageCellLayout.Zoom
            };
            dgvZamowienia.Columns.Add(colNotatka);
            */

            dgvZamowienia.Columns.Add(new DataGridViewTextBoxColumn { Name = "Klient", DataPropertyName = "Klient", HeaderText = "Nazwa Klienta", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 35 });
            dgvZamowienia.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalIlosc", DataPropertyName = "TotalIlosc", HeaderText = "Ilość (kg)", DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" }, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvZamowienia.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", DataPropertyName = "Handlowiec", HeaderText = "Handlowiec", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });
            dgvZamowienia.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "Status", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvZamowienia.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataUtworzenia", DataPropertyName = "DataUtworzenia", HeaderText = "Data utworzenia", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });

            // Obsługa wyświetlania ikony notatki
            dgvZamowienia.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == dgvZamowienia.Columns["Klient"].Index && e.RowIndex >= 0) // Kolumna Klient
                {
                    var row = dgvZamowienia.Rows[e.RowIndex];
                    if (row.DataBoundItem is ZamowienieInfo info && info.MaNotatke)
                    {
                        e.Value = "📝 " + e.Value;
                    }
                }
            };

            main.Controls.Add(dgvZamowienia, 1, 0);

            // Kolumna 3: Pozycje i notatki - zmieniony układ
            var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Color.FromArgb(38, 40, 50), Padding = new Padding(4) };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); // Pozycje - zwiększone
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Etykieta notatek zamówienia
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20)); // Notatki zamówienia - zmniejszone
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Etykieta notatek transportu
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20)); // Notatki transportu - zmniejszone
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0)); // Szczegóły transportu - ukryty panel

            dgvPozycje = CreateGrid(false);
            dgvPozycje.AllowUserToResizeRows = false; // Blokada zmiany wysokości wierszy
            dgvPozycje.RowTemplate.Height = 30; // Zwiększona wysokość wierszy
            rightPanel.Controls.Add(dgvPozycje, 0, 0);

            var lblUwagi = new Label
            {
                Text = "Notatka zamówienia",
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), // Zmniejszono czcionkę
                Dock = DockStyle.Top,
                Padding = new Padding(4),
                BackColor = Color.FromArgb(50, 52, 64),
                ForeColor = Color.Yellow,
                AutoSize = true // Dodano AutoSize
            };
            txtUwagi = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10f), // Zmniejszono czcionkę
                BackColor = Color.FromArgb(52, 54, 66),
                ForeColor = Color.White,
                Height = 50 // Zmniejszona wysokość
            };

            var lblNotatkiTransportu = new Label
            {
                Text = "Notatki transportu",
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), // Zmniejszono czcionkę
                Dock = DockStyle.Top,
                Padding = new Padding(4),
                BackColor = Color.FromArgb(50, 52, 64),
                ForeColor = Color.Orange,
                AutoSize = true // Dodano AutoSize
            };
            txtNotatkiTransportu = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10f), // Zmniejszono czcionkę
                BackColor = Color.FromArgb(52, 54, 66),
                ForeColor = Color.White,
                Height = 50 // Zmniejszona wysokość
            };

            // Nowy panel dla szczegółów transportu
            panelSzczegolyTransportu = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 47, 58),
                BorderStyle = BorderStyle.FixedSingle
            };
            var lblSzczegolyTransportu = new Label
            {
                Text = "Szczegóły transportu",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 25,
                ForeColor = Color.LightBlue,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelSzczegolyTransportu.Controls.Add(lblSzczegolyTransportu);

            rightPanel.Controls.Add(lblUwagi, 0, 1);
            rightPanel.Controls.Add(txtUwagi, 0, 2);
            rightPanel.Controls.Add(lblNotatkiTransportu, 0, 3);
            rightPanel.Controls.Add(txtNotatkiTransportu, 0, 4);
            // rightPanel.Controls.Add(panelSzczegolyTransportu, 0, 5); // Ukryto panel
            main.Controls.Add(rightPanel, 2, 0);
        }

        // Metoda do tworzenia ikony notatnika
        private Image CreateNotebookIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                // Rysowanie prostej ikony notatnika
                using (var brush = new SolidBrush(Color.Gold))
                using (var pen = new Pen(Color.DarkGoldenrod, 1))
                {
                    g.FillRectangle(brush, 2, 2, 12, 12);
                    g.DrawRectangle(pen, 2, 2, 12, 12);
                    g.DrawLine(pen, 4, 5, 12, 5);
                    g.DrawLine(pen, 4, 7, 12, 7);
                    g.DrawLine(pen, 4, 9, 12, 9);
                    g.DrawLine(pen, 4, 11, 12, 11);
                }
            }
            return bitmap;
        }

        private Button MakeTopButton(string text, int left, EventHandler onClick, Color baseColor)
        {
            var b = new Button
            {
                Text = text,
                Left = left,
                Top = 30,
                Width = 100, // Zwiększona szerokość z 90 na 100
                Height = 70, // Zwiększona wysokość z 65 na 70
                BackColor = baseColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(baseColor, 0.3f);
            b.Click += onClick;
            b.MouseEnter += (s, e) => {
                b.BackColor = ControlPaint.Light(baseColor, .3f);
                b.FlatAppearance.BorderColor = Color.FromArgb(150, 150, 150);
            };
            b.MouseLeave += (s, e) => {
                b.BackColor = baseColor;
                b.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            };
            // Zaokrąglone rogi
            b.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, b.Width, b.Height);
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                int radius = 12; // Zwiększone zaokrąglenie
                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                path.CloseAllFigures();
                b.Region = new Region(path);
            };
            return b;
        }
        private DataGridView CreateGrid(bool readOnly)
        {
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = readOnly, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(45, 47, 58), BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false };
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(70, 72, 88); g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 12f, FontStyle.Bold); g.DefaultCellStyle.BackColor = Color.FromArgb(55, 57, 70); g.DefaultCellStyle.ForeColor = Color.White; g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 110, 190); g.DefaultCellStyle.SelectionForeColor = Color.White; g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(50, 52, 63); g.RowTemplate.Height = 40; return g;
        }
        #endregion

        #region Reload + Data
        private async Task ReloadAllAsync()
        {
            lblData.Text = _selectedDate.ToString("yyyy-MM-dd ddd");
            lblUser.Text = $"Użytkownik: {UserID}";
            await PopulateProductFilterAsync();
            await LoadOrdersAsync();
            await LoadIn0ESummaryAsync();
            await LoadPojTuszkiAsync();
            await LoadPozycjeForSelectedAsync();
        }

        private string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "(Brak)";
            var parts = s.Trim().Replace('\u00A0', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "(Brak)" : string.Join(' ', parts);
        }

        private async Task PopulateProductFilterAsync()
        {
            int? prev = _filteredProductId; var ids = new HashSet<int>();
            using (var cn = new SqlConnection(_connLibra))
            { await cn.OpenAsync(); var cmd = new SqlCommand(@"SELECT DISTINCT zmt.KodTowaru FROM dbo.ZamowieniaMieso z JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id=zmt.ZamowienieId WHERE z.DataZamowienia=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')", cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0)); }
            using (var cn = new SqlConnection(_connHandel))
            { await cn.OpenAsync(); var cmd = new SqlCommand(@"SELECT DISTINCT MZ.idtw FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG ON MZ.super=MG.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D", cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0)); }
            _produktLookup.Clear();
            if (ids.Count > 0)
            {
                var list = ids.ToList(); const int batch = 400;
                for (int i = 0; i < list.Count; i += batch)
                {
                    using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var slice = list.Skip(i).Take(batch).ToList(); var cmd = cn.CreateCommand(); var paramNames = new List<string>(); for (int k = 0; k < slice.Count; k++) { var pn = "@p" + k; cmd.Parameters.AddWithValue(pn, slice[k]); paramNames.Add(pn); }
                    // Filtr katalog=67095
                    cmd.CommandText = $"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(",", paramNames)}) AND katalog=67095"; using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) { int id = rd.GetInt32(0); string kod = rd.IsDBNull(1) ? string.Empty : rd.GetString(1); _produktLookup[id] = kod; }
                }
            }
            var items = new List<ComboItem> { new ComboItem(0, "— Wszystkie —") }; items.AddRange(_produktLookup.OrderBy(k => k.Value).Select(k => new ComboItem(k.Key, k.Value))); cbFiltrProdukt.DataSource = items; if (prev.HasValue && items.Any(i => i.Value == prev.Value)) cbFiltrProdukt.SelectedItem = items.First(i => i.Value == prev.Value); else cbFiltrProdukt.SelectedIndex = 0;
        }

        private async Task LoadOrdersAsync()
        {
            _zamowienia.Clear();
            var orderListForGrid = new List<ZamowienieInfo>();
            var klientIdsWithOrder = new HashSet<int>();

            // 1. Load actual orders from LibraNet
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = @"
    SELECT 
        z.Id,
        z.KlientId, 
        ISNULL(z.Uwagi,'') AS Uwagi, 
        ISNULL(z.Status,'Nowe') AS Status,
        (SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id " + (_filteredProductId.HasValue ? " AND t.KodTowaru=@P" : "") + @") AS TotalIlosc,
        z.DataUtworzenia
    FROM dbo.ZamowieniaMieso z 
    WHERE z.DataZamowienia=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')";

                if (_filteredProductId.HasValue)
                {
                    sql += " AND EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id AND t.KodTowaru=@P)";
                }
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                if (_filteredProductId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);
                }
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var uwagi = rd.GetString(2);
                    var info = new ZamowienieInfo
                    {
                        Id = rd.GetInt32(0),
                        KlientId = rd.GetInt32(1),
                        Uwagi = uwagi,
                        Status = rd.GetString(3),
                        TotalIlosc = rd.IsDBNull(4) ? 0 : rd.GetDecimal(4),
                        IsShipmentOnly = false,
                        DataUtworzenia = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                        MaNotatke = !string.IsNullOrWhiteSpace(uwagi) // Sprawdzenie czy ma notatkę
                    };
                    _zamowienia[info.Id] = info;
                    orderListForGrid.Add(info);
                    klientIdsWithOrder.Add(info.KlientId);
                }
            }

            // 2. Load shipments from Handel and add those without an order
            var shipments = new List<(int KlientId, decimal Qty)>();
            using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                string sql = @"SELECT MG.khid, SUM(ABS(MZ.ilosc)) FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG ON MZ.super=MG.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D AND MG.khid IS NOT NULL";
                if (_filteredProductId.HasValue) sql += " AND MZ.idtw=@P";
                sql += " GROUP BY MG.khid";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    shipments.Add((rd.GetInt32(0), rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1))));
                }
            }

            var shipmentOnlyClientIds = shipments.Where(s => !klientIdsWithOrder.Contains(s.KlientId)).Select(s => s.KlientId).Distinct().ToList();
            if (shipmentOnlyClientIds.Count > 0)
            {
                var contractors = await LoadContractorsAsync(shipmentOnlyClientIds);
                foreach (var s in shipments.Where(s => shipmentOnlyClientIds.Contains(s.KlientId)))
                {
                    contractors.TryGetValue(s.KlientId, out var cinfo);
                    var info = new ZamowienieInfo
                    {
                        Id = -s.KlientId, // Negative ID to signify it's not a real order
                        KlientId = s.KlientId,
                        Klient = Normalize(cinfo?.Shortcut ?? $"KH {s.KlientId}"),
                        Handlowiec = Normalize(cinfo?.Handlowiec),
                        Status = "Wydanie Symfonia", // Zmieniono z "(Wydanie)" na "Wydanie Symfonia"
                        TotalIlosc = s.Qty,
                        IsShipmentOnly = true,
                        DataUtworzenia = null,
                        MaNotatke = false
                    };
                    orderListForGrid.Add(info);
                }
            }

            // Populate contractor names for real orders
            var orderClientIds = orderListForGrid.Where(o => !o.IsShipmentOnly).Select(o => o.KlientId).Distinct().ToList();
            if (orderClientIds.Count > 0)
            {
                var contractors = await LoadContractorsAsync(orderClientIds);
                foreach (var orderInfo in orderListForGrid.Where(o => !o.IsShipmentOnly))
                {
                    if (contractors.TryGetValue(orderInfo.KlientId, out var cinfo))
                    {
                        orderInfo.Klient = Normalize(cinfo.Shortcut);
                        orderInfo.Handlowiec = Normalize(cinfo.Handlowiec);
                    }
                    else
                    {
                        orderInfo.Klient = $"KH {orderInfo.KlientId}";
                        orderInfo.Handlowiec = "(Brak)";
                    }
                }
            }

            // Sortowanie po statusie: Nowe > Zrealizowane > Wydanie Symfonia > Anulowane, potem po Handlowiec, Klient
            int StatusOrder(string status) => status switch
            {
                "Nowe" => 0,
                "Zrealizowane" => 1,
                "Wydanie Symfonia" => 2,
                "Anulowane" => 3,
                _ => 4
            };

            dgvZamowienia.DataSource = null;
            dgvZamowienia.DataSource = orderListForGrid
                .OrderBy(o => StatusOrder(o.Status))
                .ThenBy(o => o.Handlowiec)
                .ThenBy(o => o.Klient)
                .ToList();

            foreach (DataGridViewRow row in dgvZamowienia.Rows)
            {
                if (row.DataBoundItem is ZamowienieInfo info)
                {
                    if (info.IsShipmentOnly)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(80, 58, 32);
                        row.DefaultCellStyle.ForeColor = Color.Gold;
                    }
                    else if (info.Status == "Zrealizowane")
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(32, 80, 44);
                        row.DefaultCellStyle.ForeColor = Color.LightGreen;
                    }
                    else
                    {
                        // Explicitly set ForeColor for standard rows
                        row.DefaultCellStyle.ForeColor = Color.White;
                    }
                }
            }

            UpdateStatsLabel();
        }

        private async Task LoadPozycjeForSelectedAsync()
        {
            if (dgvZamowienia.CurrentRow?.DataBoundItem is not ZamowienieInfo info)
            {
                dgvPozycje.DataSource = null;
                txtUwagi.Text = "";
                txtNotatkiTransportu.Text = "";
                return;
            }

            if (info.IsShipmentOnly)
            {
                await LoadShipmentOnlyAsync(info.KlientId);
                return;
            }

            txtUwagi.Text = info.Uwagi;
            txtNotatkiTransportu.Text = ""; // Tutaj można dodać logikę ładowania notatek transportu
            await EnsureNotesTableAsync();

            var orderPositions = new List<(int TowarId, decimal Ilosc, string Notatka)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = "SELECT zmt.KodTowaru,zmt.Ilosc, n.Notatka FROM dbo.ZamowieniaMiesoTowar zmt LEFT JOIN dbo.ZamowieniaMiesoTowarNotatki n ON n.ZamowienieId=zmt.ZamowienieId AND n.KodTowaru=zmt.KodTowaru WHERE zmt.ZamowienieId=@Id" + (_filteredProductId.HasValue ? " AND zmt.KodTowaru=@P" : "");
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", info.Id);
                if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    decimal il = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                    string note = rd.IsDBNull(2) ? string.Empty : rd.GetString(2);
                    orderPositions.Add((id, il, note));
                }
            }
            var shipments = await GetShipmentsForClientAsync(info.KlientId);
            if (_filteredProductId.HasValue) shipments = shipments.Where(k => k.Key == _filteredProductId.Value).ToDictionary(k => k.Key, v => v.Value);

            var ids = orderPositions.Select(p => p.TowarId).Union(shipments.Keys).Where(i => i > 0).Distinct().ToList();
            var towarMap = await LoadTowaryAsync(ids);

            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Zamówiono (kg)", typeof(decimal));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            dt.Columns.Add("Różnica (kg)", typeof(decimal));

            var mapOrd = orderPositions.ToDictionary(p => p.TowarId, p => (p.Ilosc, p.Notatka));

            foreach (var id in ids)
            {
                mapOrd.TryGetValue(id, out var ord);
                shipments.TryGetValue(id, out var wyd);
                string kod = towarMap.TryGetValue(id, out var t) ? t.Kod : $"ID:{id}";
                dt.Rows.Add(kod, ord.Ilosc, wyd, ord.Ilosc - wyd);
            }
            dgvPozycje.DataSource = dt;
            FormatOrderGrid();
        }

        private async Task LoadShipmentOnlyAsync(int klientId)
        {
            txtUwagi.Text = "(Wydanie bez zamówienia)";
            txtNotatkiTransportu.Text = "";
            var shipments = await GetShipmentsForClientAsync(klientId);
            if (_filteredProductId.HasValue) shipments = shipments.Where(k => k.Key == _filteredProductId.Value).ToDictionary(k => k.Key, v => v.Value);
            var ids = shipments.Keys.ToList();
            var towarMap = await LoadTowaryAsync(ids);
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            foreach (var kv in shipments)
            {
                string kod = towarMap.TryGetValue(kv.Key, out var t) ? t.Kod : $"ID:{kv.Key}";
                dt.Rows.Add(kod, kv.Value);
            }
            dgvPozycje.DataSource = dt;
            if (dgvPozycje.Columns["Wydano (kg)"] != null) dgvPozycje.Columns["Wydano (kg)"]!.DefaultCellStyle.Format = "N0";
        }

        private async Task<Dictionary<int, decimal>> GetShipmentsForClientAsync(int klientId)
        {
            var dict = new Dictionary<int, decimal>();
            using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
FROM HANDEL.HM.MZ MZ 
JOIN HANDEL.HM.MG ON MZ.super=MG.id 
JOIN HANDEL.HM.TW ON MZ.idtw=TW.id 
WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 
AND MG.data=@D AND MG.khid=@K AND TW.katalog=67095"
+ (_filteredProductId.HasValue ? " AND MZ.idtw=@P" : "") + " GROUP BY MZ.idtw";
            var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
            cmd.Parameters.AddWithValue("@K", klientId);
            if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int id = rd.GetInt32(0);
                decimal qty = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                dict[id] = qty;
            }
            return dict;
        }

        private void FormatOrderGrid()
        {
            foreach (var n in new[] { "Zamówiono (kg)", "Wydano (kg)", "Różnica (kg)" })
                if (dgvPozycje.Columns[n] != null) dgvPozycje.Columns[n]!.DefaultCellStyle.Format = "N0";
            foreach (DataGridViewColumn c in dgvPozycje.Columns) c.ReadOnly = true;
        }

        private TreeNode GetOrCreateHandlowiecNode(Dictionary<string, TreeNode> cache, string handlowiec)
        {
            handlowiec = Normalize(handlowiec);
            if (!cache.TryGetValue(handlowiec, out var node))
            {
                node = new TreeNode(handlowiec) { BackColor = Color.FromArgb(52, 54, 66), ForeColor = Color.White, NodeFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold) };
                cache[handlowiec] = node;
            }
            return node;
        }

        private TreeNode CreateOrderNode(ZamowienieInfo info)
        {
            var text = info.Status == "Zrealizowane" ? $"{info.Klient} ✓" : info.Klient;
            var node = new TreeNode(text) { NodeFont = new Font("Segoe UI", 14.5f, FontStyle.Regular) };
            if (info.Status == "Zrealizowane") { node.BackColor = Color.FromArgb(32, 80, 44); node.ForeColor = Color.LightGreen; }
            else { node.BackColor = Color.FromArgb(45, 47, 58); node.ForeColor = Color.White; }
            return node;
        }

        private async Task AddShipmentsWithoutOrdersAsync(Dictionary<string, TreeNode> handlowiecNodes, List<int> klientIdsZamowien, Dictionary<int, ContractorInfo> loaded)
        {
            // This method was populating the TreeView. Since we are not using it for shipments without orders in a grid,
            // I will leave it empty for now to avoid confusion. If you want to see "shipments without orders"
            // in the new grid, we would need to adjust the logic here.
            await Task.CompletedTask;
        }

        private async Task<Dictionary<int, ContractorInfo>> LoadContractorsAsync(List<int> ids)
        {
            var dict = new Dictionary<int, ContractorInfo>(); if (ids.Count == 0) return dict; const int batch = 400;
            for (int i = 0; i < ids.Count; i += batch)
            { var slice = ids.Skip(i).Take(batch).ToList(); using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd = cn.CreateCommand(); var paramNames = new List<string>(); for (int k = 0; k < slice.Count; k++) { string pn = "@p" + k; cmd.Parameters.AddWithValue(pn, slice[k]); paramNames.Add(pn); } cmd.CommandText = $"SELECT c.Id, ISNULL(c.Shortcut,'KH '+CAST(c.Id AS varchar(10))) Shortcut, ISNULL(w.CDim_Handlowiec_Val,'(Brak)') Handlowiec FROM SSCommon.STContractors c LEFT JOIN SSCommon.ContractorClassification w ON c.Id=w.ElementId WHERE c.Id IN ({string.Join(',', paramNames)})"; using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) { var ci = new ContractorInfo { Id = rd.GetInt32(0), Shortcut = rd.IsDBNull(1) ? string.Empty : rd.GetString(1).Trim(), Handlowiec = rd.IsDBNull(2) ? "(Brak)" : Normalize(rd.GetString(2)) }; dict[ci.Id] = ci; } }
            return dict;
        }

        private (int containers, decimal pallets) CalcContainersAndPallets(string productName, decimal kg)
        { if (kg <= 0) return (0, 0); bool offal = OffalKeywords.Any(k => productName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0); decimal size = offal ? 10m : 15m; int containers = (int)Math.Ceiling(kg / size); decimal pallets = containers / 36m; return (containers, pallets); }
        private async Task<Dictionary<int, TowarInfo>> LoadTowaryAsync(List<int> ids)
        { var dict = new Dictionary<int, TowarInfo>(); if (ids.Count == 0) return dict; const int batch = 400; for (int i = 0; i < ids.Count; i += batch) { var slice = ids.Skip(i).Take(batch).ToList(); using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd = cn.CreateCommand(); var paramNames = new List<string>(); for (int k = 0; k < slice.Count; k++) { string pn = "@t" + k; cmd.Parameters.AddWithValue(pn, slice[k]); paramNames.Add(pn); } cmd.CommandText = $"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(',', paramNames)})"; using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) { var ti = new TowarInfo { Id = rd.GetInt32(0), Kod = rd.IsDBNull(1) ? string.Empty : rd.GetString(1) }; dict[ti.Id] = ti; } } return dict; }

        private async Task LoadPojTuszkiAsync()
        {
            try
            {
                var pojData = new List<(int Poj, int Pal)>();
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    string sql = @"SELECT k.QntInCont, COUNT(DISTINCT k.GUID) Palety FROM dbo.In0E k WHERE k.ArticleID=40 AND k.QntInCont>0 AND k.CreateData=@D GROUP BY k.QntInCont ORDER BY k.QntInCont ASC";
                    var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        pojData.Add((rd.GetInt32(0), rd.GetInt32(1)));
                    }
                }

                var dt = new DataTable();
                dt.Columns.Add("Typ", typeof(string));
                dt.Columns.Add("Palety", typeof(int));
                dt.Columns.Add("Udział", typeof(string));

                decimal totalPalety = pojData.Sum(p => p.Pal);

                if (totalPalety > 0)
                {
                    foreach (var data in pojData)
                    {
                        decimal udzial = (data.Pal / totalPalety) * 100;
                        dt.Rows.Add($"Poj. {data.Poj}", data.Pal, $"{udzial:N1}%");
                    }
                }

                // Separator
                if (pojData.Any())
                {
                    dt.Rows.Add("", DBNull.Value, "");
                }

                var duzyIds = new HashSet<int> { 5, 6, 7, 8 };
                var malyIds = new HashSet<int> { 9, 10, 11 };

                int duzyKurczakPalety = pojData.Where(p => duzyIds.Contains(p.Poj)).Sum(p => p.Pal);
                int malyKurczakPalety = pojData.Where(p => malyIds.Contains(p.Poj)).Sum(p => p.Pal);
                string duzyUdzial = totalPalety > 0 ? $"{(duzyKurczakPalety / totalPalety) * 100:N1}%" : "0.0%";
                string malyUdzial = totalPalety > 0 ? $"{(malyKurczakPalety / totalPalety) * 100:N1}%" : "0.0%";


                dt.Rows.Add("Duży kurczak", duzyKurczakPalety, duzyUdzial);
                dt.Rows.Add("Mały kurczak", malyKurczakPalety, malyUdzial);
                dt.Rows.Add("SUMA", (int)totalPalety, "100%");

                dgvPojTuszki.DataSource = dt;

                // Styling
                dgvPojTuszki.Columns["Typ"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvPojTuszki.Columns["Typ"].FillWeight = 50;
                dgvPojTuszki.Columns["Palety"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvPojTuszki.Columns["Palety"].FillWeight = 25;
                dgvPojTuszki.Columns["Udział"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvPojTuszki.Columns["Udział"].FillWeight = 25;
                dgvPojTuszki.Columns["Palety"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvPojTuszki.Columns["Udział"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                foreach (DataGridViewRow row in dgvPojTuszki.Rows)
                {
                    if (row.Cells["Typ"].Value is string typ)
                    {
                        if (string.IsNullOrWhiteSpace(typ))
                        {
                            row.DefaultCellStyle.BackColor = Color.FromArgb(45, 47, 58);
                            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 47, 58);
                            row.Height = 10;
                        }
                        else if (typ == "Duży kurczak" || typ == "Mały kurczak")
                        {
                            row.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                            row.DefaultCellStyle.ForeColor = Color.LightCyan;
                        }
                        else if (typ == "SUMA")
                        {
                            row.DefaultCellStyle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
                            row.DefaultCellStyle.BackColor = Color.FromArgb(40, 42, 50);
                            row.DefaultCellStyle.ForeColor = Color.Khaki;
                        }
                    }
                }
            }
            catch
            {
                dgvPojTuszki.DataSource = null;
            }
        }

        private async Task LoadIn0ESummaryAsync()
        {
            var dt = new DataTable(); dt.Columns.Add("Towar", typeof(string)); dt.Columns.Add("Kg", typeof(decimal)); dt.Columns.Add("Poj", typeof(int)); dt.Columns.Add("Pal", typeof(decimal));
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd = new SqlCommand("SELECT ArticleName, CAST(Weight AS float) W FROM dbo.In0E WHERE Data=@D AND ISNULL(ArticleName,'')<>''", cn); cmd.Parameters.AddWithValue("@D", _selectedDate.ToString("yyyy-MM-dd")); var agg = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) { string name = rd.IsDBNull(0) ? "(Brak)" : rd.GetString(0); decimal w = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1)); if (!agg.ContainsKey(name)) agg[name] = 0; agg[name] += w; }
            foreach (var kv in agg.OrderByDescending(k => k.Value)) { var (c, p) = CalcContainersAndPallets(kv.Key, kv.Value); dt.Rows.Add(kv.Key, kv.Value, c, p); }

            dgvIn0ESumy.DataSource = dt;
            FormatIn0EGrid(dgvIn0ESumy);
            // lblIn0ERefresh.Text = "Ostatnie odświeżenie: " + DateTime.Now.ToString("HH:mm:ss"); // Usunięto
        }
        private void FormatIn0EGrid(DataGridView g) { if (g.Columns["Kg"] != null) g.Columns["Kg"]!.DefaultCellStyle.Format = "N0"; if (g.Columns["Pal"] != null) g.Columns["Pal"]!.DefaultCellStyle.Format = "N2"; }
        #endregion

        #region Actions / Notes
        private int? GetSelectedOrderId()
        {
            if (dgvZamowienia.CurrentRow?.DataBoundItem is ZamowienieInfo info && !info.IsShipmentOnly)
            {
                return info.Id;
            }
            return null;
        }

        private async Task MarkOrderRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (MessageBox.Show("Oznaczyć zamówienie jako zrealizowane?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Zrealizowane' WHERE Id=@I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            await cmd.ExecuteNonQueryAsync();
            await ReloadAllAsync();
        }
        private async Task UndoRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (MessageBox.Show("Cofnąć realizację?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Nowe' WHERE Id=@I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            await cmd.ExecuteNonQueryAsync();
            await ReloadAllAsync();
        }
        private async Task SaveItemNotesAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (dgvPozycje.DataSource is not DataTable dt) return;

            await EnsureNotesTableAsync();
            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            foreach (DataRow r in dt.Rows)
            {
                if (!dt.Columns.Contains("Notatka")) break;
                string prod = r["Produkt"]?.ToString() ?? "";
                string not = r["Notatka"]?.ToString() ?? "";
                int tid = await ResolveTowarIdByKodAsync(prod);
                if (tid <= 0) continue;
                var cmd = new SqlCommand(@"MERGE dbo.ZamowieniaMiesoTowarNotatki AS T USING (SELECT @Z AS ZamowienieId,@T AS KodTowaru) S ON T.ZamowienieId=S.ZamowienieId AND T.KodTowaru=S.KodTowaru WHEN MATCHED THEN UPDATE SET Notatka=@N WHEN NOT MATCHED THEN INSERT (ZamowienieId,KodTowaru,Notatka) VALUES (@Z,@T,@N);", cn);
                cmd.Parameters.AddWithValue("@Z", orderId.Value);
                cmd.Parameters.AddWithValue("@T", tid);
                cmd.Parameters.AddWithValue("@N", (object)not ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            MessageBox.Show("Zapisano notatki.");
        }
        private async Task<int> ResolveTowarIdByKodAsync(string kod) { if (string.IsNullOrWhiteSpace(kod)) return 0; if (kod.StartsWith("ID:") && int.TryParse(kod[3..], out int id)) return id; try { using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd = new SqlCommand("SELECT TOP 1 ID FROM HM.TW WHERE kod=@K", cn); cmd.Parameters.AddWithValue("@K", kod); var o = await cmd.ExecuteScalarAsync(); return o == null || o is DBNull ? 0 : Convert.ToInt32(o); } catch { return 0; } }
        private async Task EnsureNotesTableAsync() { if (_notesTableEnsured) return; try { using var cn = new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoTowarNotatki' AND type='U') BEGIN CREATE TABLE dbo.ZamowieniaMiesoTowarNotatki( ZamowienieId INT NOT NULL, KodTowaru INT NOT NULL, Notatka NVARCHAR(4000) NULL, CONSTRAINT PK_ZamTowNot PRIMARY KEY (ZamowienieId,KodTowaru)); END", cn); await cmd.ExecuteNonQueryAsync(); } catch { } _notesTableEnsured = true; }
        private void UpdateStatsLabel() { int total = _zamowienia.Count; if (total == 0) { lblStats.Text = "Brak zamówień"; return; } int realized = _zamowienia.Values.Count(z => string.Equals(z.Status, "Zrealizowane", StringComparison.OrdinalIgnoreCase)); lblStats.Text = $"Zrealizowane: {realized}/{total} ({100.0 * realized / total:N1}%)"; }
        private void TryOpenShipmentDetails()
        {
            if (dgvZamowienia.CurrentRow?.DataBoundItem is ZamowienieInfo info && info.IsShipmentOnly)
            {
                var f = new ShipmentDetailsForm(_connHandel, info.KlientId, _selectedDate) { StartPosition = FormStartPosition.CenterParent };
                f.Show(this);
            }
        }
        private void OpenLiveWindow() { var f = new LivePrzychodyForm(_connLibra, _selectedDate, s => true, CalcContainersAndPallets) { StartPosition = FormStartPosition.CenterParent }; f.Show(this); }
        #endregion

        #region Timer
        private void StartAutoRefresh() { refreshTimer = new Timer { Interval = 60000 }; refreshTimer.Tick += async (s, e) => await ReloadAllAsync(); refreshTimer.Start(); }

        private void InitializeComponent()
        {

        }

        protected override void OnFormClosed(FormClosedEventArgs e) { base.OnFormClosed(e); refreshTimer?.Stop(); refreshTimer?.Dispose(); }
        #endregion

        private sealed record ComboItem(int Value, string Text) { public override string ToString() => Text; }
    }

    internal class LivePrzychodyForm : Form
    {
        private readonly string _connLibra; private readonly DateTime _date; private readonly Func<string, bool> _isContainer; private readonly Func<string, decimal, (int, decimal)> _calcPack; private DataGridView _grid; private Timer _timer;
        public LivePrzychodyForm(string connLibra, DateTime date, Func<string, bool> isContainer, Func<string, decimal, (int, decimal)> calcPack) { _connLibra = connLibra; _date = date; _isContainer = isContainer; _calcPack = calcPack; Text = "Live Przychody (In0E)"; Width = 760; Height = 620; BackColor = Color.FromArgb(26, 28, 36); ForeColor = Color.White; Font = new Font("Segoe UI", 10f); BuildUi(); Shown += async (s, e) => await RefreshDataAsync(); }
        private void BuildUi() { _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(40, 42, 54), BorderStyle = BorderStyle.None, RowHeadersVisible = false }; Controls.Add(_grid); _timer = new Timer { Interval = 20000 }; _timer.Tick += async (s, e) => await RefreshDataAsync(); _timer.Start(); }
        private async Task RefreshDataAsync() { var dt = new DataTable(); dt.Columns.Add("Towar", typeof(string)); dt.Columns.Add("Kg", typeof(decimal)); dt.Columns.Add("Pojemniki", typeof(int)); try { using var cn = new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd = new SqlCommand(@"SELECT ArticleName, CAST(Weight AS float) AS W FROM dbo.In0E WHERE Data=@D AND ISNULL(ArticleName,'')<>'' ORDER BY CASE WHEN COL_LENGTH('dbo.In0E','CreatedAt') IS NOT NULL THEN CreatedAt END DESC, CASE WHEN COL_LENGTH('dbo.In0E','Id') IS NOT NULL THEN Id END DESC", cn); cmd.Parameters.AddWithValue("@D", _date.ToString("yyyy-MM-dd")); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) { string name = rd.IsDBNull(0) ? "(Brak)" : rd.GetString(0); decimal w = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1)); var (c, _) = _calcPack(name, w); dt.Rows.Add(name, w, c); } } catch (Exception ex) { dt.Rows.Add($"Błąd: {ex.Message}", 0m, 0); } _grid.DataSource = dt; if (_grid.Columns["Kg"] != null) _grid.Columns["Kg"]!.DefaultCellStyle.Format = "N0"; }
        protected override void OnFormClosed(FormClosedEventArgs e) { base.OnFormClosed(e); _timer?.Stop(); _timer?.Dispose(); }
    }

    internal class ShipmentDetailsForm : Form
    {
        private readonly string _connHandel; private readonly int _clientId; private readonly DateTime _date; private DataGridView _grid;
        public ShipmentDetailsForm(string connHandel, int clientId, DateTime date) { _connHandel = connHandel; _clientId = clientId; _date = date.Date; Text = $"Wydania bez zamówienia – KH {_clientId}"; Width = 900; Height = 600; BackColor = Color.FromArgb(30, 30, 36); ForeColor = Color.White; Font = new Font("Segoe UI", 10f); BuildUi(); Shown += async (s, e) => await LoadAsync(); }
        private void BuildUi() { _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(42, 44, 52), BorderStyle = BorderStyle.None, RowHeadersVisible = false }; Controls.Add(_grid); }
        private async Task LoadAsync() { var dt = new DataTable(); dt.Columns.Add("Dokument", typeof(string)); dt.Columns.Add("TowarID", typeof(int)); dt.Columns.Add("Kod", typeof(string)); dt.Columns.Add("Ilość (kg)", typeof(decimal)); try { using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd = new SqlCommand(@"SELECT MG.id AS DocId, MZ.idtw, TW.kod, SUM(ABS(MZ.ilosc)) AS Qty FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG ON MZ.super=MG.id JOIN HANDEL.HM.TW ON MZ.idtw=TW.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D AND MG.khid=@Kh GROUP BY MG.id, MZ.idtw, TW.kod ORDER BY MG.id DESC", cn); cmd.Parameters.AddWithValue("@D", _date); cmd.Parameters.AddWithValue("@Kh", _clientId); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) dt.Rows.Add(rd.GetInt32(0).ToString(), rd.GetInt32(1), rd.IsDBNull(2) ? string.Empty : rd.GetString(2), rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3))); } catch (Exception ex) { dt.Rows.Add($"Błąd: {ex.Message}", 0, string.Empty, 0m); } _grid.DataSource = dt; if (_grid.Columns["Ilość (kg)"] != null) _grid.Columns["Ilość (kg)"]!.DefaultCellStyle.Format = "N0"; }
    }
}