using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport
{
    internal sealed class DriversForm : Form
    {
        private readonly TransportRepository _repo;
        private DataGridView _grid = new();
        private BindingSource _bs = new();
        private CheckBox _chkInactive = new();
        private Button _btnAdd = new();
        private Button _btnEdit = new();
        private Button _btnToggle = new();
        private Button _btnClose = new();
        private StatusStrip _status = new();
        private ToolStripStatusLabel _statusInfo = new();
        private ToolTip _tt = new();

        public DriversForm(TransportRepository repo)
        {
            _repo = repo;
            Text = "Kierowcy"; Width = 720; Height = 520; StartPosition = FormStartPosition.CenterParent;
            BuildUi();
            TransportUi.ApplyTheme(this);
            Load += async (_, __) => await LoadDataAsync();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 }; root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 56, Padding = new Padding(6), AutoSize = true, WrapContents = false };
            var lblTitle = new Label { Text = "Lista kierowców", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            _chkInactive.Text = "Poka¿ nieaktywnych"; _chkInactive.CheckedChanged += async (_, __) => await LoadDataAsync();
            _btnAdd.Text = "+ Dodaj"; _btnAdd.Click += async (_, __) => await AddAsync();
            _btnEdit.Text = "Edytuj"; _btnEdit.Click += async (_, __) => await EditAsync();
            _btnToggle.Text = "Aktywuj/Deaktywuj"; _btnToggle.Click += async (_, __) => await ToggleAsync();
            _btnClose.Text = "Zamknij"; _btnClose.Click += (_, __) => Close();

            _tt.SetToolTip(_chkInactive, "Poka¿ równie¿ kierowców nieaktywnych");
            _tt.SetToolTip(_btnAdd, "Dodaj nowego kierowcê");
            _tt.SetToolTip(_btnEdit, "Edytuj zaznaczonego kierowcê");
            _tt.SetToolTip(_btnToggle, "Zmieñ status aktywnoœci");

            top.Controls.Add(lblTitle); top.Controls.Add(_chkInactive); top.Controls.Add(_btnAdd); top.Controls.Add(_btnEdit); top.Controls.Add(_btnToggle); top.Controls.Add(_btnClose);
            root.Controls.Add(top,0,0);

            _grid.Dock = DockStyle.Fill; _grid.ReadOnly = true; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.MultiSelect = false; _grid.DataSource = _bs;
            TransportUi.StyleGrid(_grid);
            root.Controls.Add(_grid,0,1);

            _status.Items.Add(_statusInfo); _statusInfo.Text = ""; root.Controls.Add(_status,0,2);
        }

        private async Task LoadDataAsync()
        {
            _statusInfo.Text = "£adowanie...";
            var dt = await _repo.GetDrivers2Async(includeInactive: _chkInactive.Checked);
            _bs.DataSource = dt;
            _statusInfo.Text = $"Rekordy: {dt.Rows.Count}";
        }

        private async Task AddAsync()
        {
            var full = Prompt("Imiê i nazwisko:"); if (string.IsNullOrWhiteSpace(full)) return;
            var phone = Prompt("Telefon (opcjonalnie):");
            try { await _repo.AddDriver2Async(full, phone); await LoadDataAsync(); }
            catch (System.Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async Task EditAsync()
        {
            if (_grid.CurrentRow == null) return;
            var idObj = _grid.CurrentRow.Cells["DriverID"].Value; if (idObj == null) return;
            var fullOld = _grid.CurrentRow.Cells["FullName"].Value?.ToString() ?? "";
            var phoneOld = _grid.CurrentRow.Cells["Phone"].Value?.ToString();
            var full = Prompt("Imiê i nazwisko:", fullOld); if (string.IsNullOrWhiteSpace(full)) return;
            var phone = Prompt("Telefon:", phoneOld ?? "");
            bool active = true; bool.TryParse(_grid.CurrentRow.Cells["Active"].Value?.ToString(), out active);
            await _repo.UpdateDriver2Async((int)idObj, full, phone, active);
            await LoadDataAsync();
        }

        private async Task ToggleAsync()
        {
            if (_grid.CurrentRow == null) return;
            var idObj = _grid.CurrentRow.Cells["DriverID"].Value; if (idObj == null) return;
            bool active = true; bool.TryParse(_grid.CurrentRow.Cells["Active"].Value?.ToString(), out active);
            await _repo.SetDriver2ActiveAsync((int)idObj, !active);
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
