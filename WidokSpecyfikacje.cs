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
    
    public partial class WidokSpecyfikacje : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
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
                    SqlCommand command = new SqlCommand("SELECT CarLp, CustomerGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5, LumQnt, ProdQnt, FullFarmWeight, EmptyFarmWeight, FullWeight, EmptyWeight, Price, PriceTypeID FROM [LibraNet].[dbo].[FarmerCalc] WHERE CalcDate = @SelectedDate", connection);
                    command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    // Wypełnienie istniejących kolumn w DataGridView danymi z bazy danych
                    if (dataTable.Rows.Count > 0)
                    {
                        dataGridView1.Rows.Clear(); // Wyczyszczenie istniejących danych
                        foreach (DataRow row in dataTable.Rows)
                        {
                            dataGridView1.Rows.Add(
                                row["CarLp"],          // Numer
                                row["CustomerGID"],    // Dostawca
                                row["DeclI1"],              // SztukiDek (pusta wartość)
                                row["DeclI2"],        // Padle
                                row["DeclI3"],        // CH
                                row["DeclI4"],        // NW
                                row["DeclI5"],        // ZM
                                row["FullFarmWeight"],// BruttoHodowcy
                                row["EmptyFarmWeight"],// TaraHodowcy
                                "",                   // NettoUbojni (pusta wartość)
                                row["FullWeight"],    // BruttoUbojni
                                row["EmptyWeight"],   // TaraUbojni
                                "",                   // NettoUbojni (pusta wartość)
                                row["LumQnt"],        // LUMEL
                                row["Price"],         // Cena
                                row["PriceTypeID"]    // TypCeny
                                                      // Brak wartości dla kolumny PiK, ponieważ nie ma odpowiadającej kolumny w bazie danych
                            );
                        }
                    }
                    else
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas ładowania danych: " + ex.Message);
            }
        }



    }
}
