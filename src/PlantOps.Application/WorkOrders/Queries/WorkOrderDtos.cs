using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Queries;

/// <summary>One row in the work-order list.</summary>
public sealed record WorkOrderSummaryDto(
    Guid Id,
    string Title,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    string PlantUnitCode,
    string EquipmentTag,
    Guid? AssignedToUserId,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Everything shown on the work-order detail page. <see cref="Version"/> is the row version the client must send
/// back when editing, so a save based on stale data is rejected instead of overwriting someone else's change.
/// </summary>
public sealed record WorkOrderDetailsDto(
    Guid Id,
    string Title,
    string Description,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    Guid PlantUnitId,
    string PlantUnitCode,
    string PlantUnitName,
    Guid EquipmentId,
    string EquipmentTag,
    string EquipmentName,
    Guid ReportedByUserId,
    Guid? ApprovedByUserId,
    Guid? AssignedToUserId,
    string? ResolutionNotes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? AssignedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    byte[] Version);
