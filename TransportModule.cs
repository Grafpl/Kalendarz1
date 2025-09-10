using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Domain.Dto;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.TransportPlanner
{
    // Interfejs repozytorium (TVehicle.Kind: 3=ci¹gnik, 4=naczepa) – wy³¹cznie te dwie wartoœci s¹ u¿ywane
    public interface ITransportRepository
    {
        Task<IEnumerable<DriverDto>> GetDriversAsync(bool onlyActive = true);
        Task<int> AddDriverAsync(DriverDto dto);
        Task UpdateDriverAsync(DriverDto dto);
        Task ToggleDriverActiveAsync(int driverId, bool active);
        Task<IEnumerable<CarTrailerDto>> GetVehiclesAsync(bool onlyActive = true);   // Kind=3
        Task<IEnumerable<CarTrailerDto>> GetTrailersAsync(bool onlyActive = true);   // Kind=4
        Task UpsertVehicleAsync(CarTrailerDto dto);   // wymusza Kind=3
        Task UpsertTrailerAsync(CarTrailerDto dto);   // wymusza Kind=4
        Task ToggleCarTrailerActiveAsync(int vehicleId, bool active);
        Task<IEnumerable<LocationDto>> GetLocationsAsync(bool onlyActive = true);
        Task<int> AddLocationAsync(LocationDto dto);
        Task UpdateLocationAsync(LocationDto dto);
        Task ToggleLocationActiveAsync(int locationId, bool active);
        Task<long> AddTripAsync(TripDto dto);
        Task UpdateTripAsync(TripDto dto);
        Task DeleteTripAsync(long tripId);
        Task<IEnumerable<TripListRowDto>> GetTripsAsync(DateTime day, string? status = null, int? driverId = null, string? carId = null);
        Task<IEnumerable<TripLoadDto>> GetTripLoadsAsync(long tripId);
        Task<long> AddTripLoadAsync(TripLoadDto dto);
        Task UpdateTripLoadAsync(TripLoadDto dto);
        Task DeleteTripLoadAsync(long loadId);
        Task ReorderTripLoadsAsync(long tripId, IReadOnlyList<long> orderedLoadIds);
        Task<SettingsDto> GetSettingsAsync();
        Task UpsertSettingsAsync(SettingsDto dto);
        Task LogEventAsync(EventLogDto dto);
    }

    internal static class SqlUtil
    {
        public static SqlParameter AddP(this SqlCommand cmd, string name, object? val, SqlDbType type, int size = 0)
        {
            var p = cmd.Parameters.Add(name, type);
            if (size > 0) p.Size = size;
            p.Value = val ?? DBNull.Value;
            return p;
        }
    }

    public sealed partial class TransportRepository : ITransportRepository
    {
        private readonly string _cs;
        public TransportRepository(string cs) => _cs = cs ?? throw new ArgumentNullException(nameof(cs));

        private async Task<SqlConnection> OpenAsync()
        {
            var cn = new SqlConnection(_cs);
            await cn.OpenAsync();
            return cn;
        }

        #region Kierowcy
        public async Task<IEnumerable<DriverDto>> GetDriversAsync(bool onlyActive = true)
        {
            var list = new List<DriverDto>();
            await using var cn = await OpenAsync();
            var sql = "SELECT DriverID, FirstName, LastName, Phone, Active FROM TDriver" + (onlyActive ? " WHERE Active=1" : "") + " ORDER BY LastName, FirstName";
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new DriverDto
                {
                    DriverID = rd.GetInt32(0),
                    FirstName = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                    LastName = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                    Phone = rd.IsDBNull(3) ? null : rd.GetString(3),
                    Active = rd.GetBoolean(4)
                });
            }
            return list;
        }

        public async Task<int> AddDriverAsync(DriverDto dto)
        {
            (string first, string last) = SplitName(dto.FullName);
            const string sql = @"INSERT INTO TDriver(FirstName,LastName,Phone,Active,CreatedAtUTC) OUTPUT INSERTED.DriverID VALUES(@FN,@LN,@P,1,SYSUTCDATETIME())";
            await using var cn = await OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.AddP("@FN", first, SqlDbType.NVarChar, 60);
            cmd.AddP("@LN", last, SqlDbType.NVarChar, 80);
            cmd.AddP("@P", dto.Phone, SqlDbType.NVarChar, 40);
            var id = (int)await cmd.ExecuteScalarAsync();
            dto.DriverID = id;
            await LogEventAsync(new EventLogDto { Action = "AddDriver", Entity = "Driver", EntityId = id.ToString(), NewValue = dto.FullName });
            return id;
        }

        public async Task UpdateDriverAsync(DriverDto dto)
        {
            (string first, string last) = SplitName(dto.FullName);
            const string sql = @"UPDATE TDriver SET FirstName=@FN, LastName=@LN, Phone=@P, ModifiedAtUTC=SYSUTCDATETIME() WHERE DriverID=@ID";
            await using var cn = await OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.AddP("@FN", first, SqlDbType.NVarChar, 60);
            cmd.AddP("@LN", last, SqlDbType.NVarChar, 80);
            cmd.AddP("@P", dto.Phone, SqlDbType.NVarChar, 40);
            cmd.AddP("@ID", dto.DriverID, SqlDbType.Int);
            await cmd.ExecuteNonQueryAsync();
            await LogEventAsync(new EventLogDto { Action = "UpdateDriver", Entity = "Driver", EntityId = dto.DriverID.ToString(), NewValue = dto.FullName });
        }

        private static (string first, string last) SplitName(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return ("", "");
            var parts = full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return (parts[0], "");
            return (parts[0], string.Join(" ", parts.Skip(1)));
        }

        public async Task ToggleDriverActiveAsync(int driverId, bool active)
        {
            const string sql = "UPDATE TDriver SET Active=@A, ModifiedAtUTC=SYSUTCDATETIME() WHERE DriverID=@ID";
            await using var cn = await OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.AddP("@A", active, SqlDbType.Bit);
            cmd.AddP("@ID", driverId, SqlDbType.Int);
            await cmd.ExecuteNonQueryAsync();
            await LogEventAsync(new EventLogDto { Action = active ? "ActivateDriver" : "DeactivateDriver", Entity = "Driver", EntityId = driverId.ToString() });
        }
        #endregion

        #region Pojazdy / Naczepy (Kind: 3=ci¹gnik, 4=naczepa)
        private async Task<IEnumerable<CarTrailerDto>> GetCarTrailersAsync(int kind, bool onlyActive)
        {
            var list = new List<CarTrailerDto>();
            await using var cn = await OpenAsync();
            var sql = "SELECT VehicleID, Kind, Registration, Brand, Model, CapacityKg, PalletSlotsH1, E2Factor, Active FROM TVehicle WHERE Kind=@K" + (onlyActive ? " AND Active=1" : "") + " ORDER BY Registration";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.AddP("@K", kind, SqlDbType.Int);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new CarTrailerDto
                {
                    VehicleID = rd.GetInt32(0),
                    Kind = rd.GetInt32(1),
                    Registration = rd.GetString(2),
                    Brand = rd.IsDBNull(3) ? null : rd.GetString(3),
                    Model = rd.IsDBNull(4) ? null : rd.GetString(4),
                    CapacityKg = rd.IsDBNull(5) ? null : rd.GetDecimal(5),
                    PalletSlotsH1 = rd.IsDBNull(6) ? null : rd.GetInt32(6),
                    E2Factor = rd.IsDBNull(7) ? null : rd.GetDecimal(7),
                    Active = rd.GetBoolean(8)
                });
            }
            return list;
        }

        public Task<IEnumerable<CarTrailerDto>> GetVehiclesAsync(bool onlyActive = true) => GetCarTrailersAsync(3, onlyActive);
        public Task<IEnumerable<CarTrailerDto>> GetTrailersAsync(bool onlyActive = true) => GetCarTrailersAsync(4, onlyActive);

        private async Task UpsertVehicleInternalAsync(CarTrailerDto dto, int enforcedKind)
        {
            try
            {
                dto.Kind = enforcedKind; // zawsze 3 lub 4
                MessageBox.Show($"Debug: Próba zapisu pojazdu Kind={dto.Kind}, Reg={dto.Registration}");
                
                await using var cn = await OpenAsync();
                if (dto.VehicleID == 0)
                {
                    const string insert = @"INSERT INTO TVehicle(Registration,Kind,Brand,Model,CapacityKg,PalletSlotsH1,E2Factor,Active,CreatedAtUTC) 
                VALUES(@R,@K,@B,@M,@CK,@PS,@E2,@A,SYSUTCDATETIME()); SELECT SCOPE_IDENTITY();";
                    await using var icmd = new SqlCommand(insert, cn);
                    icmd.AddP("@R", dto.Registration, SqlDbType.NVarChar, 20);
                    icmd.AddP("@K", dto.Kind, SqlDbType.Int);
                    icmd.AddP("@B", dto.Brand, SqlDbType.NVarChar, 50);
                    icmd.AddP("@M", dto.Model, SqlDbType.NVarChar, 50);
                    var pCK = icmd.AddP("@CK", dto.CapacityKg, SqlDbType.Decimal); pCK.Precision = 10; pCK.Scale = 2;
                    icmd.AddP("@PS", dto.PalletSlotsH1, SqlDbType.Int);
                    var pE2 = icmd.AddP("@E2", dto.E2Factor, SqlDbType.Decimal); pE2.Precision = 6; pE2.Scale = 4;
                    icmd.AddP("@A", dto.Active, SqlDbType.Bit);

                    dto.VehicleID = Convert.ToInt32(await icmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                    MessageBox.Show($"Debug: Pojazd zapisany, nowe ID={dto.VehicleID}");
                }
                else
                {
                    const string update = @"UPDATE TVehicle SET Registration=@R, Kind=@K, Brand=@B, Model=@M, CapacityKg=@CK, PalletSlotsH1=@PS, E2Factor=@E2, Active=@A, ModifiedAtUTC=SYSUTCDATETIME() WHERE VehicleID=@ID";
                    await using var ucmd = new SqlCommand(update, cn);
                    ucmd.AddP("@ID", dto.VehicleID, SqlDbType.Int);
                    ucmd.AddP("@R", dto.Registration, SqlDbType.NVarChar, 20);
                    ucmd.AddP("@K", dto.Kind, SqlDbType.Int);
                    ucmd.AddP("@B", dto.Brand, SqlDbType.NVarChar, 50);
                    ucmd.AddP("@M", dto.Model, SqlDbType.NVarChar, 50);
                    var pCK = ucmd.AddP("@CK", dto.CapacityKg, SqlDbType.Decimal); pCK.Precision = 10; pCK.Scale = 2;
                    ucmd.AddP("@PS", dto.PalletSlotsH1, SqlDbType.Int);
                    var pE2 = ucmd.AddP("@E2", dto.E2Factor, SqlDbType.Decimal); pE2.Precision = 6; pE2.Scale = 4;
                    ucmd.AddP("@A", dto.Active, SqlDbType.Bit);
                    await ucmd.ExecuteNonQueryAsync();
                }
                await LogEventAsync(new EventLogDto { 
                    Action = "UpsertVehicle", 
                    Entity = "TVehicle", 
                    EntityId = dto.VehicleID.ToString(), 
                    NewValue = $"{dto.Registration} (Kind={dto.Kind})" 
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"B³¹d zapisu pojazdu: {ex.Message}\n\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public Task UpsertVehicleAsync(CarTrailerDto dto) => UpsertVehicleInternalAsync(dto, 3);
        public Task UpsertTrailerAsync(CarTrailerDto dto) => UpsertVehicleInternalAsync(dto, 4);
        public async Task ToggleCarTrailerActiveAsync(int vehicleId, bool active)
        {
            const string sql = "UPDATE TVehicle SET Active=@A, ModifiedAtUTC=SYSUTCDATETIME() WHERE VehicleID=@ID";
            await using var cn = await OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.AddP("@A", active, SqlDbType.Bit);
            cmd.AddP("@ID", vehicleId, SqlDbType.Int);
            await cmd.ExecuteNonQueryAsync();
            await LogEventAsync(new EventLogDto { Action = active ? "ActivateVehicle" : "DeactivateVehicle", Entity = "TVehicle", EntityId = vehicleId.ToString(), NewValue = active ? "1" : "0" });
        }
        #endregion

        #region Stuby
        public Task<int> AddLocationAsync(LocationDto dto) => Task.FromResult(0);
        public Task UpdateLocationAsync(LocationDto dto) => Task.CompletedTask;
        public Task ToggleLocationActiveAsync(int locationId, bool active) => Task.CompletedTask;
        public Task<IEnumerable<LocationDto>> GetLocationsAsync(bool onlyActive = true) => Task.FromResult<IEnumerable<LocationDto>>(Array.Empty<LocationDto>());
        public Task<long> AddTripAsync(TripDto dto) => Task.FromResult(0L);
        public Task UpdateTripAsync(TripDto dto) => Task.CompletedTask;
        public Task DeleteTripAsync(long tripId) => Task.CompletedTask;
        public Task<IEnumerable<TripListRowDto>> GetTripsAsync(DateTime day, string? status = null, int? driverId = null, string? carId = null) => Task.FromResult<IEnumerable<TripListRowDto>>(Array.Empty<TripListRowDto>());
        public Task<IEnumerable<TripLoadDto>> GetTripLoadsAsync(long tripId) => Task.FromResult<IEnumerable<TripLoadDto>>(Array.Empty<TripLoadDto>());
        public Task<long> AddTripLoadAsync(TripLoadDto dto) => Task.FromResult(0L);
        public Task UpdateTripLoadAsync(TripLoadDto dto) => Task.CompletedTask;
        public Task DeleteTripLoadAsync(long loadId) => Task.CompletedTask;
        public Task ReorderTripLoadsAsync(long tripId, IReadOnlyList<long> orderedLoadIds) => Task.CompletedTask;
        public Task<SettingsDto> GetSettingsAsync() => Task.FromResult(new SettingsDto());
        public Task UpsertSettingsAsync(SettingsDto dto) => Task.CompletedTask;
        #endregion

        #region Logowanie
        public async Task LogEventAsync(EventLogDto dto)
        {
            try
            {
                const string sql = "INSERT INTO TEventLog(At,Usr,Action,Entity,EntityId,OldValue,NewValue,Notes) VALUES(@At,@U,@A,@E,@EID,@OV,@NV,@N)";
                await using var cn = await OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.AddP("@At", dto.At, SqlDbType.DateTime2);
                cmd.AddP("@U", dto.User, SqlDbType.NVarChar, 64);
                cmd.AddP("@A", dto.Action, SqlDbType.NVarChar, 64);
                cmd.AddP("@E", dto.Entity, SqlDbType.NVarChar, 64);
                cmd.AddP("@EID", dto.EntityId, SqlDbType.NVarChar, 64);
                cmd.AddP("@OV", dto.OldValue, SqlDbType.NVarChar, -1);
                cmd.AddP("@NV", dto.NewValue, SqlDbType.NVarChar, -1);
                cmd.AddP("@N", dto.Notes, SqlDbType.NVarChar, -1);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }
        #endregion
    }

    #region Formularz g³ówny
    public sealed class TransportMainForm : Form
    {
        private readonly ITransportRepository _repo;
        private readonly BindingSource _bs = new();
        private readonly DateTimePicker _dt = new() { Dock = DockStyle.Left, Width = 140 };
        private readonly ComboBox _cbStatus = new() { Dock = DockStyle.Left, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button _btnRefresh = new() { Text = "Odœwie¿", Dock = DockStyle.Left, Width = 80 };
        private readonly Button _btnDrivers = new() { Text = "Kierowcy", Dock = DockStyle.Left, Width = 90 };
        private readonly Button _btnVehicles = new() { Text = "Pojazdy", Dock = DockStyle.Left, Width = 90 };
        private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };

        public TransportMainForm(ITransportRepository repo)
        {
            _repo = repo;
            Text = "Transport – Planner"; Width = 1150; Height = 650;
            BuildUi();
            Load += async (_, __) => await RefreshDataAsync();
            KeyPreview = true;
            KeyDown += async (_, e) => { if (e.KeyCode == Keys.F5) { await RefreshDataAsync(); e.Handled = true; } };
        }

        private void BuildUi()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            _cbStatus.Items.AddRange(new object[] { "(Wszystkie)", "Planned", "InProgress", "Completed", "Canceled" });
            _cbStatus.SelectedIndex = 0;
            _btnRefresh.Click += async (_, __) => await RefreshDataAsync();
            _btnDrivers.Click += (_, __) => { using var f = new DriversForm(_repo); f.ShowDialog(this); };
            _btnVehicles.Click += (_, __) => { using var f = new VehiclesForm(_repo); f.ShowDialog(this); };
            top.Controls.AddRange(new Control[] { _btnVehicles, _btnDrivers, _btnRefresh, _cbStatus, _dt });
            Controls.AddRange(new Control[] { _grid, top });
            InitGrid();
        }

        private void InitGrid()
        {
            _grid.DataSource = _bs;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.TripDate), HeaderText = "Data" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.PlannedDeparture), HeaderText = "Wyjazd" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.DriverName), HeaderText = "Kierowca" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.CarName), HeaderText = "Auto" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.TrailerName), HeaderText = "Naczepa" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.FillPercent), HeaderText = "%" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TripListRowDto.Status), HeaderText = "Status" });
            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(TripListRowDto.Status))
                {
                    var st = _grid.Rows[e.RowIndex].Cells[nameof(TripListRowDto.Status)].Value?.ToString();
                    var row = _grid.Rows[e.RowIndex];
                    row.DefaultCellStyle.BackColor = st switch
                    {
                        "Canceled" => System.Drawing.Color.LightGray,
                        "Completed" => System.Drawing.Color.LightGreen,
                        "InProgress" => System.Drawing.Color.Khaki,
                        _ => System.Drawing.Color.White
                    };
                }
            };
        }

        private async Task RefreshDataAsync()
        {
            string? status = _cbStatus.SelectedIndex <= 0 ? null : _cbStatus.SelectedItem?.ToString();
            var data = await _repo.GetTripsAsync(_dt.Value.Date, status);
            _bs.DataSource = data.ToList();
        }
    }
    #endregion

    #region Formularz: Kierowcy
    internal sealed class DriversForm : Form
    {
        private readonly ITransportRepository _repo;
        private readonly BindingSource _bs = new();
        private readonly CheckBox _chkActive = new() { Text = "Tylko aktywni", Dock = DockStyle.Top, Checked = true };
        private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly Button _btnAdd = new() { Text = "Dodaj" };
        private readonly Button _btnEdit = new() { Text = "Edytuj" };
        private readonly Button _btnToggle = new() { Text = "Aktywuj/Dezaktywuj" };
        private readonly Button _btnClose = new() { Text = "Zamknij" };
        private readonly TextBox _tbSearch = new() { PlaceholderText = "Szukaj kierowcy", Dock = DockStyle.Top };
        private List<DriverDto> _all = new();

        public DriversForm(ITransportRepository repo)
        {
            _repo = repo; Text = "Kierowcy"; Width = 640; Height = 420; StartPosition = FormStartPosition.CenterParent;
            Build();
            Load += async (_, __) => await LoadDataAsync();
        }

        private void Build()
        {
            var panelButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
            panelButtons.Controls.AddRange(new Control[] { _btnClose, _btnToggle, _btnEdit, _btnAdd });
            _btnClose.Click += (_, __) => Close();
            _btnAdd.Click += async (_, __) => { if (DriverEditDialog.ShowDialog(this, null, out var dto)) { await _repo.AddDriverAsync(dto); await LoadDataAsync(); } };
            _btnEdit.Click += async (_, __) => { var d = Current(); if (d == null) return; if (DriverEditDialog.ShowDialog(this, d, out var edited)) { edited.DriverID = d.DriverID; await _repo.UpdateDriverAsync(edited); await LoadDataAsync(); } };
            _btnToggle.Click += async (_, __) => { var d = Current(); if (d == null) return; await _repo.ToggleDriverActiveAsync(d.DriverID, !d.Active); await LoadDataAsync(); };
            _chkActive.CheckedChanged += async (_, __) => await LoadDataAsync();
            _tbSearch.TextChanged += (_, __) => ApplyDriverFilter();
            _grid.DataSource = _bs;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriverDto.FullName), HeaderText = "Imiê i nazwisko" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriverDto.Phone), HeaderText = "Telefon" });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(DriverDto.Active), HeaderText = "Aktywny", Width = 60 });
            Controls.AddRange(new Control[] { _grid, _tbSearch, _chkActive, panelButtons });
            EnhanceDriversUi();
        }

        private DriverDto? Current() => _grid.CurrentRow?.DataBoundItem as DriverDto;

        private async Task LoadDataAsync()
        {
            _all = (await _repo.GetDriversAsync(_chkActive.Checked)).ToList();
            ApplyDriverFilter();
        }

        private void EnhanceDriversUi()
        {
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DoubleClick += async (_, __) => { var d = Current(); if (d == null) return; if (DriverEditDialog.ShowDialog(this, d, out var e2)) { e2.DriverID = d.DriverID; await _repo.UpdateDriverAsync(e2); await LoadDataAsync(); } };
            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Rows[e.RowIndex].DataBoundItem is DriverDto dto && !dto.Active)
                    _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = System.Drawing.Color.Gray;
            };
        }

        private void ApplyDriverFilter()
        {
            var t = _tbSearch.Text.Trim().ToLowerInvariant();
            IEnumerable<DriverDto> data = _all;
            if (t.Length > 1)
                data = data.Where(d => d.FullName.ToLowerInvariant().Contains(t));
            _bs.DataSource = data.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ToList();
        }
    }
    #endregion

    #region Formularz: Pojazdy / Naczepy
    internal sealed partial class VehiclesForm : Form
    {
        private readonly ITransportRepository _repo;
        private readonly BindingSource _bs = new();
        private readonly ComboBox _cbKind = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _chkActive = new() { Text = "Tylko aktywne", Dock = DockStyle.Top, Checked = true };
        private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly Button _btnAdd = new() { Text = "Dodaj" };
        private readonly Button _btnEdit = new() { Text = "Edytuj" };
        private readonly Button _btnToggle = new() { Text = "Aktywuj/Dezaktywuj" };
        private readonly Button _btnClose = new() { Text = "Zamknij" };
        private readonly TextBox _tbSearch = new() { PlaceholderText = "Szukaj (rej / marka / model)", Dock = DockStyle.Top };
        private List<CarTrailerDto> _all = new();

        public VehiclesForm(ITransportRepository repo)
        {
            _repo = repo; Text = "Pojazdy / Naczepy"; Width = 800; Height = 480; StartPosition = FormStartPosition.CenterParent;
            Build();
            Load += async (_, __) => await LoadDataAsync();
        }

        private void Build()
        {
            _cbKind.Items.AddRange(new object[] { "Ci¹gniki", "Naczepy" }); _cbKind.SelectedIndex = 0;
            _cbKind.SelectedIndexChanged += async (_, __) => await LoadDataAsync();
            _chkActive.CheckedChanged += async (_, __) => await LoadDataAsync();

            var panelButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
            panelButtons.Controls.AddRange(new Control[] { _btnClose, _btnToggle, _btnEdit, _btnAdd });
            _btnClose.Click += (_, __) => Close();
            _btnAdd.Click += async (_, __) => { if (VehicleEditDialog.ShowDialog(this, null, CurrentKind(), out var dto)) { await SaveAsync(dto); } };
            _btnEdit.Click += async (_, __) => { var v = Current(); if (v == null) return; if (VehicleEditDialog.ShowDialog(this, v, v.Kind, out var dto)) { dto.VehicleID = v.VehicleID; await SaveAsync(dto); } };
            _btnToggle.Click += async (_, __) => { var v = Current(); if (v == null) return; await _repo.ToggleCarTrailerActiveAsync(v.VehicleID, !v.Active); await LoadDataAsync(); };

            _tbSearch.TextChanged += (_, __) => ApplyFilter();

            _grid.DataSource = _bs;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.VehicleID), HeaderText = "ID", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.Registration), HeaderText = "Rej", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.Brand), HeaderText = "Marka", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.Model), HeaderText = "Model", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.PalletSlotsH1), HeaderText = "Palety" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.CapacityKg), HeaderText = "Kg" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CarTrailerDto.E2Factor), HeaderText = "E2" });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(CarTrailerDto.Active), HeaderText = "Akt" });

            Controls.AddRange(new Control[] { _grid, _tbSearch, _chkActive, _cbKind, panelButtons });
            EnhanceGridUi();
        }

        private int CurrentKind() => _cbKind.SelectedIndex == 0 ? 3 : 4;
        private CarTrailerDto? Current() => _grid.CurrentRow?.DataBoundItem as CarTrailerDto;

        private async Task SaveAsync(CarTrailerDto dto)
        {
            if (dto.Kind != 3 && dto.Kind != 4) dto.Kind = CurrentKind();
            if (dto.Kind == 3) await _repo.UpsertVehicleAsync(dto); else await _repo.UpsertTrailerAsync(dto);
            await LoadDataAsync();
        }

        private void EnhanceGridUi()
        {
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.RowHeadersVisible = true;
            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Rows[e.RowIndex].DataBoundItem is CarTrailerDto dto)
                {
                    if (!dto.Active)
                    {
                        _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = System.Drawing.Color.Gray;
                        _grid.Rows[e.RowIndex].DefaultCellStyle.Font = new System.Drawing.Font(_grid.Font, System.Drawing.FontStyle.Italic);
                    }
                    if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(CarTrailerDto.CapacityKg) && e.Value is decimal dec)
                    {
                        e.Value = dec.ToString("#,0", CultureInfo.InvariantCulture);
                        e.FormattingApplied = true;
                    }
                }
            };
            _grid.RowPostPaint += (_, e) =>
            {
                var idx = (e.RowIndex + 1).ToString();
                var rect = new System.Drawing.Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
                TextRenderer.DrawText(e.Graphics, idx, _grid.Font, rect, System.Drawing.Color.DimGray, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            };
            _grid.DoubleClick += async (_, __) => { var v = Current(); if (v == null) return; if (VehicleEditDialog.ShowDialog(this, v, v.Kind, out var dto)) { dto.VehicleID = v.VehicleID; await SaveAsync(dto); } };
        }

        private async Task LoadDataAsync()
        {
            IEnumerable<CarTrailerDto> data = CurrentKind() == 3
                ? await _repo.GetVehiclesAsync(_chkActive.Checked)
                : await _repo.GetTrailersAsync(_chkActive.Checked);
            _all = data.ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var term = _tbSearch.Text.Trim().ToLowerInvariant();
            IEnumerable<CarTrailerDto> data = _all;
            if (term.Length > 1)
            {
                data = data.Where(v =>
                    (v.Registration?.ToLowerInvariant().Contains(term) ?? false) ||
                    (v.Brand?.ToLowerInvariant().Contains(term) ?? false) ||
                    (v.Model?.ToLowerInvariant().Contains(term) ?? false));
            }
            _bs.DataSource = data.OrderBy(v => v.Registration).ToList();
        }
    }
    #endregion

    #region Dialogi
    internal static class DriverEditDialog
    {
        public static bool ShowDialog(IWin32Window owner, DriverDto? existing, out DriverDto result)
        {
            result = new DriverDto();
            var f = new Form { Text = existing == null ? "Nowy kierowca" : "Edycja kierowcy", Width = 400, Height = 220, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var tbName = new TextBox { Left = 20, Top = 30, Width = 340 };
            var tbPhone = new TextBox { Left = 20, Top = 90, Width = 200 };
            f.Controls.AddRange(new Control[] { new Label { Left = 20, Top = 10, Text = "Imiê i nazwisko:" }, tbName, new Label { Left = 20, Top = 70, Text = "Telefon:" }, tbPhone });
            var btnOk = new Button { Text = "Zapisz", Left = 200, Width = 80, Top = 130, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Anuluj", Left = 280, Width = 80, Top = 130, DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { btnOk, btnCancel });
            f.AcceptButton = btnOk; f.CancelButton = btnCancel;
            if (existing != null)
            {
                tbName.Text = existing.FullName;
                tbPhone.Text = existing.Phone;
            }
            if (f.ShowDialog(owner) == DialogResult.OK)
            {
                if (string.IsNullOrWhiteSpace(tbName.Text)) { MessageBox.Show("Nazwa jest wymagana."); return ShowDialog(owner, existing, out result); }
                var parts = tbName.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                result.FirstName = parts.Length > 0 ? parts[0] : string.Empty;
                result.LastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
                result.Phone = string.IsNullOrWhiteSpace(tbPhone.Text) ? null : tbPhone.Text.Trim();
                result.Active = existing?.Active ?? true;
                result.DriverID = existing?.DriverID ?? 0;
                return true;
            }
            return false;
        }
    }

    internal static class VehicleEditDialog
    {
        public static bool ShowDialog(IWin32Window owner, CarTrailerDto? existing, int enforcedKind, out CarTrailerDto dto)
        {
            dto = new CarTrailerDto { Kind = enforcedKind, Active = true };
            string titleKind = enforcedKind == 3 ? "ci¹gnik" : "naczepa";
            var f = new Form { Text = existing == null ? $"Nowy {titleKind}" : $"Edycja {titleKind}", Width = 520, Height = 300, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var tbId = new TextBox { Left = 20, Top = 30, Width = 80, Enabled = existing == null };
            var tbReg = new TextBox { Left = 120, Top = 30, Width = 120 };
            var tbBrand = new TextBox { Left = 260, Top = 30, Width = 100 };
            var tbModel = new TextBox { Left = 370, Top = 30, Width = 110 };
            var tbCap = new TextBox { Left = 20, Top = 100, Width = 100 };
            var tbPal = new TextBox { Left = 140, Top = 100, Width = 60 };
            var tbE2 = new TextBox { Left = 220, Top = 100, Width = 60 };
            var chk = new CheckBox { Left = 300, Top = 102, Text = "Aktywny", Checked = true };
            f.Controls.AddRange(new Control[] {
                new Label{Left=20,Top=10,Text="VehicleID"},tbId,
                new Label{Left=120,Top=10,Text="Rej"},tbReg,
                new Label{Left=260,Top=10,Text="Marka"},tbBrand,
                new Label{Left=370,Top=10,Text="Model"},tbModel,
                new Label{Left=20,Top=80,Text="Kg"},tbCap,
                new Label{Left=140,Top=80,Text="Palety"},tbPal,
                new Label{Left=220,Top=80,Text="E2"},tbE2,chk });
            var btnOk = new Button { Text = "Zapisz", Left = 300, Top = 200, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Anuluj", Left = 390, Top = 200, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { btnOk, btnCancel });
            f.AcceptButton = btnOk; f.CancelButton = btnCancel;
            if (existing != null)
            {
                tbId.Text = existing.VehicleID.ToString();
                tbReg.Text = existing.Registration;
                tbBrand.Text = existing.Brand;
                tbModel.Text = existing.Model;
                tbCap.Text = existing.CapacityKg?.ToString(CultureInfo.InvariantCulture);
                tbPal.Text = existing.PalletSlotsH1?.ToString();
                tbE2.Text = existing.E2Factor?.ToString(CultureInfo.InvariantCulture);
                chk.Checked = existing.Active;
            }
            if (f.ShowDialog(owner) == DialogResult.OK)
            {
                if (string.IsNullOrWhiteSpace(tbReg.Text)) { MessageBox.Show("Rejestracja wymagana"); return ShowDialog(owner, existing, enforcedKind, out dto); }
                dto.VehicleID = existing?.VehicleID ?? 0; // przy nowym zawsze 0
                dto.Registration = tbReg.Text.Trim();
                dto.Brand = string.IsNullOrWhiteSpace(tbBrand.Text) ? null : tbBrand.Text.Trim();
                dto.Model = string.IsNullOrWhiteSpace(tbModel.Text) ? null : tbModel.Text.Trim();
                dto.CapacityKg = decimal.TryParse(tbCap.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var cap) ? cap : null;
                dto.PalletSlotsH1 = int.TryParse(tbPal.Text, out var pal) ? pal : null;
                dto.E2Factor = decimal.TryParse(tbE2.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var e2) ? e2 : null;
                dto.Active = chk.Checked;
                dto.Kind = enforcedKind; // 3 lub 4
                return true;
            }
            return false;
        }
    }
    #endregion

    internal static class VehicleKindHelper
    {
        public static bool IsTractor(int kind) => kind == 3; // uproszczone po migracji
    }
}
