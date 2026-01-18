using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        // Colors
        private static readonly Color PrimaryGreen = (Color)ColorConverter.ConvertFromString("#27AE60");
        private static readonly Color AlertRed = (Color)ColorConverter.ConvertFromString("#E74C3C");
        private static readonly Color WarningOrange = (Color)ColorConverter.ConvertFromString("#F39C12");
        private static readonly Color InfoBlue = (Color)ColorConverter.ConvertFromString("#3498DB");
        private static readonly Color Purple = (Color)ColorConverter.ConvertFromString("#9B59B6");
        private static readonly Color DarkBg = (Color)ColorConverter.ConvertFromString("#0F1419");
        private static readonly Color CardBg = (Color)ColorConverter.ConvertFromString("#1A1F26");
        private static readonly Color CardBgHover = (Color)ColorConverter.ConvertFromString("#242B35");
        private static readonly Color TextGray = (Color)ColorConverter.ConvertFromString("#7F8C8D");
        private static readonly Color TextLight = (Color)ColorConverter.ConvertFromString("#BDC3C7");

        // Avatar colors
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
                liveDot.Opacity = isVisible ? 1.0 : 0.4;
            };

            pulseTimer.Start();
        }

        #region Statistics

        private void UpdateStatistics()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            // Live meetings
            var liveMeetings = meetings.Where(m => m.IsLive).ToList();
            if (liveMeetings.Count > 0)
            {
                liveNowStat.Visibility = Visibility.Visible;
                tasksStat.Visibility = Visibility.Collapsed;
                txtLiveCount.Text = liveMeetings.Count.ToString();
            }
            else
            {
                liveNowStat.Visibility = Visibility.Collapsed;
                tasksStat.Visibility = Visibility.Visible;
            }

            // Urgent count
            var urgentTasksCount = tasks.Count(t => t.DueDate.Date < today || (t.DueDate.Date == today && t.DueDate <= now.AddHours(2)));
            var urgentMeetingsCount = meetings.Count(m => !m.IsLive && m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 30);
            var urgentTotal = urgentTasksCount + urgentMeetingsCount;

            txtUrgentCount.Text = urgentTotal.ToString();
            txtTasksCount.Text = tasks.Count.ToString();
            txtMeetingsCount.Text = meetings.Count.ToString();

            // Subtitle with date
            var culture = new System.Globalization.CultureInfo("pl-PL");
            var dayName = DateTime.Today.ToString("dddd, d MMMM", culture);
            txtSubtitle.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);
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

        #region Build Content

        private void BuildContent()
        {
            contentPanel.Children.Clear();

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Get live meetings
            var liveMeetings = meetings.Where(m => m.IsLive).ToList();

            // Get upcoming meetings (not live)
            var upcomingMeetings = meetings.Where(m => !m.IsLive && m.MeetingDate >= now).OrderBy(m => m.MeetingDate).ToList();

            // Get overdue and upcoming tasks
            var overdueTasks = tasks.Where(t => t.DueDate < now).ToList();
            var upcomingTasks = tasks.Where(t => t.DueDate >= now).OrderBy(t => t.DueDate).ToList();

            if (liveMeetings.Count == 0 && upcomingMeetings.Count == 0 && tasks.Count == 0)
            {
                contentPanel.Children.Add(CreateEmptyState());
                return;
            }

            // LIVE MEETINGS - prominent display
            if (liveMeetings.Count > 0)
            {
                AddSectionHeader("Trwa teraz", AlertRed);
                foreach (var meeting in liveMeetings)
                    contentPanel.Children.Add(CreateLiveMeetingCard(meeting));
            }

            // Combine upcoming items by time
            var allItems = new List<(DateTime time, string type, object item)>();

            foreach (var task in overdueTasks)
                allItems.Add((task.DueDate, "overdue_task", task));

            foreach (var meeting in upcomingMeetings.Take(5))
                allItems.Add((meeting.MeetingDate, "meeting", meeting));

            foreach (var task in upcomingTasks.Take(5))
                allItems.Add((task.DueDate, "task", task));

            // Sort by time
            allItems = allItems.OrderBy(x => x.time).ToList();

            // Overdue section
            var overdueItems = allItems.Where(x => x.type == "overdue_task").ToList();
            if (overdueItems.Count > 0)
            {
                AddSectionHeader("Zaległe", AlertRed);
                foreach (var item in overdueItems)
                    contentPanel.Children.Add(CreateTaskCard((TaskNotification)item.item, true));
            }

            // Upcoming section
            var upcoming = allItems.Where(x => x.type != "overdue_task").Take(8).ToList();
            if (upcoming.Count > 0)
            {
                AddSectionHeader("Nadchodzące", InfoBlue);
                foreach (var item in upcoming)
                {
                    if (item.type == "meeting")
                        contentPanel.Children.Add(CreateMeetingCard((MeetingNotification)item.item));
                    else
                        contentPanel.Children.Add(CreateTaskCard((TaskNotification)item.item, false));
                }
            }

            contentPanel.Children.Add(new Border { Height = 10 });
        }

        private void AddSectionHeader(string title, Color color)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 14, 0, 10)
            };

            header.Children.Add(new Border
            {
                Width = 4,
                Height = 18,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            header.Children.Add(new TextBlock
            {
                Text = title.ToUpper(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });

            contentPanel.Children.Add(header);
        }

        private Border CreateLiveMeetingCard(MeetingNotification meeting)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, AlertRed.R, AlertRed.G, AlertRed.B)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = new SolidColorBrush(AlertRed),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            card.Effect = new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.3,
                Color = AlertRed
            };

            var mainStack = new StackPanel();

            // Title row with LIVE badge
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
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
                Text = $"Od {meeting.MeetingDate:HH:mm}",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextLight)
            });

            if (!string.IsNullOrEmpty(meeting.Location))
            {
                detailsRow.Children.Add(new TextBlock
                {
                    Text = $"  •  {meeting.Location}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray)
                });
            }
            titleStack.Children.Add(detailsRow);
            Grid.SetColumn(titleStack, 0);
            titleRow.Children.Add(titleStack);

            // LIVE badge
            var liveBadge = new Border
            {
                Background = new SolidColorBrush(AlertRed),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Top
            };
            liveBadge.Child = new TextBlock
            {
                Text = "LIVE",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            Grid.SetColumn(liveBadge, 1);
            titleRow.Children.Add(liveBadge);

            mainStack.Children.Add(titleRow);

            // LARGE Avatars - 48px
            if (meeting.Attendees.Count > 0)
            {
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };

                for (int i = 0; i < Math.Min(meeting.Attendees.Count, 6); i++)
                {
                    avatarRow.Children.Add(CreateAvatar(meeting.Attendees[i], i, 48));
                }

                if (meeting.Attendees.Count > 6)
                {
                    var moreCount = new Border
                    {
                        Width = 48,
                        Height = 48,
                        CornerRadius = new CornerRadius(24),
                        Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                        BorderBrush = new SolidColorBrush(CardBg),
                        BorderThickness = new Thickness(3),
                        Margin = new Thickness(-12, 0, 0, 0)
                    };
                    moreCount.Child = new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 6}",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    avatarRow.Children.Add(moreCount);
                }

                mainStack.Children.Add(avatarRow);
            }

            card.Child = mainStack;
            return card;
        }

        private Border CreateMeetingCard(MeetingNotification meeting)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Meeting icon
            var iconBg = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            iconBg.Background = new LinearGradientBrush(Purple, Color.FromArgb(255, 142, 68, 173), 45);
            iconBg.Child = new TextBlock
            {
                Text = "📅",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBg, 0);
            mainGrid.Children.Add(iconBg);

            // Content
            var contentStack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            contentStack.Children.Add(new TextBlock
            {
                Text = meeting.Title,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Time and location
            var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            infoRow.Children.Add(new TextBlock
            {
                Text = meeting.MeetingDate.ToString("HH:mm"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Purple),
                FontWeight = FontWeights.SemiBold
            });

            infoRow.Children.Add(new TextBlock
            {
                Text = $"  •  {meeting.DurationMin} min",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray)
            });

            if (!string.IsNullOrEmpty(meeting.Location))
            {
                infoRow.Children.Add(new TextBlock
                {
                    Text = $"  •  {meeting.Location}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 120
                });
            }
            contentStack.Children.Add(infoRow);

            // LARGE Avatars - 44px
            if (meeting.Attendees.Count > 0)
            {
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

                for (int i = 0; i < Math.Min(meeting.Attendees.Count, 5); i++)
                {
                    avatarRow.Children.Add(CreateAvatar(meeting.Attendees[i], i, 44));
                }

                if (meeting.Attendees.Count > 5)
                {
                    var moreText = new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 5}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(TextGray),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    };
                    avatarRow.Children.Add(moreText);
                }

                contentStack.Children.Add(avatarRow);
            }

            Grid.SetColumn(contentStack, 1);
            mainGrid.Children.Add(contentStack);

            // Time badge
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right };
            var timeText = GetRelativeTime(meeting.MeetingDate);

            var timeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, Purple.R, Purple.G, Purple.B)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4)
            };
            timeBadge.Child = new TextBlock
            {
                Text = timeText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Purple)
            };
            timeStack.Children.Add(timeBadge);

            Grid.SetColumn(timeStack, 2);
            mainGrid.Children.Add(timeStack);

            card.Child = mainGrid;
            return card;
        }

        private Border CreateTaskCard(TaskNotification task, bool isOverdue)
        {
            var priorityColor = task.Priority == 3 ? AlertRed : task.Priority == 2 ? WarningOrange : PrimaryGreen;

            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Task icon with priority indicator
            var iconContainer = new Grid { Width = 40, Height = 40, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top };

            var iconBg = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(10)
            };
            iconBg.Background = new LinearGradientBrush(InfoBlue, Color.FromArgb(255, 41, 128, 185), 45);
            iconBg.Child = new TextBlock
            {
                Text = "✓",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconContainer.Children.Add(iconBg);

            // Priority dot
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
                Margin = new Thickness(0, -2, -2, 0)
            };
            iconContainer.Children.Add(priorityDot);

            Grid.SetColumn(iconContainer, 0);
            mainGrid.Children.Add(iconContainer);

            // Content
            var contentStack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            contentStack.Children.Add(new TextBlock
            {
                Text = task.Title,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Due time
            var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            infoRow.Children.Add(new TextBlock
            {
                Text = task.DueDate.ToString("HH:mm"),
                FontSize = 11,
                Foreground = new SolidColorBrush(isOverdue ? AlertRed : InfoBlue),
                FontWeight = FontWeights.SemiBold
            });

            var priorityText = task.Priority == 3 ? "Wysoki" : task.Priority == 2 ? "Średni" : "Niski";
            infoRow.Children.Add(new TextBlock
            {
                Text = $"  •  {priorityText}",
                FontSize = 11,
                Foreground = new SolidColorBrush(priorityColor)
            });

            contentStack.Children.Add(infoRow);

            // LARGE Avatars - 44px
            if (task.Assignees.Count > 0)
            {
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

                for (int i = 0; i < Math.Min(task.Assignees.Count, 4); i++)
                {
                    var assignee = task.Assignees[i];
                    avatarRow.Children.Add(CreateAvatar(
                        new MeetingAttendee { Name = assignee.Name, OperatorId = assignee.OperatorId, Status = "Przypisany" },
                        i, 44));
                }

                if (task.Assignees.Count > 4)
                {
                    var moreText = new TextBlock
                    {
                        Text = $"+{task.Assignees.Count - 4}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(TextGray),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    };
                    avatarRow.Children.Add(moreText);
                }

                contentStack.Children.Add(avatarRow);
            }

            Grid.SetColumn(contentStack, 1);
            mainGrid.Children.Add(contentStack);

            // Time badge
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right };
            var timeText = GetRelativeTime(task.DueDate);

            var badgeColor = isOverdue ? AlertRed : InfoBlue;
            var timeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, badgeColor.R, badgeColor.G, badgeColor.B)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4)
            };
            timeBadge.Child = new TextBlock
            {
                Text = isOverdue ? "Zaległe" : timeText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(badgeColor)
            };
            timeStack.Children.Add(timeBadge);

            Grid.SetColumn(timeStack, 2);
            mainGrid.Children.Add(timeStack);

            card.Child = mainGrid;
            return card;
        }

        private Border CreateAvatar(MeetingAttendee attendee, int index, int size)
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
                Margin = new Thickness(index > 0 ? -12 : 0, 0, 0, 0)
            };

            avatar.Effect = new DropShadowEffect
            {
                BlurRadius = 6,
                ShadowDepth = 1,
                Opacity = 0.3,
                Color = Colors.Black
            };

            avatar.Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = size * 0.35,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Tooltip
            var statusText = attendee.Status == "Zaakceptowane" ? "Potwierdzone" :
                           attendee.Status == "Odrzucone" ? "Odrzucone" :
                           attendee.Status == "Przypisany" ? "Przypisany" : "Oczekuje";

            avatar.ToolTip = $"{attendee.Name}\nStatus: {statusText}";

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
            var container = new Border { Margin = new Thickness(0, 60, 0, 60) };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconBorder = new Border
            {
                Width = 70,
                Height = 70,
                CornerRadius = new CornerRadius(35),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconBorder.Background = new LinearGradientBrush(PrimaryGreen, Color.FromArgb(255, 30, 132, 73), 45);
            iconBorder.Effect = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 0, Opacity = 0.4, Color = PrimaryGreen };
            iconBorder.Child = new TextBlock
            {
                Text = "✓",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(iconBorder);

            stack.Children.Add(new TextBlock
            {
                Text = "Wszystko ogarnięte!",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Brak zadań i spotkań",
                FontSize = 12,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });

            container.Child = stack;
            return container;
        }

        private string GetRelativeTime(DateTime dateTime)
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
