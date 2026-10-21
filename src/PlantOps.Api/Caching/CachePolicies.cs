using Microsoft.AspNetCore.OutputCaching;

namespace PlantOps.Api.Caching;

/// <summary>
/// Server-side output caching: the API keeps a finished response in memory and replays it, skipping MediatR, EF Core
/// and JSON serialization. Nothing is cached unless an endpoint opts in with [OutputCache(PolicyName = ...)].
/// </summary>
public static class CachePolicies
{
    public const string Dashboard = nameof(Dashboard);

    /// <summary>Tag on the cached dashboard. Evicted after any successful change to work orders or user accounts.</summary>
    public const string DashboardTag = "dashboard";

    public static IServiceCollection AddPlantOpsOutputCache(this IServiceCollection services) =>
        services.AddOutputCache(options => options.AddPolicy(
            Dashboard,
            policy => policy
                .AddPolicy<SharedResponseCachePolicy>()
                // Eviction handles changes made through this API. The expiry covers what eviction can't see: work
                // becoming "ageing" as time passes, changes made by another instance or directly in the database, and a
                // dashboard that was being computed while a change committed (stored just after the eviction ran).
                .Expire(TimeSpan.FromSeconds(60))
                .Tag(DashboardTag),
            // The built-in default policy refuses signed-in users, and in some .NET 10 patch releases it even overrides
            // later policies that allow them (dotnet/aspnetcore#69386, fixed for 10.0.14). SharedResponseCachePolicy
            // owns all the rules, so the default policy is left out of the chain.
            excludeDefaultPolicy: true));
}
