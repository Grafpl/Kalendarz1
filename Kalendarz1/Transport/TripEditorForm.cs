using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Kalendarz1.Transport
{
    internal sealed class TripEditorForm : Form
    {
        private readonly TransportRepository _repo;
        private readonly DateTime _date;
        private readonly string _user;
        private readonly long? _tripId; // readonly – przy nowym kursie ustawiamy refleksj¹

        // Nag³ówek
        private ComboBox _cbDriver = new();
        private ComboBox _cbVehicle = new();
        private TextBox _txtRoute = new(); // TRASA: krótkie oznaczenie kierunku / pêtli (np. "Warszawa", "£ódŸ / Piotrków", "Export CZ"). Dla szybkiej identyfikacji i filtrów.
        private DateTimePicker _dtDeparture = new(); // DATA + GODZINA wyjazdu (wczeœniej by³o tylko Time)
        private ComboBox _cbStatus = new();
        private TextBox _txtNotes = new();
        private Label _lblFill = new();
        private Button _btnSave = new();

        // Grids
        private DataGridView _gridLoads = new();
        private BindingSource _bsLoads = new();
        private DataGridView _gridOrders = new();
        private BindingSource _bsOrders = new();

        // Inne
        private TabControl _tabs = new();
        private ToolStrip _tsLoads = new();
        private ToolStrip _tsOrders = new();
        private ProgressBar _pbMass = new();
        private ProgressBar _pbSpace = new();
        private ProgressBar _pbFinal = new();
        private StatusStrip _status = new();
        private ToolStripStatusLabel _statusInfo = new();
        private ToolTip _tt = new();

        // Mapowanie statusów
        private readonly Dictionary<string, string> _statusPlToEn = new(StringComparer.OrdinalIgnoreCase)
        {
            {"Zaplanowany","Planned"}, {"W trakcie","InProgress"}, {"Zakoñczony","Completed"}, {"Anulowany","Canceled"}
        };
        private readonly Dictionary<string, string> _statusEnToPl;

        private bool _ordersVisible = true;
        private Dictionary<int,string> _clientNamesCache = new(); // cache nazw klientów

        public TripEditorForm(TransportRepository repo, DateTime date, string user, long? tripId)
        {
            _repo = repo; _date = date; _user = user; _tripId = tripId;
            _statusEnToPl = _statusPlToEn.ToDictionary(k => k.Value, v => v.Key, StringComparer.OrdinalIgnoreCase);
            Text = tripId == null ? "Nowy kurs" : $"Kurs #{tripId}"; Width = 1220; Height = 780; MinimumSize = new Size(1000, 640); StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi; KeyPreview = true; ShowInTaskbar = false; Font = new Font("Segoe UI", 9.5f);
            BuildUi();
            TransportUi.ApplyTheme(this);
            Load += async (_, __) => await InitAsync();
            Resize += (_, __) => AdjustLayout();
            KeyDown += TripEditorForm_KeyDown;
        }

        private void TripEditorForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Insert) { e.Handled = true; _ = AddLoadAsync(); }
            else if (e.KeyCode == Keys.Delete && _gridLoads.Focused) { e.Handled = true; _ = DeleteLoadAsync(); }
            else if (e.KeyCode == Keys.F2) { e.Handled = true; _ = SaveHeaderAsync(); }
            else if (e.KeyCode == Keys.F9) { e.Handled = true; ToggleOrdersVisibility(); }
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // fill bars
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // notes
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // tabs
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // bottom
            Controls.Add(root);

            // Header
            var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 8, RowCount = 2, Padding = new Padding(12, 10, 12, 2), AutoSize = true };
            for (int i = 0; i < 8; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize)); header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Font lblFont = new("Segoe UI", 9.5f, FontStyle.Bold);

            _cbDriver.DropDownStyle = ComboBoxStyle.DropDownList; _cbDriver.Width = 160;
            _cbVehicle.DropDownStyle = ComboBoxStyle.DropDownList; _cbVehicle.Width = 130; _cbVehicle.SelectedIndexChanged += (_, __) => RecalcFill();
            _txtRoute.Width = 180; _txtRoute.Font = new Font("Segoe UI", 10f);
            _dtDeparture.Format = DateTimePickerFormat.Custom; _dtDeparture.CustomFormat = "dd.MM.yyyy HH:mm"; _dtDeparture.Width = 150; _dtDeparture.Font = new Font("Segoe UI", 10f);
            _cbStatus.DropDownStyle = ComboBoxStyle.DropDownList; _cbStatus.Items.AddRange(_statusPlToEn.Keys.ToArray()); _cbStatus.SelectedIndex = 0; _cbStatus.Width = 110;
            _lblFill.Text = "£adunek: 0 kg"; _lblFill.AutoSize = true; _lblFill.Font = new Font("Segoe UI Semibold", 10f);

            header.Controls.Add(new Label { Text = "Kierowca", Font = lblFont, AutoSize = true }, 0, 0); header.Controls.Add(_cbDriver, 0, 1);
            header.Controls.Add(new Label { Text = "Pojazd", Font = lblFont, AutoSize = true }, 1, 0); header.Controls.Add(_cbVehicle, 1, 1);
            header.Controls.Add(new Label { Text = "Trasa", Font = lblFont, AutoSize = true }, 2, 0); header.Controls.Add(_txtRoute, 2, 1);
            header.Controls.Add(new Label { Text = "Wyjazd", Font = lblFont, AutoSize = true }, 3, 0); header.Controls.Add(_dtDeparture, 3, 1);
            header.Controls.Add(new Label { Text = "Status", Font = lblFont, AutoSize = true }, 4, 0); header.Controls.Add(_cbStatus, 4, 1);
            header.Controls.Add(_lblFill, 5, 1); header.SetColumnSpan(_lblFill, 3);
            root.Controls.Add(header, 0, 0);

            // Fill bars
            var summary = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 6, RowCount = 2, Padding = new Padding(12, 0, 12, 4), AutoSize = true };
            for (int i = 0; i < 6; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize)); summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pbMass.Height = 12; _pbMass.Dock = DockStyle.Fill; _pbMass.Maximum = 1000;
            _pbSpace.Height = 12; _pbSpace.Dock = DockStyle.Fill; _pbSpace.Maximum = 1000;
            _pbFinal.Height = 14; _pbFinal.Dock = DockStyle.Fill; _pbFinal.Maximum = 1000; _pbFinal.Style = ProgressBarStyle.Continuous;
            summary.Controls.Add(new Label { Text = "Masa %", Font = lblFont, AutoSize = true }, 0, 0); summary.Controls.Add(_pbMass, 0, 1); summary.SetColumnSpan(_pbMass, 2);
            summary.Controls.Add(new Label { Text = "Przestrzeñ %", Font = lblFont, AutoSize = true }, 2, 0); summary.Controls.Add(_pbSpace, 2, 1); summary.SetColumnSpan(_pbSpace, 2);
            summary.Controls.Add(new Label { Text = "Final %", Font = lblFont, AutoSize = true }, 4, 0); summary.Controls.Add(_pbFinal, 4, 1); summary.SetColumnSpan(_pbFinal, 2);
            root.Controls.Add(summary, 0, 1);

            // Notes (short)
            var notesPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 0, 12, 4), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            var lblNotes = new Label { Text = "Notatki:", AutoSize = true, Font = lblFont, Margin = new Padding(0, 6, 8, 0) };
            _txtNotes.Multiline = true; _txtNotes.Height = 48; _txtNotes.Width = 600; _txtNotes.MaximumSize = new Size(600, 80); _txtNotes.ScrollBars = ScrollBars.Vertical; _txtNotes.Font = new Font("Segoe UI", 9.5f);
            notesPanel.Controls.Add(lblNotes); notesPanel.Controls.Add(_txtNotes);
            root.Controls.Add(notesPanel, 0, 2);

            // Tabs
            _tabs.Dock = DockStyle.Fill; root.Controls.Add(_tabs, 0, 3);
            var tpLoads = new TabPage("£adunki"); var tpOrders = new TabPage("Wolne zamówienia");
            _tabs.TabPages.Add(tpLoads); _tabs.TabPages.Add(tpOrders);

            // Loads toolstrip
            _tsLoads.GripStyle = ToolStripGripStyle.Hidden; _tsLoads.ImageScalingSize = new Size(20, 20);
            var tsbAdd = new ToolStripButton("Dodaj (Ins)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbEdit = new ToolStripButton("Edytuj (Enter)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbDel = new ToolStripButton("Usuñ (Del)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbUp = new ToolStripButton("Góra") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbDown = new ToolStripButton("Dó³") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbRefresh = new ToolStripButton("Odœwie¿") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbToggle = new ToolStripButton("Poka¿/Zwiñ zamówienia (F9)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            tsbAdd.Click += async (_, __) => await AddLoadAsync();
            tsbEdit.Click += async (_, __) => await EditCurrentLoadAsync();
            tsbDel.Click += async (_, __) => await DeleteLoadAsync();
            tsbUp.Click += async (_, __) => await MoveSelectedLoadAsync(-1);
            tsbDown.Click += async (_, __) => await MoveSelectedLoadAsync(1);
            tsbRefresh.Click += async (_, __) => { if (_tripId != null) await LoadLoadsAsync(_tripId.Value); RecalcFill(); };
            tsbToggle.Click += (_, __) => ToggleOrdersVisibility();
            _tsLoads.Items.AddRange(new ToolStripItem[] { tsbAdd, tsbEdit, tsbDel, new ToolStripSeparator(), tsbUp, tsbDown, new ToolStripSeparator(), tsbRefresh, new ToolStripSeparator(), tsbToggle });
            _gridLoads.Dock = DockStyle.Fill; _gridLoads.AllowUserToAddRows = false; _gridLoads.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; _gridLoads.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _gridLoads.MultiSelect = false; _gridLoads.DataSource = _bsLoads; _gridLoads.RowTemplate.Height = 30; _gridLoads.CellFormatting += GridLoads_CellFormatting; _gridLoads.CellDoubleClick += async (_, __) => await EditCurrentLoadAsync(); _gridLoads.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; await EditCurrentLoadAsync(); } }; TransportUi.StyleGrid(_gridLoads); EnableDoubleBuffer(_gridLoads);
            tpLoads.Controls.Add(_gridLoads); tpLoads.Controls.Add(_tsLoads); _tsLoads.Dock = DockStyle.Top;

            // Orders toolstrip
            _tsOrders.GripStyle = ToolStripGripStyle.Hidden; _tsOrders.ImageScalingSize = new Size(20, 20);
            var tsbAddOrders = new ToolStripButton("Dodaj zaznaczone ->") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbRefreshOrders = new ToolStripButton("Odœwie¿") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            tsbAddOrders.Click += async (_, __) => await AddSelectedOrdersAsync();
            tsbRefreshOrders.Click += async (_, __) => await LoadAvailableOrdersAsync();
            _tsOrders.Items.AddRange(new ToolStripItem[] { tsbAddOrders, new ToolStripSeparator(), tsbRefreshOrders });
            _gridOrders.Dock = DockStyle.Fill; _gridOrders.ReadOnly = true; _gridOrders.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _gridOrders.MultiSelect = true; _gridOrders.DataSource = _bsOrders; _gridOrders.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; _gridOrders.RowTemplate.Height = 30; TransportUi.StyleGrid(_gridOrders); EnableDoubleBuffer(_gridOrders);
            tpOrders.Controls.Add(_gridOrders); tpOrders.Controls.Add(_tsOrders); _tsOrders.Dock = DockStyle.Top;

            // Bottom
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 50, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(12, 6, 12, 6) };
            _btnSave.Text = "Zapisz (F2) i zamknij"; _btnSave.Width = 190; _btnSave.Click += async (_, __) => { await SaveHeaderAsync(); DialogResult = DialogResult.OK; Close(); };
            bottom.Controls.Add(_btnSave); root.Controls.Add(bottom, 0, 4);

            // Status bar / tooltips
            _status.Items.Add(_statusInfo); _statusInfo.Text = ""; Controls.Add(_status); _status.Dock = DockStyle.Bottom;
            _tt.SetToolTip(_btnSave, "Zapisz kurs (F2)");
        }

        private void ToggleOrdersVisibility()
        {
            _ordersVisible = !_ordersVisible;
            if (_ordersVisible)
            {
                if (_tabs.TabPages.Count == 1)
                {
                    var tp = new TabPage("Wolne zamówienia");
                    tp.Controls.Add(_gridOrders); tp.Controls.Add(_tsOrders); _tsOrders.Dock = DockStyle.Top; _tabs.TabPages.Add(tp);
                }
            }
            else if (_tabs.TabPages.Count > 1)
            {
                _tabs.TabPages.RemoveAt(1);
            }
        }

        private static void EnableDoubleBuffer(DataGridView dgv)
        {
            try { typeof(DataGridView).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty, null, dgv, new object[] { true }); } catch { }
        }

        private void AdjustLayout() => _lblFill.MaximumSize = new Size(ClientSize.Width / 2, 0);

        private void GridLoads_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_gridLoads.Columns[e.ColumnIndex].Name == "OrderID" && e.Value != null && e.Value != DBNull.Value)
                _gridLoads.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(235, 250, 235);
            // Nie nadpisuj ClientName, bo ju¿ jest w tabeli
        }

        private async Task InitAsync()
        {
            _statusInfo.Text = "Inicjalizacja...";
            var dtD = await _repo.GetDrivers2Async(includeInactive: false);
            _cbDriver.DataSource = dtD; _cbDriver.DisplayMember = "FullName"; _cbDriver.ValueMember = "DriverID";
            var dtV = await _repo.GetVehicles2Async(kind: 3, includeInactive: false);
            _cbVehicle.DataSource = dtV; _cbVehicle.DisplayMember = "Registration"; _cbVehicle.ValueMember = "VehicleID";

            if (_tripId == null)
            {
                _dtDeparture.Value = _date.Date.AddHours(6); // domyœlnie 06:00
                _bsLoads.DataSource = CreateLoadsTable();
            }
            else
            {
                await LoadTripHeaderAsync(_tripId.Value);
                await LoadLoadsAsync(_tripId.Value);
                await EnsureClientNamesForLoadsAsync();
            }
            await LoadAvailableOrdersAsync();
            RecalcFill();
            _statusInfo.Text = "Gotowe";
            LocalizeColumns();
            // po dostêpnych zamówieniach mo¿na uzupe³niæ te¿ brakuj¹cych klientów z ³adunków (jeœli nowy kurs ju¿ ma jakieœ)
            await EnsureClientNamesForLoadsAsync();
            _gridLoads.Invalidate();
        }

        private void LocalizeColumns()
        {
            void H(DataGridView g, string name, string header, int width = 0, string? format = null)
            { if (g.Columns[name] != null) { var c = g.Columns[name]; c.HeaderText = header; if (width > 0) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; c.Width = width; } if (format != null) c.DefaultCellStyle.Format = format; } }

            // Orders: poka¿ nazwê klienta jeœli dostêpna, ukryj kolumny ID
            string[] idCols = { "KlientId", "ClientId", "CustomerId" };
            bool hasClientName = _gridOrders.Columns.Contains("ClientName");
            if (hasClientName)
            {
                foreach (var col in idCols)
                    if (_gridOrders.Columns.Contains(col)) _gridOrders.Columns[col].Visible = false;
            }
            H(_gridOrders, "OrderID", "Zam.", 60);
            if (hasClientName) H(_gridOrders, "ClientName", "Klient", 260); else
            {
                // fallback – pierwsza istniej¹ca kolumna ID jako Klient
                foreach (var col in idCols)
                {
                    if (_gridOrders.Columns.Contains(col)) { H(_gridOrders, col, "Klient", 120); break; }
                }
            }
            H(_gridOrders, "Notes", "Uwagi", 240);
            H(_gridOrders, "Kg", "Kg", 120, "N0");
            H(_gridOrders, "Status", "Status", 80);
            H(_gridOrders, "ContainersEst", "Skrzyn.", 70);
            H(_gridOrders, "PalletsEst", "Palety", 70);

            // Loads
            H(_gridLoads, "TripLoadID", "ID", 50);
            H(_gridLoads, "SequenceNo", "Lp.", 40);
            if (_gridLoads.Columns.Contains("ClientName"))
            {
                H(_gridLoads, "ClientName", "Klient", 240);
                if (_gridLoads.Columns.Contains("CustomerCode"))
                    _gridLoads.Columns["CustomerCode"].Visible = false;
            }
            else
            {
                H(_gridLoads, "CustomerCode", "Klient", 240);
            }
            H(_gridLoads, "MeatKg", "Kg", 120, "N0");
            H(_gridLoads, "CarcassCount", "Tusze", 60);
            H(_gridLoads, "PalletsH1", "Palety", 70);
            H(_gridLoads, "ContainersE2", "E2", 60);
            H(_gridLoads, "Comment", "Uwagi", 260);
        }

        private async Task LoadAvailableOrdersAsync()
        {
            var dt = await _repo.GetAvailableOrdersForDateAsync(_date);
            if (dt.Columns.Contains("TotalKg")) dt.Columns["TotalKg"].ColumnName = "Kg";

            string? clientIdCol = new[] { "KlientId", "ClientId", "CustomerId" }.FirstOrDefault(c => dt.Columns.Contains(c));
            if (clientIdCol != null)
            {
                if (!dt.Columns.Contains("ClientName")) dt.Columns.Add("ClientName", typeof(string));
                // pobierz unikalne ID (int) – bezpieczna konwersja
                var ids = dt.AsEnumerable()
                            .Select(r => { try { var v = r[clientIdCol]; if (v == null || v == DBNull.Value) return (int?)null; return Convert.ToInt32(v); } catch { return (int?)null; } })
                            .Where(i => i.HasValue)
                            .Select(i => i!.Value)
                            .Distinct()
                            .ToList();
                if (ids.Count > 0)
                {
                    var dict = await _repo.GetClientNamesAsync(ids);
                    // do³¹cz do cache (nie nadpisuj istniej¹cych innych jeœli brak)
                    foreach (var kv in dict) _clientNamesCache[kv.Key] = kv.Value;
                    foreach (DataRow r in dt.Rows)
                    {
                        int? id = null; try { var v = r[clientIdCol]; if (v != null && v != DBNull.Value) id = Convert.ToInt32(v); } catch { }
                        if (id.HasValue) r["ClientName"] = _clientNamesCache.TryGetValue(id.Value, out var nm) ? nm : id.Value.ToString(); else r["ClientName"] = string.Empty;
                    }
                }
            }
            _bsOrders.DataSource = dt; _gridOrders.DataSource = _bsOrders;
            LocalizeColumns();
        }

        private DataTable CreateLoadsTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("TripLoadID", typeof(long));
            dt.Columns.Add("TripID", typeof(long));
            dt.Columns.Add("SequenceNo", typeof(int));
            dt.Columns.Add("CustomerCode", typeof(string));
            dt.Columns.Add("ClientName", typeof(string)); // dodaj kolumnê na nazwê klienta
            dt.Columns.Add("MeatKg", typeof(decimal));
            dt.Columns.Add("CarcassCount", typeof(int));
            dt.Columns.Add("PalletsH1", typeof(int));
            dt.Columns.Add("ContainersE2", typeof(int));
            dt.Columns.Add("Comment", typeof(string));
            dt.Columns.Add("OrderID", typeof(int));
            return dt;
        }

        private async Task LoadTripHeaderAsync(long tripId)
        {
            var dt = await _repo.GetTripsByDateAsync(_date);
            var row = dt.AsEnumerable().FirstOrDefault(r => r.Field<long>("TripID") == tripId);
            if (row == null) return;
            _cbDriver.SelectedValue = row.Field<int>("DriverID");
            _cbVehicle.SelectedValue = row.Field<int>("VehicleID");
            _txtRoute.Text = row["RouteName"]?.ToString();
            if (row.Table.Columns.Contains("PlannedDepartureDT") && row["PlannedDepartureDT"] != DBNull.Value)
                _dtDeparture.Value = (DateTime)row["PlannedDepartureDT"];
            else if (row["PlannedDeparture"] != DBNull.Value)
            {
                var ts = (TimeSpan)row["PlannedDeparture"]; _dtDeparture.Value = _date.Date.Add(ts);
            }
            var statusEn = row["Status"]?.ToString(); if (!string.IsNullOrEmpty(statusEn) && _statusEnToPl.TryGetValue(statusEn, out var pl)) _cbStatus.SelectedItem = pl;
        }

        private async Task LoadLoadsAsync(long tripId)
        {
            var dt = await _repo.GetTripLoadsAsync(tripId);
            // Dodaj kolumnê ClientName jeœli nie istnieje
            if (!dt.Columns.Contains("ClientName"))
                dt.Columns.Add("ClientName", typeof(string));
            // Uzupe³nij ClientName na podstawie cache lub pobierz brakuj¹ce
            var missing = new HashSet<int>();
            foreach (DataRow r in dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                var val = r["CustomerCode"]?.ToString();
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (int.TryParse(val, out int id))
                {
                    // Pobierz shortname (skrót) zamiast pe³nej nazwy
                    if (!_clientNamesCache.TryGetValue(id, out var shortname))
                        missing.Add(id);
                    else
                        r["ClientName"] = shortname;
                }
                else
                {
                    // Jeœli CustomerCode nie jest liczb¹, ustaw ClientName na CustomerCode (Shortcut)
                    r["ClientName"] = val;
                }
            }
            if (missing.Count > 0)
            {
                try
                {
                    // Pobierz shortname zamiast pe³nej nazwy
                    var fetched = await _repo.GetClientNamesAsync(missing); // zak³adamy, ¿e GetClientNamesAsync zwraca shortname
                    foreach (var kv in fetched)
                        _clientNamesCache[kv.Key] = kv.Value;
                    foreach (DataRow r in dt.Rows)
                    {
                        if (r.RowState == DataRowState.Deleted) continue;
                        var val = r["CustomerCode"]?.ToString();
                        if (string.IsNullOrWhiteSpace(val)) continue;
                        if (int.TryParse(val, out int id) && _clientNamesCache.TryGetValue(id, out var shortname))
                            r["ClientName"] = shortname;
                    }
                }
                catch { }
            }
            _bsLoads.DataSource = dt; _gridLoads.DataSource = _bsLoads;
            await EnsureClientNamesForLoadsAsync();
            _gridLoads.Invalidate();
        }

        // Uzupe³nia cache nazw klientów dla numerów wystêpuj¹cych w kolumnie CustomerCode (gdy jest tam ID)
        private async Task EnsureClientNamesForLoadsAsync()
        {
            if (_bsLoads.DataSource is not DataTable dt) return;
            var missing = new HashSet<int>();
            foreach (DataRow r in dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                var val = r["CustomerCode"]?.ToString();
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (int.TryParse(val, out int id) && !_clientNamesCache.ContainsKey(id))
                    missing.Add(id);
            }
            if (missing.Count == 0) return;
            try
            {
                var fetched = await _repo.GetClientNamesAsync(missing);
                foreach (var kv in fetched)
                    _clientNamesCache[kv.Key] = kv.Value;
            }
            catch { }
        }

        private async Task AddSelectedOrdersAsync()
        {
            if (_gridOrders.SelectedRows.Count == 0) { MessageBox.Show("Wybierz zamówienia do dodania."); return; }
            long tripId = _tripId ?? await CreateTripIfNeededAsync();
            foreach (DataGridViewRow r in _gridOrders.SelectedRows)
                if (r.DataBoundItem is DataRowView rv)
                    await _repo.AddTripLoadFromOrderAsync(tripId, Convert.ToInt32(rv["OrderID"]), _user);
            await LoadLoadsAsync(tripId); await LoadAvailableOrdersAsync(); RecalcFill(); _tabs.SelectedIndex = 0;
        }

        private async Task AddLoadAsync()
        {
            long tripId = _tripId ?? await CreateTripIfNeededAsync();
            string customer = Prompt("Kod klienta / odbiorcy:"); if (string.IsNullOrWhiteSpace(customer)) return;
            string meatStr = Prompt("Miêso kg:", "0"); decimal.TryParse(meatStr.Replace(',', '.'), out decimal meat);
            string palStr = Prompt("Palety H1:", "0"); int.TryParse(palStr, out int pal);
            string e2Str = Prompt("E2:", "0"); int.TryParse(e2Str, out int e2);
            string carStr = Prompt("Tuszek szt (opcjonalnie):", "0"); int.TryParse(carStr, out int carc);
            string comm = Prompt("Komentarz:");
            await _repo.AddTripLoadAsync(tripId, customer, meat, carc, pal, e2, comm);
            await LoadLoadsAsync(tripId); RecalcFill();
        }

        private async Task EditCurrentLoadAsync()
        {
            if (_gridLoads.CurrentRow == null) return;
            var row = (_gridLoads.CurrentRow.DataBoundItem as DataRowView)?.Row; if (row == null) return;
            if (row["TripLoadID"] == DBNull.Value) { MessageBox.Show("Ten ³adunek nie zosta³ jeszcze zapisany."); return; }
            long id = Convert.ToInt64(row["TripLoadID"]); int seq = row.Field<int?>("SequenceNo") ?? 0; string cust = row.Field<string>("CustomerCode") ?? ""; decimal meat = row.Field<decimal?>("MeatKg") ?? 0m; int carc = row.Field<int?>("CarcassCount") ?? 0; int pal = row.Field<int?>("PalletsH1") ?? 0; int e2 = row.Field<int?>("ContainersE2") ?? 0; string comm = row.Field<string>("Comment") ?? "";
            string nCust = Prompt("Kod klienta / odbiorcy:", cust); if (string.IsNullOrWhiteSpace(nCust)) return;
            string meatStr = Prompt("Miêso kg:", meat.ToString("N0")); decimal.TryParse(meatStr.Replace(',', '.'), out meat);
            string palStr = Prompt("Palety H1:", pal.ToString()); int.TryParse(palStr, out pal);
            string e2Str = Prompt("E2:", e2.ToString()); int.TryParse(e2Str, out e2);
            string carStr = Prompt("Tuszek szt:", carc.ToString()); int.TryParse(carStr, out carc);
            string nComm = Prompt("Komentarz:", comm);
            await _repo.UpdateTripLoadAsync(id, seq, nCust, meat, carc, pal, e2, nComm); await LoadLoadsAsync(_tripId!.Value); RecalcFill();
        }

        private async Task MoveSelectedLoadAsync(int delta)
        {
            if (_gridLoads.CurrentRow == null || _tripId == null) return;
            var dt = _bsLoads.DataSource as DataTable; if (dt == null) return;
            var rows = dt.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted && r["TripLoadID"] != DBNull.Value).OrderBy(r => r.Field<int?>("SequenceNo") ?? 0).ToList(); if (rows.Count < 2) return;
            var current = (_gridLoads.CurrentRow.DataBoundItem as DataRowView)?.Row; if (current == null) return;
            int idx = rows.IndexOf(current); int newIdx = idx + delta; if (newIdx < 0 || newIdx >= rows.Count) return;
            int seqA = rows[idx].Field<int?>("SequenceNo") ?? idx + 1; int seqB = rows[newIdx].Field<int?>("SequenceNo") ?? newIdx + 1; long idA = rows[idx].Field<long>("TripLoadID"); long idB = rows[newIdx].Field<long>("TripLoadID");
            async Task UpdateRow(DataRow r, int newSeq) { await _repo.UpdateTripLoadAsync(r.Field<long>("TripLoadID"), newSeq, r.Field<string>("CustomerCode"), r.Field<decimal?>("MeatKg") ?? 0m, r.Field<int?>("CarcassCount") ?? 0, r.Field<int?>("PalletsH1") ?? 0, r.Field<int?>("ContainersE2") ?? 0, r.Field<string>("Comment")); }
            await UpdateRow(rows[idx], seqB); await UpdateRow(rows[newIdx], seqA); await _repo.RenumberTripLoadsAsync(_tripId.Value); await LoadLoadsAsync(_tripId.Value);
            foreach (DataGridViewRow gr in _gridLoads.Rows) if (gr.Cells["TripLoadID"].Value is long vv && vv == idA) { gr.Selected = true; _gridLoads.CurrentCell = gr.Cells["CustomerCode"]; break; }
        }

        private async Task<long> CreateTripIfNeededAsync()
        {
            if (_tripId != null) return _tripId.Value;
            if (_cbDriver.SelectedValue == null || _cbVehicle.SelectedValue == null) { MessageBox.Show("Wybierz kierowcê i pojazd przed dodaniem ³adunku."); throw new InvalidOperationException(); }
            var depTime = (TimeSpan?)_dtDeparture.Value.TimeOfDay; var fullDt = _dtDeparture.Value;
            var newId = await _repo.AddTripAsync(_date, (int)_cbDriver.SelectedValue, (int)_cbVehicle.SelectedValue, _txtRoute.Text, depTime, _user, fullDt);
            typeof(TripEditorForm).GetField("_tripId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(this, newId);
            Text = $"Kurs #{newId}"; return newId;
        }

        private async Task DeleteLoadAsync()
        {
            if (_gridLoads.CurrentRow == null) return; var idObj = _gridLoads.CurrentRow.Cells["TripLoadID"].Value; if (idObj == null || idObj == DBNull.Value) return;
            if (MessageBox.Show("Usun¹æ ³adunek?", "PotwierdŸ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            await _repo.DeleteTripLoadAsync(Convert.ToInt64(idObj)); await LoadLoadsAsync(_tripId!.Value); await LoadAvailableOrdersAsync(); RecalcFill();
        }

        private async Task SaveHeaderAsync()
        {
            if (_tripId == null) await CreateTripIfNeededAsync();
            var statusPl = _cbStatus.SelectedItem?.ToString() ?? "Zaplanowany"; var statusEn = _statusPlToEn.TryGetValue(statusPl, out var en) ? en : "Planned";
            var depTime = (TimeSpan?)_dtDeparture.Value.TimeOfDay; var fullDt = _dtDeparture.Value;
            await _repo.UpdateTripHeaderAsync(_tripId!.Value, (int)_cbDriver.SelectedValue, (int)_cbVehicle.SelectedValue, null, _txtRoute.Text, depTime, statusEn, _user, _txtNotes.Text, fullDt);
            _statusInfo.Text = "Zapisano";
        }

        private void RecalcFill()
        {
            if (_bsLoads.DataSource is not DataTable dt) { _lblFill.Text = "£adunek: 0 kg"; _pbMass.Value = 0; _pbSpace.Value = 0; _pbFinal.Value = 0; return; }
            decimal meat = 0; int pal = 0; int e2 = 0;
            foreach (DataRow r in dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                meat += r["MeatKg"] == DBNull.Value ? 0 : Convert.ToDecimal(r["MeatKg"]);
                pal += r["PalletsH1"] == DBNull.Value ? 0 : Convert.ToInt32(r["PalletsH1"]);
                e2 += r["ContainersE2"] == DBNull.Value ? 0 : Convert.ToInt32(r["ContainersE2"]);
            }
            if (_cbVehicle.SelectedValue == null) { _lblFill.Text = $"£adunek: {meat:N0} kg"; return; }
            var vehicleRow = (_cbVehicle.DataSource as DataTable)?.AsEnumerable().FirstOrDefault(r => r.Field<int>("VehicleID") == (int)_cbVehicle.SelectedValue);
            decimal cap = vehicleRow?.Field<decimal?>("CapacityKg") ?? 0m; int slots = vehicleRow?.Field<int?>("PalletSlotsH1") ?? 0; decimal e2Factor = vehicleRow?.Field<decimal?>("E2Factor") ?? 0.10m;
            decimal massPct = cap > 0 ? (meat / cap) * 100m : 0m; decimal spaceUnits = pal + e2 * e2Factor; decimal spacePct = slots > 0 ? (spaceUnits / slots) * 100m : 0m; decimal final = Math.Min(100m, Math.Max(massPct, spacePct));
            _lblFill.Text = $"£adunek: {meat:N0} kg  | Palety: {pal}  | E2: {e2}  | Masa {massPct:0.0}% / Przestrzeñ {spacePct:0.0}% / Final {final:0.0}%";
            _lblFill.ForeColor = final >= 100 ? Color.DarkRed : final >= 90 ? Color.OrangeRed : (final >= 70 ? Color.DarkOrange : Color.DarkGreen);
            int ToBar(decimal pct) => (int)Math.Min(1000, Math.Max(0, Math.Round(pct * 10m)));
            _pbMass.Value = ToBar(massPct); _pbSpace.Value = ToBar(spacePct); _pbFinal.Value = ToBar(final);
        }

        private static string Prompt(string text, string? initial = null)
        {
            var f = new Form { Width = 420, Height = 150, Text = text, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false };
            var tb = new TextBox { Dock = DockStyle.Top, Text = initial ?? string.Empty, Font = new Font("Segoe UI", 10.5F) };
            var ok = new Button { Text = "OK", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
            f.Controls.Add(tb); f.Controls.Add(ok); f.AcceptButton = ok; TransportUi.ApplyTheme(f);
            return f.ShowDialog() == DialogResult.OK ? tb.Text : string.Empty;
        }
    }
}
