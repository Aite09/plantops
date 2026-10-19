using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.Users;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Commands;

/// <summary>A supervisor assigns (or reassigns) an approved work order to a technician.</summary>
public sealed record AssignWorkOrderCommand(Guid WorkOrderId, Guid TechnicianId) : ICommand;

internal sealed class AssignWorkOrderCommandValidator : AbstractValidator<AssignWorkOrderCommand>
{
    public AssignWorkOrderCommandValidator()
    {
        RuleFor(c => c.WorkOrderId).NotEmpty();
        RuleFor(c => c.TechnicianId).NotEmpty();
    }
}

internal sealed class AssignWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : ICommandHandler<AssignWorkOrderCommand>
{
    public async Task<Result> Handle(AssignWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        if (!await userDirectory.IsInRoleAsync(command.TechnicianId, Roles.Technician, cancellationToken))
        {
            return UserErrors.NotATechnician(command.TechnicianId);
        }

        var result = workOrder.Assign(command.TechnicianId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
