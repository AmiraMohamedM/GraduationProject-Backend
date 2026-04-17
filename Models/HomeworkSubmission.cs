using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class HomeworkSubmission
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public Guid StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("CourseSession")]
        public int SessionId { get; set; }
        public CourseSession CourseSession { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public string? Grade { get; set; }
        public string? TeacherComment { get; set; }
        public bool IsReviewed { get; set; } = false;
    }
}
