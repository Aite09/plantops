using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Infrastructure.Persistence;

namespace PlantOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PlantOps");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'PlantOps' is missing. Set it with `dotnet user-secrets` locally (see README).");
        }

        services.AddDbContext<PlantOpsDbContext>(options =>
            // Retries transient faults (network blips, Azure SQL failovers) with exponential back-off.
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Abstracted clock: production uses the system clock, tests can substitute a fake one.
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
