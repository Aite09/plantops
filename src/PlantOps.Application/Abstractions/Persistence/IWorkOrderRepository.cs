using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.Abstractions.Persistence;

/// <summary>Write-side access to the WorkOrder aggregate. Loads whole aggregates for commands to change.</summary>
public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(WorkOrder workOrder);

    /// <summary>
    /// Makes the next save fail with a concurrency conflict unless the stored row still has this version,
    /// i.e. nobody else has changed the work order since the client read it.
    /// </summary>
    void SetExpectedVersion(WorkOrder workOrder, byte[] version);
}
