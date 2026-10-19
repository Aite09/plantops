using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Infrastructure.Persistence;
using PlantOps.Infrastructure.Persistence.Queries;
using PlantOps.Infrastructure.Persistence.Repositories;

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

        // Scoped = one instance per HTTP request, so every repository in a request shares the same DbContext
        // and SaveChangesAsync commits all of their changes together.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PlantOpsDbContext>());
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IWorkOrderQueries, WorkOrderQueries>();
        services.AddScoped<IPlantUnitQueries, PlantUnitQueries>();

        // Abstracted clock: production uses the system clock, tests can substitute a fake one.
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
