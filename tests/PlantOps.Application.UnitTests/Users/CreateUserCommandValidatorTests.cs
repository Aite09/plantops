using FluentValidation.TestHelper;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Users;

namespace PlantOps.Application.UnitTests.Users;

public sealed class CreateUserCommandValidatorTests
{
    private static readonly CreateUserCommand Valid =
        new("blair.technician@plantops.test", "Blair Technician", "Not-checked-here-1!", Roles.Technician);

    private readonly CreateUserCommandValidator _validator = new();

    [Fact]
    public void Complete_account_is_valid()
    {
        _validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Technician)]
    [InlineData(Roles.Admin)]
    public void Each_known_role_is_accepted(string role)
    {
        _validator.TestValidate(Valid with { Role = role }).ShouldNotHaveValidationErrorFor(c => c.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Manager")]
    public void Unknown_role_is_rejected_with_the_list_of_valid_roles(string role)
    {
        var result = _validator.TestValidate(Valid with { Role = role });

        result.ShouldHaveValidationErrorFor(c => c.Role)
            .WithErrorMessage("Role must be one of: Staff, Supervisor, Technician, Admin.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("blair.technician")]
    [InlineData("@plantops.test")]
    [InlineData("blair@")]
    public void Missing_or_malformed_email_is_rejected(string email)
    {
        _validator.TestValidate(Valid with { Email = email }).ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Email_of_exactly_256_characters_is_valid()
    {
        _validator.TestValidate(Valid with { Email = EmailOfLength(256) }).ShouldNotHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Email_longer_than_256_characters_is_rejected()
    {
        _validator.TestValidate(Valid with { Email = EmailOfLength(257) }).ShouldHaveValidationErrorFor(c => c.Email);
    }

    private static string EmailOfLength(int length)
    {
        const string domain = "@plantops.test";
        return new string('a', length - domain.Length) + domain;
    }
}
