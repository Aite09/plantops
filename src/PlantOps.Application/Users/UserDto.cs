namespace PlantOps.Application.Users;

public sealed record UserDto(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);
