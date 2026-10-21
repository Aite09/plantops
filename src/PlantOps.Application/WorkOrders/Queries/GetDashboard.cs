using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;

namespace PlantOps.Application.WorkOrders.Queries;

/// <summary>A plant unit's open work, counted per status.</summary>
public sealed record UnitWorkloadDto(
    Guid PlantUnitId,
    string PlantUnitCode,
    string PlantUnitName,
    int Open,
    int Approved,
    int Assigned,
    int InProgress);

/// <summary>Work a technician holds: assigned but not started yet, and started.</summary>
public sealed record TechnicianWorkloadDto(Guid UserId, string DisplayName, int Assigned, int InProgress);

/// <summary>
/// The supervisor's start-of-shift view: what needs attention now, open work per plant unit, and every technician's
/// load. It is the same for every caller, so the API caches one copy; <see cref="GeneratedAtUtc"/> says how old it is.
/// </summary>
public sealed record DashboardDto(
    DateTimeOffset GeneratedAtUtc,
    int AgeingAfterDays,
    IReadOnlyList<WorkOrderSummaryDto> NeedsAttention,
    int NeedsAttentionTotal,
    IReadOnlyList<UnitWorkloadDto> Units,
    IReadOnlyList<TechnicianWorkloadDto> Technicians);

/// <summary>The part of the dashboard computed in SQL. The handler adds user names and the time.</summary>
public sealed record DashboardData(
    IReadOnlyList<WorkOrderSummaryDto> NeedsAttention,
    int NeedsAttentionTotal,
    IReadOnlyList<UnitWorkloadDto> Units,
    IReadOnlyList<TechnicianWorkCount> Technicians);

public sealed record TechnicianWorkCount(Guid UserId, int Assigned, int InProgress);

public sealed record GetDashboardQuery : IQuery<DashboardDto>
{
    /// <summary>Open work reported longer ago than this is ageing. The web app's list uses the same rule (AGEING_DAYS).</summary>
    public const int AgeingAfterDays = 7;

    /// <summary>"Needs attention" is a short queue, not a report; the full list is one link away.</summary>
    public const int NeedsAttentionLimit = 25;
}

/// <summary>
/// The response is output-cached and one copy is served to every Supervisor and Admin, so this handler must never
/// read who is asking (ICurrentUser). An architecture test enforces that.
/// </summary>
internal sealed class GetDashboardQueryHandler(IWorkOrderQueries queries, IUserDirectory userDirectory, TimeProvider timeProvider)
    : IQueryHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        // One "now" for the whole snapshot: the ageing cutoff and the time the page shows come from the same moment.
        var now = timeProvider.GetUtcNow();
        var data = await queries.GetDashboardAsync(
            now.AddDays(-GetDashboardQuery.AgeingAfterDays), GetDashboardQuery.NeedsAttentionLimit, cancellationToken);

        // Every technician, including those with nothing assigned: they are the people a supervisor can give work to.
        // Only technicians can hold work (AssignWorkOrderCommandHandler checks the role), so this also names every
        // assignee in the "needs attention" list.
        var technicians = await userDirectory.ListAsync(Roles.Technician, cancellationToken);
        var names = technicians.ToDictionary(t => t.Id, t => t.DisplayName);
        var counts = data.Technicians.ToDictionary(c => c.UserId);

        var needsAttention = data.NeedsAttention
            .Select(i => i.AssignedToUserId is { } id ? i with { AssignedToName = names.GetValueOrDefault(id) } : i)
            .ToList();
        var workload = technicians
            .Select(t => counts.TryGetValue(t.Id, out var c)
                ? new TechnicianWorkloadDto(t.Id, t.DisplayName, c.Assigned, c.InProgress)
                : new TechnicianWorkloadDto(t.Id, t.DisplayName, 0, 0))
            .ToList();

        return new DashboardDto(
            now, GetDashboardQuery.AgeingAfterDays, needsAttention, data.NeedsAttentionTotal, data.Units, workload);
    }
}
