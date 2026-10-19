using PlantOps.Domain.Common;

namespace PlantOps.Domain.Plant;

/// <summary>A tagged piece of equipment (pump, exchanger, compressor) installed in a plant unit. Reference data.</summary>
public sealed class Equipment : Entity
{
    public const int TagMaxLength = 30;
    public const int NameMaxLength = 100;

    public Equipment(Guid id, Guid plantUnitId, string tag, string name)
        : base(id)
    {
        PlantUnitId = plantUnitId;
        Tag = tag;
        Name = name;
    }

    private Equipment() { }

    public Guid PlantUnitId { get; private set; }

    public string Tag { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
}
