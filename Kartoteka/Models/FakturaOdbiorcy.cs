using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class FakturaOdbiorcy
    {
        public int Id { get; set; }
        public int KontrahentId { get; set; }
        public string NumerDokumentu { get; set; }
        public DateTime DataFaktury { get; set; }
        public DateTime TerminPlatnosci { get; set; }
        public decimal Brutto { get; set; }
        public decimal Rozliczono { get; set; }
        public string Typ { get; set; }
        public bool Anulowany { get; set; }

        public decimal DoZaplaty => Brutto - Rozliczono;
        public bool Przeterminowana => DoZaplaty > 0 && TerminPlatnosci < DateTime.Now;
        public int DniPoTerminie => Przeterminowana ? (int)(DateTime.Now - TerminPlatnosci).TotalDays : 0;

        public string Status
        {
            get
            {
                if (Anulowany) return "Anulowana";
                if (DoZaplaty <= 0) return "Zapłacona";
                if (Przeterminowana) return "Przeterminowana";
                return "Nieopłacona";
            }
        }
    }
}
