using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Konkurencja - firmy drobiowe
    /// </summary>
    public class IntelCompetitor
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public string Country { get; set; }
        public string Revenue { get; set; }
        public string DailyCapacity { get; set; }
        public string Notes { get; set; }
        public DateTime LastUpdated { get; set; }

        // Computed properties
        public string CountryFlag => Country switch
        {
            "PL" => "🇵🇱",
            "DE" => "🇩🇪",
            "FR" => "🇫🇷",
            "NL" => "🇳🇱",
            "UAE" => "🇦🇪",
            "CN" => "🇨🇳",
            "TH" => "🇹🇭",
            _ => "🌍"
        };

        public string NameDisplay => $"{CountryFlag} {Name}";

        public string OwnershipDisplay => !string.IsNullOrEmpty(Owner)
            ? $"Właściciel: {Owner}"
            : "Właściciel nieznany";
    }
}
