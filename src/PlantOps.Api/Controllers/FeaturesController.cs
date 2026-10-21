using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlantOps.Application.Features;

namespace PlantOps.Api.Controllers;

[Route("api/features")]
public sealed class FeaturesController(ISender sender) : ApiControllerBase
{
    /// <summary>Which optional features are switched on, so the web app only shows buttons that will work.</summary>
    [HttpGet]
    [ProducesResponseType<FeaturesDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<FeaturesDto>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFeaturesQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
