using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PlantOps.Api.ErrorHandling;

/// <summary>
/// Last line of defence for exceptions nobody handled (bugs, database outages, concurrency clashes).
/// Expected business failures never get here: they travel as Results and are mapped in ApiControllerBase.
/// The response is an RFC 7807 ProblemDetails with a trace id and no stack trace or exception message.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, code) = exception switch
        {
            // Raised when the rowversion no longer matches: someone else saved this work order first.
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record was changed by someone else. Reload it and try again.", "Concurrency.Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "Server.Error"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Concurrency conflict for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Extensions = { ["code"] = code },
            },
        });
    }
}
