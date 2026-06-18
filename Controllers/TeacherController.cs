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

        // DASHBOARD
       
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());
            var teacher = await GetCurrentTeacherAsync();

            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });
            if (user == null) return NotFound(new { message = "User not found." });

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
                    courseIds.Contains(r.Quiz.CourseSession.CourseId))
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
                TeacherName = user.firstname + " " + user.lastname,
                TotalSubjects = subjects.Count,
                TotalStudents = totalStudents,
                TotalSessions = totalSessions,
                PendingRequests = pendingRequests,
                StudentActivityLast30Days = activity
            });
        }

        // GET ALL STUDENTS
        
        [HttpGet("students")]
        public async Task<IActionResult> GetAllStudents()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

            var courseIds = await _db.Courses
                .Where(c => c.TeacherId == teacher.teacher_id)
                .Select(c => c.Id)
                .ToListAsync();




            var studentIds = await _db.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))

                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();


            var students = await _db.Students
                .Include(s => s.User)
                .Where(s => studentIds.Contains(s.student_id))
                .ToListAsync();

            var progresses = await _db.LessonProgress
                .Where(lp => studentIds.Contains(lp.StudentId))
                .ToListAsync();

            var quizResults = await _db.StudentQuizResults
                .Where(q => studentIds.Contains(q.StudentId))
                .ToListAsync();

            var result = students.Select(s =>
            {
                var studentProgress = progresses
                    .Where(p => p.StudentId == s.student_id)
                    .ToList();

                var studentScores = quizResults
                    .Where(q => q.StudentId == s.student_id)
                    .ToList();

                return new
                {
                    StudentId = s.student_id,

                    StudentName = s.User != null
                        ? $"{s.User.firstname} {s.User.lastname}"
                        : "Unknown",

                    EducationLevel = s.AcademicLevel ?? "N/A",

                    LessonsCompleted = studentProgress
                        .Count(p => p.ProgressPercent >= 100),

                    AvgScore = studentScores.Any()
                        ? Math.Round(studentScores.Average(x => (double)x.Percentage), 2)
                        : 0,

                    LastActive = studentProgress
                        .OrderByDescending(p => p.LastWatched)
                        .Select(p => p.LastWatched)
                        .FirstOrDefault()
                };
            });

            return Ok(result);
        }

        // PENDING REQUESTS
        [HttpGet("pending-requests")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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


        // SUBJECTS (COURSES)
      

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

            var subject = await _db.Courses
                .Include(c => c.CourseSessions)
                    .ThenInclude(l => l.EntryTest)
                .Include(c => c.CourseSessions)
                    .ThenInclude(l => l.Files)
                .FirstOrDefaultAsync(c =>
                    c.Id == courseId &&
                    c.TeacherId == teacher.teacher_id);

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

                    Files = l.Files.Select(f => new
                    {
                        f.Id,
                        f.FileName,
                        f.FileType,
                        f.FileSize,
                        f.FileUrl
                    }),

                    l.AvailableDays,
                    l.MaxViews,
                    l.HomeworkFileUrl,
                    l.HomeworkFileName,
                    l.HomeworkFileType,
                    l.HomeworkFileSize,
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
        public async Task<IActionResult> AddLesson(
            int courseId,
            [FromForm] AddLessonDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

            var subject = await _db.Courses
                .FirstOrDefaultAsync(c =>
                    c.Id == courseId &&
                    c.TeacherId == teacher.teacher_id);

            if (subject == null)
                return NotFound();



            string? homeworkFileUrl = null;
            string? homeworkFileName = null;
            string? homeworkFileType = null;
            long? homeworkFileSize = null;

            if (dto.HomeworkFile != null)
            {
                var homeworkFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "homeworks");

                Directory.CreateDirectory(homeworkFolder);

                var uniqueHomeworkName =
                    $"{Guid.NewGuid()}{Path.GetExtension(dto.HomeworkFile.FileName)}";

                var homeworkPath =
                    Path.Combine(homeworkFolder, uniqueHomeworkName);

                using (var stream = new FileStream(homeworkPath, FileMode.Create))
                {
                    await dto.HomeworkFile.CopyToAsync(stream);
                }

                homeworkFileUrl = $"/homeworks/{uniqueHomeworkName}";
                homeworkFileName = dto.HomeworkFile.FileName;
                homeworkFileType = dto.HomeworkFile.ContentType;
                homeworkFileSize = dto.HomeworkFile.Length;
            }

            var courseSession = new CourseSession
            {
                CourseId = courseId,
                Title = dto.Title,

                AvailableDays = dto.AvailableDays,
                MaxViews = dto.MaxViews,

                HomeworkFileUrl = homeworkFileUrl,
                HomeworkFileName = homeworkFileName,
                HomeworkFileType = homeworkFileType,
                HomeworkFileSize = homeworkFileSize,

                HasEntryTest = dto.HasEntryTest
            };

            _db.CourseSessions.Add(courseSession);
            await _db.SaveChangesAsync();


            if (dto.Files != null && dto.Files.Any())
            {
                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads");

                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in dto.Files)
                {
                    var uniqueFileName =
                        $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                    var filePath =
                        Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    _db.LessonFiles.Add(new LessonFile
                    {
                        CourseSessionId = courseSession.Id,
                        FileUrl = $"/uploads/{uniqueFileName}",
                        FileName = file.FileName,
                        FileType = file.ContentType,
                        FileSize = file.Length
                    });
                }

                await _db.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Lesson created successfully",
                lessonId = courseSession.Id
            });

            return Ok(courseSession);

        }

        [HttpPut("lessons/{lessonId}")]
        public async Task<IActionResult> UpdateLesson(
            int lessonId,
            [FromForm] AddLessonDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

            var courseSession = await _db.CourseSessions
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l =>
                    l.Id == lessonId &&
                    l.Course.TeacherId == teacher.teacher_id);


            if (courseSession == null)
                return NotFound();

            courseSession.Title = dto.Title;
            courseSession.AvailableDays = dto.AvailableDays;
            courseSession.MaxViews = dto.MaxViews;
            if (dto.HomeworkFile != null)
            {
                var homeworkFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "homeworks");

                Directory.CreateDirectory(homeworkFolder);

                var uniqueHomeworkName =
                    $"{Guid.NewGuid()}{Path.GetExtension(dto.HomeworkFile.FileName)}";

                var homeworkPath =
                    Path.Combine(homeworkFolder, uniqueHomeworkName);

                using (var stream = new FileStream(homeworkPath, FileMode.Create))
                {
                    await dto.HomeworkFile.CopyToAsync(stream);
                }

                courseSession.HomeworkFileUrl =
                    $"/homeworks/{uniqueHomeworkName}";

                courseSession.HomeworkFileName =
                    dto.HomeworkFile.FileName;

                courseSession.HomeworkFileType =
                    dto.HomeworkFile.ContentType;

                courseSession.HomeworkFileSize =
                    dto.HomeworkFile.Length;
            }
            courseSession.HasEntryTest = dto.HasEntryTest;

            if (dto.Files != null && dto.Files.Any())
            {
                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads");

                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in dto.Files)
                {
                    var uniqueFileName =
                        $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                    var filePath =
                        Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    _db.LessonFiles.Add(new LessonFile
                    {
                        CourseSessionId = courseSession.Id,
                        FileUrl = $"/uploads/{uniqueFileName}",
                        FileName = file.FileName,
                        FileType = file.ContentType,
                        FileSize = file.Length
                    });
                }
            }


            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Lesson updated successfully"
            });
        }

        [HttpDelete("lessons/{lessonId}")]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

            var courseSession = await _db.CourseSessions
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Course.TeacherId == teacher.teacher_id);

            if (courseSession == null) return NotFound();

            _db.CourseSessions.Remove(courseSession);
            await _db.SaveChangesAsync();

            return Ok(new { message = "CourseSession deleted" });
        }

        [HttpPost("lessons/{lessonId}/entry-test")]
        public async Task<IActionResult> AddEntryTest(int lessonId, AddEntryTestDto dto)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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


        [HttpGet("students/{studentId}")]
        public async Task<IActionResult> GetStudentDetails(Guid studentId)
        {
            var teacher = await GetCurrentTeacherAsync();

            if (teacher == null)
                return NotFound();


            var exists = await _db.Enrollments
                .AnyAsync(e =>
                    e.StudentId == studentId &&
                    e.Course.TeacherId == teacher.teacher_id);

            if (!exists)
                return Unauthorized();

            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.student_id == studentId);

            if (student == null)
                return NotFound();


            var courseIds = await _db.Enrollments
                .Where(e => e.StudentId == studentId &&
                            e.Course.TeacherId == teacher.teacher_id)
                .Select(e => e.CourseId)
                .ToListAsync();

            var lessons = await _db.CourseSessions
                .Where(l => courseIds.Contains(l.CourseId))
                .ToListAsync();

            var lessonIds = lessons.Select(x => x.Id).ToList();

         
            var progress = await _db.LessonProgress
                .Where(p =>
                    p.StudentId == studentId &&
                    lessonIds.Contains(p.CourseSessionId))
                .ToListAsync();


            var quizzes = await _db.StudentQuizResults
                .Include(q => q.Quiz)
                .Where(q =>
                    q.StudentId == studentId &&
                    courseIds.Contains(q.Quiz.CourseSession.CourseId))
                .ToListAsync();

            var avgScore = quizzes.Any()
                ? Math.Round(quizzes.Average(q => q.Percentage), 2)
                : 0;

            var completionRate = lessons.Any()
                ? Math.Round(
                    (decimal)progress.Count(p => p.ProgressPercent >= 100)
                    / lessons.Count * 100, 0)
                : 0;

            var grade = Math.Round(
                (avgScore + completionRate) / 2,
                0
            );

            var lessonData = lessons.Select(l =>
            {
                var p = progress
                    .FirstOrDefault(x => x.CourseSessionId == l.Id);

                var quiz = quizzes
                    .Where(q => q.Quiz.CourseSessionId == l.Id)
                    .OrderByDescending(q => q.SubmittedAt)
                    .FirstOrDefault();

                return new
                {
                    LessonId = l.Id,

                    Lesson = l.Title,

                    WatchPercent =
                        p?.ProgressPercent ?? 0,

                    ViewsUsed =
                        $"{p?.Views ?? 0}/{l.MaxViews}",

                    EntryTest =
                        quiz == null
                            ? "-"
                            : $"{quiz.Percentage}%",

                    Result =
                        p == null
                            ? "Locked"
                            : p.ProgressPercent >= 100
                                ? "Completed"
                                : "Pending"
                };
            });

            return Ok(new
            {
                StudentId = student.student_id,

                StudentName =
                    $"{student.User.firstname} {student.User.lastname}",

             

                Grade = grade,

                AverageScore = avgScore,

                CompletionRate = completionRate,

                Lessons = lessonData
            });
        }

        // STUDENTS STATS

        [HttpGet("students/stats")]
        public async Task<IActionResult> GetStudentsStats()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

            var courseIds = await _db.Courses
                .Where(c => c.TeacherId == teacher.teacher_id)
                .Select(c => c.Id)
                .ToListAsync();

            var studentIds = await _db.Enrollments

                .Where(e => courseIds.Contains(e.CourseId))

                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            var students = await _db.Students
                .Include(s => s.User)
                .Where(s => studentIds.Contains(s.student_id))
                .ToListAsync();

            var progresses = await _db.LessonProgress
                .Where(lp => studentIds.Contains(lp.StudentId))
                .ToListAsync();

            var quizScores = await _db.StudentQuizResults
                .Where(q => studentIds.Contains(q.StudentId))
                .ToListAsync();

            var enrollments = await _db.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .ToListAsync();

            var lessonCountsPerCourse = await _db.CourseSessions
                .Where(cs => courseIds.Contains(cs.CourseId))
                .GroupBy(cs => cs.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = students.Select(s =>
            {
                var user = s.User;

                var studentProgress = progresses.Where(p => p.StudentId == s.student_id).ToList();
                var studentScores = quizScores.Where(q => q.StudentId == s.student_id).ToList();

                var enrolledCourseIds = enrollments
                    .Where(e => e.StudentId == s.student_id)
                    .Select(e => e.CourseId)
                    .ToList();
                var totalLessonsForStudent = lessonCountsPerCourse
                    .Where(lc => enrolledCourseIds.Contains(lc.CourseId))
                    .Sum(lc => lc.Count);

                return new StudentStatsDto
                {
                    StudentId = s.student_id,

                    Name = user != null
                        ? $"{user.firstname} {user.lastname}"
                        : "Unknown",

                    EducationLevel = s.AcademicLevel ?? "N/A",

                    TotalLessons = totalLessonsForStudent,

                    CompletedLessons = studentProgress.Count(p => p.ProgressPercent >= 100),

                    AvgScore = studentScores.Any()
                        ? (decimal)studentScores.Average(x => x.Percentage)
                        : 0,

                    LastActive = studentProgress
                        .OrderByDescending(p => p.LastWatched)
                        .Select(p => p.LastWatched)
                        .FirstOrDefault(),

                    AbsencePercentage = 0,
                    TasksPercentage = 0,
                    QuizPercentage = 0
                };
            }).ToList();

            return Ok(result);
        }

        // GRADES
        [HttpGet("grades")]
        public async Task<IActionResult> GetGrades()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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
                if (student.User == null) continue;

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

        // LESSON STATS
     
        [HttpGet("lessons/{lessonId}/stats")]
        public async Task<IActionResult> LessonStats(int lessonId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return NotFound(new { message = "Teacher profile not found." });

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

                    Views = $"{p.Views}/{lesson.MaxViews}",
                    Progress = p.ProgressPercent,
                    LastWatched = p.LastWatched,

                    EntryTest = quiz == null
    ? new { Status = "Pending", Score = (decimal?)null }
    : new
    {
        Status = quiz.Passed ? "Passed" : "Failed",
        Score = (decimal?)quiz.Percentage
    }
                };
            });

            return Ok(new
            {
                Lesson = lesson.Title,
                Data = result
            });
        }


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


 
    }
}
