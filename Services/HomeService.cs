//using grad.Data;
//using grad.DTOs;
//using grad.Models;
//using Microsoft.EntityFrameworkCore;

//namespace grad.Services
//{
//    public class HomeService
//    {
//        private readonly AppDbContext _context;

//        public HomeService(AppDbContext context)
//        {
//            _context = context;
//        }

//        public async Task<HomeResponseDto> GetHomeDataAsync(Guid userId)
//        {
//            var user = await _context.Users.FindAsync(userId);
//            if (user == null) return null;

//            var student = await _context.Students.FirstOrDefaultAsync(s => s.user_id == userId);

//            UserStatistics stats = null;
//            if (student != null)
//            {
//                stats = await _context.UserStatistics.FirstOrDefaultAsync(x => x.StudentId == student.student_id);
//            }

//            return new HomeResponseDto
//            {
//                StudentName = user.firstname,

//                Statistics = new StatisticsDto
//                {
//                    Absence = stats?.Absence ?? 0,
//                    Tasks = stats?.Tasks ?? 0,
//                    Quiz = stats?.Quiz ?? 0
//                },

//                PopularLessons = await _context.Lessons
//                    .OrderByDescending(x => x.Rating)
//                    .Take(5)
//                    .Select(x => new LessonDto
//                    {
//                        Id = x.Id,
//                        Title = x.Title,
//                        Category = x.Category,
//                        LessonCount = x.LessonCount,
//                        Duration = x.Duration,
//                        Rating = x.Rating,
//                        ImageUrl = x.ImageUrl
//                    })
//                    .ToListAsync(),

//                TodayEvents = await _context.Events
//                    .OrderByDescending(x => x.Date)
//                    .Take(3)
//                    .Select(x => new EventDto
//                    {
//                        Title = x.Title,
//                        Date = x.Date
//                    })
//                    .ToListAsync()
//            };
//        }
//    }
//}