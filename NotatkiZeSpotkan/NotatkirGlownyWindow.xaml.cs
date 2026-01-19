using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Kalendarz1.NotatkiZeSpotkan
{
    public partial class NotatkirGlownyWindow : Window
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public string UserID { get; set; } = string.Empty;

        // Konstruktor bezparametrowy (wymagany przez XAML)
        public NotatkirGlownyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += Window_Loaded;
        }

        // NOWY konstruktor z parametrem
        public NotatkirGlownyWindow(string userID) : this()
        {
            UserID = userID;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Walidacja UserID
            if (string.IsNullOrEmpty(UserID))
            {
                MessageBox.Show("Błąd: Nie ustawiono ID użytkownika. Okno zostanie zamknięte.",
                    "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            LoadNotatki();
        }


        private void LoadNotatki()
        {
            try
            {
                var notatki = new List<NotatkaListItemDTO>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var whereClauses = new List<string>();
                    var parameters = new List<SqlParameter>();

                    if (UserID != "11111")
                    {
                        whereClauses.Add(@"(n.TworcaID = @UserID OR EXISTS (
                    SELECT 1 FROM NotatkiWidocznosc w 
                    WHERE w.NotatkaID = n.NotatkaID AND w.OperatorID = @UserID
                ))");
                        parameters.Add(new SqlParameter("@UserID", UserID));
                    }

                    if (cmbFiltrTyp?.SelectedIndex > 0)
                    {
                        var item = cmbFiltrTyp.SelectedItem as System.Windows.Controls.ComboBoxItem;
                        var typText = item?.Content?.ToString();
                        if (!string.IsNullOrEmpty(typText))
                        {
                            whereClauses.Add("n.TypSpotkania = @Typ");
                            parameters.Add(new SqlParameter("@Typ", typText));
                        }
                    }

                    if (dpFiltrOd?.SelectedDate.HasValue == true)
                    {
                        whereClauses.Add("n.DataSpotkania >= @Od");
                        parameters.Add(new SqlParameter("@Od", dpFiltrOd.SelectedDate.Value));
                    }

                    if (dpFiltrDo?.SelectedDate.HasValue == true)
                    {
                        whereClauses.Add("n.DataSpotkania <= @Do");
                        parameters.Add(new SqlParameter("@Do", dpFiltrDo.SelectedDate.Value));
                    }

                    string whereClause = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                    string sql = $@"
                SELECT 
                    n.NotatkaID,
                    ISNULL(n.TypSpotkania, '') AS TypSpotkania,
                    n.DataSpotkania,
                    ISNULL(n.Temat, '') AS Temat,
                    COALESCE(o.Name, n.TworcaID, 'Nieznany') AS TworcaNazwa,
                    n.KontrahentNazwa,
                    n.DataUtworzenia
                FROM NotatkiZeSpotkan n
                LEFT JOIN operators o ON o.ID = n.TworcaID
                {whereClause}
                ORDER BY n.DataSpotkania DESC, n.DataUtworzenia DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (parameters.Any())
                        {
                            cmd.Parameters.AddRange(parameters.ToArray());
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notatki.Add(new NotatkaListItemDTO
                                {
                                    NotatkaID = reader.GetInt64(0),
                                    TypSpotkania = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    DataSpotkania = reader.IsDBNull(2) ? DateTime.Today : reader.GetDateTime(2),
                                    Temat = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    TworcaNazwa = reader.IsDBNull(4) ? "Nieznany" : reader.GetString(4),
                                    KontrahentNazwa = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    DataUtworzenia = reader.IsDBNull(6) ? DateTime.Now : reader.GetDateTime(6)
                                });
                            }
                        }
                    }
                }

                if (dgNotatki != null)
                {
                    dgNotatki.ItemsSource = notatki;
                }

                if (txtStatus != null)
                {
                    txtStatus.Text = $"Znaleziono: {notatki.Count} notatek";
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"Błąd SQL:\n\n{sqlEx.Message}\n\nNumer: {sqlEx.Number}\n\nProcedura: {sqlEx.Procedure}",
                    "Błąd bazy danych", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania:\n\n{ex.Message}\n\nŚlad:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Filtr_Changed(object sender, EventArgs e)
        {
            LoadNotatki();
        }

        private void BtnNowa_Click(object sender, RoutedEventArgs e)
        {
            var wybor = new WyborTypuSpotkania();
            if (wybor.ShowDialog() == true)
            {
                var edytor = new EdytorNotatki(_connectionString, UserID, wybor.WybranyTyp);
                if (edytor.ShowDialog() == true)
                {
                    LoadNotatki();
                }
            }
        }

        private void BtnPodglad_Click(object sender, RoutedEventArgs e)
        {
            if (dgNotatki.SelectedItem is NotatkaListItemDTO notatka)
            {
                var podglad = new PodgladNotatki(_connectionString, UserID, notatka.NotatkaID);
                podglad.ShowDialog();
            }
            else
            {
                MessageBox.Show("Wybierz notatkę z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (dgNotatki.SelectedItem is NotatkaListItemDTO notatka)
            {
                // Sprawdź uprawnienia
                if (!MaUprawnienieDoEdycji(notatka.NotatkaID))
                {
                    MessageBox.Show(
                        "Nie masz uprawnień do edycji tej notatki.\n\n" +
                        "Edytować mogą:\n" +
                        "• Twórca notatki\n" +
                        "• Osoby wybrane przez twórcę (lista widoczności)\n" +
                        "• Administrator (ID: 11111)",
                        "Brak uprawnień",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var edytor = new EdytorNotatki(_connectionString, UserID, notatkaID: notatka.NotatkaID);
                if (edytor.ShowDialog() == true)
                {
                    LoadNotatki();
                }
            }
            else
            {
                MessageBox.Show("Wybierz notatkę z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (dgNotatki.SelectedItem is NotatkaListItemDTO notatka)
            {
                // Sprawdź uprawnienia
                if (!MaUprawnienieDoEdycji(notatka.NotatkaID))
                {
                    MessageBox.Show(
                        "Nie masz uprawnień do usunięcia tej notatki.\n\n" +
                        "Usuwać mogą:\n" +
                        "• Twórca notatki\n" +
                        "• Osoby wybrane przez twórcę (lista widoczności)\n" +
                        "• Administrator (ID: 11111)",
                        "Brak uprawnień",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Czy na pewno chcesz usunąć notatkę:\n\n{notatka.Temat}\n\nTej operacji nie można cofnąć.",
                    "Potwierdzenie usunięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SqlConnection(_connectionString))
                        {
                            conn.Open();
                            using (var cmd = new SqlCommand("DELETE FROM NotatkiZeSpotkan WHERE NotatkaID = @ID", conn))
                            {
                                cmd.Parameters.AddWithValue("@ID", notatka.NotatkaID);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show("Notatka została usunięta.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadNotatki();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd usuwania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Wybierz notatkę z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool MaUprawnienieDoEdycji(long notatkaID)
        {
            // Admin ma zawsze dostęp
            if (UserID == "11111") return true;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Sprawdź czy jest twórcą LUB ma widoczność
                    string sql = @"
                SELECT COUNT(*) 
                FROM NotatkiZeSpotkan n
                WHERE n.NotatkaID = @ID 
                  AND (n.TworcaID = @UserID 
                       OR EXISTS (SELECT 1 FROM NotatkiWidocznosc w 
                                  WHERE w.NotatkaID = n.NotatkaID 
                                    AND w.OperatorID = @UserID))";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", notatkaID);
                        cmd.Parameters.AddWithValue("@UserID", UserID);
                        return (int)cmd.ExecuteScalar() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        private void DgNotatki_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BtnPodglad_Click(sender, e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}