using CampusConnect.Models;
using Microsoft.AspNetCore.Identity;

namespace CampusConnect.Data;

// Runs once every time the application starts (called from Program.cs).
// It only ADDS what is missing, so running it again changes nothing.
public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // ----- 1. Create the three roles if they don't exist yet -----
        foreach (string roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                logger.LogInformation("Created role {Role}", roleName);
            }
        }

        // ----- 2. Create the first Admin account -----
        // The email and password come from User Secrets (development) or
        // environment variables (production). They are NEVER written in code.
        string? adminEmail = configuration["SeedAdmin:Email"];
        string? adminPassword = configuration["SeedAdmin:Password"];
        string adminFullName = configuration["SeedAdmin:FullName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin:Email / SeedAdmin:Password not configured - no admin account was created.");
            return;
        }

        // Already there? Then there is nothing to do.
        if (await userManager.FindByEmailAsync(adminEmail) != null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = adminFullName,
            EmailConfirmed = true
        };

        // CreateAsync hashes the password before saving it.
        IdentityResult result = await userManager.CreateAsync(admin, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
            logger.LogInformation("Created admin account {Email}", adminEmail);
        }
        else
        {
            // Usually means the configured password breaks the password rules.
            string errors = string.Join(" ", result.Errors.Select(error => error.Description));
            logger.LogError("Could not create admin account: {Errors}", errors);
        }
    }
}
