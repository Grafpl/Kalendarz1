using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Ceny pasz i surowców (MATIF, etc.)
    /// </summary>
    public class IntelFeedPrice
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Commodity { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; }
        public string Market { get; set; }
        public DateTime CreatedAt { get; set; }

        // Computed properties
        public string FormattedValue => $"{Value:N2} {Unit}";
        public string FormattedDate => Date.ToString("dd.MM.yyyy");

        public string CommodityIcon => Commodity switch
        {
            "Kukurydza" => "🌽",
            "Pszenica" => "🌾",
            "Rzepak" => "🌻",
            "Soja" => "🫘",
            _ => "📦"
        };

        public string CommodityDisplay => $"{CommodityIcon} {Commodity}";
    }
}
