namespace grad.DTOs
{
    public class CreateSessionDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Duration { get; set; }
        public string Description { get; set; }
        public string VideoUrl { get; set; }
        public string HomeworkUrl { get; set; }
    }
}