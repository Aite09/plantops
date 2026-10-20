using PlantOps.Domain.Common;

namespace PlantOps.Domain.UnitTests.Common;

public sealed class ResultTests
{
    private static readonly Error SomeError = Error.Conflict("Test.Conflict", "Something conflicted.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_carries_its_error()
    {
        var result = Result.Failure(SomeError);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void Failure_without_a_real_error_is_a_bug_and_throws()
    {
        var act = () => Result.Failure(Error.None);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("error");
    }

    [Fact]
    public void Failure_of_a_value_without_a_real_error_is_a_bug_and_throws()
    {
        var act = () => Result.Failure<int>(Error.None);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("error");
    }

    [Fact]
    public void Success_of_a_value_exposes_the_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().Be(Error.None);
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Value_of_a_failed_result_cannot_be_read()
    {
        var result = Result.Failure<int>(SomeError);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_value_converts_implicitly_to_a_successful_result()
    {
        Result<string> result = "P-101A";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("P-101A");
    }

    [Fact]
    public void An_error_converts_implicitly_to_a_failed_result_of_a_value()
    {
        Result<string> result = SomeError;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void An_error_converts_implicitly_to_a_failed_result()
    {
        Result result = SomeError;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }
}
