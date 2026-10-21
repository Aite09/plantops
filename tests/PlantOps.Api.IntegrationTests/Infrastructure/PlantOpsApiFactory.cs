using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Api.Caching;
using PlantOps.Infrastructure.Identity;
using PlantOps.Infrastructure.Persistence;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a real SQL Server in a throwaway Docker container. One instance is shared by
/// every test in the "Api" collection: the container starts once, migrations run once, users are created once, and
/// Respawn clears the work-order tables before each test.
/// </summary>
public sealed class PlantOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AllowedOrigin = "http://localhost:4200";

    // Same image as docker-compose.yml, so tests run against the SQL Server version used in development.
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly ConcurrentDictionary<string, string> _tokens = new();
    private Respawner? _respawner;

    /// <summary>The API's connection string: the container, with a dedicated database instead of master.</summary>
    public string ConnectionString =>
        new SqlConnectionStringBuilder(_sqlServer.GetConnectionString()) { InitialCatalog = "PlantOpsTests" }.ConnectionString;

    /// <summary>Overrides the login rate limit for this factory; the default is high so tests never trip it.</summary>
    public int LoginPermitLimit { get; init; } = 1_000;

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.StartAsync();

        // Accessing Services builds the host with the settings from ConfigureWebHost.
        await using (var scope = Services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider;
            await provider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
            await provider.GetRequiredService<PlantOpsDbContext>().Database.MigrateAsync();
            await TestUsers.CreateAllAsync(provider.GetRequiredService<UserManager<ApplicationUser>>());
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            // Only business rows. Plant units, equipment, roles and users are shared fixtures for every test.
            TablesToInclude = [new Table("dbo", "WorkOrders"), new Table("dbo", "AuditEntries")],
        });
    }

    /// <summary>Deletes all work orders and audit rows, so each test starts from the same state.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);

        // The output cache lives in this shared API's memory and would otherwise outlive the rows it was built from.
        await Services.GetRequiredService<IOutputCacheStore>().EvictByTagAsync(CachePolicies.DashboardTag, CancellationToken.None);
    }

    /// <summary>An HttpClient signed in as the given user (tokens are cached for the whole test run).</summary>
    public Task<HttpClient> CreateClientAsAsync(TestUser user) => CreateClientAsAsync(this, user);

    /// <summary>
    /// A client for another in-memory API built from this one with WithWebHostBuilder (different settings or services),
    /// signed in as the given user. It shares this API's signing key, so the cached tokens are valid there too.
    /// </summary>
    public async Task<HttpClient> CreateClientAsAsync(WebApplicationFactory<Program> api, TestUser user)
    {
        var token = _tokens.TryGetValue(user.Email, out var cached) ? cached : await LoginAsync(user);
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing", not "Development": no user-secrets, no dev seeding, no Swagger. Everything the API needs is set here.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PlantOps", ConnectionString);
        builder.UseSetting("Jwt:Issuer", "plantops-api");
        builder.UseSetting("Jwt:Audience", "plantops-web");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-only-signing-key-0123456789");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("RateLimiting:LoginPermitLimit", LoginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    private async Task<string> LoginAsync(TestUser user)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestUsers.Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        return _tokens[user.Email] = body!.AccessToken;
    }

    private sealed record LoginResponse(string AccessToken);
}
