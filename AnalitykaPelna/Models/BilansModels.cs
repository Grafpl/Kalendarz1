using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kalendarz1.AnalitykaPelna.Models
{
    public class BilansDzien
    {
        public DateTime Data { get; set; }
        public string DzienTygodnia => Data.ToString("dddd", new CultureInfo("pl-PL"));
        public decimal Produkcja { get; set; }
        public decimal Sprzedaz { get; set; }
        public decimal Prognoza { get; set; }
        public decimal Wartosc { get; set; }

        public decimal RoznicaProdSprz => Produkcja - Sprzedaz;
        public decimal MapeProc => Prognoza <= 0 ? 0
            : Math.Abs(Sprzedaz - Prognoza) / Prognoza * 100m;
        public decimal RotacjaProc => Produkcja == 0 ? 0
            : Sprzedaz / Produkcja * 100m;

        public bool Anomalia { get; set; }
        public List<BilansSzczegolSprzedazy> SzczegolySprzedazy { get; set; } = new();
        public List<BilansSzczegolProdukcji> SzczegolyProdukcji { get; set; } = new();
    }

    public class BilansSzczegolSprzedazy
    {
        public string NazwaKontrahenta { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string KodTowaru { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public decimal Ilosc { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc => Ilosc * Cena;
        public string NumerDokumentu { get; set; } = "";
    }

    public class BilansSzczegolProdukcji
    {
        public string KodTowaru { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public decimal Ilosc { get; set; }
        public string NumerDokumentu { get; set; } = "";
    }

    public class BilansRanking
    {
        public int Pozycja { get; set; }
        public string Klucz { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal Ilosc { get; set; }
        public decimal Wartosc { get; set; }
        public decimal ProcentOgolu { get; set; }
    }

    public class BilansSurowyRekord
    {
        public DateTime Data { get; set; }
        public string TypOperacji { get; set; } = "";  // PRODUKCJA / SPRZEDAZ
        public string KodTowaru { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public decimal Ilosc { get; set; }
        public decimal Cena { get; set; }
        public string NazwaKontrahenta { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public string NumerDokumentu { get; set; } = "";
    }
}
