namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Konfiguracja scoringu klienta — w pełni edytowalna przez użytkownika (okno ustawień).
    /// Domyślne wartości = rozsądny start, ale wszystko można zmienić i zapisać do bazy.
    /// </summary>
    public class Customer360ScoringConfig
    {
        // ── Wagi 4 składników (powinny sumować się do 100) ──
        public int WagaObrot { get; set; } = 35;
        public int WagaCzestotliwosc { get; set; } = 25;
        public int WagaTerminowosc { get; set; } = 25;
        public int WagaDlugosc { get; set; } = 15;

        // ── Obrót: kwota (zł) dająca 100 pkt (liniowo od 0) ──
        public decimal ObrotNaMaxPkt { get; set; } = 2_000_000m;

        // ── Częstotliwość: sub-score = 100 - max(0, śrOdstępDni - BazaDni) * SpadekNaDzien ──
        public int CzestBazaDni { get; set; } = 3;            // do ilu dni odstępu = pełne 100 pkt
        public double CzestSpadekNaDzien { get; set; } = 2.7; // ile pkt traci za każdy dzień powyżej bazy

        // ── Terminowość: wartość sub-score gdy brak rozliczonych faktur (neutralna) ──
        public int TerminowoscBrakDanychPkt { get; set; } = 60;

        // ── Długość relacji: ile lat = 100 pkt (liniowo), z dolnym progiem ──
        public double DlugoscLataNaMax { get; set; } = 3.0;
        public int DlugoscMinPkt { get; set; } = 10;

        // ── Progi liter (Total >= próg) ──
        public int ProgA { get; set; } = 85;
        public int ProgB { get; set; } = 70;
        public int ProgC { get; set; } = 55;
        public int ProgD { get; set; } = 40;

        /// <summary>Suma wag — do walidacji w oknie (ma być 100).</summary>
        public int SumaWag => WagaObrot + WagaCzestotliwosc + WagaTerminowosc + WagaDlugosc;

        public Customer360ScoringConfig Klon() => (Customer360ScoringConfig)MemberwiseClone();
    }
}
