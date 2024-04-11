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
            DateTime startDate = dateTimePicker1.Value.Date;
            string query = "SELECT Auta, Dostawca, WagaDek, SztSzuflada FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @StartDate AND Bufor = 'Potwierdzony'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", startDate);

                SqlDataAdapter adapter = new SqlDataAdapter(command);

                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie nowej kolumny do przechowywania numerów wierszy
                table.Columns.Add("Numer", typeof(int));

                // Iteracja przez wszystkie wiersze i przypisanie numerów
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    table.Rows[i]["Numer"] = i + 1;
                }

                // Wywołanie funkcji dublowania wierszy
                DuplicateRows(table);

                // Dodanie pozostałych kolumn do tabeli finalTable
                DataTable finalTable = new DataTable();
                finalTable.Columns.Add("Numer", typeof(int));
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

                // Kopiowanie danych z tabeli źródłowej do docelowej
                foreach (DataRow row in table.Rows)
                {
                    DataRow newRow = finalTable.NewRow();
                    newRow["Numer"] = row["Numer"]; // Skopiuj numer wiersza
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

                dataGridView1.DataSource = finalTable;
            }
        }

        // Funkcja do dublowania wierszy
        // Funkcja do dublowania wierszy
        private void DuplicateRows(DataTable table)
        {
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                DataRow row = table.Rows[rowIndex];

                int auta = Convert.ToInt32(row["Auta"]);
                string dostawca = row["Dostawca"].ToString();
                double wagaDek = Convert.ToDouble(row["WagaDek"]);
                int sztSzuflada = Convert.ToInt32(row["SztSzuflada"]);

                for (int i = 1; i < auta; i++) // Zaczynamy od 1, bo pierwszy wiersz już istnieje
                {
                    DataRow newRow = table.NewRow();
                    newRow.ItemArray = row.ItemArray; // Skopiuj dane z istniejącego wiersza

                    // Ustaw numer wiersza
                    newRow["Numer"] = DBNull.Value; // Lub pozostaw go pustym, jeśli nie jest potrzebny

                    table.Rows.Add(newRow);
                }
            }
        }




        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            DisplayData();
        }
    }
}
