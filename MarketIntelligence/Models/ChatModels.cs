using System;

namespace Kalendarz1.MarketIntelligence.Models
{
    /// <summary>Sesja chatu (intel_ChatSessions) — kontener rozmowy.</summary>
    public class IntelChatSession
    {
        public int Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string? Summary { get; set; }       // AI generated po EndSession
        public string? KeyTopics { get; set; }     // JSON array
        public string? OpenQuestions { get; set; } // niedokończone wątki
        public int MessageCount { get; set; }

        public string DisplayDate => StartedAt.ToString("dd.MM HH:mm");
        public bool IsActive => EndedAt == null;
    }

    /// <summary>Pojedyncza wiadomość w sesji (intel_ChatMessages).</summary>
    public class IntelChatMessage
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public string Role { get; set; } = "user"; // user | assistant
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public string? ReferencedStoryIds { get; set; }
        public string? ReferencedArticleIds { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public int? CacheReadTokens { get; set; }
    }
}
