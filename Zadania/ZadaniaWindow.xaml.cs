using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    public partial class ZadaniaWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string operatorId;
        private List<ZadanieViewModel> allTasks = new List<ZadanieViewModel>();

        public ZadaniaWindow()
        {
            InitializeComponent();
            operatorId = App.UserID;
            LoadTasks();
        }

        private void LoadTasks()
        {
            allTasks.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz zadania gdzie użytkownik jest twórcą LUB jest przypisany
                    var cmd = new SqlCommand(@"
                        SELECT DISTINCT
                            Z.ID,
                            Z.TypZadania,
                            Z.Opis,
                            Z.TerminWykonania,
                            Z.Priorytet,
                            Z.Wykonane,
                            ISNULL(Z.Zespolowe, 0) as Zespolowe,
                            Z.OperatorID as TworcaID,
                            o.Name as TworcaNazwa
                        FROM Zadania Z
                        LEFT JOIN operators o ON Z.OperatorID = o.ID
                        LEFT JOIN ZadaniaPrzypisani zp ON Z.ID = zp.ZadanieID
                        WHERE Z.OperatorID = @id OR zp.OperatorID = @id
                        ORDER BY Z.Wykonane ASC, Z.Priorytet DESC, Z.TerminWykonania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    var taskIds = new List<int>();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var task = new ZadanieViewModel
                            {
                                Id = reader.GetInt32(0),
                                TypZadania = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Opis = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                TerminWykonania = reader.GetDateTime(3),
                                Priorytet = reader.GetInt32(4),
                                Wykonane = reader.GetBoolean(5),
                                Zespolowe = reader.GetBoolean(6),
                                TworcaID = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                TworcaNazwa = reader.IsDBNull(8) ? "" : reader.GetString(8)
                            };
                            allTasks.Add(task);
                            taskIds.Add(task.Id);
                        }
                    }

                    // Pobierz przypisanych pracowników dla każdego zadania
                    if (taskIds.Count > 0)
                    {
                        var idsParam = string.Join(",", taskIds);
                        var assignCmd = new SqlCommand($@"
                            SELECT ZadanieID, OperatorID, OperatorNazwa
                            FROM ZadaniaPrzypisani
                            WHERE ZadanieID IN ({idsParam})", conn);

                        using (var reader = assignCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var zadanieId = reader.GetInt32(0);
                                var task = allTasks.FirstOrDefault(t => t.Id == zadanieId);
                                if (task != null)
                                {
                                    task.Przypisani.Add(new PracownikInfo
                                    {
                                        Id = reader.GetString(1),
                                        Nazwa = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2)
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania zadań: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ApplyFilters();
            UpdateStats();
        }

        private void ApplyFilters()
        {
            var filtered = allTasks.AsEnumerable();

            // Status filter
            if (filterAktywne.IsChecked == true)
                filtered = filtered.Where(t => !t.Wykonane && t.TerminWykonania >= DateTime.Today);
            else if (filterZalegle.IsChecked == true)
                filtered = filtered.Where(t => !t.Wykonane && t.TerminWykonania < DateTime.Today);
            else if (filterWykonane.IsChecked == true)
                filtered = filtered.Where(t => t.Wykonane);

            // Priority filter
            var priorityItem = cmbPriorytet.SelectedItem as ComboBoxItem;
            if (priorityItem != null && priorityItem.Content.ToString() != "Wszystkie")
            {
                int priority = priorityItem.Content.ToString() == "Wysoki" ? 3 :
                               priorityItem.Content.ToString() == "Średni" ? 2 : 1;
                filtered = filtered.Where(t => t.Priorytet == priority);
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                var search = txtSearch.Text.ToLower();
                filtered = filtered.Where(t =>
                    (t.TypZadania?.ToLower().Contains(search) ?? false) ||
                    (t.Opis?.ToLower().Contains(search) ?? false) ||
                    t.Przypisani.Any(p => p.Nazwa.ToLower().Contains(search)));
            }

            var result = filtered.ToList();
            BuildTimelineView(result);
            emptyState.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BuildTimelineView(List<ZadanieViewModel> tasks)
        {
            timelineContainer.Children.Clear();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekEnd = today.AddDays(7);

            // Grupowanie zadań
            var overdue = tasks.Where(t => !t.Wykonane && t.TerminWykonania.Date < today).OrderBy(t => t.TerminWykonania).ToList();
            var todayTasks = tasks.Where(t => !t.Wykonane && t.TerminWykonania.Date == today).OrderBy(t => t.TerminWykonania).ToList();
            var tomorrowTasks = tasks.Where(t => !t.Wykonane && t.TerminWykonania.Date == tomorrow).OrderBy(t => t.TerminWykonania).ToList();
            var thisWeek = tasks.Where(t => !t.Wykonane && t.TerminWykonania.Date > tomorrow && t.TerminWykonania.Date <= weekEnd).OrderBy(t => t.TerminWykonania).ToList();
            var later = tasks.Where(t => !t.Wykonane && t.TerminWykonania.Date > weekEnd).OrderBy(t => t.TerminWykonania).ToList();
            var completed = tasks.Where(t => t.Wykonane).OrderByDescending(t => t.TerminWykonania).ToList();

            // Buduj sekcje
            if (overdue.Count > 0)
                AddTimelineSection("Zaległe", "#f44336", "⚠️", overdue);
            if (todayTasks.Count > 0)
                AddTimelineSection("Dziś", "#FF9800", "📅", todayTasks);
            if (tomorrowTasks.Count > 0)
                AddTimelineSection("Jutro", "#2196F3", "📆", tomorrowTasks);
            if (thisWeek.Count > 0)
                AddTimelineSection("Ten tydzień", "#9C27B0", "🗓️", thisWeek);
            if (later.Count > 0)
                AddTimelineSection("Później", "#607D8B", "📋", later);
            if (completed.Count > 0)
                AddTimelineSection("Wykonane", "#4CAF50", "✅", completed);
        }

        private void AddTimelineSection(string title, string colorHex, string icon, List<ZadanieViewModel> tasks)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);

            // Kolumna sekcji
            var column = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x2e)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                MinWidth = 280,
                MaxWidth = 320,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var columnStack = new StackPanel();

            // Header
            var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };
            headerLeft.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            headerLeft.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(headerLeft, 0);
            header.Children.Add(headerLeft);

            var countBadge = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2)
            };
            countBadge.Child = new TextBlock
            {
                Text = tasks.Count.ToString(),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(countBadge, 2);
            header.Children.Add(countBadge);

            columnStack.Children.Add(header);

            // Linia separatora
            columnStack.Children.Add(new Border
            {
                Height = 2,
                Background = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(1),
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Lista zadań w ScrollViewer
            var tasksScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 500
            };

            var tasksStack = new StackPanel();
            foreach (var task in tasks)
            {
                tasksStack.Children.Add(CreateTaskCard(task, color));
            }
            tasksScroll.Content = tasksStack;

            columnStack.Children.Add(tasksScroll);
            column.Child = columnStack;
            timelineContainer.Children.Add(column);
        }

        private Border CreateTaskCard(ZadanieViewModel task, Color sectionColor)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x42)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = task.PriorityColor,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel();

            // Wiersz 1: Checkbox + Tytuł
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var checkbox = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Background = task.Wykonane
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                BorderBrush = task.Wykonane
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Color.FromRgb(0x5a, 0x5a, 0x7c)),
                BorderThickness = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = task.Id
            };
            checkbox.Child = new TextBlock
            {
                Text = task.Wykonane ? "✓" : "",
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkbox.MouseLeftButtonUp += (s, ev) =>
            {
                var id = (int)((Border)s).Tag;
                var t = allTasks.FirstOrDefault(x => x.Id == id);
                if (t != null)
                {
                    t.Wykonane = !t.Wykonane;
                    UpdateTaskInDatabase(id, t.Wykonane);
                    UpdateStats();
                    ApplyFilters();
                }
            };
            Grid.SetColumn(checkbox, 0);
            row1.Children.Add(checkbox);

            var titleText = new TextBlock
            {
                Text = task.TypZadania,
                Foreground = task.Wykonane ? new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) : Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                TextDecorations = task.TextDecoration,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 1);
            row1.Children.Add(titleText);

            stack.Children.Add(row1);

            // Wiersz 2: Czas + Zespołowe
            var row2 = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(26, 4, 0, 0)
            };

            row2.Children.Add(new TextBlock
            {
                Text = task.TerminWykonania.ToString("HH:mm"),
                Foreground = task.TerminColor,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (task.Zespolowe)
            {
                row2.Children.Add(new TextBlock
                {
                    Text = " 👥",
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Zadanie zespołowe"
                });
            }

            stack.Children.Add(row2);

            // Wiersz 3: Avatary + Akcje
            var row3 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            foreach (var pracownik in task.Przypisani.Take(4))
            {
                var avatar = CreateAvatar(pracownik.Id, pracownik.Nazwa, 24);
                if (avatar is FrameworkElement fe)
                {
                    fe.Margin = new Thickness(0, 0, -4, 0);
                    fe.ToolTip = pracownik.Nazwa;
                }
                avatarsPanel.Children.Add(avatar);
            }

            if (task.Przypisani.Count > 4)
            {
                var more = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                    Margin = new Thickness(2, 0, 0, 0)
                };
                more.Child = new TextBlock
                {
                    Text = $"+{task.Przypisani.Count - 4}",
                    Foreground = Brushes.White,
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                avatarsPanel.Children.Add(more);
            }

            Grid.SetColumn(avatarsPanel, 0);
            row3.Children.Add(avatarsPanel);

            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var editBtn = new Button
            {
                Content = "✏️",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = task.Id,
                FontSize = 11
            };
            editBtn.Click += BtnEdytuj_Click;
            actionsPanel.Children.Add(editBtn);

            var deleteBtn = new Button
            {
                Content = "🗑️",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = task.Id,
                FontSize = 11
            };
            deleteBtn.Click += BtnUsun_Click;
            actionsPanel.Children.Add(deleteBtn);

            Grid.SetColumn(actionsPanel, 1);
            row3.Children.Add(actionsPanel);

            stack.Children.Add(row3);

            card.Child = stack;

            // Hover effect
            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(0x2d, 0x2d, 0x50));
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x42));

            return card;
        }
        private Border CreateTaskItem(ZadanieViewModel task)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x36)),
                BorderBrush = task.BorderColor,
                BorderThickness = new Thickness(0, 0, 0, 3),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(4),
                Padding = new Thickness(12, 10, 12, 10),
                MinHeight = 95
            };

            var mainStack = new StackPanel();

            // Header row: Checkbox + Title + Status
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Checkbox
            var checkboxBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Background = task.Wykonane
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                BorderBrush = task.Wykonane
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Color.FromRgb(0x5a, 0x5a, 0x7c)),
                BorderThickness = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = task.Id
            };

            var checkMark = new TextBlock
            {
                Text = task.Wykonane ? "✓" : "",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkboxBorder.Child = checkMark;

            checkboxBorder.MouseLeftButtonUp += (s, ev) =>
            {
                var id = (int)((Border)s).Tag;
                var t = allTasks.FirstOrDefault(x => x.Id == id);
                if (t != null)
                {
                    t.Wykonane = !t.Wykonane;
                    UpdateTaskInDatabase(id, t.Wykonane);
                    UpdateStats();
                    ApplyFilters();
                }
            };

            Grid.SetColumn(checkboxBorder, 0);
            headerGrid.Children.Add(checkboxBorder);

            // Title
            var titleText = new TextBlock
            {
                Text = task.TypZadania,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextDecorations = task.TextDecoration,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleText, 1);
            headerGrid.Children.Add(titleText);

            // Status badge
            var statusBadge = new Border
            {
                Background = task.StatusColor,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadge.Child = new TextBlock
            {
                Text = task.StatusText,
                Foreground = Brushes.White,
                FontSize = 10
            };
            Grid.SetColumn(statusBadge, 2);
            headerGrid.Children.Add(statusBadge);

            mainStack.Children.Add(headerGrid);

            // Badges row (Priority + Team)
            var badgesStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

            var priorityBadge = new Border
            {
                Background = task.PriorityColor,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            priorityBadge.Child = new TextBlock
            {
                Text = task.PriorityText,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            badgesStack.Children.Add(priorityBadge);

            if (task.Zespolowe)
            {
                var teamBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                teamBadge.Child = new TextBlock
                {
                    Text = "Zespołowe",
                    Foreground = Brushes.White,
                    FontSize = 10
                };
                badgesStack.Children.Add(teamBadge);
            }

            mainStack.Children.Add(badgesStack);

            // Description
            if (!string.IsNullOrWhiteSpace(task.Opis))
            {
                var opisText = new TextBlock
                {
                    Text = task.Opis.Length > 80 ? task.Opis.Substring(0, 80) + "..." : task.Opis,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    FontSize = 11,
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 30
                };
                mainStack.Children.Add(opisText);
            }

            // Bottom row: Avatars + Termin + Actions
            var bottomGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatars
            var avatarsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var maxAvatars = 4;
            foreach (var pracownik in task.Przypisani.Take(maxAvatars))
            {
                var avatar = CreateAvatar(pracownik.Id, pracownik.Nazwa, 38);
                if (avatar is FrameworkElement fe)
                {
                    fe.Margin = new Thickness(0, 0, -8, 0);
                    fe.ToolTip = pracownik.Nazwa;
                }
                avatarsPanel.Children.Add(avatar);
            }

            if (task.Przypisani.Count > maxAvatars)
            {
                var more = new Border
                {
                    Width = 38,
                    Height = 38,
                    CornerRadius = new CornerRadius(19),
                    Background = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                    Margin = new Thickness(4, 0, 0, 0)
                };
                more.Child = new TextBlock
                {
                    Text = $"+{task.Przypisani.Count - maxAvatars}",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                avatarsPanel.Children.Add(more);
            }

            Grid.SetColumn(avatarsPanel, 0);
            bottomGrid.Children.Add(avatarsPanel);

            // Termin
            var terminStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };

            var terminText = new TextBlock
            {
                Text = task.TerminText,
                Foreground = task.TerminColor,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            terminStack.Children.Add(terminText);

            var relativeText = new TextBlock
            {
                Text = task.TerminRelative,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            terminStack.Children.Add(relativeText);

            Grid.SetColumn(terminStack, 1);
            bottomGrid.Children.Add(terminStack);

            // Action buttons
            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var editBtn = new Button
            {
                Content = "✏️",
                Background = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = task.Id
            };
            editBtn.Click += BtnEdytuj_Click;
            actionsPanel.Children.Add(editBtn);

            var deleteBtn = new Button
            {
                Content = "🗑️",
                Background = new SolidColorBrush(Color.FromRgb(0x5c, 0x3a, 0x3a)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = task.Id
            };
            deleteBtn.Click += BtnUsun_Click;
            actionsPanel.Children.Add(deleteBtn);

            Grid.SetColumn(actionsPanel, 2);
            bottomGrid.Children.Add(actionsPanel);

            mainStack.Children.Add(bottomGrid);

            border.Child = mainStack;
            return border;
        }

        private UIElement CreateAvatar(string id, string name, int size)
        {
            var avatarPath = GetAvatarPath(id);

            if (File.Exists(avatarPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    var ellipse = new Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = brush,
                        Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x42)),
                        StrokeThickness = 2
                    };
                    return ellipse;
                }
                catch { }
            }

            // Domyślny avatar
            var grid = new Grid { Width = size, Height = size };

            var bgColor = GetColorFromId(id);
            var circle = new Ellipse
            {
                Fill = new SolidColorBrush(bgColor),
                Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x42)),
                StrokeThickness = 2
            };
            grid.Children.Add(circle);

            var initials = GetInitials(name);
            var text = new TextBlock
            {
                Text = initials,
                Foreground = Brushes.White,
                FontSize = size / 2.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(text);

            return grid;
        }

        private string GetAvatarPath(string userId)
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZPSP", "Avatars", $"{userId}.png");
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private Color GetColorFromId(string id)
        {
            int hash = id?.GetHashCode() ?? 0;
            Color[] colors = {
                Color.FromRgb(46, 125, 50),
                Color.FromRgb(25, 118, 210),
                Color.FromRgb(156, 39, 176),
                Color.FromRgb(230, 81, 0),
                Color.FromRgb(0, 137, 123),
                Color.FromRgb(194, 24, 91),
                Color.FromRgb(69, 90, 100),
                Color.FromRgb(121, 85, 72)
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private void UpdateStats()
        {
            var now = DateTime.Today;
            statWszystkie.Text = allTasks.Count.ToString();
            statAktywne.Text = allTasks.Count(t => !t.Wykonane && t.TerminWykonania >= now).ToString();
            statZalegle.Text = allTasks.Count(t => !t.Wykonane && t.TerminWykonania < now).ToString();
            statWykonane.Text = allTasks.Count(t => t.Wykonane).ToString();

            var active = allTasks.Count(t => !t.Wykonane);
            var total = allTasks.Count;
            txtSummary.Text = $"{active} aktywnych zadań z {total} wszystkich";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                ApplyFilters();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void BtnNoweZadanie_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ZadanieDialog(connectionString, operatorId);
            if (dialog.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        private void BtnEdytuj_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            var taskId = (int)button.Tag;
            var task = allTasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null) return;

            var dialog = new ZadanieDialog(connectionString, operatorId, task);
            if (dialog.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            var taskId = (int)button.Tag;
            var task = allTasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć zadanie \"{task.TypZadania}\"?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        // Usuń przypisania
                        var delAssign = new SqlCommand("DELETE FROM ZadaniaPrzypisani WHERE ZadanieID = @id", conn);
                        delAssign.Parameters.AddWithValue("@id", taskId);
                        delAssign.ExecuteNonQuery();

                        // Usuń zadanie
                        var cmd = new SqlCommand("DELETE FROM Zadania WHERE ID = @id", conn);
                        cmd.Parameters.AddWithValue("@id", taskId);
                        cmd.ExecuteNonQuery();
                    }
                    LoadTasks();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateTaskInDatabase(int taskId, bool wykonane)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "UPDATE Zadania SET Wykonane = @wykonane WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@wykonane", wykonane);
                    cmd.Parameters.AddWithValue("@id", taskId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas aktualizacji: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                var checkbox = sender as CheckBox;
                if (checkbox?.Tag == null) return;

                var taskId = (int)checkbox.Tag;
                var wykonane = checkbox.IsChecked ?? false;

                UpdateTaskInDatabase(taskId, wykonane);

                var task = allTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    task.Wykonane = wykonane;
                }

                UpdateStats();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas aktualizacji: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LoadTasks();
            }
        }
    }

    public class PracownikInfo
    {
        public string Id { get; set; }
        public string Nazwa { get; set; }
    }

    public class ZadanieViewModel
    {
        public int Id { get; set; }
        public string Firma { get; set; }
        public string TypZadania { get; set; }
        public string Opis { get; set; }
        public DateTime TerminWykonania { get; set; }
        public int Priorytet { get; set; }
        public bool Wykonane { get; set; }
        public int IDOdbiorcy { get; set; }
        public bool Zespolowe { get; set; }
        public string TworcaID { get; set; }
        public string TworcaNazwa { get; set; }
        public List<PracownikInfo> Przypisani { get; set; } = new List<PracownikInfo>();

        public string PriorityText => Priorytet == 3 ? "Wysoki" : Priorytet == 2 ? "Średni" : "Niski";

        public Brush PriorityColor => Priorytet == 3 ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                      Priorytet == 2 ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) :
                                      new SolidColorBrush(Color.FromRgb(76, 175, 80));

        public string StatusText => Wykonane ? "Wykonane" :
                                    TerminWykonania < DateTime.Today ? "Zaległe" :
                                    TerminWykonania.Date == DateTime.Today ? "Na dziś" : "Aktywne";

        public Brush StatusColor => Wykonane ? new SolidColorBrush(Color.FromRgb(158, 158, 158)) :
                                    TerminWykonania < DateTime.Today ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                    TerminWykonania.Date == DateTime.Today ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) :
                                    new SolidColorBrush(Color.FromRgb(33, 150, 243));

        public string TerminText => TerminWykonania.ToString("dd.MM.yyyy HH:mm");

        public Brush TerminColor => Wykonane ? Brushes.Gray :
                                    TerminWykonania < DateTime.Now ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                    TerminWykonania.Date == DateTime.Today ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) :
                                    new SolidColorBrush(Color.FromRgb(170, 170, 170));

        public string TerminRelative
        {
            get
            {
                if (Wykonane) return "Zakończone";
                var diff = TerminWykonania.Date - DateTime.Today;
                if (diff.Days < 0) return $"{Math.Abs(diff.Days)} dni temu";
                if (diff.Days == 0) return "Dziś";
                if (diff.Days == 1) return "Jutro";
                return $"Za {diff.Days} dni";
            }
        }

        public Brush BorderColor => Wykonane ? new SolidColorBrush(Color.FromRgb(100, 100, 100)) :
                                    Priorytet == 3 ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                    Priorytet == 2 ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) :
                                    new SolidColorBrush(Color.FromRgb(76, 175, 80));

        public TextDecorationCollection TextDecoration => Wykonane ? TextDecorations.Strikethrough : null;
    }
}
