using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Auth;
using PlantOps.Application.Users;
using PlantOps.Domain.Common;

namespace PlantOps.Infrastructure.Identity;

internal sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenIssuer tokenIssuer,
    ICurrentUser currentUser,
    ILogger<AccountService> logger) : IAccountService
{
    // A real hash to check passwords against when the email is unknown. The hash records its own iteration count,
    // so verifying against it costs the same as checking a real account's password.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString());

    public async Task<Result<AccessTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Without this, an unknown email would answer in ~2 ms while a real one spends tens of ms hashing,
            // and an attacker could time the identical 401s to find out which emails have accounts.
            userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash, password);
            return AuthErrors.InvalidCredentials;
        }

        // lockoutOnFailure: after 5 wrong passwords the account is locked for 15 minutes (brute-force protection).
        var signIn = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!signIn.Succeeded)
        {
            // The caller always gets the same generic error; the real reason only goes to the log.
            logger.LogWarning(
                "Login failed for user {UserId}: locked out {IsLockedOut}, not allowed {IsNotAllowed}",
                user.Id, signIn.IsLockedOut, signIn.IsNotAllowed);
            return AuthErrors.InvalidCredentials;
        }

        var roles = (await userManager.GetRolesAsync(user)).Order().ToArray();
        var (token, expiresAt) = tokenIssuer.Issue(user, roles);

        return new AccessTokenDto(token, expiresAt, new UserDto(user.Id, user.Email!, user.DisplayName, roles));
    }

    public async Task<Result<Guid>> CreateUserAsync(string email, string displayName, string password, string role, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return UserErrors.EmailInUse;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return ToError(created.Errors);
        }

        var addedToRole = await userManager.AddToRoleAsync(user, role);
        if (!addedToRole.Succeeded)
        {
            throw new InvalidOperationException($"Could not add user {user.Id} to role {role}.");
        }

        // Account changes live in the Identity store, outside the work-order audit table, so this structured
        // security event is their audit record: who created which account with which role. No email or password.
        logger.LogInformation(
            "User {CreatedUserId} created with role {Role} by {ActorUserId}", user.Id, role, currentUser.UserId);

        return user.Id;
    }

    // Identity reports password-policy failures with codes starting "Password"; everything else here is about the
    // email (which doubles as the user name). Each lands on the field the client should highlight.
    private static Error ToError(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();
        if (list.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return UserErrors.EmailInUse;
        }

        return new ValidationError(list
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    }
}
