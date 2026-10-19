using System.Security.Claims;
using PlantOps.Application.Abstractions.Authentication;

namespace PlantOps.Api.Authentication;

/// <summary>
/// Reads the caller from the validated token's claims. Any scheme that produces a user-id claim and role claims
/// works unchanged: today our own JWTs; with Microsoft Entra ID, its tokens (read the "oid" claim there instead).
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    // "sub" (subject) is the standard JWT claim for the user's id.
    private const string UserIdClaimType = "sub";

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue(UserIdClaimType), out var id)
            ? id
            : throw new InvalidOperationException("The current request has no authenticated user.");

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
