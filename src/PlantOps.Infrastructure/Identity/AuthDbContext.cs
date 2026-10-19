using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
// Aliased because IdentityDbContext has its own "Roles" DbSet property, which would hide the constants class.
using AppRoles = PlantOps.Application.Abstractions.Authentication.Roles;

namespace PlantOps.Infrastructure.Identity;

/// <summary>
/// Identity's tables, kept in their own DbContext and "auth" schema, separate from business data. Swapping to
/// Microsoft Entra ID means deleting this module, not untangling it from work orders; and the audit interceptor on
/// PlantOpsDbContext never sees password hashes or security stamps.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<ApplicationUser>()
            .Property(u => u.DisplayName)
            .HasMaxLength(ApplicationUser.DisplayNameMaxLength)
            .IsRequired();

        // Roles are reference data, needed in every environment, so they ship inside the migration.
        // Fixed ids and concurrency stamps keep the model deterministic between migrations.
        builder.Entity<IdentityRole<Guid>>().HasData(
            Role("5d1e6c2b-7a3f-4e8d-9b1c-0000000000a1", AppRoles.Staff),
            Role("5d1e6c2b-7a3f-4e8d-9b1c-0000000000b1", AppRoles.Supervisor),
            Role("5d1e6c2b-7a3f-4e8d-9b1c-0000000000c1", AppRoles.Technician),
            Role("5d1e6c2b-7a3f-4e8d-9b1c-0000000000d1", AppRoles.Admin));
    }

    private static IdentityRole<Guid> Role(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}
