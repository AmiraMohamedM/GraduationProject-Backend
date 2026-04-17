public class CreateQuizDto
{
    public int CourseId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public int DurationInMinutes { get; set; }
    public List<QuestionDto> Questions { get; set; } 
}

public class QuestionDto
{
    public string Text { get; set; }
    public List<OptionDto> Options { get; set; }
}

public class OptionDto
{
    public string Text { get; set; }
    public bool IsCorrect { get; set; }
}