using grad.Data;
using grad.DTOs;
using grad.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace grad.Controllers
{
 
    [ApiController]
    [Route("api/student/tasks")]
    [Authorize(Roles = "Student")]
    public class TaskController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

    

        private Guid GetCurrentUserId()
            => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<Student?> GetCurrentStudentAsync()
        {
            var userId = GetCurrentUserId();
            return await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.user_id == userId);
        }

        private static TimeSpan? ParseTime(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return TimeSpan.TryParse(raw, out var ts) ? ts : null;
        }

        private static string? FormatTime(TimeSpan? ts)
        {
            if (ts == null) return null;
            var dt = DateTime.Today.Add(ts.Value);
            return dt.ToString("h:mmtt");
        }

        private static TaskResponseDto ToDto(StudentTask t) => new()
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Date = t.Date,
            StartTime = FormatTime(t.StartTime),
            EndTime = FormatTime(t.EndTime),
            IsRecurring = t.IsRecurring,
            RecurrenceFrequency = t.RecurrenceFrequency,
            RecurrenceInterval = t.RecurrenceInterval,
            RecurringDays = t.RecurringDays,
            IsCompleted = t.IsCompleted,
            CreatedAt = t.CreatedAt
        };

       
        [HttpGet("my-courses")]
        public async Task<IActionResult> GetMyCourses()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var enrollments = await _db.Enrollments
                .AsNoTracking()
                .Include(e => e.Course)
                    .ThenInclude(c => c.Teacher)
                        .ThenInclude(t => t.User)
                .Include(e => e.Course)
                    .ThenInclude(c => c.CourseSessions)
                .Where(e => e.StudentId == student.student_id)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            if (!enrollments.Any())
                return Ok(new MyCourseTabResponseDto());

            var allSessionIds = enrollments
                .SelectMany(e => e.Course.CourseSessions.Select(s => s.Id))
                .ToList();

            var progressBySession = await _db.LessonProgress
                .AsNoTracking()
                .Where(lp => lp.StudentId == student.student_id
                             && allSessionIds.Contains(lp.CourseSessionId))
                .ToDictionaryAsync(lp => lp.CourseSessionId);

            var cards = enrollments.Select(e =>
            {
                var sessions = e.Course.CourseSessions.ToList();
                int total = sessions.Count;
                int completed = sessions.Count(s =>
                    progressBySession.TryGetValue(s.Id, out var lp) && lp.Views > 0);
                int progress = total > 0
                    ? (int)Math.Round((double)completed / total * 100)
                    : 0;

                var teacher = e.Course.Teacher;
                string teacherName = teacher?.User != null
                    ? $"{teacher.User.firstname} {teacher.User.lastname}".Trim()
                    : "N/A";

                string? scheduleLabel = null;
                var lastWatched = progressBySession.Values
                    .Where(lp => sessions.Any(s => s.Id == lp.CourseSessionId))
                    .OrderByDescending(lp => lp.LastWatched)
                    .FirstOrDefault();
                if (lastWatched != null)
                    scheduleLabel = lastWatched.LastWatched.ToLocalTime().ToString("h:mmtt");

                return new CourseCardDto
                {
                    CourseId = e.CourseId,
                    Title = e.Course.Title,
                    TeacherName = teacherName,
                    ScheduleLabel = scheduleLabel,
                    ProgressPercent = progress,
                    TotalSessions = total,
                    CompletedSessions = completed,
                    AcademicLevel = e.Course.AcademicLevel,
                    AcademicYear = e.Course.AcademicYear,
                    EnrolledAt = e.EnrolledAt
                };
            }).ToList();

            return Ok(new MyCourseTabResponseDto
            {
                Ongoing = cards.Where(c => c.ProgressPercent > 0 && c.ProgressPercent < 100),
                Upcoming = cards.Where(c => c.ProgressPercent == 0),
                Completed = cards.Where(c => c.ProgressPercent == 100)
            });
        }

       
        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            if (dto.IsRecurring)
            {
                var validFreq = new[] { "Daily", "Weekly", "Monthly" };
                if (string.IsNullOrWhiteSpace(dto.RecurrenceFrequency)
                    || !validFreq.Contains(dto.RecurrenceFrequency))
                    return BadRequest(new
                    {
                        message = "RecurrenceFrequency must be 'Daily', 'Weekly', or 'Monthly' when IsRecurring is true."
                    });

                if (dto.RecurrenceInterval < 1)
                    return BadRequest(new { message = "RecurrenceInterval must be 1 or greater." });
            }

            var task = new StudentTask
            {
                StudentId = student.student_id,
                Title = dto.Title.Trim(),
                Description = dto.Description?.Trim(),
                Date = dto.Date.Date,
                StartTime = ParseTime(dto.StartTime),
                EndTime = ParseTime(dto.EndTime),
                IsRecurring = dto.IsRecurring,
                RecurrenceFrequency = dto.IsRecurring ? dto.RecurrenceFrequency : null,
                RecurrenceInterval = dto.IsRecurring ? dto.RecurrenceInterval : 1,
                RecurringDays = dto.IsRecurring && dto.RecurrenceFrequency == "Weekly"
                    ? dto.RecurringDays
                    : null,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentTasks.Add(task);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTaskById),
                new { id = task.Id },
                ToDto(task));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var task = await _db.StudentTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.StudentId == student.student_id);

            if (task == null)
                return NotFound(new { message = "Task not found." });

            if (dto.Title != null) task.Title = dto.Title.Trim();
            if (dto.Description != null) task.Description = dto.Description.Trim();
            if (dto.Date.HasValue) task.Date = dto.Date.Value.Date;
            if (dto.StartTime != null) task.StartTime = ParseTime(dto.StartTime);
            if (dto.EndTime != null) task.EndTime = ParseTime(dto.EndTime);
            if (dto.IsCompleted.HasValue) task.IsCompleted = dto.IsCompleted.Value;

            if (dto.IsRecurring.HasValue)
            {
                task.IsRecurring = dto.IsRecurring.Value;
                if (!task.IsRecurring)
                {
                    task.RecurrenceFrequency = null;
                    task.RecurrenceInterval = 1;
                    task.RecurringDays = null;
                }
            }

            if (task.IsRecurring)
            {
                if (dto.RecurrenceFrequency != null) task.RecurrenceFrequency = dto.RecurrenceFrequency;
                if (dto.RecurrenceInterval.HasValue) task.RecurrenceInterval = dto.RecurrenceInterval.Value;
                if (dto.RecurringDays != null) task.RecurringDays = dto.RecurringDays;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(ToDto(task));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var task = await _db.StudentTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.StudentId == student.student_id);

            if (task == null)
                return NotFound(new { message = "Task not found." });

            _db.StudentTasks.Remove(task);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Task deleted." });
        }

      
        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] bool? completed)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var query = _db.StudentTasks
                .AsNoTracking()
                .Where(t => t.StudentId == student.student_id);

            if (completed.HasValue)
                query = query.Where(t => t.IsCompleted == completed.Value);

            var tasks = await query
                .OrderBy(t => t.Date)
                .ThenBy(t => t.StartTime)
                .ToListAsync();

            return Ok(tasks.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var task = await _db.StudentTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.StudentId == student.student_id);

            if (task == null)
                return NotFound(new { message = "Task not found." });

            return Ok(ToDto(task));
        }

        [HttpPut("{id:int}/complete")]
        public async Task<IActionResult> CompleteTask(int id, [FromQuery] bool completed = true)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var task = await _db.StudentTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.StudentId == student.student_id);

            if (task == null)
                return NotFound(new { message = "Task not found." });

            task.IsCompleted = completed;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = completed ? "Task marked as completed." : "Task marked as incomplete." });
        }

       
        [HttpGet("homework-calendar")]
        public async Task<IActionResult> GetHomeworkCalendar()
        {
            var now = DateTime.UtcNow;
            return await GetHomeworkCalendarForMonth(now.Year, now.Month);
        }

        [HttpGet("homework-calendar/{year:int}/{month:int}")]
        public async Task<IActionResult> GetHomeworkCalendarByMonth(int year, int month)
        {
            if (month < 1 || month > 12)
                return BadRequest(new { message = "Month must be between 1 and 12." });

            return await GetHomeworkCalendarForMonth(year, month);
        }


        private async Task<IActionResult> GetHomeworkCalendarForMonth(int year, int month)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var submissions = await _db.HomeworkSubmissions
                .AsNoTracking()
                .Include(h => h.CourseSession)
                    .ThenInclude(cs => cs.Course)
                .Where(h => h.StudentId == student.student_id
                            && h.SubmittedAt >= monthStart
                            && h.SubmittedAt < monthEnd)
                .OrderBy(h => h.SubmittedAt)
                .ToListAsync();

         
            var personalTasks = await _db.StudentTasks
                .AsNoTracking()
                .Where(t => t.StudentId == student.student_id
                            && t.Date >= monthStart
                            && t.Date < monthEnd)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.StartTime)
                .ToListAsync();

            var daysWithSubmissions = submissions
                .Select(h => h.SubmittedAt.Day)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return Ok(new HomeworkCalendarResponseDto
            {
                Year = year,
                Month = month,
                DaysWithSubmissions = daysWithSubmissions,
                TasksThisMonth = personalTasks.Select(ToDto),
                Submissions = submissions.Select(h => new HomeworkCalendarItemDto
                {
                    SubmissionId = h.Id,
                    SessionTitle = h.CourseSession.Title,
                    CourseTitle = h.CourseSession.Course.Title,
                    SubmittedAt = h.SubmittedAt,
                    IsReviewed = h.IsReviewed,
                    Grade = h.Grade
                })
            });
        }

      
        [HttpGet("by-date")]
        public async Task<IActionResult> GetTasksByDate([FromQuery] DateTime date)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc); var dayStart = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day,
                0, 0, 0, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);


            var allTasks = await _db.StudentTasks
                .AsNoTracking()
                .Where(t => t.StudentId == student.student_id
                            && t.Date.Date <= targetDate)
                .ToListAsync();

            var matchingTasks = allTasks.Where(t => TaskOccursOn(t, targetDate)).ToList();

            var submissions = await _db.HomeworkSubmissions
                .AsNoTracking()
                .Include(h => h.CourseSession).ThenInclude(cs => cs.Course)
                .Where(h => h.StudentId == student.student_id
                            && h.SubmittedAt >= dayStart
                            && h.SubmittedAt < dayEnd)
                .OrderBy(h => h.SubmittedAt)
                .ToListAsync();

            return Ok(new TasksByDateResponseDto
            {
                Date = targetDate,
                Tasks = matchingTasks.Select(ToDto),
                Homeworks = submissions.Select(h => new HomeworkCalendarItemDto
                {
                    SubmissionId = h.Id,
                    SessionTitle = h.CourseSession.Title,
                    CourseTitle = h.CourseSession.Course.Title,
                    SubmittedAt = h.SubmittedAt,
                    IsReviewed = h.IsReviewed,
                    Grade = h.Grade
                })
            });
        }



        private static bool TaskOccursOn(StudentTask task, DateTime targetDate)
        {
            var taskDate = task.Date.Date;

            if (!task.IsRecurring)
                return taskDate == targetDate;

            if (targetDate < taskDate) return false;

            switch (task.RecurrenceFrequency)
            {
                case "Daily":
                {
                    int interval = task.RecurrenceInterval < 1 ? 1 : task.RecurrenceInterval;
                    int daysSinceStart = (targetDate - taskDate).Days;
                    return daysSinceStart % interval == 0;
                }

                case "Weekly":
                {
           
                    if (string.IsNullOrEmpty(task.RecurringDays))
                        return false;

                    var dayAbbr = targetDate.DayOfWeek switch
                    {
                        DayOfWeek.Sunday    => "Su",
                        DayOfWeek.Monday    => "Mo",
                        DayOfWeek.Tuesday   => "Tu",
                        DayOfWeek.Wednesday => "We",
                        DayOfWeek.Thursday  => "Th",
                        DayOfWeek.Friday    => "Fr",
                        DayOfWeek.Saturday  => "Sa",
                        _                   => ""
                    };

                    if (!task.RecurringDays.Split(',').Contains(dayAbbr))
                        return false;

                    int interval = task.RecurrenceInterval < 1 ? 1 : task.RecurrenceInterval;
                    int weeksSinceStart = (int)Math.Floor((targetDate - taskDate).TotalDays / 7);
                    return weeksSinceStart % interval == 0;
                }

                case "Monthly":
                {
                    int interval = task.RecurrenceInterval < 1 ? 1 : task.RecurrenceInterval;
                    if (targetDate.Day != taskDate.Day) return false;
                    int monthsSinceStart =
                        (targetDate.Year - taskDate.Year) * 12
                        + (targetDate.Month - taskDate.Month);
                    return monthsSinceStart % interval == 0;
                }

                default:
                    return taskDate == targetDate;
            }
        }
    }
}
