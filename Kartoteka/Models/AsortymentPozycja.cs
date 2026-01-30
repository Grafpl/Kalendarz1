using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class AsortymentPozycja
    {
        public string ProduktKod { get; set; }
        public string ProduktNazwa { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public decimal SredniaCena { get; set; }
        public int LiczbaFaktur { get; set; }
        public DateTime OstatniaSprzedaz { get; set; }

        public string ProduktPelny => string.IsNullOrEmpty(ProduktNazwa)
            ? ProduktKod
            : $"{ProduktKod} - {ProduktNazwa}";
    }
}
