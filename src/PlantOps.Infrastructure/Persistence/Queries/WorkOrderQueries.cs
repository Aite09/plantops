using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.Common;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Infrastructure.Persistence.Queries;

/// <summary>
/// Read side. Each query projects straight into a DTO with Select, so SQL Server returns only the needed
/// columns, and AsNoTracking skips change-tracking work for data nobody will modify.
/// </summary>
internal sealed class WorkOrderQueries(PlantOpsDbContext db) : IWorkOrderQueries
{
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.WorkOrders.AnyAsync(w => w.Id == id, cancellationToken);

    // User names are null here and filled in by the query handlers from IUserDirectory, because users live in a
    // separate identity store that this query cannot join to.
    public Task<WorkOrderDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        (from w in db.WorkOrders.AsNoTracking()
         join u in db.PlantUnits on w.PlantUnitId equals u.Id
         join e in db.Equipment on w.EquipmentId equals e.Id
         where w.Id == id
         select new WorkOrderDetailsDto(
             w.Id,
             w.Title,
             w.Description,
             w.Priority,
             w.Status,
             u.Id,
             u.Code,
             u.Name,
             e.Id,
             e.Tag,
             e.Name,
             w.ReportedByUserId,
             null,
             w.ApprovedByUserId,
             null,
             w.AssignedToUserId,
             null,
             w.ResolutionNotes,
             w.CreatedAtUtc,
             w.ApprovedAtUtc,
             w.AssignedAtUtc,
             w.StartedAtUtc,
             w.ClosedAtUtc,
             EF.Property<byte[]>(w, "RowVersion")))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<WorkOrderSummaryDto>> ListAsync(ListWorkOrdersQuery query, CancellationToken cancellationToken)
    {
        var workOrders = db.WorkOrders.AsNoTracking();

        if (query.Status is { } status)
        {
            workOrders = workOrders.Where(w => w.Status == status);
        }

        if (query.PlantUnitId is { } plantUnitId)
        {
            workOrders = workOrders.Where(w => w.PlantUnitId == plantUnitId);
        }

        if (query.Priority is { } priority)
        {
            workOrders = workOrders.Where(w => w.Priority == priority);
        }

        if (query.ActiveOnly)
        {
            workOrders = workOrders.Where(w => w.Status != WorkOrderStatus.Closed);
        }

        if (query.AssignedToUserId is { } assignedTo)
        {
            workOrders = workOrders.Where(w => w.AssignedToUserId == assignedTo);
        }

        var totalCount = await workOrders.CountAsync(cancellationToken);

        var items = await (
                from w in workOrders
                join u in db.PlantUnits on w.PlantUnitId equals u.Id
                join e in db.Equipment on w.EquipmentId equals e.Id
                // Id as the final tie-breaker gives a stable order, so rows never repeat or vanish between pages.
                orderby w.Priority descending, w.CreatedAtUtc, w.Id
                select new WorkOrderSummaryDto(
                    w.Id,
                    w.Title,
                    w.Priority,
                    w.Status,
                    u.Code,
                    e.Tag,
                    w.AssignedToUserId,
                    null,
                    w.CreatedAtUtc))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkOrderSummaryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<DashboardData> GetDashboardAsync(DateTimeOffset ageingCutoff, int needsAttentionLimit, CancellationToken cancellationToken)
    {
        // Open work that is either Critical or High and not yet approved or assigned, or was reported before the
        // cutoff. "Not Closed" is a top-level AND, not a test inside one branch of the OR (logically the same: the
        // High branch only matches Open or Approved). That gives SQL Server a predicate to seek
        // IX_WorkOrders_Status_PlantUnitId on, past the closed rows, instead of scanning the whole table. The cutoff
        // is a parameter computed from the injected clock, not GETUTCDATE(), so tests control it.
        var needsAttention = db.WorkOrders.AsNoTracking().Where(w =>
            w.Status != WorkOrderStatus.Closed
            && ((w.Priority >= WorkOrderPriority.High && (w.Status == WorkOrderStatus.Open || w.Status == WorkOrderStatus.Approved))
                || w.CreatedAtUtc < ageingCutoff));

        var needsAttentionTotal = await needsAttention.CountAsync(cancellationToken);
        var needsAttentionItems = await (
                from w in needsAttention
                join u in db.PlantUnits on w.PlantUnitId equals u.Id
                join e in db.Equipment on w.EquipmentId equals e.Id
                // Same order as the work-order list, so the two never disagree about what comes first.
                orderby w.Priority descending, w.CreatedAtUtc, w.Id
                select new WorkOrderSummaryDto(
                    w.Id, w.Title, w.Priority, w.Status, u.Code, e.Tag, w.AssignedToUserId, null, w.CreatedAtUtc))
            .Take(needsAttentionLimit)
            .ToListAsync(cancellationToken);

        // One row per plant unit, units with no open work included. Each count is a seek on
        // IX_WorkOrders_Status_PlantUnitId that never touches the table itself.
        var units = await db.PlantUnits.AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => new UnitWorkloadDto(
                u.Id,
                u.Code,
                u.Name,
                db.WorkOrders.Count(w => w.Status == WorkOrderStatus.Open && w.PlantUnitId == u.Id),
                db.WorkOrders.Count(w => w.Status == WorkOrderStatus.Approved && w.PlantUnitId == u.Id),
                db.WorkOrders.Count(w => w.Status == WorkOrderStatus.Assigned && w.PlantUnitId == u.Id),
                db.WorkOrders.Count(w => w.Status == WorkOrderStatus.InProgress && w.PlantUnitId == u.Id)))
            .ToListAsync(cancellationToken);

        var technicians = await db.WorkOrders.AsNoTracking()
            .Where(w => (w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress) && w.AssignedToUserId != null)
            .GroupBy(w => w.AssignedToUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Assigned = g.Count(w => w.Status == WorkOrderStatus.Assigned),
                InProgress = g.Count(w => w.Status == WorkOrderStatus.InProgress),
            })
            .ToListAsync(cancellationToken);

        return new DashboardData(
            needsAttentionItems,
            needsAttentionTotal,
            units,
            technicians.Select(t => new TechnicianWorkCount(t.UserId!.Value, t.Assigned, t.InProgress)).ToList());
    }
}
