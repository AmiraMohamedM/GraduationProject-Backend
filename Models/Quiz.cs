using grad.Models;

public class Quiz
{
    public int Id { get; set; }

    public int CourseSessionId { get; set; } 

    public string Title { get; set; }

    public int PassingScore { get; set; }

    public int RetakeIntervalHours { get; set; }

    public ICollection<Question> Questions { get; set; }

    public CourseSession? CourseSession { get; set; }
}