using PlantOps.Domain.Common;

namespace PlantOps.Domain.UnitTests.Common;

public sealed class ErrorTests
{
    public static readonly TheoryData<Func<string, string, Error>, ErrorType> Factories = new()
    {
        { Error.Validation, ErrorType.Validation },
        { Error.NotFound, ErrorType.NotFound },
        { Error.Conflict, ErrorType.Conflict },
        { Error.Forbidden, ErrorType.Forbidden },
        { Error.Unauthorized, ErrorType.Unauthorized },
        { Error.Unavailable, ErrorType.Unavailable },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void Each_factory_creates_an_error_of_its_own_type_with_the_given_code_and_description(
        Func<string, string, Error> factory, ErrorType expectedType)
    {
        var error = factory("Test.Code", "Test description.");

        error.Type.Should().Be(expectedType);
        error.Code.Should().Be("Test.Code");
        error.Description.Should().Be("Test description.");
    }

    // WorkOrderErrors.InvalidTransition builds a new Error on every call, so any code that checks for a specific
    // error (these tests included) relies on errors comparing by value rather than by reference.
    [Fact]
    public void Errors_compare_by_code_description_and_type()
    {
        var error = Error.Conflict("Test.Code", "Test description.");

        error.Should().Be(Error.Conflict("Test.Code", "Test description."));
        error.Should().NotBe(Error.Conflict("Test.Other", "Test description."));
        error.Should().NotBe(Error.Conflict("Test.Code", "Other description."));
        error.Should().NotBe(Error.Forbidden("Test.Code", "Test description."));
    }
}
