using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Wskaźnik dashboardu (górna belka)
    /// </summary>
    public class DashboardIndicator
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Trend { get; set; }  // "up", "down", "stable"
        public string TrendValue { get; set; }
        public string Category { get; set; }

        // Computed properties
        public string TrendIcon => Trend switch
        {
            "up" => "↑",
            "down" => "↓",
            _ => "→"
        };

        public string TrendColor => Trend switch
        {
            "up" => "#22C55E",    // green
            "down" => "#EF4444",  // red
            _ => "#94A3B8"        // gray
        };

        public string DisplayValue => $"{Value} {Unit}";
    }
}
