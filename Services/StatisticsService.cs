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
            _logger.LogInformation(
                "Computing live statistics for student {StudentId}", studentId);


            var absenceCount = await _db.HomeworkSubmissions
                .CountAsync(h => h.StudentId == studentId); 

            var tasksCount = await _db.HomeworkSubmissions
                .CountAsync(h => h.StudentId == studentId);

            var quizCount = await _db.StudentQuizResults
                .CountAsync(r => r.StudentId == studentId);

            return new StudentStatisticsDto
            {
                StudentId = studentId,
                Absence = absenceCount,
                Tasks = tasksCount,
                Quiz = quizCount
            };
        }

        public async Task<IEnumerable<StudentStatisticsDto>> GetAllStudentsStatisticsAsync()
        {
            _logger.LogInformation("Computing live statistics for all students");

            var studentIds = await _db.Students
                                      .Select(s => s.student_id)
                                      .ToListAsync();

            var absenceCounts = await _db.StudentAbsences
                .GroupBy(a => a.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .ToListAsync();

            var taskCounts = await _db.HomeworkSubmissions
                .GroupBy(h => h.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .ToListAsync();

            var quizCounts = await _db.StudentQuizResults
                .GroupBy(r => r.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .ToListAsync();

            var absenceMap = absenceCounts.ToDictionary(x => x.StudentId, x => x.Count);
            var taskMap    = taskCounts.ToDictionary(x => x.StudentId, x => x.Count);
            var quizMap    = quizCounts.ToDictionary(x => x.StudentId, x => x.Count);

            return studentIds.Select(id => new StudentStatisticsDto
            {
                StudentId = id,
                Absence   = absenceMap.GetValueOrDefault(id, 0),
                Tasks     = taskMap.GetValueOrDefault(id, 0),
                Quiz      = quizMap.GetValueOrDefault(id, 0)
            });
        }
    }
}
