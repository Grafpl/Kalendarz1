using System;
using System.Collections.Generic;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>Skonsolidowany wątek (intel_Stories) — np. „Przejęcie Cedrob przez ADQ".</summary>
    public class IntelStory
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string StoryType { get; set; } = "other";   // hpai_outbreak|price_movement|competitor_action|regulation|export_event|customer_event|other
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public string Status { get; set; } = "developing";  // developing|stable|closed
        public int Severity { get; set; } = 3;              // 1-5
        public int PoultryRelevance { get; set; } = 3;      // 1-5
        public string? BusinessImpact { get; set; }
        public string? EntitiesJson { get; set; }
        public string? LastDigest { get; set; }
        public DateTime? LastDigestAt { get; set; }
        public int ArticleCount { get; set; }

        // ── Computed (UI) ──
        public string TypeEmoji => StoryType switch
        {
            "hpai_outbreak" => "🦠",
            "price_movement" => "💰",
            "competitor_action" => "🏭",
            "regulation" => "📜",
            "export_event" => "🌍",
            "customer_event" => "👥",
            _ => "📰"
        };

        public string SeverityIcon => Severity >= 4 ? "🔴" : Severity == 3 ? "🟡" : "🟢";

        public string AgeDisplay
        {
            get
            {
                var days = (int)(DateTime.Now - FirstSeenAt).TotalDays;
                if (days <= 0) return "dziś";
                if (days == 1) return "od wczoraj";
                return $"rozwija się od {days} dni";
            }
        }

        public string LastUpdatedDisplay
        {
            get
            {
                var delta = DateTime.Now - LastUpdatedAt;
                if (delta.TotalHours < 1) return "przed chwilą";
                if (delta.TotalHours < 24 && LastUpdatedAt.Date == DateTime.Today) return $"dziś {LastUpdatedAt:HH:mm}";
                if (LastUpdatedAt.Date == DateTime.Today.AddDays(-1)) return "wczoraj";
                return LastUpdatedAt.ToString("dd.MM.yyyy");
            }
        }

        public string DigestPreview =>
            string.IsNullOrEmpty(LastDigest) ? "(brak digestu — wygeneruje się przy następnym pełnym pobraniu)"
            : LastDigest.Length > 220 ? LastDigest.Substring(0, 220) + "…" : LastDigest;

        public bool HasBusinessImpact => !string.IsNullOrWhiteSpace(BusinessImpact);
    }

    /// <summary>Tracked entity (intel_Entities) — konkurent/klient/dostawca/regulator/region/towar.</summary>
    public class IntelEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string EntityType { get; set; } = "other";
        public string? Aliases { get; set; }
        public bool IsTracked { get; set; } = true;
        public string? CustomerCode { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TypeLabel => EntityType switch
        {
            "competitor" => "🏭 Konkurent",
            "customer" => "👥 Klient",
            "supplier" => "🚚 Dostawca",
            "regulator" => "🏛 Regulator",
            "region" => "📍 Region",
            "commodity" => "🌾 Towar",
            "person" => "👤 Osoba",
            _ => "• Inne"
        };

        public string[] AliasArray =>
            string.IsNullOrWhiteSpace(Aliases)
                ? new[] { Name }
                : Aliases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Wzmianka o encji w artykule (intel_EntityMentions).</summary>
    public class IntelEntityMention
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public int ArticleId { get; set; }
        public int? StoryId { get; set; }
        public int Sentiment { get; set; }      // -5..+5
        public string? Context { get; set; }
        public DateTime MentionedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Wynik klastrowania pojedynczego artykułu (z Haiku).</summary>
    public class ClusterDecision
    {
        public int ArticleId { get; set; }
        public int? StoryId { get; set; }
        public bool IsNewStory { get; set; }
        public string? SuggestedNewTitle { get; set; }
        public string? SuggestedStoryType { get; set; }
    }
}
