using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Users;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Auth;

/// <summary>
/// A signed JWT access token plus who it was issued to, so the client can show the right menus.
/// <see cref="ExpiresInSeconds"/> (like OAuth's expires_in) lets a client time the session from its own clock;
/// comparing the absolute <see cref="ExpiresAtUtc"/> with a PC clock that runs fast would end the session early.
/// </summary>
public sealed record AccessTokenDto(string AccessToken, DateTimeOffset ExpiresAtUtc, int ExpiresInSeconds, UserDto User);

public sealed record LoginCommand(string Email, string Password) : ICommand<AccessTokenDto>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
    }
}

internal sealed class LoginCommandHandler(IAccountService accountService) : ICommandHandler<LoginCommand, AccessTokenDto>
{
    public Task<Result<AccessTokenDto>> Handle(LoginCommand command, CancellationToken cancellationToken) =>
        accountService.LoginAsync(command.Email, command.Password, cancellationToken);
}
