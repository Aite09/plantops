using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Infrastructure.Persistence.Auditing;

namespace PlantOps.Infrastructure.Persistence.Queries;

internal sealed class AuditLogQueries(PlantOpsDbContext db) : IAuditLogQueries
{
    public async Task<IReadOnlyList<AuditEntryDto>> GetHistoryAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        var rows = await db.AuditEntries
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.TimestampUtc)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        return rows
            .Select(a => new AuditEntryDto(
                a.TimestampUtc,
                a.Action,
                a.UserId,
                UserName: null,
                JsonSerializer.Deserialize<List<AuditChangeDto>>(a.Changes, AuditJson.Options) ?? []))
            .ToList();
    }
}
