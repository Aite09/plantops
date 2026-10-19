using Microsoft.AspNetCore.Identity;

namespace PlantOps.Infrastructure.Identity;

/// <summary>An ASP.NET Core Identity account with Guid ids, matching the user ids stored on work orders.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 100;

    public string DisplayName { get; set; } = string.Empty;
}
