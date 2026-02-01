using System;
using Kalendarz1.HandlowiecDashboard.Constants;

namespace Kalendarz1.HandlowiecDashboard.Models
{
    /// <summary>
    /// Cel sprzedażowy handlowca na dany miesiąc
    /// </summary>
    public class CelHandlowca
    {
        public int Id { get; set; }
        public string Handlowiec { get; set; }
        public int Rok { get; set; }
        public int Miesiac { get; set; }
        public decimal CelWartoscZl { get; set; }
        public decimal CelKg { get; set; }
        public decimal CelLiczbaKlientow { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public string UtworzonyPrzez { get; set; }
    }

    /// <summary>
    /// Realizacja celu - aktualne wyniki vs cel
    /// </summary>
    public class RealizacjaCelu
    {
        public string Handlowiec { get; set; }
        public int Rok { get; set; }
        public int Miesiac { get; set; }

        // Cele
        public decimal CelWartoscZl { get; set; }
        public decimal CelKg { get; set; }
        public decimal CelLiczbaKlientow { get; set; }

        // Realizacja
        public decimal AktualnaWartoscZl { get; set; }
        public decimal AktualneKg { get; set; }
        public int AktualnaLiczbaKlientow { get; set; }

        // Obliczane procenty realizacji (0-100+)
        public double RealizacjaWartoscProcent => CelWartoscZl > 0
            ? (double)(AktualnaWartoscZl / CelWartoscZl * 100)
            : 0;

        public double RealizacjaKgProcent => CelKg > 0
            ? (double)(AktualneKg / CelKg * 100)
            : 0;

        public double RealizacjaKlientowProcent => CelLiczbaKlientow > 0
            ? (double)(AktualnaLiczbaKlientow / CelLiczbaKlientow * 100)
            : 0;

        // Formatowane teksty do wyświetlenia
        public string WartoscTekst => $"{AktualnaWartoscZl:N0} / {CelWartoscZl:N0} zł";
        public string KgTekst => $"{AktualneKg:N0} / {CelKg:N0} kg";
        public string KlienciTekst => $"{AktualnaLiczbaKlientow} / {CelLiczbaKlientow:N0}";
        public string RealizacjaProcentTekst => $"{RealizacjaWartoscProcent:F1}%";

        // Kolory w zależności od realizacji
        public string KolorWartosci => GetKolorRealizacji(RealizacjaWartoscProcent);
        public string KolorKg => GetKolorRealizacji(RealizacjaKgProcent);
        public string KolorKlientow => GetKolorRealizacji(RealizacjaKlientowProcent);

        private string GetKolorRealizacji(double procent)
        {
            return procent switch
            {
                >= 100 => BusinessConstants.Kolory.Sukces,
                >= 80 => BusinessConstants.Kolory.Ostrzezenie,
                >= 50 => BusinessConstants.Kolory.Uwaga,
                _ => BusinessConstants.Kolory.Niebezpieczenstwo
            };
        }

        // Analiza czasowa
        public int DzienMiesiaca => DateTime.Now.Day;
        public int DniWMiesiacu => DateTime.DaysInMonth(Rok, Miesiac);
        public int DniDoKoncaMiesiaca => Math.Max(0, DniWMiesiacu - DzienMiesiaca);

        public int DniRoboczychDoKonca
        {
            get
            {
                int dni = 0;
                var dzis = DateTime.Now.Date;
                var koniecMiesiaca = new DateTime(Rok, Miesiac, DniWMiesiacu);

                for (var d = dzis; d <= koniecMiesiaca; d = d.AddDays(1))
                {
                    if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                        dni++;
                }
                return dni;
            }
        }

        // Ile dziennie trzeba sprzedać aby osiągnąć cel
        public decimal DziennyCelDoNadrobienia =>
            DniRoboczychDoKonca > 0 && CelWartoscZl > AktualnaWartoscZl
                ? (CelWartoscZl - AktualnaWartoscZl) / DniRoboczychDoKonca
                : 0;

        public string PrognozaTekst
        {
            get
            {
                if (RealizacjaWartoscProcent >= 100)
                    return "Cel osiagniety!";
                if (DniRoboczychDoKonca == 0)
                    return "Ostatni dzien miesiaca!";
                if (DziennyCelDoNadrobienia > 0)
                    return $"Potrzebujesz {DziennyCelDoNadrobienia:N0} zl/dzien roboczy";
                return "Brak danych do prognozy";
            }
        }

        // Prognoza czy cel zostanie osiągnięty (na podstawie średniej dziennej)
        public decimal SredniaDziennaDoTejPory => DzienMiesiaca > 0
            ? AktualnaWartoscZl / DzienMiesiaca
            : 0;

        public decimal PrognozowanaNaKoniecMiesiaca => SredniaDziennaDoTejPory * DniWMiesiacu;

        public bool PrognozaOsiagniecaCelu => PrognozowanaNaKoniecMiesiaca >= CelWartoscZl;
    }
}
