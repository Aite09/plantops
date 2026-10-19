using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Behaviors;

/// <summary>
/// Outermost pipeline step: logs every request's name, outcome and duration as structured properties.
/// Request bodies are deliberately not logged, so fault descriptions and notes never end up in log storage.
/// </summary>
internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();

        var response = await next();

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (response.IsSuccess)
        {
            logger.LogInformation("{RequestName} succeeded in {ElapsedMs:0} ms", requestName, elapsedMs);
        }
        else
        {
            logger.LogWarning(
                "{RequestName} failed with {ErrorCode} in {ElapsedMs:0} ms", requestName, response.Error.Code, elapsedMs);
        }

        return response;
    }
}
