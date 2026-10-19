using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PlantOps.Api.Authentication;

namespace PlantOps.Api.OpenApi;

/// <summary>Adds the demo-user header to the OpenAPI document, so Swagger UI shows an Authorize button for it.</summary>
internal sealed class DemoUserSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[DemoAuthenticationHandler.SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = DemoAuthenticationHandler.HeaderName,
            Description = "Development only. Enter one of: staff, supervisor, tech-a, tech-b.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(DemoAuthenticationHandler.SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
