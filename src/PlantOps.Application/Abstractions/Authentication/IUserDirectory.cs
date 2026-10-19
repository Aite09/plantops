using PlantOps.Application.Users;

namespace PlantOps.Application.Abstractions.Authentication;

/// <summary>
/// Read-only lookups about users. Backed by ASP.NET Core Identity today; with Microsoft Entra ID it would be
/// backed by Microsoft Graph. Work orders store user ids only, so they never depend on where users live.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserDto>> ListAsync(string? role, CancellationToken cancellationToken);
}
