namespace PlantOps.Domain.Common;

/// <summary>Base type for anything with an identity that persists across state changes.</summary>
public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    // Parameterless constructor lets EF Core materialize entities from the database.
    protected Entity() { }

    public Guid Id { get; private init; }
}
