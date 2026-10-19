using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Api.Authentication;

/// <summary>
/// TEMPORARY, Development only: authenticates a request as one of the seeded demo users named in the
/// X-Demo-User header. It exists so Phase 2 can be exercised before Phase 3 adds Identity and JWT, and it is
/// deleted then. Never register it outside Development: anyone could impersonate anyone.
/// </summary>
internal sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoUser";
    public const string HeaderName = "X-Demo-User";

    private static readonly Dictionary<string, (Guid Id, string Role)> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["staff"] = (SeedUsers.Staff, "Staff"),
        ["supervisor"] = (SeedUsers.Supervisor, "Supervisor"),
        ["tech-a"] = (SeedUsers.TechnicianA, "Technician"),
        ["tech-b"] = (SeedUsers.TechnicianB, "Technician"),
    };

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userName = headerValue.ToString();
        if (!Users.TryGetValue(userName, out var user))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Unknown demo user '{userName}'."));
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, user.Role),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
