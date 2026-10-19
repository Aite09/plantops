using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Commands;

/// <summary>A supervisor accepts a reported fault as real work.</summary>
public sealed record ApproveWorkOrderCommand(Guid WorkOrderId) : ICommand;

internal sealed class ApproveWorkOrderCommandValidator : AbstractValidator<ApproveWorkOrderCommand>
{
    public ApproveWorkOrderCommandValidator() => RuleFor(c => c.WorkOrderId).NotEmpty();
}

internal sealed class ApproveWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<ApproveWorkOrderCommand>
{
    public async Task<Result> Handle(ApproveWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        var result = workOrder.Approve(currentUser.UserId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
