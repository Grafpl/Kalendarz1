using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    /// <summary>
    /// Monitors meetings for changes and shows notifications when detected
    /// </summary>
    public class MeetingChangeMonitor
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string operatorId;
        private DispatcherTimer monitorTimer;
        private Dictionary<long, CachedMeeting> cachedMeetings = new Dictionary<long, CachedMeeting>();
        private bool isFirstCheck = true;

        public event EventHandler<List<MeetingChange>> ChangesDetected;

        public MeetingChangeMonitor(string userId)
        {
            operatorId = userId;
        }

        /// <summary>
        /// Starts monitoring meetings for changes
        /// </summary>
        /// <param name="intervalMinutes">Check interval in minutes (default: 2)</param>
        public void Start(int intervalMinutes = 2)
        {
            // Initial load
            RefreshMeetings();

            // Start timer
            monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(intervalMinutes)
            };
            monitorTimer.Tick += (s, e) => CheckForChanges();
            monitorTimer.Start();

            System.Diagnostics.Debug.WriteLine($"MeetingChangeMonitor started with {intervalMinutes} minute interval");
        }

        /// <summary>
        /// Stops monitoring
        /// </summary>
        public void Stop()
        {
            monitorTimer?.Stop();
            System.Diagnostics.Debug.WriteLine("MeetingChangeMonitor stopped");
        }

        /// <summary>
        /// Force an immediate check for changes
        /// </summary>
        public void CheckNow()
        {
            CheckForChanges();
        }

        private void RefreshMeetings()
        {
            try
            {
                var currentMeetings = LoadMeetingsFromDatabase();

                // Update cache
                cachedMeetings.Clear();
                foreach (var meeting in currentMeetings)
                {
                    cachedMeetings[meeting.Id] = meeting;
                }

                System.Diagnostics.Debug.WriteLine($"Cached {cachedMeetings.Count} meetings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing meetings: {ex.Message}");
            }
        }

        private void CheckForChanges()
        {
            try
            {
                var currentMeetings = LoadMeetingsFromDatabase();
                var changes = new List<MeetingChange>();

                // Check for changes in existing meetings
                foreach (var current in currentMeetings)
                {
                    if (cachedMeetings.TryGetValue(current.Id, out var cached))
                    {
                        // Check for time change
                        if (cached.MeetingDate != current.MeetingDate)
                        {
                            changes.Add(new MeetingChange
                            {
                                MeetingId = current.Id,
                                MeetingTitle = current.Title,
                                ChangeType = MeetingChangeType.TimeChanged,
                                OldValue = cached.MeetingDate.ToString("dd.MM HH:mm"),
                                NewValue = current.MeetingDate.ToString("dd.MM HH:mm")
                            });
                        }

                        // Check for location change
                        if (cached.Location != current.Location && !string.IsNullOrEmpty(current.Location))
                        {
                            changes.Add(new MeetingChange
                            {
                                MeetingId = current.Id,
                                MeetingTitle = current.Title,
                                ChangeType = MeetingChangeType.LocationChanged,
                                OldValue = cached.Location ?? "(brak)",
                                NewValue = current.Location
                            });
                        }

                        // Check for cancellation
                        if (cached.Status != "Anulowane" && current.Status == "Anulowane")
                        {
                            changes.Add(new MeetingChange
                            {
                                MeetingId = current.Id,
                                MeetingTitle = current.Title,
                                ChangeType = MeetingChangeType.Cancelled
                            });
                        }
                    }
                    else if (!isFirstCheck)
                    {
                        // New meeting - user was added
                        changes.Add(new MeetingChange
                        {
                            MeetingId = current.Id,
                            MeetingTitle = current.Title,
                            ChangeType = MeetingChangeType.AddedToMeeting,
                            NewValue = current.MeetingDate.ToString("dd.MM HH:mm")
                        });
                    }
                }

                // Check for removed meetings (user was removed from meeting)
                if (!isFirstCheck)
                {
                    var currentIds = currentMeetings.Select(m => m.Id).ToHashSet();
                    foreach (var cached in cachedMeetings.Values)
                    {
                        if (!currentIds.Contains(cached.Id) && cached.Status != "Anulowane")
                        {
                            // Check if meeting still exists but user was removed
                            var stillExists = CheckMeetingExists(cached.Id);
                            if (stillExists)
                            {
                                changes.Add(new MeetingChange
                                {
                                    MeetingId = cached.Id,
                                    MeetingTitle = cached.Title,
                                    ChangeType = MeetingChangeType.RemovedFromMeeting
                                });
                            }
                        }
                    }
                }

                // Update cache
                cachedMeetings.Clear();
                foreach (var meeting in currentMeetings)
                {
                    cachedMeetings[meeting.Id] = meeting;
                }

                isFirstCheck = false;

                // Notify if there are changes
                if (changes.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Detected {changes.Count} meeting changes");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ChangesDetected?.Invoke(this, changes);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for changes: {ex.Message}");
            }
        }

        private List<CachedMeeting> LoadMeetingsFromDatabase()
        {
            var meetings = new List<CachedMeeting>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT DISTINCT
                        s.SpotkaniID,
                        s.Tytul,
                        s.DataSpotkania,
                        s.Lokalizacja,
                        s.Status
                    FROM Spotkania s
                    LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                    WHERE (s.OrganizatorID = @id OR su.OperatorID = @id)
                      AND s.DataSpotkania >= DATEADD(DAY, -1, GETDATE())
                      AND s.DataSpotkania <= DATEADD(DAY, 30, GETDATE())
                    ORDER BY s.DataSpotkania ASC", conn);

                cmd.Parameters.AddWithValue("@id", operatorId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        meetings.Add(new CachedMeeting
                        {
                            Id = reader.GetInt64(0),
                            Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            MeetingDate = reader.GetDateTime(2),
                            Location = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Status = reader.IsDBNull(4) ? "Zaplanowane" : reader.GetString(4)
                        });
                    }
                }
            }

            return meetings;
        }

        private bool CheckMeetingExists(long meetingId)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Spotkania WHERE SpotkaniID = @id AND Status != 'Anulowane'", conn);
                    cmd.Parameters.AddWithValue("@id", meetingId);

                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private class CachedMeeting
        {
            public long Id { get; set; }
            public string Title { get; set; }
            public DateTime MeetingDate { get; set; }
            public string Location { get; set; }
            public string Status { get; set; }
        }
    }
}
