using grad.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace grad.Data
{
    public static class SeedData
    {
        public static async Task SeedRolesAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            string[] roles = new[] { "Admin", "Student", "Teacher", "Moderator" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>
                    {
                        Name = role,
                        NormalizedName = role.ToUpper()
                    });
                }
            }

            Console.WriteLine("Roles seeded successfully!");
        }

        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var admins = new[]
            {
        new {
            Email = "m314227@gmail.com",
            FirstName = "Graduation",
            LastName = "Project",
            Password = "Admin@123",
            Address = "Cairo",
            Phone = "+20 1000000000"
        },
        new {
            Email = "admin2@gmail.com",
            FirstName = "AlNour",
            LastName = "Education",
            Password = "Admin@456",
            Address = "15 Tahrir Street, Cairo",
            Phone = "+20 2 1234 5678"
        }
    };

            foreach (var a in admins)
            {
                var user = await userManager.FindByEmailAsync(a.Email);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = a.Email,
                        Email = a.Email,
                        firstname = a.FirstName,
                        lastname = a.LastName,
                        EmailConfirmed = true,

                        
                        Address = a.Address,
                        Phone = a.Phone,
                      

                        Plan = "Professional",
                        PlanExpiresAt = DateTime.UtcNow.AddYears(1)
                    };

                    var result = await userManager.CreateAsync(user, a.Password);
                    if (!result.Succeeded)
                    {
                        Console.WriteLine($"Failed to create admin {a.Email}:");
                        foreach (var error in result.Errors)
                            Console.WriteLine($"  - {error.Description}");
                        continue;
                    }
                }
                else
                {

                    user.firstname = a.FirstName;
                    user.lastname = a.LastName;
                    user.Address = a.Address;
                    user.Phone = a.Phone;
                    user.Plan = "Professional";
                    user.PlanExpiresAt = DateTime.UtcNow.AddYears(1);

                    await userManager.UpdateAsync(user);
                }

                if (!await userManager.IsInRoleAsync(user, "Admin"))
                    await userManager.AddToRoleAsync(user, "Admin");
            }

            Console.WriteLine("Admin users seeded successfully!");
        }
    }
}