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
        private List<TaskNotification> tasks = new List<TaskNotification>();
        private List<MeetingNotification> meetings = new List<MeetingNotification>();

        // Company colors
        private static readonly Color PrimaryGreen = (Color)ColorConverter.ConvertFromString("#27AE60");
        private static readonly Color AlertRed = (Color)ColorConverter.ConvertFromString("#C0392B");
        private static readonly Color WarningOrange = (Color)ColorConverter.ConvertFromString("#E67E22");
        private static readonly Color InfoBlue = (Color)ColorConverter.ConvertFromString("#3498DB");
        private static readonly Color Purple = (Color)ColorConverter.ConvertFromString("#9B59B6");
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
            LoadTasks();
            LoadMeetings();
            BuildCombinedContent();
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        }

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
                            Z.Wykonane
                        FROM Zadania Z
                        LEFT JOIN ZadaniaPrzypisani zp ON Z.ID = zp.ZadanieID
                        WHERE (Z.OperatorID = @id OR zp.OperatorID = @id)
                          AND Z.Wykonane = 0
                          AND Z.TerminWykonania <= DATEADD(DAY, 2, GETDATE())
                        ORDER BY Z.TerminWykonania ASC", conn);

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
                                IsCompleted = reader.GetBoolean(4)
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

                    // Simple query - load all upcoming meetings for this user
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
                            s.LinkSpotkania
                        FROM Spotkania s
                        LEFT JOIN Operatorzy o ON s.OrganizatorID = o.ID
                        LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                        WHERE (s.OrganizatorID = @id OR su.OperatorID = @id)
                          AND s.Status = 'Zaplanowane'
                          AND s.DataSpotkania >= GETDATE()
                          AND s.DataSpotkania <= DATEADD(DAY, 7, GETDATE())
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

        #region Build Combined Content

        private void BuildCombinedContent()
        {
            contentPanel.Children.Clear();

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Group tasks
            var overdueTasks = tasks.Where(t => t.DueDate.Date < today).ToList();
            var urgentTasks = tasks.Where(t => t.DueDate.Date == today && t.DueDate <= now.AddHours(2)).ToList();
            var todayTasks = tasks.Where(t => t.DueDate.Date == today && t.DueDate > now.AddHours(2)).ToList();
            var tomorrowTasks = tasks.Where(t => t.DueDate.Date == today.AddDays(1)).ToList();

            // Group meetings
            var urgentMeetings = meetings.Where(m => m.MinutesToMeeting > 0 && m.MinutesToMeeting <= 60).ToList();
            var todayMeetings = meetings.Where(m => m.MeetingDate.Date == today && m.MinutesToMeeting > 60).ToList();
            var tomorrowMeetings = meetings.Where(m => m.MeetingDate.Date == today.AddDays(1)).ToList();
            var laterMeetings = meetings.Where(m => m.MeetingDate.Date > today.AddDays(1)).ToList();

            int totalTasks = overdueTasks.Count + urgentTasks.Count + todayTasks.Count + tomorrowTasks.Count;
            int totalMeetings = urgentMeetings.Count + todayMeetings.Count + tomorrowMeetings.Count + laterMeetings.Count;

            if (totalTasks == 0 && totalMeetings == 0)
            {
                txtSubtitle.Text = "Brak przypomnień";
                contentPanel.Children.Add(CreateEmptyState());
                return;
            }

            txtSubtitle.Text = $"{totalTasks} zadań, {totalMeetings} spotkań";

            // URGENT section - tasks and meetings together
            if (overdueTasks.Count > 0 || urgentTasks.Count > 0 || urgentMeetings.Count > 0)
            {
                AddSectionHeader("Pilne", AlertRed);

                foreach (var task in overdueTasks.Concat(urgentTasks))
                    contentPanel.Children.Add(CreateTaskItem(task, AlertRed));

                foreach (var meeting in urgentMeetings)
                    contentPanel.Children.Add(CreateMeetingItem(meeting, AlertRed));
            }

            // TODAY section
            if (todayTasks.Count > 0 || todayMeetings.Count > 0)
            {
                AddSectionHeader("Dziś", WarningOrange);

                foreach (var task in todayTasks)
                    contentPanel.Children.Add(CreateTaskItem(task, WarningOrange));

                foreach (var meeting in todayMeetings)
                    contentPanel.Children.Add(CreateMeetingItem(meeting, WarningOrange));
            }

            // TOMORROW section
            if (tomorrowTasks.Count > 0 || tomorrowMeetings.Count > 0)
            {
                AddSectionHeader("Jutro", InfoBlue);

                foreach (var task in tomorrowTasks)
                    contentPanel.Children.Add(CreateTaskItem(task, InfoBlue));

                foreach (var meeting in tomorrowMeetings)
                    contentPanel.Children.Add(CreateMeetingItem(meeting, InfoBlue));
            }

            // LATER meetings
            if (laterMeetings.Count > 0)
            {
                AddSectionHeader("Nadchodzące", Purple);

                foreach (var meeting in laterMeetings.Take(3))
                    contentPanel.Children.Add(CreateMeetingItem(meeting, Purple));

                if (laterMeetings.Count > 3)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"... i {laterMeetings.Count - 3} więcej spotkań",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(TextGray),
                        Margin = new Thickness(12, 4, 0, 8)
                    });
                }
            }
        }

        private void AddSectionHeader(string title, Color color)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 6)
            };

            header.Children.Add(new Border
            {
                Width = 4,
                Height = 16,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            header.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });

            contentPanel.Children.Add(header);
        }

        private Border CreateTaskItem(TaskNotification task, Color sectionColor)
        {
            var item = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 10, 12, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, sectionColor.R, sectionColor.G, sectionColor.B)),
                BorderThickness = new Thickness(0, 0, 3, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Task icon
            var iconBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(40, sectionColor.R, sectionColor.G, sectionColor.B)),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = new TextBlock
            {
                Text = "✓",
                FontSize = 12,
                Foreground = new SolidColorBrush(sectionColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // Task info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            infoStack.Children.Add(new TextBlock
            {
                Text = task.Title,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var priorityText = task.Priority == 3 ? "Wysoki" : task.Priority == 2 ? "Średni" : "Niski";
            infoStack.Children.Add(new TextBlock
            {
                Text = $"Zadanie • {priorityText}",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Time
            var timeText = GetTaskRelativeTime(task.DueDate);
            var timeBlock = new TextBlock
            {
                Text = timeText,
                Foreground = new SolidColorBrush(sectionColor),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeBlock, 2);
            grid.Children.Add(timeBlock);

            item.Child = grid;
            return item;
        }

        private Border CreateMeetingItem(MeetingNotification meeting, Color sectionColor)
        {
            var item = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 10, 12, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                BorderThickness = new Thickness(0, 0, 3, 0)
            };

            var mainStack = new StackPanel();

            // Row 1: Icon + Title + Time
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Meeting icon
            var iconBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(40, InfoBlue.R, InfoBlue.G, InfoBlue.B)),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = new TextBlock
            {
                Text = "📅",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBorder, 0);
            row1.Children.Add(iconBorder);

            // Meeting info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            infoStack.Children.Add(new TextBlock
            {
                Text = meeting.Title,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var detailText = $"Spotkanie • {meeting.MeetingDate:HH:mm}";
            if (!string.IsNullOrEmpty(meeting.Location))
                detailText += $" • {meeting.Location}";

            infoStack.Children.Add(new TextBlock
            {
                Text = detailText,
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Grid.SetColumn(infoStack, 1);
            row1.Children.Add(infoStack);

            // Time
            var timeText = GetMeetingRelativeTime(meeting);
            var timeBlock = new TextBlock
            {
                Text = timeText,
                Foreground = new SolidColorBrush(sectionColor),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeBlock, 2);
            row1.Children.Add(timeBlock);

            mainStack.Children.Add(row1);

            // Row 2: Attendees (if any)
            if (meeting.Attendees.Count > 0)
            {
                var attendeesPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(34, 6, 0, 0)
                };

                var colors = new[] { "#27AE60", "#3498DB", "#9B59B6", "#E74C3C", "#F39C12" };
                var displayCount = Math.Min(meeting.Attendees.Count, 4);

                for (int i = 0; i < displayCount; i++)
                {
                    var attendee = meeting.Attendees[i];
                    var avatar = CreateSmallAvatar(attendee.Name, colors[i % colors.Length], i);
                    attendeesPanel.Children.Add(avatar);
                }

                if (meeting.Attendees.Count > 4)
                {
                    attendeesPanel.Children.Add(new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 4}",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(TextGray),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }

                var accepted = meeting.Attendees.Count(a => a.Status == "Zaakceptowane");
                attendeesPanel.Children.Add(new TextBlock
                {
                    Text = $" ({accepted}/{meeting.Attendees.Count})",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(TextGray),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                });

                mainStack.Children.Add(attendeesPanel);
            }

            item.Child = mainStack;
            return item;
        }

        private Border CreateSmallAvatar(string name, string colorHex, int index)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var initials = GetInitials(name);

            var avatar = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(DarkBg),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(index > 0 ? -6 : 0, 0, 0, 0)
            };

            avatar.Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            avatar.ToolTip = name;
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
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 60)
            };

            stack.Children.Add(new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(25),
                Background = new SolidColorBrush(Color.FromArgb(30, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 24,
                    Foreground = new SolidColorBrush(PrimaryGreen),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Wszystko pod kontrolą!",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Brak pilnych zadań i spotkań",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            return stack;
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

            return dateTime.ToString("HH:mm");
        }

        private string GetMeetingRelativeTime(MeetingNotification meeting)
        {
            if (meeting.MinutesToMeeting <= 0) return "teraz";
            if (meeting.MinutesToMeeting < 60) return $"za {meeting.MinutesToMeeting}m";
            if (meeting.MinutesToMeeting < 1440) return $"za {meeting.MinutesToMeeting / 60}h";
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

        private void BtnOpenPanel_Click(object sender, RoutedEventArgs e)
        {
            OpenPanelRequested?.Invoke(this, EventArgs.Empty);
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
