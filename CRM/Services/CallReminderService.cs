using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Windows;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;

namespace Kalendarz1.CRM.Services
{
    public class CallReminderService
    {
        private static CallReminderService _instance;
        private static readonly object _lock = new object();

        private System.Windows.Forms.Timer _checkTimer;
        private string _userID;
        private string _connectionString;
        private DateTime? _lastReminderTime;
        private CallReminderConfig _config;
        private bool _isInitialized = false;

        public static CallReminderService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new CallReminderService();
                    }
                }
                return _instance;
            }
        }

        private CallReminderService()
        {
            _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        public bool IsInitialized => _isInitialized;

        public void Initialize(string userID)
        {
            if (_isInitialized) return;

            _userID = userID;
            LoadConfig();
            StartTimer();
            _isInitialized = true;

            Debug.WriteLine($"[CallReminderService] Initialized for user {userID}");
        }

        public void Stop()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _checkTimer = null;
            _isInitialized = false;
        }

        private void LoadConfig()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Try to get existing config
                var cmdGet = new SqlCommand(
                    @"SELECT ID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder,
                      ShowOnlyNewContacts, ShowOnlyAssigned, MinutesTolerance,
                      ISNULL(DailyCallTarget, 30), ISNULL(WeeklyCallTarget, 120),
                      ISNULL(MaxAttemptsPerContact, 5), ISNULL(CooldownDays, 3),
                      ISNULL(MinCallDurationSec, 30), ISNULL(PKDPriorityWeight, 70),
                      ISNULL(SourcePriority, 'mixed'), ISNULL(ManualContactsPercent, 50),
                      TerritoryWojewodztwa, ISNULL(UseAdvancedSchedule, 0),
                      LunchBreakStart, LunchBreakEnd,
                      VacationStart, VacationEnd, SubstituteUserID,
                      ISNULL(OnlyMyImports, 0)
                      FROM CallReminderConfig WHERE UserID = @UserID", conn);
                cmdGet.Parameters.AddWithValue("@UserID", _userID);

                using var reader = cmdGet.ExecuteReader();
                if (reader.Read())
                {
                    _config = new CallReminderConfig
                    {
                        ID = reader.GetInt32(0),
                        UserID = _userID,
                        IsEnabled = reader.GetBoolean(1),
                        ReminderTime1 = reader.GetTimeSpan(2),
                        ReminderTime2 = reader.GetTimeSpan(3),
                        ContactsPerReminder = reader.GetInt32(4),
                        ShowOnlyNewContacts = reader.GetBoolean(5),
                        ShowOnlyAssigned = reader.GetBoolean(6),
                        MinutesTolerance = reader.GetInt32(7),
                        DailyCallTarget = reader.GetInt32(8),
                        WeeklyCallTarget = reader.GetInt32(9),
                        MaxAttemptsPerContact = reader.GetInt32(10),
                        CooldownDays = reader.GetInt32(11),
                        MinCallDurationSec = reader.GetInt32(12),
                        PKDPriorityWeight = reader.GetInt32(13),
                        SourcePriority = reader.GetString(14),
                        ManualContactsPercent = reader.GetInt32(15),
                        TerritoryWojewodztwa = reader.IsDBNull(16) ? null : reader.GetString(16),
                        UseAdvancedSchedule = reader.GetBoolean(17),
                        LunchBreakStart = reader.IsDBNull(18) ? new TimeSpan(12, 0, 0) : reader.GetTimeSpan(18),
                        LunchBreakEnd = reader.IsDBNull(19) ? new TimeSpan(13, 0, 0) : reader.GetTimeSpan(19),
                        VacationStart = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
                        VacationEnd = reader.IsDBNull(21) ? null : reader.GetDateTime(21),
                        SubstituteUserID = reader.IsDBNull(22) ? null : reader.GetString(22),
                        OnlyMyImports = reader.GetBoolean(23)
                    };
                }
                else
                {
                    reader.Close();
                    // Create default config
                    _config = new CallReminderConfig
                    {
                        UserID = _userID,
                        IsEnabled = true,
                        ReminderTime1 = new TimeSpan(10, 0, 0),
                        ReminderTime2 = new TimeSpan(13, 0, 0),
                        ContactsPerReminder = 5,
                        ShowOnlyNewContacts = true,
                        ShowOnlyAssigned = false,
                        MinutesTolerance = 15
                    };

                    var cmdInsert = new SqlCommand(
                        "INSERT INTO CallReminderConfig (UserID, IsEnabled, ReminderTime1, ReminderTime2, " +
                        "ContactsPerReminder, ShowOnlyNewContacts, ShowOnlyAssigned, MinutesTolerance) " +
                        "VALUES (@UserID, @IsEnabled, @Time1, @Time2, @Count, @OnlyNew, @OnlyAssigned, @Tolerance)", conn);
                    cmdInsert.Parameters.AddWithValue("@UserID", _userID);
                    cmdInsert.Parameters.AddWithValue("@IsEnabled", _config.IsEnabled);
                    cmdInsert.Parameters.AddWithValue("@Time1", _config.ReminderTime1);
                    cmdInsert.Parameters.AddWithValue("@Time2", _config.ReminderTime2);
                    cmdInsert.Parameters.AddWithValue("@Count", _config.ContactsPerReminder);
                    cmdInsert.Parameters.AddWithValue("@OnlyNew", _config.ShowOnlyNewContacts);
                    cmdInsert.Parameters.AddWithValue("@OnlyAssigned", _config.ShowOnlyAssigned);
                    cmdInsert.Parameters.AddWithValue("@Tolerance", _config.MinutesTolerance);
                    cmdInsert.ExecuteNonQuery();
                }

                Debug.WriteLine($"[CallReminderService] Config loaded: Enabled={_config.IsEnabled}, Time1={_config.ReminderTime1}, Time2={_config.ReminderTime2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Error loading config: {ex.Message}");
                // Use defaults
                _config = new CallReminderConfig
                {
                    UserID = _userID,
                    IsEnabled = true,
                    ReminderTime1 = new TimeSpan(10, 0, 0),
                    ReminderTime2 = new TimeSpan(13, 0, 0),
                    ContactsPerReminder = 5,
                    MinutesTolerance = 15
                };
            }
        }

        public void ReloadConfig()
        {
            LoadConfig();
        }

        private void StartTimer()
        {
            _checkTimer = new System.Windows.Forms.Timer();
            _checkTimer.Interval = 60000; // Check every minute
            _checkTimer.Tick += CheckTimer_Tick;
            _checkTimer.Start();

            Debug.WriteLine("[CallReminderService] Timer started");
        }

        private void CheckTimer_Tick(object sender, EventArgs e)
        {
            if (_config == null || !_config.IsEnabled) return;

            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            // Check vacation
            if (_config.VacationStart.HasValue && _config.VacationEnd.HasValue)
            {
                if (now.Date >= _config.VacationStart.Value.Date && now.Date <= _config.VacationEnd.Value.Date)
                {
                    Debug.WriteLine("[CallReminderService] User on vacation, skipping");
                    return;
                }
            }

            // Check lunch break
            if (currentTime >= _config.LunchBreakStart && currentTime <= _config.LunchBreakEnd)
            {
                Debug.WriteLine("[CallReminderService] Lunch break, skipping");
                return;
            }

            // Check if it's time for reminder
            if (IsTimeForReminder(currentTime, _config.ReminderTime1) ||
                IsTimeForReminder(currentTime, _config.ReminderTime2))
            {
                // Check if reminder wasn't shown recently (within tolerance window)
                if (!WasReminderShownRecently())
                {
                    Debug.WriteLine($"[CallReminderService] Time for reminder! Current: {currentTime}");
                    ShowReminder();
                }
            }
        }

        private bool IsTimeForReminder(TimeSpan current, TimeSpan target)
        {
            var diff = Math.Abs((current - target).TotalMinutes);
            return diff <= _config.MinutesTolerance;
        }

        private bool WasReminderShownRecently()
        {
            if (_lastReminderTime == null) return false;

            var diff = (DateTime.Now - _lastReminderTime.Value).TotalMinutes;
            return diff < (_config.MinutesTolerance * 2 + 5); // Double tolerance + buffer
        }

        private void ShowReminder()
        {
            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        var window = new CallReminderWindow(_connectionString, _userID, _config);
                        window.ShowDialog();
                        _lastReminderTime = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CallReminderService] Error showing window: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Dispatcher error: {ex.Message}");
            }
        }

        // For testing/manual trigger
        public void TriggerReminderNow()
        {
            ShowReminder();
        }

        public List<ContactToCall> GetRandomContacts(int count)
        {
            var contacts = new List<ContactToCall>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand("GetRandomContactsForReminder", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 30;
                cmd.Parameters.AddWithValue("@UserID", _userID);
                cmd.Parameters.AddWithValue("@Count", count);
                cmd.Parameters.AddWithValue("@OnlyNew", _config?.ShowOnlyNewContacts ?? true);
                cmd.Parameters.AddWithValue("@OnlyAssigned", _config?.ShowOnlyAssigned ?? false);
                cmd.Parameters.AddWithValue("@Wojewodztwa", (object)_config?.TerritoryWojewodztwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OnlyMyImports", _config?.OnlyMyImports ?? false);
                cmd.Parameters.AddWithValue("@ImportedByUser", (object)_userID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MaxAttempts", _config?.MaxAttemptsPerContact ?? 5);
                cmd.Parameters.AddWithValue("@CooldownDays", _config?.CooldownDays ?? 3);

                // PKD priorities - load from DB for this user's config
                string pkdJson = null;
                if (_config?.PKDPriorityWeight > 0)
                {
                    try
                    {
                        using var conn2 = new SqlConnection(_connectionString);
                        conn2.Open();
                        var cmdPkd = new SqlCommand(
                            "SELECT PKDCode FROM CallReminderPKDPriority WHERE ConfigID = @CID ORDER BY SortOrder", conn2);
                        cmdPkd.Parameters.AddWithValue("@CID", _config.ID);
                        var pkdCodes = new List<string>();
                        using var rdr = cmdPkd.ExecuteReader();
                        while (rdr.Read()) pkdCodes.Add(rdr.GetString(0));
                        if (pkdCodes.Count > 0)
                            pkdJson = System.Text.Json.JsonSerializer.Serialize(pkdCodes);
                    }
                    catch { }
                }
                cmd.Parameters.AddWithValue("@PKDPriorities", (object)pkdJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PKDWeight", _config?.PKDPriorityWeight ?? 70);

                using var reader = cmd.ExecuteReader();

                // Build column index map for optional columns
                var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    columnMap[reader.GetName(i)] = i;

                while (reader.Read())
                {
                    var contact = new ContactToCall
                    {
                        ID = reader.GetInt32(0),
                        Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Telefon = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Miasto = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Wojewodztwo = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Status = reader.IsDBNull(6) ? "Do zadzwonienia" : reader.GetString(6),
                        Branza = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        OstatniaNota = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        DataOstatniejNotatki = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                    };

                    // Read additional columns if available from SP
                    if (columnMap.TryGetValue("KodPocztowy", out int colKod) && !reader.IsDBNull(colKod))
                        contact.KodPocztowy = reader.GetString(colKod);
                    if (columnMap.TryGetValue("PKD", out int colPkd) && !reader.IsDBNull(colPkd))
                        contact.PKD = reader.GetString(colPkd);
                    if (columnMap.TryGetValue("PKDNazwa", out int colPkdN) && !reader.IsDBNull(colPkdN))
                        contact.PKDNazwa = reader.GetString(colPkdN);
                    if (columnMap.TryGetValue("NIP", out int colNip) && !reader.IsDBNull(colNip))
                        contact.NIP = reader.GetString(colNip);
                    if (columnMap.TryGetValue("Telefon2", out int colTel2) && !reader.IsDBNull(colTel2))
                        contact.Telefon2 = reader.GetString(colTel2);
                    if (columnMap.TryGetValue("Adres", out int colAddr) && !reader.IsDBNull(colAddr))
                        contact.Adres = reader.GetString(colAddr);
                    if (columnMap.TryGetValue("CallCount", out int colCalls) && !reader.IsDBNull(colCalls))
                        contact.CallCount = reader.GetInt32(colCalls);
                    if (columnMap.TryGetValue("LastCallDate", out int colLastCall) && !reader.IsDBNull(colLastCall))
                        contact.LastCallDate = reader.GetDateTime(colLastCall);
                    if (columnMap.TryGetValue("AssignedTo", out int colAssign) && !reader.IsDBNull(colAssign))
                        contact.AssignedTo = reader.GetString(colAssign);
                    if (columnMap.TryGetValue("OdlegloscKm", out int colDist) && !reader.IsDBNull(colDist))
                        contact.OdlegloscKm = Convert.ToDouble(reader.GetValue(colDist));
                    if (columnMap.TryGetValue("OstatniaNotaAutor", out int colAutor) && !reader.IsDBNull(colAutor))
                        contact.OstatniaNotaAutor = reader.GetString(colAutor);
                    if (columnMap.TryGetValue("Priority", out int colPrio) && !reader.IsDBNull(colPrio))
                        contact.Priority = reader.GetString(colPrio);

                    contacts.Add(contact);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Error getting contacts: {ex.Message}");
            }

            return contacts;
        }

        public int CreateReminderLog(int contactsCount)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    "INSERT INTO CallReminderLog (UserID, ReminderTime, ContactsShown) " +
                    "OUTPUT INSERTED.ID VALUES (@UserID, @Time, @Count)", conn);
                cmd.Parameters.AddWithValue("@UserID", _userID);
                cmd.Parameters.AddWithValue("@Time", DateTime.Now);
                cmd.Parameters.AddWithValue("@Count", contactsCount);

                return (int)cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Error creating log: {ex.Message}");
                return -1;
            }
        }

        public void LogContactAction(int reminderLogId, int contactId, bool wasCalled, bool noteAdded, bool statusChanged, string newStatus)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    "INSERT INTO CallReminderContacts (ReminderLogID, ContactID, WasCalled, NoteAdded, StatusChanged, NewStatus, ActionTime) " +
                    "VALUES (@LogID, @ContactID, @Called, @Note, @StatusChanged, @NewStatus, GETDATE())", conn);
                cmd.Parameters.AddWithValue("@LogID", reminderLogId);
                cmd.Parameters.AddWithValue("@ContactID", contactId);
                cmd.Parameters.AddWithValue("@Called", wasCalled);
                cmd.Parameters.AddWithValue("@Note", noteAdded);
                cmd.Parameters.AddWithValue("@StatusChanged", statusChanged);
                cmd.Parameters.AddWithValue("@NewStatus", (object)newStatus ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Error logging action: {ex.Message}");
            }
        }

        public void CompleteReminder(int reminderLogId, int calls, int notes, int statusChanges, bool wasSkipped, string skipReason)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    "UPDATE CallReminderLog SET ContactsCalled=@Calls, NotesAdded=@Notes, StatusChanges=@StatusChanges, " +
                    "WasSkipped=@Skipped, SkipReason=@Reason, CompletedAt=GETDATE() WHERE ID=@ID", conn);
                cmd.Parameters.AddWithValue("@ID", reminderLogId);
                cmd.Parameters.AddWithValue("@Calls", calls);
                cmd.Parameters.AddWithValue("@Notes", notes);
                cmd.Parameters.AddWithValue("@StatusChanges", statusChanges);
                cmd.Parameters.AddWithValue("@Skipped", wasSkipped);
                cmd.Parameters.AddWithValue("@Reason", (object)skipReason ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Error completing reminder: {ex.Message}");
            }
        }
    }
}
