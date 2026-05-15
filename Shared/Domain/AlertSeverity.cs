namespace Kalendarz1.Shared.Domain
{
    /// <summary>
    /// Poziom alertu. Krytyczny → toast + Windows notification + dźwięk.
    /// Warning → popup w prawym dolnym rogu. Info → tylko log historii.
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Krytyczny
    }

    public static class AlertSeverityHelper
    {
        public static string Polish(AlertSeverity s) => s switch
        {
            AlertSeverity.Info      => "Info",
            AlertSeverity.Warning   => "Ostrzeżenie",
            AlertSeverity.Krytyczny => "Krytyczny",
            _                       => "—"
        };

        /// <summary>HEX kolor pasujący do paletu MapaFloty.</summary>
        public static string HexColor(AlertSeverity s) => s switch
        {
            AlertSeverity.Info      => "#1565c0",
            AlertSeverity.Warning   => "#e65100",
            AlertSeverity.Krytyczny => "#c62828",
            _                       => "#78909c"
        };
    }
}
