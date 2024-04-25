using Microsoft.Data.SqlClient;
using System;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Data;
using System.Windows.Forms;


namespace Kalendarz1
{
    public static class Utility
    {
        public static string NullOrValue(object inputValue)
        {
            if (inputValue == null || string.IsNullOrWhiteSpace(inputValue.ToString()))
            {
                return "NULL";
            }
            else
            {
                return $"'{inputValue}'";
            }
        }
    }

    public class NazwaZiD
    {
        static string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public string GetNameById(string id)
        {
            string name = null;

            // Tworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Tworzenie zapytania SQL
                string query = "SELECT Name FROM [LibraNet].[dbo].[operators] WHERE ID = @Id";

                // Tworzenie obiektu SqlCommand
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Dodawanie parametru do zapytania SQL
                    command.Parameters.AddWithValue("@Id", id);

                    // Otwieranie połączenia
                    connection.Open();

                    // Wykonywanie zapytania i odczytywanie wyniku
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Pobieranie wartości z kolumny "Name"
                            name = reader["Name"].ToString();
                        }
                    }
                }
            }

            // Zwracanie nazwy
            return name;
        }
        public void publicPobierzInformacjeZBazyDanych(string lp, ComboBox LpWstawienia, ComboBox Status, DateTimePicker Data, ComboBox Dostawca, TextBox KmH, TextBox KmK, TextBox liczbaAut, TextBox srednia, TextBox sztukNaSzuflade
            , TextBox sztuki, ComboBox TypUmowy, ComboBox TypCeny, TextBox Cena, TextBox Uwagi, TextBox Dodatek, TextBox dataStwo, TextBox dataMod, TextBox Ubytek, TextBox ktoMod, TextBox ktoStwo)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
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
                                dataMod.Text = reader["DataMod"].ToString();
                                ktoStwo.Text = reader["ktoStwo"].ToString();
                                ktoMod.Text = reader["ktoMod"].ToString();
                                Ubytek.Text = reader["Ubytek"].ToString();
                                LpWstawienia.Text = reader["LpW"].ToString();
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
        public void UpdateValueInDatabase(double newValue, string columnName, string lpDostawa)
        {
            try
            {
                // Utwórz połączenie z bazą danych
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // Otwórz połączenie
                    connection.Open();

                    // Skonstruuj zapytanie SQL do aktualizacji wartości w bazie danych
                    string query = $"UPDATE [LibraNet].[dbo].[HarmonogramDostaw] SET {columnName} = @NewValue WHERE Lp = @LpDostawa";

                    // Utwórz obiekt SqlCommand
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Dodaj parametry do zapytania SQL
                        command.Parameters.AddWithValue("@NewValue", newValue);
                        command.Parameters.AddWithValue("@LpDostawa", lpDostawa);

                        // Wykonaj zapytanie SQL
                        int rowsAffected = command.ExecuteNonQuery();

                        // Sprawdź, czy zapytanie zostało wykonane poprawnie
                    }
                }
            }
            catch (Exception ex)
            {
                // Wystąpił błąd podczas aktualizacji wartości w bazie danych
                MessageBox.Show("Błąd podczas aktualizacji wartości w bazie danych: " + ex.Message);
            }
        }
        public void ReplaceCommaWithDot(TextBox textBox)
        {
            string text = textBox.Text;
            if (text.Contains("."))
            {
                text = text.Replace(".", ",");
                textBox.Text = text;
                textBox.SelectionStart = textBox.Text.Length;
            }

        }
        public void ZmianaDostawcy(ComboBox Dostawca, ComboBox Kurnik, TextBox UlicaK, TextBox KodPocztowyK, TextBox MiejscK, TextBox KmK, TextBox UlicaH, TextBox KodPocztowyH, TextBox MiejscH, TextBox KmH, TextBox Dodatek, TextBox Ubytek, TextBox tel1, TextBox tel2, TextBox tel3, TextBox info1, TextBox info2, TextBox info3)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz wartość wybraną w ComboBox "dostawca"
                    string selectedDostawca = Dostawca.Text;
                    string GID;
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
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Błąd połączenia z bazą danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        public void WypelnienieLpWstawienia(ComboBox dostawcaComboBox, ComboBox lpWstawieniaComboBox)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string selectedDostawca = dostawcaComboBox.Text;
                    lpWstawieniaComboBox.Items.Clear();

                    // Użyj parametryzowanego zapytania SQL, aby zapobiec atakom SQL Injection
                    string query = "SELECT Lp FROM dbo.WstawieniaKurczakow WHERE Dostawca = @selectedDostawca ORDER BY Lp DESC";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@selectedDostawca", selectedDostawca);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lpWstawieniaComboBox.Items.Add(reader["Lp"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    public class ZapytaniaSQL
    {
        static string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public int ZnajdzIdKierowcy(string name)
        {
            int userId = -1; // Jeśli nie znajdziemy użytkownika, zwrócimy -1
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT GID FROM dbo.Driver WHERE Name = @Name", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);

                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        userId = Convert.ToInt32(result);
                    }
                }
            }
            return userId;
        }
        public int ZnajdzIdHodowcy(string name)
        {
            int userId = -1; // Jeśli nie znajdziemy użytkownika, zwrócimy -1
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT GID FROM dbo.Dostawcy WHERE ShortName = @Name", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);

                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        userId = Convert.ToInt32(result);
                    }
                }
            }
            return userId;
        }
        public int ZnajdzIdCeny(string name)
        {
            int userId = -1; // Jeśli nie znajdziemy użytkownika, zwrócimy -1
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT ID FROM dbo.PriceType WHERE Name = @Name", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);

                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        userId = Convert.ToInt32(result);
                    }
                }
            }
            return userId;
        }

        public static DateTime CombineDateAndTime(string godzina, DateTime data)
        {
            // Parsowanie godziny i minuty z formatu "00:00"
            TimeSpan timeOfDay;
            if (!TimeSpan.TryParseExact(godzina, "hh\\:mm", null, out timeOfDay))
            {
                throw new ArgumentException("Nieprawidłowy format godziny.");
            }

            // Tworzenie nowego obiektu DateTime z daty oraz godziny i minuty
            DateTime combinedDateTime = new DateTime(data.Year, data.Month, data.Day, timeOfDay.Hours, timeOfDay.Minutes, 0);

            return combinedDateTime;
        }
        public string DodajDwukropek(string input)
        {
            // Sprawdź, czy input ma co najmniej 3 znaki
            if (input.Length >= 3)
            {
                // Dodaj ":" w trzecim miejscu i zwróć zmodyfikowany ciąg
                return input.Substring(0, 2) + ":" + input.Substring(2);
            }
            else
            {
                // Jeśli input ma mniej niż 3 znaki, zwróć input bez zmian
                return input;
            }
        }

    }
    public class CenoweMetody
        {
            static string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

            public double PobierzCeneRolniczaDzisiaj()
            {
                string zapytanie = "SELECT Cena FROM CenaRolnicza WHERE Data = @Data";
                double cena = 0.0;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(zapytanie, connection);
                    command.Parameters.AddWithValue("@Data", DateTime.Today);

                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        cena = (result != null && result != DBNull.Value) ? Convert.ToDouble(result) : 0.0;
                    }
                    catch (Exception ex)
                    {
                        // Obsługa błędów
                        Console.WriteLine("Błąd: " + ex.Message);
                    }
                }

                return cena;
            }

            public double PobierzCeneMinisterialnaDzisiaj()
            {
                string zapytanie = "SELECT Cena FROM CenaMinisterialna WHERE Data = @Data";
                double cena = 0.0;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(zapytanie, connection);
                    command.Parameters.AddWithValue("@Data", DateTime.Today);

                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        cena = (result != null && result != DBNull.Value) ? Convert.ToDouble(result) : 0.0;
                    }
                    catch (Exception ex)
                    {
                        // Obsługa błędów
                        Console.WriteLine("Błąd: " + ex.Message);
                    }
                }

                return cena;
            }

            public double PobierzCeneKurczakaA()
            {
                double cena = 0.0;

                string zapytanie = @"
            SELECT
               ROUND(SUM(DP.[wartNetto]) / SUM(DP.[ilosc]), 2) AS Cena
            FROM [HANDEL].[HM].[DP] DP 
            INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id] 
            INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id] 
            WHERE DP.[data] >= CAST(GETDATE() AS DATE)
              AND DP.[data] < DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) 
              AND DP.[kod] = 'Kurczak A' 
              AND TW.[katalog] = 67095
            GROUP BY DP.[kod], CONVERT(date, DP.[data])";

                using (SqlConnection connection = new SqlConnection(connectionString2))
                using (SqlCommand command = new SqlCommand(zapytanie, connection))
                {
                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            cena = Convert.ToDouble(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Błąd: " + ex.Message);
                    }
                }

                return cena;
            }
        }
    }

