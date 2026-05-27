using Kalendarz1.Customer360.Models;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Czyste obliczenia pochodne KPI (bez I/O). Na razie: ryzyko odejścia (churn).
    /// Scoring jest osobno w Customer360Scorer; tu logika nie-scoringowa.
    /// </summary>
    public static class Customer360KpiCalculator
    {
        /// <summary>Ryzyko odejścia klienta na bazie odstępu od ostatniego zamówienia + trendu YoY.</summary>
        public static (string level, string reason) ObliczChurn(KlientKpi kpi)
        {
            if (kpi.LiczbaZamowien12M == 0) return ("UNKNOWN", "Brak zamówień w ostatnich 12 mies");
            if (!kpi.OstatnieZamowienie.HasValue) return ("UNKNOWN", "Brak daty ostatniego zamówienia");

            int dniOd = kpi.DniOdOstatniegoZamowienia;
            decimal sredniOdstep = kpi.SredniCzasMiedzyZamowieniami;
            if (sredniOdstep == 0) sredniOdstep = 30; // fallback
            double ratio = (double)dniOd / (double)sredniOdstep;

            decimal yoyChange = kpi.Obrot12MPrev > 0 ? (kpi.Obrot12M - kpi.Obrot12MPrev) / kpi.Obrot12MPrev : 0m;
            bool yoyMocnoSpadl = yoyChange < -0.3m;  // -30% YoY
            bool ratioPrzekroczony = ratio > 2.5;
            bool ratioMocnoPrzekroczony = ratio > 4.0;

            if (ratioMocnoPrzekroczony && yoyMocnoSpadl)
                return ("CRITICAL", $"Brak zamówienia {dniOd} dni (norma {sredniOdstep:N0}) + obrót YoY {yoyChange:P0}");
            if (ratioMocnoPrzekroczony)
                return ("WARNING", $"Brak zamówienia {dniOd} dni (norma {sredniOdstep:N0})");
            if (ratioPrzekroczony)
                return ("WATCH", $"Opóźnione zamówienie ({dniOd} dni vs norma {sredniOdstep:N0})");
            if (yoyMocnoSpadl)
                return ("WATCH", $"Obrót YoY {yoyChange:P0}");

            return ("OK", $"Aktywny ({dniOd} dni od ostatniego, norma {sredniOdstep:N0})");
        }
    }
}
