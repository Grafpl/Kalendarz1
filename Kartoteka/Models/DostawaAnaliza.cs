using System;

namespace Kalendarz1.Kartoteka.Models
{
    /// <summary>
    /// Analiza dostawy - zamówienie z datami produkcji i awizacji
    /// </summary>
    public class ZamowienieDostawy
    {
        public int Id { get; set; }
        public DateTime DataUboju { get; set; }          // Data produkcji
        public DateTime? DataPrzyjazdu { get; set; }     // Data awizacji/odbioru
        public DateTime? DataWydania { get; set; }        // Data wydania (WZ)
        public string Status { get; set; }
        public decimal IloscKg { get; set; }
        public int LiczbaPojemnikow { get; set; }
        public decimal LiczbaPalet { get; set; }
        public string Uwagi { get; set; }
        public int? TransportKursId { get; set; }

        /// <summary>
        /// Dni od produkcji do awizacji (odbioru)
        /// </summary>
        public int? DniDoDostawy =>
            DataPrzyjazdu.HasValue ? (int)(DataPrzyjazdu.Value.Date - DataUboju.Date).TotalDays : null;

        /// <summary>
        /// Dzień tygodnia awizacji
        /// </summary>
        public DayOfWeek? DzienDostawy => DataPrzyjazdu?.DayOfWeek;
    }

    /// <summary>
    /// Podsumowanie statystyk dostaw dla klienta
    /// </summary>
    public class DostawaStatystyki
    {
        public int LiczbaZamowien { get; set; }
        public int LiczbaZrealizowanych { get; set; }
        public int LiczbaAnulowanych { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaPalet { get; set; }
        public int SumaPojemnikow { get; set; }

        // Terminy dostaw
        public double SredniDniDoDostawy { get; set; }
        public int MinDniDoDostawy { get; set; }
        public int MaxDniDoDostawy { get; set; }

        // Preferowane dni (% zamówień na dany dzień)
        public int ZamowieniaPoniedzialek { get; set; }
        public int ZamowieniaWtorek { get; set; }
        public int ZamowieniaSroda { get; set; }
        public int ZamowieniaCzwartek { get; set; }
        public int ZamowieniaPiatek { get; set; }
        public int ZamowieniaSobota { get; set; }

        // Częstotliwość
        public double SredniOdstepDni { get; set; }       // Średni odstęp między zamówieniami
        public int ZamowieniaMiesiecznie { get; set; }     // Średnia zamówień/msc (12m)
    }
}
