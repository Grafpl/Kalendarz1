using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokWaga : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public string TextBoxValue { get; set; }
        public WidokWaga()
        {
            InitializeComponent();
            DisplayDataInDataGridView();
        }
        public void SetTextBoxValue()
        {
            textBox1.Text = TextBoxValue;
        }
        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL
            string query = @"
    SELECT 
        k.CreateData AS Data,
        k.P1 AS Partia,
        Partia.CustomerName AS Dostawca,
        DATEDIFF(day, wk.DataWstawienia, hd.DataOdbioru) AS RoznicaDni,
        hd.WagaDek AS WagaDek,
        CONVERT(decimal(18, 2), (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) AS SredniaZywy,
        CONVERT(decimal(18, 2), ((15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) - hd.WagaDek) AS roznica,
        AVG(k.QntInCont) AS Srednia,
        CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2)) AS SredniaDokładna
    FROM 
        [LibraNet].[dbo].[In0E] k
    JOIN 
        [LibraNet].[dbo].[PartiaDostawca] Partia ON k.P1 = Partia.Partia
    LEFT JOIN 
        [LibraNet].[dbo].[HarmonogramDostaw] hd ON k.CreateData = hd.DataOdbioru AND Partia.CustomerName = hd.Dostawca
    LEFT JOIN 
        [LibraNet].[dbo].[WstawieniaKurczakow] wk ON hd.LpW = wk.Lp
    WHERE 
        k.ArticleID = 40 
        AND k.QntInCont > 4
    GROUP BY 
        k.CreateData, 
        k.P1, 
        Partia.CustomerName, 
        hd.WagaDek,
        wk.DataWstawienia,
        hd.DataOdbioru
    ORDER BY 
        k.P1 DESC, 
        k.CreateData DESC";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie DataTable jako DataSource dla DataGridView
                dataGridView1.DataSource = table;

                // Ustawienia kolumn
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    // Automatyczne zawijanie tekstu w nagłówkach kolumn
                    column.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                }

                // Ręczne dodawanie szerokości kolumn i formatowanie jednostek

                dataGridView1.Columns["RoznicaDni"].Width = 60;
                dataGridView1.Columns["RoznicaDni"].DefaultCellStyle.Format = "# 'dni'";

                dataGridView1.Columns["WagaDek"].Width = 60;
                dataGridView1.Columns["WagaDek"].DefaultCellStyle.Format = "#,##0.00 'kg'";

                dataGridView1.Columns["SredniaZywy"].Width = 60;
                dataGridView1.Columns["SredniaZywy"].DefaultCellStyle.Format = "#,##0.00 'kg'";

                dataGridView1.Columns["Srednia"].Width = 60;
                dataGridView1.Columns["Srednia"].DefaultCellStyle.Format = "#,##0 'poj'";

                dataGridView1.Columns["SredniaDokładna"].Width = 60;
                dataGridView1.Columns["SredniaDokładna"].DefaultCellStyle.Format = "#,##0.00 'poj'";

                dataGridView1.Columns["roznica"].Width = 60;
                dataGridView1.Columns["roznica"].DefaultCellStyle.Format = "#,##0.00 'kg'";



                // Ustawienie wysokości wierszy na minimalną wartość
                SetRowHeights(18);

            }
        }
        private void SetRowHeights(int height)
        {
            // Ustawienie wysokości wszystkich wierszy na określoną wartość
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                row.Height = height;
            }
        }


        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string filterText = textBox1.Text.Trim().ToLower();

            // Sprawdzenie, czy istnieje źródło danych dla DataGridView
            if (dataGridView1.DataSource is DataTable dataTable)
            {
                // Ustawienie filtra dla kolumny "Dostawca"
                dataTable.DefaultView.RowFilter = $"Dostawca LIKE '%{filterText}%'";

                // Przywrócenie pozycji kursora po zastosowaniu filtra
                if (dataGridView1.RowCount > 0)
                {
                    int currentPosition = dataGridView1.FirstDisplayedScrollingRowIndex;
                    if (currentPosition >= 0 && currentPosition < dataGridView1.RowCount)
                    {
                        dataGridView1.FirstDisplayedScrollingRowIndex = currentPosition;
                    }
                }
            }
        }


    }
}
