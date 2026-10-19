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
}
