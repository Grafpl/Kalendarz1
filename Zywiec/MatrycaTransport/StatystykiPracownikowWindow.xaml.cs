using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class StatystykiPracownikowWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public StatystykiPracownikowWindow()
        {
            InitializeComponent();

            // Domyślny zakres dat - ostatnie 30 dni
            dpDo.SelectedDate = DateTime.Today;
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);

            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            DateTime dateFrom = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dateTo = dpDo.SelectedDate ?? DateTime.Today;

            // Upewnij się że dateTo obejmuje cały dzień
            dateTo = dateTo.Date.AddDays(1).AddSeconds(-1);

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

        private void LoadSmsStatistics(DateTime dateFrom, DateTime dateTo)
        {
            var stats = new List<UserSmsStats>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Sprawdź czy tabela istnieje
                string checkTableSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    if (tableExists == 0)
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
                            stats.Add(new UserSmsStats
                            {
                                Rank = rank++,
                                UserId = reader["SentByUser"]?.ToString() ?? "-",
                                SmsCount = Convert.ToInt32(reader["TotalSms"]),
                                SmsAllCount = Convert.ToInt32(reader["SmsAll"]),
                                SmsOneCount = Convert.ToInt32(reader["SmsOne"]),
                                UniqueFarmers = Convert.ToInt32(reader["UniqueFarmers"])
                            });
                        }
                    }
                }
            }

            dgSmsStats.ItemsSource = stats;

            // Przygotuj dane dla wykresu (max 10 pracowników)
            int maxSms = stats.Any() ? stats.Max(s => s.SmsCount) : 1;
            var chartData = stats.Take(10).Select(s => new ChartBarData
            {
                UserId = s.UserId,
                SmsCount = s.SmsCount,
                BarHeight = maxSms > 0 ? (double)s.SmsCount / maxSms * 200 : 0,
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

                // Sprawdź czy tabela istnieje
                string checkTableSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    if (tableExists == 0)
                    {
                        dgConfirmations.ItemsSource = stats;
                        return;
                    }
                }

                string query = @"
                    SELECT
                        AcknowledgedByUser,
                        COUNT(*) AS TotalConfirmations
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

                // Pobierz ostatnie SMS-y
                string checkSmsTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkSmsTable, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    if (tableExists > 0)
                    {
                        string smsQuery = @"
                            SELECT TOP 30 SentDate, SentByUser, 'SMS' AS ActionType
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

                // Pobierz ostatnie potwierdzenia
                string checkLogTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkLogTable, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    if (tableExists > 0)
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

            // Sortuj po dacie i weź 50 ostatnich
            dgRecentActivity.ItemsSource = activities
                .OrderByDescending(a => a.Date)
                .Take(50)
                .ToList();
        }

        private void UpdateSummaryCards(DateTime dateFrom, DateTime dateTo)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Suma SMS
                int totalSms = 0;
                int activeUsers = 0;
                int uniqueFarmers = 0;

                string checkSmsTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                using (SqlCommand checkCmd = new SqlCommand(checkSmsTable, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    if (tableExists > 0)
                    {
                        string smsStatsQuery = @"
                            SELECT
                                COUNT(*) AS TotalSms,
                                COUNT(DISTINCT SentByUser) AS ActiveUsers,
                                COUNT(DISTINCT CustomerGID) AS UniqueFarmers
                            FROM dbo.SmsHistory
                            WHERE SentDate >= @DateFrom AND SentDate <= @DateTo";

                        using (SqlCommand cmd = new SqlCommand(smsStatsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                            cmd.Parameters.AddWithValue("@DateTo", dateTo);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    totalSms = Convert.ToInt32(reader["TotalSms"]);
                                    activeUsers = Convert.ToInt32(reader["ActiveUsers"]);
                                    uniqueFarmers = Convert.ToInt32(reader["UniqueFarmers"]);
                                }
                            }
                        }
                    }
                }

                // Suma potwierdzeń
                int totalConfirmations = 0;
                string checkLogTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                using (SqlCommand checkCmd = new SqlCommand(checkLogTable, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    if (tableExists > 0)
                    {
                        string confQuery = @"
                            SELECT COUNT(*) AS Total
                            FROM dbo.SmsChangeLog
                            WHERE AcknowledgedDate >= @DateFrom AND AcknowledgedDate <= @DateTo";

                        using (SqlCommand cmd = new SqlCommand(confQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                            cmd.Parameters.AddWithValue("@DateTo", dateTo);
                            totalConfirmations = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                }

                lblTotalSms.Text = totalSms.ToString("N0");
                lblActiveUsers.Text = activeUsers.ToString();
                lblTotalConfirmations.Text = totalConfirmations.ToString("N0");
                lblUniqueFarmers.Text = uniqueFarmers.ToString("N0");
            }
        }

        private Brush GetBarColor(int rank)
        {
            switch (rank)
            {
                case 1: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")); // Złoty
                case 2: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0")); // Srebrny
                case 3: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD7F32")); // Brązowy
                default: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")); // Niebieski
            }
        }
    }

    // Klasy pomocnicze dla danych
    public class UserSmsStats
    {
        public int Rank { get; set; }
        public string UserId { get; set; }
        public int SmsCount { get; set; }
        public int SmsAllCount { get; set; }
        public int SmsOneCount { get; set; }
        public int UniqueFarmers { get; set; }
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
}
