using grad.Data;
using grad.DTOs;
using grad.Interfaces;
using grad.Models;
using grad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace grad.Controllers
{
    [ApiController]
    [Route("api/student")]
    [Authorize(Roles = "Student")]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStatisticsService _statisticsService;
        private readonly IPhotoService _photoService;

        public StudentController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IStatisticsService statisticsService,
            IPhotoService photoService)
        {
            _db = db;
            _userManager = userManager;
            _statisticsService = statisticsService;
            _photoService = photoService;
        }

        // ─────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────

        private async Task<Student?> GetCurrentStudentAsync()
        {
            var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (rawId == null || !Guid.TryParse(rawId, out var userId))
                return null;

            return await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.user_id == userId);
        }

        private Guid GetCurrentUserId()
        {
            var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return Guid.Parse(rawId);
        }

        /// <summary>
        /// Returns true if the session at <paramref name="indexInCourse"/> is locked
        /// for the given student.
        ///
        /// Rules:
        ///  - Index 0 is always unlocked.
        ///  - Any other session requires the previous session to be watched to 100%.
        ///  - If the previous session has an entry test, the student must have passed it.
        /// </summary>
        private async Task<bool> IsSessionLockedAsync(
            Guid studentId,
            CourseSession session,
            int indexInCourse)
        {
            // First session is always unlocked
            if (indexInCourse == 0) return false;

            // Load all sessions in this course ordered consistently
            var allSessions = await _db.CourseSessions
                .AsNoTracking()
                .Where(cs => cs.CourseId == session.CourseId)
                .OrderBy(cs => cs.Id)
                .ToListAsync();

            var previousSession = allSessions.ElementAtOrDefault(indexInCourse - 1);
            if (previousSession == null) return false;

            // Previous session must be watched to 100%
            var prevProgress = await _db.LessonProgress
                .AsNoTracking()
                .FirstOrDefaultAsync(lp =>
                    lp.StudentId == studentId &&
                    lp.CourseSessionId == previousSession.Id);

            if (prevProgress == null || prevProgress.ProgressPercent < 100)
                return true;

            // If previous session has an entry test the student must have passed it
            if (previousSession.HasEntryTest)
            {
                var passed = await _db.LessonAttempts
                    .AsNoTracking()
                    .AnyAsync(la =>
                        la.StudentId == studentId &&
                        la.CourseSessionId == previousSession.Id &&
                        la.Passed);

                if (!passed) return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────
        // HOME / STATISTICS / ACHIEVEMENT
        // ─────────────────────────────────────────────────────────────

        [HttpGet("home")]
        public async Task<IActionResult> GetHome()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());

            var student = await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.user_id == userId);

            if (student == null || user == null)
                return NotFound(new { message = "Student profile not found." });

            var stats = await _statisticsService.GetStudentStatisticsAsync(student.student_id);

            var enrollments = await _db.Enrollments
                .AsNoTracking()
                .Include(e => e.Course).ThenInclude(c => c.Teacher).ThenInclude(t => t.User)
                .Include(e => e.Course).ThenInclude(c => c.CourseSessions)
                .Where(e => e.StudentId == student.student_id)
                .OrderByDescending(e => e.EnrolledAt)
                .Take(5)
                .ToListAsync();

            var sessionIds = enrollments
                .SelectMany(e => e.Course.CourseSessions.Select(s => s.Id))
                .ToList();

            var lessonProgressMap = await _db.LessonProgress
                .AsNoTracking()
                .Where(lp => lp.StudentId == student.student_id && sessionIds.Contains(lp.CourseSessionId))
                .ToDictionaryAsync(lp => lp.CourseSessionId);

            var recentCourses = enrollments.Select(e =>
            {
                var sessions = e.Course.CourseSessions.ToList();
                int total = sessions.Count;
                int completed = sessions.Count(s =>
                    lessonProgressMap.TryGetValue(s.Id, out var lp) && lp.Views > 0);
                int progress = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

                return new RecentCourseDto
                {
                    CourseId = e.CourseId,
                    Title = e.Course.Title,
                    Introduction = e.Course.Introduction,
                    PictureUrl = e.Course.PictureUrl,
                    TeacherName = e.Course.Teacher?.User != null
                        ? $"{e.Course.Teacher.User.firstname} {e.Course.Teacher.User.lastname}".Trim()
                        : "N/A",
                    ProgressPercent = progress,
                    TotalSessions = total,
                    CompletedSessions = completed
                };
            }).ToList();

            int overallProgress = recentCourses.Any()
                ? (int)recentCourses.Average(c => c.ProgressPercent)
                : 0;

            int unread = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return Ok(new StudentHomeResponseDto
            {
                StudentName = $"{user!.firstname} {user.lastname}".Trim(),
                AcademicLevel = student.AcademicLevel ?? string.Empty,
                UnreadNotifications = unread,
                Statistics = new StudentDashboardStatsDto
                {
                    Absence = (int)stats.Absence,
                    TasksSubmitted = (int)stats.Tasks,
                    QuizzesTaken = (int)stats.Quiz,
                    OverallProgress = overallProgress
                },
                RecentCourses = recentCourses
            });
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var stats = await _statisticsService.GetStudentStatisticsAsync(student.student_id);
            return Ok(stats);
        }

        [HttpGet("achievement")]
        public async Task<IActionResult> GetAchievement()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var studentId = student.student_id;

            var enrollments = await _db.Enrollments
                .AsNoTracking()
                .Include(e => e.Course).ThenInclude(c => c.Teacher).ThenInclude(t => t.User)
                .Include(e => e.Course).ThenInclude(c => c.CourseSessions)
                .Where(e => e.StudentId == studentId)
                .ToListAsync();

            var allSessionIds = enrollments
                .SelectMany(e => e.Course.CourseSessions.Select(s => s.Id))
                .ToList();

            var lessonProgressMap = await _db.LessonProgress
                .AsNoTracking()
                .Where(lp => lp.StudentId == studentId && allSessionIds.Contains(lp.CourseSessionId))
                .ToDictionaryAsync(lp => lp.CourseSessionId);

            var quizResults = await _db.StudentQuizResults
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .ToListAsync();

            var stats = await _statisticsService.GetStudentStatisticsAsync(studentId);

            decimal avgQuizScore = quizResults.Any()
                ? Math.Round(quizResults.Average(r => r.Percentage), 1)
                : 0;

            var activity = enrollments.Select(e =>
            {
                var sessions = e.Course.CourseSessions.ToList();
                int total = sessions.Count;
                int completed = sessions.Count(s =>
                    lessonProgressMap.TryGetValue(s.Id, out var lp) && lp.Views > 0);
                int progress = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

                return new CourseActivityDto
                {
                    CourseId = e.CourseId,
                    Title = e.Course.Title,
                    TeacherName = e.Course.Teacher?.User != null
                        ? $"{e.Course.Teacher.User.firstname} {e.Course.Teacher.User.lastname}".Trim()
                        : "N/A",
                    ProgressPercent = progress,
                    EnrolledAt = e.EnrolledAt
                };
            }).ToList();

            int completedCourses = activity.Count(a => a.ProgressPercent == 100);

            return Ok(new AchievementDto
            {
                TotalEnrolled = enrollments.Count,
                CompletedCourses = completedCourses,
                AverageQuizScore = avgQuizScore,
                TotalAbsences = (int)stats.Absence,
                TotalHomeworkSubmitted = (int)stats.Tasks,
                TotalQuizzesTaken = (int)stats.Quiz,
                CoursesActivity = activity
            });
        }

        // ─────────────────────────────────────────────────────────────
        // PROFILE
        // ─────────────────────────────────────────────────────────────

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());

            var student = await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.user_id == userId);

            if (student == null || user == null)
                return NotFound(new { message = "Profile not found." });

            return Ok(new StudentProfileDto
            {
                UserId = user.Id,
                FirstName = user.firstname,
                LastName = user.lastname,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
                LanguagePref = user.language_pref,
                ImageUrl = user.ProfileImageUrl,
                AvatarInitials = (user.firstname?.Substring(0, 1).ToUpper() ?? "") +
                     (user.lastname?.Substring(0, 1).ToUpper() ?? ""),
                AcademicLevel = student.AcademicLevel,
                ClassLevel = student.AcademicYear.ToString(),
                ParentPhoneNumber = student.ParentPhoneNumber
            });
        }

        [HttpPost("profile/photo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddPhoto([FromForm] AddPhotoDto dto)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                return NotFound("User not found");

            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("No file uploaded");

            if (!string.IsNullOrEmpty(user.ProfileImagePublicId))
                await _photoService.DeletePhotoAsync(user.ProfileImagePublicId);

            var result = await _photoService.UploadPhotoAsync(dto.File);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

            user.ProfileImageUrl = result.SecureUrl?.AbsoluteUri;
            user.ProfileImagePublicId = result.PublicId;

            await _userManager.UpdateAsync(user);

            return Ok(new { imageUrl = user.ProfileImageUrl });
        }

        [HttpDelete("profile/deletephoto")]
        public async Task<IActionResult> DeletePhoto()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                return NotFound(new { message = "User not found" });

            if (string.IsNullOrEmpty(user.ProfileImagePublicId))
                return BadRequest(new { message = "No photo to delete" });

            await _photoService.DeletePhotoAsync(user.ProfileImagePublicId);

            user.ProfileImageUrl = null;
            user.ProfileImagePublicId = null;

            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                success = true,
                message = "Photo deleted successfully",
                imageUrl = (string?)null
            });
        }

        [HttpPut("Update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateStudentProfileDto dto)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userManager.FindByIdAsync(userId.ToString());

            var student = await _db.Students
                .FirstOrDefaultAsync(s => s.user_id == userId);

            if (student == null || user == null)
                return NotFound(new { message = "Profile not found." });

            if (!string.IsNullOrWhiteSpace(dto.FirstName))
                user.firstname = dto.FirstName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.LastName))
                user.lastname = dto.LastName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Phone))
                user.PhoneNumber = dto.Phone.Trim();

            if (!string.IsNullOrWhiteSpace(dto.LanguagePref))
                user.language_pref = dto.LanguagePref.Trim();

            var userUpdateResult = await _userManager.UpdateAsync(user);
            if (!userUpdateResult.Succeeded)
                return BadRequest(new { errors = userUpdateResult.Errors.Select(e => e.Description) });

            if (!string.IsNullOrWhiteSpace(dto.ParentPhoneNumber))
                student.ParentPhoneNumber = dto.ParentPhoneNumber.Trim();

            await _db.SaveChangesAsync();

            return Ok(new StudentProfileDto
            {
                UserId = user.Id,
                FirstName = user.firstname,
                LastName = user.lastname,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
                LanguagePref = user.language_pref,
                ImageUrl = user.ProfileImageUrl,
                AvatarInitials = (user.firstname?.Substring(0, 1).ToUpper() ?? "") +
                     (user.lastname?.Substring(0, 1).ToUpper() ?? ""),
                AcademicLevel = student.AcademicLevel,
                ClassLevel = student.AcademicYear.ToString(),
                ParentPhoneNumber = student.ParentPhoneNumber
            });
        }

        // ─────────────────────────────────────────────────────────────
        // MESSAGES
        // ─────────────────────────────────────────────────────────────

        [HttpGet("messages")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = GetCurrentUserId();

            var messages = await _db.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var partnerId = g.Key;
                    var last = g.First();
                    var partner = last.SenderId == partnerId ? last.Sender : last.Receiver;

                    return new ConversationDto
                    {
                        PartnerId = partnerId,
                        PartnerName = $"{partner.firstname} {partner.lastname}".Trim(),
                        LastMessage = last.Content,
                        SentAt = last.SentAt,
                        UnreadCount = g.Count(m => m.SenderId == partnerId && !m.IsRead)
                    };
                })
                .OrderByDescending(c => c.SentAt)
                .ToList();

            return Ok(conversations);
        }


        [HttpGet("messages/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();

            var unreadCount = await _db.Messages
                .CountAsync(m =>
                    m.ReceiverId == userId &&
                    !m.IsRead);

            return Ok(new { unreadCount });
        }


        [HttpGet("messages/{partnerId:guid}")]
        public async Task<IActionResult> GetMessages(Guid partnerId)
        {
            var userId = GetCurrentUserId();

            var messages = await _db.Messages
                .AsNoTracking()
                .Where(m =>
                    (m.SenderId == userId && m.ReceiverId == partnerId) ||
                    (m.SenderId == partnerId && m.ReceiverId == userId))
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsMine = m.SenderId == userId,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            var unread = await _db.Messages
                .Where(m => m.SenderId == partnerId && m.ReceiverId == userId && !m.IsRead)
                .ToListAsync();

            if (unread.Any())
            {
                unread.ForEach(m => m.IsRead = true);
                await _db.SaveChangesAsync();
            }

            return Ok(messages);
        }

        [HttpPost("messages/{receiverId:guid}")]
        public async Task<IActionResult> SendMessage(Guid receiverId, [FromBody] SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
                return BadRequest(new { message = "Message content cannot be empty." });

            var userId = GetCurrentUserId();

            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var isMyModerator = await _db.Enrollments.AnyAsync(e =>
                e.StudentId == student.student_id &&
                e.Course.Teacher.Moderator.Id == receiverId);

            if (!isMyModerator) return Forbid();

            var message = new Message
            {
                SenderId = userId,
                ReceiverId = receiverId,
                Content = dto.Content.Trim()
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            return Ok(new { message.Id, message.SentAt });
        }
        // ─────────────────────────────────────────────────────────────
        // LESSON VIEW & PROGRESS
        // ─────────────────────────────────────────────────────────────

        // Call ONCE when the student opens/starts the lesson player.
        // This is the only place Views gets incremented.
        [HttpPost("lesson/{sessionId:int}/view")]
        public async Task<IActionResult> RecordView(int sessionId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var studentId = student.student_id;

            var session = await _db.CourseSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == studentId && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            // ── Lock check ───────────────────────────────────────────
            var allSessions = await _db.CourseSessions
                .AsNoTracking()
                .Where(cs => cs.CourseId == session.CourseId)
                .OrderBy(cs => cs.Id)
                .ToListAsync();

            int index = allSessions.FindIndex(cs => cs.Id == sessionId);
            bool isLocked = await IsSessionLockedAsync(studentId, session, index);

            if (isLocked)
                return StatusCode(423, new { message = "This session is locked. Complete the previous session and pass its test first." });
            // ────────────────────────────────────────────────────────

            var existing = await _db.LessonProgress
                .FirstOrDefaultAsync(lp =>
                    lp.StudentId == studentId && lp.CourseSessionId == sessionId);

            if (existing == null)
            {
                existing = new LessonProgress
                {
                    StudentId = studentId,
                    CourseSessionId = sessionId,
                    Views = 1,
                    MaxViews = session.MaxViews, // ← copy from session on first create
                    ProgressPercent = 0,
                    LastWatched = DateTime.UtcNow
                };
                _db.LessonProgress.Add(existing);
            }
            else
            {
                // use student-specific MaxViews; fall back to session default if not set
                int effectiveMaxViews = existing.MaxViews > 0 ? existing.MaxViews : session.MaxViews;

                if (existing.Views >= effectiveMaxViews)
                    return BadRequest(new { message = "Max views reached for this lesson." });

                existing.Views += 1;
                existing.LastWatched = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // return the student-specific ceiling, not the global session default
            int currentMax = existing.MaxViews > 0 ? existing.MaxViews : session.MaxViews;
            return Ok(new { views = existing.Views, maxViews = currentMax });
        }


        // Call repeatedly while the video plays (e.g. every 10-15s) and on pause/exit.
        // Never touches Views — only tracks the furthest position reached.
        [HttpPost("lesson/{sessionId:int}/progress")]
        public async Task<IActionResult> UpdateLessonProgress(
            int sessionId,
            [FromBody] UpdateLessonProgressDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var studentId = student.student_id;

            var session = await _db.CourseSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == studentId && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            double progress = Math.Clamp(dto.ProgressPercent, 0, 100);

            var existing = await _db.LessonProgress
                .FirstOrDefaultAsync(lp =>
                    lp.StudentId == studentId && lp.CourseSessionId == sessionId);

            if (existing == null)
            {
                existing = new LessonProgress
                {
                    StudentId = studentId,
                    CourseSessionId = sessionId,
                    Views = 0,
                    ProgressPercent = progress,
                    LastWatched = DateTime.UtcNow
                };
                _db.LessonProgress.Add(existing);
            }
            else
            {
                existing.ProgressPercent = Math.Max(existing.ProgressPercent, progress);
                existing.LastWatched = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "Progress updated.", ProgressPercent = existing.ProgressPercent });
        }

        // ─────────────────────────────────────────────────────────────
        // COURSE SESSIONS (with lock state)  
        // ─────────────────────────────────────────────────────────────


        [HttpGet("course/{courseId:int}/sessions")]
        public async Task<IActionResult> GetCourseSessions(int courseId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e =>
                    e.StudentId == student.student_id &&
                    e.CourseId == courseId);

            if (!enrolled) return Forbid();

            var sessions = await _db.CourseSessions
                .AsNoTracking()
                .Where(cs => cs.CourseId == courseId)
                .OrderBy(cs => cs.Id)
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();

            var progressMap = await _db.LessonProgress
                .AsNoTracking()
                .Where(lp =>
                    lp.StudentId == student.student_id &&
                    sessionIds.Contains(lp.CourseSessionId))
                .ToDictionaryAsync(lp => lp.CourseSessionId);

            var passedSessionIds = await _db.LessonAttempts
                .AsNoTracking()
                .Where(la =>
                    la.StudentId == student.student_id &&
                    sessionIds.Contains(la.CourseSessionId) &&
                    la.Passed)
                .Select(la => la.CourseSessionId)
                .Distinct()
                .ToListAsync();

            var result = new List<object>();

            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                bool isLocked = await IsSessionLockedAsync(student.student_id, s, i);

                progressMap.TryGetValue(s.Id, out var progress);

                result.Add(new
                {
                    s.Id,
                    s.Title,
                    s.HasEntryTest,
                    MaxViews = progress?.MaxViews ?? s.MaxViews,
                    IsLocked = isLocked,
                    ProgressPercent = progress?.ProgressPercent ?? 0,
                    Views = progress?.Views ?? 0,
                    TestPassed = passedSessionIds.Contains(s.Id)
                });
            }

            return Ok(result);
        }




        // ─────────────────────────────────────────────────────────────
        // LESSON DETAILS
        // ─────────────────────────────────────────────────────────────

        [HttpGet("lesson/{sessionId:int}/details")]
        public async Task<IActionResult> GetLessonDetails(int sessionId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var session = await _db.CourseSessions
                .AsNoTracking()
                .Include(cs => cs.Files)
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            // ── Lock check ───────────────────────────────────────────
            var allSessions = await _db.CourseSessions
                .AsNoTracking()
                .Where(cs => cs.CourseId == session.CourseId)
                .OrderBy(cs => cs.Id)
                .ToListAsync();

            int index = allSessions.FindIndex(cs => cs.Id == sessionId);
            bool isLocked = await IsSessionLockedAsync(student.student_id, session, index);

            if (isLocked)
                return StatusCode(423, new { message = "This session is locked. Complete the previous session and pass its test first." });
            // ────────────────────────────────────────────────────────

            return Ok(new
            {
                session.Id,
                session.Title,
                session.MaxViews,
                session.AvailableDays,
                session.HomeworkFileUrl,
                session.HomeworkFileName,
                session.HasEntryTest,
                Files = session.Files.Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FileType,
                    f.FileSize,
                    f.FileUrl
                })
            });
        }

        // ─────────────────────────────────────────────────────────────
        // QUIZ
        // ─────────────────────────────────────────────────────────────

        [HttpGet("quiz/{sessionId:int}")]
        public async Task<IActionResult> GetQuiz(int sessionId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var session = await _db.CourseSessions
                .AsNoTracking()
                .Include(cs => cs.EntryTest)
                    .ThenInclude(q => q != null ? q.Questions : null)
                        .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            if (!session.HasEntryTest || session.EntryTest == null)
                return NotFound(new { message = "This session has no entry test." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            // ── Lock check ───────────────────────────────────────────
            var allSessions = await _db.CourseSessions
                .AsNoTracking()
                .Where(cs => cs.CourseId == session.CourseId)
                .OrderBy(cs => cs.Id)
                .ToListAsync();

            int index = allSessions.FindIndex(cs => cs.Id == sessionId);
            bool isLocked = await IsSessionLockedAsync(student.student_id, session, index);

            if (isLocked)
                return StatusCode(423, new { message = "This session is locked. Complete the previous session and pass its test first." });
            // ────────────────────────────────────────────────────────

            var quiz = session.EntryTest;

            var lastAttempt = await _db.LessonAttempts
                .AsNoTracking()
                .Where(la => la.StudentId == student.student_id && la.CourseSessionId == sessionId)
                .OrderByDescending(la => la.TakenAt)
                .FirstOrDefaultAsync();

            bool alreadyPassed = lastAttempt?.Passed ?? false;

            DateTime? canRetakeAt = null;
            if (lastAttempt != null && !alreadyPassed && quiz.RetakeIntervalHours > 0)
            {
                var retakeTime = lastAttempt.TakenAt.AddHours(quiz.RetakeIntervalHours);
                if (retakeTime > DateTime.UtcNow)
                    canRetakeAt = retakeTime;
            }

            decimal? lastScorePct = null;
            if (lastAttempt != null)
            {
                int qCount = quiz.Questions?.Count ?? 0;
                lastScorePct = qCount > 0
                    ? Math.Round((decimal)lastAttempt.Score / qCount * 100, 1)
                    : lastAttempt.Score;
            }

            // ── Rebuild the per-question breakdown of the passing attempt ──────
            // We don't store a breakdown directly — only the raw selected answers
            // (StudentQuizResult.AnswersJson). Reconstruct it the same way
            // SubmitQuiz does, using the already-loaded Questions/Options.
            List<QuizBreakdownItemDto>? lastBreakdown = null;
            if (alreadyPassed)
            {
                var lastResult = await _db.StudentQuizResults
                    .AsNoTracking()
                    .Where(r => r.StudentId == student.student_id
                             && r.QuizId == quiz.Id
                             && r.Passed)
                    .OrderByDescending(r => r.SubmittedAt)
                    .FirstOrDefaultAsync();

                if (lastResult != null && !string.IsNullOrEmpty(lastResult.AnswersJson))
                {
                    Dictionary<int, int> savedAnswers;
                    try
                    {
                        savedAnswers = JsonSerializer.Deserialize<Dictionary<int, int>>(lastResult.AnswersJson)
                            ?? new Dictionary<int, int>();
                    }
                    catch
                    {
                        savedAnswers = new Dictionary<int, int>();
                    }

                    lastBreakdown = quiz.Questions?.Select(q =>
                    {
                        savedAnswers.TryGetValue(q.Id, out int selectedOptionId);
                        var correctOpt = q.Options.FirstOrDefault(o => o.IsCorrect);
                        bool isCorrect = selectedOptionId != 0
                            && q.Options.Any(o => o.Id == selectedOptionId && o.IsCorrect);

                        return new QuizBreakdownItemDto
                        {
                            QuestionId = q.Id,
                            SelectedOptionId = selectedOptionId == 0 ? null : selectedOptionId,
                            CorrectOptionId = correctOpt?.Id,
                            IsCorrect = isCorrect
                        };
                    }).ToList();
                }
            }

            return Ok(new QuizDetailsDto
            {
                QuizId = quiz.Id,
                Title = quiz.Title,
                PassingScore = quiz.PassingScore,
                RetakeIntervalHours = quiz.RetakeIntervalHours,
                AlreadyPassed = alreadyPassed,
                LastScore = lastScorePct,
                CanRetakeAt = canRetakeAt,
                // Always return questions+options (text only, no IsCorrect leak) —
                // the review screen needs them to render. Hiding them only made
                // sense if the goal was "don't let a passed student see the quiz
                // again," but they can't resubmit anyway (SubmitQuiz already
                // blocks that), so there's nothing to protect by hiding it.
                Questions = quiz.Questions?.Select(q => new QuizQuestionDto
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    Options = q.Options.Select(o => new QuizOptionDto
                    {
                        OptionId = o.Id,
                        Text = o.Text
                    })
                }) ?? Enumerable.Empty<QuizQuestionDto>(),
                LastBreakdown = lastBreakdown
            });
        }

        [HttpGet("courses/{courseId:int}/quizzes")]
        public async Task<IActionResult> GetCourseQuizzes(int courseId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == courseId);

            if (!enrolled) return Forbid();

            var sessions = await _db.CourseSessions
                .AsNoTracking()
                .Include(cs => cs.EntryTest)
                .Where(cs => cs.CourseId == courseId && cs.HasEntryTest && cs.EntryTest != null)
                .OrderBy(cs => cs.Id)
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();

            var lastAttempts = await _db.LessonAttempts
                .AsNoTracking()
                .Where(la => la.StudentId == student.student_id && sessionIds.Contains(la.CourseSessionId))
                .ToListAsync();

            var result = sessions.Select((s, index) =>
            {
                var quiz = s.EntryTest!;

                var attempt = lastAttempts
                    .Where(la => la.CourseSessionId == s.Id)
                    .OrderByDescending(la => la.TakenAt)
                    .FirstOrDefault();

                bool alreadyPassed = attempt?.Passed ?? false;

                DateTime? canRetakeAt = null;
                if (attempt != null && !alreadyPassed && quiz.RetakeIntervalHours > 0)
                {
                    var retakeTime = attempt.TakenAt.AddHours(quiz.RetakeIntervalHours);
                    if (retakeTime > DateTime.UtcNow)
                        canRetakeAt = retakeTime;
                }

                return new CourseQuizSummaryDto
                {
                    QuizNumber = index + 1,
                    SessionId = s.Id,
                    QuizId = quiz.Id,
                    Title = quiz.Title,
                    AlreadyPassed = alreadyPassed,
                    CanRetakeAt = canRetakeAt
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPost("quiz/{sessionId:int}/submit")]
        public async Task<IActionResult> SubmitQuiz(
            int sessionId,
            [FromBody] SubmitQuizDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var session = await _db.CourseSessions
                .AsNoTracking()
                .Include(cs => cs.EntryTest)
                    .ThenInclude(q => q != null ? q.Questions : null)
                        .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null || !session.HasEntryTest || session.EntryTest == null)
                return NotFound(new { message = "Session or entry test not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            var quiz = session.EntryTest;

            var lastAttempt = await _db.LessonAttempts
                .AsNoTracking()
                .Where(la => la.StudentId == student.student_id && la.CourseSessionId == sessionId)
                .OrderByDescending(la => la.TakenAt)
                .FirstOrDefaultAsync();

            if (lastAttempt != null && lastAttempt.Passed)
                return BadRequest(new { message = "You have already passed this quiz." });

            if (lastAttempt != null && quiz.RetakeIntervalHours > 0)
            {
                var retakeTime = lastAttempt.TakenAt.AddHours(quiz.RetakeIntervalHours);
                if (retakeTime > DateTime.UtcNow)
                    return BadRequest(new
                    {
                        message = "Retake interval has not expired yet.",
                        canRetakeAt = retakeTime
                    });
            }

            int correct = 0;
            int total = quiz.Questions?.Count ?? 0;

            var breakdown = quiz.Questions?.Select(q =>
            {
                dto.Answers.TryGetValue(q.Id, out int selectedOptionId);
                var correctOpt = q.Options.FirstOrDefault(o => o.IsCorrect);
                bool isCorrect = selectedOptionId != 0
                    && q.Options.Any(o => o.Id == selectedOptionId && o.IsCorrect);

                if (isCorrect) correct++;

                return new QuizBreakdownItemDto
                {
                    QuestionId = q.Id,
                    SelectedOptionId = selectedOptionId == 0 ? null : selectedOptionId,
                    CorrectOptionId = correctOpt?.Id,
                    IsCorrect = isCorrect
                };
            }).ToList() ?? new List<QuizBreakdownItemDto>();

            decimal percentage = total > 0
                ? Math.Round((decimal)correct / total * 100, 1)
                : 0;

            bool passed = percentage >= quiz.PassingScore;

            var attempt = new LessonAttempt
            {
                StudentId = student.student_id,
                CourseSessionId = sessionId,
                Score = correct,
                Passed = passed,
                TakenAt = DateTime.UtcNow
            };
            _db.LessonAttempts.Add(attempt);

            var quizResult = new StudentQuizResult
            {
                StudentId = student.student_id,
                QuizId = quiz.Id,
                Score = correct,
                TotalQuestions = total,
                Percentage = percentage,
                Passed = passed,
                AnswersJson = JsonSerializer.Serialize(dto.Answers),
                SubmittedAt = DateTime.UtcNow
            };
            _db.StudentQuizResults.Add(quizResult);

            await _db.SaveChangesAsync();

            return Ok(new QuizResultDto
            {
                Score = correct,
                TotalQuestions = total,
                Percentage = percentage,
                Passed = passed,
                Breakdown = breakdown
            });
        }

        // ─────────────────────────────────────────────────────────────
        // HOMEWORK
        // ─────────────────────────────────────────────────────────────

        [HttpPost("homework/{sessionId:int}/submit")]
        public async Task<IActionResult> SubmitHomework(
            int sessionId,
            [FromBody] SubmitHomeworkDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var session = await _db.CourseSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            if (string.IsNullOrEmpty(session.HomeworkFileUrl))
                return BadRequest(new { message = "This session does not have a homework assignment." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            var existing = await _db.HomeworkSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.StudentId == student.student_id && h.SessionId == sessionId);

            if (existing != null)
                return BadRequest(new { message = "You have already submitted homework for this session." });

            var submission = new HomeworkSubmission
            {
                StudentId = student.student_id,
                SessionId = sessionId,
                FileName = dto.FileName,
                FileUrl = dto.FileUrl,
                FileSizeBytes = dto.FileSizeBytes
            };

            _db.HomeworkSubmissions.Add(submission);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Homework submitted successfully.", submissionId = submission.Id });
        }

        [HttpGet("homework/{sessionId:int}")]
        public async Task<IActionResult> GetHomeworkStatus(int sessionId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var submission = await _db.HomeworkSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.StudentId == student.student_id && h.SessionId == sessionId);

            if (submission == null)
                return NotFound(new { message = "No homework submission found for this session." });

            return Ok(new HomeworkStatusDto
            {
                SubmissionId = submission.Id,
                FileName = submission.FileName,
                FileUrl = submission.FileUrl,
                SubmittedAt = submission.SubmittedAt,
                IsReviewed = submission.IsReviewed,
                Grade = submission.Grade,
                TeacherComment = submission.TeacherComment
            });
        }

        // ─────────────────────────────────────────────────────────────
        // STUDENT REQUESTS
        // ─────────────────────────────────────────────────────────────

        [HttpPost("request/views")]
        public async Task<IActionResult> RequestExtraViews([FromBody] StudentRequestDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var session = await _db.CourseSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == dto.SessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            var progress = await _db.LessonProgress
                .AsNoTracking()
                .FirstOrDefaultAsync(lp =>
                    lp.StudentId == student.student_id && lp.CourseSessionId == dto.SessionId);

            int currentViews = progress?.Views ?? 0;

            var duplicate = await _db.StudentRequests
                .AsNoTracking()
                .AnyAsync(r =>
                    r.StudentId == student.student_id &&
                    r.LessonId == dto.SessionId &&
                    r.Type == "view" &&
                    r.Status == "Pending");

            if (duplicate)
                return BadRequest(new { message = "You already have a pending view request for this session." });

            var request = new StudentRequests
            {
                Id = Guid.NewGuid(),
                StudentId = student.student_id,
                LessonId = dto.SessionId,
                Type = "view",
                CurrentCount = currentViews,
                Reason = dto.Reason,
                Status = "Pending"
            };

            _db.StudentRequests.Add(request);
            await _db.SaveChangesAsync();

            return Ok(new { message = "View request submitted.", requestId = request.Id });
        }

        [HttpPost("request/retake")]
        public async Task<IActionResult> RequestQuizRetake([FromBody] StudentRequestDto dto)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var session = await _db.CourseSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == dto.SessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            var enrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == student.student_id && e.CourseId == session.CourseId);

            if (!enrolled) return Forbid();

            var lastAttempt = await _db.LessonAttempts
                .AsNoTracking()
                .Where(la => la.StudentId == student.student_id && la.CourseSessionId == dto.SessionId)
                .OrderByDescending(la => la.TakenAt)
                .FirstOrDefaultAsync();

            int currentScore = lastAttempt?.Score ?? 0;

            var duplicate = await _db.StudentRequests
                .AsNoTracking()
                .AnyAsync(r =>
                    r.StudentId == student.student_id &&
                    r.LessonId == dto.SessionId &&
                    r.Type == "test" &&
                    r.Status == "Pending");

            if (duplicate)
                return BadRequest(new { message = "You already have a pending retake request for this session." });

            var request = new StudentRequests
            {
                Id = Guid.NewGuid(),
                StudentId = student.student_id,
                LessonId = dto.SessionId,
                Type = "test",
                CurrentCount = currentScore,
                Reason = dto.Reason,
                Status = "Pending"
            };

            _db.StudentRequests.Add(request);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Retake request submitted.", requestId = request.Id });
        }

        [HttpGet("request/my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            var requests = await _db.StudentRequests
                .AsNoTracking()
                .Include(r => r.CourseSession)
                .Where(r => r.StudentId == student.student_id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    SessionTitle = r.CourseSession.Title,
                    r.Type,
                    r.Reason,
                    r.Status,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(requests);
        }
    }
}