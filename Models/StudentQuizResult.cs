using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class StudentQuizResult
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public Guid StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("Quiz")]
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }

        public int Score { get; set; }      
        public int TotalQuestions { get; set; }
        public decimal Percentage { get; set; }
        public bool Passed { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public string? AnswersJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
