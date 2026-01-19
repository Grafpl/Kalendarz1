using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class SprawdzalkaUmow : Form
    {
        private readonly string connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Kolory do stylizacji UI
        private readonly Color _primaryColor = Color.FromArgb(92, 138, 58);
        private readonly Color _hoverColor = Color.FromArgb(75, 115, 47);

        public string UserID { get; set; } = "";

        public SprawdzalkaUmow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            ApplyCustomStyles();

            dgvContracts.CurrentCellDirtyStateChanged += DataGridViewKalendarz_CurrentCellDirtyStateChanged;
            dgvContracts.CellContentClick += DataGridViewKalendarz_CellContentClick;
            chkShowOnlyIncomplete.CheckedChanged += (s, e) => ApplyCombinedFilter();

            LoadDataGridKalendarz();
        }

        private void ApplyCustomStyles()
        {
            ConfigureDataGridView(dgvContracts);
            btnAddContract.MouseEnter += (s, e) => btnAddContract.BackColor = _hoverColor;
            btnAddContract.MouseLeave += (s, e) => btnAddContract.BackColor = _primaryColor;
        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgv.EditMode = DataGridViewEditMode.EditOnEnter;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 249);
            dgv.DefaultCellStyle.SelectionBackColor = _hoverColor;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.RowPrePaint += Dgv_RowPrePaint;
        }

        private void LoadDataGridKalendarz(bool preserveState = true)
        {
            (int firstRow, int? currentId, string? currentColName) state = default;
            if (preserveState)
                state = CaptureGridState();

            const string query = @"
                SELECT 
                    h.[LP] AS ID, h.[DataOdbioru], h.[Dostawca],
                    CAST(ISNULL(h.[Utworzone],0) AS bit) AS Utworzone,
                    CAST(ISNULL(h.[Wysłane],0) AS bit) AS Wysłane,
                    CAST(ISNULL(h.[Otrzymane],0) AS bit) AS Otrzymane,
                    CAST(ISNULL(h.[Posrednik],0) AS bit) AS Posrednik,
                    h.[Auta], h.[SztukiDek], h.[WagaDek], h.[SztSzuflada],
                    ISNULL(u1.Name, h.KtoUtw) AS KtoUtw, h.[KiedyUtw],
                    ISNULL(u2.Name, h.KtoWysl) AS KtoWysl, h.[KiedyWysl],
                    ISNULL(u3.Name, h.KtoOtrzym) AS KtoOtrzym, h.[KiedyOtrzm]
                FROM [LibraNet].[dbo].[HarmonogramDostaw] h
                LEFT JOIN [LibraNet].[dbo].[operators] u1 ON TRY_CAST(h.KtoUtw AS INT) = u1.ID
                LEFT JOIN [LibraNet].[dbo].[operators] u2 ON TRY_CAST(h.KtoWysl AS INT) = u2.ID
                LEFT JOIN [LibraNet].[dbo].[operators] u3 ON TRY_CAST(h.KtoOtrzym AS INT) = u3.ID
                WHERE h.Bufor = 'Potwierdzony'
                  AND h.DataOdbioru BETWEEN '2021-01-01' AND DATEADD(DAY, 2, GETDATE())
                ORDER BY h.DataOdbioru DESC;";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                dgvContracts.SuspendLayout();
                dgvContracts.AutoGenerateColumns = false;

                if (dgvContracts.Columns.Count == 0)
                {
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ID", Name = "ID", Visible = false });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DataOdbioru", Name = "DataOdbioru", HeaderText = "Data", DefaultCellStyle = { Format = "yyyy-MM-dd" }, Width = 120 });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Dostawca", Name = "Dostawca", HeaderText = "Dostawca", Width = 110 });
                    dgvContracts.Columns.Add(MakeCheckColumn("Utworzone"));
                    dgvContracts.Columns.Add(MakeCheckColumn("Wysłane"));
                    dgvContracts.Columns.Add(MakeCheckColumn("Otrzymane"));
                    dgvContracts.Columns.Add(MakeCheckColumn("Posrednik", "Pośrednik"));
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Auta", Name = "Auta", HeaderText = "Aut" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztukiDek", Name = "SztukiDek", HeaderText = "Sztuki" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WagaDek", Name = "WagaDek", HeaderText = "Waga" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztSzuflada", Name = "SztSzuflada", HeaderText = "sztPoj" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoUtw", Name = "KtoUtw", HeaderText = "Kto utworzył" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KiedyUtw", Name = "KiedyUtw", HeaderText = "Kiedy utworzył", DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" } });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoWysl", Name = "KtoWysl", HeaderText = "Kto wysłał" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KiedyWysl", Name = "KiedyWysl", HeaderText = "Kiedy wysłał", DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" } });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoOtrzym", Name = "KtoOtrzym", HeaderText = "Kto otrzymał" });
                    dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KiedyOtrzm", Name = "KiedyOtrzm", HeaderText = "Kiedy otrzymał", DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" } });
                }

                dgvContracts.DataSource = table.DefaultView;
                ApplyCombinedFilter();
                dgvContracts.ResumeLayout();
            }

            if (preserveState)
                RestoreGridState(state);
        }

        private static DataGridViewCheckBoxColumn MakeCheckColumn(string dataProperty, string header = null) => new DataGridViewCheckBoxColumn
        {
            DataPropertyName = dataProperty,
            Name = dataProperty,
            HeaderText = header ?? dataProperty,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
        };

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;

            bool okU = GetBool(dgv, e.RowIndex, "Utworzone");
            bool okW = GetBool(dgv, e.RowIndex, "Wysłane");
            bool okO = GetBool(dgv, e.RowIndex, "Otrzymane");

            if (okU && okW && okO)
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(46, 204, 113); // Flat Green
                dgv.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.White;
            }
            else
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = (e.RowIndex % 2 == 0) ? Color.White : dgv.AlternatingRowsDefaultCellStyle.BackColor;
            }
        }

        private static bool GetBool(DataGridView dgv, int rowIndex, string colName)
        {
            if (rowIndex < 0 || !dgv.Columns.Contains(colName)) return false;
            var val = dgv.Rows[rowIndex].Cells[colName]?.Value;
            if (val == null || val == DBNull.Value) return false;
            if (val is bool b) return b;
            return bool.TryParse(val.ToString(), out var parsed) && parsed;
        }

        private void DataGridViewKalendarz_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvContracts.IsCurrentCellDirty && dgvContracts.CurrentCell is DataGridViewCheckBoxCell)
            {
                dgvContracts.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DataGridViewKalendarz_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var grid = dgvContracts;
            var col = grid.Columns[e.ColumnIndex];

            if (col.Name != "Utworzone" && col.Name != "Wysłane" && col.Name != "Otrzymane" && col.Name != "Posrednik") return;

            var row = grid.Rows[e.RowIndex];
            if (!grid.Columns.Contains("ID")) return;
            int id = Convert.ToInt32(row.Cells["ID"].Value);
            bool newValue = Convert.ToBoolean(((DataGridViewCheckBoxCell)row.Cells[col.Name]).EditedFormattedValue);

            string msg = newValue ? $"Czy na pewno ustawić „{col.HeaderText}” = TAK dla pozycji ID={id}?" : $"Czy na pewno ustawić „{col.HeaderText}” = NIE dla pozycji ID={id}?";
            if (MessageBox.Show(msg, "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                row.Cells[col.Name].Value = !newValue;
                return;
            }

            string ktoCol = null, kiedyCol = null;
            switch (col.Name)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
            }

            try
            {
                UpdateKalendarzFlag_NoReload(id, col.Name, newValue);
                LoadDataGridKalendarz(); // Odśwież dane, aby zobaczyć zaktualizowane nazwisko
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                row.Cells[col.Name].Value = !newValue;
            }
        }

        private string BuildRowFilter()
        {
            string filterText = txtSearch.Text.Trim().Replace("'", "''");
            string textFilter = string.IsNullOrEmpty(filterText) ? string.Empty : $"Dostawca LIKE '%{filterText}%' OR CONVERT(DataOdbioru, 'System.String') LIKE '%{filterText}%' OR KtoUtw LIKE '%{filterText}%' OR KtoWysl LIKE '%{filterText}%' OR KtoOtrzym LIKE '%{filterText}%'";
            string incompleteFilter = "[Utworzone] = false OR [Wysłane] = false OR [Otrzymane] = false";

            if (chkShowOnlyIncomplete.Checked)
            {
                return string.IsNullOrEmpty(textFilter) ? incompleteFilter : $"({textFilter}) AND ({incompleteFilter})";
            }
            return textFilter;
        }

        private void ApplyCombinedFilter()
        {
            if (dgvContracts.DataSource is DataView dv)
            {
                dv.RowFilter = BuildRowFilter();
            }
        }

        private void UpdateKalendarzFlag_NoReload(int id, string columnName, bool value)
        {
            string[] allowed = { "Utworzone", "Wysłane", "Otrzymane", "Posrednik" };
            if (Array.IndexOf(allowed, columnName) < 0) throw new InvalidOperationException("Nieobsługiwana kolumna: " + columnName);

            string? ktoCol = null, kiedyCol = null;
            switch (columnName)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
            }

            if (value && ktoCol != null && !int.TryParse(UserID, out int userIdInt)) throw new InvalidOperationException("UserID musi być liczbą.");

            string sql = ktoCol == null
                ? $@"UPDATE dbo.HarmonogramDostaw SET [{columnName}] = @val WHERE [LP] = @id;"
                : $@"UPDATE dbo.HarmonogramDostaw SET [{columnName}] = @val, [{ktoCol}] = CASE WHEN @val = 1 THEN @kto ELSE NULL END, [{kiedyCol}] = CASE WHEN @val = 1 THEN GETDATE() ELSE NULL END WHERE [LP] = @id;";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@val", value);
                cmd.Parameters.AddWithValue("@id", id);
                if (ktoCol != null) cmd.Parameters.AddWithValue("@kto", (object)int.Parse(UserID));

                conn.Open();
                if (cmd.ExecuteNonQuery() != 1) throw new Exception("Zaktualizowano nieprawidłową liczbę wierszy.");
            }
        }

        private void CommandButton_Insert_Click(object sender, EventArgs e)
        {
            if (dgvContracts.CurrentRow == null)
            {
                MessageBox.Show("Zaznacz pozycję w kalendarzu.");
                return;
            }
            var cellVal = dgvContracts.CurrentRow.Cells["ID"]?.Value;
            if (cellVal == null || cellVal == DBNull.Value)
            {
                MessageBox.Show("Brak wartości LP (ID) w zaznaczonym wierszu.");
                return;
            }
            string lp = cellVal.ToString()!;
            var form = new UmowyForm(initialLp: lp, initialIdLibra: null) { UserID = App.UserID };
            form.FormClosed += (s, args) => LoadDataGridKalendarz();
            form.Show(this);
        }

        private void nieUzupelnione_CheckedChanged(object sender, EventArgs e)
        {
            ApplyCombinedFilter();
        }

        private (int firstRow, int? currentId, string? currentColName) CaptureGridState()
        {
            if (dgvContracts.RowCount == 0) return (-1, null, null);
            int first = dgvContracts.FirstDisplayedScrollingRowIndex;
            int? id = dgvContracts.CurrentRow?.Cells["ID"].Value as int?;
            string? colName = dgvContracts.CurrentCell != null ? dgvContracts.Columns[dgvContracts.CurrentCell.ColumnIndex].Name : null;
            return (first, id, colName);
        }

        private void RestoreGridState((int firstRow, int? currentId, string? currentColName) state)
        {
            if (state.firstRow >= 0 && state.firstRow < dgvContracts.RowCount)
            {
                try { dgvContracts.FirstDisplayedScrollingRowIndex = state.firstRow; } catch { /* ignore */ }
            }
            if (state.currentId.HasValue)
            {
                var row = dgvContracts.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Cells["ID"].Value is int val && val == state.currentId.Value);
                if (row != null)
                {
                    int colIndex = !string.IsNullOrEmpty(state.currentColName) && dgvContracts.Columns.Contains(state.currentColName) ? dgvContracts.Columns[state.currentColName].Index : 0;
                    try { dgvContracts.CurrentCell = row.Cells[colIndex]; } catch { /* ignore */ }
                }
            }
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyCombinedFilter();
        }
    }
}