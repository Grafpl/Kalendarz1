using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1
{
    // ============================================
    // Dialog dodawania/edycji kontaktu
    // ============================================
    public class DodajKontaktDialog : Window
    {
        private int _odbiorcaID;
        private string _userID;
        private string _connectionString;
        private int? _kontaktID;

        private TextBox txtImie, txtNazwisko, txtStanowisko, txtTelefon, txtEmail, txtUwagi;
        private ComboBox cmbTypKontaktu;
        private CheckBox chkGlowny;

        public event EventHandler KontaktZapisany;

        public DodajKontaktDialog(int odbiorcaID, string userID, string connString, int? kontaktID = null)
        {
            _odbiorcaID = odbiorcaID;
            _userID = userID;
            _connectionString = connString;
            _kontaktID = kontaktID;

            Title = kontaktID.HasValue ? "Edytuj Kontakt" : "Dodaj Nowy Kontakt";
            Width = 600;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InicjalizujUI();
            
            if (kontaktID.HasValue)
                WczytajDaneKontaktu();
        }

        private void InicjalizujUI()
        {
            var grid = new Grid { Margin = new Thickness(20) };
            
            for (int i = 0; i < 9; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // Typ Kontaktu
            grid.Children.Add(new TextBlock { Text = "Typ kontaktu:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            cmbTypKontaktu = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            cmbTypKontaktu.Items.Add("Zakupowiec");
            cmbTypKontaktu.Items.Add("Księgowość");
            cmbTypKontaktu.Items.Add("Pojemniki");
            cmbTypKontaktu.Items.Add("Dyrektor");
            cmbTypKontaktu.Items.Add("Właściciel");
            cmbTypKontaktu.Items.Add("Logistyka");
            Grid.SetRow(cmbTypKontaktu, row);
            Grid.SetColumn(cmbTypKontaktu, 1);
            grid.Children.Add(cmbTypKontaktu);
            row++;

            // Imię
            grid.Children.Add(new TextBlock { Text = "Imię:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtImie = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtImie, row);
            Grid.SetColumn(txtImie, 1);
            grid.Children.Add(txtImie);
            row++;

            // Nazwisko
            grid.Children.Add(new TextBlock { Text = "Nazwisko:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtNazwisko = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtNazwisko, row);
            Grid.SetColumn(txtNazwisko, 1);
            grid.Children.Add(txtNazwisko);
            row++;

            // Stanowisko
            grid.Children.Add(new TextBlock { Text = "Stanowisko:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtStanowisko = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtStanowisko, row);
            Grid.SetColumn(txtStanowisko, 1);
            grid.Children.Add(txtStanowisko);
            row++;

            // Telefon
            grid.Children.Add(new TextBlock { Text = "Telefon:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtTelefon = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtTelefon, row);
            Grid.SetColumn(txtTelefon, 1);
            grid.Children.Add(txtTelefon);
            row++;

            // Email
            grid.Children.Add(new TextBlock { Text = "Email:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtEmail = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtEmail, row);
            Grid.SetColumn(txtEmail, 1);
            grid.Children.Add(txtEmail);
            row++;

            // Główny kontakt
            chkGlowny = new CheckBox { Content = "Główny kontakt", Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(chkGlowny, row);
            Grid.SetColumn(chkGlowny, 1);
            grid.Children.Add(chkGlowny);
            row++;

            // Uwagi
            grid.Children.Add(new TextBlock { Text = "Uwagi:", VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtUwagi = new TextBox { 
                Margin = new Thickness(0, 0, 0, 10), 
                AcceptsReturn = true, 
                TextWrapping = TextWrapping.Wrap,
                Height = 80
            };
            Grid.SetRow(txtUwagi, row);
            Grid.SetColumn(txtUwagi, 1);
            grid.Children.Add(txtUwagi);
            row += 2;

            // Przyciski
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnZapisz = new Button { Content = "Zapisz", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            btnZapisz.Click += BtnZapisz_Click;
            panel.Children.Add(btnZapisz);

            var btnAnuluj = new Button { Content = "Anuluj", Width = 100, Height = 35 };
            btnAnuluj.Click += (s, e) => Close();
            panel.Children.Add(btnAnuluj);

            Grid.SetRow(panel, row);
            Grid.SetColumnSpan(panel, 2);
            grid.Children.Add(panel);

            Content = grid;
        }

        private void WczytajDaneKontaktu()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT * FROM OdbiorcyKontakty WHERE KontaktID = @KontaktID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@KontaktID", _kontaktID.Value);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                cmbTypKontaktu.SelectedItem = reader["TypKontaktu"].ToString();
                                txtImie.Text = reader["Imie"].ToString();
                                txtNazwisko.Text = reader["Nazwisko"].ToString();
                                txtStanowisko.Text = reader["Stanowisko"].ToString();
                                txtTelefon.Text = reader["Telefon"].ToString();
                                txtEmail.Text = reader["Email"].ToString();
                                txtUwagi.Text = reader["Uwagi"].ToString();
                                chkGlowny.IsChecked = Convert.ToBoolean(reader["JestGlownyKontakt"]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            // Usunięto walidację - żadne pole nie jest wymagane

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query;

                    if (_kontaktID.HasValue)
                    {
                        query = @"
                    UPDATE OdbiorcyKontakty SET
                        TypKontaktu = @TypKontaktu,
                        Imie = @Imie,
                        Nazwisko = @Nazwisko,
                        Stanowisko = @Stanowisko,
                        Telefon = @Telefon,
                        Email = @Email,
                        Uwagi = @Uwagi,
                        JestGlownyKontakt = @JestGlowny
                    WHERE KontaktID = @KontaktID";
                    }
                    else
                    {
                        query = @"
                    INSERT INTO OdbiorcyKontakty 
                    (OdbiorcaID, TypKontaktu, Imie, Nazwisko, Stanowisko, Telefon, Email, Uwagi, JestGlownyKontakt, DataUtworzenia, KtoStworzyl)
                    VALUES 
                    (@OdbiorcaID, @TypKontaktu, @Imie, @Nazwisko, @Stanowisko, @Telefon, @Email, @Uwagi, @JestGlowny, GETDATE(), @UserID)";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (_kontaktID.HasValue)
                            cmd.Parameters.AddWithValue("@KontaktID", _kontaktID.Value);
                        else
                        {
                            cmd.Parameters.AddWithValue("@OdbiorcaID", _odbiorcaID);
                            cmd.Parameters.AddWithValue("@UserID", int.Parse(_userID));
                        }

                        cmd.Parameters.AddWithValue("@TypKontaktu", cmbTypKontaktu.SelectedItem?.ToString() ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Imie", string.IsNullOrWhiteSpace(txtImie.Text) ? (object)DBNull.Value : txtImie.Text);
                        cmd.Parameters.AddWithValue("@Nazwisko", string.IsNullOrWhiteSpace(txtNazwisko.Text) ? (object)DBNull.Value : txtNazwisko.Text);
                        cmd.Parameters.AddWithValue("@Stanowisko", string.IsNullOrWhiteSpace(txtStanowisko.Text) ? (object)DBNull.Value : txtStanowisko.Text);
                        cmd.Parameters.AddWithValue("@Telefon", string.IsNullOrWhiteSpace(txtTelefon.Text) ? (object)DBNull.Value : txtTelefon.Text);
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(txtEmail.Text) ? (object)DBNull.Value : txtEmail.Text);
                        cmd.Parameters.AddWithValue("@Uwagi", string.IsNullOrWhiteSpace(txtUwagi.Text) ? (object)DBNull.Value : txtUwagi.Text);
                        cmd.Parameters.AddWithValue("@JestGlowny", chkGlowny.IsChecked ?? false);

                        cmd.ExecuteNonQuery();
                    }
                }

                KontaktZapisany?.Invoke(this, EventArgs.Empty);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ============================================
    // Dialog dodawania notatki
    // ============================================
    public class DodajNotatkeDialog : Window
    {
        private TextBox txtNotatka;
        public string TrescNotatki => txtNotatka.Text;

        public event EventHandler NotatkaZapisana;

        public DodajNotatkeDialog()
        {
            Title = "Dodaj Notatkę CRM";
            Width = 600;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new TextBlock 
            { 
                Text = "Treść notatki:", 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 10) 
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // TextBox
            txtNotatka = new TextBox 
            { 
                AcceptsReturn = true, 
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10)
            };
            Grid.SetRow(txtNotatka, 1);
            grid.Children.Add(txtNotatka);

            // Przyciski
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var btnZapisz = new Button { Content = "Zapisz", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            btnZapisz.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtNotatka.Text))
                {
                    MessageBox.Show("Wprowadź treść notatki!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                NotatkaZapisana?.Invoke(this, EventArgs.Empty);
                Close();
            };
            panel.Children.Add(btnZapisz);

            var btnAnuluj = new Button { Content = "Anuluj", Width = 100, Height = 35 };
            btnAnuluj.Click += (s, e) => Close();
            panel.Children.Add(btnAnuluj);

            Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            Content = grid;
        }
    }

    // ============================================
    // Dialog ustawiania lokalizacji GPS
    // ============================================
    public class UstawLokalizacjeDialog : Window
    {
        private int _odbiorcaID;
        private string _connectionString;
        private TextBox txtSzerokosc, txtDlugosc;

        public event EventHandler LokalizacjaZapisana;

        public UstawLokalizacjeDialog(int odbiorcaID, string connString)
        {
            _odbiorcaID = odbiorcaID;
            _connectionString = connString;

            Title = "Ustaw Lokalizację GPS";
            Width = 500;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InicjalizujUI();
            WczytajAktualneLokalizacje();
        }

        private void InicjalizujUI()
        {
            var grid = new Grid { Margin = new Thickness(20) };
            
            for (int i = 0; i < 4; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Info
            var info = new TextBlock 
            { 
                Text = "Wprowadź współrzędne GPS odbiorcy.\nPrzykład: 51.7592 (szerokość), 19.4560 (długość)", 
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = System.Windows.Media.Brushes.Gray
            };
            Grid.SetRow(info, 0);
            Grid.SetColumnSpan(info, 2);
            grid.Children.Add(info);

            // Szerokość
            grid.Children.Add(new TextBlock { Text = "Szerokość (Latitude):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], 1);
            
            txtSzerokosc = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtSzerokosc, 1);
            Grid.SetColumn(txtSzerokosc, 1);
            grid.Children.Add(txtSzerokosc);

            // Długość
            grid.Children.Add(new TextBlock { Text = "Długość (Longitude):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], 2);
            
            txtDlugosc = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtDlugosc, 2);
            Grid.SetColumn(txtDlugosc, 1);
            grid.Children.Add(txtDlugosc);

            // Przyciski
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var btnZapisz = new Button { Content = "Zapisz", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            btnZapisz.Click += BtnZapisz_Click;
            panel.Children.Add(btnZapisz);

            var btnAnuluj = new Button { Content = "Anuluj", Width = 100, Height = 35 };
            btnAnuluj.Click += (s, e) => Close();
            panel.Children.Add(btnAnuluj);

            Grid.SetRow(panel, 3);
            Grid.SetColumnSpan(panel, 2);
            grid.Children.Add(panel);

            Content = grid;
        }

        private void WczytajAktualneLokalizacje()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "SELECT Szerokosc, Dlugosc FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";
                    
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", _odbiorcaID);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["Szerokosc"] != DBNull.Value)
                                    txtSzerokosc.Text = reader["Szerokosc"].ToString();
                                if (reader["Dlugosc"] != DBNull.Value)
                                    txtDlugosc.Text = reader["Dlugosc"].ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtSzerokosc.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal szerokosc))
            {
                MessageBox.Show("Nieprawidłowa szerokość geograficzna!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!decimal.TryParse(txtDlugosc.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal dlugosc))
            {
                MessageBox.Show("Nieprawidłowa długość geograficzna!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Oblicz odległość od firmy (przykładowe współrzędne Łodzi)
                decimal szerokoscFirmy = 51.7592m;
                decimal dlugoscFirmy = 19.4560m;
                decimal odleglosc = ObliczOdleglosc(szerokoscFirmy, dlugoscFirmy, szerokosc, dlugosc);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = @"
                        UPDATE Odbiorcy SET
                            Szerokosc = @Szerokosc,
                            Dlugosc = @Dlugosc,
                            OdlegloscKm = @OdlegloscKm
                        WHERE OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", _odbiorcaID);
                        cmd.Parameters.AddWithValue("@Szerokosc", szerokosc);
                        cmd.Parameters.AddWithValue("@Dlugosc", dlugosc);
                        cmd.Parameters.AddWithValue("@OdlegloscKm", odleglosc);
                        
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show($"Lokalizacja zapisana!\nOdległość od firmy: {odleglosc:N2} km",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                LokalizacjaZapisana?.Invoke(this, EventArgs.Empty);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private decimal ObliczOdleglosc(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            const double R = 6371; // Promień Ziemi w km
            
            double dLat = ToRad((double)(lat2 - lat1));
            double dLon = ToRad((double)(lon2 - lon1));
            
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return (decimal)(R * c);
        }

        private double ToRad(double deg)
        {
            return deg * Math.PI / 180;
        }
    }

    // ============================================
    // Dialog dodawania nowego odbiorcy
    // ============================================
    public class DodajOdbiorcaDialog : Window
    {
        private string _userID;
        private string _connLibraNet;
        private string _connHandel;

        private TextBox txtNazwaSkrot, txtPelnaNazwa, txtNIP;
        private ComboBox cmbOdbiorcyHandel;

        public event EventHandler OdbiorcaDodany;

        public DodajOdbiorcaDialog(string userID, string connLibra, string connHandel)
        {
            _userID = userID;
            _connLibraNet = connLibra;
            _connHandel = connHandel;

            Title = "Dodaj Nowego Odbiorcy";
            Width = 700;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InicjalizujUI();
            WczytajOdbiorcowZHandel();
        }

        private void InicjalizujUI()
        {
            var grid = new Grid { Margin = new Thickness(20) };
            
            for (int i = 0; i < 6; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // Info
            var info = new TextBlock 
            { 
                Text = "Wybierz istniejącego odbiorcy z systemu Handel lub dodaj ręcznie:", 
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(info, row);
            Grid.SetColumnSpan(info, 2);
            grid.Children.Add(info);
            row++;

            // Wybór z Handel
            grid.Children.Add(new TextBlock { Text = "Odbiorca z systemu Handel:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            cmbOdbiorcyHandel = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            cmbOdbiorcyHandel.SelectionChanged += CmbOdbiorcyHandel_SelectionChanged;
            Grid.SetRow(cmbOdbiorcyHandel, row);
            Grid.SetColumn(cmbOdbiorcyHandel, 1);
            grid.Children.Add(cmbOdbiorcyHandel);
            row++;

            // Separator
            var separator = new System.Windows.Shapes.Rectangle 
            { 
                Height = 1, 
                Fill = System.Windows.Media.Brushes.LightGray, 
                Margin = new Thickness(0, 10, 0, 15) 
            };
            Grid.SetRow(separator, row);
            Grid.SetColumnSpan(separator, 2);
            grid.Children.Add(separator);
            row++;

            // Nazwa skrócona
            grid.Children.Add(new TextBlock { Text = "Nazwa skrócona:*", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtNazwaSkrot = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtNazwaSkrot, row);
            Grid.SetColumn(txtNazwaSkrot, 1);
            grid.Children.Add(txtNazwaSkrot);
            row++;

            // Pełna nazwa
            grid.Children.Add(new TextBlock { Text = "Pełna nazwa:*", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtPelnaNazwa = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtPelnaNazwa, row);
            Grid.SetColumn(txtPelnaNazwa, 1);
            grid.Children.Add(txtPelnaNazwa);
            row++;

            // NIP
            grid.Children.Add(new TextBlock { Text = "NIP:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row);
            
            txtNIP = new TextBox { Margin = new Thickness(0, 0, 0, 20) };
            Grid.SetRow(txtNIP, row);
            Grid.SetColumn(txtNIP, 1);
            grid.Children.Add(txtNIP);
            row++;

            // Przyciski
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnDodaj = new Button { Content = "Dodaj", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            btnDodaj.Click += BtnDodaj_Click;
            panel.Children.Add(btnDodaj);

            var btnAnuluj = new Button { Content = "Anuluj", Width = 100, Height = 35 };
            btnAnuluj.Click += (s, e) => Close();
            panel.Children.Add(btnAnuluj);

            Grid.SetRow(panel, row);
            Grid.SetColumnSpan(panel, 2);
            grid.Children.Add(panel);

            Content = grid;
        }

        private void WczytajOdbiorcowZHandel()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connHandel))
                {
                    conn.Open();
                    string query = @"
                        SELECT TOP 1000
                            Shortcut,
                            Name,
                            NIP
                        FROM [HANDEL].[SSCommon].[STContractors]
                        WHERE Shortcut IS NOT NULL
                        ORDER BY Name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            cmbOdbiorcyHandel.Items.Add(new ComboBoxItem { Content = "-- Wybierz odbiorcy --", Tag = null });
                            
                            while (reader.Read())
                            {
                                var item = new ComboBoxItem 
                                { 
                                    Content = $"{reader["Shortcut"]} - {reader["Name"]}",
                                    Tag = new { 
                                        Shortcut = reader["Shortcut"].ToString(),
                                        Name = reader["Name"].ToString(),
                                        NIP = reader["NIP"].ToString()
                                    }
                                };
                                cmbOdbiorcyHandel.Items.Add(item);
                            }
                        }
                    }
                }
                cmbOdbiorcyHandel.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania odbiorców: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbOdbiorcyHandel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbOdbiorcyHandel.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                dynamic data = item.Tag;
                txtNazwaSkrot.Text = data.Shortcut;
                txtPelnaNazwa.Text = data.Name;
                txtNIP.Text = data.NIP;
            }
        }

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNazwaSkrot.Text) || string.IsNullOrWhiteSpace(txtPelnaNazwa.Text))
            {
                MessageBox.Show("Nazwa skrócona i pełna nazwa są wymagane!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        INSERT INTO Odbiorcy 
                        (NazwaSkrot, PelnaNazwa, NIP, StatusAktywny, DataUtworzenia, KtoStworzyl)
                        VALUES 
                        (@NazwaSkrot, @PelnaNazwa, @NIP, 1, GETDATE(), @UserID)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaSkrot", txtNazwaSkrot.Text);
                        cmd.Parameters.AddWithValue("@PelnaNazwa", txtPelnaNazwa.Text);
                        cmd.Parameters.AddWithValue("@NIP", string.IsNullOrWhiteSpace(txtNIP.Text) ? (object)DBNull.Value : txtNIP.Text);
                        cmd.Parameters.AddWithValue("@UserID", int.Parse(_userID));
                        
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Odbiorca został dodany pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                OdbiorcaDodany?.Invoke(this, EventArgs.Empty);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania odbiorcy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ============================================
    // Dialog edycji parametrów transportu
    // ============================================
    public class EdytujParametryTransportuDialog : Window
    {
        private int _odbiorcaID;
        private string _connectionString;

        private TextBox txtKosztStaly, txtKosztGodziny, txtPrzebieg, txtCenaPaliwa, txtCzasRozladunku, txtMinDarmowy;
        private ComboBox cmbTypTransportu;

        public event EventHandler ParametryZapisane;

        public EdytujParametryTransportuDialog(int odbiorcaID, string connString)
        {
            _odbiorcaID = odbiorcaID;
            _connectionString = connString;

            Title = "Edytuj Parametry Transportu";
            Width = 550;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InicjalizujUI();
            WczytajDane();
        }

        private void InicjalizujUI()
        {
            var grid = new Grid { Margin = new Thickness(20) };

            for (int i = 0; i < 10; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // Nagłówek
            var header = new TextBlock
            {
                Text = "🚚 Parametry kalkulacji kosztów transportu",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(header, row);
            Grid.SetColumnSpan(header, 2);
            grid.Children.Add(header);
            row++;

            // Typ transportu
            AddLabel(grid, "Typ transportu:", row);
            cmbTypTransportu = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            cmbTypTransportu.Items.Add("Własny");
            cmbTypTransportu.Items.Add("Wynajęty");
            cmbTypTransportu.Items.Add("Odbiorca");
            Grid.SetRow(cmbTypTransportu, row);
            Grid.SetColumn(cmbTypTransportu, 1);
            grid.Children.Add(cmbTypTransportu);
            row++;

            // Koszt stały
            AddLabel(grid, "Koszt stały dostawy (PLN):", row);
            txtKosztStaly = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtKosztStaly, row);
            Grid.SetColumn(txtKosztStaly, 1);
            grid.Children.Add(txtKosztStaly);
            row++;

            // Koszt godziny kierowcy
            AddLabel(grid, "Koszt godziny kierowcy (PLN/h):", row);
            txtKosztGodziny = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtKosztGodziny, row);
            Grid.SetColumn(txtKosztGodziny, 1);
            grid.Children.Add(txtKosztGodziny);
            row++;

            // Średni przebieg
            AddLabel(grid, "Średni przebieg (km/l):", row);
            txtPrzebieg = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtPrzebieg, row);
            Grid.SetColumn(txtPrzebieg, 1);
            grid.Children.Add(txtPrzebieg);
            row++;

            // Cena paliwa
            AddLabel(grid, "Cena paliwa (PLN/l):", row);
            txtCenaPaliwa = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtCenaPaliwa, row);
            Grid.SetColumn(txtCenaPaliwa, 1);
            grid.Children.Add(txtCenaPaliwa);
            row++;

            // Czas rozładunku
            AddLabel(grid, "Czas rozładunku (min):", row);
            txtCzasRozladunku = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtCzasRozladunku, row);
            Grid.SetColumn(txtCzasRozladunku, 1);
            grid.Children.Add(txtCzasRozladunku);
            row++;

            // Min wartość darmowy
            AddLabel(grid, "Min. wartość dla darmowego transp. (PLN):", row);
            txtMinDarmowy = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(txtMinDarmowy, row);
            Grid.SetColumn(txtMinDarmowy, 1);
            grid.Children.Add(txtMinDarmowy);
            row++;

            // Info
            var info = new TextBlock
            {
                Text = "💡 Te parametry są używane do kalkulacji kosztu dostawy dla tego odbiorcy.",
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(info, row);
            Grid.SetColumnSpan(info, 2);
            grid.Children.Add(info);

            // Przyciski
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var btnZapisz = new Button { Content = "Zapisz", Width = 100, Height = 35, Margin = new Thickness(0, 0, 10, 0) };
            btnZapisz.Click += BtnZapisz_Click;
            panel.Children.Add(btnZapisz);

            var btnAnuluj = new Button { Content = "Anuluj", Width = 100, Height = 35 };
            btnAnuluj.Click += (s, e) => Close();
            panel.Children.Add(btnAnuluj);

            Grid.SetRow(panel, 11);
            Grid.SetColumnSpan(panel, 2);
            grid.Children.Add(panel);

            Content = grid;
        }

        private void AddLabel(Grid grid, string text, int row)
        {
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 10)
            };
            Grid.SetRow(label, row);
            grid.Children.Add(label);
        }

        private void WczytajDane()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT
                            TypTransportu,
                            ISNULL(KosztStalyDostawy, 0) AS KosztStaly,
                            ISNULL(KosztGodzinyKierowcy, 50) AS KosztGodziny,
                            ISNULL(SredniPrzebiegLitr, 25) AS Przebieg,
                            ISNULL(CenaPaliwaLitr, 6.50) AS CenaPaliwa,
                            ISNULL(CzasRozladunku, 30) AS CzasRozladunku,
                            ISNULL(MinWartoscDlaDarmowegoTransportu, 0) AS MinDarmowy
                        FROM OdbiorcyTransport WHERE OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", _odbiorcaID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                cmbTypTransportu.SelectedItem = reader["TypTransportu"].ToString();
                                txtKosztStaly.Text = Convert.ToDecimal(reader["KosztStaly"]).ToString("0.00");
                                txtKosztGodziny.Text = Convert.ToDecimal(reader["KosztGodziny"]).ToString("0.00");
                                txtPrzebieg.Text = Convert.ToDecimal(reader["Przebieg"]).ToString("0.0");
                                txtCenaPaliwa.Text = Convert.ToDecimal(reader["CenaPaliwa"]).ToString("0.00");
                                txtCzasRozladunku.Text = Convert.ToInt32(reader["CzasRozladunku"]).ToString();
                                txtMinDarmowy.Text = Convert.ToDecimal(reader["MinDarmowy"]).ToString("0.00");
                            }
                            else
                            {
                                // Wartości domyślne
                                cmbTypTransportu.SelectedIndex = 0;
                                txtKosztStaly.Text = "0.00";
                                txtKosztGodziny.Text = "50.00";
                                txtPrzebieg.Text = "25.0";
                                txtCenaPaliwa.Text = "6.50";
                                txtCzasRozladunku.Text = "30";
                                txtMinDarmowy.Text = "0.00";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal kosztStaly = decimal.Parse(txtKosztStaly.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                decimal kosztGodziny = decimal.Parse(txtKosztGodziny.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                decimal przebieg = decimal.Parse(txtPrzebieg.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                decimal cenaPaliwa = decimal.Parse(txtCenaPaliwa.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                int czasRozladunku = int.Parse(txtCzasRozladunku.Text);
                decimal minDarmowy = decimal.Parse(txtMinDarmowy.Text.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Sprawdź czy rekord istnieje
                    string checkQuery = "SELECT COUNT(*) FROM OdbiorcyTransport WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@OdbiorcaID", _odbiorcaID);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        string query;
                        if (count > 0)
                        {
                            query = @"
                                UPDATE OdbiorcyTransport SET
                                    TypTransportu = @TypTransportu,
                                    KosztStalyDostawy = @KosztStaly,
                                    KosztGodzinyKierowcy = @KosztGodziny,
                                    SredniPrzebiegLitr = @Przebieg,
                                    CenaPaliwaLitr = @CenaPaliwa,
                                    CzasRozladunku = @CzasRozladunku,
                                    MinWartoscDlaDarmowegoTransportu = @MinDarmowy
                                WHERE OdbiorcaID = @OdbiorcaID";
                        }
                        else
                        {
                            query = @"
                                INSERT INTO OdbiorcyTransport
                                (OdbiorcaID, TypTransportu, KosztStalyDostawy, KosztGodzinyKierowcy, SredniPrzebiegLitr, CenaPaliwaLitr, CzasRozladunku, MinWartoscDlaDarmowegoTransportu, DataUtworzenia)
                                VALUES
                                (@OdbiorcaID, @TypTransportu, @KosztStaly, @KosztGodziny, @Przebieg, @CenaPaliwa, @CzasRozladunku, @MinDarmowy, GETDATE())";
                        }

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@OdbiorcaID", _odbiorcaID);
                            cmd.Parameters.AddWithValue("@TypTransportu", cmbTypTransportu.SelectedItem?.ToString() ?? "Własny");
                            cmd.Parameters.AddWithValue("@KosztStaly", kosztStaly);
                            cmd.Parameters.AddWithValue("@KosztGodziny", kosztGodziny);
                            cmd.Parameters.AddWithValue("@Przebieg", przebieg);
                            cmd.Parameters.AddWithValue("@CenaPaliwa", cenaPaliwa);
                            cmd.Parameters.AddWithValue("@CzasRozladunku", czasRozladunku);
                            cmd.Parameters.AddWithValue("@MinDarmowy", minDarmowy);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                MessageBox.Show("Parametry transportu zostały zapisane!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                ParametryZapisane?.Invoke(this, EventArgs.Empty);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
