using System.Security.Claims;
using PlantOps.Application.Abstractions.Authentication;

namespace PlantOps.Api.Authentication;

/// <summary>
/// Reads the user id from the authenticated principal's claims. Any authentication scheme that issues a
/// NameIdentifier claim (demo header now, JWT in Phase 3, Microsoft Entra ID later) works unchanged.
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("The current request has no authenticated user.");
}
