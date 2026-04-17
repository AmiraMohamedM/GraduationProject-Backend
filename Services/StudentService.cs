using grad.Data;
using grad.DTOs;
using grad.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using grad.Services;
using Microsoft.AspNetCore.Http;

namespace grad.Services
{
    public class StudentService : IStudentService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StudentService> _logger;
        private readonly ActivityLogger _activityLogger;
        private readonly IHttpContextAccessor _httpContextAccessor; 

        private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string LowerChars = "abcdefghjkmnpqrstuvwxyz";
        private const string DigitChars = "23456789";
        private const string SymbolChars = "#@!$%";

        public StudentService(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentService> logger,
            ActivityLogger activityLogger,
            IHttpContextAccessor httpContextAccessor) 
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _activityLogger = activityLogger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<StudentCredentialsResponseDto> CreateStudentAsync(CreateStudentRequestDto dto)
        {
            var moderatorIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var moderatorId = !string.IsNullOrEmpty(moderatorIdStr) ? Guid.Parse(moderatorIdStr) : Guid.Empty;

            _logger.LogInformation("Creating student account for {First} {Last} <{Email}>",
                dto.FirstName, dto.LastName, dto.Email);

            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser is not null)
                throw new InvalidOperationException($"A user with email '{dto.Email}' already exists.");

            if (dto.TeacherIds.Any())
            {
                var foundCount = await _db.Teachers
                    .CountAsync(t => dto.TeacherIds.Contains(t.teacher_id));

                if (foundCount != dto.TeacherIds.Count)
                    throw new InvalidOperationException("One or more supplied teacher IDs were not found.");
            }

            var username = await GenerateUniqueUsernameAsync(dto.FirstName, dto.LastName);
            var password = GenerateSecurePassword();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = username,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                firstname = Capitalize(dto.FirstName),
                lastname = Capitalize(dto.LastName),
                language_pref = "en",
                device_id = string.Empty,
                is_approved = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create user account: {errors}");
            }

            await _userManager.AddToRoleAsync(user, "Student");

            var student = new Student
            {
                student_id = Guid.NewGuid(),
                user_id = user.Id,
                AcademicLevel = dto.AcademicLevel,
                AcademicYear = dto.AcademicYear,
                parent_email = dto.ParentPhoneNumber ?? string.Empty
            };

            _db.Students.Add(student);

            var assignedTeacherNames = new List<string>();
            if (dto.TeacherIds.Any())
            {
                var teachers = await _db.Teachers
                    .Include(t => t.User)
                    .Where(t => dto.TeacherIds.Contains(t.teacher_id))
                    .ToListAsync();

                foreach (var teacher in teachers)
                {
                    _db.StudentTeachers.Add(new StudentTeacher
                    {
                        StudentId = student.student_id,
                        TeacherId = teacher.teacher_id
                    });
                    assignedTeacherNames.Add($"{teacher.User.firstname} {teacher.User.lastname}");
                }
            }

            _db.UserStatistics.Add(new UserStatistics
            {
                StudentId = student.student_id,
                Absence = 0,
                Tasks = 0,
                Quiz = 0
            });

            await _db.SaveChangesAsync();

            try
            {
                string teachersText = assignedTeacherNames.Any() ? string.Join(", ", assignedTeacherNames) : "no teachers";
                string logMessage = $"Registered student {user.firstname} {user.lastname} and assigned to {teachersText}";

                await _activityLogger.Log(moderatorId, logMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Activity Log failed");
            }

            return new StudentCredentialsResponseDto
            {
                StudentId = student.student_id,
                Username = username,
                Password = password,
                FullName = $"{user.firstname} {user.lastname}",
                Email = user.Email!,
                AssignedTeacherNames = assignedTeacherNames
            };
        }

        public async Task AssignTeachersAsync(Guid studentId, List<Guid> teacherIds)
        {
            var student = await _db.Students.FindAsync(studentId)
                ?? throw new KeyNotFoundException($"Student {studentId} not found.");

            var existing = await _db.StudentTeachers.Where(st => st.StudentId == studentId).ToListAsync();
            _db.StudentTeachers.RemoveRange(existing);

            if (teacherIds.Any())
            {
                foreach (var teacherId in teacherIds.Distinct())
                {
                    _db.StudentTeachers.Add(new StudentTeacher { StudentId = studentId, TeacherId = teacherId });
                }
            }
            await _db.SaveChangesAsync();
        }

        public async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName)
        {
            var year = DateTime.UtcNow.Year;
            var baseSlug = $"{Slugify(firstName)}.{Slugify(lastName)}.{year}";
            var candidate = baseSlug;
            var counter = 1;
            while (await _userManager.FindByNameAsync(candidate) is not null)
            {
                candidate = $"{baseSlug}_{counter}";
                counter++;
            }
            return candidate;
        }

        public string GenerateSecurePassword()
        {
            var rng = new Random();
            var chars = new List<char>
            {
                UpperChars [rng.Next(UpperChars.Length)],
                LowerChars [rng.Next(LowerChars.Length)],
                DigitChars [rng.Next(DigitChars.Length)],
                SymbolChars[rng.Next(SymbolChars.Length)]
            };
            var allChars = UpperChars + LowerChars + DigitChars + SymbolChars;
            while (chars.Count < 8) chars.Add(allChars[rng.Next(allChars.Length)]);
            return new string(chars.OrderBy(_ => rng.Next()).ToArray());
        }

        private static string Slugify(string input) => new string(input.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());
        private static string Capitalize(string input) => string.IsNullOrWhiteSpace(input) ? input : char.ToUpper(input[0]) + input[1..].ToLower();
    }
}