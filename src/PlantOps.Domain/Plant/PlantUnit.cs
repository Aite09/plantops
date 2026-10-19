using PlantOps.Domain.Common;

namespace PlantOps.Domain.Plant;

/// <summary>A process unit within the plant, e.g. "U-100 Crude Distillation". Reference data.</summary>
public sealed class PlantUnit : Entity
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;

    public PlantUnit(Guid id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
    }

    private PlantUnit() { }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
}
