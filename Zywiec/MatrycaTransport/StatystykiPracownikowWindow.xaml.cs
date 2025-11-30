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
        private List<string> allUsers = new List<string>();
        private int totalDaysInRange = 1;

        // Kolory do wykresu kołowego (takie same jak w RaportyStatystyki)
        private readonly string[] pieColors = new string[]
        {
            "#5C8A3A", "#3498DB", "#E74C3C", "#9B59B6", "#F39C12",
            "#1ABC9C", "#34495E", "#E67E22", "#27AE60", "#2980B9"
        };

        public StatystykiPracownikowWindow()
        {
            InitializeComponent();

            // Domyślny zakres dat - ostatnie 30 dni
            dpDo.SelectedDate = DateTime.Today;
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);

            dpSmsOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpSmsDo.SelectedDate = DateTime.Today;

            dpConfOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpConfDo.SelectedDate = DateTime.Today;

            // Wypełnij combobox lat
            int currentYear = DateTime.Today.Year;
            for (int y = currentYear; y >= currentYear - 5; y--)
            {
                cboRokMiesiace.Items.Add(y);
            }
            cboRokMiesiace.SelectedIndex = 0;

            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllUsers();
            LoadStatistics();
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

        #region Główne statystyki i Ranking

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void LoadAllUsers()
        {
            allUsers.Clear();
            allUsers.Add("-- Wszyscy --");

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
                            cboFiltPracownik.ItemsSource = allUsers;
                            cboFiltPracownik.SelectedIndex = 0;
                            return;
                        }
                    }

                    string query = "SELECT DISTINCT SentByUser FROM dbo.SmsHistory WHERE SentByUser IS NOT NULL ORDER BY SentByUser";
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

                cboFiltPracownik.ItemsSource = allUsers;
                cboFiltPracownik.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadStatistics()
        {
            DateTime dateFrom = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dateTo = dpDo.SelectedDate ?? DateTime.Today;
            dateTo = dateTo.Date.AddDays(1).AddSeconds(-1);
            totalDaysInRange = Math.Max(1, (int)(dateTo.Date - dateFrom.Date).TotalDays + 1);

            try
            {
                var stats = LoadSmsStatistics(dateFrom, dateTo);
                UpdateSummaryCards(dateFrom, dateTo);
                DrawPieChart(stats);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania statystyk:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<UserSmsStats> LoadSmsStatistics(DateTime dateFrom, DateTime dateTo)
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
                        return stats;
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
            return stats;
        }

        private void UpdateSummaryCards(DateTime dateFrom, DateTime dateTo)
        {
            int totalSms = 0, smsAll = 0, smsOne = 0, activeUsers = 0;

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
                                SUM(CASE WHEN SmsType = 'ONE' THEN 1 ELSE 0 END) AS SmsOne,
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
                                    totalSms = Convert.ToInt32(reader["TotalSms"]);
                                    smsAll = Convert.ToInt32(reader["SmsAll"]);
                                    smsOne = Convert.ToInt32(reader["SmsOne"]);
                                    activeUsers = Convert.ToInt32(reader["ActiveUsers"]);
                                }
                            }
                        }
                    }
                }
            }

            lblTotalSms.Text = totalSms.ToString("N0");
            lblSmsAll.Text = smsAll.ToString("N0");
            lblSmsOne.Text = smsOne.ToString("N0");
            lblActiveUsers.Text = activeUsers.ToString();
        }

        #endregion

        #region Diagram kołowy (Pie Chart)

        private void DrawPieChart(List<UserSmsStats> stats)
        {
            pieChartCanvas.Children.Clear();

            if (stats == null || !stats.Any())
            {
                // Pokaż tekst "Brak danych"
                var noDataText = new TextBlock
                {
                    Text = "Brak danych",
                    FontSize = 14,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(noDataText, 100);
                Canvas.SetTop(noDataText, 115);
                pieChartCanvas.Children.Add(noDataText);
                pieLegend.ItemsSource = null;
                return;
            }

            double centerX = 125;
            double centerY = 125;
            double radius = 100;
            double totalSms = stats.Sum(s => s.SmsCount);

            if (totalSms == 0)
            {
                pieLegend.ItemsSource = null;
                return;
            }

            double startAngle = -90; // Zaczynamy od góry
            var legendItems = new List<PieLegendItem>();

            for (int i = 0; i < stats.Count && i < pieColors.Length; i++)
            {
                var stat = stats[i];
                double sweepAngle = (stat.SmsCount / totalSms) * 360;

                if (sweepAngle < 1) continue; // Pomiń bardzo małe kawałki

                // Rysuj kawałek
                var slice = CreatePieSlice(centerX, centerY, radius, startAngle, sweepAngle, pieColors[i]);
                pieChartCanvas.Children.Add(slice);

                // Dodaj do legendy
                double percent = (stat.SmsCount / totalSms) * 100;
                legendItems.Add(new PieLegendItem
                {
                    Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pieColors[i])),
                    Label = stat.UserId,
                    Percent = percent,
                    Count = stat.SmsCount
                });

                startAngle += sweepAngle;
            }

            // "Inni" jeśli jest więcej niż 10 pracowników
            if (stats.Count > pieColors.Length)
            {
                int othersCount = stats.Skip(pieColors.Length).Sum(s => s.SmsCount);
                double othersPercent = (othersCount / totalSms) * 100;
                if (othersPercent > 0)
                {
                    legendItems.Add(new PieLegendItem
                    {
                        Color = Brushes.Gray,
                        Label = "Inni",
                        Percent = othersPercent,
                        Count = othersCount
                    });
                }
            }

            pieLegend.ItemsSource = legendItems;
        }

        private Path CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double sweepAngle, string colorHex)
        {
            // Konwersja kątów na radiany
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            // Punkty początkowy i końcowy łuku
            Point startPoint = new Point(
                centerX + radius * Math.Cos(startRad),
                centerY + radius * Math.Sin(startRad));
            Point endPoint = new Point(
                centerX + radius * Math.Cos(endRad),
                centerY + radius * Math.Sin(endRad));

            // Czy łuk jest większy niż 180 stopni?
            bool isLargeArc = sweepAngle > 180;

            // Tworzenie geometrii
            var figure = new PathFigure
            {
                StartPoint = new Point(centerX, centerY),
                IsClosed = true
            };

            figure.Segments.Add(new LineSegment(startPoint, true));
            figure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            return path;
        }

        #endregion

        #region Gdzie poszły SMS (Historia)

        private void CboFiltPracownik_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nic nie rób - użytkownik musi kliknąć "POKAŻ HISTORIĘ SMS"
        }

        private void BtnShowSmsHistory_Click(object sender, RoutedEventArgs e)
        {
            LoadSmsHistory();
        }

        private void LoadSmsHistory()
        {
            DateTime dateFrom = dpSmsOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dateTo = (dpSmsDo.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);
            string selectedUser = cboFiltPracownik.SelectedItem?.ToString();

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

                    string query = @"
                        SELECT TOP 500
                            SentDate,
                            SentByUser,
                            SmsType,
                            CustomerGID,
                            PhoneNumber,
                            SmsContent
                        FROM dbo.SmsHistory
                        WHERE SentDate >= @DateFrom AND SentDate <= @DateTo";

                    if (!string.IsNullOrEmpty(selectedUser) && selectedUser != "-- Wszyscy --")
                    {
                        query += " AND SentByUser = @UserId";
                    }

                    query += " ORDER BY SentDate DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);
                        if (!string.IsNullOrEmpty(selectedUser) && selectedUser != "-- Wszyscy --")
                        {
                            cmd.Parameters.AddWithValue("@UserId", selectedUser);
                        }

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime sentDate = Convert.ToDateTime(reader["SentDate"]);
                                string smsType = reader["SmsType"]?.ToString() ?? "";

                                history.Add(new SmsHistoryItem
                                {
                                    SentDate = sentDate,
                                    SentDateF = sentDate.ToString("dd.MM.yyyy"),
                                    SentTimeF = sentDate.ToString("HH:mm"),
                                    SentByUser = reader["SentByUser"]?.ToString() ?? "-",
                                    SmsType = smsType,
                                    SmsTypeF = smsType == "ALL" ? "Zbiorczy" : "Pojedynczy",
                                    CustomerGID = reader["CustomerGID"]?.ToString() ?? "-",
                                    PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "-",
                                    SmsContent = reader["SmsContent"]?.ToString() ?? "-"
                                });
                            }
                        }
                    }
                }

                dgSmsHistory.ItemsSource = history;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania historii SMS:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Statystyki Miesięczne

        private void CboRokMiesiace_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                                    AvgPerDay = Math.Round((double)totalSms / daysInMonth, 1)
                                });
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
                }
                chartMonthly.ItemsSource = stats;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania statystyk miesięcznych:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Potwierdzenia Zmian

        private void BtnShowConfirmations_Click(object sender, RoutedEventArgs e)
        {
            LoadConfirmations();
        }

        private void LoadConfirmations()
        {
            DateTime dateFrom = dpConfOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dateTo = (dpConfDo.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);

            var confirmations = new List<ConfirmationItem>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsChangeLog'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        if ((int)checkCmd.ExecuteScalar() == 0)
                        {
                            dgConfirmations.ItemsSource = confirmations;
                            return;
                        }
                    }

                    string query = @"
                        SELECT TOP 500
                            CalcDate,
                            AcknowledgedByUser,
                            AcknowledgedDate,
                            HodowcaNazwa,
                            ChangeType,
                            OriginalSmsUser,
                            OriginalSmsDate
                        FROM dbo.SmsChangeLog
                        WHERE AcknowledgedDate >= @DateFrom AND AcknowledgedDate <= @DateTo
                        ORDER BY AcknowledgedDate DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime calcDate = reader["CalcDate"] != DBNull.Value ? Convert.ToDateTime(reader["CalcDate"]) : DateTime.MinValue;
                                DateTime ackDate = reader["AcknowledgedDate"] != DBNull.Value ? Convert.ToDateTime(reader["AcknowledgedDate"]) : DateTime.MinValue;
                                DateTime origSmsDate = reader["OriginalSmsDate"] != DBNull.Value ? Convert.ToDateTime(reader["OriginalSmsDate"]) : DateTime.MinValue;
                                string changeType = reader["ChangeType"]?.ToString() ?? "";

                                confirmations.Add(new ConfirmationItem
                                {
                                    CalcDate = calcDate,
                                    CalcDateF = calcDate != DateTime.MinValue ? calcDate.ToString("dd.MM.yyyy") : "-",
                                    AcknowledgedByUser = reader["AcknowledgedByUser"]?.ToString() ?? "-",
                                    AcknowledgedDate = ackDate,
                                    AcknowledgedDateF = ackDate != DateTime.MinValue ? ackDate.ToString("dd.MM.yyyy HH:mm") : "-",
                                    HodowcaNazwa = reader["HodowcaNazwa"]?.ToString() ?? "-",
                                    ChangeType = changeType,
                                    ChangeTypeF = GetChangeTypeDisplay(changeType),
                                    OriginalSmsUser = reader["OriginalSmsUser"]?.ToString() ?? "-",
                                    OriginalSmsDate = origSmsDate,
                                    OriginalSmsDateF = origSmsDate != DateTime.MinValue ? origSmsDate.ToString("dd.MM.yyyy HH:mm") : "-"
                                });
                            }
                        }
                    }
                }

                dgConfirmations.ItemsSource = confirmations;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania potwierdzeń:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetChangeTypeDisplay(string changeType)
        {
            switch (changeType?.ToUpper())
            {
                case "ORDER": return "Kolejność aut";
                case "TIME": return "Godzina";
                case "TRUCK": return "Zmiana auta";
                case "OTHER": return "Inna zmiana";
                default: return changeType ?? "-";
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
        public string AvgPerDayF => AvgPerDay.ToString("F1");
    }

    public class PieLegendItem
    {
        public Brush Color { get; set; }
        public string Label { get; set; }
        public double Percent { get; set; }
        public int Count { get; set; }
    }

    public class SmsHistoryItem
    {
        public DateTime SentDate { get; set; }
        public string SentDateF { get; set; }
        public string SentTimeF { get; set; }
        public string SentByUser { get; set; }
        public string SmsType { get; set; }
        public string SmsTypeF { get; set; }
        public string CustomerGID { get; set; }
        public string PhoneNumber { get; set; }
        public string SmsContent { get; set; }
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
        public double AvgPerDay { get; set; }
        public string AvgPerDayF => AvgPerDay.ToString("F1");
        public double BarHeight { get; set; }
    }

    public class ConfirmationItem
    {
        public DateTime CalcDate { get; set; }
        public string CalcDateF { get; set; }
        public string AcknowledgedByUser { get; set; }
        public DateTime AcknowledgedDate { get; set; }
        public string AcknowledgedDateF { get; set; }
        public string HodowcaNazwa { get; set; }
        public string ChangeType { get; set; }
        public string ChangeTypeF { get; set; }
        public string OriginalSmsUser { get; set; }
        public DateTime OriginalSmsDate { get; set; }
        public string OriginalSmsDateF { get; set; }
    }

    #endregion
}
