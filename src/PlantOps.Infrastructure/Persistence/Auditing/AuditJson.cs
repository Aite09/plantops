using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlantOps.Infrastructure.Persistence.Auditing;

internal static class AuditJson
{
    // Enums as names ("Approved", not 1) so the stored history stays readable even if enum numbers change.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

internal sealed record AuditChange(string Property, object? OldValue, object? NewValue);
