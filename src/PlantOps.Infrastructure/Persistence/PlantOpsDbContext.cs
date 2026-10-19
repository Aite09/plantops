using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Infrastructure.Persistence;

/// <summary>
/// EF Core's DbContext already is a unit of work: it tracks changes and SaveChangesAsync commits them in one
/// transaction. Implementing <see cref="IUnitOfWork"/> just exposes that to the Application layer without EF types.
/// </summary>
public sealed class PlantOpsDbContext(DbContextOptions<PlantOpsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<PlantUnit> PlantUnits => Set<PlantUnit>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Picks up every IEntityTypeConfiguration<T> in this assembly, so mappings live next to each other, not in one giant method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantOpsDbContext).Assembly);
}
