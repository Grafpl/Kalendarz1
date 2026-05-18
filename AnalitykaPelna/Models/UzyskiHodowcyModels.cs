using System;
using System.Globalization;

namespace Kalendarz1.AnalitykaPelna.Models
{
    /// <summary>
    /// Okres agregacji statystyk uzyskania.
    /// </summary>
    public enum OkresAgregacji
    {
        Dzienna,
        Tygodniowa,
        Miesieczna,
        Kwartalna,
        Roczna,
        CalyOkres
    }

    /// <summary>
    /// Wiersz raportu uzyskania per hodowca w danym okresie.
    /// PWU (tuszki + podroby) dzielone proporcjonalnie do udziału żywca hodowcy w dniu dostawy.
    /// </summary>
    public class UzyskiPerHodowca
    {
        public string KluczOkresu { get; set; } = "";       // wewnętrzny ID grupowania
        public string EtykietaOkresu { get; set; } = "";    // display ("2026-W18", "Maj 2026", "Q2 2026", "Pn 05.05.2026")
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }

        public int KhId { get; set; }
        public string Hodowca { get; set; } = "";
        public int LiczbaDokumentow { get; set; }
        public int LiczbaDniDostaw { get; set; }

        public decimal ZywiecKg { get; set; }       // sPZ kat. 65882
        public decimal TuszkaAKg { get; set; }      // sPWU 'Kurczak A' (proporcjonalnie)
        public decimal TuszkaBKg { get; set; }      // sPWU 'Kurczak B' (proporcjonalnie)
        public decimal WatrobaKg { get; set; }      // sPWU 'Wątroba' (proporcjonalnie)
        public decimal ZoladkiKg { get; set; }      // sPWU 'Żołądki' (proporcjonalnie)
        public decimal SerceKg { get; set; }        // sPWU 'Serce' (proporcjonalnie)

        public decimal SumaTuszekAB => TuszkaAKg + TuszkaBKg;
        public decimal SumaPodrobow => WatrobaKg + ZoladkiKg + SerceKg;
        public decimal SumaWyjsciaUboju => SumaTuszekAB + SumaPodrobow;

        public decimal WydajnoscTuszekProc =>
            ZywiecKg <= 0 ? 0 : SumaTuszekAB / ZywiecKg * 100m;
        public decimal WydajnoscCalkowitaProc =>
            ZywiecKg <= 0 ? 0 : SumaWyjsciaUboju / ZywiecKg * 100m;

        public decimal ProcTuszkaA =>
            ZywiecKg <= 0 ? 0 : TuszkaAKg / ZywiecKg * 100m;
        public decimal ProcTuszkaB =>
            ZywiecKg <= 0 ? 0 : TuszkaBKg / ZywiecKg * 100m;
        public decimal ProcWatroba =>
            ZywiecKg <= 0 ? 0 : WatrobaKg / ZywiecKg * 100m;
        public decimal ProcZoladki =>
            ZywiecKg <= 0 ? 0 : ZoladkiKg / ZywiecKg * 100m;
        public decimal ProcSerce =>
            ZywiecKg <= 0 ? 0 : SerceKg / ZywiecKg * 100m;
    }

    /// <summary>
    /// Helpery do tworzenia kluczy/etykiet okresu.
    /// </summary>
    public static class OkresHelper
    {
        public static (string Klucz, string Etykieta, DateTime Od, DateTime Do)
            DlaDaty(DateTime data, OkresAgregacji okres)
        {
            switch (okres)
            {
                case OkresAgregacji.Dzienna:
                    {
                        var od = data.Date;
                        return ($"D-{od:yyyy-MM-dd}",
                                od.ToString("ddd dd.MM.yyyy", new CultureInfo("pl-PL")),
                                od, od);
                    }
                case OkresAgregacji.Tygodniowa:
                    {
                        int tydzien = ISOWeek.GetWeekOfYear(data);
                        int rokIso = ISOWeek.GetYear(data);
                        var pn = ISOWeek.ToDateTime(rokIso, tydzien, DayOfWeek.Monday);
                        var nd = pn.AddDays(6);
                        return ($"W-{rokIso}-{tydzien:00}",
                                $"Tydz. {tydzien:00}/{rokIso} ({pn:dd.MM}–{nd:dd.MM})",
                                pn, nd);
                    }
                case OkresAgregacji.Miesieczna:
                    {
                        var od = new DateTime(data.Year, data.Month, 1);
                        var doData = od.AddMonths(1).AddDays(-1);
                        return ($"M-{od:yyyy-MM}",
                                od.ToString("MMMM yyyy", new CultureInfo("pl-PL")),
                                od, doData);
                    }
                case OkresAgregacji.Kwartalna:
                    {
                        int q = (data.Month - 1) / 3 + 1;
                        var od = new DateTime(data.Year, (q - 1) * 3 + 1, 1);
                        var doData = od.AddMonths(3).AddDays(-1);
                        return ($"Q-{data.Year}-{q}",
                                $"Q{q} {data.Year} ({od:dd.MM}–{doData:dd.MM})",
                                od, doData);
                    }
                case OkresAgregacji.Roczna:
                    {
                        var od = new DateTime(data.Year, 1, 1);
                        var doData = new DateTime(data.Year, 12, 31);
                        return ($"R-{data.Year}",
                                $"Rok {data.Year}",
                                od, doData);
                    }
                case OkresAgregacji.CalyOkres:
                default:
                    return ("CALY", "Cały okres", DateTime.MinValue, DateTime.MaxValue);
            }
        }
    }
}
