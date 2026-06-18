using System.Text.Json.Serialization;

namespace grad.Models
{
    public class Photo
    {
        public int Id { get; set; }

        public required string Url { get; set; }

        public string? PublicId { get; set; }

        public Guid UserId { get; set; }

        [JsonIgnore]
        public ApplicationUser User { get; set; } = null!;

    }
}