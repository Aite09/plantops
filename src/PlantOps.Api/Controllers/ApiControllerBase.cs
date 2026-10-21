using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PlantOps.Domain.Common;

namespace PlantOps.Api.Controllers;

// No class-level [Produces]: it is a result filter that would overwrite application/problem+json on error responses.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Turns a failed Result into an RFC 7807 problem response with the matching HTTP status code.</summary>
    protected ActionResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            // 503, never 401: the web app signs the user out on any 401, and a failing upstream service is not
            // the user's fault.
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = error is ValidationError validation
            ? ProblemDetailsFactory.CreateValidationProblemDetails(HttpContext, ToModelState(validation), statusCode)
            : ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, detail: error.Description);

        // A stable, machine-readable code the Angular client can switch on, e.g. "WorkOrder.InvalidTransition".
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static ModelStateDictionary ToModelState(ValidationError validation)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in validation.Errors)
        {
            foreach (var message in messages)
            {
                // camelCase so field names match the JSON the client sent.
                modelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(field), message);
            }
        }

        return modelState;
    }
}
