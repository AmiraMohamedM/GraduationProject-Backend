using grad.Data;
using grad.DTOs;
using Microsoft.EntityFrameworkCore;

namespace grad.Services
{

    public class StatisticsService : IStatisticsService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<StatisticsService> _logger;

        public StatisticsService(AppDbContext db, ILogger<StatisticsService> logger)
        {
            _db     = db;
            _logger = logger;
        }


        public async Task<StudentStatisticsDto> GetStudentStatisticsAsync(Guid studentId)
        {
            var enrolledCourseIds = await _db.Enrollments
                .Where(e => e.StudentId == studentId)
                .Select(e => e.CourseId)
                .ToListAsync();

            var totalSessions = await _db.CourseSessions
                .CountAsync(cs => enrolledCourseIds.Contains(cs.CourseId));

            var absenceCount = await _db.StudentAbsences
                .CountAsync(a => a.StudentId == studentId);
            var absencePercent = totalSessions > 0
                ? Math.Round((decimal)absenceCount / totalSessions * 100, 1)
                : 0;

            var totalHomework = await _db.CourseSessions
                .CountAsync(cs => enrolledCourseIds.Contains(cs.CourseId) && cs.HomeworkFileUrl != null);
            var submittedHomework = await _db.HomeworkSubmissions
                .CountAsync(h => h.StudentId == studentId);
            var tasksPercent = totalHomework > 0
                ? Math.Round((decimal)submittedHomework / totalHomework * 100, 1)
                : 0;

            var quizResults = await _db.StudentQuizResults
                .Where(r => r.StudentId == studentId)
                .ToListAsync();
            var quizAvg = quizResults.Any()
                ? Math.Round(quizResults.Average(r => r.Percentage), 1)
                : 0;

            return new StudentStatisticsDto
            {
                StudentId = studentId,
                Absence = absencePercent,
                Tasks = tasksPercent,
                Quiz = quizAvg
            };
        }

        public async Task<IEnumerable<StudentStatisticsDto>> GetAllStudentsStatisticsAsync()
        {
            _logger.LogInformation("Computing live statistics for all students");

            var studentIds = await _db.Students
                .Select(s => s.student_id)
                .ToListAsync();

            var enrollments = await _db.Enrollments
                .GroupBy(e => e.StudentId)
                .Select(g => new { StudentId = g.Key, CourseIds = g.Select(e => e.CourseId).ToList() })
                .ToListAsync();

            var allSessionCounts = await _db.CourseSessions
                .GroupBy(cs => cs.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            var allHomeworkCounts = await _db.CourseSessions
                .Where(cs => cs.HomeworkFileUrl != null)
                .GroupBy(cs => cs.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            var absenceCounts = await _db.StudentAbsences
                .GroupBy(a => a.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StudentId, x => x.Count);

            var taskCounts = await _db.HomeworkSubmissions
                .GroupBy(h => h.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StudentId, x => x.Count);

            var quizAvgs = await _db.StudentQuizResults
                .GroupBy(r => r.StudentId)
                .Select(g => new { StudentId = g.Key, Avg = g.Average(r => r.Percentage) })
                .ToDictionaryAsync(x => x.StudentId, x => Math.Round(x.Avg, 1));

            var sessionCountMap = allSessionCounts.ToDictionary(x => x.CourseId, x => x.Count);
            var homeworkCountMap = allHomeworkCounts.ToDictionary(x => x.CourseId, x => x.Count);
            var enrollmentMap = enrollments.ToDictionary(x => x.StudentId, x => x.CourseIds);

            return studentIds.Select(id =>
            {
                var courseIds = enrollmentMap.GetValueOrDefault(id) ?? new List<int>();

                var totalSessions = courseIds.Sum(cid => sessionCountMap.GetValueOrDefault(cid, 0));
                var totalHomework = courseIds.Sum(cid => homeworkCountMap.GetValueOrDefault(cid, 0));

                var absenceCount = absenceCounts.GetValueOrDefault(id, 0);
                var submittedCount = taskCounts.GetValueOrDefault(id, 0);

                return new StudentStatisticsDto
                {
                    StudentId = id,
                    Absence = totalSessions > 0
                        ? Math.Round((decimal)absenceCount / totalSessions * 100, 1)
                        : 0,
                    Tasks = totalHomework > 0
                        ? Math.Round((decimal)submittedCount / totalHomework * 100, 1)
                        : 0,
                    Quiz = quizAvgs.GetValueOrDefault(id, 0)
                };
            });
        }
    }
    }

