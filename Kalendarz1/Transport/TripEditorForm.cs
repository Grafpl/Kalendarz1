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
        private readonly long? _tripId; // readonly jak pierwotnie – przy nowym kursie ustawiamy refleksj¹

        private ComboBox _cbDriver = new();
        private ComboBox _cbVehicle = new();
        private TextBox _txtRoute = new();
        private DateTimePicker _tpDeparture = new();
        private ComboBox _cbStatus = new();
        private TextBox _txtNotes = new();
        private DataGridView _gridLoads = new();
        private BindingSource _bsLoads = new();
        private Button _btnSave = new();
        private Label _lblFill = new();
        // Dodane ponownie (nieu¿ywane bezpoœrednio – zachowane dla kompatybilnoœci / EncEdit)
        private Button _btnAddLoad = new();
        private Button _btnDelLoad = new();

        // Orders integration
        private DataGridView _gridOrders = new();
        private BindingSource _bsOrders = new();

        // Nowe elementy UI
        private TabControl _tabs = new();
        private ToolStrip _tsLoads = new();
        private ToolStrip _tsOrders = new();
        private ProgressBar _pbMass = new();
        private ProgressBar _pbSpace = new();
        private ProgressBar _pbFinal = new();

        private StatusStrip _status = new();
        private ToolStripStatusLabel _statusInfo = new();
        private ToolTip _tt = new();

        // Mapowanie statusów PL -> EN
        private readonly Dictionary<string,string> _statusPlToEn = new(StringComparer.OrdinalIgnoreCase)
        {
            {"Zaplanowany","Planned"}, {"W trakcie","InProgress"}, {"Zakoñczony","Completed"}, {"Anulowany","Canceled"}
        };
        private readonly Dictionary<string,string> _statusEnToPl;

        private bool _ordersVisible = true;

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
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // summary bars
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // notes
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grids
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // bottom
            Controls.Add(root);

            // ---------------- Nag³ówek (kompaktowy) ----------------
            var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 8, RowCount = 2, Padding = new Padding(12, 10, 12, 2), AutoSize = true };
            for (int i=0;i<8;i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize)); header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Font lblFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            _cbDriver.DropDownStyle = ComboBoxStyle.DropDownList; _cbDriver.Width = 180;
            _cbVehicle.DropDownStyle = ComboBoxStyle.DropDownList; _cbVehicle.Width = 150; _cbVehicle.SelectedIndexChanged += (_, __) => RecalcFill();
            _tpDeparture.Format = DateTimePickerFormat.Time; _tpDeparture.ShowUpDown = true; _tpDeparture.Width = 80; _tpDeparture.Font = new Font("Segoe UI", 10.5F);
            _cbStatus.DropDownStyle = ComboBoxStyle.DropDownList; _cbStatus.Items.AddRange(_statusPlToEn.Keys.ToArray()); _cbStatus.SelectedIndex = 0; _cbStatus.Width = 140;
            _txtRoute.Width = 220; _txtRoute.Font = new Font("Segoe UI", 10.5F);
            _lblFill.Text = "£adunek: 0 kg"; _lblFill.AutoSize = true; _lblFill.Font = new Font("Segoe UI Semibold", 10.5F);

            header.Controls.Add(new Label{Text="Kierowca",Font=lblFont,AutoSize=true},0,0); header.Controls.Add(_cbDriver,0,1);
            header.Controls.Add(new Label{Text="Pojazd",Font=lblFont,AutoSize=true},1,0); header.Controls.Add(_cbVehicle,1,1);
            header.Controls.Add(new Label{Text="Trasa",Font=lblFont,AutoSize=true},2,0); header.Controls.Add(_txtRoute,2,1);
            header.Controls.Add(new Label{Text="Wyjazd",Font=lblFont,AutoSize=true},3,0); header.Controls.Add(_tpDeparture,3,1);
            header.Controls.Add(new Label{Text="Status",Font=lblFont,AutoSize=true},4,0); header.Controls.Add(_cbStatus,4,1);
            header.Controls.Add(_lblFill,5,1); header.SetColumnSpan(_lblFill,3);
            root.Controls.Add(header,0,0);

            // ---------------- Paski wype³nienia ----------------
            var summary = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 6, RowCount = 2, Padding = new Padding(12,0,12,4), AutoSize = true };
            for(int i=0;i<6;i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize)); summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pbMass.Height = 12; _pbMass.Dock = DockStyle.Fill; _pbMass.Maximum = 1000;
            _pbSpace.Height = 12; _pbSpace.Dock = DockStyle.Fill; _pbSpace.Maximum = 1000;
            _pbFinal.Height = 14; _pbFinal.Dock = DockStyle.Fill; _pbFinal.Maximum = 1000; _pbFinal.Style = ProgressBarStyle.Continuous;
            summary.Controls.Add(new Label{Text="Masa %",Font=lblFont,AutoSize=true},0,0); summary.Controls.Add(_pbMass,0,1); summary.SetColumnSpan(_pbMass,2);
            summary.Controls.Add(new Label{Text="Przestrzeñ %",Font=lblFont,AutoSize=true},2,0); summary.Controls.Add(_pbSpace,2,1); summary.SetColumnSpan(_pbSpace,2);
            summary.Controls.Add(new Label{Text="Final %",Font=lblFont,AutoSize=true},4,0); summary.Controls.Add(_pbFinal,4,1); summary.SetColumnSpan(_pbFinal,2);
            root.Controls.Add(summary,0,1);

            // ---------------- Notatki ----------------
            var notesPanel = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Padding = new Padding(12,0,12,4), AutoSize = true };
            notesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            notesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            notesPanel.Controls.Add(new Label{ Text="Notatki:", AutoSize = true, Font = lblFont, Margin = new Padding(0,4,8,0)},0,0);
            _txtNotes.Multiline = true; _txtNotes.Height = 50; _txtNotes.ScrollBars = ScrollBars.Vertical; _txtNotes.Dock = DockStyle.Fill; _txtNotes.Font = new Font("Segoe UI", 9.5f);
            notesPanel.Controls.Add(_txtNotes,1,0);
            root.Controls.Add(notesPanel,0,2);

            // ---------------- Grids (taby) ----------------
            _tabs.Dock = DockStyle.Fill; root.Controls.Add(_tabs,0,3);
            var tpLoads = new TabPage("£adunki"); var tpOrders = new TabPage("Wolne zamówienia");
            _tabs.TabPages.Add(tpLoads); _tabs.TabPages.Add(tpOrders);

            // Toolstrip loads (rozbudowany)
            _tsLoads.GripStyle = ToolStripGripStyle.Hidden; _tsLoads.ImageScalingSize = new Size(20,20);
            var tsbAddManual = new ToolStripButton("Dodaj (Ins)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbEdit = new ToolStripButton("Edytuj (Enter)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbDel = new ToolStripButton("Usuñ (Del)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbUp = new ToolStripButton("Góra") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbDown = new ToolStripButton("Dó³") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbRefreshLoads = new ToolStripButton("Odœwie¿") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbToggleOrders = new ToolStripButton("Poka¿/Zwiñ zamówienia (F9)") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            tsbAddManual.Click += async (_, __) => await AddLoadAsync();
            tsbEdit.Click += async (_, __) => await EditCurrentLoadAsync();
            tsbDel.Click += async (_, __) => await DeleteLoadAsync();
            tsbUp.Click += async (_, __) => await MoveSelectedLoadAsync(-1);
            tsbDown.Click += async (_, __) => await MoveSelectedLoadAsync(1);
            tsbRefreshLoads.Click += async (_, __) => { if(_tripId!=null) await LoadLoadsAsync(_tripId.Value); RecalcFill(); };
            tsbToggleOrders.Click += (_, __) => ToggleOrdersVisibility();
            _tsLoads.Items.AddRange(new ToolStripItem[]{ tsbAddManual, tsbEdit, tsbDel, new ToolStripSeparator(), tsbUp, tsbDown, new ToolStripSeparator(), tsbRefreshLoads, new ToolStripSeparator(), tsbToggleOrders });
            _gridLoads.Dock = DockStyle.Fill; _gridLoads.AllowUserToAddRows = false; _gridLoads.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _gridLoads.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _gridLoads.MultiSelect = false; _gridLoads.DataSource = _bsLoads; _gridLoads.RowTemplate.Height = 30; _gridLoads.CellFormatting += GridLoads_CellFormatting; _gridLoads.CellDoubleClick += async (_, __) => await EditCurrentLoadAsync(); _gridLoads.KeyDown += async (s,e)=> { if(e.KeyCode==Keys.Enter){ e.Handled=true; await EditCurrentLoadAsync(); } }; TransportUi.StyleGrid(_gridLoads);
            EnableDoubleBuffer(_gridLoads);
            tpLoads.Controls.Add(_gridLoads); tpLoads.Controls.Add(_tsLoads); _tsLoads.Dock = DockStyle.Top;

            // Toolstrip orders
            _tsOrders.GripStyle = ToolStripGripStyle.Hidden; _tsOrders.ImageScalingSize = new Size(20,20);
            var tsbAddOrders = new ToolStripButton("Dodaj zaznaczone ->") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            var tsbRefreshOrders = new ToolStripButton("Odœwie¿") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            tsbAddOrders.Click += async (_, __) => await AddSelectedOrdersAsync();
            tsbRefreshOrders.Click += async (_, __) => await LoadAvailableOrdersAsync();
            _tsOrders.Items.AddRange(new ToolStripItem[]{ tsbAddOrders, new ToolStripSeparator(), tsbRefreshOrders });
            _gridOrders.Dock = DockStyle.Fill; _gridOrders.ReadOnly = true; _gridOrders.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _gridOrders.MultiSelect = true; _gridOrders.DataSource = _bsOrders; _gridOrders.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _gridOrders.RowTemplate.Height = 30; TransportUi.StyleGrid(_gridOrders); EnableDoubleBuffer(_gridOrders);
            tpOrders.Controls.Add(_gridOrders); tpOrders.Controls.Add(_tsOrders); _tsOrders.Dock = DockStyle.Top;

            // ---------------- Dolny panel (Zapis) ----------------
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 50, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(12,6,12,6) };
            _btnSave.Text = "Zapisz (F2) i zamknij"; _btnSave.Width = 190; _btnSave.Click += async (_, __) => { await SaveHeaderAsync(); DialogResult = DialogResult.OK; Close(); };
            bottom.Controls.Add(_btnSave);
            root.Controls.Add(bottom,0,4);

            _status.Items.Add(_statusInfo); _statusInfo.Text = ""; Controls.Add(_status); _status.Dock = DockStyle.Bottom;

            // Tooltips
            _tt.SetToolTip(_btnSave, "Zapisz kurs (F2)");
            _tt.SetToolTip(_gridLoads, "Dwuklik lub Enter – edycja ³adunku. Strza³ki Góra/Dó³ + przyciski do zmiany kolejnoœci.");
        }

        private void ToggleOrdersVisibility()
        {
            _ordersVisible = !_ordersVisible;
            if (_ordersVisible)
            {
                if (!_tabs.TabPages.ContainsKey("orders"))
                {
                    // nic – zak³adka ju¿ istnieje? dodana w BuildUi – nazwy brak Key, wiêc uproszczenie: poka¿ indeks 1
                }
                if (_tabs.TabPages.Count == 1)
                {
                    var tp = new TabPage("Wolne zamówienia");
                    tp.Controls.Add(_gridOrders); tp.Controls.Add(_tsOrders); _tsOrders.Dock = DockStyle.Top; _tabs.TabPages.Add(tp);
                }
            }
            else
            {
                if (_tabs.TabPages.Count > 1)
                {
                    _tabs.TabPages.RemoveAt(1);
                }
            }
        }

        private static void EnableDoubleBuffer(DataGridView dgv)
        {
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty, null, dgv, new object[] { true });
            }
            catch { }
        }

        private void AdjustLayout()
        {
            // Mo¿liwe przysz³e dopasowania – obecnie tylko zabezpieczenie, aby lblFill nie wychodzi³ poza obszar
            _lblFill.MaximumSize = new Size(ClientSize.Width / 2, 0);
        }

        private void GridLoads_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_gridLoads.Columns[e.ColumnIndex].Name == "OrderID" && e.Value != null && e.Value != DBNull.Value)
            {
                _gridLoads.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(235, 250, 235);
            }
        }

        private async Task InitAsync()
        {
            _statusInfo.Text = "Inicjalizacja...";
            var dtD = await _repo.GetDrivers2Async(includeInactive: false);
            _cbDriver.DataSource = dtD; _cbDriver.DisplayMember = "FullName"; _cbDriver.ValueMember = "DriverID";
            var dtV = await _repo.GetVehicles2Async(kind:3, includeInactive:false);
            _cbVehicle.DataSource = dtV; _cbVehicle.DisplayMember = "Registration"; _cbVehicle.ValueMember = "VehicleID";

            if (_tripId == null)
            {
                _tpDeparture.Value = DateTime.Today.Date.AddHours(6);
                _bsLoads.DataSource = CreateLoadsTable();
            }
            else
            {
                await LoadTripHeaderAsync(_tripId.Value);
                await LoadLoadsAsync(_tripId.Value);
            }
            await LoadAvailableOrdersAsync();
            RecalcFill();
            _statusInfo.Text = "Gotowe";
            LocalizeColumns();
        }

        private void LocalizeColumns()
        {
            void H(DataGridView g,string name,string header,int width=0,string? format=null){ if(g.Columns[name]!=null){ var c=g.Columns[name]; c.HeaderText=header; if(width>0) c.Width=width; if(format!=null) c.DefaultCellStyle.Format=format; }}
            H(_gridOrders,"OrderID","Zam."); H(_gridOrders,"ClientId","Klient"); H(_gridOrders,"Kg","Kg");
            H(_gridOrders,"Status","Status"); H(_gridOrders,"Notes","Uwagi",220); H(_gridOrders,"ContainersEst","Skrzyn."); H(_gridOrders,"PalletsEst","Palety");
            H(_gridLoads,"TripLoadID","ID"); H(_gridLoads,"SequenceNo","Lp."); H(_gridLoads,"CustomerCode","Klient"); H(_gridLoads,"MeatKg","Kg",0,"N0"); H(_gridLoads,"CarcassCount","Tusze"); H(_gridLoads,"PalletsH1","Palety"); H(_gridLoads,"ContainersE2","E2"); H(_gridLoads,"Comment","Uwagi",220);
        }

        private async Task LoadAvailableOrdersAsync()
        {
            var dt = await _repo.GetAvailableOrdersForDateAsync(_date);
            _bsOrders.DataSource = dt;
            if (dt.Columns.Contains("TotalKg")) dt.Columns["TotalKg"].ColumnName = "Kg";
            LocalizeColumns();
        }

        private DataTable CreateLoadsTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("TripLoadID", typeof(long));
            dt.Columns.Add("TripID", typeof(long));
            dt.Columns.Add("SequenceNo", typeof(int));
            dt.Columns.Add("CustomerCode", typeof(string));
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
            if (row["PlannedDeparture"] != DBNull.Value)
            {
                var ts = (TimeSpan)row["PlannedDeparture"]; _tpDeparture.Value = DateTime.Today.Date.Add(ts);
            }
            var statusEn = row["Status"]?.ToString();
            if (!string.IsNullOrEmpty(statusEn) && _statusEnToPl.TryGetValue(statusEn, out var pl))
            {
                _cbStatus.SelectedItem = pl;
            }
        }

        private async Task LoadLoadsAsync(long tripId)
        {
            var dt = await _repo.GetTripLoadsAsync(tripId);
            _bsLoads.DataSource = dt;
            LocalizeColumns();
        }

        private async Task AddSelectedOrdersAsync()
        {
            if (_gridOrders.SelectedRows.Count == 0)
            {
                MessageBox.Show("Wybierz zamówienia do dodania.");
                return;
            }
            long tripId = _tripId ?? await CreateTripIfNeededAsync();
            foreach (DataGridViewRow r in _gridOrders.SelectedRows)
            {
                if (r.DataBoundItem is DataRowView rv)
                {
                    int orderId = Convert.ToInt32(rv["OrderID"]);
                    await _repo.AddTripLoadFromOrderAsync(tripId, orderId, _user);
                }
            }
            await LoadLoadsAsync(tripId);
            await LoadAvailableOrdersAsync();
            RecalcFill();
            _tabs.SelectedIndex = 0; // prze³¹cz na ³adunki po dodaniu
        }

        private async Task AddLoadAsync()
        {
            long tripId = _tripId ?? await CreateTripIfNeededAsync();
            string customer = Prompt("Kod klienta / odbiorcy:");
            if (string.IsNullOrWhiteSpace(customer)) return;
            string meatStr = Prompt("Miêso kg:", "0"); decimal.TryParse(meatStr.Replace(',', '.'), out decimal meat);
            string palStr = Prompt("Palety H1:", "0"); int.TryParse(palStr, out int pal);
            string e2Str = Prompt("E2:", "0"); int.TryParse(e2Str, out int e2);
            string carStr = Prompt("Tuszek szt (opcjonalnie):", "0"); int.TryParse(carStr, out int carc);
            string comm = Prompt("Komentarz:");
            await _repo.AddTripLoadAsync(tripId, customer, meat, carc, pal, e2, comm);
            await LoadLoadsAsync(tripId);
            RecalcFill();
        }

        private async Task EditCurrentLoadAsync()
        {
            if (_gridLoads.CurrentRow == null) return;
            var row = (_gridLoads.CurrentRow.DataBoundItem as DataRowView)?.Row;
            if (row == null) return;
            if (row["TripLoadID"] == DBNull.Value) { MessageBox.Show("Ten ³adunek nie zosta³ jeszcze zapisany."); return; }
            long id = Convert.ToInt64(row["TripLoadID"]);
            int seq = row.Field<int?>("SequenceNo") ?? 0;
            string cust = row.Field<string>("CustomerCode") ?? "";
            decimal meat = row.Field<decimal?>("MeatKg") ?? 0m;
            int carc = row.Field<int?>("CarcassCount") ?? 0;
            int pal = row.Field<int?>("PalletsH1") ?? 0;
            int e2 = row.Field<int?>("ContainersE2") ?? 0;
            string comm = row.Field<string>("Comment") ?? "";

            string nCust = Prompt("Kod klienta / odbiorcy:", cust); if (string.IsNullOrWhiteSpace(nCust)) return;
            string meatStr = Prompt("Miêso kg:", meat.ToString("N0")); decimal.TryParse(meatStr.Replace(',', '.'), out meat);
            string palStr = Prompt("Palety H1:", pal.ToString()); int.TryParse(palStr, out pal);
            string e2Str = Prompt("E2:", e2.ToString()); int.TryParse(e2Str, out e2);
            string carStr = Prompt("Tuszek szt:", carc.ToString()); int.TryParse(carStr, out carc);
            string nComm = Prompt("Komentarz:", comm);
            await _repo.UpdateTripLoadAsync(id, seq, nCust, meat, carc, pal, e2, nComm);
            await LoadLoadsAsync(_tripId!.Value);
            RecalcFill();
        }

        private async Task MoveSelectedLoadAsync(int delta)
        {
            if (_gridLoads.CurrentRow == null || _tripId == null) return;
            var dt = _bsLoads.DataSource as DataTable; if (dt == null) return;
            var rows = dt.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted && r["TripLoadID"] != DBNull.Value).OrderBy(r => r.Field<int?>("SequenceNo") ?? 0).ToList();
            if (rows.Count < 2) return;
            var current = (_gridLoads.CurrentRow.DataBoundItem as DataRowView)?.Row; if (current == null) return;
            int idx = rows.IndexOf(current); if (idx < 0) return;
            int newIdx = idx + delta; if (newIdx < 0 || newIdx >= rows.Count) return;
            // Zamiana SequenceNo
            int seqA = rows[idx].Field<int?>("SequenceNo") ?? idx+1;
            int seqB = rows[newIdx].Field<int?>("SequenceNo") ?? newIdx+1;
            long idA = rows[idx].Field<long>("TripLoadID"); long idB = rows[newIdx].Field<long>("TripLoadID");
            // Wczytaj wartoœci aby zupdateowaæ (UpdateTripLoadAsync wymaga pe³nych parametrów)
            async Task UpdateRow(DataRow r, int newSeq)
            {
                await _repo.UpdateTripLoadAsync(r.Field<long>("TripLoadID"), newSeq,
                    r.Field<string>("CustomerCode"), r.Field<decimal?>("MeatKg") ?? 0m, r.Field<int?>("CarcassCount") ?? 0,
                    r.Field<int?>("PalletsH1") ?? 0, r.Field<int?>("ContainersE2") ?? 0, r.Field<string>("Comment"));
            }
            await UpdateRow(rows[idx], seqB);
            await UpdateRow(rows[newIdx], seqA);
            await _repo.RenumberTripLoadsAsync(_tripId.Value);
            await LoadLoadsAsync(_tripId.Value);
            // Ustaw fokus na przeniesiony wiersz
            var refreshed = (_bsLoads.DataSource as DataTable)?.AsEnumerable().OrderBy(r => r.Field<int?>("SequenceNo") ?? 0).ToList();
            if (refreshed != null && newIdx >=0 && newIdx < refreshed.Count)
            {
                var idTarget = delta < 0 ? idA : idA; // po zamianie idA ma now¹ pozycjê (delta -/+)
                foreach (DataGridViewRow gr in _gridLoads.Rows)
                {
                    if (gr.Cells["TripLoadID"].Value is long vv && vv == idA) { gr.Selected = true; _gridLoads.CurrentCell = gr.Cells["CustomerCode"]; break; }
                }
            }
        }

        private async Task<long> CreateTripIfNeededAsync()
        {
            if (_tripId != null) return _tripId.Value;
            if (_cbDriver.SelectedValue == null || _cbVehicle.SelectedValue == null)
            {
                MessageBox.Show("Wybierz kierowcê i pojazd przed dodaniem ³adunku.");
                throw new System.InvalidOperationException();
            }
            var dep = (TimeSpan?)_tpDeparture.Value.TimeOfDay;
            var newId = await _repo.AddTripAsync(_date, (int)_cbDriver.SelectedValue, (int)_cbVehicle.SelectedValue, _txtRoute.Text, dep, _user);
            typeof(TripEditorForm).GetField("_tripId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(this, newId);
            Text = $"Kurs #{newId}";
            return newId;
        }

        private async Task DeleteLoadAsync()
        {
            if (_gridLoads.CurrentRow == null) return;
            var idObj = _gridLoads.CurrentRow.Cells["TripLoadID"].Value; if (idObj == null || idObj == DBNull.Value) return;
            if (MessageBox.Show("Usun¹æ ³adunek?", "PotwierdŸ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            await _repo.DeleteTripLoadAsync(Convert.ToInt64(idObj));
            await LoadLoadsAsync(_tripId!.Value);
            await LoadAvailableOrdersAsync();
            RecalcFill();
        }

        private async Task SaveHeaderAsync()
        {
            // jeœli kurs jeszcze nie utworzony – utwórz od razu (pozwala zapisaæ nag³ówek bez ³adunków)
            if (_tripId == null)
            {
                await CreateTripIfNeededAsync();
            }
            var statusPl = _cbStatus.SelectedItem?.ToString() ?? "Zaplanowany";
            var statusEn = _statusPlToEn.TryGetValue(statusPl, out var en) ? en : "Planned";
            await _repo.UpdateTripHeaderAsync(_tripId!.Value, (int)_cbDriver.SelectedValue, (int)_cbVehicle.SelectedValue, null, _txtRoute.Text, _tpDeparture.Value.TimeOfDay, statusEn, _user, _txtNotes.Text);
            _statusInfo.Text = "Zapisano";
        }

        private void RecalcFill()
        {
            if (_bsLoads.DataSource is not DataTable dt) { _lblFill.Text = "£adunek: 0 kg"; _pbMass.Value = 0; _pbSpace.Value = 0; _pbFinal.Value = 0; return; }
            decimal meat = 0; int pal=0; int e2=0;
            foreach (DataRow r in dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                meat += r["MeatKg"] == DBNull.Value ? 0 : Convert.ToDecimal(r["MeatKg"]);
                pal  += r["PalletsH1"] == DBNull.Value ? 0 : Convert.ToInt32(r["PalletsH1"]);
                e2   += r["ContainersE2"] == DBNull.Value ? 0 : Convert.ToInt32(r["ContainersE2"]);
            }
            if (_cbVehicle.SelectedValue == null) { _lblFill.Text = $"£adunek: {meat:N0} kg"; return; }
            var vehicleRow = (_cbVehicle.DataSource as DataTable)?.AsEnumerable().FirstOrDefault(r => r.Field<int>("VehicleID") == (int)_cbVehicle.SelectedValue);
            decimal cap = vehicleRow?.Field<decimal?>("CapacityKg") ?? 0m;
            int slots = vehicleRow?.Field<int?>("PalletSlotsH1") ?? 0;
            decimal e2Factor = vehicleRow?.Field<decimal?>("E2Factor") ?? 0.10m;
            decimal massPct = cap > 0 ? (meat / cap) * 100m : 0m;
            decimal spaceUnits = pal + e2 * e2Factor;
            decimal spacePct = slots > 0 ? (spaceUnits / slots) * 100m : 0m;
            decimal final = Math.Min(100m, Math.Max(massPct, spacePct));
            _lblFill.Text = $"£adunek: {meat:N0} kg  | Palety: {pal}  | E2: {e2}  | Masa {massPct:0.0}% / Przestrzeñ {spacePct:0.0}% / Final {final:0.0}%";
            _lblFill.ForeColor = final >= 100 ? Color.DarkRed : final >= 90 ? Color.OrangeRed : (final >= 70 ? Color.DarkOrange : Color.DarkGreen);
            int ToBar(decimal pct) => (int)Math.Min(1000, Math.Max(0, Math.Round(pct * 10m)));
            _pbMass.Value = ToBar(massPct);
            _pbSpace.Value = ToBar(spacePct);
            _pbFinal.Value = ToBar(final);
        }

        private static string Prompt(string text, string? initial = null)
        {
            var f = new Form { Width = 420, Height = 150, Text = text, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox=false, MaximizeBox=false, ShowInTaskbar=false };
            var tb = new TextBox { Dock = DockStyle.Top, Text = initial ?? string.Empty, Font = new Font("Segoe UI", 10.5F) };
            var ok = new Button { Text = "OK", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
            f.Controls.Add(tb); f.Controls.Add(ok); f.AcceptButton = ok;
            TransportUi.ApplyTheme(f);
            return f.ShowDialog() == DialogResult.OK ? tb.Text : string.Empty;
        }
    }
}
