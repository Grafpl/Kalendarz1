using System;

namespace Kalendarz1.Komunikator.Models
{
    /// <summary>
    /// Model wiadomości czatu
    /// </summary>
    public class ChatMessage
    {
        public int Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string ReceiverId { get; set; }
        public string ReceiverName { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsRead => ReadAt.HasValue;
        public MessageType Type { get; set; } = MessageType.Text;

        /// <summary>
        /// Czy wiadomość jest od aktualnego użytkownika
        /// </summary>
        public bool IsFromCurrentUser(string currentUserId) => SenderId == currentUserId;
    }

    public enum MessageType
    {
        Text = 0,
        Image = 1,
        File = 2,
        System = 3
    }
}
