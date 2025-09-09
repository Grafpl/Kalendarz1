using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;  // Added SQL namespace

namespace Kalendarz1.Transport
{
    internal sealed class TransportMainForm : Form
    {
        private readonly TransportRepository _repo;
        private readonly string _conn;
        private DateTimePicker _dtDate = new();
        private DataGridView _grid = new();
        private Button _btnRefresh = new();
        private Button _btnNew = new();
        private Button _btnDelete = new();
        private Button _btnLoads = new();
        private Button _btnDrivers = new();
        private Button _btnVehicles = new();
        private ComboBox _cbStatusFilter = new();
        private BindingSource _bs = new();
        private string _user;
        private StatusStrip _status = new();
        private ToolStripStatusLabel _statusInfo = new();
        private ToolTip _tt = new();

        public TransportMainForm(string connectionString, string connectionStringSymf, string? userId = null)
        {
            _conn = connectionString;
            _repo = new TransportRepository(_conn, connectionStringSymf);
            _user = string.IsNullOrWhiteSpace(userId) ? Environment.UserName : userId!;
            Text = "Transport – Kursy";
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            BuildUi();
            TransportUi.ApplyTheme(this);
            Load += async (_, __) => {
                _statusInfo.Text = "Trwa inicjalizacja...";
                await InitializeDatabaseAsync();
                await LoadTripsAsync();
                _statusInfo.Text = "Gotowe. U¿yj przycisków powy¿ej aby zarz¹dzaæ kursami.";
            };
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                
                // Ensure required tables exist
                var sql = @"
                    IF OBJECT_ID('dbo.TDriver') IS NULL CREATE TABLE dbo.TDriver(DriverID INT IDENTITY PRIMARY KEY, FirstName NVARCHAR(50) NOT NULL, LastName NVARCHAR(80) NOT NULL, Phone NVARCHAR(30) NULL, Active BIT NOT NULL DEFAULT 1);
                    IF OBJECT_ID('dbo.TVehicle') IS NULL CREATE TABLE dbo.TVehicle(VehicleID INT IDENTITY PRIMARY KEY, Registration NVARCHAR(20) NOT NULL UNIQUE, Kind INT NOT NULL DEFAULT 3);
                    IF OBJECT_ID('dbo.TTrip') IS NULL CREATE TABLE dbo.TTrip(TripID BIGINT IDENTITY PRIMARY KEY, TripDate DATE NOT NULL);
                    IF OBJECT_ID('dbo.TTripLoad') IS NULL CREATE TABLE dbo.TTripLoad(TripLoadID BIGINT IDENTITY PRIMARY KEY, TripID BIGINT NOT NULL);";
                
                await using var cmd = new SqlCommand(sql, cn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"B³¹d inicjalizacji bazy danych: {ex.Message}", "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 72, Padding = new Padding(12, 10, 12, 6), AutoSize = true, WrapContents = false };
            var lblTitle = new Label { Text = "ZARZ¥DZANIE KURSAMI", AutoSize = true, Font = new Font("Segoe UI Semibold", 14F), Margin = new Padding(0, 8, 30, 0) };
            _dtDate.Value = DateTime.Today; _dtDate.Width = 190; _dtDate.Font = new Font("Segoe UI", 11F);
            _dtDate.ValueChanged += async (_, __) => await LoadTripsAsync();

            _btnRefresh.Text = "Odœwie¿"; _btnRefresh.Width = 110; _btnRefresh.Click += async (_, __) => await LoadTripsAsync();
            _btnNew.Text = "+ Nowy kurs"; _btnNew.Width = 140; _btnNew.Click += async (_, __) => await CreateTripAsync();
            _btnDelete.Text = "Usuñ"; _btnDelete.Width = 100; _btnDelete.Click += async (_, __) => await DeleteTripAsync();
            _btnLoads.Text = "Otwórz / Edytuj"; _btnLoads.Width = 150; _btnLoads.Click += (_, __) => OpenLoadsEditor();
            _btnDrivers.Text = "Kierowcy"; _btnDrivers.Width = 120; _btnDrivers.Click += async (_, __) => { using var f = new DriversForm(_repo); f.ShowDialog(this); await LoadTripsAsync(); };
            _btnVehicles.Text = "Pojazdy"; _btnVehicles.Width = 120; _btnVehicles.Click += async (_, __) => { using var f = new VehiclesForm(_repo); f.ShowDialog(this); await LoadTripsAsync(); };
            _cbStatusFilter.DropDownStyle = ComboBoxStyle.DropDownList; _cbStatusFilter.Width = 160; _cbStatusFilter.Font = new Font("Segoe UI", 11F);
            _cbStatusFilter.Items.AddRange(new object[] { "(Wszystkie)", "Zaplanowane", "W trakcie", "Zakoñczone", "Anulowane" });
            _cbStatusFilter.SelectedIndex = 0; _cbStatusFilter.SelectedIndexChanged += (_, __) => ApplyStatusFilter();

            // Tooltips
            _tt.SetToolTip(_dtDate, "Data planowanych kursów");
            _tt.SetToolTip(_btnRefresh, "Odœwie¿ listê kursów dla daty (F5)");
            _tt.SetToolTip(_btnNew, "Utwórz nowy kurs (Insert)");
            _tt.SetToolTip(_btnDelete, "Usuñ zaznaczony kurs (Del)");
            _tt.SetToolTip(_btnLoads, "Otwórz szczegó³y wybranego kursu (Enter)");
            _tt.SetToolTip(_cbStatusFilter, "Filtruj listê wed³ug statusu");
            _tt.SetToolTip(_btnDrivers, "Lista kierowców");
            _tt.SetToolTip(_btnVehicles, "Lista pojazdów");

            top.Controls.Add(lblTitle);
            top.Controls.Add(new Label { Text = "Data:", AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Margin = new Padding(10, 14, 4, 0) }); top.Controls.Add(_dtDate);
            top.Controls.Add(_btnRefresh); top.Controls.Add(_btnNew); top.Controls.Add(_btnDelete); top.Controls.Add(_btnLoads);
            top.Controls.Add(new Label { Text = "Status:", AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Margin = new Padding(18, 14, 4, 0) }); top.Controls.Add(_cbStatusFilter);
            top.Controls.Add(_btnDrivers); top.Controls.Add(_btnVehicles);
            root.Controls.Add(top, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.RowTemplate.Height = 34;
            _grid.DataSource = _bs;
            _grid.CellDoubleClick += (_, __) => OpenLoadsEditor();
            _grid.DataError += (_, e) => { if (e.Exception is FormatException) { e.ThrowException = false; } };
            TransportUi.StyleGrid(_grid);
            root.Controls.Add(_grid, 0, 1);

            _status.Items.Add(_statusInfo); _statusInfo.Text = "Wczytywanie..."; root.Controls.Add(_status, 0, 2);

            // Skróty klawiszowe jak poprzednio w formie
            KeyPreview = true;
            KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.F5) { await LoadTripsAsync(); e.Handled = true; }
                else if (e.KeyCode == Keys.Insert) { await CreateTripAsync(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { await DeleteTripAsync(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { OpenLoadsEditor(); e.Handled = true; }
            };
        }

        private async Task LoadTripsAsync()
        {
            try
            {
                var dt = await _repo.GetTripsByDateAsync(_dtDate.Value.Date);
                if (!dt.Columns.Contains("FinalFillPctDisplay")) dt.Columns.Add("FinalFillPctDisplay", typeof(string));
                if (!dt.Columns.Contains("WyjazdTxt")) dt.Columns.Add("WyjazdTxt", typeof(string));
                foreach (DataRow r in dt.Rows)
                {
                    decimal pct = 0m; var val = r["FinalFillPct"]; if (val != null && val != DBNull.Value)
                    {
                        switch (val)
                        {
                            case decimal d: pct = d; break;
                            case double db: pct = (decimal)db; break;
                            case float fl: pct = (decimal)fl; break;
                            case int i: pct = i; break;
                            case long l: pct = l; break;
                            case string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out var p): pct = p; break;
                            case string s2 when decimal.TryParse(s2, NumberStyles.Any, CultureInfo.InvariantCulture, out var p2): pct = p2; break;
                        }
                    }
                    r["FinalFillPctDisplay"] = pct.ToString("0.0") + "%";
                    if (r["PlannedDeparture"] is TimeSpan ts) r["WyjazdTxt"] = ts.ToString("hh':'mm"); else r["WyjazdTxt"] = string.Empty;
                }
                _bs.DataSource = dt;
                ConfigureGridColumns();
                ApplyStatusFilter();
                _statusInfo.Text = $"Kursy: {dt.DefaultView.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("B³¹d ³adowania kursów: " + ex.Message, "B³¹d", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusInfo.Text = "B³¹d ³adowania";
            }
        }

        private void ConfigureGridColumns()
        {
            foreach (DataGridViewColumn c in _grid.Columns) c.Visible = false;
            Show("TripID", "ID", 70);
            ShowDate("TripDate", "Data", 110);
            if (_grid.Columns["WyjazdTxt"] != null) Show("WyjazdTxt", "Wyjazd", 90); else Show("PlannedDeparture", "Wyjazd", 90);
            Show("DriverName", "Kierowca", 160);
            Show("VehicleReg", "Pojazd", 120);
            Show("RouteName", "Trasa", 240); // Zwiêkszono szerokoœæ dla d³u¿szych tras
            Show("Status", "Status", 110);
            Show("MassFillPct", "% Masa", 90, format: "N1");
            Show("SpaceFillPct", "% Przestrzeñ", 110, format: "N1");
            Show("FinalFillPctDisplay", "% Wype³nienie", 120);
            void Show(string name, string header, int width, string? format = null) { if (_grid.Columns[name] != null) { var col = _grid.Columns[name]; col.Visible = true; col.HeaderText = header; col.Width = width; if (format != null) col.DefaultCellStyle.Format = format; } }
            void ShowDate(string name, string header, int width) { if (_grid.Columns[name] != null) { var col = _grid.Columns[name]; col.Visible = true; col.HeaderText = header; col.Width = width; col.DefaultCellStyle.Format = "d"; } }
        }

        private void ApplyStatusFilter()
        {
            if (_bs.DataSource is DataTable dt)
            {
                if (_cbStatusFilter.SelectedIndex <= 0)
                {
                    dt.DefaultView.RowFilter = string.Empty;
                }
                else
                {
                    var st = _cbStatusFilter.SelectedIndex switch
                    {
                        1 => "Planned",
                        2 => "InProgress",
                        3 => "Completed",
                        4 => "Canceled",
                        _ => null
                    };
                    dt.DefaultView.RowFilter = st != null ? $"Status = '{st}'" : string.Empty;
                }
                _statusInfo.Text = $"Kursy: {dt.DefaultView.Count}";
            }
        }

        private long? SelectedTripId => _grid.CurrentRow?.Cells["TripID"]?.Value is long v ? v : (_grid.CurrentRow?.Cells["TripID"].Value is int i ? i : (long?)null);

        private async Task CreateTripAsync()
        {
            using var dlg = new TripEditorForm(_repo, _dtDate.Value.Date, _user, null);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                await LoadTripsAsync();
        }

        private async Task DeleteTripAsync()
        {
            var id = SelectedTripId; 
            if (id == null) return;

            if (MessageBox.Show("Usun¹æ wybrany kurs?", "PotwierdŸ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) 
                return;

            try
            {
                await using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("DELETE FROM dbo.TTrip WHERE TripID=@id", cn);
                cmd.Parameters.AddWithValue("@id", id.Value);
                await cmd.ExecuteNonQueryAsync();
                await LoadTripsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("B³¹d usuwania: " + ex.Message);
            }
        }

        private void OpenLoadsEditor()
        {
            var id = SelectedTripId; if (id == null) return;
            using var dlg = new TripEditorForm(_repo, _dtDate.Value.Date, _user, id.Value);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _ = LoadTripsAsync();
        }
    }
}
