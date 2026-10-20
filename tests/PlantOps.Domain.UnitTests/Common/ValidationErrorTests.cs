using PlantOps.Domain.Common;

namespace PlantOps.Domain.UnitTests.Common;

public sealed class ValidationErrorTests
{
    private static readonly Dictionary<string, string[]> FieldErrors = new()
    {
        ["Title"] = ["Title is required."],
        ["Priority"] = ["Priority is not a recognised value."],
    };

    [Fact]
    public void Validation_error_has_a_fixed_code_the_Validation_type_and_the_messages_per_field()
    {
        var error = new ValidationError(FieldErrors);

        error.Code.Should().Be("Validation.Failed");
        error.Type.Should().Be(ErrorType.Validation);
        error.Errors.Should().BeEquivalentTo(FieldErrors);
    }

    [Fact]
    public void Validation_error_can_fail_a_result_and_still_be_read_as_a_ValidationError()
    {
        Result<Guid> result = new ValidationError(FieldErrors);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>().Which.Errors.Should().ContainKeys("Title", "Priority");
    }
}
