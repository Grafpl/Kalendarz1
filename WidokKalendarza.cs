using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using System.Drawing; // Dodaj tę dyrektywę

namespace Kalendarz1
{
    public partial class WidokKalendarza : Form
    {
        // kod
    }
}


namespace Kalendarz1
{
    public partial class WidokKalendarza : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public WidokKalendarza()
        {
            InitializeComponent();
        }
        private void Data_ValueChanged_1(object sender, EventArgs e)
        {
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionPermission))
                    {
                        connection.Open();

                        DateTime selectedDate = Data.Value;
                        string wybranaData = selectedDate.ToString("yyyy-MM-dd");

                        string strSQL = $"SELECT Dostawca, Auta, SztukiDek, WagaDek, bufor FROM HarmonogramDostaw WHERE DataOdbioru = @wybranaData";

                        using (SqlCommand command = new SqlCommand(strSQL, connection))
                        {
                            command.Parameters.AddWithValue("@wybranaData", wybranaData);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                HarmonogramDnia.Items.Clear(); // Wyczyść listę przed dodaniem nowych elementów
                                double sumaAut = 0;
                                double sumaSztuk = 0;

                                while (reader.Read())
                                {
                                    if (!reader.IsDBNull(reader.GetOrdinal("Auta")) && !reader.IsDBNull(reader.GetOrdinal("SztukiDek")))
                                    {
                                        HarmonogramDnia.Items.Add("Auta: " + reader["Auta"] + " - " + reader["Dostawca"] + " - Sztuki deklarowane: " + reader["SztukiDek"] + " - Waga Dek: " + reader["WagaDek"]);
                                        sumaAut += Convert.ToDouble(reader["Auta"]);
                                        sumaSztuk += Convert.ToDouble(reader["SztukiDek"]);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private void MyCalendar_DateChanged_1(object sender, DateRangeEventArgs e)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    connection.Open();

                    DateTime selectedDate = MyCalendar.SelectionStart;
                    DateTime startOfWeek = selectedDate.AddDays(-(int)selectedDate.DayOfWeek);
                    DateTime endOfWeek = startOfWeek.AddDays(6);

                    string strSQL = $"SELECT DataOdbioru, Dostawca, Auta, SztukiDek, WagaDek, bufor FROM HarmonogramDostaw WHERE DataOdbioru >= @startDate AND DataOdbioru <= @endDate ORDER BY DataOdbioru, bufor";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@startDate", startOfWeek);
                        command.Parameters.AddWithValue("@endDate", endOfWeek);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            dataGridView1.Rows.Clear(); // Wyczyść wszystkie wiersze z poprzednich danych
                            dataGridView1.Columns.Clear(); // Wyczyść wszystkie kolumny z poprzednich danych

                            // Dodaj kolumny do DataGridView
                            dataGridView1.Columns.Add("DataOdbioru", "DataOdbioru");
                            dataGridView1.Columns.Add("Dostawca", "Dostawca");
                            dataGridView1.Columns.Add("Auta", "Auta");
                            dataGridView1.Columns.Add("SztukiDek", "SztukiDek");
                            dataGridView1.Columns.Add("WagaDek", "WagaDek");
                            dataGridView1.Columns.Add("bufor", "bufor");

                            // Grupowanie danych według daty
                            DateTime? currentDate = null;
                            DataGridViewRow currentGroupRow = null;

                            while (reader.Read())
                            {
                                DateTime date = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));
                                string formattedDate = date.ToString("yyyy-MM-dd dddd");

                                if (currentDate != date)
                                {
                                    currentGroupRow = new DataGridViewRow();
                                    currentGroupRow.CreateCells(dataGridView1);
                                    currentGroupRow.Cells[0].Value = formattedDate;
                                    dataGridView1.Rows.Add(currentGroupRow);

                                    currentDate = date;
                                }

                                DataGridViewRow row = new DataGridViewRow();
                                row.CreateCells(dataGridView1);
                                for (int i = 1; i < reader.FieldCount; i++)
                                {
                                    row.Cells[i].Value = reader.GetValue(i);
                                }

                                dataGridView1.Rows.Add(row);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                FormatujWierszeZgodnieZStatus(i);
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            FormatujWierszeZgodnieZStatus(e.RowIndex);
        }

        private void FormatujWierszeZgodnieZStatus(int rowIndex)
        {
            if (rowIndex >= 0)
            {
                var statusCell = dataGridView1.Rows[rowIndex].Cells["Status"];
                if (statusCell != null && statusCell.Value != null && statusCell.Value.ToString() == "Potwierdzony")
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.White; // Domyślny kolor tła dla pozostałych wierszy
                }
            }
        }
    }
}
