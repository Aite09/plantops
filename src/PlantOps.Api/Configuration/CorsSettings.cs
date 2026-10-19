namespace PlantOps.Api.Configuration;

/// <summary>
/// Origins allowed to call this API from a browser, from the "Cors" configuration section. Empty means no
/// cross-origin calls at all, which is the safe default for any environment that has not configured its front end.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}
