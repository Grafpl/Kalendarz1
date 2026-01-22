using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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

            txtTotalSummary.Text = $"SUMA: {totalSztuki:# ##0} szt. | {totalAuta} aut";

            // Apply pulsing animation to total summary
            ApplyPulsingToSummary();

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

        private void ApplyPulsingToSummary()
        {
            if (borderTotalSummary?.Effect is DropShadowEffect effect)
            {
                var pulseOpacity = new DoubleAnimation
                {
                    From = 0.4,
                    To = 0.9,
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var pulseBlur = new DoubleAnimation
                {
                    From = 5,
                    To = 15,
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                effect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
                effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);
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

            // Add week sum row at the end
            var weekSumRow = CreateWeekSumRow(week);
            panel.Children.Add(weekSumRow);
        }

        private Border CreateWeekSumRow(WeekData week)
        {
            var sumRow = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), // Dark blue-gray
                Padding = new Thickness(6, 6, 6, 6),
                Margin = new Thickness(0, 4, 0, 0),
                CornerRadius = new CornerRadius(4)
            };

            // Add pulsing orange glow
            var glowEffect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 167, 38), // Orange
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            sumRow.Effect = glowEffect;

            // Pulsing animation for sum row
            var pulseOpacity = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var pulseBlur = new DoubleAnimation
            {
                From = 10,
                To = 25,
                Duration = TimeSpan.FromMilliseconds(800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
            glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Label
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Suma szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Auta
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Avg waga

            // Sum label
            var labelText = new TextBlock
            {
                Text = $"★ SUMA tyg.{week.WeekNumber}",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelText, 0);
            grid.Children.Add(labelText);

            // Total pieces
            var sztukiText = new TextBlock
            {
                Text = $"{week.TotalSztuki:# ##0} szt.",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow/gold
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sztukiText, 1);
            grid.Children.Add(sztukiText);

            // Total auta
            var autaText = new TextBlock
            {
                Text = $"{week.TotalAuta} aut",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 2);
            grid.Children.Add(autaText);

            // Average weight
            var allDeliveries = week.DeliveriesByDay.Values.SelectMany(d => d).ToList();
            var avgWaga = allDeliveries.Any() ? allDeliveries.Average(d => (double)d.WagaDek) : 0;
            var wagaText = new TextBlock
            {
                Text = $"ś.{avgWaga:0.00}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wagaText, 3);
            grid.Children.Add(wagaText);

            sumRow.Child = grid;
            return sumRow;
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

            // Determine background color and glow color based on capacity
            Color bgColor, glowColor;
            bool shouldPulse = capacityPercent >= 100; // Pulsuj jeśli powyżej 100%

            if (capacityPercent >= 100)
            {
                bgColor = Color.FromRgb(183, 28, 28); // Dark red
                glowColor = Color.FromRgb(244, 67, 54); // Red glow
                shouldPulse = true;
            }
            else if (capacityPercent >= 80)
            {
                bgColor = Color.FromRgb(239, 68, 68); // Red
                glowColor = Color.FromRgb(239, 68, 68);
            }
            else if (capacityPercent >= 50)
            {
                bgColor = Color.FromRgb(251, 191, 36); // Yellow/Orange
                glowColor = Color.FromRgb(255, 193, 7);
            }
            else
            {
                bgColor = Color.FromRgb(34, 197, 94); // Green
                glowColor = Color.FromRgb(76, 175, 80);
            }

            if (isToday)
            {
                bgColor = Color.FromRgb(59, 130, 246); // Blue for today
                glowColor = Color.FromRgb(33, 150, 243);
                shouldPulse = true; // Today always pulses
            }

            var header = new Border
            {
                Background = new SolidColorBrush(bgColor),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 2, 0, 0),
                CornerRadius = new CornerRadius(3)
            };

            // Add pulsing glow effect
            var glowEffect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = shouldPulse ? 12 : 6,
                ShadowDepth = 0,
                Opacity = shouldPulse ? 0.7 : 0.4
            };
            header.Effect = glowEffect;

            // Apply pulsing animation
            if (shouldPulse)
            {
                var pulseOpacity = new DoubleAnimation
                {
                    From = 0.4,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var pulseBlur = new DoubleAnimation
                {
                    From = 8,
                    To = 22,
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
                glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Data + star
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Suma szt + procent
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Auto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Waga avg

            // Day label with star for high capacity
            var starPrefix = capacityPercent >= 100 ? "★ " : "";
            var dayText = new TextBlock
            {
                Text = $"{starPrefix}{dayLabel}",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dayText, 0);
            grid.Children.Add(dayText);

            // SUMA: sztuki + procent w jednej kolumnie (wyróżnione)
            var sumaBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var sumaText = new TextBlock
            {
                Text = $"{totalSztuki:# ##0} szt. {capacityPercent:0}%",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sumaBorder.Child = sumaText;
            Grid.SetColumn(sumaBorder, 1);
            grid.Children.Add(sumaBorder);

            // Total auta
            var autaText = new TextBlock
            {
                Text = $"{totalAuta} aut",
                FontWeight = FontWeights.SemiBold,
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 2);
            grid.Children.Add(autaText);

            // Average weight
            var wagaText = new TextBlock
            {
                Text = $"{avgWaga:0.00}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wagaText, 3);
            grid.Children.Add(wagaText);

            header.Child = grid;
            return header;
        }

        private Border CreateDeliveryRow(DeliveryCalendarItem delivery)
        {
            var isConfirmed = delivery.Bufor == "Potwierdzony";
            var isPartiallyConfirmed = delivery.Bufor == "B.Kontr." || delivery.Bufor == "B.Wolny";
            var isPlanned = string.IsNullOrEmpty(delivery.Bufor) || delivery.Bufor == "Planowany" || delivery.Bufor == "Do w.";

            // Kolory tła i efektu glow w zależności od statusu
            Color bgColor, glowColor;
            bool shouldPulse = false;

            if (isConfirmed)
            {
                bgColor = Color.FromRgb(200, 230, 201); // Light green
                glowColor = Color.FromRgb(76, 175, 80); // Green glow
            }
            else if (isPartiallyConfirmed)
            {
                bgColor = Color.FromRgb(255, 249, 196); // Light yellow
                glowColor = Color.FromRgb(255, 193, 7); // Yellow glow
                shouldPulse = true; // Pulsuj częściowo potwierdzone
            }
            else
            {
                bgColor = Color.FromRgb(227, 242, 253); // Light blue
                glowColor = Color.FromRgb(33, 150, 243); // Blue glow
                shouldPulse = true; // Pulsuj planowane
            }

            var row = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand
            };

            // Add pulsing glow effect for planned/partially confirmed deliveries
            if (shouldPulse)
            {
                var glowEffect = new DropShadowEffect
                {
                    Color = glowColor,
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.3
                };
                row.Effect = glowEffect;

                var pulseOpacity = new DoubleAnimation
                {
                    From = 0.2,
                    To = 0.6,
                    Duration = TimeSpan.FromMilliseconds(900),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var pulseBlur = new DoubleAnimation
                {
                    From = 3,
                    To = 10,
                    Duration = TimeSpan.FromMilliseconds(900),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
                glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);
            }

            // Hover effect
            var originalBgColor = bgColor;
            row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
            row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(originalBgColor);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Hodowca
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Auto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Status

            // Hodowca name with indicator for planned
            var hodowcaPrefix = shouldPulse ? "○ " : "✓ ";
            var hodowcaText = new TextBlock
            {
                Text = hodowcaPrefix + delivery.Dostawca,
                FontSize = 11,
                FontWeight = shouldPulse ? FontWeights.Normal : FontWeights.SemiBold,
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
                FontWeight = FontWeights.SemiBold,
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

            // Status (Bufor) with color coding and glow
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

            // Add subtle glow to status badge
            if (shouldPulse)
            {
                statusBorder.Effect = new DropShadowEffect
                {
                    Color = statusBg,
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }

            var statusDisplayText = delivery.Bufor switch
            {
                "Potwierdzony" => "Potw.",
                "B.Kontr." => "B.Ko.",
                "B.Wolny" => "B.Wo.",
                "Do w." => "Do w.",
                "Planowany" => "Plan.",
                "" or null => "Plan.",
                _ => delivery.Bufor.Length > 6 ? delivery.Bufor.Substring(0, 5) + "." : delivery.Bufor
            };

            var statusText = new TextBlock
            {
                Text = statusDisplayText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
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
