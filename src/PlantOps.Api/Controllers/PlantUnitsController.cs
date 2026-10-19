using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlantOps.Application.PlantUnits;

namespace PlantOps.Api.Controllers;

[Route("api/plant-units")]
public sealed class PlantUnitsController(ISender sender) : ApiControllerBase
{
    /// <summary>Lists plant units with their equipment, for pick lists.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlantUnitDto>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<PlantUnitDto>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListPlantUnitsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
