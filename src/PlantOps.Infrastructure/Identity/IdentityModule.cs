using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlantOps.Application.Abstractions.Authentication;

namespace PlantOps.Infrastructure.Identity;

public static class IdentityModule
{
    /// <summary>
    /// Self-hosted accounts: ASP.NET Core Identity stores users, this API issues JWTs, and JWT bearer validates them.
    /// Everything identity-specific is behind this call and the IAccountService/IUserDirectory ports, so a move to
    /// Microsoft Entra ID is contained; the README lists the steps, including remapping stored user ids.
    /// </summary>
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(
            DependencyInjection.GetConnectionString(configuration),
            sql => sql
                .EnableRetryOnFailure()
                .MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // The email doubles as the user name; allow apostrophes, which real addresses use (o'brien@...).
                options.User.AllowedUserNameCharacters += "'";
                options.Password.RequiredLength = 10;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // In Azure the key can be a Key Vault reference. If App Service can't resolve it, the app receives the
            // reference text itself, which is long enough to pass MinLength but publicly guessable. Refuse to start.
            .Validate(
                jwt => !jwt.SigningKey.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase),
                "Jwt:SigningKey is an unresolved Key Vault reference; check the app's access to the vault.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configure the bearer handler from the validated JwtOptions instead of reading configuration twice.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the token's own claim names ("sub", "role") instead of remapping them to long XML-style URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = JwtTokenIssuer.RoleClaimType,
                };
            });

        services.AddSingleton<JwtTokenIssuer>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }
}
