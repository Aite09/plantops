using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Users;

/// <summary>An admin creates an account. There is no self-registration: plant accounts are issued, not signed up for.</summary>
public sealed record CreateUserCommand(string Email, string DisplayName, string Password, string Role) : ICommand<Guid>;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(100);
        // Length and character rules are enforced by Identity's password policy, which returns its own messages.
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Role)
            .Must(role => Roles.All.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
    }
}

internal sealed class CreateUserCommandHandler(IAccountService accountService) : ICommandHandler<CreateUserCommand, Guid>
{
    public Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken cancellationToken) =>
        accountService.CreateUserAsync(command.Email, command.DisplayName, command.Password, command.Role, cancellationToken);
}
