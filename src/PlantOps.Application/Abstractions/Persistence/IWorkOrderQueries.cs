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

    /// <summary>
    /// The dashboard's numbers, counted in SQL: Critical/High work not yet approved or assigned plus open work
    /// reported before <paramref name="ageingCutoff"/> (first <paramref name="needsAttentionLimit"/> rows, highest
    /// priority and oldest first), open work per unit and status, and assigned and started work per technician.
    /// </summary>
    Task<DashboardData> GetDashboardAsync(DateTimeOffset ageingCutoff, int needsAttentionLimit, CancellationToken cancellationToken);
}
