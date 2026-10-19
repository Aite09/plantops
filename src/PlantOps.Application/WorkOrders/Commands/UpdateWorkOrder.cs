using FluentValidation;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Commands;

/// <summary>
/// Edits the title, fault description and priority of a work order that is not closed. <see cref="Version"/> is the
/// value from the details the user was looking at; if the work order changed since, the edit is rejected.
/// </summary>
public sealed record UpdateWorkOrderCommand(
    Guid WorkOrderId,
    string Title,
    string Description,
    WorkOrderPriority Priority,
    byte[] Version) : ICommand;

internal sealed class UpdateWorkOrderCommandValidator : AbstractValidator<UpdateWorkOrderCommand>
{
    public UpdateWorkOrderCommandValidator()
    {
        RuleFor(c => c.WorkOrderId).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(WorkOrder.TitleMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(WorkOrder.DescriptionMaxLength);
        RuleFor(c => c.Priority).IsInEnum();
        // SQL Server rowversion values are always 8 bytes.
        RuleFor(c => c.Version)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(v => v.Length == 8).WithMessage("'Version' is not a valid work order version.");
    }
}

internal sealed class UpdateWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateWorkOrderCommand>
{
    public async Task<Result> Handle(UpdateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        workOrderRepository.SetExpectedVersion(workOrder, command.Version);

        var result = workOrder.UpdateDetails(command.Title, command.Description, command.Priority);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
