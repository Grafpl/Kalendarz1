using System;

namespace Kalendarz1.Kartoteka.Models
{
    public class ScoringResult
    {
        public int Id { get; set; }
        public int KlientId { get; set; }

        // Składniki (0-max dla każdego)
        public int TerminowoscPkt { get; set; }   // max 40
        public int HistoriaPkt { get; set; }       // max 20
        public int RegularnoscPkt { get; set; }    // max 20
        public int TrendPkt { get; set; }          // max 10
        public int LimitPkt { get; set; }          // max 10

        // Wynik
        public int ScoreTotal { get; set; }
        public string Kategoria { get; set; }

        // Rekomendacje
        public decimal RekomendacjaLimitu { get; set; }
        public string RekomendacjaOpis { get; set; }

        public DateTime DataObliczenia { get; set; }

        // Właściwości wyświetlania
        public string KategoriaKolor => Kategoria switch
        {
            "Doskonały" => "#10B981",
            "Dobry" => "#34D399",
            "Średni" => "#F59E0B",
            "Słaby" => "#F97316",
            "Krytyczny" => "#EF4444",
            _ => "#6B7280"
        };

        public static string KategoryzujScore(int score) => score switch
        {
            >= 90 => "Doskonały",
            >= 70 => "Dobry",
            >= 50 => "Średni",
            >= 30 => "Słaby",
            _ => "Krytyczny"
        };
    }
}
