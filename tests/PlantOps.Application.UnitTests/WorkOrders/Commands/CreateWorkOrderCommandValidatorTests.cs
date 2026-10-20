using FluentValidation.TestHelper;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class CreateWorkOrderCommandValidatorTests
{
    private static readonly CreateWorkOrderCommand Valid =
        new("Seal leak", "Dripping from the inboard seal.", WorkOrderPriority.High, Guid.NewGuid());

    private readonly CreateWorkOrderCommandValidator _validator = new();

    [Fact]
    public void Complete_report_is_valid()
    {
        _validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_title_is_rejected(string title)
    {
        _validator.TestValidate(Valid with { Title = title }).ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_at_the_maximum_length_is_valid()
    {
        _validator.TestValidate(Valid with { Title = new string('T', WorkOrder.TitleMaxLength) })
            .ShouldNotHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_longer_than_the_maximum_length_is_rejected()
    {
        _validator.TestValidate(Valid with { Title = new string('T', WorkOrder.TitleMaxLength + 1) })
            .ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Blank_description_is_rejected()
    {
        _validator.TestValidate(Valid with { Description = " " }).ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void Description_at_the_maximum_length_is_valid()
    {
        _validator.TestValidate(Valid with { Description = new string('D', WorkOrder.DescriptionMaxLength) })
            .ShouldNotHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void Description_longer_than_the_maximum_length_is_rejected()
    {
        _validator.TestValidate(Valid with { Description = new string('D', WorkOrder.DescriptionMaxLength + 1) })
            .ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Theory]
    [InlineData(0)] // default(WorkOrderPriority): the enum starts at Low = 1, so an omitted priority is not valid
    [InlineData(5)]
    public void Undefined_priority_is_rejected(int priority)
    {
        _validator.TestValidate(Valid with { Priority = (WorkOrderPriority)priority }).ShouldHaveValidationErrorFor(c => c.Priority);
    }

    [Fact]
    public void Empty_equipment_id_is_rejected()
    {
        _validator.TestValidate(Valid with { EquipmentId = Guid.Empty }).ShouldHaveValidationErrorFor(c => c.EquipmentId);
    }
}
