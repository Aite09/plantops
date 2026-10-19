using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Users;

namespace PlantOps.Infrastructure.Identity;

internal sealed class UserDirectory(AuthDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await db.Users
            .Where(u => userIds.Contains(u.Id))
            // Project in SQL first: ToDictionaryAsync's selectors run in memory, so without this Select the query
            // would load whole Identity rows, password hashes included, just to read a name.
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

    public Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken) =>
        (from userRole in db.UserRoles
         join r in db.Roles on userRole.RoleId equals r.Id
         where userRole.UserId == userId && r.Name == role
         select userRole).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? role, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();
        if (role is not null)
        {
            users = users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        }

        var rows = await users
            .OrderBy(u => u.DisplayName)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                Roles = (from userRole in db.UserRoles
                         join r in db.Roles on userRole.RoleId equals r.Id
                         where userRole.UserId == u.Id
                         orderby r.Name
                         select r.Name).ToList(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(u => new UserDto(u.Id, u.Email!, u.DisplayName, u.Roles.OfType<string>().ToList())).ToList();
    }
}
