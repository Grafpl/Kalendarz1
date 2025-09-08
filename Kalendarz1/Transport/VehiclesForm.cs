using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport
{
    internal sealed class VehiclesForm : Form
    {
        private readonly TransportRepository _repo;
        private DataGridView _grid = new();
        private BindingSource _bs = new();
        private CheckBox _chkInactive = new();
        private Button _btnAdd = new();
        private Button _btnEdit = new();
        private Button _btnToggle = new();
        private Button _btnClose = new();
        private ComboBox _cbKind = new();
        private StatusStrip _status = new();
        private ToolStripStatusLabel _statusInfo = new();
        private ToolTip _tt = new();

        public VehiclesForm(TransportRepository repo)
        {
            _repo = repo;
            Text = "Pojazdy"; Width = 920; Height = 560; StartPosition = FormStartPosition.CenterParent;
            BuildUi();
            TransportUi.ApplyTheme(this);
            Load += async (_, __) => await LoadDataAsync();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 }; root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 60, Padding = new Padding(6), AutoSize = true, WrapContents = false };
            var lblTitle = new Label { Text = "Lista pojazdów", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            _cbKind.DropDownStyle = ComboBoxStyle.DropDownList; _cbKind.Width = 140; _cbKind.Items.AddRange(new object[]{"(Wszystkie)", "3 - Auto", "4 - Naczepa"}); _cbKind.SelectedIndex = 0; _cbKind.SelectedIndexChanged += async (_, __) => await LoadDataAsync();
            _chkInactive.Text = "Poka¿ nieaktywne"; _chkInactive.CheckedChanged += async (_, __) => await LoadDataAsync();
            _btnAdd.Text = "+ Dodaj"; _btnAdd.Click += async (_, __) => await AddAsync();
            _btnEdit.Text = "Edytuj"; _btnEdit.Click += async (_, __) => await EditAsync();
            _btnToggle.Text = "Aktywuj/Deaktywuj"; _btnToggle.Click += async (_, __) => await ToggleAsync();
            _btnClose.Text = "Zamknij"; _btnClose.Click += (_, __) => Close();

            _tt.SetToolTip(_cbKind, "Filtruj po rodzaju pojazdu");
            _tt.SetToolTip(_chkInactive, "Poka¿ równie¿ nieaktywne pojazdy");
            _tt.SetToolTip(_btnAdd, "Dodaj pojazd");
            _tt.SetToolTip(_btnEdit, "Edytuj zaznaczony pojazd");
            _tt.SetToolTip(_btnToggle, "Prze³¹cz aktywnoœæ");

            top.Controls.Add(lblTitle); top.Controls.Add(new Label{Text="Rodzaj:", AutoSize=true, Margin=new Padding(16,9,3,3)}); top.Controls.Add(_cbKind); top.Controls.Add(_chkInactive); top.Controls.Add(_btnAdd); top.Controls.Add(_btnEdit); top.Controls.Add(_btnToggle); top.Controls.Add(_btnClose);
            root.Controls.Add(top,0,0);

            _grid.Dock = DockStyle.Fill; _grid.ReadOnly = true; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.MultiSelect = false; _grid.DataSource = _bs;
            TransportUi.StyleGrid(_grid);
            root.Controls.Add(_grid,0,1);

            _status.Items.Add(_statusInfo); root.Controls.Add(_status,0,2);
        }

        private async Task LoadDataAsync()
        {
            _statusInfo.Text = "£adowanie...";
            int? kind = null;
            if (_cbKind.SelectedIndex > 0)
            {
                if (_cbKind.SelectedIndex == 1) kind = 3; else if (_cbKind.SelectedIndex == 2) kind = 4;
            }
            var dt = await _repo.GetVehicles2Async(kind, includeInactive: _chkInactive.Checked);
            _bs.DataSource = dt;
            _statusInfo.Text = $"Rekordy: {dt.Rows.Count}";
        }

        private async Task AddAsync()
        {
            var reg = Prompt("Rejestracja:"); if (string.IsNullOrWhiteSpace(reg)) return;
            var brand = Prompt("Marka:");
            var model = Prompt("Model:");
            var capStr = Prompt("£adownoœæ kg:"); decimal? cap = null; if (decimal.TryParse(capStr.Replace(',', '.'), out var c1)) cap = c1;
            var slotsStr = Prompt("Sloty H1:"); int? slots = null; if (int.TryParse(slotsStr, out var s1)) slots = s1;
            var kindStr = Prompt("Rodzaj (3=Auto,4=Naczepa):", "3"); if (!int.TryParse(kindStr, out var kind)) kind = 3;
            var e2Str = Prompt("E2Factor (np. 0.10):", "0.10"); decimal? e2 = null; if (decimal.TryParse(e2Str.Replace(',', '.'), out var e2v)) e2 = e2v;
            try { await _repo.AddVehicle2Async(reg, cap, slots, kind, brand, model, e2); await LoadDataAsync(); } catch (System.Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async Task EditAsync()
        {
            if (_grid.CurrentRow == null) return;
            var idObj = _grid.CurrentRow.Cells["VehicleID"].Value; if (idObj == null) return;
            var regOld = _grid.CurrentRow.Cells["Registration"].Value?.ToString() ?? "";
            var brandOld = _grid.CurrentRow.Cells["Brand"].Value?.ToString() ?? "";
            var modelOld = _grid.CurrentRow.Cells["Model"].Value?.ToString() ?? "";
            var capOld = _grid.CurrentRow.Cells["CapacityKg"].Value?.ToString() ?? "0";
            var slotsOld = _grid.CurrentRow.Cells["PalletSlotsH1"].Value?.ToString() ?? "0";
            var kindOld = _grid.CurrentRow.Cells["Kind"].Value?.ToString() ?? "3";
            var e2Old = _grid.CurrentRow.Cells["E2Factor"].Value?.ToString() ?? "0.10";
            bool active = true; bool.TryParse(_grid.CurrentRow.Cells["Active"].Value?.ToString(), out active);

            var reg = Prompt("Rejestracja:", regOld); if (string.IsNullOrWhiteSpace(reg)) return;
            var brand = Prompt("Marka:", brandOld);
            var model = Prompt("Model:", modelOld);
            var capStr = Prompt("£adownoœæ kg:", capOld); decimal? cap = null; if (decimal.TryParse(capStr.Replace(',', '.'), out var c1)) cap = c1;
            var slotsStr = Prompt("Sloty H1:", slotsOld); int? slots = null; if (int.TryParse(slotsStr, out var s1)) slots = s1;
            var kindStr = Prompt("Rodzaj (3/4):", kindOld); if (!int.TryParse(kindStr, out var kind)) kind = 3;
            var e2Str = Prompt("E2Factor:", e2Old); decimal? e2 = null; if (decimal.TryParse(e2Str.Replace(',', '.'), out var e2v)) e2 = e2v;
            await _repo.UpdateVehicle2Async((int)idObj, reg, cap, slots, kind, brand, model, e2, active);
            await LoadDataAsync();
        }

        private async Task ToggleAsync()
        {
            if (_grid.CurrentRow == null) return;
            var idObj = _grid.CurrentRow.Cells["VehicleID"].Value; if (idObj == null) return;
            bool active = true; bool.TryParse(_grid.CurrentRow.Cells["Active"].Value?.ToString(), out active);
            await _repo.SetVehicle2ActiveAsync((int)idObj, !active);
            await LoadDataAsync();
        }

        private static string Prompt(string text, string? initial = null)
        {
            var f = new Form { Width = 420, Height = 150, Text = text, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox=false, MaximizeBox=false };
            var tb = new TextBox { Dock = DockStyle.Top, Text = initial ?? string.Empty };
            var ok = new Button { Text = "OK", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
            f.Controls.Add(tb); f.Controls.Add(ok); f.AcceptButton = ok;
            TransportUi.ApplyTheme(f);
            return f.ShowDialog() == DialogResult.OK ? tb.Text : string.Empty;
        }
    }
}
