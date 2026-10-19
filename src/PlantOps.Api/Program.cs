using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using PlantOps.Api.Authentication;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using PlantOps.Api.Authorization;
using PlantOps.Api.Configuration;
using PlantOps.Api.Controllers;
using PlantOps.Api.ErrorHandling;
using PlantOps.Api.OpenApi;
using PlantOps.Application;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Infrastructure;
using PlantOps.Infrastructure.Identity;
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

// Authentication: ASP.NET Core Identity accounts + JWT bearer tokens, all behind this call. Moving to Microsoft
// Entra ID starts here; the README lists the full set of steps.
builder.Services.AddIdentityAuthentication(builder.Configuration);
builder.Services.AddPlantOpsAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// FluentValidation is the single source of field rules and messages. Without the Suppress flag, MVC would also add
// its own "field is required" errors for non-nullable properties, with different wording and C#-cased field names.
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        // Enums travel as text ("High", not 3).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Malformed JSON gets a generic message instead of one that names internal .NET types.
        options.AllowInputFormatterExceptionMessages = false;
    });
// The OpenAPI generator reads the minimal-API JSON options rather than the MVC ones, so enums need registering here too.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 7807 ProblemDetails for every error response, including unhandled exceptions.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Login is limited per client IP to slow down password guessing across many accounts. Identity's lockout
// (5 failures locks an account for 15 minutes) covers repeated guesses against one account.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.LoginRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// CORS locked down: only the configured front-end origins, only the methods and headers the Angular app uses.
// No cookies are involved (the token travels in the Authorization header), so credentials stay disallowed.
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(CorsSettings.SectionName));
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IOptions<CorsSettings>>((cors, settings) => cors.AddDefaultPolicy(policy => policy
        .WithOrigins(settings.Value.AllowedOrigins)
        .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put)
        .WithHeaders("Authorization", "Content-Type")
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// Outermost, so the request log records the final status code (e.g. 409) after the exception handler has run,
// instead of seeing the raw exception and logging every handled error as a 500 with a stack trace.
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
// Turns empty 401/403/404/429 responses into ProblemDetails bodies too.
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
// Before authentication, so browser preflight (OPTIONS) requests are answered without needing a token.
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

await app.RunAsync();
