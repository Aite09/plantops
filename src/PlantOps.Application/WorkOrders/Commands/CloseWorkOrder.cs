using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Commands;

/// <summary>The assigned technician finishes the job and records what was done.</summary>
public sealed record CloseWorkOrderCommand(Guid WorkOrderId, string ResolutionNotes) : ICommand;

internal sealed class CloseWorkOrderCommandValidator : AbstractValidator<CloseWorkOrderCommand>
{
    public CloseWorkOrderCommandValidator()
    {
        RuleFor(c => c.WorkOrderId).NotEmpty();
        RuleFor(c => c.ResolutionNotes).NotEmpty().MaximumLength(WorkOrder.ResolutionNotesMaxLength);
    }
}

internal sealed class CloseWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<CloseWorkOrderCommand>
{
    public async Task<Result> Handle(CloseWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        var result = workOrder.Close(currentUser.UserId, command.ResolutionNotes, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
