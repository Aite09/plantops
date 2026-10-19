using MediatR;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Abstractions.Messaging;

// CQRS made explicit in the type system: a command changes state, a query only reads.
// Both always return a Result, which is what lets ValidationBehavior turn failures into Results.

public interface ICommand : IRequest<Result>;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
