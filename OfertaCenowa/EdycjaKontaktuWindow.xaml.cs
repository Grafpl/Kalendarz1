using System;
using System.Data.SqlClient;
using System.Windows;

namespace Kalendarz1.OfertaCenowa
{
    public partial class EdycjaKontaktuWindow : Window
    {
        private readonly string _connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;";

        public int KlientID { get; set; }
        public string KlientNazwa { get; set; }
        public string OperatorID { get; set; }

        public bool ZapisanoZmiany { get; private set; } = false;
        public string NowyEmail { get; private set; }
        public string NowyTelefon { get; private set; }
        public string NoweImie { get; private set; }
        public string NoweNazwisko { get; private set; }

        public EdycjaKontaktuWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += EdycjaKontaktuWindow_Loaded;
        }

        private void EdycjaKontaktuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajWojewodztwa();
            WczytajPKD();
            WczytajDaneKontaktowe();
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

        private void WczytajDaneKontaktowe()
        {
            if (KlientID <= 0) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    const string sql = @"
                        SELECT Nazwa, PKD_Opis, KOD, MIASTO, Ulica, Wojewodztwo,
                               Imie, Nazwisko, Stanowisko, Email, TELEFON_K, TelefonDodatkowy
                        FROM OdbiorcyCRM
                        WHERE ID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", KlientID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtNazwa.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                cmbPKD.Text = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                txtKod.Text = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                txtMiasto.Text = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                txtUlica.Text = reader.IsDBNull(4) ? "" : reader.GetString(4);

                                string woj = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                int idx = cmbWojewodztwo.Items.IndexOf(woj);
                                if (idx >= 0) cmbWojewodztwo.SelectedIndex = idx;

                                txtImie.Text = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                txtNazwisko.Text = reader.IsDBNull(7) ? "" : reader.GetString(7);
                                txtStanowisko.Text = reader.IsDBNull(8) ? "" : reader.GetString(8);
                                txtEmail.Text = reader.IsDBNull(9) ? "" : reader.GetString(9);
                                txtTelefon.Text = reader.IsDBNull(10) ? "" : reader.GetString(10);
                                txtTelefonDodatkowy.Text = reader.IsDBNull(11) ? "" : reader.GetString(11);

                                txtKlientNazwa.Text = txtNazwa.Text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Blad wczytywania: {ex.Message}";
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (KlientID <= 0)
            {
                MessageBox.Show("Brak ID klienta.", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNazwa.Text))
            {
                MessageBox.Show("Podaj nazwe firmy!", "Brak nazwy", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNazwa.Focus();
                return;
            }

            try
            {
                SprawdzIUtworzKolumny();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    const string sql = @"
                        UPDATE OdbiorcyCRM
                        SET Nazwa = @Nazwa,
                            PKD_Opis = @PKD,
                            KOD = @Kod,
                            MIASTO = @Miasto,
                            Ulica = @Ulica,
                            Wojewodztwo = @Woj,
                            Imie = @Imie,
                            Nazwisko = @Nazwisko,
                            Stanowisko = @Stanowisko,
                            Email = @Email,
                            TELEFON_K = @Telefon,
                            TelefonDodatkowy = @TelefonDodatkowy
                        WHERE ID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Nazwa", txtNazwa.Text ?? "");
                        cmd.Parameters.AddWithValue("@PKD", cmbPKD.Text ?? "");
                        cmd.Parameters.AddWithValue("@Kod", txtKod.Text ?? "");
                        cmd.Parameters.AddWithValue("@Miasto", txtMiasto.Text ?? "");
                        cmd.Parameters.AddWithValue("@Ulica", txtUlica.Text ?? "");
                        cmd.Parameters.AddWithValue("@Woj", cmbWojewodztwo.Text ?? "");
                        cmd.Parameters.AddWithValue("@Imie", txtImie.Text ?? "");
                        cmd.Parameters.AddWithValue("@Nazwisko", txtNazwisko.Text ?? "");
                        cmd.Parameters.AddWithValue("@Stanowisko", txtStanowisko.Text ?? "");
                        cmd.Parameters.AddWithValue("@Email", txtEmail.Text ?? "");
                        cmd.Parameters.AddWithValue("@Telefon", txtTelefon.Text ?? "");
                        cmd.Parameters.AddWithValue("@TelefonDodatkowy", txtTelefonDodatkowy.Text ?? "");
                        cmd.Parameters.AddWithValue("@ID", KlientID);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            ZapiszHistorie(conn);

                            // Dodaj notatke jesli jest
                            if (!string.IsNullOrWhiteSpace(txtNotatki.Text))
                            {
                                var cmdNotatka = new SqlCommand(@"
                                    INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal)
                                    VALUES (@id, @tresc, @kto)", conn);
                                cmdNotatka.Parameters.AddWithValue("@id", KlientID);
                                cmdNotatka.Parameters.AddWithValue("@tresc", txtNotatki.Text.Trim());
                                cmdNotatka.Parameters.AddWithValue("@kto", OperatorID ?? "");
                                cmdNotatka.ExecuteNonQuery();
                            }

                            ZapisanoZmiany = true;
                            NowyEmail = txtEmail.Text;
                            NowyTelefon = txtTelefon.Text;
                            NoweImie = txtImie.Text;
                            NoweNazwisko = txtNazwisko.Text;

                            MessageBox.Show($"Dane kontrahenta zostaly zapisane.", "Zapisano",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show("Nie znaleziono klienta w bazie.", "Blad",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SprawdzIUtworzKolumny()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string[] kolumny = new[] { "Email", "TelefonDodatkowy", "Imie", "Nazwisko", "Stanowisko" };

                    foreach (var kolumna in kolumny)
                    {
                        string checkSql = $@"
                            IF NOT EXISTS (
                                SELECT * FROM sys.columns
                                WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = '{kolumna}'
                            )
                            BEGIN
                                ALTER TABLE OdbiorcyCRM ADD {kolumna} NVARCHAR(200) NULL
                            END";

                        using (var cmd = new SqlCommand(checkSql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad tworzenia kolumn: {ex.Message}");
            }
        }

        private void ZapiszHistorie(SqlConnection conn)
        {
            try
            {
                const string sql = @"
                    INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                    VALUES (@ID, @Typ, @Wartosc, @Operator, GETDATE())";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", KlientID);
                    cmd.Parameters.AddWithValue("@Typ", "Edycja danych kontaktowych");
                    cmd.Parameters.AddWithValue("@Wartosc", $"Email: {txtEmail.Text}, Tel: {txtTelefon.Text}");
                    cmd.Parameters.AddWithValue("@Operator", OperatorID ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
