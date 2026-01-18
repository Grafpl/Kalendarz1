using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    public partial class NotificationWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string operatorId;
        private List<TaskNotification> notifications = new List<TaskNotification>();

        public event EventHandler OpenPanelRequested;
        public event EventHandler<TimeSpan> SnoozeRequested;

        public NotificationWindow(string userId)
        {
            InitializeComponent();
            operatorId = userId;
            PositionWindow();
            LoadNotifications();
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;
        }

        private void LoadNotifications()
        {
            notifications.Clear();
            contentPanel.Children.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT DISTINCT
                            Z.ID,
                            Z.TypZadania,
                            Z.TerminWykonania,
                            Z.Priorytet,
                            Z.Wykonane
                        FROM Zadania Z
                        LEFT JOIN ZadaniaPrzypisani zp ON Z.ID = zp.ZadanieID
                        WHERE (Z.OperatorID = @id OR zp.OperatorID = @id)
                          AND Z.Wykonane = 0
                        ORDER BY Z.TerminWykonania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            notifications.Add(new TaskNotification
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                DueDate = reader.GetDateTime(2),
                                Priority = reader.GetInt32(3),
                                IsCompleted = reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading notifications: {ex.Message}");
            }

            BuildContent();
        }

        private void BuildContent()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            var overdue = notifications.Where(t => t.DueDate.Date < today).ToList();
            var todayUrgent = notifications.Where(t => t.DueDate.Date == today && t.DueDate <= now.AddHours(1)).ToList();
            var todayOther = notifications.Where(t => t.DueDate.Date == today && t.DueDate > now.AddHours(1)).ToList();
            var tomorrow = notifications.Where(t => t.DueDate.Date == today.AddDays(1)).ToList();

            int totalCount = overdue.Count + todayUrgent.Count + todayOther.Count + tomorrow.Count;

            if (totalCount == 0)
            {
                txtSubtitle.Text = "Brak pilnych przypomnień";
                contentPanel.Children.Add(CreateEmptyState());
                return;
            }

            txtSubtitle.Text = $"Masz {totalCount} zadań do przejrzenia";

            if (overdue.Count > 0)
                AddSection("Zaległe", "#f44336", "⚠️", overdue, true);

            if (todayUrgent.Count > 0)
                AddSection("Za chwilę", "#FF5722", "🔥", todayUrgent, true);

            if (todayOther.Count > 0)
                AddSection("Dziś", "#FF9800", "📅", todayOther, false);

            if (tomorrow.Count > 0)
                AddSection("Jutro", "#2196F3", "📆", tomorrow, false);
        }

        private UIElement CreateEmptyState()
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 40)
            };

            stack.Children.Add(new TextBlock
            {
                Text = "✅",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Wszystko pod kontrolą!",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });

            return stack;
        }

        private void AddSection(string title, string colorHex, string icon, List<TaskNotification> tasks, bool isUrgent)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);

            // Header sekcji
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 8)
            };

            header.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            header.Children.Add(new TextBlock
            {
                Text = $"{title} ({tasks.Count})",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });

            contentPanel.Children.Add(header);

            // Lista zadań
            foreach (var task in tasks.Take(5))
            {
                contentPanel.Children.Add(CreateTaskItem(task, color, isUrgent));
            }

            if (tasks.Count > 5)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = $"... i {tasks.Count - 5} więcej",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    Margin = new Thickness(10, 5, 0, 0)
                });
            }
        }

        private Border CreateTaskItem(TaskNotification task, Color sectionColor, bool isUrgent)
        {
            var item = new Border
            {
                Background = isUrgent
                    ? new SolidColorBrush(Color.FromArgb(30, sectionColor.R, sectionColor.G, sectionColor.B))
                    : new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x42)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, sectionColor.R, sectionColor.G, sectionColor.B)),
                BorderThickness = new Thickness(0, 0, 0, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Tytuł + priorytet
            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var priorityColor = task.Priority == 3 ? Color.FromRgb(244, 67, 54) :
                               task.Priority == 2 ? Color.FromRgb(255, 152, 0) :
                               Color.FromRgb(76, 175, 80);

            titleStack.Children.Add(new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(priorityColor),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            titleStack.Children.Add(new TextBlock
            {
                Text = task.Title,
                Foreground = Brushes.White,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 220
            });

            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            // Czas
            var timeText = GetRelativeTime(task.DueDate);
            var timeBlock = new TextBlock
            {
                Text = timeText,
                Foreground = isUrgent
                    ? new SolidColorBrush(sectionColor)
                    : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = isUrgent ? FontWeights.SemiBold : FontWeights.Normal
            };
            Grid.SetColumn(timeBlock, 1);
            grid.Children.Add(timeBlock);

            item.Child = grid;
            return item;
        }

        private string GetRelativeTime(DateTime dateTime)
        {
            var now = DateTime.Now;
            var diff = dateTime - now;

            if (dateTime.Date < DateTime.Today)
            {
                var days = (DateTime.Today - dateTime.Date).Days;
                return days == 1 ? "wczoraj" : $"{days} dni temu";
            }

            if (dateTime.Date == DateTime.Today)
            {
                if (diff.TotalMinutes < 0)
                    return "minęło";
                if (diff.TotalMinutes < 60)
                    return $"za {(int)diff.TotalMinutes} min";
                return $"za {(int)diff.TotalHours} godz";
            }

            if (dateTime.Date == DateTime.Today.AddDays(1))
            {
                return dateTime.ToString("HH:mm");
            }

            return dateTime.ToString("dd.MM HH:mm");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSnooze_Click(object sender, RoutedEventArgs e)
        {
            var selected = cmbSnooze.SelectedIndex;
            TimeSpan snoozeTime;

            switch (selected)
            {
                case 0: snoozeTime = TimeSpan.FromMinutes(15); break;
                case 1: snoozeTime = TimeSpan.FromMinutes(30); break;
                case 2: snoozeTime = TimeSpan.FromHours(1); break;
                case 3: snoozeTime = TimeSpan.FromHours(2); break;
                case 4: snoozeTime = TimeSpan.FromHours(24); break;
                default: snoozeTime = TimeSpan.FromMinutes(30); break;
            }

            SnoozeRequested?.Invoke(this, snoozeTime);
            Close();
        }

        private void BtnOpenPanel_Click(object sender, RoutedEventArgs e)
        {
            OpenPanelRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        public bool HasNotifications => notifications.Any(t =>
            t.DueDate.Date <= DateTime.Today.AddDays(1) || t.DueDate < DateTime.Today);
    }

    public class TaskNotification
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime DueDate { get; set; }
        public int Priority { get; set; }
        public bool IsCompleted { get; set; }
    }
}
