using System.Text.Json;
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
    string? AssignedToName,
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
    string? ReportedByName,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    Guid? AssignedToUserId,
    string? AssignedToName,
    string? ResolutionNotes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? AssignedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    byte[] Version);

/// <summary>One recorded change: who did it, when, and the old and new value of each field that changed.</summary>
public sealed record AuditEntryDto(
    DateTimeOffset TimestampUtc,
    string Action,
    Guid? UserId,
    string? UserName,
    IReadOnlyList<AuditChangeDto> Changes);

/// <summary>Values stay as raw JSON (string, number or null) so each keeps its original type.</summary>
public sealed record AuditChangeDto(string Property, JsonElement? OldValue, JsonElement? NewValue);
