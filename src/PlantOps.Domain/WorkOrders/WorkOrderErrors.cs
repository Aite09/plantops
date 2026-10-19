using PlantOps.Domain.Common;

namespace PlantOps.Domain.WorkOrders;

public static class WorkOrderErrors
{
    public static readonly Error TitleRequired = Error.Validation(
        "WorkOrder.TitleRequired", $"Title is required and must be at most {WorkOrder.TitleMaxLength} characters.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "WorkOrder.DescriptionRequired", $"Fault description is required and must be at most {WorkOrder.DescriptionMaxLength} characters.");

    public static readonly Error ResolutionNotesRequired = Error.Validation(
        "WorkOrder.ResolutionNotesRequired", $"Resolution notes are required and must be at most {WorkOrder.ResolutionNotesMaxLength} characters.");

    public static readonly Error InvalidPriority = Error.Validation(
        "WorkOrder.InvalidPriority", "Priority is not a recognised value.");

    public static readonly Error UserRequired = Error.Validation(
        "WorkOrder.UserRequired", "A valid user id is required.");

    public static readonly Error ClosedCannotBeEdited = Error.Conflict(
        "WorkOrder.ClosedCannotBeEdited", "A closed work order cannot be edited.");

    public static readonly Error NotAssignedTechnician = Error.Forbidden(
        "WorkOrder.NotAssignedTechnician", "Only the technician assigned to this work order can do that.");

    public static Error InvalidTransition(WorkOrderStatus from, WorkOrderStatus to) => Error.Conflict(
        "WorkOrder.InvalidTransition", $"A work order cannot move from {from} to {to}.");
}
