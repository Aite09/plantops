using Microsoft.AspNetCore.Authorization;
using PlantOps.Application.Abstractions.Authentication;

namespace PlantOps.Api.Authorization;

/// <summary>
/// Named policies describe what a user may do; which roles that maps to is decided once, here.
/// Controllers say [Authorize(Policy = Policies.ManageWorkOrders)], never a list of role strings.
/// Finer rules that depend on the specific work order (who reported it, who it's assigned to) live in the
/// handlers and the domain, because a role alone can't answer them.
/// </summary>
public static class Policies
{
    public const string ReportFaults = nameof(ReportFaults);
    public const string ManageWorkOrders = nameof(ManageWorkOrders);
    public const string PerformWork = nameof(PerformWork);
    public const string ViewUsers = nameof(ViewUsers);
    public const string ManageUsers = nameof(ManageUsers);

    public static IServiceCollection AddPlantOpsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Secure by default: every endpoint requires a signed-in user unless it explicitly opts out.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(ReportFaults, policy => policy.RequireRole(Roles.All))
            .AddPolicy(ManageWorkOrders, policy => policy.RequireRole(Roles.Supervisor, Roles.Admin))
            .AddPolicy(PerformWork, policy => policy.RequireRole(Roles.Technician))
            .AddPolicy(ViewUsers, policy => policy.RequireRole(Roles.Supervisor, Roles.Admin))
            .AddPolicy(ManageUsers, policy => policy.RequireRole(Roles.Admin));

        return services;
    }
}
