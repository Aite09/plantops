using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.PlantUnits;

namespace PlantOps.Infrastructure.Persistence.Queries;

internal sealed class PlantUnitQueries(PlantOpsDbContext db) : IPlantUnitQueries
{
    public async Task<IReadOnlyList<PlantUnitDto>> ListWithEquipmentAsync(CancellationToken cancellationToken) =>
        await db.PlantUnits
            .AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => new PlantUnitDto(
                u.Id,
                u.Code,
                u.Name,
                db.Equipment
                    .Where(e => e.PlantUnitId == u.Id)
                    .OrderBy(e => e.Tag)
                    .Select(e => new EquipmentDto(e.Id, e.Tag, e.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
}
