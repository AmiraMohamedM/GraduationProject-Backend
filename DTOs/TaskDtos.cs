using System.ComponentModel.DataAnnotations;

namespace grad.DTOs
{

    public class MyCourseTabResponseDto
    {
        public IEnumerable<CourseCardDto> Ongoing { get; set; } = new List<CourseCardDto>();
        public IEnumerable<CourseCardDto> Upcoming { get; set; } = new List<CourseCardDto>();
        public IEnumerable<CourseCardDto> Completed { get; set; } = new List<CourseCardDto>();
    }

    public class CourseCardDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string? ScheduleLabel { get; set; }
        public int ProgressPercent { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public string AcademicLevel { get; set; } = string.Empty;
        public int AcademicYear { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

  

    public class CreateTaskDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }


        public bool IsRecurring { get; set; } = false;

        public string? RecurrenceFrequency { get; set; }

        public int RecurrenceInterval { get; set; } = 1;


        public string? RecurringDays { get; set; }
    }

    public class UpdateTaskDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }
        public DateTime? Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool? IsRecurring { get; set; }
        public string? RecurrenceFrequency { get; set; }
        public int? RecurrenceInterval { get; set; }
        public string? RecurringDays { get; set; }
        public bool? IsCompleted { get; set; }
    }



    public class TaskResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }

        public bool IsRecurring { get; set; }
        public string? RecurrenceFrequency { get; set; }
        public int RecurrenceInterval { get; set; }
        public string? RecurringDays { get; set; }

        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

 

    public class HomeworkCalendarResponseDto
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public IEnumerable<int> DaysWithSubmissions { get; set; } = new List<int>();

        public IEnumerable<TaskResponseDto> TasksThisMonth { get; set; } = new List<TaskResponseDto>();

        public IEnumerable<HomeworkCalendarItemDto> Submissions { get; set; } = new List<HomeworkCalendarItemDto>();
    }

    public class HomeworkCalendarItemDto
    {
        public int SubmissionId { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public bool IsReviewed { get; set; }
        public string? Grade { get; set; }
    }

  

    public class TasksByDateResponseDto
    {
        public DateTime Date { get; set; }
        public IEnumerable<TaskResponseDto> Tasks { get; set; } = new List<TaskResponseDto>();
        public IEnumerable<HomeworkCalendarItemDto> Homeworks { get; set; } = new List<HomeworkCalendarItemDto>();
    }
}
