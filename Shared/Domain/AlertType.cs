namespace Kalendarz1.Shared.Domain
{
    /// <summary>
    /// Typy alertów floty/transportu. Konsumowane przez AlertBus (Faza 2).
    /// </summary>
    public enum AlertType
    {
        NieWyjechal,
        NieWrocil,
        NieplanowanyPostoj,
        PrzekroczenieMaxV,
        Spoznienie,
        BrakKierowcy,
        BrakPojazdu,
        KonfliktKursow
    }

    public static class AlertTypeHelper
    {
        public static string Polish(AlertType t) => t switch
        {
            AlertType.NieWyjechal        => "Nie wyjechał",
            AlertType.NieWrocil          => "Nie wrócił",
            AlertType.NieplanowanyPostoj => "Nieplanowany postój",
            AlertType.PrzekroczenieMaxV  => "Przekroczenie prędkości",
            AlertType.Spoznienie         => "Spóźnienie",
            AlertType.BrakKierowcy       => "Brak kierowcy",
            AlertType.BrakPojazdu        => "Brak pojazdu",
            AlertType.KonfliktKursow     => "Konflikt kursów",
            _                            => "—"
        };

        public static string ToDbString(AlertType t) => t.ToString();

        public static AlertType Parse(string? value)
        {
            if (System.Enum.TryParse<AlertType>(value, out var result))
                return result;
            return AlertType.NieplanowanyPostoj;
        }
    }
}
