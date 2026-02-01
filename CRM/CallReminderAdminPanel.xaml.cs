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
            LoadDashboard();
        }

        #region Database Schema

        private void EnsureDatabaseSchema()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var schemaSql = @"
                -- Add new columns to CallReminderConfig if missing
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'PKDPriorityWeight')
                    ALTER TABLE CallReminderConfig ADD PKDPriorityWeight INT DEFAULT 70;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'UsePresetPKD')
                    ALTER TABLE CallReminderConfig ADD UsePresetPKD NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'SourcePriority')
                    ALTER TABLE CallReminderConfig ADD SourcePriority NVARCHAR(20) DEFAULT 'mixed';

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'ManualContactsPercent')
                    ALTER TABLE CallReminderConfig ADD ManualContactsPercent INT DEFAULT 50;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'ImportDateFrom')
                    ALTER TABLE CallReminderConfig ADD ImportDateFrom DATE NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'ImportDateTo')
                    ALTER TABLE CallReminderConfig ADD ImportDateTo DATE NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'PrioritizeRecentImports')
                    ALTER TABLE CallReminderConfig ADD PrioritizeRecentImports BIT DEFAULT 1;

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

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'TerritoryWojewodztwa')
                    ALTER TABLE CallReminderConfig ADD TerritoryWojewodztwa NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'TerritoryRadiusKm')
                    ALTER TABLE CallReminderConfig ADD TerritoryRadiusKm INT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'ExclusiveTerritory')
                    ALTER TABLE CallReminderConfig ADD ExclusiveTerritory BIT DEFAULT 0;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'SharePoolWithUsers')
                    ALTER TABLE CallReminderConfig ADD SharePoolWithUsers NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'UseAdvancedSchedule')
                    ALTER TABLE CallReminderConfig ADD UseAdvancedSchedule BIT DEFAULT 0;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'LunchBreakStart')
                    ALTER TABLE CallReminderConfig ADD LunchBreakStart TIME DEFAULT '12:00';

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'LunchBreakEnd')
                    ALTER TABLE CallReminderConfig ADD LunchBreakEnd TIME DEFAULT '13:00';

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'VacationStart')
                    ALTER TABLE CallReminderConfig ADD VacationStart DATE NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'VacationEnd')
                    ALTER TABLE CallReminderConfig ADD VacationEnd DATE NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'SubstituteUserID')
                    ALTER TABLE CallReminderConfig ADD SubstituteUserID NVARCHAR(50) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CallReminderConfig') AND name = 'PresetType')
                    ALTER TABLE CallReminderConfig ADD PresetType NVARCHAR(30) NULL;

                -- Create PKD Priority table
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CallReminderPKDPriority') AND type = 'U')
                BEGIN
                    CREATE TABLE CallReminderPKDPriority (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        ConfigID INT NOT NULL,
                        PKDCode NVARCHAR(10) NOT NULL,
                        PKDName NVARCHAR(255),
                        Priority INT DEFAULT 50,
                        IsExcluded BIT DEFAULT 0,
                        CreatedAt DATETIME DEFAULT GETDATE(),
                        CONSTRAINT FK_PKDPriority_Config FOREIGN KEY (ConfigID) REFERENCES CallReminderConfig(ID)
                    );
                    CREATE INDEX IX_PKDPriority_Config ON CallReminderPKDPriority(ConfigID);
                END

                -- Create TimeSlots table
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CallReminderTimeSlots') AND type = 'U')
                BEGIN
                    CREATE TABLE CallReminderTimeSlots (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        ConfigID INT NOT NULL,
                        DayOfWeek INT NULL,
                        TimeSlot TIME NOT NULL,
                        IsActive BIT DEFAULT 1,
                        CONSTRAINT FK_TimeSlots_Config FOREIGN KEY (ConfigID) REFERENCES CallReminderConfig(ID)
                    );
                END

                -- Create Lead Transfers table
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CallReminderLeadTransfers') AND type = 'U')
                BEGIN
                    CREATE TABLE CallReminderLeadTransfers (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        FromUserID NVARCHAR(50) NOT NULL,
                        ToUserID NVARCHAR(50) NOT NULL,
                        ContactsCount INT NOT NULL,
                        FilterCriteria NVARCHAR(MAX),
                        TransferredAt DATETIME DEFAULT GETDATE(),
                        TransferredBy NVARCHAR(50) NOT NULL,
                        Reason NVARCHAR(500)
                    );
                END

                -- Create Audit table
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

                // Get existing configs with all new columns
                var configs = new Dictionary<string, ConfigData>();
                var cmdConfigs = new SqlCommand(
                    @"SELECT UserID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder,
                             ShowOnlyNewContacts, ShowOnlyAssigned, ID,
                             ISNULL(DailyCallTarget, 30), ISNULL(WeeklyCallTarget, 120),
                             ISNULL(MaxAttemptsPerContact, 5), ISNULL(CooldownDays, 3),
                             ISNULL(MinCallDurationSec, 30), ISNULL(AlertBelowPercent, 50),
                             ISNULL(PKDPriorityWeight, 70), UsePresetPKD,
                             ISNULL(SourcePriority, 'mixed'), ISNULL(ManualContactsPercent, 50),
                             ImportDateFrom, ImportDateTo, ISNULL(PrioritizeRecentImports, 1),
                             TerritoryWojewodztwa, TerritoryRadiusKm, ISNULL(ExclusiveTerritory, 0),
                             ISNULL(UseAdvancedSchedule, 0), LunchBreakStart, LunchBreakEnd,
                             VacationStart, VacationEnd, SubstituteUserID, PresetType
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
                            MinCallDurationSec = reader.GetInt32(12),
                            AlertBelowPercent = reader.GetInt32(13),
                            PKDPriorityWeight = reader.GetInt32(14),
                            UsePresetPKD = reader.IsDBNull(15) ? null : reader.GetString(15),
                            SourcePriority = reader.GetString(16),
                            ManualContactsPercent = reader.GetInt32(17),
                            ImportDateFrom = reader.IsDBNull(18) ? (DateTime?)null : reader.GetDateTime(18),
                            ImportDateTo = reader.IsDBNull(19) ? (DateTime?)null : reader.GetDateTime(19),
                            PrioritizeRecentImports = reader.GetBoolean(20),
                            TerritoryWojewodztwa = reader.IsDBNull(21) ? null : reader.GetString(21),
                            TerritoryRadiusKm = reader.IsDBNull(22) ? (int?)null : reader.GetInt32(22),
                            ExclusiveTerritory = reader.GetBoolean(23),
                            UseAdvancedSchedule = reader.GetBoolean(24),
                            LunchBreakStart = reader.IsDBNull(25) ? new TimeSpan(12, 0, 0) : reader.GetTimeSpan(25),
                            LunchBreakEnd = reader.IsDBNull(26) ? new TimeSpan(13, 0, 0) : reader.GetTimeSpan(26),
                            VacationStart = reader.IsDBNull(27) ? (DateTime?)null : reader.GetDateTime(27),
                            VacationEnd = reader.IsDBNull(28) ? (DateTime?)null : reader.GetDateTime(28),
                            SubstituteUserID = reader.IsDBNull(29) ? null : reader.GetString(29),
                            PresetType = reader.IsDBNull(30) ? null : reader.GetString(30)
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

                // Get last call time per user (for alerts)
                var lastCallTime = new Dictionary<string, DateTime>();
                try
                {
                    var cmdLastCall = new SqlCommand(
                        @"SELECT UserID, MAX(ReminderTime) FROM CallReminderLog
                          WHERE CAST(ReminderTime AS DATE) = CAST(GETDATE() AS DATE)
                          GROUP BY UserID", conn);
                    using (var reader = cmdLastCall.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(1))
                                lastCallTime[reader.GetString(0)] = reader.GetDateTime(1);
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
                        vm.MinCallDurationSec = cfg.MinCallDurationSec;
                        vm.AlertBelowPercent = cfg.AlertBelowPercent;
                        vm.PKDPriorityWeight = cfg.PKDPriorityWeight;
                        vm.UsePresetPKD = cfg.UsePresetPKD;
                        vm.SourcePriority = cfg.SourcePriority;
                        vm.ManualContactsPercent = cfg.ManualContactsPercent;
                        vm.ImportDateFrom = cfg.ImportDateFrom;
                        vm.ImportDateTo = cfg.ImportDateTo;
                        vm.PrioritizeRecentImports = cfg.PrioritizeRecentImports;
                        vm.ExclusiveTerritory = cfg.ExclusiveTerritory;
                        vm.UseAdvancedSchedule = cfg.UseAdvancedSchedule;
                        vm.LunchBreakStart = cfg.LunchBreakStart;
                        vm.LunchBreakEnd = cfg.LunchBreakEnd;
                        vm.VacationStart = cfg.VacationStart;
                        vm.VacationEnd = cfg.VacationEnd;
                        vm.SubstituteUserID = cfg.SubstituteUserID;
                        vm.PresetType = cfg.PresetType;
                        vm.TerritoryRadiusKm = cfg.TerritoryRadiusKm;

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
                    }

                    if (todayCalls.TryGetValue(op.id, out var tc))
                        vm.TodayCalls = tc;
                    if (weekCalls.TryGetValue(op.id, out var wc))
                        vm.WeekCalls = wc;
                    if (lastCallTime.TryGetValue(op.id, out var lct))
                        vm.LastCallTime = lct;

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
                txtStatsTelefony.Text = activeCount.ToString();
                txtStatsAgentsPercent.Text = totalCount > 0 ? $"{activeCount}/{totalCount}" : "0";

                // This week
                var cmdWeek = new SqlCommand(
                    "SELECT ISNULL(SUM(ContactsCalled), 0) FROM CallReminderLog WHERE ReminderTime >= DATEADD(DAY, -7, GETDATE())", conn);
                txtStatsTydzien.Text = cmdWeek.ExecuteScalar()?.ToString() ?? "0";

                // This month
                var cmdMonth = new SqlCommand(
                    "SELECT ISNULL(SUM(ContactsCalled), 0) FROM CallReminderLog WHERE ReminderTime >= DATEADD(DAY, -30, GETDATE())", conn);
                txtStatsMiesiac.Text = cmdMonth.ExecuteScalar()?.ToString() ?? "0";

                // Week daily bars
                LoadWeekBars(conn);
            }
            catch
            {
                // Stats are optional
            }
        }

        private void LoadWeekBars(SqlConnection conn)
        {
            try
            {
                var cmdDays = new SqlCommand(
                    @"SELECT DATEPART(WEEKDAY, ReminderTime) as DW, SUM(ContactsCalled) as Total
                      FROM CallReminderLog
                      WHERE ReminderTime >= DATEADD(DAY, -(DATEPART(WEEKDAY, GETDATE())-1), CAST(GETDATE() AS DATE))
                        AND ReminderTime < DATEADD(DAY, 7-(DATEPART(WEEKDAY, GETDATE())-1), CAST(GETDATE() AS DATE))
                      GROUP BY DATEPART(WEEKDAY, ReminderTime)", conn);

                var dayTotals = new Dictionary<int, int>();
                using (var reader = cmdDays.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dayTotals[reader.GetInt32(0)] = reader.GetInt32(1);
                    }
                }

                int maxVal = dayTotals.Values.DefaultIfEmpty(1).Max();
                if (maxVal == 0) maxVal = 1;

                // Monday=2, Tuesday=3, etc. in SQL DATEPART(WEEKDAY) with default settings
                txtBarMonVal.Text = dayTotals.GetValueOrDefault(2, 0).ToString();
                txtBarTueVal.Text = dayTotals.GetValueOrDefault(3, 0).ToString();
                txtBarWedVal.Text = dayTotals.GetValueOrDefault(4, 0).ToString();
                txtBarThuVal.Text = dayTotals.GetValueOrDefault(5, 0).ToString();
                txtBarFriVal.Text = dayTotals.GetValueOrDefault(6, 0).ToString();

                int total = dayTotals.Values.Sum();
                txtWeekTotal.Text = total.ToString();
            }
            catch { }
        }

        private void LoadDashboard()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Dashboard KPIs
                var cmdNotes = new SqlCommand(
                    "SELECT ISNULL(SUM(NotesAdded), 0) FROM CallReminderLog WHERE CAST(ReminderTime AS DATE) = CAST(GETDATE() AS DATE)", conn);
                int notesToday = (int)cmdNotes.ExecuteScalar();
                txtDashNotes.Text = notesToday.ToString();

                var cmdConversions = new SqlCommand(
                    "SELECT ISNULL(SUM(StatusChanges), 0) FROM CallReminderLog WHERE CAST(ReminderTime AS DATE) = CAST(GETDATE() AS DATE)", conn);
                int conversionsToday = (int)cmdConversions.ExecuteScalar();
                txtDashConversions.Text = conversionsToday.ToString();

                // Average call duration - placeholder since we don't track duration yet
                txtDashAvgDuration.Text = "--:--";

                // Team goal percentage
                if (_handlowcy != null && _handlowcy.Any(h => h.IsEnabled))
                {
                    var activeHandlowcy = _handlowcy.Where(h => h.IsEnabled).ToList();
                    int totalTarget = activeHandlowcy.Sum(h => h.DailyCallTarget);
                    int totalCalls = activeHandlowcy.Sum(h => h.TodayCalls);
                    int pct = totalTarget > 0 ? (int)Math.Round(100.0 * totalCalls / totalTarget) : 0;
                    txtDashGoalPct.Text = $"{pct}%";
                    dashGoalProgress.Value = Math.Min(pct, 100);
                }

                // Top performer today
                var topToday = _handlowcy?.Where(h => h.IsEnabled).OrderByDescending(h => h.TodayCalls).FirstOrDefault();
                if (topToday != null && topToday.TodayCalls > 0)
                    txtTopPerformer.Text = $"TOP DZIŚ: {topToday.UserName} ({topToday.TodayCalls})";
                else
                    txtTopPerformer.Text = "TOP DZIŚ: --";

                // Alerts
                LoadAlerts();
            }
            catch { }
        }

        private void LoadAlerts()
        {
            var alerts = new List<string>();
            if (_handlowcy != null)
            {
                var now = DateTime.Now;
                foreach (var h in _handlowcy.Where(x => x.IsEnabled))
                {
                    // No calls today
                    if (h.TodayCalls == 0 && now.Hour >= 10)
                    {
                        alerts.Add($"  {h.UserName} - 0 telefonów dziś");
                    }
                    // No calls in 2+ hours
                    else if (h.LastCallTime.HasValue && (now - h.LastCallTime.Value).TotalHours >= 2 && now.Hour < 17)
                    {
                        alerts.Add($"  {h.UserName} - brak aktywności od {h.LastCallTime.Value:HH:mm}");
                    }
                    // Below target percentage
                    if (h.DailyCallTarget > 0)
                    {
                        double pct = 100.0 * h.TodayCalls / h.DailyCallTarget;
                        double expectedPct = Math.Min(100, 100.0 * (now.Hour - 8) / 9.0); // 8:00-17:00 work
                        if (pct < expectedPct * 0.5 && now.Hour >= 11)
                        {
                            alerts.Add($"  {h.UserName} - poniżej celu ({h.TodayCalls}/{h.DailyCallTarget})");
                        }
                    }
                }
            }

            if (alerts.Count > 0)
            {
                txtAlerts.Text = string.Join("\n", alerts.Take(5));
                alertsPanel.Visibility = Visibility.Visible;
                txtAlertsCount.Text = alerts.Count.ToString();
            }
            else
            {
                txtAlerts.Text = "Brak alertów";
                alertsPanel.Visibility = Visibility.Visible;
                txtAlertsCount.Text = "0";
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
                    // Serialize territory
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
                                MinCallDurationSec = @MinDuration,
                                AlertBelowPercent = @AlertBelow,
                                PKDPriorityWeight = @PKDWeight,
                                UsePresetPKD = @PresetPKD,
                                SourcePriority = @SourcePrio,
                                ManualContactsPercent = @ManualPct,
                                ImportDateFrom = @ImportFrom,
                                ImportDateTo = @ImportTo,
                                PrioritizeRecentImports = @PrioImports,
                                TerritoryWojewodztwa = @Territory,
                                TerritoryRadiusKm = @Radius,
                                ExclusiveTerritory = @Exclusive,
                                UseAdvancedSchedule = @AdvSchedule,
                                LunchBreakStart = @LunchStart,
                                LunchBreakEnd = @LunchEnd,
                                VacationStart = @VacStart,
                                VacationEnd = @VacEnd,
                                SubstituteUserID = @Substitute,
                                PresetType = @Preset,
                                ModifiedAt = GETDATE()
                              WHERE UserID = @UserID", conn);
                        AddAllParams(cmdUpdate, h, territoryJson);
                        cmdUpdate.ExecuteNonQuery();
                    }
                    else
                    {
                        var cmdInsert = new SqlCommand(
                            @"INSERT INTO CallReminderConfig (
                                UserID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder,
                                ShowOnlyNewContacts, ShowOnlyAssigned, DailyCallTarget, WeeklyCallTarget,
                                MaxAttemptsPerContact, CooldownDays, MinCallDurationSec, AlertBelowPercent,
                                PKDPriorityWeight, UsePresetPKD, SourcePriority, ManualContactsPercent,
                                ImportDateFrom, ImportDateTo, PrioritizeRecentImports,
                                TerritoryWojewodztwa, TerritoryRadiusKm, ExclusiveTerritory,
                                UseAdvancedSchedule, LunchBreakStart, LunchBreakEnd,
                                VacationStart, VacationEnd, SubstituteUserID, PresetType)
                              VALUES (
                                @UserID, @Enabled, @Time1, @Time2, @Count,
                                @OnlyNew, @OnlyAssigned, @DailyTarget, @WeeklyTarget,
                                @MaxAttempts, @Cooldown, @MinDuration, @AlertBelow,
                                @PKDWeight, @PresetPKD, @SourcePrio, @ManualPct,
                                @ImportFrom, @ImportTo, @PrioImports,
                                @Territory, @Radius, @Exclusive,
                                @AdvSchedule, @LunchStart, @LunchEnd,
                                @VacStart, @VacEnd, @Substitute, @Preset)", conn);
                        AddAllParams(cmdInsert, h, territoryJson);
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

        private void AddAllParams(SqlCommand cmd, HandlowiecConfigViewModel h, string territoryJson)
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
            cmd.Parameters.AddWithValue("@MinDuration", h.MinCallDurationSec);
            cmd.Parameters.AddWithValue("@AlertBelow", h.AlertBelowPercent);
            cmd.Parameters.AddWithValue("@PKDWeight", h.PKDPriorityWeight);
            cmd.Parameters.AddWithValue("@PresetPKD", (object)h.UsePresetPKD ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SourcePrio", h.SourcePriority ?? "mixed");
            cmd.Parameters.AddWithValue("@ManualPct", h.ManualContactsPercent);
            cmd.Parameters.AddWithValue("@ImportFrom", (object)h.ImportDateFrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ImportTo", (object)h.ImportDateTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrioImports", h.PrioritizeRecentImports);
            cmd.Parameters.AddWithValue("@Territory", (object)territoryJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Radius", (object)h.TerritoryRadiusKm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Exclusive", h.ExclusiveTerritory);
            cmd.Parameters.AddWithValue("@AdvSchedule", h.UseAdvancedSchedule);
            cmd.Parameters.AddWithValue("@LunchStart", h.LunchBreakStart);
            cmd.Parameters.AddWithValue("@LunchEnd", h.LunchBreakEnd);
            cmd.Parameters.AddWithValue("@VacStart", (object)h.VacationStart ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VacEnd", (object)h.VacationEnd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Substitute", (object)h.SubstituteUserID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Preset", (object)h.PresetType ?? DBNull.Value);
        }

        #endregion

        #region Button Handlers

        private void BtnApplyToAll_Click(object sender, RoutedEventArgs e)
        {
            if (_handlowcy == null || _handlowcy.Count == 0) return;

            var result = MessageBox.Show(
                "Czy chcesz zastosować ustawienia pierwszego handlowca do wszystkich?\n\n" +
                $"Godzina 1: {_handlowcy[0].Time1String}\n" +
                $"Godzina 2: {_handlowcy[0].Time2String}\n" +
                $"Kontaktów: {_handlowcy[0].ContactsPerReminder}\n" +
                $"Cel dzienny: {_handlowcy[0].DailyCallTarget}\n" +
                $"Cel tygodniowy: {_handlowcy[0].WeeklyCallTarget}",
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
                    h.DailyCallTarget = template.DailyCallTarget;
                    h.WeeklyCallTarget = template.WeeklyCallTarget;
                    h.MaxAttemptsPerContact = template.MaxAttemptsPerContact;
                    h.CooldownDays = template.CooldownDays;
                }
                dgHandlowcy.Items.Refresh();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            LoadStats();
            LoadDashboard();
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

            if (string.IsNullOrEmpty(searchText))
            {
                dgHandlowcy.ItemsSource = _handlowcy;
                txtSearchCount.Text = "";
            }
            else
            {
                var filtered = _handlowcy.Where(h =>
                    h.UserName != null && h.UserName.ToLower().Contains(searchText)).ToList();
                dgHandlowcy.ItemsSource = filtered;
                txtSearchCount.Text = $"{filtered.Count} / {_handlowcy.Count}";
            }
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

        #endregion

        #region Row Detail Handlers

        private void CmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cmb && cmb.DataContext is HandlowiecConfigViewModel vm)
            {
                var selected = cmb.SelectedItem as ComboBoxItem;
                var tag = selected?.Tag?.ToString();
                if (string.IsNullOrEmpty(tag) || tag == "custom") return;

                if (ConfigPresets.Presets.TryGetValue(tag, out var preset))
                {
                    vm.PresetType = tag;
                    vm.DailyCallTarget = preset.DailyTarget;
                    vm.WeeklyCallTarget = preset.WeeklyTarget;
                    vm.ContactsPerReminder = preset.ContactsPerReminder;
                    vm.ShowOnlyAssigned = preset.ShowOnlyAssigned;
                    vm.ShowOnlyNewContacts = preset.ShowOnlyNewContacts;
                    vm.PKDPriorityWeight = preset.PKDPriorityWeight;
                    vm.SourcePriority = preset.SourcePriority;
                    vm.MaxAttemptsPerContact = preset.MaxAttemptsPerContact;
                    vm.CooldownDays = preset.CooldownDays;
                }
            }
        }

        private void CmbPKDPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cmb && cmb.DataContext is HandlowiecConfigViewModel vm)
            {
                var selected = cmb.SelectedItem as ComboBoxItem;
                var tag = selected?.Tag?.ToString();
                vm.UsePresetPKD = tag;
            }
        }

        private void CmbSourcePriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cmb && cmb.DataContext is HandlowiecConfigViewModel vm)
            {
                var selected = cmb.SelectedItem as ComboBoxItem;
                vm.SourcePriority = selected?.Tag?.ToString() ?? "mixed";
            }
        }

        private void BtnShowHistory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HandlowiecConfigViewModel vm)
            {
                ShowHistoryDialog(vm);
            }
        }

        private void BtnTransferLeads_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HandlowiecConfigViewModel vm)
            {
                ShowTransferDialog(vm);
            }
        }

        private void WojewodztwoCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Handled via binding
        }

        #endregion

        #region Dialogs

        private void ShowHistoryDialog(HandlowiecConfigViewModel vm)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    @"SELECT TOP 50 FieldName, OldValue, NewValue, ChangedBy, ChangedAt, ChangeType
                      FROM CallReminderConfigAudit
                      WHERE ConfigID = @ConfigID
                      ORDER BY ChangedAt DESC", conn);
                cmd.Parameters.AddWithValue("@ConfigID", vm.ConfigID);

                var entries = new List<string>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var field = reader.GetString(0);
                        var oldVal = reader.IsDBNull(1) ? "-" : reader.GetString(1);
                        var newVal = reader.IsDBNull(2) ? "-" : reader.GetString(2);
                        var by = reader.GetString(3);
                        var at = reader.GetDateTime(4);
                        entries.Add($"[{at:yyyy-MM-dd HH:mm}] {field}: {oldVal} -> {newVal} (przez {by})");
                    }
                }

                var text = entries.Count > 0 ? string.Join("\n", entries) : "Brak historii zmian.";
                MessageBox.Show(text, $"Historia zmian - {vm.UserName}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowTransferDialog(HandlowiecConfigViewModel vm)
        {
            var otherUsers = _handlowcy.Where(h => h.UserID != vm.UserID && h.IsEnabled).ToList();
            if (otherUsers.Count == 0)
            {
                MessageBox.Show("Brak aktywnych handlowców do przekazania.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new Window
            {
                Title = $"Przekaż leady od: {vm.UserName}",
                Width = 450,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 17, 23)),
                Foreground = System.Windows.Media.Brushes.White
            };

            var sp = new StackPanel { Margin = new Thickness(20) };

            var lblTo = new TextBlock { Text = "Przekaż do:", Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 5) };
            sp.Children.Add(lblTo);

            var cmbTo = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var u in otherUsers)
                cmbTo.Items.Add(new ComboBoxItem { Content = u.UserName, Tag = u.UserID });
            if (cmbTo.Items.Count > 0) cmbTo.SelectedIndex = 0;
            sp.Children.Add(cmbTo);

            var lblReason = new TextBlock { Text = "Powód:", Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 5) };
            sp.Children.Add(lblReason);

            var txtReason = new TextBox { Height = 60, TextWrapping = TextWrapping.Wrap, Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 27, 34)), Foreground = System.Windows.Media.Brushes.White, BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 54, 61)) };
            sp.Children.Add(txtReason);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };

            var btnCancel = new Button { Content = "Anuluj", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 10, 0) };
            btnCancel.Click += (s, ev) => window.Close();
            btnPanel.Children.Add(btnCancel);

            var btnTransfer = new Button { Content = "Przekaż", Padding = new Thickness(20, 8, 20, 8), Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 185, 80)), Foreground = System.Windows.Media.Brushes.White };
            btnTransfer.Click += (s, ev) =>
            {
                var toItem = cmbTo.SelectedItem as ComboBoxItem;
                if (toItem == null) return;

                try
                {
                    using var connT = new SqlConnection(_connectionString);
                    connT.Open();
                    var cmdT = new SqlCommand(
                        @"INSERT INTO CallReminderLeadTransfers (FromUserID, ToUserID, ContactsCount, Reason, TransferredBy)
                          VALUES (@From, @To, 0, @Reason, @By)", connT);
                    cmdT.Parameters.AddWithValue("@From", vm.UserID);
                    cmdT.Parameters.AddWithValue("@To", toItem.Tag.ToString());
                    cmdT.Parameters.AddWithValue("@Reason", txtReason.Text ?? "");
                    cmdT.Parameters.AddWithValue("@By", "admin");
                    cmdT.ExecuteNonQuery();
                    MessageBox.Show("Przekazanie zarejestrowane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    window.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            btnPanel.Children.Add(btnTransfer);
            sp.Children.Add(btnPanel);

            window.Content = sp;
            window.ShowDialog();
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
        public int MinCallDurationSec { get; set; }
        public int AlertBelowPercent { get; set; }
        public int PKDPriorityWeight { get; set; }
        public string UsePresetPKD { get; set; }
        public string SourcePriority { get; set; }
        public int ManualContactsPercent { get; set; }
        public DateTime? ImportDateFrom { get; set; }
        public DateTime? ImportDateTo { get; set; }
        public bool PrioritizeRecentImports { get; set; }
        public string TerritoryWojewodztwa { get; set; }
        public int? TerritoryRadiusKm { get; set; }
        public bool ExclusiveTerritory { get; set; }
        public bool UseAdvancedSchedule { get; set; }
        public TimeSpan LunchBreakStart { get; set; }
        public TimeSpan LunchBreakEnd { get; set; }
        public DateTime? VacationStart { get; set; }
        public DateTime? VacationEnd { get; set; }
        public string SubstituteUserID { get; set; }
        public string PresetType { get; set; }
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
        private int _todayCalls;
        private int _weekCalls;
        private BitmapSource _avatarSource;
        private bool _hasAvatar;
        private int _dailyCallTarget = 30;
        private int _weeklyCallTarget = 120;
        private int _maxAttemptsPerContact = 5;
        private int _cooldownDays = 3;
        private int _minCallDurationSec = 30;
        private int _alertBelowPercent = 50;
        private int _pkdPriorityWeight = 70;
        private string _usePresetPKD;
        private string _sourcePriority = "mixed";
        private int _manualContactsPercent = 50;
        private DateTime? _importDateFrom;
        private DateTime? _importDateTo;
        private bool _prioritizeRecentImports = true;
        private bool _exclusiveTerritory;
        private bool _useAdvancedSchedule;
        private TimeSpan _lunchBreakStart = new TimeSpan(12, 0, 0);
        private TimeSpan _lunchBreakEnd = new TimeSpan(13, 0, 0);
        private DateTime? _vacationStart;
        private DateTime? _vacationEnd;
        private string _substituteUserID;
        private string _presetType;
        private int? _territoryRadiusKm;

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

        public int MinCallDurationSec
        {
            get => _minCallDurationSec;
            set { _minCallDurationSec = value; OnPropertyChanged(nameof(MinCallDurationSec)); }
        }

        public int AlertBelowPercent
        {
            get => _alertBelowPercent;
            set { _alertBelowPercent = value; OnPropertyChanged(nameof(AlertBelowPercent)); }
        }

        // PKD
        public int PKDPriorityWeight
        {
            get => _pkdPriorityWeight;
            set { _pkdPriorityWeight = value; OnPropertyChanged(nameof(PKDPriorityWeight)); }
        }

        public string UsePresetPKD
        {
            get => _usePresetPKD;
            set { _usePresetPKD = value; OnPropertyChanged(nameof(UsePresetPKD)); }
        }

        // Source
        public string SourcePriority
        {
            get => _sourcePriority;
            set { _sourcePriority = value; OnPropertyChanged(nameof(SourcePriority)); OnPropertyChanged(nameof(IsSourceMixed)); }
        }

        public int ManualContactsPercent
        {
            get => _manualContactsPercent;
            set { _manualContactsPercent = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(nameof(ManualContactsPercent)); }
        }

        public DateTime? ImportDateFrom
        {
            get => _importDateFrom;
            set { _importDateFrom = value; OnPropertyChanged(nameof(ImportDateFrom)); }
        }

        public DateTime? ImportDateTo
        {
            get => _importDateTo;
            set { _importDateTo = value; OnPropertyChanged(nameof(ImportDateTo)); }
        }

        public bool PrioritizeRecentImports
        {
            get => _prioritizeRecentImports;
            set { _prioritizeRecentImports = value; OnPropertyChanged(nameof(PrioritizeRecentImports)); }
        }

        public bool IsSourceMixed => SourcePriority == "mixed";

        // Territory
        public bool ExclusiveTerritory
        {
            get => _exclusiveTerritory;
            set { _exclusiveTerritory = value; OnPropertyChanged(nameof(ExclusiveTerritory)); }
        }

        public int? TerritoryRadiusKm
        {
            get => _territoryRadiusKm;
            set { _territoryRadiusKm = value; OnPropertyChanged(nameof(TerritoryRadiusKm)); }
        }

        // Schedule
        public bool UseAdvancedSchedule
        {
            get => _useAdvancedSchedule;
            set { _useAdvancedSchedule = value; OnPropertyChanged(nameof(UseAdvancedSchedule)); }
        }

        public TimeSpan LunchBreakStart
        {
            get => _lunchBreakStart;
            set { _lunchBreakStart = value; OnPropertyChanged(nameof(LunchBreakStart)); OnPropertyChanged(nameof(LunchStartString)); }
        }

        public TimeSpan LunchBreakEnd
        {
            get => _lunchBreakEnd;
            set { _lunchBreakEnd = value; OnPropertyChanged(nameof(LunchBreakEnd)); OnPropertyChanged(nameof(LunchEndString)); }
        }

        public string LunchStartString
        {
            get => $"{LunchBreakStart.Hours:D2}:{LunchBreakStart.Minutes:D2}";
            set { if (TimeSpan.TryParse(value, out var ts)) LunchBreakStart = ts; }
        }

        public string LunchEndString
        {
            get => $"{LunchBreakEnd.Hours:D2}:{LunchBreakEnd.Minutes:D2}";
            set { if (TimeSpan.TryParse(value, out var ts)) LunchBreakEnd = ts; }
        }

        public DateTime? VacationStart
        {
            get => _vacationStart;
            set { _vacationStart = value; OnPropertyChanged(nameof(VacationStart)); }
        }

        public DateTime? VacationEnd
        {
            get => _vacationEnd;
            set { _vacationEnd = value; OnPropertyChanged(nameof(VacationEnd)); }
        }

        public string SubstituteUserID
        {
            get => _substituteUserID;
            set { _substituteUserID = value; OnPropertyChanged(nameof(SubstituteUserID)); }
        }

        public string PresetType
        {
            get => _presetType;
            set { _presetType = value; OnPropertyChanged(nameof(PresetType)); }
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

        private static BitmapSource ConvertToBitmapSource(Image image)
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
