using Kalendarz1.Customer360.Models;
using System;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Kalkulator scoringu — CZYSTA funkcja (bez I/O). Liczy 4 składniki z już-pobranego KPI
    /// + daty pierwszej faktury, wg parametrów z configu (edytowalnych w oknie ustawień).
    /// </summary>
    public static class Customer360Scorer
    {
        public static Customer360Score BudujScore(KlientKpi kpi, DateTime? pierwszaFaktura, Customer360ScoringConfig cfg)
        {
            int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

            // 1) Obrót 12M — liniowo do progu „na 100 pkt"
            int obrotPkt = cfg.ObrotNaMaxPkt > 0
                ? Clamp((int)Math.Round(kpi.Obrot12M / cfg.ObrotNaMaxPkt * 100m), 0, 100)
                : 0;

            // 2) Częstotliwość — wg średniego odstępu dni między zamówieniami
            int czestPkt;
            if (kpi.LiczbaZamowien12M <= 0) czestPkt = 0;
            else if (kpi.SredniCzasMiedzyZamowieniami <= 0)
                czestPkt = kpi.LiczbaZamowien12M >= 2 ? 100 : 50;  // <=1 zamówienie → brak odstępu
            else
                czestPkt = Clamp((int)Math.Round(100 - Math.Max(0, (double)kpi.SredniCzasMiedzyZamowieniami - cfg.CzestBazaDni) * cfg.CzestSpadekNaDzien), 0, 100);

            // 3) Terminowość — udział salda „w terminie" w całym otwartym saldzie; brak salda → wartość neutralna z configu
            decimal saldoRazem = kpi.Terminowe + kpi.Przeterminowane;
            int terminPkt;
            decimal terminProc;
            if (saldoRazem <= 0.01m) { terminPkt = Clamp(cfg.TerminowoscBrakDanychPkt, 0, 100); terminProc = terminPkt; }
            else { terminProc = kpi.Terminowe / saldoRazem * 100m; terminPkt = Clamp((int)Math.Round(terminProc), 0, 100); }

            // 4) Długość relacji — lata od pierwszej faktury
            double lata = pierwszaFaktura.HasValue ? (DateTime.Today - pierwszaFaktura.Value.Date).TotalDays / 365.0 : 0;
            int dlugPkt = cfg.DlugoscLataNaMax > 0
                ? Clamp((int)Math.Round(lata / cfg.DlugoscLataNaMax * 100), cfg.DlugoscMinPkt, 100)
                : cfg.DlugoscMinPkt;

            // Total ważony (wagi sumują się do ~100)
            int sumaWag = Math.Max(1, cfg.SumaWag);
            int total = (int)Math.Round(
                (obrotPkt * cfg.WagaObrot + czestPkt * cfg.WagaCzestotliwosc + terminPkt * cfg.WagaTerminowosc + dlugPkt * cfg.WagaDlugosc)
                / (double)sumaWag);
            total = Clamp(total, 0, 100);

            (string litera, string kat) = total >= cfg.ProgA ? ("A", "Doskonały")
                : total >= cfg.ProgB ? ("B", "Dobry")
                : total >= cfg.ProgC ? ("C", "Średni")
                : total >= cfg.ProgD ? ("D", "Słaby")
                : ("F", "Krytyczny");

            // Rekomendacja limitu wg oceny
            decimal faktor = litera switch { "A" => 1.2m, "B" => 1.0m, "C" => 1.0m, "D" => 0.7m, _ => 0m };
            decimal rekomendacja = kpi.LimitKredytowy * faktor;
            string opis = litera switch
            {
                "A" => "Klient wzorowy — można rozważyć podniesienie limitu kredytowego.",
                "B" => "Solidny klient — limit bez zmian.",
                "C" => "Przeciętny — monitoruj terminowość i obrót.",
                "D" => "Podwyższone ryzyko — rozważ obniżenie limitu / przedpłaty.",
                _ => "Wysokie ryzyko — wstrzymaj kredyt kupiecki, tylko przedpłata."
            };

            return new Customer360Score
            {
                ObrotPkt = obrotPkt,
                CzestotliwoscPkt = czestPkt,
                TerminowoscPkt = terminPkt,
                DlugoscPkt = dlugPkt,
                Total = total,
                Litera = litera,
                Kategoria = kat,
                WagaObrot = cfg.WagaObrot,
                WagaCzestotliwosc = cfg.WagaCzestotliwosc,
                WagaTerminowosc = cfg.WagaTerminowosc,
                WagaDlugosc = cfg.WagaDlugosc,
                Obrot12M = kpi.Obrot12M,
                SrOdstepDni = kpi.SredniCzasMiedzyZamowieniami,
                TerminowoscProc = terminProc,
                LataRelacji = lata,
                RekomendacjaLimitu = rekomendacja,
                RekomendacjaOpis = opis
            };
        }
    }
}
