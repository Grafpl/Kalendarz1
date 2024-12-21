using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace Kalendarz1
{

    public partial class WidokSpecyfikacje : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL(); // Tworzenie egzemplarza klasy ZapytaniaSQL


        public WidokSpecyfikacje()
        {

            InitializeComponent();

        }

        private void WidokSpecyfikacje_Load(object sender, EventArgs e)
        {
            // Inicjalizacja formularza
            dateTimePicker1.ValueChanged += dateTimePicker1_ValueChanged;
        }


        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            // Obsługa zmiany daty w dateTimePicker1
            // Użyj tylko daty bez informacji o czasie
            LoadData(dateTimePicker1.Value.Date);
        }
        private void LoadData(DateTime selectedDate)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("SELECT ID, CarLp, CustomerGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5, LumQnt, ProdQnt, ProdWgt, " +
                        "FullFarmWeight, EmptyFarmWeight, NettoFarmWeight, FullWeight, EmptyWeight, NettoWeight, " +
                        "Price, PriceTypeID, IncDeadConf, Loss FROM [LibraNet].[dbo].[FarmerCalc] WHERE CalcDate = @SelectedDate Order By CarLP"
                        , connection);
                    command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    // Clear existing data in DataGridView
                    dataGridView1.Rows.Clear();

                    // Populate DataGridView with data from database
                    if (dataTable.Rows.Count > 0)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            string customerGID = ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerGID", defaultValue: "-1");
                            string Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "ShortName");

                            string BruttoHodowcy = row["FullFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["FullFarmWeight"]).ToString("#,0") : null;
                            string TaraHodowcy = row["EmptyFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["EmptyFarmWeight"]).ToString("#,0") : null;
                            string NettoHodowcy = row["NettoFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["NettoFarmWeight"]).ToString("#,0") : null;

                            string BruttoUbojni = row["FullWeight"] != DBNull.Value ? Convert.ToDecimal(row["FullWeight"]).ToString("#,0") : null;
                            string TaraUbojni = row["EmptyWeight"] != DBNull.Value ? Convert.ToDecimal(row["EmptyWeight"]).ToString("#,0") : null;
                            string NettoUbojni = row["NettoWeight"] != DBNull.Value ? Convert.ToDecimal(row["NettoWeight"]).ToString("#,0") : null;

                            int priceTypeID = ZapytaniaSQL.GetValueOrDefault<int>(row, "PriceTypeID", defaultValue: -1);
                            string typCeny = zapytaniasql.ZnajdzNazweCenyPoID(priceTypeID);

                            decimal ubytek = Math.Round(ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Loss", defaultValue: 0) * 100, 2);

                            // Set the checkbox value for "PiK" based on "IncDeadConf"
                            bool incDeadConf = row["IncDeadConf"] != DBNull.Value && Convert.ToBoolean(row["IncDeadConf"]);

                            // Populate the DataGridView row
                            dataGridView1.Rows.Add(
                                row["ID"],
                                row["CarLp"],
                                Dostawca,
                                row["DeclI1"],
                                row["DeclI2"],
                                row["DeclI3"],
                                row["DeclI4"],
                                row["DeclI5"],
                                BruttoHodowcy,
                                TaraHodowcy,
                                NettoHodowcy,
                                BruttoUbojni,
                                TaraUbojni,
                                NettoUbojni,
                                row["LumQnt"],
                                row["ProdQnt"],
                                row["ProdWgt"],
                                row["Price"],
                                typCeny,
                                incDeadConf, // Add the checkbox value for "PiK"
                                ubytek
                            );
                        }
                    }
                    else
                    {
                        // Handle case where no data is returned
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas ładowania danych: " + ex.Message);
            }
        }



        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdź, czy wiersz i kolumna zostały kliknięte
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                // Pobierz wartość ID z klikniętego wiersza i przekonwertuj na int
                int idSpecyfikacja = Convert.ToInt32(dataGridView1.Rows[e.RowIndex].Cells["ID"].Value);


                // Wywołaj nowe okno Dostawa, przekazując wartość LP
                WidokAvilog dostawaForm = new WidokAvilog(idSpecyfikacja);
                dostawaForm.Show();
            }
        }
        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridViewRow editedRow = dataGridView1.Rows[e.RowIndex];

                // Check if the changed cell is in the "PiK" column
                if (dataGridView1.Columns[e.ColumnIndex].Name == "PiK")
                {
                    int id = Convert.ToInt32(editedRow.Cells["ID"].Value);
                    bool newValue = Convert.ToBoolean(editedRow.Cells["PiK"].Value);

                    // Update the database with the new checkbox value
                    UpdateDatabase(id, e.ColumnIndex, newValue.ToString());

                    // Apply formatting based on the checkbox value
                    ApplyFormattingToRow(editedRow, newValue);
                }
            }
        }
        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            bool isChecked = Convert.ToBoolean(row.Cells["PiK"].Value);

            ApplyFormattingToRow(row, isChecked);
        }

        private void ApplyFormattingToRow(DataGridViewRow row, bool isChecked)
        {
            DataGridViewCellStyle style = new DataGridViewCellStyle();

            if (isChecked)
            {
                style.Font = new Font(dataGridView1.Font, FontStyle.Strikeout);
                style.ForeColor = Color.Red;
            }
            else
            {
                style.Font = dataGridView1.Font;
                style.ForeColor = dataGridView1.ForeColor;
            }

            row.Cells["Padle"].Style = style; // Assuming DeclI2 is the "Padle" column
        }


        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // Sprawdź czy edytowana komórka jest w drugiej kolumnie i w pierwszym wierszu
                if (e.ColumnIndex == 1 && e.RowIndex == 0)
                {
                    // Sprawdź czy wpisano liczbę w pierwszym wierszu pierwszej kolumny
                    int newValue;
                    if (int.TryParse(dataGridView1.Rows[0].Cells[0].Value?.ToString(), out newValue))
                    {
                        // Inkrementuj liczbę w dół dla pozostałych wierszy
                        for (int i = 1; i < dataGridView1.Rows.Count; i++)
                        {
                            newValue++;
                            dataGridView1.Rows[i].Cells[1].Value = newValue;
                        }
                    }
                }
                else
                {
                    DataGridViewRow editedRow = dataGridView1.Rows[e.RowIndex];

                    // Pobierz ID z edytowanego wiersza
                    int id = Convert.ToInt32(editedRow.Cells["ID"].Value);

                    // Pobierz nową wartość z edytowanej komórki
                    string newValue = editedRow.Cells[e.ColumnIndex].Value.ToString();

                    // Zaktualizuj odpowiednią kolumnę w bazie danych
                    UpdateDatabase(id, e.ColumnIndex, newValue);
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas aktualizacji danych: " + ex.Message);
            }
        }




        private void UpdateDatabase(int id, int columnIndex, string newValue)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string columnName = GetColumnName(columnIndex); // Funkcja do pobrania nazwy kolumny na podstawie indeksu
                    string strSQL = $@"UPDATE dbo.FarmerCalc
                               SET {columnName} = @NewValue
                               WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@ID", id);

                        // Check if the column is "Loss" and process the newValue accordingly
                        if (columnName == "Loss")
                        {
                            if (decimal.TryParse(newValue, out decimal lossValue))
                            {
                                lossValue /= 100;
                                command.Parameters.AddWithValue("@NewValue", lossValue);
                            }
                            else
                            {
                                MessageBox.Show("Nieprawidłowa wartość dla kolumny 'Loss'.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                        else if (columnName == "IncDeadConf")
                        {
                            bool checkboxValue = Convert.ToBoolean(newValue);
                            command.Parameters.AddWithValue("@NewValue", checkboxValue);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@NewValue", newValue);
                        }

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {

                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować danych.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas aktualizacji danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetColumnName(int columnIndex)
        {
            // Funkcja zwracająca nazwę kolumny na podstawie indeksu
            switch (columnIndex)
            {
                case 1: return "CarLp"; // Załóżmy, że CarLp to nazwa kolumny w bazie danych
                case 3: return "DeclI1"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 4: return "DeclI2"; // Załóżmy, że DeclI2 to nazwa kolumny w bazie danych Padłe
                case 5: return "DeclI3"; // Załóżmy, że DeclI3 to nazwa kolumny w bazie danych
                case 6: return "DeclI4"; // Załóżmy, że DeclI4 to nazwa kolumny w bazie danych
                case 7: return "DeclI5"; // Załóżmy, że DeclI5 to nazwa kolumny w bazie danych
                case 14: return "LumQnt"; // Załóżmy, że LumQnt to nazwa kolumny w bazie danych
                case 15: return "ProdQnt"; // Załóżmy, że ProdQnt to nazwa kolumny w bazie danych
                case 16: return "ProdWgt"; // Załóżmy, że ProdWgt to nazwa kolumny w bazie danych
                case 17: return "Price"; // Załóżmy, że ProdWgt to nazwa kolumny w bazie danych
                case 19: return "IncDeadConf"; // Załóżmy, że IncDeadConf to nazwa kolumny w bazie danych
                case 20: return "Loss"; // Załóżmy, że Loss to nazwa kolumny w bazie danych

                default: throw new ArgumentException("Nieprawidłowy indeks kolumny.");
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            // Odczytaj wartość z DateTimePicker
            DateTime wybranaData = dateTimePicker1.Value;

            // Utwórz nowy formularz z przekazaną datą
            SzczegolyDrukowaniaSpecki PDFview = new SzczegolyDrukowaniaSpecki(wybranaData);

            // Wyświetl nowy formularz
            PDFview.Show();
        }
        private void MoveRowUp()
        {
            if (dataGridView1.CurrentCell != null)
            {
                int rowIndex = dataGridView1.CurrentCell.RowIndex;
                if (rowIndex > 0)
                {
                    DataTable table = dataGridView1.DataSource as DataTable;
                    if (table != null)
                    {
                        DataRow row = table.NewRow();
                        row.ItemArray = table.Rows[rowIndex].ItemArray;
                        table.Rows.RemoveAt(rowIndex);
                        table.Rows.InsertAt(row, rowIndex - 1);
                        RefreshNumeration();
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[rowIndex - 1].Selected = true;
                        dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex - 1].Cells[0];
                    }
                }
            }
        }

        private void MoveRowDown()
        {
            if (dataGridView1.CurrentCell != null)
            {
                int rowIndex = dataGridView1.CurrentCell.RowIndex;
                if (rowIndex < dataGridView1.Rows.Count - 2) // -2 to ignore the new row
                {
                    DataTable table = (DataTable)dataGridView1.DataSource;
                    if (table != null)
                    {
                        DataRow row = table.NewRow();
                        row.ItemArray = table.Rows[rowIndex].ItemArray;
                        table.Rows.RemoveAt(rowIndex);
                        table.Rows.InsertAt(row, rowIndex + 1);
                        RefreshNumeration();
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[rowIndex + 1].Selected = true;
                        dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex + 1].Cells[0];
                    }
                }
            }
        }

        private void RefreshNumeration()
        {
            for (int i = 0; i < dataGridView1.Rows.Count - 1; i++)
            {
                dataGridView1.Rows[i].Cells["Nr"].Value = i + 1;
            }
        }


        private void btnLoadData_Click_1(object sender, EventArgs e)

        {
            string ftpUrl = "ftp://admin:wago@192.168.0.98/POMIARY.TXT";
            string content;

            // Pobierz zawartość pliku TXT z serwera FTP
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.DownloadFile;

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    content = reader.ReadToEnd();
                }

                // Podziel zawartość na wiersze
                string[] rows = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // Utwórz listę do przechowywania danych
                List<string[]> data = new List<string[]>();
                DateTime selectedDate = dateTimePicker1.Value.Date;
                // Dodaj wiersze do listy danych
                foreach (string row in rows)
                {
                    // Podziel wiersz na kolumny
                    string[] columns = row.Split(';');
                    data.Add(columns);
                }

                // Ustaw dane w DataGrid tylko dla wybranej daty
                dataGridView2.Rows.Clear();
                dataGridView2.Columns.Clear(); // Wyczyść istniejące kolumny

                // Dodaj kolumny do DataGridView
                dataGridView2.Columns.Add("Column1", "Data");
                dataGridView2.Columns.Add("Column2", "Godzina Końca Partii");
                dataGridView2.Columns.Add("Column3", "Ilość sztuk");



                // Ustawienia regionalne do konwersji liczbowej
                var numberFormat = new System.Globalization.NumberFormatInfo();
                numberFormat.NumberDecimalSeparator = ".";  // Ustawienie kropki jako separatora dziesiętnego

                foreach (string[] row in data)
                {
                    // Sprawdź czy data w wierszu jest równa wybranej dacie
                    if (DateTime.TryParse(row[0], out DateTime rowDate) && rowDate.Date == selectedDate && row[2] != "0.0")
                    {
                        // Próba konwersji trzeciej kolumny na liczbową wartość zmiennoprzecinkową
                        if (double.TryParse(row[2], System.Globalization.NumberStyles.Any, numberFormat, out double quantity))
                        {
                            double roundedQuantity = Math.Ceiling(quantity);
                            string[] rowData = new string[] { row[0], row[1], roundedQuantity.ToString() };
                            dataGridView2.Rows.Add(rowData);
                        }
                        else
                        {
                            // Logowanie nieudanej próby konwersji
                            MessageBox.Show($"Nieprawidłowy format liczbowy w wierszu: {row[0]}, {row[1]}, {row[2]}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas pobierania danych: " + ex.Message);
            }

        }

        private void buttonUP_Click(object sender, EventArgs e)
        {
            MoveRowUp();
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            MoveRowDown();
        }
    }
}