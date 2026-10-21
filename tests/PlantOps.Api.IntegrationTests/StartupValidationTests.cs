using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using PlantOps.Api.IntegrationTests.Infrastructure;

namespace PlantOps.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class StartupValidationTests(PlantOpsApiFactory factory)
{
    [Fact]
    public void The_API_refuses_to_start_when_the_signing_key_is_an_unresolved_Key_Vault_reference()
    {
        // If App Service can't resolve a Key Vault reference it passes the reference text on as the value. That text is
        // long enough for MinLength but publicly guessable, so signing tokens with it would let anyone forge them.
        using var api = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Jwt:SigningKey", "@Microsoft.KeyVault(VaultName=kv-plantops-example;SecretName=jwt-signing-key)"));

        var start = () => api.CreateClient();

        start.Should().Throw<OptionsValidationException>().WithMessage("*unresolved Key Vault reference*");
    }

    [Fact]
    public void The_API_refuses_to_start_when_AI_is_switched_on_without_a_valid_base_URL()
    {
        // Otherwise the mistake would only show up as a 503 the first time someone presses "Explain this fault".
        using var api = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Ai:Enabled", "true")
            .UseSetting("Ai:BaseUrl", "localhost:11434"));

        var start = () => api.CreateClient();

        start.Should().Throw<OptionsValidationException>().WithMessage("*Ai:BaseUrl must be an absolute http or https URL*");
    }
}
