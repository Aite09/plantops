using PlantOps.Application.Common;
using PlantOps.Application.WorkOrders.Queries;

namespace PlantOps.Application.Abstractions.Persistence;

/// <summary>
/// Read side of CQRS: returns ready-made DTOs straight from the database, without loading or tracking aggregates.
/// </summary>
public interface IWorkOrderQueries
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkOrderDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<WorkOrderSummaryDto>> ListAsync(ListWorkOrdersQuery query, CancellationToken cancellationToken);
}
