using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace PlantOps.Api.Caching;

/// <summary>
/// Caches a successful GET from a signed-in user and replays that one copy to everyone else who calls the endpoint.
/// Authorization still runs on every request, because UseOutputCache comes after UseAuthorization in Program.cs: a
/// caller who fails the endpoint's policy is rejected before the cache is consulted. Only use this on endpoints whose
/// response is the same for every caller who passes that policy.
/// </summary>
internal sealed class SharedResponseCachePolicy : IOutputCachePolicy
{
    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var method = context.HttpContext.Request.Method;
        var cacheable = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        // Concurrent misses wait for the first one instead of all querying the database (cache stampede).
        context.AllowLocking = true;
        // A safeguard: UseCors runs before UseOutputCache, so CORS headers are added to every response, cache hits
        // included. Keeping one copy per Origin means a stored copy can never carry headers meant for another origin.
        context.CacheVaryByRules.HeaderNames = HeaderNames.Origin;
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // Only a 200 is stored: never an error, and never a response that sets a cookie.
        var response = context.HttpContext.Response;
        if (response.StatusCode != StatusCodes.Status200OK || !StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
