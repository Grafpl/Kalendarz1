using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
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
                        "Price, PriceTypeID, IncDeadConf FROM [LibraNet].[dbo].[FarmerCalc] WHERE CalcDate = @SelectedDate", connection);
                    command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    // Wyczyszczenie istniejących danych w DataGridView
                    dataGridView1.Rows.Clear();

                    // Wypełnienie istniejących kolumn w DataGridView danymi z bazy danych
                    if (dataTable.Rows.Count > 0)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            // Pobranie wartości kolumny "CustomerGID" jako obiektu Int32
                            int customerGID = ZapytaniaSQL.GetValueOrDefault<int>(row, "CustomerGID", defaultValue: -1);
                            string Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcow(customerGID, "ShortName");

                            string BruttoHodowcy = row["FullFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["FullFarmWeight"]).ToString("#,0") : null;
                            string TaraHodowcy = row["EmptyFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["EmptyFarmWeight"]).ToString("#,0") : null;
                            string NettoHodowcy = row["NettoFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["NettoFarmWeight"]).ToString("#,0") : null;

                            string BruttoUbojni = row["FullWeight"] != DBNull.Value ? Convert.ToDecimal(row["FullWeight"]).ToString("#,0") : null;
                            string TaraUbojni = row["EmptyWeight"] != DBNull.Value ? Convert.ToDecimal(row["EmptyWeight"]).ToString("#,0") : null;
                            string NettoUbojni = row["NettoWeight"] != DBNull.Value ? Convert.ToDecimal(row["NettoWeight"]).ToString("#,0") : null;

                            // Pobranie wartości kolumny "PriceTypeID" jako Nullable<int> (int?)
                            int priceTypeID = ZapytaniaSQL.GetValueOrDefault<int>(row, "PriceTypeID", defaultValue: -1);
                            string typCeny = zapytaniasql.ZnajdzNazweCenyPoID(priceTypeID);

                            dataGridView1.Rows.Add(
                                row["ID"],         // Numer
                                row["CarLp"],         // Numer
                                Dostawca,             // Dostawca
                                row["DeclI1"],        // SztukiDek (pusta wartość)
                                row["DeclI2"],        // Padle
                                row["DeclI3"],        // CH
                                row["DeclI4"],        // NW
                                row["DeclI5"],        // ZM
                                BruttoHodowcy,     // Zastosowanie sformatowanych wartości
                                TaraHodowcy,       // Zastosowanie sformatowanych wartości
                                NettoHodowcy,       // Zastosowanie sformatowanych wartości
                                BruttoUbojni,     // Zastosowanie sformatowanych wartości
                                TaraUbojni,       // Zastosowanie sformatowanych wartości
                                NettoUbojni,       // Zastosowanie sformatowanych wartości
                                row["LumQnt"],        // LUMEL
                                row["ProdQnt"],       // Prod Sztuki
                                row["ProdWgt"],       // Prod Wagi
                                row["Price"],         // Cena
                                typCeny,   // TypCeny
                                row["IncDeadConf"]    // Czy odliczamy padłe i konfiskaty
                                                      // Brak wartości dla kolumny PiK, ponieważ nie ma odpowiadającej kolumny w bazie danych
                            );
                        }

                    }
                    else
                    {
                        // Obsługa przypadku braku danych
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

                MessageBox.Show("Wartość zaktualizowana pomyślnie!");
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
                        command.Parameters.AddWithValue("@NewValue", newValue);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Dane zaktualizowane pomyślnie!");
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
                case 0: return "CarLp"; // Załóżmy, że CarLp to nazwa kolumny w bazie danych
                case 3: return "DeclI1"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 4: return "DeclI2"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 5: return "DeclI3"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 6: return "DeclI4"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 7: return "DeclI5"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 14: return "LumQnt"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 15: return "ProdQnt"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 16: return "ProdWgt"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                default: throw new ArgumentException("Nieprawidłowy indeks kolumny.");
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            SzczegolyDrukowaniaSpecki PDFview = new SzczegolyDrukowaniaSpecki();

            // Wyświetlanie Form1
            PDFview.Show();
        }







    }
}
