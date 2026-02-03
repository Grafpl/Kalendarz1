using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Ogniska HPAI (ptasia grypa)
    /// </summary>
    public class IntelHpaiOutbreak
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public int? BirdsAffected { get; set; }
        public int OutbreakCount { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        // Computed properties
        public string FormattedDate => Date.ToString("dd.MM.yyyy");

        public string CountryFlag => Country switch
        {
            "PL" => "🇵🇱",
            "DE" => "🇩🇪",
            "FR" => "🇫🇷",
            "NL" => "🇳🇱",
            "IT" => "🇮🇹",
            "ES" => "🇪🇸",
            "HU" => "🇭🇺",
            "UA" => "🇺🇦",
            _ => "🌍"
        };

        public string BirdsAffectedDisplay => BirdsAffected.HasValue
            ? $"{BirdsAffected.Value:N0} ptaków"
            : "b/d";

        public string RegionDisplay => $"{CountryFlag} {Region}";

        public bool IsLocalRisk => Country == "PL" &&
            (Region.Contains("łódzkie") || Region.Contains("mazowieckie") || Region.Contains("wielkopolskie"));
    }
}
