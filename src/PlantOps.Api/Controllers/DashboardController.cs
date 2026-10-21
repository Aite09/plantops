using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PlantOps.Api.Authorization;
using PlantOps.Api.Caching;
using PlantOps.Application.WorkOrders.Queries;

namespace PlantOps.Api.Controllers;

[Route("api/dashboard")]
public sealed class DashboardController(ISender sender) : ApiControllerBase
{
    /// <summary>
    /// Supervisor's overview: Critical and High work waiting for approval or assignment, ageing work, open work per
    /// plant unit and each technician's load. Cached for up to 60 seconds and dropped after any work-order or account change;
    /// generatedAtUtc says when it was computed.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.ManageWorkOrders)]
    [OutputCache(PolicyName = CachePolicies.Dashboard)]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        // Not RequestAborted: on a cache miss, concurrent requests wait for this one and receive its outcome (output-cache
        // locking), so one caller hanging up must not fail everyone waiting. A miss is five short queries.
        var result = await sender.Send(new GetDashboardQuery(), CancellationToken.None);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
