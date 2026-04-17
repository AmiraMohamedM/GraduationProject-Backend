using grad.Data;
using grad.DTOs;
using grad.Models;
using grad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace grad.Controllers
{
    [ApiController]
    [Route("api/moderator")]
    [Authorize(Roles = "Moderator")]
    public class ModeratorController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IStudentService _studentService;
        private readonly IStatisticsService _statisticsService;
        private readonly ILogger<ModeratorController> _logger;

        public ModeratorController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService,
            IStudentService studentService,
            IStatisticsService statisticsService,
            ILogger<ModeratorController> logger)
        {
            _db = db;
            _userManager = userManager;
            _tokenService = tokenService;
            _studentService = studentService;
            _statisticsService = statisticsService;
            _logger = logger;
        }

       
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var totalTeachers = await _db.Teachers
                .Where(t => t.ModeratorId == moderatorId && t.is_approved)
                .CountAsync();

            var totalStudents = await _db.StudentTeachers
                .Where(st => st.Teacher.ModeratorId == moderatorId)
                .Select(st => st.StudentId)
                .Distinct()
                .CountAsync();

            var totalCourses = await _db.Courses
                .Where(c => c.Teacher.ModeratorId == moderatorId)
                .CountAsync();

            var totalEnrollments = await _db.Enrollments
                .Where(e => e.Course.Teacher.ModeratorId == moderatorId)
                .CountAsync();

            var recentStudents = await _db.Students
                .Include(s => s.User)
                .Where(s => _db.StudentTeachers.Any(st => st.StudentId == s.student_id && st.Teacher.ModeratorId == moderatorId))
                .OrderByDescending(s => s.student_id)
                .Take(5)
                .Select(s => new
                {
                    s.student_id,
                    Name = s.User.firstname + " " + s.User.lastname,
                    s.User.Email,
                    s.AcademicLevel,
                    s.AcademicYear
                })
                .ToListAsync();


            var pendingRequests = await _db.StudentRequests
                .CountAsync(r => r.Status == "Pending");

            var assignedTeachers = await _db.Teachers
                .Include(t => t.User)
                .Where(t => t.ModeratorId == moderatorId && t.is_approved)
                .Select(t => new {
                    t.teacher_id,
                    FullName = t.User.firstname + " " + t.User.lastname,
                    Subject = t.subject,
                    Initials = t.User.firstname.Substring(0, 1).ToUpper() + t.User.lastname.Substring(0, 1).ToUpper()
                })
                .ToListAsync();

            var teachersDistribution = assignedTeachers.Select(t => new {
                TeacherName = t.FullName,
                StudentCount = _db.StudentTeachers.Count(st => st.TeacherId == t.teacher_id)
            }).ToList();
            var firstDayOfMonth = DateTime.SpecifyKind(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), DateTimeKind.Utc);
            var newStudentsThisMonth = await _db.Students
                .Include(s => s.User)
                .Where(s => _db.StudentTeachers.Any(st => st.StudentId == s.student_id && st.Teacher.ModeratorId == moderatorId))
                .CountAsync(s => s.User.CreatedAt >= firstDayOfMonth);

            return Ok(new
            {
                TotalStudents = totalStudents,
                TotalTeachers = totalTeachers,
                TotalCourses = totalCourses,
                TotalEnrollments = totalEnrollments,
                RecentStudents = recentStudents,
                PendingRequests = pendingRequests,
                NewThisMonth = newStudentsThisMonth,
                AssignedTeachersList = assignedTeachers,
                ChartData = teachersDistribution
            });
        }
        [HttpGet("my-students")]
        public async Task<IActionResult> GetMyStudents()
        {
            var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _db.Students
                .Include(s => s.User)
                .Where(s => _db.StudentTeachers.Any(st =>
                    st.Teacher.ModeratorId == moderatorId &&
                    (st.StudentId == s.student_id))) 
                .Select(s => new
                {
                    StudentId = s.student_id,
                    FullName = s.User.firstname + " " + s.User.lastname,
                    EducationLevel = s.AcademicLevel + " - " + s.AcademicYear,

                    AssignedTeachers = _db.StudentTeachers
                        .Where(st => st.StudentId == s.student_id)
                        .Select(st => st.Teacher.User.firstname + " " + st.Teacher.User.lastname)
                        .ToList(),

                    Subjects = _db.StudentTeachers
                        .Where(st => st.StudentId == s.student_id)
                        .Select(st => st.Teacher.subject)
                        .Distinct()
                        .ToList(),

                    AvgScore = _db.StudentQuizResults
                        .Where(qr => qr.StudentId == s.student_id)
                        .Average(qr => (double?)qr.Score) ?? 0,

                    MissingDays = _db.StudentAbsences
                        .Count(a => a.StudentId == s.student_id),

                    Joined = s.User.CreatedAt.ToString("MMM dd, yyyy")
                })
                .ToListAsync();

            return Ok(result);
        }
        [HttpGet("my-teachers")]
        public async Task<IActionResult> GetMyTeachers()
        {
            var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var teachers = await _db.Teachers
                .Include(t => t.User) 
                .Where(t => t.ModeratorId == moderatorId)
                .Select(t => new
                {
                    TeacherId = t.teacher_id,
                    UserId = t.user_id,       
                    FullName = t.User.firstname + " " + t.User.lastname,
                    Email = t.User.Email,
                    Subject = t.subject,
                    IsApproved = t.is_approved,
                    StudentCount = _db.StudentTeachers.Count(st => st.TeacherId == t.teacher_id)
                })
                .ToListAsync();

            return Ok(teachers);
        }


        [HttpGet("students")]
        public async Task<IActionResult> GetStudents(
            [FromQuery] string? search,
            [FromQuery] string? academicLevel)
        {
            var query = _db.Students
                .Include(s => s.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var lower = search.ToLower();
                query = query.Where(s =>
                    s.User.firstname.ToLower().Contains(lower) ||
                    s.User.lastname.ToLower().Contains(lower) ||
                    s.User.Email!.ToLower().Contains(lower));
            }

            if (!string.IsNullOrEmpty(academicLevel))
            {
                query = query.Where(s => s.AcademicLevel.ToLower() == academicLevel.ToLower());
            }

            var students = await query.Select(s => new
            {
                s.student_id,
                s.user_id,
                Name = s.User.firstname + " " + s.User.lastname,
                s.User.Email,
                AcademicLevel = s.AcademicLevel,
                AcademicYear = s.AcademicYear,
                s.parent_email
            }).ToListAsync();

            return Ok(students);
        }


        [HttpGet("students/{studentId:guid}")]
        public async Task<IActionResult> GetStudent(Guid studentId)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .Include(s => s.AssignedTeachers)
                    .ThenInclude(st => st.Teacher)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(s => s.student_id == studentId);

            if (student is null) return NotFound(new { message = "Student not found." });

            var enrollments = await _db.Enrollments
                .Include(e => e.Course).ThenInclude(c => c.Teacher).ThenInclude(t => t.User)
                .Where(e => e.StudentId == studentId)
                .ToListAsync();

            var quizResults = await _db.StudentQuizResults
                .Include(r => r.Quiz)
                .Where(r => r.StudentId == studentId)
                .ToListAsync();

            var stats = await _statisticsService.GetStudentStatisticsAsync(studentId);

            return Ok(new
            {
                student.student_id,
                Name = student.User.firstname + " " + student.User.lastname,
                student.User.Email,
                student.User.UserName,
                student.User.PhoneNumber,
                AcademicLevel = student.AcademicLevel,
                AcademicYear = student.AcademicYear,
                ParentPhone = student.parent_email,
                AssignedTeachers = student.AssignedTeachers.Select(st => new
                {
                    st.Teacher.teacher_id,
                    Name = st.Teacher.User.firstname + " " + st.Teacher.User.lastname,
                    Subject = st.Teacher.subject
                }),
                Statistics = new
                {
                    stats.Absence,
                    stats.Tasks,
                    stats.Quiz
                },
                Enrollments = enrollments.Select(e => new
                {
                    e.CourseId,
                    CourseTitle = e.Course.Title,
                  
                    TeacherName = e.Course.Teacher.User.firstname + " " + e.Course.Teacher.User.lastname,
                    Progress = e.ProgressPercent,
                    e.EnrolledAt
                }),
                QuizResults = quizResults.Select(r => new
                {
                    QuizTitle = r.Quiz.Title,
                    r.Score,
                    r.TotalQuestions,
                    r.Percentage,
                    r.Passed,
                    r.SubmittedAt
                })
            });
        }

        [HttpPost("students")]
        public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var credentials = await _studentService.CreateStudentAsync(dto);
                return Ok(credentials);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("CreateStudent validation error: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating student.");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again." });
            }
        }
        [HttpGet("enrollment-records")]
        public async Task<IActionResult> GetEnrollmentRecords()
        {
            var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var records = await _db.Enrollments
                .Include(e => e.Student).ThenInclude(s => s.User)
                .Include(e => e.Course).ThenInclude(c => c.Teacher).ThenInclude(t => t.User)
                .Where(e => e.Course.Teacher.ModeratorId == moderatorId)
                .OrderByDescending(e => e.EnrolledAt) 
                .Select(e => new
                {
                    StudentName = e.Student.User.firstname + " " + e.Student.User.lastname,
                    TeacherName = e.Course.Teacher.User.firstname + " " + e.Course.Teacher.User.lastname,
                    Subject = e.Course.Teacher.subject,
                    DateEnrolled = e.EnrolledAt.ToString("MMM dd, yyyy")
                })
                .ToListAsync();

            return Ok(records);
        }

        // UPDATE STUDENT
        [HttpPut("students/{studentId:guid}")]
        public async Task<IActionResult> UpdateStudent(Guid studentId, [FromBody] UpdateStudentDto dto)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.student_id == studentId);

            if (student is null) return NotFound(new { message = "Student not found." });

            student.User.firstname = dto.FirstName ?? student.User.firstname;
            student.User.lastname = dto.LastName ?? student.User.lastname;
            student.User.Email = dto.Email ?? student.User.Email;
            student.parent_email = dto.ParentPhoneNumber ?? student.parent_email;
            if (!string.IsNullOrEmpty(dto.AcademicLevel)) student.AcademicLevel = dto.AcademicLevel;
            if (dto.AcademicYear.HasValue) student.AcademicYear = dto.AcademicYear.Value;

            await _userManager.UpdateAsync(student.User);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Student updated." });
        }

        // DELETE STUDENT
        [HttpDelete("students/{studentId:guid}")]
        public async Task<IActionResult> DeleteStudent(Guid studentId)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.student_id == studentId);

            if (student is null) return NotFound(new { message = "Student not found." });

            await _userManager.DeleteAsync(student.User);
            return Ok(new { message = "Student deleted." });
        }

        // REPLACE TEACHERS FOR A STUDENT
        [HttpPost("students/{studentId:guid}/teachers")]
        public async Task<IActionResult> AssignTeachers(Guid studentId, [FromBody] AssignTeacherDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _studentService.AssignTeachersAsync(studentId, dto.TeacherIds);
                return Ok(new { message = "Teachers assigned successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning teachers to student {StudentId}.", studentId);
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        // GET TEACHERS ASSIGNED TO A STUDENT
        [HttpGet("students/{studentId:guid}/teachers")]
        public async Task<IActionResult> GetStudentTeachers(Guid studentId)
        {
            var exists = await _db.Students.AnyAsync(s => s.student_id == studentId);
            if (!exists) return NotFound(new { message = "Student not found." });

            var teachers = await _db.StudentTeachers
                .Include(st => st.Teacher).ThenInclude(t => t.User)
                .Where(st => st.StudentId == studentId)
                .Select(st => new
                {
                    st.Teacher.teacher_id,
                    Name = st.Teacher.User.firstname + " " + st.Teacher.User.lastname,
                    Subject = st.Teacher.subject,
                    st.Teacher.User.Email
                })
                .ToListAsync();

            return Ok(teachers);
        }

        // RESET STUDENT PASSWORD
        [HttpPost("students/{studentId:guid}/reset-password")]
        public async Task<IActionResult> ResetStudentPassword(
            Guid studentId,
            [FromBody] ResetPasswordDirectDto dto)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.student_id == studentId);

            if (student is null) return NotFound(new { message = "Student not found." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(student.User);
            var result = await _userManager.ResetPasswordAsync(student.User, token, dto.NewPassword);

            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new { message = "Password reset successfully." });
        }

        // ENROLL STUDENT IN COURSE
        [HttpPost("students/{studentId:guid}/enroll")]
        public async Task<IActionResult> EnrollStudent(Guid studentId, [FromBody] EnrollStudentDto dto)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student is null) return NotFound(new { message = "Student not found." });

            var course = await _db.Courses.FindAsync(dto.CourseId);
            if (course is null) return NotFound(new { message = "Course not found." });

            var existing = await _db.Enrollments.FirstOrDefaultAsync(
                e => e.StudentId == studentId && e.CourseId == dto.CourseId);
            if (existing is not null) return BadRequest(new { message = "Student already enrolled." });

            _db.Enrollments.Add(new Enrollment { StudentId = studentId, CourseId = dto.CourseId });

            _db.Notifications.Add(new Notification
            {
                UserId = student.user_id,
                Title = "Enrolled in New Course",
                Body = $"You have been enrolled in '{course.Title}'.",
                Type = "general"
            });

            await _db.SaveChangesAsync();
            return Ok(new { message = "Student enrolled successfully." });
        }

        // UNENROLL STUDENT FROM COURSE
        [HttpDelete("students/{studentId:guid}/enroll/{courseId:int}")]
        public async Task<IActionResult> UnenrollStudent(Guid studentId, int courseId)
        {
            var enrollment = await _db.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId);

            if (enrollment is null) return NotFound(new { message = "Enrollment not found." });

            _db.Enrollments.Remove(enrollment);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Student unenrolled." });
        }

        // STUDENT STATISTICS
        [HttpGet("student-stats/{studentId}")]
        public async Task<IActionResult> GetStudentStatistics(Guid studentId)
        {
            var absenceCount = await _db.StudentAbsences
                .CountAsync(a => a.StudentId == studentId);

            var tasksCount = await _db.HomeworkSubmissions
                .CountAsync(h => h.StudentId == studentId);

            var quizCount = await _db.StudentQuizResults
                .CountAsync(r => r.StudentId == studentId);

            return Ok(new
            {
                studentId = studentId,
                absence = absenceCount,
                tasks = tasksCount,
                quiz = quizCount
            });
        }

        // AVAILABLE COURSES
        [HttpGet("courses")]
        public async Task<IActionResult> GetAllCourses()
        {
            var courses = await _db.Courses
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Where(c => c.Teacher.is_approved)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                   
                    TeacherName = c.Teacher.User.firstname + " " + c.Teacher.User.lastname,

                })
                .ToListAsync();

            return Ok(courses);
        }

        // ACADEMIC LEVELS & CLASS LEVELS
       // [HttpGet("academic-levels")]
       // public async Task<IActionResult> GetAcademicLevels()
       // {
       //     var levels = await _db.AcademicLevels
           //     .Include(a => a.ClassLevels)
            //    .Select(a => new
            //    {
             //       a.id,
             //       a.name,
              //      ClassLevels = a.ClassLevels.Select(c => new { c.id, c.name })
              //  })
               // .ToListAsync();

           // return Ok(levels);
      //  }

        // SEND NOTIFICATION TO STUDENT
        [HttpPost("students/{studentId:guid}/notify")]
        public async Task<IActionResult> NotifyStudent(Guid studentId, [FromBody] SendNotificationDto dto)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student is null) return NotFound(new { message = "Student not found." });

            _db.Notifications.Add(new Notification
            {
                UserId = student.user_id,
                Title = dto.Title,
                Body = dto.Body,
                Type = dto.Type ?? "general"
            });

            await _db.SaveChangesAsync();
            return Ok(new { message = "Notification sent." });
        }


        [HttpGet("all-students-stats")]
        public async Task<IActionResult> GetAllStudentsStats()
        {
            var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var stats = await _db.Students
                .Include(s => s.User) 
                .Where(s => _db.StudentTeachers.Any(st => st.Teacher.ModeratorId == moderatorId && st.StudentId == s.student_id))
                .Select(s => new
                {
                    StudentId = s.student_id,
                    FirstName = s.User.firstname,
                    LastName = s.User.lastname,
                    FullName = s.User.firstname + " " + s.User.lastname,

                    Absence = _db.StudentAbsences.Count(a => a.StudentId == s.student_id),
                    Tasks = _db.HomeworkSubmissions.Count(h => h.StudentId == s.student_id),
                    Quiz = _db.StudentQuizResults.Count(r => r.StudentId == s.student_id)
                })
                .ToListAsync();

            return Ok(stats);
        }

        // ABSENCE MANAGEMENT

        [HttpGet("students/{studentId:guid}/absences-report")]
        public async Task<IActionResult> GetAutomatedAbsenceReport(Guid studentId)
        {
            var report = await _db.Enrollments
                .Where(e => e.StudentId == studentId)
                .SelectMany(e => e.Course.CourseSessions)
                .Select(session => new
                {
                    SessionTitle = session.Title,
                    Progress = _db.LessonProgress
                        .Where(p => p.StudentId == studentId && p.LessonId == session.Id)
                        .Select(p => p.ProgressPercent)
                        .FirstOrDefault(),

                    Status = _db.LessonProgress.Any(p => p.StudentId == studentId && p.LessonId == session.Id && p.ProgressPercent > 20)
                             ? "Attended" : "Absent"
                })
                .ToListAsync();

            return Ok(report);
        }
        [HttpGet("students/{studentId:guid}/watch-progress")]
        public async Task<IActionResult> GetDetailedWatchProgress(Guid studentId)
        {
            var progress = await _db.LessonProgress
                .Include(lp => lp.CourseSession)
                .Where(lp => lp.StudentId == studentId)
                .Select(lp => new {
                    LessonName = lp.CourseSession.Title,
                    Percentage = lp.ProgressPercent,
                    LastSeen = lp.LastWatched
                }).ToListAsync();

            return Ok(progress);
        }

        [HttpPost("students/{studentId:guid}/absences")]
        public async Task<IActionResult> RecordAbsence(Guid studentId, [FromBody] RecordAbsenceDto dto)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student is null) return NotFound(new { message = "Student not found." });

            var moderatorId = Guid.TryParse(
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var mid) ? mid : (Guid?)null;

            var absence = new grad.Models.StudentAbsence
            {
                StudentId = studentId,
                AbsenceDate = dto.AbsenceDate ?? DateTime.UtcNow,
                Note = dto.Note,
                RecordedBy = moderatorId
            };

            _db.StudentAbsences.Add(absence);
            await _db.SaveChangesAsync();

            var totalAbsences = await _db.StudentAbsences.CountAsync(a => a.StudentId == studentId);

            return Ok(new
            {
                message = "Absence recorded.",
                AbsenceId = absence.Id,
                TotalAbsence = totalAbsences
            });
        }

        [HttpDelete("students/{studentId:guid}/absences/{absenceId:int}")]
        public async Task<IActionResult> DeleteAbsence(Guid studentId, int absenceId)
        {
            var absence = await _db.StudentAbsences
                .FirstOrDefaultAsync(a => a.Id == absenceId && a.StudentId == studentId);

            if (absence is null) return NotFound(new { message = "Absence record not found." });

            _db.StudentAbsences.Remove(absence);
            await _db.SaveChangesAsync();

            var totalAbsences = await _db.StudentAbsences.CountAsync(a => a.StudentId == studentId);

            return Ok(new
            {
                message = "Absence record deleted.",
                TotalAbsence = totalAbsences
            });
        }
        [HttpGet("activity-timeline")]
        public async Task<IActionResult> GetActivityTimeline()
        {
            var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var activities = await _db.ActivityLogs
                .Where(a => a.UserId == moderatorId) 
                .OrderByDescending(a => a.CreatedAt)
                .Take(15)
                .Select(a => new
                {
                    a.Text,
                    TimeDisplay = GetTimeDisplay(a.CreatedAt)
                })
                .ToListAsync();

            return Ok(activities);
        }

        private static string GetTimeDisplay(DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
            if (span.TotalDays < 2) return "Yesterday";
            return dt.ToString("MMM dd, yyyy");
        }

        // TEACHERS LIST
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _db.Teachers
                .Include(t => t.User)
                .Where(t => t.is_approved)
                .Select(t => new
                {
                    t.teacher_id,
                    Name = t.User.firstname + " " + t.User.lastname,
                    t.User.Email,
                    t.subject
                    
                })
                .ToListAsync();

            return Ok(teachers);
        }
    }


    public class UpdateStudentDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? ParentPhoneNumber { get; set; }
        public string? AcademicLevel { get; set; }
        public int? AcademicYear { get; set; }
    }

    public class ResetPasswordDirectDto
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class EnrollStudentDto
    {
        public int CourseId { get; set; }
    }

    public class UpdateStatsDto
    {
        public int? Absence { get; set; }
        public int? Tasks { get; set; }
        public int? Quiz { get; set; }
    }

    public class SendNotificationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Type { get; set; }
    }

    public class RecordAbsenceDto
    {

        public DateTime? AbsenceDate { get; set; }

        public string? Note { get; set; }
    }
}
