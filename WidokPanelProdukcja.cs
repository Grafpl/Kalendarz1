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
        private TreeView treeKlienci;
        private DataGridView dgvPozycje;
        private TextBox txtUwagi, txtUwagiTransport;
        private DataGridView dgvPojTuszki;
        private DataGridView dgvIn0ESumy, dgvIn0ESumy2; // dwa widoki przychodów
        private ComboBox cbFiltrProdukt;
        private Timer refreshTimer;

        // Cache / state
        private readonly Dictionary<int, ZamowienieInfo> _zamowienia = new();
        private bool _notesTableEnsured = false;
        private int? _filteredProductId = null;
        private Dictionary<int, string> _produktLookup = new();
        private readonly HashSet<string> _nodeKeys = new(StringComparer.OrdinalIgnoreCase); // handlowiec|klientId|typ(O/S)

        private static readonly string[] OffalKeywords = { "wątroba", "watrob", "serce", "serca", "żołąd", "zolad", "żołądki", "zoladki" };

        private sealed class ZamowienieInfo { public int Id; public int KlientId; public string Klient=""; public string Handlowiec=""; public string Uwagi=""; public string Status=""; }
        private sealed class ContractorInfo { public int Id; public string Shortcut=""; public string Handlowiec="(Brak)"; }
        private sealed class TowarInfo { public int Id; public string Kod=""; }
        private sealed class NodeContext { public int? OrderId; public int ClientId; public bool IsShipmentOnly; }

        public WidokPanelProdukcja()
        {
            BuildUi();
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); if (e.KeyCode == Keys.Enter) TryOpenShipmentDetails(); };
            treeKlienci.NodeMouseDoubleClick += (s, e) => TryOpenShipmentDetails();
            Shown += async (s, e) => await ReloadAllAsync();
            StartAutoRefresh();
        }

        #region UI
        private void BuildUi()
        {
            BackColor = Color.FromArgb(22, 24, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 11f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); // mniejsze centrum
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); // większe przychody
            Controls.Add(root);

            // TOP BAR (jedna poprawna instancja)
            var top = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Color.FromArgb(34, 36, 46) };
            lblData = new Label { AutoSize = true, Left = 16, Top = 10, Font = new Font("Segoe UI Semibold", 30f, FontStyle.Bold) };
            lblUser = new Label { AutoSize = true, Left = 18, Top = 72, Font = new Font("Segoe UI", 11f, FontStyle.Italic), ForeColor = Color.LightGray };
            lblStats = new Label { AutoSize = true, Left = 320, Top = 76, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.Khaki };

            var lblFiltr = new Label { Text = "Towar:", Left = 320, Top = 18, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
            cbFiltrProdukt = new ComboBox { Left = 380, Top = 14, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11f) };
            cbFiltrProdukt.SelectedIndexChanged += async (s, e) => { if (cbFiltrProdukt.SelectedItem is ComboItem it) { _filteredProductId = it.Value == 0 ? null : it.Value; await LoadOrdersAsync(); await LoadPozycjeForSelectedAsync(); } };

            btnPrev = MakeTopButton("◀", 640, (s, e) => { _selectedDate = _selectedDate.AddDays(-1); _ = ReloadAllAsync(); }, Color.FromArgb(60,70,110));
            btnNext = MakeTopButton("▶", 730, (s, e) => { _selectedDate = _selectedDate.AddDays(1); _ = ReloadAllAsync(); }, Color.FromArgb(60,70,110));
            btnRefresh = MakeTopButton("Odśwież", 820, async (s, e) => await ReloadAllAsync(), Color.FromArgb(55,110,160));
            btnLive = MakeTopButton("Live", 915, (s, e) => OpenLiveWindow(), Color.FromArgb(85,90,140));
            btnUndo = MakeTopButton("Cofnij", 1000, async (s, e) => await UndoRealizedAsync(), Color.FromArgb(140,100,60));
            btnSaveNotes = MakeTopButton("Zapisz not.", 1085, async (s, e) => await SaveItemNotesAsync(), Color.FromArgb(95,95,95));
            btnZrealizowano = MakeTopButton("ZREAL.", 1170, async (s, e) => await MarkOrderRealizedAsync(), Color.FromArgb(25,135,75)); btnZrealizowano.Width = 120; btnZrealizowano.Height = 70; btnZrealizowano.Top = 28; btnZrealizowano.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            btnClose = MakeTopButton("X", 1295, (s, e) => Close(), Color.FromArgb(170,40,50)); btnClose.Width = 70; btnClose.Height = 70; btnClose.Top = 28;

            // Panel Poj tuszki
            var panelPoj = new Panel { Left = 16, Top = 110, Width = 300, Height = 36, BackColor = Color.FromArgb(45, 47, 58) };
            var lblPoj = new Label { Text = "Tuszek poj/palety", AutoSize = true, Left = 4, Top = -2, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.LightSkyBlue };
            dgvPojTuszki = new DataGridView { Left = 4, Top = 14, Width = 292, Height = 18, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ColumnHeadersVisible = false, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(45, 47, 58), BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9f), ScrollBars = ScrollBars.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            panelPoj.Controls.Add(lblPoj); panelPoj.Controls.Add(dgvPojTuszki);

            top.Controls.AddRange(new Control[] { lblData, lblUser, lblStats, lblFiltr, cbFiltrProdukt, btnPrev, btnNext, btnRefresh, btnLive, btnUndo, btnSaveNotes, btnZrealizowano, btnClose, panelPoj });
            root.Controls.Add(top, 0, 0);

            // MAIN – zamówienia + pozycje + notatki
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(4) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
            root.Controls.Add(main, 0, 1);

            treeKlienci = new TreeView { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 42, 54), ForeColor = Color.White, Font = new Font("Segoe UI", 14f), HideSelection = false, FullRowSelect = true };
            treeKlienci.AfterSelect += async (s, e) => await LoadPozycjeForSelectedAsync();
            main.Controls.Add(treeKlienci, 0, 0);

            dgvPozycje = CreateGrid(false);
            main.Controls.Add(dgvPozycje, 1, 0);

            var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.FromArgb(38, 40, 50), Padding = new Padding(4) };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            var lblUwagi = new Label { Text = "Notatka zamówienia / wydania", Font = new Font("Segoe UI Semibold", 14.5f, FontStyle.Bold), Dock = DockStyle.Top, Padding = new Padding(4) };
            txtUwagi = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 13f), BackColor = Color.FromArgb(52, 54, 66), ForeColor = Color.White };
            var lblTrans = new Label { Text = "Notatka transportu (planowane)", Font = new Font("Segoe UI Semibold", 14f), Dock = DockStyle.Top, Padding = new Padding(4) };
            txtUwagiTransport = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 12f), BackColor = Color.FromArgb(48, 50, 60), ForeColor = Color.Gainsboro, ReadOnly = true, Text = "(W kolejnej wersji)" };
            rightPanel.Controls.Add(lblUwagi, 0, 0);
            rightPanel.Controls.Add(txtUwagi, 0, 1);
            rightPanel.Controls.Add(lblTrans, 0, 2);
            rightPanel.Controls.Add(txtUwagiTransport, 0, 3);
            main.Controls.Add(rightPanel, 2, 0);

            // BOTTOM – przychody 2 kolumny
            var bottom = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 38) };
            var lblIn0E = new Label { Text = "Przychody (In0E) – Pojemniki / Palety", Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold), AutoSize = true, Left = 8, Top = 6 };
            lblIn0ERefresh = new Label { Text = "", AutoSize = true, Left = 8, Top = 38, Font = new Font("Segoe UI", 10f, FontStyle.Italic), ForeColor = Color.LightGray };
            var gridsLayout = new TableLayoutPanel { Left = 0, Top = 62, Width = bottom.Width - 8, Height = bottom.Height - 66, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, ColumnCount = 2, RowCount = 1, Padding = new Padding(6) };
            gridsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            gridsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            dgvIn0ESumy = CreateGrid(true);
            dgvIn0ESumy2 = CreateGrid(true);
            gridsLayout.Controls.Add(dgvIn0ESumy, 0, 0);
            gridsLayout.Controls.Add(dgvIn0ESumy2, 1, 0);
            bottom.Resize += (s, e) => gridsLayout.Height = bottom.Height - 66;
            bottom.Controls.Add(lblIn0E);
            bottom.Controls.Add(lblIn0ERefresh);
            bottom.Controls.Add(gridsLayout);
            root.Controls.Add(bottom, 0, 2);
        }

        private Button MakeTopButton(string text, int left, EventHandler onClick, Color baseColor)
        {
            var b = new Button { Text = text, Left = left, Top = 30, Width = 85, Height = 60, BackColor = baseColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 13f, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0; b.Cursor = Cursors.Hand; b.Click += onClick; b.MouseEnter += (s, e) => b.BackColor = ControlPaint.Light(baseColor, .25f); b.MouseLeave += (s, e) => b.BackColor = baseColor; return b;
        }
        private DataGridView CreateGrid(bool readOnly)
        {
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = readOnly, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(45, 47, 58), BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false };
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(70, 72, 88); g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 12f, FontStyle.Bold); g.DefaultCellStyle.BackColor = Color.FromArgb(55, 57, 70); g.DefaultCellStyle.ForeColor = Color.White; g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(90, 120, 200); g.DefaultCellStyle.SelectionForeColor = Color.White; g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(50, 52, 63); g.RowTemplate.Height = 40; return g;
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
            {   await cn.OpenAsync(); var cmd = new SqlCommand(@"SELECT DISTINCT zmt.KodTowaru FROM dbo.ZamowieniaMieso z JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id=zmt.ZamowienieId WHERE z.DataZamowienia=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')", cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0)); }
            using (var cn = new SqlConnection(_connHandel))
            {   await cn.OpenAsync(); var cmd = new SqlCommand(@"SELECT DISTINCT MZ.idtw FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG MG ON MZ.super=MG.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D", cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0)); }
            _produktLookup.Clear();
            if (ids.Count > 0)
            {
                var list = ids.ToList(); const int batch = 400;
                for (int i = 0; i < list.Count; i += batch)
                {
                    using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var slice = list.Skip(i).Take(batch).ToList(); var cmd = cn.CreateCommand(); var paramNames = new List<string>(); for (int k = 0; k < slice.Count; k++) { var pn = "@p" + k; cmd.Parameters.AddWithValue(pn, slice[k]); paramNames.Add(pn); } cmd.CommandText = $"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(",", paramNames)})"; using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) { int id = rd.GetInt32(0); string kod = rd.IsDBNull(1) ? string.Empty : rd.GetString(1); _produktLookup[id] = kod; } }
            }
            var items = new List<ComboItem> { new ComboItem(0, "— Wszystkie —") }; items.AddRange(_produktLookup.OrderBy(k => k.Value).Select(k => new ComboItem(k.Key, k.Value))); cbFiltrProdukt.DataSource = items; if (prev.HasValue && items.Any(i => i.Value == prev.Value)) cbFiltrProdukt.SelectedItem = items.First(i => i.Value == prev.Value); else cbFiltrProdukt.SelectedIndex = 0;
        }

        private async Task LoadOrdersAsync()
        {
            _zamowienia.Clear(); _nodeKeys.Clear();
            treeKlienci.BeginUpdate(); treeKlienci.Nodes.Clear(); var handlowiecNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);

            var orders = new List<(int Id, int Klient, string Uwagi, string Status)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = @"SELECT z.Id,z.KlientId, ISNULL(z.Uwagi,'') Uwagi, ISNULL(z.Status,'Nowe') Status FROM dbo.ZamowieniaMieso z WHERE z.DataZamowienia=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')";
                if (_filteredProductId.HasValue) sql += " AND EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id AND t.KodTowaru=@P)";
                var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date); if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) orders.Add((rd.GetInt32(0), rd.GetInt32(1), rd.GetString(2), rd.GetString(3)));
            }
            var klientIds = orders.Select(o => o.Klient).Distinct().ToList();
            var contractors = await LoadContractorsAsync(klientIds);

            foreach (var o in orders)
            {
                contractors.TryGetValue(o.Klient, out var cinfo);
                var info = new ZamowienieInfo { Id = o.Id, KlientId = o.Klient, Klient = Normalize(cinfo?.Shortcut ?? ($"KH {o.Klient}")), Handlowiec = Normalize(cinfo?.Handlowiec), Uwagi = o.Uwagi, Status = o.Status };
                _zamowienia[info.Id] = info;
                var parent = GetOrCreateHandlowiecNode(handlowiecNodes, info.Handlowiec);
                string key = $"{info.Handlowiec}|{info.KlientId}|O";
                if (_nodeKeys.Add(key))
                {
                    var child = CreateOrderNode(info); child.Tag = new NodeContext { OrderId = info.Id, ClientId = info.KlientId, IsShipmentOnly = false }; parent.Nodes.Add(child);
                }
            }

            await AddShipmentsWithoutOrdersAsync(handlowiecNodes, klientIds, contractors);

            foreach (TreeNode n in treeKlienci.Nodes) n.Expand();
            treeKlienci.EndUpdate();
            UpdateStatsLabel();
        }

        private TreeNode GetOrCreateHandlowiecNode(Dictionary<string, TreeNode> cache, string handlowiec)
        {
            handlowiec = Normalize(handlowiec);
            if (!cache.TryGetValue(handlowiec, out var node))
            {
                node = new TreeNode(handlowiec) { BackColor = Color.FromArgb(52, 54, 66), ForeColor = Color.White, NodeFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold) };
                cache[handlowiec] = node; treeKlienci.Nodes.Add(node);
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
            var shipments = new List<(int KlientId, decimal Qty)>();
            using (var cn = new SqlConnection(_connHandel))
            {   await cn.OpenAsync(); string sql = @"SELECT MG.khid, SUM(ABS(MZ.ilosc)) FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG MG ON MZ.super=MG.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D AND MG.khid IS NOT NULL"; if (_filteredProductId.HasValue) sql += " AND MZ.idtw=@P"; sql += " GROUP BY MG.khid"; var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date); if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value); using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) shipments.Add((rd.GetInt32(0), rd.IsDBNull(1)?0m:Convert.ToDecimal(rd.GetValue(1)))); }
            var extra = shipments.Where(s => !klientIdsZamowien.Contains(s.KlientId)).Select(s => s.KlientId).Distinct().ToList();
            if (extra.Count > 0) { var missing = await LoadContractorsAsync(extra); foreach (var kv in missing) loaded[kv.Key] = kv.Value; }
            foreach (var s in shipments.Where(s => !klientIdsZamowien.Contains(s.KlientId)))
            {
                if (!loaded.TryGetValue(s.KlientId, out var cinfo)) cinfo = new ContractorInfo { Id = s.KlientId, Shortcut = $"KH {s.KlientId}", Handlowiec = "(Brak)" };
                string hand = Normalize(cinfo.Handlowiec);
                string key = $"{hand}|{s.KlientId}|S";
                if (!_nodeKeys.Add(key)) continue; // już dodano
                var parent = GetOrCreateHandlowiecNode(handlowiecNodes, hand);
                var node = new TreeNode($"{cinfo.Shortcut} (bez zam. {s.Qty:N0} kg)") { BackColor = Color.FromArgb(80, 58, 32), ForeColor = Color.Gold, NodeFont = new Font("Segoe UI", 13.5f, FontStyle.Italic), Tag = new NodeContext { OrderId = null, ClientId = s.KlientId, IsShipmentOnly = true } };
                parent.Nodes.Add(node);
            }
        }

        private async Task<Dictionary<int, ContractorInfo>> LoadContractorsAsync(List<int> ids)
        {
            var dict = new Dictionary<int, ContractorInfo>(); if (ids.Count == 0) return dict; const int batch=400;
            for (int i=0;i<ids.Count;i+=batch)
            {   var slice = ids.Skip(i).Take(batch).ToList(); using var cn = new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd=cn.CreateCommand(); var paramNames=new List<string>(); for(int k=0;k<slice.Count;k++){ string pn="@p"+k; cmd.Parameters.AddWithValue(pn, slice[k]); paramNames.Add(pn);} cmd.CommandText=$"SELECT c.Id, ISNULL(c.Shortcut,'KH '+CAST(c.Id AS varchar(10))) Shortcut, ISNULL(w.CDim_Handlowiec_Val,'(Brak)') Handlowiec FROM SSCommon.STContractors c LEFT JOIN SSCommon.ContractorClassification w ON c.Id=w.ElementId WHERE c.Id IN ({string.Join(',',paramNames)})"; using var rd=await cmd.ExecuteReaderAsync(); while(await rd.ReadAsync()){ var ci=new ContractorInfo{ Id=rd.GetInt32(0), Shortcut=rd.IsDBNull(1)?string.Empty:rd.GetString(1).Trim(), Handlowiec=rd.IsDBNull(2)?"(Brak)":Normalize(rd.GetString(2))}; dict[ci.Id]=ci; }} return dict;
        }

        private async Task LoadPozycjeForSelectedAsync()
        {
            if (treeKlienci.SelectedNode?.Tag is not NodeContext ctx) { dgvPozycje.DataSource = null; txtUwagi.Text = ""; return; }
            if (ctx.IsShipmentOnly) { await LoadShipmentOnlyAsync(ctx.ClientId); return; }
            if (ctx.OrderId is not int orderId || !_zamowienia.TryGetValue(orderId, out var info)) { dgvPozycje.DataSource = null; txtUwagi.Text = ""; return; }
            txtUwagi.Text = info.Uwagi; await EnsureNotesTableAsync();
            var orderPositions = new List<(int TowarId, decimal Ilosc, string Notatka)>();
            using (var cn = new SqlConnection(_connLibra)) { await cn.OpenAsync(); string sql = "SELECT zmt.KodTowaru,zmt.Ilosc, n.Notatka FROM dbo.ZamowieniaMiesoTowar zmt LEFT JOIN dbo.ZamowieniaMiesoTowarNotatki n ON n.ZamowienieId=zmt.ZamowienieId AND n.KodTowaru=zmt.KodTowaru WHERE zmt.ZamowienieId=@Id" + (_filteredProductId.HasValue?" AND zmt.KodTowaru=@P":""); var cmd=new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@Id", orderId); if(_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value); using var rd= await cmd.ExecuteReaderAsync(); while(await rd.ReadAsync()){ int id=rd.GetInt32(0); decimal il=rd.IsDBNull(1)?0m:Convert.ToDecimal(rd.GetValue(1)); string note=rd.IsDBNull(2)?string.Empty:rd.GetString(2); orderPositions.Add((id,il,note)); } }
            var shipments = await GetShipmentsForClientAsync(info.KlientId); if (_filteredProductId.HasValue) shipments = shipments.Where(k=>k.Key==_filteredProductId.Value).ToDictionary(k=>k.Key,v=>v.Value);
            var ids = orderPositions.Select(p=>p.TowarId).Union(shipments.Keys).Where(i=>i>0).Distinct().ToList(); var towarMap = await LoadTowaryAsync(ids);
            var dt = new DataTable(); dt.Columns.Add("Produkt", typeof(string)); dt.Columns.Add("Zamówiono (kg)", typeof(decimal)); dt.Columns.Add("Wydano (kg)", typeof(decimal)); dt.Columns.Add("Różnica (kg)", typeof(decimal)); dt.Columns.Add("Pojemniki", typeof(int)); dt.Columns.Add("Palety", typeof(decimal)); dt.Columns.Add("Notatka", typeof(string));
            var mapOrd = orderPositions.ToDictionary(p=>p.TowarId, p=>(p.Ilosc,p.Notatka));
            foreach(var id in ids){ mapOrd.TryGetValue(id, out var ord); shipments.TryGetValue(id, out var wyd); string kod = towarMap.TryGetValue(id,out var t)? t.Kod : $"ID:{id}"; var (c,p)=CalcContainersAndPallets(kod, ord.Ilosc); dt.Rows.Add(kod, ord.Ilosc, wyd, ord.Ilosc-wyd, c, p, ord.Notatka); }
            dgvPozycje.DataSource = dt; FormatOrderGrid();
        }

        private async Task LoadShipmentOnlyAsync(int klientId)
        {
            txtUwagi.Text = "(Wydanie bez zamówienia)"; var shipments = await GetShipmentsForClientAsync(klientId); if(_filteredProductId.HasValue) shipments = shipments.Where(k=>k.Key==_filteredProductId.Value).ToDictionary(k=>k.Key,v=>v.Value); var ids = shipments.Keys.ToList(); var towarMap = await LoadTowaryAsync(ids); var dt = new DataTable(); dt.Columns.Add("Produkt", typeof(string)); dt.Columns.Add("Wydano (kg)", typeof(decimal)); dt.Columns.Add("Pojemniki", typeof(int)); dt.Columns.Add("Palety", typeof(decimal)); foreach(var kv in shipments){ string kod = towarMap.TryGetValue(kv.Key,out var t)? t.Kod : $"ID:{kv.Key}"; var (c,p)=CalcContainersAndPallets(kod, kv.Value); dt.Rows.Add(kod, kv.Value, c, p);} dgvPozycje.DataSource = dt; if(dgvPozycje.Columns["Wydano (kg)"]!=null) dgvPozycje.Columns["Wydano (kg)"].DefaultCellStyle.Format="N0"; if(dgvPozycje.Columns["Palety"]!=null) dgvPozycje.Columns["Palety"].DefaultCellStyle.Format="N2";
        }

        private async Task<Dictionary<int, decimal>> GetShipmentsForClientAsync(int klientId)
        {
            var dict = new Dictionary<int, decimal>();
            using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
FROM HANDEL.HM.MZ MZ 
JOIN HANDEL.HM.MG ON MZ.super=MG.id 
JOIN HANDEL.HM.TW TW ON MZ.idtw=TW.id 
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
        { foreach(var n in new[]{"Zamówiono (kg)","Wydano (kg)","Różnica (kg)"}) if(dgvPozycje.Columns[n]!=null) dgvPozycje.Columns[n].DefaultCellStyle.Format="N0"; if(dgvPozycje.Columns["Palety"]!=null) dgvPozycje.Columns["Palety"].DefaultCellStyle.Format="N2"; if(dgvPozycje.Columns["Notatka"]!=null) dgvPozycje.Columns["Notatka"].ReadOnly=false; foreach(DataGridViewColumn c in dgvPozycje.Columns) if(c.Name!="Notatka") c.ReadOnly=true; }

        private (int containers, decimal pallets) CalcContainersAndPallets(string productName, decimal kg)
        { if (kg<=0) return (0,0); bool offal = OffalKeywords.Any(k=> productName.IndexOf(k, StringComparison.OrdinalIgnoreCase)>=0); decimal size = offal?10m:15m; int containers=(int)Math.Ceiling(kg/size); decimal pallets = containers/36m; return (containers,pallets); }
        private async Task<Dictionary<int, TowarInfo>> LoadTowaryAsync(List<int> ids)
        { var dict=new Dictionary<int,TowarInfo>(); if(ids.Count==0) return dict; const int batch=400; for(int i=0;i<ids.Count;i+=batch){ var slice=ids.Skip(i).Take(batch).ToList(); using var cn=new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd=cn.CreateCommand(); var paramNames=new List<string>(); for(int k=0;k<slice.Count;k++){ string pn="@t"+k; cmd.Parameters.AddWithValue(pn,slice[k]); paramNames.Add(pn);} cmd.CommandText=$"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(',',paramNames)})"; using var rd=await cmd.ExecuteReaderAsync(); while(await rd.ReadAsync()){ var ti=new TowarInfo{ Id=rd.GetInt32(0), Kod=rd.IsDBNull(1)?string.Empty:rd.GetString(1)}; dict[ti.Id]=ti; } } return dict; }

        private async Task LoadPojTuszkiAsync()
        {
            try
            {
                using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
                string sql = @"SELECT k.QntInCont, COUNT(DISTINCT k.GUID) Palety FROM dbo.In0E k WHERE k.ArticleID=40 AND k.QntInCont>0 AND k.CreateData=@D GROUP BY k.QntInCont ORDER BY k.QntInCont DESC";
                var cmd = new SqlCommand(sql, cn); cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                var dt = new DataTable(); dt.Columns.Add("Poj", typeof(int)); dt.Columns.Add("Pal", typeof(int));
                using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) dt.Rows.Add(rd.GetInt32(0), rd.GetInt32(1));
                dgvPojTuszki.DataSource = dt; foreach (DataGridViewColumn c in dgvPojTuszki.Columns) c.Width = 60; dgvPojTuszki.Height = 18 + dt.Rows.Count * 20;
            }
            catch { dgvPojTuszki.DataSource = null; }
        }

        private async Task LoadIn0ESummaryAsync()
        {
            var dt = new DataTable(); dt.Columns.Add("Towar", typeof(string)); dt.Columns.Add("Kg", typeof(decimal)); dt.Columns.Add("Poj", typeof(int)); dt.Columns.Add("Pal", typeof(decimal));
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd = new SqlCommand("SELECT ArticleName, CAST(Weight AS float) W FROM dbo.In0E WHERE Data=@D AND ISNULL(ArticleName,'')<>''", cn); cmd.Parameters.AddWithValue("@D", _selectedDate.ToString("yyyy-MM-dd")); var agg = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase); using var rd = await cmd.ExecuteReaderAsync(); while(await rd.ReadAsync()){ string name = rd.IsDBNull(0)?"(Brak)":rd.GetString(0); decimal w = rd.IsDBNull(1)?0m:Convert.ToDecimal(rd.GetValue(1)); if(!agg.ContainsKey(name)) agg[name]=0; agg[name]+=w; }
            foreach(var kv in agg.OrderByDescending(k=>k.Value)){ var (c,p)=CalcContainersAndPallets(kv.Key, kv.Value); dt.Rows.Add(kv.Key, kv.Value, c, p); }
            // split
            var rows = dt.Rows.Cast<DataRow>().ToList(); int half = (rows.Count+1)/2; var dtL = dt.Clone(); var dtR = dt.Clone(); for(int i=0;i<rows.Count;i++){ if(i<half) dtL.ImportRow(rows[i]); else dtR.ImportRow(rows[i]); }
            dgvIn0ESumy.DataSource = dtL; dgvIn0ESumy2.DataSource = dtR; FormatIn0EGrid(dgvIn0ESumy); FormatIn0EGrid(dgvIn0ESumy2); lblIn0ERefresh.Text = "Ostatnie odświeżenie: " + DateTime.Now.ToString("HH:mm:ss");
        }
        private void FormatIn0EGrid(DataGridView g){ if(g.Columns["Kg"]!=null) g.Columns["Kg"].DefaultCellStyle.Format="N0"; if(g.Columns["Pal"]!=null) g.Columns["Pal"].DefaultCellStyle.Format="N2"; }
        #endregion

        #region Actions / Notes
        private async Task MarkOrderRealizedAsync(){ if(treeKlienci.SelectedNode?.Tag is not NodeContext ctx || ctx.IsShipmentOnly || ctx.OrderId is not int id) return; if(MessageBox.Show("Oznaczyć zamówienie jako zrealizowane?","Potwierdzenie",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return; using var cn=new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd=new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Zrealizowane' WHERE Id=@I",cn); cmd.Parameters.AddWithValue("@I",id); await cmd.ExecuteNonQueryAsync(); await ReloadAllAsync(); }
        private async Task UndoRealizedAsync(){ if(treeKlienci.SelectedNode?.Tag is not NodeContext ctx || ctx.IsShipmentOnly || ctx.OrderId is not int id) return; if(MessageBox.Show("Cofnąć realizację?","Potwierdzenie",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return; using var cn=new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd=new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Nowe' WHERE Id=@I",cn); cmd.Parameters.AddWithValue("@I",id); await cmd.ExecuteNonQueryAsync(); await ReloadAllAsync(); }
        private async Task SaveItemNotesAsync(){ if(treeKlienci.SelectedNode?.Tag is not NodeContext ctx || ctx.IsShipmentOnly) return; if(dgvPozycje.DataSource is not DataTable dt) return; if(ctx.OrderId is not int orderId) return; await EnsureNotesTableAsync(); using var cn=new SqlConnection(_connLibra); await cn.OpenAsync(); foreach(DataRow r in dt.Rows){ if(!dt.Columns.Contains("Notatka")) break; string prod = r["Produkt"]?.ToString() ?? ""; string not = r["Notatka"]?.ToString() ?? ""; int tid = await ResolveTowarIdByKodAsync(prod); if(tid<=0) continue; var cmd=new SqlCommand(@"MERGE dbo.ZamowieniaMiesoTowarNotatki AS T USING (SELECT @Z AS ZamowienieId,@T AS KodTowaru) S ON T.ZamowienieId=S.ZamowienieId AND T.KodTowaru=S.KodTowaru WHEN MATCHED THEN UPDATE SET Notatka=@N WHEN NOT MATCHED THEN INSERT (ZamowienieId,KodTowaru,Notatka) VALUES (@Z,@T,@N);", cn); cmd.Parameters.AddWithValue("@Z",orderId); cmd.Parameters.AddWithValue("@T",tid); cmd.Parameters.AddWithValue("@N", (object)not??DBNull.Value); await cmd.ExecuteNonQueryAsync(); } MessageBox.Show("Zapisano notatki."); }
        private async Task<int> ResolveTowarIdByKodAsync(string kod){ if(string.IsNullOrWhiteSpace(kod)) return 0; if(kod.StartsWith("ID:") && int.TryParse(kod[3..], out int id)) return id; try{ using var cn=new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd=new SqlCommand("SELECT TOP 1 ID FROM HM.TW WHERE kod=@K",cn); cmd.Parameters.AddWithValue("@K",kod); var o=await cmd.ExecuteScalarAsync(); return o==null||o is DBNull?0:Convert.ToInt32(o);} catch { return 0;} }
        private async Task EnsureNotesTableAsync(){ if(_notesTableEnsured) return; try{ using var cn=new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd=new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoTowarNotatki' AND type='U') BEGIN CREATE TABLE dbo.ZamowieniaMiesoTowarNotatki( ZamowienieId INT NOT NULL, KodTowaru INT NOT NULL, Notatka NVARCHAR(4000) NULL, CONSTRAINT PK_ZamTowNot PRIMARY KEY (ZamowienieId,KodTowaru)); END",cn); await cmd.ExecuteNonQueryAsync(); } catch{} _notesTableEnsured=true; }
        private void UpdateStatsLabel(){ int total=_zamowienia.Count; if(total==0){ lblStats.Text="Brak zamówień"; return;} int realized=_zamowienia.Values.Count(z=> string.Equals(z.Status,"Zrealizowane",StringComparison.OrdinalIgnoreCase)); lblStats.Text=$"Zrealizowane: {realized}/{total} ({100.0*realized/total:N1}%)"; }
        private void TryOpenShipmentDetails(){ if(treeKlienci.SelectedNode?.Tag is not NodeContext ctx || !ctx.IsShipmentOnly) return; var f=new ShipmentDetailsForm(_connHandel, ctx.ClientId, _selectedDate){ StartPosition=FormStartPosition.CenterParent}; f.Show(this); }
        private void OpenLiveWindow(){ var f=new LivePrzychodyForm(_connLibra,_selectedDate, s=>true, CalcContainersAndPallets){ StartPosition=FormStartPosition.CenterParent}; f.Show(this); }
        #endregion

        #region Timer
        private void StartAutoRefresh(){ refreshTimer=new Timer{ Interval=60000 }; refreshTimer.Tick += async (s,e)=> await ReloadAllAsync(); refreshTimer.Start(); }
        protected override void OnFormClosed(FormClosedEventArgs e){ base.OnFormClosed(e); refreshTimer?.Stop(); refreshTimer?.Dispose(); }
        #endregion

        private sealed record ComboItem(int Value, string Text){ public override string ToString() => Text; }
    }

    internal class LivePrzychodyForm : Form
    {
        private readonly string _connLibra; private readonly DateTime _date; private readonly Func<string,bool> _isContainer; private readonly Func<string,decimal,(int,decimal)> _calcPack; private DataGridView _grid; private Timer _timer;
        public LivePrzychodyForm(string connLibra, DateTime date, Func<string,bool> isContainer, Func<string,decimal,(int,decimal)> calcPack){ _connLibra=connLibra; _date=date; _isContainer=isContainer; _calcPack=calcPack; Text="Live Przychody (In0E)"; Width=760; Height=620; BackColor=Color.FromArgb(26,28,36); ForeColor=Color.White; Font=new Font("Segoe UI",10f); BuildUi(); Shown+= async (s,e)=> await RefreshDataAsync(); }
        private void BuildUi(){ _grid=new DataGridView{ Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor=Color.FromArgb(40,42,54), BorderStyle=BorderStyle.None, RowHeadersVisible=false}; Controls.Add(_grid); _timer=new Timer{ Interval=20000 }; _timer.Tick += async (s,e)=> await RefreshDataAsync(); _timer.Start(); }
        private async Task RefreshDataAsync(){ var dt=new DataTable(); dt.Columns.Add("Towar",typeof(string)); dt.Columns.Add("Kg",typeof(decimal)); dt.Columns.Add("Pojemniki",typeof(int)); try{ using var cn=new SqlConnection(_connLibra); await cn.OpenAsync(); var cmd=new SqlCommand(@"SELECT ArticleName, CAST(Weight AS float) AS W FROM dbo.In0E WHERE Data=@D AND ISNULL(ArticleName,'')<>'' ORDER BY CASE WHEN COL_LENGTH('dbo.In0E','CreatedAt') IS NOT NULL THEN CreatedAt END DESC, CASE WHEN COL_LENGTH('dbo.In0E','Id') IS NOT NULL THEN Id END DESC",cn); cmd.Parameters.AddWithValue("@D", _date.ToString("yyyy-MM-dd")); using var rd=await cmd.ExecuteReaderAsync(); while(await rd.ReadAsync()){ string name=rd.IsDBNull(0)?"(Brak)":rd.GetString(0); decimal w=rd.IsDBNull(1)?0m:Convert.ToDecimal(rd.GetValue(1)); var (c,_) = _calcPack(name,w); dt.Rows.Add(name,w,c);} } catch(Exception ex){ dt.Rows.Add($"Błąd: {ex.Message}",0m,0);} _grid.DataSource=dt; if(_grid.Columns["Kg"]!=null) _grid.Columns["Kg"].DefaultCellStyle.Format="N0"; }
        protected override void OnFormClosed(FormClosedEventArgs e){ base.OnFormClosed(e); _timer?.Stop(); _timer?.Dispose(); }
    }

    internal class ShipmentDetailsForm : Form
    {
        private readonly string _connHandel; private readonly int _clientId; private readonly DateTime _date; private DataGridView _grid;
        public ShipmentDetailsForm(string connHandel,int clientId,DateTime date){ _connHandel=connHandel; _clientId=clientId; _date=date.Date; Text=$"Wydania bez zamówienia – KH {_clientId}"; Width=900; Height=600; BackColor=Color.FromArgb(30,30,36); ForeColor=Color.White; Font=new Font("Segoe UI",10f); BuildUi(); Shown+= async (s,e)=> await LoadAsync(); }
        private void BuildUi(){ _grid=new DataGridView{ Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor=Color.FromArgb(42,44,52), BorderStyle=BorderStyle.None, RowHeadersVisible=false}; Controls.Add(_grid);}        
        private async Task LoadAsync(){ var dt=new DataTable(); dt.Columns.Add("Dokument",typeof(string)); dt.Columns.Add("TowarID",typeof(int)); dt.Columns.Add("Kod",typeof(string)); dt.Columns.Add("Ilość (kg)",typeof(decimal)); try{ using var cn=new SqlConnection(_connHandel); await cn.OpenAsync(); var cmd=new SqlCommand(@"SELECT MG.id AS DocId, MZ.idtw, TW.kod, SUM(ABS(MZ.ilosc)) AS Qty FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG ON MZ.super=MG.id JOIN HANDEL.HM.TW ON MZ.idtw=TW.id WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D AND MG.khid=@Kh GROUP BY MG.id, MZ.idtw, TW.kod ORDER BY MG.id DESC",cn); cmd.Parameters.AddWithValue("@D", _date); cmd.Parameters.AddWithValue("@Kh", _clientId); using var rd=await cmd.ExecuteReaderAsync(); while(await rd.ReadAsync()) dt.Rows.Add(rd.GetInt32(0).ToString(), rd.GetInt32(1), rd.IsDBNull(2)?string.Empty:rd.GetString(2), rd.IsDBNull(3)?0m:Convert.ToDecimal(rd.GetValue(3))); } catch(Exception ex){ dt.Rows.Add($"Błąd: {ex.Message}",0,string.Empty,0m);} _grid.DataSource=dt; if(_grid.Columns["Ilość (kg)"]!=null) _grid.Columns["Ilość (kg)"].DefaultCellStyle.Format="N0"; }
    }
}
