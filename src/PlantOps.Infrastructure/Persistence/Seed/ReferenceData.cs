using PlantOps.Domain.Plant;

namespace PlantOps.Infrastructure.Persistence.Seed;

/// <summary>
/// Plant units and equipment that exist in every environment. Seeded through HasData, so they are
/// part of the migrations. Ids are fixed because HasData needs deterministic values.
/// </summary>
internal static class ReferenceData
{
    public static class UnitIds
    {
        public static readonly Guid CrudeDistillation = new("6f0d2c1a-1b2e-4c3d-9a10-000000000100");
        public static readonly Guid Hydrotreater = new("6f0d2c1a-1b2e-4c3d-9a10-000000000200");
        public static readonly Guid CoolingWater = new("6f0d2c1a-1b2e-4c3d-9a10-000000000300");
        public static readonly Guid Utilities = new("6f0d2c1a-1b2e-4c3d-9a10-000000000400");
    }

    public static readonly PlantUnit[] PlantUnits =
    [
        new(UnitIds.CrudeDistillation, "U-100", "Crude Distillation"),
        new(UnitIds.Hydrotreater, "U-200", "Diesel Hydrotreater"),
        new(UnitIds.CoolingWater, "U-300", "Cooling Water System"),
        new(UnitIds.Utilities, "U-400", "Steam and Utilities"),
    ];

    public static readonly Equipment[] Equipment =
    [
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000101"), UnitIds.CrudeDistillation, "P-101A", "Crude charge pump A"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000102"), UnitIds.CrudeDistillation, "E-102", "Crude preheat exchanger"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000103"), UnitIds.CrudeDistillation, "F-101", "Crude charge heater"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000201"), UnitIds.Hydrotreater, "C-201", "Recycle gas compressor"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000202"), UnitIds.Hydrotreater, "R-201", "Hydrotreating reactor"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000203"), UnitIds.Hydrotreater, "P-205", "Reactor feed pump"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000301"), UnitIds.CoolingWater, "CT-301", "Cooling tower fan 1"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000302"), UnitIds.CoolingWater, "P-301B", "Cooling water pump B"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000401"), UnitIds.Utilities, "B-401", "Package boiler"),
        new(new("8a4e1f30-5d6c-4b7a-8e21-000000000402"), UnitIds.Utilities, "PSV-410", "Steam drum safety valve"),
    ];
}
