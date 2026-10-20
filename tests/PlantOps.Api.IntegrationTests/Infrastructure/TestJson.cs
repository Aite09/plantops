using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>The same JSON conventions as the API: camelCase names and enums as strings.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
