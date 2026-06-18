using System.ComponentModel.DataAnnotations;

namespace grad.Models
{
    public class CourseSession
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }

        public string Title { get; set; } = string.Empty;

        public int AvailableDays { get; set; }

        public int MaxViews { get; set; }

        public string? HomeworkFileUrl { get; set; }

        public string? HomeworkFileName { get; set; }

        public string? HomeworkFileType { get; set; }

        public long? HomeworkFileSize { get; set; }

        public bool HasEntryTest { get; set; }

        public int? EntryTestId { get; set; }
        public Quiz? EntryTest { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<LessonFile> Files { get; set; } = new List<LessonFile>();
    }
}