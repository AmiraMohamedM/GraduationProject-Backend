namespace grad.Models
{
    public class LessonFile
    {
        public int Id { get; set; }

        public int CourseSessionId { get; set; }

        public string FileUrl { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string FileType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public CourseSession CourseSession { get; set; } = null!;
    }
}
