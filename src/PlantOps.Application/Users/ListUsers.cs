using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Users;

/// <summary>Users, optionally only those in one role (e.g. technicians for the assign pick list).</summary>
public sealed record ListUsersQuery(string? Role = null) : IQuery<IReadOnlyList<UserDto>>;

internal sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator() =>
        RuleFor(q => q.Role)
            .Must(role => role is null || Roles.All.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
}

/// <summary>
/// Only an Admin sees Admin accounts. Supervisors need the list to assign work, and on the public demo anyone can sign
/// in as one, so it must not reveal who the Admins are.
/// </summary>
internal sealed class ListUsersQueryHandler(IUserDirectory userDirectory, ICurrentUser currentUser)
    : IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var users = await userDirectory.ListAsync(query.Role, cancellationToken);
        return currentUser.IsInRole(Roles.Admin)
            ? Result.Success(users)
            : Result.Success<IReadOnlyList<UserDto>>(users.Where(user => !user.Roles.Contains(Roles.Admin)).ToList());
    }
}
