using FluentValidation;
using MediatR;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator for the request before its handler. Invalid requests never reach
/// the handler; they come back as a failed Result carrying a <see cref="ValidationError"/>.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    // The behavior is generic, so it cannot write Result.Failure<T>(...) for an unknown T. The matching factory
    // is looked up once per response type and cached in this static field.
    private static readonly Func<Error, TResponse> CreateFailure = BuildFailureFactory();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next();
        }

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        return CreateFailure(new ValidationError(errors));
    }

    private static Func<Error, TResponse> BuildFailureFactory()
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return error => (TResponse)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethods()
            .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition)
            .MakeGenericMethod(valueType);

        return failureMethod.CreateDelegate<Func<Error, TResponse>>();
    }
}
