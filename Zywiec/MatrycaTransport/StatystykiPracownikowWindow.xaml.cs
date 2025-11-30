using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1
{
    public partial class StatystykiPracownikowWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DateTime dateFrom;
        private DateTime dateTo;

        // Kolory (jak w screenshocie - zielony, niebieski, żółty, fioletowy, pomarańczowy)
        private readonly string[] chartColors = new string[]
        {
            "#4CAF50", "#2196F3", "#FFC107", "#9C27B0", "#FF5722",
            "#00BCD4", "#795548", "#607D8B", "#E91E63", "#3F51B5"
        };

        public StatystykiPracownikowWindow()
        {
            InitializeComponent();

            // Domyślny zakres - ten miesiąc
            SetThisMonth();

            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllData();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Wybór okresu

        private void SetDateRange(DateTime from, DateTime to)
        {
            dateFrom = from.Date;
            dateTo = to.Date.AddDays(1).AddSeconds(-1);
            LoadAllData();
        }

        private void SetThisMonth()
        {
            var today = DateTime.Today;
            dateFrom = new DateTime(today.Year, today.Month, 1);
            dateTo = today.AddDays(1).AddSeconds(-1);
        }

        private void BtnDzis_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(DateTime.Today, DateTime.Today);
        }

        private void BtnWczoraj_Click(object sender, RoutedEventArgs e)
        {
            var yesterday = DateTime.Today.AddDays(-1);
            SetDateRange(yesterday, yesterday);
        }

        private void BtnTenTydzien_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff);
            SetDateRange(monday, today);
        }

        private void BtnPoprzedniTydzien_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var thisMonday = today.AddDays(-diff);
            var prevMonday = thisMonday.AddDays(-7);
            var prevSunday = thisMonday.AddDays(-1);
            SetDateRange(prevMonday, prevSunday);
        }

        private void BtnTenMiesiac_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var firstDay = new DateTime(today.Year, today.Month, 1);
            SetDateRange(firstDay, today);
        }

        private void BtnPoprzedniMiesiac_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var firstDayThisMonth = new DateTime(today.Year, today.Month, 1);
            var lastDayPrevMonth = firstDayThisMonth.AddDays(-1);
            var firstDayPrevMonth = new DateTime(lastDayPrevMonth.Year, lastDayPrevMonth.Month, 1);
            SetDateRange(firstDayPrevMonth, lastDayPrevMonth);
        }

        private void BtnRok2025_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        }

        private void BtnRok2024_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        }

        private void BtnMonth_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string[] parts = btn.Tag.ToString().Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                {
                    var firstDay = new DateTime(year, month, 1);
                    var lastDay = firstDay.AddMonths(1).AddDays(-1);
                    SetDateRange(firstDay, lastDay);
                }
            }
        }

        #endregion

        #region Ładowanie danych

        private void LoadAllData()
        {
            try
            {
                var smsStats = LoadSmsStatistics();
                var confirmStats = LoadConfirmationStatistics();
                LoadSmsHistory();
                UpdateSummaryCards(smsStats);
                DrawDonutChart(donutChartSent, legendSent, smsStats);
                DrawDonutChart(donutChartConfirm, legendConfirm, confirmStats);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<UserStats> LoadSmsStatistics()
        {
            var stats = new List<UserStats>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() == 0) return stats;
                    }

                    // JOIN z operators aby uzyskać pełną nazwę użytkownika
                    string query = @"
                        SELECT ISNULL(o.Name, s.SentByUser) AS UserName, COUNT(*) AS Total
                        FROM dbo.SmsHistory s
                        LEFT JOIN dbo.operators o ON s.SentByUser = o.ID
                        WHERE s.SentDate >= @DateFrom AND s.SentDate <= @DateTo
                          AND s.SentByUser IS NOT NULL
                        GROUP BY ISNULL(o.Name, s.SentByUser)
                        ORDER BY Total DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string userName = reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "-";
                                int total = reader["Total"] != DBNull.Value ? Convert.ToInt32(reader["Total"]) : 0;

                                if (!string.IsNullOrEmpty(userName) && total > 0)
                                {
                                    stats.Add(new UserStats
                                    {
                                        UserId = userName,
                                        Count = total
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return stats;
        }

        private List<UserStats> LoadConfirmationStatistics()
        {
            var stats = new List<UserStats>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() == 0) return stats;
                    }

                    // JOIN z operators aby uzyskać pełną nazwę użytkownika
                    string query = @"
                        SELECT ISNULL(o.Name, s.AcknowledgedByUser) AS UserName, COUNT(*) AS Total
                        FROM dbo.SmsChangeLog s
                        LEFT JOIN dbo.operators o ON s.AcknowledgedByUser = o.ID
                        WHERE s.AcknowledgedDate >= @DateFrom AND s.AcknowledgedDate <= @DateTo
                          AND s.AcknowledgedByUser IS NOT NULL
                        GROUP BY ISNULL(o.Name, s.AcknowledgedByUser)
                        ORDER BY Total DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string userName = reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "-";
                                int total = reader["Total"] != DBNull.Value ? Convert.ToInt32(reader["Total"]) : 0;

                                if (!string.IsNullOrEmpty(userName) && total > 0)
                                {
                                    stats.Add(new UserStats
                                    {
                                        UserId = userName,
                                        Count = total
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return stats;
        }

        private void LoadSmsHistory()
        {
            var history = new List<SmsHistoryItem>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() == 0)
                        {
                            dgSmsHistory.ItemsSource = history;
                            return;
                        }
                    }

                    // JOIN z operators aby uzyskać pełną nazwę użytkownika
                    string query = @"
                        SELECT TOP 500
                            s.SentDate,
                            ISNULL(o.Name, s.SentByUser) AS UserName,
                            s.SmsType,
                            s.CustomerGID,
                            s.PhoneNumber,
                            s.CalcDate
                        FROM dbo.SmsHistory s
                        LEFT JOIN dbo.operators o ON s.SentByUser = o.ID
                        WHERE s.SentDate >= @DateFrom AND s.SentDate <= @DateTo
                        ORDER BY s.SentDate DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        int lp = 1;
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime? sentDate = reader["SentDate"] != DBNull.Value ? Convert.ToDateTime(reader["SentDate"]) : (DateTime?)null;
                                DateTime? calcDate = reader["CalcDate"] != DBNull.Value ? Convert.ToDateTime(reader["CalcDate"]) : (DateTime?)null;
                                string smsType = reader["SmsType"] != DBNull.Value ? reader["SmsType"].ToString() : "";
                                string userName = reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "-";
                                string customerGID = reader["CustomerGID"] != DBNull.Value ? reader["CustomerGID"].ToString() : "-";
                                string phoneNumber = reader["PhoneNumber"] != DBNull.Value ? reader["PhoneNumber"].ToString() : "-";

                                history.Add(new SmsHistoryItem
                                {
                                    Lp = lp++,
                                    SentDate = sentDate ?? DateTime.MinValue,
                                    SentDateTimeF = sentDate?.ToString("dd.MM.yyyy HH:mm") ?? "-",
                                    SentByUser = userName,
                                    SmsType = smsType,
                                    SmsTypeF = smsType == "ALL" ? "Zbiorczy" : "Pojedynczy",
                                    CustomerGID = customerGID,
                                    PhoneNumber = phoneNumber,
                                    CalcDate = calcDate,
                                    CalcDateF = calcDate?.ToString("dd.MM.yyyy") ?? "-"
                                });
                            }
                        }
                    }
                }

                dgSmsHistory.ItemsSource = history;
            }
            catch { }
        }

        private void UpdateSummaryCards(List<UserStats> smsStats)
        {
            int totalSms = 0, smsAll = 0, smsOne = 0, activeUsers = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() > 0)
                        {
                            string query = @"
                                SELECT
                                    COUNT(*) AS TotalSms,
                                    ISNULL(SUM(CASE WHEN SmsType = 'ALL' THEN 1 ELSE 0 END), 0) AS SmsAll,
                                    ISNULL(SUM(CASE WHEN SmsType = 'ONE' THEN 1 ELSE 0 END), 0) AS SmsOne,
                                    COUNT(DISTINCT SentByUser) AS ActiveUsers
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
                                        totalSms = reader["TotalSms"] != DBNull.Value ? Convert.ToInt32(reader["TotalSms"]) : 0;
                                        smsAll = reader["SmsAll"] != DBNull.Value ? Convert.ToInt32(reader["SmsAll"]) : 0;
                                        smsOne = reader["SmsOne"] != DBNull.Value ? Convert.ToInt32(reader["SmsOne"]) : 0;
                                        activeUsers = reader["ActiveUsers"] != DBNull.Value ? Convert.ToInt32(reader["ActiveUsers"]) : 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            lblTotalSms.Text = totalSms.ToString();
            lblSmsAll.Text = smsAll.ToString();
            lblSmsOne.Text = smsOne.ToString();
            lblActiveUsers.Text = activeUsers.ToString();
        }

        #endregion

        #region Donut Chart

        private void DrawDonutChart(Canvas canvas, ItemsControl legend, List<UserStats> stats)
        {
            canvas.Children.Clear();

            if (stats == null || !stats.Any())
            {
                var noData = new TextBlock
                {
                    Text = "Brak danych",
                    FontSize = 13,
                    Foreground = Brushes.Gray
                };
                Canvas.SetLeft(noData, 45);
                Canvas.SetTop(noData, 70);
                canvas.Children.Add(noData);
                legend.ItemsSource = null;
                return;
            }

            double centerX = 75;
            double centerY = 75;
            double outerRadius = 70;
            double innerRadius = 45; // Tworzy efekt "donut"
            double total = stats.Sum(s => s.Count);

            if (total == 0)
            {
                legend.ItemsSource = null;
                return;
            }

            double startAngle = -90;
            var legendItems = new List<ChartLegendItem>();
            double maxBarWidth = 120; // Max szerokość paska w legendzie

            for (int i = 0; i < stats.Count && i < chartColors.Length; i++)
            {
                var stat = stats[i];
                double sweepAngle = (stat.Count / total) * 360;

                if (sweepAngle < 0.5) continue;

                // Rysuj segment donut
                var slice = CreateDonutSlice(centerX, centerY, outerRadius, innerRadius, startAngle, sweepAngle, chartColors[i]);
                canvas.Children.Add(slice);

                // Dodaj do legendy
                double percent = (stat.Count / total) * 100;
                legendItems.Add(new ChartLegendItem
                {
                    Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chartColors[i])),
                    Label = $"{stat.UserId} ({stat.Count})",
                    Percent = percent,
                    PercentText = $"- {percent:F1}%",
                    BarWidth = (percent / 100) * maxBarWidth
                });

                startAngle += sweepAngle;
            }

            legend.ItemsSource = legendItems;
        }

        private Path CreateDonutSlice(double centerX, double centerY, double outerRadius, double innerRadius, double startAngle, double sweepAngle, string colorHex)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            // Outer arc points
            Point outerStart = new Point(
                centerX + outerRadius * Math.Cos(startRad),
                centerY + outerRadius * Math.Sin(startRad));
            Point outerEnd = new Point(
                centerX + outerRadius * Math.Cos(endRad),
                centerY + outerRadius * Math.Sin(endRad));

            // Inner arc points
            Point innerStart = new Point(
                centerX + innerRadius * Math.Cos(startRad),
                centerY + innerRadius * Math.Sin(startRad));
            Point innerEnd = new Point(
                centerX + innerRadius * Math.Cos(endRad),
                centerY + innerRadius * Math.Sin(endRad));

            bool isLargeArc = sweepAngle > 180;

            var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };

            // Outer arc
            figure.Segments.Add(new ArcSegment(
                outerEnd,
                new Size(outerRadius, outerRadius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            // Line to inner arc
            figure.Segments.Add(new LineSegment(innerEnd, true));

            // Inner arc (reversed)
            figure.Segments.Add(new ArcSegment(
                innerStart,
                new Size(innerRadius, innerRadius),
                0,
                isLargeArc,
                SweepDirection.Counterclockwise,
                true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new Path
            {
                Data = geometry,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
        }

        #endregion
    }

    #region Data Classes

    public class UserStats
    {
        public string UserId { get; set; }
        public int Count { get; set; }
    }

    public class ChartLegendItem
    {
        public Brush Color { get; set; }
        public string Label { get; set; }
        public double Percent { get; set; }
        public string PercentText { get; set; }
        public double BarWidth { get; set; }
    }

    public class SmsHistoryItem
    {
        public int Lp { get; set; }
        public DateTime SentDate { get; set; }
        public string SentDateTimeF { get; set; }
        public string SentByUser { get; set; }
        public string SmsType { get; set; }
        public string SmsTypeF { get; set; }
        public string CustomerGID { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? CalcDate { get; set; }
        public string CalcDateF { get; set; }
    }

    #endregion
}
