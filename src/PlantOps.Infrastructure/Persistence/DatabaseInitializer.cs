using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations and loads sample data. Development only: in deployed environments the
    /// CI/CD pipeline applies migrations, so the app's runtime identity needs no schema-change (DDL) rights
    /// and a failed migration fails the deployment instead of the app's startup.
    /// </summary>
    public static async Task InitializeDevelopmentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantOpsDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await db.Database.MigrateAsync(cancellationToken);
        await DevDataSeeder.SeedAsync(db, timeProvider.GetUtcNow(), cancellationToken);
    }
}
