using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Panel Transportu – planowanie kursów oraz przypisywanie zamówieñ.
    /// </summary>
    public partial class WidokTransport : Form
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly BindingSource _bsTrips = new();
        private readonly BindingSource _bsTripOrders = new();
        private readonly BindingSource _bsFreeOrders = new();
        private DateTime _date = DateTime.Today;

        private DataGridView dgvTrips = new();
        private DataGridView dgvTripOrders = new();
        private DataGridView dgvFreeOrders = new();
        private DateTimePicker dtpDay = new();
        private Button btnNewTrip = new();
        private Button btnDeleteTrip = new();
        private Button btnAssign = new();
        private Button btnUnassign = new();
        private Button btnRefresh = new();
        private TextBox txtNotes = new();
        private ComboBox cbStatus = new();
        private Button btnManageDrivers = new();
        private Button btnManageVehicles = new();
        private TransportRepository _repo;

        public string UserID { get; set; } = Environment.UserName;

        public WidokTransport()
        {
            _repo = new TransportRepository(_connLibra);
            BuildUi();
            Load += async (_, __) => await RefreshAllAsync();
        }

        private void BuildUi()
        {
            Text = "Panel Transportu";
            Width = 1400;
            Height = 800;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            dtpDay.Value = _date;
            dtpDay.ValueChanged += async (s, e) => { _date = dtpDay.Value.Date; await RefreshAllAsync(); };
            btnRefresh.Text = "Odœwie¿"; btnRefresh.Click += async (_, __) => await RefreshAllAsync();
            btnNewTrip.Text = "+ Kurs"; btnNewTrip.Click += async (_, __) => await CreateTripAsync();
            btnDeleteTrip.Text = "Usuñ kurs"; btnDeleteTrip.Click += async (_, __) => await DeleteSelectedTripAsync();
            btnAssign.Text = ">>"; btnAssign.Click += async (_, __) => await AssignOrdersAsync();
            btnUnassign.Text = "<<"; btnUnassign.Click += async (_, __) => await UnassignOrdersAsync();
            cbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cbStatus.Items.AddRange(new object[] {"Planned","InProgress","Completed","Canceled"});
            cbStatus.SelectedIndexChanged += async (_, __) => await UpdateTripStatusAsync();
            btnManageDrivers.Text = "Kierowcy"; btnManageDrivers.Click += async (_, __) => await ShowDriversDialogAsync();
            btnManageVehicles.Text = "Pojazdy"; btnManageVehicles.Click += async (_, __) => await ShowVehiclesDialogAsync();

            top.Controls.AddRange(new Control[] { new Label{Text="Dzieñ:"}, dtpDay, btnRefresh, btnNewTrip, btnDeleteTrip, new Label{Text="Status:"}, cbStatus, btnAssign, btnUnassign, btnManageDrivers, btnManageVehicles });
            root.Controls.Add(top,0,0); root.SetColumnSpan(top,3);

            ConfigureGrid(dgvTrips, "Kursy");
            ConfigureGrid(dgvTripOrders, "Zamówienia w kursie");
            ConfigureGrid(dgvFreeOrders, "Wolne zamówienia dnia");

            dgvTrips.SelectionChanged += async (_, __) => await LoadTripOrdersAsync();
            dgvTrips.CellEndEdit += async (_, __) => await PersistTripInlineAsync();
            dgvTripOrders.CellEndEdit += async (_, __) => await PersistTripOrderInlineAsync();

            root.Controls.Add(dgvTrips,0,1);
            root.Controls.Add(dgvTripOrders,1,1);
            root.Controls.Add(dgvFreeOrders,2,1);

            txtNotes.Multiline = true; txtNotes.Dock = DockStyle.Bottom; txtNotes.Height = 80;
            txtNotes.Leave += async (_, __) => await SaveNotesAsync();
            Controls.Add(txtNotes);
        }

        private void ConfigureGrid(DataGridView g, string caption)
        {
            g.Dock = DockStyle.Fill;
            g.ReadOnly = false;
            g.AllowUserToAddRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.MultiSelect = true;
            g.Tag = caption;
        }

        private async Task RefreshAllAsync()
        {
            await LoadTripsAsync();
            await LoadFreeOrdersAsync();
            await LoadTripOrdersAsync();
        }

        private async Task LoadTripsAsync()
        {
            var dt = new DataTable();
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand(@"SELECT TripID, TripDate, Status, DriverGID, CarID, TrailerID, PlannedDeparture, PickupWindowFrom, PickupWindowTo, CombineGroup, Notes FROM dbo.TransportTrip WHERE TripDate=@d ORDER BY PlannedDeparture, TripID", cn);
            cmd.Parameters.AddWithValue("@d", _date);
            using var da = new SqlDataAdapter(cmd); da.Fill(dt);
            _bsTrips.DataSource = dt; dgvTrips.DataSource = _bsTrips;
            // Kolumny – uproszczenie
            if (!dgvTrips.Columns.Contains("TripID")) return;
            cbStatus.Enabled = dgvTrips.CurrentRow != null;
            if (dgvTrips.CurrentRow != null)
            {
                cbStatus.SelectedItem = dgvTrips.CurrentRow.Cells["Status"].Value?.ToString();
                txtNotes.Text = dgvTrips.CurrentRow.Cells["Notes"].Value?.ToString();
            }
        }

        private async Task ShowDriversDialogAsync()
        {
            var dt = await _repo.GetDriversAsync();
            using var f = new Form { Text = "Kierowcy (Typ=10)", Width = 650, Height = 500, StartPosition = FormStartPosition.CenterParent };
            var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = dt, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            var panelBtns = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            var btnAdd = new Button { Text = "Dodaj" };
            var btnDelete = new Button { Text = "Usuñ (soft)" };
            var btnRefreshLocal = new Button { Text = "Odœwie¿" };
            var btnClose = new Button { Text = "Zamknij" };
            btnAdd.Click += async (_, __) => { var name = Prompt("Nowy kierowca - imiê i nazwisko:"); if (string.IsNullOrWhiteSpace(name)) return; await _repo.AddDriverAsync(name, UserID); grid.DataSource = await _repo.GetDriversAsync(); };
            btnDelete.Click += async (_, __) => { if (grid.CurrentRow==null) return; var idObj = grid.CurrentRow.Cells["GID"].Value; if (idObj==null) return; if (MessageBox.Show("Usun¹æ (soft) kierowcê?","PotwierdŸ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)==DialogResult.Yes){ await _repo.SoftDeleteDriverAsync(Convert.ToInt32(idObj)); grid.DataSource = await _repo.GetDriversAsync(); }};
            btnRefreshLocal.Click += async (_, __) => grid.DataSource = await _repo.GetDriversAsync();
            btnClose.Click += (_, __) => f.Close();
            panelBtns.Controls.AddRange(new Control[]{btnAdd,btnDelete,btnRefreshLocal,btnClose});
            f.Controls.Add(grid); f.Controls.Add(panelBtns);
            f.ShowDialog(this);
        }

        private async Task ShowVehiclesDialogAsync()
        {
            using var f = new Form { Text = "Pojazdy i naczepy (Kind 3/4)", Width = 900, Height = 550, StartPosition = FormStartPosition.CenterParent };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var tabSam = new TabPage("Samochody (3)");
            var tabNacz = new TabPage("Naczepy (4)");
            tabs.TabPages.Add(tabSam); tabs.TabPages.Add(tabNacz);
            f.Controls.Add(tabs);

            void BuildVehicleTab(TabPage tab, string kind)
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 }; tab.Controls.Add(layout);
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                var btnAdd = new Button { Text = "Dodaj" };
                var btnEdit = new Button { Text = "Zapisz zmiany" };
                var btnRefreshLocal = new Button { Text = "Odœwie¿" };
                var grid = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false };
                bar.Controls.AddRange(new Control[]{btnAdd, btnEdit, btnRefreshLocal});
                layout.Controls.Add(bar,0,0); layout.Controls.Add(grid,0,1);

                async Task RefreshGrid(){ grid.DataSource = await _repo.GetVehiclesAsync(kind); }
                btnRefreshLocal.Click += async (_, __) => await RefreshGrid();
                btnAdd.Click += async (_, __) =>
                {
                    var id = Prompt("Rejestracja (ID):"); if (string.IsNullOrWhiteSpace(id)) return;
                    var brand = Prompt("Marka:");
                    var model = Prompt("Model:");
                    var capacityStr = Prompt("£adownoœæ (kg) – opcjonalnie:");
                    decimal? cap = null; if (decimal.TryParse(capacityStr.Replace(',','.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var capV)) cap = capV;
                    try { await _repo.AddVehicleAsync(id, kind, brand, model, cap); await RefreshGrid(); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                btnEdit.Click += async (_, __) =>
                {
                    if (grid.CurrentRow == null) return; var idObj = grid.CurrentRow.Cells["ID"].Value?.ToString(); if (string.IsNullOrWhiteSpace(idObj)) return;
                    string? brand = grid.Columns.Contains("Brand") ? grid.CurrentRow.Cells["Brand"].Value?.ToString() : null;
                    string? model = grid.Columns.Contains("Model") ? grid.CurrentRow.Cells["Model"].Value?.ToString() : null;
                    decimal? cap = null;
                    if (grid.Columns.Contains("Capacity"))
                    {
                        var capStr = grid.CurrentRow.Cells["Capacity"].Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(capStr) && decimal.TryParse(capStr.Replace(',','.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c))
                            cap = c;
                    }
                    try { await _repo.UpdateVehicleAsync(idObj, brand, model, cap); MessageBox.Show("Zapisano.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                grid.DataBindingComplete += (_, __) =>
                {
                    if (grid.Columns.Contains("Kind")) grid.Columns["Kind"].HeaderText = "Rodzaj";
                    if (grid.Columns.Contains("Brand")) grid.Columns["Brand"].HeaderText = "Marka";
                    if (grid.Columns.Contains("Model")) grid.Columns["Model"].HeaderText = "Model";
                    if (grid.Columns.Contains("Capacity")) grid.Columns["Capacity"].HeaderText = "£adownoœæ";
                    if (grid.Columns.Contains("ID")) grid.Columns["ID"].HeaderText = "Rejestracja";
                };
                grid.CellBeginEdit += (_, e) => { /* edycja w locie – zapis przyciskiem */ };
                _ = RefreshGrid();
            }

            BuildVehicleTab(tabSam, "3");
            BuildVehicleTab(tabNacz, "4");
            f.ShowDialog(this);
        }

        private static string Prompt(string text)
        {
            var f = new Form { Width = 400, Height = 150, Text = text, StartPosition = FormStartPosition.CenterParent };
            var tb = new TextBox { Dock = DockStyle.Top };
            var ok = new Button { Text = "OK", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
            f.Controls.Add(tb); f.Controls.Add(ok); f.AcceptButton = ok;
            return f.ShowDialog() == DialogResult.OK ? tb.Text : string.Empty;
        }

        private async Task LoadFreeOrdersAsync()
        {
            var dt = new DataTable();
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            // Usuniêto JOIN do nieistniej¹cej bazy / schematu – pozostaje tylko ID klienta.
            string sql = @"SELECT zm.Id AS OrderID, zm.KlientId, zm.Status, zm.DataZamowienia
                            FROM dbo.ZamowieniaMieso zm
                            WHERE zm.DataZamowienia=@d AND zm.Status NOT IN ('Anulowane')
                              AND NOT EXISTS(SELECT 1 FROM dbo.TransportTripOrder tto JOIN dbo.TransportTrip tt ON tto.TripID=tt.TripID WHERE tt.TripDate=@d AND tto.OrderID=zm.Id)";
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@d", _date);
            using var da = new SqlDataAdapter(cmd); da.Fill(dt);
            if (!dt.Columns.Contains("KlientDisplay")) dt.Columns.Add("KlientDisplay", typeof(string));
            foreach (DataRow r in dt.Rows)
            {
                r["KlientDisplay"] = r["KlientId"]?.ToString();
            }
            _bsFreeOrders.DataSource = dt; dgvFreeOrders.DataSource = _bsFreeOrders;
            if (dgvFreeOrders.Columns.Contains("KlientId")) dgvFreeOrders.Columns["KlientId"].HeaderText = "KlientID";
            if (dgvFreeOrders.Columns.Contains("KlientDisplay")) dgvFreeOrders.Columns["KlientDisplay"].HeaderText = "Klient";
        }

        private async Task LoadTripOrdersAsync()
        {
            var tripId = SelectedTripID;
            var dt = new DataTable();
            if (tripId == null) { _bsTripOrders.DataSource = dt; dgvTripOrders.DataSource = _bsTripOrders; return; }
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand(@"SELECT TripOrderID, TripID, OrderID, SequenceNo, PlannedPickup, MergeNote FROM dbo.TransportTripOrder WHERE TripID=@id ORDER BY SequenceNo, TripOrderID", cn);
            cmd.Parameters.AddWithValue("@id", tripId);
            using var da = new SqlDataAdapter(cmd); da.Fill(dt);
            _bsTripOrders.DataSource = dt; dgvTripOrders.DataSource = _bsTripOrders;
            if (dgvTrips.CurrentRow != null)
            {
                cbStatus.SelectedItem = dgvTrips.CurrentRow.Cells["Status"].Value?.ToString();
                txtNotes.Text = dgvTrips.CurrentRow.Cells["Notes"].Value?.ToString();
            }
        }

        private async Task CreateTripAsync()
        {
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand(@"INSERT INTO dbo.TransportTrip(TripDate, CreatedBy) VALUES(@d,@u); SELECT SCOPE_IDENTITY();", cn);
            cmd.Parameters.AddWithValue("@d", _date);
            cmd.Parameters.AddWithValue("@u", UserID ?? Environment.UserName);
            var idObj = await cmd.ExecuteScalarAsync();
            await LoadTripsAsync();
            if (idObj != null)
            {
                var idStr = idObj.ToString();
                foreach (DataGridViewRow r in dgvTrips.Rows)
                {
                    if (r.Cells["TripID"].Value?.ToString() == idStr) { r.Selected = true; dgvTrips.CurrentCell = r.Cells[0]; break; }
                }
            }
        }

        private async Task DeleteSelectedTripAsync()
        {
            var id = SelectedTripID; if (id == null) return;
            if (MessageBox.Show("Usun¹æ zaznaczony kurs?", "PotwierdŸ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM dbo.TransportTrip WHERE TripID=@id", cn); cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            await RefreshAllAsync();
        }

        private async Task AssignOrdersAsync()
        {
            var id = SelectedTripID; if (id == null) return;
            var selectedOrders = dgvFreeOrders.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Cells["OrderID"].Value).Where(v => v != null).Select(v => Convert.ToInt32(v)).ToList();
            if (!selectedOrders.Any()) return;
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var tx = await cn.BeginTransactionAsync();
            try
            {
                foreach (var orderId in selectedOrders)
                {
                    using var cmd = new SqlCommand(@"INSERT INTO dbo.TransportTripOrder(TripID, OrderID, CreatedBy) VALUES(@t,@o,@u);", cn, (SqlTransaction)tx);
                    cmd.Parameters.AddWithValue("@t", id);
                    cmd.Parameters.AddWithValue("@o", orderId);
                    cmd.Parameters.AddWithValue("@u", UserID ?? Environment.UserName);
                    await cmd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }
            catch { await tx.RollbackAsync(); throw; }
            await LoadFreeOrdersAsync();
            await LoadTripOrdersAsync();
        }

        private async Task UnassignOrdersAsync()
        {
            var selected = dgvTripOrders.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Cells["TripOrderID"].Value).Where(v => v != null).Select(v => Convert.ToInt64(v)).ToList();
            if (!selected.Any()) return;
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var tx = await cn.BeginTransactionAsync();
            try
            {
                foreach (var toid in selected)
                {
                    using var cmd = new SqlCommand("DELETE FROM dbo.TransportTripOrder WHERE TripOrderID=@id", cn, (SqlTransaction)tx);
                    cmd.Parameters.AddWithValue("@id", toid);
                    await cmd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }
            catch { await tx.RollbackAsync(); throw; }
            await LoadFreeOrdersAsync();
            await LoadTripOrdersAsync();
        }

        private async Task SaveNotesAsync()
        {
            var id = SelectedTripID; if (id == null) return;
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand("UPDATE dbo.TransportTrip SET Notes=@n, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@u WHERE TripID=@id", cn);
            cmd.Parameters.AddWithValue("@n", (object?)txtNotes.Text ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", UserID ?? Environment.UserName);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateTripStatusAsync()
        {
            var id = SelectedTripID; if (id == null) return;
            if (cbStatus.SelectedItem == null) return;
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand("UPDATE dbo.TransportTrip SET Status=@s, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@u WHERE TripID=@id", cn);
            cmd.Parameters.AddWithValue("@s", cbStatus.SelectedItem.ToString());
            cmd.Parameters.AddWithValue("@u", UserID ?? Environment.UserName);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task PersistTripInlineAsync()
        {
            var id = SelectedTripID; if (id == null) return; var row = dgvTrips.CurrentRow; if (row == null) return;
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand(@"UPDATE dbo.TransportTrip SET DriverGID=@d, CarID=@c, TrailerID=@t, PlannedDeparture=@pd, PickupWindowFrom=@pwf, PickupWindowTo=@pwt, CombineGroup=@cg, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@u WHERE TripID=@id", cn);
            cmd.Parameters.AddWithValue("@d", row.Cells["DriverGID"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@c", row.Cells["CarID"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@t", row.Cells["TrailerID"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pd", row.Cells["PlannedDeparture"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pwf", row.Cells["PickupWindowFrom"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pwt", row.Cells["PickupWindowTo"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@cg", row.Cells["CombineGroup"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@u", UserID ?? Environment.UserName);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task PersistTripOrderInlineAsync()
        {
            if (dgvTripOrders.CurrentRow == null) return;
            var tripOrderIdObj = dgvTripOrders.CurrentRow.Cells["TripOrderID"].Value; if (tripOrderIdObj == null) return;
            long tripOrderId = Convert.ToInt64(tripOrderIdObj);
            using var cn = new SqlConnection(_connLibra); await cn.OpenAsync();
            using var cmd = new SqlCommand(@"UPDATE dbo.TransportTripOrder SET SequenceNo=@seq, PlannedPickup=@pp, MergeNote=@mn, ModifiedAtUTC=SYSUTCDATETIME(), ModifiedBy=@u WHERE TripOrderID=@id", cn);
            cmd.Parameters.AddWithValue("@seq", dgvTripOrders.CurrentRow.Cells["SequenceNo"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pp", dgvTripOrders.CurrentRow.Cells["PlannedPickup"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@mn", dgvTripOrders.CurrentRow.Cells["MergeNote"].Value ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@u", UserID ?? Environment.UserName);
            cmd.Parameters.AddWithValue("@id", tripOrderId);
            await cmd.ExecuteNonQueryAsync();
        }

        private long? SelectedTripID => dgvTrips.CurrentRow?.Cells["TripID"]?.Value is long v ? v : (dgvTrips.CurrentRow?.Cells["TripID"]?.Value is int i ? i : (long?)null);
    }
}
