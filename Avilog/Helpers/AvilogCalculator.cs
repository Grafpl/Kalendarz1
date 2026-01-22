using System;
using System.Collections.Generic;
using System.Linq;
using Kalendarz1.Avilog.Models;

namespace Kalendarz1.Avilog.Helpers
{
    /// <summary>
    /// Klasa pomocnicza do obliczeń rozliczeń Avilog
    /// </summary>
    public static class AvilogCalculator
    {
        /// <summary>
        /// Oblicza podsumowanie dla listy kursów
        /// </summary>
        public static AvilogSummaryModel CalculateSummary(IEnumerable<AvilogKursModel> kursy, decimal stawkaZaKg)
        {
            var lista = kursy?.ToList() ?? new List<AvilogKursModel>();

            if (!lista.Any())
            {
                return new AvilogSummaryModel { StawkaZaKg = stawkaZaKg };
            }

            var summary = new AvilogSummaryModel
            {
                DataOd = lista.Min(k => k.CalcDate),
                DataDo = lista.Max(k => k.CalcDate),
                StawkaZaKg = stawkaZaKg,

                LiczbaKursow = lista.Count,
                LiczbaZestawow = lista.Select(k => $"{k.CarID}-{k.TrailerID}").Distinct().Count(),
                LiczbaDni = lista.Select(k => k.CalcDate.Date).Distinct().Count(),

                SumaSztuk = lista.Sum(k => k.SztukiRazem),
                SumaUpadkowSzt = lista.Sum(k => k.SztukiPadle),

                SumaBrutto = lista.Sum(k => k.BruttoHodowcy),
                SumaTara = lista.Sum(k => k.TaraHodowcy),
                SumaNetto = lista.Sum(k => k.NettoHodowcy),
                SumaUpadkowKg = lista.Sum(k => k.UpadkiKg),
                SumaRoznicaKg = lista.Sum(k => k.RoznicaKg),

                SumaKM = lista.Sum(k => k.DystansKM),
                SumaGodzin = lista.Sum(k => k.CzasUslugiGodziny)
            };

            return summary;
        }

        /// <summary>
        /// Oblicza podsumowanie dla listy dni
        /// </summary>
        public static AvilogSummaryModel CalculateSummaryFromDays(IEnumerable<AvilogDayModel> dni, decimal stawkaZaKg)
        {
            var lista = dni?.Where(d => !d.JestSuma).ToList() ?? new List<AvilogDayModel>();

            if (!lista.Any())
            {
                return new AvilogSummaryModel { StawkaZaKg = stawkaZaKg };
            }

            var summary = new AvilogSummaryModel
            {
                DataOd = lista.Min(d => d.Data),
                DataDo = lista.Max(d => d.Data),
                StawkaZaKg = stawkaZaKg,

                LiczbaKursow = lista.Sum(d => d.LiczbaKursow),
                LiczbaZestawow = lista.Sum(d => d.LiczbaZestawow), // Uwaga: może być niedokładne jeśli ten sam zestaw w różne dni
                LiczbaDni = lista.Count,

                SumaSztuk = lista.Sum(d => d.SumaSztuk),
                SumaUpadkowSzt = lista.Sum(d => d.SumaUpadkowSzt),

                SumaBrutto = lista.Sum(d => d.SumaBrutto),
                SumaTara = lista.Sum(d => d.SumaTara),
                SumaNetto = lista.Sum(d => d.SumaNetto),
                SumaUpadkowKg = lista.Sum(d => d.SumaUpadkowKg),
                SumaRoznicaKg = lista.Sum(d => d.SumaRoznicaKg),

                SumaKM = lista.Sum(d => d.SumaKM),
                SumaGodzin = lista.Sum(d => d.SumaGodzin)
            };

            return summary;
        }

        /// <summary>
        /// Tworzy wiersz sumy dla tabeli dziennej
        /// </summary>
        public static AvilogDayModel CreateSumRow(IEnumerable<AvilogDayModel> dni, decimal stawkaZaKg)
        {
            var lista = dni?.Where(d => !d.JestSuma).ToList() ?? new List<AvilogDayModel>();

            return new AvilogDayModel
            {
                LP = 0,
                Data = DateTime.Today,
                DzienTygodnia = "SUMA",
                JestSuma = true,
                StawkaZaKg = stawkaZaKg,

                LiczbaKursow = lista.Sum(d => d.LiczbaKursow),
                LiczbaZestawow = lista.Sum(d => d.LiczbaZestawow),

                SumaSztuk = lista.Sum(d => d.SumaSztuk),
                SumaUpadkowSzt = lista.Sum(d => d.SumaUpadkowSzt),

                SumaBrutto = lista.Sum(d => d.SumaBrutto),
                SumaTara = lista.Sum(d => d.SumaTara),
                SumaNetto = lista.Sum(d => d.SumaNetto),
                SumaUpadkowKg = lista.Sum(d => d.SumaUpadkowKg),

                SumaKM = lista.Sum(d => d.SumaKM),
                SumaGodzin = lista.Sum(d => d.SumaGodzin),

                LiczbaBrakowKM = lista.Sum(d => d.LiczbaBrakowKM),
                LiczbaBrakowGodzin = lista.Sum(d => d.LiczbaBrakowGodzin),
                MaBrakiDanych = lista.Any(d => d.MaBrakiDanych)
            };
        }

        /// <summary>
        /// Grupuje kursy po dniach
        /// </summary>
        public static Dictionary<DateTime, List<AvilogKursModel>> GroupByDay(IEnumerable<AvilogKursModel> kursy)
        {
            return kursy?
                .GroupBy(k => k.CalcDate.Date)
                .ToDictionary(g => g.Key, g => g.ToList())
                ?? new Dictionary<DateTime, List<AvilogKursModel>>();
        }

        /// <summary>
        /// Pobiera zakres dat dla bieżącego tygodnia (pon-pt)
        /// </summary>
        public static (DateTime Od, DateTime Do) GetCurrentWeekRange()
        {
            var today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff);
            var friday = monday.AddDays(4);

            // Jeśli dziś jest weekend, weź poprzedni tydzień
            if (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
            {
                monday = monday.AddDays(-7);
                friday = friday.AddDays(-7);
            }

            return (monday, friday);
        }

        /// <summary>
        /// Pobiera zakres dat dla poprzedniego tygodnia (pon-pt)
        /// </summary>
        public static (DateTime Od, DateTime Do) GetPreviousWeekRange()
        {
            var (currentMon, _) = GetCurrentWeekRange();
            var prevMonday = currentMon.AddDays(-7);
            var prevFriday = prevMonday.AddDays(4);
            return (prevMonday, prevFriday);
        }

        /// <summary>
        /// Pobiera zakres dat dla wybranego miesiąca
        /// </summary>
        public static (DateTime Od, DateTime Do) GetMonthRange(int year, int month)
        {
            var first = new DateTime(year, month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            return (first, last);
        }

        /// <summary>
        /// Formatuje kwotę jako walutę
        /// </summary>
        public static string FormatCurrency(decimal value)
        {
            return $"{value:N2} zł";
        }

        /// <summary>
        /// Formatuje wagę w kg
        /// </summary>
        public static string FormatWeight(decimal value)
        {
            return $"{value:N0} kg";
        }

        /// <summary>
        /// Formatuje czas w godzinach
        /// </summary>
        public static string FormatHours(decimal value)
        {
            return $"{value:N2} h";
        }

        /// <summary>
        /// Waliduje dane kursu i zwraca listę ostrzeżeń
        /// </summary>
        public static List<string> ValidateKurs(AvilogKursModel kurs)
        {
            var warnings = new List<string>();

            if (kurs.StartKM == 0)
                warnings.Add("Brak StartKM");
            if (kurs.StopKM == 0)
                warnings.Add("Brak StopKM");
            if (kurs.DystansKM < 0)
                warnings.Add("Ujemny dystans KM");
            if (!kurs.PoczatekUslugi.HasValue)
                warnings.Add("Brak początku usługi");
            if (!kurs.KoniecUslugi.HasValue)
                warnings.Add("Brak końca usługi");
            if (kurs.CzasUslugiGodziny > 24)
                warnings.Add("Czas usługi > 24h");
            if (Math.Abs(kurs.RoznicaWagProcent) > 5)
                warnings.Add($"Bardzo duża różnica wag: {kurs.RoznicaWagProcent:N2}%");
            if (kurs.NettoHodowcy == 0)
                warnings.Add("Brak wagi netto hodowcy");
            if (kurs.SztukiLumel == 0)
                warnings.Add("Brak sztuk LUMEL");

            return warnings;
        }

        /// <summary>
        /// Oblicza udziały procentowe hodowców w tonażu
        /// </summary>
        public static List<(string Hodowca, decimal Tonaz, decimal Procent)> CalculateHodowcyUdzialy(IEnumerable<AvilogKursModel> kursy)
        {
            var lista = kursy?.ToList() ?? new List<AvilogKursModel>();
            if (!lista.Any()) return new List<(string, decimal, decimal)>();

            var totalTonaz = lista.Sum(k => k.NettoHodowcy);
            if (totalTonaz == 0) return new List<(string, decimal, decimal)>();

            return lista
                .GroupBy(k => k.HodowcaNazwa)
                .Select(g => (
                    Hodowca: g.Key,
                    Tonaz: g.Sum(k => k.NettoHodowcy),
                    Procent: Math.Round(g.Sum(k => k.NettoHodowcy) / totalTonaz * 100, 2)
                ))
                .OrderByDescending(x => x.Tonaz)
                .ToList();
        }
    }
}
