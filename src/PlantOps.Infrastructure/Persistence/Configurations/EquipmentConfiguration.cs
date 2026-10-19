using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantOps.Domain.Plant;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Infrastructure.Persistence.Configurations;

internal sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Tag).HasMaxLength(Equipment.TagMaxLength).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(Equipment.NameMaxLength).IsRequired();
        builder.HasIndex(e => e.Tag).IsUnique();

        // Relationship by id only: Equipment has no navigation property to PlantUnit.
        builder.HasOne<PlantUnit>()
            .WithMany()
            .HasForeignKey(e => e.PlantUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(ReferenceData.Equipment);
    }
}
