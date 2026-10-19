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

internal sealed class ListUsersQueryHandler(IUserDirectory userDirectory) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken) =>
        Result.Success(await userDirectory.ListAsync(query.Role, cancellationToken));
}
