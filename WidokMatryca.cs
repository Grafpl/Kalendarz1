using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Controls;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Kalendarz1
{
    public partial class WidokMatryca : Form

    {
        private bool dragging = false;
        private int rowIndexFromMouseDown;
        private DataGridViewRow draggedRow;
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

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
                string query = "SELECT Lp, Auta, Dostawca, WagaDek, SztSzuflada FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @StartDate AND Bufor = 'Potwierdzony'";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", dateTimePicker1.Value.Date);

                // Tworzenie adaptera danych i wypełnianie DataTable
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                // Dodanie pozostałych kolumn do tabeli finalTable
                DataTable finalTable = new DataTable();
                finalTable.Columns.Add("Nr", typeof(int)); // Dodanie kolumny "Numer" na początku
                finalTable.Columns.Add("Lp", typeof(int)); // Dodanie kolumny "Numer" na początku
                finalTable.Columns.Add("Auta", typeof(int));
                finalTable.Columns.Add("Dostawca", typeof(string));
                finalTable.Columns.Add("WagaDek", typeof(double));
                finalTable.Columns.Add("SztSzuflada", typeof(int));
                finalTable.Columns.Add("Kierowca", typeof(string)); // Kolumna dla kierowcy
                finalTable.Columns.Add("Pojazd", typeof(string)); // Kolumna dla pojazdu
                finalTable.Columns.Add("Naczepa", typeof(string)); // Kolumna dla naczepy
                finalTable.Columns.Add("Wyjazd", typeof(string)); // Kolumna dla wyjazdu (zmieniona na string)
                finalTable.Columns.Add("Zaladunek", typeof(string)); // Kolumna dla załadunku
                finalTable.Columns.Add("Powrot", typeof(string)); // Kolumna dla powrotu
                finalTable.Columns.Add("Przyjazd", typeof(string)); // Kolumna dla przyjazdu (zmieniona na TimeSpan)
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
                        newRow["Nr"] = numer++; // Ustaw numer i zwiększ wartość
                        newRow["Lp"] = row["Lp"];
                        newRow["Auta"] = row["Auta"];
                        newRow["Dostawca"] = row["Dostawca"];
                        newRow["WagaDek"] = row["WagaDek"];
                        newRow["SztSzuflada"] = row["SztSzuflada"];
                        newRow["Kierowca"] = ""; // Pozostaw kolumnę Kierowca pustą
                        newRow["Pojazd"] = ""; // Pozostaw kolumnę Pojazd pustą
                        newRow["Naczepa"] = ""; // Pozostaw kolumnę Naczepa pustą
                        newRow["Wyjazd"] = "00:00"; // Ustaw domyślną wartość dla kolumny Wyjazd
                        newRow["Zaladunek"] = "00:00"; // Pozostaw kolumnę Zaladunek pustą
                        newRow["Powrot"] = "00:00"; // Pozostaw kolumnę Powrot pustą
                        newRow["Przyjazd"] = "00:00"; // Inicjalizacja wartości kolumny "Przyjazd" jako pustej
                        newRow["Wozek"] = ""; // Pozostaw kolumnę Wozek pustą

                        finalTable.Rows.Add(newRow);
                    }
                }

                // Ustawienie źródła danych dla DataGridView
                dataGridView1.DataSource = finalTable;

                // Automatyczne dopasowanie szerokości kolumn
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                }

                // Dodanie kolumny DataGridViewComboBoxColumn dla kolumny "Kierowca"
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

                // Ustawienie zawsze ":" w kolumnie "Wyjazd"
                dataGridView1.CellFormatting += (sender, e) =>
                {
                    // Sprawdź, czy aktualna kolumna to "Wyjazd", "Zaladunek", "Powrot" lub "Przyjazd"
                    if ((dataGridView1.Columns.Contains("Wyjazd") && e.ColumnIndex == dataGridView1.Columns["Wyjazd"].Index) ||
                        (dataGridView1.Columns.Contains("Zaladunek") && e.ColumnIndex == dataGridView1.Columns["Zaladunek"].Index) ||
                        (dataGridView1.Columns.Contains("Powrot") && e.ColumnIndex == dataGridView1.Columns["Powrot"].Index) ||
                        (dataGridView1.Columns.Contains("Przyjazd") && e.ColumnIndex == dataGridView1.Columns["Przyjazd"].Index))
                    {
                        // Sprawdź, czy wartość komórki nie jest pusta
                        if (e.Value != null)
                        {
                            string value = e.Value.ToString();
                            if (value.Length == 3)
                            {
                                // Dodaj "0" na początku i ":" na 3. miejscu
                                e.Value = "0" + value.Substring(0, 1) + ":" + value.Substring(1);
                            }
                            else if (value.Length == 4)
                            {
                                // Wstaw ":" w 3. miejscu
                                e.Value = value.Substring(0, 2) + ":" + value.Substring(2);
                            }
                        }
                    }
                };

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
                dataGridView1.Rows[i].Cells["Nr"].Value = i + 1;
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

        private void button3_Click(object sender, EventArgs e)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (!row.IsNewRow) // Pomijamy wiersz tworzący nowe rekordy
                    {

                        string sql = "INSERT INTO dbo.FarmerCalc (ID, CalcDate, CustomerGID, DriverGID, CarLp, SztPoj, WagaDek, PriceTypeID, CarID, TrailerID, NotkaWozek, LpDostawy) " +
                            "VALUES (@ID, @Date, @Dostawca, @Kierowca, @Nr, @SztPoj, @WagaDek, @Cena, @Ciagnik, @Naczepa, @Wozek, @LpDostawy)";
                        // Pobierz dane z wiersza DataGridView
                        string Dostawca = row.Cells["Dostawca"].Value.ToString();
                        string Kierowca = row.Cells["Kierowca"].Value.ToString();
                        string LpDostawy = row.Cells["Lp"].Value.ToString();
                        string Nr = row.Cells["Nr"].Value.ToString();
                        string Przyjazd = row.Cells["Przyjazd"].Value.ToString();
                        string SztPoj = row.Cells["SztSzuflada"].Value.ToString();
                        string WagaDek = row.Cells["WagaDek"].Value.ToString();
                        string Ciagnik = row.Cells["Pojazd"].Value.ToString();
                        string Naczepa = row.Cells["Naczepa"].Value.ToString();
                        string Wozek = row.Cells["Wozek"].Value.ToString();

                        string Cena = row.Cells["Wozek"].Value.ToString();
                        int CenaInt = zapytaniasql.ZnajdzIdCeny(Cena);


                        // Znajdź ID kierowcy i dostawcy
                        int userId = zapytaniasql.ZnajdzIdKierowcy(Kierowca);
                        int userId2 = zapytaniasql.ZnajdzIdHodowcy(Dostawca);
                        
                        // Znajdź największe ID w tabeli FarmerCalc
                        long maxLP;
                        string maxLPSql = "SELECT MAX(ID) AS MaxLP FROM dbo.[FarmerCalc];";
                        using (SqlCommand command = new SqlCommand(maxLPSql, conn))
                        {
                            object result = command.ExecuteScalar();
                            maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                        }
                        

                        // Wstaw dane do tabeli FarmerCalc
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", maxLP);
                            cmd.Parameters.AddWithValue("@Dostawca", userId2);
                            cmd.Parameters.AddWithValue("@Kierowca", userId);
                            cmd.Parameters.AddWithValue("@LpDostawy", string.IsNullOrEmpty(LpDostawy) ? (object)DBNull.Value : LpDostawy);
                            cmd.Parameters.AddWithValue("@Nr", string.IsNullOrEmpty(Nr) ? (object)DBNull.Value : Nr);
                            cmd.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj) ? (object)DBNull.Value : decimal.Parse(SztPoj));
                            cmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(WagaDek) ? (object)DBNull.Value : decimal.Parse(WagaDek));
                            cmd.Parameters.AddWithValue("@Date", dateTimePicker1.Value.Date);
                            cmd.Parameters.AddWithValue("@Cena", CenaInt);
                            cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik) ? (object)DBNull.Value : Ciagnik);
                            cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa) ? (object)DBNull.Value : Naczepa);
                            cmd.Parameters.AddWithValue("@Wozek", string.IsNullOrEmpty(Wozek) ? (object)DBNull.Value : Wozek);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }
}
