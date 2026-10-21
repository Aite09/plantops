using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using PlantOps.Api.Caching;

namespace PlantOps.Api.IntegrationTests;

/// <summary>
/// The storage rule of the dashboard's cache policy on its own: only a 200 without a cookie is kept. Over HTTP the
/// dashboard never answers anything else to a caller who gets past authorization, so this is tested directly.
/// </summary>
public sealed class SharedResponseCachePolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_200_without_a_cookie_is_stored()
    {
        var context = await ServedAsync(StatusCodes.Status200OK);

        context.AllowCacheStorage.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusCodes.Status204NoContent)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task Any_other_status_is_never_stored(int status)
    {
        var context = await ServedAsync(status);

        context.AllowCacheStorage.Should().BeFalse();
    }

    [Fact]
    public async Task A_response_that_sets_a_cookie_is_never_stored()
    {
        var context = await ServedAsync(StatusCodes.Status200OK, setCookie: "session=abc");

        context.AllowCacheStorage.Should().BeFalse();
    }

    // What the middleware does around the endpoint: CacheRequestAsync before it runs, ServeResponseAsync after.
    private static async Task<OutputCacheContext> ServedAsync(int status, string? setCookie = null)
    {
        IOutputCachePolicy policy = new SharedResponseCachePolicy();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        var context = new OutputCacheContext { HttpContext = httpContext };

        await policy.CacheRequestAsync(context, Ct);
        httpContext.Response.StatusCode = status;
        if (setCookie is not null)
        {
            httpContext.Response.Headers.SetCookie = setCookie;
        }

        await policy.ServeResponseAsync(context, Ct);
        return context;
    }
}
