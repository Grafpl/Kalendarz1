using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;
using Kalendarz1.CRM.Services;
using Kalendarz1.CRM.Dialogs;

namespace Kalendarz1.CRM
{
    public partial class CallReminderAdminPanel : Window
    {
        private readonly string _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ObservableCollection<HandlowiecConfigViewModel> _handlowcy;
        private string _currentSortMode = "activity";

        public CallReminderAdminPanel()
        {
            InitializeComponent();
            EnsureDatabaseSchema();
            LoadData();
            LoadStats();
        }

        #region Database Schema

        private void EnsureDatabaseSchema()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var schemaSql = @"
                -- Core columns
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'DailyCallTarget')
                    ALTER TABLE CallReminderConfig ADD DailyCallTarget INT DEFAULT 30;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'WeeklyCallTarget')
                    ALTER TABLE CallReminderConfig ADD WeeklyCallTarget INT DEFAULT 120;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'MaxAttemptsPerContact')
                    ALTER TABLE CallReminderConfig ADD MaxAttemptsPerContact INT DEFAULT 5;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'CooldownDays')
                    ALTER TABLE CallReminderConfig ADD CooldownDays INT DEFAULT 3;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'MinCallDurationSec')
                    ALTER TABLE CallReminderConfig ADD MinCallDurationSec INT DEFAULT 30;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'AlertBelowPercent')
                    ALTER TABLE CallReminderConfig ADD AlertBelowPercent INT DEFAULT 50;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'PKDPriorityWeight')
                    ALTER TABLE CallReminderConfig ADD PKDPriorityWeight INT DEFAULT 70;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'UsePresetPKD')
                    ALTER TABLE CallReminderConfig ADD UsePresetPKD NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'SourcePriority')
                    ALTER TABLE CallReminderConfig ADD SourcePriority NVARCHAR(20) DEFAULT 'mixed';

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'ManualContactsPercent')
                    ALTER TABLE CallReminderConfig ADD ManualContactsPercent INT DEFAULT 50;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'TerritoryWojewodztwa')
                    ALTER TABLE CallReminderConfig ADD TerritoryWojewodztwa NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'PresetType')
                    ALTER TABLE CallReminderConfig ADD PresetType NVARCHAR(30) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'OnlyMyImports')
                    ALTER TABLE CallReminderConfig ADD OnlyMyImports BIT DEFAULT 0;

                -- PKD Priority table with SortOrder
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CallReminderPKDPriority') AND type = 'U')
                BEGIN
                    CREATE TABLE CallReminderPKDPriority (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        ConfigID INT NOT NULL,
                        PKDCode NVARCHAR(10) NOT NULL,
                        PKDName NVARCHAR(255),
                        SortOrder INT DEFAULT 0,
                        Priority INT DEFAULT 50,
                        IsExcluded BIT DEFAULT 0,
                        CreatedAt DATETIME DEFAULT GETDATE(),
                        CONSTRAINT FK_PKDPriority_Config FOREIGN KEY (ConfigID) REFERENCES CallReminderConfig(ID)
                    );
                    CREATE INDEX IX_PKDPriority_Config ON CallReminderPKDPriority(ConfigID);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderPKDPriority') AND name = 'SortOrder')
                    ALTER TABLE CallReminderPKDPriority ADD SortOrder INT DEFAULT 0;

                -- PKD Dictionary table
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('PKD_Slownik') AND type = 'U')
                BEGIN
                    CREATE TABLE PKD_Slownik (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        Kod NVARCHAR(10) NOT NULL,
                        Nazwa NVARCHAR(500) NOT NULL,
                        Kategoria NVARCHAR(100) NULL
                    );
                    CREATE INDEX IX_PKD_Kod ON PKD_Slownik(Kod);
                END

                -- Import History table
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('crm_ImportHistory') AND type = 'U')
                BEGIN
                    CREATE TABLE crm_ImportHistory (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        ImportedBy NVARCHAR(50) NOT NULL,
                        FileName NVARCHAR(500),
                        TotalRows INT DEFAULT 0,
                        SuccessRows INT DEFAULT 0,
                        FailedRows INT DEFAULT 0,
                        ImportedAt DATETIME DEFAULT GETDATE()
                    );
                END

                -- Columns for crm_Kontakty import tracking
                IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('crm_Kontakty') AND type = 'U')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('crm_Kontakty') AND name = 'IsFromImport')
                        ALTER TABLE crm_Kontakty ADD IsFromImport BIT DEFAULT 0;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('crm_Kontakty') AND name = 'ImportID')
                        ALTER TABLE crm_Kontakty ADD ImportID INT NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('crm_Kontakty') AND name = 'ImportedBy')
                        ALTER TABLE crm_Kontakty ADD ImportedBy NVARCHAR(50) NULL;
                END

                -- Audit table
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CallReminderConfigAudit') AND type = 'U')
                BEGIN
                    CREATE TABLE CallReminderConfigAudit (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        ConfigID INT NOT NULL,
                        FieldName NVARCHAR(100) NOT NULL,
                        OldValue NVARCHAR(MAX),
                        NewValue NVARCHAR(MAX),
                        ChangedBy NVARCHAR(50) NOT NULL,
                        ChangedAt DATETIME DEFAULT GETDATE(),
                        ChangeType NVARCHAR(20)
                    );
                END
                ";

                var cmd = new SqlCommand(schemaSql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdminPanel] Schema ensure error: {ex.Message}");
            }
        }

        #endregion

        #region Data Loading

        private void LoadData()
        {
            _handlowcy = new ObservableCollection<HandlowiecConfigViewModel>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Get all operators
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
                var configs = new Dictionary<string, ConfigData>();
                var cmdConfigs = new SqlCommand(
                    @"SELECT UserID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder,
                             ShowOnlyNewContacts, ShowOnlyAssigned, ID,
                             ISNULL(DailyCallTarget, 30), ISNULL(WeeklyCallTarget, 120),
                             ISNULL(MaxAttemptsPerContact, 5), ISNULL(CooldownDays, 3),
                             ISNULL(PKDPriorityWeight, 70),
                             TerritoryWojewodztwa, PresetType,
                             ISNULL(OnlyMyImports, 0)
                      FROM CallReminderConfig", conn);
                using (var reader = cmdConfigs.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var userId = reader.GetString(0);
                        configs[userId] = new ConfigData
                        {
                            Enabled = reader.GetBoolean(1),
                            Time1 = reader.GetTimeSpan(2),
                            Time2 = reader.GetTimeSpan(3),
                            Count = reader.GetInt32(4),
                            OnlyNew = reader.GetBoolean(5),
                            OnlyAssigned = reader.GetBoolean(6),
                            ConfigID = reader.GetInt32(7),
                            DailyCallTarget = reader.GetInt32(8),
                            WeeklyCallTarget = reader.GetInt32(9),
                            MaxAttemptsPerContact = reader.GetInt32(10),
                            CooldownDays = reader.GetInt32(11),
                            PKDPriorityWeight = reader.GetInt32(12),
                            TerritoryWojewodztwa = reader.IsDBNull(13) ? null : reader.GetString(13),
                            PresetType = reader.IsDBNull(14) ? null : reader.GetString(14),
                            OnlyMyImports = reader.GetBoolean(15)
                        };
                    }
                }

                // Get today's call counts
                var todayCalls = new Dictionary<string, int>();
                try
                {
                    var cmdTodayCalls = new SqlCommand(
                        @"SELECT UserID, SUM(ContactsCalled) FROM CallReminderLog
                          WHERE CAST(ReminderTime AS DATE) = CAST(GETDATE() AS DATE)
                          GROUP BY UserID", conn);
                    using (var reader = cmdTodayCalls.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            todayCalls[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
                catch { }

                // Get week call counts
                var weekCalls = new Dictionary<string, int>();
                try
                {
                    var cmdWeekCalls = new SqlCommand(
                        @"SELECT UserID, SUM(ContactsCalled) FROM CallReminderLog
                          WHERE ReminderTime >= DATEADD(DAY, -7, GETDATE())
                          GROUP BY UserID", conn);
                    using (var reader = cmdWeekCalls.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            weekCalls[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
                catch { }

                // Load PKD priorities per config
                var pkdPriorities = new Dictionary<int, List<string>>();
                try
                {
                    var cmdPkd = new SqlCommand(
                        @"SELECT ConfigID, PKDCode FROM CallReminderPKDPriority
                          ORDER BY ConfigID, SortOrder", conn);
                    using (var reader = cmdPkd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int configId = reader.GetInt32(0);
                            string code = reader.GetString(1);
                            if (!pkdPriorities.ContainsKey(configId))
                                pkdPriorities[configId] = new List<string>();
                            pkdPriorities[configId].Add(code);
                        }
                    }
                }
                catch { }

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
                        vm.IsEnabled = cfg.Enabled;
                        vm.ReminderTime1 = cfg.Time1;
                        vm.ReminderTime2 = cfg.Time2;
                        vm.ContactsPerReminder = cfg.Count;
                        vm.ShowOnlyNewContacts = cfg.OnlyNew;
                        vm.ShowOnlyAssigned = cfg.OnlyAssigned;
                        vm.ConfigID = cfg.ConfigID;
                        vm.DailyCallTarget = cfg.DailyCallTarget;
                        vm.WeeklyCallTarget = cfg.WeeklyCallTarget;
                        vm.MaxAttemptsPerContact = cfg.MaxAttemptsPerContact;
                        vm.CooldownDays = cfg.CooldownDays;
                        vm.PKDPriorityWeight = cfg.PKDPriorityWeight;
                        vm.PresetType = cfg.PresetType;
                        vm.OnlyMyImports = cfg.OnlyMyImports;

                        // Parse territory
                        if (!string.IsNullOrEmpty(cfg.TerritoryWojewodztwa))
                        {
                            try
                            {
                                var list = JsonSerializer.Deserialize<List<string>>(cfg.TerritoryWojewodztwa);
                                if (list != null)
                                    foreach (var w in list) vm.SelectedWojewodztwa.Add(w);
                            }
                            catch { }
                        }

                        // PKD priorities
                        if (pkdPriorities.TryGetValue(cfg.ConfigID, out var codes))
                        {
                            vm.PKDPriorityCodes = codes;
                        }
                    }

                    if (todayCalls.TryGetValue(op.id, out var tc))
                        vm.TodayCalls = tc;
                    if (weekCalls.TryGetValue(op.id, out var wc))
                        vm.WeekCalls = wc;

                    vm.LoadAvatar();
                    _handlowcy.Add(vm);
                }

                ApplySorting();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySorting()
        {
            if (_handlowcy == null) return;

            IOrderedEnumerable<HandlowiecConfigViewModel> sorted;
            switch (_currentSortMode)
            {
                case "name":
                    sorted = _handlowcy.OrderByDescending(h => h.IsEnabled).ThenBy(h => h.UserName);
                    break;
                case "today":
                    sorted = _handlowcy.OrderByDescending(h => h.IsEnabled).ThenByDescending(h => h.TodayCalls);
                    break;
                case "week":
                    sorted = _handlowcy.OrderByDescending(h => h.IsEnabled).ThenByDescending(h => h.WeekCalls);
                    break;
                default: // "activity"
                    sorted = _handlowcy.OrderByDescending(h => h.IsEnabled).ThenBy(h => h.UserName);
                    break;
            }

            var list = sorted.ToList();
            _handlowcy.Clear();
            foreach (var item in list) _handlowcy.Add(item);

            dgHandlowcy.ItemsSource = _handlowcy;
        }

        private void LoadStats()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Today calls count
                var cmdToday = new SqlCommand(
                    "SELECT ISNULL(SUM(ContactsCalled), 0) FROM CallReminderLog WHERE CAST(ReminderTime AS DATE) = CAST(GETDATE() AS DATE)", conn);
                txtStatsDzis.Text = cmdToday.ExecuteScalar()?.ToString() ?? "0";

                // Active agents
                var activeCount = _handlowcy?.Count(h => h.IsEnabled) ?? 0;
                var totalCount = _handlowcy?.Count ?? 0;
                txtStatsAgents.Text = $"{activeCount}/{totalCount}";

                // This week
                var cmdWeek = new SqlCommand(
                    "SELECT ISNULL(SUM(ContactsCalled), 0) FROM CallReminderLog WHERE ReminderTime >= DATEADD(DAY, -7, GETDATE())", conn);
                txtStatsTydzien.Text = cmdWeek.ExecuteScalar()?.ToString() ?? "0";

                // Top performer today
                var topToday = _handlowcy?.Where(h => h.IsEnabled).OrderByDescending(h => h.TodayCalls).FirstOrDefault();
                if (topToday != null && topToday.TodayCalls > 0)
                    txtTopPerformer.Text = $"TOP: {topToday.UserName} ({topToday.TodayCalls})";
                else
                    txtTopPerformer.Text = "TOP: --";
            }
            catch
            {
                // Stats are optional
            }
        }

        #endregion

        #region Save

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                foreach (var h in _handlowcy)
                {
                    string territoryJson = null;
                    if (h.SelectedWojewodztwa.Count > 0)
                        territoryJson = JsonSerializer.Serialize(h.SelectedWojewodztwa.ToList());

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
                                DailyCallTarget = @DailyTarget,
                                WeeklyCallTarget = @WeeklyTarget,
                                MaxAttemptsPerContact = @MaxAttempts,
                                CooldownDays = @Cooldown,
                                PKDPriorityWeight = @PKDWeight,
                                TerritoryWojewodztwa = @Territory,
                                PresetType = @Preset,
                                OnlyMyImports = @OnlyMyImports,
                                ModifiedAt = GETDATE()
                              WHERE UserID = @UserID", conn);
                        AddSaveParams(cmdUpdate, h, territoryJson);
                        cmdUpdate.ExecuteNonQuery();
                    }
                    else
                    {
                        var cmdInsert = new SqlCommand(
                            @"INSERT INTO CallReminderConfig (
                                UserID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder,
                                ShowOnlyNewContacts, ShowOnlyAssigned, DailyCallTarget, WeeklyCallTarget,
                                MaxAttemptsPerContact, CooldownDays, PKDPriorityWeight,
                                TerritoryWojewodztwa, PresetType, OnlyMyImports)
                              VALUES (
                                @UserID, @Enabled, @Time1, @Time2, @Count,
                                @OnlyNew, @OnlyAssigned, @DailyTarget, @WeeklyTarget,
                                @MaxAttempts, @Cooldown, @PKDWeight,
                                @Territory, @Preset, @OnlyMyImports)", conn);
                        AddSaveParams(cmdInsert, h, territoryJson);
                        cmdInsert.ExecuteNonQuery();
                    }
                }

                CallReminderService.Instance.ReloadConfig();
                MessageBox.Show("Zapisano zmiany!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSaveParams(SqlCommand cmd, HandlowiecConfigViewModel h, string territoryJson)
        {
            cmd.Parameters.AddWithValue("@UserID", h.UserID);
            cmd.Parameters.AddWithValue("@Enabled", h.IsEnabled);
            cmd.Parameters.AddWithValue("@Time1", h.ReminderTime1);
            cmd.Parameters.AddWithValue("@Time2", h.ReminderTime2);
            cmd.Parameters.AddWithValue("@Count", h.ContactsPerReminder);
            cmd.Parameters.AddWithValue("@OnlyNew", h.ShowOnlyNewContacts);
            cmd.Parameters.AddWithValue("@OnlyAssigned", h.ShowOnlyAssigned);
            cmd.Parameters.AddWithValue("@DailyTarget", h.DailyCallTarget);
            cmd.Parameters.AddWithValue("@WeeklyTarget", h.WeeklyCallTarget);
            cmd.Parameters.AddWithValue("@MaxAttempts", h.MaxAttemptsPerContact);
            cmd.Parameters.AddWithValue("@Cooldown", h.CooldownDays);
            cmd.Parameters.AddWithValue("@PKDWeight", h.PKDPriorityWeight);
            cmd.Parameters.AddWithValue("@Territory", (object)territoryJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Preset", (object)h.PresetType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OnlyMyImports", h.OnlyMyImports);
        }

        #endregion

        #region Button Handlers

        private void BtnEditHandlowiec_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HandlowiecConfigViewModel vm)
            {
                var editWindow = new EditHandlowiecWindow(_connectionString, vm);
                editWindow.Owner = this;
                if (editWindow.ShowDialog() == true)
                {
                    // Refresh data after edit
                    LoadData();
                    LoadStats();
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var importDialog = new ImportFirmDialog(_connectionString, "admin");
            importDialog.Owner = this;
            if (importDialog.ShowDialog() == true)
            {
                MessageBox.Show($"Zaimportowano {importDialog.ImportedCount} firm.",
                    "Import zakończony", MessageBoxButton.OK, MessageBoxImage.Information);
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
                DragMove();
        }

        private void TxtSearchHandlowiec_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_handlowcy == null) return;

            var searchText = txtSearchHandlowiec.Text?.Trim().ToLower() ?? "";
            bool onlyActive = chkOnlyActive.IsChecked == true;

            ApplyFilters(searchText, onlyActive);
        }

        private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSort?.SelectedIndex == null) return;
            switch (cmbSort.SelectedIndex)
            {
                case 0: _currentSortMode = "activity"; break;
                case 1: _currentSortMode = "name"; break;
                case 2: _currentSortMode = "today"; break;
                case 3: _currentSortMode = "week"; break;
            }
            ApplySorting();
        }

        private void ChkOnlyActive_Changed(object sender, RoutedEventArgs e)
        {
            if (_handlowcy == null) return;

            var searchText = txtSearchHandlowiec?.Text?.Trim().ToLower() ?? "";
            bool onlyActive = chkOnlyActive.IsChecked == true;

            ApplyFilters(searchText, onlyActive);
        }

        private void ApplyFilters(string searchText, bool onlyActive)
        {
            var filtered = _handlowcy.AsEnumerable();

            if (!string.IsNullOrEmpty(searchText))
                filtered = filtered.Where(h => h.UserName != null && h.UserName.ToLower().Contains(searchText));

            if (onlyActive)
                filtered = filtered.Where(h => h.IsEnabled);

            var list = filtered.ToList();
            dgHandlowcy.ItemsSource = list;
            txtSearchCount.Text = list.Count < _handlowcy.Count ? $"{list.Count} / {_handlowcy.Count}" : "";
        }

        #endregion
    }

    #region ConfigData helper

    internal class ConfigData
    {
        public bool Enabled { get; set; }
        public TimeSpan Time1 { get; set; }
        public TimeSpan Time2 { get; set; }
        public int Count { get; set; }
        public bool OnlyNew { get; set; }
        public bool OnlyAssigned { get; set; }
        public int ConfigID { get; set; }
        public int DailyCallTarget { get; set; }
        public int WeeklyCallTarget { get; set; }
        public int MaxAttemptsPerContact { get; set; }
        public int CooldownDays { get; set; }
        public int PKDPriorityWeight { get; set; }
        public string TerritoryWojewodztwa { get; set; }
        public string PresetType { get; set; }
        public bool OnlyMyImports { get; set; }
    }

    #endregion

    #region ViewModel

    public class HandlowiecConfigViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private TimeSpan _reminderTime1 = new TimeSpan(10, 0, 0);
        private TimeSpan _reminderTime2 = new TimeSpan(13, 0, 0);
        private int _contactsPerReminder = 5;
        private bool _showOnlyNewContacts = true;
        private bool _showOnlyAssigned = false;
        private bool _onlyMyImports = false;
        private int _todayCalls;
        private int _weekCalls;
        private BitmapSource _avatarSource;
        private bool _hasAvatar;
        private int _dailyCallTarget = 30;
        private int _weeklyCallTarget = 120;
        private int _maxAttemptsPerContact = 5;
        private int _cooldownDays = 3;
        private int _pkdPriorityWeight = 70;
        private string _presetType;
        private List<string> _pkdPriorityCodes = new List<string>();

        public string UserID { get; set; }
        public string UserName { get; set; }
        public int ConfigID { get; set; }
        public DateTime? LastCallTime { get; set; }

        public ObservableCollection<string> SelectedWojewodztwa { get; set; } = new ObservableCollection<string>();

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); OnPropertyChanged(nameof(StatusBadge)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(RowOpacity)); OnPropertyChanged(nameof(RoleName)); OnPropertyChanged(nameof(RowBorderColor)); }
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
            set { if (TimeSpan.TryParse(value, out var ts)) ReminderTime1 = ts; }
        }

        public string Time2String
        {
            get => $"{ReminderTime2.Hours:D2}:{ReminderTime2.Minutes:D2}";
            set { if (TimeSpan.TryParse(value, out var ts)) ReminderTime2 = ts; }
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

        public bool OnlyMyImports
        {
            get => _onlyMyImports;
            set { _onlyMyImports = value; OnPropertyChanged(nameof(OnlyMyImports)); }
        }

        public int TodayCalls
        {
            get => _todayCalls;
            set { _todayCalls = value; OnPropertyChanged(nameof(TodayCalls)); OnPropertyChanged(nameof(DailyProgressPct)); OnPropertyChanged(nameof(DailyProgressText)); OnPropertyChanged(nameof(DailyProgressColor)); }
        }

        public int WeekCalls
        {
            get => _weekCalls;
            set { _weekCalls = value; OnPropertyChanged(nameof(WeekCalls)); OnPropertyChanged(nameof(WeeklyProgressPct)); OnPropertyChanged(nameof(WeeklyProgressText)); OnPropertyChanged(nameof(WeeklyProgressColor)); }
        }

        // Goals
        public int DailyCallTarget
        {
            get => _dailyCallTarget;
            set { _dailyCallTarget = Math.Max(1, value); OnPropertyChanged(nameof(DailyCallTarget)); OnPropertyChanged(nameof(DailyProgressPct)); OnPropertyChanged(nameof(DailyProgressText)); OnPropertyChanged(nameof(DailyProgressColor)); }
        }

        public int WeeklyCallTarget
        {
            get => _weeklyCallTarget;
            set { _weeklyCallTarget = Math.Max(1, value); OnPropertyChanged(nameof(WeeklyCallTarget)); OnPropertyChanged(nameof(WeeklyProgressPct)); OnPropertyChanged(nameof(WeeklyProgressText)); OnPropertyChanged(nameof(WeeklyProgressColor)); }
        }

        public int MaxAttemptsPerContact
        {
            get => _maxAttemptsPerContact;
            set { _maxAttemptsPerContact = value; OnPropertyChanged(nameof(MaxAttemptsPerContact)); }
        }

        public int CooldownDays
        {
            get => _cooldownDays;
            set { _cooldownDays = value; OnPropertyChanged(nameof(CooldownDays)); }
        }

        public int PKDPriorityWeight
        {
            get => _pkdPriorityWeight;
            set { _pkdPriorityWeight = value; OnPropertyChanged(nameof(PKDPriorityWeight)); }
        }

        public string PresetType
        {
            get => _presetType;
            set { _presetType = value; OnPropertyChanged(nameof(PresetType)); }
        }

        public List<string> PKDPriorityCodes
        {
            get => _pkdPriorityCodes;
            set { _pkdPriorityCodes = value ?? new List<string>(); OnPropertyChanged(nameof(PKDPriorityCodes)); OnPropertyChanged(nameof(PKDSummary)); }
        }

        // Computed display properties
        public string WojewodztwaShort
        {
            get
            {
                if (SelectedWojewodztwa == null || SelectedWojewodztwa.Count == 0)
                    return "Cała Polska";
                if (SelectedWojewodztwa.Count <= 2)
                    return string.Join(", ", SelectedWojewodztwa.Select(w => w.Length > 3 ? w.Substring(0, 3) + "." : w));
                return $"{SelectedWojewodztwa.Count} woj.";
            }
        }

        public string PKDSummary
        {
            get
            {
                if (_pkdPriorityCodes == null || _pkdPriorityCodes.Count == 0)
                    return "Brak";
                if (_pkdPriorityCodes.Count <= 2)
                    return string.Join(", ", _pkdPriorityCodes);
                return $"{_pkdPriorityCodes.Count} kodów";
            }
        }

        // Progress computed properties
        public double DailyProgressPct => DailyCallTarget > 0 ? Math.Min(100.0, 100.0 * TodayCalls / DailyCallTarget) : 0;
        public string DailyProgressText => $"{TodayCalls}/{DailyCallTarget} ({(int)DailyProgressPct}%)";
        public string DailyProgressColor => DailyProgressPct >= 80 ? "#3fb950" : DailyProgressPct >= 50 ? "#f59e0b" : "#f85149";

        public double WeeklyProgressPct => WeeklyCallTarget > 0 ? Math.Min(100.0, 100.0 * WeekCalls / WeeklyCallTarget) : 0;
        public string WeeklyProgressText => $"{WeekCalls}/{WeeklyCallTarget} ({(int)WeeklyProgressPct}%)";
        public string WeeklyProgressColor => WeeklyProgressPct >= 80 ? "#3fb950" : WeeklyProgressPct >= 50 ? "#f59e0b" : "#f85149";

        // Status visual properties
        public string StatusBadge => IsEnabled ? "AKTYWNY" : "WYŁĄCZONY";
        public string StatusColor => IsEnabled ? "#3fb950" : "#f85149";
        public double RowOpacity => IsEnabled ? 1.0 : 0.5;
        public string RowBorderColor => IsEnabled ? "#3fb950" : "#f85149";
        public string RoleName => IsEnabled ? "Handlowiec" : "Nieaktywny";

        // Avatar support
        public BitmapSource AvatarSource
        {
            get => _avatarSource;
            set { _avatarSource = value; OnPropertyChanged(nameof(AvatarSource)); }
        }

        public bool HasAvatar
        {
            get => _hasAvatar;
            set { _hasAvatar = value; OnPropertyChanged(nameof(HasAvatar)); OnPropertyChanged(nameof(HasNoAvatar)); }
        }

        public bool HasNoAvatar => !_hasAvatar;

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UserName)) return "?";
                var parts = UserName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
                return UserName.Length >= 2 ? UserName.Substring(0, 2).ToUpper() : UserName.ToUpper();
            }
        }

        public System.Windows.Media.Color AvatarColor
        {
            get
            {
                int hash = UserID?.GetHashCode() ?? 0;
                var colors = new[]
                {
                    System.Windows.Media.Color.FromRgb(46, 125, 50),
                    System.Windows.Media.Color.FromRgb(25, 118, 210),
                    System.Windows.Media.Color.FromRgb(156, 39, 176),
                    System.Windows.Media.Color.FromRgb(230, 81, 0),
                    System.Windows.Media.Color.FromRgb(0, 137, 123),
                    System.Windows.Media.Color.FromRgb(194, 24, 91),
                    System.Windows.Media.Color.FromRgb(69, 90, 100),
                    System.Windows.Media.Color.FromRgb(121, 85, 72)
                };
                return colors[Math.Abs(hash) % colors.Length];
            }
        }

        public void LoadAvatar()
        {
            try
            {
                if (UserAvatarManager.HasAvatar(UserID))
                {
                    using (var avatar = UserAvatarManager.GetAvatarRounded(UserID, 42))
                    {
                        if (avatar != null)
                        {
                            AvatarSource = ConvertToBitmapSource(avatar);
                            HasAvatar = true;
                            return;
                        }
                    }
                }
            }
            catch { }
            HasAvatar = false;
        }

        private static BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using (var bitmap = new Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public List<string> AvailableTimes { get; } = Enumerable.Range(6, 18).SelectMany(h => new[] { $"{h:D2}:00", $"{h:D2}:30" }).ToList();
        public List<int> AvailableCounts { get; } = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion

    #region Converters

    public class ProgressToWidthHelper : IValueConverter
    {
        public static readonly ProgressToWidthHelper Instance = new ProgressToWidthHelper();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double pct)
                return Math.Max(0, Math.Min(80, 80.0 * pct / 100.0));
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseLengthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int length)
                return length == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double pct && values[1] is double maxWidth)
                return Math.Max(0, Math.Min(maxWidth, maxWidth * pct / 100.0));
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr); }
                catch { }
            }
            return System.Windows.Media.Colors.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                    return new SolidColorBrush(color);
                }
                catch { }
            }
            return System.Windows.Media.Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
