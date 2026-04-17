//using grad.Data;
//using grad.DTOs;
//using grad.Models;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Authorization;

//namespace grad.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class CoursesController : ControllerBase
//    {
//        private readonly AppDbContext _context;

//        public CoursesController(AppDbContext context)
//        {
//            _context = context;
//        }

//        // ==========================================
//        // 1. API: Get Courses for Subject Page (GET)
//        // ==========================================
//        [HttpGet("subject-courses")]
//        public async Task<IActionResult> GetCoursesByCategory([FromQuery] string category)
//        {
//            var query = _context.Courses
//                .Include(c => c.Teacher)
//                .ThenInclude(t => t.User)
//                .Where(c => c.Teacher.is_approved == true)
//                .AsQueryable();

//            if (!string.IsNullOrEmpty(category))
//            {
//                query = query.Where(c => c.Category.ToLower() == category.ToLower());
//            }

//            var coursesList = await query.Select(c => new
//            {
//                CourseId = c.Id,
//                TeacherId = c.TeacherId,
//                TeacherName = c.Teacher.User.firstname + " " + c.Teacher.User.lastname,
//                Category = c.Category,
//                Schedule = c.Schedule,
//                ClassType = c.ClassType,
//                MonthlyPrice = c.MonthlyPrice,
//                IsFavorite = false
//            }).ToListAsync();

//            return Ok(coursesList);
//        }

//        // ==========================================
//        // 2. API: Teacher adds a new Course (POST)
//        // ==========================================
//        [HttpPost("create-course")]
//        [Authorize]
//        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
//        {
//            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

//            if (string.IsNullOrEmpty(userIdString))
//            {
//                return Unauthorized("Unauthorized. Please log in first.");
//            }

//            var currentTeacher = await _context.Teachers
//                .FirstOrDefaultAsync(t => t.user_id.ToString() == userIdString);

//            if (currentTeacher == null)
//            {
//                return BadRequest("No teacher profile found for this user.");
//            }

//            var newCourse = new Course
//            {
//                TeacherId = currentTeacher.teacher_id,
//                Title = dto.Title,
//                Category = dto.Category,
//                Introduction = dto.Introduction,
//                VideoUrl = dto.VideoUrl,
//                Schedule = dto.Schedule,
//                ClassType = dto.ClassType,
//                MonthlyPrice = dto.MonthlyPrice
//            };

//            _context.Courses.Add(newCourse);
//            await _context.SaveChangesAsync();

//            return Ok("Course created successfully!");
//        }

//        // ==========================================
//        // 3. API: Get Course Details (GET)
//        // Includes LESSONS and TESTS (With Questions & Options)
//        // ==========================================
//        [HttpGet("course-details/{courseId}")]
//        public async Task<IActionResult> GetCourseDetails(int courseId)
//        {
//            var course = await _context.Courses
//                .Include(c => c.Teacher).ThenInclude(t => t.User)
//                .Include(c => c.Sessions)
//                .Include(c => c.Quizzes).ThenInclude(q => q.Questions).ThenInclude(ques => ques.Options)
//                .FirstOrDefaultAsync(c => c.Id == courseId);

//            if (course == null)
//            {
//                return NotFound("Course not found.");
//            }

//            var courseDetails = new
//            {
//                CourseId = course.Id,
//                Title = course.Title,
//                TeacherName = course.Teacher.User.firstname + " " + course.Teacher.User.lastname,
//                Category = course.Category,
//                Introduction = course.Introduction,
//                VideoUrl = course.VideoUrl,
//                Schedule = course.Schedule,
//                ClassType = course.ClassType,
//                MonthlyPrice = course.MonthlyPrice,

//                // Tab: LESSONS
//                Sessions = course.Sessions.OrderBy(s => s.Id).Select(s => new
//                {
//                    SessionId = s.Id,
//                    Title = s.Title,
//                    Duration = s.Duration,
//                    Description = s.Description,
//                    VideoUrl = s.VideoUrl,
//                    HomeworkUrl = s.HomeworkUrl,
//                    IsLocked = s.IsLocked
//                }).ToList(),

//                // Tab: TESTS (Includes full quiz structure for students)
//                Quizzes = course.Quizzes.OrderBy(q => q.Id).Select(q => new
//                {
//                    QuizId = q.Id,
//                    Title = q.Title,
//                    Description = q.Description,
//                    DurationInMinutes = q.DurationInMinutes,
//                    IsLocked = q.IsLocked,
//                    Questions = q.Questions.Select(ques => new {
//                        ques.Id,
//                        ques.Text,
//                        Options = ques.Options.Select(opt => new {
//                            opt.Id,
//                            opt.Text,
//                            // Note: We don't usually send 'IsCorrect' to the student's browser 
//                            // until they submit to prevent cheating!
//                        })
//                    })
//                }).ToList()
//            };

//            return Ok(courseDetails);
//        }

//        // ==========================================
//        // 4. API: Teacher adds a Session (POST)
//        // ==========================================
//        [HttpPost("add-session")]
//        [Authorize]
//        public async Task<IActionResult> AddSession([FromBody] CreateSessionDto dto)
//        {
//            var course = await _context.Courses.FindAsync(dto.CourseId);
//            if (course == null) return NotFound("Course not found.");

//            var newSession = new CourseSession
//            {
//                CourseId = dto.CourseId,
//                Title = dto.Title,
//                Duration = dto.Duration,
//                Description = dto.Description,
//                VideoUrl = dto.VideoUrl,
//                HomeworkUrl = dto.HomeworkUrl,
//                IsLocked = true
//            };

//            _context.CourseSessions.Add(newSession);
//            await _context.SaveChangesAsync();

//            return Ok("Session added successfully!");
//        }

//        // ==========================================
//        // 5. API: Teacher creates a Full Quiz with Questions (POST)
//        // ==========================================
//        [HttpPost("create-full-quiz")]
//        [Authorize]
//        public async Task<IActionResult> CreateFullQuiz([FromBody] CreateQuizDto dto)
//        {
//            var course = await _context.Courses.FindAsync(dto.CourseId);
//            if (course == null) return NotFound("Course not found.");

//            var newQuiz = new Quiz
//            {
//                CourseId = dto.CourseId,
//                Title = dto.Title,
//                Description = dto.Description,
//                DurationInMinutes = dto.DurationInMinutes,
//                IsLocked = true,
//                // Adding questions and their options in one go
//                Questions = dto.Questions.Select(q => new Question
//                {
//                    Text = q.Text,
//                    Options = q.Options.Select(o => new QuestionOption
//                    {
//                        Text = o.Text,
//                        IsCorrect = o.IsCorrect
//                    }).ToList()
//                }).ToList()
//            };

//            _context.Quizzes.Add(newQuiz);
//            await _context.SaveChangesAsync();

//            return Ok("Quiz with questions and timer created successfully!");
//        }

//        // ==========================================
//        // 6. API: Submit Quiz and Unlock Next (POST)
//        // ==========================================
//        [HttpPost("submit-quiz/{quizId}")]
//        [Authorize]
//        public async Task<IActionResult> SubmitQuiz(int quizId)
//        {
//            var currentQuiz = await _context.Quizzes.FindAsync(quizId);
//            if (currentQuiz == null) return NotFound("Quiz not found.");

//            var nextQuiz = await _context.Quizzes
//                .Where(q => q.CourseId == currentQuiz.CourseId && q.Id > quizId)
//                .OrderBy(q => q.Id)
//                .FirstOrDefaultAsync();

//            if (nextQuiz != null)
//            {
//                nextQuiz.IsLocked = false;
//                await _context.SaveChangesAsync();
//            }

//            return Ok("Quiz submitted. Next content unlocked!");
//        }

//        // ==========================================
//        // 7. API: Get Teachers' IDs (Utility)
//        // ==========================================
//        [HttpGet("get-teachers-ids")]
//        public IActionResult GetTeachersIds()
//        {
//            var teachers = _context.Teachers
//                .Select(t => new
//                {
//                    TeacherName = t.User.firstname + " " + t.User.lastname,
//                    CorrectTeacherId = t.teacher_id
//                })
//                .ToList();

//            return Ok(teachers);
//        }
//    }
//}