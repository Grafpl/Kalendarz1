using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class SprawdzalkaUmow : Form
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public SprawdzalkaUmow()
        {
            InitializeComponent();

            ConfigureDataGridView(dataGridViewKalendarz);
            ConfigureDataGridView(dataGridViewPartie);

            // Ważne dla klików w checkbox — commit od razu po kliknięciu
            dataGridViewKalendarz.CurrentCellDirtyStateChanged += DataGridViewKalendarz_CurrentCellDirtyStateChanged;
            dataGridViewKalendarz.CellContentClick += DataGridViewKalendarz_CellContentClick;

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
            dgv.EditMode = DataGridViewEditMode.EditOnEnter; // pozwala łatwiej klikać w checkboxy
        }

        private void LoadDataGridKalendarz()
        {
            // Pobieramy także ID i Posrednik
            string query = @"
                SELECT 
                    [LP] AS ID,
                    [DataOdbioru],
                    [Dostawca],
                    CAST([Utworzone] AS bit) AS Utworzone,
                    CAST([Wysłane]   AS bit) AS Wysłane,
                    CAST([Otrzymane] AS bit) AS Otrzymane,
                    CAST(ISNULL([Posrednik],0) AS bit) AS Posrednik,
                    [Auta],
                    [SztukiDek],
                    [WagaDek],
                    [SztSzuflada]
                FROM [LibraNet].[dbo].[HarmonogramDostaw]
                WHERE Bufor = 'Potwierdzony' 
                  AND DataOdbioru BETWEEN '2021-01-01' AND DATEADD(DAY, 2, GETDATE())
                ORDER BY DataOdbioru DESC;";

            using (var connection = new SqlConnection(connectionString))
            using (var adapter = new SqlDataAdapter(query, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                // Budujemy kolumny ręcznie, aby checkboxy były checkboxami, a ID ukryte
                dataGridViewKalendarz.AutoGenerateColumns = false;
                dataGridViewKalendarz.Columns.Clear();

                // ID (ukryte)
                var colId = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "ID",
                    Name = "ID",
                    Visible = false
                };
                dataGridViewKalendarz.Columns.Add(colId);

                // Tekstowe/numericzne
                dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DataOdbioru", Name = "DataOdbioru", HeaderText = "Data" });
                dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Dostawca", Name = "Dostawca", HeaderText = "Dostawca" });

                // Checkboxy
                dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Utworzone", "Utworzone"));
                dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Wysłane", "Wysłane"));
                dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Otrzymane", "Otrzymane"));
                dataGridViewKalendarz.Columns.Add(MakeCheckColumn("Posrednik", "Pośrednik"));

                // Reszta
                dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Auta", Name = "Auta", HeaderText = "Aut" });
                dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztukiDek", Name = "SztukiDek", HeaderText = "Sztuki" });
                dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WagaDek", Name = "WagaDek", HeaderText = "Waga" });
                dataGridViewKalendarz.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SztSzuflada", Name = "SztSzuflada", HeaderText = "sztPoj" });

                dataGridViewKalendarz.DataSource = table;
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
                dataGridViewPartie.AutoGenerateColumns = true; // tu może być auto
                dataGridViewPartie.DataSource = table;
            }
        }

        private void ConfigureDataGridViewColumns()
        {
            // KALENDARZ
            if (dataGridViewKalendarz.Columns.Contains("DataOdbioru"))
            {
                dataGridViewKalendarz.Columns["DataOdbioru"].HeaderText = "Data";
                dataGridViewKalendarz.Columns["DataOdbioru"].Width = 120;
            }
            if (dataGridViewKalendarz.Columns.Contains("Dostawca"))
            {
                dataGridViewKalendarz.Columns["Dostawca"].HeaderText = "Dostawca";
                dataGridViewKalendarz.Columns["Dostawca"].Width = 110;
            }
            if (dataGridViewKalendarz.Columns.Contains("Utworzone"))
                dataGridViewKalendarz.Columns["Utworzone"].Width = 90;
            if (dataGridViewKalendarz.Columns.Contains("Wysłane"))
                dataGridViewKalendarz.Columns["Wysłane"].Width = 90;
            if (dataGridViewKalendarz.Columns.Contains("Otrzymane"))
                dataGridViewKalendarz.Columns["Otrzymane"].Width = 90;
            if (dataGridViewKalendarz.Columns.Contains("Posrednik"))
                dataGridViewKalendarz.Columns["Posrednik"].Width = 95;

            if (dataGridViewKalendarz.Columns.Contains("Auta"))
            {
                dataGridViewKalendarz.Columns["Auta"].HeaderText = "Aut";
                dataGridViewKalendarz.Columns["Auta"].Width = 65;
            }
            if (dataGridViewKalendarz.Columns.Contains("SztukiDek"))
            {
                dataGridViewKalendarz.Columns["SztukiDek"].HeaderText = "Sztuki";
                dataGridViewKalendarz.Columns["SztukiDek"].Width = 120;
            }
            if (dataGridViewKalendarz.Columns.Contains("WagaDek"))
            {
                dataGridViewKalendarz.Columns["WagaDek"].HeaderText = "Waga";
                dataGridViewKalendarz.Columns["WagaDek"].Width = 90;
            }
            if (dataGridViewKalendarz.Columns.Contains("SztSzuflada"))
            {
                dataGridViewKalendarz.Columns["SztSzuflada"].HeaderText = "sztPoj";
                dataGridViewKalendarz.Columns["SztSzuflada"].Width = 70;
            }

            // PARTIE
            if (dataGridViewPartie.Columns.Contains("CreateData"))
            {
                dataGridViewPartie.Columns["CreateData"].HeaderText = "Data";
                dataGridViewPartie.Columns["CreateData"].Width = 120;
            }
            if (dataGridViewPartie.Columns.Contains("CustomerID"))
            {
                dataGridViewPartie.Columns["CustomerID"].HeaderText = "ID";
                dataGridViewPartie.Columns["CustomerID"].Width = 100;
            }
            if (dataGridViewPartie.Columns.Contains("CustomerName"))
            {
                dataGridViewPartie.Columns["CustomerName"].HeaderText = "Dostawca";
                dataGridViewPartie.Columns["CustomerName"].Width = 110;
            }
            if (dataGridViewPartie.Columns.Contains("Auta"))
            {
                dataGridViewPartie.Columns["Auta"].HeaderText = "Aut";
                dataGridViewPartie.Columns["Auta"].Width = 65;
            }
            if (dataGridViewPartie.Columns.Contains("Srednia"))
            {
                dataGridViewPartie.Columns["Srednia"].HeaderText = "Waga";
                dataGridViewPartie.Columns["Srednia"].Width = 55;
            }
            if (dataGridViewPartie.Columns.Contains("Sztuki"))
            {
                dataGridViewPartie.Columns["Sztuki"].HeaderText = "Sztuki";
                dataGridViewPartie.Columns["Sztuki"].Width = 100;
            }
            if (dataGridViewPartie.Columns.Contains("SumaSztuka"))
            {
                dataGridViewPartie.Columns["SumaSztuka"].HeaderText = "Suma Sztuk";
                dataGridViewPartie.Columns["SumaSztuka"].Width = 100;
            }
        }

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || dgv != dataGridViewKalendarz) return;

            // Kolorujemy tylko siatkę Kalendarz
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
                // reset na wypadek przewijania / recyklingu wierszy
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

        // Commit edycji natychmiast po kliknięciu w checkbox
        private void DataGridViewKalendarz_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewKalendarz.IsCurrentCellDirty &&
                dataGridViewKalendarz.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridViewKalendarz.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // Logika aktualizacji DB z potwierdzeniem
        private void DataGridViewKalendarz_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = dataGridViewKalendarz;
            var col = grid.Columns[e.ColumnIndex];

            // Reagujemy tylko na nasze checkboxy
            if (col.Name != "Utworzone" && col.Name != "Wysłane" && col.Name != "Otrzymane" && col.Name != "Posrednik")
                return;

            var row = grid.Rows[e.RowIndex];
            if (!grid.Columns.Contains("ID")) return;

            int id = Convert.ToInt32(row.Cells["ID"].Value);

            // Docelowa wartość po kliknięciu (stan po zmianie w komórce)
            bool newValue = Convert.ToBoolean(((DataGridViewCheckBoxCell)row.Cells[col.Name]).EditedFormattedValue);

            string msg = newValue
                ? $"Czy na pewno ustawić „{col.HeaderText}” = TAK dla pozycji ID={id}?"
                : $"Czy na pewno ustawić „{col.HeaderText}” = NIE dla pozycji ID={id}?";

            var confirm = MessageBox.Show(msg, "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                // Cofnij zmianę w UI
                row.Cells[col.Name].Value = !newValue;
                return;
            }

            try
            {
                UpdateKalendarzFlag(id, col.Name, newValue);

                // Odśwież lokalnie wiersz (jeśli masz triggery itp., możesz zrobić reload całej tabeli)
                row.Cells[col.Name].Value = newValue;

                // Opcjonalnie: jeśli wszystkie 3 = true, podświetl na zielono
                grid.InvalidateRow(e.RowIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // cofnij zmianę UI w razie błędu
                row.Cells[col.Name].Value = !newValue;
            }
        }

        private void UpdateKalendarzFlag(int id, string columnName, bool value)
        {
            // Whitelist — zabezpieczenie przed SQL injection nazwą kolumny
            string[] allowed = { "Utworzone", "Wysłane", "Otrzymane", "Posrednik" };
            if (Array.IndexOf(allowed, columnName) < 0)
                throw new InvalidOperationException("Nieobsługiwana kolumna: " + columnName);

            string sql = $@"
UPDATE [LibraNet].[dbo].[HarmonogramDostaw]
SET [{columnName}] = @val
WHERE [LP] = @id;";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@val", value);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                int affected = cmd.ExecuteNonQuery();
                if (affected != 1)
                    throw new Exception($"Zaktualizowano {affected} wierszy (oczekiwano 1).");
            }
        }

        // Nie używane, ale możesz dopiąć inne zdarzenia
        private void dataGridViewKalendarz_CellContentClick(object sender, DataGridViewCellEventArgs e) { }

        private void CommandButton_Insert_Click(object sender, EventArgs e)
        {
            // Wyświetlanie formy Dostawa
            UmowyForm umowyForm = new UmowyForm();
            umowyForm.Show();
        }
    }
}
