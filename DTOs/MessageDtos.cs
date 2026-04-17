
namespace grad.DTOs
{

    public class ContactDto
    {
        public Guid StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarInitials { get; set; }

        public string LastMessage { get; set; } = string.Empty;

        public DateTime LastMessageAt { get; set; }
        public string TimeAgo { get; set; } = string.Empty;

        public int UnreadCount { get; set; }
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;

        public bool IsFromModerator { get; set; }

        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }

  
    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
    }
}
