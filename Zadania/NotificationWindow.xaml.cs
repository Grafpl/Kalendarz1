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
        private List<MeetingNotification> meetings = new List<MeetingNotification>();
        private bool showingTasks = true;

        // Company colors
        private static readonly Color PrimaryGreen = (Color)ColorConverter.ConvertFromString("#27AE60");
        private static readonly Color DarkGreen = (Color)ColorConverter.ConvertFromString("#219A52");
        private static readonly Color AlertRed = (Color)ColorConverter.ConvertFromString("#C0392B");
        private static readonly Color WarningOrange = (Color)ColorConverter.ConvertFromString("#E67E22");
        private static readonly Color InfoBlue = (Color)ColorConverter.ConvertFromString("#3498DB");
        private static readonly Color DarkBg = (Color)ColorConverter.ConvertFromString("#1C2833");
        private static readonly Color CardBg = (Color)ColorConverter.ConvertFromString("#212F3D");
        private static readonly Color TextGray = (Color)ColorConverter.ConvertFromString("#95A5A6");

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
            LoadNotifications();
            LoadMeetings();
            BuildTasksContent();
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        }

        #region Tab Switching

        private void BtnTabZadania_Click(object sender, RoutedEventArgs e)
        {
            showingTasks = true;
            UpdateTabStyles();
            BuildTasksContent();
        }

        private void BtnTabSpotkania_Click(object sender, RoutedEventArgs e)
        {
            showingTasks = false;
            UpdateTabStyles();
            BuildMeetingsContent();
        }

        private void UpdateTabStyles()
        {
            if (showingTasks)
            {
                btnTabZadania.BorderBrush = new SolidColorBrush(PrimaryGreen);
                btnTabZadania.Foreground = Brushes.White;
                btnTabSpotkania.BorderBrush = Brushes.Transparent;
                btnTabSpotkania.Foreground = new SolidColorBrush(TextGray);
            }
            else
            {
                btnTabSpotkania.BorderBrush = new SolidColorBrush(PrimaryGreen);
                btnTabSpotkania.Foreground = Brushes.White;
                btnTabZadania.BorderBrush = Brushes.Transparent;
                btnTabZadania.Foreground = new SolidColorBrush(TextGray);
            }
        }

        #endregion

        #region Load Data

        private void LoadNotifications()
        {
            notifications.Clear();

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
        }

        private void LoadMeetings()
        {
            meetings.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Load meetings where user is invited or is organizer
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
                            s.Priorytet,
                            s.LinkSpotkania,
                            DATEDIFF(MINUTE, GETDATE(), s.DataSpotkania) AS MinutyDoSpotkania
                        FROM Spotkania s
                        LEFT JOIN Operatorzy o ON s.OrganizatorID = o.ID
                        LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                        WHERE (s.OrganizatorID = @id OR su.OperatorID = @id)
                          AND s.Status IN ('Zaplanowane', 'WTrakcie')
                          AND s.DataSpotkania >= DATEADD(DAY, -1, GETDATE())
                          AND s.DataSpotkania <= DATEADD(DAY, 7, GETDATE())
                        ORDER BY s.DataSpotkania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            meetings.Add(new MeetingNotification
                            {
                                Id = reader.GetInt64(0),
                                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                MeetingDate = reader.GetDateTime(2),
                                DurationMin = reader.IsDBNull(3) ? 60 : reader.GetInt32(3),
                                Location = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Status = reader.IsDBNull(5) ? "Zaplanowane" : reader.GetString(5),
                                OrganizerId = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                OrganizerName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                Priority = reader.IsDBNull(8) ? "Normalny" : reader.GetString(8),
                                MeetingLink = reader.IsDBNull(9) ? null : reader.GetString(9),
                                MinutesToMeeting = reader.IsDBNull(10) ? 0 : reader.GetInt32(10)
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

        #region Build Tasks Content

        private void BuildTasksContent()
        {
            contentPanel.Children.Clear();

            var today = DateTime.Today;
            var now = DateTime.Now;

            var overdue = notifications.Where(t => t.DueDate.Date < today).ToList();
            var todayUrgent = notifications.Where(t => t.DueDate.Date == today && t.DueDate <= now.AddHours(1)).ToList();
            var todayOther = notifications.Where(t => t.DueDate.Date == today && t.DueDate > now.AddHours(1)).ToList();
            var tomorrow = notifications.Where(t => t.DueDate.Date == today.AddDays(1)).ToList();

            int totalCount = overdue.Count + todayUrgent.Count + todayOther.Count + tomorrow.Count;

            if (totalCount == 0)
            {
                txtSubtitle.Text = "Brak pilnych zadań";
                contentPanel.Children.Add(CreateEmptyState("Wszystko pod kontrolą!", "Brak zaległych zadań"));
                return;
            }

            txtSubtitle.Text = $"Masz {totalCount} zadań do przejrzenia";

            if (overdue.Count > 0)
                AddTaskSection("Zaległe", AlertRed, overdue, true);

            if (todayUrgent.Count > 0)
                AddTaskSection("Za chwilę", WarningOrange, todayUrgent, true);

            if (todayOther.Count > 0)
                AddTaskSection("Dziś", PrimaryGreen, todayOther, false);

            if (tomorrow.Count > 0)
                AddTaskSection("Jutro", InfoBlue, tomorrow, false);
        }

        private void AddTaskSection(string title, Color color, List<TaskNotification> tasks, bool isUrgent)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 8)
            };

            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            header.Children.Add(dot);

            header.Children.Add(new TextBlock
            {
                Text = $"{title} ({tasks.Count})",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });

            contentPanel.Children.Add(header);

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
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(16, 5, 0, 0)
                });
            }
        }

        private Border CreateTaskItem(TaskNotification task, Color sectionColor, bool isUrgent)
        {
            var item = new Border
            {
                Background = isUrgent
                    ? new SolidColorBrush(Color.FromArgb(25, sectionColor.R, sectionColor.G, sectionColor.B))
                    : new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 10, 12, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, sectionColor.R, sectionColor.G, sectionColor.B)),
                BorderThickness = isUrgent ? new Thickness(1) : new Thickness(0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var priorityColor = task.Priority == 3 ? AlertRed :
                               task.Priority == 2 ? WarningOrange : PrimaryGreen;

            titleStack.Children.Add(new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(priorityColor),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            titleStack.Children.Add(new TextBlock
            {
                Text = task.Title,
                Foreground = Brushes.White,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 250
            });

            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            var timeText = GetRelativeTime(task.DueDate);
            var timeBlock = new TextBlock
            {
                Text = timeText,
                Foreground = isUrgent
                    ? new SolidColorBrush(sectionColor)
                    : new SolidColorBrush(TextGray),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = isUrgent ? FontWeights.SemiBold : FontWeights.Normal
            };
            Grid.SetColumn(timeBlock, 1);
            grid.Children.Add(timeBlock);

            item.Child = grid;
            return item;
        }

        #endregion

        #region Build Meetings Content

        private void BuildMeetingsContent()
        {
            contentPanel.Children.Clear();

            var today = DateTime.Today;
            var now = DateTime.Now;

            var urgent = meetings.Where(m => m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 30).ToList();
            var soon = meetings.Where(m => m.MinutesToMeeting > 30 && m.MinutesToMeeting <= 60).ToList();
            var todayMeetings = meetings.Where(m => m.MeetingDate.Date == today && m.MinutesToMeeting > 60).ToList();
            var tomorrowMeetings = meetings.Where(m => m.MeetingDate.Date == today.AddDays(1)).ToList();
            var laterMeetings = meetings.Where(m => m.MeetingDate.Date > today.AddDays(1)).ToList();

            int totalCount = urgent.Count + soon.Count + todayMeetings.Count + tomorrowMeetings.Count + laterMeetings.Count;

            if (totalCount == 0)
            {
                txtSubtitle.Text = "Brak nadchodzących spotkań";
                contentPanel.Children.Add(CreateEmptyState("Kalendarz pusty", "Brak zaplanowanych spotkań"));
                return;
            }

            txtSubtitle.Text = $"Masz {totalCount} nadchodzących spotkań";

            if (urgent.Count > 0)
                AddMeetingSection("Za chwilę!", AlertRed, urgent, true);

            if (soon.Count > 0)
                AddMeetingSection("W ciągu godziny", WarningOrange, soon, true);

            if (todayMeetings.Count > 0)
                AddMeetingSection("Dziś", PrimaryGreen, todayMeetings, false);

            if (tomorrowMeetings.Count > 0)
                AddMeetingSection("Jutro", InfoBlue, tomorrowMeetings, false);

            if (laterMeetings.Count > 0)
                AddMeetingSection("Nadchodzące", TextGray, laterMeetings, false);
        }

        private void AddMeetingSection(string title, Color color, List<MeetingNotification> meetingsList, bool isUrgent)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 8)
            };

            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            header.Children.Add(dot);

            header.Children.Add(new TextBlock
            {
                Text = $"{title} ({meetingsList.Count})",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });

            contentPanel.Children.Add(header);

            foreach (var meeting in meetingsList.Take(4))
            {
                contentPanel.Children.Add(CreateMeetingItem(meeting, color, isUrgent));
            }

            if (meetingsList.Count > 4)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = $"... i {meetingsList.Count - 4} więcej",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    Margin = new Thickness(16, 5, 0, 0)
                });
            }
        }

        private Border CreateMeetingItem(MeetingNotification meeting, Color sectionColor, bool isUrgent)
        {
            var item = new Border
            {
                Background = isUrgent
                    ? new SolidColorBrush(Color.FromArgb(25, sectionColor.R, sectionColor.G, sectionColor.B))
                    : new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 10, 12, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, sectionColor.R, sectionColor.G, sectionColor.B)),
                BorderThickness = isUrgent ? new Thickness(1) : new Thickness(0)
            };

            var mainStack = new StackPanel();

            // Row 1: Title + Time
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = meeting.Title,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleText, 0);
            titleRow.Children.Add(titleText);

            var timeText = new TextBlock
            {
                Text = GetMeetingTimeText(meeting),
                Foreground = isUrgent ? new SolidColorBrush(sectionColor) : new SolidColorBrush(TextGray),
                FontSize = 11,
                FontWeight = isUrgent ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeText, 1);
            titleRow.Children.Add(timeText);

            mainStack.Children.Add(titleRow);

            // Row 2: Date/Time + Location
            var detailsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0)
            };

            detailsStack.Children.Add(new TextBlock
            {
                Text = meeting.MeetingDate.ToString("dd.MM HH:mm"),
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray)
            });

            if (!string.IsNullOrEmpty(meeting.Location))
            {
                detailsStack.Children.Add(new TextBlock
                {
                    Text = " • ",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray)
                });
                detailsStack.Children.Add(new TextBlock
                {
                    Text = meeting.Location,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    MaxWidth = 150,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            if (!string.IsNullOrEmpty(meeting.MeetingLink))
            {
                detailsStack.Children.Add(new TextBlock
                {
                    Text = " • Online",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(InfoBlue)
                });
            }

            mainStack.Children.Add(detailsStack);

            // Row 3: Attendees with avatars
            if (meeting.Attendees.Count > 0)
            {
                mainStack.Children.Add(CreateAttendeesPanel(meeting.Attendees));
            }

            item.Child = mainStack;
            return item;
        }

        private UIElement CreateAttendeesPanel(List<MeetingAttendee> attendees)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // Avatar stack (overlapping)
            var avatarStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var colors = new[] { "#27AE60", "#3498DB", "#9B59B6", "#E74C3C", "#F39C12" };
            var displayedCount = Math.Min(attendees.Count, 5);

            for (int i = 0; i < displayedCount; i++)
            {
                var attendee = attendees[i];
                var avatar = CreateAvatar(attendee, colors[i % colors.Length], i);
                avatarStack.Children.Add(avatar);
            }

            panel.Children.Add(avatarStack);

            // Count text if more
            if (attendees.Count > 5)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"+{attendees.Count - 5}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextGray),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                });
            }

            // Status summary
            var accepted = attendees.Count(a => a.Status == "Zaakceptowane");
            var pending = attendees.Count(a => a.Status == "Oczekuje");

            panel.Children.Add(new TextBlock
            {
                Text = $" ({accepted}/{attendees.Count} potw.)",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });

            return panel;
        }

        private Border CreateAvatar(MeetingAttendee attendee, string colorHex, int index)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var initials = GetInitials(attendee.Name);

            var statusColor = attendee.Status switch
            {
                "Zaakceptowane" => PrimaryGreen,
                "Odrzucone" => AlertRed,
                _ => WarningOrange
            };

            var container = new Grid
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(index > 0 ? -8 : 0, 0, 0, 0)
            };

            // Main avatar circle
            var avatar = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(DarkBg),
                BorderThickness = new Thickness(2)
            };

            avatar.Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            container.Children.Add(avatar);

            // Status indicator
            var statusDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(statusColor),
                BorderBrush = new SolidColorBrush(DarkBg),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            container.Children.Add(statusDot);

            avatar.ToolTip = $"{attendee.Name}\n{GetStatusText(attendee.Status)}";

            return new Border { Child = container };
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "Zaakceptowane" => "Potwierdził/a udział",
                "Odrzucone" => "Odrzucił/a zaproszenie",
                "Moze" => "Może uczestniczyć",
                _ => "Oczekuje na odpowiedź"
            };
        }

        private string GetMeetingTimeText(MeetingNotification meeting)
        {
            if (meeting.MinutesToMeeting <= 0)
                return "Trwa teraz";
            if (meeting.MinutesToMeeting < 60)
                return $"za {meeting.MinutesToMeeting} min";
            if (meeting.MinutesToMeeting < 1440)
                return $"za {meeting.MinutesToMeeting / 60}h";
            return $"za {meeting.MinutesToMeeting / 1440} dni";
        }

        #endregion

        #region Helpers

        private UIElement CreateEmptyState(string title, string subtitle)
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 50)
            };

            stack.Children.Add(new Border
            {
                Width = 60,
                Height = 60,
                CornerRadius = new CornerRadius(30),
                Background = new SolidColorBrush(Color.FromArgb(30, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 28,
                    Foreground = new SolidColorBrush(PrimaryGreen),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });

            return stack;
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

        private void BtnOpenPanel_Click(object sender, RoutedEventArgs e)
        {
            if (showingTasks)
                OpenPanelRequested?.Invoke(this, EventArgs.Empty);
            else
                OpenMeetingsRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        #endregion

        public bool HasNotifications => notifications.Count > 0 || meetings.Count > 0;
        public int TaskCount => notifications.Count;
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
        public string Priority { get; set; }
        public string MeetingLink { get; set; }
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
