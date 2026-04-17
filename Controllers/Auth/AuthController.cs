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
[Route("api/auth")]
[ApiExplorerSettings(GroupName = "auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    // ------------------- GET PROFILE -------------------
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == guid);
        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.firstname,
            user.lastname,
            user.Email,
            user.language_pref,
            user.device_id,

            student = await _db.Students
           .Where(s => s.user_id == user.Id)
           .Select(s => new { s.parent_email, s.AcademicLevel, s.AcademicYear })
           .FirstOrDefaultAsync(),

            teacher = await _db.Teachers
                .Where(t => t.user_id == user.Id)
                .Select(t => new {  t.subject, t.is_approved })
                .FirstOrDefaultAsync()
        });
    }

    // ------------------- UPDATE PROFILE -------------------
    [HttpPut("update")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.FirstName)) user.firstname = req.FirstName;
        if (!string.IsNullOrWhiteSpace(req.LastName)) user.lastname = req.LastName;
        if (!string.IsNullOrWhiteSpace(req.LanguagePref)) user.language_pref = req.LanguagePref;
        if (!string.IsNullOrWhiteSpace(req.DeviceId)) user.device_id = req.DeviceId;

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, req.Password);
        }

        // Update Student/Teacher info
        var student = await _db.Students.FirstOrDefaultAsync(s => s.user_id == user.Id);
        if (student != null)
        {
          //  if (!string.IsNullOrWhiteSpace(req.ClassLevel))
        //    {
                // Try to find the ClassLevel entity by name
              //  var classLevelEntity = await _db.ClassLevels
                   // .FirstOrDefaultAsync(cl => cl.name == req.ClassLevel);

              //  if (classLevelEntity != null)
              //  {
                  //  student.ClassLevel = classLevelEntity;
                    //student.class_level_id = classLevelEntity.id;
                //}
            //}
            if (!string.IsNullOrWhiteSpace(req.AcademicLevel)) student.AcademicLevel = req.AcademicLevel;
            if (req.AcademicYear.HasValue) student.AcademicYear = req.AcademicYear.Value;
            if (!string.IsNullOrWhiteSpace(req.ParentEmail)) student.parent_email = req.ParentEmail;
        }

        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.user_id == user.Id);
        if (teacher != null)
        {
          
            if (!string.IsNullOrWhiteSpace(req.Subject)) teacher.subject = req.Subject;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Profile updated successfully" });
    }

    // ------------------- FORGOT PASSWORD -------------------
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("Email is required.");

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
            return Ok(new { message = "If that email exists, a reset link has been sent." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // TODO: Send token via email
        Console.WriteLine($"Reset password token for {user.Email}: {token}");

        return Ok(new { message = "Password reset link sent if email exists." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Token and new password are required.");

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
            return BadRequest("Invalid token.");

        var result = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        return Ok(new { message = "Password has been reset successfully." });
    }


}
