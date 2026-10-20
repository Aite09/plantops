using System.ComponentModel.DataAnnotations;

namespace PlantOps.Api.Configuration;

/// <summary>
/// Rate limits from the "RateLimiting" configuration section. Defaults: 10 login attempts per minute per IP, and
/// 12 readiness checks per minute across all clients.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    [Range(1, 10_000)]
    public int LoginPermitLimit { get; init; } = 10;

    [Range(1, 3_600)]
    public int LoginWindowSeconds { get; init; } = 60;

    [Range(1, 10_000)]
    public int HealthPermitLimit { get; init; } = 12;

    [Range(1, 3_600)]
    public int HealthWindowSeconds { get; init; } = 60;
}
