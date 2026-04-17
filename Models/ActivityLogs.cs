using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class ActivityLogs
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("User")]
        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
    }
}