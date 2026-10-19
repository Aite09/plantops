using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using PlantOps.Api.Authentication;
using PlantOps.Api.ErrorHandling;
using PlantOps.Api.OpenApi;
using PlantOps.Application;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Infrastructure;
using PlantOps.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging: log properties stay queryable fields instead of being flattened into text.
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Composition root: the only place that knows about every layer.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Enums travel as text ("High", not 3) in both requests and responses. The second line covers the
// OpenAPI generator, which reads the minimal-API JSON options rather than the MVC ones.
// FluentValidation is the single source of field rules and messages. Without this, MVC would also add its own
// "field is required" errors for non-nullable properties, with different wording and C#-cased field names.
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Malformed JSON gets a generic message instead of one that names internal .NET types.
        options.AllowInputFormatterExceptionMessages = false;
    });
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 7807 ProblemDetails for every error response, including unhandled exceptions.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Secure by default: every endpoint requires an authenticated user unless it explicitly opts out.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

if (builder.Environment.IsDevelopment())
{
    // TEMPORARY until Phase 3 replaces it with ASP.NET Core Identity + JWT bearer tokens.
    builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, configureOptions: null);
}

builder.Services.AddOpenApi(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddDocumentTransformer<DemoUserSecuritySchemeTransformer>();
    }
});

var app = builder.Build();

// Outermost, so the request log records the final status code (e.g. 409) after the exception handler has run,
// instead of seeing the raw exception and logging every handled error as a 500 with a stack trace.
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
// Turns empty 401/403/404 responses into ProblemDetails bodies too.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "PlantOps API v1");
        options.EnablePersistAuthorization();
    });

    await app.Services.InitializeDevelopmentDatabaseAsync();
}

// No UseHttpsRedirection: for an API a redirect comes too late, because the client has already sent its token
// over plain HTTP. Clients must call https:// URLs. In Azure, TLS is terminated by App Service (Phase 6), whose
// "HTTPS only" setting is only a redirect safety net, not a refusal.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
