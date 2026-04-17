namespace grad.DTOs
{
    public class HomeResponseDto
    {
        public string StudentName { get; set; } = string.Empty; 
        public StatisticsDto Statistics { get; set; } = new();
        public List<LessonDto> PopularLessons { get; set; } = new();
        public List<EventDto> TodayEvents { get; set; } = new();
    }

    public class StatisticsDto
    {
        public int Absence { get; set; }
        public int Tasks { get; set; }
        public int Quiz { get; set; }
    }

    public class LessonDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; 
        public int LessonCount { get; set; } 
        public string Duration { get; set; } = "0h 0m"; 
        public decimal Rating { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class EventDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; } 
    }
}