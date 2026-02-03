using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Ceny skupu i rynkowe drobiu
    /// </summary>
    public class IntelPrice
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string PriceType { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }

        // Computed properties
        public string FormattedValue => $"{Value:N2} {Unit}";
        public string FormattedDate => Date.ToString("dd.MM.yyyy");

        public string PriceTypeDisplay => PriceType switch
        {
            "WolnyRynek" => "Wolny rynek",
            "Kontraktacja" => "Kontraktacja",
            "TuszkaHurt" => "Tuszka hurt",
            "Filet" => "Filet z piersi",
            "Udko" => "Udko",
            "Skrzydlo" => "Skrzydło",
            "Podudzie" => "Podudzie",
            "Cwiartka" => "Ćwiartka",
            "Noga" => "Noga",
            "Korpus" => "Korpus",
            _ => PriceType
        };
    }
}
