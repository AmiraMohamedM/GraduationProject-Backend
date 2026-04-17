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
    [Route("api/subject")]
    [Authorize(Roles = "Student")]
    public class SubjectController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public SubjectController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db          = db;
            _userManager = userManager;
        }

       
        private async Task<Student?> GetCurrentStudentAsync()
        {
            var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (rawId == null || !Guid.TryParse(rawId, out var userId))
                return null;

            return await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.user_id == userId);
        }

      
        [HttpGet("my-subjects")]
        public async Task<IActionResult> GetMySubjects()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var studentId = student.student_id;

            var enrollments = await _db.Enrollments
                .AsNoTracking()
                .Include(e => e.Course)
                    .ThenInclude(c => c.Teacher)
                        .ThenInclude(t => t.User)
                .Include(e => e.Course)
                    .ThenInclude(c => c.CourseSessions)
                .Where(e => e.StudentId == studentId)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            if (!enrollments.Any())
                return Ok(new List<SubjectListItemDto>());

            var courseIds = enrollments.Select(e => e.CourseId).ToList();

            var allSessionIds = enrollments
                .SelectMany(e => e.Course.CourseSessions.Select(s => s.Id))
                .ToList();

            var lessonProgressMap = await _db.LessonProgress
                .AsNoTracking()
                .Where(lp => lp.StudentId == studentId && allSessionIds.Contains(lp.CourseSessionId))
                .ToListAsync();

            var progressBySession = lessonProgressMap
                .ToDictionary(lp => lp.CourseSessionId);

            var result = enrollments.Select(e =>
            {
                var sessions      = e.Course.CourseSessions.ToList();
                int totalSessions = sessions.Count;

                int completedSessions = sessions.Count(s =>
                    progressBySession.TryGetValue(s.Id, out var lp) && lp.Views > 0);

                int progressPercent = totalSessions > 0
                    ? (int)Math.Round((double)completedSessions / totalSessions * 100)
                    : 0;

                var teacher = e.Course.Teacher;

                return new SubjectListItemDto
                {
                    CourseId          = e.CourseId,
                    Title             = e.Course.Title,
                    AcademicLevel     = e.Course.AcademicLevel,
                    AcademicYear      = e.Course.AcademicYear,
                    TeacherName       = teacher?.User != null
                                            ? $"{teacher.User.firstname} {teacher.User.lastname}".Trim()
                                            : "N/A",
                    TeacherSubject    = teacher?.subject ?? string.Empty,
                    ProgressPercent   = progressPercent,
                    TotalSessions     = totalSessions,
                    CompletedSessions = completedSessions,
                    EnrolledAt        = e.EnrolledAt
                };
            }).ToList();

            return Ok(result);
        }

       
        [HttpGet("{courseId:int}/sessions")]
        public async Task<IActionResult> GetCourseSessions(int courseId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var studentId = student.student_id;

            var enrolled = await _db.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);

            if (!enrolled)
                return Forbid(); 

            var course = await _db.Courses
                .AsNoTracking()
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Include(c => c.CourseSessions)
                    .ThenInclude(cs => cs.EntryTest)
                        .ThenInclude(q => q != null ? q.Questions : null)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
                return NotFound(new { message = "Course not found." });

            var sessions    = course.CourseSessions.OrderBy(s => s.Id).ToList();
            var sessionIds  = sessions.Select(s => s.Id).ToList();


            var lessonProgressList = await _db.LessonProgress
                .AsNoTracking()
                .Where(lp => lp.StudentId == studentId && sessionIds.Contains(lp.CourseSessionId))
                .ToListAsync();

            var progressBySession = lessonProgressList
                .ToDictionary(lp => lp.CourseSessionId);

            var homeworkSubmissions = await _db.HomeworkSubmissions
                .AsNoTracking()
                .Where(h => h.StudentId == studentId && sessionIds.Contains(h.SessionId))
                .ToListAsync();

            var homeworkBySession = homeworkSubmissions
                .ToDictionary(h => h.SessionId);


            var lessonAttempts = await _db.LessonAttempts
                .AsNoTracking()
                .Where(la => la.StudentId == studentId && sessionIds.Contains(la.CourseSessionId))
                .ToListAsync();

            var attemptsBySession = lessonAttempts
                .GroupBy(la => la.CourseSessionId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        BestScore  = g.Max(a => (decimal)a.Score),
                        AnyPassed  = g.Any(a => a.Passed)
                    });

            int totalSessions     = sessions.Count;
            int completedSessions = sessions.Count(s =>
                progressBySession.TryGetValue(s.Id, out var lp) && lp.Views > 0);
            int courseProgress    = totalSessions > 0
                ? (int)Math.Round((double)completedSessions / totalSessions * 100)
                : 0;

            var sessionDtos = sessions.Select(s =>
            {
                progressBySession.TryGetValue(s.Id, out var lp);
                homeworkBySession.TryGetValue(s.Id, out var hw);
                attemptsBySession.TryGetValue(s.Id, out var attempt);

                int viewsUsed      = lp?.Views ?? 0;
                int viewsRemaining = Math.Max(0, s.MaxViews - viewsUsed);

                bool hasEntryTest    = s.HasEntryTest && s.EntryTest != null;
                bool? entryTestPassed = hasEntryTest ? attempt?.AnyPassed : null;

  
                decimal? entryTestBestScore = null;
                if (hasEntryTest && attempt != null && s.EntryTest != null)
                {
                    int questionCount = s.EntryTest.Questions?.Count ?? 0;
                    entryTestBestScore = questionCount > 0
                        ? Math.Round(attempt.BestScore / questionCount * 100, 1)
                        : attempt.BestScore;
                }

                return new SessionItemDto
                {
                    SessionId              = s.Id,
                    Title                  = s.Title,
                    AvailableDays          = s.AvailableDays,
                    MaxViews               = s.MaxViews,
                    ViewsUsed              = viewsUsed,
                    ViewsRemaining         = viewsRemaining,
                    IsWatched              = viewsUsed > 0,
                    WatchProgressPercent   = lp?.ProgressPercent ?? 0.0,
                    HasAttachment          = !string.IsNullOrEmpty(s.AttachmentUrl),
                    AttachmentUrl          = s.AttachmentUrl,
                    HasHomework            = !string.IsNullOrEmpty(s.HomeworkUrl),
                    HomeworkUrl            = s.HomeworkUrl,
                    HasEntryTest           = hasEntryTest,
                    EntryTestPassed        = entryTestPassed,
                    EntryTestBestScore     = entryTestBestScore,
                    HomeworkSubmitted      = hw != null,
                    HomeworkGrade          = hw?.Grade
                };
            }).ToList();

            var teacher = course.Teacher;
            var response = new CourseSessionsResponseDto
            {
                CourseId          = course.Id,
                CourseTitle       = course.Title,
                AcademicLevel     = course.AcademicLevel,
                AcademicYear      = course.AcademicYear,
                TeacherName       = teacher?.User != null
                                        ? $"{teacher.User.firstname} {teacher.User.lastname}".Trim()
                                        : "N/A",
                TeacherSubject    = teacher?.subject ?? string.Empty,
                ProgressPercent   = courseProgress,
                TotalSessions     = totalSessions,
                CompletedSessions = completedSessions,
                Sessions          = sessionDtos
            };

            return Ok(response);
        }
    }
}
