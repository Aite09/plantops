using Microsoft.AspNetCore.Identity;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Infrastructure.Identity;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email, string DisplayName, string Role);

/// <summary>One account per role, with the same ids as the development demo users.</summary>
public static class TestUsers
{
    /// <summary>Test-only password; the API never sees it outside these tests.</summary>
    public const string Password = "Integration-Test-Pw1!";

    public static readonly TestUser Staff = new(SeedUsers.Staff, "staff@tests.plantops", "Sam Staff", Roles.Staff);
    public static readonly TestUser Supervisor = new(SeedUsers.Supervisor, "supervisor@tests.plantops", "Pat Supervisor", Roles.Supervisor);
    public static readonly TestUser TechnicianA = new(SeedUsers.TechnicianA, "tech.a@tests.plantops", "Alex Technician", Roles.Technician);
    public static readonly TestUser TechnicianB = new(SeedUsers.TechnicianB, "tech.b@tests.plantops", "Blair Technician", Roles.Technician);
    public static readonly TestUser Admin = new(SeedUsers.Admin, "admin@tests.plantops", "Ada Admin", Roles.Admin);

    public static IReadOnlyList<TestUser> All { get; } = [Staff, Supervisor, TechnicianA, TechnicianB, Admin];

    internal static async Task CreateAllAsync(UserManager<ApplicationUser> userManager)
    {
        foreach (var user in All)
        {
            var account = new ApplicationUser
            {
                Id = user.Id,
                UserName = user.Email,
                Email = user.Email,
                EmailConfirmed = true,
                DisplayName = user.DisplayName,
            };
            var created = await userManager.CreateAsync(account, Password);
            if (!created.Succeeded || !(await userManager.AddToRoleAsync(account, user.Role)).Succeeded)
            {
                throw new InvalidOperationException($"Could not create test user {user.Email}.");
            }
        }
    }
}
