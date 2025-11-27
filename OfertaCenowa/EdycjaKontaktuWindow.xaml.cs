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
        
        // Właściwości do przekazania danych po zapisie
        public bool ZapisanoZmiany { get; private set; } = false;
        public string NowyEmail { get; private set; }
        public string NowyTelefon { get; private set; }
        public string NoweImie { get; private set; }
        public string NoweNazwisko { get; private set; }

        public EdycjaKontaktuWindow()
        {
            InitializeComponent();
            Loaded += EdycjaKontaktuWindow_Loaded;
        }

        private void EdycjaKontaktuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtKlientNazwa.Text = KlientNazwa ?? "Klient";
            WczytajDaneKontaktowe();
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
                        SELECT Imie, Nazwisko, Stanowisko, Email, TELEFON_K, TelefonDodatkowy
                        FROM OdbiorcyCRM 
                        WHERE ID = @ID";
                    
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", KlientID);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtImie.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                txtNazwisko.Text = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                txtStanowisko.Text = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                txtEmail.Text = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                txtTelefon.Text = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                txtTelefonDodatkowy.Text = reader.IsDBNull(5) ? "" : reader.GetString(5);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd wczytywania: {ex.Message}";
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (KlientID <= 0)
            {
                MessageBox.Show("Brak ID klienta.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                // Najpierw sprawdź czy kolumny istnieją - jeśli nie, utwórz je
                SprawdzIUtworzKolumny();
                
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    
                    const string sql = @"
                        UPDATE OdbiorcyCRM 
                        SET Imie = @Imie,
                            Nazwisko = @Nazwisko,
                            Stanowisko = @Stanowisko,
                            Email = @Email,
                            TELEFON_K = @Telefon,
                            TelefonDodatkowy = @TelefonDodatkowy
                        WHERE ID = @ID";
                    
                    using (var cmd = new SqlCommand(sql, conn))
                    {
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
                            // Zapisz do historii
                            ZapiszHistorie(conn);
                            
                            // Ustaw właściwości
                            ZapisanoZmiany = true;
                            NowyEmail = txtEmail.Text;
                            NowyTelefon = txtTelefon.Text;
                            NoweImie = txtImie.Text;
                            NoweNazwisko = txtNazwisko.Text;
                            
                            MessageBox.Show("✅ Dane kontaktowe zostały zapisane.", "Zapisano", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show("Nie znaleziono klienta w bazie.", "Błąd", 
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", 
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
                    
                    // Lista kolumn do sprawdzenia/utworzenia
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
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia kolumn: {ex.Message}");
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
            catch { /* Ignoruj błędy historii */ }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
