using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlantOps.Api.Authorization;
using PlantOps.Api.Caching;
using PlantOps.Api.Contracts;
using PlantOps.Application.Users;

namespace PlantOps.Api.Controllers;

[Route("api/users")]
// A new technician appears in the dashboard's workload table, so a successful create drops the cached copy.
[EvictDashboardCache]
public sealed class UsersController(ISender sender) : ApiControllerBase
{
    /// <summary>Lists users, optionally filtered by role (e.g. role=Technician for the assign pick list).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewUsers)]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List([FromQuery] string? role, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListUsersQuery(role), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Admin creates an account with one role. There is no self-registration.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageUsers)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CreatedResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(request.Email, request.DisplayName, request.Password, request.Role);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new CreatedResponse(result.Value))
            : Problem(result.Error);
    }
}
