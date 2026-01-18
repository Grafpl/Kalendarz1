using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    public partial class NotificationWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string operatorId;
        private List<TaskNotification> tasks = new List<TaskNotification>();
        private List<MeetingNotification> meetings = new List<MeetingNotification>();
        private DispatcherTimer pulseTimer;

        private enum ViewMode { All, Tasks, Meetings }
        private ViewMode currentView = ViewMode.All;

        // Company colors
        private static readonly Color PrimaryGreen = (Color)ColorConverter.ConvertFromString("#27AE60");
        private static readonly Color AlertRed = (Color)ColorConverter.ConvertFromString("#E74C3C");
        private static readonly Color WarningOrange = (Color)ColorConverter.ConvertFromString("#F39C12");
        private static readonly Color InfoBlue = (Color)ColorConverter.ConvertFromString("#3498DB");
        private static readonly Color Purple = (Color)ColorConverter.ConvertFromString("#9B59B6");
        private static readonly Color LiveRed = (Color)ColorConverter.ConvertFromString("#E74C3C");
        private static readonly Color DarkBg = (Color)ColorConverter.ConvertFromString("#1A242F");
        private static readonly Color CardBg = (Color)ColorConverter.ConvertFromString("#212F3D");
        private static readonly Color CardBgHover = (Color)ColorConverter.ConvertFromString("#283747");
        private static readonly Color CardBgLive = (Color)ColorConverter.ConvertFromString("#2D1F1F");
        private static readonly Color TextGray = (Color)ColorConverter.ConvertFromString("#7F8C8D");
        private static readonly Color TextLight = (Color)ColorConverter.ConvertFromString("#BDC3C7");

        // Avatar colors palette
        private static readonly string[] AvatarColors = new[]
        {
            "#27AE60", "#3498DB", "#9B59B6", "#E74C3C", "#F39C12",
            "#1ABC9C", "#E91E63", "#00BCD4", "#FF5722", "#607D8B"
        };

        public event EventHandler OpenPanelRequested;
        public event EventHandler OpenMeetingsRequested;
        public event EventHandler<TimeSpan> SnoozeRequested;

        public NotificationWindow(string userId)
        {
            InitializeComponent();
            operatorId = userId;
            Loaded += NotificationWindow_Loaded;
            Closed += NotificationWindow_Closed;
        }

        private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
            LoadTasks();
            LoadMeetings();
            UpdateStatistics();
            BuildLiveNowSection();
            BuildContent();
            StartPulseAnimation();
        }

        private void NotificationWindow_Closed(object sender, EventArgs e)
        {
            pulseTimer?.Stop();
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        }

        private void StartPulseAnimation()
        {
            pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            bool isVisible = true;

            pulseTimer.Tick += (s, e) =>
            {
                isVisible = !isVisible;
                var opacity = isVisible ? 1.0 : 0.4;

                liveDot.Opacity = opacity;
                liveIndicator.Opacity = opacity;
            };

            pulseTimer.Start();
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

            // Live now meetings
            var liveMeetings = meetings.Where(m => m.IsLive).ToList();
            txtLiveCount.Text = liveMeetings.Count.ToString();
            txtLiveLabel.Text = GetPolishPlural(liveMeetings.Count, "spotkanie", "spotkania", "spotkań");

            // Urgent: overdue tasks + tasks due within 2h + meetings within 30min
            var urgentTasksCount = tasks.Count(t => t.DueDate.Date < today || (t.DueDate.Date == today && t.DueDate <= now.AddHours(2)));
            var urgentMeetingsCount = meetings.Count(m => !m.IsLive && m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 30);
            var urgentTotal = urgentTasksCount + urgentMeetingsCount;

            txtUrgentCount.Text = urgentTotal.ToString();
            txtUrgentLabel.Text = GetPolishPlural(urgentTotal, "element", "elementy", "elementów");

            txtTasksCount.Text = tasks.Count.ToString();
            txtTasksLabel.Text = GetPolishPlural(tasks.Count, "zadanie", "zadania", "zadań");

            txtMeetingsCount.Text = meetings.Count.ToString();
            txtMeetingsLabel.Text = GetPolishPlural(meetings.Count, "spotkanie", "spotkania", "spotkań");

            // Update subtitle with date
            var culture = new System.Globalization.CultureInfo("pl-PL");
            var dayName = DateTime.Today.ToString("dddd, d MMMM yyyy", culture);
            txtSubtitle.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);

            // Highlight live card if there are live meetings
            if (liveMeetings.Count > 0)
            {
                liveNowCard.Background = new SolidColorBrush(CardBgLive);
                liveNowCard.BorderBrush = new SolidColorBrush(LiveRed);
                liveNowCard.BorderThickness = new Thickness(1);
            }
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
                            Z.Opis,
                            Z.DataUtworzenia,
                            o.Name AS OperatorNazwa
                        FROM Zadania Z
                        LEFT JOIN ZadaniaPrzypisani zp ON Z.ID = zp.ZadanieID
                        LEFT JOIN operators o ON Z.OperatorID = o.ID
                        WHERE (Z.OperatorID = @id OR zp.OperatorID = @id)
                          AND Z.Wykonane = 0
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
                                Description = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                CreatedDate = reader.IsDBNull(6) ? DateTime.Now : reader.GetDateTime(6),
                                CreatorName = reader.IsDBNull(7) ? "" : reader.GetString(7)
                            });
                        }
                    }

                    // Load assigned users for each task
                    foreach (var task in tasks)
                    {
                        LoadTaskAssignees(conn, task);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tasks: {ex.Message}");
            }
        }

        private void LoadTaskAssignees(SqlConnection conn, TaskNotification task)
        {
            try
            {
                var cmd = new SqlCommand(@"
                    SELECT OperatorID, OperatorNazwa
                    FROM ZadaniaPrzypisani
                    WHERE ZadanieID = @taskId", conn);

                cmd.Parameters.AddWithValue("@taskId", task.Id);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        task.Assignees.Add(new TaskAssignee
                        {
                            OperatorId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            Name = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading task assignees: {ex.Message}");
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
                            s.OrganizatorNazwa,
                            s.LinkSpotkania,
                            s.Opis,
                            s.Priorytet
                        FROM Spotkania s
                        LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                        WHERE (s.OrganizatorID = @id OR su.OperatorID = @id)
                          AND s.Status = 'Zaplanowane'
                        ORDER BY s.DataSpotkania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var meetingDate = reader.GetDateTime(2);
                            var durationMin = reader.IsDBNull(3) ? 60 : reader.GetInt32(3);
                            var now = DateTime.Now;
                            var endTime = meetingDate.AddMinutes(durationMin);
                            var minutesToMeeting = (int)(meetingDate - now).TotalMinutes;
                            var isLive = now >= meetingDate && now <= endTime;

                            meetings.Add(new MeetingNotification
                            {
                                Id = reader.GetInt64(0),
                                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                MeetingDate = meetingDate,
                                DurationMin = durationMin,
                                Location = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Status = reader.IsDBNull(5) ? "Zaplanowane" : reader.GetString(5),
                                OrganizerId = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                OrganizerName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                MeetingLink = reader.IsDBNull(8) ? null : reader.GetString(8),
                                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                Priority = reader.IsDBNull(10) ? "Normalny" : reader.GetString(10),
                                MinutesToMeeting = minutesToMeeting,
                                IsLive = isLive,
                                EndTime = endTime
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
                        OperatorID,
                        OperatorNazwa,
                        StatusZaproszenia,
                        CzyObowiazkowy
                    FROM SpotkaniaUczestnicy
                    WHERE SpotkaniID = @meetingId", conn);

                cmd.Parameters.AddWithValue("@meetingId", meeting.Id);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        meeting.Attendees.Add(new MeetingAttendee
                        {
                            OperatorId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            Name = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1),
                            Status = reader.IsDBNull(2) ? "Oczekuje" : reader.GetString(2),
                            IsRequired = !reader.IsDBNull(3) && reader.GetBoolean(3),
                            Email = ""
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

        #region Build Live Now Section

        private void BuildLiveNowSection()
        {
            var liveMeetings = meetings.Where(m => m.IsLive).ToList();

            if (liveMeetings.Count == 0)
            {
                liveNowSection.Visibility = Visibility.Collapsed;
                return;
            }

            liveNowSection.Visibility = Visibility.Visible;
            liveMeetingsPanel.Children.Clear();

            foreach (var meeting in liveMeetings)
            {
                liveMeetingsPanel.Children.Add(CreateLiveMeetingCard(meeting));
            }
        }

        private Border CreateLiveMeetingCard(MeetingNotification meeting)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, LiveRed.R, LiveRed.G, LiveRed.B)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var mainStack = new StackPanel();

            // Header row with title and time remaining
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Live icon
            var liveIcon = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(60, LiveRed.R, LiveRed.G, LiveRed.B)),
                Margin = new Thickness(0, 0, 14, 0)
            };
            liveIcon.Child = new TextBlock
            {
                Text = "🔴",
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(liveIcon, 0);
            headerRow.Children.Add(liveIcon);

            // Title and info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = meeting.Title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var detailsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            detailsRow.Children.Add(new TextBlock
            {
                Text = $"Rozpoczęto o {meeting.MeetingDate:HH:mm}",
                FontSize = 12,
                Foreground = new SolidColorBrush(TextLight)
            });

            if (!string.IsNullOrEmpty(meeting.Location))
            {
                detailsRow.Children.Add(new TextBlock
                {
                    Text = $"  •  📍 {meeting.Location}",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(TextGray)
                });
            }
            infoStack.Children.Add(detailsRow);

            Grid.SetColumn(infoStack, 1);
            headerRow.Children.Add(infoStack);

            // Time remaining
            var remainingMinutes = (int)(meeting.EndTime - DateTime.Now).TotalMinutes;
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            timeStack.Children.Add(new TextBlock
            {
                Text = "TRWA",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(LiveRed),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            timeStack.Children.Add(new TextBlock
            {
                Text = remainingMinutes > 0 ? $"jeszcze {remainingMinutes}m" : "kończy się",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            Grid.SetColumn(timeStack, 2);
            headerRow.Children.Add(timeStack);

            mainStack.Children.Add(headerRow);

            // Attendees row with LARGE avatars
            if (meeting.Attendees.Count > 0)
            {
                var attendeesSection = new StackPanel { Margin = new Thickness(58, 12, 0, 0) };

                // Avatar row
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal };
                var displayCount = Math.Min(meeting.Attendees.Count, 8);

                for (int i = 0; i < displayCount; i++)
                {
                    var attendee = meeting.Attendees[i];
                    avatarRow.Children.Add(CreateLargeAvatar(attendee, i, 38));
                }

                if (meeting.Attendees.Count > 8)
                {
                    var moreCount = new Border
                    {
                        Width = 38,
                        Height = 38,
                        CornerRadius = new CornerRadius(19),
                        Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                        BorderBrush = new SolidColorBrush(CardBgLive),
                        BorderThickness = new Thickness(3),
                        Margin = new Thickness(-10, 0, 0, 0)
                    };
                    moreCount.Child = new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 8}",
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    avatarRow.Children.Add(moreCount);
                }

                attendeesSection.Children.Add(avatarRow);

                // Status summary
                var accepted = meeting.Attendees.Count(a => a.Status == "Zaakceptowane");
                var statusText = new TextBlock
                {
                    Text = $"{accepted} z {meeting.Attendees.Count} uczestników potwierdziło",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(0, 8, 0, 0)
                };
                attendeesSection.Children.Add(statusText);

                mainStack.Children.Add(attendeesSection);
            }

            card.Child = mainStack;
            return card;
        }

        #endregion

        #region Build Content

        private void BuildContent()
        {
            contentPanel.Children.Clear();

            var today = DateTime.Today;
            var now = DateTime.Now;

            var showTasks = currentView == ViewMode.All || currentView == ViewMode.Tasks;
            var showMeetings = currentView == ViewMode.All || currentView == ViewMode.Meetings;

            // Exclude live meetings from regular content (they're shown in special section)
            var nonLiveMeetings = meetings.Where(m => !m.IsLive).ToList();

            // Group tasks
            var overdueTasks = showTasks ? tasks.Where(t => t.DueDate.Date < today).ToList() : new List<TaskNotification>();
            var urgentTasks = showTasks ? tasks.Where(t => t.DueDate.Date == today && t.DueDate <= now.AddHours(2)).ToList() : new List<TaskNotification>();
            var todayTasks = showTasks ? tasks.Where(t => t.DueDate.Date == today && t.DueDate > now.AddHours(2)).ToList() : new List<TaskNotification>();
            var tomorrowTasks = showTasks ? tasks.Where(t => t.DueDate.Date == today.AddDays(1)).ToList() : new List<TaskNotification>();
            var laterTasks = showTasks ? tasks.Where(t => t.DueDate.Date > today.AddDays(1)).ToList() : new List<TaskNotification>();

            // Group meetings
            var urgentMeetings = showMeetings ? nonLiveMeetings.Where(m => m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 60).ToList() : new List<MeetingNotification>();
            var todayMeetings = showMeetings ? nonLiveMeetings.Where(m => m.MeetingDate.Date == today && m.MinutesToMeeting > 60).ToList() : new List<MeetingNotification>();
            var tomorrowMeetings = showMeetings ? nonLiveMeetings.Where(m => m.MeetingDate.Date == today.AddDays(1)).ToList() : new List<MeetingNotification>();
            var laterMeetings = showMeetings ? nonLiveMeetings.Where(m => m.MeetingDate.Date > today.AddDays(1)).ToList() : new List<MeetingNotification>();

            int totalItems = overdueTasks.Count + urgentTasks.Count + todayTasks.Count + tomorrowTasks.Count + laterTasks.Count +
                           urgentMeetings.Count + todayMeetings.Count + tomorrowMeetings.Count + laterMeetings.Count;

            if (totalItems == 0)
            {
                contentPanel.Children.Add(CreateEmptyState());
                return;
            }

            // OVERDUE TASKS
            if (overdueTasks.Count > 0)
            {
                AddSectionHeader("Zaległe", "Przekroczony termin!", AlertRed, overdueTasks.Count);
                foreach (var task in overdueTasks)
                    contentPanel.Children.Add(CreateTaskCard(task, AlertRed, true));
            }

            // URGENT section
            if (urgentTasks.Count > 0 || urgentMeetings.Count > 0)
            {
                var urgentCount = urgentTasks.Count + urgentMeetings.Count;
                AddSectionHeader("Za chwilę", "W ciągu godziny", WarningOrange, urgentCount);

                foreach (var meeting in urgentMeetings.OrderBy(m => m.MeetingDate))
                    contentPanel.Children.Add(CreateMeetingCard(meeting, WarningOrange, true));

                foreach (var task in urgentTasks)
                    contentPanel.Children.Add(CreateTaskCard(task, WarningOrange, false));
            }

            // TODAY section
            if (todayTasks.Count > 0 || todayMeetings.Count > 0)
            {
                var todayCount = todayTasks.Count + todayMeetings.Count;
                var culture = new System.Globalization.CultureInfo("pl-PL");
                AddSectionHeader("Dzisiaj", DateTime.Today.ToString("dddd, d MMMM", culture), InfoBlue, todayCount);

                foreach (var meeting in todayMeetings.OrderBy(m => m.MeetingDate))
                    contentPanel.Children.Add(CreateMeetingCard(meeting, InfoBlue, false));

                foreach (var task in todayTasks.OrderByDescending(t => t.Priority))
                    contentPanel.Children.Add(CreateTaskCard(task, InfoBlue, false));
            }

            // TOMORROW section
            if (tomorrowTasks.Count > 0 || tomorrowMeetings.Count > 0)
            {
                var tomorrowCount = tomorrowTasks.Count + tomorrowMeetings.Count;
                var culture = new System.Globalization.CultureInfo("pl-PL");
                AddSectionHeader("Jutro", DateTime.Today.AddDays(1).ToString("dddd, d MMMM", culture), Purple, tomorrowCount);

                foreach (var meeting in tomorrowMeetings.OrderBy(m => m.MeetingDate))
                    contentPanel.Children.Add(CreateMeetingCard(meeting, Purple, false));

                foreach (var task in tomorrowTasks.OrderByDescending(t => t.Priority))
                    contentPanel.Children.Add(CreateTaskCard(task, Purple, false));
            }

            // LATER section
            if (laterTasks.Count > 0 || laterMeetings.Count > 0)
            {
                var laterCount = laterTasks.Count + laterMeetings.Count;
                AddSectionHeader("Nadchodzące", "Następne dni", PrimaryGreen, laterCount);

                var allLaterItems = new List<(DateTime date, object item)>();
                allLaterItems.AddRange(laterMeetings.Select(m => (m.MeetingDate.Date, (object)m)));
                allLaterItems.AddRange(laterTasks.Select(t => (t.DueDate.Date, (object)t)));

                var groupedByDate = allLaterItems.GroupBy(x => x.date).OrderBy(g => g.Key).Take(7);

                foreach (var group in groupedByDate)
                {
                    AddDateSubheader(group.Key);

                    foreach (var item in group.OrderBy(x => x.item is MeetingNotification m ? m.MeetingDate : ((TaskNotification)x.item).DueDate))
                    {
                        if (item.item is MeetingNotification meeting)
                            contentPanel.Children.Add(CreateMeetingCard(meeting, PrimaryGreen, false));
                        else if (item.item is TaskNotification task)
                            contentPanel.Children.Add(CreateTaskCard(task, PrimaryGreen, false));
                    }
                }
            }

            contentPanel.Children.Add(new Border { Height = 16 });
        }

        private void AddSectionHeader(string title, string subtitle, Color color, int count)
        {
            var header = new Border { Margin = new Thickness(0, 16, 0, 10) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            leftStack.Children.Add(new Border
            {
                Width = 5,
                Height = 32,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 14, 0)
            });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 2, 0, 0)
            });
            leftStack.Children.Add(titleStack);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 5, 12, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
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
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(TextLight),
                Margin = new Thickness(19, 12, 0, 8)
            };

            contentPanel.Children.Add(subheader);
        }

        private Border CreateTaskCard(TaskNotification task, Color sectionColor, bool isOverdue)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 14, 16, 14),
                BorderThickness = new Thickness(0, 0, 5, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, sectionColor.R, sectionColor.G, sectionColor.B)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainStack = new StackPanel();

            // Header row
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Task icon with priority
            var priorityColor = task.Priority == 3 ? AlertRed : task.Priority == 2 ? WarningOrange : PrimaryGreen;
            var iconContainer = new Grid { Width = 42, Height = 42, Margin = new Thickness(0, 0, 14, 0) };

            var iconBg = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(40, priorityColor.R, priorityColor.G, priorityColor.B))
            };
            iconContainer.Children.Add(iconBg);

            iconContainer.Children.Add(new TextBlock
            {
                Text = "✓",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(priorityColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Priority indicator dot
            var priorityDot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(priorityColor),
                BorderBrush = new SolidColorBrush(CardBg),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -3, -3, 0)
            };
            iconContainer.Children.Add(priorityDot);

            Grid.SetColumn(iconContainer, 0);
            headerRow.Children.Add(iconContainer);

            // Title and details
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            infoStack.Children.Add(new TextBlock
            {
                Text = task.Title,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 450
            });

            // Badges row
            var badgesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            // Type badge
            badgesRow.Children.Add(CreateBadge("ZADANIE", TextLight, Color.FromArgb(40, 255, 255, 255)));

            // Priority badge
            var priorityText = task.Priority == 3 ? "Wysoki" : task.Priority == 2 ? "Średni" : "Niski";
            badgesRow.Children.Add(CreateBadge(priorityText, priorityColor, Color.FromArgb(30, priorityColor.R, priorityColor.G, priorityColor.B)));

            // Due time
            badgesRow.Children.Add(new TextBlock
            {
                Text = $"Termin: {task.DueDate:HH:mm}",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });

            infoStack.Children.Add(badgesRow);

            Grid.SetColumn(infoStack, 1);
            headerRow.Children.Add(infoStack);

            // Time indicator
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var timeText = GetTaskRelativeTime(task.DueDate);
            timeStack.Children.Add(new TextBlock
            {
                Text = timeText,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isOverdue ? AlertRed : sectionColor),
                HorizontalAlignment = HorizontalAlignment.Right
            });

            if (!isOverdue)
            {
                timeStack.Children.Add(new TextBlock
                {
                    Text = task.DueDate.ToString("HH:mm"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            Grid.SetColumn(timeStack, 2);
            headerRow.Children.Add(timeStack);

            mainStack.Children.Add(headerRow);

            // Description preview
            if (!string.IsNullOrWhiteSpace(task.Description))
            {
                var descPreview = task.Description.Length > 80 ? task.Description.Substring(0, 80) + "..." : task.Description;
                mainStack.Children.Add(new TextBlock
                {
                    Text = descPreview,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(56, 8, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontStyle = FontStyles.Italic
                });
            }

            // Assignees avatars
            if (task.Assignees.Count > 0)
            {
                var assigneesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(56, 10, 0, 0) };

                for (int i = 0; i < Math.Min(task.Assignees.Count, 6); i++)
                {
                    var assignee = task.Assignees[i];
                    assigneesRow.Children.Add(CreateLargeAvatar(
                        new MeetingAttendee { Name = assignee.Name, OperatorId = assignee.OperatorId, Status = "Przypisany" },
                        i, 32));
                }

                if (task.Assignees.Count > 6)
                {
                    assigneesRow.Children.Add(new TextBlock
                    {
                        Text = $"+{task.Assignees.Count - 6}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(TextGray),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(6, 0, 0, 0)
                    });
                }

                mainStack.Children.Add(assigneesRow);
            }

            card.Child = mainStack;
            return card;
        }

        private Border CreateMeetingCard(MeetingNotification meeting, Color sectionColor, bool isUrgent)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 14, 16, 14),
                BorderThickness = new Thickness(0, 0, 5, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainStack = new StackPanel();

            // Header row
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Meeting icon
            var iconBg = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(40, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                Margin = new Thickness(0, 0, 14, 0)
            };
            iconBg.Child = new TextBlock
            {
                Text = "📅",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBg, 0);
            headerRow.Children.Add(iconBg);

            // Title and details
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            infoStack.Children.Add(new TextBlock
            {
                Text = meeting.Title,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 450
            });

            // Badges row
            var badgesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            badgesRow.Children.Add(CreateBadge("SPOTKANIE", InfoBlue, Color.FromArgb(40, InfoBlue.R, InfoBlue.G, InfoBlue.B)));

            badgesRow.Children.Add(new TextBlock
            {
                Text = $"{meeting.DurationMin} min",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });

            if (!string.IsNullOrEmpty(meeting.Location))
            {
                badgesRow.Children.Add(new TextBlock
                {
                    Text = $"📍 {meeting.Location}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                });
            }

            infoStack.Children.Add(badgesRow);

            Grid.SetColumn(infoStack, 1);
            headerRow.Children.Add(infoStack);

            // Time indicator
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var timeText = GetMeetingRelativeTime(meeting);
            timeStack.Children.Add(new TextBlock
            {
                Text = timeText,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isUrgent ? WarningOrange : sectionColor),
                HorizontalAlignment = HorizontalAlignment.Right
            });

            timeStack.Children.Add(new TextBlock
            {
                Text = meeting.MeetingDate.ToString("HH:mm"),
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 3, 0, 0)
            });

            Grid.SetColumn(timeStack, 2);
            headerRow.Children.Add(timeStack);

            mainStack.Children.Add(headerRow);

            // LARGE Attendees section
            if (meeting.Attendees.Count > 0)
            {
                var attendeesSection = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(56, 12, 0, 0)
                };

                var attendeesStack = new StackPanel();

                // Label
                attendeesStack.Children.Add(new TextBlock
                {
                    Text = $"UCZESTNICY ({meeting.Attendees.Count})",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Avatar row - LARGE avatars
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal };
                var displayCount = Math.Min(meeting.Attendees.Count, 10);

                for (int i = 0; i < displayCount; i++)
                {
                    var attendee = meeting.Attendees[i];
                    avatarRow.Children.Add(CreateLargeAvatar(attendee, i, 36));
                }

                if (meeting.Attendees.Count > 10)
                {
                    var moreCount = new Border
                    {
                        Width = 36,
                        Height = 36,
                        CornerRadius = new CornerRadius(18),
                        Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        BorderBrush = new SolidColorBrush(CardBg),
                        BorderThickness = new Thickness(3),
                        Margin = new Thickness(-10, 0, 0, 0)
                    };
                    moreCount.Child = new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 10}",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    avatarRow.Children.Add(moreCount);
                }

                attendeesStack.Children.Add(avatarRow);

                // Status summary
                var accepted = meeting.Attendees.Count(a => a.Status == "Zaakceptowane");
                var declined = meeting.Attendees.Count(a => a.Status == "Odrzucone");
                var pending = meeting.Attendees.Count(a => a.Status == "Oczekuje");

                var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

                if (accepted > 0)
                {
                    statusRow.Children.Add(new TextBlock
                    {
                        Text = $"✓ {accepted}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(PrimaryGreen),
                        Margin = new Thickness(0, 0, 12, 0)
                    });
                }
                if (declined > 0)
                {
                    statusRow.Children.Add(new TextBlock
                    {
                        Text = $"✗ {declined}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(AlertRed),
                        Margin = new Thickness(0, 0, 12, 0)
                    });
                }
                if (pending > 0)
                {
                    statusRow.Children.Add(new TextBlock
                    {
                        Text = $"? {pending}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(TextGray)
                    });
                }

                attendeesStack.Children.Add(statusRow);

                attendeesSection.Child = attendeesStack;
                mainStack.Children.Add(attendeesSection);
            }

            // Organizer
            if (!string.IsNullOrEmpty(meeting.OrganizerName))
            {
                mainStack.Children.Add(new TextBlock
                {
                    Text = $"Organizator: {meeting.OrganizerName}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(56, 8, 0, 0),
                    FontStyle = FontStyles.Italic
                });
            }

            card.Child = mainStack;
            return card;
        }

        private Border CreateBadge(string text, Color textColor, Color bgColor)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 0)
            };
            badge.Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(textColor)
            };
            return badge;
        }

        private Border CreateLargeAvatar(MeetingAttendee attendee, int index, int size)
        {
            var colorHex = AvatarColors[Math.Abs(attendee.Name?.GetHashCode() ?? index) % AvatarColors.Length];
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var initials = GetInitials(attendee.Name);

            // Status-based border color
            var borderColor = attendee.Status == "Zaakceptowane" ? PrimaryGreen :
                            attendee.Status == "Odrzucone" ? AlertRed :
                            attendee.Status == "Przypisany" ? InfoBlue : TextGray;

            var avatar = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(3),
                Margin = new Thickness(index > 0 ? -10 : 0, 0, 0, 0)
            };

            avatar.Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = size * 0.32,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Detailed tooltip
            var statusText = attendee.Status == "Zaakceptowane" ? "Potwierdzone" :
                           attendee.Status == "Odrzucone" ? "Odrzucone" :
                           attendee.Status == "Przypisany" ? "Przypisany" : "Oczekuje na odpowiedź";

            var tooltipContent = $"{attendee.Name}";
            if (!string.IsNullOrEmpty(attendee.Email))
                tooltipContent += $"\n{attendee.Email}";
            tooltipContent += $"\nStatus: {statusText}";
            if (attendee.IsRequired)
                tooltipContent += "\n⭐ Obowiązkowy uczestnik";

            avatar.ToolTip = tooltipContent;

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
            var container = new Border { Margin = new Thickness(0, 80, 0, 80) };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconBorder = new Border
            {
                Width = 80,
                Height = 80,
                CornerRadius = new CornerRadius(40),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(30, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B))
            };
            iconBorder.Child = new TextBlock
            {
                Text = "✓",
                FontSize = 36,
                Foreground = new SolidColorBrush(PrimaryGreen),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(iconBorder);

            stack.Children.Add(new TextBlock
            {
                Text = "Wszystko pod kontrolą!",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });

            var subtitle = currentView switch
            {
                ViewMode.Tasks => "Brak zadań do wykonania",
                ViewMode.Meetings => "Brak zaplanowanych spotkań",
                _ => "Brak pilnych zadań i spotkań"
            };

            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 14,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
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
            if (meeting.IsLive) return "TRWA";
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
        public DateTime CreatedDate { get; set; }
        public string CreatorName { get; set; }
        public List<TaskAssignee> Assignees { get; set; } = new List<TaskAssignee>();
    }

    public class TaskAssignee
    {
        public string OperatorId { get; set; }
        public string Name { get; set; }
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
        public string Priority { get; set; }
        public int MinutesToMeeting { get; set; }
        public bool IsLive { get; set; }
        public DateTime EndTime { get; set; }
        public List<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();
    }

    public class MeetingAttendee
    {
        public string OperatorId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public bool IsRequired { get; set; }
        public string Email { get; set; }
    }

    #endregion
}
