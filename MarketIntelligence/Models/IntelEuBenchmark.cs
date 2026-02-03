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

        // Computed properties
        public string CountryFlag => Country switch
        {
            "Polska" => "🇵🇱",
            "Niemcy" => "🇩🇪",
            "Francja" => "🇫🇷",
            "Holandia" => "🇳🇱",
            "Włochy" => "🇮🇹",
            "Hiszpania" => "🇪🇸",
            "Węgry" => "🇭🇺",
            "Ukraina" => "🇺🇦",
            "Brazylia" => "🇧🇷",
            "USA" => "🇺🇸",
            _ => "🌍"
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

        public string CountryDisplay => $"{CountryFlag} {Country}";
    }
}
