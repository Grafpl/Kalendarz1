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
            var dayLabel = $"{dayName}.{date:dd.MM}";

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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) }); // Day
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Count
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // Sztuki
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // Ub (placeholder)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Auta
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Anulowane info

            // Day label
            var dayText = new TextBlock
            {
                Text = dayLabel,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dayText, 0);
            grid.Children.Add(dayText);

            // Delivery count
            var countText = new TextBlock
            {
                Text = deliveries.Count.ToString(),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(countText, 1);
            grid.Children.Add(countText);

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
            Grid.SetColumn(autaText, 5);
            grid.Children.Add(autaText);

            header.Child = grid;
            return header;
        }

        private Border CreateDeliveryRow(DeliveryCalendarItem delivery)
        {
            var isConfirmed = delivery.Bufor == "Potwierdzony";
            var isPlanned = delivery.Bufor == "Planowany" || string.IsNullOrEmpty(delivery.Bufor);

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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) }); // Dostawca
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Auta
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // Sztuki
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // TypCeny
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Cena
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Uwagi

            // Dostawca name (truncated)
            var dostawcaText = new TextBlock
            {
                Text = TruncateString(delivery.Dostawca, 12),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = delivery.Dostawca
            };
            Grid.SetColumn(dostawcaText, 0);
            grid.Children.Add(dostawcaText);

            // Auta
            var autaText = new TextBlock
            {
                Text = delivery.Auta.ToString(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 1);
            grid.Children.Add(autaText);

            // Sztuki
            var sztukiText = new TextBlock
            {
                Text = delivery.SztukiDek.ToString("# ##0"),
                FontSize = 10,
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
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wagaText, 3);
            grid.Children.Add(wagaText);

            // TypCeny with background color
            var typCenyBg = GetTypCenyBackground(delivery.TypCeny);
            var typCenyBorder = new Border
            {
                Background = new SolidColorBrush(typCenyBg),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(2, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var typCenyText = new TextBlock
            {
                Text = GetTypCenyShort(delivery.TypCeny),
                FontSize = 9,
                Foreground = GetTypCenyForeground(delivery.TypCeny),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            typCenyBorder.Child = typCenyText;
            Grid.SetColumn(typCenyBorder, 4);
            grid.Children.Add(typCenyBorder);

            // Cena
            var cenaText = new TextBlock
            {
                Text = delivery.Cena > 0 ? delivery.Cena.ToString("0.00") : "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(cenaText, 5);
            grid.Children.Add(cenaText);

            // Uwagi with icon
            if (!string.IsNullOrEmpty(delivery.Uwagi))
            {
                var uwagiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
                var uwagiIcon = new TextBlock
                {
                    Text = "💬",
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 3, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var uwagiText = new TextBlock
                {
                    Text = TruncateString(delivery.Uwagi, 25),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = delivery.Uwagi
                };
                uwagiPanel.Children.Add(uwagiIcon);
                uwagiPanel.Children.Add(uwagiText);
                Grid.SetColumn(uwagiPanel, 6);
                grid.Children.Add(uwagiPanel);
            }

            row.Child = grid;

            // Double-click to edit (future feature)
            row.MouseDoubleClick += (s, e) =>
            {
                MessageBox.Show($"LP: {delivery.LP}\nDostawca: {delivery.Dostawca}\nData: {delivery.DataOdbioru:dd.MM.yyyy}\nSztuki: {delivery.SztukiDek}\nWaga: {delivery.WagaDek:0.00}\nStatus: {delivery.Bufor}",
                    "Szczegóły dostawy", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            return row;
        }

        private Color GetTypCenyBackground(string typCeny)
        {
            return typCeny?.ToLower() switch
            {
                "wolny" => Color.FromRgb(255, 245, 157), // Yellow
                "rolniczy" => Color.FromRgb(46, 125, 50), // Green
                "mini" => Color.FromRgb(25, 118, 210), // Blue
                "laczony" => Color.FromRgb(123, 31, 162), // Purple
                _ => Colors.Transparent
            };
        }

        private Brush GetTypCenyForeground(string typCeny)
        {
            return typCeny?.ToLower() switch
            {
                "wolny" => Brushes.Black,
                "rolniczy" => Brushes.White,
                "mini" => Brushes.White,
                "laczony" => Brushes.White,
                _ => Brushes.Black
            };
        }

        private string GetTypCenyShort(string typCeny)
        {
            return typCeny?.ToLower() switch
            {
                "wolny" => "wol.",
                "rolniczy" => "rol.",
                "mini" => "mini.",
                "laczony" => "łącz.",
                _ => typCeny ?? ""
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
