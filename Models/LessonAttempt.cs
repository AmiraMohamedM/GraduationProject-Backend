namespace grad.Models
{
    public class LessonAttempt
    {
        public int Id { get; set; }

        public Guid StudentId { get; set; }
        public int CourseSessionId { get; set; }

        public int Score { get; set; }
        public bool Passed { get; set; }

        public DateTime TakenAt { get; set; }
    }
}