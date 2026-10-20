using Microsoft.AspNetCore.Identity;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Infrastructure.Identity;

/// <summary>
/// Creates accounts from outside the API: the demo accounts (one per role, with the ids the sample work orders use) and
/// the first Admin. Passwords come from configuration at run time, never from source code. Every method is safe to run
/// again: it brings the accounts to the state described here (adding, fixing or removing what differs), so a run that
/// failed half-way is completed by the next one.
/// </summary>
internal static class IdentitySeeder
{
    private static readonly (Guid Id, string Email, string DisplayName, string Role)[] DemoUsers =
    [
        (SeedUsers.Staff, "staff@plantops.test", "Sam Staff", Roles.Staff),
        (SeedUsers.Supervisor, "supervisor@plantops.test", "Pat Supervisor", Roles.Supervisor),
        (SeedUsers.TechnicianA, "tech.a@plantops.test", "Alex Technician", Roles.Technician),
        (SeedUsers.TechnicianB, "tech.b@plantops.test", "Blair Technician", Roles.Technician),
        (SeedUsers.Admin, "admin@plantops.test", "Ada Admin", Roles.Admin),
    ];

    /// <param name="includeAdmin">Development only. The public demo has no Admin: an Admin can create accounts.</param>
    public static async Task SeedDemoUsersAsync(UserManager<ApplicationUser> userManager, string password, bool includeAdmin)
    {
        foreach (var demo in DemoUsers.Where(d => includeAdmin || d.Role != Roles.Admin))
        {
            var user = await userManager.FindByIdAsync(demo.Id.ToString());
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = demo.Id,
                    UserName = demo.Email,
                    Email = demo.Email,
                    EmailConfirmed = true,
                    DisplayName = demo.DisplayName,
                };
                Ensure(await userManager.CreateAsync(user, password), demo.Email);
            }
            else
            {
                // The shared password is configuration, so it may have changed since the account was created.
                await EnsurePasswordAsync(userManager, user, password);
            }

            await EnsureRoleAsync(userManager, user, demo.Role);

            // Exactly one role per demo account. Anyone can sign in to these, so an extra role given by mistake (Admin
            // above all) must not survive the next seed.
            var extraRoles = (await userManager.GetRolesAsync(user)).Where(role => role != demo.Role).ToList();
            if (extraRoles.Count > 0)
            {
                Ensure(await userManager.RemoveFromRolesAsync(user, extraRoles), demo.Email);
            }

            // The public demo prints these passwords on its login page, so lockout protects nothing there; it would only
            // let any visitor lock everyone out with five wrong guesses. The per-IP login rate limit still applies.
            if (demo.Role != Roles.Admin && await userManager.GetLockoutEnabledAsync(user))
            {
                Ensure(await userManager.SetLockoutEnabledAsync(user, false), demo.Email);
            }
        }
    }

    /// <summary>
    /// The first Admin, which the API can't create (only an Admin may create accounts). Running it again for an existing
    /// account sets the password given this time (so it also recovers a forgotten one) and adds the Admin role if it is
    /// missing. Lockout stays on: this password is private. A demo email is refused, because promoting a public-password
    /// account would give every visitor Admin.
    /// </summary>
    public static async Task EnsureAdminAsync(UserManager<ApplicationUser> userManager, string email, string password)
    {
        if (DemoUsers.Any(demo => string.Equals(demo.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{email} is a demo account, and its password is public. Use a dedicated address for the Admin.");
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Administrator",
            };
            Ensure(await userManager.CreateAsync(admin, password), email);
        }
        else
        {
            await EnsurePasswordAsync(userManager, admin, password);
        }

        await EnsureRoleAsync(userManager, admin, Roles.Admin);
    }

    /// <summary>
    /// Sets the password if it differs. Remove-then-add because no reset-token providers are registered. The new
    /// password is checked against the policy first, so a rejected one never leaves the account without a password; a
    /// run interrupted between the two calls is completed by the next one.
    /// </summary>
    private static async Task EnsurePasswordAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string password)
    {
        if (await userManager.CheckPasswordAsync(user, password))
        {
            return;
        }

        foreach (var validator in userManager.PasswordValidators)
        {
            Ensure(await validator.ValidateAsync(userManager, user, password), user.Email!);
        }

        Ensure(await userManager.RemovePasswordAsync(user), user.Email!);
        Ensure(await userManager.AddPasswordAsync(user, password), user.Email!);
    }

    private static async Task EnsureRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string role)
    {
        if (!await userManager.IsInRoleAsync(user, role))
        {
            Ensure(await userManager.AddToRoleAsync(user, role), user.Email!);
        }
    }

    private static void Ensure(IdentityResult result, string account)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not set up {account}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
