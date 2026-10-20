using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A second in-memory API on the same SQL Server whose clock only moves when the test moves it, for tests about
/// timestamps and "oldest first" that must not depend on the machine clock ticking between two requests.
/// Clients reuse the shared API's tokens: bearer validation checks token lifetimes against the real clock, which
/// this fake one does not replace, so tokens stay valid whatever time the fake clock shows.
/// </summary>
public sealed class FakeClockApi : IAsyncDisposable
{
    private readonly PlantOpsApiFactory _factory;
    private readonly WebApplicationFactory<Program> _api;

    public FakeClockApi(PlantOpsApiFactory factory, DateTimeOffset now)
    {
        _factory = factory;
        Clock = new FakeTimeProvider(now);
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock)));
        Steps = new WorkOrderSteps(CreateClientAsAsync);
    }

    /// <summary>The clock every handler and the audit interceptor read "now" from.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>Lifecycle steps sent to this API, so each one is stamped with the fake clock's time.</summary>
    public WorkOrderSteps Steps { get; }

    public async Task<HttpClient> CreateClientAsAsync(TestUser user)
    {
        var signedIn = await _factory.CreateClientAsAsync(user);
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
        return client;
    }

    public ValueTask DisposeAsync() => _api.DisposeAsync();
}
