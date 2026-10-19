using PlantOps.Domain.Common;
using PlantOps.Domain.Plant;

namespace PlantOps.Domain.WorkOrders;

/// <summary>
/// Aggregate root for a maintenance work order. All state changes go through its methods,
/// so the lifecycle Open → Approved → Assigned → InProgress → Closed cannot be bypassed.
/// </summary>
public sealed class WorkOrder : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int ResolutionNotesMaxLength = 4000;

    // The whole state machine in one table. Assigned → Assigned and InProgress → Assigned let a supervisor
    // reassign, e.g. at shift handover; the new technician then calls Start again.
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> AllowedTransitions = new()
    {
        [WorkOrderStatus.Open] = [WorkOrderStatus.Approved],
        [WorkOrderStatus.Approved] = [WorkOrderStatus.Assigned],
        [WorkOrderStatus.Assigned] = [WorkOrderStatus.Assigned, WorkOrderStatus.InProgress],
        [WorkOrderStatus.InProgress] = [WorkOrderStatus.Assigned, WorkOrderStatus.Closed],
        [WorkOrderStatus.Closed] = [],
    };

    private WorkOrder(
        Guid id,
        string title,
        string description,
        WorkOrderPriority priority,
        Guid plantUnitId,
        Guid equipmentId,
        Guid reportedByUserId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        Title = title;
        Description = description;
        Priority = priority;
        PlantUnitId = plantUnitId;
        EquipmentId = equipmentId;
        ReportedByUserId = reportedByUserId;
        CreatedAtUtc = createdAtUtc;
        Status = WorkOrderStatus.Open;
    }

    private WorkOrder() { }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public WorkOrderPriority Priority { get; private set; }

    public WorkOrderStatus Status { get; private set; }

    // Copied from the equipment at creation so open work can be filtered and grouped by unit without a join.
    public Guid PlantUnitId { get; private set; }

    public Guid EquipmentId { get; private set; }

    public Guid ReportedByUserId { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public DateTimeOffset? AssignedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public static Result<WorkOrder> Create(
        string title,
        string description,
        WorkOrderPriority priority,
        Equipment equipment,
        Guid reportedByUserId,
        DateTimeOffset now)
    {
        var detailsCheck = ValidateDetails(title, description, priority);
        if (detailsCheck.IsFailure)
        {
            return detailsCheck.Error;
        }

        if (reportedByUserId == Guid.Empty)
        {
            return WorkOrderErrors.UserRequired;
        }

        return new WorkOrder(
            Guid.NewGuid(),
            title.Trim(),
            description.Trim(),
            priority,
            equipment.PlantUnitId,
            equipment.Id,
            reportedByUserId,
            now);
    }

    public bool CanTransitionTo(WorkOrderStatus next) => AllowedTransitions[Status].Contains(next);

    public Result UpdateDetails(string title, string description, WorkOrderPriority priority)
    {
        if (Status == WorkOrderStatus.Closed)
        {
            return WorkOrderErrors.ClosedCannotBeEdited;
        }

        var detailsCheck = ValidateDetails(title, description, priority);
        if (detailsCheck.IsFailure)
        {
            return detailsCheck;
        }

        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        return Result.Success();
    }

    public Result Approve(Guid supervisorId, DateTimeOffset now)
    {
        var check = EnsureCanMoveTo(WorkOrderStatus.Approved, supervisorId);
        if (check.IsFailure)
        {
            return check;
        }

        Status = WorkOrderStatus.Approved;
        ApprovedByUserId = supervisorId;
        ApprovedAtUtc = now;
        return Result.Success();
    }

    public Result Assign(Guid technicianId, DateTimeOffset now)
    {
        var check = EnsureCanMoveTo(WorkOrderStatus.Assigned, technicianId);
        if (check.IsFailure)
        {
            return check;
        }

        Status = WorkOrderStatus.Assigned;
        AssignedToUserId = technicianId;
        AssignedAtUtc = now;
        StartedAtUtc = null;
        return Result.Success();
    }

    public Result Start(Guid technicianId, DateTimeOffset now)
    {
        var check = EnsureCanMoveTo(WorkOrderStatus.InProgress, technicianId);
        if (check.IsFailure)
        {
            return check;
        }

        if (technicianId != AssignedToUserId)
        {
            return WorkOrderErrors.NotAssignedTechnician;
        }

        Status = WorkOrderStatus.InProgress;
        StartedAtUtc = now;
        return Result.Success();
    }

    public Result Close(Guid technicianId, string resolutionNotes, DateTimeOffset now)
    {
        var check = EnsureCanMoveTo(WorkOrderStatus.Closed, technicianId);
        if (check.IsFailure)
        {
            return check;
        }

        if (technicianId != AssignedToUserId)
        {
            return WorkOrderErrors.NotAssignedTechnician;
        }

        if (string.IsNullOrWhiteSpace(resolutionNotes) || resolutionNotes.Trim().Length > ResolutionNotesMaxLength)
        {
            return WorkOrderErrors.ResolutionNotesRequired;
        }

        Status = WorkOrderStatus.Closed;
        ResolutionNotes = resolutionNotes.Trim();
        ClosedAtUtc = now;
        return Result.Success();
    }

    private Result EnsureCanMoveTo(WorkOrderStatus next, Guid actingUserId)
    {
        if (actingUserId == Guid.Empty)
        {
            return WorkOrderErrors.UserRequired;
        }

        return CanTransitionTo(next)
            ? Result.Success()
            : WorkOrderErrors.InvalidTransition(Status, next);
    }

    private static Result ValidateDetails(string title, string description, WorkOrderPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMaxLength)
        {
            return WorkOrderErrors.TitleRequired;
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > DescriptionMaxLength)
        {
            return WorkOrderErrors.DescriptionRequired;
        }

        if (!Enum.IsDefined(priority))
        {
            return WorkOrderErrors.InvalidPriority;
        }

        return Result.Success();
    }
}
