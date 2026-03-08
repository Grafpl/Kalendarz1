using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.Reklamacje
{
    public partial class FormPanelReklamacjiWindow : Window
    {
        private string connectionString;
        private string userId;
        private ObservableCollection<ReklamacjaItem> reklamacje = new ObservableCollection<ReklamacjaItem>();
        private bool isInitialized = false;
        private bool isJakosc = false;
        private DispatcherTimer searchDebounceTimer;

        private const string HandelConnString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Dozwolone przejscia statusow - delegacja do centralnej definicji
        private static Dictionary<string, List<string>> dozwolonePrzejscia => FormRozpatrzenieWindow.dozwolonePrzejscia;

        public FormPanelReklamacjiWindow(string connString, string user)
        {
            connectionString = connString;
            userId = user;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Sprawdz uprawnienia
            SprawdzUprawnieniaJakosc();

            // Ustaw domyslne daty
            dpDataOd.SelectedDate = DateTime.Now.AddMonths(-1);
            dpDataDo.SelectedDate = DateTime.Now;

            dgReklamacje.ItemsSource = reklamacje;

            // Debounce timer dla wyszukiwania (300ms)
            searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            searchDebounceTimer.Tick += (s, args) =>
            {
                searchDebounceTimer.Stop();
                WczytajReklamacje();
            };

            // Ukryj/pokaz kontrolki na podstawie uprawnien
            UstawWidocznoscKontrolek();

            isInitialized = true;
            Loaded += Window_Loaded;
        }

        private void SprawdzUprawnieniaJakosc()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Access FROM operators WHERE ID = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            string accessString = result.ToString();
                            // Pozycja 33 = ReklamacjeJakosc (zaktualizowana pozycja po merge)
                            if (accessString.Length > 33 && accessString[33] == '1')
                            {
                                isJakosc = true;
                            }
                        }
                    }
                }
            }
            catch
            {
                isJakosc = false;
            }
        }

        private void UstawWidocznoscKontrolek()
        {
            // Przyciski zmiany statusu - tylko dla jakosci
            if (btnZmienStatus != null) btnZmienStatus.Visibility = isJakosc ? Visibility.Visible : Visibility.Collapsed;
            if (btnZaakceptuj != null) btnZaakceptuj.Visibility = isJakosc ? Visibility.Visible : Visibility.Collapsed;
            if (btnOdrzuc != null) btnOdrzuc.Visibility = isJakosc ? Visibility.Visible : Visibility.Collapsed;

            // Statystyki - tylko dla jakosci
            if (btnStatystyki != null) btnStatystyki.Visibility = isJakosc ? Visibility.Visible : Visibility.Collapsed;

            // Usuwanie + debug - tylko admin
            if (btnUsun != null) btnUsun.Visibility = userId == "11111" ? Visibility.Visible : Visibility.Collapsed;
            if (btnDebugSync != null) btnDebugSync.Visibility = userId == "11111" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try { SyncFakturyKorygujace(); } catch { }
            WczytajReklamacje();
            WczytajStatystyki();
        }

        private void WczytajReklamacje()
        {
            if (!isInitialized || string.IsNullOrEmpty(connectionString)) return;

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
                            ISNULL(o2.Name, r.OsobaRozpatrujaca) AS OsobaRozpatrujaca,
                            ISNULL(r.TypReklamacji, 'Inne') AS TypReklamacji,
                            ISNULL(r.Priorytet, 'Normalny') AS Priorytet,
                            r.UserID AS ZglaszajacyId,
                            ISNULL(r.OsobaRozpatrujaca, '') AS RozpatrujacyId
                        FROM [dbo].[Reklamacje] r
                        LEFT JOIN [dbo].[operators] o ON r.UserID = o.ID
                        LEFT JOIN [dbo].[operators] o2 ON r.OsobaRozpatrujaca = o2.ID
                        WHERE 1=1";

                    string statusFilter = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Wszystkie")
                    {
                        query += " AND r.Status = @Status";
                    }

                    string typFilter = (cmbTyp?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(typFilter) && typFilter != "Wszystkie")
                    {
                        query += " AND r.TypReklamacji = @Typ";
                    }

                    string priorytetFilter = (cmbPriorytet?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(priorytetFilter) && priorytetFilter != "Wszystkie")
                    {
                        query += " AND r.Priorytet = @Priorytet";
                    }

                    string szukaj = txtSzukaj?.Text?.Trim();
                    if (!string.IsNullOrEmpty(szukaj))
                    {
                        query += " AND (r.NumerDokumentu LIKE @Szukaj OR r.NazwaKontrahenta LIKE @Szukaj OR r.Opis LIKE @Szukaj)";
                    }

                    query += " ORDER BY r.DataZgloszenia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Wszystkie")
                        {
                            cmd.Parameters.AddWithValue("@Status", statusFilter);
                        }

                        if (!string.IsNullOrEmpty(typFilter) && typFilter != "Wszystkie")
                        {
                            cmd.Parameters.AddWithValue("@Typ", typFilter);
                        }

                        if (!string.IsNullOrEmpty(priorytetFilter) && priorytetFilter != "Wszystkie")
                        {
                            cmd.Parameters.AddWithValue("@Priorytet", priorytetFilter);
                        }

                        if (!string.IsNullOrEmpty(szukaj))
                        {
                            cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");
                        }

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item2 = new ReklamacjaItem
                                {
                                    Id = reader.GetInt32(0),
                                    DataZgloszenia = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                                    NumerDokumentu = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NazwaKontrahenta = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Opis = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    SumaKg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                    Status = reader.IsDBNull(6) ? "Nowa" : reader.GetString(6),
                                    Zglaszajacy = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    OsobaRozpatrujaca = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    TypReklamacji = reader.IsDBNull(9) ? "Inne" : reader.GetString(9),
                                    Priorytet = reader.IsDBNull(10) ? "Normalny" : reader.GetString(10),
                                    ZglaszajacyId = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    RozpatrujacyId = reader.IsDBNull(12) ? "" : reader.GetString(12)
                                };
                                item2.ZglaszajacyAvatar = GetCachedAvatar(item2.ZglaszajacyId, item2.Zglaszajacy);
                                item2.RozpatrujacyAvatar = GetCachedAvatar(item2.RozpatrujacyId, item2.OsobaRozpatrujaca);
                                reklamacje.Add(item2);
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
            if (!isInitialized || string.IsNullOrEmpty(connectionString)) return;

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
                                    case "Nowa": nowe += liczba; break;
                                    case "Przyjeta":
                                    case "W analizie":
                                    case "W trakcie realizacji":
                                    case "Oczekuje na dostawce":
                                    case "W trakcie":
                                        wTrakcie += liczba; break;
                                    case "Zaakceptowana": zaakceptowane += liczba; break;
                                    case "Odrzucona": odrzucone += liczba; break;
                                    case "Zamknieta": case "Zamknięta": zamkniete += liczba; break;
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
            {
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            }
        }

        private void DgReklamacje_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = dgReklamacje.SelectedItem != null;
            btnUzupelnij.IsEnabled = selected;
            btnSzczegoly.IsEnabled = selected;
            btnZmienStatus.IsEnabled = selected;
            btnZaakceptuj.IsEnabled = selected;
            btnOdrzuc.IsEnabled = selected;
            btnUsun.IsEnabled = selected;

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
                    var window = new FormSzczegolyReklamacjiWindow(connectionString, item.Id, userId);
                    window.Owner = this;
                    window.ShowDialog();

                    if (window.StatusZmieniony)
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
            if (!isJakosc)
            {
                MessageBox.Show("Nie masz uprawnien do zmiany statusu reklamacji.\nTa funkcja jest dostepna tylko dla dzialu jakosci.",
                    "Brak uprawnien", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

                // Ogranicz dozwolone przejscia statusow
                bool isAdmin = userId == "11111";
                if (isAdmin)
                {
                    foreach (var s in FormRozpatrzenieWindow.statusPipeline)
                        combo.Items.Add(s);
                }
                else if (dozwolonePrzejscia.ContainsKey(item.Status))
                {
                    foreach (var s in dozwolonePrzejscia[item.Status])
                        combo.Items.Add(s);
                }

                if (combo.Items.Count == 0)
                {
                    MessageBox.Show("Reklamacja jest zamknieta. Tylko administrator moze zmienic status.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                combo.SelectedIndex = 0;
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
            if (!isJakosc)
            {
                MessageBox.Show("Nie masz uprawnien do zmiany statusu reklamacji.\nTa funkcja jest dostepna tylko dla dzialu jakosci.",
                    "Brak uprawnien", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                // Walidacja przejscia
                if (!CzyPrzejscieDozwolone(item.Status, "Zaakceptowana"))
                {
                    MessageBox.Show($"Nie mozna zaakceptowac reklamacji o statusie '{item.Status}'.",
                        "Niedozwolone przejscie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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
            if (!isJakosc)
            {
                MessageBox.Show("Nie masz uprawnien do zmiany statusu reklamacji.\nTa funkcja jest dostepna tylko dla dzialu jakosci.",
                    "Brak uprawnien", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                // Walidacja przejscia
                if (!CzyPrzejscieDozwolone(item.Status, "Odrzucona"))
                {
                    MessageBox.Show($"Nie mozna odrzucic reklamacji o statusie '{item.Status}'.",
                        "Niedozwolone przejscie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Pobierz poprzedni status
                            string poprzedniStatus = "";
                            using (var cmdGet = new SqlCommand("SELECT Status FROM [dbo].[Reklamacje] WHERE Id = @Id", conn, transaction))
                            {
                                cmdGet.Parameters.AddWithValue("@Id", idReklamacji);
                                poprzedniStatus = cmdGet.ExecuteScalar()?.ToString() ?? "";
                            }

                            // Aktualizuj reklamacje
                            string query = @"
                                UPDATE [dbo].[Reklamacje]
                                SET Status = @Status,
                                    OsobaRozpatrujaca = @Osoba,
                                    DataModyfikacji = GETDATE(),
                                    DataZamkniecia = CASE WHEN @Status = 'Zamknieta' THEN GETDATE() ELSE DataZamkniecia END
                                WHERE Id = @Id";

                            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Status", nowyStatus);
                                cmd.Parameters.AddWithValue("@Osoba", userId);
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            // Dodaj wpis do historii z PoprzedniStatus
                            string queryHistoria = @"
                                INSERT INTO [dbo].[ReklamacjeHistoria]
                                (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
                                VALUES
                                (@IdReklamacji, @UserID, @PoprzedniStatus, @StatusNowy, @Komentarz, 'ZmianaStatusu')";

                            using (SqlCommand cmd = new SqlCommand(queryHistoria, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@PoprzedniStatus", poprzedniStatus);
                                cmd.Parameters.AddWithValue("@StatusNowy", nowyStatus);
                                cmd.Parameters.AddWithValue("@Komentarz", $"Zmiana statusu na: {nowyStatus}");
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
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

        private void CmbTyp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WczytajReklamacje();
        }

        private void CmbPriorytet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WczytajReklamacje();
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (reklamacje.Count == 0)
                {
                    MessageBox.Show("Brak danych do eksportu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Plik CSV (*.csv)|*.csv",
                    FileName = $"Reklamacje_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "Eksportuj reklamacje"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    // Naglowki
                    sb.AppendLine("ID;Data;Nr faktury;Kontrahent;Typ;Priorytet;Kg;Status;Zglaszajacy");

                    // Dane
                    foreach (var r in reklamacje)
                    {
                        sb.AppendLine($"{r.Id};{r.DataZgloszenia:yyyy-MM-dd};\"{r.NumerDokumentu?.Replace("\"", "\"\"")}\";\"{r.NazwaKontrahenta?.Replace("\"", "\"\"")}\";\"{r.TypReklamacji}\";\"{r.Priorytet}\";{r.SumaKg:N2};\"{r.Status}\";\"{r.Zglaszajacy?.Replace("\"", "\"\"")}\"");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, sb.ToString(), System.Text.Encoding.UTF8);

                    MessageBox.Show($"Eksportowano {reklamacje.Count} reklamacji do pliku:\n{saveDialog.FileName}",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Dictionary<string, ImageSource> _avatarCache = new Dictionary<string, ImageSource>();
        private static ImageSource GetCachedAvatar(string odbiorcaId, string userName)
        {
            if (string.IsNullOrEmpty(odbiorcaId)) return null;
            if (_avatarCache.TryGetValue(odbiorcaId, out var cached)) return cached;
            var avatar = FormRozpatrzenieWindow.LoadWpfAvatar(odbiorcaId, userName, 64);
            _avatarCache[odbiorcaId] = avatar;
            return avatar;
        }

        private bool CzyPrzejscieDozwolone(string obecnyStatus, string nowyStatus)
        {
            if (userId == "11111") return true; // Admin
            if (!dozwolonePrzejscia.ContainsKey(obecnyStatus)) return false;
            return dozwolonePrzejscia[obecnyStatus].Contains(nowyStatus);
        }

        private void BtnStatystyki_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var stats = new System.Text.StringBuilder();
                stats.AppendLine("=== STATYSTYKI REKLAMACJI ===\n");

                // Statystyki ogolne
                stats.AppendLine($"Liczba reklamacji: {reklamacje.Count}");
                stats.AppendLine($"Suma kg: {reklamacje.Sum(r => r.SumaKg):N2} kg\n");

                // Wg statusu
                stats.AppendLine("--- WG STATUSU ---");
                foreach (var group in reklamacje.GroupBy(r => r.Status).OrderByDescending(g => g.Count()))
                {
                    stats.AppendLine($"  {group.Key}: {group.Count()} ({group.Sum(r => r.SumaKg):N2} kg)");
                }

                // Wg typu
                stats.AppendLine("\n--- WG TYPU ---");
                foreach (var group in reklamacje.GroupBy(r => r.TypReklamacji).OrderByDescending(g => g.Count()))
                {
                    stats.AppendLine($"  {group.Key}: {group.Count()} ({group.Sum(r => r.SumaKg):N2} kg)");
                }

                // Wg priorytetu
                stats.AppendLine("\n--- WG PRIORYTETU ---");
                foreach (var group in reklamacje.GroupBy(r => r.Priorytet).OrderByDescending(g => g.Count()))
                {
                    stats.AppendLine($"  {group.Key}: {group.Count()}");
                }

                // Top kontrahenci
                stats.AppendLine("\n--- TOP 5 KONTRAHENTOW ---");
                int i = 1;
                foreach (var group in reklamacje.GroupBy(r => r.NazwaKontrahenta).OrderByDescending(g => g.Count()).Take(5))
                {
                    stats.AppendLine($"  {i++}. {group.Key}: {group.Count()} reklamacji ({group.Sum(r => r.SumaKg):N2} kg)");
                }

                MessageBox.Show(stats.ToString(), "Statystyki reklamacji", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================
        // UZUPELNIANIE REKLAMACJI
        // ========================================

        private void BtnUzupelnij_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                var window = new UzupelnijReklamacjeWindow(connectionString, item.Id, userId);
                window.Owner = this;
                if (window.ShowDialog() == true)
                {
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        // ========================================
        // USUWANIE (ADMIN)
        // ========================================

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (userId != "11111")
            {
                MessageBox.Show("Brak uprawnien do usuwania reklamacji.", "Brak uprawnien", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                if (MessageBox.Show($"Czy na pewno chcesz USUNAC reklamacje #{item.Id} ({item.NumerDokumentu})?\n\nTa operacja jest nieodwracalna!",
                    "Potwierdzenie usuwania", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var tr = conn.BeginTransaction())
                        {
                            try
                            {
                                foreach (var table in new[] { "ReklamacjeZdjecia", "ReklamacjeTowary", "ReklamacjeHistoria" })
                                {
                                    using (var cmd = new SqlCommand($"DELETE FROM [dbo].[{table}] WHERE IdReklamacji = @Id", conn, tr))
                                    {
                                        cmd.Parameters.AddWithValue("@Id", item.Id);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                using (var cmd = new SqlCommand("DELETE FROM [dbo].[Reklamacje] WHERE Id = @Id", conn, tr))
                                {
                                    cmd.Parameters.AddWithValue("@Id", item.Id);
                                    cmd.ExecuteNonQuery();
                                }
                                tr.Commit();
                            }
                            catch
                            {
                                tr.Rollback();
                                throw;
                            }
                        }
                    }

                    MessageBox.Show($"Reklamacja #{item.Id} zostala usunieta.", "Usunieto", MessageBoxButton.OK, MessageBoxImage.Information);
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad usuwania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ========================================
        // DEBUG SYNC
        // ========================================

        private void BtnDebugSync_Click(object sender, RoutedEventArgs e)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== DEBUG SYNC FAKTUR KORYGUJACYCH ===\n");

            // KROK 1: Test polaczenia HANDEL
            log.AppendLine("[1] Polaczenie z HANDEL (192.168.0.112)...");
            try
            {
                using (var conn = new SqlConnection(HandelConnString))
                {
                    conn.Open();
                    log.AppendLine("    OK - Polaczono\n");

                    // KROK 2: Sprawdz ile jest faktur korygujacych (bez filtrow)
                    log.AppendLine("[2] Szukam faktur korygujacych (seria IN sFKS, sFKSB, sFWK)...");
                    using (var cmd = new SqlCommand(
                        "SELECT seria, COUNT(*) AS cnt FROM [HANDEL].[HM].[DK] WHERE seria IN ('sFKS', 'sFKSB', 'sFWK') GROUP BY seria", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            bool any = false;
                            while (reader.Read())
                            {
                                any = true;
                                log.AppendLine($"    seria='{reader.GetString(0)}' -> {reader.GetInt32(1)} dokumentow");
                            }
                            if (!any) log.AppendLine("    BRAK wynikow! Sprawdz nazwy serii.");
                        }
                    }

                    // KROK 3: Sprawdz z filtrami (anulowany=0, data 6 mies)
                    log.AppendLine("\n[3] Z filtrami (anulowany=0, ostatnie 6 mies)...");
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM [HANDEL].[HM].[DK]
                        WHERE seria IN ('sFKS', 'sFKSB', 'sFWK')
                          AND anulowany = 0
                          AND data >= DATEADD(MONTH, -6, GETDATE())", conn))
                    {
                        int cnt = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Znaleziono: {cnt} dokumentow");
                    }

                    // KROK 4: Pokaz przykladowe 5 dokumentow
                    log.AppendLine("\n[4] Przykladowe dokumenty (TOP 5)...");
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 5 DK.id, DK.kod, DK.seria, DK.data, DK.walNetto, DK.anulowany,
                               ISNULL(C.shortcut, '?') AS Kontrahent
                        FROM [HANDEL].[HM].[DK] DK
                        LEFT JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        WHERE DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                        ORDER BY DK.data DESC", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                log.AppendLine($"    id={reader["id"]} kod={reader["kod"]} seria={reader["seria"]} data={reader["data"]} walNetto={reader["walNetto"]} anul={reader["anulowany"]} kontr={reader["Kontrahent"]}");
                            }
                        }
                    }

                    // KROK 4b: Sprawdz WSZYSTKIE unikalne serie w bazie
                    log.AppendLine("\n[4b] Wszystkie unikalne serie w DK (TOP 30)...");
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 30 seria, COUNT(*) AS cnt FROM [HANDEL].[HM].[DK] GROUP BY seria ORDER BY cnt DESC", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string s = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                                log.AppendLine($"    '{s}' -> {reader.GetInt32(1)}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"    BLAD: {ex.Message}");
            }

            // KROK 5: Test polaczenia LibraNet
            log.AppendLine($"\n[5] Polaczenie z LibraNet ({connectionString.Substring(0, 40)}...)...");
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    log.AppendLine("    OK - Polaczono");

                    // Ile reklamacji typu Faktura korygujaca juz jest
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE TypReklamacji = 'Faktura korygujaca'", conn))
                    {
                        int cnt = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Istniejace reklamacje typu 'Faktura korygujaca': {cnt}");
                    }

                    // Sprawdz czy kolumna IdDokumentu istnieje
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reklamacje' AND COLUMN_NAME = 'IdDokumentu'", conn))
                    {
                        int exists = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Kolumna IdDokumentu istnieje: {(exists > 0 ? "TAK" : "NIE")}");
                    }

                    // Sprawdz czy kolumna SumaWartosc istnieje
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reklamacje' AND COLUMN_NAME = 'SumaWartosc'", conn))
                    {
                        int exists = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Kolumna SumaWartosc istnieje: {(exists > 0 ? "TAK" : "NIE")}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"    BLAD: {ex.Message}");
            }

            // KROK 6: Probuj sync
            log.AppendLine("\n[6] Uruchamiam SyncFakturyKorygujace()...");
            try
            {
                SyncFakturyKorygujace();
                log.AppendLine("    OK - Sync zakonczony bez bledu");
            }
            catch (Exception ex)
            {
                log.AppendLine($"    BLAD SYNC: {ex.Message}\n    {ex.StackTrace}");
            }

            // Pokaz wynik
            var debugWindow = new Window
            {
                Title = "Debug Sync Faktur Korygujacych",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            var textBox = new TextBox
            {
                Text = log.ToString(),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(10)
            };
            debugWindow.Content = textBox;
            debugWindow.ShowDialog();

            // Odswiez dane
            WczytajReklamacje();
            WczytajStatystyki();
        }

        // ========================================
        // SYNC FAKTUR KORYGUJACYCH Z HANDEL
        // ========================================

        private void SyncFakturyKorygujace()
        {
            try
            {
                // Pobierz faktury korygujace z HANDEL
                var korygujace = new List<(int id, string kod, DateTime data, decimal wartosc, int khid, string kontrahent, string handlowiec, decimal sumaKg)>();

                using (var connHandel = new SqlConnection(HandelConnString))
                {
                    connHandel.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT DK.id, DK.kod, DK.data,
                               ABS(DK.walNetto) AS Wartosc,
                               DK.khid,
                               C.shortcut AS NazwaKontrahenta,
                               ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
                               ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id), 0)) AS SumaKg
                        FROM [HANDEL].[HM].[DK] DK
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                        WHERE DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                          AND DK.anulowany = 0
                          AND DK.data >= DATEADD(MONTH, -6, GETDATE())", connHandel))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                korygujace.Add((
                                    reader.GetInt32(0),
                                    reader.GetString(1),
                                    reader.GetDateTime(2),
                                    reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                                    reader.GetInt32(4),
                                    reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    reader.IsDBNull(6) ? "-" : reader.GetString(6),
                                    reader.IsDBNull(7) ? 0m : Convert.ToDecimal(reader.GetValue(7))
                                ));
                            }
                        }
                    }
                }

                if (korygujace.Count == 0) return;

                // Wstaw do LibraNet te, ktorych jeszcze nie ma
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (var fk in korygujace)
                    {
                        // Sprawdz czy juz istnieje
                        using (var cmdCheck = new SqlCommand(
                            "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE IdDokumentu = @IdDok AND TypReklamacji = 'Faktura korygujaca'", conn))
                        {
                            cmdCheck.Parameters.AddWithValue("@IdDok", fk.id);
                            if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0) continue;
                        }

                        // Wstaw nowa reklamacje
                        int noweIdReklamacji = 0;
                        using (var cmdInsert = new SqlCommand(@"
                            INSERT INTO [dbo].[Reklamacje]
                            (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta,
                             Opis, SumaKg, SumaWartosc, Status, TypReklamacji, Priorytet)
                            VALUES
                            (@Data, '', @IdDok, @NrDok, @IdKontr, @Kontrahent,
                             @Opis, @SumaKg, @Wartosc, 'Nowa', 'Faktura korygujaca', 'Normalny');
                            SELECT SCOPE_IDENTITY();", conn))
                        {
                            cmdInsert.Parameters.AddWithValue("@Data", fk.data);
                            cmdInsert.Parameters.AddWithValue("@IdDok", fk.id);
                            cmdInsert.Parameters.AddWithValue("@NrDok", fk.kod);
                            cmdInsert.Parameters.AddWithValue("@IdKontr", fk.khid);
                            cmdInsert.Parameters.AddWithValue("@Kontrahent", fk.kontrahent);
                            cmdInsert.Parameters.AddWithValue("@Opis", $"Faktura korygujaca {fk.kod} | Handlowiec: {fk.handlowiec}");
                            cmdInsert.Parameters.AddWithValue("@SumaKg", fk.sumaKg);
                            cmdInsert.Parameters.AddWithValue("@Wartosc", fk.wartosc);
                            var result = cmdInsert.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                                noweIdReklamacji = Convert.ToInt32(result);
                        }

                        // Wstaw towary z faktury korygujuacej z HANDEL
                        if (noweIdReklamacji > 0)
                        {
                            try
                            {
                                using (var connH = new SqlConnection(HandelConnString))
                                {
                                    connH.Open();
                                    using (var cmdTow = new SqlCommand(@"
                                        SELECT DP.kod AS Symbol,
                                               ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                                               ABS(SUM(ISNULL(DP.ilosc, 0))) AS Waga,
                                               MAX(ABS(ISNULL(DP.cena, 0))) AS Cena,
                                               ABS(SUM(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0))) AS Wartosc
                                        FROM [HANDEL].[HM].[DP] DP
                                        LEFT JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                                        WHERE DP.super = @IdDok
                                        GROUP BY DP.kod, TW.nazwa, TW.kod
                                        HAVING ABS(SUM(ISNULL(DP.ilosc, 0))) > 0.01
                                        ORDER BY MIN(DP.lp)", connH))
                                    {
                                        cmdTow.Parameters.AddWithValue("@IdDok", fk.id);
                                        using (var rdrTow = cmdTow.ExecuteReader())
                                        {
                                            var pozycje = new List<(string symbol, string nazwa, decimal waga, decimal cena, decimal wartosc)>();
                                            while (rdrTow.Read())
                                            {
                                                pozycje.Add((
                                                    rdrTow.IsDBNull(0) ? "" : rdrTow.GetString(0),
                                                    rdrTow.IsDBNull(1) ? "" : rdrTow.GetString(1),
                                                    rdrTow.IsDBNull(2) ? 0m : Convert.ToDecimal(rdrTow.GetValue(2)),
                                                    rdrTow.IsDBNull(3) ? 0m : Convert.ToDecimal(rdrTow.GetValue(3)),
                                                    rdrTow.IsDBNull(4) ? 0m : Convert.ToDecimal(rdrTow.GetValue(4))
                                                ));
                                            }
                                            rdrTow.Close();

                                            foreach (var poz in pozycje)
                                            {
                                                using (var cmdInsTow = new SqlCommand(@"
                                                    INSERT INTO [dbo].[ReklamacjeTowary]
                                                    (IdReklamacji, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc, PrzyczynaReklamacji)
                                                    VALUES (@IdRek, 0, @Symbol, @Nazwa, @Waga, @Cena, @Wartosc, 'Korekta')", conn))
                                                {
                                                    cmdInsTow.Parameters.AddWithValue("@IdRek", noweIdReklamacji);
                                                    cmdInsTow.Parameters.AddWithValue("@Symbol", poz.symbol);
                                                    cmdInsTow.Parameters.AddWithValue("@Nazwa", poz.nazwa);
                                                    cmdInsTow.Parameters.AddWithValue("@Waga", poz.waga);
                                                    cmdInsTow.Parameters.AddWithValue("@Cena", poz.cena);
                                                    cmdInsTow.Parameters.AddWithValue("@Wartosc", poz.wartosc);
                                                    cmdInsTow.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SyncFakturyKorygujace error: {ex.Message}");
                throw; // rethrow aby BtnDebugSync_Click mógł złapać
            }
        }
    }

    public class ReklamacjaItem : INotifyPropertyChanged
    {
        private int _id;
        private DateTime _dataZgloszenia;
        private string _numerDokumentu;
        private string _nazwaKontrahenta;
        private string _opis;
        private decimal _sumaKg;
        private string _status;
        private string _zglaszajacy;
        private string _osobaRozpatrujaca;
        private string _typReklamacji;
        private string _priorytet;

        public int Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }
        public DateTime DataZgloszenia { get => _dataZgloszenia; set { _dataZgloszenia = value; OnPropertyChanged(nameof(DataZgloszenia)); } }
        public string NumerDokumentu { get => _numerDokumentu; set { _numerDokumentu = value; OnPropertyChanged(nameof(NumerDokumentu)); } }
        public string NazwaKontrahenta { get => _nazwaKontrahenta; set { _nazwaKontrahenta = value; OnPropertyChanged(nameof(NazwaKontrahenta)); } }
        public string Opis { get => _opis; set { _opis = value; OnPropertyChanged(nameof(Opis)); } }
        public decimal SumaKg { get => _sumaKg; set { _sumaKg = value; OnPropertyChanged(nameof(SumaKg)); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string Zglaszajacy { get => _zglaszajacy; set { _zglaszajacy = value; OnPropertyChanged(nameof(Zglaszajacy)); } }
        public string OsobaRozpatrujaca { get => _osobaRozpatrujaca; set { _osobaRozpatrujaca = value; OnPropertyChanged(nameof(OsobaRozpatrujaca)); } }
        public string TypReklamacji { get => _typReklamacji; set { _typReklamacji = value; OnPropertyChanged(nameof(TypReklamacji)); } }
        public string Priorytet { get => _priorytet; set { _priorytet = value; OnPropertyChanged(nameof(Priorytet)); } }

        // Avatar support
        public string ZglaszajacyId { get; set; }
        public string RozpatrujacyId { get; set; }
        public ImageSource ZglaszajacyAvatar { get; set; }
        public ImageSource RozpatrujacyAvatar { get; set; }
        public string ZglaszajacyInitials => FormRozpatrzenieWindow.GetInitials(Zglaszajacy);
        public SolidColorBrush ZglaszajacyAvatarBrush => FormRozpatrzenieWindow.GetAvatarBrush(Zglaszajacy);
        public string RozpatrujacyInitials => FormRozpatrzenieWindow.GetInitials(OsobaRozpatrujaca);
        public SolidColorBrush RozpatrujacyAvatarBrush => FormRozpatrzenieWindow.GetAvatarBrush(OsobaRozpatrujaca);
        public Visibility ZglaszajacyAvatarPhotoVis => ZglaszajacyAvatar != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RozpatrujacyAvatarPhotoVis => RozpatrujacyAvatar != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RozpatrujacyVis => string.IsNullOrEmpty(OsobaRozpatrujaca) ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
