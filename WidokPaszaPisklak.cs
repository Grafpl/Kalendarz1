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
using static Azure.Core.HttpHeader;

namespace Kalendarz1
{
    public partial class WidokPaszaPisklak : Form
    {
        static string connectionPermission = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public string TextBoxValue { get; set; }
        public WidokPaszaPisklak()
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
            // Zapytanie SQL zależne od stanu checkboxa
            string query = @"
            /****** Script for SelectTopNRows command from SSMS ******/
            SELECT
                ST.Shortcut,
                DK.walBrutto,
                DK.kod,
                DK.Data
            FROM
                [HANDEL].[HM].[DK] DK
            JOIN
                [HANDEL].[SSCommon].[STContractors] ST ON DK.khid = ST.ID
            WHERE 
                DK.seria = 'sFPP'
                AND DK.ok = 0
            ORDER BY 
                ST.Shortcut ASC";

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

                // Ustawienie wysokości wierszy na minimalną wartość
                SetRowHeights(18);
            }
        }

        // Metoda do ustawienia wysokości wierszy
        private void SetRowHeights(int height)
        {
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
                dataTable.DefaultView.RowFilter = $"Shortcut LIKE '%{filterText}%'";

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
