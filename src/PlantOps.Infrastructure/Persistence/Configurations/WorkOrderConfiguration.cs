using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Title).HasMaxLength(WorkOrder.TitleMaxLength).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(WorkOrder.DescriptionMaxLength).IsRequired();
        builder.Property(w => w.ResolutionNotes).HasMaxLength(WorkOrder.ResolutionNotesMaxLength);

        // Status as text keeps the table readable in SQL; priority stays numeric so ORDER BY priority works.
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(w => w.Priority);

        // Optimistic concurrency: SQL Server bumps rowversion on every update, and EF Core adds it to the
        // UPDATE's WHERE clause. A shadow property keeps this persistence detail out of the domain model.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // Restrict, not cascade: deleting reference data must never silently delete work-order history.
        builder.HasOne<PlantUnit>()
            .WithMany()
            .HasForeignKey(w => w.PlantUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(w => w.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
