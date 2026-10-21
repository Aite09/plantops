using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PlantOps.Application.WorkOrders.Queries;

namespace PlantOps.Api.Controllers;

/// <summary>
/// Separate from WorkOrdersController on purpose: explaining changes nothing, so it must not trigger that
/// controller's cache eviction after every successful POST.
/// </summary>
[Route("api/work-orders/{id:guid}/explanation")]
public sealed class WorkOrderExplanationsController(ISender sender) : ApiControllerBase
{
    public const string RateLimitPolicy = "explain";

    /// <summary>
    /// Asks the configured AI model for likely causes, first checks and safety points for this fault, as plain text.
    /// Advisory only: nothing is saved. 404 when the feature is switched off, 503 when the model does not answer.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicy)]
    [ProducesResponseType<FaultExplanationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<FaultExplanationDto>> Explain(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ExplainWorkOrderFaultQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
