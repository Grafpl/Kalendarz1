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
using static Kalendarz1.CenoweMetody;

namespace Kalendarz1
{
    public partial class WidokZamowienia : Form
    {
        public string UserID { get; set; }
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        static string connectionString1 = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        private string selectedId { get; set; }
        public WidokZamowienia()
        {
            InitializeComponent();
            RozwijanieComboBox.RozwijanieOdbiorcowSymfonia(comboBoxOdbiorca);
            PrzygotujDataGridView();

        }
        public class Towar
        {
            public string Kod { get; set; }
            public int Id { get; set; } // Identyfikator towarupublic string Kod { get; set; }
        }
        private DataService dataService = new DataService();


        private void PrzypiszDaneDoUI(Dictionary<RozwijanieComboBox.DaneKontrahenta, string> dane)
        {
            textBoxLimit.Text = dane.ContainsKey(RozwijanieComboBox.DaneKontrahenta.Limit) ? dane[RozwijanieComboBox.DaneKontrahenta.Limit] : "-";
            textBoxNazwaOdbiorca.Text = dane.ContainsKey(RozwijanieComboBox.DaneKontrahenta.Nazwa) ? dane[RozwijanieComboBox.DaneKontrahenta.Nazwa] : "-";
            textBoxNIP.Text = dane.ContainsKey(RozwijanieComboBox.DaneKontrahenta.NIP) ? dane[RozwijanieComboBox.DaneKontrahenta.NIP] : "-";
            textBoxKod.Text = dane.ContainsKey(RozwijanieComboBox.DaneKontrahenta.KodPocztowy) ? dane[RozwijanieComboBox.DaneKontrahenta.KodPocztowy] : "-";
            textBoxMiejscowosc.Text = dane.ContainsKey(RozwijanieComboBox.DaneKontrahenta.Miejscowosc) ? dane[RozwijanieComboBox.DaneKontrahenta.Miejscowosc] : "-";
        }


        private void comboBoxOdbiorca_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxOdbiorca.SelectedItem is KeyValuePair<string, string> selectedItem)
            {
                selectedId = selectedItem.Key;

                // Pobierz dane z bazy i przypisz do UI
                var daneOdbiorcy = dataService.PobierzDaneOdbiorcy(selectedId);
                PrzypiszDaneDoUI(daneOdbiorcy);

                // Pobierz handlowca
                string handlowiec = dataService.PobierzHandlowca(selectedId);
                textBoxHandlowiec.Text = handlowiec;
            }
            
        }

        private void PrzygotujDataGridView()
        {
            DataGridViewComboBoxColumn kolumnaTowar = new DataGridViewComboBoxColumn
            {
                Name = "Towar",
                HeaderText = "Kod Towaru",
                DataSource = PobierzListeTowarow(), // Pobiera listę obiektów Towar
                DisplayMember = "Kod", // Wyświetlany tekst
                ValueMember = "Id",   // Przechowywana wartość
                Width = 200
            };

            DataGridViewTextBoxColumn kolumnaIlosc = new DataGridViewTextBoxColumn
            {
                Name = "Ilosc",
                HeaderText = "Ilość",
                Width = 100
            };

            DataGridViewTextBoxColumn kolumnaCena = new DataGridViewTextBoxColumn
            {
                Name = "Cena",
                HeaderText = "Cena",
                Width = 100
            };

            DataGridViewTextBoxColumn kolumnaWartosc = new DataGridViewTextBoxColumn
            {
                Name = "Wartosc",
                HeaderText = "Wartość",
                ReadOnly = true, // Tylko do odczytu (automatycznie obliczana)
                Width = 150
            };

            dataGridViewZamowienie.Columns.Add(kolumnaTowar);
            dataGridViewZamowienie.Columns.Add(kolumnaIlosc);
            dataGridViewZamowienie.Columns.Add(kolumnaCena);
            dataGridViewZamowienie.Columns.Add(kolumnaWartosc);

            // Obsługa zdarzeń, aby automatycznie aktualizować wartości
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.RowsAdded += DataGridViewZamowienie_RowsAdded;
        }
        private void DataGridViewZamowienie_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = dataGridViewZamowienie.Rows[e.RowIndex];

                if (decimal.TryParse(row.Cells["Ilosc"].Value?.ToString(), out decimal ilosc) &&
                    decimal.TryParse(row.Cells["Cena"].Value?.ToString(), out decimal cena))
                {
                    row.Cells["Wartosc"].Value = ilosc * cena;
                }
                else
                {
                    row.Cells["Wartosc"].Value = 0; // Domyślna wartość, jeśli brak danych
                }

                // Zaktualizuj sumę wartości
                AktualizujSumeWartosci();
            }
        }
        private void AktualizujSumeWartosci()
        {
            decimal suma = 0;

            foreach (DataGridViewRow row in dataGridViewZamowienie.Rows)
            {
                if (row.Cells["Wartosc"].Value != null &&
                    decimal.TryParse(row.Cells["Wartosc"].Value.ToString(), out decimal wartosc))
                {
                    suma += wartosc;
                }
            }

            // Przypisz sumę do TextBox
            textBoxSuma.Text = suma.ToString("0.00");
        }
        private void DataGridViewZamowienie_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
            {
                var row = dataGridViewZamowienie.Rows[i];
                row.Cells["Ilosc"].Value = 0; // Domyślna ilość
                row.Cells["Cena"].Value = 0;  // Domyślna cena
                row.Cells["Wartosc"].Value = 0; // Domyślna wartość
            }
        }



        private List<Towar> PobierzListeTowarow()
        {
            List<Towar> towary = new List<Towar>();
            string query = "SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = '67095' ORDER BY Kod ASC";

            using (SqlConnection connection = new SqlConnection(connectionString2))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    towary.Add(new Towar
                    {
                        Kod = reader["Kod"].ToString(),
                        Id = Convert.ToInt32(reader["Id"])
                    });
                }
            }

            return towary;
        }




        private void ZapiszZamowienie()
        {
            if (selectedId == null)
            {
                MessageBox.Show("Proszę wybrać klienta przed zapisaniem zamówienia.");
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString1))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Pobierz maksymalne Id z tabeli ZamowieniaMieso
                        string queryMaxIdZamowienie = "SELECT ISNULL(MAX(Id), 0) + 1 FROM [LibraNet].[dbo].[ZamowieniaMieso]";
                        SqlCommand commandMaxIdZamowienie = new SqlCommand(queryMaxIdZamowienie, connection, transaction);
                        int newZamowienieId = Convert.ToInt32(commandMaxIdZamowienie.ExecuteScalar());
                        DateTime selectedDate = dateTimePickerSprzedaz.Value.Date;



                        // Wstaw nowe zamówienie
                        string queryZamowienie = @"
                    INSERT INTO [LibraNet].[dbo].[ZamowieniaMieso] (Id, DataZamowienia, KlientId, Uwagi)
                    VALUES (@Id, @DataZamowienia, @KlientId, @Uwagi)";
                        SqlCommand commandZamowienie = new SqlCommand(queryZamowienie, connection, transaction);
                        commandZamowienie.Parameters.AddWithValue("@Id", newZamowienieId);
                        commandZamowienie.Parameters.AddWithValue("@DataZamowienia", selectedDate);
                        commandZamowienie.Parameters.AddWithValue("@KlientId", selectedId);
                        commandZamowienie.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(textBoxUwagi.Text) ? DBNull.Value : textBoxUwagi.Text);
                        commandZamowienie.ExecuteNonQuery();

                        // Wstaw szczegóły zamówienia (bez kolumny Id)
                        foreach (DataGridViewRow row in dataGridViewZamowienie.Rows)
                        {
                            if (row.IsNewRow) continue;

                            if (row.Cells["Towar"].Value == null ||
                                !decimal.TryParse(row.Cells["Ilosc"].Value?.ToString(), out decimal ilosc) ||
                                !decimal.TryParse(row.Cells["Cena"].Value?.ToString(), out decimal cena))
                            {
                                throw new Exception("Nieprawidłowe dane w wierszu zamówienia.");
                            }

                            string queryTowar = @"
                        INSERT INTO [LibraNet].[dbo].[ZamowieniaMiesoTowar] (ZamowienieId, KodTowaru, Ilosc, Cena)
                        VALUES (@ZamowienieId, @KodTowaru, @Ilosc, @Cena)";
                            SqlCommand commandTowar = new SqlCommand(queryTowar, connection, transaction);
                            commandTowar.Parameters.AddWithValue("@ZamowienieId", newZamowienieId);
                            commandTowar.Parameters.AddWithValue("@KodTowaru", row.Cells["Towar"].Value); // Przechowuje Id towaru
                            commandTowar.Parameters.AddWithValue("@Ilosc", ilosc);
                            commandTowar.Parameters.AddWithValue("@Cena", cena);
                            commandTowar.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        MessageBox.Show("Zamówienie zapisano pomyślnie!");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Wystąpił błąd podczas zapisywania zamówienia: {ex.Message}");
                    }
                }
            }
        }


        private void WidokZamowienia_Load(object sender, EventArgs e)
        {

        }

        private void textBoxIdOdbiorca_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBoxMiejscowosc_TextChanged(object sender, EventArgs e)
        {

        }

        private void CommandButton_Update_Click(object sender, EventArgs e)
        {
            ZapiszZamowienie();
        }
    }
}
