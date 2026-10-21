using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OutputCaching;

namespace PlantOps.Api.Caching;

/// <summary>
/// After a successful change (an action that is not a GET and returns 2xx) to something the dashboard shows (work
/// orders, user accounts), drops the cached dashboard, so later requests recompute it instead of waiting for the cache
/// to expire. One exception: a dashboard that was already being computed when the change committed can still be stored
/// just after this eviction; the 60-second expiry bounds that. It is an MVC filter in the Api layer because output
/// caching is an HTTP concern; the Application layer never knows about it.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class EvictDashboardCacheAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || executed.Exception is not null
            || executed.Result is not IStatusCodeActionResult { StatusCode: >= 200 and < 300 })
        {
            return;
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<IOutputCacheStore>();
        // Not RequestAborted: the change is already saved, so a client that hangs up now must not skip the eviction.
        await store.EvictByTagAsync(CachePolicies.DashboardTag, CancellationToken.None);
    }
}
