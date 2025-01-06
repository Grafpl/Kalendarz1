using Microsoft.Data.SqlClient;
using System;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Data;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing; // Dodaj tę dyrektywę



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
            , TextBox sztuki, ComboBox TypUmowy, ComboBox TypCeny, TextBox Cena, TextBox Uwagi, TextBox Dodatek, TextBox dataStwo, TextBox dataMod, TextBox Ubytek, TextBox ktoMod, TextBox ktoStwo, TextBox ktoWaga, TextBox KiedyWaga, TextBox KtoSztuki, TextBox KiedySztuki)
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
                                string idModyfikujacego = GetNameById(reader["ktoMod"].ToString()), idTworzacego = GetNameById(reader["ktoStwo"].ToString());
                                string idWagi = GetNameById(reader["KtoWaga"].ToString()), idSztuki = GetNameById(reader["KtoSztuki"].ToString());
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
                                ktoStwo.Text = idTworzacego;
                                ktoMod.Text = idModyfikujacego;
                                Ubytek.Text = reader["Ubytek"].ToString();
                                LpWstawienia.Text = reader["LpW"].ToString();
                                ktoWaga.Text = idWagi;
                                KiedyWaga.Text = reader["KiedyWaga"].ToString();
                                KtoSztuki.Text = idSztuki;
                                KiedySztuki.Text = reader["KiedySztuki"].ToString();
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

        public void PokazPojTuszki(DataGridView dataGrid)
        {
            // Utwórz połączenie z bazą danych SQL Server
            using (SqlConnection cnn = new SqlConnection(connectionString))
            {
                cnn.Open();

                string strSQL = @"
        SELECT 
            k.CreateData AS Data, 
            k.QntInCont AS poj, 
            COUNT(*) AS Palety
        FROM [LibraNet].[dbo].[In0E] K
        JOIN [LibraNet].[dbo].[PartiaDostawca] Partia ON K.P1 = Partia.Partia
        LEFT JOIN [LibraNet].[dbo].[HarmonogramDostaw] hd ON k.CreateData = hd.DataOdbioru AND Partia.CustomerName = hd.Dostawca
        WHERE ArticleID = 40 
            AND k.QntInCont > 4
            AND k.CreateData = CAST(GETDATE() AS DATE)
        GROUP BY k.CreateData, k.QntInCont
        ORDER BY k.CreateData DESC, k.QntInCont DESC;
        ";

                using (SqlCommand command = new SqlCommand(strSQL, cnn))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        try
                        {
                            // Przygotowanie DataGrid
                            dataGrid.Rows.Clear();
                            dataGrid.Columns.Clear();
                            dataGrid.RowHeadersVisible = false;

                            // Dodaj kolumny do DataGrid
                            if (dataGrid.Columns["Data"] == null)
                            {
                                dataGrid.Columns.Add("Data", "Data");
                            }
                            if (dataGrid.Columns["poj"] == null)
                            {
                                dataGrid.Columns.Add("poj", "poj");
                            }
                            if (dataGrid.Columns["Palety"] == null)
                            {
                                dataGrid.Columns.Add("Palety", "Palety");
                            }

                            // Ustawienia kolumn
                            dataGrid.Columns["Data"].Visible = false;
                            dataGrid.Columns["poj"].Width = 35;
                            dataGrid.Columns["Palety"].Width = 45;

                            // Dodaj dane do DataGrid
                            while (reader.Read())
                            {
                                DataGridViewRow newRow = new DataGridViewRow();
                                newRow.CreateCells(dataGrid);
                                newRow.Cells[0].Value = reader["Data"];
                                newRow.Cells[1].Value = reader["poj"];
                                newRow.Cells[2].Value = reader["Palety"];

                                dataGrid.Rows.Add(newRow);
                            }

                            // Ustawienia dla wierszy
                            foreach (DataGridViewRow row in dataGrid.Rows)
                            {
                                row.Height = 17; // Wysokość wierszy
                                foreach (DataGridViewCell cell in row.Cells)
                                {
                                    cell.Style.Font = new Font("Arial", 7); // Czcionka w wierszach
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Błąd odczytu danych: " + ex.Message);
                        }
                    }
                }
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
    public class RozwijanieComboBox
    {
        private string connectionStringSymfonia = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public void RozwijanieKontrPoKatalogu(ComboBox comboBox, string katalog)
        {
            string query = $@"
                SELECT DISTINCT [vKh].[id], [vKh].[kod]
                FROM [SSCommon].[vKontrahenci] AS [vKh]
                INNER JOIN [HM].[ContractorCatalog] AS [ContrCata]
                    ON [vKh].[katalog] = CAST([ContrCata].[id] AS VARCHAR(MAX))
                WHERE [ContrCata].[Name] LIKE '{katalog}' Order by [vKh].[kod] DESC";

            using (SqlConnection connection = new SqlConnection(connectionStringSymfonia))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string kod = reader["kod"].ToString();
                    string id = reader["id"].ToString();


                    // Dodawanie KeyValuePair do ComboBox
                    comboBox.Items.Add(new KeyValuePair<string, string>(id, kod));
                }

                reader.Close();
            }

            // Ustawienie wyświetlania tylko wartości 'kod' w ComboBox
            comboBox.DisplayMember = "Value"; // Wyświetlany tekst
            comboBox.ValueMember = "Key";    // Ukryty klucz
        }


        public enum DaneKontrahenta
        {Kod=0, Limit=1, Nazwa=2, NIP=3, KodPocztowy=4, Miejscowosc=5}


        public string PobierzNazweKolumny(DaneKontrahenta kolumna)
        {
            return kolumna switch
            {
                DaneKontrahenta.Kod => "kod",
                DaneKontrahenta.Limit => "limitKwota",
                DaneKontrahenta.Nazwa => "nazwa",
                DaneKontrahenta.NIP=> "nip",
                DaneKontrahenta.KodPocztowy => "kodpocz",
                DaneKontrahenta.Miejscowosc => "miejscowosc",
                _ => throw new ArgumentException("Nieznana kolumna.")
            };
        }

        public void PrzypiszDaneOdbiorcySYMF(string id, DaneKontrahenta kolumna, TextBox textBox)
        {
            string columnName = PobierzNazweKolumny(kolumna);

            string query = $@"
            SELECT [{columnName}]
            FROM [SSCommon].[vKontrahenci]
            WHERE [id] = @Id";

            using (SqlConnection connection = new SqlConnection(connectionStringSymfonia))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                if (reader.Read())
                {
                    // Pobierz wartość kolumny i przypisz do TextBox
                    string value = reader[columnName].ToString();
                    textBox.Text = value;
                }

                reader.Close();
            }
        }
    }

    public class DataService
    {
        private readonly string connectionStringSymfonia = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        internal Dictionary<RozwijanieComboBox.DaneKontrahenta, string> 
            PobierzDaneOdbiorcy(string id)
        {
            var dane = new Dictionary<RozwijanieComboBox.DaneKontrahenta, string>();
            string query = @"
            SELECT 
                kod, limitKwota, nazwa, nip, kodpocz, miejscowosc
            FROM [SSCommon].[vKontrahenci]
            WHERE id = @Id";

            using (SqlConnection connection = new SqlConnection(connectionStringSymfonia))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                if (reader.Read())
                {
                    dane[RozwijanieComboBox.DaneKontrahenta.Kod] = reader["kod"].ToString();
                    dane[RozwijanieComboBox.DaneKontrahenta.Limit] = reader["limitKwota"].ToString();
                    dane[RozwijanieComboBox.DaneKontrahenta.Nazwa] = reader["nazwa"].ToString();
                    dane[RozwijanieComboBox.DaneKontrahenta.NIP] = reader["nip"].ToString();
                    dane[RozwijanieComboBox.DaneKontrahenta.KodPocztowy] = reader["kodpocz"].ToString();
                    dane[RozwijanieComboBox.DaneKontrahenta.Miejscowosc] = reader["miejscowosc"].ToString();
                }

                reader.Close();
            }

            return dane;
        }

        public string PobierzHandlowca(string id)
        {
            string query = @"
            SELECT ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
            FROM [HANDEL].[SSCommon].[STContractors] C
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
            WHERE C.Id = @Id";

            using (SqlConnection connection = new SqlConnection(connectionStringSymfonia))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                if (reader.Read())
                {
                    return reader["Handlowiec"].ToString();
                }

                return "-";
            }
        }

        public double WydajnoscElement(double tuszka, int kod)
        {
            // Wynikowa wartość
            double wynik = 0;

            // Oblicz wydajność w zależności od kodu
            switch (kod)
            {
                case 66443: // Kurczak A
                    wynik = tuszka; // Kurczak A to cały tuszka
                    break;

                case 66444: // Noga
                    wynik = tuszka * 0.37;
                    break;

                case 66445: // Filet A
                    wynik = tuszka * 0.295;
                    break;

                case 66442: // Korpus
                    wynik = tuszka * 0.235;
                    break;

                case 66818: // Skrzydło I
                    wynik = tuszka * 0.09;
                    break;

                default:
                    wynik = 0; // Kod nieobsługiwany
                    break;
            }

            return wynik;
        }

    }



    public class ZapytaniaSQL
    {
        static string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public string idHodowcy { get; set; }
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
                using (SqlCommand cmd = new SqlCommand("SELECT ID FROM dbo.Dostawcy WHERE Name = @Name", conn))
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
        public string ZnajdzIdHodowcyString(string name)
        {
            string userId = null; // Jeśli nie znajdziemy użytkownika, zwrócimy null
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT ID FROM dbo.Dostawcy WHERE Name = @Name" +
                    "", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);

                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        userId = result.ToString();
                    }
                }
            }
            return userId;
        }
        public string PobierzInformacjeZBazyDanychHodowcowString(string id, string kolumna)
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
            // Sprawdzenie, czy godzina jest pusta lub null
            if (string.IsNullOrWhiteSpace(godzina) || godzina == "00:00")
            {
                // Jeśli godzina to "00:00" lub jest pusta, zwróć datę z godziną 00:00
                return data.Date;
            }

            // Parsowanie godziny i minuty z formatu "hh:mm"
            if (TimeSpan.TryParseExact(godzina, "hh\\:mm", null, out TimeSpan timeOfDay))
            {
                // Tworzenie nowego obiektu DateTime z daty oraz godziny i minuty
                return new DateTime(data.Year, data.Month, data.Day, timeOfDay.Hours, timeOfDay.Minutes, 0);
            }
            else
            {
                // Rzucenie wyjątku, jeśli godzina jest w nieprawidłowym formacie
                throw new ArgumentException("Nieprawidłowy format godziny. Oczekiwano formatu hh:mm.");
            }
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
                    string strSQL = $"SELECT {kolumna} FROM {Bazadanych} WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@ID", ID);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal(kolumna)))
                                {
                                    object value = reader[kolumna];
                                    if (value is T castedValue)
                                    {
                                        wartosc = castedValue;
                                    }
                                    else
                                    {
                                        wartosc = (T)Convert.ChangeType(value, typeof(T));
                                    }
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


        public T PobierzInformacjeZBazyDanychHarmonogram<T>(int ID, string Bazadanych, string kolumna)
        {
            T wartosc = default(T); // Wartość domyślna dla typu generycznego

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Budowanie zapytania z bezpośrednim wstawieniem nazw tabeli i kolumny
                    string strSQL = $"SELECT {kolumna} FROM {Bazadanych} WHERE Lp = @ID";

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

        public void UzupelnijComboBoxHodowcami3(ComboBox comboBox)
        {
            string query = "SELECT DISTINCT Name, ID FROM dbo.DOSTAWCY WHERE halt = '0' Order by Name Asc  ";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                // Lista par ID -> Name
                List<KeyValuePair<string, string>> dostawcy = new List<KeyValuePair<string, string>>();

                while (reader.Read())
                {
                    string id = reader["ID"].ToString().Trim();
                    string name = reader["Name"].ToString().Trim();
                    dostawcy.Add(new KeyValuePair<string, string>(id, name));
                }

                // Ustawienie źródła danych
                comboBox.DataSource = dostawcy;
                comboBox.DisplayMember = "Value"; // Wyświetlane: nazwa
                comboBox.ValueMember = "Key";    // Przechowywane: ID

                reader.Close();
            }
        }







        public void UzupelnijComboBoxHodowcami2(ComboBox comboBox, ZapytaniaSQL hodowca)
        {
            string query = "SELECT DISTINCT ID, Name FROM dbo.DOSTAWCY WHERE halt = '0'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                // Tworzymy słownik do przechowywania ID i Name
                Dictionary<string, string> dostawcy = new Dictionary<string, string>();

                while (reader.Read())
                {
                    string id = reader["ID"].ToString();
                    string name = reader["Name"].ToString();
                    dostawcy.Add(id, name); // Dodajemy ID jako klucz i Name jako wartość
                }

                // Wypełniamy ComboBox tylko nazwami
                comboBox.Items.Clear();
                foreach (var dostawca in dostawcy)
                {
                    comboBox.Items.Add(new KeyValuePair<string, string>(dostawca.Key, dostawca.Value));
                }

                // Wyświetlanie tylko nazw w ComboBox
                comboBox.DisplayMember = "Value";
                comboBox.ValueMember = "Key";

                reader.Close();
            }

            // Obsługa zmiany wyboru w ComboBox
            comboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (comboBox.SelectedItem is KeyValuePair<string, string> selectedItem)
                {
                    hodowca.idHodowcy = selectedItem.Key; // Przypisanie wybranego ID do obiektu
                    Console.WriteLine($"Wybrane ID: {hodowca.idHodowcy}");
                }
            };
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
        public void UzupełnienieDanychHodowcydoTextBoxow(string idDostawcy, TextBox adres, TextBox kodPocztowy, TextBox miejscowosc, TextBox dystans, TextBox telefon1, TextBox telefon2, TextBox telefon3)
        {

            string Zmienna;
            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Address");
            adres.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "PostalCode");
            kodPocztowy.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Distance");
            dystans.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "City");
            miejscowosc.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone1");
            telefon1.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone2");
            telefon2.Text = Zmienna.ToString();

            Zmienna = PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone3");
            telefon3.Text = Zmienna.ToString();
        }

        public void UpdateDaneHodowowAvilog(int IdSpecyfikacji, string hodowca, string hodowcaReal)
        {
            try
            {
                using (SqlConnection cnn = new SqlConnection(connectionString))
                {
                    cnn.Open();

                    // Zapytanie SQL do aktualizacji danych
                    string strSQL = @"UPDATE dbo.FarmerCalc
                              SET CustomerGID = @Hodowca,
                                  CustomerRealGID = @RealHodowca
                              WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, cnn))
                    {
                        // Dodanie parametrów, z obsługą wartości null
                        command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                        command.Parameters.AddWithValue("@Hodowca", string.IsNullOrEmpty(hodowca) ? DBNull.Value : hodowca);
                        command.Parameters.AddWithValue("@RealHodowca", string.IsNullOrEmpty(hodowcaReal) ? DBNull.Value : hodowcaReal);

                        // Wykonanie zapytania
                        int rowsAffected = command.ExecuteNonQuery();

                        // Obsługa wyniku
                        if (rowsAffected > 0)
                        {

                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować danych Hodowców Nazwy", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateDaneAdresoweDostawcy(string idHodowcy, TextBox Ulica, TextBox KodPocztowy, TextBox Miejscowosc, TextBox Dystans)
        {
            try
            {
                // Sprawdź, czy idHodowcy nie jest puste
                if (!string.IsNullOrEmpty(idHodowcy))
                {
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
                                  WHERE ID = @ID AND halt = '0';";

                        using (SqlCommand command = new SqlCommand(strSQL, cnn))
                        {
                            // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                            command.Parameters.AddWithValue("@ID", idHodowcy);
                            command.Parameters.AddWithValue("@Ulica", string.IsNullOrEmpty(Ulica.Text) ? (object)DBNull.Value : Ulica.Text);
                            command.Parameters.AddWithValue("@KodPocztowy", string.IsNullOrEmpty(KodPocztowy.Text) ? (object)DBNull.Value : KodPocztowy.Text);
                            command.Parameters.AddWithValue("@Miejscowosc", string.IsNullOrEmpty(Miejscowosc.Text) ? (object)DBNull.Value : Miejscowosc.Text);
                            command.Parameters.AddWithValue("@Dystans", string.IsNullOrEmpty(Dystans.Text) ? (object)DBNull.Value : int.Parse(Dystans.Text));

                            // Wykonanie zapytania SQL
                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {

                            }
                            else
                            {
                                MessageBox.Show("Nie udało się zaktualizować danych adresowych dostawcy.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Proszę podać poprawne ID dostawcy.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateDaneKontaktowe(string idHodowcy, TextBox Phone1, TextBox Info1, TextBox Phone2, TextBox Info2, TextBox Phone3, TextBox Info3, TextBox Email)
        {
            try
            {
                // Sprawdź, czy idHodowcy nie jest puste
                if (!string.IsNullOrEmpty(idHodowcy))
                {
                    // Utworzenie połączenia z bazą danych
                    using (SqlConnection cnn = new SqlConnection(connectionString))
                    {
                        cnn.Open();

                        // Utworzenie zapytania SQL do aktualizacji danych
                        string strSQL = @"UPDATE dbo.Dostawcy
                                  SET phone1 = @Phone1,
                                      phone2 = @Phone2,
                                      phone3 = @Phone3,
                                      info1 = @Info1,
                                      info2 = @Info2,
                                      info3 = @Info3,
                                      Email = @Email
                                  WHERE ID = @ID AND halt = '0';";

                        using (SqlCommand command = new SqlCommand(strSQL, cnn))
                        {
                            // Dodanie parametrów do zapytania SQL, ustawiając wartość NULL dla pustych pól
                            command.Parameters.AddWithValue("@ID", idHodowcy);
                            command.Parameters.AddWithValue("@Phone1", string.IsNullOrEmpty(Phone1.Text) ? (object)DBNull.Value : Phone1.Text);
                            command.Parameters.AddWithValue("@Phone2", string.IsNullOrEmpty(Phone2.Text) ? (object)DBNull.Value : Phone2.Text);
                            command.Parameters.AddWithValue("@Phone3", string.IsNullOrEmpty(Phone3.Text) ? (object)DBNull.Value : Phone3.Text);
                            command.Parameters.AddWithValue("@Info1", string.IsNullOrEmpty(Info1.Text) ? (object)DBNull.Value : Info1.Text);
                            command.Parameters.AddWithValue("@Info2", string.IsNullOrEmpty(Info2.Text) ? (object)DBNull.Value : Info2.Text);
                            command.Parameters.AddWithValue("@Info3", string.IsNullOrEmpty(Info3.Text) ? (object)DBNull.Value : Info3.Text);
                            command.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(Email.Text) ? (object)DBNull.Value : Email.Text);

                            // Wykonanie zapytania SQL
                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {

                            }
                            else
                            {
                                MessageBox.Show("Nie udało się zaktualizować danych kontaktowych.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Proszę podać poprawne ID dostawcy.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        command.Parameters.AddWithValue("@PiecesFarm", string.IsNullOrEmpty(PiecesFarm.Text) ? (object)DBNull.Value : decimal.Parse(PiecesFarm.Text));
                        command.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj.Text) ? (object)DBNull.Value : decimal.Parse(SztPoj.Text));


                        // Wykonanie zapytania SQL
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Zaktualizowano dane pomyślnie
                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować DANYCH ROZLICZENIOWYCH HODOWCY", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                            MessageBox.Show("Nie udało się zaktualizować DANYCH ROZLICZENIOWYCH UBOJNI", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                    // Sprawdź, czy Kierowca ma wybrany element
                    int? idKierowcy = null;
                    if (Kierowca?.SelectedItem != null)
                    {
                        string driverName = Kierowca.SelectedItem.ToString();
                        idKierowcy = ZnajdzIdKierowcy(driverName);
                    }

                    // Utworzenie zapytania SQL do aktualizacji danych
                    string strSQL = @"UPDATE dbo.FarmerCalc
                              SET CarID = @Ciagnik,
                                  DriverGID = @Kierowca,
                                  TrailerID = @Naczepa
                              WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, cnn))
                    {
                        // Dodanie parametrów do zapytania SQL
                        command.Parameters.AddWithValue("@ID", IdSpecyfikacji);
                        command.Parameters.AddWithValue("@Kierowca", idKierowcy.HasValue ? (object)idKierowcy.Value : DBNull.Value);
                        command.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik.Text) ? (object)DBNull.Value : Ciagnik.Text);
                        command.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa.Text) ? (object)DBNull.Value : Naczepa.Text);

                        // Wykonanie zapytania SQL
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {

                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować danych kierowców lub pojazdów.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        public void PokazwTabeliRozliczeniaAvilogazDanegoDnia(DateTime selectedDate, DataGridView grid)
        {
            string query = @"
        SELECT 
            F.[CarLp] AS Nr,
            P.[ShortName] AS Dostawca, -- Zamiast CustomerGID wyświetlamy nazwę dostawcy
            DATEDIFF(MINUTE, F.[Wyjazd], F.[DojazdHodowca]) AS [Dojazd],
            DATEDIFF(MINUTE, F.[Zaladunek], F.[ZaladunekKoniec]) AS [Zaladunek],
            DATEDIFF(MINUTE, F.[WyjazdHodowca], F.[Przyjazd]) AS [Przyjazd],
            F.[DistanceKM] AS KmAvi,
            P.Distance * 2 AS KmHod, 
            (F.[DistanceKM] - (P.Distance * 2)) AS [Km-],
            F.[DeclI2] AS P,
            F.[CarID] AS Auto
        FROM 
            [LibraNet].[dbo].[FarmerCalc] F
        LEFT JOIN 
            [LibraNet].[dbo].[Dostawcy] P 
            ON F.CustomerGID = P.ID -- Łączenie z tabelą zawierającą nazwy dostawców
        WHERE 
            F.[CalcDate] = @CalcDate
        ORDER BY 
            F.CustomerGID;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CalcDate", selectedDate.ToString("yyyy-MM-dd"));

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable dataTable = new DataTable();

                try
                {
                    connection.Open();
                    adapter.Fill(dataTable);
                    grid.DataSource = dataTable;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error fetching data: " + ex.Message);
                }
            }
        


    }
    public void stylGridaPodstawowy(DataGridView grid)
        {
            // Automatyczne dopasowanie szerokości kolumn
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            // Ustawienia wysokości wierszy
            grid.RowTemplate.Height = 18; // Zmniejszona wysokość wierszy

            // Wyłącz automatyczne generowanie kolumn, jeśli nie jest potrzebne
            grid.AutoGenerateColumns = true;

            // Opcjonalnie: Ustawienie stylu dla nagłówków kolumn
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9F, FontStyle.Bold);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 25;
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
        public double PobierzSredniaCenePotwierdzone()
        {
            double sredniaCena = 0.0;

            string zapytanie = @"
            SELECT 
                SUM(CAST(Auta AS DECIMAL(10, 2)) * CAST(Cena AS DECIMAL(10, 2))) / NULLIF(SUM(CAST(Auta AS DECIMAL(10, 2))), 0) AS SredniaCena
            FROM 
                [LibraNet].[dbo].[HarmonogramDostaw]
            WHERE 
                Bufor = 'Potwierdzony'
                AND DataOdbioru >= CAST(@StartDate() AS DATE)
                AND DataOdbioru < DATEADD(DAY, 1, CAST(@StartDate() AS DATE))
            ";



            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(zapytanie, connection))
            {
                try
                {
                    //command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate });
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
}

