using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    // ============================================
    // Partial class z dodatkowymi metodami
    // Dodaj tę zawartość do KartotekaOdbiorcowWindow.xaml.cs
    // jako partial class lub skopiuj metody do głównej klasy
    // ============================================
    public partial class KartotekaOdbiorcowWindow : Window
    {
        // Metody pomocnicze do budowania UI dynamicznie

        private void InicjalizujZakladki()
        {
            // Inicjalizacja zakładki Dane Podstawowe
            gridDanePodstawowe.Children.Clear();
            gridDanePodstawowe.RowDefinitions.Clear();
            gridDanePodstawowe.ColumnDefinitions.Clear();

            // Definicje kolumn
            gridDanePodstawowe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            gridDanePodstawowe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridDanePodstawowe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            gridDanePodstawowe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // Sekcja: Dane firmowe
            DodajNaglowekSekcji(gridDanePodstawowe, "🏢 Dane Firmowe", ref row);
            DodajPoleFormularza(gridDanePodstawowe, "Nazwa skrócona:", "txtNazwaSkrot", ref row, 0);
            DodajPoleFormularza(gridDanePodstawowe, "Pełna nazwa:", "txtPelnaNazwa", ref row, 0, 4);
            DodajPoleFormularza(gridDanePodstawowe, "NIP:", "txtNIP", ref row, 0);
            DodajPoleFormularza(gridDanePodstawowe, "REGON:", "txtREGON", ref row, 2);

            // Sekcja: Adres
            DodajNaglowekSekcji(gridDanePodstawowe, "📍 Adres", ref row);
            DodajPoleFormularza(gridDanePodstawowe, "Ulica:", "txtUlica", ref row, 0, 4);
            DodajPoleFormularza(gridDanePodstawowe, "Kod pocztowy:", "txtKodPocztowy", ref row, 0);
            DodajPoleFormularza(gridDanePodstawowe, "Miejscowość:", "txtMiejscowosc", ref row, 2);
            DodajPoleFormularza(gridDanePodstawowe, "Województwo:", "txtWojewodztwo", ref row, 0);
            DodajPoleFormularza(gridDanePodstawowe, "Kraj:", "txtKraj", ref row, 2);

            // Sekcja: Klasyfikacja
            DodajNaglowekSekcji(gridDanePodstawowe, "📊 Klasyfikacja", ref row);
            DodajComboBoxFormularza(gridDanePodstawowe, "Kategoria:", "cmbKategoriaOdbiorcy", 
                new[] { "A", "B", "C" }, ref row, 0);
            DodajComboBoxFormularza(gridDanePodstawowe, "Typ odbiorcy:", "cmbTypOdbiorcyEdit", 
                new[] { "Hurtownia", "Sklep", "Restauracja", "Export", "Przetwórnia" }, ref row, 2);
            DodajComboBoxFormularza(gridDanePodstawowe, "Rating kredytowy:", "cmbRating", 
                new[] { "AAA", "AA", "A", "B", "C" }, ref row, 0);
            DodajCheckBoxFormularza(gridDanePodstawowe, "Aktywny:", "chkAktywny", ref row, 2);

            // Przyciski akcji
            DodajPrzyciskiAkcji(gridDanePodstawowe, ref row);
        }

        private void WczytajDanePodstawowe()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT * FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                UstawWartoscPola(gridDanePodstawowe, "txtNazwaSkrot", reader["NazwaSkrot"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtPelnaNazwa", reader["PelnaNazwa"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtNIP", reader["NIP"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtREGON", reader["REGON"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtUlica", reader["Ulica"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtKodPocztowy", reader["KodPocztowy"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtMiejscowosc", reader["Miejscowosc"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtWojewodztwo", reader["Wojewodztwo"].ToString());
                                UstawWartoscPola(gridDanePodstawowe, "txtKraj", reader["Kraj"].ToString());
                                
                                UstawWartoscComboBox(gridDanePodstawowe, "cmbKategoriaOdbiorcy", reader["KategoriaOdbiorcy"].ToString());
                                UstawWartoscComboBox(gridDanePodstawowe, "cmbTypOdbiorcyEdit", reader["TypOdbiorcy"].ToString());
                                UstawWartoscComboBox(gridDanePodstawowe, "cmbRating", reader["RatingKredytowy"].ToString());
                                
                                var chk = ZnajdzKontrolke<CheckBox>(gridDanePodstawowe, "chkAktywny");
                                if (chk != null) chk.IsChecked = Convert.ToBoolean(reader["StatusAktywny"]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ZapiszDanePodstawowe()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        UPDATE Odbiorcy SET
                            NazwaSkrot = @NazwaSkrot,
                            PelnaNazwa = @PelnaNazwa,
                            NIP = @NIP,
                            REGON = @REGON,
                            Ulica = @Ulica,
                            KodPocztowy = @KodPocztowy,
                            Miejscowosc = @Miejscowosc,
                            Wojewodztwo = @Wojewodztwo,
                            Kraj = @Kraj,
                            KategoriaOdbiorcy = @KategoriaOdbiorcy,
                            TypOdbiorcy = @TypOdbiorcy,
                            RatingKredytowy = @RatingKredytowy,
                            StatusAktywny = @StatusAktywny,
                            DataModyfikacji = GETDATE(),
                            KtoZmodyfikowal = @UserID
                        WHERE OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        cmd.Parameters.AddWithValue("@NazwaSkrot", PobierzWartoscPola(gridDanePodstawowe, "txtNazwaSkrot"));
                        cmd.Parameters.AddWithValue("@PelnaNazwa", PobierzWartoscPola(gridDanePodstawowe, "txtPelnaNazwa"));
                        cmd.Parameters.AddWithValue("@NIP", PobierzWartoscPola(gridDanePodstawowe, "txtNIP") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@REGON", PobierzWartoscPola(gridDanePodstawowe, "txtREGON") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Ulica", PobierzWartoscPola(gridDanePodstawowe, "txtUlica") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@KodPocztowy", PobierzWartoscPola(gridDanePodstawowe, "txtKodPocztowy") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Miejscowosc", PobierzWartoscPola(gridDanePodstawowe, "txtMiejscowosc") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Wojewodztwo", PobierzWartoscPola(gridDanePodstawowe, "txtWojewodztwo") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Kraj", PobierzWartoscPola(gridDanePodstawowe, "txtKraj") ?? "Polska");
                        cmd.Parameters.AddWithValue("@KategoriaOdbiorcy", PobierzWartoscComboBox(gridDanePodstawowe, "cmbKategoriaOdbiorcy") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypOdbiorcy", PobierzWartoscComboBox(gridDanePodstawowe, "cmbTypOdbiorcyEdit") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@RatingKredytowy", PobierzWartoscComboBox(gridDanePodstawowe, "cmbRating") ?? (object)DBNull.Value);
                        
                        var chk = ZnajdzKontrolke<CheckBox>(gridDanePodstawowe, "chkAktywny");
                        cmd.Parameters.AddWithValue("@StatusAktywny", chk?.IsChecked ?? true);
                        cmd.Parameters.AddWithValue("@UserID", int.Parse(UserID));

                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Dane zapisane pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                        WczytajOdbiorcow();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajDaneFinansowe()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            gridDaneFinansowe.Children.Clear();
            gridDaneFinansowe.RowDefinitions.Clear();
            gridDaneFinansowe.ColumnDefinitions.Clear();

            gridDaneFinansowe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            gridDaneFinansowe.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            o.LimitKredytu,
                            o.AktualnaSaldo,
                            o.TerminPlatnosci,
                            o.FormaPlatnosci
                        FROM Odbiorcy o
                        WHERE o.OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int row = 0;
                                DodajNaglowekSekcji(gridDaneFinansowe, "💰 Dane Finansowe", ref row);
                                
                                DodajPoleTekstowe(gridDaneFinansowe, "Limit kredytu:", 
                                    reader["LimitKredytu"] != DBNull.Value ? 
                                    Convert.ToDecimal(reader["LimitKredytu"]).ToString("N2") + " PLN" : "0.00 PLN", ref row);
                                
                                DodajPoleTekstowe(gridDaneFinansowe, "Aktualne saldo:", 
                                    reader["AktualnaSaldo"] != DBNull.Value ? 
                                    Convert.ToDecimal(reader["AktualnaSaldo"]).ToString("N2") + " PLN" : "0.00 PLN", ref row);
                                
                                DodajPoleTekstowe(gridDaneFinansowe, "Termin płatności:", 
                                    reader["TerminPlatnosci"] != DBNull.Value ? 
                                    reader["TerminPlatnosci"].ToString() + " dni" : "0 dni", ref row);
                                
                                DodajPoleTekstowe(gridDaneFinansowe, "Forma płatności:", 
                                    reader["FormaPlatnosci"].ToString(), ref row);
                            }
                        }
                    }
                }

                // Przycisk do szczegółów płatności
                int btnRow = gridDaneFinansowe.RowDefinitions.Count;
                gridDaneFinansowe.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var btn = new Button
                {
                    Content = "📊 Zobacz szczegóły płatności",
                    Style = (Style)FindResource("ButtonPrimary"),
                    Margin = new Thickness(0, 20, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 250
                };
                btn.Click += BtnSzczegolyPlatnosci_Click;
                Grid.SetRow(btn, btnRow);
                Grid.SetColumnSpan(btn, 2);
                gridDaneFinansowe.Children.Add(btn);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajDaneTransportu()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            gridTransport.Children.Clear();
            gridTransport.RowDefinitions.Clear();
            gridTransport.ColumnDefinitions.Clear();

            gridTransport.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            gridTransport.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            TypTransportu,
                            KosztTransportuKm,
                            MinimalneZamowienie,
                            PreferowaneDniDostawy,
                            PreferowaneGodzinyOd,
                            PreferowaneGodzinyDo,
                            CzasRozladunku,
                            WymaganyTypPojazdu,
                            Uwagi
                        FROM OdbiorcyTransport
                        WHERE OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int row = 0;
                            DodajNaglowekSekcji(gridTransport, "🚚 Dane Transportowe", ref row);
                            
                            if (reader.Read())
                            {
                                DodajPoleTekstowe(gridTransport, "Typ transportu:", reader["TypTransportu"].ToString(), ref row);
                                DodajPoleTekstowe(gridTransport, "Koszt transportu:", 
                                    reader["KosztTransportuKm"] != DBNull.Value ? 
                                    Convert.ToDecimal(reader["KosztTransportuKm"]).ToString("N2") + " PLN/km" : "-", ref row);
                                DodajPoleTekstowe(gridTransport, "Minimalne zamówienie:", 
                                    reader["MinimalneZamowienie"] != DBNull.Value ? 
                                    Convert.ToDecimal(reader["MinimalneZamowienie"]).ToString("N2") + " kg" : "-", ref row);
                                DodajPoleTekstowe(gridTransport, "Preferowane dni:", reader["PreferowaneDniDostawy"].ToString(), ref row);
                                DodajPoleTekstowe(gridTransport, "Godziny dostawy:", 
                                    $"{reader["PreferowaneGodzinyOd"]} - {reader["PreferowaneGodzinyDo"]}", ref row);
                                DodajPoleTekstowe(gridTransport, "Czas rozładunku:", 
                                    reader["CzasRozladunku"] != DBNull.Value ? reader["CzasRozladunku"].ToString() + " min" : "-", ref row);
                                DodajPoleTekstowe(gridTransport, "Typ pojazdu:", reader["WymaganyTypPojazdu"].ToString(), ref row);
                                DodajPoleTekstowe(gridTransport, "Uwagi:", reader["Uwagi"].ToString(), ref row);
                            }
                            else
                            {
                                DodajInfoBrak(gridTransport, "Brak danych transportowych. Dodaj je w systemie.", ref row);
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

        private void WczytajLokalizacje()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            gridLokalizacja.Children.Clear();
            gridLokalizacja.RowDefinitions.Clear();
            gridLokalizacja.ColumnDefinitions.Clear();

            gridLokalizacja.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            gridLokalizacja.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            Ulica, KodPocztowy, Miejscowosc, Wojewodztwo,
                            Szerokosc, Dlugosc, OdlegloscKm
                        FROM Odbiorcy
                        WHERE OdbiorcaID = @OdbiorcaID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int row = 0;
                                DodajNaglowekSekcji(gridLokalizacja, "🗺️ Lokalizacja", ref row);
                                
                                string adres = $"{reader["Ulica"]}, {reader["KodPocztowy"]} {reader["Miejscowosc"]}";
                                DodajPoleTekstowe(gridLokalizacja, "Adres:", adres, ref row);
                                
                                if (reader["Szerokosc"] != DBNull.Value && reader["Dlugosc"] != DBNull.Value)
                                {
                                    DodajPoleTekstowe(gridLokalizacja, "Współrzędne GPS:", 
                                        $"{reader["Szerokosc"]}, {reader["Dlugosc"]}", ref row);
                                    
                                    if (reader["OdlegloscKm"] != DBNull.Value)
                                    {
                                        decimal odleglosc = Convert.ToDecimal(reader["OdlegloscKm"]);
                                        DodajPoleTekstowe(gridLokalizacja, "Odległość od firmy:", 
                                            $"{odleglosc:N2} km", ref row);
                                        
                                        decimal kosztKm = 3.50m;
                                        decimal kosztTransportu = odleglosc * 2 * kosztKm;
                                        DodajPoleTekstowe(gridLokalizacja, "Szacunkowy koszt transportu:", 
                                            $"{kosztTransportu:N2} PLN (w dwie strony, {kosztKm:N2} PLN/km)", ref row);
                                    }
                                }
                                else
                                {
                                    DodajInfoBrak(gridLokalizacja, "Brak danych GPS. Kliknij poniżej aby ustawić lokalizację.", ref row);
                                }

                                // Przycisk
                                int btnRow = gridLokalizacja.RowDefinitions.Count;
                                gridLokalizacja.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                                
                                var btn = new Button
                                {
                                    Content = "📍 Ustaw lokalizację GPS",
                                    Style = (Style)FindResource("ButtonPrimary"),
                                    Margin = new Thickness(0, 20, 0, 0),
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    Width = 200
                                };
                                btn.Click += BtnUstawLokalizacje_Click;
                                Grid.SetRow(btn, btnRow);
                                Grid.SetColumnSpan(btn, 2);
                                gridLokalizacja.Children.Add(btn);
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

        private void WczytajStatystyki()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            gridStatystyki.Children.Clear();
            gridStatystyki.RowDefinitions.Clear();
            gridStatystyki.ColumnDefinitions.Clear();

            gridStatystyki.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            gridStatystyki.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                string nazwaOdbiorcy = "";
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = "SELECT NazwaSkrot FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        nazwaOdbiorcy = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                using (SqlConnection conn = new SqlConnection(_connHandel))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            COUNT(DISTINCT d.DocumentID) AS LiczbaFaktur,
                            ISNULL(SUM(d.GrossValue), 0) AS WartoscBrutto,
                            ISNULL(SUM(d.NetValue), 0) AS WartoscNetto,
                            ISNULL(AVG(d.GrossValue), 0) AS SredniaWartosc,
                            MIN(d.DocumentDate) AS PierwszaFaktura,
                            MAX(d.DocumentDate) AS OstatniaFaktura
                        FROM [HANDEL].[SSCommon].[STDocuments] d
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] c ON d.ContractorGuid = c.Guid
                        WHERE c.Shortcut = @NazwaOdbiorcy
                            AND d.DocumentType IN (310, 311)
                            AND d.DocumentDate >= DATEADD(YEAR, -1, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaOdbiorcy", nazwaOdbiorcy);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int row = 0;
                                DodajNaglowekSekcji(gridStatystyki, "📊 Statystyki Sprzedaży (12 miesięcy)", ref row);
                                
                                DodajPoleTekstowe(gridStatystyki, "Liczba faktur:", reader["LiczbaFaktur"].ToString(), ref row);
                                DodajPoleTekstowe(gridStatystyki, "Wartość netto:", 
                                    Convert.ToDecimal(reader["WartoscNetto"]).ToString("N2") + " PLN", ref row);
                                DodajPoleTekstowe(gridStatystyki, "Wartość brutto:", 
                                    Convert.ToDecimal(reader["WartoscBrutto"]).ToString("N2") + " PLN", ref row);
                                DodajPoleTekstowe(gridStatystyki, "Średnia wartość faktury:", 
                                    Convert.ToDecimal(reader["SredniaWartosc"]).ToString("N2") + " PLN", ref row);
                                
                                if (reader["PierwszaFaktura"] != DBNull.Value)
                                    DodajPoleTekstowe(gridStatystyki, "Pierwsza faktura:", 
                                        Convert.ToDateTime(reader["PierwszaFaktura"]).ToString("dd.MM.yyyy"), ref row);
                                
                                if (reader["OstatniaFaktura"] != DBNull.Value)
                                    DodajPoleTekstowe(gridStatystyki, "Ostatnia faktura:", 
                                        Convert.ToDateTime(reader["OstatniaFaktura"]).ToString("dd.MM.yyyy"), ref row);
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

        // Metody pomocnicze UI

        private void DodajNaglowekSekcji(Grid grid, string tekst, ref int row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var separator = new Border
            {
                Height = 3,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                Margin = new Thickness(0, row == 0 ? 0 : 20, 0, 10),
                CornerRadius = new CornerRadius(2)
            };
            Grid.SetRow(separator, row);
            Grid.SetColumnSpan(separator, grid.ColumnDefinitions.Count);
            grid.Children.Add(separator);
            
            row++;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var textBlock = new TextBlock
            {
                Text = tekst,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(textBlock, row);
            Grid.SetColumnSpan(textBlock, grid.ColumnDefinitions.Count);
            grid.Children.Add(textBlock);
            
            row++;
        }

        private void DodajPoleFormularza(Grid grid, string label, string nazwa, ref int row, int kolumna, int colspan = 2)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var labelControl = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
            };
            Grid.SetRow(labelControl, row);
            Grid.SetColumn(labelControl, kolumna);
            grid.Children.Add(labelControl);
            
            row++;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var textBox = new TextBox
            {
                Name = nazwa,
                Style = (Style)FindResource("TextBoxModern"),
                Margin = new Thickness(0, 0, kolumna == 0 && colspan == 2 ? 20 : 0, 15)
            };
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, kolumna);
            Grid.SetColumnSpan(textBox, colspan);
            grid.Children.Add(textBox);
            
            row++;
        }

        private void DodajComboBoxFormularza(Grid grid, string label, string nazwa, string[] items, ref int row, int kolumna)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var labelControl = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
            };
            Grid.SetRow(labelControl, row);
            Grid.SetColumn(labelControl, kolumna);
            grid.Children.Add(labelControl);
            
            row++;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var comboBox = new ComboBox
            {
                Name = nazwa,
                Style = (Style)FindResource("ComboBoxModern"),
                Margin = new Thickness(0, 0, 20, 15)
            };
            
            foreach (var item in items)
                comboBox.Items.Add(new ComboBoxItem { Content = item });
            
            Grid.SetRow(comboBox, row);
            Grid.SetColumn(comboBox, kolumna);
            grid.Children.Add(comboBox);
            
            row++;
        }

        private void DodajCheckBoxFormularza(Grid grid, string label, string nazwa, ref int row, int kolumna)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var checkBox = new CheckBox
            {
                Name = nazwa,
                Content = label,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 15),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(checkBox, row);
            Grid.SetColumn(checkBox, kolumna);
            grid.Children.Add(checkBox);
            
            row++;
        }

        private void DodajPrzyciskiAkcji(Grid grid, ref int row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var btnZapisz = new Button
            {
                Content = "💾 Zapisz zmiany",
                Style = (Style)FindResource("ButtonSuccess"),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 150
            };
            btnZapisz.Click += (s, e) => ZapiszDanePodstawowe();
            stackPanel.Children.Add(btnZapisz);
            
            var btnSynchronizuj = new Button
            {
                Content = "🔄 Synchronizuj z Handel",
                Style = (Style)FindResource("ButtonPrimary"),
                Width = 200
            };
            btnSynchronizuj.Click += BtnSynchronizuj_Click;
            stackPanel.Children.Add(btnSynchronizuj);
            
            Grid.SetRow(stackPanel, row);
            Grid.SetColumnSpan(stackPanel, grid.ColumnDefinitions.Count);
            grid.Children.Add(stackPanel);
            
            row++;
        }

        private void DodajPoleTekstowe(Grid grid, string label, string wartosc, ref int row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var labelControl = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(labelControl, row);
            Grid.SetColumn(labelControl, 0);
            grid.Children.Add(labelControl);
            
            var wartoscControl = new TextBlock
            {
                Text = wartosc,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(wartoscControl, row);
            Grid.SetColumn(wartoscControl, 1);
            grid.Children.Add(wartoscControl);
            
            row++;
        }

        private void DodajInfoBrak(Grid grid, string tekst, ref int row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var textBlock = new TextBlock
            {
                Text = tekst,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                Margin = new Thickness(0, 10, 0, 10)
            };
            Grid.SetRow(textBlock, row);
            Grid.SetColumnSpan(textBlock, 2);
            grid.Children.Add(textBlock);
            
            row++;
        }

        private void UstawWartoscPola(Grid grid, string nazwa, string wartosc)
        {
            var textBox = ZnajdzKontrolke<TextBox>(grid, nazwa);
            if (textBox != null) textBox.Text = wartosc ?? "";
        }

        private string PobierzWartoscPola(Grid grid, string nazwa)
        {
            var textBox = ZnajdzKontrolke<TextBox>(grid, nazwa);
            return textBox?.Text;
        }

        private void UstawWartoscComboBox(Grid grid, string nazwa, string wartosc)
        {
            var comboBox = ZnajdzKontrolke<ComboBox>(grid, nazwa);
            if (comboBox != null && !string.IsNullOrEmpty(wartosc))
            {
                foreach (ComboBoxItem item in comboBox.Items)
                {
                    if (item.Content.ToString() == wartosc)
                    {
                        comboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private string PobierzWartoscComboBox(Grid grid, string nazwa)
        {
            var comboBox = ZnajdzKontrolke<ComboBox>(grid, nazwa);
            return (comboBox?.SelectedItem as ComboBoxItem)?.Content.ToString();
        }

        private T ZnajdzKontrolke<T>(Grid grid, string nazwa) where T : FrameworkElement
        {
            foreach (var child in grid.Children)
            {
                if (child is T control && control.Name == nazwa)
                    return control;
            }
            return null;
        }
    }
}
