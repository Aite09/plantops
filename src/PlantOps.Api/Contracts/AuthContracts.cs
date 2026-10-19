namespace PlantOps.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string Role);
