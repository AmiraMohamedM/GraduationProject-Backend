using grad.Data;
using grad.DTOs;
using grad.Models;
using grad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ActivityLogger _logger;

    public AdminController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ActivityLogger logger,
        ITokenService tokenService)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    private Guid GetAdminId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

    // ================================================================
    // LOGIN
    // ================================================================

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin([FromBody] LoginRequest req)
    {
        if (string.IsNullOrEmpty(req.Email) || string.IsNullOrEmpty(req.Password))
            return BadRequest("Email and password are required.");

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized("Invalid credentials.");

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
            return Unauthorized("You are not an admin.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
        if (!result.Succeeded) return Unauthorized("Invalid credentials.");

        var token = await _tokenService.CreateToken(user);

        return Ok(new
        {
            Token = token,
            Id = user.Id,
            user.firstname,
            user.lastname,
            Email = user.Email
        });
    }

    // ================================================================
    // OVERVIEW
    // ================================================================

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var adminId = GetAdminId();

        var totalTeachers = await _db.Teachers.CountAsync(t => t.admin_id == adminId);


        var totalModerators = await _db.Moderators.CountAsync(m => m.admin_id == adminId);

        var totalCourses = await _db.Courses
            .CountAsync(c => c.Teacher.admin_id == adminId);

        var totalStudents = await _db.StudentTeachers
            .Where(st => st.Teacher.admin_id == adminId)
            .Select(st => st.StudentId)
            .Distinct()
            .CountAsync();

        var enrollmentByCourse = await _db.Enrollments
            .Where(e => e.Course.Teacher.admin_id == adminId)
            .GroupBy(e => new
            {
                e.Course.Id,
                e.Course.Title
            })
            .Select(g => new
            {
                CourseId = g.Key.Id,
                CourseTitle = g.Key.Title,
                Students = g.Count()
            })
            .ToListAsync();

        var recentActivities = await _db.ActivityLogs
     .OrderByDescending(a => a.CreatedAt)
     .Take(10)
     .Select(a => new
     {
         id = a.Id,
         text = a.Text,
         time = a.CreatedAt
     })
     .ToListAsync();

        return Ok(new
        {
            totalTeachers,
            totalModerators,
            totalStudents,
            totalCourses,

            enrollmentByCourse,
            recentActivities
        });
    }

    // ================================================================
    // TEACHERS
    // ================================================================

    [HttpGet("AllTeachers")]
    public async Task<IActionResult> GetTeachers()
    {
        var adminId = GetAdminId();

        var teachers = await _db.Teachers
            .Include(t => t.User)
            .Include(t => t.Admin)
            .Where(t => t.admin_id == adminId)
            .Select(t => new
            {
                id = t.User.Id,
                teacherId = t.teacher_id,
                firstname = t.User.firstname,
                lastname = t.User.lastname,
                email = t.User.Email,
                subject = t.subject,
                courseCount = _db.Courses.Count(c => c.TeacherId == t.teacher_id),
                studentsCount = _db.StudentTeachers
                    .Where(st => st.TeacherId == t.teacher_id)
                    .Select(st => st.StudentId)
                    .Distinct()
                    .Count(),

                moderatorId = t.ModeratorId,
                moderatorName = t.ModeratorId.HasValue
                    ? _db.Users
                        .Where(u => u.Id == t.ModeratorId)
                        .Select(u => u.firstname + " " + u.lastname)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync();

        return Ok(teachers);
    }

    [HttpPost("AddTeachers")]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherRequest req)
    {
        var adminId = GetAdminId();

        if (await _userManager.FindByEmailAsync(req.email) != null)
            return BadRequest("Email already exists.");


        if (req.password != req.passwordConfirm)
            return BadRequest("Password and confirmation do not match.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.email,
            Email = req.email,
            firstname = req.firstname,
            lastname = req.lastname,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(user, req.password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "Teacher");

        _db.Teachers.Add(new Teacher
        {
            teacher_id = Guid.NewGuid(),
            user_id = user.Id,
            admin_id = adminId,
            subject = req.subject ?? "",
        });

        await _db.SaveChangesAsync();

        await _logger.Log(adminId,
            $"Teacher {req.firstname} {req.lastname} created"
        );

        return Ok(new { message = "Teacher created successfully.", id = user.Id });
    }

    [HttpPut("UpdateTeachers/{userId}")]
    public async Task<IActionResult> UpdateTeacher(Guid userId, [FromBody] UpdateTeacherRequest req)
    {
        var adminId = GetAdminId();

        var teacher = await _db.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.user_id == userId && t.admin_id == adminId);

        if (teacher == null)
            return NotFound("Teacher not found.");

        if (teacher.User == null)
            return BadRequest("Teacher has no user account.");

        if (!string.IsNullOrEmpty(req.firstname))
            teacher.User.firstname = req.firstname;

        if (!string.IsNullOrEmpty(req.lastname))
            teacher.User.lastname = req.lastname;

        if (!string.IsNullOrEmpty(req.subject))
            teacher.subject = req.subject;

        if (!string.IsNullOrEmpty(req.email))
            teacher.User.Email = req.email;

        var result = await _userManager.UpdateAsync(teacher.User);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _db.SaveChangesAsync();

        await _logger.Log(adminId,
            $"Teacher {teacher.User.firstname} {teacher.User.lastname} updated");

        return Ok(new { message = "Teacher updated successfully." });
    }

    [HttpDelete("DeleteTeachers/{userId}")]
    public async Task<IActionResult> DeleteTeacher(Guid userId)
    {
        var adminId = GetAdminId();

        var teacher = await _db.Teachers
            .FirstOrDefaultAsync(t => t.user_id == userId && t.admin_id == adminId);

        if (teacher == null)
            return NotFound("Teacher not found.");

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user != null)
            await _userManager.DeleteAsync(user);

        return Ok(new { message = "Teacher deleted successfully." });
    }

    // ================================================================
    // MODERATORS
    // ================================================================

    [HttpGet("AllModerators")]
    public async Task<IActionResult> GetModerators()
    {
        var adminId = GetAdminId();

        var moderators = await _db.Moderators
            .Include(m => m.User)
            .Include(m => m.Admin)
            .Include(m => m.AssignedTeachers)
            .Where(m => m.admin_id == adminId)
            .Select(m => new
            {
                id = m.User.Id,
                moderator_id = m.moderator_id,
                firstname = m.User.firstname,
                lastname = m.User.lastname,
                email = m.User.Email,
                students_managed = _db.Enrollments
                 .Where(e =>
                             m.AssignedTeachers
                                .Select(at => at.teacher_user_id)
                                .Contains(e.Course.Teacher.user_id))
                 .Select(e => e.StudentId)
                 .Distinct()
                 .Count(),
                last_active = m.last_active,
                assigned_teacher_ids = m.AssignedTeachers.Select(at => at.teacher_user_id).ToList(),
                admin_name = m.Admin.firstname + " " + m.Admin.lastname
            })
            .ToListAsync();

        return Ok(moderators);
    }

    [HttpPost("AddModerators")]
    public async Task<IActionResult> CreateModerator([FromBody] CreateModeratorRequest req)
    {
        var adminId = GetAdminId();

        if (await _userManager.FindByEmailAsync(req.email) != null)
            return BadRequest("Email already exists.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.email,
            Email = req.email,
            firstname = req.firstname,
            lastname = req.lastname,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, req.password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "Moderator");

        var moderator = new Moderator
        {
            moderator_id = Guid.NewGuid(),
            user_id = user.Id,
            admin_id = adminId,
            last_active = DateTime.UtcNow
        };

        _db.Moderators.Add(moderator);

        
        if (req.assigned_teacher_ids != null)
        {
            foreach (var tid in req.assigned_teacher_ids)
            {
                _db.ModeratorTeachers.Add(new ModeratorTeacher
                {
                    moderator_id = moderator.moderator_id,
                    teacher_user_id = tid
                });

                var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.user_id == tid);
                if (teacher != null)
                {
                    teacher.ModeratorId = user.Id;
                }
            }
        }

        await _db.SaveChangesAsync();

        await _logger.Log(adminId, $"Moderator {req.firstname} {req.lastname} created");

        return Ok(new { message = "Moderator created successfully.", id = user.Id });
    }

    [HttpPut("UpdateModerators/{id}")]
    public async Task<IActionResult> UpdateModerator(Guid id, [FromBody] UpdateModeratorRequest req)
    {
        var adminId = GetAdminId();

        var moderator = await _db.Moderators
            .Include(m => m.User)
            .Include(m => m.AssignedTeachers)
            .FirstOrDefaultAsync(m => m.User.Id == id && m.admin_id == adminId);

        if (moderator == null) return NotFound("Moderator not found.");

        if (!string.IsNullOrEmpty(req.firstname)) moderator.User.firstname = req.firstname;
        if (!string.IsNullOrEmpty(req.lastname)) moderator.User.lastname = req.lastname;

        await _userManager.UpdateAsync(moderator.User);
        if (req.assigned_teacher_ids != null)
        {
            var currentTeacherIds = moderator.AssignedTeachers
                .Select(x => x.teacher_user_id)
                .OrderBy(x => x)
                .ToList();

            var newTeacherIds = req.assigned_teacher_ids
                .OrderBy(x => x)
                .ToList();

            bool changed = !currentTeacherIds.SequenceEqual(newTeacherIds);

            if (changed)
            {
                _db.ModeratorTeachers.RemoveRange(moderator.AssignedTeachers);

                var oldTeachers = await _db.Teachers
                    .Where(t => t.ModeratorId == moderator.User.Id)
                    .ToListAsync();

                foreach (var ot in oldTeachers)
                    ot.ModeratorId = null;

                foreach (var tid in req.assigned_teacher_ids)
                {
                    _db.ModeratorTeachers.Add(new ModeratorTeacher
                    {
                        moderator_id = moderator.moderator_id,
                        teacher_user_id = tid
                    });

                    var teacher = await _db.Teachers
                        .FirstOrDefaultAsync(t => t.user_id == tid);

                    if (teacher != null)
                        teacher.ModeratorId = moderator.User.Id;
                }
            }
        }

        await _db.SaveChangesAsync();

        await _logger.Log(adminId,
            $"Moderator {moderator.User.firstname} {moderator.User.lastname} updated"
        );

        return Ok(new { message = "Moderator updated successfully." });
    }

    [HttpDelete("DeleteModerators/{id}")]
    public async Task<IActionResult> DeleteModerator(Guid id)
    {
        var adminId = GetAdminId();
        var moderator = await _db.Moderators
            .Include(m => m.User)
            .Include(m => m.AssignedTeachers)
            .FirstOrDefaultAsync(m => m.user_id == id && m.admin_id == adminId);
        if (moderator == null) return NotFound("Moderator not found.");

        var fullName = $"{moderator.User?.firstname} {moderator.User?.lastname}";

        // Clear the direct FK on teachers.ModeratorId -> Users.Id
        var teachersToUnassign = await _db.Teachers
            .Where(t => t.ModeratorId == id)
            .ToListAsync();
        foreach (var t in teachersToUnassign)
            t.ModeratorId = null;

        _db.ModeratorTeachers.RemoveRange(moderator.AssignedTeachers);
        _db.Moderators.Remove(moderator);
        await _db.SaveChangesAsync();

        if (moderator.User != null)
            await _userManager.DeleteAsync(moderator.User);

        await _logger.Log(adminId, $"Moderator {fullName} deleted");
        return Ok(new { message = "Moderator deleted successfully." });
    }

    // ================================================================
    // REPORTS
    // ================================================================

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports()
    {
        var adminId = GetAdminId();

        // Monthly new enrollments for the last 12 months
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);

        var monthlyEnrollments = await _db.Enrollments
            .Where(e =>
                e.Course.Teacher.admin_id == adminId &&
                e.EnrolledAt >= twelveMonthsAgo)
            .GroupBy(e => new { e.EnrolledAt.Year, e.EnrolledAt.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                count = g.Count()
            })
            .OrderBy(x => x.year)
            .ThenBy(x => x.month)
            .ToListAsync();

        // Format month names for Flutter
        var monthlyFormatted = monthlyEnrollments.Select(x => new
        {
            label = new DateTime(x.year, x.month, 1).ToString("MMM"),
            count = x.count
        }).ToList();

        // Students per teacher (actual distinct students enrolled)
        var studentsPerTeacher = await _db.Teachers
            .Include(t => t.User)
            .Where(t => t.admin_id == adminId)
            .Select(t => new
            {
                name = t.User.firstname + " " + t.User.lastname,
                value = _db.Enrollments
                    .Where(e => e.Course.TeacherId == t.teacher_id)
                    .Select(e => e.StudentId)
                    .Distinct()
                    .Count()
            })
            .Where(x => x.value > 0)
            .ToListAsync();

        return Ok(new { monthlyEnrollments = monthlyFormatted, studentsPerTeacher });
    }
    // ================================================================
    // SETTINGS
    // ================================================================

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var user = await _userManager.FindByIdAsync(GetAdminId().ToString());
        if (user == null) return NotFound();

        return Ok(new
        {
            institutionName = user.FullName,

            contactEmail = user.Email,
            address = user.Address,
            phone = user.Phone,
            subscription = new
            {
                plan = user.Plan,
                expires = user.PlanExpiresAt
            }

        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateAdminSettingsRequest req)
    {
        var user = await _userManager.FindByIdAsync(GetAdminId().ToString());
        if (user == null) return NotFound();

        if (!string.IsNullOrEmpty(req.firstname)) user.firstname = req.firstname;
        if (!string.IsNullOrEmpty(req.lastname)) user.lastname = req.lastname;


        if (!string.IsNullOrEmpty(req.contactEmail))
            user.Email = req.contactEmail;

        if (!string.IsNullOrEmpty(req.address))
            user.Address = req.address;

        if (!string.IsNullOrEmpty(req.phone))
            user.Phone = req.phone;





        await _userManager.UpdateAsync(user);

        return Ok(new { message = "Settings updated." });
    }
}

// ================================================================
// REQUEST DTOs
// ================================================================

public class CreateTeacherRequest
{
    public string firstname { get; set; }
    public string lastname { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    public string passwordConfirm { get; set; }
    public string? subject { get; set; }

}

public class UpdateTeacherRequest
{
    public string? firstname { get; set; }
    public string? lastname { get; set; }
    public string? subject { get; set; }
    public string? email { get; set; }
}

public class CreateModeratorRequest
{
    public string firstname { get; set; }
    public string lastname { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    public List<Guid>? assigned_teacher_ids { get; set; }
}

public class UpdateModeratorRequest
{
    public string? firstname { get; set; }
    public string? lastname { get; set; }
    public List<Guid>? assigned_teacher_ids { get; set; }
}

public class UpdateAdminSettingsRequest
{
    public string? firstname { get; set; }
    public string? lastname { get; set; }
    public string? contactEmail { get; set; }
    public string? address { get; set; }
    public string? phone { get; set; }

}