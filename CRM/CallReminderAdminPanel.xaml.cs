using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Services;

namespace Kalendarz1.CRM
{
    public partial class CallReminderAdminPanel : Window
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ObservableCollection<HandlowiecConfigViewModel> _handlowcy;

        public CallReminderAdminPanel()
        {
            InitializeComponent();
            LoadData();
            LoadStats();
        }

        private void LoadData()
        {
            _handlowcy = new ObservableCollection<HandlowiecConfigViewModel>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Get all operators with CRM access (or all operators)
                var cmdOperators = new SqlCommand(
                    @"SELECT CAST(o.ID AS NVARCHAR) as UserID, o.Name
                      FROM operators o
                      WHERE o.Name IS NOT NULL AND o.Name <> ''
                      ORDER BY o.Name", conn);

                var operators = new List<(string id, string name)>();
                using (var reader = cmdOperators.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        operators.Add((reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
                    }
                }

                // Get existing configs
                var configs = new Dictionary<string, (bool enabled, TimeSpan t1, TimeSpan t2, int count, bool onlyNew, bool onlyAssigned)>();
                var cmdConfigs = new SqlCommand(
                    "SELECT UserID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder, ShowOnlyNewContacts, ShowOnlyAssigned FROM CallReminderConfig", conn);
                using (var reader = cmdConfigs.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var userId = reader.GetString(0);
                        configs[userId] = (
                            reader.GetBoolean(1),
                            reader.GetTimeSpan(2),
                            reader.GetTimeSpan(3),
                            reader.GetInt32(4),
                            reader.GetBoolean(5),
                            reader.GetBoolean(6)
                        );
                    }
                }

                // Build view models
                foreach (var op in operators)
                {
                    var vm = new HandlowiecConfigViewModel
                    {
                        UserID = op.id,
                        UserName = string.IsNullOrEmpty(op.name) ? $"Operator {op.id}" : op.name
                    };

                    if (configs.TryGetValue(op.id, out var cfg))
                    {
                        vm.IsEnabled = cfg.enabled;
                        vm.ReminderTime1 = cfg.t1;
                        vm.ReminderTime2 = cfg.t2;
                        vm.ContactsPerReminder = cfg.count;
                        vm.ShowOnlyNewContacts = cfg.onlyNew;
                        vm.ShowOnlyAssigned = cfg.onlyAssigned;
                    }

                    _handlowcy.Add(vm);
                }

                dgHandlowcy.ItemsSource = _handlowcy;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStats()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Today
                var cmdToday = new SqlCommand(
                    "SELECT COUNT(*), ISNULL(SUM(ContactsCalled), 0) FROM CallReminderLog WHERE CAST(ReminderTime AS DATE) = CAST(GETDATE() AS DATE)", conn);
                using (var reader = cmdToday.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        txtStatsDzis.Text = reader.GetInt32(0).ToString();
                        txtStatsTelefony.Text = reader.GetInt32(1).ToString();
                    }
                }

                // This week
                var cmdWeek = new SqlCommand(
                    "SELECT COUNT(*) FROM CallReminderLog WHERE ReminderTime >= DATEADD(DAY, -7, GETDATE())", conn);
                txtStatsTydzien.Text = cmdWeek.ExecuteScalar()?.ToString() ?? "0";

                // This month
                var cmdMonth = new SqlCommand(
                    "SELECT COUNT(*) FROM CallReminderLog WHERE ReminderTime >= DATEADD(DAY, -30, GETDATE())", conn);
                txtStatsMiesiac.Text = cmdMonth.ExecuteScalar()?.ToString() ?? "0";
            }
            catch
            {
                // Stats are optional, ignore errors
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                foreach (var h in _handlowcy)
                {
                    // Check if config exists
                    var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM CallReminderConfig WHERE UserID = @UserID", conn);
                    cmdCheck.Parameters.AddWithValue("@UserID", h.UserID);
                    var exists = (int)cmdCheck.ExecuteScalar() > 0;

                    if (exists)
                    {
                        var cmdUpdate = new SqlCommand(
                            @"UPDATE CallReminderConfig SET
                                IsEnabled = @Enabled,
                                ReminderTime1 = @Time1,
                                ReminderTime2 = @Time2,
                                ContactsPerReminder = @Count,
                                ShowOnlyNewContacts = @OnlyNew,
                                ShowOnlyAssigned = @OnlyAssigned,
                                ModifiedAt = GETDATE()
                              WHERE UserID = @UserID", conn);
                        cmdUpdate.Parameters.AddWithValue("@UserID", h.UserID);
                        cmdUpdate.Parameters.AddWithValue("@Enabled", h.IsEnabled);
                        cmdUpdate.Parameters.AddWithValue("@Time1", h.ReminderTime1);
                        cmdUpdate.Parameters.AddWithValue("@Time2", h.ReminderTime2);
                        cmdUpdate.Parameters.AddWithValue("@Count", h.ContactsPerReminder);
                        cmdUpdate.Parameters.AddWithValue("@OnlyNew", h.ShowOnlyNewContacts);
                        cmdUpdate.Parameters.AddWithValue("@OnlyAssigned", h.ShowOnlyAssigned);
                        cmdUpdate.ExecuteNonQuery();
                    }
                    else
                    {
                        var cmdInsert = new SqlCommand(
                            @"INSERT INTO CallReminderConfig (UserID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder, ShowOnlyNewContacts, ShowOnlyAssigned)
                              VALUES (@UserID, @Enabled, @Time1, @Time2, @Count, @OnlyNew, @OnlyAssigned)", conn);
                        cmdInsert.Parameters.AddWithValue("@UserID", h.UserID);
                        cmdInsert.Parameters.AddWithValue("@Enabled", h.IsEnabled);
                        cmdInsert.Parameters.AddWithValue("@Time1", h.ReminderTime1);
                        cmdInsert.Parameters.AddWithValue("@Time2", h.ReminderTime2);
                        cmdInsert.Parameters.AddWithValue("@Count", h.ContactsPerReminder);
                        cmdInsert.Parameters.AddWithValue("@OnlyNew", h.ShowOnlyNewContacts);
                        cmdInsert.Parameters.AddWithValue("@OnlyAssigned", h.ShowOnlyAssigned);
                        cmdInsert.ExecuteNonQuery();
                    }
                }

                // Reload service config for current user
                CallReminderService.Instance.ReloadConfig();

                MessageBox.Show("Zapisano zmiany!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnApplyToAll_Click(object sender, RoutedEventArgs e)
        {
            if (_handlowcy == null || _handlowcy.Count == 0) return;

            var result = MessageBox.Show(
                "Czy chcesz zastosować ustawienia pierwszego handlowca do wszystkich?\n\n" +
                $"Godzina 1: {_handlowcy[0].Time1String}\n" +
                $"Godzina 2: {_handlowcy[0].Time2String}\n" +
                $"Kontaktów: {_handlowcy[0].ContactsPerReminder}\n" +
                $"Tylko nowi: {(_handlowcy[0].ShowOnlyNewContacts ? "Tak" : "Nie")}\n" +
                $"Tylko przypisani: {(_handlowcy[0].ShowOnlyAssigned ? "Tak" : "Nie")}",
                "Zastosuj do wszystkich",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var template = _handlowcy[0];
                foreach (var h in _handlowcy.Skip(1))
                {
                    h.ReminderTime1 = template.ReminderTime1;
                    h.ReminderTime2 = template.ReminderTime2;
                    h.ContactsPerReminder = template.ContactsPerReminder;
                    h.ShowOnlyNewContacts = template.ShowOnlyNewContacts;
                    h.ShowOnlyAssigned = template.ShowOnlyAssigned;
                }
                dgHandlowcy.Items.Refresh();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            LoadStats();
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy chcesz wyświetlić testowe okno przypomnienia?",
                "Test",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                CallReminderService.Instance.TriggerReminderNow();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }

    public class HandlowiecConfigViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private TimeSpan _reminderTime1 = new TimeSpan(10, 0, 0);
        private TimeSpan _reminderTime2 = new TimeSpan(13, 0, 0);
        private int _contactsPerReminder = 5;
        private bool _showOnlyNewContacts = true;
        private bool _showOnlyAssigned = false;

        public string UserID { get; set; }
        public string UserName { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public TimeSpan ReminderTime1
        {
            get => _reminderTime1;
            set { _reminderTime1 = value; OnPropertyChanged(nameof(ReminderTime1)); OnPropertyChanged(nameof(Time1String)); }
        }

        public TimeSpan ReminderTime2
        {
            get => _reminderTime2;
            set { _reminderTime2 = value; OnPropertyChanged(nameof(ReminderTime2)); OnPropertyChanged(nameof(Time2String)); }
        }

        public string Time1String
        {
            get => $"{ReminderTime1.Hours:D2}:{ReminderTime1.Minutes:D2}";
            set
            {
                if (TimeSpan.TryParse(value, out var ts))
                {
                    ReminderTime1 = ts;
                }
            }
        }

        public string Time2String
        {
            get => $"{ReminderTime2.Hours:D2}:{ReminderTime2.Minutes:D2}";
            set
            {
                if (TimeSpan.TryParse(value, out var ts))
                {
                    ReminderTime2 = ts;
                }
            }
        }

        public int ContactsPerReminder
        {
            get => _contactsPerReminder;
            set { _contactsPerReminder = value; OnPropertyChanged(nameof(ContactsPerReminder)); }
        }

        public bool ShowOnlyNewContacts
        {
            get => _showOnlyNewContacts;
            set { _showOnlyNewContacts = value; OnPropertyChanged(nameof(ShowOnlyNewContacts)); }
        }

        public bool ShowOnlyAssigned
        {
            get => _showOnlyAssigned;
            set { _showOnlyAssigned = value; OnPropertyChanged(nameof(ShowOnlyAssigned)); }
        }

        // Available options for ComboBoxes
        public List<string> AvailableTimes { get; } = Enumerable.Range(6, 18).SelectMany(h => new[] { $"{h:D2}:00", $"{h:D2}:30" }).ToList();
        public List<int> AvailableCounts { get; } = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
