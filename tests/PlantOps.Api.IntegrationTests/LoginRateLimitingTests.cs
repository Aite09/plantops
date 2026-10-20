using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using PlantOps.Api.Contracts;
using PlantOps.Api.IntegrationTests.Infrastructure;

namespace PlantOps.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class LoginRateLimitingTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Login_attempts_beyond_the_limit_are_rejected_with_429()
    {
        // A second in-memory API with a limit of 3 on the same SQL Server container, so the shared API keeps its
        // high limit. The hour-long window means the limit cannot reset part-way through the test.
        await using var limitedApi = factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimiting:LoginPermitLimit", "3")
            .UseSetting("RateLimiting:LoginWindowSeconds", "3600"));
        var client = limitedApi.CreateClient();
        var request = new LoginRequest(TestUsers.Staff.Email, TestUsers.Password);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await (await client.PostAsJsonAsync("/api/auth/login", request, TestJson.Options, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        }

        var fourth = await client.PostAsJsonAsync("/api/auth/login", request, TestJson.Options, Ct);

        await fourth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Behind_a_proxy_each_client_IP_has_its_own_login_limit()
    {
        // In Azure the API runs behind App Service's front end with ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
        // (infra/main.bicep). The client IP then comes from X-Forwarded-For, so one visitor using up the limit doesn't
        // lock everyone else out. FORWARDEDHEADERS_ENABLED is that same setting without the ASPNETCORE_ prefix.
        await using var limitedApi = factory.WithWebHostBuilder(builder => builder
            .UseSetting("FORWARDEDHEADERS_ENABLED", "true")
            .UseSetting("RateLimiting:LoginPermitLimit", "1")
            .UseSetting("RateLimiting:LoginWindowSeconds", "3600"));
        var client = limitedApi.CreateClient();

        await (await LoginFromAsync(client, "203.0.113.10")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await LoginFromAsync(client, "203.0.113.10")).ShouldBeProblemAsync(HttpStatusCode.TooManyRequests);
        await (await LoginFromAsync(client, "203.0.113.20")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(TestUsers.Staff.Email, TestUsers.Password), options: TestJson.Options),
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return client.SendAsync(request, Ct);
    }
}
