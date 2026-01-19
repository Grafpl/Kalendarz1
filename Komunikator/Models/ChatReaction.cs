using System;

namespace Kalendarz1.Komunikator.Models
{
    /// <summary>
    /// Model reakcji na wiadomość
    /// </summary>
    public class ChatReaction
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Emoji { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Dostępne reakcje emoji
    /// </summary>
    public static class ReactionEmojis
    {
        public static readonly string[] All = { "👍", "❤️", "😂", "😮", "😢", "😡" };

        public const string ThumbsUp = "👍";
        public const string Heart = "❤️";
        public const string Laugh = "😂";
        public const string Wow = "😮";
        public const string Sad = "😢";
        public const string Angry = "😡";
    }
}
