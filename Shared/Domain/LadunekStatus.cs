namespace Kalendarz1.Shared.Domain
{
    /// <summary>
    /// Status pojedynczego ładunku w kursie. Używany m.in. przez KursMonitorService
    /// do śledzenia realizacji przystanków (KursRealizacja.Status).
    /// </summary>
    public enum LadunekStatus
    {
        Oczekujacy,
        WDrodze,
        Dotarl,
        Obsluzony,
        Pominiety,
        Zwrocony
    }

    public static class LadunekStatusHelper
    {
        public static LadunekStatus Parse(string? value) => (value ?? "").Trim() switch
        {
            "Oczekujacy" => LadunekStatus.Oczekujacy,
            "Oczekujący" => LadunekStatus.Oczekujacy,
            "WDrodze"    => LadunekStatus.WDrodze,
            "W drodze"   => LadunekStatus.WDrodze,
            "Dotarl"     => LadunekStatus.Dotarl,
            "Dotarł"     => LadunekStatus.Dotarl,
            "Obsluzony"  => LadunekStatus.Obsluzony,
            "Obsłużony"  => LadunekStatus.Obsluzony,
            "Pominiety"  => LadunekStatus.Pominiety,
            "Pominięty"  => LadunekStatus.Pominiety,
            "Zwrocony"   => LadunekStatus.Zwrocony,
            "Zwrócony"   => LadunekStatus.Zwrocony,
            _            => LadunekStatus.Oczekujacy
        };

        public static string ToDbString(LadunekStatus s) => s switch
        {
            LadunekStatus.Oczekujacy => "Oczekujacy",
            LadunekStatus.WDrodze    => "WDrodze",
            LadunekStatus.Dotarl     => "Dotarl",
            LadunekStatus.Obsluzony  => "Obsluzony",
            LadunekStatus.Pominiety  => "Pominiety",
            LadunekStatus.Zwrocony   => "Zwrocony",
            _                        => "Oczekujacy"
        };

        public static string Polish(LadunekStatus s) => s switch
        {
            LadunekStatus.Oczekujacy => "Oczekujący",
            LadunekStatus.WDrodze    => "W drodze",
            LadunekStatus.Dotarl     => "Dotarł",
            LadunekStatus.Obsluzony  => "Obsłużony",
            LadunekStatus.Pominiety  => "Pominięty",
            LadunekStatus.Zwrocony   => "Zwrócony",
            _                        => "—"
        };
    }
}
