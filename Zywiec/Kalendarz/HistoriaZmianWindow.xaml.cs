using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Okno do przeglądania historii zmian (Audit Log)
    /// </summary>
    public partial class HistoriaZmianWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _userId;
        private readonly string _filterByLP;
        private readonly string _hodowcaName;
        private ObservableCollection<AuditLogDisplayModel> _auditLogs = new ObservableCollection<AuditLogDisplayModel>();
        private ObservableCollection<NotatkaHistoryModel> _notatki = new ObservableCollection<NotatkaHistoryModel>();

        public HistoriaZmianWindow(string connectionString, string userId = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connectionString = connectionString;
            _userId = userId;
            _filterByLP = null;
            _hodowcaName = null;

            dgAuditLog.ItemsSource = _auditLogs;
            dgNotatki.ItemsSource = _notatki;
            dpDateFrom.SelectedDate = DateTime.Today.AddDays(-7);
            dpDateTo.SelectedDate = DateTime.Today;

            Loaded += async (s, e) =>
            {
                await LoadUsersAsync();
                await LoadAuditLogAsync();
            };
        }

        /// <summary>
        /// Konstruktor do otwierania historii dla konkretnego LP (pełny ekran)
        /// </summary>
        public HistoriaZmianWindow(string connectionString, string userId, string filterLP, string hodowcaName)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connectionString = connectionString;
            _userId = userId;
            _filterByLP = filterLP;
            _hodowcaName = hodowcaName;

            // Pełny ekran
            WindowState = WindowState.Maximized;

            // Wyświetl informacje o filtrowanym LP i hodowcy
            if (!string.IsNullOrEmpty(filterLP))
            {
                txtFilteredLP.Text = $"- LP: {filterLP}";
                txtFilterLP.Text = filterLP;
                txtFilterLP.IsEnabled = false; // Zablokuj zmianę LP
            }
            if (!string.IsNullOrEmpty(hodowcaName))
            {
                txtFilteredHodowca.Text = $"| Hodowca: {hodowcaName}";
            }

            // Pokaż wszystkie zmiany dla tego LP (bez limitu czasowego)
            cmbFilterPeriod.SelectedIndex = 3; // "Własny zakres"
            dpDateFrom.SelectedDate = DateTime.Today.AddYears(-1);
            dpDateTo.SelectedDate = DateTime.Today;

            dgAuditLog.ItemsSource = _auditLogs;
            dgNotatki.ItemsSource = _notatki;

            Loaded += async (s, e) =>
            {
                await LoadUsersAsync();
                await LoadAuditLogAsync();
                if (!string.IsNullOrEmpty(_filterByLP))
                {
                    await LoadNotatkiAsync();
                }
            };
        }

        #region Ładowanie danych

        private async Task LoadUsersAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    using (var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditLog_Dostawy'", conn))
                    {
                        var exists = (int)await checkCmd.ExecuteScalarAsync();
                        if (exists == 0)
                        {
                            txtStatusInfo.Text = "Tabela audytu nie istnieje. Uruchom skrypt CreateAuditLogTable.sql";
                            return;
                        }
                    }

                    // Pobierz listę użytkowników z audytu
                    using (var cmd = new SqlCommand(
                        "SELECT DISTINCT ISNULL(UserName, UserID) AS UserDisplay FROM AuditLog_Dostawy ORDER BY UserDisplay", conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string user = reader["UserDisplay"]?.ToString();
                                if (!string.IsNullOrEmpty(user))
                                {
                                    cmbFilterUser.Items.Add(new ComboBoxItem { Content = user });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatusInfo.Text = $"Błąd: {ex.Message}";
            }
        }

        private async Task LoadAuditLogAsync()
        {
            _auditLogs.Clear();

            try
            {
                // Pobierz parametry filtrowania
                string filterLP = txtFilterLP.Text?.Trim();
                string filterUser = (cmbFilterUser.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string filterOperation = (cmbFilterOperation.SelectedItem as ComboBoxItem)?.Content?.ToString();
                int hours = GetFilterHours();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    using (var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditLog_Dostawy'", conn))
                    {
                        var exists = (int)await checkCmd.ExecuteScalarAsync();
                        if (exists == 0)
                        {
                            txtStatusInfo.Text = "Tabela audytu nie istnieje. Uruchom skrypt SQL.";
                            return;
                        }
                    }

                    // Buduj zapytanie
                    var sql = new StringBuilder(@"
                        SELECT TOP 1000
                            AuditID, DataZmiany, UserID, UserName, NazwaTabeli, RekordID,
                            TypOperacji, ZrodloZmiany, NazwaPola, StaraWartosc, NowaWartosc,
                            OpisZmiany, NazwaKomputera
                        FROM AuditLog_Dostawy
                        WHERE 1=1");

                    var parameters = new List<SqlParameter>();

                    // Filtr LP
                    if (!string.IsNullOrEmpty(filterLP))
                    {
                        sql.Append(" AND RekordID = @lp");
                        parameters.Add(new SqlParameter("@lp", filterLP));
                    }

                    // Filtr użytkownika
                    if (!string.IsNullOrEmpty(filterUser) && filterUser != "Wszyscy")
                    {
                        sql.Append(" AND (UserName = @user OR UserID = @user)");
                        parameters.Add(new SqlParameter("@user", filterUser));
                    }

                    // Filtr operacji
                    if (!string.IsNullOrEmpty(filterOperation) && filterOperation != "Wszystkie")
                    {
                        sql.Append(" AND TypOperacji = @op");
                        parameters.Add(new SqlParameter("@op", filterOperation));
                    }

                    // Filtr dat
                    if (hours > 0)
                    {
                        sql.Append(" AND DataZmiany >= DATEADD(HOUR, -@hours, GETDATE())");
                        parameters.Add(new SqlParameter("@hours", hours));
                    }
                    else if (dpDateFrom.SelectedDate.HasValue && dpDateTo.SelectedDate.HasValue)
                    {
                        sql.Append(" AND DataZmiany >= @dateFrom AND DataZmiany < DATEADD(DAY, 1, @dateTo)");
                        parameters.Add(new SqlParameter("@dateFrom", dpDateFrom.SelectedDate.Value));
                        parameters.Add(new SqlParameter("@dateTo", dpDateTo.SelectedDate.Value));
                    }

                    sql.Append(" ORDER BY DataZmiany DESC");

                    using (var cmd = new SqlCommand(sql.ToString(), conn))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                _auditLogs.Add(new AuditLogDisplayModel
                                {
                                    AuditID = reader.GetInt64(reader.GetOrdinal("AuditID")),
                                    DataZmiany = reader.GetDateTime(reader.GetOrdinal("DataZmiany")),
                                    UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) ? null : reader.GetString(reader.GetOrdinal("UserID")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
                                    NazwaTabeli = reader.IsDBNull(reader.GetOrdinal("NazwaTabeli")) ? null : reader.GetString(reader.GetOrdinal("NazwaTabeli")),
                                    RekordID = reader.IsDBNull(reader.GetOrdinal("RekordID")) ? null : reader.GetString(reader.GetOrdinal("RekordID")),
                                    TypOperacji = reader.IsDBNull(reader.GetOrdinal("TypOperacji")) ? null : reader.GetString(reader.GetOrdinal("TypOperacji")),
                                    ZrodloZmiany = reader.IsDBNull(reader.GetOrdinal("ZrodloZmiany")) ? null : reader.GetString(reader.GetOrdinal("ZrodloZmiany")),
                                    NazwaPola = reader.IsDBNull(reader.GetOrdinal("NazwaPola")) ? null : reader.GetString(reader.GetOrdinal("NazwaPola")),
                                    StaraWartosc = reader.IsDBNull(reader.GetOrdinal("StaraWartosc")) ? null : reader.GetString(reader.GetOrdinal("StaraWartosc")),
                                    NowaWartosc = reader.IsDBNull(reader.GetOrdinal("NowaWartosc")) ? null : reader.GetString(reader.GetOrdinal("NowaWartosc")),
                                    OpisZmiany = reader.IsDBNull(reader.GetOrdinal("OpisZmiany")) ? null : reader.GetString(reader.GetOrdinal("OpisZmiany")),
                                    NazwaKomputera = reader.IsDBNull(reader.GetOrdinal("NazwaKomputera")) ? null : reader.GetString(reader.GetOrdinal("NazwaKomputera"))
                                });
                            }
                        }
                    }
                }

                txtResultCount.Text = _auditLogs.Count.ToString();
                txtLastRefresh.Text = DateTime.Now.ToString("HH:mm:ss");
                txtStatusInfo.Text = _auditLogs.Count >= 1000 ? "Wyświetlono maksymalnie 1000 rekordów" : "";
            }
            catch (Exception ex)
            {
                txtStatusInfo.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania audytu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetFilterHours()
        {
            var selectedItem = cmbFilterPeriod.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag != null && int.TryParse(selectedItem.Tag.ToString(), out int hours))
            {
                return hours;
            }
            return 0;
        }

        private async Task LoadNotatkiAsync()
        {
            _notatki.Clear();

            if (string.IsNullOrEmpty(_filterByLP))
            {
                txtNotatkiCount.Text = "";
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Pobierz notatki dla danego LP (IndeksID)
                    string sql = @"
                        SELECT N.ID, N.Tresc, N.DataUtworzenia, N.DataModyfikacji,
                               N.KtoStworzyl, N.KtoZmodyfikowal,
                               O1.Name AS KtoDodal, O2.Name AS KtoZmienil
                        FROM Notatki N
                        LEFT JOIN operators O1 ON N.KtoStworzyl = O1.ID
                        LEFT JOIN operators O2 ON N.KtoZmodyfikowal = O2.ID
                        WHERE N.IndeksID = @lp
                        ORDER BY N.DataUtworzenia DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", _filterByLP);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                _notatki.Add(new NotatkaHistoryModel
                                {
                                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                    Tresc = reader.IsDBNull(reader.GetOrdinal("Tresc")) ? "" : reader.GetString(reader.GetOrdinal("Tresc")),
                                    DataUtworzenia = reader.IsDBNull(reader.GetOrdinal("DataUtworzenia")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DataUtworzenia")),
                                    DataModyfikacji = reader.IsDBNull(reader.GetOrdinal("DataModyfikacji")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DataModyfikacji")),
                                    KtoDodal = reader.IsDBNull(reader.GetOrdinal("KtoDodal")) ? "" : reader.GetString(reader.GetOrdinal("KtoDodal")),
                                    KtoZmienil = reader.IsDBNull(reader.GetOrdinal("KtoZmienil")) ? "" : reader.GetString(reader.GetOrdinal("KtoZmienil"))
                                });
                            }
                        }
                    }
                }

                txtNotatkiCount.Text = $"({_notatki.Count} notatek)";
            }
            catch (Exception ex)
            {
                txtNotatkiCount.Text = $"Błąd: {ex.Message}";
            }
        }

        #endregion

        #region Obsługa zdarzeń

        private void CmbFilterPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlCustomDates == null) return;

            var selectedItem = cmbFilterPeriod.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag?.ToString() == "0")
            {
                pnlCustomDates.Visibility = Visibility.Visible;
            }
            else
            {
                pnlCustomDates.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            await LoadAuditLogAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAuditLogAsync();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtFilterLP.Text = "";
            cmbFilterUser.SelectedIndex = 0;
            cmbFilterOperation.SelectedIndex = 0;
            cmbFilterPeriod.SelectedIndex = 0;
            pnlCustomDates.Visibility = Visibility.Collapsed;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Plik CSV|*.csv",
                    FileName = $"AuditLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Data/Czas;Użytkownik;LP;Operacja;Źródło;Pole;Stara wartość;Nowa wartość;Opis");

                    foreach (var item in _auditLogs)
                    {
                        sb.AppendLine($"\"{item.DataZmianyFormatted}\";\"{item.UserDisplay}\";\"{item.RekordID}\";\"{item.TypOperacjiDisplay}\";\"{item.ZrodloZmianyDisplay}\";\"{item.NazwaPolaDisplay}\";\"{item.StaraWartosc}\";\"{item.NowaWartosc}\";\"{item.OpisZmiany}\"");
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Wyeksportowano {_auditLogs.Count} rekordów.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }

    #region Model wyświetlania

    /// <summary>
    /// Model do wyświetlania w DataGrid z formatowaniem
    /// </summary>
    public class AuditLogDisplayModel : INotifyPropertyChanged
    {
        public long AuditID { get; set; }
        public DateTime DataZmiany { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string NazwaTabeli { get; set; }
        public string RekordID { get; set; }
        public string TypOperacji { get; set; }
        public string ZrodloZmiany { get; set; }
        public string NazwaPola { get; set; }
        public string StaraWartosc { get; set; }
        public string NowaWartosc { get; set; }
        public string OpisZmiany { get; set; }
        public string NazwaKomputera { get; set; }

        // Właściwości formatujące
        public string DataZmianyFormatted => DataZmiany.ToString("yyyy-MM-dd HH:mm:ss");

        public string UserDisplay => !string.IsNullOrEmpty(UserName) ? UserName : UserID;

        public string TypOperacjiDisplay => TypOperacji switch
        {
            "INSERT" => "Dodanie",
            "UPDATE" => "Zmiana",
            "DELETE" => "Usunięcie",
            _ => TypOperacji
        };

        public string ZrodloZmianyDisplay => ZrodloZmiany switch
        {
            "DoubleClick_Auta" => "Dwuklik - Auta",
            "DoubleClick_Sztuki" => "Dwuklik - Sztuki",
            "DoubleClick_Waga" => "Dwuklik - Waga",
            "DoubleClick_Uwagi" => "Dwuklik - Uwagi",
            "DoubleClick_Cena" => "Dwuklik - Cena",
            "Checkbox_Potwierdzenie" => "Checkbox potw.",
            "Checkbox_Wstawienie" => "Checkbox wstaw.",
            "Button_DataUp" => "Data +1",
            "Button_DataDown" => "Data -1",
            "DragDrop" => "Przeciągnij/upuść",
            "Form_Zapisz" => "Formularz",
            "Form_DodajNotatke" => "Dodaj notatkę",
            "QuickNote" => "Szybka notatka",
            "Button_Duplikuj" => "Duplikacja",
            "Button_Usun" => "Usunięcie",
            "BulkConfirm" => "Masowe potw.",
            "BulkCancel" => "Masowe anul.",
            _ => ZrodloZmiany
        };

        public string NazwaPolaDisplay => NazwaPola switch
        {
            "Auta" => "Ilość aut",
            "SztukiDek" => "Sztuki",
            "WagaDek" => "Waga",
            "Cena" => "Cena",
            "DataOdbioru" => "Data odbioru",
            "Bufor" => "Status",
            "Dostawca" => "Dostawca",
            "TypCeny" => "Typ ceny",
            "TypUmowy" => "Typ umowy",
            "Dodatek" => "Dodatek",
            "Tresc" => "Treść notatki",
            "isConf" => "Potwierdzenie",
            "PotwSztuki" => "Potw. sztuk",
            "PotwWaga" => "Potw. wagi",
            _ => NazwaPola
        };

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Model do wyświetlania notatek w DataGrid
    /// </summary>
    public class NotatkaHistoryModel
    {
        public int ID { get; set; }
        public string Tresc { get; set; }
        public DateTime? DataUtworzenia { get; set; }
        public DateTime? DataModyfikacji { get; set; }
        public string KtoDodal { get; set; }
        public string KtoZmienil { get; set; }
    }

    #endregion
}
