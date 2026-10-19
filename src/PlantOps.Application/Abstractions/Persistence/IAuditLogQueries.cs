using PlantOps.Application.WorkOrders.Queries;

namespace PlantOps.Application.Abstractions.Persistence;

public interface IAuditLogQueries
{
    /// <summary>Every recorded change to one entity, oldest first.</summary>
    Task<IReadOnlyList<AuditEntryDto>> GetHistoryAsync(string entityType, Guid entityId, CancellationToken cancellationToken);
}
