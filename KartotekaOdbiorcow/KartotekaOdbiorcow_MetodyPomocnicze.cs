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

        private void WczytajAnalizaKosztow()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            gridAnalizaKosztow.Children.Clear();
            gridAnalizaKosztow.RowDefinitions.Clear();
            gridAnalizaKosztow.ColumnDefinitions.Clear();

            gridAnalizaKosztow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            gridAnalizaKosztow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            gridAnalizaKosztow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            gridAnalizaKosztow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                // Pobierz dane odbiorcy i transportu
                decimal odlegloscKm = 0;
                decimal kosztKm = 3.50m;
                decimal kosztStalyDostawy = 0;
                decimal kosztGodzinyKierowcy = 50m;
                decimal sredniPrzebiegLitr = 25m;
                decimal cenaPaliwaLitr = 6.50m;
                int czasRozladunku = 30;
                decimal minWartoscDarmowy = 0;
                string nazwaOdbiorcy = "";

                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();

                    // Dane odbiorcy
                    string queryOdbiorca = "SELECT NazwaSkrot, OdlegloscKm FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(queryOdbiorca, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                nazwaOdbiorcy = reader["NazwaSkrot"].ToString();
                                if (reader["OdlegloscKm"] != DBNull.Value)
                                    odlegloscKm = Convert.ToDecimal(reader["OdlegloscKm"]);
                            }
                        }
                    }

                    // Dane transportowe
                    string queryTransport = @"
                        SELECT
                            ISNULL(KosztTransportuKm, 3.50) AS KosztKm,
                            ISNULL(KosztStalyDostawy, 0) AS KosztStaly,
                            ISNULL(KosztGodzinyKierowcy, 50) AS KosztGodziny,
                            ISNULL(SredniPrzebiegLitr, 25) AS Przebieg,
                            ISNULL(CenaPaliwaLitr, 6.50) AS CenaPaliwa,
                            ISNULL(CzasRozladunku, 30) AS CzasRozladunku,
                            ISNULL(MinWartoscDlaDarmowegoTransportu, 0) AS MinDarmowy
                        FROM OdbiorcyTransport WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(queryTransport, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                kosztKm = Convert.ToDecimal(reader["KosztKm"]);
                                kosztStalyDostawy = Convert.ToDecimal(reader["KosztStaly"]);
                                kosztGodzinyKierowcy = Convert.ToDecimal(reader["KosztGodziny"]);
                                sredniPrzebiegLitr = Convert.ToDecimal(reader["Przebieg"]);
                                cenaPaliwaLitr = Convert.ToDecimal(reader["CenaPaliwa"]);
                                czasRozladunku = Convert.ToInt32(reader["CzasRozladunku"]);
                                minWartoscDarmowy = Convert.ToDecimal(reader["MinDarmowy"]);
                            }
                        }
                    }
                }

                // Obliczenia kosztów transportu
                decimal dystansWObie = odlegloscKm * 2;
                decimal kosztPaliwa = (dystansWObie / sredniPrzebiegLitr) * cenaPaliwaLitr;
                decimal czasJazdyGodzin = dystansWObie / 60m; // zakładamy 60 km/h średnio
                decimal czasCalkowityGodzin = czasJazdyGodzin + (czasRozladunku / 60m);
                decimal kosztKierowcy = czasCalkowityGodzin * kosztGodzinyKierowcy;
                decimal kosztCalkowityDostawy = kosztStalyDostawy + kosztPaliwa + kosztKierowcy;

                // Pobierz statystyki zamówień
                int liczbaZamowien = 0;
                decimal srednieZamowienieKg = 0;
                decimal srednieZamowieniePLN = 0;

                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string queryZam = @"
                        SELECT
                            COUNT(*) AS Liczba,
                            ISNULL(AVG(CAST(zm.IloscKg AS DECIMAL)), 0) AS SredniaKg
                        FROM ZamowieniaMieso zm
                        INNER JOIN Odbiorcy o ON zm.KlientId = o.IdOdbiorcy
                        WHERE o.OdbiorcaID = @OdbiorcaID
                            AND zm.DataPrzyjazdu >= DATEADD(MONTH, -3, GETDATE())
                            AND zm.Status NOT IN ('Anulowane')";
                    using (SqlCommand cmd = new SqlCommand(queryZam, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                liczbaZamowien = Convert.ToInt32(reader["Liczba"]);
                                srednieZamowienieKg = Convert.ToDecimal(reader["SredniaKg"]);
                            }
                        }
                    }
                }

                // Pobierz średnią wartość faktury z Handel
                using (SqlConnection conn = new SqlConnection(_connHandel))
                {
                    conn.Open();
                    string queryFak = @"
                        SELECT ISNULL(AVG(d.NetValue), 0) AS SredniaWartosc
                        FROM [HANDEL].[SSCommon].[STDocuments] d
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] c ON d.ContractorGuid = c.Guid
                        WHERE c.Shortcut = @NazwaOdbiorcy
                            AND d.DocumentType IN (310, 311)
                            AND d.DocumentDate >= DATEADD(MONTH, -3, GETDATE())";
                    using (SqlCommand cmd = new SqlCommand(queryFak, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaOdbiorcy", nazwaOdbiorcy);
                        var result = cmd.ExecuteScalar();
                        if (result != DBNull.Value)
                            srednieZamowieniePLN = Convert.ToDecimal(result);
                    }
                }

                // Oblicz wskaźniki
                decimal kosztNaKg = srednieZamowienieKg > 0 ? kosztCalkowityDostawy / srednieZamowienieKg : 0;
                decimal procentKosztuTransportu = srednieZamowieniePLN > 0 ? (kosztCalkowityDostawy / srednieZamowieniePLN) * 100 : 0;

                // Buduj UI
                int row = 0;

                // Sekcja 1: Parametry transportu
                DodajNaglowekSekcji(gridAnalizaKosztow, "🚚 Parametry Transportu", ref row);

                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Odległość od firmy:", $"{odlegloscKm:N1} km", ref row, 0);
                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Dystans w obie strony:", $"{dystansWObie:N1} km", ref row, 2, true);

                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Koszt stały dostawy:", $"{kosztStalyDostawy:N2} PLN", ref row, 0);
                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Czas rozładunku:", $"{czasRozladunku} min", ref row, 2, true);

                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Cena paliwa:", $"{cenaPaliwaLitr:N2} PLN/l", ref row, 0);
                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Średni przebieg:", $"{sredniPrzebiegLitr:N1} km/l", ref row, 2, true);

                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Koszt godziny kierowcy:", $"{kosztGodzinyKierowcy:N2} PLN/h", ref row, 0);
                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Szacowany czas jazdy:", $"{czasJazdyGodzin:N1} h", ref row, 2, true);

                // Sekcja 2: Kalkulacja kosztów
                DodajNaglowekSekcji(gridAnalizaKosztow, "💰 Kalkulacja Kosztów Dostawy", ref row);

                // Panel z kosztami
                var panelKoszty = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F9FF")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 10, 0, 20)
                };

                var stackKoszty = new StackPanel();
                stackKoszty.Children.Add(CreateCostLine("Koszt paliwa:", $"{kosztPaliwa:N2} PLN", "(dystans / przebieg × cena)"));
                stackKoszty.Children.Add(CreateCostLine("Koszt kierowcy:", $"{kosztKierowcy:N2} PLN", $"({czasCalkowityGodzin:N1}h × {kosztGodzinyKierowcy:N0} PLN)"));
                stackKoszty.Children.Add(CreateCostLine("Koszt stały:", $"{kosztStalyDostawy:N2} PLN", ""));
                stackKoszty.Children.Add(new System.Windows.Shapes.Rectangle { Height = 2, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")), Margin = new Thickness(0, 10, 0, 10) });

                var totalLine = CreateCostLine("RAZEM KOSZT DOSTAWY:", $"{kosztCalkowityDostawy:N2} PLN", "");
                ((totalLine.Children[0] as TextBlock)!).FontSize = 16;
                ((totalLine.Children[0] as TextBlock)!).FontWeight = FontWeights.Bold;
                ((totalLine.Children[1] as TextBlock)!).FontSize = 18;
                ((totalLine.Children[1] as TextBlock)!).Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                stackKoszty.Children.Add(totalLine);

                panelKoszty.Child = stackKoszty;

                gridAnalizaKosztow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(panelKoszty, row);
                Grid.SetColumnSpan(panelKoszty, 4);
                gridAnalizaKosztow.Children.Add(panelKoszty);
                row++;

                // Sekcja 3: Wskaźniki rentowności
                DodajNaglowekSekcji(gridAnalizaKosztow, "📊 Wskaźniki Rentowności (3 miesiące)", ref row);

                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Liczba dostaw:", $"{liczbaZamowien}", ref row, 0);
                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Średnie zamówienie:", $"{srednieZamowienieKg:N0} kg", ref row, 2, true);

                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Średnia wartość faktury:", $"{srednieZamowieniePLN:N2} PLN", ref row, 0);
                DodajPoleTekstoweKolumna(gridAnalizaKosztow, "Koszt transportu na kg:", $"{kosztNaKg:N2} PLN/kg", ref row, 2, true);

                // Panel z procentem
                var panelProcent = new Border
                {
                    Background = procentKosztuTransportu > 10 ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FDF4")),
                    BorderBrush = procentKosztuTransportu > 10 ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 15, 0, 0)
                };

                var stackProcent = new StackPanel { Orientation = Orientation.Horizontal };
                stackProcent.Children.Add(new TextBlock
                {
                    Text = "Udział transportu w wartości zamówienia: ",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                });
                stackProcent.Children.Add(new TextBlock
                {
                    Text = $"{procentKosztuTransportu:N1}%",
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = procentKosztuTransportu > 10 ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0)
                });
                stackProcent.Children.Add(new TextBlock
                {
                    Text = procentKosztuTransportu > 10 ? "⚠️ Wysoki!" : procentKosztuTransportu > 5 ? "📊 Średni" : "✅ Niski",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                });

                panelProcent.Child = stackProcent;

                gridAnalizaKosztow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(panelProcent, row);
                Grid.SetColumnSpan(panelProcent, 4);
                gridAnalizaKosztow.Children.Add(panelProcent);
                row++;

                // Minimalna wartość dla darmowego transportu
                if (minWartoscDarmowy > 0)
                {
                    gridAnalizaKosztow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var txtDarmowy = new TextBlock
                    {
                        Text = $"💡 Minimalna wartość zamówienia dla darmowego transportu: {minWartoscDarmowy:N2} PLN",
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                        Margin = new Thickness(0, 15, 0, 0)
                    };
                    Grid.SetRow(txtDarmowy, row);
                    Grid.SetColumnSpan(txtDarmowy, 4);
                    gridAnalizaKosztow.Children.Add(txtDarmowy);
                    row++;
                }

                // Przycisk edycji parametrów
                gridAnalizaKosztow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var btnEdytuj = new Button
                {
                    Content = "✏️ Edytuj parametry transportu",
                    Style = (Style)FindResource("ButtonPrimary"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 250,
                    Margin = new Thickness(0, 25, 0, 0)
                };
                btnEdytuj.Click += BtnEdytujParametryTransportu_Click;
                Grid.SetRow(btnEdytuj, row);
                Grid.SetColumnSpan(btnEdytuj, 4);
                gridAnalizaKosztow.Children.Add(btnEdytuj);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private StackPanel CreateCostLine(string label, string value, string formula)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            stack.Children.Add(new TextBlock { Text = label, Width = 200, FontWeight = FontWeights.Medium });
            stack.Children.Add(new TextBlock { Text = value, Width = 120, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")) });
            if (!string.IsNullOrEmpty(formula))
                stack.Children.Add(new TextBlock { Text = formula, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")), FontStyle = FontStyles.Italic });
            return stack;
        }

        private void DodajPoleTekstoweKolumna(Grid grid, string label, string wartosc, ref int row, int kolumna, bool nowyWiersz = false)
        {
            if (kolumna == 0)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelControl = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(labelControl, row);
            Grid.SetColumn(labelControl, kolumna);
            grid.Children.Add(labelControl);

            var wartoscControl = new TextBlock
            {
                Text = wartosc,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(wartoscControl, row);
            Grid.SetColumn(wartoscControl, kolumna + 1);
            grid.Children.Add(wartoscControl);

            if (nowyWiersz)
                row++;
        }

        private void BtnEdytujParametryTransportu_Click(object sender, RoutedEventArgs e)
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            var dialog = new EdytujParametryTransportuDialog(wybranyOdbiorcaID.Value, _connLibraNet);
            dialog.ParametryZapisane += (s, ev) => WczytajAnalizaKosztow();
            dialog.ShowDialog();
        }

        private void WczytajRentownosc()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            gridRentownosc.Children.Clear();
            gridRentownosc.RowDefinitions.Clear();
            gridRentownosc.ColumnDefinitions.Clear();

            gridRentownosc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            gridRentownosc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            gridRentownosc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            gridRentownosc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                string nazwaOdbiorcy = "";
                decimal odlegloscKm = 0;
                decimal kosztTransportuDostawy = 0;

                // Pobierz dane odbiorcy
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string queryOdbiorca = "SELECT NazwaSkrot, OdlegloscKm FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(queryOdbiorca, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                nazwaOdbiorcy = reader["NazwaSkrot"].ToString();
                                if (reader["OdlegloscKm"] != DBNull.Value)
                                    odlegloscKm = Convert.ToDecimal(reader["OdlegloscKm"]);
                            }
                        }
                    }

                    // Oblicz koszt transportu
                    decimal kosztKm = 3.50m;
                    decimal kosztGodzinyKierowcy = 50m;
                    decimal sredniPrzebiegLitr = 25m;
                    decimal cenaPaliwaLitr = 6.50m;
                    int czasRozladunku = 30;

                    string queryTransport = @"
                        SELECT
                            ISNULL(KosztTransportuKm, 3.50) AS KosztKm,
                            ISNULL(KosztGodzinyKierowcy, 50) AS KosztGodziny,
                            ISNULL(SredniPrzebiegLitr, 25) AS Przebieg,
                            ISNULL(CenaPaliwaLitr, 6.50) AS CenaPaliwa,
                            ISNULL(CzasRozladunku, 30) AS CzasRozladunku
                        FROM OdbiorcyTransport WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(queryTransport, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                kosztKm = Convert.ToDecimal(reader["KosztKm"]);
                                kosztGodzinyKierowcy = Convert.ToDecimal(reader["KosztGodziny"]);
                                sredniPrzebiegLitr = Convert.ToDecimal(reader["Przebieg"]);
                                cenaPaliwaLitr = Convert.ToDecimal(reader["CenaPaliwa"]);
                                czasRozladunku = Convert.ToInt32(reader["CzasRozladunku"]);
                            }
                        }
                    }

                    decimal dystansWObie = odlegloscKm * 2;
                    decimal kosztPaliwa = (dystansWObie / sredniPrzebiegLitr) * cenaPaliwaLitr;
                    decimal czasJazdyGodzin = dystansWObie / 60m;
                    decimal czasCalkowityGodzin = czasJazdyGodzin + (czasRozladunku / 60m);
                    decimal kosztKierowcy = czasCalkowityGodzin * kosztGodzinyKierowcy;
                    kosztTransportuDostawy = kosztPaliwa + kosztKierowcy;
                }

                // Pobierz ceny zakupu z PZ (średnia ważona z ostatnich 3 miesięcy)
                var cenyZakupu = new Dictionary<string, (decimal cena, decimal ilosc, string nazwa)>();

                using (SqlConnection conn = new SqlConnection(_connHandel))
                {
                    conn.Open();

                    // Pobierz średnie ceny zakupu z PZ dla głównych produktów
                    string queryPZ = @"
                        SELECT
                            TW.symbol AS SymbolTowaru,
                            TW.nazwa AS NazwaTowaru,
                            SUM(MZ.wartn) AS WartoscNetto,
                            SUM(MZ.ilosc) AS Ilosc
                        FROM [HANDEL].[CDN].[MZ] MZ
                        INNER JOIN [HANDEL].[CDN].[TW] TW ON MZ.tw_id = TW.ID
                        WHERE MZ.seria = 'sPZ'
                            AND MZ.data >= DATEADD(MONTH, -3, GETDATE())
                            AND MZ.ilosc > 0
                            AND TW.symbol LIKE '%TUSZKA%' OR TW.symbol LIKE '%KURCZAK%' OR TW.symbol LIKE '%NOGI%'
                                OR TW.symbol LIKE '%SKRZYD%' OR TW.symbol LIKE '%FILET%' OR TW.symbol LIKE '%UDZIEC%'
                        GROUP BY TW.symbol, TW.nazwa
                        HAVING SUM(MZ.ilosc) > 0";

                    using (SqlCommand cmd = new SqlCommand(queryPZ, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string symbol = reader["SymbolTowaru"].ToString();
                                decimal wartosc = Convert.ToDecimal(reader["WartoscNetto"]);
                                decimal ilosc = Convert.ToDecimal(reader["Ilosc"]);
                                string nazwa = reader["NazwaTowaru"].ToString();
                                decimal sredniaCena = ilosc > 0 ? wartosc / ilosc : 0;
                                cenyZakupu[symbol] = (sredniaCena, ilosc, nazwa);
                            }
                        }
                    }
                }

                // Pobierz ceny sprzedaży dla tego odbiorcy
                var cenySprzedazy = new Dictionary<string, (decimal cena, decimal ilosc, decimal wartosc)>();
                decimal sumaSprzedazyNetto = 0;
                decimal sumaIlosciKg = 0;
                int liczbaFaktur = 0;

                using (SqlConnection conn = new SqlConnection(_connHandel))
                {
                    conn.Open();

                    string querySprzedaz = @"
                        SELECT
                            TW.symbol AS SymbolTowaru,
                            SUM(DP.ilosc) AS Ilosc,
                            SUM(DP.wartn) AS WartoscNetto
                        FROM [HANDEL].[CDN].[DK] DK
                        INNER JOIN [HANDEL].[CDN].[DP] DP ON DK.ID = DP.dk_id
                        INNER JOIN [HANDEL].[CDN].[TW] TW ON DP.tw_id = TW.ID
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.podmiot_id = C.id
                        WHERE C.Shortcut = @NazwaOdbiorcy
                            AND DK.seria IN ('sFV', 'sFKOR')
                            AND DK.data >= DATEADD(MONTH, -3, GETDATE())
                            AND DP.ilosc > 0
                        GROUP BY TW.symbol";

                    using (SqlCommand cmd = new SqlCommand(querySprzedaz, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaOdbiorcy", nazwaOdbiorcy);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string symbol = reader["SymbolTowaru"].ToString();
                                decimal ilosc = Convert.ToDecimal(reader["Ilosc"]);
                                decimal wartosc = Convert.ToDecimal(reader["WartoscNetto"]);
                                decimal cena = ilosc > 0 ? wartosc / ilosc : 0;
                                cenySprzedazy[symbol] = (cena, ilosc, wartosc);
                                sumaSprzedazyNetto += wartosc;
                                sumaIlosciKg += ilosc;
                            }
                        }
                    }

                    // Liczba faktur
                    string queryLiczbaFaktur = @"
                        SELECT COUNT(DISTINCT DK.ID) AS Liczba
                        FROM [HANDEL].[CDN].[DK] DK
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.podmiot_id = C.id
                        WHERE C.Shortcut = @NazwaOdbiorcy
                            AND DK.seria IN ('sFV', 'sFKOR')
                            AND DK.data >= DATEADD(MONTH, -3, GETDATE())";
                    using (SqlCommand cmd = new SqlCommand(queryLiczbaFaktur, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaOdbiorcy", nazwaOdbiorcy);
                        var result = cmd.ExecuteScalar();
                        if (result != DBNull.Value) liczbaFaktur = Convert.ToInt32(result);
                    }
                }

                // Oblicz marże dla każdego produktu
                var marze = new List<(string symbol, string nazwa, decimal cenaZakupu, decimal cenaSprzedazy, decimal marza, decimal marzaProc, decimal ilosc)>();
                decimal sumaMarzy = 0;
                decimal sumaKosztuZakupu = 0;

                foreach (var sprzedaz in cenySprzedazy)
                {
                    string symbol = sprzedaz.Key;
                    decimal cenaSprzedazy = sprzedaz.Value.cena;
                    decimal ilosc = sprzedaz.Value.ilosc;
                    decimal wartosc = sprzedaz.Value.wartosc;

                    decimal cenaZakupu = 0;
                    string nazwa = symbol;

                    // Znajdź cenę zakupu dla tego produktu
                    if (cenyZakupu.ContainsKey(symbol))
                    {
                        cenaZakupu = cenyZakupu[symbol].cena;
                        nazwa = cenyZakupu[symbol].nazwa;
                    }
                    else
                    {
                        // Spróbuj znaleźć podobny produkt
                        var podobny = cenyZakupu.FirstOrDefault(x => symbol.Contains(x.Key) || x.Key.Contains(symbol));
                        if (podobny.Key != null)
                        {
                            cenaZakupu = podobny.Value.cena;
                            nazwa = podobny.Value.nazwa;
                        }
                    }

                    decimal marza = cenaSprzedazy - cenaZakupu;
                    decimal marzaProc = cenaZakupu > 0 ? (marza / cenaZakupu) * 100 : 0;

                    marze.Add((symbol, nazwa, cenaZakupu, cenaSprzedazy, marza, marzaProc, ilosc));
                    sumaMarzy += marza * ilosc;
                    sumaKosztuZakupu += cenaZakupu * ilosc;
                }

                // Oblicz rentowność
                decimal sredniaMarza = sumaIlosciKg > 0 ? sumaMarzy / sumaIlosciKg : 0;
                decimal sredniaCenaSprzedazy = sumaIlosciKg > 0 ? sumaSprzedazyNetto / sumaIlosciKg : 0;
                decimal sredniaCenaZakupu = sumaIlosciKg > 0 ? sumaKosztuZakupu / sumaIlosciKg : 0;
                decimal kosztTransportuNaKg = sumaIlosciKg > 0 && liczbaFaktur > 0 ? (kosztTransportuDostawy * liczbaFaktur) / sumaIlosciKg : 0;
                decimal marzaPoTransporcie = sredniaMarza - kosztTransportuNaKg;
                decimal marzaProcPoTransporcie = sredniaCenaSprzedazy > 0 ? (marzaPoTransporcie / sredniaCenaSprzedazy) * 100 : 0;
                decimal zyskCalkowity = sumaMarzy - (kosztTransportuDostawy * liczbaFaktur);

                // Buduj UI
                int row = 0;

                // Nagłówek
                DodajNaglowekSekcji(gridRentownosc, "📈 Analiza Rentowności Klienta (3 miesiące)", ref row);

                // Panel podsumowania
                var panelPodsumowanie = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FDF4")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(25),
                    Margin = new Thickness(0, 10, 0, 20)
                };

                var stackPodsumowanie = new StackPanel();

                // Główny wskaźnik rentowności
                var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
                headerStack.Children.Add(new TextBlock
                {
                    Text = "RENTOWNOŚĆ KLIENTA: ",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                });

                bool jestRentowny = marzaPoTransporcie > 0.50m; // min 0.50 PLN/kg zysku
                bool jestOK = marzaPoTransporcie > 0;

                headerStack.Children.Add(new TextBlock
                {
                    Text = jestRentowny ? "WYSOKA" : (jestOK ? "ŚREDNIA" : "NISKA"),
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        jestRentowny ? "#10B981" : (jestOK ? "#F59E0B" : "#EF4444"))),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0)
                });
                headerStack.Children.Add(new TextBlock
                {
                    Text = jestRentowny ? "✅" : (jestOK ? "⚠️" : "❌"),
                    FontSize = 22,
                    VerticalAlignment = VerticalAlignment.Center
                });
                stackPodsumowanie.Children.Add(headerStack);

                // Szczegóły
                var gridSzczegoly = new Grid();
                gridSzczegoly.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridSzczegoly.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridSzczegoly.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Kolumna 1: Sprzedaż
                var stack1 = CreateSummaryColumn("💰 SPRZEDAŻ", new[] {
                    ($"Wartość netto:", $"{sumaSprzedazyNetto:N2} PLN"),
                    ($"Ilość:", $"{sumaIlosciKg:N0} kg"),
                    ($"Średnia cena/kg:", $"{sredniaCenaSprzedazy:N2} PLN"),
                    ($"Liczba faktur:", $"{liczbaFaktur}")
                });
                Grid.SetColumn(stack1, 0);
                gridSzczegoly.Children.Add(stack1);

                // Kolumna 2: Zakup
                var stack2 = CreateSummaryColumn("📦 ZAKUP", new[] {
                    ($"Koszt zakupu:", $"{sumaKosztuZakupu:N2} PLN"),
                    ($"Średnia cena/kg:", $"{sredniaCenaZakupu:N2} PLN"),
                    ($"Marża brutto/kg:", $"{sredniaMarza:N2} PLN"),
                    ($"Marża brutto %:", $"{(sredniaCenaZakupu > 0 ? (sredniaMarza/sredniaCenaZakupu)*100 : 0):N1}%")
                });
                Grid.SetColumn(stack2, 1);
                gridSzczegoly.Children.Add(stack2);

                // Kolumna 3: Transport
                var stack3 = CreateSummaryColumn("🚚 TRANSPORT", new[] {
                    ($"Koszt dostawy:", $"{kosztTransportuDostawy:N2} PLN"),
                    ($"Koszt/kg:", $"{kosztTransportuNaKg:N2} PLN"),
                    ($"Razem transport:", $"{(kosztTransportuDostawy * liczbaFaktur):N2} PLN"),
                    ($"Odległość:", $"{odlegloscKm:N0} km")
                });
                Grid.SetColumn(stack3, 2);
                gridSzczegoly.Children.Add(stack3);

                stackPodsumowanie.Children.Add(gridSzczegoly);

                // Separator
                stackPodsumowanie.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Height = 2,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                    Margin = new Thickness(0, 20, 0, 20)
                });

                // Wynik końcowy
                var wynikGrid = new Grid();
                wynikGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                wynikGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var wynikStack1 = new StackPanel();
                wynikStack1.Children.Add(new TextBlock
                {
                    Text = "MARŻA NETTO (po transporcie):",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                });
                wynikStack1.Children.Add(new TextBlock
                {
                    Text = $"{marzaPoTransporcie:N2} PLN/kg ({marzaProcPoTransporcie:N1}%)",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        marzaPoTransporcie > 0.50m ? "#10B981" : (marzaPoTransporcie > 0 ? "#F59E0B" : "#EF4444")))
                });
                Grid.SetColumn(wynikStack1, 0);
                wynikGrid.Children.Add(wynikStack1);

                var wynikStack2 = new StackPanel();
                wynikStack2.Children.Add(new TextBlock
                {
                    Text = "ZYSK CAŁKOWITY (3 miesiące):",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                });
                wynikStack2.Children.Add(new TextBlock
                {
                    Text = $"{zyskCalkowity:N2} PLN",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        zyskCalkowity > 0 ? "#10B981" : "#EF4444"))
                });
                Grid.SetColumn(wynikStack2, 1);
                wynikGrid.Children.Add(wynikStack2);

                stackPodsumowanie.Children.Add(wynikGrid);

                panelPodsumowanie.Child = stackPodsumowanie;

                gridRentownosc.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(panelPodsumowanie, row);
                Grid.SetColumnSpan(panelPodsumowanie, 4);
                gridRentownosc.Children.Add(panelPodsumowanie);
                row++;

                // Sekcja szczegółów produktów
                if (marze.Count > 0)
                {
                    DodajNaglowekSekcji(gridRentownosc, "📊 Szczegóły Marży wg Produktów", ref row);

                    // DataGrid z produktami
                    var dgProdukty = new DataGrid
                    {
                        AutoGenerateColumns = false,
                        CanUserAddRows = false,
                        CanUserDeleteRows = false,
                        IsReadOnly = true,
                        HeadersVisibility = DataGridHeadersVisibility.Column,
                        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                        Background = new SolidColorBrush(Colors.White),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                        BorderThickness = new Thickness(1),
                        RowHeight = 35,
                        MaxHeight = 300
                    };

                    dgProdukty.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Produkt",
                        Binding = new System.Windows.Data.Binding("Nazwa"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                    dgProdukty.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Ilość (kg)",
                        Binding = new System.Windows.Data.Binding("Ilosc") { StringFormat = "N0" },
                        Width = 80
                    });
                    dgProdukty.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Cena zakupu",
                        Binding = new System.Windows.Data.Binding("CenaZakupu") { StringFormat = "N2" },
                        Width = 100
                    });
                    dgProdukty.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Cena sprzedaży",
                        Binding = new System.Windows.Data.Binding("CenaSprzedazy") { StringFormat = "N2" },
                        Width = 110
                    });
                    dgProdukty.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Marża/kg",
                        Binding = new System.Windows.Data.Binding("Marza") { StringFormat = "N2" },
                        Width = 80
                    });
                    dgProdukty.Columns.Add(new DataGridTextColumn
                    {
                        Header = "Marża %",
                        Binding = new System.Windows.Data.Binding("MarzaProc") { StringFormat = "N1" },
                        Width = 70
                    });

                    var produktyData = marze.Select(m => new
                    {
                        Nazwa = m.nazwa,
                        Ilosc = m.ilosc,
                        CenaZakupu = m.cenaZakupu,
                        CenaSprzedazy = m.cenaSprzedazy,
                        Marza = m.marza,
                        MarzaProc = m.marzaProc
                    }).OrderByDescending(x => x.Ilosc).ToList();

                    dgProdukty.ItemsSource = produktyData;

                    gridRentownosc.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    Grid.SetRow(dgProdukty, row);
                    Grid.SetColumnSpan(dgProdukty, 4);
                    gridRentownosc.Children.Add(dgProdukty);
                    row++;
                }

                // Sekcja rekomendacji
                DodajNaglowekSekcji(gridRentownosc, "💡 Rekomendacje", ref row);

                var panelRekomendacje = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var stackRekomendacje = new StackPanel();

                if (marzaPoTransporcie < 0)
                {
                    stackRekomendacje.Children.Add(CreateRecommendation("❌", "Klient jest NIERENTOWNY - koszt transportu przewyższa marżę na produktach"));
                    stackRekomendacje.Children.Add(CreateRecommendation("💰", $"Minimalna cena sprzedaży powinna wynosić: {(sredniaCenaZakupu + kosztTransportuNaKg + 0.50m):N2} PLN/kg"));
                }
                else if (marzaPoTransporcie < 0.50m)
                {
                    stackRekomendacje.Children.Add(CreateRecommendation("⚠️", "Rentowność jest NISKA - rozważ podwyżkę cen lub zwiększenie wolumenu"));
                    stackRekomendacje.Children.Add(CreateRecommendation("📦", $"Przy zwiększeniu zamówienia o 50% marża wzrośnie do: {(marzaPoTransporcie + kosztTransportuNaKg * 0.33m):N2} PLN/kg"));
                }
                else
                {
                    stackRekomendacje.Children.Add(CreateRecommendation("✅", "Klient jest RENTOWNY - utrzymuj dotychczasowe warunki współpracy"));
                }

                if (kosztTransportuNaKg > 0.30m)
                {
                    stackRekomendacje.Children.Add(CreateRecommendation("🚚", $"Wysoki udział transportu ({(kosztTransportuNaKg/sredniaCenaSprzedazy*100):N1}%) - rozważ łączenie dostaw lub min. wartość zamówienia"));
                }

                panelRekomendacje.Child = stackRekomendacje;

                gridRentownosc.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(panelRekomendacje, row);
                Grid.SetColumnSpan(panelRekomendacje, 4);
                gridRentownosc.Children.Add(panelRekomendacje);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private StackPanel CreateSummaryColumn(string header, (string label, string value)[] items)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            stack.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (var item in items)
            {
                var itemStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                itemStack.Children.Add(new TextBlock
                {
                    Text = item.label,
                    Width = 120,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
                });
                itemStack.Children.Add(new TextBlock
                {
                    Text = item.value,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
                });
                stack.Children.Add(itemStack);
            }

            return stack;
        }

        private TextBlock CreateRecommendation(string icon, string text)
        {
            return new TextBlock
            {
                Text = $"{icon} {text}",
                FontSize = 13,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
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
