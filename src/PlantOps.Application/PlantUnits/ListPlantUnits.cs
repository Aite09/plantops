using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;

namespace PlantOps.Application.PlantUnits;

public sealed record EquipmentDto(Guid Id, string Tag, string Name);

public sealed record PlantUnitDto(Guid Id, string Code, string Name, IReadOnlyList<EquipmentDto> Equipment);

/// <summary>Plant units with their equipment, for the "report a fault" form's pickers.</summary>
public sealed record ListPlantUnitsQuery : IQuery<IReadOnlyList<PlantUnitDto>>;

internal sealed class ListPlantUnitsQueryHandler(IPlantUnitQueries queries)
    : IQueryHandler<ListPlantUnitsQuery, IReadOnlyList<PlantUnitDto>>
{
    public async Task<Result<IReadOnlyList<PlantUnitDto>>> Handle(ListPlantUnitsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await queries.ListWithEquipmentAsync(cancellationToken));
}
