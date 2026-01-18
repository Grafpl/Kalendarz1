using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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
        private static readonly Color CardBg = (Color)ColorConverter.ConvertFromString("#242B35");
        private static readonly Color CardBgHover = (Color)ColorConverter.ConvertFromString("#2D3640");
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
            BuildMeetingsColumn();
            BuildTasksColumn();
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

        #region Statistics

        private void UpdateStatistics()
        {
            // Update subtitle with date
            var culture = new System.Globalization.CultureInfo("pl-PL");
            var dayName = DateTime.Today.ToString("dddd, d MMMM", culture);
            txtSubtitle.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);

            // Update counts
            txtTasksCount.Text = tasks.Count == 0 ? "Brak zadań" :
                                 tasks.Count == 1 ? "1 zadanie" :
                                 $"{tasks.Count} zadań";

            txtMeetingsCount.Text = meetings.Count == 0 ? "Brak spotkań" :
                                    meetings.Count == 1 ? "1 spotkanie" :
                                    $"{meetings.Count} spotkań";
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

        #region Build Columns

        private void BuildMeetingsColumn()
        {
            meetingsPanel.Children.Clear();

            if (meetings.Count == 0)
            {
                meetingsPanel.Children.Add(CreateEmptyState("Brak spotkań", "📅", Purple));
                return;
            }

            foreach (var meeting in meetings.Take(10))
            {
                meetingsPanel.Children.Add(CreateMeetingCard(meeting));
            }
        }

        private void BuildTasksColumn()
        {
            tasksPanel.Children.Clear();

            if (tasks.Count == 0)
            {
                tasksPanel.Children.Add(CreateEmptyState("Brak zadań", "✓", InfoBlue));
                return;
            }

            var now = DateTime.Now;
            var overdueTasks = tasks.Where(t => t.DueDate < now).ToList();
            var upcomingTasks = tasks.Where(t => t.DueDate >= now).ToList();

            foreach (var task in overdueTasks.Take(5))
            {
                tasksPanel.Children.Add(CreateTaskCard(task, true));
            }

            foreach (var task in upcomingTasks.Take(5))
            {
                tasksPanel.Children.Add(CreateTaskCard(task, false));
            }
        }

        private Border CreateMeetingCard(MeetingNotification meeting)
        {
            var isLive = meeting.IsLive;
            var borderColor = isLive ? AlertRed : Color.FromArgb(0, 0, 0, 0);

            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = isLive ? new SolidColorBrush(AlertRed) : null,
                BorderThickness = isLive ? new Thickness(1) : new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (isLive)
            {
                card.Effect = new DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.3,
                    Color = AlertRed
                };
            }

            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainStack = new StackPanel();

            // Title row
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = meeting.Title,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(title, 0);
            titleRow.Children.Add(title);

            // Time badge
            var timeText = isLive ? "LIVE" : GetRelativeTime(meeting.MeetingDate);
            var badgeColor = isLive ? AlertRed : Purple;
            var timeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(isLive ? (byte)255 : (byte)50, badgeColor.R, badgeColor.G, badgeColor.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            timeBadge.Child = new TextBlock
            {
                Text = timeText,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = isLive ? Brushes.White : new SolidColorBrush(badgeColor)
            };
            Grid.SetColumn(timeBadge, 1);
            titleRow.Children.Add(timeBadge);

            mainStack.Children.Add(titleRow);

            // Time and location
            var infoText = meeting.MeetingDate.ToString("HH:mm") + $" • {meeting.DurationMin} min";
            if (!string.IsNullOrEmpty(meeting.Location))
                infoText += $" • {meeting.Location}";

            mainStack.Children.Add(new TextBlock
            {
                Text = infoText,
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Avatars - LARGE 42px
            if (meeting.Attendees.Count > 0)
            {
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

                for (int i = 0; i < Math.Min(meeting.Attendees.Count, 5); i++)
                {
                    avatarRow.Children.Add(CreateAvatar(meeting.Attendees[i], i, 42));
                }

                if (meeting.Attendees.Count > 5)
                {
                    var moreCount = new Border
                    {
                        Width = 42,
                        Height = 42,
                        CornerRadius = new CornerRadius(21),
                        Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                        Margin = new Thickness(-10, 0, 0, 0)
                    };
                    moreCount.Child = new TextBlock
                    {
                        Text = $"+{meeting.Attendees.Count - 5}",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    avatarRow.Children.Add(moreCount);
                }

                mainStack.Children.Add(avatarRow);
            }

            // Attendance buttons - Będę / Nie będę
            var currentUserAttendee = meeting.Attendees.FirstOrDefault(a => a.OperatorId == operatorId);
            var isOrganizer = meeting.OrganizerId == operatorId;

            // Only show buttons if user is an attendee (not organizer) and hasn't responded yet
            if (currentUserAttendee != null && !isOrganizer)
            {
                var currentStatus = currentUserAttendee.Status;
                var buttonRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Będę button
                var acceptBtn = new Border
                {
                    Background = currentStatus == "Zaakceptowane"
                        ? new SolidColorBrush(PrimaryGreen)
                        : new SolidColorBrush(Color.FromArgb(50, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                acceptBtn.Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "✓", FontSize = 11, Foreground = currentStatus == "Zaakceptowane" ? Brushes.White : new SolidColorBrush(PrimaryGreen), Margin = new Thickness(0, 0, 4, 0) },
                        new TextBlock { Text = "Będę", FontSize = 11, FontWeight = FontWeights.Medium, Foreground = currentStatus == "Zaakceptowane" ? Brushes.White : new SolidColorBrush(PrimaryGreen) }
                    }
                };
                acceptBtn.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    UpdateAttendanceStatus(meeting.Id, "Zaakceptowane");
                };
                acceptBtn.MouseEnter += (s, e) =>
                {
                    if (currentStatus != "Zaakceptowane")
                        acceptBtn.Background = new SolidColorBrush(Color.FromArgb(100, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B));
                };
                acceptBtn.MouseLeave += (s, e) =>
                {
                    if (currentStatus != "Zaakceptowane")
                        acceptBtn.Background = new SolidColorBrush(Color.FromArgb(50, PrimaryGreen.R, PrimaryGreen.G, PrimaryGreen.B));
                };

                // Nie będę button
                var declineBtn = new Border
                {
                    Background = currentStatus == "Odrzucone"
                        ? new SolidColorBrush(AlertRed)
                        : new SolidColorBrush(Color.FromArgb(50, AlertRed.R, AlertRed.G, AlertRed.B)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                declineBtn.Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "✗", FontSize = 11, Foreground = currentStatus == "Odrzucone" ? Brushes.White : new SolidColorBrush(AlertRed), Margin = new Thickness(0, 0, 4, 0) },
                        new TextBlock { Text = "Nie będę", FontSize = 11, FontWeight = FontWeights.Medium, Foreground = currentStatus == "Odrzucone" ? Brushes.White : new SolidColorBrush(AlertRed) }
                    }
                };
                declineBtn.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    UpdateAttendanceStatus(meeting.Id, "Odrzucone");
                };
                declineBtn.MouseEnter += (s, e) =>
                {
                    if (currentStatus != "Odrzucone")
                        declineBtn.Background = new SolidColorBrush(Color.FromArgb(100, AlertRed.R, AlertRed.G, AlertRed.B));
                };
                declineBtn.MouseLeave += (s, e) =>
                {
                    if (currentStatus != "Odrzucone")
                        declineBtn.Background = new SolidColorBrush(Color.FromArgb(50, AlertRed.R, AlertRed.G, AlertRed.B));
                };

                buttonRow.Children.Add(acceptBtn);
                buttonRow.Children.Add(declineBtn);

                // Show current status indicator
                if (currentStatus == "Zaakceptowane" || currentStatus == "Odrzucone")
                {
                    var statusIndicator = new TextBlock
                    {
                        Text = currentStatus == "Zaakceptowane" ? "(Potwierdzono)" : "(Odrzucono)",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(TextGray),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    buttonRow.Children.Add(statusIndicator);
                }

                mainStack.Children.Add(buttonRow);
            }

            card.Child = mainStack;
            return card;
        }

        private void UpdateAttendanceStatus(long meetingId, string newStatus)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        UPDATE SpotkaniaUczestnicy
                        SET StatusZaproszenia = @status
                        WHERE SpotkaniID = @meetingId AND OperatorID = @operatorId", conn);

                    cmd.Parameters.AddWithValue("@status", newStatus);
                    cmd.Parameters.AddWithValue("@meetingId", meetingId);
                    cmd.Parameters.AddWithValue("@operatorId", operatorId);
                    cmd.ExecuteNonQuery();
                }

                // Refresh meetings display
                LoadMeetings();
                BuildMeetingsColumn();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating attendance: {ex.Message}");
                MessageBox.Show($"Błąd podczas aktualizacji statusu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateTaskCard(TaskNotification task, bool isOverdue)
        {
            var priorityColor = task.Priority == 3 ? AlertRed : task.Priority == 2 ? WarningOrange : PrimaryGreen;

            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(CardBgHover);
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(CardBg);

            var mainStack = new StackPanel();

            // Title row
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Priority dot
            var priorityDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(priorityColor),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(priorityDot, 0);
            titleRow.Children.Add(priorityDot);

            var title = new TextBlock
            {
                Text = task.Title,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);
            titleRow.Children.Add(title);

            // Time badge
            var timeText = isOverdue ? "Zaległe" : GetRelativeTime(task.DueDate);
            var badgeColor = isOverdue ? AlertRed : InfoBlue;
            var timeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, badgeColor.R, badgeColor.G, badgeColor.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            timeBadge.Child = new TextBlock
            {
                Text = timeText,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(badgeColor)
            };
            Grid.SetColumn(timeBadge, 2);
            titleRow.Children.Add(timeBadge);

            mainStack.Children.Add(titleRow);

            // Time and priority
            var priorityText = task.Priority == 3 ? "Wysoki" : task.Priority == 2 ? "Średni" : "Niski";
            mainStack.Children.Add(new TextBlock
            {
                Text = $"{task.DueDate:HH:mm} • Priorytet: {priorityText}",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(16, 4, 0, 0)
            });

            // Avatars - LARGE 42px
            if (task.Assignees.Count > 0)
            {
                var avatarRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 10, 0, 0) };

                for (int i = 0; i < Math.Min(task.Assignees.Count, 4); i++)
                {
                    var assignee = task.Assignees[i];
                    avatarRow.Children.Add(CreateAvatar(
                        new MeetingAttendee { Name = assignee.Name, OperatorId = assignee.OperatorId, Status = "Przypisany" },
                        i, 42));
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

                mainStack.Children.Add(avatarRow);
            }

            card.Child = mainStack;
            return card;
        }

        private Border CreateAvatar(MeetingAttendee attendee, int index, int size)
        {
            // Status-based border color
            var borderColor = attendee.Status == "Zaakceptowane" ? PrimaryGreen :
                            attendee.Status == "Odrzucone" ? AlertRed :
                            attendee.Status == "Przypisany" ? InfoBlue : TextGray;

            var avatar = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(3),
                Margin = new Thickness(index > 0 ? -10 : 0, 0, 0, 0)
            };

            avatar.Effect = new DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.25,
                Color = Colors.Black
            };

            // Check if user has a real avatar
            bool hasRealAvatar = !string.IsNullOrEmpty(attendee.OperatorId) && UserAvatarManager.HasAvatar(attendee.OperatorId);

            if (hasRealAvatar)
            {
                // Use real avatar image
                try
                {
                    var avatarPath = UserAvatarManager.GetAvatarPath(attendee.OperatorId);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = size;
                    bitmap.DecodePixelHeight = size;
                    bitmap.EndInit();

                    var imageBrush = new ImageBrush
                    {
                        ImageSource = bitmap,
                        Stretch = Stretch.UniformToFill
                    };
                    avatar.Background = imageBrush;
                }
                catch
                {
                    // Fallback to initials if image loading fails
                    SetInitialsBackground(avatar, attendee, index, size);
                }
            }
            else
            {
                // Use initials fallback
                SetInitialsBackground(avatar, attendee, index, size);
            }

            // Tooltip
            var statusText = attendee.Status == "Zaakceptowane" ? "Potwierdzone" :
                           attendee.Status == "Odrzucone" ? "Odrzucone" :
                           attendee.Status == "Przypisany" ? "Przypisany" : "Oczekuje";

            avatar.ToolTip = $"{attendee.Name}\nStatus: {statusText}";

            return avatar;
        }

        private void SetInitialsBackground(Border avatar, MeetingAttendee attendee, int index, int size)
        {
            var colorHex = AvatarColors[Math.Abs(attendee.Name?.GetHashCode() ?? index) % AvatarColors.Length];
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var initials = GetInitials(attendee.Name);

            avatar.Background = new SolidColorBrush(color);
            avatar.Child = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = size * 0.35,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private UIElement CreateEmptyState(string message, string icon, Color color)
        {
            var container = new Border { Margin = new Thickness(0, 40, 0, 40) };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconBorder = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(25),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B))
            };
            iconBorder.Child = new TextBlock
            {
                Text = icon,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(iconBorder);

            stack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = new SolidColorBrush(TextGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
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
                default: snoozeTime = TimeSpan.FromHours(2); break; // Default to 2 hours
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
