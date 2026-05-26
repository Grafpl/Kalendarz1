namespace Kalendarz1.Customer360.Services
{
    /// <summary>Wynik scoringu Customer360 — 4 składniki + total + litera (parametry z configu).</summary>
    public class Customer360Score
    {
        public int ObrotPkt { get; set; }          // 0-100
        public int CzestotliwoscPkt { get; set; }   // 0-100
        public int TerminowoscPkt { get; set; }     // 0-100
        public int DlugoscPkt { get; set; }         // 0-100
        public int Total { get; set; }              // 0-100 (ważony)
        public string Litera { get; set; } = "?";
        public string Kategoria { get; set; } = "";

        // Wagi użyte (z configu — do wyświetlenia w UI)
        public int WagaObrot { get; set; }
        public int WagaCzestotliwosc { get; set; }
        public int WagaTerminowosc { get; set; }
        public int WagaDlugosc { get; set; }

        // Wartości surowe (do tooltipów/opisów)
        public decimal Obrot12M { get; set; }
        public decimal SrOdstepDni { get; set; }
        public decimal TerminowoscProc { get; set; }
        public double LataRelacji { get; set; }

        public decimal RekomendacjaLimitu { get; set; }
        public string RekomendacjaOpis { get; set; } = "";

        public string KategoriaKolor => Litera switch
        {
            "A" => "#10B981",
            "B" => "#34D399",
            "C" => "#F59E0B",
            "D" => "#F97316",
            "F" => "#EF4444",
            _ => "#6B7280"
        };
    }
}
