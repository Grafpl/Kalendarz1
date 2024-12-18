using System;
using System.Data;
using System.Data.SqlClient;
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

            LoadDataGridKalendarz();
            LoadDataGridPartie();
            ConfigureDataGridViewColumns();

        }

        private void ConfigureDataGridView(DataGridView dgv)
        {
            // Automatyczne dopasowanie kolumn
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            // Akcenty szaro-białe
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.LightGray;

            // Czcionka i styl
            dgv.DefaultCellStyle.SelectionBackColor = Color.DarkGray;
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            // Wyłączenie zmiany wysokości wierszy
            dgv.AllowUserToResizeRows = false;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;

            // Dodanie zdarzenia do zmiany koloru wierszy
            dgv.RowPrePaint += Dgv_RowPrePaint;
        }

        private void LoadDataGridKalendarz()
        {
            string query = @"
                SELECT 
                    [DataOdbioru],
                    [Dostawca],
                    [Utworzone],
                    [Wysłane],
                    [Otrzymane],
                    [Auta],
                    [SztukiDek],
                    [WagaDek],
                    [SztSzuflada]
                FROM [LibraNet].[dbo].[HarmonogramDostaw]
                WHERE Bufor = 'Potwierdzony' 
                    AND DataOdbioru BETWEEN '2021-01-01' AND DATEADD(DAY, 2, GETDATE())
                ORDER BY DataOdbioru DESC";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                dataGridViewKalendarz.DataSource = table;
            }
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
ORDER BY p.[CustomerID], p.[CreateData] DESC;
";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable table = new DataTable();
                adapter.Fill(table);
                dataGridViewPartie.DataSource = table;
            }
        }
        private void ConfigureDataGridViewColumns()
        {
            // Konfiguracja dla dataGridViewKalendarz
            dataGridViewKalendarz.Columns["DataOdbioru"].HeaderText = "Data";
            dataGridViewKalendarz.Columns["DataOdbioru"].Width = 120;

            dataGridViewKalendarz.Columns["Dostawca"].HeaderText = "Dostawca";
            dataGridViewKalendarz.Columns["Dostawca"].Width = 110;

            dataGridViewKalendarz.Columns["Utworzone"].HeaderText = "Utworzone";
            dataGridViewKalendarz.Columns["Utworzone"].Width = 100;

            dataGridViewKalendarz.Columns["Wysłane"].HeaderText = "Wysłane";
            dataGridViewKalendarz.Columns["Wysłane"].Width = 100;

            dataGridViewKalendarz.Columns["Otrzymane"].HeaderText = "Otrzymane";
            dataGridViewKalendarz.Columns["Otrzymane"].Width = 100;

            dataGridViewKalendarz.Columns["Auta"].HeaderText = "Aut";
            dataGridViewKalendarz.Columns["Auta"].Width = 65;

            dataGridViewKalendarz.Columns["SztukiDek"].HeaderText = "Sztuki";
            dataGridViewKalendarz.Columns["SztukiDek"].Width = 120;

            dataGridViewKalendarz.Columns["WagaDek"].HeaderText = "Waga";
            dataGridViewKalendarz.Columns["WagaDek"].Width = 90;

            dataGridViewKalendarz.Columns["SztSzuflada"].HeaderText = "sztPoj";
            dataGridViewKalendarz.Columns["SztSzuflada"].Width = 50;

            // Konfiguracja dla dataGridViewPartie
            dataGridViewPartie.Columns["CreateData"].HeaderText = "Data";
            dataGridViewPartie.Columns["CreateData"].Width = 120;

            dataGridViewPartie.Columns["CustomerID"].HeaderText = "ID";
            dataGridViewPartie.Columns["CustomerID"].Width = 100;

            dataGridViewPartie.Columns["CustomerName"].HeaderText = "Dostawca";
            dataGridViewPartie.Columns["CustomerName"].Width = 110;

            dataGridViewPartie.Columns["Auta"].HeaderText = "Aut";
            dataGridViewPartie.Columns["Auta"].Width = 65;

            dataGridViewPartie.Columns["Srednia"].HeaderText = "Waga";
            dataGridViewPartie.Columns["Srednia"].Width = 55;

            dataGridViewPartie.Columns["Sztuki"].HeaderText = "Sztuki";
            dataGridViewPartie.Columns["Sztuki"].Width = 100;

            dataGridViewPartie.Columns["SumaSztuka"].HeaderText = "Suma Sztuk";
            dataGridViewPartie.Columns["SumaSztuka"].Width = 100;
        }


        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;

            if (dgv == null) return;

            if (dgv.Columns.Contains("Utworzone") &&
                dgv.Columns.Contains("Wysłane") &&
                dgv.Columns.Contains("Otrzymane"))
            {
                bool utworzone = dgv.Rows[e.RowIndex].Cells["Utworzone"]?.Value?.ToString() == "True";
                bool wyslane = dgv.Rows[e.RowIndex].Cells["Wysłane"]?.Value?.ToString() == "True";
                bool otrzymane = dgv.Rows[e.RowIndex].Cells["Otrzymane"]?.Value?.ToString() == "True";

                if (utworzone && wyslane && otrzymane)
                {
                    // Ustaw kolor tła i czcionki
                    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Green;
                    dgv.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.White;
                }
            }
        }

    }
}
