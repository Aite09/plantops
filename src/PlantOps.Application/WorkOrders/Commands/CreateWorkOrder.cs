using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Commands;

/// <summary>Plant staff report a fault on a piece of equipment. Returns the new work order's id.</summary>
public sealed record CreateWorkOrderCommand(
    string Title,
    string Description,
    WorkOrderPriority Priority,
    Guid EquipmentId) : ICommand<Guid>;

internal sealed class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(WorkOrder.TitleMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(WorkOrder.DescriptionMaxLength);
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.EquipmentId).NotEmpty();
    }
}

internal sealed class CreateWorkOrderCommandHandler(
    IEquipmentRepository equipmentRepository,
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<CreateWorkOrderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var equipment = await equipmentRepository.GetByIdAsync(command.EquipmentId, cancellationToken);
        if (equipment is null)
        {
            return EquipmentErrors.NotFound(command.EquipmentId);
        }

        var created = WorkOrder.Create(
            command.Title,
            command.Description,
            command.Priority,
            equipment,
            currentUser.UserId,
            timeProvider.GetUtcNow());

        if (created.IsFailure)
        {
            return created.Error;
        }

        workOrderRepository.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return created.Value.Id;
    }
}
