using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
                    var cmd = new SqlCommand(@"
                        SELECT
                            Z.ID,
                            ISNULL(O.Nazwa, 'Brak firmy') AS Firma,
                            Z.TypZadania,
                            Z.Opis,
                            Z.TerminWykonania,
                            Z.Priorytet,
                            Z.Wykonane,
                            Z.IDOdbiorcy
                        FROM Zadania Z
                        LEFT JOIN OdbiorcyCRM O ON Z.IDOdbiorcy = O.ID
                        WHERE Z.OperatorID = @id
                        ORDER BY Z.Wykonane ASC, Z.Priorytet DESC, Z.TerminWykonania ASC", conn);

                    cmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var task = new ZadanieViewModel
                            {
                                Id = reader.GetInt32(0),
                                Firma = reader.GetString(1),
                                TypZadania = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Opis = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                TerminWykonania = reader.GetDateTime(4),
                                Priorytet = reader.GetInt32(5),
                                Wykonane = reader.GetBoolean(6),
                                IDOdbiorcy = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                            };
                            allTasks.Add(task);
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
                    (t.Firma?.ToLower().Contains(search) ?? false));
            }

            var result = filtered.ToList();
            tasksList.ItemsSource = result;

            emptyState.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        private void TaskCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox?.Tag == null) return;

            var taskId = (int)checkbox.Tag;
            var wykonane = checkbox.IsChecked ?? false;

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

                // Update local data
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

        public string PriorityText => Priorytet == 3 ? "Wysoki" : Priorytet == 2 ? "Średni" : "Niski";

        public Brush PriorityColor => Priorytet == 3 ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                      Priorytet == 2 ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) :
                                      new SolidColorBrush(Color.FromRgb(76, 175, 80));

        public string StatusText => Wykonane ? "Wykonane" :
                                    TerminWykonania < DateTime.Today ? "Zaległe" :
                                    TerminWykonania == DateTime.Today ? "Na dziś" : "Aktywne";

        public Brush StatusColor => Wykonane ? new SolidColorBrush(Color.FromRgb(158, 158, 158)) :
                                    TerminWykonania < DateTime.Today ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                    TerminWykonania == DateTime.Today ? new SolidColorBrush(Color.FromRgb(255, 152, 0)) :
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
