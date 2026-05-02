using System;

namespace Kalendarz1.AnalizaPrzychoduProdukcji.Models
{
    public class PrzychodRecord
    {
        public string ArticleID { get; set; }
        public string NazwaTowaru { get; set; }
        public string JM { get; set; }
        public int TermID { get; set; }
        public string Terminal { get; set; }
        public decimal Weight { get; set; }              // waga standardowa (deklarowana)
        public DateTime Data { get; set; }
        public DateTime Godzina { get; set; }
        public string OperatorID { get; set; }
        public string Operator { get; set; }
        public decimal Tara { get; set; }
        public string Partia { get; set; }
        public decimal ActWeight { get; set; }           // waga rzeczywista (zmierzona)
        public int Klasa { get; set; }                   // wielkość/klasa palety

        // Różnica między wagą rzeczywistą a standardową
        // dodatnia = "dokładamy" (rzecz. > standard, oddajemy klientowi za darmo)
        // ujemna = "niedowaga" (rzecz. < standard, klient niezadowolony)
        public decimal Roznica => ActWeight - Weight;
        public decimal RoznicaProc => Weight != 0 ? (ActWeight - Weight) / Weight * 100m : 0;
        public bool Dokladamy => Roznica > 0.05m;        // tolerancja 50 g
        public bool Niedowaga => Roznica < -0.05m;
    }

    public class ComboItem
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
    }

    public class ComboItemString
    {
        public string Wartosc { get; set; }
        public string Nazwa { get; set; }
    }

    public class OperatorStats
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public string OperatorID { get; set; }
        public decimal SumaKg { get; set; }
        public int LiczbaWazen { get; set; }
        public int LiczbaAnulacji { get; set; }
        public decimal SredniaKg { get; set; }
        public bool Paletuje { get; set; } // true jeśli głównie ArticleID=40
        // 0..100 — udział vs. lider (do mini-paska sparkline w rankingu)
        public double PctMax { get; set; }
    }

    public class PartiaStats
    {
        public string Partia { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int Liczba { get; set; }
        public int LiczbaAnulacji { get; set; }
        public string Dostawca { get; set; }      // CustomerName z PartiaDostawca
        public string CustomerID { get; set; }
        public string Status { get; set; }         // ikona stanu (np. "🟢 aktywna" / "⚪ historyczna")
        public DateTime? CreateData { get; set; }
        public string PierwszeWaz { get; set; }
        public string OstatnieWaz { get; set; }
    }

    public class TerminalStats
    {
        public int Pozycja { get; set; }
        public int TermID { get; set; }
        public string Nazwa { get; set; }
        public decimal SumaKg { get; set; }
        public int LiczbaWazen { get; set; }
        public decimal SredniaKg { get; set; }
    }

    public class KlasaStats
    {
        public int Klasa { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int Liczba { get; set; }
        public decimal SredniaKg { get; set; }
    }

    public class DzienTygodniaStats
    {
        public int DzienNumer { get; set; }
        public string DzienTygodnia { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SredniaKg { get; set; }
        public int LiczbaDni { get; set; }
    }

    public class ArticleInfo
    {
        public string ID { get; set; }
        public string ShortName { get; set; }
        public string Name { get; set; }
    }

    public class ArticleStats
    {
        public string ArticleID { get; set; }
        public string ShortName { get; set; }
        public string ArticleName { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public decimal KumulacyjnyPct { get; set; }
        public int LiczbaWazen { get; set; }
        public decimal SredniaKg { get; set; }
        public bool TopPareto { get; set; }      // true jeśli w pierwszych ~80% wolumenu
    }

    public class PartiaArticleStats
    {
        public string ArticleID { get; set; }
        public string ArticleName { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int LiczbaWazen { get; set; }
    }

    public class SalesRecord
    {
        public string ArticleID { get; set; }
        public string ArticleName { get; set; }
        public string CustomerID { get; set; }
        public string CustomerName { get; set; }
        public DateTime Data { get; set; }
        public DateTime Godzina { get; set; }
        public decimal Weight { get; set; }
        public decimal ActWeight { get; set; }
        public decimal Price { get; set; }
        public decimal Wartosc { get; set; }      // ActWeight * Price
        public string PartiaIn { get; set; }      // Related_IN
        public string PartiaOut { get; set; }     // P1
        public int? DocNo { get; set; }
        public string OrderNo { get; set; }
    }

    public class SalesStats
    {
        public string ArticleID { get; set; }
        public string ArticleName { get; set; }
        public string CustomerName { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SumaWartosc { get; set; }
        public decimal SredniaCena { get; set; }
        public int LiczbaTransakcji { get; set; }
    }

    public enum OperatorTypeFilter
    {
        Wszyscy = 0,
        TylkoPaletujacy = 1,    // ArticleID=40 dominuje
        TylkoPorcjujacy = 2     // ArticleID != 40
    }

    public class PrzychodFilter
    {
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
        public string ArticleID { get; set; }
        public string OperatorID { get; set; }
        public int? TerminalId { get; set; }
        public string Partia { get; set; }
        public int? Klasa { get; set; }
        public int? GodzinaOd { get; set; }
        public int? GodzinaDo { get; set; }
        public string Dostawca { get; set; }              // CustomerName lub CustomerID
        public OperatorTypeFilter TypOperatora { get; set; } = OperatorTypeFilter.Wszyscy;
    }
}
