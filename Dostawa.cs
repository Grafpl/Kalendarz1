using Microsoft.Data.SqlClient;
using System;
using System.Windows.Forms;

namespace Kalendarz1
{

    public partial class Dostawa : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private MojeObliczenia obliczenia = new MojeObliczenia();

        public Dostawa()
        {
            InitializeComponent();
            FillComboBox();
            SetupComboBox2();
        }

        private void FillComboBox()
        {
            string query = "SELECT DISTINCT Name FROM dbo.DOSTAWCY";

            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string dostawca = reader["Name"].ToString();
                    Dostawca.Items.Add(dostawca);
                }

                reader.Close();
            }
        }
        private void SetupComboBox2()
        {
            // Dodaj opcje do comboBox2
            Status.Items.AddRange(new string[] { "", "Potwierdzony", "Do wykupienia", "Anulowany", "Sprzedany", "B.Wolny.", "B.Kontr." });

            // Opcjonalnie ustaw domyślną opcję wybraną
            Status.SelectedIndex = 0; // Wybierz pierwszą opcję
        }

        private void srednia_TextChanged(object sender, EventArgs e)
        {

            obliczenia.ObliczWage(srednia, WagaTuszki, iloscPoj, sztukNaSzuflade, wyliczone, KGwSkrzynce, CalcSztukNaSzuflade);
            obliczenia.ileSztukOblcizenie(sztukNaSzuflade, wyliczone);
        }

        private void Data_ValueChanged(object sender, EventArgs e)
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

                            SumAutDzien.Text = sumaAut.ToString();
                            SumaSztukDzien.Text = sumaSztuk.ToString();
                        }
                    }

                    obliczenia.ObliczRozniceDni(Data, dataWstawienia);
                    obliczenia.ObliczWageDni(WagaDni, RoznicaDni);
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void sztukNaSzuflade_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ProponowanaIloscNaSkrzynke(sztukNaSzuflade, sztuki, obliczeniaAut, srednia, KGwSkrzynce, wyliczone);
        }

        private void sztuki_TextChanged(object sender, EventArgs e)
        {
            obliczenia.ObliczenieSztuki(sztuki, sztukNaSzuflade, obliczeniaAut);
        }

        private void ObliczAuta_Click(object sender, EventArgs e)
        {

            // Tworzenie nowej instancji Form1
            ObliczenieAut obliczenieAut = new ObliczenieAut();

            // Wyświetlanie Form1
            obliczenieAut.ShowDialog();

            // Po zamknięciu Form2 odczytujemy wartości z jego właściwości i przypisujemy do kontrolki TextBox w Form1
            sztukNaSzuflade.Text = obliczenieAut.sztukiNaSzuflade;
            liczbaAut.Text = obliczenieAut.iloscAut;
            sztuki.Text = obliczenieAut.iloscSztuk;

            // Opcjonalnie, jeśli chcesz, aby użytkownik mógł interaktywnie korzystać z Form1 i wrócić do Form2, użyj form1.ShowDialog() zamiast form1.Show().
            // form1.ShowDialog();
        }
    }

}
