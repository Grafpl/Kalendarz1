using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Xml.Linq;
using Microsoft.VisualBasic.ApplicationServices;
using System.Globalization;

namespace Kalendarz1
{
    public partial class Dostawa : Form
    {
        private string GID;
        private string lpDostawa;
        public string UserID { get; set; }
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        public Dostawa()
        {
            this.Load += Dostawa_Load;
            InitializeComponent();
            FillComboBox();
            SetupComboBox2();
        }
        public Dostawa(string lp) : this()
        {
            lpDostawa = lp;
            PobierzInformacjeZBazyDanych(lpDostawa);
        }
        private void Dostawa_Load(object sender, EventArgs e)
        {
            NazwaZiD databaseManager = new NazwaZiD();
            string name = databaseManager.GetNameById(UserID);
            // Przypisanie wartości UserID do TextBoxa userTextbox
            ktoStwo.Text = name;
        }
        private void LpWstawienia_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Utwórz połączenie z bazą danych SQL Server
            using (SqlConnection cnn = new SqlConnection(connectionPermission))
            {
                cnn.Open();

                // Utwórz zapytanie SQL do pobrania danych na podstawie wybranej wartości "Lp"
                string lpWstawieniaValue = LpWstawienia.Text;
                string strSQL = "SELECT * FROM dbo.WstawieniaKurczakow WHERE Lp = @lp";

                // Wykonaj zapytanie SQL
                using (SqlCommand command = new SqlCommand(strSQL, cnn))
                {
                    command.Parameters.AddWithValue("@lp", lpWstawieniaValue);

                    using (SqlDataReader rst = command.ExecuteReader())
                    {
                        // Wyświetl dane w TextBoxach i ComboBoxach
                        if (rst.Read())
                        {
                            string dataWstawieniaFormatted = Convert.ToDateTime(rst["DataWstawienia"]).ToString("yyyy-MM-dd");
                            dataWstawienia.Text = dataWstawieniaFormatted;
                            sztukiWstawienia.Text = rst["IloscWstawienia"].ToString();
                            dataUbiorka.Text = rst["DataUbiorki"].ToString();
                            SztukiUbiorka.Text = rst["IloscUbiorki"].ToString();
                            dataPelne.Text = rst["DataPelne"].ToString();
                            sztukiPelne.Text = rst["IloscPelne"].ToString();
                        }
                    }
                }

                // Przygotowanie drugiego zapytania
                double sumaSztukWstawienia = 0;
                double sumaAutWstawienia = 0;

                strSQL = "SELECT LP, DataOdbioru, Dostawca, Auta, SztukiDek, WagaDek, bufor FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LpW = @NumerWstawienia order by DataOdbioru ASC";

                using (SqlCommand command2 = new SqlCommand(strSQL, cnn))
                {
                    command2.Parameters.AddWithValue("@NumerWstawienia", lpWstawieniaValue);

                    using (SqlDataReader rs = command2.ExecuteReader())
                    {
                        while (rs.Read())
                        {
                            if (rs["bufor"].ToString() == "Potwierdzony" || rs["bufor"].ToString() == "B.Kontr." || rs["bufor"].ToString() == "B.Wolny.")
                            {
                                // Tutaj wklej kod do formatowania daty
                                string dataOdbioru = Convert.ToDateTime(rs["DataOdbioru"]).ToString("yyyy-MM-dd dddd");

                                // Dodaj sformatowaną datę do ComboBoxa
                                HarmonogramWstawien.Items.Add(dataOdbioru + " Auta: " + rs["Auta"] + " - " + rs["Dostawca"] + " - Szt.Dek: " + rs["SztukiDek"] + " - Waga: " + rs["WagaDek"]);

                                sumaAutWstawienia += Convert.ToDouble(rs["Auta"]);
                                sumaSztukWstawienia += Convert.ToDouble(rs["SztukiDek"]);
                            }
                        }
                    }
                }
            }
        }
        private void PobierzInformacjeZBazyDanych(string lp)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionPermission))
                {
                    connection.Open();
                    string strSQL = "SELECT * FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LP = @lp";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@lp", lp);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Przypisz pobrane wartości z bazy danych do TextBox-ów
                                Status.Text = reader["bufor"].ToString(); // Przypisz wartość bufora
                                Data.Text = reader["DataOdbioru"].ToString(); // Przypisz wartość daty
                                Dostawca.Text = reader["Dostawca"].ToString();
                                KmH.Text = reader["KmH"].ToString();
                                //Kurnik.Text = reader["GID"].ToString();
                                KmK.Text = reader["KmK"].ToString();
                                liczbaAut.Text = reader["Auta"].ToString();
                                srednia.Text = reader["WagaDek"].ToString();
                                sztukNaSzuflade.Text = reader["SztSzuflada"].ToString();
                                sztuki.Text = reader["SztukiDek"].ToString();
                                TypUmowy.Text = reader["typUmowy"].ToString();
                                TypCeny.Text = reader["typCeny"].ToString();
                                Cena.Text = reader["Cena"].ToString();
                                Status.Text = reader["Bufor"].ToString();
                                Uwagi.Text = reader["uwagi"].ToString();
                                Dodatek.Text = reader["Dodatek"].ToString();
                                dataStwo.Text = reader["DataUtw"].ToString();
                                Ubytek.Text = reader["Ubytek"].ToString();
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
        private void Dostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionPermission))
                {
                    conn.Open();

                    // Pobierz wartość wybraną w ComboBox "dostawca"
                    string selectedDostawca = Dostawca.Text;
                    Kurnik.Items.Clear(); // Wyczyść listę elementów
                    Kurnik.SelectedIndex = -1; // Ustaw wartość wybraną na brak wyboru
                    UlicaK.Text = string.Empty;
                    KodPocztowyK.Text = string.Empty;
                    MiejscK.Text = string.Empty;
                    KmK.Text = string.Empty;
                    Kurnik.Text = string.Empty;
                    // Znajdź GID dostawcy na podstawie jego nazwy
                    using (SqlCommand cmd = new SqlCommand("SELECT GID, PriceTypeID FROM dbo.DOSTAWCY WHERE Name = @selectedDostawca", conn))
                    {
                        cmd.Parameters.AddWithValue("@selectedDostawca", selectedDostawca);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                GID = reader["GID"].ToString();

                                // Znajdź rekordy w tabeli [LibraNet].[dbo].[DostawcyAdresy] na podstawie CustomerGID
                                reader.Close();
                                using (SqlCommand cmd2 = new SqlCommand("SELECT Name FROM [LibraNet].[dbo].[DostawcyAdresy] WHERE CustomerGID = @GID", conn))
                                {
                                    cmd2.Parameters.AddWithValue("@GID", GID);
                                    using (SqlDataReader reader2 = cmd2.ExecuteReader())
                                    {
                                        while (reader2.Read())
                                        {
                                            Kurnik.Items.Add(reader2["Name"].ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Znajdź rekordy w tabeli [LibraNet].[dbo].[DostawcyAdresy] na podstawie GID dostawcy
                    using (SqlCommand cmd4 = new SqlCommand("SELECT Address, PostalCode, City, Addition, Loss, Distance, Phone1, Phone2, Phone3, Info1, Info2, Info3 FROM [LibraNet].[dbo].[DOSTAWCY] WHERE Name = @selectedDostawca", conn))
                    {

                        cmd4.Parameters.AddWithValue("@selectedDostawca", selectedDostawca);
                        using (SqlDataReader reader3 = cmd4.ExecuteReader())
                        {
                            if (reader3.Read())
                            {
                                // Dane Hodowcy
                                UlicaH.Text = reader3["Address"].ToString();
                                KodPocztowyH.Text = reader3["PostalCode"].ToString();
                                MiejscH.Text = reader3["City"].ToString();
                                KmH.Text = reader3["Distance"].ToString();

                                // Dane Finansowe
                                Dodatek.Text = reader3["Addition"].ToString();
                                Ubytek.Text = reader3["Loss"].ToString();

                                // Kontakt
                                tel1.Text = reader3["Phone1"].ToString();
                                tel2.Text = reader3["Phone2"].ToString();
                                tel3.Text = reader3["Phone3"].ToString();
                                info1.Text = reader3["Info1"].ToString();
                                info2.Text = reader3["Info2"].ToString();
                                info3.Text = reader3["Info3"].ToString();
                            }
                        }
                    }

                    // Wypełnienie ComboBox "LpWstawienia" wartościami z kolumny "Lp" w tabeli "dbo.WstawieniaKurczakow"
                    LpWstawienia.Items.Clear();
                    using (SqlCommand cmd5 = new SqlCommand("SELECT Lp FROM dbo.WstawieniaKurczakow WHERE Dostawca = @selectedDostawca ORDER BY Lp DESC", conn))
                    {
                        cmd5.Parameters.AddWithValue("@selectedDostawca", selectedDostawca);
                        using (SqlDataReader reader4 = cmd5.ExecuteReader())
                        {
                            while (reader4.Read())
                            {
                                LpWstawienia.Items.Add(reader4["Lp"].ToString());
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Błąd połączenia z bazą danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        private void Kurnik_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Obsługa zmiany ComboBox "Kurnik"
            string selectedDostawca = Kurnik.SelectedItem.ToString();

            // Tworzenie i otwieranie połączenia z bazą danych
            using (SqlConnection conn = new SqlConnection(connectionPermission))
            {
                try
                {
                    conn.Open();

                    // Tworzenie i wykonanie zapytania SQL
                    string query = "SELECT Address, PostalCode, City, Distance FROM [LibraNet].[dbo].[DostawcyAdresy] WHERE Name = @selectedDostawca";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@selectedDostawca", selectedDostawca);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Przypisanie wartości z bazy danych do TextBox-ów
                                UlicaK.Text = reader["Address"].ToString();
                                KodPocztowyK.Text = reader["PostalCode"].ToString();
                                MiejscK.Text = reader["City"].ToString();
                                KmK.Text = reader["Distance"].ToString();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd połączenia z bazą danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
            // Tworzenie nowej instancji formularza ObliczenieAut z przekazanymi wartościami
            ObliczenieAut obliczenieAut = new ObliczenieAut(sztukNaSzuflade.Text, liczbaAut.Text, sztuki.Text);

            // Wyświetlanie Form1
            obliczenieAut.ShowDialog();

            // Po zamknięciu Form2 odczytujemy wartości z jego właściwości i przypisujemy do kontrolki TextBox w Form1
            sztukNaSzuflade.Text = obliczenieAut.sztukiNaSzuflade;
            liczbaAut.Text = obliczenieAut.iloscAut;
            sztuki.Text = obliczenieAut.iloscSztuk;

            // Opcjonalnie, jeśli chcesz, aby użytkownik mógł interaktywnie korzystać z Form1 i wrócić do Form2, użyj form1.ShowDialog() zamiast form1.Show().
            // form1.ShowDialog();
        }
        private void cancelButton_Click(object sender, EventArgs e)
        {
            // Zamknij bieżący formularz
            this.Close();
        }
        private void dataWstawienia_TextChanged(object sender, EventArgs e)
        {
            // Sprawdź, czy oba TextBoxy zawierają daty
            if (!string.IsNullOrEmpty(dataWstawienia.Text) && Data.Value != null)
            {
                // Parsuj datę z TextBoxa dataWstawienia
                if (DateTime.TryParseExact(dataWstawienia.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dataWstawieniaValue))
                {
                    // Oblicz różnicę w dniach
                    TimeSpan roznica = Data.Value - dataWstawieniaValue;

                    // Wyświetl różnicę w dniach w TextBoxie RoznicaDni
                    int roznicaDni = (int)roznica.TotalDays;
                    RoznicaDni.Text = roznicaDni.ToString();
                }
                else
                {
                    // Obsługa błędu parsowania daty
                    // Możesz wyświetlić komunikat o błędzie lub podjąć inne działania w przypadku niepowodzenia parsowania daty
                    MessageBox.Show("Nieprawidłowy format daty wstawienia. Wprowadź datę w formacie yyyy-MM-dd", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private void CommandButton_Update_Click(object sender, EventArgs e)
        {
            string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

            try
            {
                using (SqlConnection cnn = new SqlConnection(connectionString))
                {
                    cnn.Open();

                    // Pobranie maksymalnego LP
                    string getMaxLpSql = "SELECT MAX(Lp) AS MaxLP FROM dbo.HarmonogramDostaw;";
                    SqlCommand getMaxLpCmd = new SqlCommand(getMaxLpSql, cnn);
                    int maxLP = Convert.ToInt32(getMaxLpCmd.ExecuteScalar()) + 1;

                    // Utworzenie zapytania SQL do wstawienia danych
                    string insertSql = @"
                    INSERT INTO dbo.HarmonogramDostaw 
                    (Lp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek, 
                    SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, DataUtw, LpW, Ubytek, ktoStwo) 
                    VALUES 
                    (@Lp, @DataOdbioru, @Dostawca, @KmH, @Kurnik, @KmK, @Auta, @SztukiDek, @WagaDek, 
                    @SztSzuflada, @TypUmowy, @TypCeny, @Cena, @Bufor, @UWAGI, @Dodatek, @DataUtw, @LpW, @Ubytek, @ktoStwo)";

                    SqlCommand insertCmd = new SqlCommand(insertSql, cnn);
                    insertCmd.Parameters.AddWithValue("@Lp", maxLP);
                    insertCmd.Parameters.AddWithValue("@DataOdbioru", string.IsNullOrEmpty(Data.Text) ? (object)DBNull.Value : DateTime.Parse(Data.Text).Date);
                    insertCmd.Parameters.AddWithValue("@Dostawca", string.IsNullOrEmpty(Dostawca.Text) ? (object)DBNull.Value : Dostawca.Text);
                    insertCmd.Parameters.AddWithValue("@Auta", string.IsNullOrEmpty(liczbaAut.Text) ? (object)DBNull.Value : int.Parse(liczbaAut.Text));
                    insertCmd.Parameters.AddWithValue("@KmH", string.IsNullOrEmpty(KmH.Text) ? (object)DBNull.Value : int.Parse(KmH.Text));
                    insertCmd.Parameters.AddWithValue("@KmK", string.IsNullOrEmpty(KmK.Text) ? (object)DBNull.Value : int.Parse(KmK.Text));
                    insertCmd.Parameters.AddWithValue("@Kurnik", string.IsNullOrEmpty(GID) ? (object)DBNull.Value : int.Parse(GID));
                    insertCmd.Parameters.AddWithValue("@SztukiDek", string.IsNullOrEmpty(sztuki.Text) ? (object)DBNull.Value : int.Parse(sztuki.Text));
                    insertCmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(srednia.Text) ? (object)DBNull.Value : decimal.Parse(srednia.Text));
                    insertCmd.Parameters.AddWithValue("@SztSzuflada", string.IsNullOrEmpty(sztukNaSzuflade.Text) ? (object)DBNull.Value : int.Parse(sztukNaSzuflade.Text));
                    insertCmd.Parameters.AddWithValue("@TypUmowy", string.IsNullOrEmpty(TypUmowy.Text) ? (object)DBNull.Value : TypUmowy.Text);
                    insertCmd.Parameters.AddWithValue("@TypCeny", string.IsNullOrEmpty(TypCeny.Text) ? (object)DBNull.Value : TypCeny.Text);
                    insertCmd.Parameters.AddWithValue("@Cena", string.IsNullOrEmpty(Cena.Text) ? (object)DBNull.Value : decimal.Parse(Cena.Text));
                    insertCmd.Parameters.AddWithValue("@Ubytek", string.IsNullOrEmpty(Ubytek.Text) ? (object)DBNull.Value : decimal.Parse(Ubytek.Text));
                    insertCmd.Parameters.AddWithValue("@Dodatek", string.IsNullOrEmpty(Dodatek.Text) ? (object)DBNull.Value : decimal.Parse(Dodatek.Text));
                    insertCmd.Parameters.AddWithValue("@Bufor", string.IsNullOrEmpty(Status.Text) ? (object)DBNull.Value : Status.Text);
                    insertCmd.Parameters.AddWithValue("@DataMod", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@DataUtw", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@KtoMod", UserID);
                    insertCmd.Parameters.AddWithValue("@ktoStwo", UserID);
                    insertCmd.Parameters.AddWithValue("@LpW", string.IsNullOrEmpty(LpWstawienia.Text) ? (object)DBNull.Value : int.Parse(LpWstawienia.Text));
                    insertCmd.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(Uwagi.Text) ? (object)DBNull.Value : Uwagi.Text);

                    // Wykonaj polecenie
                    insertCmd.ExecuteNonQuery();

                    // Aktualizacja danych dostawcy
                    string updateSupplierSql = @"
                    UPDATE dbo.Dostawcy 
                    SET Address = @Address, 
                        PostalCode = @PostalCode,
                        City = @City,
                        Phone1 = @Phone1,
                        Phone2 = @Phone2,
                        Phone3 = @Phone3,
                        info1 = @info1,
                        info2 = @info2,
                        info3 = @info3,
                        Distance = @Distance 
                    WHERE Shortname = @Shortname";

                    SqlCommand updateCmd = new SqlCommand(updateSupplierSql, cnn);
                    // Dodaj parametry do polecenia update
                    updateCmd.Parameters.AddWithValue("@Address", string.IsNullOrEmpty(UlicaH.Text) ? (object)DBNull.Value : UlicaH.Text);
                    updateCmd.Parameters.AddWithValue("@PostalCode", string.IsNullOrEmpty(KodPocztowyH.Text) ? (object)DBNull.Value : KodPocztowyH.Text);
                    updateCmd.Parameters.AddWithValue("@City", string.IsNullOrEmpty(MiejscH.Text) ? (object)DBNull.Value : MiejscH.Text);
                    updateCmd.Parameters.AddWithValue("@Phone1", string.IsNullOrEmpty(tel1.Text) ? (object)DBNull.Value : tel1.Text);
                    updateCmd.Parameters.AddWithValue("@Phone2", string.IsNullOrEmpty(tel2.Text) ? (object)DBNull.Value : tel2.Text);
                    updateCmd.Parameters.AddWithValue("@Phone3", string.IsNullOrEmpty(tel3.Text) ? (object)DBNull.Value : tel3.Text);
                    updateCmd.Parameters.AddWithValue("@info1", string.IsNullOrEmpty(info1.Text) ? (object)DBNull.Value : info1.Text);
                    updateCmd.Parameters.AddWithValue("@info2", string.IsNullOrEmpty(info2.Text) ? (object)DBNull.Value : info2.Text);
                    updateCmd.Parameters.AddWithValue("@info3", string.IsNullOrEmpty(info3.Text) ? (object)DBNull.Value : info3.Text);
                    updateCmd.Parameters.AddWithValue("@Distance", string.IsNullOrEmpty(KmH.Text) ? (object)DBNull.Value : int.Parse(KmH.Text));
                    updateCmd.Parameters.AddWithValue("@Shortname", string.IsNullOrEmpty(Dostawca.Text) ? (object)DBNull.Value : Dostawca.Text);

                    // Wykonaj polecenie
                    updateCmd.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas Dodawania wstawienia: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void label7_Click(object sender, EventArgs e)
        {

        }
    }
}
