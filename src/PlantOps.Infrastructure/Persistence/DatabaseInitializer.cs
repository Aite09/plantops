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

    /// <summary>
    /// Applies pending migrations and loads demo users and sample data. Development only: in deployed environments the
    /// CI/CD pipeline applies migrations, so the app's runtime identity needs no schema-change (DDL) rights
    /// and a failed migration fails the deployment instead of the app's startup.
    /// </summary>
    public static async Task InitializeDevelopmentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<AuthDbContext>().Database.MigrateAsync(cancellationToken);
        var db = provider.GetRequiredService<PlantOpsDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var demoPassword = provider.GetRequiredService<IConfiguration>()[DemoPasswordKey];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            throw new InvalidOperationException(
                $"'{DemoPasswordKey}' is missing. Set a demo password with `dotnet user-secrets` (see README).");
        }

        await DevIdentitySeeder.SeedAsync(provider.GetRequiredService<UserManager<ApplicationUser>>(), demoPassword);
        await DevDataSeeder.SeedAsync(db, provider.GetRequiredService<TimeProvider>().GetUtcNow(), cancellationToken);
    }
}
