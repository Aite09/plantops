using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Infrastructure.Identity;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public const string DemoPasswordKey = "Seed:DemoUserPassword";
    public const string AdminEmailKey = "Bootstrap:AdminEmail";
    public const string AdminPasswordKey = "Bootstrap:AdminPassword";

    /// <summary>
    /// Development: applies pending migrations, then loads every demo user (Admin included) and the sample data.
    /// Deployed environments never run this. The pipeline applies migrations with its own identity, so the app's
    /// runtime identity needs no schema-change (DDL) rights, and a failed migration fails the deployment instead of the
    /// app's startup.
    /// </summary>
    public static async Task InitializeDevelopmentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<AuthDbContext>().Database.MigrateAsync(cancellationToken);
        await provider.GetRequiredService<PlantOpsDbContext>().Database.MigrateAsync(cancellationToken);
        await SeedAsync(provider, includeAdmin: true, cancellationToken);
    }

    /// <summary>
    /// Deployed environments: `PlantOps.Api seed-demo`, run by the deploy pipeline after the migration bundles. Loads
    /// the public demo accounts (no Admin) and, into an empty table, the sample work orders. Rows only, no DDL. Safe to
    /// run again: it adds what is missing, applies a changed demo password and removes any extra role from a demo
    /// account.
    /// </summary>
    public static async Task SeedDemoDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider, includeAdmin: false, cancellationToken);
    }

    /// <summary>
    /// `PlantOps.Api create-admin`, run once by the owner from their own machine (infra/README.md step 12). The email
    /// and password are given at run time and never stored. Safe to run again; a re-run sets the password given then.
    /// </summary>
    public static async Task CreateAdminAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var configuration = provider.GetRequiredService<IConfiguration>();

        await IdentitySeeder.EnsureAdminAsync(
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            Required(configuration, AdminEmailKey),
            Required(configuration, AdminPasswordKey));
    }

    private static async Task SeedAsync(IServiceProvider provider, bool includeAdmin, CancellationToken cancellationToken)
    {
        var demoPassword = Required(provider.GetRequiredService<IConfiguration>(), DemoPasswordKey);

        await IdentitySeeder.SeedDemoUsersAsync(provider.GetRequiredService<UserManager<ApplicationUser>>(), demoPassword, includeAdmin);
        await DemoDataSeeder.SeedAsync(
            provider.GetRequiredService<PlantOpsDbContext>(), provider.GetRequiredService<TimeProvider>().GetUtcNow(), cancellationToken);
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"'{key}' is missing. The README says where it comes from in each environment.")
            : value;
    }
}
