using Microsoft.Data.SqlClient;

using Microsoft.IdentityModel.Tokens;

using Microsoft.VisualBasic.ApplicationServices;
using System;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Threading.Channels;
using System.Windows.Forms;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Security.Policy;
using System.Globalization;


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
        public void AktualizacjaPotwZDostaw(string lp, CheckBox checkBox, string columnName, string userID, string userColumnName, string dateColumnName)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Tworzenie dynamicznego zapytania SQL
                    string updateSQL = $"UPDATE [LibraNet].[dbo].[HarmonogramDostaw] SET {columnName} = @columnValue";
                    if (checkBox.Checked)
                    {
                        updateSQL += $", {userColumnName} = @userID, {dateColumnName} = @currentDate";
                    }
                    updateSQL += " WHERE LP = @lp";

                    using (SqlCommand command = new SqlCommand(updateSQL, connection))
                    {
                        command.Parameters.AddWithValue("@lp", lp);
                        command.Parameters.AddWithValue("@columnValue", checkBox.Checked ? "1" : "0");

                        if (checkBox.Checked)
                        {
                            command.Parameters.AddWithValue("@userID", userID);
                            command.Parameters.AddWithValue("@currentDate", DateTime.Now);
                        }

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Jeśli aktualizacja się powiodła
                        }
                        else
                        {
                            MessageBox.Show("Aktualizacja danych nie powiodła się.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        public void PobierzCheckBoxyWagSztuk(string lp, CheckBox checkBoxWaga, CheckBox checkBoxSztuki)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string strSQL = "SELECT PotwWaga, PotwSztuki FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LP = @lp";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@lp", lp);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                checkBoxWaga.Checked = reader["PotwWaga"].ToString() == "True";
                                checkBoxSztuki.Checked = reader["PotwSztuki"].ToString() == "True";
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
                                
                                KmH.Text = reader["KmH"].ToString();
                                //Kurnik.Text = reader["GID"].ToString();
                                KmK.Text = reader["KmK"].ToString();
                                Dostawca.Text = reader["Dostawca"].ToString();
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
        public void ZmianaDostawcy(ComboBox Dostawca, ComboBox Kurnik, TextBox UlicaK, TextBox KodPocztowyK, TextBox MiejscK, TextBox KmK, TextBox UlicaH, TextBox KodPocztowyH, TextBox MiejscH, TextBox KmH, TextBox Dodatek, TextBox Ubytek, TextBox tel1, TextBox tel2, TextBox tel3, TextBox info1, TextBox info2, TextBox info3, TextBox Email)
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
                    using (SqlCommand cmd4 = new SqlCommand("SELECT Address, PostalCode, City, Addition, Loss, Distance, Phone1, Phone2, Phone3, Info1, Info2, Info3, Email FROM [LibraNet].[dbo].[DOSTAWCY] WHERE Name = @selectedDostawca", conn))
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
                                Email.Text = reader3["Email"].ToString();
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

        public string ZnajdzNazweKierowcy(int name)
        {
            string driverName = "Brak Kierowcy"; // Jeśli nie znajdziemy kierowcy, zwrócimy "Brak Kierowcy"
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT Name FROM dbo.Driver WHERE GID = @GID", conn))
                {
                    cmd.Parameters.AddWithValue("@GID", name);

                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        driverName = Convert.ToString(result);
                    }
                }
            }
            return driverName;
        }
        public int ZnajdzIdHodowcy(string name)
        {
            int userId = -1; // Jeśli nie znajdziemy użytkownika, zwrócimy -1
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT ID FROM dbo.Dostawcy WHERE ShortName = @Name", conn))
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

        public static DateTime CombineDateAndTime(string godzina, DateTime data)
        {
            // Parsowanie godziny i minuty z formatu "00:00"
            TimeSpan timeOfDay;
            if (!TimeSpan.TryParseExact(godzina, "hh\\:mm", null, out timeOfDay))
            {
                throw new ArgumentException("Nieprawidłowy format godziny. Albo jest tylko 00:00");
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
        public string PobierzInformacjeZBazyDanychKonkretne(string lp, string kolumna)
        {
            string wartosc = null;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string strSQL = $"SELECT * FROM [LibraNet].[dbo].[HarmonogramDostaw] WHERE LP = @lp";
                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@lp", lp);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                wartosc = reader[kolumna].ToString();
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return wartosc;
        }
        public string PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(int ID, string Bazadanych, string kolumna)
        {
            string wartosc = null;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Budowanie zapytania z bezpośrednim wstawieniem nazw tabeli i kolumny
                    string strSQL = $"SELECT {kolumna} FROM {Bazadanych} WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@ID", ID);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                wartosc = reader[kolumna].ToString();
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return wartosc;
        }
        public T PobierzInformacjeZBazyDanych<T>(int ID, string Bazadanych, string kolumna)
        {
            T wartosc = default(T); // Wartość domyślna dla typu generycznego

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Budowanie zapytania z bezpośrednim wstawieniem nazw tabeli i kolumny
                    string strSQL = $"SELECT {kolumna} FROM {Bazadanych} WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@ID", ID);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal(kolumna))) // Sprawdzenie, czy wartość w kolumnie nie jest DBNull
                                {
                                    wartosc = (T)reader[kolumna];
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

            return wartosc;
        }


        public string PobierzInformacjeZBazyDanychHodowcow(int id, string kolumna)
        {
            string wartosc = null;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string strSQL = $"SELECT * FROM [LibraNet].[dbo].[Dostawcy] WHERE ID = @id";
                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                wartosc = reader[kolumna].ToString();
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return wartosc;
        }
        public string ZnajdzNazweCenyPoID(int id)
        {
            string wartosc = null;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string strSQL = $"SELECT * FROM [LibraNet].[dbo].[PriceType] WHERE ID = @id";
                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                wartosc = reader["Name"].ToString();
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą danych: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return wartosc;
        }
        public static T GetValueOrDefault<T>(DataRow row, string columnName, T defaultValue = default)
        {
            // Sprawdź, czy kolumna istnieje w wierszu
            if (!row.Table.Columns.Contains(columnName))
            {
                // Jeśli kolumna nie istnieje, zwróć wartość domyślną
                return defaultValue;
            }

            // Pobranie wartości kolumny jako obiektu
            object valueObject = row[columnName];

            // Jeśli wartość jest DBNull, zwróć wartość domyślną
            if (valueObject == DBNull.Value)
            {
                return defaultValue;
            }

            // Jeśli wartość nie jest DBNull, spróbuj ją skonwertować do typu T
            try
            {
                return (T)valueObject;
            }
            catch (InvalidCastException)
            {
                // Jeśli konwersja nie powiodła się, zwróć wartość domyślną
                return defaultValue;
            }
        }

        public void UzupelnijComboBoxHodowcami(ComboBox comboBox)
        {
            string query = "SELECT DISTINCT Name FROM dbo.DOSTAWCY where halt = '0'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                // Clear existing items
                comboBox.Items.Clear();

                while (reader.Read())
                {
                    string dostawca = reader["Name"].ToString();
                    comboBox.Items.Add(dostawca);
                }

                reader.Close();
            }
        }
        public void UzupelnijComboBoxKierowcami(ComboBox comboBox)
        {
            string query = "SELECT DISTINCT Name FROM dbo.Driver where halt = '0'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                // Clear existing items
                comboBox.Items.Clear();

                while (reader.Read())
                {
                    string dostawca = reader["Name"].ToString();
                    comboBox.Items.Add(dostawca);
                }

                reader.Close();
            }
        }
        public void UzupelnijComboBoxCiagnikami(ComboBox comboBox)
        {
            string query = "SELECT DISTINCT ID FROM dbo.CarTrailer where kind = '1'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                // Clear existing items
                comboBox.Items.Clear();

                while (reader.Read())
                {
                    string dostawca = reader["ID"].ToString();
                    comboBox.Items.Add(dostawca);
                }

                reader.Close();
            }
        }
        public void UzupelnijComboBoxNaczepami(ComboBox comboBox)
        {
            string query = "SELECT DISTINCT ID FROM dbo.CarTrailer where kind = '2'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                // Clear existing items
                comboBox.Items.Clear();

                while (reader.Read())
                {
                    string dostawca = reader["ID"].ToString();
                    comboBox.Items.Add(dostawca);
                }

                reader.Close();
            }
        }
        public void UzupełnienieDanychHodowcydoTextBoxow(ComboBox Dostawca, TextBox adres, TextBox kodPocztowy, TextBox miejscowosc, TextBox dystans, TextBox telefon1, TextBox telefon2, TextBox telefon3)
        {
            string selectedValue = Dostawca.SelectedItem.ToString();
            int idDostawcy = ZnajdzIdHodowcy(selectedValue);
            string Zmienna;
            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "Address");
            adres.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "PostalCode");
            kodPocztowy.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "Distance");
            dystans.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "City");
            miejscowosc.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "Phone1");
            telefon1.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "Phone2");
            telefon2.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcow(idDostawcy, "Phone3");
            telefon3.Text = Zmienna.ToString();

        }
        public void UpdateDaneAdresoweDostawcy(ComboBox Dostawca, TextBox Ulica, TextBox KodPocztowy, TextBox Miejscowosc, TextBox Dystans)

        {
            try
            {
                // Sprawdź, czy wybrano dostawcę
                if (Dostawca.SelectedItem != null)
                {
                    string selectedDostawca = Dostawca.SelectedItem.ToString();
                    // Utworzenie połączenia z bazą danych
                    using (SqlConnection cnn = new SqlConnection(connectionString))
                    {
                        cnn.Open();

                        // Utworzenie zapytania SQL do aktualizacji danych
                        string strSQL = @"UPDATE dbo.Dostawcy
                                  SET Address = @Ulica,
                                      PostalCode = @KodPocztowy,
                                      City = @Miejscowosc,
                                      Distance = @Dystans
                                  WHERE Name = @Dostawca AND halt = '0';";

                        using (SqlCommand command = new SqlCommand(strSQL, cnn))
                        {
                            // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                            command.Parameters.AddWithValue("@Dostawca", selectedDostawca);
                            command.Parameters.AddWithValue("@Ulica", string.IsNullOrEmpty(Ulica.Text) ? (object)DBNull.Value : Ulica.Text);
                            command.Parameters.AddWithValue("@KodPocztowy", string.IsNullOrEmpty(KodPocztowy.Text) ? (object)DBNull.Value : KodPocztowy.Text);
                            command.Parameters.AddWithValue("@Miejscowosc", string.IsNullOrEmpty(Miejscowosc.Text) ? (object)DBNull.Value : Miejscowosc.Text);
                            command.Parameters.AddWithValue("@Dystans", string.IsNullOrEmpty(Dystans.Text) ? (object)DBNull.Value : int.Parse(Dystans.Text));

                            // Wykonanie zapytania SQL
                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                // Zaktualizowano dane pomyślnie
                            }
                            else
                            {
                                MessageBox.Show("Nie udało się zaktualizować DANYCH ADRESOWYCH", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Proszę wybrać dostawcę", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void UpdateDaneKontaktowe(ComboBox Dostawca, TextBox Phone1, TextBox Info1, TextBox Phone2, TextBox Info2, TextBox Phone3, TextBox Info3, TextBox Email)

        {
            try
            {
                // Sprawdź, czy wybrano dostawcę
                if (Dostawca.SelectedItem != null)
                {
                    string selectedDostawca = Dostawca.SelectedItem.ToString();
                    // Utworzenie połączenia z bazą danych
                    using (SqlConnection cnn = new SqlConnection(connectionString))
                    {
                        cnn.Open();

                        // Utworzenie zapytania SQL do aktualizacji danych
                        string strSQL = @"UPDATE dbo.Dostawcy
                                  SET phone1 = @Phone1,
                                      phone2 = @Phone2,
                                      phone3 = @Phone3,
                                      info1 = @info1,
                                      info2 = @info2,
                                      info3 = @info3,
                                      Email = @Email
                                  WHERE Name = @Dostawca AND halt = '0';";

                        using (SqlCommand command = new SqlCommand(strSQL, cnn))
                        {
                            // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                            command.Parameters.AddWithValue("@Dostawca", selectedDostawca);
                            command.Parameters.AddWithValue("@Phone1", string.IsNullOrEmpty(Phone1.Text) ? (object)DBNull.Value : Phone1.Text);
                            command.Parameters.AddWithValue("@Phone2", string.IsNullOrEmpty(Phone2.Text) ? (object)DBNull.Value : Phone2.Text);
                            command.Parameters.AddWithValue("@Phone3", string.IsNullOrEmpty(Phone3.Text) ? (object)DBNull.Value : Phone3.Text);
                            command.Parameters.AddWithValue("@info1", string.IsNullOrEmpty(Info1.Text) ? (object)DBNull.Value : Info1.Text);
                            command.Parameters.AddWithValue("@info2", string.IsNullOrEmpty(Info2.Text) ? (object)DBNull.Value : Info2.Text);
                            command.Parameters.AddWithValue("@info3", string.IsNullOrEmpty(Info3.Text) ? (object)DBNull.Value : Info3.Text);
                            command.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(Email.Text) ? (object)DBNull.Value : Email.Text);

                            // Wykonanie zapytania SQL
                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                // Zaktualizowano dane pomyślnie
                            }
                            else
                            {
                                MessageBox.Show("Nie udało się zaktualizować DANYCH ADRESOWYCH", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Proszę wybrać dostawcę", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void UpdateDaneRozliczenioweAvilogHodowca(int IdSpecyfikacji, TextBox FullFarmWeight, TextBox EmptyFarmWeight, TextBox NettoFarmWeight, TextBox AvWeightFarm, TextBox PiecesFarm, TextBox SztPoj)

        {
            try
            {
                    using (SqlConnection cnn = new SqlConnection(connectionString))
                    {
                        cnn.Open();

                        // Utworzenie zapytania SQL do aktualizacji danych
                        string strSQL = @"UPDATE dbo.FarmerCalc
                                  SET FullFarmWeight = @FullFarmWeight,
                                      EmptyFarmWeight = @EmptyFarmWeight,
                                      NettoFarmWeight = @NettoFarmWeight,
                                      AvWeightFarm = @AvWeightFarm,
                                      PiecesFarm = @PiecesFarm,
                                      SztPoj = @SztPoj
                                  WHERE ID = @ID";

                        using (SqlCommand command = new SqlCommand(strSQL, cnn))
                        {
                            // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                            command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                            command.Parameters.AddWithValue("@FullFarmWeight", string.IsNullOrEmpty(FullFarmWeight.Text) ? (object)DBNull.Value : decimal.Parse(FullFarmWeight.Text));
                            command.Parameters.AddWithValue("@EmptyFarmWeight", string.IsNullOrEmpty(EmptyFarmWeight.Text) ? (object)DBNull.Value : decimal.Parse(EmptyFarmWeight.Text));
                            command.Parameters.AddWithValue("@NettoFarmWeight", string.IsNullOrEmpty(NettoFarmWeight.Text) ? (object)DBNull.Value : decimal.Parse(NettoFarmWeight.Text));
                            command.Parameters.AddWithValue("@AvWeightFarm", string.IsNullOrEmpty(AvWeightFarm.Text) ? (object)DBNull.Value : decimal.Parse(AvWeightFarm.Text));
                            command.Parameters.AddWithValue("@PiecesFarm", string.IsNullOrEmpty(PiecesFarm.Text) ? (object)DBNull.Value : int.Parse(PiecesFarm.Text));
                            command.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj.Text) ? (object)DBNull.Value : decimal.Parse(SztPoj.Text));

                            // Wykonanie zapytania SQL
                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                // Zaktualizowano dane pomyślnie
                            }
                            else
                            {
                                MessageBox.Show("Nie udało się zaktualizować DANYCH ROZLICZENIOWYCH", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void UpdateDaneRozliczenioweAvilogUbojnia(int IdSpecyfikacji, TextBox FullFarmWeight, TextBox EmptyFarmWeight, TextBox NettoFarmWeight, TextBox AvWeightFarm, TextBox PiecesFarm, TextBox SztPoj)
        {
            try
            {
                // Utworzenie połączenia z bazą danych
                using (SqlConnection cnn = new SqlConnection(connectionString))
                {
                    cnn.Open();

                    // Utworzenie zapytania SQL do aktualizacji danych
                    string strSQL = @"UPDATE dbo.FarmerCalc
                                  SET FullWeight = @FullFarmWeight,
                                      EmptyWeight = @EmptyFarmWeight,
                                      NettoWeight = @NettoFarmWeight,
                                      AvWeight = @AvWeightFarm,
                                      Pieces = @PiecesFarm,
                                      SztPoj = @SztPoj
                                  WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, cnn))
                    {
                        // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                        command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                        command.Parameters.AddWithValue("@FullFarmWeight", string.IsNullOrEmpty(FullFarmWeight.Text) ? (object)DBNull.Value : decimal.Parse(FullFarmWeight.Text));
                        command.Parameters.AddWithValue("@EmptyFarmWeight", string.IsNullOrEmpty(EmptyFarmWeight.Text) ? (object)DBNull.Value : decimal.Parse(EmptyFarmWeight.Text));
                        command.Parameters.AddWithValue("@NettoFarmWeight", string.IsNullOrEmpty(NettoFarmWeight.Text) ? (object)DBNull.Value : decimal.Parse(NettoFarmWeight.Text));
                        command.Parameters.AddWithValue("@AvWeightFarm", string.IsNullOrEmpty(AvWeightFarm.Text) ? (object)DBNull.Value : decimal.Parse(AvWeightFarm.Text));
                        command.Parameters.AddWithValue("@PiecesFarm", string.IsNullOrEmpty(PiecesFarm.Text) ? (object)DBNull.Value : int.Parse(PiecesFarm.Text));
                        command.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj.Text) ? (object)DBNull.Value : decimal.Parse(SztPoj.Text));

                        // Wykonanie zapytania SQL
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Zaktualizowano dane pomyślnie
                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować DANYCH ROZLICZENIOWYCH", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateDaneAutKierowcy(int IdSpecyfikacji, ComboBox Kierowca, ComboBox Ciagnik, ComboBox Naczepa)

                {
                    try
                    {
                        using (SqlConnection cnn = new SqlConnection(connectionString))
                        {
                            cnn.Open();

                            string driverName = Kierowca.SelectedItem.ToString();
                            int idKierowcy = ZnajdzIdKierowcy(driverName);
                            // Utworzenie zapytania SQL do aktualizacji danych
                            string strSQL = @"UPDATE dbo.FarmerCalc
                                  SET CarID = @Ciagnik,
                                      DriverGID = @Kierowca,
                                      TrailerID = @Naczepa
                                  WHERE ID = @ID";

                            using (SqlCommand command = new SqlCommand(strSQL, cnn))
                            {
                                // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                                command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                                command.Parameters.AddWithValue("@Kierowca", idKierowcy);
                                command.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik.Text) ? (object)DBNull.Value : Ciagnik.Text);
                                command.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa.Text) ? (object)DBNull.Value : Naczepa.Text);

                                // Wykonanie zapytania SQL
                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    // Zaktualizowano dane pomyślnie
                                }
                                else
                                {
                                    MessageBox.Show("Nie udało się zaktualizować DANYCH ROZLICZENIOWYCH", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                public void UpdateDaneDystansu(int IdSpecyfikacji, TextBox KMstart, TextBox KMkoniec, TextBox KMdystans)

                {
                    try
                    {
                        using (SqlConnection cnn = new SqlConnection(connectionString))
                        {
                            cnn.Open();
                            string strSQL = @"UPDATE dbo.FarmerCalc
                                  SET StartKM = @KMstart,
                                      StopKM = @KMkoniec,
                                      DistanceKM = @KMdystans
                                  WHERE ID = @ID";

                            using (SqlCommand command = new SqlCommand(strSQL, cnn))
                            {
                                // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                                command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                                command.Parameters.AddWithValue("@KMstart", string.IsNullOrEmpty(KMstart.Text) ? (object)DBNull.Value : int.Parse(KMstart.Text));
                                command.Parameters.AddWithValue("@KMkoniec", string.IsNullOrEmpty(KMkoniec.Text) ? (object)DBNull.Value : int.Parse(KMkoniec.Text));
                                command.Parameters.AddWithValue("@KMdystans", string.IsNullOrEmpty(KMdystans.Text) ? (object)DBNull.Value : int.Parse(KMdystans.Text));
                                // Wykonanie zapytania SQL
                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    // Zaktualizowano dane pomyślnie
                                }
                                else
                                {
                                    MessageBox.Show("Nie udało się zaktualizować DANYCH Kilometrowych", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                public void UpdateCzas(int IdSpecyfikacji, String kolumna, DateTimePicker PoczatekUslugi)
                {
                    try
                    {
                        using (SqlConnection cnn = new SqlConnection(connectionString))
                        {
                            cnn.Open();

                            // Pobierz datę z kolumny CalcDate
                            string queryDate = "SELECT CalcDate FROM dbo.FarmerCalc WHERE ID = @ID";
                            using (SqlCommand dateCommand = new SqlCommand(queryDate, cnn))
                            {
                                dateCommand.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                                DateTime calcDate = Convert.ToDateTime(dateCommand.ExecuteScalar());

                                // Aktualizuj godzinę i minutę w zaktualizowanym PoczatekUslugi
                                DateTime updatedDateTime = new DateTime(
                                    calcDate.Year,
                                    calcDate.Month,
                                    calcDate.Day,
                                    PoczatekUslugi.Value.Hour,
                                    PoczatekUslugi.Value.Minute,
                                    0 // Zeruj sekundy
                                );

                                // Jeśli godzina jest między 17 a 23, zmniejsz datę o jeden dzień
                                if (PoczatekUslugi.Value.Hour >= 17 && PoczatekUslugi.Value.Hour <= 23)
                                {
                                    updatedDateTime = updatedDateTime.AddDays(-1);
                                }

                                // Zaktualizuj rekord w bazie danych
                                string strSQL = $"UPDATE dbo.FarmerCalc SET {kolumna} = @Zmienna WHERE ID = @ID";

                                using (SqlCommand command = new SqlCommand(strSQL, cnn))
                                {
                                    command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                                    command.Parameters.AddWithValue("@Zmienna", updatedDateTime);

                                    int rowsAffected = command.ExecuteNonQuery();

                                    if (rowsAffected > 0)
                                    {
                                       
                                    }
                                    else
                                    {
                                        MessageBox.Show("Nie udało się zaktualizować danych", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
        public void OtworzGoogleMaps(TextBox UlicaK, TextBox KodPocztowyK)
        {
            string przegladarka = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
            string alternatywnaPrzegladarka = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            string adresZrodlowy = "Koziołki 40, 95-061 Dmosin";
            string adresDocelowy = $"{UlicaK.Text}, {KodPocztowyK.Text}";

            // Sprawdzenie, czy pierwotna lokalizacja istnieje
            if (!File.Exists(przegladarka))
            {
                // Lokalizacja nie istnieje, zmień na alternatywną lokalizację
                przegladarka = alternatywnaPrzegladarka;
            }

            // Zamiana spacji na znaki +
            adresZrodlowy = adresZrodlowy.Replace(" ", "+");
            adresDocelowy = adresDocelowy.Replace(" ", "+");

            // Tworzenie URL
            string adres = $"https://www.google.com/maps/dir/{adresZrodlowy}/{adresDocelowy}";

            // Uruchomienie przeglądarki z odpowiednim adresem
            Process.Start(przegladarka, adres);
        }

        public void OtworzCenyRolne()
        {
            string url = "https://www.cenyrolnicze.pl/wiadomosci/rynki-rolne/drob";

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć strony: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OtworzCenyTuszki()
        {
            string url = "https://www.cenyrolnicze.pl/mieso/drob";

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć strony: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void OtworzCenyMinistra()
        {
            string url = "https://www.gov.pl/web/rolnictwo/rynek-drobiu";

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć strony: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ObliczanieUbytkuTransportowegoNaPodstawieKM(TextBox inputTextBox, TextBox resultTextBox)
        {
            double inputValue;
            double result;

            // Sprawdzanie, czy TextBox1 zawiera liczbę
            if (double.TryParse(inputTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out inputValue))
            {
                // Obliczenia w zależności od wartości w TextBox1
                if (inputValue <= 100)
                {
                    result = inputValue * 0.01;
                }
                else
                {
                    result = 1 + (inputValue - 100) * 0.007;
                }

                // Sformatuj wynik jako liczba z kropką jako separatorem dziesiętnym
                resultTextBox.Text = result.ToString("0.00", CultureInfo.InvariantCulture);
            }
            else
            {
                // Jeśli TextBox1 nie zawiera liczby, wyczyść TextBox2
                resultTextBox.Text = "";
            }
        }

    }
    // Metoda pomocnicza do otwierania URL w domyślnej przeglądarce

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
            public double PobierzCeneTuszkiDzisiaj()
            {
                string zapytanie = "SELECT Cena FROM CenaTuszki WHERE Data = @Data";
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
            public double PobierzSredniaCeneWolnorynkowa()
            {
                double sredniaCena = 0.0;

                string zapytanie = @"
            SELECT 
                SUM(CAST(Auta AS DECIMAL(10, 2)) * CAST(Cena AS DECIMAL(10, 2))) / NULLIF(SUM(CAST(Auta AS DECIMAL(10, 2))), 0) AS SredniaCena
            FROM 
                [LibraNet].[dbo].[HarmonogramDostaw]
            WHERE 
                (TypCeny = 'Wolnorynkowa' OR TypCeny = 'wolnorynkowa')
                AND Bufor = 'Potwierdzony'
                AND DataOdbioru >= CAST(GETDATE() AS DATE)
                AND DataOdbioru < DATEADD(DAY, 1, CAST(GETDATE() AS DATE))
            ";

                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand command = new SqlCommand(zapytanie, connection))
                {
                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            sredniaCena = Convert.ToDouble(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Błąd: " + ex.Message);
                    }
                }

                return sredniaCena;
            }


}

