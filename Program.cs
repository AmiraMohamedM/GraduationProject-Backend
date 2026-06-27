using grad.Data;
using grad.Helpers;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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
c.SwaggerDoc("v1", new OpenApiInfo { Title = "Learning Platform API", Version = "v1" });
c.SwaggerDoc("students", new OpenApiInfo { Title = "Student API", Version = "v1" });
c.SwaggerDoc("teachers", new OpenApiInfo { Title = "Teacher API", Version = "v1" });
c.SwaggerDoc("admin", new OpenApiInfo { Title = "Admin API", Version = "v1" });
c.SwaggerDoc("moderator", new OpenApiInfo { Title = "Moderator API", Version = "v1" });

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

        if (doc == "students") return path.Contains("student");
        if (doc == "teachers") return path.Contains("teacher");
        if (doc == "admin") return path.Contains("admin");
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Learning Platform API");
        c.SwaggerEndpoint("/swagger/students/swagger.json", "Students");
        c.SwaggerEndpoint("/swagger/teachers/swagger.json", "Teachers");
        c.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin");
        c.SwaggerEndpoint("/swagger/moderator/swagger.json", "Moderator");
    });
}

app.UseHttpsRedirection();

app.UseStaticFiles(); // wwwroot


app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();