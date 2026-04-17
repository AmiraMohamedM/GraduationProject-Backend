using grad.Data;
using grad.DTOs;
using grad.Models;
using grad.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/auth/teacher")]
public class TeacherAuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public TeacherAuthController(
        AppDbContext db,
        ITokenService tokenService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _db = db;
        _tokenService = tokenService;
        _userManager = userManager;
        _signInManager = signInManager;
    }


    [HttpPost("login")]
    public async Task<IActionResult> LoginTeacher(LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized("Invalid credentials.");

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Teacher")) return Unauthorized("Invalid credentials.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
        if (!result.Succeeded) return Unauthorized("Invalid credentials.");

        var teacher = await _db.Teachers
            .Include(t => t.Admin)
            .FirstOrDefaultAsync(t => t.user_id == user.Id);

        if (teacher == null)
            return Unauthorized("Teacher profile not found.");

        if (!teacher.is_approved)
            return Unauthorized("Your account is pending admin approval.");

        var token = await _tokenService.CreateToken(user);

        return Ok(new
        {
            Token = token,
            UserId = user.Id,
            Role = "Teacher",

            Firstname = user.firstname,
            Lastname = user.lastname,
            Email = user.Email,

         
            Center = new
            {
                AdminId = teacher.admin_id,
                Name = teacher.Admin.firstname + " " + teacher.Admin.lastname
            }
        });
    }





}
