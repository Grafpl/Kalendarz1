using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Ceny benchmark EU - porównanie krajów
    /// </summary>
    public class IntelEuBenchmark
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Country { get; set; }
        public decimal PricePer100kg { get; set; }
        public decimal? ChangeMonthPercent { get; set; }
        public DateTime CreatedAt { get; set; }

        // Computed properties - używamy 2-literowych kodów ISO (PL, DE, FR, etc.)
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
            "BR" => "🇧🇷",
            "US" => "🇺🇸",
            _ => "🌍"
        };

        public string CountryName => Country switch
        {
            "PL" => "Polska",
            "DE" => "Niemcy",
            "FR" => "Francja",
            "NL" => "Holandia",
            "IT" => "Włochy",
            "ES" => "Hiszpania",
            "HU" => "Węgry",
            "UA" => "Ukraina",
            "BR" => "Brazylia",
            "US" => "USA",
            _ => Country
        };

        public string FormattedPrice => $"{PricePer100kg:N1} EUR/100kg";

        public string ChangeDisplay => ChangeMonthPercent.HasValue
            ? $"{(ChangeMonthPercent.Value >= 0 ? "+" : "")}{ChangeMonthPercent.Value:N1}%"
            : "b/d";

        public string ChangeColor => ChangeMonthPercent switch
        {
            > 0 => "#22C55E",  // green
            < 0 => "#EF4444",  // red
            _ => "#94A3B8"     // gray
        };

        public string CountryDisplay => $"{CountryFlag} {CountryName}";
    }
}
