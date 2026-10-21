using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Infrastructure.Ai;
using PlantOps.Infrastructure.Persistence;
using PlantOps.Infrastructure.Persistence.Auditing;
using PlantOps.Infrastructure.Persistence.Queries;
using PlantOps.Infrastructure.Persistence.Repositories;

namespace PlantOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        // Scoped like the DbContext, because it needs the current request's user.
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<PlantOpsDbContext>((serviceProvider, options) => options
            // Retries transient faults (network blips, Azure SQL failovers) with exponential back-off.
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .AddInterceptors(serviceProvider.GetRequiredService<AuditingInterceptor>()));

        // Scoped = one instance per HTTP request, so every repository in a request shares the same DbContext
        // and SaveChangesAsync commits all of their changes together.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PlantOpsDbContext>());
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IWorkOrderQueries, WorkOrderQueries>();
        services.AddScoped<IPlantUnitQueries, PlantUnitQueries>();
        services.AddScoped<IAuditLogQueries, AuditLogQueries>();

        // Abstracted clock: production uses the system clock, tests can substitute a fake one.
        services.AddSingleton(TimeProvider.System);

        // "Explain this fault": optional, off by default. One typed HttpClient for any OpenAI-compatible server.
        // The AI provider is deliberately not a health check: an optional feature must not make the API unhealthy.
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<IFaultExplainer, ChatCompletionsFaultExplainer>((serviceProvider, client) =>
        {
            var ai = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
            if (!ai.Enabled)
            {
                return;
            }

            // The trailing slash keeps "/v1" when the relative "chat/completions" is resolved against it.
            client.BaseAddress = new Uri(ai.BaseUrl.EndsWith('/') ? ai.BaseUrl : ai.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
            // An answer capped at a few hundred tokens is a few KB. The cap (default 2 GB) stops a wrong or misbehaving
            // server from filling the API's memory; going over it is an HttpRequestException, so a 503.
            client.MaxResponseContentBufferSize = 256 * 1024;
            // Hosted providers take a bearer token; Ollama needs none, so no header is sent without a key.
            if (!string.IsNullOrWhiteSpace(ai.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ai.ApiKey);
            }
        });

        // Readiness check: can the app reach its database? Tagged so the API can expose it separately from liveness.
        services.AddHealthChecks()
            .AddDbContextCheck<PlantOpsDbContext>("database", tags: [HealthCheckTags.Ready]);

        return services;
    }

    internal static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PlantOps");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'PlantOps' is missing. Set it with `dotnet user-secrets` locally (see README); in Azure it is the ConnectionStrings__PlantOps app setting.");
        }

        return connectionString;
    }
}
