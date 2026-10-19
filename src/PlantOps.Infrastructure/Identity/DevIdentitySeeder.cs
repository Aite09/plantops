using Microsoft.AspNetCore.Identity;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Infrastructure.Identity;

/// <summary>
/// Demo accounts for local development, one per role, with the same ids the sample work orders use.
/// The shared password comes from configuration (user-secrets), never from source code.
/// </summary>
internal static class DevIdentitySeeder
{
    private static readonly (Guid Id, string Email, string DisplayName, string Role)[] DemoUsers =
    [
        (SeedUsers.Staff, "staff@plantops.test", "Sam Staff", Roles.Staff),
        (SeedUsers.Supervisor, "supervisor@plantops.test", "Pat Supervisor", Roles.Supervisor),
        (SeedUsers.TechnicianA, "tech.a@plantops.test", "Alex Technician", Roles.Technician),
        (SeedUsers.TechnicianB, "tech.b@plantops.test", "Blair Technician", Roles.Technician),
        (SeedUsers.Admin, "admin@plantops.test", "Ada Admin", Roles.Admin),
    ];

    public static async Task SeedAsync(UserManager<ApplicationUser> userManager, string password)
    {
        foreach (var demo in DemoUsers)
        {
            if (await userManager.FindByIdAsync(demo.Id.ToString()) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Id = demo.Id,
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                DisplayName = demo.DisplayName,
            };

            Ensure(await userManager.CreateAsync(user, password), demo.Email);
            Ensure(await userManager.AddToRoleAsync(user, demo.Role), demo.Email);
        }
    }

    private static void Ensure(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not seed demo user {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
