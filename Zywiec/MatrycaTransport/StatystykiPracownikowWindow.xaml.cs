using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class StatystykiPracownikowWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private List<string> allUsers = new List<string>();
        private int totalDaysInRange = 1;

        public StatystykiPracownikowWindow()
        {
            InitializeComponent();

            // Domyślny zakres dat - ostatnie 30 dni
            dpDo.SelectedDate = DateTime.Today;
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpPorownanieOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpPorownanieDo.SelectedDate = DateTime.Today;

            // Wypełnij combobox lat
            int currentYear = DateTime.Today.Year;
            for (int y = currentYear; y >= currentYear - 5; y--)
            {
                cboRokTygodnie.Items.Add(y);
                cboRokMiesiace.Items.Add(y);
            }
            cboRokTygodnie.SelectedIndex = 0;
            cboRokMiesiace.SelectedIndex = 0;

            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
            LoadAllUsers();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Szybki wybór dat

        private void BtnDzis_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = DateTime.Today;
            dpDo.SelectedDate = DateTime.Today;
            LoadStatistics();
        }

        private void BtnTydzien_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = DateTime.Today.AddDays(-7);
            dpDo.SelectedDate = DateTime.Today;
            LoadStatistics();
        }

        private void BtnMiesiac_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDo.SelectedDate = DateTime.Today;
            LoadStatistics();
        }

        private void BtnRok_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
            dpDo.SelectedDate = DateTime.Today;
            LoadStatistics();
        }

        #endregion

        #region Główne statystyki

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            DateTime dateFrom = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dateTo = dpDo.SelectedDate ?? DateTime.Today;
            dateTo = dateTo.Date.AddDays(1).AddSeconds(-1);
            totalDaysInRange = Math.Max(1, (int)(dateTo.Date - dateFrom.Date).TotalDays + 1);

            try
            {
                LoadSmsStatistics(dateFrom, dateTo);
                LoadConfirmationStatistics(dateFrom, dateTo);
                LoadRecentActivity(dateFrom, dateTo);
                UpdateSummaryCards(dateFrom, dateTo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania statystyk:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAllUsers()
        {
            allUsers.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() == 0) return;
                    }

                    string query = "SELECT DISTINCT SentByUser FROM dbo.SmsHistory ORDER BY SentByUser";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string user = reader["SentByUser"]?.ToString();
                            if (!string.IsNullOrEmpty(user))
                                allUsers.Add(user);
                        }
                    }
                }

                cboPracownik1.ItemsSource = allUsers;
                cboPracownik2.ItemsSource = allUsers;
                if (allUsers.Count > 0) cboPracownik1.SelectedIndex = 0;
                if (allUsers.Count > 1) cboPracownik2.SelectedIndex = 1;
            }
            catch { }
        }

        private void LoadSmsStatistics(DateTime dateFrom, DateTime dateTo)
        {
            var stats = new List<UserSmsStats>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkTableSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() == 0)
                    {
                        dgSmsStats.ItemsSource = stats;
                        chartBars.ItemsSource = stats;
                        return;
                    }
                }

                string query = @"
                    SELECT
                        SentByUser,
                        COUNT(*) AS TotalSms,
                        SUM(CASE WHEN SmsType = 'ALL' THEN 1 ELSE 0 END) AS SmsAll,
                        SUM(CASE WHEN SmsType = 'ONE' THEN 1 ELSE 0 END) AS SmsOne,
                        COUNT(DISTINCT CustomerGID) AS UniqueFarmers
                    FROM dbo.SmsHistory
                    WHERE SentDate >= @DateFrom AND SentDate <= @DateTo
                    GROUP BY SentByUser
                    ORDER BY TotalSms DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                    cmd.Parameters.AddWithValue("@DateTo", dateTo);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        int rank = 1;
                        while (reader.Read())
                        {
                            int smsCount = Convert.ToInt32(reader["TotalSms"]);
                            stats.Add(new UserSmsStats
                            {
                                Rank = rank++,
                                UserId = reader["SentByUser"]?.ToString() ?? "-",
                                SmsCount = smsCount,
                                SmsAllCount = Convert.ToInt32(reader["SmsAll"]),
                                SmsOneCount = Convert.ToInt32(reader["SmsOne"]),
                                UniqueFarmers = Convert.ToInt32(reader["UniqueFarmers"]),
                                AvgPerDay = Math.Round((double)smsCount / totalDaysInRange, 1)
                            });
                        }
                    }
                }
            }

            dgSmsStats.ItemsSource = stats;

            // Wykres (max 8 pracowników)
            int maxSms = stats.Any() ? stats.Max(s => s.SmsCount) : 1;
            var chartData = stats.Take(8).Select(s => new ChartBarData
            {
                UserId = s.UserId,
                SmsCount = s.SmsCount,
                BarHeight = maxSms > 0 ? (double)s.SmsCount / maxSms * 130 : 0,
                BarColor = GetBarColor(s.Rank)
            }).ToList();

            chartBars.ItemsSource = chartData;
        }

        private void LoadConfirmationStatistics(DateTime dateFrom, DateTime dateTo)
        {
            var stats = new List<UserConfirmationStats>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkTableSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() == 0)
                    {
                        dgConfirmations.ItemsSource = stats;
                        return;
                    }
                }

                string query = @"
                    SELECT AcknowledgedByUser, COUNT(*) AS TotalConfirmations
                    FROM dbo.SmsChangeLog
                    WHERE AcknowledgedDate >= @DateFrom AND AcknowledgedDate <= @DateTo
                    GROUP BY AcknowledgedByUser
                    ORDER BY TotalConfirmations DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                    cmd.Parameters.AddWithValue("@DateTo", dateTo);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stats.Add(new UserConfirmationStats
                            {
                                UserId = reader["AcknowledgedByUser"]?.ToString() ?? "-",
                                Count = Convert.ToInt32(reader["TotalConfirmations"])
                            });
                        }
                    }
                }
            }

            dgConfirmations.ItemsSource = stats;
        }

        private void LoadRecentActivity(DateTime dateFrom, DateTime dateTo)
        {
            var activities = new List<RecentActivity>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // SMS
                string checkSmsTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkSmsTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        string smsQuery = @"
                            SELECT TOP 40 SentDate, SentByUser, 'SMS' AS ActionType
                            FROM dbo.SmsHistory
                            WHERE SentDate >= @DateFrom AND SentDate <= @DateTo
                            ORDER BY SentDate DESC";

                        using (SqlCommand cmd = new SqlCommand(smsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                            cmd.Parameters.AddWithValue("@DateTo", dateTo);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DateTime date = Convert.ToDateTime(reader["SentDate"]);
                                    activities.Add(new RecentActivity
                                    {
                                        Date = date,
                                        DateStr = date.ToString("dd.MM"),
                                        TimeStr = date.ToString("HH:mm"),
                                        UserId = reader["SentByUser"]?.ToString() ?? "-",
                                        ActionType = "SMS"
                                    });
                                }
                            }
                        }
                    }
                }

                // Potwierdzenia
                string checkLogTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkLogTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        string logQuery = @"
                            SELECT TOP 20 AcknowledgedDate, AcknowledgedByUser, 'POT' AS ActionType
                            FROM dbo.SmsChangeLog
                            WHERE AcknowledgedDate >= @DateFrom AND AcknowledgedDate <= @DateTo
                            ORDER BY AcknowledgedDate DESC";

                        using (SqlCommand cmd = new SqlCommand(logQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                            cmd.Parameters.AddWithValue("@DateTo", dateTo);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DateTime date = Convert.ToDateTime(reader["AcknowledgedDate"]);
                                    activities.Add(new RecentActivity
                                    {
                                        Date = date,
                                        DateStr = date.ToString("dd.MM"),
                                        TimeStr = date.ToString("HH:mm"),
                                        UserId = reader["AcknowledgedByUser"]?.ToString() ?? "-",
                                        ActionType = "POT"
                                    });
                                }
                            }
                        }
                    }
                }
            }

            dgRecentActivity.ItemsSource = activities.OrderByDescending(a => a.Date).Take(60).ToList();
        }

        private void UpdateSummaryCards(DateTime dateFrom, DateTime dateTo)
        {
            int totalSms = 0, smsAll = 0, activeUsers = 0, uniqueFarmers = 0, totalConfirmations = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkSmsTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkSmsTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        string query = @"
                            SELECT
                                COUNT(*) AS TotalSms,
                                SUM(CASE WHEN SmsType = 'ALL' THEN 1 ELSE 0 END) AS SmsAll,
                                COUNT(DISTINCT SentByUser) AS ActiveUsers,
                                COUNT(DISTINCT CustomerGID) AS UniqueFarmers
                            FROM dbo.SmsHistory
                            WHERE SentDate >= @DateFrom AND SentDate <= @DateTo";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                            cmd.Parameters.AddWithValue("@DateTo", dateTo);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    totalSms = Convert.ToInt32(reader["TotalSms"]);
                                    smsAll = Convert.ToInt32(reader["SmsAll"]);
                                    activeUsers = Convert.ToInt32(reader["ActiveUsers"]);
                                    uniqueFarmers = Convert.ToInt32(reader["UniqueFarmers"]);
                                }
                            }
                        }
                    }
                }

                string checkLogTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkLogTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        string query = "SELECT COUNT(*) FROM dbo.SmsChangeLog WHERE AcknowledgedDate >= @DateFrom AND AcknowledgedDate <= @DateTo";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                            cmd.Parameters.AddWithValue("@DateTo", dateTo);
                            totalConfirmations = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                }
            }

            lblTotalSms.Text = totalSms.ToString("N0");
            lblSmsAll.Text = smsAll.ToString("N0");
            lblActiveUsers.Text = activeUsers.ToString();
            lblTotalConfirmations.Text = totalConfirmations.ToString("N0");
            lblUniqueFarmers.Text = uniqueFarmers.ToString("N0");
        }

        private void DgSmsStats_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgSmsStats.SelectedItem is UserSmsStats selectedUser)
            {
                MessageBox.Show(
                    $"Szczegóły pracownika: {selectedUser.UserId}\n\n" +
                    $"SMS wysłanych: {selectedUser.SmsCount}\n" +
                    $"  - Zbiorczych: {selectedUser.SmsAllCount}\n" +
                    $"  - Pojedynczych: {selectedUser.SmsOneCount}\n\n" +
                    $"Hodowców obsłużonych: {selectedUser.UniqueFarmers}\n" +
                    $"Średnio dziennie: {selectedUser.AvgPerDay:F1} SMS",
                    $"Statystyki - {selectedUser.UserId}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Statystyki Tygodniowe

        private void CboRokTygodnie_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadWeeklyStatistics();
        }

        private void BtnRefreshTygodnie_Click(object sender, RoutedEventArgs e)
        {
            LoadWeeklyStatistics();
        }

        private void LoadWeeklyStatistics()
        {
            if (cboRokTygodnie.SelectedItem == null) return;
            int year = (int)cboRokTygodnie.SelectedItem;
            var stats = new List<WeeklyStats>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() == 0)
                    {
                        dgWeeklyStats.ItemsSource = stats;
                        return;
                    }
                }

                string query = @"
                    SELECT
                        DATEPART(WEEK, SentDate) AS WeekNum,
                        MIN(CAST(SentDate AS DATE)) AS WeekStart,
                        MAX(CAST(SentDate AS DATE)) AS WeekEnd,
                        COUNT(*) AS TotalSms,
                        SUM(CASE WHEN SmsType = 'ALL' THEN 1 ELSE 0 END) AS SmsAll,
                        SUM(CASE WHEN SmsType = 'ONE' THEN 1 ELSE 0 END) AS SmsOne,
                        COUNT(DISTINCT SentByUser) AS ActiveUsers,
                        COUNT(DISTINCT CustomerGID) AS UniqueFarmers
                    FROM dbo.SmsHistory
                    WHERE YEAR(SentDate) = @Year
                    GROUP BY DATEPART(WEEK, SentDate)
                    ORDER BY WeekNum";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int weekNum = Convert.ToInt32(reader["WeekNum"]);
                            DateTime weekStart = Convert.ToDateTime(reader["WeekStart"]);
                            DateTime weekEnd = Convert.ToDateTime(reader["WeekEnd"]);
                            int totalSms = Convert.ToInt32(reader["TotalSms"]);
                            int days = Math.Max(1, (int)(weekEnd - weekStart).TotalDays + 1);

                            stats.Add(new WeeklyStats
                            {
                                WeekNumber = $"Tydz. {weekNum}",
                                Period = $"{weekStart:dd.MM} - {weekEnd:dd.MM}",
                                TotalSms = totalSms,
                                SmsAll = Convert.ToInt32(reader["SmsAll"]),
                                SmsOne = Convert.ToInt32(reader["SmsOne"]),
                                ActiveUsers = Convert.ToInt32(reader["ActiveUsers"]),
                                UniqueFarmers = Convert.ToInt32(reader["UniqueFarmers"]),
                                Confirmations = 0,
                                AvgPerDay = Math.Round((double)totalSms / days, 1)
                            });
                        }
                    }
                }

                // Dodaj potwierdzenia
                string checkLog = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkLog, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        string confQuery = @"
                            SELECT DATEPART(WEEK, AcknowledgedDate) AS WeekNum, COUNT(*) AS Cnt
                            FROM dbo.SmsChangeLog
                            WHERE YEAR(AcknowledgedDate) = @Year
                            GROUP BY DATEPART(WEEK, AcknowledgedDate)";

                        using (SqlCommand cmd = new SqlCommand(confQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Year", year);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int weekNum = Convert.ToInt32(reader["WeekNum"]);
                                    int count = Convert.ToInt32(reader["Cnt"]);
                                    var stat = stats.FirstOrDefault(s => s.WeekNumber == $"Tydz. {weekNum}");
                                    if (stat != null) stat.Confirmations = count;
                                }
                            }
                        }
                    }
                }
            }

            dgWeeklyStats.ItemsSource = stats;
        }

        #endregion

        #region Statystyki Miesięczne

        private void CboRokMiesiace_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadMonthlyStatistics();
        }

        private void BtnRefreshMiesiace_Click(object sender, RoutedEventArgs e)
        {
            LoadMonthlyStatistics();
        }

        private void LoadMonthlyStatistics()
        {
            if (cboRokMiesiace.SelectedItem == null) return;
            int year = (int)cboRokMiesiace.SelectedItem;
            var stats = new List<MonthlyStats>();

            string[] monthNames = { "", "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
                                    "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };
            string[] shortNames = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() == 0)
                    {
                        dgMonthlyStats.ItemsSource = stats;
                        chartMonthly.ItemsSource = stats;
                        return;
                    }
                }

                string query = @"
                    SELECT
                        MONTH(SentDate) AS MonthNum,
                        COUNT(*) AS TotalSms,
                        SUM(CASE WHEN SmsType = 'ALL' THEN 1 ELSE 0 END) AS SmsAll,
                        SUM(CASE WHEN SmsType = 'ONE' THEN 1 ELSE 0 END) AS SmsOne,
                        COUNT(DISTINCT SentByUser) AS ActiveUsers,
                        COUNT(DISTINCT CustomerGID) AS UniqueFarmers
                    FROM dbo.SmsHistory
                    WHERE YEAR(SentDate) = @Year
                    GROUP BY MONTH(SentDate)
                    ORDER BY MonthNum";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int month = Convert.ToInt32(reader["MonthNum"]);
                            int totalSms = Convert.ToInt32(reader["TotalSms"]);
                            int daysInMonth = DateTime.DaysInMonth(year, month);

                            stats.Add(new MonthlyStats
                            {
                                MonthNum = month,
                                MonthName = monthNames[month],
                                ShortName = shortNames[month],
                                TotalSms = totalSms,
                                SmsAll = Convert.ToInt32(reader["SmsAll"]),
                                SmsOne = Convert.ToInt32(reader["SmsOne"]),
                                ActiveUsers = Convert.ToInt32(reader["ActiveUsers"]),
                                UniqueFarmers = Convert.ToInt32(reader["UniqueFarmers"]),
                                Confirmations = 0,
                                AvgPerDay = Math.Round((double)totalSms / daysInMonth, 1)
                            });
                        }
                    }
                }

                // Dodaj potwierdzenia
                string checkLog = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkLog, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        string confQuery = @"
                            SELECT MONTH(AcknowledgedDate) AS MonthNum, COUNT(*) AS Cnt
                            FROM dbo.SmsChangeLog
                            WHERE YEAR(AcknowledgedDate) = @Year
                            GROUP BY MONTH(AcknowledgedDate)";

                        using (SqlCommand cmd = new SqlCommand(confQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Year", year);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int month = Convert.ToInt32(reader["MonthNum"]);
                                    int count = Convert.ToInt32(reader["Cnt"]);
                                    var stat = stats.FirstOrDefault(s => s.MonthNum == month);
                                    if (stat != null) stat.Confirmations = count;
                                }
                            }
                        }
                    }
                }
            }

            dgMonthlyStats.ItemsSource = stats;

            // Wykres miesięczny
            int maxSms = stats.Any() ? stats.Max(s => s.TotalSms) : 1;
            foreach (var s in stats)
            {
                s.BarHeight = maxSms > 0 ? (double)s.TotalSms / maxSms * 120 : 0;
                s.BarColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
            }
            chartMonthly.ItemsSource = stats;
        }

        #endregion

        #region Porównanie pracowników

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            if (cboPracownik1.SelectedItem == null || cboPracownik2.SelectedItem == null)
            {
                MessageBox.Show("Wybierz dwóch pracowników do porównania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string user1 = cboPracownik1.SelectedItem.ToString();
            string user2 = cboPracownik2.SelectedItem.ToString();
            DateTime dateFrom = dpPorownanieOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dateTo = (dpPorownanieDo.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);

            var stats1 = GetUserStats(user1, dateFrom, dateTo);
            var stats2 = GetUserStats(user2, dateFrom, dateTo);

            lblPracownik1Nazwa.Text = user1;
            lblP1Sms.Text = stats1.SmsCount.ToString();
            lblP1All.Text = stats1.SmsAllCount.ToString();
            lblP1One.Text = stats1.SmsOneCount.ToString();
            lblP1Farmers.Text = stats1.UniqueFarmers.ToString();

            lblPracownik2Nazwa.Text = user2;
            lblP2Sms.Text = stats2.SmsCount.ToString();
            lblP2All.Text = stats2.SmsAllCount.ToString();
            lblP2One.Text = stats2.SmsOneCount.ToString();
            lblP2Farmers.Text = stats2.UniqueFarmers.ToString();

            comparisonPlaceholder.Visibility = Visibility.Collapsed;
            comparisonGrid.Visibility = Visibility.Visible;
        }

        private UserSmsStats GetUserStats(string userId, DateTime dateFrom, DateTime dateTo)
        {
            var stats = new UserSmsStats { UserId = userId };

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                {
                    if ((int)checkCmd.ExecuteScalar() == 0) return stats;
                }

                string query = @"
                    SELECT
                        COUNT(*) AS TotalSms,
                        SUM(CASE WHEN SmsType = 'ALL' THEN 1 ELSE 0 END) AS SmsAll,
                        SUM(CASE WHEN SmsType = 'ONE' THEN 1 ELSE 0 END) AS SmsOne,
                        COUNT(DISTINCT CustomerGID) AS UniqueFarmers
                    FROM dbo.SmsHistory
                    WHERE SentByUser = @UserId AND SentDate >= @DateFrom AND SentDate <= @DateTo";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                    cmd.Parameters.AddWithValue("@DateTo", dateTo);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.SmsCount = reader["TotalSms"] != DBNull.Value ? Convert.ToInt32(reader["TotalSms"]) : 0;
                            stats.SmsAllCount = reader["SmsAll"] != DBNull.Value ? Convert.ToInt32(reader["SmsAll"]) : 0;
                            stats.SmsOneCount = reader["SmsOne"] != DBNull.Value ? Convert.ToInt32(reader["SmsOne"]) : 0;
                            stats.UniqueFarmers = reader["UniqueFarmers"] != DBNull.Value ? Convert.ToInt32(reader["UniqueFarmers"]) : 0;
                        }
                    }
                }
            }

            return stats;
        }

        #endregion

        #region Helpers

        private Brush GetBarColor(int rank)
        {
            switch (rank)
            {
                case 1: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
                case 2: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0"));
                case 3: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD7F32"));
                default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
            }
        }

        #endregion
    }

    #region Data Classes

    public class UserSmsStats
    {
        public int Rank { get; set; }
        public string UserId { get; set; }
        public int SmsCount { get; set; }
        public int SmsAllCount { get; set; }
        public int SmsOneCount { get; set; }
        public int UniqueFarmers { get; set; }
        public double AvgPerDay { get; set; }
    }

    public class UserConfirmationStats
    {
        public string UserId { get; set; }
        public int Count { get; set; }
    }

    public class RecentActivity
    {
        public DateTime Date { get; set; }
        public string DateStr { get; set; }
        public string TimeStr { get; set; }
        public string UserId { get; set; }
        public string ActionType { get; set; }
    }

    public class ChartBarData
    {
        public string UserId { get; set; }
        public int SmsCount { get; set; }
        public double BarHeight { get; set; }
        public Brush BarColor { get; set; }
    }

    public class WeeklyStats
    {
        public string WeekNumber { get; set; }
        public string Period { get; set; }
        public int TotalSms { get; set; }
        public int SmsAll { get; set; }
        public int SmsOne { get; set; }
        public int ActiveUsers { get; set; }
        public int UniqueFarmers { get; set; }
        public int Confirmations { get; set; }
        public double AvgPerDay { get; set; }
    }

    public class MonthlyStats
    {
        public int MonthNum { get; set; }
        public string MonthName { get; set; }
        public string ShortName { get; set; }
        public int TotalSms { get; set; }
        public int SmsAll { get; set; }
        public int SmsOne { get; set; }
        public int ActiveUsers { get; set; }
        public int UniqueFarmers { get; set; }
        public int Confirmations { get; set; }
        public double AvgPerDay { get; set; }
        public double BarHeight { get; set; }
        public Brush BarColor { get; set; }
    }

    #endregion
}
