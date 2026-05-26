using System;

namespace Kalendarz1.Customer360.Models
{
    /// <summary>Wiersz portfela klienta — zagregowane dane finansowe per kontrahent (do pulpitu portfela).</summary>
    public class PortfelKlient
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public decimal Limit { get; set; }
        public decimal Obrot12M { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Przeterminowane { get; set; }
        public int MaxDniOpoznienia { get; set; }
        public DateTime? OstatniaFaktura { get; set; }

        public int DniOdOstatniej => OstatniaFaktura.HasValue
            ? (int)(DateTime.Today - OstatniaFaktura.Value.Date).TotalDays : -1;
        public decimal WykorzystanieLimitProc => Limit > 0 ? DoZaplaty / Limit * 100m : 0m;
        public bool PrzekroczonyLimit => Limit > 0 && DoZaplaty > Limit;
        public bool MaPrzeterminowane => Przeterminowane > 0.01m;
    }

    /// <summary>Podsumowanie portfela — KPI na górze pulpitu.</summary>
    public class PortfelPodsumowanie
    {
        public int LiczbaKlientow { get; set; }
        public decimal ObrotPortfela12M { get; set; }
        public decimal SumaPrzeterminowanych { get; set; }
        public int LiczbaZPrzeterminowanymi { get; set; }
        public int LiczbaPrzekroczonyLimit { get; set; }
        public int LiczbaChurnZagrozonych { get; set; }   // brak faktury > 60 dni a wcześniej aktywni
        public decimal ObrotTop10Proc { get; set; }        // % obrotu generowany przez TOP 10 klientów
    }
}
