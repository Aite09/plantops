using System.Net;
using PlantOps.Api.IntegrationTests.Infrastructure;

namespace PlantOps.Api.IntegrationTests;

/// <summary>
/// Browser preflight (OPTIONS) requests. Only the configured web-app origin may call the API cross-origin, and only
/// with the methods and headers the web app uses.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CorsTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Preflight_from_the_web_app_allows_PUT_with_authorization_and_content_type_headers()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight(PlantOpsApiFactory.AllowedOrigin, "PUT"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        HeaderValues(response, "Access-Control-Allow-Origin").Should().Equal(PlantOpsApiFactory.AllowedOrigin);
        HeaderValues(response, "Access-Control-Allow-Methods").Should().Contain("PUT");
        // Header names are case-insensitive, so compare them in lower case.
        HeaderValues(response, "Access-Control-Allow-Headers").Select(h => h.ToLowerInvariant())
            .Should().Contain(["authorization", "content-type"]);
    }

    [Fact]
    public async Task Preflight_from_an_unknown_origin_gets_no_CORS_headers()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight("https://evil.example", "PUT"), Ct);

        // Without Access-Control-Allow-* headers the browser blocks the real request.
        response.Headers.Select(h => h.Key).Should().NotContain(name => name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preflight_does_not_allow_a_method_the_web_app_never_uses()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight(PlantOpsApiFactory.AllowedOrigin, "DELETE"), Ct);

        // ASP.NET Core answers with the policy's own method list and leaves the refusal to the browser, so the
        // check is that this list is exactly what the web app uses (an absent header would also pass NotContain).
        HeaderValues(response, "Access-Control-Allow-Methods").Should().BeEquivalentTo("GET", "POST", "PUT");
    }

    private static HttpRequestMessage Preflight(string origin, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/work-orders/6f0d2c1a-0000-0000-0000-000000000001");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    // The Allow-Methods and Allow-Headers values are comma-separated lists.
    private static IEnumerable<string> HeaderValues(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? values.SelectMany(v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            : [];
}
