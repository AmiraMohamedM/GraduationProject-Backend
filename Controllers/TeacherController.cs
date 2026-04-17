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
    [Route("api/teacher")]
    [Authorize(Roles = "Teacher")]
    public class TeacherController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TeacherController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await _db.Teachers.FirstOrDefaultAsync(t => t.user_id == userId);
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());
            var teacher = await GetCurrentTeacherAsync();

            if (teacher == null) return NotFound();

            var subjects = await _db.Courses
                .Where(c => c.TeacherId == teacher.teacher_id)
                .Include(c => c.CourseSessions)
                .ToListAsync();

            var courseIds = subjects.Select(c => c.Id).ToList();

            var totalStudents = await _db.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync();

            var totalSessions = subjects.Sum(s => s.CourseSessions.Count);

            var pendingRequests = await _db.StudentRequests
                .Where(r => r.Status == "Pending"
                    && courseIds.Contains(r.CourseSession.CourseId))
                .CountAsync();

            var last30Days = DateTime.UtcNow.AddDays(-30);

            var activity = await _db.StudentQuizResults
                .Where(r =>
                    r.CreatedAt >= last30Days &&
                    courseIds.Contains(r.Quiz.CourseSession.CourseId)
                )
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key,
                    count = g.Count()
                })
                .OrderBy(x => x.date)
                .ToListAsync();

            return Ok(new
            {
                TeacherName = user!.firstname + " " + user.lastname,
                TotalSubjects = subjects.Count,
                TotalStudents = totalStudents,
                TotalSessions = totalSessions,
                PendingRequests = pendingRequests,

                StudentActivityLast30Days = activity
            });
        }
        [HttpGet("pending-requests")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var teacher = await GetCurrentTeacherAsync();

            var requests = await _db.StudentRequests
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.CourseSession)
                .Where(r => r.Status == "Pending"
                    && r.CourseSession.Course.TeacherId == teacher.teacher_id)
                .Select(r => new
                {
                    student = r.Student.User.firstname + " " + r.Student.User.lastname,
                    lesson = r.CourseSession.Title,
                    count = r.CurrentCount,
                    reason = r.Reason,
                    date = r.CreatedAt,
                    type = r.Type
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpPost("request/{id}/approve")]
        public async Task<IActionResult> ApproveRequest(Guid id)
        {
            var req = await _db.StudentRequests.FindAsync(id);
            if (req == null) return NotFound();

            req.Status = "Approved";
            await _db.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("request/{id}/deny")]
        public async Task<IActionResult> DenyRequest(Guid id)
        {
            var req = await _db.StudentRequests.FindAsync(id);
            if (req == null) return NotFound();

            req.Status = "Denied";
            await _db.SaveChangesAsync();

            return Ok();
        }
        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects()
        {
            var teacher = await GetCurrentTeacherAsync();

            var subjects = await _db.Courses
                .Where(c => c.TeacherId == teacher.teacher_id)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.AcademicLevel,
                    c.AcademicYear,

                    SessionsCount = c.CourseSessions.Count(),

                    StudentsCount = c.Enrollments
                        .Select(e => e.StudentId)
                        .Distinct()
                        .Count()
                })
                .ToListAsync();

            return Ok(subjects);
        }

        [HttpPost("subjects")]
        public async Task<IActionResult> CreateSubject(CreateCourseDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();

            var subject = new Course
            {
                TeacherId = teacher.teacher_id,
                Title = dto.Title,
                AcademicLevel = dto.AcademicLevel,
                AcademicYear = dto.AcademicYear
            };

            _db.Courses.Add(subject);
            await _db.SaveChangesAsync();

            return Ok(subject);
        }

        [HttpPut("subjects/{courseId}")]
        public async Task<IActionResult> UpdateSubject(int courseId, CreateCourseDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return Unauthorized();

            var subject = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.TeacherId == teacher.teacher_id);

            if (subject == null) return NotFound();

            subject.Title = dto.Title;
            subject.AcademicLevel = dto.AcademicLevel;
            subject.AcademicYear = dto.AcademicYear;

            _db.Courses.Update(subject);

            await _db.SaveChangesAsync();

            return Ok(new { message = "Updated", subject });
        }

        [HttpDelete("subjects/{courseId}")]
        public async Task<IActionResult> DeleteSubject(int courseId)
        {
            var teacher = await GetCurrentTeacherAsync();

            var subject = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.TeacherId == teacher.teacher_id);

            if (subject == null) return NotFound();

            _db.Courses.Remove(subject);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Deleted" });
        }

        [HttpGet("subjects/{courseId}")]
        public async Task<IActionResult> GetSubjectDetail(int courseId)
        {
            var teacher = await GetCurrentTeacherAsync();

            var subject = await _db.Courses
                .Include(c => c.CourseSessions)
                    .ThenInclude(l => l.EntryTest)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.TeacherId == teacher.teacher_id);

            if (subject == null) return NotFound();

            return Ok(new
            {
                subject.Id,
                subject.Title,
                subject.AcademicLevel,
                subject.AcademicYear,
                Sessions = subject.CourseSessions.Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.AttachmentUrl,
                    l.AvailableDays,
                    l.MaxViews,
                    l.HomeworkUrl,
                    l.HasEntryTest,
                    EntryTest = l.EntryTest == null ? null : new
                    {
                        l.EntryTest.Id,
                        l.EntryTest.Title,
                        l.EntryTest.PassingScore
                    }
                })
            });
        }

        [HttpPost("subjects/{courseId}/lessons")]
        public async Task<IActionResult> AddLesson(int courseId, AddLessonDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();

            var subject = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.TeacherId == teacher.teacher_id);

            if (subject == null) return NotFound();

            var CourseSession = new CourseSession
            {
                CourseId = courseId,
                Title = dto.Title,
                AttachmentUrl = dto.AttachmentUrl,
                AvailableDays = dto.AvailableDays,
                MaxViews = dto.MaxViews,
                HomeworkUrl = dto.HomeworkUrl,
                HasEntryTest = dto.HasEntryTest
            };

            _db.CourseSessions.Add(CourseSession);
            await _db.SaveChangesAsync();

            return Ok(CourseSession);
        }

        [HttpPut("lessons/{lessonId}")]
        public async Task<IActionResult> UpdateLesson(int lessonId, AddLessonDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();

            var CourseSession = await _db.CourseSessions
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Course.TeacherId == teacher.teacher_id);

            if (CourseSession == null) return NotFound();

            CourseSession.Title = dto.Title ?? CourseSession.Title;
            CourseSession.AttachmentUrl = dto.AttachmentUrl ?? CourseSession.AttachmentUrl;
            CourseSession.AvailableDays = dto.AvailableDays;
            CourseSession.MaxViews = dto.MaxViews;
            CourseSession.HomeworkUrl = dto.HomeworkUrl ?? CourseSession.HomeworkUrl;
            CourseSession.HasEntryTest = dto.HasEntryTest;

            await _db.SaveChangesAsync();

            return Ok(new { message = "CourseSession updated" });
        }

        [HttpDelete("lessons/{lessonId}")]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            var teacher = await GetCurrentTeacherAsync();

            var CourseSession = await _db.CourseSessions
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Course.TeacherId == teacher.teacher_id);

            if (CourseSession == null) return NotFound();

            _db.CourseSessions.Remove(CourseSession);
            await _db.SaveChangesAsync();

            return Ok(new { message = "CourseSession deleted" });
        }

        [HttpPost("lessons/{lessonId}/entry-test")]
        public async Task<IActionResult> AddEntryTest(int lessonId, AddEntryTestDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();

            var courseSession = await _db.CourseSessions
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Course.TeacherId == teacher.teacher_id);

            if (courseSession == null) return NotFound();

            var quiz = new Quiz
            {
                CourseSessionId = lessonId, 
                Title = dto.Title,
                PassingScore = dto.PassingScore,
                RetakeIntervalHours = dto.RetakeIntervalHours,
                Questions = dto.Questions.Select(q => new Question
                {
                    Text = q.Text,
                    Options = q.Options.Select(o => new QuestionOption
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                }).ToList()
            };

            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                quiz.Id,
                quiz.Title,
                quiz.PassingScore,
                quiz.RetakeIntervalHours
            });
        }
        [HttpGet("students/stats")]
        public async Task<IActionResult> GetStudentsStats()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound();

            var courseIds = await _db.Courses
                .Where(c => c.TeacherId == teacher.teacher_id)
                .Select(c => c.Id)
                .ToListAsync();

            var studentIds = await _db.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            var users = await _db.Users.Where(u => studentIds.Contains(u.Id)).ToListAsync();
            var students = await _db.Students.Where(s => studentIds.Contains(s.user_id)).ToListAsync();
            var progresses = await _db.LessonProgress.Where(lp => studentIds.Contains(lp.StudentId)).ToListAsync();
            var quizScores = await _db.StudentQuizResults.Where(q => studentIds.Contains(q.StudentId)).ToListAsync();
            var lessonsCount = await _db.CourseSessions.CountAsync(cs => courseIds.Contains(cs.CourseId));

            var result = studentIds.Select(id =>
            {
                var user = users.FirstOrDefault(u => u.Id == id);
                var student = students.FirstOrDefault(s => s.user_id == id);
                var studentProgress = progresses.Where(p => p.StudentId == id).ToList();
                var studentScores = quizScores.Where(s => s.StudentId == id).ToList();

                return new StudentStatsDto
                {
                    StudentId = id,
                    Name = user != null ? $"{user.firstname} {user.lastname}" : "Unknown",
                    EducationLevel = student?.AcademicLevel ?? "N/A",

                    TotalLessons = lessonsCount,
                    CompletedLessons = studentProgress.Count(p => p.ProgressPercent >= 100),

                    AvgScore = studentScores.Any() ? (decimal)studentScores.Average(s => s.Score) : 0,

                    LastActive = studentProgress.OrderByDescending(p => p.LastWatched).Select(p => p.LastWatched).FirstOrDefault(),

                    AbsencePercentage = 0,
                    TasksPercentage = 0,
                    QuizPercentage = 0
                };
            }).ToList();

            return Ok(result);
        }


        //        [HttpGet("students/stats")]
        // public async Task<IActionResult> GetStudentsStats()
        //{
        // var teacher = await GetCurrentTeacherAsync();
        // if (teacher == null) return NotFound();

        // 1. Courses of teacher
        //  var courseIds = await _db.Courses
        //  .Where(c => c.TeacherId == teacher.teacher_id)
        //    .Select(c => c.Id)
        //      .ToListAsync();

        // 2. Students enrolled in those courses
        //    var enrollments = await _db.Enrollments
        // .Where(e => courseIds.Contains(e.CourseId))
        //   .ToListAsync();

        // var studentIds = enrollments
        //  .Select(e => e.StudentId)
        //.Distinct()
        //  .ToList();

        // 3. Preload data (IMPORTANT for performance)
        // var users = await _db.Users
        //.Where(u => studentIds.Contains(u.Id))
        //  .ToListAsync();

        //var students = await _db.Students
        //    .Where(s => studentIds.Contains(s.user_id))
        //      .ToListAsync();

        //    var progresses = await _db.LessonProgress
        //    .Where(lp => studentIds.Contains(lp.StudentId))
        //      .ToListAsync();

        //    var scores = await _db.StudentQuizResults
        // .Where(q => studentIds.Contains(q.StudentId))
        //   .ToListAsync();

        // var lessonsPerCourse = await _db.CourseSessions
        //  .Where(cs => courseIds.Contains(cs.CourseId))
        //    .ToListAsync();

        // 4. Build response
        //  var result = studentIds.Select(id =>
        //    {
        //          var user = users.FirstOrDefault(u => u.Id == id);
        //            var student = students.FirstOrDefault(s => s.user_id == id);

        //              var studentProgress = progresses.Where(p => p.StudentId == id).ToList();
        // var studentScores = scores.Where(s => s.StudentId == id).ToList();
        //
        //         return new StudentStatsDto
        //           {
        //                 StudentId = id,
        //                   Name = user != null ? $"{user.firstname} {user.lastname}" : "",

        //  EducationLevel = student?.AcademicLevel ?? "",
        //TotalLessons = lessonsPerCourse.Count,

        // CompletedLessons = studentProgress
        //    .Count(p => p.ProgressPercent >= 100),

        //  AvgScore = studentScores.Any()
        //? studentScores.Average(s => s.Percentage)
        //  : 0,

        //LastActive = studentProgress
        //      .OrderByDescending(p => p.LastWatched)
        //        .Select(p => p.LastWatched)
        //          .FirstOrDefault()
        //    };
        //  });

        //    return Ok(result);
        //  }

        private string GetRelativeTime(DateTime date)
        {
            var span = DateTime.UtcNow - date;

            if (span.TotalDays < 1)
                return "Today";
            if (span.TotalDays < 2)
                return "Yesterday";
            if (span.TotalDays < 7)
                return $"{(int)span.TotalDays} days ago";

            return $"{(int)(span.TotalDays / 7)} week(s) ago";
        }


        [HttpGet("grades")]
        public async Task<IActionResult> GetGrades()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound();

            var courseIds = await _db.Courses
                .Where(c => c.TeacherId == teacher.teacher_id)
                .Select(c => c.Id)
                .ToListAsync();

            var students = await _db.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .Select(e => e.Student)
                .Distinct()
                .Include(s => s.User)
                .ToListAsync();

            var result = new List<StudentGradesDto>();

            foreach (var student in students)
            {
                var entryTest = await _db.StudentQuizResults
                    .Where(q => q.StudentId == student.student_id)
                    .Select(q => (double?)q.Percentage)
                    .AverageAsync() ?? 0;

                result.Add(new StudentGradesDto
                {
                    StudentName = student.User.firstname + " " + student.User.lastname,
                    EntryTest = Math.Round(entryTest, 0),
                    Overall = Math.Round(entryTest, 0)
                });
            }

            return Ok(result);
        }




        [HttpGet("lessons/{lessonId}/stats")]
        public async Task<IActionResult> LessonStats(int lessonId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound();

            var lesson = await _db.CourseSessions
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l =>
                    l.Id == lessonId &&
                    l.Course.TeacherId == teacher.teacher_id);

            if (lesson == null) return NotFound();

            var progress = await _db.LessonProgress
                .Where(lp => lp.CourseSessionId == lessonId)
                .ToListAsync();

            var studentIds = progress.Select(p => p.StudentId).Distinct();

            var users = await _db.Users
                .Where(u => studentIds.Contains(u.Id))
                .ToListAsync();

            var quizResults = await _db.StudentQuizResults
                .Where(q => studentIds.Contains(q.StudentId))
                .ToListAsync();

            var result = progress.Select(p =>
            {
                var user = users.FirstOrDefault(u => u.Id == p.StudentId);

                var quiz = quizResults
                    .Where(q => q.StudentId == p.StudentId)
                    .OrderByDescending(q => q.SubmittedAt)
                    .FirstOrDefault();

                return new
                {
                    StudentId = p.StudentId,
                    StudentName = user != null ? $"{user.firstname} {user.lastname}" : "",

                    Views = p.Views,
                    Progress = p.ProgressPercent,
                    LastWatched = p.LastWatched,

                    EntryTest = quiz == null
                        ? "Pending"
                        : (quiz.Passed ? $"Passed ({quiz.Percentage}%)" : $"Failed ({quiz.Percentage}%)")
                };
            });

            return Ok(new
            {
                Lesson = lesson.Title,
                Data = result
            });
        }
        public class CreateCourseDto
        {
            public string Title { get; set; }
            public string AcademicLevel { get; set; }
            public int AcademicYear { get; set; }
        }

        public class AddLessonDto
        {
            public string Title { get; set; }
            public string? AttachmentUrl { get; set; }
            public int AvailableDays { get; set; }
            public int MaxViews { get; set; }
            public string? HomeworkUrl { get; set; }
            public bool HasEntryTest { get; set; }
        }

        public class AddEntryTestDto
        {
            public string Title { get; set; }
            public int PassingScore { get; set; }
            public int RetakeIntervalHours { get; set; }
            public List<AddQuestionDto> Questions { get; set; }
        }

        public class AddQuestionDto
        {
            public string Text { get; set; }
            public List<AddOptionDto> Options { get; set; }
        }

        public class AddOptionDto
        {
            public string Text { get; set; }
            public bool IsCorrect { get; set; }
        }
        public class TeacherStudentDto
        {
            public string Code { get; set; }        
            public string StudentName { get; set; }
            public string EducationLevel { get; set; }
            public string LessonsCompleted { get; set; }
            public double AvgScore { get; set; }
            public string LastActive { get; set; }
        }
        public class StudentGradesDto
        {
            public string StudentName { get; set; }
            public double EntryTest { get; set; }
            public double Overall { get; set; }
        }

        public class LessonStatsDto
        {
            public string StudentName { get; set; }
            public string Views { get; set; }
            public int Progress { get; set; }
            public string LastWatched { get; set; }
            public string EntryTest { get; set; }
            public string Homework { get; set; }
        }

      //  public class StudentStatsDto
      //  {
        //    public Guid StudentId { get; set; }
           // public string Name { get; set; }
          //  public string EducationLevel { get; set; }

         //   public int TotalLessons { get; set; }
          //  public int CompletedLessons { get; set; }

          //  public decimal AvgScore { get; set; }
          //  public DateTime? LastActive { get; set; }
       // }
    }
}