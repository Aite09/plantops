using System.ComponentModel.DataAnnotations;

namespace PlantOps.Infrastructure.Identity;

/// <summary>
/// Options pattern: the "Jwt" configuration section, bound to a typed class and validated at startup, so a missing
/// or weak signing key stops the app from starting instead of failing on the first login.
/// SigningKey is a secret: user-secrets locally; in Azure the Jwt__SigningKey app setting (encrypted at rest), which is
/// a Key Vault reference when infra/main.bicep's enableKeyVault is on. Never appsettings.json.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    // HMAC-SHA256 needs a key of at least 256 bits (32 bytes).
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(5, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;
}
