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
        private int? modyfikowaneIdZamowienia;
        public WidokZamowienia()
        {
            InitializeComponent();
            ConfigureDateTimePicker();
            PrzestawienieDaty();
            RozwijanieComboBox.RozwijanieKontrPoKatalogu(comboBoxOdbiorca, "Odbiorcy Drobiu");
            PrzygotujDataGridView();

        }
        public WidokZamowienia(int? idZamowienia = null)
        {
            InitializeComponent();

            // Konfiguruj kontrolki
            ConfigureDateTimePicker();
            RozwijanieComboBox.RozwijanieKontrPoKatalogu(comboBoxOdbiorca, "Odbiorcy Drobiu");
            PrzygotujDataGridView(); // Upewnij się, że DataGridView ma już kolumny

            if (idZamowienia.HasValue)
            {
                modyfikowaneIdZamowienia = idZamowienia;
                ZaladujDaneZamowienia(modyfikowaneIdZamowienia.Value);
                CommandButton_Update.BackColor = Color.LightYellow;
                CommandButton_Update.Text = "Modyfikuj";
            }
            else
            {
                modyfikowaneIdZamowienia = null; // Nowe zamówienie
                PrzestawienieDaty();
            }
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

        private void ConfigureDateTimePicker()
        {
            // Ustaw format niestandardowy, aby wyświetlać datę i czas
            dateTimePickerGodzinaPrzyjazdu.Format = DateTimePickerFormat.Custom;
            dateTimePickerGodzinaPrzyjazdu.CustomFormat = "HH:mm"; // Data + Godzina:Minuta

            // Ustaw tryb góra/dół (bez kalendarza, opcjonalnie)
            dateTimePickerGodzinaPrzyjazdu.ShowUpDown = true;
        }



        private void PrzygotujDataGridView()
        {
            // Dodaj kolumnę z przyciskiem "Usuń"
            DataGridViewButtonColumn kolumnaUsun = new DataGridViewButtonColumn
            {
                Name = "Usun",
                HeaderText = "Usuń",
                Text = "Usuń",
                UseColumnTextForButtonValue = true, // Ustaw tekst w przycisku
                Width = 75
            };

            // Dodaj inne kolumny, jak wcześniej
            DataGridViewComboBoxColumn kolumnaTowar = new DataGridViewComboBoxColumn
            {
                Name = "Towar",
                HeaderText = "Kod Towaru",
                DataSource = PobierzListeTowarow(),
                DisplayMember = "Kod",
                ValueMember = "Id",
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
                ReadOnly = true,
                Width = 150
            };

            dataGridViewZamowienie.Columns.Add(kolumnaTowar);
            dataGridViewZamowienie.Columns.Add(kolumnaIlosc);
            dataGridViewZamowienie.Columns.Add(kolumnaCena);
            dataGridViewZamowienie.Columns.Add(kolumnaWartosc);
            dataGridViewZamowienie.Columns.Add(kolumnaUsun); // Dodaj kolumnę "Usuń"

            // Obsługa zdarzeń
            dataGridViewZamowienie.CellValueChanged += DataGridViewZamowienie_CellValueChanged;
            dataGridViewZamowienie.RowsAdded += DataGridViewZamowienie_RowsAdded;
            dataGridViewZamowienie.CellClick += DataGridViewZamowienie_CellClick; // Dodaj obsługę kliknięcia
        }

        private void DataGridViewZamowienie_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                // Sprawdź, czy kliknięto w kolumnę "Usuń"
                if (dataGridViewZamowienie.Columns[e.ColumnIndex].Name == "Usun")
                {
                    // Potwierdź usunięcie
                    var confirmResult = MessageBox.Show("Czy na pewno chcesz usunąć ten wiersz?",
                                                        "Potwierdzenie usunięcia",
                                                        MessageBoxButtons.YesNo);
                    if (confirmResult == DialogResult.Yes)
                    {
                        // Usuń wiersz
                        dataGridViewZamowienie.Rows.RemoveAt(e.RowIndex);

                        // Zaktualizuj sumę wartości
                        AktualizujSumeWartosci();
                    }
                }
            }
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
        private void PrzestawienieDaty()
        {
            // Pobierz aktualną datę
            DateTime dzisiaj = DateTime.Now.Date;

            // Sprawdź, czy jest sobota
            if (dzisiaj.DayOfWeek == DayOfWeek.Friday)
            {
                // Ustaw na najbliższy poniedziałek
                dateTimePickerSprzedaz.Value = dzisiaj.AddDays(3);
            }
            else
            {
                // Przesuń datę o jeden dzień
                dateTimePickerSprzedaz.Value = dzisiaj.AddDays(1);
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


        private void ZaladujDaneZamowienia(int idZamowienia)
        {
            if (dataGridViewZamowienie.Columns.Count == 0)
            {
                PrzygotujDataGridView(); // Upewnij się, że kolumny są zdefiniowane
            }

            using (SqlConnection connection = new SqlConnection(connectionString1))
            {
                connection.Open();

                // Pobierz dane zamówienia
                string queryZamowienie = @"
            SELECT DataZamowienia, KlientId, Uwagi, DataPrzyjazdu
            FROM [LibraNet].[dbo].[ZamowieniaMieso]
            WHERE Id = @Id";
                SqlCommand commandZamowienie = new SqlCommand(queryZamowienie, connection);
                commandZamowienie.Parameters.AddWithValue("@Id", idZamowienia);

                SqlDataReader readerZamowienie = commandZamowienie.ExecuteReader();

                if (readerZamowienie.Read())
                {
                    dateTimePickerSprzedaz.Value = Convert.ToDateTime(readerZamowienie["DataZamowienia"]);
                    selectedId = readerZamowienie["KlientId"].ToString();
                    textBoxUwagi.Text = readerZamowienie["Uwagi"].ToString();
                    dateTimePickerGodzinaPrzyjazdu.Value = Convert.ToDateTime(readerZamowienie["DataPrzyjazdu"]);
                }
                readerZamowienie.Close();

                // Pobierz szczegóły zamówienia (towary)
                string queryTowary = @"
            SELECT KodTowaru, Ilosc, Cena
            FROM [LibraNet].[dbo].[ZamowieniaMiesoTowar]
            WHERE ZamowienieId = @ZamowienieId";
                SqlCommand commandTowary = new SqlCommand(queryTowary, connection);
                commandTowary.Parameters.AddWithValue("@ZamowienieId", idZamowienia);

                SqlDataReader readerTowary = commandTowary.ExecuteReader();

                dataGridViewZamowienie.Rows.Clear();

                while (readerTowary.Read())
                {
                    int rowIndex = dataGridViewZamowienie.Rows.Add();
                    DataGridViewRow row = dataGridViewZamowienie.Rows[rowIndex];
                    row.Cells["Towar"].Value = readerTowary["KodTowaru"];
                    row.Cells["Ilosc"].Value = readerTowary["Ilosc"];
                    row.Cells["Cena"].Value = readerTowary["Cena"];
                }
                readerTowary.Close();

                // Pobierz dane odbiorcy
                var daneOdbiorcy = dataService.PobierzDaneOdbiorcy(selectedId);
                PrzypiszDaneDoUI(daneOdbiorcy);
            }
        }


        private void ZapiszZamowienie()
        {
            using (SqlConnection connection = new SqlConnection(connectionString1))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        if (modyfikowaneIdZamowienia.HasValue)
                        {
                            // Aktualizacja istniejącego zamówienia
                            string queryUpdate = @"
                    UPDATE [LibraNet].[dbo].[ZamowieniaMieso]
                    SET DataZamowienia = @DataZamowienia,
                        DataPrzyjazdu = @DataPrzyjazdu,
                        KlientId = @KlientId,
                        Uwagi = @Uwagi,
                        KtoMod = @KtoModyfikowal,
                        KiedyMod = @KiedyModyfikowal
                    WHERE Id = @Id";
                            SqlCommand commandUpdate = new SqlCommand(queryUpdate, connection, transaction);
                            commandUpdate.Parameters.AddWithValue("@DataZamowienia", dateTimePickerSprzedaz.Value.Date);
                            commandUpdate.Parameters.AddWithValue("@DataPrzyjazdu", dateTimePickerGodzinaPrzyjazdu.Value);
                            commandUpdate.Parameters.AddWithValue("@KtoModyfikowal", UserID);
                            commandUpdate.Parameters.AddWithValue("@KiedyModyfikowal", dateTimePickerGodzinaPrzyjazdu.Value);
                            commandUpdate.Parameters.AddWithValue("@KlientId", selectedId);
                            commandUpdate.Parameters.AddWithValue("@Uwagi", textBoxUwagi.Text ?? (object)DBNull.Value);
                            commandUpdate.Parameters.AddWithValue("@Id", modyfikowaneIdZamowienia.Value);
                            commandUpdate.ExecuteNonQuery();

                            // Usuń stare towary
                            string queryDeleteTowary = @"
                    DELETE FROM [LibraNet].[dbo].[ZamowieniaMiesoTowar]
                    WHERE ZamowienieId = @ZamowienieId";
                            SqlCommand commandDeleteTowary = new SqlCommand(queryDeleteTowary, connection, transaction);
                            commandDeleteTowary.Parameters.AddWithValue("@ZamowienieId", modyfikowaneIdZamowienia.Value);
                            commandDeleteTowary.ExecuteNonQuery();
                        }
                        else
                        {
                            // Dodanie nowego zamówienia
                            string queryMaxId = "SELECT ISNULL(MAX(Id), 0) + 1 FROM [LibraNet].[dbo].[ZamowieniaMieso]";
                            SqlCommand commandMaxId = new SqlCommand(queryMaxId, connection, transaction);
                            int newId = Convert.ToInt32(commandMaxId.ExecuteScalar());

                            string queryInsert = @"
                    INSERT INTO [LibraNet].[dbo].[ZamowieniaMieso] (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser)
                    VALUES (@Id, @DataZamowienia, @DataPrzyjazdu, @KlientId, @Uwagi, @KtoStworzyl)";
                            SqlCommand commandInsert = new SqlCommand(queryInsert, connection, transaction);
                            commandInsert.Parameters.AddWithValue("@Id", newId);
                            commandInsert.Parameters.AddWithValue("@KtoStworzyl", UserID);
                            commandInsert.Parameters.AddWithValue("@DataZamowienia", dateTimePickerSprzedaz.Value.Date);
                            commandInsert.Parameters.AddWithValue("@DataPrzyjazdu", dateTimePickerGodzinaPrzyjazdu.Value);
                            commandInsert.Parameters.AddWithValue("@KlientId", selectedId);
                            commandInsert.Parameters.AddWithValue("@Uwagi", textBoxUwagi.Text ?? (object)DBNull.Value);
                            commandInsert.ExecuteNonQuery();

                            modyfikowaneIdZamowienia = newId; // Ustaw nowe ID
                        }

                        // Dodaj nowe towary
                        foreach (DataGridViewRow row in dataGridViewZamowienie.Rows)
                        {
                            if (row.IsNewRow) continue;

                            string queryInsertTowar = @"
                    INSERT INTO [LibraNet].[dbo].[ZamowieniaMiesoTowar] (ZamowienieId, KodTowaru, Ilosc, Cena)
                    VALUES (@ZamowienieId, @KodTowaru, @Ilosc, @Cena)";
                            SqlCommand commandInsertTowar = new SqlCommand(queryInsertTowar, connection, transaction);
                            commandInsertTowar.Parameters.AddWithValue("@ZamowienieId", modyfikowaneIdZamowienia.Value);
                            commandInsertTowar.Parameters.AddWithValue("@KodTowaru", row.Cells["Towar"].Value);
                            commandInsertTowar.Parameters.AddWithValue("@Ilosc", Convert.ToDecimal(row.Cells["Ilosc"].Value));
                            commandInsertTowar.Parameters.AddWithValue("@Cena", Convert.ToDecimal(row.Cells["Cena"].Value));
                            commandInsertTowar.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        MessageBox.Show("Zamówienie zapisano pomyślnie!");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Wystąpił błąd: {ex.Message}");
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

        private void dateTimePickerGodzinaPrzyjazdu_ValueChanged(object sender, EventArgs e)
        {

        }
    }
}
