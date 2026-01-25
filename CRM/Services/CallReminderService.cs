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
                    "SELECT ID, IsEnabled, ReminderTime1, ReminderTime2, ContactsPerReminder, " +
                    "ShowOnlyNewContacts, ShowOnlyAssigned, MinutesTolerance FROM CallReminderConfig WHERE UserID = @UserID", conn);
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
                        MinutesTolerance = reader.GetInt32(7)
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
                cmd.Parameters.AddWithValue("@UserID", _userID);
                cmd.Parameters.AddWithValue("@Count", count);
                cmd.Parameters.AddWithValue("@OnlyNew", _config?.ShowOnlyNewContacts ?? true);
                cmd.Parameters.AddWithValue("@OnlyAssigned", _config?.ShowOnlyAssigned ?? false);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    contacts.Add(new ContactToCall
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
                    });
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
