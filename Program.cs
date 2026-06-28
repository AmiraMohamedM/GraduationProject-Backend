using grad.Data;
using grad.Helpers;
using grad.Hubs;
using grad.Interfaces;
using grad.Infrastructure.Interceptors;
using grad.Models;
using grad.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Security.Claims;


var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<ActivityInterceptor>();
builder.Services.AddScoped<ActivityLogger>();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddHostedService<NotificationBackgroundService>();

builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024;
});

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
// ─────────────────────────────────────────────────────────────────────────────

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // AllowCredentials() is required for SignalR; can't use AllowAnyOrigin with it
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// JWT
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSection["Secret"];
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var key = Encoding.UTF8.GetBytes(secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // ── SignalR: read JWT from query string for WebSocket connections ─────────
    // ── SignalR: read JWT from query string for WebSocket connections ─────────
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                context.Token = accessToken;
            return Task.CompletedTask;
        },

        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            if (principal is null || !principal.IsInRole("Student"))
                return; // غير الطالب مش متأثرين بالفحص ده

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var sid = principal.FindFirstValue("sid");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sid))
            {
                context.Fail("Invalid session.");
                return;
            }

            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByIdAsync(userId);

            if (user is null || user.CurrentSessionId is null ||
                user.CurrentSessionId.ToString() != sid)
            {
                context.Fail("session_revoked");
            }
        },

        OnChallenge = async context =>
        {
            if (context.AuthenticateFailure?.Message == "session_revoked")
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"error\":\"session_revoked\",\"message\":\"This account was used to log in on another device.\"}");
            }
        }
    };
    // ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,

        ValidateAudience = true,
        ValidAudience = audience,

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"];
    options.ClientSecret = builder.Configuration["Google:ClientSecret"];
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1",        new OpenApiInfo { Title = "Learning Platform API", Version = "v1" });
    c.SwaggerDoc("students",  new OpenApiInfo { Title = "Student API",           Version = "v1" });
    c.SwaggerDoc("teachers",  new OpenApiInfo { Title = "Teacher API",           Version = "v1" });
    c.SwaggerDoc("admin",     new OpenApiInfo { Title = "Admin API",             Version = "v1" });
    c.SwaggerDoc("moderator", new OpenApiInfo { Title = "Moderator API",         Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    c.AddSecurityDefinition(scheme.Reference.Id, scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { scheme, Array.Empty<string>() }
    });

    c.DocInclusionPredicate((doc, desc) =>
    {
        var path = desc.RelativePath?.ToLower() ?? "";
        if (doc == "students")  return path.Contains("student");
        if (doc == "teachers")  return path.Contains("teacher");
        if (doc == "admin")     return path.Contains("admin");
        if (doc == "moderator") return path.Contains("moderator");
        return doc == "v1";
    });
});

var app = builder.Build();

// Migration + Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();

        logger.LogInformation("Seeding roles and admin user...");
        await SeedData.SeedRolesAsync(services);
        await SeedData.SeedAdminAsync(services);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        string adminEmail = "m314227@gmail.com";
        string adminPassword = "Admin@123";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                firstname = "Graduation",
                lastname = "Project",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(adminUser, adminPassword);
            await userManager.AddToRoleAsync(adminUser, "Admin");
            logger.LogInformation("Admin user created");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startup error");
        throw;
    }
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json",        "Learning Platform API");
        c.SwaggerEndpoint("/swagger/students/swagger.json",  "Students");
        c.SwaggerEndpoint("/swagger/teachers/swagger.json",  "Teachers");
        c.SwaggerEndpoint("/swagger/admin/swagger.json",     "Admin");
        c.SwaggerEndpoint("/swagger/moderator/swagger.json", "Moderator");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── SignalR hub endpoint ──────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/hubs/notifications");
// ─────────────────────────────────────────────────────────────────────────────

app.Run();