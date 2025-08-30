using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class MultiChangeRequestForm : Form
    {
        private readonly string _connString;
        private readonly string _appUser;
        private readonly string _dostawcaId;
        private readonly Dictionary<string, Control> _sourceMap; // z HodowcaForm: kolumna -> kontrolka z aktualną wartością
        private readonly List<(string Label, string Column)> _allowedFields;

        private DataGridView dgv;
        private DateTimePicker dtpEff;
        private TextBox tbReason;
        private Button btnAdd, btnRemove, btnSave, btnCancel;

        public MultiChangeRequestForm(
            string connString,
            string appUser,
            string dostawcaId,
            Dictionary<string, Control> currentValueMap,
            List<(string Label, string Column)> allowedFields)
        {
            _connString = connString;
            _appUser = string.IsNullOrWhiteSpace(appUser) ? Environment.UserName : appUser;
            _dostawcaId = dostawcaId;
            _sourceMap = currentValueMap;
            _allowedFields = allowedFields;

            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Wniosek o zmianę – wiele pól";
            StartPosition = FormStartPosition.CenterParent;
            Width = 900; Height = 560;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            Controls.Add(root);

            // 1) Grid pozycji
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
            };

            // Kolumna: Field (ComboBox)
            var colField = new DataGridViewComboBoxColumn
            {
                HeaderText = "Pole",
                Name = "Field",
                DataPropertyName = "Field",
                DisplayMember = "Label",
                ValueMember = "Column",
                FlatStyle = FlatStyle.Flat,
                Width = 200
            };
            // Źródło do combo
            var dtFields = new DataTable();
            dtFields.Columns.Add("Label");
            dtFields.Columns.Add("Column");
            foreach (var f in _allowedFields)
            {
                var r = dtFields.NewRow();
                r["Label"] = $"{f.Label} ({f.Column})";
                r["Column"] = f.Column;
                dtFields.Rows.Add(r);
            }
            colField.DataSource = dtFields;

            // Kolumna: OldValue (read-only)
            var colOld = new DataGridViewTextBoxColumn
            {
                HeaderText = "Stara wartość",
                Name = "OldValue",
                DataPropertyName = "OldValue",
                ReadOnly = true,
                Width = 220
            };

            // Kolumna: NewValue
            var colNew = new DataGridViewTextBoxColumn
            {
                HeaderText = "Nowa wartość",
                Name = "ProposedNewValue",
                DataPropertyName = "ProposedNewValue",
                Width = 220
            };

            dgv.Columns.AddRange(colField, colOld, colNew);

            // Model danych (in memory)
            var table = new DataTable();
            table.Columns.Add("Field");
            table.Columns.Add("OldValue");
            table.Columns.Add("ProposedNewValue");
            dgv.DataSource = table;

            dgv.EditingControlShowing += Dgv_EditingControlShowing;
            dgv.CellValueChanged += Dgv_CellValueChanged;

            root.Controls.Add(dgv, 0, 0);

            // 2) Data wejścia w życie
            var pnlEff = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            pnlEff.Controls.Add(new Label { Text = "Obowiązuje od:", AutoSize = true, Padding = new Padding(0, 7, 10, 0) });
            dtpEff = new DateTimePicker { Format = DateTimePickerFormat.Short };
            dtpEff.Value = DateTime.Today.AddDays(1);
            pnlEff.Controls.Add(dtpEff);
            root.Controls.Add(pnlEff, 0, 1);

            // 3) Powód
            var lblReason = new Label { Text = "Powód zmiany (wymagany):", AutoSize = true };
            tbReason = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 80 };
            root.Controls.Add(lblReason, 0, 2);
            root.Controls.Add(tbReason, 0, 3);

            // 4) Przyciski
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44 };
            btnSave = new Button { Text = "Zapisz wniosek", AutoSize = true };
            btnCancel = new Button { Text = "Anuluj", AutoSize = true };
            btnAdd = new Button { Text = "Dodaj pozycję", AutoSize = true };
            btnRemove = new Button { Text = "Usuń pozycję", AutoSize = true };

            btnAdd.Click += (_, __) => AddRow();
            btnRemove.Click += (_, __) => RemoveSelected();
            btnSave.Click += (_, __) => SaveRequest();
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            buttons.Controls.AddRange(new Control[] { btnSave, btnCancel, btnRemove, btnAdd });
            Controls.Add(buttons);
        }

        private void AddRow()
        {
            var dt = (DataTable)dgv.DataSource;
            var r = dt.NewRow();
            r["Field"] = _allowedFields.First().Column;
            r["OldValue"] = GetCurrentValue(_allowedFields.First().Column);
            r["ProposedNewValue"] = "";
            dt.Rows.Add(r);
        }

        private void RemoveSelected()
        {
            foreach (DataGridViewRow r in dgv.SelectedRows)
                dgv.Rows.Remove(r);
        }

        private void Dgv_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgv.CurrentCell.OwningColumn.Name == "Field" && e.Control is ComboBox cb)
            {
                cb.SelectedIndexChanged -= ComboFieldChanged;
                cb.SelectedIndexChanged += ComboFieldChanged;
            }
        }

        private void ComboFieldChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox cb && dgv.CurrentRow != null)
            {
                var col = cb.SelectedValue?.ToString();
                if (!string.IsNullOrEmpty(col))
                {
                    dgv.CurrentRow.Cells["OldValue"].Value = GetCurrentValue(col);
                }
            }
        }

        private void Dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name == "Field")
            {
                var col = dgv.Rows[e.RowIndex].Cells["Field"].Value?.ToString();
                if (!string.IsNullOrEmpty(col))
                    dgv.Rows[e.RowIndex].Cells["OldValue"].Value = GetCurrentValue(col);
            }
        }

        private string GetCurrentValue(string column)
        {
            if (!_sourceMap.TryGetValue(column, out var ctrl) || ctrl == null) return null;

            return ctrl switch
            {
                TextBox tb => tb.Text,
                ComboBox cb => cb.Enabled ? cb.SelectedItem?.ToString() : (cb.SelectedItem?.ToString() ?? cb.Text),
                CheckBox ch => ch.Checked ? "1" : "0",
                NumericUpDown nud => nud.Value.ToString(CultureInfo.InvariantCulture),
                DateTimePicker dtp => dtp.Value.ToString("yyyy-MM-dd"),
                _ => ctrl.Text
            };
        }

        private void SaveRequest()
        {
            if (string.IsNullOrWhiteSpace(tbReason.Text))
            {
                MessageBox.Show("Powód jest wymagany.", "Wniosek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var dt = (DataTable)dgv.DataSource;
            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jedną pozycję.", "Wniosek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var conn = new SqlConnection(_connString);
            conn.Open();

            // Header
            long crid;
            using (var cmd = new SqlCommand(@"
INSERT INTO dbo.DostawcyCR (DostawcaID, Reason, RequestedBy, EffectiveFrom, Status)
OUTPUT INSERTED.CRID
VALUES (@ID, @Reason, @User, @Eff, 'Proposed');", conn))
            {
                cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _dostawcaId;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 4000).Value = tbReason.Text.Trim();
                cmd.Parameters.Add("@User", SqlDbType.NVarChar, 128).Value = _appUser;
                cmd.Parameters.Add("@Eff", SqlDbType.Date).Value = dtpEff.Value.Date;
                crid = (long)cmd.ExecuteScalar();
            }

            // Items
            foreach (DataRow r in dt.Rows)
            {
                string field = Convert.ToString(r["Field"]);
                string oldV = Convert.ToString(r["OldValue"]);
                string newV = Convert.ToString(r["ProposedNewValue"]);

                if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(newV))
                    continue;

                using var item = new SqlCommand(@"
INSERT INTO dbo.DostawcyCRItem (CRID, Field, OldValue, ProposedNewValue)
VALUES (@CRID, @Field, @Old, @New);", conn);
                item.Parameters.Add("@CRID", SqlDbType.BigInt).Value = crid;
                item.Parameters.Add("@Field", SqlDbType.NVarChar, 128).Value = field;
                item.Parameters.Add("@Old", SqlDbType.NVarChar, 4000).Value = (object)oldV ?? DBNull.Value;
                item.Parameters.Add("@New", SqlDbType.NVarChar, 4000).Value = newV.Trim();
                item.ExecuteNonQuery();
            }

            MessageBox.Show($"Zapisano wniosek CRID={crid}.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
    }
}
