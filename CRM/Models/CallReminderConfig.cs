using System;

namespace Kalendarz1.CRM.Models
{
    public class CallReminderConfig
    {
        public int ID { get; set; }
        public string UserID { get; set; }
        public bool IsEnabled { get; set; } = true;
        public TimeSpan ReminderTime1 { get; set; } = new TimeSpan(10, 0, 0);
        public TimeSpan ReminderTime2 { get; set; } = new TimeSpan(13, 0, 0);
        public int ContactsPerReminder { get; set; } = 5;
        public bool ShowOnlyNewContacts { get; set; } = true;
        public bool ShowOnlyAssigned { get; set; } = false;
        public int MinutesTolerance { get; set; } = 15;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string ModifiedBy { get; set; }
    }

    public class CallReminderLog
    {
        public int ID { get; set; }
        public string UserID { get; set; }
        public DateTime ReminderTime { get; set; }
        public int ContactsShown { get; set; }
        public int ContactsCalled { get; set; }
        public int NotesAdded { get; set; }
        public int StatusChanges { get; set; }
        public bool WasSkipped { get; set; }
        public string SkipReason { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class CallReminderStats
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public int TotalReminders { get; set; }
        public int TotalContactsShown { get; set; }
        public int TotalCalls { get; set; }
        public int TotalNotes { get; set; }
        public int TotalStatusChanges { get; set; }
        public int SkippedCount { get; set; }
        public decimal CallRate { get; set; }
    }
}
