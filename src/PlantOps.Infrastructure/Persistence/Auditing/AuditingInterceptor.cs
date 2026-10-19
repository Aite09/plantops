using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Domain.Common;

namespace PlantOps.Infrastructure.Persistence.Auditing;

/// <summary>
/// Runs just before EF Core saves. For every added, modified or deleted entity it adds an AuditEntry to the same
/// SaveChanges call, so the change and its audit record are committed in one transaction: either both or neither.
/// Because it hooks into EF Core itself, no handler can forget to audit. (Raw SQL would bypass it; this app has none.)
/// </summary>
internal sealed class AuditingInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var userId = currentUser.IsAuthenticated ? currentUser.UserId : (Guid?)null;
        var now = timeProvider.GetUtcNow();

        // Materialise first: adding AuditEntry rows while enumerating the change tracker would modify it mid-loop.
        var auditEntries = context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entry: e, Changes: GetChanges(e)))
            .Where(x => x.Changes.Count > 0)
            .Select(x => new AuditEntry
            {
                EntityType = x.Entry.Metadata.ClrType.Name,
                EntityId = x.Entry.Entity.Id,
                Action = x.Entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated",
                },
                Changes = JsonSerializer.Serialize(x.Changes, AuditJson.Options),
                UserId = userId,
                TimestampUtc = now,
            })
            .ToList();

        context.Set<AuditEntry>().AddRange(auditEntries);
    }

    private static List<AuditChange> GetChanges(EntityEntry entry) =>
        entry.Properties
            // The key is already the EntityId; the rowversion is a persistence detail, not business data.
            .Where(p => !p.Metadata.IsPrimaryKey() && !p.Metadata.IsConcurrencyToken)
            .Where(p => entry.State != EntityState.Modified || (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
            .Select(p => new AuditChange(
                p.Metadata.Name,
                entry.State == EntityState.Added ? null : p.OriginalValue,
                entry.State == EntityState.Deleted ? null : p.CurrentValue))
            .ToList();
}
