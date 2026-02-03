using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>
    /// Artykuł/news z informacjami rynkowymi
    /// </summary>
    public class IntelArticle
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public string AiAnalysis { get; set; }
        public DateTime PublishDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string Tags { get; set; }

        // Computed properties for UI
        public string SeverityIcon => Severity switch
        {
            "critical" => "🔴",
            "warning" => "🟠",
            "positive" => "🟢",
            _ => "🔵"
        };

        public string CategoryBadge => Category switch
        {
            "HPAI" => "🦠 HPAI",
            "Konkurencja" => "🏢 Konkurencja",
            "Ceny" => "💰 Ceny",
            "Eksport" => "🌍 Eksport",
            "Regulacje" => "📜 Regulacje",
            "Analizy" => "📊 Analizy",
            "Swiat" => "🌐 Świat",
            "Klienci" => "🛒 Klienci",
            "Pogoda" => "🌡️ Pogoda",
            _ => $"📄 {Category}"
        };

        public string FormattedDate => PublishDate.ToString("dd.MM.yyyy");

        public string ShortBody => Body?.Length > 200
            ? Body.Substring(0, 200) + "..."
            : Body;

        public bool HasAiAnalysis => !string.IsNullOrWhiteSpace(AiAnalysis);

        public bool HasTags => !string.IsNullOrWhiteSpace(Tags);
    }
}
