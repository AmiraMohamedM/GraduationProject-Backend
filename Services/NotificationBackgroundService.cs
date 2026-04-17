using grad.Data;
using grad.Models;
using Microsoft.EntityFrameworkCore;

namespace grad.Services
{
    public class NotificationBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(30);

        private const int DeadlineWarningHours = 24;

        private const double LowViewsThreshold = 0.80;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationBackgroundService> _logger;

        public NotificationBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunChecksAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during notification check cycle.");
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }

        
        private async Task RunChecksAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;

            await CheckDeadlinesAsync(db, now, ct);
            await CheckLowViewsAsync(db, ct);
            await CheckNewContentAsync(db, now, ct);

            await db.SaveChangesAsync(ct);
        }

   
        private async Task CheckDeadlinesAsync(AppDbContext db, DateTime now, CancellationToken ct)
        {
            var windowEnd = now.AddHours(DeadlineWarningHours);
            var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);

            var enrollments = await db.Enrollments
                .Include(e => e.Student)
                    .ThenInclude(s => s.User)
                .Include(e => e.Course)
                    .ThenInclude(c => c.CourseSessions)
                .Where(e => e.Course.CourseSessions.Any())
                .ToListAsync(ct);

            foreach (var enrollment in enrollments)
            {
                foreach (var session in enrollment.Course.CourseSessions)
                {
                    bool hasHomework = !string.IsNullOrEmpty(session.HomeworkUrl);
                    bool hasQuiz = session.HasEntryTest;
                    if (!hasHomework && !hasQuiz) continue;

                    var enrolledAt = DateTime.SpecifyKind(enrollment.EnrolledAt, DateTimeKind.Utc);
                    var dueDate = enrolledAt.AddDays(session.AvailableDays);

                    if (dueDate < now || dueDate > windowEnd) continue;

                    if (hasHomework)
                    {
                        bool submitted = await db.HomeworkSubmissions
                            .AnyAsync(h =>
                                h.StudentId == enrollment.StudentId &&
                                h.SessionId == session.Id, ct);
                        if (submitted) continue;
                    }

                    if (hasQuiz && session.EntryTestId.HasValue)
                    {
                        bool quizDone = await db.StudentQuizResults
                            .AnyAsync(r =>
                                r.StudentId == enrollment.StudentId &&
                                r.QuizId == session.EntryTestId.Value, ct);
                        if (quizDone) continue;
                    }

                    // De-duplicate: skip if already notified today
                    var alreadyNotified = await db.Notifications.AnyAsync(n =>
                        n.UserId == enrollment.Student.user_id &&
                        n.Type == "deadline" &&
                        n.CreatedAt >= todayStart &&
                        n.Title.Contains(session.Title), ct);

                    if (alreadyNotified) continue;

                    var taskLabel = hasQuiz ? "quiz" : "homework";
                    var hoursLeft = (int)(dueDate - now).TotalHours;

                    db.Notifications.Add(new Notification
                    {
                        UserId = enrollment.Student.user_id,
                        Title = $"⏰ {taskLabel.ToUpper()} due in {hoursLeft}h",
                        Body = $"Don't forget to submit your {taskLabel} for \"{session.Title}\". " +
                                    $"Due at {dueDate:MMM dd, HH:mm} UTC.",
                        Type = "deadline",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    _logger.LogInformation(
                        "Deadline notification queued → student {S}, session \"{Sess}\"",
                        enrollment.StudentId, session.Title);
                }
            }
        }

        private async Task CheckLowViewsAsync(AppDbContext db, CancellationToken ct)
        {
            var atRiskProgress = await db.LessonProgress
                .Include(lp => lp.CourseSession)
                .Where(lp => lp.MaxViews > 0 &&
                             (double)lp.Views / lp.MaxViews >= LowViewsThreshold)
                .ToListAsync(ct);

            foreach (var lp in atRiskProgress)
            {
                var student = await db.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.student_id == lp.StudentId, ct);
                if (student is null) continue;

                var alreadyNotified = await db.Notifications.AnyAsync(n =>
                    n.UserId == student.user_id &&
                    n.Type == "low_views" &&
                    n.Title.Contains(lp.CourseSession.Title), ct);

                if (alreadyNotified) continue;

                int remaining = lp.MaxViews - lp.Views;
                int usedPercent = (int)((double)lp.Views / lp.MaxViews * 100);

                db.Notifications.Add(new Notification
                {
                    UserId = student.user_id,
                    Title = $"⚠️ Low views remaining for \"{lp.CourseSession.Title}\"",
                    Body = $"You have used {usedPercent}% of your allowed views " +
                                $"({lp.Views}/{lp.MaxViews}). Only {remaining} view(s) left.",
                    Type = "low_views",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "Low-views notification queued → student {S}, session \"{Sess}\" ({P}%)",
                    lp.StudentId, lp.CourseSession.Title, usedPercent);
            }
        }


        private async Task CheckNewContentAsync(AppDbContext db, DateTime now, CancellationToken ct)
        {
            var allSessions = await db.CourseSessions
                .Include(cs => cs.Course)
                    .ThenInclude(c => c.Enrollments)
                        .ThenInclude(e => e.Student)
                .ToListAsync(ct);

            foreach (var session in allSessions)
            {
                foreach (var enrollment in session.Course.Enrollments)
                {
                    var student = enrollment.Student;

                    if (student == null || student.user_id == Guid.Empty)
                    {
                        continue;
                    }

                    bool alreadyNotified = await db.Notifications.AnyAsync(n =>
                        n.UserId == student.user_id &&
                        n.Type == "new_content" &&
                        n.Title.Contains(session.Title), ct);

                    if (alreadyNotified) continue;

                    db.Notifications.Add(new Notification
                    {
                        UserId = student.user_id,
                        Title = $"📚 New session in \"{session.Course.Title}\"",
                        Body = $"A new session \"{session.Title}\" has been added to your course. Check it out!",
                        Type = "new_content",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    _logger.LogInformation(
                         "New-content notification queued → student {S}, session \"{Sess}\"",
                         student.student_id, session.Title);
                } 
            } 
        } 
    } 
} 
