using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Users;
using PlantOps.Domain.Common;

namespace PlantOps.Application.UnitTests.Users;

public sealed class CreateUserCommandHandlerTests
{
    private readonly Mock<IAccountService> _accounts = new();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests() => _handler = new CreateUserCommandHandler(_accounts.Object);

    [Fact]
    public async Task Account_is_created_from_the_commands_fields_and_the_services_result_is_returned()
    {
        // Email, display name, password and role are all strings, so the compiler would not notice two of them swapped.
        var command = new CreateUserCommand("nia.supervisor@plantops.test", "Nia Supervisor", "Not-checked-here-1!", Roles.Supervisor);
        var created = Result.Success(Guid.NewGuid());
        _accounts
            .Setup(a => a.CreateUserAsync(
                "nia.supervisor@plantops.test", "Nia Supervisor", "Not-checked-here-1!", Roles.Supervisor, TestContext.Current.CancellationToken))
            .ReturnsAsync(created);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(created);
    }
}
