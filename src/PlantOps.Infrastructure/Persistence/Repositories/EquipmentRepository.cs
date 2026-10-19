using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Plant;

namespace PlantOps.Infrastructure.Persistence.Repositories;

internal sealed class EquipmentRepository(PlantOpsDbContext db) : IEquipmentRepository
{
    public Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Equipment.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
}
