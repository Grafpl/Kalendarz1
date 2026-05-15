namespace Kalendarz1.Shared.Domain
{
    /// <summary>
    /// Status kursu transportowego. Mapowanie 1:1 do kolumny TransportPL.Kurs.Status (varchar(50)).
    /// </summary>
    public enum KursStatus
    {
        Planowany,
        Akceptowany,
        WTrasie,
        Zakonczony,
        Anulowany
    }

    public static class KursStatusHelper
    {
        public static KursStatus Parse(string? value) => (value ?? "").Trim() switch
        {
            "Planowany"    => KursStatus.Planowany,
            "Akceptowany"  => KursStatus.Akceptowany,
            "WTrasie"      => KursStatus.WTrasie,
            "W trasie"     => KursStatus.WTrasie,
            "Zakonczony"   => KursStatus.Zakonczony,
            "Zakończony"   => KursStatus.Zakonczony,
            "Anulowany"    => KursStatus.Anulowany,
            _              => KursStatus.Planowany
        };

        public static string ToDbString(KursStatus s) => s switch
        {
            KursStatus.Planowany   => "Planowany",
            KursStatus.Akceptowany => "Akceptowany",
            KursStatus.WTrasie     => "WTrasie",
            KursStatus.Zakonczony  => "Zakonczony",
            KursStatus.Anulowany   => "Anulowany",
            _                      => "Planowany"
        };

        public static string Polish(KursStatus s) => s switch
        {
            KursStatus.Planowany   => "Planowany",
            KursStatus.Akceptowany => "Akceptowany",
            KursStatus.WTrasie     => "W trasie",
            KursStatus.Zakonczony  => "Zakończony",
            KursStatus.Anulowany   => "Anulowany",
            _                      => "—"
        };
    }
}
