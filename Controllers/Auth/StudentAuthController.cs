using grad.Data;
using grad.DTOs;
using grad.Models;
using grad.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/auth/student")]
public class StudentAuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStudentService _studentService; 

    public StudentAuthController(
        AppDbContext db,
        ITokenService tokenService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStudentService studentService) 
    {
        _db = db;
        _tokenService = tokenService;
        _userManager = userManager;
        _signInManager = signInManager;
        _studentService = studentService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterStudent(RegisterStudentRequest req)
    {
        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing != null) return BadRequest("Email already exists.");

        var generatedUsername = await _studentService.GenerateUniqueUsernameAsync(req.FirstName, req.LastName);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = generatedUsername,
            Email = req.Email,
            firstname = req.FirstName,
            lastname = req.LastName,
            language_pref = req.LanguagePref,
            device_id = req.DeviceId,
            is_approved = true
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "Student");

        var student = new Student
        {
            student_id = Guid.NewGuid(),
            user_id = user.Id,
            parent_email = req.ParentEmail
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync();

        var token = await _tokenService.CreateToken(user);

        return Ok(new
        {
            message = "Student registered successfully",
            username = user.UserName, 
            userId = user.Id,
            Token = token
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> StudentLogin([FromBody] StudentLoginRequest req)
    {
        var user = await _userManager.FindByNameAsync(req.Username);

        if (user == null || !await _userManager.IsInRoleAsync(user, "Student"))
        {
            return Unauthorized("Invalid Username or Password.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);

        if (!result.Succeeded) return Unauthorized("Invalid Username or Password.");

        var token = await _tokenService.CreateToken(user);

        return Ok(new
        {
            Token = token,
            Username = user.UserName,
            FullName = $"{user.firstname} {user.lastname}"
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("Email is required.");

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null) return Ok(new { message = "If that email exists, a reset link has been sent." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // TODO: Send email with token
        Console.WriteLine($"Student reset token for {user.Email}: {token}");

        return Ok(new { message = "Password reset link sent if email exists." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Token and new password are required.");

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null) return BadRequest("Invalid token.");

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Student")) return BadRequest("Invalid token.");

        var result = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "Password has been reset successfully." });
    }

    [HttpGet("google-login")]
    public IActionResult GoogleLogin()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleResponse)) // callback endpoint
        };

        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-response")]
    public async Task<IActionResult> GoogleResponse()
    {
        // get external login info from Google
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
            return BadRequest("Error loading external login info.");

        // try to sign in
        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);

        ApplicationUser user;

        if (!signInResult.Succeeded)
        {
            // create a new user
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name);

            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                firstname = fullName.Split(' ')[0],
                lastname = fullName.Split(' ').Length > 1 ? string.Join(" ", fullName.Split(' ').Skip(1)) : "",
                language_pref = "en",
                device_id = Guid.NewGuid().ToString(),
                is_approved = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return BadRequest(createResult.Errors);

            // add Google login info
            await _userManager.AddLoginAsync(user, info);

            // assign default role: Student
            await _userManager.AddToRoleAsync(user, "Student");

            // create Student record
            var student = new Student
            {
                student_id = Guid.NewGuid(),
                user_id = user.Id,
                parent_email = email
            };
            _db.Students.Add(student);
            await _db.SaveChangesAsync();
        }
        else
        {
            // existing user
            user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        }

        // create JWT
        var token = await _tokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Role = "Student",
            Firstname = user.firstname,
            Lastname = user.lastname,
            Email = user.Email
        });
    }



}
