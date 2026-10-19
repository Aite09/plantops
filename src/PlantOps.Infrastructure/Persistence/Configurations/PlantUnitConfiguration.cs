using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantOps.Domain.Plant;
using PlantOps.Infrastructure.Persistence.Seed;

namespace PlantOps.Infrastructure.Persistence.Configurations;

internal sealed class PlantUnitConfiguration : IEntityTypeConfiguration<PlantUnit>
{
    public void Configure(EntityTypeBuilder<PlantUnit> builder)
    {
        builder.ToTable("PlantUnits");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Code).HasMaxLength(PlantUnit.CodeMaxLength).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(PlantUnit.NameMaxLength).IsRequired();
        builder.HasIndex(u => u.Code).IsUnique();

        builder.HasData(ReferenceData.PlantUnits);
    }
}
