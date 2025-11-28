using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.Reklamacje
{
    public partial class FormPanelReklamacjiWindow : Window
    {
        private string connectionString;
        private string userId;
        private ObservableCollection<ReklamacjaItem> reklamacje = new ObservableCollection<ReklamacjaItem>();

        public FormPanelReklamacjiWindow(string connString, string user)
        {
            InitializeComponent();

            connectionString = connString;
            userId = user;

            // Ustaw domyslne daty
            dpDataOd.SelectedDate = DateTime.Now.AddMonths(-1);
            dpDataDo.SelectedDate = DateTime.Now;

            dgReklamacje.ItemsSource = reklamacje;

            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajReklamacje();
            WczytajStatystyki();
        }

        private void WczytajReklamacje()
        {
            try
            {
                reklamacje.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT
                            r.Id,
                            r.DataZgloszenia,
                            r.NumerDokumentu,
                            r.NazwaKontrahenta,
                            LEFT(ISNULL(r.Opis, ''), 100) + CASE WHEN LEN(ISNULL(r.Opis, '')) > 100 THEN '...' ELSE '' END AS Opis,
                            ISNULL(r.SumaKg, 0) AS SumaKg,
                            ISNULL(r.Status, 'Nowa') AS Status,
                            ISNULL(o.Name, r.UserID) AS Zglaszajacy,
                            ISNULL(o2.Name, r.OsobaRozpatrujaca) AS OsobaRozpatrujaca
                        FROM [dbo].[Reklamacje] r
                        LEFT JOIN [dbo].[operators] o ON r.UserID = o.ID
                        LEFT JOIN [dbo].[operators] o2 ON r.OsobaRozpatrujaca = o2.ID
                        WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo";

                    string statusFilter = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Wszystkie")
                    {
                        query += " AND r.Status = @Status";
                    }

                    string szukaj = txtSzukaj?.Text?.Trim();
                    if (!string.IsNullOrEmpty(szukaj))
                    {
                        query += " AND (r.NumerDokumentu LIKE @Szukaj OR r.NazwaKontrahenta LIKE @Szukaj OR r.Opis LIKE @Szukaj)";
                    }

                    query += " ORDER BY r.DataZgloszenia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate ?? DateTime.Now.AddMonths(-1));
                        cmd.Parameters.AddWithValue("@DataDo", (dpDataDo.SelectedDate ?? DateTime.Now).AddDays(1).AddSeconds(-1));

                        if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Wszystkie")
                        {
                            cmd.Parameters.AddWithValue("@Status", statusFilter);
                        }

                        if (!string.IsNullOrEmpty(szukaj))
                        {
                            cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");
                        }

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reklamacje.Add(new ReklamacjaItem
                                {
                                    Id = reader.GetInt32(0),
                                    DataZgloszenia = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                                    NumerDokumentu = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NazwaKontrahenta = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Opis = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    SumaKg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                    Status = reader.IsDBNull(6) ? "Nowa" : reader.GetString(6),
                                    Zglaszajacy = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    OsobaRozpatrujaca = reader.IsDBNull(8) ? "" : reader.GetString(8)
                                });
                            }
                        }
                    }
                }

                txtLiczbaReklamacji.Text = reklamacje.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas wczytywania reklamacji:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajStatystyki()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT
                            ISNULL(Status, 'Nowa') AS Status,
                            COUNT(*) AS Liczba
                        FROM [dbo].[Reklamacje]
                        WHERE DataZgloszenia >= DATEADD(MONTH, -1, GETDATE())
                        GROUP BY Status";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int nowe = 0, wTrakcie = 0, zaakceptowane = 0, odrzucone = 0, zamkniete = 0;

                            while (reader.Read())
                            {
                                string status = reader.GetString(0);
                                int liczba = reader.GetInt32(1);

                                switch (status)
                                {
                                    case "Nowa": nowe = liczba; break;
                                    case "W trakcie": wTrakcie = liczba; break;
                                    case "Zaakceptowana": zaakceptowane = liczba; break;
                                    case "Odrzucona": odrzucone = liczba; break;
                                    case "Zamknieta": zamkniete = liczba; break;
                                }
                            }

                            txtStatNowe.Text = nowe.ToString();
                            txtStatWTrakcie.Text = wTrakcie.ToString();
                            txtStatZaakceptowane.Text = zaakceptowane.ToString();
                            txtStatOdrzucone.Text = odrzucone.ToString();
                            txtStatZamkniete.Text = zamkniete.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj bledy statystyk
            }
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajReklamacje();
            WczytajStatystyki();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajReklamacje();
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajReklamacje();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajReklamacje();
        }

        private void DgReklamacje_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = dgReklamacje.SelectedItem != null;
            btnSzczegoly.IsEnabled = selected;
            btnZmienStatus.IsEnabled = selected;
            btnZaakceptuj.IsEnabled = selected;
            btnOdrzuc.IsEnabled = selected;

            if (selected && dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                txtZaznaczenie.Text = $"Wybrano: #{item.Id} - {item.NazwaKontrahenta} ({item.Status})";
            }
            else
            {
                txtZaznaczenie.Text = "Wybierz reklamacje z listy";
            }
        }

        private void DgReklamacje_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BtnSzczegoly_Click(sender, e);
        }

        private void BtnSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                try
                {
                    var formSzczegoly = new FormSzczegolyReklamacji(connectionString, item.Id, userId);
                    if (formSzczegoly.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        WczytajReklamacje();
                        WczytajStatystyki();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad podczas otwierania szczegolow:\n{ex.Message}",
                        "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnZmienStatus_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                var dialog = new Window
                {
                    Title = "Zmiana statusu reklamacji",
                    Width = 400,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = $"Zmiana statusu reklamacji #{item.Id}",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                var labelStatus = new TextBlock { Text = "Wybierz nowy status:", Margin = new Thickness(0, 0, 0, 5) };
                Grid.SetRow(labelStatus, 1);
                grid.Children.Add(labelStatus);

                var combo = new ComboBox { Margin = new Thickness(0, 0, 0, 20) };
                combo.Items.Add("Nowa");
                combo.Items.Add("W trakcie");
                combo.Items.Add("Zaakceptowana");
                combo.Items.Add("Odrzucona");
                combo.Items.Add("Zamknieta");
                combo.SelectedItem = item.Status;
                Grid.SetRow(combo, 2);
                grid.Children.Add(combo);

                var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

                var btnOK = new Button
                {
                    Content = "Zapisz",
                    Width = 100,
                    Height = 35,
                    Background = System.Windows.Media.Brushes.Green,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                btnOK.Click += (s, args) =>
                {
                    ZmienStatus(item.Id, combo.SelectedItem?.ToString());
                    dialog.DialogResult = true;
                    dialog.Close();
                };
                buttonsPanel.Children.Add(btnOK);

                var btnCancel = new Button { Content = "Anuluj", Width = 100, Height = 35 };
                btnCancel.Click += (s, args) => dialog.Close();
                buttonsPanel.Children.Add(btnCancel);

                Grid.SetRow(buttonsPanel, 3);
                grid.Children.Add(buttonsPanel);

                dialog.Content = grid;

                if (dialog.ShowDialog() == true)
                {
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        private void BtnZaakceptuj_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                if (MessageBox.Show($"Czy na pewno chcesz zaakceptowac reklamacje #{item.Id}?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ZmienStatus(item.Id, "Zaakceptowana");
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        private void BtnOdrzuc_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                if (MessageBox.Show($"Czy na pewno chcesz odrzucic reklamacje #{item.Id}?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ZmienStatus(item.Id, "Odrzucona");
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        private void ZmienStatus(int idReklamacji, string nowyStatus)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        UPDATE [dbo].[Reklamacje]
                        SET Status = @Status,
                            OsobaRozpatrujaca = @Osoba,
                            DataModyfikacji = GETDATE()
                        WHERE Id = @Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", nowyStatus);
                        cmd.Parameters.AddWithValue("@Osoba", userId);
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.ExecuteNonQuery();
                    }

                    // Dodaj wpis do historii
                    string queryHistoria = @"
                        INSERT INTO [dbo].[ReklamacjeHistoria]
                        (IdReklamacji, UserID, StatusNowy, Komentarz, TypAkcji)
                        VALUES
                        (@IdReklamacji, @UserID, @StatusNowy, @Komentarz, 'ZmianaStatusu')";

                    using (SqlCommand cmd = new SqlCommand(queryHistoria, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        cmd.Parameters.AddWithValue("@StatusNowy", nowyStatus);
                        cmd.Parameters.AddWithValue("@Komentarz", $"Zmiana statusu na: {nowyStatus}");
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show($"Status reklamacji #{idReklamacji} zostal zmieniony na: {nowyStatus}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas zmiany statusu:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja eksportu do Excel zostanie dodana wkrotce.",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Klasa pomocnicza
    public class ReklamacjaItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public DateTime DataZgloszenia { get; set; }
        public string NumerDokumentu { get; set; }
        public string NazwaKontrahenta { get; set; }
        public string Opis { get; set; }
        public decimal SumaKg { get; set; }
        public string Status { get; set; }
        public string Zglaszajacy { get; set; }
        public string OsobaRozpatrujaca { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
