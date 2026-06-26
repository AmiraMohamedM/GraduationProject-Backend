namespace grad.DTOs
{
    public class CourseQuizSummaryDto
    {
        public int QuizNumber { get; set; }
        public int SessionId { get; set; }
        public int QuizId { get; set; }
        public string Title { get; set; }
        public bool AlreadyPassed { get; set; }
        public DateTime? CanRetakeAt { get; set; }

    }
}
