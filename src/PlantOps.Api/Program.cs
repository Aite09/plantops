using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using PlantOps.Api.Authentication;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
using Serilog.Events;

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
// (5 failures locks an account for 15 minutes) covers repeated guesses against one account; the public demo accounts
// have it switched off, because their password is published anyway.
// The readiness check queries the database, so it has one limit shared by every client: a flood of anonymous
// requests cannot turn into database load. A limit caps load, not wake-ups: a single request still resumes a paused
// serverless database, so nothing should poll /health.
const string HealthRateLimitPolicy = "health";
builder.Services.AddOptions<RateLimitingSettings>()
    .Bind(builder.Configuration.GetSection(RateLimitingSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.LoginRateLimitPolicy, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingSettings>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.LoginPermitLimit,
                Window = TimeSpan.FromSeconds(settings.LoginWindowSeconds),
            });
    });
    options.AddPolicy(HealthRateLimitPolicy, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingSettings>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            HealthRateLimitPolicy,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.HealthPermitLimit,
                Window = TimeSpan.FromSeconds(settings.HealthWindowSeconds),
            });
    });
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

// One-off maintenance commands that run instead of the web server and exit when done. Their passwords come from
// configuration at run time, never from the repo. A bare word, not --switch, so the command-line configuration
// provider ignores it.
switch (args)
{
    case ["seed-demo"]:
        // The deploy pipeline, after the migrations: the public demo accounts (no Admin) and the sample work orders.
        await app.Services.SeedDemoDataAsync();
        return;
    case ["create-admin"]:
        // The owner, once, from their own machine: the first Admin, which the API can't create by itself.
        await app.Services.CreateAdminAsync();
        return;
}

// Outermost, so the request log records the final status code (e.g. 409) after the exception handler has run,
// instead of seeing the raw exception and logging every handled error as a 500 with a stack trace.
// App Service probes /health/live every minute; those requests are logged at Verbose, below the minimum level, so
// 1,440 lines a day don't bury real traffic. Failures are still logged as errors.
app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) =>
    exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError ? LogEventLevel.Error
    : httpContext.Request.Path.StartsWithSegments("/health/live") ? LogEventLevel.Verbose
    : LogEventLevel.Information);
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

// Health endpoints are anonymous: load balancers and uptime monitors can't sign in. They return only
// "Healthy"/"Unhealthy" (200/503), never details. Liveness has no dependencies, so the platform's frequent probes
// never touch the database (which matters for Azure SQL's free tier, which pauses when idle). Point monitors at
// /health/live only. Readiness signs in to the database, so each call resumes a paused one; it is for deploy
// verification and manual checks, and is rate limited.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthCheckTags.Ready) })
    .AllowAnonymous()
    .RequireRateLimiting(HealthRateLimitPolicy);

await app.RunAsync();
