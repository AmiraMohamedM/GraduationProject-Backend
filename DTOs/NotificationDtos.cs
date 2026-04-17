
namespace grad.DTOs
{

    public class NotificationResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

      
        public string Type { get; set; } = string.Empty;

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TimeAgo { get; set; } = string.Empty;
    }

   
    public class MarkReadResponseDto
    {
        public bool Success { get; set; }
        public int UnreadCount { get; set; }
    }
}
