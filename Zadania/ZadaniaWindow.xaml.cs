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

            // Buduj listę zadań z avatarami
            tasksList.Items.Clear();
            foreach (var task in result)
            {
                tasksList.Items.Add(CreateTaskItem(task));
            }

            emptyState.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private Border CreateTaskItem(ZadanieViewModel task)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x42)),
                BorderBrush = task.BorderColor,
                BorderThickness = new Thickness(0, 0, 0, 3),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(15, 12, 15, 12)
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Checkbox - duży i widoczny
            var checkboxBorder = new Border
            {
                Width = 28,
                Height = 28,
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
                FontSize = 16,
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
            mainGrid.Children.Add(checkboxBorder);

            // Zadanie info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };

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
            headerStack.Children.Add(priorityBadge);

            if (task.Zespolowe)
            {
                var teamBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                teamBadge.Child = new TextBlock
                {
                    Text = "👥 Zespołowe",
                    Foreground = Brushes.White,
                    FontSize = 10
                };
                headerStack.Children.Add(teamBadge);
            }

            var titleText = new TextBlock
            {
                Text = task.TypZadania,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextDecorations = task.TextDecoration,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerStack.Children.Add(titleText);
            infoStack.Children.Add(headerStack);

            if (!string.IsNullOrWhiteSpace(task.Opis))
            {
                var opisText = new TextBlock
                {
                    Text = task.Opis.Length > 80 ? task.Opis.Substring(0, 80) + "..." : task.Opis,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    FontSize = 12,
                    Margin = new Thickness(0, 4, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                infoStack.Children.Add(opisText);
            }

            Grid.SetColumn(infoStack, 1);
            mainGrid.Children.Add(infoStack);

            // Przypisani (avatary)
            var avatarsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 15, 0)
            };

            var maxAvatars = 4;
            foreach (var pracownik in task.Przypisani.Take(maxAvatars))
            {
                var avatar = CreateAvatar(pracownik.Id, pracownik.Nazwa, 36);
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
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                    Margin = new Thickness(2, 0, 0, 0)
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

            Grid.SetColumn(avatarsPanel, 2);
            mainGrid.Children.Add(avatarsPanel);

            // Termin
            var terminStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
                Width = 100
            };

            var terminText = new TextBlock
            {
                Text = task.TerminText,
                Foreground = task.TerminColor,
                FontSize = 12,
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

            Grid.SetColumn(terminStack, 3);
            mainGrid.Children.Add(terminStack);

            // Status badge
            var statusBadge = new Border
            {
                Background = task.StatusColor,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadge.Child = new TextBlock
            {
                Text = task.StatusText,
                Foreground = Brushes.White,
                FontSize = 11
            };
            Grid.SetColumn(statusBadge, 4);
            mainGrid.Children.Add(statusBadge);

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

            Grid.SetColumn(actionsPanel, 5);
            mainGrid.Children.Add(actionsPanel);

            border.Child = mainGrid;
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
                FontSize = size / 2.8,
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
