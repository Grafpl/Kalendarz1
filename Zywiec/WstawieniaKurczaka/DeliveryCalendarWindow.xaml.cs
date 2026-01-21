using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Calendar window for viewing deliveries grouped by week like Dostawy Żywca view
    /// Shows only weeks that have deliveries
    /// </summary>
    public partial class DeliveryCalendarWindow : Window
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DateTime _startDate;
        private Dictionary<DateTime, List<DeliveryCalendarItem>> _deliveriesByDate = new();
        private List<WeekData> _weeksWithDeliveries = new();
        private int _currentWeekIndex = 0;
        private const int MAX_DAILY_CAPACITY = 80000;
        private static readonly CultureInfo _polishCulture = new CultureInfo("pl-PL");

        public DeliveryCalendarWindow()
        {
            InitializeComponent();
            _startDate = GetStartOfWeek(DateTime.Today);
            LoadDeliveries();
            FindWeeksWithDeliveries();
            ScrollToCurrentWeek();
            RenderWeekView();
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-diff).Date;
        }

        private int GetWeekOfYear(DateTime date)
        {
            return _polishCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private void LoadDeliveries()
        {
            _deliveriesByDate.Clear();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    SELECT
                        h.LP,
                        h.DataOdbioru,
                        h.Dostawca,
                        ISNULL(h.SztukiDek, 0) AS SztukiDek,
                        ISNULL(h.WagaDek, 0) AS WagaDek,
                        ISNULL(h.Auta, 0) AS Auta,
                        h.Bufor,
                        h.TypCeny,
                        ISNULL(h.Cena, 0) AS Cena,
                        h.Uwagi
                    FROM [LibraNet].[dbo].[HarmonogramDostaw] h
                    WHERE h.DataOdbioru IS NOT NULL
                      AND h.DataOdbioru >= DATEADD(MONTH, -3, GETDATE())
                      AND h.DataOdbioru <= DATEADD(MONTH, 6, GETDATE())
                      AND (h.Bufor IS NULL OR h.Bufor NOT IN ('Anulowany', 'Sprzedany'))
                    ORDER BY h.DataOdbioru, h.Dostawca";

                using var cmd = new SqlCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var delivery = new DeliveryCalendarItem
                    {
                        LP = reader.GetInt32(0),
                        DataOdbioru = reader.GetDateTime(1),
                        Dostawca = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        SztukiDek = Convert.ToInt32(reader.GetValue(3)),
                        WagaDek = Convert.ToDecimal(reader.GetValue(4)),
                        Auta = Convert.ToInt32(reader.GetValue(5)),
                        Bufor = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        TypCeny = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        Cena = Convert.ToDecimal(reader.GetValue(8)),
                        Uwagi = reader.IsDBNull(9) ? "" : reader.GetString(9)
                    };

                    var date = delivery.DataOdbioru.Date;
                    if (!_deliveriesByDate.ContainsKey(date))
                        _deliveriesByDate[date] = new List<DeliveryCalendarItem>();

                    _deliveriesByDate[date].Add(delivery);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania dostaw: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FindWeeksWithDeliveries()
        {
            _weeksWithDeliveries.Clear();

            if (!_deliveriesByDate.Any()) return;

            var minDate = _deliveriesByDate.Keys.Min();
            var maxDate = _deliveriesByDate.Keys.Max();

            var weekStart = GetStartOfWeek(minDate);
            while (weekStart <= maxDate)
            {
                var weekEnd = weekStart.AddDays(6);
                var weekDeliveries = _deliveriesByDate
                    .Where(kvp => kvp.Key >= weekStart && kvp.Key <= weekEnd)
                    .SelectMany(kvp => kvp.Value)
                    .ToList();

                if (weekDeliveries.Any())
                {
                    _weeksWithDeliveries.Add(new WeekData
                    {
                        WeekStart = weekStart,
                        WeekEnd = weekEnd,
                        WeekNumber = GetWeekOfYear(weekStart),
                        TotalSztuki = weekDeliveries.Sum(d => d.SztukiDek),
                        TotalAuta = weekDeliveries.Sum(d => d.Auta),
                        TotalWaga = weekDeliveries.Sum(d => d.WagaDek),
                        DeliveriesByDay = _deliveriesByDate
                            .Where(kvp => kvp.Key >= weekStart && kvp.Key <= weekEnd)
                            .OrderBy(kvp => kvp.Key)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    });
                }

                weekStart = weekStart.AddDays(7);
            }
        }

        private void ScrollToCurrentWeek()
        {
            var currentWeekStart = GetStartOfWeek(DateTime.Today);
            _currentWeekIndex = _weeksWithDeliveries.FindIndex(w => w.WeekStart >= currentWeekStart);
            if (_currentWeekIndex < 0) _currentWeekIndex = 0;
            if (_currentWeekIndex > 0) _currentWeekIndex--; // Show previous week on left
        }

        private void RenderWeekView()
        {
            panelLeftWeeks.Children.Clear();
            panelRightWeeks.Children.Clear();

            if (!_weeksWithDeliveries.Any())
            {
                txtDateRange.Text = "Brak dostaw";
                txtTotalSummary.Text = "";
                txtLeftPanelHeader.Text = "Brak danych";
                txtRightPanelHeader.Text = "Brak danych";
                return;
            }

            // Ensure index is valid
            _currentWeekIndex = Math.Max(0, Math.Min(_currentWeekIndex, _weeksWithDeliveries.Count - 1));

            // Get weeks to display
            var leftWeek = _weeksWithDeliveries.ElementAtOrDefault(_currentWeekIndex);
            var rightWeek = _weeksWithDeliveries.ElementAtOrDefault(_currentWeekIndex + 1);

            // Calculate total for displayed weeks
            var displayedWeeks = new[] { leftWeek, rightWeek }.Where(w => w != null).ToList();
            var totalSztuki = displayedWeeks.Sum(w => w.TotalSztuki);
            var totalAuta = displayedWeeks.Sum(w => w.TotalAuta);

            // Update headers
            if (leftWeek != null && rightWeek != null)
            {
                txtDateRange.Text = $"tyg.{leftWeek.WeekNumber}-{rightWeek.WeekNumber} ({leftWeek.WeekStart:dd.MM} - {rightWeek.WeekEnd:dd.MM})";
            }
            else if (leftWeek != null)
            {
                txtDateRange.Text = $"tyg.{leftWeek.WeekNumber} ({leftWeek.WeekStart:dd.MM} - {leftWeek.WeekEnd:dd.MM})";
            }

            txtTotalSummary.Text = $"Suma: {totalSztuki:# ##0} szt. | {totalAuta} aut";

            // Render left panel
            if (leftWeek != null)
            {
                txtLeftPanelHeader.Text = $"tyg.{leftWeek.WeekNumber} ({leftWeek.WeekStart:dd.MM}-{leftWeek.WeekEnd:dd.MM})";
                RenderWeekToPanel(leftWeek, panelLeftWeeks);
            }
            else
            {
                txtLeftPanelHeader.Text = "Brak danych";
            }

            // Render right panel
            if (rightWeek != null)
            {
                txtRightPanelHeader.Text = $"tyg.{rightWeek.WeekNumber} ({rightWeek.WeekStart:dd.MM}-{rightWeek.WeekEnd:dd.MM})";
                RenderWeekToPanel(rightWeek, panelRightWeeks);
            }
            else
            {
                txtRightPanelHeader.Text = "Brak danych";
            }
        }

        private void RenderWeekToPanel(WeekData week, StackPanel panel)
        {
            foreach (var dayEntry in week.DeliveriesByDay.OrderBy(d => d.Key))
            {
                var date = dayEntry.Key;
                var deliveries = dayEntry.Value;

                // Create day header
                var dayHeader = CreateDayHeader(date, deliveries);
                panel.Children.Add(dayHeader);

                // Add delivery rows
                foreach (var delivery in deliveries.OrderByDescending(d => d.SztukiDek))
                {
                    var deliveryRow = CreateDeliveryRow(delivery);
                    panel.Children.Add(deliveryRow);
                }
            }
        }

        private Border CreateDayHeader(DateTime date, List<DeliveryCalendarItem> deliveries)
        {
            var totalSztuki = deliveries.Sum(d => d.SztukiDek);
            var totalWaga = deliveries.Sum(d => d.WagaDek);
            var totalAuta = deliveries.Sum(d => d.Auta);
            var avgWaga = deliveries.Any() ? deliveries.Average(d => (double)d.WagaDek) : 0;
            var capacityPercent = (double)totalSztuki / MAX_DAILY_CAPACITY * 100;

            var isToday = date.Date == DateTime.Today;
            var dayName = date.ToString("ddd", _polishCulture).ToLower().TrimEnd('.');
            var dayLabel = $"{dayName}. {date:dd.MM}";

            // Determine background color based on capacity
            Color bgColor;
            if (capacityPercent >= 80)
                bgColor = Color.FromRgb(239, 68, 68); // Red
            else if (capacityPercent >= 50)
                bgColor = Color.FromRgb(251, 191, 36); // Yellow/Orange
            else
                bgColor = Color.FromRgb(34, 197, 94); // Green

            if (isToday)
                bgColor = Color.FromRgb(59, 130, 246); // Blue for today

            var header = new Border
            {
                Background = new SolidColorBrush(bgColor),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 2, 0, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Data + count
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Auto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Capacity %

            // Day label with count
            var dayText = new TextBlock
            {
                Text = $"{dayLabel} ({deliveries.Count} dostaw)",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dayText, 0);
            grid.Children.Add(dayText);

            // Total auta
            var autaText = new TextBlock
            {
                Text = totalAuta.ToString(),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 1);
            grid.Children.Add(autaText);

            // Total sztuki
            var sztukiText = new TextBlock
            {
                Text = totalSztuki.ToString("# ##0"),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sztukiText, 2);
            grid.Children.Add(sztukiText);

            // Average weight
            var wagaText = new TextBlock
            {
                Text = avgWaga.ToString("0.00"),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wagaText, 3);
            grid.Children.Add(wagaText);

            // Capacity percentage
            var capacityText = new TextBlock
            {
                Text = $"{capacityPercent:0}%",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(capacityText, 4);
            grid.Children.Add(capacityText);

            header.Child = grid;
            return header;
        }

        private Border CreateDeliveryRow(DeliveryCalendarItem delivery)
        {
            var isConfirmed = delivery.Bufor == "Potwierdzony";

            Color bgColor = Colors.White;
            if (isConfirmed)
                bgColor = Color.FromRgb(236, 253, 245); // Light green for confirmed

            var row = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand
            };

            // Hover effect
            row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
            row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(bgColor);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Hodowca
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Auto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Status

            // Hodowca name
            var hodowcaText = new TextBlock
            {
                Text = delivery.Dostawca,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = delivery.Dostawca
            };
            Grid.SetColumn(hodowcaText, 0);
            grid.Children.Add(hodowcaText);

            // Auto
            var autaText = new TextBlock
            {
                Text = delivery.Auta.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 1);
            grid.Children.Add(autaText);

            // Szt (Sztuki)
            var sztukiText = new TextBlock
            {
                Text = delivery.SztukiDek.ToString("# ##0"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sztukiText, 2);
            grid.Children.Add(sztukiText);

            // Waga
            var wagaText = new TextBlock
            {
                Text = delivery.WagaDek.ToString("0.00"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wagaText, 3);
            grid.Children.Add(wagaText);

            // Status (Bufor) with color coding
            var statusBg = GetStatusBackground(delivery.Bufor);
            var statusFg = GetStatusForeground(delivery.Bufor);
            var statusBorder = new Border
            {
                Background = new SolidColorBrush(statusBg),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(4, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var statusText = new TextBlock
            {
                Text = string.IsNullOrEmpty(delivery.Bufor) ? "Planowany" : delivery.Bufor,
                FontSize = 10,
                Foreground = statusFg,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBorder.Child = statusText;
            Grid.SetColumn(statusBorder, 4);
            grid.Children.Add(statusBorder);

            row.Child = grid;

            // Double-click to show details
            row.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    MessageBox.Show($"LP: {delivery.LP}\nHodowca: {delivery.Dostawca}\nData: {delivery.DataOdbioru:dd.MM.yyyy}\nAuto: {delivery.Auta}\nSztuki: {delivery.SztukiDek}\nWaga: {delivery.WagaDek:0.00}\nStatus: {(string.IsNullOrEmpty(delivery.Bufor) ? "Planowany" : delivery.Bufor)}",
                        "Szczegóły dostawy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            return row;
        }

        private Color GetStatusBackground(string status)
        {
            return status?.ToLower() switch
            {
                "potwierdzony" => Color.FromRgb(34, 197, 94), // Green
                "planowany" => Color.FromRgb(59, 130, 246), // Blue
                "anulowany" => Color.FromRgb(239, 68, 68), // Red
                "sprzedany" => Color.FromRgb(251, 191, 36), // Yellow
                _ => Color.FromRgb(59, 130, 246) // Default blue for empty/planowany
            };
        }

        private Brush GetStatusForeground(string status)
        {
            return status?.ToLower() switch
            {
                "sprzedany" => Brushes.Black,
                _ => Brushes.White
            };
        }

        private string TruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        #region Navigation

        private void BtnPrevWeeks_Click(object sender, RoutedEventArgs e)
        {
            if (_currentWeekIndex > 0)
            {
                _currentWeekIndex -= 2;
                if (_currentWeekIndex < 0) _currentWeekIndex = 0;
                RenderWeekView();
            }
        }

        private void BtnNextWeeks_Click(object sender, RoutedEventArgs e)
        {
            if (_currentWeekIndex + 2 < _weeksWithDeliveries.Count)
            {
                _currentWeekIndex += 2;
                RenderWeekView();
            }
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            ScrollToCurrentWeek();
            RenderWeekView();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }

    /// <summary>
    /// Represents delivery data for a week
    /// </summary>
    public class WeekData
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int WeekNumber { get; set; }
        public int TotalSztuki { get; set; }
        public int TotalAuta { get; set; }
        public decimal TotalWaga { get; set; }
        public Dictionary<DateTime, List<DeliveryCalendarItem>> DeliveriesByDay { get; set; } = new();
    }

    /// <summary>
    /// Represents a delivery item in the calendar
    /// </summary>
    public class DeliveryCalendarItem
    {
        public int LP { get; set; }
        public DateTime DataOdbioru { get; set; }
        public string Dostawca { get; set; }
        public int SztukiDek { get; set; }
        public decimal WagaDek { get; set; }
        public int Auta { get; set; }
        public string Bufor { get; set; }
        public string TypCeny { get; set; }
        public decimal Cena { get; set; }
        public string Uwagi { get; set; }
    }
}
