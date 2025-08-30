using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class SprawdzalkaUmow : Form
    {
        private readonly string connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Ustaw to z zewnątrz po utworzeniu formy (np. z ekranu logowania)
        public string UserID { get; set; } = "";

        public SprawdzalkaUmow()
        {
            InitializeComponent();

            // wygląd siatek
            ConfigureDataGridView(dataGridViewKalendarz);
            ConfigureDataGridView(dataGridViewPartie);

            // klik w checkbox = commit
            dataGridViewKalendarz.CurrentCellDirtyStateChanged += DataGridViewKalendarz_CurrentCellDirtyStateChanged;
            dataGridViewKalendarz.CellContentClick += DataGridViewKalendarz_CellContentClick;

            // filtr „nie uzupełnione”
            nieUzupelnione.CheckedChanged += (s, e) => ApplyKalendarzFilter();

            // load
            LoadDataGridKalendarz();
            LoadDataGridPartie();
            ConfigureDataGridViewColumns();
        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.LightGray;
            dgv.DefaultCellStyle.SelectionBackColor = Color.DarkGray;
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgv.AllowUserToResizeRows = false;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgv.RowPrePaint += Dgv_RowPrePaint;
            dgv.EditMode = DataGridViewEditMode.EditOnEnter;
        }

        private void LoadDataGridKalendarz(bool preserveState = true)
        {
            // (1) Opcjonalnie zapamiętaj stan przewinięcia/selekt
            (int firstRow, int? currentId, string? currentColName) state = default;
            if (preserveState)
                state = CaptureGridState();

            const string query = @"
SELECT 
    [LP] AS ID,
    [DataOdbioru],
    [Dostawca],
    CAST(ISNULL([Utworzone],0)  AS bit) AS Utworzone,
    CAST(ISNULL([Wysłane],0)    AS bit) AS Wysłane,
    CAST(ISNULL([Otrzymane],0)  AS bit) AS Otrzymane,
    CAST(ISNULL([Posrednik],0)  AS bit) AS Posrednik,
    [Auta],
    [SztukiDek],
    [WagaDek],
    [SztSzuflada],
    [KtoUtw],   [KiedyUtw],
    [KtoWysl],  [KiedyWysl],
    [KtoOtrzym],[KiedyOtrzm]
FROM [LibraNet].[dbo].[HarmonogramDostaw]
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru BETWEEN '2021-01-01' AND DATEADD(DAY, 2, GETDATE())
ORDER BY DataOdbioru DESC;";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                // (2) Przygotowanie DataGridView
                dataGridViewKalendarz.SuspendLayout();
                dataGridViewKalendarz.AutoGenerateColumns = false;

                // Kolumny tworzymy TYLKO raz – jeśli jeszcze nie istnieją
                if (dataGridViewKalendarz.Columns.Count == 0)
                {
                    // ID (ukryte)
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        DataPropertyName = "ID",
                        Name = "ID",
                        Visible = false
                    });

                    // podstawowe
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        DataPropertyName = "DataOdbioru",
                        Name = "DataOdbioru",
                        HeaderText = "Data",
                        DefaultCellStyle = { Format = "yyyy-MM-dd" }
                    });
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        DataPropertyName = "Dostawca",
                        Name = "Dostawca",
                        HeaderText = "Dostawca"
                    });

                    // checkboxy
                    dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Utworzone", "Utworzone"));
                    dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Wysłane", "Wysłane"));
                    dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Otrzymane", "Otrzymane"));
                    dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Posrednik", "Pośrednik"));

                    // reszta
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Auta", Name = "Auta", HeaderText = "Aut" });
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztukiDek", Name = "SztukiDek", HeaderText = "Sztuki" });
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WagaDek", Name = "WagaDek", HeaderText = "Waga" });
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztSzuflada", Name = "SztSzuflada", HeaderText = "sztPoj" });

                    // kto/kiedy
                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoUtw", Name = "KtoUtw", HeaderText = "KtoUtw" });
                    var cKiedyUtw = new DataGridViewTextBoxColumn { DataPropertyName = "KiedyUtw", Name = "KiedyUtw", HeaderText = "KiedyUtw" };
                    cKiedyUtw.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    dataGridViewKalendarz.Columns.Add(cKiedyUtw);

                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoWysl", Name = "KtoWysl", HeaderText = "KtoWysl" });
                    var cKiedyWysl = new DataGridViewTextBoxColumn { DataPropertyName = "KiedyWysl", Name = "KiedyWysl", HeaderText = "KiedyWysl" };
                    cKiedyWysl.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    dataGridViewKalendarz.Columns.Add(cKiedyWysl);

                    dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "KtoOtrzym", Name = "KtoOtrzym", HeaderText = "KtoOtrzym" });
                    var cKiedyOtrzm = new DataGridViewTextBoxColumn { DataPropertyName = "KiedyOtrzm", Name = "KiedyOtrzm", HeaderText = "KiedyOtrzm" };
                    cKiedyOtrzm.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    dataGridViewKalendarz.Columns.Add(cKiedyOtrzm);

                    // Drobne dopasowania
                    if (dataGridViewKalendarz.Columns.Contains("DataOdbioru"))
                        dataGridViewKalendarz.Columns["DataOdbioru"].Width = 120;
                    if (dataGridViewKalendarz.Columns.Contains("Dostawca"))
                        dataGridViewKalendarz.Columns["Dostawca"].Width = 110;
                }

                // (3) Podstaw dane (DataView) – a filtr ustawimy zaraz niżej
                var dv = table.DefaultView;
                dataGridViewKalendarz.DataSource = dv;

                // (4) Reaplikuj łączony filtr (fraza + nieuzupełnione)
                ApplyCombinedFilter();
                dataGridViewKalendarz.ResumeLayout();
            }

            // (5) Opcjonalnie przywróć stan przewinięcia/selekt
            if (preserveState)
                RestoreGridState(state);
        }

        private void ApplyKalendarzFilter()
        {
            if (dataGridViewKalendarz.DataSource is DataView dv)
            {
                dv.RowFilter = nieUzupelnione.Checked
                    ? "[Utworzone] = false OR [Wysłane] = false OR [Otrzymane] = false"
                    : string.Empty;
            }
        }

        private static DataGridViewCheckBoxColumn MakeCheckColumn(string dataProperty, string header)
        {
            return new DataGridViewCheckBoxColumn
            {
                DataPropertyName = dataProperty,
                Name = dataProperty,
                HeaderText = header,
                ThreeState = false,
                TrueValue = true,
                FalseValue = false,
                IndeterminateValue = false,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
        }

        private void LoadDataGridPartie()
        {
            string query = @"
SELECT 
    p.[CreateData],
    p.[CustomerID], 
    p.[CustomerName],
    COUNT(*) AS Auta,
    ISNULL(s.Srednia, 0) AS Srednia,
    CASE 
        WHEN ISNULL(s.Srednia, 0) BETWEEN 1.80 AND 2.00 THEN 5808
        WHEN ISNULL(s.Srednia, 0) BETWEEN 2.01 AND 2.50 THEN 5280
        WHEN ISNULL(s.Srednia, 0) BETWEEN 2.51 AND 2.58 THEN 4752
        WHEN ISNULL(s.Srednia, 0) BETWEEN 2.59 AND 2.76 THEN 4488
        WHEN ISNULL(s.Srednia, 0) BETWEEN 2.77 AND 2.85 THEN 4224
        WHEN ISNULL(s.Srednia, 0) BETWEEN 2.86 AND 3.00 THEN 3960
        ELSE 0 
    END AS Sztuki,
    CONVERT(decimal(18, 2), ISNULL(s.Srednia, 0) * 
        CASE 
            WHEN ISNULL(s.Srednia, 0) BETWEEN 1.80 AND 2.00 THEN 5808
            WHEN ISNULL(s.Srednia, 0) BETWEEN 2.01 AND 2.50 THEN 5280
            WHEN ISNULL(s.Srednia, 0) BETWEEN 2.51 AND 2.58 THEN 4752
            WHEN ISNULL(s.Srednia, 0) BETWEEN 2.59 AND 2.76 THEN 4488
            WHEN ISNULL(s.Srednia, 0) BETWEEN 2.77 AND 2.85 THEN 4224
            WHEN ISNULL(s.Srednia, 0) BETWEEN 2.86 AND 3.00 THEN 3960
            ELSE 0 
        END
    ) AS SumaSztuka
FROM [LibraNet].[dbo].[PartiaDostawca] p
LEFT JOIN (
    SELECT 
        k.CreateData, 
        Partia.CustomerID, 
        CONVERT(decimal(18, 2), 
            (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22
        ) AS Srednia
    FROM [LibraNet].[dbo].[In0E] k
    JOIN [LibraNet].[dbo].[PartiaDostawca] Partia ON k.P1 = Partia.Partia
    WHERE ArticleID = 40 AND k.QntInCont > 4
    GROUP BY k.CreateData, Partia.CustomerID
) s ON p.CreateData = s.CreateData AND p.CustomerID = s.CustomerID
WHERE YEAR(p.[CreateData]) >= 2021
GROUP BY p.[CreateData], p.[CustomerID], p.[CustomerName], s.Srednia
ORDER BY p.[CustomerID], p.[CreateData] DESC;";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);
                dataGridViewPartie.AutoGenerateColumns = true;
                dataGridViewPartie.DataSource = table;
            }
        }

        private void ConfigureDataGridViewColumns()
        {
            // dopasowania drobne — opcjonalnie
            if (dataGridViewKalendarz.Columns.Contains("DataOdbioru"))
                dataGridViewKalendarz.Columns["DataOdbioru"].Width = 120;
            if (dataGridViewKalendarz.Columns.Contains("Dostawca"))
                dataGridViewKalendarz.Columns["Dostawca"].Width = 110;
        }

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || dgv != dataGridViewKalendarz) return;

            bool okU = GetBool(dgv, e.RowIndex, "Utworzone");
            bool okW = GetBool(dgv, e.RowIndex, "Wysłane");
            bool okO = GetBool(dgv, e.RowIndex, "Otrzymane");

            if (okU && okW && okO)
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Green;
                dgv.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.White;
            }
            else
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = dgv.AlternatingRowsDefaultCellStyle.BackColor;
                dgv.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Black;
            }
        }

        private static bool GetBool(DataGridView dgv, int rowIndex, string colName)
        {
            if (!dgv.Columns.Contains(colName)) return false;
            var val = dgv.Rows[rowIndex].Cells[colName]?.Value;
            if (val == null || val == DBNull.Value) return false;
            if (val is bool b) return b;
            bool.TryParse(val.ToString(), out var parsed);
            return parsed;
        }

        private void DataGridViewKalendarz_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewKalendarz.IsCurrentCellDirty &&
                dataGridViewKalendarz.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridViewKalendarz.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DataGridViewKalendarz_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = dataGridViewKalendarz;
            var col = grid.Columns[e.ColumnIndex];

            // tylko nasze checkboxy
            if (col.Name != "Utworzone" && col.Name != "Wysłane" && col.Name != "Otrzymane" && col.Name != "Posrednik")
                return;

            var row = grid.Rows[e.RowIndex];
            if (!grid.Columns.Contains("ID")) return;

            int id = Convert.ToInt32(row.Cells["ID"].Value);

            // wartość po kliknięciu (po commit'cie EditedFormattedValue jest docelową)
            bool newValue = Convert.ToBoolean(((DataGridViewCheckBoxCell)row.Cells[col.Name]).EditedFormattedValue);

            string msg = newValue
                ? $"Czy na pewno ustawić „{col.HeaderText}” = TAK dla pozycji ID={id}?"
                : $"Czy na pewno ustawić „{col.HeaderText}” = NIE dla pozycji ID={id}?";

            var confirm = MessageBox.Show(msg, "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                // cofka w UI
                row.Cells[col.Name].Value = !newValue;
                return;
            }

            // mapowanie pól kto/kiedy
            string ktoCol = null, kiedyCol = null;
            switch (col.Name)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
                case "Posrednik": /* brak pary kto/kiedy */                    break;
            }

            try
            {
                // 1) Zapis do bazy (bez reloadu)
                UpdateKalendarzFlag_NoReload(id, col.Name, newValue);

                // 2) Aktualizacja modelu danych podpiętego do siatki (DataRowView),
                //    aby RowFilter i UI od razu odzwierciedliły zmianę
                if (row.DataBoundItem is DataRowView drv)
                {
                    drv.BeginEdit();
                    drv.Row[col.Name] = newValue;

                    if (ktoCol != null)
                    {
                        int uid;
                        object ktoVal = (newValue && int.TryParse(UserID, out uid)) ? (object)uid : DBNull.Value;
                        object kiedyVal = newValue ? (object)DateTime.Now : DBNull.Value;

                        drv.Row[ktoCol] = ktoVal;
                        drv.Row[kiedyCol] = kiedyVal;
                    }
                    drv.EndEdit();
                }
                else
                {
                    // fallback – ustaw przynajmniej wartości w komórkach UI
                    row.Cells[col.Name].Value = newValue;
                    if (ktoCol != null && int.TryParse(UserID, out var uid2))
                    {
                        row.Cells[ktoCol].Value = newValue ? (object)uid2 : DBNull.Value;
                        row.Cells[kiedyCol].Value = newValue ? (object)DateTime.Now : DBNull.Value;
                    }
                }

                // 3) Przemaluj wiersz (kolorowanie w RowPrePaint)
                grid.InvalidateRow(e.RowIndex);

                // 4) JEŚLI filtr "nieuzupełnione" jest włączony i po zmianie wiersz przestaje spełniać warunek,
                //    DataView może go odfiltrować. Nie robi to przewinięcia na górę, ale dla pewności:
                //    zachowaj fokus na siatce, jeśli bieżąca komórka wyleci z widoku.
                if (grid.CurrentCell == null && grid.RowCount > 0)
                {
                    int r = Math.Min(e.RowIndex, grid.RowCount - 1);
                    int c = Math.Min(e.ColumnIndex, grid.ColumnCount - 1);
                    try { grid.CurrentCell = grid.Rows[r].Cells[c]; } catch { /* ignoruj */ }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // cofka w UI przy błędzie
                row.Cells[col.Name].Value = !newValue;
            }
        }

        private string BuildRowFilter()
        {
            string filterText = textBoxSearch.Text.Trim().Replace("'", "''");
            string baseFilter = string.IsNullOrEmpty(filterText)
                ? string.Empty
                : $"Dostawca LIKE '%{filterText}%' OR CONVERT(DataOdbioru, 'System.String') LIKE '%{filterText}%'";

            string nieUz = "[Utworzone] = false OR [Wysłane] = false OR [Otrzymane] = false";

            if (string.IsNullOrEmpty(baseFilter))
                return nieUzupelnione.Checked ? nieUz : string.Empty;

            return nieUzupelnione.Checked ? $"({baseFilter}) AND ({nieUz})" : baseFilter;
        }

        private void ApplyCombinedFilter()
        {
            if (dataGridViewKalendarz.DataSource is DataView dv)
                dv.RowFilter = BuildRowFilter();
        }

        private void UpdateKalendarzFlag_NoReload(int id, string columnName, bool value)
{
    string[] allowed = { "Utworzone", "Wysłane", "Otrzymane", "Posrednik" };
    if (Array.IndexOf(allowed, columnName) < 0)
        throw new InvalidOperationException("Nieobsługiwana kolumna: " + columnName);

    string? ktoCol = null, kiedyCol = null;
    switch (columnName)
    {
        case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
        case "Wysłane":   ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
        case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
    }

    int userIdInt = 0;
    if (value && ktoCol != null && !int.TryParse(UserID, out userIdInt))
        throw new InvalidOperationException("UserID musi być liczbą.");

    string sql =
        ktoCol == null
        ? $@"
UPDATE dbo.HarmonogramDostaw
SET [{columnName}] = @val
WHERE [LP] = @id;"
        : $@"
UPDATE dbo.HarmonogramDostaw
SET [{columnName}] = @val,
    [{ktoCol}]   = CASE WHEN @val = 1 THEN @kto ELSE NULL END,
    [{kiedyCol}] = CASE WHEN @val = 1 THEN GETDATE() ELSE NULL END
WHERE [LP] = @id;";

    using (var conn = new SqlConnection(connectionString))
    using (var cmd = new SqlCommand(sql, conn))
    {
        cmd.Parameters.AddWithValue("@val", value);
        cmd.Parameters.AddWithValue("@id", id);
        if (ktoCol != null) cmd.Parameters.AddWithValue("@kto", (object)userIdInt);

        conn.Open();
        int affected = cmd.ExecuteNonQuery();
        if (affected != 1)
            throw new Exception($"Zaktualizowano {affected} wierszy (oczekiwano 1).");
    }
}


        private void UpdateKalendarzFlag(int id, string columnName, bool value)
        {
            string[] allowed = { "Utworzone", "Wysłane", "Otrzymane", "Posrednik" };
            if (Array.IndexOf(allowed, columnName) < 0)
                throw new InvalidOperationException("Nieobsługiwana kolumna: " + columnName);

            // mapowanie kto/kiedy
            string? ktoCol = null, kiedyCol = null;
            switch (columnName)
            {
                case "Utworzone": ktoCol = "KtoUtw"; kiedyCol = "KiedyUtw"; break;
                case "Wysłane": ktoCol = "KtoWysl"; kiedyCol = "KiedyWysl"; break;
                case "Otrzymane": ktoCol = "KtoOtrzym"; kiedyCol = "KiedyOtrzm"; break;
                    // Posrednik — bez pary kto/kiedy
            }

            // jeśli ustawiamy na true i trzeba zapisać „kto”
            int userIdInt = 0;
            if (value && ktoCol != null && !int.TryParse(UserID, out userIdInt))
                throw new InvalidOperationException("UserID musi być liczbą (potrzebne do zapisania 'kto').");

            string sql =
                ktoCol == null
                ? $@"
UPDATE dbo.HarmonogramDostaw
SET [{columnName}] = @val
WHERE [LP] = @id;"
                : $@"
UPDATE dbo.HarmonogramDostaw
SET [{columnName}] = @val,
    [{ktoCol}]   = CASE WHEN @val = 1 THEN @kto ELSE NULL END,
    [{kiedyCol}] = CASE WHEN @val = 1 THEN GETDATE() ELSE NULL END
WHERE [LP] = @id;";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@val", value);
                cmd.Parameters.AddWithValue("@id", id);
                if (ktoCol != null)
                    cmd.Parameters.AddWithValue("@kto", (object)userIdInt);

                conn.Open();
                int affected = cmd.ExecuteNonQuery();
                if (affected != 1)
                    throw new Exception($"Zaktualizowano {affected} wierszy (oczekiwano 1).");
            }

            // Odśwież widok po zapisie, żeby zobaczyć od razu kolumny kto/kiedy
            LoadDataGridKalendarz();
        }

        private void CommandButton_Insert_Click(object sender, EventArgs e)
        {
            {
                if (dataGridViewKalendarz.CurrentRow == null)
                {
                    MessageBox.Show("Zaznacz pozycję w kalendarzu.");
                    return;
                }

                var cellVal = dataGridViewKalendarz.CurrentRow.Cells["ID"]?.Value;
                if (cellVal == null || cellVal == DBNull.Value)
                {
                    MessageBox.Show("Brak wartości LP (ID) w zaznaczonym wierszu.");
                    return;
                }

                string lp = cellVal.ToString()!;
                var form = new UmowyForm(initialLp: lp, initialIdLibra: null);
                form.UserID = App.UserID;

                form.FormClosed += (s, args) =>
                {
                    // opcjonalnie: tylko jeśli coś zapisano (np. form.HasSaved == true)
                    LoadDataGridKalendarz();
                };

                form.Show(this);
            }
        }

        private void nieUzupelnione_CheckedChanged(object sender, EventArgs e)
        {
            ApplyKalendarzFilter();
            ApplyCombinedFilter();
        }
        private (int firstRow, int? currentId, string? currentColName) CaptureGridState()
        {
            int first = -1;
            int? id = null;
            string? colName = null;

            if (dataGridViewKalendarz.RowCount > 0)
                first = dataGridViewKalendarz.FirstDisplayedScrollingRowIndex;

            if (dataGridViewKalendarz.CurrentRow != null && dataGridViewKalendarz.Columns.Contains("ID"))
                id = dataGridViewKalendarz.CurrentRow.Cells["ID"].Value as int?
                     ?? (int?)Convert.ToInt32(dataGridViewKalendarz.CurrentRow.Cells["ID"].Value);

            if (dataGridViewKalendarz.CurrentCell != null)
                colName = dataGridViewKalendarz.Columns[dataGridViewKalendarz.CurrentCell.ColumnIndex].Name;

            return (first, id, colName);
        }

        private void RestoreGridState((int firstRow, int? currentId, string? currentColName) state)
        {
            // 1) Przywróć przewinięcie
            if (state.firstRow >= 0 && state.firstRow < dataGridViewKalendarz.RowCount)
            {
                try { dataGridViewKalendarz.FirstDisplayedScrollingRowIndex = state.firstRow; }
                catch { /* ignoruj, jeśli siatka ma mniej wierszy */ }
            }

            // 2) Spróbuj wrócić do tego samego wiersza po ID
            if (state.currentId.HasValue && dataGridViewKalendarz.Columns.Contains("ID"))
            {
                int targetIndex = -1;
                foreach (DataGridViewRow r in dataGridViewKalendarz.Rows)
                {
                    if (r.Cells["ID"].Value != null &&
                        Convert.ToInt32(r.Cells["ID"].Value) == state.currentId.Value)
                    {
                        targetIndex = r.Index;
                        break;
                    }
                }

                if (targetIndex >= 0)
                {
                    int colIndex = 0;
                    if (!string.IsNullOrEmpty(state.currentColName) && dataGridViewKalendarz.Columns.Contains(state.currentColName))
                        colIndex = dataGridViewKalendarz.Columns[state.currentColName].Index;

                    try
                    {
                        dataGridViewKalendarz.CurrentCell = dataGridViewKalendarz.Rows[targetIndex].Cells[colIndex];
                        dataGridViewKalendarz.FirstDisplayedScrollingRowIndex =
                            Math.Max(0, Math.Min(dataGridViewKalendarz.FirstDisplayedScrollingRowIndex, targetIndex));
                    }
                    catch { /* bez paniki jeśli poza zakresem */ }
                }
            }
        }


        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyCombinedFilter();
        
        }
    }
}
