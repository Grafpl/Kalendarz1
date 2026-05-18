using System;
using System.Collections.Generic;

namespace Kalendarz1.AnalitykaPelna.Models
{
    public class WazenieRekord
    {
        public DateTime Data { get; set; }
        public DateTime Godzina { get; set; }
        public string ArticleID { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public string OperatorID { get; set; } = "";
        public string Wagowy { get; set; } = "";
        public int TermID { get; set; }
        public string Terminal { get; set; } = "";
        public string Partia { get; set; } = "";
        public decimal Weight { get; set; }       // Standard (norma)
        public decimal ActWeight { get; set; }    // Rzeczywista
        public decimal Roznica => ActWeight - Weight;
        public decimal Tara { get; set; }
        public int Klasa { get; set; }
        public string Hodowca { get; set; } = "";  // dosypiemy z PartiaDostawca
        public string CustomerID { get; set; } = "";
    }

    public class RankingOperatora
    {
        public int Pozycja { get; set; }
        public string OperatorID { get; set; } = "";
        public string Wagowy { get; set; } = "";
        public int LiczbaWazen { get; set; }
        public int LiczbaAnulacji { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SredniaKg { get; set; }
        public decimal ProcentLidera { get; set; }   // dla progress baru
    }

    public class RankingPartii
    {
        public string Partia { get; set; } = "";
        public string Hodowca { get; set; } = "";
        public DateTime PierwszeWazenie { get; set; }
        public DateTime OstatnieWazenie { get; set; }
        public int LiczbaWazen { get; set; }
        public decimal SumaKg { get; set; }
        public List<string> Towary { get; set; } = new();
    }

    public class HeatmapaGodzinowa
    {
        public DateTime Data { get; set; }
        public Dictionary<int, decimal> KgPerGodzina { get; set; } = new();  // 0..23
    }

    public class StatystykaZmian
    {
        public DateTime Data { get; set; }
        public decimal KgZmianaDzienna { get; set; }
        public decimal KgZmianaNocna { get; set; }
        public int LiczbaWazenDzienna { get; set; }
        public int LiczbaWazenNocna { get; set; }
    }
}
