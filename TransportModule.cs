using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
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
        private readonly CheckBox _chkActive;
        private readonly DataGridView _grid;
        private readonly Button _btnAdd;
        private readonly Button _btnEdit;
        private readonly Button _btnToggle;
        private readonly Button _btnClose;
        private readonly TextBox _tbSearch;
        private List<DriverDto> _all = new();

        public DriversForm(ITransportRepository repo)
        {
            _repo = repo;
            Text = "Kartoteka kierowców";
            Width = 800;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            
            // Panel górny z kontrolkami
            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 2
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Pierwsza kolumna - filtrowanie
            var filterGroup = new GroupBox { Text = "Filtrowanie", Dock = DockStyle.Fill };
            var filterLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            
            _chkActive = new CheckBox { 
                Text = "Poka¿ tylko aktywnych kierowców",
                Checked = true,
                AutoSize = true
            };
            
            filterLayout.Controls.Add(_chkActive);
            filterGroup.Controls.Add(filterLayout);
            topPanel.Controls.Add(filterGroup, 0, 0);

            // Druga kolumna - wyszukiwanie
            var searchGroup = new GroupBox { Text = "Wyszukiwanie", Dock = DockStyle.Fill };
            _tbSearch = new TextBox { 
                Dock = DockStyle.Fill,
                PlaceholderText = "Wpisz imiê, nazwisko lub numer telefonu...",
                Margin = new Padding(5)
            };
            searchGroup.Controls.Add(_tbSearch);
            topPanel.Controls.Add(searchGroup, 1, 0);

            // Panel przycisków
            var buttonPanel = new FlowLayoutPanel { 
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(10, 0, 10, 10),
                FlowDirection = FlowDirection.RightToLeft
            };

            _btnClose = new Button { Text = "Zamknij", Width = 80 };
            _btnToggle = new Button { Text = "Aktywuj/Dezaktywuj", Width = 130 };
            _btnEdit = new Button { Text = "Edytuj", Width = 80 };
            _btnAdd = new Button { Text = "Dodaj nowego", Width = 100 };

            buttonPanel.Controls.AddRange(new Control[] { _btnClose, _btnToggle, _btnEdit, _btnAdd });

            // Grid
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _grid.Columns.AddRange(new DataGridViewColumn[] {
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(DriverDto.FullName),
                    HeaderText = "Imiê i nazwisko",
                    Width = 250
                },
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(DriverDto.Phone),
                    HeaderText = "Telefon",
                    Width = 120
                },
                new DataGridViewCheckBoxColumn { 
                    DataPropertyName = nameof(DriverDto.Active),
                    HeaderText = "Aktywny",
                    Width = 70
                }
            });

            // Dodanie kontrolek do formularza
            Controls.AddRange(new Control[] { _grid, buttonPanel, topPanel });

            // Podpiêcie zdarzeñ
            Load += async (_, __) => await LoadDataAsync();
            _chkActive.CheckedChanged += async (_, __) => await LoadDataAsync();
            _tbSearch.TextChanged += (_, __) => ApplyDriverFilter();
            _btnClose.Click += (_, __) => Close();
            
            _btnAdd.Click += async (_, __) => {
                if (DriverEditDialog.ShowDialog(this, null, out var dto))
                {
                    try
                    {
                        Cursor = Cursors.WaitCursor;
                        await _repo.AddDriverAsync(dto);
                        await LoadDataAsync();
                    }
                    finally
                    {
                        Cursor = Cursors.Default;
                    }
                }
            };

            _btnEdit.Click += async (_, __) => {
                var d = Current();
                if (d == null) return;
                if (DriverEditDialog.ShowDialog(this, d, out var edited))
                {
                    try
                    {
                        Cursor = Cursors.WaitCursor;
                        edited.DriverID = d.DriverID;
                        await _repo.UpdateDriverAsync(edited);
                        await LoadDataAsync();
                    }
                    finally
                    {
                        Cursor = Cursors.Default;
                    }
                }
            };

            _btnToggle.Click += async (_, __) => {
                var d = Current();
                if (d == null) return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    await _repo.ToggleDriverActiveAsync(d.DriverID, !d.Active);
                    await LoadDataAsync();
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            };

            EnhanceDriversUi();
        }

        private DriverDto? Current() => _grid.CurrentRow?.DataBoundItem as DriverDto;

        private async Task LoadDataAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _all = (await _repo.GetDriversAsync(_chkActive.Checked)).ToList();
                ApplyDriverFilter();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void EnhanceDriversUi()
        {
            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Rows[e.RowIndex].DataBoundItem is DriverDto dto)
                {
                    if (!dto.Active)
                    {
                        _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Gray;
                        _grid.Rows[e.RowIndex].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Italic);
                    }
                }
            };

            _grid.RowPostPaint += (_, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var idx = (e.RowIndex + 1).ToString();
                    var rect = new Rectangle(
                        e.RowBounds.Left,
                        e.RowBounds.Top,
                        _grid.RowHeadersWidth - 4,
                        e.RowBounds.Height
                    );
                    TextRenderer.DrawText(
                        e.Graphics,
                        idx,
                        _grid.Font,
                        rect,
                        Color.Gray,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Right
                    );
                }
            };

            _grid.DoubleClick += async (_, __) => {
                var d = Current();
                if (d == null) return;
                if (DriverEditDialog.ShowDialog(this, d, out var edited))
                {
                    try
                    {
                        Cursor = Cursors.WaitCursor;
                        edited.DriverID = d.DriverID;
                        await _repo.UpdateDriverAsync(edited);
                        await LoadDataAsync();
                    }
                    finally
                    {
                        Cursor = Cursors.Default;
                    }
                }
            };
        }

        private void ApplyDriverFilter()
        {
            var term = _tbSearch.Text.Trim().ToLowerInvariant();
            IEnumerable<DriverDto> data = _all;
            
            if (term.Length > 1)
            {
                data = data.Where(d =>
                    d.FullName.ToLowerInvariant().Contains(term) ||
                    (d.Phone?.ToLowerInvariant().Contains(term) ?? false));
            }
            
            _bs.DataSource = data
                .OrderBy(d => d.LastName)
                .ThenBy(d => d.FirstName)
                .ToList();
            
            _grid.DataSource = _bs;
        }
    }
    #endregion

    #region Formularz: Pojazdy / Naczepy
    internal sealed partial class VehiclesForm : Form
    {
        private readonly ITransportRepository _repo;
        private readonly BindingSource _bs = new();
        private readonly ComboBox _cbKind;
        private readonly CheckBox _chkActive;
        private readonly DataGridView _grid;
        private readonly Button _btnAdd;
        private readonly Button _btnEdit;
        private readonly Button _btnToggle;
        private readonly Button _btnClose;
        private readonly TextBox _tbSearch;
        private List<CarTrailerDto> _all = new();

        public VehiclesForm(ITransportRepository repo)
        {
            _repo = repo;
            Text = "Zarz¹dzanie flot¹";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            
            // Panel górny z kontrolkami
            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 2
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Pierwsza kolumna - filtrowanie
            var filterGroup = new GroupBox { Text = "Filtrowanie", Dock = DockStyle.Fill };
            var filterLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            
            _cbKind = new ComboBox { 
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Items = { "Ci¹gniki", "Naczepy" }
            };
            _cbKind.SelectedIndex = 0;
            
            _chkActive = new CheckBox { 
                Text = "Tylko aktywne",
                Checked = true,
                AutoSize = true
            };
            
            filterLayout.Controls.AddRange(new Control[] { 
                new Label { Text = "Typ:", AutoSize = true, Padding = new Padding(0, 3, 5, 0) },
                _cbKind,
                new Label { Width = 20 },
                _chkActive
            });
            filterGroup.Controls.Add(filterLayout);
            topPanel.Controls.Add(filterGroup, 0, 0);

            // Druga kolumna - wyszukiwanie
            var searchGroup = new GroupBox { Text = "Wyszukiwanie", Dock = DockStyle.Fill };
            _tbSearch = new TextBox { 
                Dock = DockStyle.Fill,
                PlaceholderText = "Wpisz numer rejestracyjny, markê lub model...",
                Margin = new Padding(5)
            };
            searchGroup.Controls.Add(_tbSearch);
            topPanel.Controls.Add(searchGroup, 1, 0);

            // Panel przycisków
            var buttonPanel = new FlowLayoutPanel { 
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(10, 0, 10, 10),
                FlowDirection = FlowDirection.RightToLeft
            };

            _btnClose = new Button { Text = "Zamknij", Width = 80 };
            _btnToggle = new Button { Text = "Aktywuj/Dezaktywuj", Width = 130 };
            _btnEdit = new Button { Text = "Edytuj", Width = 80 };
            _btnAdd = new Button { Text = "Dodaj nowy", Width = 90 };

            buttonPanel.Controls.AddRange(new Control[] { _btnClose, _btnToggle, _btnEdit, _btnAdd });

            // Grid
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _grid.Columns.AddRange(new DataGridViewColumn[] {
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.Registration),
                    HeaderText = "Nr rejestracyjny",
                    Width = 100
                },
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.Brand),
                    HeaderText = "Marka",
                    Width = 120
                },
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.Model),
                    HeaderText = "Model",
                    Width = 120
                },
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.PalletSlotsH1),
                    HeaderText = "Iloœæ palet",
                    Width = 80
                },
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.CapacityKg),
                    HeaderText = "£adownoœæ [kg]",
                    Width = 100
                },
                new DataGridViewTextBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.E2Factor),
                    HeaderText = "Wsp. E2",
                    Width = 80
                },
                new DataGridViewCheckBoxColumn { 
                    DataPropertyName = nameof(CarTrailerDto.Active),
                    HeaderText = "Aktywny",
                    Width = 70
                }
            });

            // Dodanie kontrolek do formularza
            Controls.AddRange(new Control[] { _grid, buttonPanel, topPanel });

            // Podpiêcie zdarzeñ
            Load += async (_, __) => await LoadDataAsync();
            _cbKind.SelectedIndexChanged += async (_, __) => await LoadDataAsync();
            _chkActive.CheckedChanged += async (_, __) => await LoadDataAsync();
            _tbSearch.TextChanged += (_, __) => ApplyFilter();
            _btnClose.Click += (_, __) => Close();
            _btnAdd.Click += async (_, __) => { 
                if (VehicleEditDialog.ShowDialog(this, null, CurrentKind(), out var dto)) 
                { 
                    await SaveAsync(dto); 
                } 
            };
            _btnEdit.Click += async (_, __) => { 
                var v = Current(); 
                if (v == null) return;
                if (VehicleEditDialog.ShowDialog(this, v, v.Kind, out var dto)) 
                { 
                    dto.VehicleID = v.VehicleID; 
                    await SaveAsync(dto); 
                } 
            };
            _btnToggle.Click += async (_, __) => { 
                var v = Current(); 
                if (v == null) return; 
                await _repo.ToggleCarTrailerActiveAsync(v.VehicleID, !v.Active); 
                await LoadDataAsync(); 
            };

            EnhanceGridUi();
        }

        // ...istniej¹ce metody pomocnicze...

        private void EnhanceGridUi()
        {
            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Rows[e.RowIndex].DataBoundItem is CarTrailerDto dto)
                {
                    if (!dto.Active)
                    {
                        _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Gray;
                        _grid.Rows[e.RowIndex].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Italic);
                    }
                    
                    // Formatowanie liczb
                    if (e.ColumnIndex >= 0)
                    {
                        var column = _grid.Columns[e.ColumnIndex];
                        if (column.DataPropertyName == nameof(CarTrailerDto.CapacityKg) && e.Value is decimal dec)
                        {
                            e.Value = dec.ToString("#,##0", CultureInfo.InvariantCulture);
                            e.FormattingApplied = true;
                        }
                        else if (column.DataPropertyName == nameof(CarTrailerDto.E2Factor) && e.Value is decimal e2)
                        {
                            e.Value = e2.ToString("0.0000", CultureInfo.InvariantCulture);
                            e.FormattingApplied = true;
                        }
                    }
                }
            };

            _grid.RowPostPaint += (_, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var idx = (e.RowIndex + 1).ToString();
                    var rect = new Rectangle(
                        e.RowBounds.Left, 
                        e.RowBounds.Top,
                        _grid.RowHeadersWidth - 4,
                        e.RowBounds.Height
                    );
                    TextRenderer.DrawText(
                        e.Graphics,
                        idx,
                        _grid.Font,
                        rect,
                        Color.Gray,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Right
                    );
                }
            };

            _grid.DoubleClick += async (_, __) => {
                var v = Current();
                if (v == null) return;
                if (VehicleEditDialog.ShowDialog(this, v, v.Kind, out var dto))
                {
                    dto.VehicleID = v.VehicleID;
                    await SaveAsync(dto);
                }
            };
        }

        private int CurrentKind() => _cbKind.SelectedIndex == 0 ? 3 : 4;
        private CarTrailerDto? Current() => _grid.CurrentRow?.DataBoundItem as CarTrailerDto;

        private async Task LoadDataAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                IEnumerable<CarTrailerDto> data = CurrentKind() == 3
                    ? await _repo.GetVehiclesAsync(_chkActive.Checked)
                    : await _repo.GetTrailersAsync(_chkActive.Checked);
                _all = data.ToList();
                ApplyFilter();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
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
            _grid.DataSource = _bs;
        }

        private async Task SaveAsync(CarTrailerDto dto)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                if (dto.Kind != 3 && dto.Kind != 4) dto.Kind = CurrentKind();
                if (dto.Kind == 3)
                    await _repo.UpsertVehicleAsync(dto);
                else
                    await _repo.UpsertTrailerAsync(dto);
                await LoadDataAsync();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
    #endregion

    #region Dialog edycji kierowcy
    internal static class DriverEditDialog
    {
        public static bool ShowDialog(IWin32Window owner, DriverDto? existing, out DriverDto result)
        {
            result = new DriverDto();
            
            var f = new Form
            {
                Text = existing == null ? "Nowy kierowca" : "Edycja kierowcy",
                Width = 420,
                Height = 260,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Padding = new Padding(20)
            };

            // Panel danych osobowych
            var personalGroup = new GroupBox
            {
                Text = "Dane osobowe",
                Dock = DockStyle.Top,
                Height = 120,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Absolute, 100),
                    new ColumnStyle(SizeType.Percent, 100)
                }
            };

            var tbName = new TextBox { Width = 250, Dock = DockStyle.Fill };
            var tbPhone = new TextBox { Width = 150, Dock = DockStyle.Fill };

            layout.Controls.Add(new Label { Text = "Imiê i nazwisko:", Dock = DockStyle.Left }, 0, 0);
            layout.Controls.Add(tbName, 1, 0);
            layout.Controls.Add(new Label { Text = "Telefon:", Dock = DockStyle.Left }, 0, 1);
            layout.Controls.Add(tbPhone, 1, 1);

            personalGroup.Controls.Add(layout);

            // Status
            var chkActive = new CheckBox { Text = "Aktywny", Checked = true, Left = 20, Top = 140 };

            // Panel przycisków
            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 40
            };

            var btnOk = new Button { Text = "Zapisz", Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Anuluj", Width = 80, DialogResult = DialogResult.Cancel };
            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnOk });

            f.Controls.AddRange(new Control[] { personalGroup, chkActive, btnPanel });
            f.AcceptButton = btnOk;
            f.CancelButton = btnCancel;

            if (existing != null)
            {
                tbName.Text = existing.FullName;
                tbPhone.Text = existing.Phone;
                chkActive.Checked = existing.Active;
            }

            if (f.ShowDialog(owner) == DialogResult.OK)
            {
                if (string.IsNullOrWhiteSpace(tbName.Text))
                {
                    MessageBox.Show("Imiê i nazwisko s¹ wymagane", "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return ShowDialog(owner, existing, out result);
                }

                var parts = tbName.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                result.FirstName = parts.Length > 0 ? parts[0] : string.Empty;
                result.LastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
                result.Phone = string.IsNullOrWhiteSpace(tbPhone.Text) ? null : tbPhone.Text.Trim();
                result.Active = chkActive.Checked;
                result.DriverID = existing?.DriverID ?? 0;
                return true;
            }

            return false;
        }
    }
    #endregion

    #region Dialog edycji pojazdu
    internal static class VehicleEditDialog
    {
        public static bool ShowDialog(IWin32Window owner, CarTrailerDto? existing, int enforcedKind, out CarTrailerDto dto)
        {
            dto = new CarTrailerDto { Kind = enforcedKind, Active = true };
            string titleKind = enforcedKind == 3 ? "ci¹gnik" : "naczepa";
            var f = new Form
            {
                Text = existing == null ? $"Nowy {titleKind}" : $"Edycja {titleKind}",
                Width = 460,
                Height = 300,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Padding = new Padding(20)
            };

            // G³ówne informacje
            var mainGroup = new GroupBox
            {
                Text = "Podstawowe informacje",
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(10)
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                ColumnStyles = {
                    new ColumnStyle(SizeType.Percent, 33.33f),
                    new ColumnStyle(SizeType.Percent, 33.33f),
                    new ColumnStyle(SizeType.Percent, 33.33f)
                }
            };

            var tbReg = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            var tbBrand = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            var tbModel = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };

            mainLayout.Controls.Add(new Label { Text = "Rejestracja:", AutoSize = true }, 0, 0);
            mainLayout.Controls.Add(new Label { Text = "Marka:", AutoSize = true }, 1, 0);
            mainLayout.Controls.Add(new Label { Text = "Model:", AutoSize = true }, 2, 0);
            mainLayout.Controls.Add(tbReg, 0, 1);
            mainLayout.Controls.Add(tbBrand, 1, 1);
            mainLayout.Controls.Add(tbModel, 2, 1);
            mainGroup.Controls.Add(mainLayout);

            // Parametry techniczne
            var techGroup = new GroupBox
            {
                Text = "Parametry techniczne",
                Dock = DockStyle.Top,
                Height = 80,
                Top = 90,
                Padding = new Padding(10)
            };

            var techLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                ColumnStyles = {
                    new ColumnStyle(SizeType.Percent, 33.33f),
                    new ColumnStyle(SizeType.Percent, 33.33f),
                    new ColumnStyle(SizeType.Percent, 33.33f)
                }
            };

            var tbCap = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            var tbPal = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            var tbE2 = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };

            techLayout.Controls.Add(new Label { Text = "£adownoœæ [kg]:", AutoSize = true }, 0, 0);
            techLayout.Controls.Add(new Label { Text = "Iloœæ palet:", AutoSize = true }, 1, 0);
            techLayout.Controls.Add(new Label { Text = "Wspó³czynnik E2:", AutoSize = true }, 2, 0);
            techLayout.Controls.Add(tbCap, 0, 1);
            techLayout.Controls.Add(tbPal, 1, 1);
            techLayout.Controls.Add(tbE2, 2, 1);
            techGroup.Controls.Add(techLayout);

            // Status
            var chk = new CheckBox { Text = "Aktywny", Checked = true, Left = 20, Top = 180 };

            // Panel przycisków
            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 40
            };

            var btnOk = new Button { Text = "Zapisz", Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Anuluj", Width = 80, DialogResult = DialogResult.Cancel };
            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnOk });

            f.Controls.AddRange(new Control[] { mainGroup, techGroup, chk, btnPanel });
            f.AcceptButton = btnOk;
            f.CancelButton = btnCancel;

            // Wype³nienie danymi istniej¹cego pojazdu
            if (existing != null)
            {
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
                if (string.IsNullOrWhiteSpace(tbReg.Text))
                {
                    MessageBox.Show("Numer rejestracyjny jest wymagany", "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return ShowDialog(owner, existing, enforcedKind, out dto);
                }

                // ID jest zawsze 0 dla nowego pojazdu lub zachowane z istniej¹cego
                dto.VehicleID = existing?.VehicleID ?? 0;
                dto.Registration = tbReg.Text.Trim().ToUpper(); // Numery rejestracyjne wielkimi literami
                dto.Brand = string.IsNullOrWhiteSpace(tbBrand.Text) ? null : tbBrand.Text.Trim();
                dto.Model = string.IsNullOrWhiteSpace(tbModel.Text) ? null : tbModel.Text.Trim();
                dto.CapacityKg = decimal.TryParse(tbCap.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var cap) ? cap : null;
                dto.PalletSlotsH1 = int.TryParse(tbPal.Text, out var pal) ? pal : null;
                dto.E2Factor = decimal.TryParse(tbE2.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var e2) ? e2 : null;
                dto.Active = chk.Checked;
                dto.Kind = enforcedKind;
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
