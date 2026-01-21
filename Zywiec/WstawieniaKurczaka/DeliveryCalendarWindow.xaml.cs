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
    /// Calendar window for viewing and managing deliveries with drag & drop support
    /// </summary>
    public partial class DeliveryCalendarWindow : Window
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private DateTime _currentMonth;
        private Dictionary<DateTime, List<DeliveryCalendarItem>> _deliveriesByDate = new();
        private const int MAX_DAILY_CAPACITY = 80000; // Maximum 80,000 pieces per day

        // Drag & Drop state
        private DeliveryCalendarItem _draggedDelivery = null;
        private Border _draggedElement = null;

        public DeliveryCalendarWindow()
        {
            InitializeComponent();
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            LoadDeliveries();
            RenderCalendar();
        }

        private void LoadDeliveries()
        {
            _deliveriesByDate.Clear();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                // Load all confirmed deliveries
                const string query = @"
                    SELECT
                        h.LP,
                        h.DataOdbioru,
                        h.Dostawca,
                        ISNULL(h.SztukiDek, 0) AS SztukiDek,
                        ISNULL(h.WagaDek, 0) AS WagaDek,
                        ISNULL(h.Auta, 0) AS Auta,
                        h.Bufor
                    FROM [LibraNet].[dbo].[HarmonogramDostaw] h
                    WHERE h.DataOdbioru IS NOT NULL
                      AND h.DataOdbioru >= DATEADD(MONTH, -3, GETDATE())
                      AND h.DataOdbioru <= DATEADD(MONTH, 6, GETDATE())
                    ORDER BY h.DataOdbioru";

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
                        Bufor = reader.IsDBNull(6) ? "" : reader.GetString(6)
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

        private void RenderCalendar()
        {
            calendarGrid.Children.Clear();

            // Get first day of month and calculate start of calendar grid
            var firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            // Calculate which day of week the month starts on (Monday = 0)
            int startDayOfWeek = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

            // Update header
            txtCurrentMonth.Text = _currentMonth.ToString("MMMM yyyy", new CultureInfo("pl-PL"));

            // Calculate total for month
            int monthTotal = 0;
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                if (_deliveriesByDate.TryGetValue(date, out var dayDeliveries))
                {
                    monthTotal += dayDeliveries.Sum(d => d.SztukiDek);
                }
            }
            txtMonthTotal.Text = $"Suma: {monthTotal:# ##0} szt.";

            // Add empty cells for days before month starts
            for (int i = 0; i < startDayOfWeek; i++)
            {
                var emptyCell = CreateEmptyDayCell();
                calendarGrid.Children.Add(emptyCell);
            }

            // Add cells for each day of month
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                var dayCell = CreateDayCell(date);
                calendarGrid.Children.Add(dayCell);
            }

            // Add empty cells to complete the grid (ensure full rows)
            int totalCells = startDayOfWeek + daysInMonth;
            int remainingCells = (7 - (totalCells % 7)) % 7;
            for (int i = 0; i < remainingCells; i++)
            {
                var emptyCell = CreateEmptyDayCell();
                calendarGrid.Children.Add(emptyCell);
            }
        }

        private Border CreateEmptyDayCell()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                MinHeight = 100
            };
        }

        private Border CreateDayCell(DateTime date)
        {
            var deliveries = _deliveriesByDate.TryGetValue(date, out var list) ? list : new List<DeliveryCalendarItem>();
            var totalSzt = deliveries.Sum(d => d.SztukiDek);
            var capacityPercent = (double)totalSzt / MAX_DAILY_CAPACITY * 100;

            // Determine background color based on capacity
            Color bgColor;
            if (capacityPercent >= 100)
                bgColor = Color.FromRgb(255, 205, 210); // Red - overfilled
            else if (capacityPercent >= 80)
                bgColor = Color.FromRgb(255, 224, 178); // Orange - near capacity
            else if (capacityPercent >= 50)
                bgColor = Color.FromRgb(255, 243, 224); // Light orange
            else if (totalSzt > 0)
                bgColor = Color.FromRgb(232, 245, 233); // Light green
            else
                bgColor = Colors.White;

            // Check if today
            bool isToday = date.Date == DateTime.Today;
            var borderColor = isToday ? Color.FromRgb(92, 138, 58) : Color.FromRgb(224, 224, 224);
            var borderThickness = isToday ? 3 : 1;

            var cell = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(borderThickness),
                Margin = new Thickness(1),
                MinHeight = 100,
                AllowDrop = true,
                Tag = date
            };

            // Setup drag & drop events
            cell.DragEnter += DayCell_DragEnter;
            cell.DragLeave += DayCell_DragLeave;
            cell.Drop += DayCell_Drop;

            var content = new StackPanel { Margin = new Thickness(4) };

            // Day number header
            var headerGrid = new Grid();
            var dayNumber = new TextBlock
            {
                Text = date.Day.ToString(),
                FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize = isToday ? 16 : 14,
                Foreground = new SolidColorBrush(isToday ? Color.FromRgb(92, 138, 58) : Color.FromRgb(44, 62, 80))
            };
            headerGrid.Children.Add(dayNumber);

            // Capacity indicator
            if (totalSzt > 0)
            {
                var capacityText = new TextBlock
                {
                    Text = $"{totalSzt:# ##0} szt.",
                    FontSize = 10,
                    FontWeight = capacityPercent >= 80 ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(
                        capacityPercent >= 100 ? Color.FromRgb(198, 40, 40) :
                        capacityPercent >= 80 ? Color.FromRgb(239, 108, 0) :
                        Color.FromRgb(46, 125, 50)),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                headerGrid.Children.Add(capacityText);
            }

            content.Children.Add(headerGrid);

            // Capacity bar
            if (totalSzt > 0)
            {
                var progressBg = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 2, 0, 4)
                };
                var progressFg = new Border
                {
                    Background = new SolidColorBrush(
                        capacityPercent >= 100 ? Color.FromRgb(198, 40, 40) :
                        capacityPercent >= 80 ? Color.FromRgb(239, 108, 0) :
                        Color.FromRgb(92, 138, 58)),
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Min(capacityPercent, 100) * 0.9 // Scale to cell width
                };
                var progressGrid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
                progressGrid.Children.Add(progressBg);
                progressGrid.Children.Add(progressFg);
                content.Children.Add(progressGrid);
            }

            // Deliveries list (scrollable)
            var deliveriesPanel = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 80
            };
            var deliveriesList = new StackPanel();

            // Show up to 5 deliveries, then show count
            var sortedDeliveries = deliveries.OrderByDescending(d => d.SztukiDek).ToList();
            int shown = 0;
            foreach (var delivery in sortedDeliveries.Take(5))
            {
                var deliveryItem = CreateDeliveryItem(delivery);
                deliveriesList.Children.Add(deliveryItem);
                shown++;
            }

            if (sortedDeliveries.Count > 5)
            {
                var moreText = new TextBlock
                {
                    Text = $"... +{sortedDeliveries.Count - 5} więcej",
                    FontSize = 9,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    Margin = new Thickness(4, 2, 0, 0)
                };
                deliveriesList.Children.Add(moreText);
            }

            deliveriesPanel.Content = deliveriesList;
            content.Children.Add(deliveriesPanel);

            cell.Child = content;
            return cell;
        }

        private Border CreateDeliveryItem(DeliveryCalendarItem delivery)
        {
            // Color based on status
            Color bgColor;
            if (delivery.Bufor == "Potwierdzony")
                bgColor = Color.FromRgb(227, 242, 253); // Blue for confirmed
            else if (delivery.Bufor == "Planowany")
                bgColor = Color.FromRgb(255, 243, 224); // Orange for planned
            else
                bgColor = Color.FromRgb(245, 245, 245); // Gray for others

            var item = new Border
            {
                Background = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
                Tag = delivery
            };

            var content = new StackPanel();

            var nameText = new TextBlock
            {
                Text = TruncateString(delivery.Dostawca, 15),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };

            var detailsText = new TextBlock
            {
                Text = $"{delivery.SztukiDek:# ##0} szt. | {delivery.Auta} aut",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141))
            };

            content.Children.Add(nameText);
            content.Children.Add(detailsText);
            item.Child = content;

            // Drag start
            item.MouseLeftButtonDown += (s, e) =>
            {
                _draggedDelivery = delivery;
                _draggedElement = item;
                DragDrop.DoDragDrop(item, delivery, DragDropEffects.Move);
            };

            // Tooltip
            item.ToolTip = new ToolTip
            {
                Content = CreateDeliveryTooltip(delivery),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(8)
            };

            return item;
        }

        private StackPanel CreateDeliveryTooltip(DeliveryCalendarItem delivery)
        {
            var tooltip = new StackPanel { MinWidth = 200 };

            tooltip.Children.Add(new TextBlock
            {
                Text = delivery.Dostawca,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58)),
                Margin = new Thickness(0, 0, 0, 5)
            });

            tooltip.Children.Add(new Separator { Margin = new Thickness(0, 2, 0, 5) });

            AddTooltipRow(tooltip, "Data:", delivery.DataOdbioru.ToString("dd.MM.yyyy (dddd)", new CultureInfo("pl-PL")));
            AddTooltipRow(tooltip, "Sztuki:", $"{delivery.SztukiDek:# ##0} szt.");
            AddTooltipRow(tooltip, "Waga:", $"{delivery.WagaDek:# ##0.00} kg");
            AddTooltipRow(tooltip, "Auta:", delivery.Auta.ToString());
            AddTooltipRow(tooltip, "Status:", delivery.Bufor);
            AddTooltipRow(tooltip, "LP:", delivery.LP.ToString());

            return tooltip;
        }

        private void AddTooltipRow(StackPanel parent, string label, string value)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                Width = 60
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            });
            parent.Children.Add(row);
        }

        private string TruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        #region Drag & Drop

        private void DayCell_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border cell)
            {
                cell.BorderBrush = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                cell.BorderThickness = new Thickness(3);
            }
        }

        private void DayCell_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border cell)
            {
                var date = (DateTime)cell.Tag;
                bool isToday = date.Date == DateTime.Today;
                cell.BorderBrush = new SolidColorBrush(isToday ? Color.FromRgb(92, 138, 58) : Color.FromRgb(224, 224, 224));
                cell.BorderThickness = new Thickness(isToday ? 3 : 1);
            }
        }

        private void DayCell_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border cell && _draggedDelivery != null)
            {
                var newDate = (DateTime)cell.Tag;
                var oldDate = _draggedDelivery.DataOdbioru.Date;

                if (newDate != oldDate)
                {
                    // Confirm move
                    var result = MessageBox.Show(
                        $"Czy przenieść dostawę od '{_draggedDelivery.Dostawca}'\n" +
                        $"z dnia {oldDate:dd.MM.yyyy} na {newDate:dd.MM.yyyy}?",
                        "Potwierdzenie przeniesienia",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Update in database
                        if (UpdateDeliveryDate(_draggedDelivery.LP, newDate))
                        {
                            // Update local data
                            if (_deliveriesByDate.TryGetValue(oldDate, out var oldList))
                            {
                                oldList.Remove(_draggedDelivery);
                            }

                            _draggedDelivery.DataOdbioru = newDate;

                            if (!_deliveriesByDate.ContainsKey(newDate))
                                _deliveriesByDate[newDate] = new List<DeliveryCalendarItem>();

                            _deliveriesByDate[newDate].Add(_draggedDelivery);

                            // Refresh calendar
                            RenderCalendar();
                        }
                    }
                }

                _draggedDelivery = null;
                _draggedElement = null;
            }
        }

        private bool UpdateDeliveryDate(int lp, DateTime newDate)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                const string sql = "UPDATE [LibraNet].[dbo].[HarmonogramDostaw] SET DataOdbioru = @newDate WHERE LP = @lp";
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@newDate", newDate);
                cmd.Parameters.AddWithValue("@lp", lp);

                int affected = cmd.ExecuteNonQuery();
                return affected == 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd aktualizacji daty: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region Navigation

        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            RenderCalendar();
        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            RenderCalendar();
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            RenderCalendar();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
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
    }
}
