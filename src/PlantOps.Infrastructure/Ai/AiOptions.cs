using System.ComponentModel.DataAnnotations;

namespace PlantOps.Infrastructure.Ai;

/// <summary>
/// Options pattern: the "Ai" section, validated at startup. Any server that implements the OpenAI chat-completions
/// API works, so the provider is configuration only; the defaults point at Ollama on this machine.
/// Off unless Enabled is true. ApiKey is a secret (user-secrets locally, an environment variable in Docker), never in
/// appsettings.json; Ollama needs none.
/// </summary>
internal sealed class AiOptions : IValidatableObject
{
    public const string SectionName = "Ai";

    public bool Enabled { get; init; }

    /// <summary>The API root, ending in /v1/ for OpenAI-compatible servers. A missing trailing slash is added.</summary>
    public string BaseUrl { get; init; } = "http://localhost:11434/v1/";

    public string Model { get; init; } = "phi4-mini";

    public string? ApiKey { get; init; }

    /// <summary>A local model's first answer includes loading it into memory, which takes a while on a laptop.</summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; init; } = 60;

    [Range(50, 2000)]
    public int MaxTokens { get; init; } = 400;

    // Runs as part of ValidateDataAnnotations: the provider settings only have to be valid when the feature is on.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult("Ai:BaseUrl must be an absolute http or https URL when Ai:Enabled is true.", [nameof(BaseUrl)]);
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            yield return new ValidationResult("Ai:Model is required when Ai:Enabled is true.", [nameof(Model)]);
        }
    }
}
