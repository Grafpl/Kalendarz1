using System;
using System.Collections.Generic;

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

        // PKD Priority
        public int PKDPriorityWeight { get; set; } = 70;
        public string UsePresetPKD { get; set; }

        // Contact Source
        public string SourcePriority { get; set; } = "mixed";
        public int ManualContactsPercent { get; set; } = 50;
        public DateTime? ImportDateFrom { get; set; }
        public DateTime? ImportDateTo { get; set; }
        public bool PrioritizeRecentImports { get; set; } = true;

        // Goals & Limits
        public int DailyCallTarget { get; set; } = 30;
        public int WeeklyCallTarget { get; set; } = 120;
        public int MaxAttemptsPerContact { get; set; } = 5;
        public int CooldownDays { get; set; } = 3;
        public int MinCallDurationSec { get; set; } = 30;
        public int AlertBelowPercent { get; set; } = 50;

        // Territory
        public string TerritoryWojewodztwa { get; set; }
        public int? TerritoryRadiusKm { get; set; }
        public bool ExclusiveTerritory { get; set; } = false;
        public string SharePoolWithUsers { get; set; }

        // Advanced Schedule
        public bool UseAdvancedSchedule { get; set; } = false;
        public TimeSpan LunchBreakStart { get; set; } = new TimeSpan(12, 0, 0);
        public TimeSpan LunchBreakEnd { get; set; } = new TimeSpan(13, 0, 0);
        public DateTime? VacationStart { get; set; }
        public DateTime? VacationEnd { get; set; }
        public string SubstituteUserID { get; set; }

        // Preset
        public string PresetType { get; set; }
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

    public class CallReminderPKDPriority
    {
        public int ID { get; set; }
        public int ConfigID { get; set; }
        public string PKDCode { get; set; }
        public string PKDName { get; set; }
        public int Priority { get; set; } = 50;
        public bool IsExcluded { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }

    public class CallReminderTimeSlot
    {
        public int ID { get; set; }
        public int ConfigID { get; set; }
        public int? DayOfWeek { get; set; }
        public TimeSpan TimeSlot { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CallReminderLeadTransfer
    {
        public int ID { get; set; }
        public string FromUserID { get; set; }
        public string ToUserID { get; set; }
        public int ContactsCount { get; set; }
        public string FilterCriteria { get; set; }
        public DateTime TransferredAt { get; set; }
        public string TransferredBy { get; set; }
        public string Reason { get; set; }
    }

    public class CallReminderConfigAudit
    {
        public int ID { get; set; }
        public int ConfigID { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangeType { get; set; }
    }

    public class ConfigPreset
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int DailyTarget { get; set; }
        public int WeeklyTarget { get; set; }
        public int ContactsPerReminder { get; set; }
        public bool ShowOnlyAssigned { get; set; }
        public bool ShowOnlyNewContacts { get; set; }
        public int PKDPriorityWeight { get; set; }
        public string SourcePriority { get; set; } = "mixed";
        public int MaxAttemptsPerContact { get; set; } = 5;
        public int CooldownDays { get; set; } = 3;
    }

    public static class ConfigPresets
    {
        public static readonly Dictionary<string, ConfigPreset> Presets = new()
        {
            ["junior"] = new ConfigPreset
            {
                Name = "Junior",
                Description = "Dla nowych handlowców - mniej telefonów, pełny tutorial",
                DailyTarget = 20,
                WeeklyTarget = 80,
                ContactsPerReminder = 3,
                ShowOnlyAssigned = true,
                ShowOnlyNewContacts = false,
                PKDPriorityWeight = 50
            },
            ["senior"] = new ConfigPreset
            {
                Name = "Senior",
                Description = "Dla doświadczonych - więcej telefonów, cała Polska",
                DailyTarget = 40,
                WeeklyTarget = 160,
                ContactsPerReminder = 5,
                ShowOnlyAssigned = false,
                ShowOnlyNewContacts = false,
                PKDPriorityWeight = 80
            },
            ["hunter"] = new ConfigPreset
            {
                Name = "Hunter",
                Description = "Agresywne pozyskiwanie - tylko nowe kontakty",
                DailyTarget = 60,
                WeeklyTarget = 250,
                ContactsPerReminder = 8,
                ShowOnlyAssigned = false,
                ShowOnlyNewContacts = true,
                PKDPriorityWeight = 90
            },
            ["farmer"] = new ConfigPreset
            {
                Name = "Farmer",
                Description = "Obsługa istniejących klientów - follow-up",
                DailyTarget = 15,
                WeeklyTarget = 60,
                ContactsPerReminder = 3,
                ShowOnlyAssigned = false,
                ShowOnlyNewContacts = false,
                SourcePriority = "manual",
                PKDPriorityWeight = 30
            }
        };

        public static readonly Dictionary<string, string[]> PKDPresets = new()
        {
            ["meat"] = new[] { "10.11", "10.12", "10.13", "10.85", "46.32", "47.22" },
            ["gastro"] = new[] { "56.10", "56.21", "56.29", "56.30" },
            ["wholesale"] = new[] { "46.31", "46.32", "46.33", "46.34", "46.38", "46.39" },
            ["retail"] = new[] { "47.11", "47.19", "47.21", "47.22", "47.29" }
        };

        public static readonly string[] Wojewodztwa = new[]
        {
            "dolnośląskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
            "łódzkie", "małopolskie", "mazowieckie", "opolskie",
            "podkarpackie", "podlaskie", "pomorskie", "śląskie",
            "świętokrzyskie", "warmińsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
        };
    }
}
