using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class KartotekaOdbiorcowWindow : Window
    {
        private readonly string _connLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        
        public string UserID { get; set; }
        private int? wybranyOdbiorcaID = null;

        public KartotekaOdbiorcowWindow()
        {
            InitializeComponent();
            UserID = App.UserID;
            txtUserID.Text = UserID;
            
            Loaded += KartotekaOdbiorcowWindow_Loaded;
        }

        private void KartotekaOdbiorcowWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajOdbiorcow();
            InicjalizujZakladki();
        }

        #region Wczytywanie danych

        private void WczytajOdbiorcow()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            OdbiorcaID,
                            NazwaSkrot,
                            PelnaNazwa,
                            Miejscowosc,
                            KategoriaOdbiorcy,
                            TypOdbiorcy,
                            StatusAktywny,
                            AktualnaSaldo,
                            DataOstatniegoZakupu
                        FROM Odbiorcy
                        WHERE 1=1";

                    // Filtry
                    if (cmbKategoria.SelectedIndex > 0)
                    {
                        var kategoria = (cmbKategoria.SelectedItem as ComboBoxItem)?.Content.ToString();
                        query += $" AND KategoriaOdbiorcy = '{kategoria}'";
                    }

                    if (cmbTypOdbiorcy.SelectedIndex > 0)
                    {
                        var typ = (cmbTypOdbiorcy.SelectedItem as ComboBoxItem)?.Content.ToString();
                        query += $" AND TypOdbiorcy = '{typ}'";
                    }

                    if (cmbStatus.SelectedIndex == 0) // Aktywni
                        query += " AND StatusAktywny = 1";
                    else if (cmbStatus.SelectedIndex == 1) // Nieaktywni
                        query += " AND StatusAktywny = 0";

                    if (!string.IsNullOrWhiteSpace(txtSzukaj.Text))
                    {
                        query += $" AND (NazwaSkrot LIKE '%{txtSzukaj.Text}%' OR PelnaNazwa LIKE '%{txtSzukaj.Text}%')";
                    }

                    query += " ORDER BY NazwaSkrot";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        DataTable dt = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        dgOdbiorcy.ItemsSource = dt.DefaultView;
                        txtLiczbaOdbiorcow.Text = dt.Rows.Count.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania odbiorców: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgOdbiorcy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgOdbiorcy.SelectedItem is DataRowView row)
            {
                wybranyOdbiorcaID = Convert.ToInt32(row["OdbiorcaID"]);
                WczytajSzczegolyOdbiorcy();
            }
        }

        private void WczytajSzczegolyOdbiorcy()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            // Wczytaj dane w zależności od aktywnej zakładki
            if (tabControl.SelectedIndex == 0) // Dane Podstawowe
                WczytajDanePodstawowe();
            else if (tabControl.SelectedIndex == 1) // Kontakty
                WczytajKontakty();
            else if (tabControl.SelectedIndex == 2) // Dane Finansowe
                WczytajDaneFinansowe();
            else if (tabControl.SelectedIndex == 3) // Transport
                WczytajDaneTransportu();
            else if (tabControl.SelectedIndex == 4) // Notatki
                WczytajNotatki();
            else if (tabControl.SelectedIndex == 5) // Lokalizacja
                WczytajLokalizacje();
            else if (tabControl.SelectedIndex == 6) // Statystyki
                WczytajStatystyki();
            else if (tabControl.SelectedIndex == 7) // Analiza Kosztów
                WczytajAnalizaKosztow();
        }

        #endregion

        #region Obsługa zdarzeń

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            WczytajOdbiorcow();
        }

        private void CmbFiltr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajOdbiorcow();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajSzczegolyOdbiorcy();
        }

        private void BtnNowyOdbiorca_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DodajOdbiorcaDialog(UserID, _connLibraNet, _connHandel);
            dialog.OdbiorcaDodany += (s, ev) => WczytajOdbiorcow();
            dialog.Show();
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajOdbiorcow();
            if (wybranyOdbiorcaID.HasValue)
                WczytajSzczegolyOdbiorcy();
        }

        #endregion

        #region Dane Podstawowe - część będzie w kolejnym pliku

       
        #endregion

        #region Kontakty

        private void WczytajKontakty()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            KontaktID,
                            TypKontaktu,
                            ISNULL(Imie, '') + ' ' + ISNULL(Nazwisko, '') AS ImieNazwisko,
                            Stanowisko,
                            ISNULL(Telefon, TelefonKomorkowy) AS Telefon,
                            Email,
                            JestGlownyKontakt
                        FROM OdbiorcyKontakty
                        WHERE OdbiorcaID = @OdbiorcaID
                        ORDER BY JestGlownyKontakt DESC, TypKontaktu";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        
                        DataTable dt = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        dgKontakty.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania kontaktów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDodajKontakt_Click(object sender, RoutedEventArgs e)
        {
            if (!wybranyOdbiorcaID.HasValue)
            {
                MessageBox.Show("Najpierw wybierz odbiorcy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new DodajKontaktDialog(wybranyOdbiorcaID.Value, UserID, _connLibraNet);
            dialog.KontaktZapisany += (s, ev) => WczytajKontakty();
            dialog.Show();
        }

        private void BtnEdytujKontakt_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                int kontaktID = Convert.ToInt32(row["KontaktID"]);
                var dialog = new DodajKontaktDialog(wybranyOdbiorcaID.Value, UserID, _connLibraNet, kontaktID);
                dialog.KontaktZapisany += (s, ev) => WczytajKontakty();
                dialog.Show();
            }
            else
            {
                MessageBox.Show("Wybierz kontakt do edycji!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnUsunKontakt_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                if (MessageBox.Show("Czy na pewno chcesz usunąć ten kontakt?", "Potwierdzenie", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        int kontaktID = Convert.ToInt32(row["KontaktID"]);
                        using (SqlConnection conn = new SqlConnection(_connLibraNet))
                        {
                            conn.Open();
                            string query = "DELETE FROM OdbiorcyKontakty WHERE KontaktID = @KontaktID";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@KontaktID", kontaktID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        WczytajKontakty();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas usuwania kontaktu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region Dane Finansowe



        private void BtnSzczegolyPlatnosci_Click(object sender, RoutedEventArgs e)
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            try
            {
                string nazwaSkrot = "";
                string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = "SELECT NazwaSkrot FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        nazwaSkrot = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                if (!string.IsNullOrEmpty(nazwaSkrot))
                {
                    // SzczegolyPlatnosciWindow wymaga dwóch parametrów: kontrahent i connectionString
                    var window = new SzczegolyPlatnosciWindow(nazwaSkrot, connectionString);
                    window.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Nie znaleziono nazwy odbiorcy!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Transport


        #endregion

        #region Notatki CRM

        private void WczytajNotatki()
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            NotatkaID,
                            Tresc,
                            KtoStworzyl,
                            DataUtworzenia
                        FROM Notatki
                        WHERE IndeksID = @OdbiorcaID AND TypID = 2
                        ORDER BY DataUtworzenia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        
                        DataTable dt = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        dgNotatki.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania notatek: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDodajNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (!wybranyOdbiorcaID.HasValue)
            {
                MessageBox.Show("Najpierw wybierz odbiorcy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new DodajNotatkeDialog();
            dialog.NotatkaZapisana += (s, ev) =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(_connLibraNet))
                    {
                        conn.Open();
                        string query = @"
                            INSERT INTO Notatki (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia)
                            VALUES (@IndeksID, 2, @Tresc, @UserID, GETDATE())";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@IndeksID", wybranyOdbiorcaID.Value);
                            cmd.Parameters.AddWithValue("@Tresc", dialog.TrescNotatki);
                            cmd.Parameters.AddWithValue("@UserID", int.Parse(UserID));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    WczytajNotatki();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas dodawania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            dialog.Show();
        }

        #endregion

        #region Lokalizacja

        

        private void BtnUstawLokalizacje_Click(object sender, RoutedEventArgs e)
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            var dialog = new UstawLokalizacjeDialog(wybranyOdbiorcaID.Value, _connLibraNet);
            dialog.LokalizacjaZapisana += (s, ev) => WczytajLokalizacje();
            dialog.Show();
        }

        #endregion

        #region Statystyki

        

        #endregion

        #region Synchronizacja z Handel

        private void BtnSynchronizuj_Click(object sender, RoutedEventArgs e)
        {
            if (!wybranyOdbiorcaID.HasValue) return;

            try
            {
                string nazwaSkrot = "";
                using (SqlConnection conn = new SqlConnection(_connLibraNet))
                {
                    conn.Open();
                    string query = "SELECT NazwaSkrot FROM Odbiorcy WHERE OdbiorcaID = @OdbiorcaID";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                        nazwaSkrot = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                if (string.IsNullOrEmpty(nazwaSkrot))
                {
                    MessageBox.Show("Nie można znaleźć nazwy odbiorcy!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Pobierz dane z systemu Handel
                using (SqlConnection conn = new SqlConnection(_connHandel))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            C.id AS IdOdbiorcy,
                            C.Name AS PelnaNazwa,
                            C.Shortcut AS Nazwa,
                            ISNULL(C.NIP, '') AS NIP,
                            ISNULL(POA.Street, '') AS Ulica,
                            ISNULL(POA.PostCode, '') AS KodPocztowy,
                            ISNULL(POA.Place, '') AS Miejscowosc
                        FROM [HANDEL].[SSCommon].[STContractors] C
                        LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] POA 
                            ON POA.ContactGuid = C.ContactGuid 
                            AND POA.AddressName = N'adres domyślny'
                        WHERE C.Shortcut = @NazwaKontrahenta";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaSkrot);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Aktualizuj dane w LibraNet
                                using (SqlConnection connLib = new SqlConnection(_connLibraNet))
                                {
                                    connLib.Open();
                                    string updateQuery = @"
                                        UPDATE Odbiorcy SET
                                            IdOdbiorcy = @IdOdbiorcy,
                                            PelnaNazwa = @PelnaNazwa,
                                            NIP = @NIP,
                                            Ulica = @Ulica,
                                            KodPocztowy = @KodPocztowy,
                                            Miejscowosc = @Miejscowosc,
                                            DataModyfikacji = GETDATE(),
                                            KtoZmodyfikowal = @UserID
                                        WHERE OdbiorcaID = @OdbiorcaID";

                                    using (SqlCommand cmdUpdate = new SqlCommand(updateQuery, connLib))
                                    {
                                        cmdUpdate.Parameters.AddWithValue("@OdbiorcaID", wybranyOdbiorcaID.Value);
                                        cmdUpdate.Parameters.AddWithValue("@IdOdbiorcy", reader["IdOdbiorcy"]);
                                        cmdUpdate.Parameters.AddWithValue("@PelnaNazwa", reader["PelnaNazwa"].ToString());
                                        cmdUpdate.Parameters.AddWithValue("@NIP", reader["NIP"].ToString());
                                        cmdUpdate.Parameters.AddWithValue("@Ulica", reader["Ulica"].ToString());
                                        cmdUpdate.Parameters.AddWithValue("@KodPocztowy", reader["KodPocztowy"].ToString());
                                        cmdUpdate.Parameters.AddWithValue("@Miejscowosc", reader["Miejscowosc"].ToString());
                                        cmdUpdate.Parameters.AddWithValue("@UserID", int.Parse(UserID));
                                        
                                        cmdUpdate.ExecuteNonQuery();
                                    }
                                }

                                MessageBox.Show("Dane zostały pomyślnie zsynchronizowane z systemem Handel!", 
                                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                                WczytajSzczegolyOdbiorcy();
                            }
                            else
                            {
                                MessageBox.Show("Nie znaleziono odbiorcy w systemie Handel!", 
                                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas synchronizacji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
