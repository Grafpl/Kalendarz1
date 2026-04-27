using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Okno do przeglądania historii zmian (Audit Log)
    /// </summary>
    public partial class HistoriaZmianWindow : Window
    {
        private const int PAGE_SIZE = 200;

        private readonly string _connectionString;
        private readonly string _userId;
        private readonly string _filterByLP;
        private readonly string _hodowcaName;
        private ObservableCollection<AuditLogDisplayModel> _auditLogs = new ObservableCollection<AuditLogDisplayModel>();

        // Cache listy użytkowników (5 minut)
        private static List<string> _usersCache;
        private static DateTime _usersCacheExpiry = DateTime.MinValue;

        // Cache listy hodowców (5 minut)
        private static List<string> _hodowcyCache;
        private static DateTime _hodowcyCacheExpiry = DateTime.MinValue;

        // Paginacja kursorowa - najmniejsze AuditID z aktualnej listy
        private long? _oldestLoadedAuditId;
        private bool _hasMoreData;

        /// <summary>
        /// Callback wywoływany przy dwukliku na wiersz — przekazuje LP i (opcjonalnie) datę dostawy do nawigacji.
        /// </summary>
        public Action<string, DateTime?> NavigateToDostawaRequested { get; set; }

        public HistoriaZmianWindow(string connectionString, string userId = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connectionString = connectionString;
            _userId = userId;
            _filterByLP = null;
            _hodowcaName = null;

            dgAuditLog.ItemsSource = _auditLogs;
            dpDateFrom.SelectedDate = DateTime.Today.AddDays(-7);
            dpDateTo.SelectedDate = DateTime.Today;

            Loaded += async (s, e) =>
            {
                await LoadUsersAsync();
                await LoadHodowcyAsync();
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

            Loaded += async (s, e) =>
            {
                await LoadUsersAsync();
                await LoadHodowcyAsync();
                await LoadAuditLogAsync();
            };
        }

        #region Ładowanie danych

        private async Task EnsureAuditTableExistsAsync(SqlConnection conn)
        {
            using (var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditLog_Dostawy'", conn))
            {
                var exists = (int)await checkCmd.ExecuteScalarAsync();
                if (exists == 0)
                {
                    // Auto-tworzenie tabeli audytu
                    string createSql = @"
                        CREATE TABLE AuditLog_Dostawy (
                            AuditID BIGINT IDENTITY(1,1) PRIMARY KEY,
                            DataZmiany DATETIME2 NOT NULL DEFAULT GETDATE(),
                            UserID NVARCHAR(50) NOT NULL,
                            UserName NVARCHAR(100) NULL,
                            NazwaTabeli NVARCHAR(100) NOT NULL,
                            RekordID NVARCHAR(50) NOT NULL,
                            TypOperacji NVARCHAR(20) NOT NULL,
                            ZrodloZmiany NVARCHAR(100) NOT NULL,
                            NazwaPola NVARCHAR(100) NULL,
                            StaraWartosc NVARCHAR(MAX) NULL,
                            NowaWartosc NVARCHAR(MAX) NULL,
                            DodatkoweInfo NVARCHAR(MAX) NULL,
                            AdresIP NVARCHAR(50) NULL,
                            NazwaKomputera NVARCHAR(100) NULL,
                            OpisZmiany NVARCHAR(500) NULL
                        );
                        CREATE NONCLUSTERED INDEX IX_AuditLog_DataZmiany ON AuditLog_Dostawy (DataZmiany DESC);
                        CREATE NONCLUSTERED INDEX IX_AuditLog_RekordID ON AuditLog_Dostawy (RekordID);
                        CREATE NONCLUSTERED INDEX IX_AuditLog_UserID ON AuditLog_Dostawy (UserID);";
                    using (var createCmd = new SqlCommand(createSql, conn))
                    {
                        await createCmd.ExecuteNonQueryAsync();
                    }
                    txtStatusInfo.Text = "Tabela audytu została automatycznie utworzona.";
                }
            }
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                List<string> users;

                // Cache 5-minutowy — DISTINCT na całej tabeli to drogie zapytanie.
                if (_usersCache != null && DateTime.Now < _usersCacheExpiry)
                {
                    users = _usersCache;
                }
                else
                {
                    users = new List<string>();
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        await EnsureAuditTableExistsAsync(conn);

                        // Tylko aktywni użytkownicy z ostatnich 90 dni — szybsze niż pełen DISTINCT
                        using (var cmd = new SqlCommand(@"
                            SELECT DISTINCT ISNULL(UserName, UserID) AS UserDisplay
                            FROM AuditLog_Dostawy WITH (READUNCOMMITTED)
                            WHERE DataZmiany >= DATEADD(DAY, -90, GETDATE())
                            ORDER BY UserDisplay", conn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string user = reader["UserDisplay"]?.ToString();
                                    if (!string.IsNullOrEmpty(user)) users.Add(user);
                                }
                            }
                        }
                    }
                    _usersCache = users;
                    _usersCacheExpiry = DateTime.Now.AddMinutes(5);
                }

                cmbFilterUser.Items.Clear();
                cmbFilterUser.Items.Add(new ComboBoxItem { Content = "Wszyscy", IsSelected = true });
                foreach (var user in users)
                    cmbFilterUser.Items.Add(new ComboBoxItem { Content = user });
            }
            catch (Exception ex)
            {
                txtStatusInfo.Text = $"Błąd: {ex.Message}";
            }
        }

        private async Task LoadHodowcyAsync()
        {
            try
            {
                List<string> hodowcy;
                if (_hodowcyCache != null && DateTime.Now < _hodowcyCacheExpiry)
                {
                    hodowcy = _hodowcyCache;
                }
                else
                {
                    hodowcy = new List<string>();
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand(@"
                            SELECT DISTINCT Dostawca FROM HarmonogramDostaw WITH (READUNCOMMITTED)
                            WHERE Dostawca IS NOT NULL AND LEN(Dostawca) > 0
                            ORDER BY Dostawca", conn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string h = reader["Dostawca"]?.ToString();
                                    if (!string.IsNullOrEmpty(h)) hodowcy.Add(h);
                                }
                            }
                        }
                    }
                    _hodowcyCache = hodowcy;
                    _hodowcyCacheExpiry = DateTime.Now.AddMinutes(5);
                }

                cmbFilterHodowca.Items.Clear();
                cmbFilterHodowca.Items.Add(new ComboBoxItem { Content = "Wszyscy", IsSelected = true });
                foreach (var h in hodowcy)
                    cmbFilterHodowca.Items.Add(new ComboBoxItem { Content = h });
            }
            catch (Exception ex)
            {
                txtStatusInfo.Text = $"Błąd ładowania hodowców: {ex.Message}";
            }
        }

        private async Task LoadAuditLogAsync(bool append = false)
        {
            if (!append)
            {
                _auditLogs.Clear();
                _oldestLoadedAuditId = null;
                _hasMoreData = false;
            }

            try
            {
                string filterLP = txtFilterLP.Text?.Trim();
                string filterUser = (cmbFilterUser.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string filterOperation = (cmbFilterOperation.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string filterHodowca = GetSelectedComboText(cmbFilterHodowca);
                int hours = GetFilterHours();

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    await EnsureAuditTableExistsAsync(conn);

                    var sql = new StringBuilder(@"
                        SELECT TOP (@pageSize)
                            AL.AuditID, AL.DataZmiany, AL.UserID, AL.UserName, AL.NazwaTabeli, AL.RekordID,
                            AL.TypOperacji, AL.ZrodloZmiany, AL.NazwaPola, AL.StaraWartosc, AL.NowaWartosc,
                            AL.OpisZmiany, AL.NazwaKomputera, AL.DodatkoweInfo,
                            HD.Dostawca AS HD_Dostawca, HD.DataOdbioru AS HD_DataOdbioru
                        FROM AuditLog_Dostawy AL WITH (READUNCOMMITTED)
                        LEFT JOIN HarmonogramDostaw HD ON TRY_CAST(AL.RekordID AS INT) = HD.Lp
                        WHERE 1=1");

                    var parameters = new List<SqlParameter>
                    {
                        new SqlParameter("@pageSize", PAGE_SIZE)
                    };

                    if (!string.IsNullOrEmpty(filterLP))
                    {
                        sql.Append(" AND AL.RekordID = @lp");
                        parameters.Add(new SqlParameter("@lp", filterLP));
                    }

                    if (!string.IsNullOrEmpty(filterUser) && filterUser != "Wszyscy")
                    {
                        sql.Append(" AND (AL.UserName = @user OR AL.UserID = @user)");
                        parameters.Add(new SqlParameter("@user", filterUser));
                    }

                    if (!string.IsNullOrEmpty(filterOperation) && filterOperation != "Wszystkie")
                    {
                        sql.Append(" AND AL.TypOperacji = @op");
                        parameters.Add(new SqlParameter("@op", filterOperation));
                    }

                    if (!string.IsNullOrEmpty(filterHodowca) && filterHodowca != "Wszyscy")
                    {
                        // Hodowca może pochodzić z JSON DodatkoweInfo lub z JOIN-owanego HarmonogramDostaw
                        sql.Append(@" AND (
                            JSON_VALUE(AL.DodatkoweInfo, '$.Dostawca') = @hodowca
                            OR HD.Dostawca = @hodowca
                        )");
                        parameters.Add(new SqlParameter("@hodowca", filterHodowca));
                    }

                    if (hours > 0)
                    {
                        sql.Append(" AND AL.DataZmiany >= DATEADD(HOUR, -@hours, GETDATE())");
                        parameters.Add(new SqlParameter("@hours", hours));
                    }
                    else if (dpDateFrom.SelectedDate.HasValue && dpDateTo.SelectedDate.HasValue)
                    {
                        sql.Append(" AND AL.DataZmiany >= @dateFrom AND AL.DataZmiany < DATEADD(DAY, 1, @dateTo)");
                        parameters.Add(new SqlParameter("@dateFrom", dpDateFrom.SelectedDate.Value));
                        parameters.Add(new SqlParameter("@dateTo", dpDateTo.SelectedDate.Value));
                    }

                    // Paginacja kursorowa - dla "Załaduj więcej" pobieraj rekordy starsze niż najmniejszy AuditID
                    if (append && _oldestLoadedAuditId.HasValue)
                    {
                        sql.Append(" AND AL.AuditID < @cursorId");
                        parameters.Add(new SqlParameter("@cursorId", _oldestLoadedAuditId.Value));
                    }

                    sql.Append(" ORDER BY AL.AuditID DESC");

                    var newRows = new List<AuditLogDisplayModel>();
                    using (var cmd = new SqlCommand(sql.ToString(), conn))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var item = new AuditLogDisplayModel
                                {
                                    AuditID = reader.GetInt64(reader.GetOrdinal("AuditID")),
                                    DataZmiany = reader.GetDateTime(reader.GetOrdinal("DataZmiany")),
                                    UserID = SafeGetString(reader, "UserID"),
                                    UserName = SafeGetString(reader, "UserName"),
                                    NazwaTabeli = SafeGetString(reader, "NazwaTabeli"),
                                    RekordID = SafeGetString(reader, "RekordID"),
                                    TypOperacji = SafeGetString(reader, "TypOperacji"),
                                    ZrodloZmiany = SafeGetString(reader, "ZrodloZmiany"),
                                    NazwaPola = SafeGetString(reader, "NazwaPola"),
                                    StaraWartosc = SafeGetString(reader, "StaraWartosc"),
                                    NowaWartosc = SafeGetString(reader, "NowaWartosc"),
                                    OpisZmiany = SafeGetString(reader, "OpisZmiany"),
                                    NazwaKomputera = SafeGetString(reader, "NazwaKomputera"),
                                };

                                // Hodowca: priorytetowo z DodatkoweInfo JSON, fallback do JOIN HD.Dostawca
                                string ctxJson = SafeGetString(reader, "DodatkoweInfo");
                                ExtractHodowcaFromJson(ctxJson, item);
                                if (string.IsNullOrEmpty(item.Hodowca))
                                    item.Hodowca = SafeGetString(reader, "HD_Dostawca");
                                if (!item.DataOdbioru.HasValue)
                                {
                                    int hdDataIdx = reader.GetOrdinal("HD_DataOdbioru");
                                    if (!reader.IsDBNull(hdDataIdx))
                                        item.DataOdbioru = reader.GetDateTime(hdDataIdx);
                                }

                                newRows.Add(item);
                            }
                        }
                    }

                    // Wyznacz batch (zmiany tej samej dostawy przez tego samego usera w ciągu 3 sekund)
                    AssignBatchCounts(newRows);

                    foreach (var r in newRows) _auditLogs.Add(r);

                    if (newRows.Count > 0)
                        _oldestLoadedAuditId = newRows.Min(r => r.AuditID);

                    _hasMoreData = newRows.Count >= PAGE_SIZE;
                    if (btnLoadMore != null) btnLoadMore.Visibility = _hasMoreData ? Visibility.Visible : Visibility.Collapsed;
                }

                txtResultCount.Text = _auditLogs.Count.ToString();
                txtLastRefresh.Text = DateTime.Now.ToString("HH:mm:ss");
                txtStatusInfo.Text = _hasMoreData
                    ? $"Załadowano {_auditLogs.Count} rekordów. Kliknij 'Załaduj kolejne', aby zobaczyć starsze."
                    : (_auditLogs.Count == 0 ? "Brak wyników dla aktualnych filtrów." : "Pokazano wszystkie rekordy.");

                UpdateStats();
                UpdateSparkline();
                UpdatePieChart();
            }
            catch (Exception ex)
            {
                txtStatusInfo.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania audytu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string SafeGetString(System.Data.IDataRecord r, string col)
        {
            int idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? null : r.GetString(idx);
        }

        private static void ExtractHodowcaFromJson(string json, AuditLogDisplayModel item)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Dostawca", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String)
                    item.Hodowca = d.GetString();
                if (doc.RootElement.TryGetProperty("DataOdbioru", out var od) && od.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (DateTime.TryParse(od.GetString(), out var dt)) item.DataOdbioru = dt;
                }
            }
            catch { /* nie psuj UI gdy JSON jest uszkodzony */ }
        }

        /// <summary>
        /// Wpisy tego samego usera w tej samej dostawie w odstępie ≤3s traktuj jako jedną „transakcję".
        /// Każdy wiersz dostaje BatchCount = wielkość grupy (do wizualnego sygnalizatora).
        /// </summary>
        private static void AssignBatchCounts(List<AuditLogDisplayModel> rows)
        {
            if (rows.Count == 0) return;
            var sorted = rows.OrderBy(r => r.RekordID).ThenBy(r => r.UserID).ThenBy(r => r.DataZmiany).ToList();

            int i = 0;
            while (i < sorted.Count)
            {
                int j = i + 1;
                while (j < sorted.Count
                       && sorted[j].RekordID == sorted[i].RekordID
                       && sorted[j].UserID == sorted[i].UserID
                       && (sorted[j].DataZmiany - sorted[j - 1].DataZmiany).TotalSeconds <= 3)
                {
                    j++;
                }
                int count = j - i;
                if (count > 1)
                {
                    for (int k = i; k < j; k++) sorted[k].BatchCount = count;
                }
                i = j;
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

        /// <summary>
        /// Pobiera tekst z ComboBoxa: jeśli wybrano pozycję z listy → Content, jeśli wpisano ręcznie → Text.
        /// </summary>
        private static string GetSelectedComboText(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboBoxItem cbi)
                return cbi.Content?.ToString();
            return cmb.Text?.Trim();
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
            if (cmbFilterHodowca.Items.Count > 0) cmbFilterHodowca.SelectedIndex = 0;
            cmbFilterHodowca.Text = "";
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

        private async void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            await LoadAuditLogAsync(append: true);
        }

        private void DgAuditLog_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgAuditLog.SelectedItem is AuditLogDisplayModel item && !string.IsNullOrEmpty(item.RekordID))
            {
                NavigateToDostawaRequested?.Invoke(item.RekordID, item.DataOdbioru);
            }
        }

        private void UpdateStats()
        {
            try
            {
                var today = _auditLogs.Where(a => a.DataZmiany.Date == DateTime.Today).ToList();
                int dzisCount = today.Count;
                int mojeCount = today.Count(a => a.UserID == _userId || a.UserName == _userId);

                string topUser = "-";
                int topCount = 0;
                if (today.Count > 0)
                {
                    var grouped = today
                        .GroupBy(a => string.IsNullOrEmpty(a.UserName) ? a.UserID : a.UserName)
                        .Select(g => new { User = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault();
                    if (grouped != null) { topUser = grouped.User ?? "-"; topCount = grouped.Count; }
                }

                txtStatDzis.Text = dzisCount.ToString();
                txtStatMoje.Text = mojeCount.ToString();
                txtStatTop.Text = topCount > 0 ? $"{topUser} ({topCount})" : "-";
            }
            catch { }
        }

        // Paleta segmentów wykresu (12 dystynktywnych kolorów Material)
        private static readonly string[] PiePalette =
        {
            "#1976D2", "#388E3C", "#F57C00", "#7B1FA2", "#C62828", "#00838F",
            "#5D4037", "#455A64", "#AD1457", "#558B2F", "#E65100", "#283593"
        };

        private void UpdatePieChart()
        {
            try
            {
                if (pieChartCanvas == null || icPieLegend == null) return;
                pieChartCanvas.Children.Clear();

                // Agreguj zmiany per użytkownik z rozbiciem na operacje i pola
                var grouped = _auditLogs
                    .GroupBy(a => string.IsNullOrEmpty(a.UserName) ? (a.UserID ?? "?") : a.UserName)
                    .Select(g =>
                    {
                        var topFields = g
                            .Where(x => !string.IsNullOrEmpty(x.NazwaPola))
                            .GroupBy(x => x.NazwaPolaDisplay ?? x.NazwaPola)
                            .Select(fg => new KeyValuePair<string, int>(fg.Key, fg.Count()))
                            .OrderByDescending(kv => kv.Value)
                            .Take(5)
                            .ToList();

                        return new
                        {
                            UserDisplay = g.Key,
                            UserID = g.First().UserID,
                            Count = g.Count(),
                            InsertCount = g.Count(x => x.TypOperacji == "INSERT"),
                            UpdateCount = g.Count(x => x.TypOperacji == "UPDATE"),
                            DeleteCount = g.Count(x => x.TypOperacji == "DELETE"),
                            TopFields = topFields
                        };
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                int total = grouped.Sum(g => g.Count);
                txtPieChartCenter.Text = total.ToString();
                txtPieChartTotal.Text = grouped.Count > 0
                    ? $"{grouped.Count} użytkowników, {total} zmian"
                    : "(brak danych)";

                if (total == 0)
                {
                    icPieLegend.ItemsSource = null;
                    return;
                }

                // Przygotuj segmenty z kolorami
                var segments = new List<UserPieSegment>();
                for (int i = 0; i < grouped.Count; i++)
                {
                    var g = grouped[i];
                    string color = PiePalette[i % PiePalette.Length];
                    segments.Add(new UserPieSegment
                    {
                        UserDisplay = g.UserDisplay,
                        UserID = g.UserID,
                        Count = g.Count,
                        Percent = (double)g.Count / total * 100.0,
                        Color = color,
                        InsertCount = g.InsertCount,
                        UpdateCount = g.UpdateCount,
                        DeleteCount = g.DeleteCount,
                        TopFields = g.TopFields
                    });
                }

                // Renderuj donut chart
                RenderDonutChart(pieChartCanvas, segments, total);

                // Wypełnij listę
                icPieLegend.ItemsSource = segments;
            }
            catch { }
        }

        private static void RenderDonutChart(Canvas canvas, List<UserPieSegment> segments, int total)
        {
            const double size = 180;
            const double cx = size / 2.0;
            const double cy = size / 2.0;
            const double rOuter = 80;
            const double rInner = 50;

            double currentAngle = -90.0; // start od góry

            foreach (var seg in segments)
            {
                double sweep = seg.Percent / 100.0 * 360.0;
                if (sweep < 0.5) continue; // pomijamy znikome

                double startAngle = currentAngle;
                double endAngle = currentAngle + sweep;

                var path = new System.Windows.Shapes.Path
                {
                    Fill = (Brush)new BrushConverter().ConvertFromString(seg.Color),
                    Data = BuildDonutSegment(cx, cy, rOuter, rInner, startAngle, endAngle),
                    ToolTip = $"{seg.UserDisplay}: {seg.Count} zmian ({seg.Percent:F1}%)"
                };
                canvas.Children.Add(path);

                currentAngle = endAngle;
            }
        }

        private static Geometry BuildDonutSegment(double cx, double cy, double rOuter, double rInner, double startAngleDeg, double endAngleDeg)
        {
            double DegToRad(double d) => d * Math.PI / 180.0;
            Point Pt(double r, double angleDeg) =>
                new Point(cx + r * Math.Cos(DegToRad(angleDeg)), cy + r * Math.Sin(DegToRad(angleDeg)));

            bool isLargeArc = (endAngleDeg - startAngleDeg) > 180.0;

            var p1 = Pt(rOuter, startAngleDeg);
            var p2 = Pt(rOuter, endAngleDeg);
            var p3 = Pt(rInner, endAngleDeg);
            var p4 = Pt(rInner, startAngleDeg);

            var fig = new PathFigure { StartPoint = p1, IsClosed = true };
            fig.Segments.Add(new ArcSegment(p2, new Size(rOuter, rOuter), 0, isLargeArc, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(p3, true));
            fig.Segments.Add(new ArcSegment(p4, new Size(rInner, rInner), 0, isLargeArc, SweepDirection.Counterclockwise, true));
            fig.Segments.Add(new LineSegment(p1, true));

            var geom = new PathGeometry();
            geom.Figures.Add(fig);
            return geom;
        }

        private void UpdateSparkline()
        {
            try
            {
                if (sparklineCanvas == null) return;
                sparklineCanvas.Children.Clear();

                // 24 godzinne kubełki dla ostatnich 24h
                var since = DateTime.Now.AddHours(-24);
                var buckets = new int[24];
                foreach (var a in _auditLogs)
                {
                    if (a.DataZmiany < since) continue;
                    int hoursAgo = (int)(DateTime.Now - a.DataZmiany).TotalHours;
                    if (hoursAgo < 0 || hoursAgo > 23) continue;
                    buckets[23 - hoursAgo]++;
                }

                int max = buckets.Max();
                if (max == 0) return;

                double w = sparklineCanvas.ActualWidth > 0 ? sparklineCanvas.ActualWidth : 220;
                double h = sparklineCanvas.ActualHeight > 0 ? sparklineCanvas.ActualHeight : 30;
                double barW = w / 24.0;

                for (int i = 0; i < 24; i++)
                {
                    double bh = (buckets[i] / (double)max) * (h - 2);
                    var bar = new System.Windows.Shapes.Rectangle
                    {
                        Width = Math.Max(1, barW - 1),
                        Height = Math.Max(1, bh),
                        Fill = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                        ToolTip = $"{buckets[i]} zmian"
                    };
                    System.Windows.Controls.Canvas.SetLeft(bar, i * barW);
                    System.Windows.Controls.Canvas.SetTop(bar, h - bh);
                    sparklineCanvas.Children.Add(bar);
                }
            }
            catch { }
        }

        #endregion
    }

    /// <summary>
    /// Konwerter UserID → ImageBrush (zdjęcie z UserAvatarManager). Cache + freeze.
    /// Gdy user nie ma zdjęcia — zwraca null (Ellipse pozostaje przezroczysty, widać pod nim inicjały).
    /// </summary>
    public class UserAvatarBrushConverter : System.Windows.Data.IValueConverter
    {
        private static readonly Dictionary<string, ImageBrush> _brushCache = new Dictionary<string, ImageBrush>();
        private static readonly HashSet<string> _missingCache = new HashSet<string>();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string userId = value?.ToString();
            if (string.IsNullOrEmpty(userId)) return null;
            if (_missingCache.Contains(userId)) return null;

            if (_brushCache.TryGetValue(userId, out var brush)) return brush;

            try
            {
                if (!UserAvatarManager.HasAvatar(userId))
                {
                    _missingCache.Add(userId);
                    return null;
                }
                using (var img = UserAvatarManager.GetAvatarRounded(userId, 60))
                {
                    if (img == null)
                    {
                        _missingCache.Add(userId);
                        return null;
                    }
                    using (var memory = new MemoryStream())
                    {
                        img.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                        memory.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = memory;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        var b = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                        b.Freeze();
                        _brushCache[userId] = b;
                        return b;
                    }
                }
            }
            catch
            {
                _missingCache.Add(userId);
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
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
        public string Hodowca { get; set; }
        public DateTime? DataOdbioru { get; set; }
        public int BatchCount { get; set; } = 1;

        public string DataZmianyFormatted => DataZmiany.ToString("yyyy-MM-dd HH:mm:ss");

        public string DataZmianyShort => DataZmiany.ToString("HH:mm:ss");
        public string DataZmianyDateShort => DataZmiany.ToString("dd.MM.yyyy");
        public string DataZmianyDateLong
        {
            get
            {
                string dzien = DniSkrot[(int)DataZmiany.DayOfWeek];
                return $"{dzien} {DataZmiany:dd.MM.yyyy}";
            }
        }

        private static readonly string[] DniSkrot = { "niedz", "pon", "wt", "śr", "czw", "pt", "sob" };

        public string DataOdbioruDisplay
        {
            get
            {
                if (!DataOdbioru.HasValue) return "-";
                string dzien = DniSkrot[(int)DataOdbioru.Value.DayOfWeek];
                return $"{dzien} {DataOdbioru.Value:dd.MM.yyyy}";
            }
        }


        public string DataZmianyRelative
        {
            get
            {
                var diff = DateTime.Now - DataZmiany;
                if (diff.TotalSeconds < 60) return "przed chwilą";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
                if (diff.TotalHours < 24 && DataZmiany.Date == DateTime.Today) return $"{(int)diff.TotalHours} h temu";
                if (DataZmiany.Date == DateTime.Today.AddDays(-1)) return $"wczoraj {DataZmiany:HH:mm}";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
                return DataZmiany.ToString("dd.MM.yyyy");
            }
        }

        public string HodowcaDisplay => string.IsNullOrEmpty(Hodowca) ? "-" : Hodowca;

        public bool IsPartOfBatch => BatchCount > 1;
        public string BatchBadge => IsPartOfBatch ? $"⚭{BatchCount}" : "";

        // Wartości pillsów — gdy JSON notatki, pokaż tylko 'Tresc'
        public string StaraWartoscDisplay => ExtractTrescOrSelf(StaraWartosc);
        public string NowaWartoscDisplay => ExtractTrescOrSelf(NowaWartosc);

        // Flaga: czy ten audyt dotyczy notatki (wpływa na styl czcionki)
        public bool IsNote =>
            (string.Equals(NazwaTabeli, "Notatki", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(NazwaPola, "Tresc", StringComparison.OrdinalIgnoreCase))
            || LooksLikeJsonNote(StaraWartosc) || LooksLikeJsonNote(NowaWartosc);

        public bool HasLongContent =>
            (StaraWartoscDisplay?.Length ?? 0) > 40 || (NowaWartoscDisplay?.Length ?? 0) > 40;

        private static bool LooksLikeJsonNote(string s)
            => !string.IsNullOrEmpty(s) && s.TrimStart().StartsWith("{") && s.Contains("\"Tresc\"");

        private static string ExtractTrescOrSelf(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var trimmed = s.TrimStart();
            if (!trimmed.StartsWith("{")) return s;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("Tresc", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                    return t.GetString();
            }
            catch { }
            return s;
        }

        public string UserDisplay => !string.IsNullOrEmpty(UserName) ? UserName : UserID;

        public string TypOperacjiDisplay => TypOperacji switch
        {
            "INSERT" => "Dodanie",
            "UPDATE" => "Zmiana",
            "DELETE" => "Usunięcie",
            _ => TypOperacji
        };

        public string TypOperacjiColor => TypOperacji switch
        {
            "INSERT" => "#2E7D32",   // zielony
            "UPDATE" => "#F57C00",   // pomarańczowy
            "DELETE" => "#C62828",   // czerwony
            _ => "#616161"
        };

        public string TypOperacjiBackground => TypOperacji switch
        {
            "INSERT" => "#E8F5E9",
            "UPDATE" => "#FFF3E0",
            "DELETE" => "#FFEBEE",
            _ => "#F5F5F5"
        };

        public string TypOperacjiIcon => TypOperacji switch
        {
            "INSERT" => "➕",
            "UPDATE" => "✏️",
            "DELETE" => "🗑",
            _ => "•"
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
    /// Segment wykresu kołowego (kto co zmieniał) z rozbiciem operacji i pól.
    /// </summary>
    public class UserPieSegment
    {
        public string UserDisplay { get; set; }
        public string UserID { get; set; }
        public int Count { get; set; }
        public double Percent { get; set; }
        public string Color { get; set; }

        // Rozbicie po typie operacji
        public int InsertCount { get; set; }
        public int UpdateCount { get; set; }
        public int DeleteCount { get; set; }

        // Top pola które edytował (pole → liczba zmian), max 5
        public List<KeyValuePair<string, int>> TopFields { get; set; } = new List<KeyValuePair<string, int>>();

        public string PercentDisplay => $"{Percent:F1}%";

        public string OperationsBreakdown
        {
            get
            {
                var parts = new List<string>();
                if (InsertCount > 0) parts.Add($"➕ {InsertCount}");
                if (UpdateCount > 0) parts.Add($"✏️ {UpdateCount}");
                if (DeleteCount > 0) parts.Add($"🗑 {DeleteCount}");
                return string.Join("  ", parts);
            }
        }

        public string TopFieldsBreakdown
        {
            get
            {
                if (TopFields == null || TopFields.Count == 0) return "";
                return string.Join("  ·  ", TopFields.Take(3).Select(kv => $"{kv.Key} ({kv.Value})"));
            }
        }

        public string FullTooltip
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Użytkownik: {UserDisplay}");
                sb.AppendLine($"Łącznie zmian: {Count} ({PercentDisplay})");
                sb.AppendLine();
                sb.AppendLine("Operacje:");
                if (InsertCount > 0) sb.AppendLine($"  ➕ Dodanie: {InsertCount}");
                if (UpdateCount > 0) sb.AppendLine($"  ✏️ Zmiana: {UpdateCount}");
                if (DeleteCount > 0) sb.AppendLine($"  🗑 Usunięcie: {DeleteCount}");
                if (TopFields != null && TopFields.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Edytowane pola:");
                    foreach (var kv in TopFields)
                        sb.AppendLine($"  • {kv.Key}: {kv.Value}");
                }
                return sb.ToString().TrimEnd();
            }
        }

        public Brush ColorBrush
        {
            get
            {
                try
                {
                    var b = (SolidColorBrush)new BrushConverter().ConvertFromString(Color);
                    b.Freeze();
                    return b;
                }
                catch { return Brushes.Gray; }
            }
        }
    }

    #endregion
}
