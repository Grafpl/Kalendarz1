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
                      ISNULL(OnlyMyImports, 0), RequiredTags, ReminderTime3
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
                        OnlyMyImports = reader.GetBoolean(23),
                        RequiredTags = reader.IsDBNull(24) ? null : reader.GetString(24),
                        ReminderTime3 = reader.IsDBNull(25) ? null : reader.GetTimeSpan(25)
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

                Debug.WriteLine($"[CallReminderService] Config loaded: Enabled={_config.IsEnabled}, Time1={_config.ReminderTime1}, Time2={_config.ReminderTime2}, Time3={_config.ReminderTime3?.ToString() ?? "brak"}");
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

            // Check if it's time for reminder (exact minute match)
            bool isTime = IsTimeForReminder(currentTime, _config.ReminderTime1) ||
                          IsTimeForReminder(currentTime, _config.ReminderTime2);
            if (_config.ReminderTime3.HasValue)
                isTime = isTime || IsTimeForReminder(currentTime, _config.ReminderTime3.Value);

            if (isTime)
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
            // Only trigger at the exact minute (e.g. 10:00:00 - 10:00:59)
            return current.Hours == target.Hours && current.Minutes == target.Minutes;
        }

        private bool WasReminderShownRecently()
        {
            if (_lastReminderTime == null) return false;

            var diff = (DateTime.Now - _lastReminderTime.Value).TotalMinutes;
            return diff < 2; // Don't show again within 2 minutes
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

                // Try stored procedure first
                bool usedSP = false;
                try
                {
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
                    cmd.Parameters.AddWithValue("@RequiredTags", (object)_config?.RequiredTags ?? DBNull.Value);

                    using var reader = cmd.ExecuteReader();
                    contacts = ReadContactsFromReader(reader);
                    usedSP = true;
                }
                catch (Exception spEx)
                {
                    Debug.WriteLine($"[CallReminderService] SP failed, using fallback: {spEx.Message}");
                }

                // Fallback: direct query if SP failed
                if (!usedSP)
                {
                    contacts = GetContactsFallback(conn, count);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Error getting contacts: {ex.Message}");
            }

            return contacts;
        }

        private List<ContactToCall> ReadContactsFromReader(SqlDataReader reader)
        {
            var contacts = new List<ContactToCall>();
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
                };

                if (columnMap.TryGetValue("Email", out int colEmail) && !reader.IsDBNull(colEmail))
                    contact.Email = reader.GetValue(colEmail).ToString();
                if (columnMap.TryGetValue("MIASTO", out int colMiasto) && !reader.IsDBNull(colMiasto))
                    contact.Miasto = reader.GetValue(colMiasto).ToString();
                else if (columnMap.TryGetValue("Miasto", out int colMiasto2) && !reader.IsDBNull(colMiasto2))
                    contact.Miasto = reader.GetValue(colMiasto2).ToString();
                if (columnMap.TryGetValue("Wojewodztwo", out int colWoj) && !reader.IsDBNull(colWoj))
                    contact.Wojewodztwo = reader.GetValue(colWoj).ToString();
                if (columnMap.TryGetValue("Status", out int colStatus) && !reader.IsDBNull(colStatus))
                    contact.Status = reader.GetValue(colStatus).ToString();
                else
                    contact.Status = "Do zadzwonienia";
                if (columnMap.TryGetValue("Branza", out int colBr) && !reader.IsDBNull(colBr))
                    contact.Branza = reader.GetValue(colBr).ToString();
                if (columnMap.TryGetValue("OstatniaNota", out int colNota) && !reader.IsDBNull(colNota))
                    contact.OstatniaNota = reader.GetValue(colNota).ToString();
                if (columnMap.TryGetValue("DataOstatniejNotatki", out int colNotaDate) && !reader.IsDBNull(colNotaDate))
                    contact.DataOstatniejNotatki = reader.GetDateTime(colNotaDate);
                if (columnMap.TryGetValue("PKD", out int colPkd) && !reader.IsDBNull(colPkd))
                    contact.PKD = reader.GetValue(colPkd).ToString();
                if (columnMap.TryGetValue("KodPocztowy", out int colKod) && !reader.IsDBNull(colKod))
                    contact.KodPocztowy = reader.GetValue(colKod).ToString();
                if (columnMap.TryGetValue("Adres", out int colAdres) && !reader.IsDBNull(colAdres))
                    contact.Adres = reader.GetValue(colAdres).ToString();
                if (columnMap.TryGetValue("Tagi", out int colTagi) && !reader.IsDBNull(colTagi))
                    contact.Tagi = reader.GetValue(colTagi).ToString();
                if (columnMap.TryGetValue("Priority", out int colPrio) && !reader.IsDBNull(colPrio))
                    contact.Priority = reader.GetValue(colPrio).ToString();
                if (columnMap.TryGetValue("IsFromImport", out int colImport) && !reader.IsDBNull(colImport))
                    contact.IsFromImport = Convert.ToBoolean(reader.GetValue(colImport));
                if (columnMap.TryGetValue("ImportedBy", out int colImpBy) && !reader.IsDBNull(colImpBy))
                    contact.ImportedBy = reader.GetValue(colImpBy).ToString();

                contacts.Add(contact);
            }
            return contacts;
        }

        private List<ContactToCall> GetContactsFallback(SqlConnection conn, int count)
        {
            var contacts = new List<ContactToCall>();
            var sb = new System.Text.StringBuilder();
            var parameters = new List<SqlParameter>();

            sb.AppendLine("SELECT TOP (@Count) o.ID, o.Nazwa, o.Telefon_K as Telefon, o.MIASTO as Miasto, o.Wojewodztwo,");
            sb.AppendLine("  o.PKD_Opis as PKD, ISNULL(o.Status, 'Do zadzwonienia') as Status, o.Tagi as Branza,");
            sb.AppendLine("  o.KOD as KodPocztowy, o.ULICA as Adres, o.Tagi,");
            sb.AppendLine("  ISNULL(o.IsFromImport, 0) as IsFromImport, o.ImportedBy,");
            sb.AppendLine("  (SELECT TOP 1 n.Tresc FROM NotatkiCRM n WHERE n.IDOdbiorcy = o.ID ORDER BY n.DataUtworzenia DESC) as OstatniaNota,");
            sb.AppendLine("  (SELECT TOP 1 n.DataUtworzenia FROM NotatkiCRM n WHERE n.IDOdbiorcy = o.ID ORDER BY n.DataUtworzenia DESC) as DataOstatniejNotatki");
            sb.AppendLine("FROM OdbiorcyCRM o");
            sb.AppendLine("LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy");
            sb.AppendLine("WHERE o.Telefon_K IS NOT NULL AND o.Telefon_K <> ''");
            sb.AppendLine("  AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')");

            parameters.Add(new SqlParameter("@Count", count));

            if (_config?.ShowOnlyNewContacts == true)
                sb.AppendLine("  AND ISNULL(o.Status, 'Do zadzwonienia') = 'Do zadzwonienia'");

            if (_config?.ShowOnlyAssigned == true)
            {
                sb.AppendLine("  AND w.OperatorID = @UserID");
                parameters.Add(new SqlParameter("@UserID", _userID));
            }

            if (_config?.OnlyMyImports == true)
            {
                sb.AppendLine("  AND o.ImportedBy = @ImportedByUser");
                parameters.Add(new SqlParameter("@ImportedByUser", _userID));
            }

            // Territory filter
            if (!string.IsNullOrEmpty(_config?.TerritoryWojewodztwa))
            {
                try
                {
                    var woj = System.Text.Json.JsonSerializer.Deserialize<List<string>>(_config.TerritoryWojewodztwa);
                    if (woj != null && woj.Count > 0)
                    {
                        var wojParams = new List<string>();
                        for (int i = 0; i < woj.Count; i++)
                        {
                            var pName = $"@woj{i}";
                            wojParams.Add(pName);
                            parameters.Add(new SqlParameter(pName, woj[i]));
                        }
                        sb.AppendLine($"  AND o.Wojewodztwo IN ({string.Join(",", wojParams)})");
                    }
                }
                catch { }
            }

            // Tags filter
            if (!string.IsNullOrEmpty(_config?.RequiredTags))
            {
                try
                {
                    var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(_config.RequiredTags);
                    if (tags != null && tags.Count > 0)
                    {
                        var tagConditions = new List<string>();
                        for (int i = 0; i < tags.Count; i++)
                        {
                            var pName = $"@tag{i}";
                            tagConditions.Add($"o.Tagi LIKE '%' + {pName} + '%'");
                            parameters.Add(new SqlParameter(pName, tags[i]));
                        }
                        sb.AppendLine($"  AND ({string.Join(" OR ", tagConditions)})");
                    }
                }
                catch { }
            }

            // Exclude already shown today
            sb.AppendLine("  AND o.ID NOT IN (SELECT crc.ContactID FROM CallReminderContacts crc");
            sb.AppendLine("    INNER JOIN CallReminderLog crl ON crc.ReminderLogID = crl.ID");
            sb.AppendLine("    WHERE crl.UserID = @UserIDLog AND CAST(crl.ReminderTime AS DATE) = CAST(GETDATE() AS DATE))");
            parameters.Add(new SqlParameter("@UserIDLog", _userID));

            sb.AppendLine("ORDER BY NEWID()");

            try
            {
                var cmd = new SqlCommand(sb.ToString(), conn);
                cmd.CommandTimeout = 30;
                foreach (var p in parameters)
                    cmd.Parameters.Add(p);

                using var reader = cmd.ExecuteReader();
                contacts = ReadContactsFromReader(reader);
                Debug.WriteLine($"[CallReminderService] Fallback query returned {contacts.Count} contacts");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallReminderService] Fallback query error: {ex.Message}");
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
