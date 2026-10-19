using PlantOps.Domain.Common;

namespace PlantOps.Domain.Plant;

public static class EquipmentErrors
{
    public static Error NotFound(Guid id) => Error.NotFound(
        "Equipment.NotFound", $"Equipment '{id}' was not found.");
}
