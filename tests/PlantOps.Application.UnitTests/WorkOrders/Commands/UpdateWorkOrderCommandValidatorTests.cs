using FluentValidation.TestHelper;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class UpdateWorkOrderCommandValidatorTests
{
    private const string InvalidVersionMessage = "'Version' is not a valid work order version.";

    private static readonly UpdateWorkOrderCommand Valid = new(
        Guid.NewGuid(), "Seal leak", "Dripping from the inboard seal.", WorkOrderPriority.High, Version: new byte[8]);

    private readonly UpdateWorkOrderCommandValidator _validator = new();

    [Fact]
    public void Command_with_an_8_byte_version_is_valid()
    {
        _validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(9)]
    public void Version_that_is_not_8_bytes_long_is_rejected(int length)
    {
        var result = _validator.TestValidate(Valid with { Version = new byte[length] });

        result.ShouldHaveValidationErrorFor(c => c.Version).WithErrorMessage(InvalidVersionMessage);
    }

    [Fact]
    public void Missing_version_is_reported_as_one_error_without_throwing()
    {
        // Cascade.Stop keeps the length check from running on null, where it would throw instead of reporting.
        var command = Valid with { Version = null! };

        var validate = () => _validator.TestValidate(command);

        validate.Should().NotThrow()
            .Which.ShouldHaveValidationErrorFor(c => c.Version).Should().ContainSingle()
            .Which.ErrorCode.Should().Be("NotEmptyValidator");
    }

    [Fact]
    public void Empty_version_is_reported_as_one_error()
    {
        var result = _validator.TestValidate(Valid with { Version = [] });

        result.ShouldHaveValidationErrorFor(c => c.Version).Should().ContainSingle()
            .Which.ErrorCode.Should().Be("NotEmptyValidator");
    }

    [Fact]
    public void Empty_work_order_id_is_rejected()
    {
        _validator.TestValidate(Valid with { WorkOrderId = Guid.Empty }).ShouldHaveValidationErrorFor(c => c.WorkOrderId);
    }
}
