namespace grad.DTOs
{
 
    public class SubjectListItemDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AcademicLevel { get; set; } = string.Empty;
        public int AcademicYear { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string TeacherSubject { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

    public class CourseSessionsResponseDto
    {
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string AcademicLevel { get; set; } = string.Empty;
        public int AcademicYear { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string TeacherSubject { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }

        public IEnumerable<SessionItemDto> Sessions { get; set; } = new List<SessionItemDto>();
    }


    public class SessionItemDto
    {
        public int SessionId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int AvailableDays { get; set; }

        public int MaxViews { get; set; }

        public int ViewsUsed { get; set; }

      
        public int ViewsRemaining { get; set; }
        public bool IsWatched { get; set; }
        public double WatchProgressPercent { get; set; }
        public bool HasAttachment { get; set; }
        public string? AttachmentUrl { get; set; }
        public bool HasHomework { get; set; }
        public string? HomeworkUrl { get; set; }
        public bool HasEntryTest { get; set; }
        public bool? EntryTestPassed { get; set; }
        public decimal? EntryTestBestScore { get; set; }
        public bool HomeworkSubmitted { get; set; }
        public string? HomeworkGrade { get; set; }
    }
}
