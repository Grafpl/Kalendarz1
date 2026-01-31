using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Godzina awizacji (HH:mm)
        /// </summary>
        public string GodzinaAwizacji =>
            DataPrzyjazdu.HasValue && DataPrzyjazdu.Value.TimeOfDay.TotalMinutes > 0
                ? DataPrzyjazdu.Value.ToString("HH:mm")
                : null;
    }

    /// <summary>
    /// Analiza transportu - kierowcy, pojazdy, współtransportowani klienci
    /// </summary>
    public class TransportAnaliza
    {
        public int LiczbaKursow { get; set; }
        public List<TransportOsobaStats> Kierowcy { get; set; } = new();
        public List<TransportPojazdStats> Pojazdy { get; set; } = new();
        public List<TransportTrasaStats> Trasy { get; set; } = new();
        public List<TransportWspolKlient> WspolKlienci { get; set; } = new();
    }

    public class TransportOsobaStats
    {
        public string Nazwa { get; set; }
        public string Telefon { get; set; }
        public int LiczbaKursow { get; set; }
    }

    public class TransportPojazdStats
    {
        public string Nazwa { get; set; }
        public int PaletyH1 { get; set; }
        public int LiczbaKursow { get; set; }
    }

    public class TransportTrasaStats
    {
        public string Nazwa { get; set; }
        public int LiczbaKursow { get; set; }
    }

    public class TransportWspolKlient
    {
        public string Nazwa { get; set; }
        public int LiczbaWspolnychKursow { get; set; }
    }
}
