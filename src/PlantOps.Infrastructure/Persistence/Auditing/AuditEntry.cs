namespace PlantOps.Infrastructure.Persistence.Auditing;

/// <summary>
/// One row per changed entity per save: what changed (old and new values as JSON), who changed it and when.
/// Written only by <see cref="AuditingInterceptor"/>; nothing in the application updates or deletes these rows.
/// </summary>
public sealed class AuditEntry
{
    public const int EntityTypeMaxLength = 100;
    public const int ActionMaxLength = 20;

    public long Id { get; init; }

    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required string Action { get; init; }

    public required string Changes { get; init; }

    // Null for changes made without a signed-in user, such as development seeding.
    public Guid? UserId { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }
}
