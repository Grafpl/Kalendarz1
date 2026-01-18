using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    public partial class NotificationWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string operatorId;
        private List<TaskNotification> tasks = new List<TaskNotification>();
        private List<MeetingNotification> meetings = new List<MeetingNotification>();

        private enum ViewMode { All, Tasks, Meetings }
        private ViewMode currentView = ViewMode.All;

        // Company colors
        private static readonly Color PrimaryGreen = (Color)ColorConverter.ConvertFromString("#27AE60");
        private static readonly Color AlertRed = (Color)ColorConverter.ConvertFromString("#E74C3C");
        private static readonly Color WarningOrange = (Color)ColorConverter.ConvertFromString("#F39C12");
        private static readonly Color InfoBlue = (Color)ColorConverter.ConvertFromString("#3498DB");
        private static readonly Color Purple = (Color)ColorConverter.ConvertFromString("#9B59B6");
        private static readonly Color DarkBg = (Color)ColorConverter.ConvertFromString("#1A242F");
        private static readonly Color CardBg = (Color)ColorConverter.ConvertFromString("#212F3D");
        private static readonly Color CardBgHover = (Color)ColorConverter.ConvertFromString("#283747");
        private static readonly Color TextGray = (Color)ColorConverter.ConvertFromString("#7F8C8D");
        private static readonly Color TextLight = (Color)ColorConverter.ConvertFromString("#BDC3C7");

        public event EventHandler OpenPanelRequested;
        public event EventHandler OpenMeetingsRequested;
        public event EventHandler<TimeSpan> SnoozeRequested;

        public NotificationWindow(string userId)
        {
            InitializeComponent();
            operatorId = userId;
            Loaded += NotificationWindow_Loaded;
        }

        private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
            LoadTasks();
            LoadMeetings();
            UpdateStatistics();
            BuildContent();
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        }

        #region Tab Navigation

        private void TabAll_Click(object sender, RoutedEventArgs e)
        {
            currentView = ViewMode.All;
            UpdateTabStyles();
            BuildContent();
        }

        private void TabTasks_Click(object sender, RoutedEventArgs e)
        {
            currentView = ViewMode.Tasks;
            UpdateTabStyles();
            BuildContent();
        }

        private void TabMeetings_Click(object sender, RoutedEventArgs e)
        {
            currentView = ViewMode.Meetings;
            UpdateTabStyles();
            BuildContent();
        }

        private void UpdateTabStyles()
        {
            var activeStyle = (Style)FindResource("TabButtonActive");
            var inactiveStyle = (Style)FindResource("TabButton");

            tabAll.Style = currentView == ViewMode.All ? activeStyle : inactiveStyle;
            tabTasks.Style = currentView == ViewMode.Tasks ? activeStyle : inactiveStyle;
            tabMeetings.Style = currentView == ViewMode.Meetings ? activeStyle : inactiveStyle;
        }

        #endregion

        #region Statistics

        private void UpdateStatistics()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            // Urgent: overdue tasks + tasks due within 2h + meetings within 1h
            var urgentTasksCount = tasks.Count(t => t.DueDate.Date < today || (t.DueDate.Date == today && t.DueDate <= now.AddHours(2)));
            var urgentMeetingsCount = meetings.Count(m => m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 60);
            var urgentTotal = urgentTasksCount + urgentMeetingsCount;

            // Today count
            var todayTasksCount = tasks.Count(t => t.DueDate.Date == today);
            var todayMeetingsCount = meetings.Count(m => m.MeetingDate.Date == today);
            var todayTotal = todayTasksCount + todayMeetingsCount;

            // Update UI
            txtUrgentCount.Text = urgentTotal.ToString();
            txtUrgentLabel.Text = GetPolishPlural(urgentTotal, "element", "elementy", "elementów");

            txtTasksCount.Text = tasks.Count.ToString();
            txtTasksLabel.Text = GetPolishPlural(tasks.Count, "zadanie", "zadania", "zadań");

            txtMeetingsCount.Text = meetings.Count.ToString();
            txtMeetingsLabel.Text = GetPolishPlural(meetings.Count, "spotkanie", "spotkania", "spotkań");

            txtTodayCount.Text = todayTotal.ToString();
            txtTodayLabel.Text = "na dziś";

            // Update subtitle
            var dayName = DateTime.Today.ToString("dddd, d MMMM", new System.Globalization.CultureInfo("pl-PL"));
            txtSubtitle.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);
        }

        private string GetPolishPlural(int count, string one, string few, string many)
        {
            if (count == 1) return one;
            if (count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 10 || count % 100 >= 20)) return few;
            return many;
        }

        #endregion

        #region Load Data

        private void LoadTasks()
        {
            tasks.Clear();

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
                            Z.Wykonane,
                            Z.Opis
                        FROM Zadania Z
                        LEFT JOIN ZadaniaPrzypisani zp ON Z.ID = zp.ZadanieID
                        WHERE (Z.OperatorID = @id OR zp.OperatorID = @id)
                          AND Z.Wykonane = 0
                          AND Z.TerminWykonania <= DATEADD(DAY, 7, GETDATE())
                        ORDER BY Z.Priorytet DESC, Z.TerminWykonania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tasks.Add(new TaskNotification
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                DueDate = reader.GetDateTime(2),
                                Priority = reader.GetInt32(3),
                                IsCompleted = reader.GetBoolean(4),
                                Description = reader.IsDBNull(5) ? "" : reader.GetString(5)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tasks: {ex.Message}");
            }
        }

        private void LoadMeetings()
        {
            meetings.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT DISTINCT
                            s.SpotkaniID,
                            s.Tytul,
                            s.DataSpotkania,
                            s.CzasTrwaniaMin,
                            s.Lokalizacja,
                            s.Status,
                            s.OrganizatorID,
                            o.Nazwa AS OrganizatorNazwa,
                            s.LinkSpotkania,
                            s.Opis
                        FROM Spotkania s
                        LEFT JOIN Operatorzy o ON s.OrganizatorID = o.ID
                        LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                        WHERE (s.OrganizatorID = @id OR su.OperatorID = @id)
                          AND s.Status = 'Zaplanowane'
                          AND s.DataSpotkania >= GETDATE()
                          AND s.DataSpotkania <= DATEADD(DAY, 14, GETDATE())
                        ORDER BY s.DataSpotkania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var meetingDate = reader.GetDateTime(2);
                            meetings.Add(new MeetingNotification
                            {
                                Id = reader.GetInt64(0),
                                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                MeetingDate = meetingDate,
                                DurationMin = reader.IsDBNull(3) ? 60 : reader.GetInt32(3),
                                Location = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Status = reader.IsDBNull(5) ? "Zaplanowane" : reader.GetString(5),
                                OrganizerId = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                OrganizerName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                MeetingLink = reader.IsDBNull(8) ? null : reader.GetString(8),
                                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                MinutesToMeeting = (int)(meetingDate - DateTime.Now).TotalMinutes
                            });
                        }
                    }

                    // Load attendees for each meeting
                    foreach (var meeting in meetings)
                    {
                        LoadMeetingAttendees(conn, meeting);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading meetings: {ex.Message}");
            }
        }

        private void LoadMeetingAttendees(SqlConnection conn, MeetingNotification meeting)
        {
            try
            {
                var cmd = new SqlCommand(@"
                    SELECT
                        su.OperatorID,
                        o.Nazwa,
                        su.StatusZaproszenia,
                        su.CzyObowiazkowy
                    FROM SpotkaniaUczestnicy su
                    LEFT JOIN Operatorzy o ON su.OperatorID = o.ID
                    WHERE su.SpotkaniID = @meetingId", conn);

                cmd.Parameters.AddWithValue("@meetingId", meeting.Id);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        meeting.Attendees.Add(new MeetingAttendee
                        {
                            OperatorId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Status = reader.IsDBNull(2) ? "Oczekuje" : reader.GetString(2),
                            IsRequired = !reader.IsDBNull(3) && reader.GetBoolean(3)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading attendees: {ex.Message}");
            }
        }

        #endregion

        #region Build Content

        private void BuildContent()
        {
            contentPanel.Children.Clear();

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Determine what to show based on current view
            var showTasks = currentView == ViewMode.All || currentView == ViewMode.Tasks;
            var showMeetings = currentView == ViewMode.All || currentView == ViewMode.Meetings;

            // Group tasks by urgency
            var overdueTasks = showTasks ? tasks.Where(t => t.DueDate.Date < today).ToList() : new List<TaskNotification>();
            var urgentTasks = showTasks ? tasks.Where(t => t.DueDate.Date == today && t.DueDate <= now.AddHours(2)).ToList() : new List<TaskNotification>();
            var todayTasks = showTasks ? tasks.Where(t => t.DueDate.Date == today && t.DueDate > now.AddHours(2)).ToList() : new List<TaskNotification>();
            var tomorrowTasks = showTasks ? tasks.Where(t => t.DueDate.Date == today.AddDays(1)).ToList() : new List<TaskNotification>();
            var laterTasks = showTasks ? tasks.Where(t => t.DueDate.Date > today.AddDays(1)).ToList() : new List<TaskNotification>();

            // Group meetings
            var urgentMeetings = showMeetings ? meetings.Where(m => m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 60).ToList() : new List<MeetingNotification>();
            var todayMeetings = showMeetings ? meetings.Where(m => m.MeetingDate.Date == today && m.MinutesToMeeting > 60).ToList() : new List<MeetingNotification>();
            var tomorrowMeetings = showMeetings ? meetings.Where(m => m.MeetingDate.Date == today.AddDays(1)).ToList() : new List<MeetingNotification>();
            var laterMeetings = showMeetings ? meetings.Where(m => m.MeetingDate.Date > today.AddDays(1)).ToList() : new List<MeetingNotification>();

            int totalItems = overdueTasks.Count + urgentTasks.Count + todayTasks.Count + tomorrowTasks.Count + laterTasks.Count +
                           urgentMeetings.Count + todayMeetings.Count + tomorrowMeetings.Count + laterMeetings.Count;

            if (totalItems == 0)
            {
                contentPanel.Children.Add(CreateEmptyState());
                return;
            }

            // OVERDUE TASKS (highest priority)
            if (overdueTasks.Count > 0)
            {
                AddSectionHeader("Zaległe", "Przekroczony termin", AlertRed, overdueTasks.Count);
                foreach (var task in overdueTasks)
                    contentPanel.Children.Add(CreateTaskCard(task, AlertRed, true));
            }

            // URGENT section - starting within 1-2 hours
            if (urgentTasks.Count > 0 || urgentMeetings.Count > 0)
            {
                var urgentCount = urgentTasks.Count + urgentMeetings.Count;
                AddSectionHeader("Pilne", "W ciągu najbliższych godzin", AlertRed, urgentCount);

                foreach (var meeting in urgentMeetings)
                    contentPanel.Children.Add(CreateMeetingCard(meeting, AlertRed, true));

                foreach (var task in urgentTasks)
                    contentPanel.Children.Add(CreateTaskCard(task, AlertRed, false));
            }

            // TODAY section
            if (todayTasks.Count > 0 || todayMeetings.Count > 0)
            {
                var todayCount = todayTasks.Count + todayMeetings.Count;
                AddSectionHeader("Dzisiaj", DateTime.Today.ToString("d MMMM", new System.Globalization.CultureInfo("pl-PL")), WarningOrange, todayCount);

                foreach (var meeting in todayMeetings.OrderBy(m => m.MeetingDate))
                    contentPanel.Children.Add(CreateMeetingCard(meeting, WarningOrange, false));

                foreach (var task in todayTasks.OrderBy(t => t.Priority).ThenBy(t => t.DueDate))
                    contentPanel.Children.Add(CreateTaskCard(task, WarningOrange, false));
            }

            // TOMORROW section
            if (tomorrowTasks.Count > 0 || tomorrowMeetings.Count > 0)
            {
                var tomorrowCount = tomorrowTasks.Count + tomorrowMeetings.Count;
                AddSectionHeader("Jutro", DateTime.Today.AddDays(1).ToString("d MMMM", new System.Globalization.CultureInfo("pl-PL")), InfoBlue, tomorrowCount);

                foreach (var meeting in tomorrowMeetings.OrderBy(m => m.MeetingDate))
                    contentPanel.Children.Add(CreateMeetingCard(meeting, InfoBlue, false));

                foreach (var task in tomorrowTasks.OrderByDescending(t => t.Priority))
                    contentPanel.Children.Add(CreateTaskCard(task, InfoBlue, false));
            }

            // LATER section
            if (laterTasks.Count > 0 || laterMeetings.Count > 0)
            {
                var laterCount = laterTasks.Count + laterMeetings.Count;
                AddSectionHeader("Nadchodzące", "Następne dni", Purple, laterCount);

                // Group by date
                var allLaterItems = new List<(DateTime date, object item)>();
                allLaterItems.AddRange(laterMeetings.Select(m => (m.MeetingDate.Date, (object)m)));
                allLaterItems.AddRange(laterTasks.Select(t => (t.DueDate.Date, (object)t)));

                var groupedByDate = allLaterItems.GroupBy(x => x.date).OrderBy(g => g.Key).Take(5);

                foreach (var group in groupedByDate)
                {
                    // Date subheader
                    AddDateSubheader(group.Key);

                    foreach (var item in group.OrderBy(x => x.item is MeetingNotification m ? m.MeetingDate : ((TaskNotification)x.item).DueDate))
                    {
                        if (item.item is MeetingNotification meeting)
                            contentPanel.Children.Add(CreateMeetingCard(meeting, Purple, false));
                        else if (item.item is TaskNotification task)
                            contentPanel.Children.Add(CreateTaskCard(task, Purple, false));
                    }
                }

                var remaining = laterTasks.Count + laterMeetings.Count - allLaterItems.Take(5).SelectMany(g => groupedByDate).Count();
                if (remaining > 0)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"... i {remaining} więcej",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(TextGray),
                        Margin = new Thickness(16, 8, 0, 12),
                        FontStyle = FontStyles.Italic
                    });
                }
            }

            // Add bottom padding
            contentPanel.Children.Add(new Border { Height = 10 });
        }

        private void AddSectionHeader(string title, string subtitle, Color color, int count)
        {
            var header = new Border
            {
                Margin = new Thickness(0, 12, 0, 8),
                Padding = new Thickness(0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Color indicator + Title
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            leftStack.Children.Add(new Border
            {
                Width = 4,
                Height = 28,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 12, 0)
            });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 1, 0, 0)
            });
            leftStack.Children.Add(titleStack);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // Count badge
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color)
            };

            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            header.Child = grid;
            contentPanel.Children.Add(header);
        }

        private void AddDateSubheader(DateTime date)
        {
            var culture = new System.Globalization.CultureInfo("pl-PL");
            var dayName = date.ToString("dddd", culture);
            dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);

            var subheader = new TextBlock
            {
                Text = $"{dayName}, {date:d MMMM}",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(TextLight),
                Margin = new Thickness(16, 10, 0, 6)
            };

            contentPanel.Children.Add(subheader);
        }

        private Border CreateTaskCard(TaskNotification task, Color sectionColor, bool isOverdue)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(14, 12, 14, 12),
                BorderThickness = new Thickness(0, 0, 4, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, sectionColor.R, sectionColor.G, sectionColor.B)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Hover effect
            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Task icon with priority indicator
            var iconContainer = new Grid { Width = 36, Height = 36, Margin = new Thickness(0, 0, 12, 0) };

            var priorityColor = task.Priority == 3 ? AlertRed : task.Priority == 2 ? WarningOrange : PrimaryGreen;
            var iconBg = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(30, priorityColor.R, priorityColor.G, priorityColor.B))
            };
            iconContainer.Children.Add(iconBg);

            var iconText = new TextBlock
            {
                Text = "✓",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(priorityColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconContainer.Children.Add(iconText);

            // Priority dot
            var priorityDot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(priorityColor),
                BorderBrush = new SolidColorBrush(CardBg),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -2, -2, 0)
            };
            iconContainer.Children.Add(priorityDot);

            Grid.SetColumn(iconContainer, 0);
            mainGrid.Children.Add(iconContainer);

            // Task info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // Title row with type badge
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = task.Title,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 350
            });

            infoStack.Children.Add(titleRow);

            // Details row
            var detailsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            // Type badge
            var typeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            typeBadge.Child = new TextBlock
            {
                Text = "ZADANIE",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextLight)
            };
            detailsRow.Children.Add(typeBadge);

            // Priority badge
            var priorityText = task.Priority == 3 ? "Wysoki" : task.Priority == 2 ? "Średni" : "Niski";
            var priorityBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, priorityColor.R, priorityColor.G, priorityColor.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            priorityBadge.Child = new TextBlock
            {
                Text = priorityText,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(priorityColor)
            };
            detailsRow.Children.Add(priorityBadge);

            // Due time
            detailsRow.Children.Add(new TextBlock
            {
                Text = $"Termin: {task.DueDate:HH:mm}",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray)
            });

            infoStack.Children.Add(detailsRow);

            // Description preview (if exists)
            if (!string.IsNullOrWhiteSpace(task.Description))
            {
                var descPreview = task.Description.Length > 60 ? task.Description.Substring(0, 60) + "..." : task.Description;
                infoStack.Children.Add(new TextBlock
                {
                    Text = descPreview,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(0, 4, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontStyle = FontStyles.Italic
                });
            }

            Grid.SetColumn(infoStack, 1);
            mainGrid.Children.Add(infoStack);

            // Time indicator
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

            var timeText = GetTaskRelativeTime(task.DueDate);
            var timeBlock = new TextBlock
            {
                Text = timeText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isOverdue ? AlertRed : sectionColor),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            timeStack.Children.Add(timeBlock);

            if (!isOverdue)
            {
                timeStack.Children.Add(new TextBlock
                {
                    Text = task.DueDate.ToString("HH:mm"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(TextGray),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            Grid.SetColumn(timeStack, 2);
            mainGrid.Children.Add(timeStack);

            card.Child = mainGrid;
            return card;
        }

        private Border CreateMeetingCard(MeetingNotification meeting, Color sectionColor, bool isUrgent)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(14, 12, 14, 12),
                BorderThickness = new Thickness(0, 0, 4, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Hover effect
            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainStack = new StackPanel();

            // Row 1: Icon + Title + Time
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Meeting icon
            var iconBg = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(30, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBg.Child = new TextBlock
            {
                Text = "📅",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBg, 0);
            row1.Children.Add(iconBg);

            // Meeting info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            infoStack.Children.Add(new TextBlock
            {
                Text = meeting.Title,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 350
            });

            // Details row
            var detailsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            // Type badge
            var typeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            typeBadge.Child = new TextBlock
            {
                Text = "SPOTKANIE",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(InfoBlue)
            };
            detailsRow.Children.Add(typeBadge);

            // Duration
            detailsRow.Children.Add(new TextBlock
            {
                Text = $"{meeting.DurationMin} min",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Location
            if (!string.IsNullOrEmpty(meeting.Location))
            {
                detailsRow.Children.Add(new TextBlock
                {
                    Text = $"📍 {meeting.Location}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(TextGray),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            infoStack.Children.Add(detailsRow);

            Grid.SetColumn(infoStack, 1);
            row1.Children.Add(infoStack);

            // Time indicator
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

            var timeText = GetMeetingRelativeTime(meeting);
            var timeBlock = new TextBlock
            {
                Text = timeText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isUrgent ? AlertRed : sectionColor),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            timeStack.Children.Add(timeBlock);

            timeStack.Children.Add(new TextBlock
            {
                Text = meeting.MeetingDate.ToString("HH:mm"),
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(timeStack, 2);
            row1.Children.Add(timeStack);

            mainStack.Children.Add(row1);

            // Row 2: Attendees
            if (meeting.Attendees.Count > 0)
            {
                var attendeesRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(48, 8, 0, 0)
                };

                // Avatars
                var avatarStack = new StackPanel { Orientation = Orientation.Horizontal };
                var colors = new[] { "#27AE60", "#3498DB", "#9B59B6", "#E74C3C", "#F39C12" };
                var displayCount = Math.Min(meeting.Attendees.Count, 5);

                for (int i = 0; i < displayCount; i++)
                {
                    var attendee = meeting.Attendees[i];
                    var avatar = CreateAvatar(attendee, colors[i % colors.Length], i);
                    avatarStack.Children.Add(avatar);
                }

                if (meeting.Attendees.Count > 5)
                {
                    var moreBadge = new Border
                    {
                        Width = 26,
                        Height = 26,
                        CornerRadius = new CornerRadius(13),
                        Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        BorderBrush = new SolidColorBrush(CardBg),
                        BorderThickness = new Thickness(2),
                        Margin = new Thickness(-8, 0, 0, 0)
                    };
                    moreBadge.Child = new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 5}",
                        FontSize = 9,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    avatarStack.Children.Add(moreBadge);
                }

                attendeesRow.Children.Add(avatarStack);

                // Attendance status
                var accepted = meeting.Attendees.Count(a => a.Status == "Zaakceptowane");
                var pending = meeting.Attendees.Count(a => a.Status == "Oczekuje");

                var statusText = new TextBlock
                {
                    FontSize = 10,
                    Foreground = new SolidColorBrush(TextGray),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };

                if (accepted > 0 && pending > 0)
                    statusText.Text = $"{accepted} potwierdz. / {pending} oczekuje";
                else if (accepted > 0)
                    statusText.Text = $"{accepted} potwierdzonych";
                else
                    statusText.Text = $"{pending} oczekujących";

                attendeesRow.Children.Add(statusText);

                mainStack.Children.Add(attendeesRow);
            }

            // Organizer info
            if (!string.IsNullOrEmpty(meeting.OrganizerName))
            {
                mainStack.Children.Add(new TextBlock
                {
                    Text = $"Organizator: {meeting.OrganizerName}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(48, 4, 0, 0),
                    FontStyle = FontStyles.Italic
                });
            }

            card.Child = mainStack;
            return card;
        }

        private Border CreateAvatar(MeetingAttendee attendee, string colorHex, int index)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var initials = GetInitials(attendee.Name);

            // Status-based border color
            var borderColor = attendee.Status == "Zaakceptowane" ? PrimaryGreen :
                            attendee.Status == "Odrzucone" ? AlertRed : TextGray;

            var avatar = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(index > 0 ? -8 : 0, 0, 0, 0)
            };

            avatar.Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Tooltip with status
            var statusText = attendee.Status == "Zaakceptowane" ? "Potwierdzone" :
                           attendee.Status == "Odrzucone" ? "Odrzucone" : "Oczekuje";
            avatar.ToolTip = $"{attendee.Name}\n{statusText}{(attendee.IsRequired ? " • Obowiązkowy" : "")}";

            return avatar;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private UIElement CreateEmptyState()
        {
            var container = new Border
            {
                Margin = new Thickness(0, 60, 0, 60)
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Icon
            var iconBorder = new Border
            {
                Width = 70,
                Height = 70,
                CornerRadius = new CornerRadius(35),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconBorder.Background = new SolidColorBrush(Color.FromArgb(30, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B));
            iconBorder.Child = new TextBlock
            {
                Text = "✓",
                FontSize = 32,
                Foreground = new SolidColorBrush(PrimaryGreen),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(iconBorder);

            // Title
            stack.Children.Add(new TextBlock
            {
                Text = "Wszystko pod kontrolą!",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            });

            // Subtitle based on view
            var subtitle = currentView switch
            {
                ViewMode.Tasks => "Brak zadań w tym okresie",
                ViewMode.Meetings => "Brak zaplanowanych spotkań",
                _ => "Brak pilnych zadań i spotkań"
            };

            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 13,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });

            container.Child = stack;
            return container;
        }

        private string GetTaskRelativeTime(DateTime dateTime)
        {
            var now = DateTime.Now;
            var diff = dateTime - now;

            if (dateTime.Date < DateTime.Today)
            {
                var days = (DateTime.Today - dateTime.Date).Days;
                return days == 1 ? "wczoraj" : $"{days}d temu";
            }

            if (dateTime.Date == DateTime.Today)
            {
                if (diff.TotalMinutes < 0) return "minęło";
                if (diff.TotalMinutes < 60) return $"za {(int)diff.TotalMinutes}m";
                return $"za {(int)diff.TotalHours}h";
            }

            if (dateTime.Date == DateTime.Today.AddDays(1))
                return "jutro";

            return dateTime.ToString("dd.MM");
        }

        private string GetMeetingRelativeTime(MeetingNotification meeting)
        {
            if (meeting.MinutesToMeeting <= 0) return "teraz!";
            if (meeting.MinutesToMeeting < 60) return $"za {meeting.MinutesToMeeting}m";
            if (meeting.MinutesToMeeting < 1440) return $"za {meeting.MinutesToMeeting / 60}h";
            if (meeting.MeetingDate.Date == DateTime.Today.AddDays(1)) return "jutro";
            return meeting.MeetingDate.ToString("dd.MM");
        }

        #endregion

        #region Button Handlers

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

        private void BtnOpenTasksPanel_Click(object sender, RoutedEventArgs e)
        {
            OpenPanelRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void BtnOpenMeetingsPanel_Click(object sender, RoutedEventArgs e)
        {
            OpenMeetingsRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        #endregion

        public bool HasNotifications => tasks.Count > 0 || meetings.Count > 0;
        public int TaskCount => tasks.Count;
        public int MeetingCount => meetings.Count;
    }

    #region Models

    public class TaskNotification
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime DueDate { get; set; }
        public int Priority { get; set; }
        public bool IsCompleted { get; set; }
        public string Description { get; set; }
    }

    public class MeetingNotification
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public DateTime MeetingDate { get; set; }
        public int DurationMin { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public string OrganizerId { get; set; }
        public string OrganizerName { get; set; }
        public string MeetingLink { get; set; }
        public string Description { get; set; }
        public int MinutesToMeeting { get; set; }
        public List<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();
    }

    public class MeetingAttendee
    {
        public string OperatorId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public bool IsRequired { get; set; }
    }

    #endregion
}
