using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantOps.Infrastructure.Persistence.Auditing;

namespace PlantOps.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType).HasMaxLength(AuditEntry.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(AuditEntry.ActionMaxLength).IsRequired();
        builder.Property(a => a.Changes).IsRequired();

        // Serves "history of one work order, in order", the only way the log is read.
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.TimestampUtc });
    }
}
