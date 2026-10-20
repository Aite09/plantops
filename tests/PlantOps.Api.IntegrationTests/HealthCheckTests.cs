using System.Net;
using Microsoft.AspNetCore.Hosting;
using PlantOps.Api.IntegrationTests.Infrastructure;

namespace PlantOps.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthCheckTests(PlantOpsApiFactory factory)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Health_endpoints_report_healthy_without_signing_in(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Readiness_checks_beyond_the_shared_limit_get_429_while_liveness_stays_open()
    {
        // A second in-memory API with a limit of 2, so the shared API keeps its normal limit. The hour-long window
        // means the limit cannot reset part-way through the test.
        await using var limitedApi = factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimiting:HealthPermitLimit", "2")
            .UseSetting("RateLimiting:HealthWindowSeconds", "3600"));
        var ct = TestContext.Current.CancellationToken;
        // Different clients share the limit: it caps total database load, not load per caller.
        using var first = limitedApi.CreateClient();
        using var second = limitedApi.CreateClient();
        await (await first.GetAsync("/health", ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await second.GetAsync("/health", ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var third = await first.GetAsync("/health", ct);
        var liveness = await second.GetAsync("/health/live", ct);

        await third.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests);
        liveness.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
