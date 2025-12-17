using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Kalendarz1.OfertaCenowa
{
    public partial class DodajOdbiorceWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _operatorID;
        private DispatcherTimer _timerSzukaj;

        public bool FiltrujTylkoMoje { get; private set; }

        // Mapowanie prefixow kodu pocztowego do wojewodztw
        private static readonly Dictionary<string, string> kodDoWojewodztwa = new Dictionary<string, string>
        {
            {"00", "Mazowieckie"}, {"01", "Mazowieckie"}, {"02", "Mazowieckie"}, {"03", "Mazowieckie"}, {"04", "Mazowieckie"}, {"05", "Mazowieckie"},
            {"06", "Mazowieckie"}, {"07", "Mazowieckie"}, {"08", "Mazowieckie"}, {"09", "Mazowieckie"},
            {"10", "Warminsko-Mazurskie"}, {"11", "Warminsko-Mazurskie"}, {"12", "Warminsko-Mazurskie"}, {"13", "Warminsko-Mazurskie"}, {"14", "Warminsko-Mazurskie"},
            {"15", "Podlaskie"}, {"16", "Podlaskie"}, {"17", "Podlaskie"}, {"18", "Podlaskie"}, {"19", "Podlaskie"},
            {"20", "Lubelskie"}, {"21", "Lubelskie"}, {"22", "Lubelskie"}, {"23", "Lubelskie"}, {"24", "Lubelskie"},
            {"25", "Swietokrzyskie"}, {"26", "Swietokrzyskie"}, {"27", "Swietokrzyskie"}, {"28", "Swietokrzyskie"}, {"29", "Swietokrzyskie"},
            {"30", "Malopolskie"}, {"31", "Malopolskie"}, {"32", "Malopolskie"}, {"33", "Malopolskie"}, {"34", "Malopolskie"},
            {"35", "Podkarpackie"}, {"36", "Podkarpackie"}, {"37", "Podkarpackie"}, {"38", "Podkarpackie"}, {"39", "Podkarpackie"},
            {"40", "Slaskie"}, {"41", "Slaskie"}, {"42", "Slaskie"}, {"43", "Slaskie"}, {"44", "Slaskie"},
            {"45", "Opolskie"}, {"46", "Opolskie"}, {"47", "Opolskie"}, {"48", "Opolskie"}, {"49", "Opolskie"},
            {"50", "Dolnoslaskie"}, {"51", "Dolnoslaskie"}, {"52", "Dolnoslaskie"}, {"53", "Dolnoslaskie"}, {"54", "Dolnoslaskie"},
            {"55", "Dolnoslaskie"}, {"56", "Dolnoslaskie"}, {"57", "Dolnoslaskie"}, {"58", "Dolnoslaskie"}, {"59", "Dolnoslaskie"},
            {"60", "Wielkopolskie"}, {"61", "Wielkopolskie"}, {"62", "Wielkopolskie"}, {"63", "Wielkopolskie"}, {"64", "Wielkopolskie"},
            {"65", "Lubuskie"}, {"66", "Lubuskie"}, {"67", "Lubuskie"}, {"68", "Lubuskie"}, {"69", "Lubuskie"},
            {"70", "Zachodniopomorskie"}, {"71", "Zachodniopomorskie"}, {"72", "Zachodniopomorskie"}, {"73", "Zachodniopomorskie"}, {"74", "Zachodniopomorskie"},
            {"75", "Zachodniopomorskie"}, {"76", "Zachodniopomorskie"}, {"77", "Pomorskie"}, {"78", "Zachodniopomorskie"},
            {"80", "Pomorskie"}, {"81", "Pomorskie"}, {"82", "Pomorskie"}, {"83", "Pomorskie"}, {"84", "Pomorskie"},
            {"85", "Kujawsko-Pomorskie"}, {"86", "Kujawsko-Pomorskie"}, {"87", "Kujawsko-Pomorskie"}, {"88", "Kujawsko-Pomorskie"}, {"89", "Kujawsko-Pomorskie"},
            {"90", "Lodzkie"}, {"91", "Lodzkie"}, {"92", "Lodzkie"}, {"93", "Lodzkie"}, {"94", "Lodzkie"},
            {"95", "Lodzkie"}, {"96", "Lodzkie"}, {"97", "Lodzkie"}, {"98", "Lodzkie"}, {"99", "Lodzkie"}
        };

        public DodajOdbiorceWindow(string connectionString, string operatorID)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _operatorID = operatorID;

            _timerSzukaj = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _timerSzukaj.Tick += TimerSzukaj_Tick;

            WczytajWojewodztwa();
            WczytajPKD();
        }

        private void WczytajWojewodztwa()
        {
            cmbWojewodztwo.Items.Clear();
            cmbWojewodztwo.Items.Add("");
            cmbWojewodztwo.Items.Add("Dolnoslaskie");
            cmbWojewodztwo.Items.Add("Kujawsko-Pomorskie");
            cmbWojewodztwo.Items.Add("Lubelskie");
            cmbWojewodztwo.Items.Add("Lubuskie");
            cmbWojewodztwo.Items.Add("Lodzkie");
            cmbWojewodztwo.Items.Add("Malopolskie");
            cmbWojewodztwo.Items.Add("Mazowieckie");
            cmbWojewodztwo.Items.Add("Opolskie");
            cmbWojewodztwo.Items.Add("Podkarpackie");
            cmbWojewodztwo.Items.Add("Podlaskie");
            cmbWojewodztwo.Items.Add("Pomorskie");
            cmbWojewodztwo.Items.Add("Slaskie");
            cmbWojewodztwo.Items.Add("Swietokrzyskie");
            cmbWojewodztwo.Items.Add("Warminsko-Mazurskie");
            cmbWojewodztwo.Items.Add("Wielkopolskie");
            cmbWojewodztwo.Items.Add("Zachodniopomorskie");
        }

        private void WczytajPKD()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT DISTINCT PKD_Opis FROM OdbiorcyCRM WHERE PKD_Opis IS NOT NULL AND PKD_Opis <> '' ORDER BY PKD_Opis", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbPKD.Items.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch { }
        }

        private void TxtNazwa_TextChanged(object sender, TextChangedEventArgs e)
        {
            _timerSzukaj.Stop();
            if (txtNazwa.Text.Length >= 3)
            {
                _timerSzukaj.Start();
            }
            else
            {
                UkryjWyniki();
            }
        }

        private void TimerSzukaj_Tick(object sender, EventArgs e)
        {
            _timerSzukaj.Stop();
            SzukajPodobnychKlientow();
        }

        private void UkryjWyniki()
        {
            panelOstrzezenie.Visibility = Visibility.Collapsed;
            panelPlaceholder.Visibility = Visibility.Visible;
            listPodobni.Items.Clear();
        }

        private void PokazWyniki(int count)
        {
            if (count > 0)
            {
                panelOstrzezenie.Visibility = Visibility.Visible;
                panelPlaceholder.Visibility = Visibility.Collapsed;
                txtLiczbaZnalezionych.Text = $"UWAGA! Znaleziono {count} podobnych klientow:";
            }
            else
            {
                panelOstrzezenie.Visibility = Visibility.Collapsed;
                panelPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void SzukajPodobnychKlientow()
        {
            string szukany = txtNazwa.Text.Trim();
            if (szukany.Length < 3) return;

            listPodobni.Items.Clear();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT TOP 50 ID, Nazwa, MIASTO, Status
                        FROM OdbiorcyCRM
                        WHERE Nazwa LIKE '%' + @szukany + '%'
                        ORDER BY
                            CASE
                                WHEN Nazwa LIKE @szukany + '%' THEN 1
                                WHEN Nazwa LIKE '%' + @szukany + '%' THEN 2
                                ELSE 3
                            END,
                            Nazwa", conn);
                    cmd.Parameters.AddWithValue("@szukany", szukany);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string miasto = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            string status = reader.IsDBNull(3) ? "" : reader.GetString(3);

                            string info = $"[{id}]  {nazwa}";
                            if (!string.IsNullOrEmpty(miasto))
                                info += $"   -   {miasto}";
                            if (!string.IsNullOrEmpty(status))
                                info += $"   ({status})";

                            listPodobni.Items.Add(info);
                        }
                    }
                }

                PokazWyniki(listPodobni.Items.Count);
            }
            catch { }
        }

        private void TxtKod_TextChanged(object sender, TextChangedEventArgs e)
        {
            string kod = txtKod.Text.Replace("-", "").Trim();

            if (kod.Length >= 2)
            {
                string prefix = kod.Substring(0, 2);

                if (kodDoWojewodztwa.TryGetValue(prefix, out string woj))
                {
                    int index = cmbWojewodztwo.Items.IndexOf(woj);
                    if (index >= 0)
                        cmbWojewodztwo.SelectedIndex = index;
                }

                if (kod.Length >= 5)
                {
                    try
                    {
                        using (var conn = new SqlConnection(_connectionString))
                        {
                            conn.Open();

                            // Szukaj w tabeli KodyPocztowe
                            var cmd = new SqlCommand(@"
                                SELECT TOP 1 miej FROM KodyPocztowe
                                WHERE REPLACE(Kod, '-', '') = @kod", conn);
                            cmd.Parameters.AddWithValue("@kod", kod);
                            var miasto = cmd.ExecuteScalar() as string;

                            if (!string.IsNullOrEmpty(miasto) && string.IsNullOrEmpty(txtMiasto.Text))
                            {
                                txtMiasto.Text = miasto;
                            }

                            // Szukaj w OdbiorcyCRM
                            var cmd2 = new SqlCommand(@"
                                SELECT TOP 1 MIASTO, Wojewodztwo
                                FROM OdbiorcyCRM
                                WHERE REPLACE(KOD, '-', '') = @kod", conn);
                            cmd2.Parameters.AddWithValue("@kod", kod);
                            using (var reader = cmd2.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    if (string.IsNullOrEmpty(txtMiasto.Text) && !reader.IsDBNull(0))
                                        txtMiasto.Text = reader.GetString(0);

                                    if (!reader.IsDBNull(1))
                                    {
                                        string wojDB = reader.GetString(1);
                                        int idx = cmbWojewodztwo.Items.IndexOf(wojDB);
                                        if (idx >= 0)
                                            cmbWojewodztwo.SelectedIndex = idx;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private void ListPodobni_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (listPodobni.SelectedItem == null) return;

            string wybrany = listPodobni.SelectedItem.ToString();
            var result = MessageBox.Show(
                $"Wybrany klient juz istnieje w CRM:\n\n{wybrany}\n\nCzy na pewno chcesz dodac nowego kontrahenta?",
                "Klient juz istnieje",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                DialogResult = false;
                Close();
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNazwa.Text))
            {
                MessageBox.Show("Podaj nazwe firmy!", "Brak nazwy", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNazwa.Focus();
                return;
            }

            // Ostrzezenie jesli sa duplikaty
            if (listPodobni.Items.Count > 0)
            {
                var result = MessageBox.Show(
                    $"Znaleziono {listPodobni.Items.Count} podobnych klientow w bazie.\n\nCzy na pewno chcesz dodac nowego kontrahenta?",
                    "Mozliwy duplikat",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                    return;
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Sprawdz i utworz tabele WlascicieleOdbiorcow jesli nie istnieje
                        var cmdCheckTable = new SqlCommand(@"
                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WlascicieleOdbiorcow')
                            CREATE TABLE WlascicieleOdbiorcow (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                IDOdbiorcy INT NOT NULL,
                                OperatorID NVARCHAR(50) NOT NULL,
                                DataDodania DATETIME DEFAULT GETDATE()
                            )", conn, transaction);
                        cmdCheckTable.ExecuteNonQuery();

                        // Sprawdz i utworz brakujace kolumny (tak jak w EdycjaKontaktuWindow)
                        string[] kolumny = { "Email", "Imie", "Nazwisko", "Stanowisko", "TelefonDodatkowy" };
                        foreach (var kol in kolumny)
                        {
                            var cmdKol = new SqlCommand($@"
                                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = '{kol}')
                                ALTER TABLE OdbiorcyCRM ADD {kol} NVARCHAR(200) NULL", conn, transaction);
                            cmdKol.ExecuteNonQuery();
                        }

                        var cmdMaxId = new SqlCommand("SELECT ISNULL(MAX(ID), 0) + 1 FROM OdbiorcyCRM", conn, transaction);
                        int nowyID = (int)cmdMaxId.ExecuteScalar();

                        var cmdOdbiorca = new SqlCommand(@"
                            INSERT INTO OdbiorcyCRM
                            (ID, Nazwa, KOD, MIASTO, Ulica, Telefon_K, Email, Wojewodztwo, PKD_Opis, Status, Imie, Nazwisko, Stanowisko, TelefonDodatkowy)
                            VALUES
                            (@id, @nazwa, @kod, @miasto, @ulica, @tel, @email, @woj, @pkd, 'Do zadzwonienia', @imie, @nazwisko, @stanowisko, @telDod)",
                            conn, transaction);

                        cmdOdbiorca.Parameters.AddWithValue("@id", nowyID);
                        cmdOdbiorca.Parameters.AddWithValue("@nazwa", txtNazwa.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@kod", txtKod.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@miasto", txtMiasto.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@ulica", txtUlica.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@tel", txtTelefon.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@email", txtEmail.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@woj", cmbWojewodztwo.Text ?? "");
                        cmdOdbiorca.Parameters.AddWithValue("@pkd", cmbPKD.Text?.Trim() ?? "");
                        cmdOdbiorca.Parameters.AddWithValue("@imie", txtImie.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@nazwisko", txtNazwisko.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@stanowisko", txtStanowisko.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@telDod", txtTelefonDodatkowy.Text.Trim());

                        cmdOdbiorca.ExecuteNonQuery();

                        // Dodaj notatke jesli jest
                        if (!string.IsNullOrWhiteSpace(txtNotatki.Text))
                        {
                            var cmdNotatka = new SqlCommand(@"
                                INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal)
                                VALUES (@id, @tresc, @kto)", conn, transaction);
                            cmdNotatka.Parameters.AddWithValue("@id", nowyID);
                            cmdNotatka.Parameters.AddWithValue("@tresc", txtNotatki.Text.Trim());
                            cmdNotatka.Parameters.AddWithValue("@kto", _operatorID);
                            cmdNotatka.ExecuteNonQuery();
                        }

                        // Dodaj wlasciciela
                        var cmdWlasciciel = new SqlCommand(@"
                            INSERT INTO WlascicieleOdbiorcow (IDOdbiorcy, OperatorID)
                            VALUES (@odbiorca, @operator)",
                            conn, transaction);

                        cmdWlasciciel.Parameters.AddWithValue("@odbiorca", nowyID);
                        cmdWlasciciel.Parameters.AddWithValue("@operator", _operatorID);
                        cmdWlasciciel.ExecuteNonQuery();

                        // Dodaj log historii
                        var cmdLog = new SqlCommand(@"
                            INSERT INTO HistoriaZmianCRM
                            (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal)
                            VALUES (@id, 'Utworzenie kontaktu', 'Dodany recznie', @kto)",
                            conn, transaction);

                        cmdLog.Parameters.AddWithValue("@id", nowyID);
                        cmdLog.Parameters.AddWithValue("@kto", _operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();

                        FiltrujTylkoMoje = chkTylkoMoje.IsChecked == true;
                        MessageBox.Show($"Dodano kontrahenta: {txtNazwa.Text}\nID: {nowyID}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Blad przy zapisie:\n" + ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
