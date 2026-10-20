using System.ComponentModel.DataAnnotations;

namespace PlantOps.Api.Configuration;

/// <summary>Login rate limit, from the "RateLimiting" configuration section (defaults: 10 attempts per minute per IP).</summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    [Range(1, 10_000)]
    public int LoginPermitLimit { get; init; } = 10;

    [Range(1, 3_600)]
    public int LoginWindowSeconds { get; init; } = 60;
}
