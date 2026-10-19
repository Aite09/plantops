using PlantOps.Application.PlantUnits;

namespace PlantOps.Application.Abstractions.Persistence;

public interface IPlantUnitQueries
{
    Task<IReadOnlyList<PlantUnitDto>> ListWithEquipmentAsync(CancellationToken cancellationToken);
}
