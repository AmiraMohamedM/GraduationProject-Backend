namespace grad.DTOs
{
    public class StudentStatsDto
    {
        public Guid StudentId { get; set; }
        public string Name { get; set; }
        public string EducationLevel { get; set; }

        public int TotalLessons { get; set; }
        public int CompletedLessons { get; set; }
        public decimal AvgScore { get; set; }
        public DateTime? LastActive { get; set; }

        public int AbsencePercentage { get; set; }
        public int TasksPercentage { get; set; }
        public int QuizPercentage { get; set; }
        public int TotalGradesCompleted { get; set; }
    }
}