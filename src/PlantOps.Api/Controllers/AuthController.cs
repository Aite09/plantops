using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PlantOps.Api.Contracts;
using PlantOps.Application.Auth;

namespace PlantOps.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(ISender sender) : ApiControllerBase
{
    public const string LoginRateLimitPolicy = "login";

    /// <summary>Exchanges email and password for a JWT access token. Limited to a few attempts per minute per IP.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType<AccessTokenDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AccessTokenDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
