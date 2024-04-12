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
    public partial class WidokMatryca : Form

    {
        private bool dragging = false;
        private int rowIndexFromMouseDown;
        private DataGridViewRow draggedRow;
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public WidokMatryca()
        {
            InitializeComponent();
            DisplayData();
        }
        private void DisplayData()
        {
            // Tworzenie połączenia z bazą danych i pobieranie danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Tworzenie komendy SQL
                string query = "SELECT Auta, Dostawca, WagaDek, SztSzuflada FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @StartDate AND Bufor = 'Potwierdzony'";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);

                // Tworzenie adaptera danych i wypełnianie DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie pozostałych kolumn do tabeli finalTable
                DataTable finalTable = new DataTable();
                finalTable.Columns.Add("Numer", typeof(int)); // Dodanie kolumny "Numer" na początku
                finalTable.Columns.Add("Auta", typeof(int));
                finalTable.Columns.Add("Dostawca", typeof(string));
                finalTable.Columns.Add("WagaDek", typeof(double));
                finalTable.Columns.Add("SztSzuflada", typeof(int));
                finalTable.Columns.Add("Kierowca", typeof(string)); // Kolumna dla kierowcy
                finalTable.Columns.Add("Pojazd", typeof(string)); // Kolumna dla pojazdu
                finalTable.Columns.Add("Naczepa", typeof(string)); // Kolumna dla naczepy
                finalTable.Columns.Add("Wyjazd", typeof(DateTime)); // Kolumna dla wyjazdu
                finalTable.Columns.Add("Zaladunek", typeof(DateTime)); // Kolumna dla załadunku
                finalTable.Columns.Add("Powrot", typeof(DateTime)); // Kolumna dla powrotu
                finalTable.Columns.Add("Wozek", typeof(string)); // Kolumna dla wózka

                int numer = 1; // Początkowa wartość numeru

                // Iteracja przez wiersze tabeli źródłowej
                foreach (DataRow row in table.Rows)
                {
                    int autaValue = (int)row["Auta"];

                    // Duplikowanie wiersza tyle razy, ile wynosi wartość w kolumnie Auta
                    for (int i = 0; i < autaValue; i++)
                    {
                        DataRow newRow = finalTable.NewRow();
                        newRow["Numer"] = numer++; // Ustaw numer i zwiększ wartość
                        newRow["Auta"] = row["Auta"];
                        newRow["Dostawca"] = row["Dostawca"];
                        newRow["WagaDek"] = row["WagaDek"];
                        newRow["SztSzuflada"] = row["SztSzuflada"];
                        newRow["Kierowca"] = ""; // Pozostaw kolumnę Kierowca pustą
                        newRow["Pojazd"] = ""; // Pozostaw kolumnę Pojazd pustą
                        newRow["Naczepa"] = ""; // Pozostaw kolumnę Naczepa pustą
                        newRow["Wyjazd"] = DBNull.Value; // Pozostaw kolumnę Wyjazd pustą
                        newRow["Zaladunek"] = DBNull.Value; // Pozostaw kolumnę Zaladunek pustą
                        newRow["Powrot"] = DBNull.Value; // Pozostaw kolumnę Powrot pustą
                        newRow["Wozek"] = ""; // Pozostaw kolumnę Wozek pustą

                        finalTable.Rows.Add(newRow);
                    }
                }

                // Ustawienie źródła danych dla DataGridView
                dataGridView1.DataSource = finalTable;

                // Tworzenie DataGridViewComboBoxColumn dla kolumny "Kierowca"
                DataGridViewComboBoxColumn comboBoxColumn = new DataGridViewComboBoxColumn();
                comboBoxColumn.Name = "Kierowca";
                comboBoxColumn.HeaderText = "Kierowca";
                comboBoxColumn.DataPropertyName = "Kierowca";

                // Pobranie unikalnych wartości kierowców z bazy danych i dodanie ich do ComboBoxa w kolumnie "Kierowca"
                connection.Open();
                SqlCommand cmd = new SqlCommand("SELECT Name FROM [LibraNet].[dbo].[Driver]", connection);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    comboBoxColumn.Items.Add(reader["Name"].ToString());
                }
                reader.Close();
                connection.Close();

                // Dodanie kolumny ComboBox do DataGridView
                dataGridView1.Columns.Add(comboBoxColumn);

            }
        }

        private int selectedRowIndex = -1; // Dodaj zmienną do przechowywania indeksu zaznaczonego wiersza

        // Metoda do przesuwania wiersza w górę
        // Metoda do przesuwania wiersza w górę
        private void MoveRowUp()
        {
            int rowIndex = dataGridView1.CurrentCell.RowIndex;
            if (rowIndex > 0)
            {
                DataTable table = (DataTable)dataGridView1.DataSource;
                DataRow row = table.NewRow();
                row.ItemArray = table.Rows[rowIndex].ItemArray;
                table.Rows.RemoveAt(rowIndex);
                table.Rows.InsertAt(row, rowIndex - 1);
                RefreshNumeration();
                dataGridView1.Rows[rowIndex - 1].Selected = true; // Zaznacz przesunięty wiersz
                dataGridView1.Rows[rowIndex].Selected = false; // Odznacz poprzedni zaznaczony wiersz
                dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex - 1].Cells[0]; // Ustawienie aktywnej komórki na pierwszą kolumnę przesuniętego wiersza
                selectedRowIndex = rowIndex - 1; // Zaktualizuj indeks zaznaczonego wiersza
            }
        }

        // Metoda do przesuwania wiersza w dół
        private void MoveRowDown()
        {
            int rowIndex = dataGridView1.CurrentCell.RowIndex;
            if (rowIndex < dataGridView1.Rows.Count - 1)
            {
                DataTable table = (DataTable)dataGridView1.DataSource;
                DataRow row = table.NewRow();
                row.ItemArray = table.Rows[rowIndex].ItemArray;
                table.Rows.RemoveAt(rowIndex);
                table.Rows.InsertAt(row, rowIndex + 1);
                RefreshNumeration();
                dataGridView1.Rows[rowIndex + 1].Selected = true; // Zaznacz przesunięty wiersz
                dataGridView1.Rows[rowIndex].Selected = false; // Odznacz poprzedni zaznaczony wiersz
                dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex + 1].Cells[0]; // Ustawienie aktywnej komórki na pierwszą kolumnę przesuniętego wiersza
                selectedRowIndex = rowIndex + 1; // Zaktualizuj indeks zaznaczonego wiersza
            }
        }
        // Metoda odświeżająca numerację wierszy
        private void RefreshNumeration()
        {
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                dataGridView1.Rows[i].Cells["Numer"].Value = i + 1;
            }
        }
        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            DisplayData();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MoveRowUp();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MoveRowDown();
        }
    }
}
