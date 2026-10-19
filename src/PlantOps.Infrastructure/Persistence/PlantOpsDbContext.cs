using Microsoft.EntityFrameworkCore;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Infrastructure.Persistence;

public sealed class PlantOpsDbContext(DbContextOptions<PlantOpsDbContext> options) : DbContext(options)
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<PlantUnit> PlantUnits => Set<PlantUnit>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Picks up every IEntityTypeConfiguration<T> in this assembly, so mappings live next to each other, not in one giant method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantOpsDbContext).Assembly);
}
