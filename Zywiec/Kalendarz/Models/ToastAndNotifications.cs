using System;
using System.Collections.Generic;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastMessage
    {
        public string Message { get; set; }
        public ToastType Type { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
    }

    public enum ChangeNotificationType
    {
        InlineEdit,
        FormSave,
        DragDrop,
        Confirmation,
        BulkOperation,
        Delete
    }

    public class FieldChange
    {
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }

    public class ChangeNotificationItem
    {
        public string Title { get; set; }
        public string Dostawca { get; set; }
        public string LP { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public DateTime? DataOdbioru { get; set; }
        public ChangeNotificationType NotificationType { get; set; }
        public List<FieldChange> Changes { get; set; } = new List<FieldChange>();
    }

    /// <summary>
    /// Wpis zmiany w trybie symulacji (przesunięcie dostawy)
    /// </summary>
    public class SimulationChange
    {
        public string LP { get; set; }
        public string Dostawca { get; set; }
        public DateTime OldDate { get; set; }
        public DateTime NewDate { get; set; }
        public DateTime Timestamp { get; set; }

        private static readonly string[] DniSkrot = { "ndz", "pon", "wt", "śr", "czw", "pt", "sob" };

        public string OldDateShort => $"{DniSkrot[(int)OldDate.DayOfWeek]} {OldDate:dd.MM}";
        public string NewDateShort => $"{DniSkrot[(int)NewDate.DayOfWeek]} {NewDate:dd.MM}";

        public int DayDelta => (NewDate.Date - OldDate.Date).Days;
        public string DayDeltaDisplay => DayDelta > 0 ? $"+{DayDelta}d" : $"{DayDelta}d";
        public bool IsForward => DayDelta > 0;

        public string TimeShort => Timestamp.ToString("HH:mm");

        public string Display => $"{Dostawca}: {OldDate:yyyy-MM-dd} {DniSkrot[(int)OldDate.DayOfWeek]} → {NewDate:yyyy-MM-dd} {DniSkrot[(int)NewDate.DayOfWeek]} ({DayDeltaDisplay})";
    }
}
